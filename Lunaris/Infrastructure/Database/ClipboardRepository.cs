using Lunaris.Core.Models;
using Microsoft.Data.Sqlite;

namespace Lunaris.Infrastructure.Database;

public sealed class ClipboardRepository
{
    private readonly DatabaseFactory _factory;

    public ClipboardRepository(DatabaseFactory factory) => _factory = factory;

    public void Insert(string content)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO ClipboardHistory (Content, CopiedAt, Size) VALUES ($c, $at, $size);";
        cmd.Parameters.AddWithValue("$c", content);
        cmd.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$size", System.Text.Encoding.UTF8.GetByteCount(content));
        cmd.ExecuteNonQuery();
    }

    public List<string> GetRecent(int limit)
    {
        var list = new List<string>();

        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Content FROM ClipboardHistory ORDER BY Id DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(reader.GetString(0));

        return list;
    }

    public IReadOnlyList<SearchResult> Search(string query, int limit)
    {
        var results = new List<SearchResult>();

        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Content FROM ClipboardHistory WHERE Content LIKE $p ESCAPE '\\' ORDER BY Id DESC LIMIT $limit;";
        var escaped = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        cmd.Parameters.AddWithValue("$p", "%" + escaped + "%");
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var content = reader.GetString(0);
            results.Add(new SearchResult
            {
                Id = "clip:" + content,
                Title = content.Length > 120 ? content[..120] + "…" : content,
                Subtitle = "Clipboard",
                Category = "Clipboard",
                Kind = SearchResultKind.ClipboardItem,
                Icon = Lunaris.Core.Utilities.GlyphCatalog.Clipboard,
                ExecuteHint = content,
            });
        }

        return results;
    }

    public void Clear()
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ClipboardHistory;";
        cmd.ExecuteNonQuery();
    }
}