using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MailKit.Security;
using Microsoft.Data.Sqlite;
using MimeKit;

namespace LP_PopovD_App
{
    internal enum EmailCodePurpose { Registration, Login }

    // Данные пользователя сохраняются ЛОКАЛЬНО в SQLite, не в исходники.
    // Пароль хранится только как PBKDF2-HMAC-SHA256 с индивидуальной солью.
    internal sealed class HockeyUserDatabase
    {
        private const int Iterations = 310_000;
        private readonly string connectionString;

        internal string DatabasePath { get; }

        internal HockeyUserDatabase(string? databasePath = null)
        {
            DatabasePath = databasePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TerritoriyaHokkeya", "users.db");
            string? folder = Path.GetDirectoryName(DatabasePath);
            if (folder != null) Directory.CreateDirectory(folder);
            connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = true
            }.ToString();
        }

        private async Task<SqliteConnection> OpenAsync()
        {
            SqliteConnection connection = new(connectionString);
            try
            {
                await connection.OpenAsync();
                using SqliteCommand init = connection.CreateCommand();
                init.CommandText = """
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY,
                        Login TEXT NOT NULL COLLATE NOCASE UNIQUE,
                        Email TEXT NOT NULL COLLATE NOCASE UNIQUE,
                        PasswordSalt BLOB NOT NULL,
                        PasswordHash BLOB NOT NULL,
                        PasswordIterations INTEGER NOT NULL,
                        RegisteredUtc TEXT NOT NULL
                    );
                    """;
                await init.ExecuteNonQueryAsync();
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        internal async Task<bool> ExistsAsync(string login, string email)
        {
            await using SqliteConnection db = await OpenAsync();
            await using SqliteCommand command = db.CreateCommand();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM Users WHERE Login = $login OR Email = $email)";
            command.Parameters.AddWithValue("$login", login.Trim());
            command.Parameters.AddWithValue("$email", email.Trim());
            return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
        }

        internal static (byte[] Salt, byte[] Hash, int Iterations) HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(24);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
            return (salt, hash, Iterations);
        }

        internal async Task<string?> VerifyLoginAsync(string login, string password)
        {
            await using SqliteConnection db = await OpenAsync();
            await using SqliteCommand command = db.CreateCommand();
            command.CommandText = """
                SELECT Email, PasswordSalt, PasswordHash, PasswordIterations
                FROM Users WHERE Login = $login LIMIT 1
                """;
            command.Parameters.AddWithValue("$login", login.Trim());
            await using SqliteDataReader row = await command.ExecuteReaderAsync();
            if (!await row.ReadAsync()) return null;
            string email = row.GetString(0);
            byte[] salt = (byte[])row.GetValue(1);
            byte[] storedHash = (byte[])row.GetValue(2);
            int work = row.GetInt32(3);
            if (work < 100_000 || work > 2_000_000 || storedHash.Length != 32) return null;
            byte[] calculated = Rfc2898DeriveBytes.Pbkdf2(password, salt, work, HashAlgorithmName.SHA256, 32);
            try
            {
                return CryptographicOperations.FixedTimeEquals(calculated, storedHash) ? email : null;
            }
            finally { CryptographicOperations.ZeroMemory(calculated); }
        }

