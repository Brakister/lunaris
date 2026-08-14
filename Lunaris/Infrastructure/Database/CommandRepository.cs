using Lunaris.Core.Models;
using Microsoft.Data.Sqlite;

namespace Lunaris.Infrastructure.Database;

public sealed class CommandRepository
{
    private readonly DatabaseFactory _factory;

    public CommandRepository(DatabaseFactory factory) => _factory = factory;

    public List<CustomCommand> GetAll()
    {
        var list = new List<CustomCommand>();

        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Alias, Executable, Arguments, RunAsAdministrator FROM CustomCommands ORDER BY Name;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new CustomCommand
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Alias = reader.GetString(2),
                Executable = reader.GetString(3),
                Arguments = reader.GetString(4),
                RunAsAdministrator = reader.GetInt64(5) != 0,
            });
        }

        return list;
    }

    public void Save(CustomCommand command)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO CustomCommands (Id, Name, Alias, Executable, Arguments, RunAsAdministrator)
            VALUES ($id, $name, $alias, $exe, $args, $admin)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Alias = excluded.Alias,
                Executable = excluded.Executable,
                Arguments = excluded.Arguments,
                RunAsAdministrator = excluded.RunAsAdministrator;
            """;
        cmd.Parameters.AddWithValue("$id", command.Id);
        cmd.Parameters.AddWithValue("$name", command.Name);
        cmd.Parameters.AddWithValue("$alias", command.Alias);
        cmd.Parameters.AddWithValue("$exe", command.Executable);
        cmd.Parameters.AddWithValue("$args", command.Arguments);
        cmd.Parameters.AddWithValue("$admin", command.RunAsAdministrator ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = _factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM CustomCommands WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}