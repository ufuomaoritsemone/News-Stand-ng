using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace NewsScraperService.Services;

public class GazetteerEntityExtractor : IEntityExtractor
{
    private readonly ILogger<GazetteerEntityExtractor> _logger;
    private readonly List<(EntityDefinition Entity, List<Regex> AliasRegexes)> _compiledEntities = [];

    public GazetteerEntityExtractor(ILogger<GazetteerEntityExtractor> logger, string? customDataPath = null)
    {
        _logger = logger;
        LoadGazetteer(customDataPath);
    }

    private void LoadGazetteer(string? customDataPath)
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            var searchPaths = new List<string>();

            if (!string.IsNullOrWhiteSpace(customDataPath))
            {
                searchPaths.Add(customDataPath);
            }

            searchPaths.AddRange(
            [
                Path.Combine(baseDir, "data", "ng-entities.json"),
                Path.Combine(baseDir, "ng-entities.json"),
                Path.GetFullPath(Path.Combine(baseDir, "../../../data/ng-entities.json")),
                Path.GetFullPath(Path.Combine(baseDir, "../../../../src/NewsScraperService/data/ng-entities.json")),
                Path.GetFullPath(Path.Combine(baseDir, "../../../../../src/NewsScraperService/data/ng-entities.json")),
                Path.GetFullPath(Path.Combine(baseDir, "../../../../src/NewsApi/data/ng-entities.json")),
                Path.GetFullPath(Path.Combine(baseDir, "../../../../../src/NewsApi/data/ng-entities.json"))
            ]);

            string? foundPath = searchPaths.FirstOrDefault(File.Exists);

            if (foundPath != null)
            {
                var json = File.ReadAllText(foundPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var entities = JsonSerializer.Deserialize<List<EntityDefinition>>(json, options) ?? [];

                foreach (var entity in entities)
                {
                    var regexes = new List<Regex>();
                    var allAliases = new HashSet<string>(entity.Aliases, StringComparer.OrdinalIgnoreCase)
                    {
                        entity.Name
                    };

                    foreach (var alias in allAliases)
                    {
                        if (string.IsNullOrWhiteSpace(alias) || alias.Length < 2) continue;

                        // Case-insensitive word boundary matching
                        var pattern = $@"\b{Regex.Escape(alias.Trim())}\b";
                        regexes.Add(new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase));
                    }

                    if (regexes.Count > 0)
                    {
                        _compiledEntities.Add((entity, regexes));
                    }
                }

                _logger.LogInformation("Successfully loaded {Count} gazetteer entities from {Path}", _compiledEntities.Count, foundPath);
            }
            else
            {
                _logger.LogWarning("Gazetteer data file 'ng-entities.json' not found in search paths.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Nigerian entity gazetteer.");
        }
    }

    public Task<List<EntityMatch>> ExtractEntitiesAsync(
        string title,
        string? summary = null,
        string? content = null,
        CancellationToken cancellationToken = default)
    {
        var matches = new List<EntityMatch>();
        var seenSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var safeTitle = title ?? string.Empty;
        var safeBody = $"{summary ?? string.Empty} {content ?? string.Empty}";

        foreach (var (entity, regexes) in _compiledEntities)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (seenSlugs.Contains(entity.Slug)) continue;

            bool inTitle = regexes.Any(r => r.IsMatch(safeTitle));
            bool inBody = regexes.Any(r => r.IsMatch(safeBody));

            if (inTitle || inBody)
            {
                float confidence = (inTitle && inBody) ? 1.0f : (inTitle ? 0.95f : 0.85f);

                matches.Add(new EntityMatch
                {
                    Name = entity.Name,
                    Slug = entity.Slug,
                    Type = entity.Type,
                    Description = entity.Description,
                    Confidence = confidence,
                    SourceTier = "Gazetteer"
                });

                seenSlugs.Add(entity.Slug);
            }
        }

        // Return matches ordered by confidence descending, then by name length descending
        return Task.FromResult(matches
            .OrderByDescending(m => m.Confidence)
            .ThenByDescending(m => m.Name.Length)
            .ToList());
    }
}
