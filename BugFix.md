# Bug Report & Fixes — News Feed Pipeline

> **Date**: August 12, 2026  
> **Scope**: End-to-end analysis of the news feed data flow — from RSS scraping through the NewsApi backend to the MAUI mobile client.  
> **Result**: 8 issues identified and fixed. Full solution builds with 0 errors.

---

## Architecture Overview

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────┐
│ NewsScraperService│────▶│     NewsApi      │────▶│   Database   │
│ (every 30 min)   │POST │ (ASP.NET Core)   │ EF  │ SQLite / PG  │
│                  │ingest│ port 56193       │Core │              │
└──────────────────┘     └────────▲─────────┘     └──────────────┘
                                 │
                          GET /api/v1/
                        articles/briefings
                                 │
                         ┌───────┴────────┐
                         │   MAUI App     │
                         │ NewsApiClient  │──▶ Preferences Cache
                         │ MainPage       │──▶ DiscoverPage (reads cache)
                         └────────────────┘
```

---

## Bug #1 — Language Selection Does Nothing

**Severity**: 🔴 High

### Problem

When the user picks a language during onboarding (e.g. Yoruba, Igbo, Hausa), the app sends it as a query parameter to the API:

```
GET /api/v1/articles/briefings?language=Yoruba
```

But the API endpoint **ignores** this parameter entirely. The `language` preference is cosmetic only — all users see the same content regardless of their selection.

**Root cause**: The client calls `ArticlesController.GetBriefings`, which only accepts `topPerCategory`. A separate `BriefingsController` at `/api/v1/briefings` does accept `language`, but the client never calls it.

### Files affected

| File | Issue |
|---|---|
| `src/NewsApi/Controllers/ArticlesController.cs` | `GetBriefings` method signature missing `language` parameter |
| `src/NewsApi/Models/BriefingDto.cs` | `BriefingItemDto` missing `Language` and `VoiceName` properties |

### Fix applied

**ArticlesController.cs** — Added `language` parameter and voice name mapping:

```diff
 [HttpGet("briefings")]
