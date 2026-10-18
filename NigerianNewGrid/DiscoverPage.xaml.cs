using System.Diagnostics;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Services;
using NigerianNewGrid.ViewModels;

namespace NigerianNewGrid;

/// <summary>
/// Discover tab — search + category filter over 7-day articles and trending YouTube video stories,
/// with full MVVM data binding, compiled XAML data templates, and infinite scroll (Fix #13).
/// </summary>
public partial class DiscoverPage : ContentPage, IQueryAttributable
{
    private readonly DiscoverViewModel _vm;
    private bool _isInitialized;

    public DiscoverPage(
        DiscoverViewModel viewModel,
        IRecentlyReadService? recentlyReadService = null)
    {
        InitializeComponent();
        _vm            = viewModel;
        BindingContext = _vm;

        Appearing += async (_, _) =>
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                await _vm.LoadAsync();
            }
            else if (string.Equals(_vm.SelectedCategory, "RecentlyRead", StringComparison.OrdinalIgnoreCase))
            {
                await _vm.ApplyCategoryFilterAsync("RecentlyRead");
            }
        };
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _vm.ApplyQueryAttributes(query);
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        await _vm.SearchAsync(DiscoverSearchEntry?.Text);
    }

    private async void OnClearSearch(object? sender, EventArgs e)
    {
        DiscoverSearchEntry.Text = string.Empty;
        await _vm.ClearSearchAsync();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _vm.IsClearButtonVisible = !string.IsNullOrWhiteSpace(e.NewTextValue);
    }

    private async void OnLoadMoreClicked(object? sender, EventArgs e)
    {
        await _vm.LoadMoreAsync();
    }


    private async void OnCategoryPillTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string cat) return;
        if (string.Equals(_vm.SelectedCategory, cat, StringComparison.OrdinalIgnoreCase) && !_vm.IsSearchMode) return;

        var pills  = new[] { PillAll, PillRecentlyRead, PillKeywordAlerts, PillOpinions, PillPolitics, PillSports, PillBusiness, PillEntertainment, PillTech, PillInternational };
        var labels = new[] { "All", "RecentlyRead", "KeywordAlerts", "Opinion", "Politics", "Sports", "Business", "Entertainment", "Technology", "International" };

        for (int i = 0; i < pills.Length; i++)
        {
            bool isSelected = string.Equals(labels[i], cat, StringComparison.OrdinalIgnoreCase) ||
                              (cat == "All" && labels[i] == "All");

            if (isSelected)
            {
                pills[i].BackgroundColor = GetRes<Color>("AccentBlue", Color.FromArgb("#0066FF"));
                pills[i].StrokeThickness = 0;
                if (pills[i].Content is Label lbl)
                {
                    lbl.TextColor = Colors.White;
                    lbl.FontAttributes = FontAttributes.Bold;
                }
            }
            else
            {
                pills[i].BackgroundColor = GetRes<Color>("White", Colors.White);
                pills[i].Stroke = GetRes<Brush>("Gray200Brush", new SolidColorBrush(Color.FromArgb("#D5E2D8")));
                pills[i].StrokeThickness = 1;
                if (pills[i].Content is Label lbl)
                {
                    lbl.TextColor = GetRes<Color>("Gray900", Color.FromArgb("#212529"));
                    lbl.FontAttributes = FontAttributes.Bold;
                }
            }
        }

        await _vm.ApplyCategoryFilterAsync(cat);
    }

    private static T GetRes<T>(string key, T fallback)
    {
        if (Application.Current?.Resources is { } res &&
            res.TryGetValue(key, out var raw) && raw is T typed)
            return typed;
        return fallback;
    }
}
