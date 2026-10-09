using NigerianNewGrid.ViewModels;

namespace NigerianNewGrid;

/// <summary>
/// SettingsPage code-behind (Issue #28).
/// Delegates business logic, state mutations, and API requests to SettingsViewModel via compiled bindings.
/// Strictly handles view-specific visual selection highlights.
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = _vm;

        Appearing += (_, _) =>
        {
            _vm.LoadSettings();
            HighlightRating(_vm.SelectedRating);
            HighlightCategory(_vm.SelectedFeedbackCategory);
        };
    }

    // ──────────────────────────────────────────────────────────
    // Feedback Rating & Category Visual Selection States
    // ──────────────────────────────────────────────────────────

    private void OnStarClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out int rating))
        {
            _vm.SetRating(rating);
            HighlightRating(rating);
        }
    }

    private void HighlightRating(int rating)
    {
        Button[] stars = [Star1, Star2, Star3, Star4, Star5];
        for (int i = 0; i < stars.Length; i++)
        {
            if (i + 1 == rating)
            {
                stars[i].BackgroundColor = GetColor("AccentBlue", Color.FromArgb("#0066FF"));
                stars[i].TextColor = Colors.White;
                stars[i].FontAttributes = FontAttributes.Bold;
            }
            else
            {
                stars[i].BackgroundColor = GetRes<Color>("Gray100", Color.FromArgb("#F1F5F9"));
                stars[i].TextColor = GetRes<Color>("Gray900", Color.FromArgb("#212529"));
                stars[i].FontAttributes = FontAttributes.None;
            }
        }
    }

    private void OnFeedbackCategoryClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string category)
        {
            _vm.SetFeedbackCategory(category);
            HighlightCategory(category);
        }
    }

    private void HighlightCategory(string category)
    {
        Button[] categories = [CatGeneral, CatBug, CatFeature, CatContent];
        foreach (var b in categories)
        {
            bool isSel = string.Equals(b.CommandParameter as string, category, StringComparison.OrdinalIgnoreCase);
            if (isSel)
            {
                b.BackgroundColor = GetColor("AccentBlue", Color.FromArgb("#0066FF"));
                b.TextColor = Colors.White;
                b.FontAttributes = FontAttributes.Bold;
            }
            else
            {
                b.BackgroundColor = GetRes<Color>("Gray100", Color.FromArgb("#F1F5F9"));
                b.TextColor = GetRes<Color>("Gray900", Color.FromArgb("#212529"));
                b.FontAttributes = FontAttributes.None;
            }
        }
    }

    private static Color GetColor(string resourceKey, Color fallbackColor)
    {
        if (Application.Current?.Resources != null &&
            Application.Current.Resources.TryGetValue(resourceKey, out var res))
        {
            if (res is Color color) return color;
            if (res is SolidColorBrush brush) return brush.Color;
        }

        return fallbackColor;
    }

    private static T GetRes<T>(string key, T fallback)
    {
        if (Application.Current?.Resources is { } res &&
            res.TryGetValue(key, out var raw) && raw is T typed)
            return typed;
        return fallback;
    }
}
