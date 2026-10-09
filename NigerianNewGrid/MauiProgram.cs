using Microsoft.Extensions.Logging;
using NigerianNewGrid.Extensions;
using Plugin.AdMob;

namespace NigerianNewGrid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseAdMob()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("LegacySerifITCTTBold.ttf", "LegacySerifBold");
                fonts.AddFont("Legacy Sans Book.TTF", "LegacySansBook");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services
            .AddNewsHttpClients()
            .AddNewsServices()
            .AddPlatformServices()
            .AddViewModels()
            .AddPages();

        return builder.Build();
    }
}
