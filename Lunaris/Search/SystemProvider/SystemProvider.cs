using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Services;
using Lunaris.Core.Utilities;

namespace Lunaris.Search.SystemProvider;

/// <summary>Windows settings pages (ms-settings:) and built-in system tools.</summary>
public sealed class SystemProvider : ISearchProvider
{
    private readonly IActionRunner _runner;
    private readonly ISettingsService _settingsService;

    public string Id => "system";

    public string Name => "Sistema";

    private static readonly SystemEntry[] Entries =
    {
        // --- Windows settings (ms-settings: URIs) ---
        new("Wi-Fi", "wifi", "Configurações", GlyphCatalog.Setting, "ms-settings:network-wifi", SearchResultKind.Setting, "wireless", "rede sem fio"),
        new("Bluetooth", "bluetooth", "Configurações", GlyphCatalog.Setting, "ms-settings:bluetooth", SearchResultKind.Setting),
        new("Display", "display", "Configurações", GlyphCatalog.Setting, "ms-settings:display", SearchResultKind.Setting, "tela", "monitor", "video", "resolução"),
        new("Som", "sound", "Configurações", GlyphCatalog.Setting, "ms-settings:sound", SearchResultKind.Setting, "áudio", "volume", "audio"),
        new("Rede", "network", "Configurações", GlyphCatalog.Setting, "ms-settings:network", SearchResultKind.Setting, "rede", "internet"),
        new("Notificações", "notifications", "Configurações", GlyphCatalog.Setting, "ms-settings:notifications", SearchResultKind.Setting, "notificacoes"),
        new("Energia", "power", "Configurações", GlyphCatalog.Setting, "ms-settings:power", SearchResultKind.Setting, "bateria", "energia", "sleep"),
        new("Aparência", "appearance", "Configurações", GlyphCatalog.Setting, "ms-settings:personalization", SearchResultKind.Setting, "aparência", "personalização", "personalizacao", "cores"),
        new("Temas", "themes", "Configurações", GlyphCatalog.Setting, "ms-settings:themes", SearchResultKind.Setting, "tema"),
        new("Fundo", "background", "Configurações", GlyphCatalog.Setting, "ms-settings:personalization-background", SearchResultKind.Setting, "wallpaper", "papel de parede"),
        new("Taskbar", "taskbar", "Configurações", GlyphCatalog.Setting, "ms-settings:taskbar", SearchResultKind.Setting, "barra de tarefas"),
        new("Armazenamento", "storage", "Configurações", GlyphCatalog.Setting, "ms-settings:storagesense", SearchResultKind.Setting, "armazenamento", "disco"),
        new("Windows Update", "update", "Configurações", GlyphCatalog.Setting, "ms-settings:windowsupdate", SearchResultKind.Setting, "atualização", "atualizacao", "windows update"),
        new("Privacidade", "privacy", "Configurações", GlyphCatalog.Setting, "ms-settings:privacy", SearchResultKind.Setting, "privacidade"),
        new("Contas", "accounts", "Configurações", GlyphCatalog.Setting, "ms-settings:accounts", SearchResultKind.Setting, "contas", "usuários", "usuarios"),
        new("Acessibilidade", "accessibility", "Configurações", GlyphCatalog.Setting, "ms-settings:easeofaccess", SearchResultKind.Setting, "facilidade de acesso"),
        new("Data e Hora", "date time", "Configurações", GlyphCatalog.Setting, "ms-settings:dateandtime", SearchResultKind.Setting, "data", "hora"),
        new("Sobre", "about", "Configurações", GlyphCatalog.Setting, "ms-settings:about", SearchResultKind.Setting, "sobre", "specs"),
        new("Apps instalados", "apps", "Configurações", GlyphCatalog.Setting, "ms-settings:appsfeatures", SearchResultKind.Setting, "aplicativos", "programas"),

        // --- System tools ---
        new("Gerenciador de Tarefas", "taskmgr", "Ferramenta", GlyphCatalog.Tool, "taskmgr", SearchResultKind.SystemTool, "task manager", "gerenciador de tarefas", "processos"),
        new("Gerenciador de Dispositivos", "devmgmt", "Ferramenta", GlyphCatalog.Tool, "devmgmt.msc", SearchResultKind.SystemTool, "device manager", "dispositivos"),
        new("Serviços", "services", "Ferramenta", GlyphCatalog.Tool, "services.msc", SearchResultKind.SystemTool, "serviços", "servicos"),
        new("Visualizador de Eventos", "event viewer", "Ferramenta", GlyphCatalog.Tool, "eventvwr.msc", SearchResultKind.SystemTool, "eventvwr", "eventos", "logs"),
        new("Gerenciamento do Computador", "computer management", "Ferramenta", GlyphCatalog.Tool, "compmgmt.msc", SearchResultKind.SystemTool, "compmgmt", "gerenciamento"),
        new("Gerenciamento de Disco", "disk management", "Ferramenta", GlyphCatalog.Tool, "diskmgmt.msc", SearchResultKind.SystemTool, "diskmgmt", "gerenciamento de disco"),
        new("Editor de Registro", "regedit", "Ferramenta", GlyphCatalog.Tool, "regedit", SearchResultKind.SystemTool, "registry", "registro"),
        new("PowerShell", "powershell", "Ferramenta", GlyphCatalog.Tool, "powershell", SearchResultKind.SystemTool, "pwsh", "shell"),
        new("Prompt de Comando", "cmd", "Ferramenta", GlyphCatalog.Tool, "cmd", SearchResultKind.SystemTool, "command prompt", "prompt", "command"),
        new("Windows Terminal", "terminal", "Ferramenta", GlyphCatalog.Tool, "wt", SearchResultKind.SystemTool, "wt", "terminal"),
        new("Painel de Controle", "control panel", "Ferramenta", GlyphCatalog.Tool, "control", SearchResultKind.SystemTool, "painel de controle", "control"),
        new("Desligar", "shutdown", "Ferramenta", GlyphCatalog.Info, "shutdown", SearchResultKind.SystemTool, "desligar", "power off") { RequiresConfirmation = true, Arguments = "/s /t 0" },
        new("Reiniciar", "restart", "Ferramenta", GlyphCatalog.Info, "shutdown", SearchResultKind.SystemTool, "reiniciar", "reboot") { RequiresConfirmation = true, Arguments = "/r /t 0" },
    };

