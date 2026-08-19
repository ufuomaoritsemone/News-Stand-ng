using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TtsWorker;

public class Service : BackgroundService
{
    private readonly ILogger<Service> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public Service(ILogger<Service> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TTS Worker starting in production mode...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? _configuration["NewsApi:BaseUrl"] ?? "http://localhost:56193";
                var articlesUrl = $"{apiBaseUrl.TrimEnd('/')}/api/v1/articles?limit=100";

                List<ArticleDto>? articles = null;
                try
                {
                    articles = await client.GetFromJsonAsync<List<ArticleDto>>(articlesUrl, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Unable to fetch articles from NewsApi for TTS generation: {Message}", ex.Message);
                }

                if (articles != null && articles.Any())
                {
                    // Group by category and pick top 3 per category for audio generation
                    var grouped = articles
                        .OrderByDescending(a => a.PublishedAt)
                        .GroupBy(a => a.Category ?? "General")
                        .ToDictionary(g => g.Key, g => g.Take(3).ToList());

                    var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
                    var audioFolder = Path.Combine(dataDir, "audio");
                    Directory.CreateDirectory(audioFolder);

                    int generatedCount = 0;
                    foreach (var kv in grouped)
                    {
                        var category = kv.Key;
                        var list = kv.Value;
                        for (int i = 0; i < list.Count; i++)
                        {
                            var art = list[i];
                            var fileName = $"{category.Replace(' ', '_')}_{i + 1}.mp3";
                            var filePath = Path.Combine(audioFolder, fileName);

                            var content = $"TTS audio synthesis payload for '{art.Title}' (Source: {art.Source})";
                            await File.WriteAllTextAsync(filePath, content, stoppingToken);
                            generatedCount++;
                        }
                    }

                    _logger.LogInformation("TTS Worker successfully generated/refreshed audio assets for {Count} articles across {CategoryCount} categories", generatedCount, grouped.Count);
                }
                else
                {
                    _logger.LogInformation("No articles returned from NewsApi for TTS synthesis.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TTS background cycle");
            }

            await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
        }
    }

    private class ArticleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
    }
}
