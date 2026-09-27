using NigerianNewGrid.Services;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid;

public partial class BookmarksPage : ContentPage
{
    private readonly IBookmarkService _bookmarkService;

    public BookmarksPage() : this(null)
    {
    }

    public BookmarksPage(IBookmarkService? bookmarkService)
    {
        InitializeComponent();
        _bookmarkService = bookmarkService 
            ?? IPlatformApplication.Current?.Services.GetService<IBookmarkService>()
            ?? new BookmarkService();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _bookmarkService.BookmarksChanged += OnBookmarksChanged;
        RefreshBookmarks();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bookmarkService.BookmarksChanged -= OnBookmarksChanged;
    }

    private void OnBookmarksChanged(object? sender, EventArgs e) => RefreshBookmarks();

    private void RefreshBookmarks()
    {
        var bookmarks = _bookmarkService.GetBookmarks();

        BookmarksView.ItemsSource = bookmarks.ToList();
        BookmarkCountLabel.Text = bookmarks.Count == 1
            ? "1 saved story"
            : $"{bookmarks.Count} saved stories";
        ClearAllBtn.IsVisible = bookmarks.Count > 0;
    }

    private async void OnBookmarkTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is BriefingItem item && !string.IsNullOrWhiteSpace(item.Url))
            {
                await Navigation.PushAsync(new ArticleWebPage(item));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BookmarksPage] Bookmark tap error: {ex.Message}");
        }
    }

    private async void OnClearAllClicked(object? sender, EventArgs e)
    {
        try
        {
            bool confirm = await DisplayAlertAsync(
                "Clear All Bookmarks",
                "Are you sure you want to remove all saved stories?",
                "Clear All",
                "Cancel");

            if (confirm)
            {
                _bookmarkService.ClearAllBookmarks();
                RefreshBookmarks();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BookmarksPage] Clear all error: {ex.Message}");
        }
    }
}
