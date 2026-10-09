using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Models;
using NigerianNewGrid.Services;
using NigerianNewGrid.ViewModels;

namespace NigerianNewGrid;

/// <summary>
/// Instant Native Reader Mode and WebView fallback page for reading articles (Issue #19).
/// Pure MVVM implementation: UI state, hydration, related stories, typography, and sharing
/// are handled declaratively via compiled bindings to <see cref="ArticleViewModel"/>.
/// The code-behind is restricted exclusively to view-layer animations, back navigation,
/// and WebView JavaScript injection.
/// </summary>
[QueryProperty(nameof(ArticleIdQuery), "id")]
[QueryProperty(nameof(ArticleUrlQuery), "url")]
public partial class ArticleWebPage : ContentPage
{
    private readonly ArticleViewModel _vm;

    public string? ArticleIdQuery
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(_vm.ArticleId))
            {
                _vm.ArticleId = value;
                _ = _vm.HydrateContentIfMissingAsync(value);
            }
        }
    }

    public string? ArticleUrlQuery
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(_vm.ArticleUrl))
            {
                _vm.ArticleUrl = value;
            }
        }
    }

    public ArticleWebPage(ArticleNavigationParams parameters)
        : this(ResolveViewModel())
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _vm.Initialize(parameters);
        ApplyInitialViewMode();
    }

    public ArticleWebPage()
        : this(ResolveViewModel())
    {
    }

    public ArticleWebPage(ArticleViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = _vm;

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.FontSizeChanged += OnFontSizeChanged;
    }

    public ArticleWebPage(string articleId)
        : this(new ArticleNavigationParams(Url: string.Empty, ArticleId: articleId))
    {
    }

    public ArticleWebPage(
        string url,
        string? title = null,
        string? imageUrl = null,
        string? category = null,
        string? articleId = null,
        bool isSponsored = false,
        string? sponsorName = null,
        string? content = null,
        string? summary = null,
        string? author = null,
        string? contentType = null,
        string? slug = null)
        : this(new ArticleNavigationParams(
            Url: url,
            Title: title,
            ImageUrl: imageUrl,
            Category: category,
            ArticleId: articleId,
            IsSponsored: isSponsored,
            SponsorName: sponsorName,
            Content: content,
            Summary: summary,
            Author: author,
            ContentType: contentType,
            Slug: slug))
    {
    }

    public ArticleWebPage(BriefingItem item, bool isSponsored = false, string? sponsorName = null)
        : this(ArticleNavigationParams.FromBriefingItem(item, isSponsored, sponsorName))
    {
    }

    private static ArticleViewModel ResolveViewModel()
    {
        // Always resolve from the DI container to ensure bookmarks, read history, and analytics
        // use the same singleton instances as the rest of the app (Fix #2).
        if (IPlatformApplication.Current?.Services is { } sp)
        {
            return sp.GetRequiredService<ArticleViewModel>();
        }

        // Last-resort fallback for previewers/isolated execution
        System.Diagnostics.Debug.WriteLine("[ArticleWebPage] WARNING: DI container unavailable — creating standalone ArticleViewModel.");
        var fallbackClient = GetFallbackNewsApiClient();
        var persistence = new NewsPersistenceService(Microsoft.Extensions.Logging.Abstractions.NullLogger<NewsPersistenceService>.Instance);
        var bService = new BookmarkService(persistence);
        var rService = new RecentlyReadService(persistence);
        var aService = NullAnalyticsService.Instance;
        var xService = new XShareService();
        return new ArticleViewModel(fallbackClient, persistence, bService, rService, aService, xService, NullLogger<ArticleViewModel>.Instance);
    }

    private static NewsApiClient GetFallbackNewsApiClient()
    {
        var client = IPlatformApplication.Current?.Services.GetService<NewsApiClient>();
        if (client != null) return client;

        var factory = IPlatformApplication.Current?.Services.GetService<IHttpClientFactory>();
        var http = factory?.CreateClient() ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        var baseUrl = AppNetworkConstants.GetConfiguredApiBaseUrl();
        return new NewsApiClient(http) { BaseUrl = baseUrl };
    }

    private void ApplyInitialViewMode()
    {
        bool hasValidUrl = !string.IsNullOrWhiteSpace(_vm.ArticleUrl) && Uri.TryCreate(_vm.ArticleUrl, UriKind.Absolute, out _);

        if (_vm.IsVideoStory && hasValidUrl)
        {
            _vm.IsReaderMode = false;
            EnsureWebViewSourceLoaded();
        }
        else
        {
            _vm.IsReaderMode = true;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ArticleViewModel.IsWebViewVisible) && _vm.IsWebViewVisible)
        {
            EnsureWebViewSourceLoaded();
        }
    }

    private void EnsureWebViewSourceLoaded()
    {
        if (!string.IsNullOrWhiteSpace(_vm.ArticleUrl) &&
            (ArticleWebView.Source == null || (ArticleWebView.Source is UrlWebViewSource u && string.IsNullOrEmpty(u.Url))))
        {
            ArticleWebView.Source = _vm.ArticleUrl;
        }
    }

    private void OnToggleReaderModeClicked(object? sender, EventArgs e)
    {
        if (_vm.IsVideoStory) return;

        _vm.ToggleReaderMode();
        if (_vm.IsWebViewVisible)
        {
            EnsureWebViewSourceLoaded();
        }
    }

    private void OnFontSizeChanged()
    {
        if (ArticleWebView.IsVisible)
        {
            try
            {
                var script = ArticleDistillerService.GetSetFontSizeScript(_vm.GetFontSizeString());
                _ = ArticleWebView.EvaluateJavaScriptAsync(script);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ArticleWebPage] Font size adjustment error: {ex.Message}");
            }
        }
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        _vm.IsLoading = true;
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _vm.IsLoading = false;

        if (_vm.IsVideoStory) return;

        if (e.Result == WebNavigationResult.Success)
        {
            try
            {
                var script = ArticleDistillerService.GenerateChromeReaderDistillerScript(
                    publisherName: _vm.Publisher.Name,
                    publisherLogoUrl: _vm.Publisher.LogoUrl,
                    brandColor: _vm.Publisher.BrandColor,
                    fallbackTitle: _vm.Title,
                    fallbackImageUrl: _vm.ImageUrl,
                    category: _vm.Category,
                    active: _vm.IsReaderMode,
                    fontSize: _vm.GetFontSizeString());

                await ArticleWebView.EvaluateJavaScriptAsync(script);
                await Task.Delay(400);
                await ArticleWebView.EvaluateJavaScriptAsync(script);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ArticleWebPage] Reader mode injection error: {ex.Message}");
            }
        }
    }

    private async void OnToggleRelatedClicked(object? sender, EventArgs e)
    {
        _vm.ToggleRelatedExpanded();

        if (_vm.IsRelatedExpanded)
        {
            RelatedExpandedSheet.Opacity = 0;
            RelatedExpandedSheet.TranslationY = 80;
            await Task.WhenAll(
                RelatedExpandedSheet.FadeToAsync(1, 220, Easing.CubicOut),
                RelatedExpandedSheet.TranslateToAsync(0, 0, 220, Easing.CubicOut)
            );
        }
        else
        {
            await Task.WhenAll(
                RelatedExpandedSheet.FadeToAsync(0, 180, Easing.CubicIn),
                RelatedExpandedSheet.TranslateToAsync(0, 80, 180, Easing.CubicIn)
            );
        }
    }

    private void OnRelatedFilterClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string filter)
        {
            _vm.SetRelatedFilter(filter);

            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var activeBg = (Color)Application.Current!.Resources["AccentOrange"];
            var activeText = Colors.White;
            var activeBorder = (Color)Application.Current!.Resources["AccentOrange"];

            var inactiveBg = isDark ? (Color)Application.Current!.Resources["ButtonSecondaryBgDark"] : (Color)Application.Current!.Resources["ButtonBgLight"];
            var inactiveText = isDark ? (Color)Application.Current!.Resources["ButtonTextDark"] : (Color)Application.Current!.Resources["ButtonTextLight"];
            var inactiveBorder = isDark ? (Color)Application.Current!.Resources["ButtonBorderDark"] : (Color)Application.Current!.Resources["ButtonBorderLight"];

            void StyleFilterButton(Button button, bool isActive)
            {
                button.BackgroundColor = isActive ? activeBg : inactiveBg;
                button.TextColor = isActive ? activeText : inactiveText;
                button.BorderColor = isActive ? activeBorder : inactiveBorder;
                button.BorderWidth = 1.5;
            }

            StyleFilterButton(FilterAllBtn, filter == "All");
            StyleFilterButton(FilterArticlesBtn, filter == "Articles");
            StyleFilterButton(FilterVideosBtn, filter == "Videos");
        }
    }

    private async void OnRelatedItemCardTapped(object? sender, EventArgs e)
    {
        try
        {
            RelatedStoryDisplayItem? item = null;
            if (sender is BindableObject bindable && bindable.BindingContext is RelatedStoryDisplayItem contextItem)
            {
                item = contextItem;
            }
            else if (e is TappedEventArgs tapped && tapped.Parameter is RelatedStoryDisplayItem paramItem)
            {
                item = paramItem;
            }

            if (item != null && !string.IsNullOrWhiteSpace(item.Url))
            {
                _vm.IsRelatedExpanded = false;
                _vm.IsRelatedCollapsedVisible = _vm.TotalRelatedCount > 0;

                var targetCategory = item.IsVideo ? "Video" : (item.IsSocial ? "Socials" : item.Category);
                await Navigation.PushAsync(new ArticleWebPage(new ArticleNavigationParams(
                    Url: item.Url,
                    Title: item.Title,
                    ImageUrl: item.ImageUrl,
                    Category: targetCategory,
                    ArticleId: item.Id,
                    Content: item.Content,
                    Summary: item.Subtitle)));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Related story navigation error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // When this page is popped from the navigation stack, eagerly release native WebView resources
        // so that memory from previously read stories does not accumulate on mobile devices.
        var nav = Navigation?.NavigationStack;
        if (nav != null && !nav.Contains(this))
        {
            try
            {
                ArticleWebView.Source = null;
                ArticleWebView.Handler?.DisconnectHandler();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ArticleWebPage] Teardown error on pop: {ex.Message}");
            }
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!_vm.IsReaderMode && ArticleWebView.CanGoBack)
                ArticleWebView.GoBack();
            else
                await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Navigation back error: {ex.Message}");
        }
    }
}
