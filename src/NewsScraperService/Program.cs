using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewsScraperService;
using NewsScraperService.Services;
using NigerianNewsGrid.Client;

// Automatically load .env environment variables
EnvLoader.Load();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddSingleton<MlCategorizerEngine>();
        services.AddSingleton<ArticleContentExtractor>();
        services.AddSingleton<UrlFrontierManager>();
        services.AddSingleton<IArticleEnricher, ArticleEnricher>();
        services.AddSingleton<GazetteerEntityExtractor>();
        services.AddSingleton<IEntityExtractor, LlmEntityExtractor>();
        services.AddHostedService<ScraperWorker>();
    })
    .Build();

await host.RunAsync();