        internal async Task InsertAsync(string login, string email, byte[] salt, byte[] hash, int iterations)
        {
            await using SqliteConnection db = await OpenAsync();
            await using SqliteCommand command = db.CreateCommand();
            command.CommandText = """
                INSERT INTO Users(Login, Email, PasswordSalt, PasswordHash, PasswordIterations, RegisteredUtc)
                VALUES ($login, $email, $salt, $hash, $iterations, $time);
                """;
            command.Parameters.AddWithValue("$login", login.Trim());
            command.Parameters.AddWithValue("$email", email.Trim());
            command.Parameters.AddWithValue("$salt", salt);
            command.Parameters.AddWithValue("$hash", hash);
            command.Parameters.AddWithValue("$iterations", iterations);
            command.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            try { await command.ExecuteNonQueryAsync(); }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException("Логин или электронная почта уже зарегистрированы.", ex);
            }
        }
    }

    // Настраиваемая SMTP-доставка: без настроенного SMTP код НЕ считается отправленным.
    internal static class HockeyVerificationMail
    {
        internal static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOCKEY_SMTP_HOST")) &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOCKEY_SMTP_USER")) &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOCKEY_SMTP_PASSWORD")) &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOCKEY_SMTP_FROM"));

        internal static async Task SendAsync(string email, string code, EmailCodePurpose purpose)
        {
            if (!IsConfigured)
                throw new InvalidOperationException(
                    "Отправка почты не настроена. Укажите HOCKEY_SMTP_HOST, USER, PASSWORD и FROM.");

            string host = Environment.GetEnvironmentVariable("HOCKEY_SMTP_HOST")!.Trim();
            string user = Environment.GetEnvironmentVariable("HOCKEY_SMTP_USER")!.Trim();
            string secret = Environment.GetEnvironmentVariable("HOCKEY_SMTP_PASSWORD")!;
            string from = Environment.GetEnvironmentVariable("HOCKEY_SMTP_FROM")!.Trim();
            string portText = Environment.GetEnvironmentVariable("HOCKEY_SMTP_PORT") ?? "587";
            if (!int.TryParse(portText, out int port) || port is < 1 or > 65535)
                throw new InvalidOperationException("Некорректный HOCKEY_SMTP_PORT.");

            MimeMessage message = new();
            message.From.Add(MailboxAddress.Parse(from));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = "Территория хоккея — код подтверждения";
            message.Body = new TextPart("plain")
            {
                Text = $"Код для {(purpose == EmailCodePurpose.Registration ? "регистрации" : "входа")}: {code}\n" +
                       "Он действует 10 минут. Никому его не сообщайте.\n" +
                       "Если запрос сделали не вы, проигнорируйте письмо."
            };

            using MailKit.Net.Smtp.SmtpClient smtp = new();
            smtp.Timeout = 15_000;
            // Никакого нешифрованного SMTP или обхода проверки сертификата.
            await smtp.ConnectAsync(host, port,
                port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(user, secret);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
    }

    // Подтверждение почты не хранит OTP или пароль открытым текстом и
    // действует только в запущенном экземпляре приложения.
    internal sealed class HockeyAccountService
    {
        private sealed class PendingCode
        {
            internal required EmailCodePurpose Purpose;
            internal required string Login;
            internal required string Email;
            internal required byte[] Secret;
            internal required byte[] Digest;
            internal required DateTimeOffset ExpiresAt;
            internal byte[]? PasswordSalt;
            internal byte[]? PasswordHash;
            internal int Iterations;
            internal int Failures;

            internal void Clear()
            {
                CryptographicOperations.ZeroMemory(Secret);
                CryptographicOperations.ZeroMemory(Digest);
                if (PasswordHash != null) CryptographicOperations.ZeroMemory(PasswordHash);
                if (PasswordSalt != null) CryptographicOperations.ZeroMemory(PasswordSalt);
            }
        }

        private PendingCode? pending;
        private DateTimeOffset lastSent;
        private readonly HockeyUserDatabase database;
        private readonly Func<string, string, EmailCodePurpose, Task> deliver;

        internal HockeyAccountService(HockeyUserDatabase? database = null,
            Func<string, string, EmailCodePurpose, Task>? deliver = null)
        {
            this.database = database ?? new HockeyUserDatabase();
            this.deliver = deliver ?? HockeyVerificationMail.SendAsync;
        }

        internal bool RegistrationCodeRequested => pending?.Purpose == EmailCodePurpose.Registration;
        internal bool LoginCodeRequested => pending?.Purpose == EmailCodePurpose.Login;

        internal void Reset()
        {
            pending?.Clear();
            pending = null;
        }

        private void CheckCooldown()
        {
            if (DateTimeOffset.UtcNow - lastSent < TimeSpan.FromSeconds(60))
                throw new InvalidOperationException("Повторный код можно запросить через 60 секунд.");
        }

        private async Task SendCodeAsync(EmailCodePurpose purpose, string login, string email,
            (byte[] Salt, byte[] Hash, int Iterations)? credential = null)
        {
            CheckCooldown();
            string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            byte[] secret = RandomNumberGenerator.GetBytes(32);
            byte[] digest;
            using (HMACSHA256 hmac = new(secret))
                digest = hmac.ComputeHash(Encoding.ASCII.GetBytes(code));

            PendingCode candidate = new()
            {
                Purpose = purpose,
                Login = login.Trim(),
                Email = email.Trim(),
                Secret = secret,
                Digest = digest,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                PasswordSalt = credential?.Salt,
                PasswordHash = credential?.Hash,
                Iterations = credential?.Iterations ?? 0
            };

            try { await deliver(email, code, purpose); }
            catch { candidate.Clear(); throw; }

            Reset();
            pending = candidate;
            lastSent = DateTimeOffset.UtcNow;
        }

        internal async Task RequestRegistrationAsync(string login, string password, string email)
        {
            if (await database.ExistsAsync(login, email))
                throw new InvalidOperationException("Логин или электронная почта уже зарегистрированы.");
            // PBKDF2 намеренно выполняется вне UI-потока.
            var credential = await Task.Run(() => HockeyUserDatabase.HashPassword(password));
            await SendCodeAsync(EmailCodePurpose.Registration, login, email, credential);
        }

        internal async Task RequestLoginAsync(string login, string password)
        {
            string? email = await database.VerifyLoginAsync(login, password);
            if (email == null)
                throw new InvalidOperationException("Неверный логин или пароль.");
            await SendCodeAsync(EmailCodePurpose.Login, login, email);
        }

        private PendingCode Confirm(string code, EmailCodePurpose purpose)
        {
            PendingCode? entry = pending;
            if (entry == null || entry.Purpose != purpose)
                throw new InvalidOperationException("Сначала запросите код подтверждения.");
            if (DateTimeOffset.UtcNow > entry.ExpiresAt)
            {
                Reset();
                throw new InvalidOperationException("Срок действия кода истёк. Запросите новый.");
            }
            if (entry.Failures >= 5)
            {
                Reset();
                throw new InvalidOperationException("Превышено число попыток. Запросите новый код.");
            }
            bool matches = false;
            if (code.Length == 6 && code.All(char.IsAsciiDigit))
            {
                using HMACSHA256 hmac = new(entry.Secret);
                byte[] actual = hmac.ComputeHash(Encoding.ASCII.GetBytes(code));
                matches = CryptographicOperations.FixedTimeEquals(actual, entry.Digest);
                CryptographicOperations.ZeroMemory(actual);
            }
            if (!matches)
            {
                entry.Failures++;
                if (entry.Failures >= 5) Reset();
                throw new InvalidOperationException("Неверный код подтверждения.");
            }
            return entry;
        }

        internal async Task CompleteRegistrationAsync(string code)
        {
            PendingCode entry = Confirm(code, EmailCodePurpose.Registration);
            await database.InsertAsync(entry.Login, entry.Email,
                entry.PasswordSalt!, entry.PasswordHash!, entry.Iterations);
            Reset();
            // После успешной регистрации разрешаем немедленно запросить
            // ОТДЕЛЬНЫЙ код для первого входа в созданную учётную запись.
            lastSent = DateTimeOffset.MinValue;
        }

        internal void CompleteLogin(string code)
        {
            _ = Confirm(code, EmailCodePurpose.Login);
            Reset();
        }
    }
}
