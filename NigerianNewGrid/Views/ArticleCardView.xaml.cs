using System.Windows.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using NigerianNewGrid.Services;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Views;

/// <summary>
/// Reusable article card view for news feeds (Issue #31 / Fix 3).
/// Encapsulates thumbnail rendering, category and sponsor badges, source attribution,
/// and interactive actions (bookmark, system share, and 𝕏 share).
/// </summary>
public partial class ArticleCardView : ContentView
{
    public static readonly BindableProperty ArticleTappedCommandProperty =
        BindableProperty.Create(nameof(ArticleTappedCommand), typeof(ICommand), typeof(ArticleCardView));

    public static readonly BindableProperty BookmarkCommandProperty =
        BindableProperty.Create(nameof(BookmarkCommand), typeof(ICommand), typeof(ArticleCardView));

    public static readonly BindableProperty ShareCommandProperty =
        BindableProperty.Create(nameof(ShareCommand), typeof(ICommand), typeof(ArticleCardView));

    public static readonly BindableProperty ShareOnXCommandProperty =
        BindableProperty.Create(nameof(ShareOnXCommand), typeof(ICommand), typeof(ArticleCardView));

    public static readonly BindableProperty WhatsAppCommandProperty =
        BindableProperty.Create(nameof(WhatsAppCommand), typeof(ICommand), typeof(ArticleCardView));

    public ICommand? ArticleTappedCommand
    {
        get => (ICommand?)GetValue(ArticleTappedCommandProperty);
        set => SetValue(ArticleTappedCommandProperty, value);
    }

    public ICommand? BookmarkCommand
    {
        get => (ICommand?)GetValue(BookmarkCommandProperty);
        set => SetValue(BookmarkCommandProperty, value);
    }

    public ICommand? ShareCommand
    {
        get => (ICommand?)GetValue(ShareCommandProperty);
        set => SetValue(ShareCommandProperty, value);
    }

    public ICommand? ShareOnXCommand
    {
        get => (ICommand?)GetValue(ShareOnXCommandProperty);
        set => SetValue(ShareOnXCommandProperty, value);
    }

    public ICommand? WhatsAppCommand
    {
        get => (ICommand?)GetValue(WhatsAppCommandProperty);
        set => SetValue(WhatsAppCommandProperty, value);
    }

    public event EventHandler<TappedEventArgs>? ArticleTapped;
    public event EventHandler<EventArgs>? BookmarkClicked;
    public event EventHandler<EventArgs>? ShareClicked;
    public event EventHandler<EventArgs>? ShareOnXClicked;
    public event EventHandler<EventArgs>? WhatsAppClicked;

    public ArticleCardView()
    {
        InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (BindingContext is BriefingItem story)
        {
            UpdateBookmarkVisualState(story);
        }
    }

    private async void OnCardTapped(object? sender, EventArgs e)
    {
        if (BindingContext is BriefingItem story)
        {
            try
            {
                await this.ScaleToAsync(0.985, 55, Easing.CubicOut);
                await this.ScaleToAsync(1.0, 55, Easing.CubicIn);
            }
            catch
            {
                // Ignore animation cancellation if view detaches
            }

            if (ArticleTappedCommand?.CanExecute(story) == true)
            {
                ArticleTappedCommand.Execute(story);
                return;
            }

            if (ArticleTapped != null)
            {
                ArticleTapped.Invoke(this, new TappedEventArgs(story));
                return;
            }

            // Default navigation fallback
            if (!string.IsNullOrWhiteSpace(story.Url) || !string.IsNullOrWhiteSpace(story.Id))
            {
                await Navigation.PushAsync(new ArticleWebPage(story));
            }
        }
    }

    private void OnBookmarkButtonClicked(object? sender, EventArgs e)
    {
        if (BindingContext is BriefingItem story)
        {
            if (BookmarkCommand?.CanExecute(story) == true)
            {
                BookmarkCommand.Execute(story);
                UpdateBookmarkVisualState(story);
                return;
            }

            if (BookmarkClicked != null)
            {
                BookmarkClicked.Invoke(this, EventArgs.Empty);
                UpdateBookmarkVisualState(story);
                return;
            }

            // Default bookmark toggle fallback (e.g. on BookmarksPage)
            var bookmarkService = IPlatformApplication.Current?.Services.GetService<IBookmarkService>();
            bookmarkService?.ToggleBookmark(story);
            UpdateBookmarkVisualState(story);
        }
    }

    private void UpdateBookmarkVisualState(BriefingItem story)
    {
        if (BookmarkButton == null) return;
        var bookmarkService = IPlatformApplication.Current?.Services.GetService<IBookmarkService>();
        if (bookmarkService == null) return;

        bool isSaved = !string.IsNullOrWhiteSpace(story.Url) && bookmarkService.IsBookmarked(story.Url);
        if (isSaved)
        {
            BookmarkButton.BorderColor = Color.FromArgb("#10B981");
            BookmarkButton.BackgroundColor = Color.FromArgb("#1A10B981");
        }
        else
        {
            BookmarkButton.ClearValue(Button.BorderColorProperty);
            BookmarkButton.ClearValue(Button.BackgroundColorProperty);
        }
    }

    private async void OnShareButtonClicked(object? sender, EventArgs e)
    {
        if (BindingContext is BriefingItem story)
        {
            if (ShareCommand?.CanExecute(story) == true)
            {
                ShareCommand.Execute(story);
                return;
            }

            if (ShareClicked != null)
            {
                ShareClicked.Invoke(this, EventArgs.Empty);
                return;
            }

            // Default system share fallback
            var newsstandUrl = NewsStandUrlHelper.GetArticleWebUrl(story);
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = !string.IsNullOrWhiteSpace(story.Title) ? story.Title : "Share News Story",
                Text = $"{story.Title}\n\nRead more on News Stand NG:\n{newsstandUrl}",
                Uri = newsstandUrl
            });
        }
    }

    private async void OnShareOnXButtonClicked(object? sender, EventArgs e)
    {
        if (BindingContext is BriefingItem story)
        {
            if (ShareOnXCommand?.CanExecute(story) == true)
            {
                ShareOnXCommand.Execute(story);
                return;
            }

            if (ShareOnXClicked != null)
            {
                ShareOnXClicked.Invoke(this, EventArgs.Empty);
                return;
            }

            // Default 𝕏 share fallback
            await XShareService.ShareStoryOnXAsync(story);
        }
    }

    private async void OnWhatsAppButtonClicked(object? sender, EventArgs e)
    {
        if (BindingContext is BriefingItem story)
        {
            if (WhatsAppCommand?.CanExecute(story) == true)
            {
                WhatsAppCommand.Execute(story);
                return;
            }

            if (WhatsAppClicked != null)
            {
                WhatsAppClicked.Invoke(this, EventArgs.Empty);
                return;
            }

            var newsstandUrl = NewsStandUrlHelper.GetArticleWebUrl(story);
            var text = $"*{story.Title}*\n\n{story.Summary ?? string.Empty}\n\nRead more on News Stand NG: {newsstandUrl}";
            var encoded = Uri.EscapeDataString(text);
            try
            {
                await Launcher.Default.OpenAsync(new Uri($"https://wa.me/?text={encoded}"));
            }
            catch
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = story.Title,
                    Text = text,
                    Uri = newsstandUrl
                });
            }
        }
    }
}
