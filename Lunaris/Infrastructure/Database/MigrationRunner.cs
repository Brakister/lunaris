using System.IO;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Infrastructure.Database;

/// <summary>
/// Applies versioned schema migrations in order. Never spreads raw schema SQL
/// through the business layer.
/// </summary>
public sealed class MigrationRunner
{
    private static readonly (int Version, string Sql)[] Migrations =
    {
        (1, """
            CREATE TABLE IF NOT EXISTS Applications (
                Id          TEXT PRIMARY KEY,
                Name        TEXT NOT NULL,
                Path        TEXT NOT NULL,
                Category    TEXT NOT NULL DEFAULT '',
                Arguments   TEXT NOT NULL DEFAULT '',
                Icon        TEXT NOT NULL DEFAULT '',
                UpdatedAt   TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS IndexedFiles (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Path        TEXT NOT NULL UNIQUE,
                Name        TEXT NOT NULL,
                Directory   TEXT NOT NULL,
                Size        INTEGER NOT NULL DEFAULT 0,
                ModifiedAt  TEXT NOT NULL,
                IsFolder    INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS IX_IndexedFiles_Name ON IndexedFiles(Name);
            CREATE INDEX IF NOT EXISTS IX_IndexedFiles_Directory ON IndexedFiles(Directory);

            CREATE TABLE IF NOT EXISTS SearchHistory (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                Query           TEXT NOT NULL,
                ResultId        TEXT NOT NULL,
                ResultName      TEXT NOT NULL,
                ExecutedAt      TEXT NOT NULL,
                ExecutionCount  INTEGER NOT NULL DEFAULT 1
            );

            CREATE UNIQUE INDEX IF NOT EXISTS UX_SearchHistory_Query_ResultId ON SearchHistory(Query, ResultId);
            CREATE INDEX IF NOT EXISTS IX_SearchHistory_ResultId ON SearchHistory(ResultId);
            CREATE INDEX IF NOT EXISTS IX_SearchHistory_ExecutedAt ON SearchHistory(ExecutedAt);

            CREATE TABLE IF NOT EXISTS Favorites (
                Id                      TEXT PRIMARY KEY,
                Title                   TEXT NOT NULL,
                Subtitle                TEXT NOT NULL DEFAULT '',
                Icon                    TEXT NOT NULL DEFAULT '',
                Category                TEXT NOT NULL DEFAULT '',
                Kind                    TEXT NOT NULL DEFAULT '',
                ExecuteHint             TEXT,
                ExecuteArguments        TEXT,
                CanRunAsAdministrator   INTEGER NOT NULL DEFAULT 0,
                CreatedAt               TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CustomCommands (
                Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                Name                TEXT NOT NULL,
                Alias               TEXT NOT NULL,
                Executable          TEXT NOT NULL,
                Arguments           TEXT NOT NULL DEFAULT '',
                RunAsAdministrator  INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS IX_CustomCommands_Alias ON CustomCommands(Alias);

            CREATE TABLE IF NOT EXISTS Settings (
                Key     TEXT PRIMARY KEY,
                Value   TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ClipboardHistory (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Content     TEXT NOT NULL,
                CopiedAt    TEXT NOT NULL,
                Size        INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS IX_ClipboardHistory_CopiedAt ON ClipboardHistory(CopiedAt);
            """),
        (2, """
            ALTER TABLE SearchHistory ADD COLUMN ExecuteHint TEXT;
            """),
        (3, """
            ALTER TABLE Applications ADD COLUMN SearchText TEXT NOT NULL DEFAULT '';
            """),
    };

    private readonly DatabaseFactory _factory;

    public MigrationRunner(DatabaseFactory factory) => _factory = factory;

    public void Apply()
    {
        using var connection = _factory.CreateConnection();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS SchemaMigrations (
                    Version     INTEGER PRIMARY KEY,
                    AppliedAt   TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        foreach (var (version, sql) in Migrations)
        {
            if (GetAppliedVersion(connection, version))
                continue;

            Log.Info("Applying database migration v{Version}", version);

            using var transaction = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
            cmd.CommandText = "INSERT INTO SchemaMigrations (Version, AppliedAt) VALUES ($v, $t);";
            cmd.Parameters.AddWithValue("$v", version);
            cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    private static bool GetAppliedVersion(Microsoft.Data.Sqlite.SqliteConnection connection, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM SchemaMigrations WHERE Version = $v;";
        cmd.Parameters.AddWithValue("$v", version);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }
}