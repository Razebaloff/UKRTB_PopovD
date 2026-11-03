using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace LP_PopovD_App
{
    // В БД попадают только пользователи с подтверждённой почтой.
    // Код не сохраняется на диск, пароль не хранится в открытом виде.
    internal sealed class RegistrationFlow
    {
        internal const int PasswordIterations = 600_000;
        private const int MaxAttempts = 5;
        private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(60);

        private sealed class Pending
        {
            internal required string Login, Email;
            internal required byte[] PasswordSalt, PasswordHash, CodeSalt, CodeHash;
            internal required DateTimeOffset ExpiresAt;
            internal int Attempts;
        }

        private readonly RegistrationStore store;
        private readonly SemaphoreSlim operation = new(1, 1);
        private Pending? pending;
        private DateTimeOffset lastSent = DateTimeOffset.MinValue;

        internal bool CodeRequested => pending != null;
        internal RegistrationFlow(RegistrationStore? store = null)
            => this.store = store ?? new RegistrationStore();

        internal void Reset() => pending = null;

        private static byte[] HashCode(string code, byte[] salt)
        {
            byte[] digits = Encoding.UTF8.GetBytes(code);
            byte[] input = new byte[salt.Length + digits.Length];
            salt.CopyTo(input, 0);
            digits.CopyTo(input, salt.Length);
            return SHA256.HashData(input);
        }

        private static byte[] DerivePassword(string password, byte[] salt)
            => Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations,
                HashAlgorithmName.SHA256, 32);

        internal async Task RequestCodeAsync(string login, string password, string email)
        {
            await operation.WaitAsync();
            try
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (now - lastSent < SendInterval)
                    throw new InvalidOperationException(
                        "Повторная отправка возможна через 60 секунд.");
                login = login.Trim();
                email = email.Trim();
                if (login.Length is < 3 or > 64)
                    throw new InvalidOperationException("Логин должен содержать от 3 до 64 символов.");
                if (password.Length is < 8 or > 128)
                    throw new InvalidOperationException("Пароль должен содержать от 8 до 128 символов.");
                if (email.Length > 254 || !MailAddress.TryCreate(email, out MailAddress? address) ||
                    address.Address != email)
                    throw new InvalidOperationException("Проверьте адрес электронной почты.");
                if (await store.ExistsAsync(login, email))
                    throw new InvalidOperationException("Логин или адрес почты уже заняты.");

                // Проверка настроек ДО создания кода и дорогого PBKDF2.
                SmtpRegistrationMailer mailer = SmtpRegistrationMailer.FromEnvironment();
                string code = RandomNumberGenerator.GetInt32(0, 1_000_000)
                    .ToString("D6", CultureInfo.InvariantCulture);
                byte[] codeSalt = RandomNumberGenerator.GetBytes(16);
                byte[] passwordSalt = RandomNumberGenerator.GetBytes(16);
                byte[] hash = await Task.Run(() => DerivePassword(password, passwordSalt));
                await mailer.SendAsync(email, code);

                pending = new Pending
                {
                    Login = login,
                    Email = email,
                    PasswordSalt = passwordSalt,
                    PasswordHash = hash,
                    CodeSalt = codeSalt,
                    CodeHash = HashCode(code, codeSalt),
                    ExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime)
                };
                lastSent = DateTimeOffset.UtcNow;
            }
            finally { operation.Release(); }
        }

        internal async Task ConfirmAsync(string login, string password,
            string email, string code)
        {
            await operation.WaitAsync();
            try
            {
                Pending p = pending ?? throw new InvalidOperationException(
                    "Сначала запросите код по электронной почте.");
                if (DateTimeOffset.UtcNow >= p.ExpiresAt)
                {
                    pending = null;
                    throw new InvalidOperationException("Срок действия кода истёк. Запросите новый.");
                }
                if (!string.Equals(login.Trim(), p.Login, StringComparison.Ordinal) ||
                    !string.Equals(email.Trim(), p.Email, StringComparison.OrdinalIgnoreCase))
                {
                    pending = null;
                    throw new InvalidOperationException("Данные изменены. Запросите новый код.");
                }

                // Пароль привязан к тому же запросу, которым отправлен код.
                byte[] passwordHash = await Task.Run(() => DerivePassword(password, p.PasswordSalt));
                if (!CryptographicOperations.FixedTimeEquals(passwordHash, p.PasswordHash))
                {
                    pending = null;
                    throw new InvalidOperationException("Пароль изменён. Запросите новый код.");
                }

                if (code.Length != 6 || !code.All(char.IsAsciiDigit) ||
                    !CryptographicOperations.FixedTimeEquals(
                        HashCode(code, p.CodeSalt), p.CodeHash))
                {
                    if (++p.Attempts >= MaxAttempts)
                    {
                        pending = null;
                        throw new InvalidOperationException(
                            "Превышено число попыток. Запросите новый код.");
                    }
                    throw new InvalidOperationException(
                        $"Неверный код. Осталось попыток: {MaxAttempts - p.Attempts}.");
                }

                // Только после проверки всех условий появляется запись в БД.
                await store.SaveAsync(p.Login, p.Email, p.PasswordSalt,
                    p.PasswordHash, PasswordIterations);
                pending = null;
            }
            finally { operation.Release(); }
        }
    }

    // SMTP-конфигурация задаётся на компьютере пользователя, не в Git.
    // STARTTLS требуется всегда, пароль от почты не попадает в логи.
    internal sealed class SmtpRegistrationMailer
    {
        private readonly string host, user, password, from;
        private readonly int port;

        private SmtpRegistrationMailer(string host, int port, string user,
            string password, string from)
        {
            this.host = host;
            this.port = port;
            this.user = user;
            this.password = password;
            this.from = from;
        }

        internal static SmtpRegistrationMailer FromEnvironment()
        {
            string host = Environment.GetEnvironmentVariable("HOCKEY_SMTP_HOST") ?? "";
            string user = Environment.GetEnvironmentVariable("HOCKEY_SMTP_USER") ?? "";
            string password = Environment.GetEnvironmentVariable("HOCKEY_SMTP_PASSWORD") ?? "";
            string from = Environment.GetEnvironmentVariable("HOCKEY_SMTP_FROM") ?? "";
            string rawPort = Environment.GetEnvironmentVariable("HOCKEY_SMTP_PORT") ?? "587";
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password) || !MailAddress.TryCreate(from, out _) ||
                !int.TryParse(rawPort, out int port) || port is <= 0 or > 65535)
                throw new InvalidOperationException(
                    "Для отправки кода настройте HOCKEY_SMTP_HOST, USER, PASSWORD, FROM и PORT.");

            return new SmtpRegistrationMailer(host, port, user, password, from);
        }

        internal async Task SendAsync(string recipient, string code)
        {
            using SmtpClient client = new(host, port)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(user, password),
                Timeout = 15_000
            };
            using MailMessage message = new(from, recipient)
            {
                Subject = "Территория хоккея — подтверждение регистрации",
                Body = $"Код подтверждения: {code}\nДействует 10 минут. " +
                    "Если вы не регистрировались, проигнорируйте письмо.",
                IsBodyHtml = false
            };
            try { await client.SendMailAsync(message); }
            catch (SmtpException)
            {
                throw new InvalidOperationException(
                    "Не удалось отправить письмо. Проверьте настройки SMTP и соединение.");
            }
        }
    }
}
