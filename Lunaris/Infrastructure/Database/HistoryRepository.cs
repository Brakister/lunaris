using Lunaris.Core.Models;
using Microsoft.Data.Sqlite;

namespace Lunaris.Infrastructure.Database;

public sealed class HistoryRepository
{
    private readonly DatabaseFactory _factory;

    public HistoryRepository(DatabaseFactory factory) => _factory = factory;

    public void Record(string query, SearchResult result)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SearchHistory (Query, ResultId, ResultName, ExecutedAt, ExecutionCount, ExecuteHint)
            VALUES ($q, $rid, $name, $at, 1, $hint)
            ON CONFLICT(Query, ResultId) DO UPDATE SET
                ExecutionCount = ExecutionCount + 1,
                ExecutedAt = excluded.ExecutedAt,
                ExecuteHint = excluded.ExecuteHint;
            """;
        cmd.Parameters.AddWithValue("$q", query);
        cmd.Parameters.AddWithValue("$rid", result.Id);
        cmd.Parameters.AddWithValue("$name", result.Title);
        cmd.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$hint", (object?)result.ExecuteHint ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public UsageStats? GetStats(string resultId)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT ResultId, SUM(ExecutionCount) AS Count, MAX(ExecutedAt)
            FROM SearchHistory
            WHERE ResultId = $rid
            GROUP BY ResultId;
            """;
        cmd.Parameters.AddWithValue("$rid", resultId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new UsageStats
        {
            ResultId = reader.GetString(0),
            ExecutionCount = Convert.ToInt32(reader.GetInt64(1)),
            LastExecuted = DateTime.TryParse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToLocalTime()
                : DateTime.MinValue,
        };
    }

    /// <summary>Most recently executed (distinct) results.</summary>
    public List<(SearchResult Result, DateTime ExecutedAt)> GetRecent(int limit)
    {
        var list = new List<(SearchResult, DateTime)>();

        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Query, ResultId, ResultName, MAX(ExecutedAt) AS LastAt, ExecuteHint
            FROM SearchHistory
            GROUP BY ResultId
            ORDER BY LastAt DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(2);
            var executedAt = DateTime.TryParse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToLocalTime()
                : DateTime.MinValue;

            list.Add((new SearchResult
            {
                Id = reader.GetString(1),
                Title = name,
                Category = "Histórico",
                Kind = SearchResultKind.History,
                Icon = Lunaris.Core.Utilities.GlyphCatalog.History,
                Subtitle = $"Executado em {executedAt:dd/MM HH:mm}",
                ExecuteHint = reader.IsDBNull(4) ? null : reader.GetString(4),
            }, executedAt));
        }

        return list;
    }

    public IReadOnlyList<SearchResult> Search(string query, int limit)
    {
        var results = new List<SearchResult>();

        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT ResultId, ResultName, SUM(ExecutionCount), MAX(ExecutedAt), ExecuteHint
            FROM SearchHistory
            WHERE ResultName LIKE $pattern ESCAPE '\'
            GROUP BY ResultId
            ORDER BY SUM(ExecutionCount) DESC
            LIMIT $limit;
            """;
        var escaped = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        cmd.Parameters.AddWithValue("$pattern", "%" + escaped + "%");
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SearchResult
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Category = "Histórico",
                Kind = SearchResultKind.History,
                Icon = Lunaris.Core.Utilities.GlyphCatalog.History,
                Subtitle = $"Executado {reader.GetInt64(2)}x",
                ExecuteHint = reader.IsDBNull(4) ? null : reader.GetString(4),
            });
        }

        return results;
    }

    public void Clear()
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM SearchHistory;";
        cmd.ExecuteNonQuery();
    }
}