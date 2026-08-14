using Lunaris.Core.Models;
using Lunaris.Core.Utilities;
using Microsoft.Data.Sqlite;

namespace Lunaris.Infrastructure.Database;

public sealed class FavoriteRepository
{
    private readonly DatabaseFactory _factory;

    public FavoriteRepository(DatabaseFactory factory) => _factory = factory;

    public List<SearchResult> GetAll()
    {
        var list = new List<SearchResult>();

        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Title, Subtitle, Icon, Category, Kind, ExecuteHint, ExecuteArguments, CanRunAsAdministrator
            FROM Favorites
            ORDER BY CreatedAt DESC;
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var kind = Enum.TryParse<SearchResultKind>(reader.GetString(5), out var k) ? k : SearchResultKind.App;
            var result = new SearchResult
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Subtitle = reader.GetString(2),
                Icon = string.IsNullOrEmpty(reader.GetString(3)) ? GlyphCatalog.ForKind(kind) : reader.GetString(3),
                Category = reader.GetString(4),
                Kind = kind,
                ExecuteHint = reader.IsDBNull(6) ? null : reader.GetString(6),
                ExecuteArguments = reader.IsDBNull(7) ? null : reader.GetString(7),
                CanRunAsAdministrator = reader.GetInt64(8) != 0,
                IsFavorite = true,
            };
            list.Add(result);
        }

        return list;
    }

    public bool Contains(string resultId)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Favorites WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", resultId);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public void Add(SearchResult result)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Favorites (Id, Title, Subtitle, Icon, Category, Kind, ExecuteHint, ExecuteArguments, CanRunAsAdministrator, CreatedAt)
            VALUES ($id, $title, $subtitle, $icon, $category, $kind, $hint, $args, $admin, $at);
            """;
        cmd.Parameters.AddWithValue("$id", result.Id);
        cmd.Parameters.AddWithValue("$title", result.Title);
        cmd.Parameters.AddWithValue("$subtitle", result.Subtitle);
        cmd.Parameters.AddWithValue("$icon", result.Icon);
        cmd.Parameters.AddWithValue("$category", result.Category);
        cmd.Parameters.AddWithValue("$kind", result.Kind.ToString());
        cmd.Parameters.AddWithValue("$hint", (object?)result.ExecuteHint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$args", (object?)result.ExecuteArguments ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$admin", result.CanRunAsAdministrator ? 1 : 0);
        cmd.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void Remove(string resultId)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Favorites WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", resultId);
        cmd.ExecuteNonQuery();
    }
}