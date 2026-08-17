using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Utilities;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Search.ToolsProvider;

/// <summary>Developer tools: file hashes, password generation and JSON formatting.</summary>
public sealed class ToolsProvider : ISearchProvider
{
    private static readonly string[] HashAlgorithms = { "md5", "sha1", "sha256", "sha384", "sha512" };

    private readonly IActionRunner _runner;
    private readonly INotificationService _notification;

    public string Id => "tools";

    public string Name => "Ferramentas";

    public ToolsProvider(IActionRunner runner, INotificationService notification)
    {
        _runner = runner;
        _notification = notification;
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return Array.Empty<SearchResult>();

        var results = new List<SearchResult>();

        // Hash: "sha256 <file>"
        var hashParts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (hashParts.Length == 2 && HashAlgorithms.Contains(hashParts[0].ToLowerInvariant()))
        {
            var algo = hashParts[0].ToLowerInvariant();
            var path = hashParts[1];
            results.Add(await BuildHashResultAsync(algo, path, cancellationToken));
            return results;
        }

        // Password: "password [length]"
        if (trimmed.StartsWith("password", StringComparison.OrdinalIgnoreCase))
        {
            var lengthPart = trimmed.Length > "password".Length ? trimmed["password".Length..].Trim() : string.Empty;
            if (int.TryParse(lengthPart, out var length))
            {
                results.Add(BuildPasswordResult(length));
                return results;
            }

            foreach (var size in new[] { 16, 24, 32 })
                results.Add(BuildPasswordResult(size));
            return results;
        }

        // Time/date: "hora", "data", "agora", "time", "date", "hoje"
        var clockResult = BuildClockResults(trimmed);
        if (clockResult is not null)
            return new[] { clockResult };

        // JSON: starts with { or [
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            results.AddRange(BuildJsonResults(trimmed));
            return results;
        }

        return results;
    }

    private SearchResult? BuildClockResults(string query)
    {
        var now = DateTime.Now;
        string? payload = query.ToLowerInvariant() switch
        {
            "hora" or "time" or "agora" or "que horas sao" => now.ToString("HH:mm:ss"),
            "data" or "date" or "hoje" or "dia" => now.ToString("dd/MM/yyyy"),
            "agora completo" or "datetime" => now.ToString("dd/MM/yyyy HH:mm:ss"),
            _ => null,
        };

        if (payload is null)
            return null;

        var result = new SearchResult
        {
            Id = "clock:" + query.ToLowerInvariant(),
            Title = payload,
            Subtitle = "Copiar data/hora",
            Icon = GlyphCatalog.Clock,
            Category = "Ferramenta",
            Kind = SearchResultKind.TextAction,
            Score = 0.9,
            ExecuteHint = payload,
            ProviderId = Id,
        };
        result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);
        return result;
    }

    private async Task<SearchResult> BuildHashResultAsync(string algo, string path, CancellationToken cancellationToken)
    {
        var display = path.Length > 70 ? path[..70] + "…" : path;
        var result = new SearchResult
        {
            Id = "hash:" + algo + ":" + path.ToLowerInvariant(),
            Title = $"Calcular {algo.ToUpperInvariant()}",
            Subtitle = display,
            Icon = GlyphCatalog.Hash,
            Category = "Ferramenta",
            Kind = SearchResultKind.TextAction,
            Score = 0.95,
            ProviderId = Id,
        };

        result.ExecuteAsync = async () =>
        {
            if (!File.Exists(path))
            {
                _notification.Show("Lunaris", "Arquivo não encontrado");
                return;
            }

            var hash = await Task.Run(() => ComputeHash(path, algo), cancellationToken);
            await _runner.CopyToClipboardAsync(hash);
            _notification.Show("Lunaris", $"{algo.ToUpperInvariant()} copiado");
        };

        return result;
    }

    private static string ComputeHash(string path, string algo)
    {
        using var stream = File.OpenRead(path);
        HashAlgorithm hashAlgorithm = algo.ToLowerInvariant() switch
        {
            "md5" => MD5.Create(),
            "sha1" => SHA1.Create(),
            "sha256" => SHA256.Create(),
            "sha384" => SHA384.Create(),
            "sha512" => SHA512.Create(),
            _ => throw new InvalidOperationException($"Unknown hash algorithm: {algo}"),
        };
        using (hashAlgorithm)
        {
            var bytes = hashAlgorithm.ComputeHash(stream);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }

    private SearchResult BuildPasswordResult(int length)
    {
        var password = PasswordGenerator.Generate(length);
        var result = new SearchResult
        {
            Id = "password:" + length,
            Title = $"Copiar senha ({length} caracteres)",
            Subtitle = "Gerada com RandomNumberGenerator",
            Icon = GlyphCatalog.Lock,
            Category = "Ferramenta",
            Kind = SearchResultKind.TextAction,
            Score = 0.9,
            ExecuteHint = password,
            ProviderId = Id,
        };
        result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);
        return result;
    }

    private IEnumerable<SearchResult> BuildJsonResults(string json)
    {
        var valid = false;
        string? validationMessage = null;

        try
        {
            _ = JsonNode.Parse(json);
            valid = true;
        }
        catch (Exception ex)
        {
            validationMessage = ex.Message;
        }

        var results = new List<SearchResult>();

        if (!valid)
        {
            results.Add(new SearchResult
            {
                Id = "json:invalid",
                Title = "JSON inválido",
                Subtitle = validationMessage ?? "Erro de parsing",
                Icon = GlyphCatalog.File,
                Category = "Ferramenta",
                Kind = SearchResultKind.TextAction,
                Score = 0.98,
                ProviderId = Id,
            });
            return results;
        }

        AddJsonAction(results, "Formatar JSON", "Indenta e copia o JSON", () => JsonNode.Parse(json)?.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        AddJsonAction(results, "Minificar JSON", "Compacta e copia o JSON", () => JsonNode.Parse(json)?.ToJsonString());
        results.Add(new SearchResult
        {
            Id = "json:valid",
            Title = "JSON válido",
            Subtitle = "Nenhum erro encontrado",
            Icon = GlyphCatalog.Info,
            Category = "Ferramenta",
            Kind = SearchResultKind.TextAction,
            Score = 0.97,
            ProviderId = Id,
        });

        return results;
    }

    private void AddJsonAction(List<SearchResult> results, string title, string subtitle, Func<string?> produce)
    {
        var result = new SearchResult
        {
            Id = "json:" + title.ToLowerInvariant(),
            Title = title,
            Subtitle = subtitle,
            Icon = GlyphCatalog.File,
            Category = "Ferramenta",
            Kind = SearchResultKind.TextAction,
            Score = 0.99,
            ProviderId = Id,
        };
        result.ExecuteAsync = async () =>
        {
            var output = produce();
            if (output is not null)
                await _runner.CopyToClipboardAsync(output);
        };
        results.Add(result);
    }
}