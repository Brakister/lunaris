using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Services;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Search.CommandProvider;

/// <summary>Searches user-defined custom commands and aliases stored in SQLite.</summary>
public sealed class CommandProvider : ISearchProvider
{
    private readonly CommandRepository _repository;
    private readonly IActionRunner _runner;
    private volatile IReadOnlyList<CustomCommand> _commands = Array.Empty<CustomCommand>();

    public string Id => "commands";

    public string Name => "Comandos";

    public CommandProvider(CommandRepository repository, IActionRunner runner)
    {
        _repository = repository;
        _runner = runner;
    }

    public void Reload() => _commands = _repository.GetAll();

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var results = new List<SearchResult>();

        foreach (var command in _commands)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var match = FuzzyMatcher.Score(query, command.Name);
            if (match <= 0.12)
                match = FuzzyMatcher.Score(query, command.Alias);

            if (match <= 0.12)
                continue;

            var result = new SearchResult
            {
                Id = "cmd:" + command.Id,
                Title = command.Name,
                Subtitle = string.IsNullOrEmpty(command.Arguments)
                    ? command.Executable
                    : $"{command.Executable} {command.Arguments}",
                SearchText = string.Join(' ', new[]
                {
                    command.Name,
                    command.Alias,
                    command.Executable,
                    command.Arguments,
                }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Icon = Lunaris.Core.Utilities.GlyphCatalog.Command,
                Category = "Comando",
                Kind = SearchResultKind.Command,
                Score = match,
                ExecuteHint = command.Executable,
                ExecuteArguments = command.Arguments,
                CanRunAsAdministrator = command.RunAsAdministrator,
                ProviderId = Id,
            };
            result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);
            results.Add(result);
        }

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }
}
