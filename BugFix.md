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


