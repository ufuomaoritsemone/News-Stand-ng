using System.Diagnostics;
using System.Text.Json;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid;

/// <summary>
/// Discover tab — search + category filter over articles and trending YouTube video stories.
/// </summary>
public partial class DiscoverPage : ContentPage
{
    private readonly NewsApiClient? _apiClient;
    private List<BriefingItem> _allItems = [];
    private List<VideoStoryItem> _trendingVideos = [];
    private string _selectedCategory = "All";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public DiscoverPage() : this(null)
    {
    }

    public DiscoverPage(NewsApiClient? apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        Appearing += async (_, _) =>
        {
            LoadFromCache();
            await LoadTrendingVideosAsync();
        };
    }

    private void LoadFromCache()
    {
        var cached = Preferences.Get("last_briefing", string.Empty);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var categories = JsonSerializer.Deserialize<List<BriefingCategory>>(cached, JsonOptions);
                _allItems = categories?.SelectMany(c => c.Top).ToList() ?? [];
            }
            catch (JsonException) { _allItems = []; }
        }

        var cachedVideos = Preferences.Get("last_trending_videos", string.Empty);
        if (!string.IsNullOrWhiteSpace(cachedVideos))
        {
            try
            {
                _trendingVideos = JsonSerializer.Deserialize<List<VideoStoryItem>>(cachedVideos, JsonOptions) ?? [];
                RenderTrendingVideos(_trendingVideos);
            }
            catch (JsonException) { }
        }

        ApplyFilter();
    }

    private async Task LoadTrendingVideosAsync()
    {
        try
        {
            var baseUrl = Preferences.Get("api_base_url", "http://localhost:56193");
            var client = _apiClient ?? new NewsApiClient(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });
            client.BaseUrl = baseUrl;

            var videos = await client.GetTrendingVideoStoriesAsync(15);
            if (videos.Count > 0)
            {
                _trendingVideos = videos;
                Preferences.Set("last_trending_videos", JsonSerializer.Serialize(videos, JsonOptions));
                RenderTrendingVideos(_trendingVideos);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DiscoverPage] Error loading trending videos: {ex.Message}");
        }
    }

    private void RenderTrendingVideos(List<VideoStoryItem> videos)
    {
        TrendingVideosLayout.Children.Clear();

        if (videos.Count == 0)
        {
            TrendingSection.IsVisible = false;
            return;
        }

        TrendingSection.IsVisible = true;

        foreach (var video in videos)
        {
            var card = CreateVideoCard(video);
            TrendingVideosLayout.Children.Add(card);
        }
    }

    private View CreateVideoCard(VideoStoryItem video)
    {
        var cardBorder = new Border
        {
            WidthRequest = 200,
            HeightRequest = 240,
            Padding = 0,
            BackgroundColor = Colors.White,
            StrokeThickness = 1,
            Stroke = (Brush)Application.Current!.Resources["Gray100Brush"],
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) }
        };

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(126) },
                new RowDefinition { Height = GridLength.Star }
            }
        };

        // Thumbnail with duration & rank overlays
        var imgGrid = new Grid();
        var img = new Image
        {
            Source = !string.IsNullOrWhiteSpace(video.ThumbnailUrl) ? video.ThumbnailUrl : "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600",
            Aspect = Aspect.AspectFill,
            HeightRequest = 126
        };
        imgGrid.Children.Add(img);

        // Dark gradient overlay
        var darkOverlay = new BoxView
        {
            Color = Colors.Black,
            Opacity = 0.25,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };
        imgGrid.Children.Add(darkOverlay);

        // Rank badge
        if (video.TrendingRank.HasValue)
        {
            var rankPill = new Border
            {
                Padding = new Thickness(6, 2),
                BackgroundColor = Color.FromArgb("#F59E0B"),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10) },
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(8),
                Content = new Label
                {
                    Text = $"#{video.TrendingRank}",
                    FontFamily = "LegacySansBook",
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                }
            };
            imgGrid.Children.Add(rankPill);
        }

        // Play icon in center
        var playIcon = new Label
        {
            Text = "▶",
            FontSize = 18,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        imgGrid.Children.Add(playIcon);

        // Duration badge at bottom right
        var durPill = new Border
        {
            Padding = new Thickness(5, 2),
            BackgroundColor = Color.FromRgba(0, 0, 0, 180),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(6) },
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(6),
            Content = new Label
            {
                Text = string.IsNullOrWhiteSpace(video.Duration) ? "HD" : video.Duration,
                FontFamily = "LegacySansBook",
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            }
        };
        imgGrid.Children.Add(durPill);

        grid.Children.Add(imgGrid);
        Grid.SetRow(imgGrid, 0);

        // Meta content
        var metaStack = new VerticalStackLayout
        {
            Padding = new Thickness(10, 8),
            Spacing = 3
        };

        var channelLabel = new Label
        {
            Text = video.ChannelName,
            FontFamily = "LegacySansBook",
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = (Color)Application.Current!.Resources["AccentBlue"],
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var titleLabel = new Label
        {
            Text = video.Title,
            FontFamily = "LegacySerifBold",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = (Color)Application.Current!.Resources["Gray900"],
            MaxLines = 2,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var viewsText = video.ViewCount > 0
            ? $"🔥 {(video.ViewCount >= 1000 ? $"{video.ViewCount / 1000.0:F1}K" : video.ViewCount.ToString())} views"
            : "🔥 Trending";

        var viewsLabel = new Label
        {
            Text = viewsText,
            FontFamily = "LegacySansBook",
            FontSize = 10,
            TextColor = (Color)Application.Current!.Resources["Gray500"]
        };

        metaStack.Children.Add(channelLabel);
        metaStack.Children.Add(titleLabel);
        metaStack.Children.Add(viewsLabel);

        grid.Children.Add(metaStack);
        Grid.SetRow(metaStack, 1);

        cardBorder.Content = grid;

        // Tap Gesture
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(video.VideoUrl))
            {
                await Navigation.PushAsync(new ArticleWebPage(video.VideoUrl, video.Title, video.ThumbnailUrl, video.Category, video.Id));
            }
        };
        cardBorder.GestureRecognizers.Add(tap);

        return cardBorder;
    }

    private void ApplyFilter()
    {
        var query = DiscoverSearchEntry?.Text?.Trim() ?? string.Empty;
        ClearBtn.IsVisible = !string.IsNullOrEmpty(query);

        IEnumerable<BriefingItem> filtered = _allItems;

        if (_selectedCategory != "All")
            filtered = filtered.Where(i =>
                string.Equals(i.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(query))
            filtered = filtered.Where(i =>
                (i.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (i.Summary?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (i.Source?.Contains(query, StringComparison.OrdinalIgnoreCase) == true));

        var list = filtered.ToList();
        RenderArticles(list);
    }

    private void RenderArticles(List<BriefingItem> articles)
    {
        ResultsContainer.Children.Clear();

        if (articles.Count == 0)
        {
            EmptyStateView.IsVisible = true;
            ResultsHeaderLabel.IsVisible = false;
            return;
        }

        EmptyStateView.IsVisible = false;
        ResultsHeaderLabel.IsVisible = true;
        ResultsHeaderLabel.Text = _selectedCategory == "All"
            ? $"All News Articles ({articles.Count})"
            : $"{_selectedCategory} Articles ({articles.Count})";

        foreach (var item in articles)
        {
            var card = CreateArticleCard(item);
            ResultsContainer.Children.Add(card);
        }
    }

    private View CreateArticleCard(BriefingItem item)
    {
        var border = new Border
        {
            Padding = 0,
            BackgroundColor = (Color)Application.Current!.Resources["White"],
            StrokeThickness = 1,
            Stroke = (Brush)Application.Current!.Resources["Gray100Brush"],
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
            HeightRequest = 132
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(132) },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 0
        };

        var imgBorder = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16, 0, 16, 0) },
            Content = new Image
            {
                Source = !string.IsNullOrWhiteSpace(item.ImageUrl) ? item.ImageUrl : "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600",
                Aspect = Aspect.AspectFill,
                HeightRequest = 132,
                WidthRequest = 132
            }
        };
        grid.Children.Add(imgBorder);
        Grid.SetColumn(imgBorder, 0);

        var textStack = new VerticalStackLayout
        {
            Padding = new Thickness(14, 10),
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center
        };

        var catLabel = new Label
        {
            Text = item.Category?.ToUpperInvariant() ?? "NEWS",
            FontFamily = "LegacySansBook",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = (Color)Application.Current!.Resources["AccentBlue"]
        };

        var titleLabel = new Label
        {
            Text = item.Title,
            FontFamily = "LegacySerifBold",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            MaxLines = 2,
            LineBreakMode = LineBreakMode.TailTruncation,
            TextColor = (Color)Application.Current!.Resources["Gray900"]
        };

        var sourceLabel = new Label
        {
            Text = item.Source,
            FontFamily = "LegacySansBook",
            FontSize = 11,
            TextColor = (Color)Application.Current!.Resources["Gray500"]
        };

        textStack.Children.Add(catLabel);
        textStack.Children.Add(titleLabel);
        textStack.Children.Add(sourceLabel);

        grid.Children.Add(textStack);
        Grid.SetColumn(textStack, 1);

        border.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(item.Url))
            {
                await Navigation.PushAsync(new ArticleWebPage(item.Url, item.Title, item.ImageUrl, item.Category, item.Id));
            }
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnClearSearch(object? sender, EventArgs e)
    {
        DiscoverSearchEntry.Text = string.Empty;
        ApplyFilter();
    }

    private void OnCategoryPillTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string cat) return;
        _selectedCategory = cat;

        // Reset all pill backgrounds
        var pills = new[] { PillAll, PillPolitics, PillSports, PillBusiness, PillEntertainment, PillTech };
        var labels = new[] { "All", "Politics", "Sports", "Business", "Entertainment", "Technology" };

        for (int i = 0; i < pills.Length; i++)
        {
            bool isSelected = string.Equals(labels[i], cat, StringComparison.OrdinalIgnoreCase) ||
                              (cat == "All" && labels[i] == "All");
            pills[i].BackgroundColor = isSelected
                ? (Color)Application.Current!.Resources["AccentBlue"]
                : (Color)Application.Current!.Resources["Secondary"];

            if (pills[i].Content is Label lbl)
                lbl.TextColor = isSelected ? Colors.White : (Color)Application.Current!.Resources["Gray900"];
        }

        ApplyFilter();
    }
}

