using Microsoft.Data.Sqlite;

namespace Lunaris.Infrastructure.Database;

/// <summary>Snapshot of the discovered applications, used for fast startup reload.</summary>
public sealed class AppIndexRepository
{
    private readonly DatabaseFactory _factory;

    public AppIndexRepository(DatabaseFactory factory) => _factory = factory;

    public List<IndexedApplication> GetAll()
    {
        var list = new List<IndexedApplication>();

        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Path, Category, Arguments, Icon, SearchText FROM Applications;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new IndexedApplication
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Path = reader.GetString(2),
                Category = reader.GetString(3),
                Arguments = reader.GetString(4),
                Icon = reader.GetString(5),
                SearchText = reader.GetString(6),
            });
        }

        return list;
    }

    public void ReplaceAll(IReadOnlyList<IndexedApplication> apps)
    {
        using var connection = _factory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM Applications;";
            cmd.ExecuteNonQuery();
        }

        if (apps.Count > 0)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO Applications (Id, Name, Path, Category, Arguments, Icon, SearchText, UpdatedAt)
                VALUES ($id, $name, $path, $cat, $args, $icon, $searchText, $at);
                """;
            var id = cmd.Parameters.Add("$id", SqliteType.Text);
            var name = cmd.Parameters.Add("$name", SqliteType.Text);
            var path = cmd.Parameters.Add("$path", SqliteType.Text);
            var cat = cmd.Parameters.Add("$cat", SqliteType.Text);
            var args = cmd.Parameters.Add("$args", SqliteType.Text);
            var icon = cmd.Parameters.Add("$icon", SqliteType.Text);
            var searchText = cmd.Parameters.Add("$searchText", SqliteType.Text);
            var at = cmd.Parameters.Add("$at", SqliteType.Text);

            foreach (var app in apps)
            {
                id.Value = app.Id;
                name.Value = app.Name;
                path.Value = app.Path;
                cat.Value = app.Category;
                args.Value = app.Arguments;
                icon.Value = app.Icon;
                searchText.Value = app.SearchText;
                at.Value = DateTime.UtcNow.ToString("O");
                cmd.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }
}

public sealed class IndexedApplication
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Path { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    /// <summary>Extra terms (executable name, aliases) used only for fuzzy matching.</summary>
    public string SearchText { get; set; } = string.Empty;
}