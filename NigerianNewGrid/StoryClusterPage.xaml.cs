using Microsoft.Extensions.Logging.Abstractions;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Services;
using NigerianNewGrid.ViewModels;

namespace NigerianNewGrid;

[QueryProperty(nameof(StoryItem), "Story")]
[QueryProperty(nameof(SlugQuery), "slug")]
public partial class StoryClusterPage : ContentPage
{
    private readonly StoryClusterViewModel _vm;

    public TrendingStoryItem? StoryItem
    {
        set
        {
            if (value is not null)
            {
                _ = _vm.InitializeFromTrendingStoryAsync(value);
            }
        }
    }

    public string? SlugQuery
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(_vm.Slug))
            {
                _ = _vm.InitializeFromSlugAsync(value);
            }
        }
    }

    public StoryClusterPage(StoryClusterViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = _vm;
    }

    public StoryClusterPage()
        : this(ResolveViewModel())
    {
    }

    private static StoryClusterViewModel ResolveViewModel()
    {
        if (IPlatformApplication.Current?.Services is { } sp)
        {
            return sp.GetRequiredService<StoryClusterViewModel>();
        }

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var client = new NewsApiClient(http) { BaseUrl = AppNetworkConstants.GetConfiguredApiBaseUrl() };
        return new StoryClusterViewModel(
            client,
            NullAnalyticsService.Instance,
            NullLogger<StoryClusterViewModel>.Instance);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("..");
        }
        else if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync();
        }
    }
}
