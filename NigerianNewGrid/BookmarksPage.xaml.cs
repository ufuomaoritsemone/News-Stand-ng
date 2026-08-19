using System.Text.Json;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid;

public partial class BookmarksPage : ContentPage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly List<BriefingItem> _bookmarks = [];

    public BookmarksPage()
    {
        InitializeComponent();

        Appearing += (_, _) => RefreshBookmarks();
    }

    private void RefreshBookmarks()
    {
        _bookmarks.Clear();

        var cached = Preferences.Get("bookmarks", string.Empty);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<BriefingItem>>(cached, JsonOptions);
                if (items != null)
                    _bookmarks.AddRange(items);
            }
            catch (JsonException) { /* ignore corrupt cache */ }
        }

        BookmarksView.ItemsSource = _bookmarks.ToList();
        BookmarkCountLabel.Text = _bookmarks.Count == 1
            ? "1 saved story"
            : $"{_bookmarks.Count} saved stories";
        ClearAllBtn.IsVisible = _bookmarks.Count > 0;
    }

    private async void OnBookmarkTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is BriefingItem item && !string.IsNullOrWhiteSpace(item.Url))
        {
            await Navigation.PushAsync(new ArticleWebPage(item.Url, item.Title, item.ImageUrl, item.Category, item.Id));
        }
    }

    private async void OnClearAllClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync(
            "Clear All Bookmarks",
            "Are you sure you want to remove all saved stories?",
            "Clear All",
            "Cancel");

        if (confirm)
        {
            Preferences.Remove("bookmarks");
            RefreshBookmarks();
        }
    }
}
