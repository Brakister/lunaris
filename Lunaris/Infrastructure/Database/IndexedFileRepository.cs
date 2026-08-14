using Lunaris.Core.Models;
using Microsoft.Data.Sqlite;

namespace Lunaris.Infrastructure.Database;

public sealed class IndexedFileRepository
{
    private readonly DatabaseFactory _factory;

    public IndexedFileRepository(DatabaseFactory factory) => _factory = factory;

    public void UpsertBatch(IReadOnlyList<(string Path, string Name, string Directory, long Size, DateTime Modified, bool IsFolder)> entries)
    {
        if (entries.Count == 0)
            return;

        using var connection = _factory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO IndexedFiles (Path, Name, Directory, Size, ModifiedAt, IsFolder)
                VALUES ($p, $n, $d, $s, $m, $f)
                ON CONFLICT(Path) DO UPDATE SET
                    Name = excluded.Name,
                    Directory = excluded.Directory,
                    Size = excluded.Size,
                    ModifiedAt = excluded.ModifiedAt,
                    IsFolder = excluded.IsFolder;
                """;

            var p = cmd.Parameters.Add("$p", SqliteType.Text);
            var n = cmd.Parameters.Add("$n", SqliteType.Text);
            var d = cmd.Parameters.Add("$d", SqliteType.Text);
            var s = cmd.Parameters.Add("$s", SqliteType.Integer);
            var m = cmd.Parameters.Add("$m", SqliteType.Text);
            var f = cmd.Parameters.Add("$f", SqliteType.Integer);

            foreach (var entry in entries)
            {
                p.Value = entry.Path;
                n.Value = entry.Name;
                d.Value = entry.Directory;
                s.Value = entry.Size;
                m.Value = entry.Modified.ToString("O");
                f.Value = entry.IsFolder ? 1 : 0;
                cmd.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public void PruneOutside(IReadOnlyList<string> rootDirectories)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Path FROM IndexedFiles;";

        var stale = new List<string>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var path = reader.GetString(0);
                var isInside = false;
                foreach (var root in rootDirectories)
                {
                    if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        isInside = true;
                        break;
                    }
                }
                if (!isInside)
                    stale.Add(path);
            }
        }

        if (stale.Count == 0)
            return;

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM IndexedFiles WHERE Path = $p;";
        var p = deleteCmd.Parameters.Add("$p", SqliteType.Text);
        foreach (var path in stale)
        {
            p.Value = path;
            deleteCmd.ExecuteNonQuery();
        }
    }

    /// <summary>Name-based lookup with optional extension filter; limited to avoid scanning everything.</summary>
    public IReadOnlyList<SearchResult> Search(string namePattern, string? extension, string? directory, int limit)
    {
        var results = new List<SearchResult>();

        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        var sql = new System.Text.StringBuilder("SELECT Path, Name, Directory, IsFolder FROM IndexedFiles WHERE Name LIKE $p");
        if (!string.IsNullOrEmpty(extension))
            sql.Append(" AND LOWER(Name) LIKE $ext");
        if (!string.IsNullOrEmpty(directory))
            sql.Append(" AND Directory = $dir");
        sql.Append(" LIMIT $limit;");

        cmd.CommandText = sql.ToString();
        cmd.Parameters.AddWithValue("$p", "%" + namePattern + "%");
        if (!string.IsNullOrEmpty(extension))
            cmd.Parameters.AddWithValue("$ext", "%." + extension.TrimStart('.'));
        if (!string.IsNullOrEmpty(directory))
            cmd.Parameters.AddWithValue("$dir", directory);
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var path = reader.GetString(0);
            var name = reader.GetString(1);
            var dir = reader.GetString(2);
            var isFolder = reader.GetInt64(3) != 0;

            results.Add(new SearchResult
            {
                Id = "file:" + path.ToLowerInvariant(),
                Title = name,
                Subtitle = dir,
                Icon = isFolder ? Lunaris.Core.Utilities.GlyphCatalog.FolderOpen : Lunaris.Core.Utilities.GlyphCatalog.File,
                Category = isFolder ? "Pasta" : "Arquivo",
                Kind = isFolder ? SearchResultKind.Folder : SearchResultKind.File,
                ExecuteHint = path,
            });
        }

        return results;
    }

    public long Count() => CountOf("SELECT COUNT(*) FROM IndexedFiles;");

    private long CountOf(string sql)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}