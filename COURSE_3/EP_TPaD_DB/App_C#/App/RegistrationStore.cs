using Microsoft.Data.Sqlite;

namespace LP_PopovD_App
{
    // База пользователей конкретной установки Windows. Таблица создаётся
    // автоматически; логин и адрес почты уникальны без учёта регистра.
    internal sealed class RegistrationStore
    {
        private readonly string path;
        internal RegistrationStore(string? databasePath = null)
        {
            path = databasePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TerritoryHockey", "users.db");
        }

        private SqliteConnection Open()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            SqliteConnection connection = new(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString());
            connection.Open();
            try
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Login TEXT NOT NULL COLLATE NOCASE UNIQUE,
                        Email TEXT NOT NULL COLLATE NOCASE UNIQUE,
                        PasswordSalt BLOB NOT NULL,
                        PasswordHash BLOB NOT NULL,
                        PasswordIterations INTEGER NOT NULL,
                        CreatedUtc TEXT NOT NULL
                    );
                    """;
                command.ExecuteNonQuery();
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }

        internal Task<bool> ExistsAsync(string login, string email) => Task.Run(() =>
        {
            using SqliteConnection connection = Open();
            using SqliteCommand query = connection.CreateCommand();
            query.CommandText = """
                SELECT EXISTS(SELECT 1 FROM Users
                    WHERE Login = $login COLLATE NOCASE OR Email = $email COLLATE NOCASE);
                """;
            query.Parameters.AddWithValue("$login", login);
            query.Parameters.AddWithValue("$email", email);
            return Convert.ToInt64(query.ExecuteScalar()) != 0;
        });

        internal Task SaveAsync(string login, string email, byte[] salt,
            byte[] hash, int iterations) => Task.Run(() =>
        {
            using SqliteConnection connection = Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Users (Login, Email, PasswordSalt, PasswordHash,
                    PasswordIterations, CreatedUtc)
                VALUES ($login, $email, $salt, $hash, $iterations, $created);
                """;
            command.Parameters.AddWithValue("$login", login);
            command.Parameters.AddWithValue("$email", email);
            command.Parameters.AddWithValue("$salt", salt);
            command.Parameters.AddWithValue("$hash", hash);
            command.Parameters.AddWithValue("$iterations", iterations);
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            try { command.ExecuteNonQuery(); }
            catch (SqliteException error) when (error.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException("Логин или адрес почты уже заняты.", error);
            }
        });
    }
}