    public SystemProvider(IActionRunner runner, ISettingsService settingsService)
    {
        _runner = runner;
        _settingsService = settingsService;
    }

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (!_settingsService.Current.EnableSettings)
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var results = new List<SearchResult>();

        foreach (var entry in Entries)
        {
            var match = FuzzyMatcher.Score(query, entry.Name);
            foreach (var keyword in entry.Keywords)
            {
                var keywordScore = FuzzyMatcher.Score(query, keyword);
                if (keywordScore > match)
                    match = keywordScore;
            }

            if (match <= 0.2)
                continue;

            var result = new SearchResult
            {
                Id = "sys:" + entry.Name.ToLowerInvariant(),
                Title = entry.Name,
                Subtitle = entry.Hint.StartsWith("ms-settings", StringComparison.Ordinal)
                    ? $"Configurações do Windows · {entry.Hint}"
                    : entry.Hint,
                SearchText = string.Join(' ', new[]
                {
                    entry.Name,
                    entry.DefaultAlias,
                    entry.Hint,
                    string.Join(' ', entry.Keywords),
                }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Icon = entry.Icon,
                Category = entry.Category,
                Kind = entry.Kind,
                Score = match,
                ExecuteHint = entry.Hint,
                ExecuteArguments = entry.Arguments,
                RequiresConfirmation = entry.RequiresConfirmation,
                CanRunAsAdministrator = !entry.Hint.StartsWith("ms-settings", StringComparison.Ordinal),
                ProviderId = Id,
            };
            result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);
            results.Add(result);
        }

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }
}

internal sealed class SystemEntry
{
    public string Name { get; }
    public string DefaultAlias { get; }
    public string Category { get; }
    public string Icon { get; }
    public string Hint { get; }
    public SearchResultKind Kind { get; }
    public string[] Keywords { get; }
    public bool RequiresConfirmation { get; init; }
    public string Arguments { get; init; } = string.Empty;

    public SystemEntry(string name, string alias, string category, string icon, string hint, SearchResultKind kind, params string[] extraKeywords)
    {
        Name = name;
        DefaultAlias = alias;
        Category = category;
        Icon = icon;
        Hint = hint;
        Kind = kind;
        Keywords = extraKeywords.Concat(new[] { alias }).ToArray();
    }
}
