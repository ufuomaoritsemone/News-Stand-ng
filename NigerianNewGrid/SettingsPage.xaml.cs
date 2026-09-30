using System.ComponentModel;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Services;

namespace NigerianNewGrid;

public partial class SettingsPage : ContentPage
{
    private readonly INotificationService _notificationService;
    private readonly NewsApiClient _apiClient;
    private readonly ISonicFeedbackService _sonicService;
    private readonly IBackgroundSyncService _backgroundSyncService;
    private bool _isInitializing = true;
    private int _selectedRating = 5;
    private string _selectedFeedbackCategory = "General";

    private static readonly string[] PresetSuggestions =
    [
        "Naira",
        "Tinubu",
        "EFCC",
        "Fuel Price",
        "Super Eagles",
        "Tech/Startups",
        "CBN"
    ];

    public SettingsPage(
        INotificationService notificationService,
        NewsApiClient apiClient,
        ISonicFeedbackService sonicService,
        IBackgroundSyncService backgroundSyncService)
    {
        InitializeComponent();
        _notificationService = notificationService;
        _apiClient = apiClient;
        _sonicService = sonicService;
        _backgroundSyncService = backgroundSyncService;

        Appearing += (_, _) =>
        {
            LoadNotificationSettings();
        };
    }

    // ──────────────────────────────────────────────────────────
    // Notification & Keyword Alerts Management
    // ──────────────────────────────────────────────────────────

    private void LoadNotificationSettings()
    {
        _isInitializing = true;

        SwitchMorningBriefing.IsToggled = NotificationPreferences.MorningBriefingEnabled;
        MorningTimeContainer.IsVisible = NotificationPreferences.MorningBriefingEnabled;
        TimePickerMorning.Time = NotificationPreferences.MorningBriefingTime;

        SwitchAudioBriefings.IsToggled = NotificationPreferences.AudioBriefingsEnabled;
        AudioBriefingsScheduleContainer.IsVisible = NotificationPreferences.AudioBriefingsEnabled;

        SwitchKeywordAlerts.IsToggled = NotificationPreferences.KeywordAlertsEnabled;
        KeywordAlertsContainer.IsVisible = NotificationPreferences.KeywordAlertsEnabled;

        // Background Updates & Battery saver toggle (default true)
        SwitchBackgroundUpdates.IsToggled = NotificationPreferences.BackgroundUpdatesEnabled;

        // Analytics opt-in (default true — permitted by design)
        SwitchAnalytics.IsToggled = Preferences.Get("analytics_enabled", defaultValue: true);

        // Sonic Feedback opt-in (default true)
        SwitchSonicFeedback.IsToggled = _sonicService.IsEnabled;

        RenderActiveKeywordChips();
        RenderPresetSuggestions();

        _isInitializing = false;
    }

    private void OnBackgroundUpdatesToggled(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing) return;

        NotificationPreferences.BackgroundUpdatesEnabled = e.Value;

