using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewsScraperService;
using NewsScraperService.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddSingleton<MlCategorizerEngine>();
        services.AddSingleton<ArticleContentExtractor>();
        services.AddSingleton<UrlFrontierManager>();
        services.AddHostedService<ScraperWorker>();
    })
    .Build();

await host.RunAsync();
