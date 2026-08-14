using System.IO;
using Microsoft.Data.Sqlite;

namespace Lunaris.Infrastructure.Database;

/// <summary>Creates pooled SQLite connections pointing at the Lunaris local database.</summary>
public sealed class DatabaseFactory
{
    public string ConnectionString { get; }

    public string DatabasePath { get; }

    public DatabaseFactory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lunaris");

        Directory.CreateDirectory(dir);
        DatabasePath = Path.Combine(dir, "lunaris.db");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
        };

        ConnectionString = builder.ToString();
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        Configure(connection);
        return connection;
    }

    private static void Configure(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteScalar();
        cmd.CommandText = "PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA cache_size=-2048;";
        cmd.ExecuteNonQuery();
    }
}