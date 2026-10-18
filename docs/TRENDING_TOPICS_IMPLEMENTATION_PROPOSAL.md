# Trending Topics Feature - Architecture & Implementation Proposal

**Document Version:** 1.0  
**Status:** Under Review / Proposal  
**Target Systems:** NewsApi (ASP.NET Core), NigerianNewsGrid.Web (Remix / React), NigerianNewGrid (.NET MAUI Mobile App)  

---

## 1. Executive Summary

This proposal outlines the technical architecture, data scoring models, API contracts, UI/UX designs, and SEO protections required to introduce a **"Trending Topics"** section across the News Stand NG ecosystem.

The feature capitalizes on existing infrastructure:
- **Database entities:** `Topic`, `ArticleTopic`, `UserEvent`, and `StoryCluster`.
- **Entity extraction:** The gazetteer + LLM pipeline in `NewsScraperService`.
- **Web topic hubs:** Pre-existing SEO routes at `/topic/:slug` (`topic.$slug.jsx`).

---

## 2. Trending Score Algorithms

To identify what is trending in real time, three ranking algorithms are evaluated:

### Option A: Coverage Velocity (Publisher Momentum)
Measures the rate of new articles published by national news outlets under each topic within a rolling 24–48-hour window.
$$\text{Score} = \text{Count}(\text{Articles published in window with Confidence} \ge 0.7)$$
* **Pros:** Works out of the box with zero user traffic; immediately reflects what the press is reporting.
* **Cons:** Does not capture which stories readers find most engaging.

### Option B: Reader Engagement Momentum
Measures direct user interactions (`article_read`, `article_share`, `article_bookmark`) on articles tagged with the topic in the last 24 hours.
$$\text{Score} = (\text{Reads} \times 1.0) + (\text{Shares} \times 3.0) + (\text{Bookmarks} \times 2.0)$$
* **Pros:** Highly reflective of true viral reader interest.
* **Cons:** Sensitive to cold-start during off-peak hours if overall user traffic is low.

### Option C: Hybrid Algorithm (Recommended)
Combines publisher coverage velocity with weighted reader engagement, applying a mild time-decay factor.
$$\text{Score} = (\text{Articles}_{24h} \times 2.0) + (\text{Reads}_{24h} \times 1.0) + (\text{Shares}_{24h} \times 3.0)$$
* **Fallback Rule:** If total active topics in the last 24 hours is below the requested threshold (e.g., late night / early morning), pad with all-time high-density canonical topics (topics having $\ge 5$ lifetime articles).

---

## 3. Backend Architecture (`NewsApi`)

### 3.1 DTO Contract
In `src/NewsApi/Models/Dtos.cs`:
```csharp
public class TrendingTopicDto
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Person, Organization, Place, Theme
    public int ArticleCount24h { get; set; }
    public int TotalArticleCount { get; set; }
    public double TrendingScore { get; set; }
    public string? SampleImageUrl { get; set; }
}
```

### 3.2 API Endpoint
Add endpoint in `src/NewsApi/Controllers/TopicsController.cs`:
* **Route:** `GET /api/v1/topics/trending?limit=10&windowHours=24`
* **Caching Strategy:**
  - In-Memory Cache via `IMemoryCache` (TTL: 5 minutes) to avoid repetitive database aggregation.
  - HTTP Cache Header: `Cache-Control: public, max-age=300`.
  - HTTP `ETag` generation based on topic IDs and counts for `304 Not Modified` support.
* **Query Safeguard:**
  - Filter `Confidence >= 0.7` on `ArticleTopics`.
  - Enforce a minimum threshold of $\ge 2$ articles in the window to qualify as trending.

---

## 4. Web Frontend Architecture (`NigerianNewsGrid.Web`)

### 4.1 Data Fetching & SSR
* Update `src/NigerianNewsGrid.Web/src/services/api.js` with `api.getTrendingTopics(limit)`.
* In `src/NigerianNewsGrid.Web/app/routes/home.jsx`, include `api.getTrendingTopics(10)` in the `Promise.all` SSR loader.

### 4.2 Component: `TrendingTopicsBar.jsx`
* **Placement:** Positioned prominently below `BreakingTicker.jsx` and above the `CategoryTabs.jsx` or hero grid.
* **Design & Aesthetics:**
  - Sleek horizontal pill rail with scroll gradient masks on mobile.
  - Ranking tags (`#1`, `#2`, `#3`) with vibrant badge accents.
  - Iconography: `Flame` / `TrendingUp` from `lucide-react`.
  - Hover / active micro-animations with subtle shadow elevation.
* **Routing:** Clicking a pill navigates directly to `/topic/:slug`, leveraging the already implemented `topic.$slug.jsx` hub.

---

## 5. Mobile App Architecture (`NigerianNewGrid` - .NET MAUI)

### 5.1 Client Service
* Extend `NigerianNewsGrid.Client.NewsApiClient` with `GetTrendingTopicsAsync(int limit, CancellationToken ct)`.

### 5.2 ViewModel Integration
* In `NigerianNewGrid/ViewModels/MainViewModel.cs`:
  - `ObservableCollection<TrendingTopicDto> TrendingTopics`
  - `bool HasTrendingTopics`
  - Hydrate during initial load and pull-to-refresh alongside feed articles.

### 5.3 UI Presentation
* In `MainPage.xaml`, insert a horizontal chip carousel above or below category chips.
* Tapping a topic chip can either navigate to a dedicated topic landing modal/page or apply a temporary topic filter to the main feed.

---

## 6. SEO, Editorial & Guardrails

1. **Thin-Content Guardrail:**
   Under Google News guidelines and the project's SEO guardrails, pages with fewer than 5 articles are marked `noindex, follow`. The trending topic query must only surface topics that meet minimum article count criteria to prevent driving traffic to thin landing pages.
2. **Database Indexing:**
   Ensure index coverage on:
   - `ArticleTopics(TopicId, ArticleId)`
   - `Articles(PublishedAt)`
3. **Internal Link Equity:**
   Prominently linking trending topics from the homepage funnels organic PageRank to evergreen topic hubs (e.g., `/topic/bola-tinubu`, `/topic/naira-exchange-rate`), accelerating their search engine indexation.

---

## 7. Next Actions / Decision Matrix

When ready to proceed, decisions will be needed on:
1. **Ranking Selection:** Confirm whether to launch with **Option A (publisher volume)** or **Option C (hybrid with clicks)**.
2. **UI Real Estate:** Confirm whether the trending section on web should be a **full-width pill bar** or a **sidebar widget** next to the hero story.
3. **Rollout Sequence:** Recommended order:
   - Step 1: `NewsApi` endpoint + unit tests.
   - Step 2: `NigerianNewsGrid.Web` component & SSR loader.
   - Step 3: `NigerianNewGrid` MAUI mobile app integration.
