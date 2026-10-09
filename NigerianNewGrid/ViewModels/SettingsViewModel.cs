using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Accessibility;
using Microsoft.Maui.Graphics;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Extensions;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.ViewModels;

/// <summary>
/// ViewModel for SettingsPage (Issue #28).
/// Encapsulates notification preferences, keyword alerts, sonic feedback, and feedback submission.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IUserPreferencesService _userPreferences;
    private readonly INotificationService _notificationService;
    private readonly NewsApiClient _apiClient;
    private readonly ISonicFeedbackService _sonicService;
    private readonly IBackgroundSyncService _backgroundSyncService;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _isLoadingSettings;

    private static readonly string[] DefaultPresets =
    [
        "Naira",
        "Tinubu",
        "EFCC",
        "Fuel Price",
        "Super Eagles",
        "Tech/Startups",
        "CBN"
    ];

    [ObservableProperty] public partial bool MorningBriefingEnabled { get; set; }
    [ObservableProperty] public partial TimeSpan MorningBriefingTime { get; set; }
    [ObservableProperty] public partial bool AudioBriefingsEnabled { get; set; }
    [ObservableProperty] public partial bool KeywordAlertsEnabled { get; set; }
    [ObservableProperty] public partial bool BackgroundUpdatesEnabled { get; set; }
    [ObservableProperty] public partial bool AnalyticsEnabled { get; set; }
    [ObservableProperty] public partial bool SonicFeedbackEnabled { get; set; }

    [ObservableProperty] public partial ObservableCollection<string> MonitoredKeywords { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<string> AvailablePresets { get; set; } = [];
    [ObservableProperty] public partial string NewKeywordInput { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasNoKeywords { get; set; }

    // Feedback state
    [ObservableProperty] public partial int SelectedRating { get; set; } = 5;
    [ObservableProperty] public partial string SelectedFeedbackCategory { get; set; } = "General";
    [ObservableProperty] public partial string FeedbackMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial string FeedbackEmail { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsSubmittingFeedback { get; set; }
    [ObservableProperty] public partial string FeedbackStatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsFeedbackStatusVisible { get; set; }
    [ObservableProperty] public partial bool IsFeedbackStatusSuccess { get; set; }

    public bool CanSubmitFeedback => !IsSubmittingFeedback;

    public Color FeedbackStatusColor => IsFeedbackStatusSuccess
        ? Color.FromArgb("#16A34A")
        : Color.FromArgb("#DC2626");

    partial void OnIsSubmittingFeedbackChanged(bool value) => OnPropertyChanged(nameof(CanSubmitFeedback));
    partial void OnIsFeedbackStatusSuccessChanged(bool value) => OnPropertyChanged(nameof(FeedbackStatusColor));

    partial void OnMorningBriefingEnabledChanged(bool value)
    {
        if (_isLoadingSettings) return;
        SetMorningBriefingEnabledAsync(value).SafeFireAndForget(_logger, nameof(OnMorningBriefingEnabledChanged));
    }

    partial void OnMorningBriefingTimeChanged(TimeSpan value)
    {
        if (_isLoadingSettings) return;
        SetMorningBriefingTime(value);
    }

    partial void OnAudioBriefingsEnabledChanged(bool value)
    {
        if (_isLoadingSettings) return;
        SetAudioBriefingsEnabledAsync(value).SafeFireAndForget(_logger, nameof(OnAudioBriefingsEnabledChanged));
    }

    partial void OnKeywordAlertsEnabledChanged(bool value)
    {
        if (_isLoadingSettings) return;
        SetKeywordAlertsEnabledAsync(value).SafeFireAndForget(_logger, nameof(OnKeywordAlertsEnabledChanged));
    }

    partial void OnBackgroundUpdatesEnabledChanged(bool value)
    {
        if (_isLoadingSettings) return;
        SetBackgroundUpdatesEnabled(value);
    }

    partial void OnSonicFeedbackEnabledChanged(bool value)
    {
        if (_isLoadingSettings) return;
        SetSonicFeedbackEnabled(value);
    }

    partial void OnAnalyticsEnabledChanged(bool value)
    {
        if (_isLoadingSettings) return;
        SetAnalyticsEnabled(value);
    }

    public event Action? KeywordsChanged;

    public SettingsViewModel(
        IUserPreferencesService userPreferences,
        INotificationService notificationService,
        NewsApiClient apiClient,
        ISonicFeedbackService sonicService,
        IBackgroundSyncService backgroundSyncService,
        ILogger<SettingsViewModel> logger)
    {
        _userPreferences = userPreferences;
        _notificationService = notificationService;
        _apiClient = apiClient;
        _sonicService = sonicService;
        _backgroundSyncService = backgroundSyncService;
        _logger = logger;
    }

    public void LoadSettings()
    {
        _isLoadingSettings = true;
        try
        {
            MorningBriefingEnabled   = _userPreferences.MorningBriefingEnabled;
            MorningBriefingTime      = _userPreferences.MorningBriefingTime;
            AudioBriefingsEnabled    = _userPreferences.AudioBriefingsEnabled;
            KeywordAlertsEnabled     = _userPreferences.KeywordAlertsEnabled;
            BackgroundUpdatesEnabled = _userPreferences.BackgroundUpdatesEnabled;
            AnalyticsEnabled         = Preferences.Get("analytics_enabled", defaultValue: true);
            SonicFeedbackEnabled     = _sonicService.IsEnabled;

            RefreshKeywordCollections();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    public void RefreshKeywordCollections()
    {
        var active = _userPreferences.MonitoredKeywords;
        MonitoredKeywords.Clear();
        foreach (var kw in active)
        {
            MonitoredKeywords.Add(kw);
        }

        HasNoKeywords = MonitoredKeywords.Count == 0;

        var available = DefaultPresets
            .Where(p => !active.Any(k => string.Equals(k, p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        AvailablePresets.Clear();
        foreach (var preset in available)
        {
            AvailablePresets.Add(preset);
        }

        KeywordsChanged?.Invoke();
    }

    [RelayCommand]
    public async Task SetMorningBriefingEnabledAsync(bool enabled)
    {
        MorningBriefingEnabled = enabled;
        _userPreferences.MorningBriefingEnabled = enabled;

        if (enabled)
        {
            var granted = await _notificationService.RequestPermissionAsync();
            if (granted)
            {
                _notificationService.ScheduleDailyMorningBriefing(_userPreferences.MorningBriefingTime);
            }
        }
        else
        {
            _notificationService.CancelDailyMorningBriefing();
        }
    }

    [RelayCommand]
    public void SetMorningBriefingTime(TimeSpan time)
    {
        MorningBriefingTime = time;
        _userPreferences.MorningBriefingTime = time;
        if (_userPreferences.MorningBriefingEnabled)
        {
            _notificationService.ScheduleDailyMorningBriefing(time);
        }
    }

    [RelayCommand]
    public async Task SetAudioBriefingsEnabledAsync(bool enabled)
    {
        AudioBriefingsEnabled = enabled;
        _userPreferences.AudioBriefingsEnabled = enabled;

        if (enabled)
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

    [RelayCommand]
    public async Task SetKeywordAlertsEnabledAsync(bool enabled)
    {
        KeywordAlertsEnabled = enabled;
        _userPreferences.KeywordAlertsEnabled = enabled;

        if (enabled)
        {
            await _notificationService.RequestPermissionAsync();
            _backgroundSyncService.ScheduleNewsSync(immediate: true);
        }
        else
        {
            _backgroundSyncService.CancelNewsSync();
        }
    }

    [RelayCommand]
    public void SetBackgroundUpdatesEnabled(bool enabled)
    {
        BackgroundUpdatesEnabled = enabled;
        _userPreferences.BackgroundUpdatesEnabled = enabled;

        if (enabled)
        {
            _backgroundSyncService.ScheduleNewsSync(immediate: true);
        }
        else
        {
            _backgroundSyncService.CancelNewsSync();
        }
    }

    [RelayCommand]
    public void SetSonicFeedbackEnabled(bool enabled)
    {
        SonicFeedbackEnabled = enabled;
        _sonicService.IsEnabled = enabled;
    }

    [RelayCommand]
    public void SetAnalyticsEnabled(bool enabled)
    {
        AnalyticsEnabled = enabled;
        Preferences.Set("analytics_enabled", enabled);
    }

    [RelayCommand]
    public async Task PlayRefreshChimeAsync()
    {
        await _sonicService.PlayRefreshChimeAsync();
    }

    [RelayCommand]
    public void AddKeyword(string? keyword = null)
    {
        var target = (keyword ?? NewKeywordInput)?.Trim();
        if (string.IsNullOrWhiteSpace(target)) return;

        if (_userPreferences.AddKeyword(target))
        {
            NewKeywordInput = string.Empty;
            RefreshKeywordCollections();
        }
    }

    [RelayCommand]
    public void RemoveKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return;

        if (_userPreferences.RemoveKeyword(keyword))
        {
            RefreshKeywordCollections();
        }
    }

    [RelayCommand]
    public void SetRating(int rating)
    {
        SelectedRating = rating;
    }

    [RelayCommand]
    public void SetFeedbackCategory(string category)
    {
        SelectedFeedbackCategory = category;
    }

    [RelayCommand]
    public async Task SubmitFeedbackAsync()
    {
        var msg = FeedbackMessage?.Trim();
        if (string.IsNullOrWhiteSpace(msg) || msg.Length < 3)
        {
            FeedbackStatusText = "⚠️ Please type a message with at least 3 characters.";
            IsFeedbackStatusSuccess = false;
            IsFeedbackStatusVisible = true;
            return;
        }

        try
        {
            IsSubmittingFeedback = true;
            IsFeedbackStatusVisible = false;

            var baseUrl = AppNetworkConstants.GetConfiguredApiBaseUrl();
            _apiClient.BaseUrl = baseUrl;

            var req = new FeedbackRequestDto(
                Rating: SelectedRating,
                Category: SelectedFeedbackCategory,
                Message: msg,
                UserEmail: FeedbackEmail?.Trim(),
                AppVersion: "1.0.0",
                Platform: DeviceInfo.Platform.ToString()
            );

            var resp = await _apiClient.SubmitFeedbackAsync(req);
            if (resp.Success)
            {
                FeedbackStatusText = $"✅ {resp.Message}";
                IsFeedbackStatusSuccess = true;
                IsFeedbackStatusVisible = true;
                FeedbackMessage = string.Empty;
                SemanticScreenReader.Announce("Feedback submitted successfully. Thank you!");
            }
            else
            {
                FeedbackStatusText = $"❌ {resp.Message}";
                IsFeedbackStatusSuccess = false;
                IsFeedbackStatusVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting feedback.");
            FeedbackStatusText = $"❌ Error sending feedback: {ex.Message}";
            IsFeedbackStatusSuccess = false;
            IsFeedbackStatusVisible = true;
        }
        finally
        {
            IsSubmittingFeedback = false;
        }
    }
}
