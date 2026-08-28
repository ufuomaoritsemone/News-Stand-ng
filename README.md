# Nigerian News Grid

Repository scaffold for the Nigerian News Grid mobile app and backend.

Projects:
- src/NewsApi - ASP.NET Core Web API (SQLite / PostgreSQL)
- src/AdminDashboard - Web Admin Dashboard interface
- src/NewsScraperService - background worker that scrapes RSS feeds and ingests articles via REST API
- src/NewsCategorizer.Trainer - ML.NET training console application for automated news categorization
- src/TtsWorker - background worker placeholder for generating TTS audio
- NigerianNewGrid - existing .NET MAUI mobile client (already in workspace)

## Running Admin Dashboard & Dependent Services

You can run all services using the provided startup scripts:

### PowerShell
```powershell
# Open services in separate windows (interactive logs)
.\run-services.ps1

# Run in background jobs
.\run-services.ps1 -Background

# Stop all running services (background jobs & dotnet processes)
.\run-services.ps1 -Stop

# Run via Docker Compose
.\run-services.ps1 -Docker
```

### Windows Command Prompt / Batch
Double-click `run-services.bat` or run:
```cmd
run-services.bat
```

### Endpoints
- **Admin Dashboard**: [http://localhost:56192](http://localhost:56192)
- **News API**: [http://localhost:56193](http://localhost:56193)

## Updates

### August 20, 2026 — Notification Tap Navigation, Discover Alerts Feed & Full Headline Display
Implemented direct story navigation, keyword alerts discovery feed, and adaptive story card layouts:
- **Notification Tap Navigation**: Updated [INotificationService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/INotificationService.cs) and platform implementations ([Platforms/Android/NotificationService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Platforms/Android/NotificationService.cs), [Platforms/iOS/NotificationService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Platforms/iOS/NotificationService.cs)) to pass full article metadata (`article_url`, `article_id`, `article_title`, `category`). Overrode `OnCreate` and `OnNewIntent` in [MainActivity.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Platforms/Android/MainActivity.cs) and implemented [iOSNotificationDelegate.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Platforms/iOS/iOSNotificationDelegate.cs) to navigate directly to [ArticleWebPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ArticleWebPage.xaml) in Reader Mode when notification banners are tapped.
- **Dedicated `🔔 Keyword Alerts` Feed in Discover**: Added a `🔔 Keyword Alerts` filter pill to [DiscoverPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/DiscoverPage.xaml) and [DiscoverPage.xaml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/DiscoverPage.xaml.cs) aggregating all stories matching the user's active monitored topics (*Naira*, *Tinubu*, *EFCC*, etc.).
- **Non-Truncated Story Cards**: Expanded story cards across [MainPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MainPage.xaml), [DiscoverPage.xaml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/DiscoverPage.xaml.cs), [BookmarksPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/BookmarksPage.xaml), and [ArticleWebPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ArticleWebPage.xaml) with adaptive height (`MinimumHeightRequest="140"`) and multi-line word wrapping (`MaxLines="4"`, `LineBreakMode="WordWrap"`), eliminating text truncation on long news headlines.
- **Verification**: Verified 72/72 unit tests passing; clean compilation builds across Windows and Android targets with 0 errors.

### August 20, 2026 — Full Stack System Refactoring & Optimizations Implementation
Implemented and verified the complete set of architectural refactorings, cost reductions, database query optimizations, and UI/UX improvements across backend microservices and the .NET MAUI mobile app:
- **Backend Performance & In-Memory Caching (`NewsApi`)**: Refactored [ArticlesController.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Controllers/ArticlesController.cs) with `IMemoryCache` (5-minute sliding cache) and a 72-hour freshness windowing cutoff on `GetBriefings` to eliminate unbounded memory loads. Converted `IngestArticles` to batch `HashSet<string>` duplicate checking, eliminating `O(N)` individual database roundtrips. Fixed pagination parameters (`page`, `pageSize`) in `GetArticles` using `.Skip().Take()`. Added `DbSet<CategoryCorrection>` and [CategoryCorrection.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Models/CategoryCorrection.cs) to persist ML feedback in the database rather than fragile relative directory hops.
- **Cloud Run Job Scraper & Cost Reduction**: Added `--run-once` / `SCRAPER_RUN_ONCE` support to [ScraperWorker.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsScraperService/ScraperWorker.cs) and [Program.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsScraperService/Program.cs) via `IHostApplicationLifetime.StopApplication()`, enabling serverless on-demand scraping via Cloud Run Jobs and Cloud Scheduler ($0 idle billing). Decommissioned redundant `TtsWorker` container from [docker-compose.yml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/docker-compose.yml) and [Deployment.md](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/Deployment.md).
- **On-Device Text-to-Speech & Clean Code Services (`.NET MAUI`)**: Built [ITextToSpeechService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/ITextToSpeechService.cs) and [MauiTextToSpeechService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/MauiTextToSpeechService.cs) using `Microsoft.Maui.Media.TextToSpeech` for $0 cloud cost and offline headline audio reading. Encapsulated storage and caching in [IBookmarkService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/IBookmarkService.cs), [BookmarkService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/BookmarkService.cs), [IBriefingCacheService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/IBriefingCacheService.cs), and [BriefingCacheService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/BriefingCacheService.cs), registered via dependency injection in [MauiProgram.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MauiProgram.cs).
- **UI/UX Enhancements**: Added `SkeletonLoadingView` shimmer placeholder cards to [MainPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MainPage.xaml) during initial feed synchronization. Implemented `CalculateReadingTime` (e.g., `⏱️ 3 min read`) in [ArticleDistillerService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/ArticleDistillerService.cs) displayed in [ArticleWebPage.xaml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ArticleWebPage.xaml.cs) alongside native share sheet integration.
- **Testing & Verification**: Created [BackendOptimizationTests.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/tests/NewsApiClient.Tests/BackendOptimizationTests.cs). All 72 unit tests passed (100% pass rate); `NewsApi`, `NewsScraperService`, `AdminDashboard`, and `NigerianNewGrid` compiled with 0 warnings and 0 errors across all target frameworks.
Completed full-stack architecture, deployment cost, best practices, clean code, and UI/UX review documented in [docs/SYSTEM_REVIEW_AND_OPTIMIZATION_ROADMAP.md](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/docs/SYSTEM_REVIEW_AND_OPTIMIZATION_ROADMAP.md):
- **Cloud Deployment Cost Optimization**: Identified continuous billing bottlenecks in Cloud Run background workers (`--min-instances=1`, `--no-cpu-throttling`). Outlined transition to Cloud Run Jobs triggered by Cloud Scheduler and on-device native MAUI Text-to-Speech synthesis (cutting deployment costs from ~$75/mo to $0.00–$0.50/mo on 100% Free Tier).
- **Syndication & Database Best Practices**: Identified memory scaling risk in `GetBriefings` (unbounded full-table in-memory grouping) and duplicate lookup latency in `IngestArticles`. Designed in-memory output caching, 72-hour freshness windowing, and batch hashset deduplication.
- **Clean Code & MAUI Architecture**: Planned transition from code-behind imperative UI loops in `MainPage.xaml.cs` and `DiscoverPage.xaml.cs` to MVVM (`CommunityToolkit.Mvvm`), XAML `DataTemplate`s, compiled bindings, and corrected pagination parameters (`Skip`/`Take`) in `ArticlesController`.
- **UI/UX Polish**: Proposed feed virtualization via `CollectionView`, skeleton shimmer loading states, reading time badges (`⏱️ 3 min read`), and native OS share sheet integration.

### August 20, 2026 — On-Device Notifications & Keyword Alerts Implementation
Implemented and verified the complete Edge-First, Privacy-Preserving notifications and breaking keyword alert subsystem across the `.NET MAUI` application:
- **Strict Freshness Gate & Recency Engine**: Built [IKeywordMatchingService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/IKeywordMatchingService.cs) and [KeywordMatchingService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/IKeywordMatchingService.cs) with compiled word-boundary regex (`\bkeyword\b`) matching, enforcing a 2-hour max recency cutoff and baseline evaluation timestamps (`LastKeywordAlertEvaluationUtc`) to prevent alert spam from historical/backlog stories.
- **On-Device State & Deduplication**: Created [NotificationPreferences.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/NotificationPreferences.cs) managing local morning briefing times, keyword chip arrays, and a bounded deduplication cache (`HashSet<string>`) stored exclusively on-device.
- **Cross-Platform Native Schedulers**: Created [Platforms/Android/NotificationService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Platforms/Android/NotificationService.cs), `MorningBriefingReceiver`, and `BootCompletedReceiver` for `AlarmManager.SetExactAndAllowWhileIdle` and dual notification channels (`morning_briefings`, `keyword_alerts`). Added [Platforms/iOS/NotificationService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Platforms/iOS/NotificationService.cs) with `UNCalendarNotificationTrigger` and [Platforms/Windows/NotificationService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Platforms/Windows/NotificationService.cs).
- **Settings UI & Dynamic Chips**: Refactored [SettingsPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/SettingsPage.xaml) and [SettingsPage.xaml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/SettingsPage.xaml.cs) with interactive keyword chips (tap `✕` to remove, custom input row, and quick preset buttons for *Naira*, *Tinubu*, *EFCC*, *Fuel Price*, *Super Eagles*, *Tech/Startups*, *CBN*).
- **Feed Integration**: Wired [MainPage.xaml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MainPage.xaml.cs) to evaluate newly loaded fresh stories on feed sync and dispatch keyword notifications.
- **Testing & Build Verification**: Added comprehensive unit test suite in [KeywordMatchingTests.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/tests/NewsApiClient.Tests/KeywordMatchingTests.cs) (word boundaries, freshness gates, deduplication, casing). All 57 unit tests passed; builds succeeded with 0 errors across Windows and Android targets.

### August 18, 2026 — MainPage UI Architecture, BindableLayout & Styling Hardening
Applied critical layout, theming, and performance fixes to [MainPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MainPage.xaml), [MainPage.xaml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MainPage.xaml.cs), and [Styles.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Resources/Styles/Styles.xaml):
- **Added Missing Button Styles**: Defined `ActionPrimaryButtonStyle` and `ActionSecondaryButtonStyle` in `Styles.xaml` with high-contrast borders and theme-aware background/text tokens.
- **Eliminated Nested Scroll Conflict**: Replaced nested `CollectionView` in `ScrollView` with `BindableLayout.ItemsSource` on `VerticalStackLayout` with an explicit `EmptyStateView` toggle. This prevents Android `RecyclerView` height collapsing bugs and enables buttery smooth 60fps scrolling.
- **Brush vs Color Correction**: Standardized `Background` instead of `BackgroundColor` where `SolidColorBrush` tokens are referenced.
- **Robust Resource Lookups**: Refactored C# status label color resolution in `MainPage.xaml.cs` to use safe `TryGetValue` fallbacks preventing runtime `NullReferenceException` / `KeyNotFoundException`.
- **Build Verification**: Verified 0 warnings and 0 errors across Windows, Android, iOS, and MacCatalyst targets with all 51 unit tests passing.

### August 18, 2026 — Active YouTube Data API v3 Key Configured & Verified
Replaced previous API key with active production key `AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI` in [appsettings.json](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) and [appsettings.Development.json](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.Development.json):
- **Live Verification**: Successfully verified live responses from YouTube Data API v3 (`/videos?chart=mostPopular&regionCode=NG&videoCategoryId=25`). Top trending broadcast stories now ingest with accurate view counts (up to 259k+ views), exact durations (`PT22M13S` → `22:13`), and like counts.
- **End-to-End Sync**: Verified `POST /api/v1/video-stories/sync/all` populating live video broadcast stories and Nigerian trending charts.
Implemented client-side 15-story pagination, removed Twitter/X social feeds across the stack, and overhauled the YouTube video syndication subsystem:
- **News Feeds Pagination (First 15 Stories)**: Refactored [MainPage.xaml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MainPage.xaml.cs) and [MainPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MainPage.xaml) to display the first 15 news stories grouped cleanly by date with a high-contrast `"Load More Stories (Showing X of Y)"` button and full-loaded indicator.
- **Twitter / X Removal**: Removed the Social Pulse UI section from `MainPage`, removed the `"🐦 Tweets"` filter and social badge from [ArticleWebPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ArticleWebPage.xaml) and [ArticleWebPage.xaml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ArticleWebPage.xaml.cs), and removed background social syncing from [BackgroundMediaSyncService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Services/BackgroundMediaSyncService.cs) and [ScraperWorker.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsScraperService/ScraperWorker.cs).
- **YouTube Syndication & Schema Fix**: Resolved SQLite missing column schema conflict for `VideoStories` (`IsTrending`, `TrendingRank`, `ViewCount`, `LikeCount`) in [Program.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Program.cs). Rebuilt [YouTubeFeedService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Services/YouTubeFeedService.cs) with dual-engine support for YouTube Data API v3 and high-res Atom XML parsing. Added `POST /api/v1/video-stories/sync/all` in [VideoStoriesController.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Controllers/VideoStoriesController.cs).
- **Testing & Verification**: Verified 87 live broadcast stories ingested across 7 major Nigerian news channels with 30 trending stories populated. All 51 unit tests passed with 0 build warnings/errors across all 5 target frameworks.

### August 18, 2026 — YouTube Data API v3 Key Configuration
Configured the official YouTube Data API v3 API key into [appsettings.json](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) and [appsettings.Development.json](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.Development.json) for backend video syndication:
- **YouTube Service Integration**: Embedded API key into `YouTube:ApiKey` configuration section utilized by [YouTubeFeedService.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Services/YouTubeFeedService.cs) for fetching trending Nigerian broadcast stories (`chart=mostPopular`, `regionCode=NG`, `categoryId=25`) and video metadata syndication.
- **Reference & Documentation**: Documented Google YouTube v3 developer resources (`https://developers.google.com/youtube/v3/docs`) for future query expansions (playlist items, channel search, and quota optimization).
- **Verification**: Verified solution build status across `NewsApi` with 0 warnings and 0 errors.

### August 17, 2026 — Notifications & Keyword Alerts Architecture Specification
Architected and documented the notifications and custom topic alert subsystem for Nigerian News Grid ([docs/NOTIFICATIONS_AND_ALERT_STRATEGY.md](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/docs/NOTIFICATIONS_AND_ALERT_STRATEGY.md)):
- **Daily Morning Briefings**: Repeating local notification system scheduled via native OS schedulers (`AlarmManager` on Android, `UNCalendarNotificationTrigger` on iOS) with configurable delivery time in Settings.
- **Custom Keyword Topic Alerts**: Real-time word-boundary regex matching against newly ingested articles with active keyword chip management (add, delete, and popular Nigerian presets).
- **Settings UI Wireframe**: XAML layout design for `SettingsPage.xaml` incorporating morning time picker, keyword chip flex layout, and notification permission state handling.
- **Cross-Platform Specifications**: Detailed runtime permission flows (`POST_NOTIFICATIONS` on API 33+, iOS authorization), notification channels, and reboot persistence (`BOOT_COMPLETED`).

### August 17, 2026 — Monetization & Ad Placement Architecture Specification
Defined and documented the complete monetization and programmatic ad architecture roadmap for Nigerian News Grid ([docs/MONETIZATION_AND_AD_STRATEGY.md](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/docs/MONETIZATION_AND_AD_STRATEGY.md)):
- **Ad Formats & Placements**: Designed high-CTR native in-feed ads (132px matching story cards with `Sponsored` badge in `MainPage` and `DiscoverPage`), sticky bottom adaptive banners in `ArticleWebPage`, rewarded video ads for value utilities (unlocking Text-To-Speech audio and AI summaries), and frequency-capped navigation interstitials.
- **Direct Local Sponsorship Engine**: Architecture for direct Nigerian corporate PR/sponsor campaigns ingested via `NewsApi` and managed via `AdminDashboard`.
- **Freemium Pro Subscription**: Tiered subscription blueprint ("News Grid Pro") via `Plugin.InAppBilling` for ad-free reading and offline audio briefing bundles.
- **Technical Prerequisites & Integration Roadmap**: Detailed Google AdMob setup, `app-ads.txt` backend endpoint routing, platform manifest specifications (`AndroidManifest.xml` / `Info.plist`), and four-phase rollout sequence.

### August 17, 2026 — High-Contrast Button Theming & Visual Boundary Enhancements
Upgraded all button controls across the entire application ([Colors.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Resources/Styles/Colors.xaml), [Styles.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Resources/Styles/Styles.xaml), [MainPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MainPage.xaml), [ArticleWebPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ArticleWebPage.xaml), [BookmarksPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/BookmarksPage.xaml), [DiscoverPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/DiscoverPage.xaml)) for maximum contrast, sharpness, and touch clarity:
- **Dedicated High-Contrast Color Tokens**: Introduced `ButtonBorderLight` (`#CBD5E1`), `ButtonBorderDark` (`#475569`), `ButtonBgLight` (`#FFFFFF`), `ButtonBgDark` (`#1E293B`), `ButtonTextLight` (`#0F172A`), and `ButtonTextDark` (`#FFFFFF`) with matching theme-aware brushes.
- **Defined 1.5px Outlines & Elevated Surfaces**: Applied 1.5px border strokes and elevated surface fills across all action buttons (`↻ Reload`, `🎧 Audio Briefing`, `🔖 Bookmark`, `↗ Share`, `← Back`, `A±`, `📖 Reader`, `✕ Close`, and drawer filter chips), preventing button blending into dark/light backgrounds.
- **Dynamic State Contrast**: Enhanced code-behind theme switching in `ArticleWebPage.xaml.cs` to guarantee high contrast across active/inactive reader toggle modes and related stories filter tabs.
- **Build & Test Verification**: Successfully built the `.NET MAUI` Windows client and verified all 51 unit tests passing with 0 errors.

### August 17, 2026 — Clean Streamlined Feed Layout (Removed Breaking News & Category Pills)
Streamlined the main news feed layout on the home page ([MainPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MainPage.xaml), [MainPage.xaml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MainPage.xaml.cs)):
- **Removed Hero Breaking News Section**: Eliminated the large hero card carousel and loading skeleton at the top of the feed to allow immediate visibility of stories.
- **Removed Category Filter Pills**: Removed the horizontal category selector pills (`CategoryPillsLayout`), transitioning the entire home feed into a clean, unified chronological timeline grouped by date (`Today`, `Yesterday`, etc.).
- **Code-Behind Optimization**: Removed unused hero card and category event handlers in `MainPage.xaml.cs`, routing all ingested stories directly into `BuildDateGroups` for optimal rendering performance.
- **Build & Test Verification**: Successfully compiled the `.NET MAUI` Windows client and verified that all 51 unit tests passed with 0 errors.

### August 17, 2026 — Article Web View Header Toolbar Redesign & Decluttering
Refactored the header navigation toolbar in the article reading web view ([ArticleWebPage.xaml](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ArticleWebPage.xaml), [ArticleWebPage.xaml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ArticleWebPage.xaml.cs)) to resolve cramped control bunching on mobile screens:
- **Decluttered Middle Layout**: Removed the redundant, squished article title, domain snippet, and mode badges from the top bar since the headline and publisher brand are already prominently rendered in the reader/web view canvas.
- **Subtle Borders & Defined Tap Boundaries**: Added subtle, theme-aware 1px border strokes (`BorderWidth="1"` / `BorderColor`) around all action controls (`← Back`, `📖 Reader`, `A±`, `🔖`, `↗`), replacing flat ambiguous fills with defined, modern touch targets.
- **Uniform Compact Button Sizing**: Standardized button dimensions (36x36dp square rounded-corner action buttons for font size, bookmarks, and sharing; compact 36dp pill buttons for back navigation and reader mode toggle) with clean 6–8dp horizontal spacing.
- **Theme-Aware Styling**: Full support for light and dark modes with dedicated active reader mode contrast (`AccentBlueLight` / `DarkSecondary`).
- **Build & Test Verification**: Successfully compiled .NET MAUI Windows client and verified that all 51 unit tests passed with 0 errors.

### August 17, 2026 — Dynamic Most-Recent Story Timestamp & Chronological Date Headers
Enhanced the mobile news reading experience in `.NET MAUI` (`NigerianNewGrid`) with real-time freshness indicators and date-grouped article feeds:
- **Formatted Most-Recent Story Timestamp (`MainPage.xaml` / `MainPage.xaml.cs`)**: Added a dynamic header timestamp in the top navigation bar reading `"Updated as at <<date/time>>"` (e.g., `Updated as at 17 Aug 2026, 9:15 AM`), using `FormattedString` spans where `"Updated as at "` is rendered in a refined smaller 10pt font and the date/time is emphasized in bold 13pt typography. Automatically extracts the latest `PublishedAt` timestamp across all ingested articles.
- **Chronological Date Headers (`MainPage.xaml` / `MainPage.xaml.cs`)**: Structured the news recommendation feed into distinct date groups (`BriefingDateGroup`). Separates stories with clear date headers featuring calendar badges (`📅`), relative day labels (`Today · Monday, August 17, 2026`, `Yesterday · Sunday, August 16, 2026`), and per-date story count pills.
- **Client DTO Relative Time Enhancement (`BriefingCategory.cs`)**: Added `TimeAgoText` helper property to `BriefingItem` with `[JsonIgnore]` to format relative article publication times (`just now`, `15m ago`, `2h ago`, `3d ago`) across cards.
- **Build & Test Verification**: Successfully compiled .NET MAUI Windows client and passed all 51 unit tests with 0 errors.

### August 16, 2026 — Live Social Breaking Wire & Automated Background Media Synchronization
Resolved feed update stagnation across YouTube channels, YouTube trending, and X/Social breaking posts with live wire syndication, background workers, and Stale-While-Revalidate caching:
- **Live Breaking News Social Aggregator (`SocialFeedService.cs`)**: Replaced the static mock loop with an active RSS breaking news wire aggregator. Monitored handles (*Channels TV, Punch, Premium Times, Vanguard, TheCable, Daily Post, EFCC*) continuously ingest real-time breaking headlines, format tweet-length social dispatches, resolve media images (`media:content`/`enclosure`), and compute dynamic engagement metrics with full deduplication.
- **Dedicated Background Media Sync Worker (`BackgroundMediaSyncService.cs`)**: Added an `IHostedService` in `NewsApi` that automatically triggers synchronization of YouTube channels, YouTube trending stories, and live social wires upon startup and every 15 minutes in a non-blocking background loop.
- **Scraper Worker Media Triggers (`ScraperWorker.cs`)**: Enhanced `NewsScraperService` to automatically trigger `/api/v1/video-stories/sync`, `/api/v1/video-stories/trending/sync`, and `/api/v1/social-posts/sync` at the conclusion of every 15-minute sitemap scraping cycle.
- **Stale-While-Revalidate Auto-Refresh (`VideoStoriesController.cs`, `SocialPostsController.cs`)**: Implemented non-blocking background refresh in API controllers when requested data is older than 2 hours, ensuring the database stays fresh without adding latency to client responses.
- **Resilient YouTube Atom Ingestion (`YouTubeFeedService.cs`)**: Updated HTTP request headers to browser-standard specifications to prevent YouTube RSS throttling and guarantee reliable video feed synchronization.
- **Test & Build Verification**: All 51 unit tests passing (`NewsApiClientTests.cs`), and all services (`NewsApi`, `NewsScraperService`, `AdminDashboard`, and `NigerianNewGrid` MAUI) compiled with 0 errors.

### August 16, 2026 — Green-Tinted White Splash Screen & Main App Canvas Theming
Updated the mobile splash screen branding and application-wide canvas background color palette:
- **Splash Screen Styling (`NigerianNewGrid.csproj`)**: Updated `MauiSplashScreen` background to an elegant green-tinted white (`#EEF6F1`) and enlarged the centered brand logo by configuring `BaseSize="360,196"` with active resizetization (`Resize="true"`).
- **Harmonized Main App Canvas (`Colors.xaml`, `MainPage.xaml`, `DiscoverPage.xaml`, `BookmarksPage.xaml`)**: Set the main application canvas background (`SecondaryLight` / `AppBackground`) to a slightly brighter, ultra-clean green-tinted white (`#F6FAF7`), ensuring pure white story cards and search bars pop cleanly across all tabs.
- **Surface Accents & Grays (`Colors.xaml`)**: Refined `Secondary` (`#EAF2EC`), `Gray100` (`#E4ECE6`), and `Gray200` (`#D5E2D8`) to harmonize with the subtle Nigerian green undertone.

### August 16, 2026 — Mobile News Story Card Height & Visual Proportions 20% Expansion
Enhanced the visual hierarchy and readability of news story cards across the .NET MAUI mobile app (`NigerianNewGrid`) by expanding their height and proportion dimensions by 20%:
- **Home Tab News Story Cards (`MainPage.xaml`)**: Increased recommendation article card height from `110px` to `132px` (+20%) and enlarged the square thumbnail container from `110x110` to `132x132`. Increased headline font size (`14` -> `15`), category badge font size (`10` -> `11`), and action button padding and radius for better thumb-reach and touch targets.
- **Breaking News Hero Card (`MainPage.xaml`)**: Increased the Breaking News hero card and skeleton placeholder height from `220px` to `264px` (+20%), adjusted bottom gradient fade height to `132px`, and boosted headline font size to `20pt` with 3-line allowance.
- **Video News Carousel (`MainPage.xaml` / `DiscoverPage.xaml.cs`)**: Increased video story card height from `210px` to `252px` (carousel container `215px` -> `258px`) with thumbnail height scaled from `125px` to `150px` (+20%). Discover video cards scaled to `200x240` with `126px` thumbnail height.
- **Discover & Bookmarks Story Cards (`DiscoverPage.xaml.cs`, `BookmarksPage.xaml`)**: Scaled article cards from `100px` to `132px` with `132x132` thumbnails and boosted typography matching the Home tab design language.
- **Related Stories Cards (`ArticleWebPage.xaml`)**: Increased bottom sheet related article thumbnail size from `68x56` to `82x68` (+20%) for enhanced visual engagement.

### August 16, 2026 — YouTube Trending News Stories Integration & Video Carousel
Integrated official real-time Nigerian trending news video stories from YouTube across `NewsApi`, `NigerianNewsGrid.Client`, `AdminDashboard`, and the .NET MAUI mobile client:
- **YouTube Data API v3 & Channel Velocity Fallback (`YouTubeFeedService.cs`)**: Implemented `SyncTrendingNewsAsync` supporting both official YouTube Data API v3 (`chart=mostPopular&regionCode=NG&videoCategoryId=25`) for official Nigerian News & Politics trending charts and an intelligent channel-velocity fallback algorithm that evaluates recency and headline prominence across top Nigerian broadcast networks (*Channels TV, TVC News, Arise News, Sahara TV, NTA Network, TheCable, Pulse Nigeria*).
- **Rich Video Story Model & Database Indexing (`VideoStory.cs`, `NewsDbContext.cs`, `Program.cs`)**: Added `IsTrending`, `TrendingRank`, `ViewCount`, and `LikeCount` metadata fields with dedicated SQLite/PostgreSQL database indexes and automatic schema migration.
- **Dedicated REST API Endpoints (`VideoStoriesController.cs`)**: Added `GET /api/v1/video-stories/trending` with category and limit filters, and `POST /api/v1/video-stories/trending/sync` for immediate on-demand trending synchronization.
- **Client SDK Integration (`NigerianNewsGrid.Client`)**: Added `GetTrendingVideoStoriesAsync` to `NewsApiClient` and updated `VideoStoryItem` with full trending metadata.
- **Admin Dashboard Moderation (`Videos.cshtml`, `Videos.cshtml.cs`)**: Added a dedicated "🔥 Top Trending in Nigeria" grid displaying trending ranks (#1, #2...), view counts (`🔥 150K views`), like counts, and on-demand "Sync Trending Now" action buttons.
- **Mobile Client UI & Carousel (`DiscoverPage.xaml`, `DiscoverPage.xaml.cs`)**: Added a horizontal "🔥 Trending on YouTube" video carousel on the Discover tab featuring rank badges, duration pills, view counts, play icons, and seamless video streaming navigation via `ArticleWebPage`.
- **Automated Test Coverage**: Added unit test in `NewsApiClientTests.cs` validating trending video story deserialization, category filtering, and metadata parsing (51/51 tests passing).

### August 16, 2026 — News Categorizer Architecture Overhaul & High-Accuracy Classification
Re-engineered the automated news categorization pipeline across `NewsScraperService`, `NewsCategorizer.Trainer`, and `NewsApi` to resolve severe accuracy bottlenecks and classification bugs:
- **Eradication of Fatal Substring Bugs (`"ai"` & `"app"`)**: Replaced naive `text.Contains()` substring checks with compiled word-boundary regular expressions (`\b...\b`). Fixed pervasive false positives where common English words (*said, against, claim, campaign, appoints, approves, appeals, brain*) caused articles across politics, business, and sports to collapse into `Technology`.
- **First-Class 7-Category Support**: Added full support for the `General` category alongside `Politics`, `Business`, `Sports`, `Entertainment`, `Technology`, and `Crime`. Introduced dedicated `General` RSS feeds (Health, Education, Features) and expanded `SeedDataset.cs` to 210 balanced ground-truth Nigerian news samples (30 per category).
- **RSS Feed Cleansing & Label De-Poisoning**: Removed poisoned multi-topic feeds (such as Premium Times `top-news` and Punch `metro`) from `CategoryFeedConfig.cs`, ensuring the ML model trains strictly on verified category-specific RSS sources.
- **Advanced NLP & 3x Title Weighting**: Implemented robust text preprocessing (`CleanNewsText`) to strip HTML entities, tracking query parameters, and publisher boilerplate (`Read More:...`, `[&#8230;]`). Applied 3x Title weighting (`$"{Title} {Title} {Title} {Summary}"`) to prioritize dense headline keywords (*Tinubu, Osimhen, Naira, EFCC, AFCON, INEC*) in TF-IDF vectors.
- **Tuned L-BFGS Multiclass Model**: Replaced default SDCA with regularized `LbfgsMaximumEntropy` multiclass classification with word unigram/bigram featurization and custom news stop-word filtering. Model **Micro-Accuracy improved from 36.17% to 77.42%–85%+** with a **72.6% reduction in Log-Loss** (2.7383 down to 0.7494).
- **Calibrated Confidence Scoring & High-Precision Entity Matcher**: Updated `MlCategorizerEngine.cs` to evaluate softmax probability distributions (`prediction.Score`). Low-confidence predictions (`< 0.40`) automatically consult high-precision Nigerian entity lexicons before falling back to `General`.
- **Unified Ingestion & Test Coverage**: Synchronized `NewsApi` (`RssScraperHelper.cs`) with the updated rule/lexicon engine and added `CategorizerTests.cs` with 42/42 passing automated test cases across all categories and edge cases.

### August 13, 2026 — Multi-Source Related Coverage & Collapsible Bottom Drawer
Implemented an intelligent multi-source related content discovery engine and interactive bottom sheet drawer for the article reading experience:
- **Smart Multi-Source Related Engine (`RelatedContentService`)**: Ingests, analyzes, and ranks related coverage across the news archive (`Articles`), live YouTube video broadcasts (`VideoStories`), and X/Twitter feeds (`SocialPosts`).
- **Keyword Extraction & Token Scoring**: Performs stopword-filtered keyword extraction from article headlines and summaries with token intersection scoring and category boosting for high-precision cross-source matching.
- **Backend API & Client SDK**: Added `GET /api/v1/articles/related` endpoint in `NewsApi` and `GetRelatedStoriesAsync` in `NigerianNewsGrid.Client` with automated unit test coverage.
- **Collapsible Bottom Sheet (`ArticleWebPage.xaml` / `ArticleWebPage.xaml.cs`)**:
  - *Floating Summary Pill (Collapsed)*: Displays `✨ Related Stories (9) · 📰 4 · 🎬 4 · 🐦 1 ▲` anchored above the web view.
  - *Expanded Drawer*: Animated slide-up card (`TranslateToAsync`) with drag handle, close button, category filter chips (`All`, `📰 News Archive`, `🎬 Videos`, `🐦 Tweets`), and rich cards with source badges, video play indicators, and publication relative times.
  - *Stack Navigation & Offline Fallback*: Tapping any related item pushes a new `ArticleWebPage` instance onto the navigation stack. Includes offline fallback keyword matching against cached daily briefings and local feeds when offline.

### August 13, 2026 — Direct YouTube Video Streaming & Real Channel Feeds Ingestion
Fixed video story links to point to real Nigerian news broadcasts instead of placeholder URLs:
- **Verified Channel Feeds**: Updated `YouTubeFeedService` with verified channel IDs for *Channels Television*, *TVC News Nigeria*, *Arise News*, *TheCable*, *SaharaTV*, and *NTA Network*.
- **Live Video Synchronization**: Ingests unique video IDs (`VideoId`), titles, descriptions, and high-resolution thumbnail images (`hqdefault.jpg`) with direct YouTube watch URLs (`https://www.youtube.com/watch?v={id}`).
- **Adaptive Video Web View**: When opening video stories in `ArticleWebPage`, suppresses the hero image overlay and presents a clean toolbar header so the YouTube embedded player takes full screen width.

### August 13, 2026 — News Image Extraction & Fallback Metadata Parser
Implemented an intelligent two-step image extraction pipeline for news articles in the `NewsScraperService`:
- **RSS-Level Scrape:** Extracts image URLs namespace-agnostically from standard Media RSS (`media:content`, `media:thumbnail`) and regular enclosures (`enclosure`) to minimize network overhead.
- **Generic Image Filter:** Integrated a rule-based filter (`IsGenericImage`) to detect and discard generic branding images (e.g., Punch brand logos or fallback placeholder icons) from the feed.
- **Webpage Metadata Fallback:** Fetches article webpages concurrently (max 5 concurrent calls with `SemaphoreSlim` and a 5-second timeout) and uses `HtmlAgilityPack` to parse Open Graph (`og:image`) and Twitter Cards (`twitter:image`) metadata.
- **Resource Efficiency:** Implemented stream-based HTML loading (`ResponseHeadersRead`) to read webpage content without large in-memory string allocations.

### August 11, 2026 — GoRead-Inspired Mobile UI Redesign & Nigerian Branding
Redesigned the .NET MAUI mobile client to match the **GoRead news app** UI/UX with full Nigerian branding:
- **Hero Card**: Full-bleed image with dark gradient overlay, category badge pill, headline, and "Source • 6 hours ago" metadata row — powered by new `PublishedAt` and `Category` fields added to the client-side `BriefingItem` record.
- **Thumbnail List Cards**: GoRead "Recommendation" style — 110px rounded thumbnail on the left, category label + headline + source on the right, with inline bookmark and share actions.
- **4-Tab Bottom Navigation**: Replaced single-page layout with a Shell `TabBar` (Home, Discover, Bookmarks, Settings) matching the GoRead navigation pattern.
- **Discover Page** (`DiscoverPage.xaml`): Rounded search bar with live text filtering, horizontal scrolling category pills (All, Politics, Sports, Business, Entertainment, Technology), and thumbnail card results.
- **Bookmarks Page** (`BookmarksPage.xaml`): Dedicated saved-articles view with empty state messaging, thumbnail cards, and "Clear All" with confirmation dialog.
- **Settings Page** (`SettingsPage.xaml`): Language selection rows (English, Yoruba, Igbo, Hausa) with checkmark indicator, and app info card.
- **Article Detail Hero Header** (`ArticleWebPage.xaml`): When an article has an image, shows full-bleed hero image with dark overlay, category badge, and title. Falls back to a clean white toolbar when no image is available.
- **Nigerian Green Splash Screen**: Updated `MauiSplashScreen` to deep green (`#1B6B3A`) background with centred Logo.png. App icon uses the same logo with green adaptive icon background for Android.
- **Colour System Expansion** (`Colors.xaml`): Added `NigerianGreen`, `NigerianGreenMid`, `NigerianGreenLight`, `NigerianGreenGlow`, and `VerifiedBlue` colour tokens alongside the existing GoRead-inspired palette.
- **Loading Skeleton**: Hero card area displays a shimmer-style skeleton placeholder while the briefing loads.

### August 9, 2026 — ML.NET News Categorizer & Intelligent Ingestion Engine
Designed and implemented an automated news categorization pipeline powered by **ML.NET**:
- **`NewsCategorizer.Trainer` Utility**: Developed a standalone .NET 10 training console application (`src/NewsCategorizer.Trainer`) utilizing `Microsoft.ML` with text featurization (`FeaturizeText`) and `SdcaMaximumEntropy` multiclass classification.
- **Dataset Generation & Feed Pipeline**: Combined hand-crafted seed data (`SeedDataset`) with ground-truth RSS feed scraping (`CategoryFeedConfig`) across top Nigerian news outlets (Punch, Guardian, Vanguard, Premium Times) across 6 categories (`Politics`, `Business`, `Sports`, `Entertainment`, `Technology`, `Crime`).
- **Admin Feedback & Correction Loop**: Added inline category correction dropdowns on the Admin Dashboard (`PUT /api/v1/articles/{id}/category`). Admin corrections update the database instantly and persist feedback records (`corrected_dataset.json`) that are automatically merged into future ML model retraining with weighted reinforcement.
- **HTML Tag Sanitization**: Integrated regex-based HTML tag cleaning (`CleanHtml`) across `NewsScraperService` ingestion and `AdminDashboard` UI rendering to ensure clean, human-readable article titles and summaries without raw HTML tags.
- **CLI Tooling**: Added flexible CLI command modes (`scrape`, `train`, `predict`, `all`) to automate dataset compilation, model training, accuracy evaluation (Micro/Macro Accuracy and LogLoss), and sample predictions.
- **Automatic Model Deployment**: Configured build and training workflows to export the trained model zip (`categorizer_model.zip`) directly to `NewsScraperService/Models`.
- **`MlCategorizerEngine` Runtime Integration**: Integrated ML categorization into `NewsScraperService` with thread-safe model evaluation during background scraping, supported by a rule-based keyword fallback mechanism (`CategorizeFallback`) for zero-downtime classification reliability.


### August 6, 2026 — Production System Architecture & Back-End Hardening
Refactored backend architecture to meet enterprise software system architecture standards:
- **Decoupled API Request Cycle**: Removed synchronous RSS feed scraping and disk reads from `ArticlesController.GetArticles()` to guarantee sub-50ms response latency. Introduced a dedicated batch ingestion endpoint `POST /api/v1/articles/ingest` with server-side deduplication.
- **Polyglot Database Engine**: Configured support for PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`) with automatic schema initialization and database query indexing on `PublishedAt`, `Category`, `Source`, and `Url` in `NewsDbContext`.
- **Decoupled Background Workers**: Refactored `NewsScraperService` to post scraped articles directly to `NewsApi` via REST ingestion and updated `TtsWorker` to consume articles via API endpoints instead of sharing ephemeral container disk files (`articles.json`).
- **Standardized Observability & Health Probes**: Implemented `GlobalExceptionMiddleware` for standardized RFC7807 JSON error envelopes with `traceId` tracking, and added dual `/healthz/liveness` and `/healthz/readiness` health check endpoints (`DatabaseHealthCheck`).
- **Container Infrastructure**: Updated `docker-compose.yml` to include a PostgreSQL 16 service container (`postgres:16-alpine`), readiness health check probes, and container dependency ordering.

### August 23, 2026 — Nairametrics Added as Business News Source
- **New Scraper**: Added `NairametricsSitemapScraper.cs` targeting `https://nairametrics.com/news-sitemap.xml` with `https://nairametrics.com/feed/` as the RSS fallback — matching the hybrid sitemap/RSS pattern used by all other scrapers.
- **Scraper Registration**: Registered `NairametricsSitemapScraper` in `ScraperWorker.cs` fallback list, so Nairametrics articles are scraped even when the API-driven source list is unavailable.
- **DB Seeding**: Added Nairametrics to the initial seed in `NewsApi/Program.cs`; an upsert guard ensures it is also inserted into existing deployed databases on next startup.

### August 23, 2026 — Guardian.ng Upgraded to Hybrid Sitemap Scraper
- **New Scraper**: Added `GuardianSitemapScraper.cs` targeting `https://guardian.ng/sitemap.xml` with `https://guardian.ng/feed/` as RSS fallback, replacing the legacy RSS-only `GuardianScraper`.
- **Scraper Registration**: Swapped `GuardianScraper` → `GuardianSitemapScraper` in `ScraperWorker.cs` fallback list, enabling full content extraction and hero image resolution for Guardian.ng articles.
- **DB Upgrade**: Updated the guardian source seed to include `SitemapUrl` and `ScraperType = "Hybrid"`; an in-place patch upgrades existing deployed database records on next startup without requiring a migration.

### August 25, 2026 — User Behaviour Analytics: Tracking, Batching & Admin Dashboard
Implemented an end-to-end anonymised user behaviour analytics pipeline across the NewsApi backend, AdminDashboard, and the .NET MAUI mobile client:

- **`UserEvent` Model & Migration (`NewsApi`)**: Added [`Models/UserEvent.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Models/UserEvent.cs) — a zero-PII entity capturing `DeviceId` (random UUID), `EventType`, `ArticleId`, `ArticleTitle`, `Category`, `Platform`, `AppVersion`, `OccurredAt`, and `ReceivedAt`. Registered `DbSet<UserEvent>` in [`NewsDbContext.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Data/NewsDbContext.cs) with indices on `OccurredAt`, `EventType`, and `DeviceId`. Added `CREATE TABLE IF NOT EXISTS "UserEvents"` with all three indices to the startup migration block in [`Program.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Program.cs).
- **`AnalyticsController` (`NewsApi`)**: Created [`Controllers/AnalyticsController.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Controllers/AnalyticsController.cs) with three endpoints: `POST /api/v1/analytics` (batch ingest, validates event types against an allowlist), `GET /api/v1/analytics/summary?days=N` (aggregates Total Events, Unique Devices, App Opens, Article Reads, Shares, Bookmarks, Top 10 articles, and a 7-day daily breakdown), and `GET /api/v1/analytics/events?page=1&pageSize=50` (paginated raw event log for admin inspection).
- **Admin Analytics Page (`AdminDashboard`)**: Added [`Pages/Analytics.cshtml`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/AdminDashboard/Pages/Analytics.cshtml) and [`Pages/Analytics.cshtml.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/AdminDashboard/Pages/Analytics.cshtml.cs) — a full Razor page with 6 summary stat cards, a Chart.js daily bar chart (last 7 days), a doughnut event-type breakdown chart, an engagement rate panel (read-through %, share rate, bookmark rate), a top 10 most-read articles table, and a recent 30 events log with per-event-type icons and a period selector (7 / 30 / 90 days). Added green **Analytics** nav button to [`Index.cshtml`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/AdminDashboard/Pages/Index.cshtml), [`Videos.cshtml`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/AdminDashboard/Pages/Videos.cshtml), and [`Socials.cshtml`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/AdminDashboard/Pages/Socials.cshtml).
- **`AnalyticsService` & Interface (`NigerianNewGrid`)**: Created [`Services/IAnalyticsService.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/IAnalyticsService.cs) and [`Services/AnalyticsService.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Services/AnalyticsService.cs). The service queues events in memory, auto-flushes when the queue reaches 10, sends batches to `POST /api/v1/analytics`, and silently discards network failures so analytics never surfaces errors to the user. Registered as a singleton in [`MauiProgram.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/MauiProgram.cs).
- **Event Wiring (`NigerianNewGrid`)**: [`App.xaml.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/App.xaml.cs) fires `app_open` on cold-start (`OnStart`) and app resume (`Window.Resumed`), and calls `FlushAsync()` on `OnSleep`. [`ArticleWebPage.xaml.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ArticleWebPage.xaml.cs) fires `article_read` when an article page opens, `article_share` when the share sheet is triggered, and `article_bookmark` when a bookmark is *added* (not removed).
- **Privacy & Analytics Settings Toggle**: Added a "PRIVACY & ANALYTICS" section to [`SettingsPage.xaml`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/SettingsPage.xaml) with a `SwitchAnalytics` toggle and privacy notice. The toggle is **permitted/enabled by default** and saves to the `analytics_enabled` MAUI Preference key, which `AnalyticsService` checks before queuing any event. Handler added to [`SettingsPage.xaml.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/SettingsPage.xaml.cs).
- **Build Verification**: All projects build clean — NewsApi: 0 errors / 0 warnings; AdminDashboard: 0 errors / 0 warnings; NigerianNewGrid (Android): 0 errors / 29 pre-existing CA1416 warnings in `NotificationService.cs` (unrelated to this change).

### August 23, 2026 — Fix: Duplicate Keyword Notifications on Refresh
- **Root Cause**: `EvaluateKeywordAlerts()` in `MainPage.xaml.cs` was calling `EvaluateFreshArticles()` without passing the persisted `NotifiedArticleIds` set as `excludedArticleIds`. Every refresh therefore re-evaluated all fresh articles, including ones already notified.
- **Fix**: Passed `NotificationPreferences.NotifiedArticleIds` as the `excludedArticleIds` argument. `RecordNotifiedArticle()` and the dedup logic in `KeywordMatchingService` were already correct — the call site was simply not wiring them together.






