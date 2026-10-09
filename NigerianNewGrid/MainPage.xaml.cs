using System.Diagnostics;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Services;
using NigerianNewGrid.ViewModels;

namespace NigerianNewGrid;

/// <summary>
/// Main landing page for News Stand NG.
/// Follows MVVM pattern with minimal code-behind: all business logic, data retrieval,
/// pagination, and state are encapsulated in <see cref="MainViewModel"/>.
/// This code-behind is strictly responsible for view-layer concerns (navigation, system share, onboarding dialog).
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;

    public MainPage(MainViewModel viewModel)
    {
#if DEBUG && ANDROID
        Android.Util.Log.Info("APP_DEBUG", "MainPage constructor START");
#endif
        try
        {
            InitializeComponent();
#if DEBUG && ANDROID
            Android.Util.Log.Info("APP_DEBUG", "MainPage InitializeComponent DONE");
#endif
            _vm = viewModel;
            BindingContext = _vm;
            _vm.ScrollToTopRequested += OnScrollToTopRequested;
#if DEBUG && ANDROID
            Android.Util.Log.Info("APP_DEBUG", "MainPage BindingContext SET");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainPage constructor ERROR: {ex}");
#if DEBUG && ANDROID
            Android.Util.Log.Error("APP_DEBUG", $"MainPage constructor ERROR: {ex}");
#endif
            throw;
        }

        Appearing += async (_, _) =>
        {
#if DEBUG && ANDROID
            Android.Util.Log.Info("APP_DEBUG", "MainPage Appearing FIRED");
#endif
            // Yield to allow the native window to draw the first frame and dismiss the splash screen immediately
            await Task.Yield();

            try
            {
                await _vm.InitializeAsync();
#if DEBUG && ANDROID
                Android.Util.Log.Info("APP_DEBUG", "MainPage _vm.InitializeAsync COMPLETED");
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainPage Appearing ERROR: {ex}");
#if DEBUG && ANDROID
                Android.Util.Log.Error("APP_DEBUG", $"MainPage Appearing ERROR: {ex}");
#endif
            }
        };
#if DEBUG && ANDROID
        Android.Util.Log.Info("APP_DEBUG", "MainPage constructor FINISHED");
#endif
    }

    private async void OnSearchToolbarClicked(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("//discover");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] Search navigation error: {ex.Message}");
        }
    }

    private void OnScrollToTopRequested()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                // Yield briefly to allow MAUI CollectionView to measure newly inserted items before animating scroll
                await Task.Delay(50);
                if (_vm.DateGroups.Count > 0 && _vm.DateGroups[0].Count > 0)
                {
                    FeedCollectionView.ScrollTo(0, groupIndex: 0, position: ScrollToPosition.Start, animate: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] ScrollToTop failed: {ex.Message}");
            }
        });
    }

    private async void OnApplyNewStoriesPillTapped(object? sender, EventArgs e)
    {
        try
        {
            if (_vm.ApplyNewStoriesCommand.CanExecute(null))
            {
                await _vm.ApplyNewStoriesCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] ApplyNewStories error: {ex.Message}");
        }
    }
}