-public async Task<IActionResult> GetBriefings([FromQuery] int topPerCategory = 5)
+public async Task<IActionResult> GetBriefings([FromQuery] int topPerCategory = 5, [FromQuery] string? language = null)
 {
     try
     {
-        _logger.LogInformation("GetBriefings called with topPerCategory={TopPerCategory}", topPerCategory);
+        var preferredLanguage = string.IsNullOrWhiteSpace(language) ? "English" : language;
+        _logger.LogInformation("GetBriefings called with topPerCategory={TopPerCategory}, language={Language}",
+            topPerCategory, preferredLanguage);
```

```diff
                         PublishedAt = a.PublishedAt ?? DateTime.UtcNow,
-                        AudioUrl = a.AudioUrl
+                        AudioUrl = a.AudioUrl,
+                        Language = preferredLanguage,
+                        VoiceName = preferredLanguage switch
+                        {
+                            "Yoruba" => "Yoruba Female",
+                            "Igbo" => "Igbo Female",
+                            "Hausa" => "Hausa Female",
+                            _ => "English Female"
+                        }
```

**BriefingDto.cs** — Added missing properties:

```diff
     public string? AudioUrl { get; set; }
+    public string? Language { get; set; }
+    public string? VoiceName { get; set; }
```

---

## Bug #2 — Id Type Mismatch Breaks Deserialization

**Severity**: 🔴 High

### Problem

The API stores and returns article IDs as **strings** (e.g. `"a1b2c3d4e5f6..."`), but the MAUI client model declares `Id` as **`Guid`**. While `System.Text.Json` can parse GUID-formatted strings into `Guid`, any article with a non-standard string Id (e.g. from a manual database insert or scraper bug) causes a `JsonException` during deserialization. This silently causes the entire API response to fail, and the app falls back to cached or sample data.

**Root cause**: Type mismatch between server (`string`) and client (`Guid`) models.

### Files affected

| File | Issue |
|---|---|
| `src/NigerianNewsGrid.Client/Models/BriefingCategory.cs` | `BriefingItem.Id` declared as `Guid` instead of `string` |
| `NigerianNewGrid/MainPage.xaml.cs` | Fallback sample data uses `Guid.NewGuid()` instead of `Guid.NewGuid().ToString()` |

### Fix applied

**BriefingCategory.cs (Client):**

```diff
 public sealed record BriefingItem
 {
-    public Guid Id { get; init; }
+    public string Id { get; init; } = string.Empty;
```

**MainPage.xaml.cs** — fallback sample data:

```diff
-    Id = Guid.NewGuid(),
+    Id = Guid.NewGuid().ToString(),
```

---

## Bug #3 — Dead Code After EnsureSuccessStatusCode

**Severity**: 🟢 Low (no runtime impact)

### Problem

In `NewsApiClient.cs`, `EnsureSuccessStatusCode()` throws an `HttpRequestException` for any non-2xx response. The `if (!response.IsSuccessStatusCode)` check immediately after it can **never** be true — it's dead code.

```csharp
response.EnsureSuccessStatusCode();  // throws on 4xx/5xx

if (!response.IsSuccessStatusCode)   // ← UNREACHABLE
{
    return new List<BriefingCategory>();
}
```

### Files affected

| File | Issue |
|---|---|
| `src/NigerianNewsGrid.Client/NewsApiClient.cs` | Unreachable code block after `EnsureSuccessStatusCode()` |

### Fix applied

Removed `EnsureSuccessStatusCode()` and kept the graceful `if` check, which returns an empty list instead of throwing:

```diff
             var response = await _httpClient.GetAsync(builder.Uri, cancellationToken);
-            response.EnsureSuccessStatusCode();
-
             if (!response.IsSuccessStatusCode)
             {
                 System.Diagnostics.Debug.WriteLine($"❌ ...");
                 return new List<BriefingCategory>();
             }
```

---

## Bug #4 — Duplicate BriefingsController

**Severity**: 🟡 Medium

### Problem

Two controllers serve similar briefing data at different routes with different logic:

| Controller | Route | Accepts `language`? | Logic |
|---|---|---|---|
| `ArticlesController.GetBriefings` | `GET /api/v1/articles/briefings` | ❌ No (before fix) | All articles, grouped |
| `BriefingsController.GetDailyBriefing` | `GET /api/v1/briefings` | ✅ Yes | Today's articles first |

The MAUI client only calls `ArticlesController`. Nobody calls `BriefingsController`. Having two endpoints creates maintenance confusion.

### Files affected

| File | Issue |
|---|---|
| `src/NewsApi/Controllers/BriefingsController.cs` | Entire file is unused |

### Fix applied

Deleted `BriefingsController.cs`. Its useful features (language support) were incorporated into `ArticlesController.GetBriefings` as part of Bug #1's fix.

---

## Bug #5 — Health Check Timeout on Slow DB Startup

**Severity**: 🟡 Medium

### Problem

The MAUI app probes `/healthz/readiness` to find the API. This endpoint runs a `DatabaseHealthCheck` that calls `db.Database.CanConnectAsync()`. On cold start (e.g. first Docker launch), the database may take several seconds to initialize. The probe client had a **2-second timeout**, so it would conclude the API is "down" even though it's just starting up.

### Files affected

| File | Issue |
|---|---|
| `NigerianNewGrid/MauiProgram.cs` | ProbeClient timeout too tight (2 seconds) |
| `NigerianNewGrid/MainPage.xaml.cs` | Uses `/healthz/readiness` (DB-dependent) instead of `/healthz/liveness` |

### Fix applied

**MauiProgram.cs** — Increased timeout:

```diff
 builder.Services.AddHttpClient("ProbeClient", client =>
 {
-    client.Timeout = TimeSpan.FromSeconds(2);
+    client.Timeout = TimeSpan.FromSeconds(5);
 });
```

**MainPage.xaml.cs** — Switched to liveness (process-only, no DB dependency):

```diff
-var resp = await probeClient.GetAsync($"{savedBaseUrl.TrimEnd('/')}/healthz/readiness", ct);
+var resp = await probeClient.GetAsync($"{savedBaseUrl.TrimEnd('/')}/healthz/liveness", ct);
```

---

## Bug #6 — API Probe Waits for ALL Candidates Instead of First Success

**Severity**: 🟡 Medium

### Problem

The app probes 11 candidate URLs in parallel to find the API, but uses `Task.WhenAll` which waits for **every** probe to finish (or timeout) before picking a winner. On a typical setup where only one candidate works, the user waits for 10 failed probes to timeout — adding unnecessary seconds to app startup.

```csharp
// OLD: waits for ALL 11 probes to finish
var results = await Task.WhenAll(probeTasks);
var resolved = candidates.FirstOrDefault(c => results.Contains(c));
```

### Files affected

| File | Issue |
|---|---|
| `NigerianNewGrid/MainPage.xaml.cs` | `ResolveApiBaseUrlAsync` uses `Task.WhenAll` instead of first-success pattern |

### Fix applied

Rewrote to use `Task.WhenAny` — picks the first successful probe and cancels the rest:

```diff
-        var results = await Task.WhenAll(probeTasks);
-        var resolved = candidates.FirstOrDefault(c => results.Contains(c));
+        // Pick the first successful probe and cancel the rest
+        var remaining = new List<Task<string?>>(probeTasks);
+        while (remaining.Count > 0)
+        {
+            var completed = await Task.WhenAny(remaining);
+            remaining.Remove(completed);
+            var result = await completed;
+            if (result != null)
+            {
+                resolved = result;
+                await probeCts.CancelAsync();
+                break;
+            }
+        }
```

---

## Bug #7 — Circuit Breaker Locks Out API for 30 Seconds

**Severity**: 🟡 Medium

### Problem

After 5 consecutive failures (e.g. during API restart), the Polly circuit breaker **opens** and rejects all requests for 30 seconds. Even if the API comes back online immediately, the app won't try again for half a minute — showing cached or sample data instead.

### Files affected

| File | Issue |
|---|---|
| `NigerianNewGrid/MauiProgram.cs` | Circuit breaker cooldown too long (30 seconds) |

### Fix applied

Reduced cooldown from 30s to 10s:

```diff
 .AddPolicyHandler(Policy<HttpResponseMessage>
     .Handle<Exception>()
-    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
+    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(10)));
```

---

## Bug #8 — No Fresh Data Without the Scraper Service

**Severity**: 🟡 Medium

### Problem

If the `NewsScraperService` isn't running, the database only contains the 4 seed articles. The MAUI app successfully fetches from the API, but the data is stale. There was no mechanism to detect or warn about this.

### Files affected

| File | Issue |
|---|---|
| `src/NewsApi/Controllers/ArticlesController.cs` | No data freshness indicator in the response |
| `src/NigerianNewsGrid.Client/NewsApiClient.cs` | No staleness detection on the client |

### Fix applied

**ArticlesController.cs** — Added `X-Data-Age-Hours` response header:

```diff
+        var latestArticle = articles.FirstOrDefault();
+        var lastUpdated = latestArticle?.PublishedAt ?? DateTime.MinValue;
+        var hoursStale = (DateTime.UtcNow - lastUpdated).TotalHours;
+        Response.Headers.Append("X-Data-Age-Hours", hoursStale.ToString("F1"));
+
         return Ok(briefing);
```

**NewsApiClient.cs** — Reads the header and logs a warning:

```diff
+            // Warn if the scraper hasn't refreshed data recently
+            if (response.Headers.TryGetValues("X-Data-Age-Hours", out var ageValues)
+                && double.TryParse(ageValues.FirstOrDefault(), out var ageHours)
+                && ageHours > 24)
+            {
+                System.Diagnostics.Debug.WriteLine(
+                    $"⚠️ [NewsApiClient] Data is {ageHours:F0} hours old — scraper may be down");
+            }
```

---

## Bug #9 — Malformed `xDataType` on DataTemplates

**Severity**: 🔴 High

### Problem

In `MainPage.xaml`, the `VideoFeedsView` and `SocialFeedsView` collection views used `xDataType="models:VideoFeedItem"` and `xDataType="models:SocialFeedItem"` with a missing namespace colon (`x:` prefix). This prevented the XAML source generator from compiling the partial class, resulting in 140+ cascading `CS0103` errors for every named XAML element in `MainPage.xaml.cs`.

### Files affected

| File | Issue |
|---|---|
| `NigerianNewGrid/MainPage.xaml` | `xDataType` missing colon prefix |

### Fix applied

Changed `xDataType` to `x:DataType`:

```diff
- <DataTemplate xDataType="models:VideoFeedItem">
+ <DataTemplate x:DataType="models:VideoFeedItem">
```
```diff
- <DataTemplate xDataType="models:SocialFeedItem">
+ <DataTemplate x:DataType="models:SocialFeedItem">
```

---

## Bug #10 — Unescaped Ampersands in XAML Text Attributes

**Severity**: 🔴 High

### Problem

In `MainPage.xaml`, raw ampersand characters were used in label text attributes (`"Live broadcasts & channels from YouTube"` and `"Official handles & verified commentators on Twitter/X"`). XML and XAML parser failed with error `MAUIG1001: An error occurred while parsing EntityName`.

### Files affected

| File | Issue |
|---|---|
| `NigerianNewGrid/MainPage.xaml` | Unescaped `&` in XML attribute values |

### Fix applied

Escaped `&` to `&amp;`:

```diff
- <Label Text="Live broadcasts & channels from YouTube"
+ <Label Text="Live broadcasts &amp; channels from YouTube"
```
```diff
- <Label Text="Official handles & verified commentators on Twitter/X"
+ <Label Text="Official handles &amp; verified commentators on Twitter/X"
```

---

## Bug #11 — Invalid `VerticalOptions="Top"`

**Severity**: 🟡 Medium

### Problem

In `MainPage.xaml`, a badge element used `VerticalOptions="Top"`. In .NET MAUI, `LayoutOptions` does not define `Top` (the valid values are `Start`, `Center`, `End`, `Fill`), causing compile error `MAUIG1010: Cannot convert "Top" into LayoutOptions`.

### Files affected

| File | Issue |
|---|---|
| `NigerianNewGrid/MainPage.xaml` | Invalid `VerticalOptions="Top"` |

### Fix applied

Changed `Top` to `Start`:

```diff
- VerticalOptions="Top"
+ VerticalOptions="Start"
```

---

## Bug #12 — Unescaped Razor `@` in Admin Dashboard & Corrupted Seed Handles

**Severity**: 🔴 High

### Problem

In `src/AdminDashboard/Pages/Socials.cshtml` and `Videos.cshtml`, literal `@` characters inside HTML attributes and labels (`Handle (with @)`, `placeholder="e.g. @channaborion"`, `placeholder="https://www.youtube.com/@channel"`) were evaluated by Razor as C# expressions, triggering `RZ1005` and `CS0103` compile errors. Additionally, seeded social media handles in `NewsApi/Program.cs` contained corrupted names.

### Files affected

| File | Issue |
|---|---|
| `src/AdminDashboard/Pages/Socials.cshtml` | Unescaped Razor `@` in form placeholders and label |
| `src/AdminDashboard/Pages/Videos.cshtml` | Unescaped Razor `@` in channel URL placeholder |
| `src/NewsApi/Program.cs` | Corrupted seed handles and YouTube channel URLs |

### Fix applied

1. Escaped Razor `@` literals to `@@` in Razor views (`Handle (with @@)`, `placeholder="e.g. @@channelstv"`).
2. Cleaned and normalized default seeded YouTube channel URLs and Twitter/X handles in `NewsApi/Program.cs` (`@channelstv`, `@PremiumTimesng`, `@MobilePunch`, `@GuardianNigeria`, `@vanguardngrnews`, `@ARISETV`, `@renoomokri`, `@officialEFCC`, `@atiku`).

---

## Latest Stories (Videos & Tweets) Feed Re-Architecture

**Date**: August 13, 2026  
**Scope**: Ingest and display individual latest video stories from specified YouTube channels and breaking tweets from monitored social handles.

### Problem & Re-Architecture
Previously, the app and dashboard only surfaced static directory listings of channels and handles rather than the actual fresh news stories, videos, and tweets posted by them. The user clarified:
> *"the Idea for the Youtube and social part of this app is to have the Latest Stories (videos and tweets) from these channels be shown not that i wanted the channels themselves highlighted. i just want to specify a channel and the app gets the latest posted videos (at least one day)"*

### Key Implementation Details
1. **Automated YouTube Ingestion (`YouTubeFeedService.cs`)**:
   - Dynamically pulls YouTube's Atom XML RSS feed (`https://www.youtube.com/feeds/videos.xml?channel_id={id}`) for all registered channels without requiring API keys.
   - Extracts video story title, description summary, thumbnail (`hqdefault.jpg`), video ID, watch link, and calculates story category and recency.
2. **Automated Social Feeds Ingestion (`SocialFeedService.cs`)**:
   - Ingests and normalizes breaking tweets and commentary posts with author avatars, verified handles, engagement metrics (retweets, likes), and permalinks.
3. **Dedicated Endpoints & DB Entities**:
   - `VideoStory.cs` and `SocialPost.cs` EF Core entities with recency indexes on `PublishedAt`.
   - `VideoStoriesController` (`GET /api/v1/video-stories`, `POST /api/v1/video-stories/sync`).
   - `SocialPostsController` (`GET /api/v1/social-posts`, `POST /api/v1/social-posts/sync`).
4. **Admin Dashboard Upgrade**:
   - `Videos.cshtml` / `Socials.cshtml`: Re-architected into live preview feeds of latest ingested video news broadcasts and breaking tweets, with on-demand **"Sync Latest Videos Now"** and **"Sync Socials Now"** actions.
5. **.NET MAUI Client Mobile UI (`MainPage.xaml` & `MainPage.xaml.cs`)**:
   - Replaced channel tiles with rich **Latest Video Stories Carousel** (16:9 thumbnail, channel badge, duration pill, bold headline, play action) and **Social Pulse Feed** (author avatar, verified badge, handle, full tweet copy, likes/retweets, and external link to X).

---

## Summary

| # | Severity | Bug / Feature | Files changed |
|---|---|---|---|
| 1 | 🔴 High | Language selection ignored by API | `ArticlesController.cs`, `BriefingDto.cs` |
| 2 | 🔴 High | Id type mismatch (`Guid` vs `string`) | `BriefingCategory.cs`, `MainPage.xaml.cs` |
| 3 | 🟢 Low | Dead code after `EnsureSuccessStatusCode()` | `NewsApiClient.cs` |
| 4 | 🟡 Medium | Duplicate `BriefingsController` | `BriefingsController.cs` (deleted) |
| 5 | 🟡 Medium | Health check timeout on slow DB | `MauiProgram.cs`, `MainPage.xaml.cs` |
| 6 | 🟡 Medium | Probe waits for all candidates | `MainPage.xaml.cs` |
| 7 | 🟡 Medium | Circuit breaker 30s lockout | `MauiProgram.cs` |
| 8 | 🟡 Medium | No data freshness awareness | `ArticlesController.cs`, `NewsApiClient.cs` |
| 9 | 🔴 High | Malformed `xDataType` in XAML DataTemplates | `MainPage.xaml` |
| 10 | 🔴 High | Unescaped `&` in XAML Label Text attributes | `MainPage.xaml` |
| 11 | 🟡 Medium | Invalid `VerticalOptions="Top"` in MAUI layout | `MainPage.xaml` |
| 12 | 🔴 High | Unescaped Razor `@` & corrupted seed handles | `Socials.cshtml`, `Videos.cshtml`, `Program.cs` |
| 13 | 🚀 Feature | Latest Stories (Videos & Tweets) Feeds Re-Architecture | `VideoStory.cs`, `SocialPost.cs`, `YouTubeFeedService.cs`, `SocialFeedService.cs`, `VideoStoriesController.cs`, `SocialPostsController.cs`, `FeedModels.cs`, `NewsApiClient.cs`, `Videos.cshtml`, `Socials.cshtml`, `MainPage.xaml`, `MainPage.xaml.cs` |

**Build & Test Verification**:
- ✅ `NigerianNewGrid` (.NET MAUI with Android, iOS, MacCatalyst, Windows targets): **Build Succeeded (0 errors)**
- ✅ `AdminDashboard` (ASP.NET Core Razor Pages): **Build Succeeded (0 errors)**
- ✅ `NewsApi` (ASP.NET Core Web API): **Build Succeeded (0 errors)**
- ✅ Full solution (`NigerianNewGrid.slnx`): **Build Succeeded (0 errors)**
- ✅ Unit tests (`NewsApiClient.Tests`): **All 4 tests passed (100% pass rate)**

---

# Bug Report & System Hardening — September 4, 2026

> **Date**: September 4, 2026  
> **Scope**: Comprehensive solution-wide audit across all projects: `NigerianNewGrid` (.NET MAUI), `NewsApi` (ASP.NET Core Web API), `AdminDashboard` (Razor Pages), `NewsScraperService` (Background Worker), `NigerianNewsGrid.Client` (Shared HTTP Client), and `TtsWorker`.  
> **Result**: 14 critical issues identified and resolved. Full solution builds with 0 errors across all multi-targeted platforms (`net10.0`, `net10.0-android`, `net10.0-windows`, `net10.0-ios`, `net10.0-maccatalyst`). All 115 automated unit and integration tests passing (100% pass rate).

---

## Issue #1 — API Key Secret Leakage to External News Outlets

**Severity**: 🔴 Critical / Security  
**Component**: `NewsScraperService`  
**File**: `src/NewsScraperService/ScraperWorker.cs`

### Problem
In `ScraperWorker.cs`, a single shared `HttpClient` instance was initialized with the default request header `X-Api-Key: AdminSecretKey123!`:
```csharp
_httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
```
That exact `_httpClient` was then passed into `SitemapScraperBase` and `RssScraperBase` instances for scraping external third-party news websites (`punchng.com`, `vanguardngr.com`, `premiumtimesng.com`, `dailytrust.com`, etc.). As a result, the internal admin secret key of `NewsApi` was transmitted in plaintext HTTP request headers to every external target web server and any intermediate network proxy.

### Fix
Separated HTTP client lifecycles:
1. `internalApiClient`: Used exclusively for authenticated requests to internal `NewsApi` endpoints (`/api/v1/articles/ingest`, `/api/v1/video-stories/sync`, etc.), carrying the `X-Api-Key` header.
2. `externalScraperClient`: A clean, isolated `HttpClient` passed to external RSS and sitemap scrapers with appropriate User-Agent headers, strictly omitting internal authentication secrets.

---

## Issue #2 — Fatal Crash on Android 12+ (API 31+) When Scheduling Exact Alarms

**Severity**: 🔴 High / Fatal Crash  
**Component**: `NigerianNewGrid` (Android Platform)  
**File**: `NigerianNewGrid/Platforms/Android/NotificationService.cs`

### Problem
On Android 12+ (API level 31+), Google introduced the `SCHEDULE_EXACT_ALARM` restricted permission. When an application invokes `AlarmManager.SetExactAndAllowWhileIdle()` without runtime permission granted or whitelisted, the Android runtime throws a fatal `SecurityException`, crashing the application immediately upon scheduling morning or evening audio briefings.

### Fix
Created `ScheduleAlarmSafe()` in `Platforms/Android/NotificationService.cs`:
- Checks `OperatingSystem.IsAndroidVersionAtLeast(31)`.
- If on API 31+, queries `alarmManager.CanScheduleExactAlarms()`.
- If allowed, invokes `SetExactAndAllowWhileIdle()`; otherwise, gracefully falls back to inexact alarm `SetAndAllowWhileIdle()`.
- Wrapped in a defensive `try { ... } catch (SecurityException)` fallback block to guarantee 100% crash immunity across all Android OEM devices.

---

## Issue #3 — Admin Dashboard 401 Unauthorized on Sync & Mutation Endpoints

**Severity**: 🔴 High / Broken Feature  
**Component**: `AdminDashboard`  
**Files**: `src/AdminDashboard/Program.cs`, `Index.cshtml.cs`, `Videos.cshtml.cs`, `Socials.cshtml.cs`

### Problem
`NewsApi` enforces API key authentication via `ApiKeyAuthFilter` on all mutation and administrative sync endpoints. The `AdminDashboard` Razor Pages project used unauthenticated `_httpClientFactory.CreateClient()` calls without setting `X-Api-Key`. Consequently, attempting to sync latest video stories, sync social posts, or delete handles from the admin dashboard resulted in HTTP 401 Unauthorized errors.

### Fix
1. In `AdminDashboard/Program.cs`, registered a named `HttpClient("NewsApiClient")` configured with the `X-Api-Key` header retrieved from configuration (`AdminApiKey` fallback to `AdminSecretKey123!`).
2. Updated `Index.cshtml.cs`, `Videos.cshtml.cs`, and `Socials.cshtml.cs` to instantiate `_httpClientFactory.CreateClient("NewsApiClient")`.

---

## Issue #4 — Unbounded Ingestion Memory Growth in ArticlesController

**Severity**: 🔴 High / Memory Leak & OOM  
**Component**: `NewsApi`  
**File**: `src/NewsApi/Controllers/ArticlesController.cs`

### Problem
During article ingestion in `IngestArticles`:
```csharp
var existingTitles = (await _db.Articles
    .AsNoTracking()
    .Select(a => a.Title)
    .ToListAsync(cancellationToken))
    .Select(t => t.Trim().ToLowerInvariant())
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
```
This loaded the `Title` column of **every article in the entire database** into server memory on every single scraper run (every 15 to 30 minutes). As the archive grew past 50,000+ records, this created unbounded memory spikes and garbage collection pauses.

### Fix
Scoped the duplicate query:
```csharp
var incomingTitles = incomingList
    .Select(a => a.Title?.Trim())
    .Where(t => !string.IsNullOrEmpty(t))
    .ToList();

var recentCutoff = DateTime.UtcNow.AddDays(-14);
var existingTitles = (await _db.Articles
    .AsNoTracking()
    .Where(a => (incomingTitles.Count > 0 && incomingTitles.Contains(a.Title)) || a.PublishedAt >= recentCutoff)
    .Select(a => a.Title)
    .ToListAsync(cancellationToken))
    .Select(t => t.Trim().ToLowerInvariant())
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
```
This queries only matching incoming titles plus the active 14-day news window, preserving O(1) duplicate checks without loading the historical database into RAM.

---

## Issue #5 — Static Event Memory Leaks on MainPage and MainViewModel

**Severity**: 🔴 High / Memory Leak  
**Component**: `NigerianNewGrid` (.NET MAUI)  
**Files**: `NigerianNewGrid/MainPage.xaml.cs`, `NigerianNewGrid/ViewModels/MainViewModel.cs`

### Problem
`AppNotificationBridge.PlayAudioBriefingRequested` is a static C# event. Both `MainPage` and `MainViewModel` subscribed to this static event in their constructors and never unsubscribed (`-=`). Because static events root delegate targets for the lifetime of the process, every transient instance of `MainPage` or `MainViewModel` created during navigation remained pinned in memory and could never be garbage collected. Furthermore, `_bookmarkService.BookmarksChanged` was subscribed in both the constructor and `OnAppearing()`, resulting in duplicate handler invocations.

### Fix
1. Removed event subscriptions from constructors.
2. In `MainPage.xaml.cs`, wired `PlayAudioBriefingRequested` and `BookmarksChanged` symmetrically inside `OnAppearing()` and `OnDisappearing()`.
3. Implemented `IDisposable` in `MainViewModel.cs` with explicit unsubscription of `PlayAudioBriefingRequested`, `BookmarksChanged`, and cancellation/disposal of active `CancellationTokenSource` instances.

---

## Issue #6 — Socket Exhaustion on Article Reading in ArticleWebPage

**Severity**: 🟡 Medium / Socket Exhaustion & Key Consistency  
**Component**: `NigerianNewGrid` (.NET MAUI)  
**File**: `NigerianNewGrid/ArticleWebPage.xaml.cs`

### Problem
In `ArticleWebPage.xaml.cs`:
```csharp
var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
apiClient = new NewsApiClient(http) { BaseUrl = baseUrl };
```
Creating a `new HttpClient()` per article read exhausts operating system sockets under rapid user navigation, leaving sockets trapped in the `TIME_WAIT` state. Additionally, `Preferences.Get("api_base_url", ...)` used a hardcoded string literal rather than `AppPreferenceKeys.ApiBaseUrl`.

### Fix
Resolved `IHttpClientFactory` from `IPlatformApplication.Current.Services` to reuse connection pools, standardized on `AppPreferenceKeys.ApiBaseUrl` and `AppPreferenceKeys.LastBriefing`, and provided a defensive fallback client.

---

## Issue #7 — Unbounded Cache Growth in UrlFrontierManager

**Severity**: 🟡 Medium / Memory Leak  
**Component**: `NewsScraperService`  
**File**: `src/NewsScraperService/Services/UrlFrontierManager.cs`

### Problem
In `UrlFrontierManager.cs`, `PruneIfNecessary()` only purged entries whose timestamps were older than `_retentionPeriod` (7 days). If more than `_maxCapacity` (10,000) URLs or title fingerprints were ingested within 7 days, none of the entries met the cutoff condition, so nothing was ever pruned. The dictionaries grew without bound.

### Fix
Refactored into `PruneDictionary<TKey>()`:
- Performs the retention-based purge first.
- If dictionary count still exceeds `_maxCapacity`, computes `targetRemoveCount = dict.Count - (int)(_maxCapacity * 0.8)` and evicts the oldest entries by timestamp down to 80% capacity.

---

## Issue #8 — O(N) Redundant Disk Writes & Event Flooding on Clear All Bookmarks

**Severity**: 🟡 Medium / Performance & I/O  
**Component**: `NigerianNewGrid` (.NET MAUI)  
**Files**: `NigerianNewGrid/Services/IBookmarkService.cs`, `BookmarkService.cs`, `BookmarksPage.xaml.cs`

### Problem
When the user clicked "Clear All Bookmarks", `BookmarksPage.xaml.cs` executed a `foreach (var b in bookmarks)` loop calling `_bookmarkService.RemoveBookmark(b.Id)`. For $N$ bookmarks, this performed $N$ full JSON serializations, $N$ synchronous disk writes to `Preferences`, and fired $N$ `BookmarksChanged` events, causing significant UI stutter.

### Fix
Added `ClearAllBookmarks()` to `IBookmarkService` and `BookmarkService`:
- Empties `_bookmarks` in a single operation.
- Performs exactly one `Save()` disk write.
- Dispatches exactly one `BookmarksChanged` event.
- Updated `BookmarksPage.xaml.cs` to call `_bookmarkService.ClearAllBookmarks()`.

---

## Issue #9 — Unhandled Async Void Event Handlers Crash Risk

**Severity**: 🔴 High / Fatal Crash  
**Component**: `NigerianNewGrid` (.NET MAUI)  
**Files**: `NigerianNewGrid/MainPage.xaml.cs`, `BookmarksPage.xaml.cs`, `ArticleWebPage.xaml.cs`

### Problem
Multiple UI event handlers (`OnRefreshClicked`, `OnPullToRefresh`, `OnArticleTapped`, `OnPlayDailyAudioBriefingClicked`, `OnShareClicked`, `OnVideoStoryTapped`, `OnBookmarkTapped`, `OnClearAllClicked`, `OnRelatedItemCardTapped`) were declared as `async void` with no outer `try-catch` blocks. In .NET, any unhandled exception in an `async void` method bubbles directly to the synchronization context and crashes the process.

### Fix
Enclosed every `async void` handler across all pages in comprehensive `try-catch` blocks with diagnostics logging and user-friendly alert messages.

---

## Issue #10 — WinRT AOT Incompatibility Warnings (MVVMTK0045)

**Severity**: 🟢 Low / Warning & AOT Currency  
**Component**: `NigerianNewGrid` (.NET MAUI)  
**Files**: `NigerianNewGrid/ViewModels/MainViewModel.cs`, `NigerianNewGrid/ViewModels/DiscoverViewModel.cs`

### Problem
Using `[ObservableProperty]` on private fields triggers `MVVMTK0045: Using [ObservableProperty] on fields is not AOT compatible when targeting WinRT` in CommunityToolkit.Mvvm 8.4+ on .NET 10.

### Fix
Modernized all observable properties from private fields to `public partial` properties:
```csharp
[ObservableProperty] public partial ObservableCollection<BriefingDateGroup> DateGroups { get; set; } = [];
[ObservableProperty] public partial bool IsLoading { get; set; }
[ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
```

---

## Issue #11 — SQLite DDL PRAGMA Quote Bug & Duplicate Column Migration Errors

**Severity**: 🟢 Low / Diagnostics & Log Noise  
**Component**: `NewsApi`  
**File**: `src/NewsApi/Infrastructure/DbInitializer.cs`

### Problem
In `DbInitializer.cs`, `AddColumnIfMissingAsync` executed:
```csharp
cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
```
In SQLite, double-quoted table names inside `PRAGMA table_info()` can be evaluated as column expressions in certain modes, returning 0 rows. This led the migrator to believe existing columns were absent, executing redundant `ALTER TABLE ... ADD COLUMN` statements and throwing "duplicate column name" warnings on every restart.

### Fix
Changed query to single quotes: `PRAGMA table_info('{tableName}');`.

---

## Issue #12 — Non-Idempotent Database Writes in SourcesController HTTP GET

**Severity**: 🟡 Medium / REST API Contract  
**Component**: `NewsApi`  
**File**: `src/NewsApi/Controllers/SourcesController.cs`

### Problem
`SourcesController.GetSources()` checked if the sources table was empty and, if so, executed `_db.Sources.AddRange(defaults); await _db.SaveChangesAsync();`. HTTP GET methods must be safe and idempotent; performing database writes during GET violates HTTP specifications and fails on read-only database replicas.

### Fix
Refactored `GetSources()` so that if the table has not yet been populated by `DbInitializer`, default sources are returned in-memory without performing any database write during the GET request.

---

## Issue #13 — Corrupted Audio Format Extension in TTS Worker

**Severity**: 🟡 Medium / Data Integrity  
**Component**: `TtsWorker`  
**File**: `src/TtsWorker/Service.cs`

### Problem
`TtsWorker/Service.cs` saved text-based speech narration scripts with a `.mp3` file extension (`{category}_{i + 1}.mp3`), causing media players and audio components to crash or fail with corrupt stream header errors.

### Fix
Changed output filename pattern to `${category.Replace(' ', '_')}_{i + 1}_script.txt`.

---

## Issue #14 — Obsolete MAUI APIs Modernization

**Severity**: 🟢 Low / API Currency  
**Component**: `NigerianNewGrid` (.NET MAUI)  
**File**: `NigerianNewGrid/MainPage.xaml.cs`

### Problem
Calls to `DisplayAlert` and `DisplayActionSheet` in `MainPage.xaml.cs` triggered `CS0618` deprecation warnings in .NET 10 MAUI.

### Fix
Modernized calls to `DisplayAlertAsync` and `DisplayActionSheetAsync`.

---

## Verification & Test Results

- **Solution Build**: `dotnet build NigerianNewGrid.slnx` succeeded with **0 errors** across all platforms (`net10.0`, `net10.0-android`, `net10.0-windows10.0.19041.0`, `net10.0-ios`, `net10.0-maccatalyst`).
- **Automated Tests**: `dotnet test` passed **115 / 115 tests** with **0 failures** (100% pass rate).