        if (e.Value)
        {
            _backgroundSyncService.ScheduleNewsSync(immediate: true);
        }
        else
        {
            _backgroundSyncService.CancelNewsSync();
        }
    }

    private void OnSonicFeedbackToggled(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing) return;
        _sonicService.IsEnabled = e.Value;
    }

    private async void OnTestNewspaperHornClicked(object? sender, EventArgs e)
    {
        await _sonicService.PlayRefreshChimeAsync();
    }

    private async void OnAudioBriefingsToggled(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing) return;

        NotificationPreferences.AudioBriefingsEnabled = e.Value;
        AudioBriefingsScheduleContainer.IsVisible = e.Value;

        if (e.Value)
        {
            var granted = await _notificationService.RequestPermissionAsync();
            if (granted)
            {
                _notificationService.ScheduleAudioBriefings();
            }
        }
        else
        {
            _notificationService.CancelAudioBriefings();
        }
    }

    private async void OnMorningBriefingToggled(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing) return;

        NotificationPreferences.MorningBriefingEnabled = e.Value;
        MorningTimeContainer.IsVisible = e.Value;

        if (e.Value)
        {
            var granted = await _notificationService.RequestPermissionAsync();
            if (granted)
            {
                _notificationService.ScheduleDailyMorningBriefing(NotificationPreferences.MorningBriefingTime);
            }
        }
        else
        {
            _notificationService.CancelDailyMorningBriefing();
        }
    }

    private void OnMorningTimeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isInitializing || e.PropertyName != nameof(TimePicker.Time)) return;

        var picker = sender as TimePicker ?? TimePickerMorning;
        if (picker?.Time is { } time)
        {
            NotificationPreferences.MorningBriefingTime = time;
            if (NotificationPreferences.MorningBriefingEnabled)
            {
                _notificationService.ScheduleDailyMorningBriefing(time);
            }
        }
    }

    private async void OnKeywordAlertsToggled(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing) return;

        NotificationPreferences.KeywordAlertsEnabled = e.Value;
        KeywordAlertsContainer.IsVisible = e.Value;

        if (e.Value)
        {
            await _notificationService.RequestPermissionAsync();
            _backgroundSyncService.ScheduleNewsSync(immediate: true);
        }
        else
        {
            _backgroundSyncService.CancelNewsSync();
        }
    }

    private void RenderActiveKeywordChips()
    {
        ActiveKeywordsFlexLayout.Children.Clear();
        var keywords = NotificationPreferences.MonitoredKeywords;

        LblEmptyKeywords.IsVisible = keywords.Count == 0;

        foreach (var keyword in keywords)
        {
            var chip = CreateActiveKeywordChip(keyword);
            ActiveKeywordsFlexLayout.Children.Add(chip);
        }
    }

    private View CreateActiveKeywordChip(string keyword)
    {
        var border = new Border
        {
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 0, 8, 8),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
            StrokeThickness = 1,
            Stroke = GetBrush("AccentBlueBrush", Color.FromArgb("#0066FF")),
            Background = GetBrush("AccentBlueLightBrush", Color.FromArgb("#EBF3FF"))
        };

        var stack = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center
        };

        var label = new Label
        {
            Text = keyword,
            FontFamily = "LegacySansBook",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetColor("AccentBlue", Color.FromArgb("#0066FF")),
            VerticalOptions = LayoutOptions.Center
        };

        var deleteBtn = new Label
        {
            Text = "✕",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetColor("Gray500", Color.FromArgb("#6C757D")),
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(4, 0, 0, 0)
        };

        var tapRecognizer = new TapGestureRecognizer();
        tapRecognizer.Tapped += (_, _) =>
        {
            NotificationPreferences.RemoveKeyword(keyword);
            RenderActiveKeywordChips();
            RenderPresetSuggestions();
        };

        deleteBtn.GestureRecognizers.Add(tapRecognizer);

        stack.Children.Add(label);
        stack.Children.Add(deleteBtn);
        border.Content = stack;

        return border;
    }

    private void RenderPresetSuggestions()
    {
        PresetSuggestionsFlexLayout.Children.Clear();
        var activeKeywords = NotificationPreferences.MonitoredKeywords;

        var availablePresets = PresetSuggestions
            .Where(p => !activeKeywords.Any(k => string.Equals(k, p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (availablePresets.Count == 0)
            return;

        foreach (var preset in availablePresets)
        {
            var btn = new Border
            {
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 6, 6),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(14) },
                StrokeThickness = 1,
                Stroke = GetBrush("Gray200Brush", Color.FromArgb("#D5E2D8")),
                Background = GetBrush("WhiteBrush", Colors.White)
            };

            var label = new Label
            {
                Text = $"+ {preset}",
                FontFamily = "LegacySansBook",
                FontSize = 12,
                TextColor = GetColor("Gray700Brush", Color.FromArgb("#334155")),
                VerticalOptions = LayoutOptions.Center
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                NotificationPreferences.AddKeyword(preset);
                RenderActiveKeywordChips();
                RenderPresetSuggestions();
            };

            btn.GestureRecognizers.Add(tap);
            btn.Content = label;

            PresetSuggestionsFlexLayout.Children.Add(btn);
        }
    }

    private void OnAddKeywordClicked(object? sender, EventArgs e)
    {
        var input = TxtNewKeyword.Text?.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        if (NotificationPreferences.AddKeyword(input))
        {
            TxtNewKeyword.Text = string.Empty;
            RenderActiveKeywordChips();
            RenderPresetSuggestions();
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

    private static Brush GetBrush(string resourceKey, Color fallbackColor)
    {
        if (Application.Current?.Resources != null &&
            Application.Current.Resources.TryGetValue(resourceKey, out var res) &&
            res is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallbackColor);
    }

    // ──────────────────────────────────────────────────────────
    // Privacy & Analytics
    // ──────────────────────────────────────────────────────────

    private void OnAnalyticsToggled(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing) return;
        Preferences.Set("analytics_enabled", e.Value);
    }

    // ──────────────────────────────────────────────────────────
    // Feedback & Support Submission
    // ──────────────────────────────────────────────────────────

    private void OnStarClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out int rating))
        {
            _selectedRating = rating;
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
    }

    private void OnFeedbackCategoryClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string category)
        {
            _selectedFeedbackCategory = category;
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
    }

    private async void OnSubmitFeedbackClicked(object? sender, EventArgs e)
    {
        var msg = TxtFeedbackMessage.Text?.Trim();
        if (string.IsNullOrWhiteSpace(msg) || msg.Length < 3)
        {
            LblFeedbackStatus.Text = "⚠️ Please type a message with at least 3 characters.";
            LblFeedbackStatus.TextColor = Colors.Red;
            LblFeedbackStatus.IsVisible = true;
            return;
        }

        try
        {
            BtnSubmitFeedback.IsEnabled = false;
            FeedbackSpinner.IsVisible = true;
            FeedbackSpinner.IsRunning = true;
            LblFeedbackStatus.IsVisible = false;

            var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
            _apiClient.BaseUrl = baseUrl;

            var req = new FeedbackRequestDto(
                Rating: _selectedRating,
                Category: _selectedFeedbackCategory,
                Message: msg,
                UserEmail: TxtFeedbackEmail.Text?.Trim(),
                AppVersion: "1.0.0",
                Platform: DeviceInfo.Platform.ToString()
            );

            var resp = await _apiClient.SubmitFeedbackAsync(req);
            FeedbackSpinner.IsRunning = false;
            FeedbackSpinner.IsVisible = false;

            if (resp.Success)
            {
                LblFeedbackStatus.Text = $"✅ {resp.Message}";
                LblFeedbackStatus.TextColor = GetRes<Color>("AccentGreen", Color.FromArgb("#16A34A"));
                LblFeedbackStatus.IsVisible = true;
                TxtFeedbackMessage.Text = string.Empty;
                SemanticScreenReader.Announce("Feedback submitted successfully. Thank you!");
            }
            else
            {
                LblFeedbackStatus.Text = $"❌ {resp.Message}";
                LblFeedbackStatus.TextColor = Colors.Red;
                LblFeedbackStatus.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            FeedbackSpinner.IsRunning = false;
            FeedbackSpinner.IsVisible = false;
            LblFeedbackStatus.Text = $"❌ Error sending feedback: {ex.Message}";
            LblFeedbackStatus.TextColor = Colors.Red;
            LblFeedbackStatus.IsVisible = true;
        }
        finally
        {
            BtnSubmitFeedback.IsEnabled = true;
        }
    }

    private static T GetRes<T>(string key, T fallback)
    {
        if (Application.Current?.Resources is { } res &&
            res.TryGetValue(key, out var raw) && raw is T typed)
            return typed;
        return fallback;
    }
}

