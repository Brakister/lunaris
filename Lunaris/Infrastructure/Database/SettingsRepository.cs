using Microsoft.Data.Sqlite;

namespace Lunaris.Infrastructure.Database;

/// <summary>Key/value store for the settings table.</summary>
public sealed class SettingsRepository
{
    private readonly DatabaseFactory _factory;

    public SettingsRepository(DatabaseFactory factory) => _factory = factory;

    public string? Get(string key)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $k;";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    public void Set(string key, string value)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ($k, $v)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }
}