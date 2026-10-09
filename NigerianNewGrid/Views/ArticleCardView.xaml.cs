using System.Windows.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using NigerianNewGrid.Services;
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

    private async void OnCardTapped(object? sender, EventArgs e)
    {
        if (BindingContext is BriefingItem story)
        {
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
                return;
            }

            BookmarkClicked?.Invoke(this, EventArgs.Empty);
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
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Share News Story",
                Text = $"{story.Title}\n\nRead more on News Stand NG:\n{story.Url}",
                Uri = !string.IsNullOrWhiteSpace(story.Url) ? story.Url : null
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

            var text = $"*{story.Title}*\n\n{story.Summary ?? string.Empty}\n\nRead more on News Stand NG: {story.Url}";
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
                    Uri = story.Url
                });
            }
        }
    }
}
