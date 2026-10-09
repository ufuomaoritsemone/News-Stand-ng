# News Stand NG — Full-Body AI News Summarizer Implementation Plan

This document outlines the architecture, data contracts, AI prompt engineering, and UI/UX design for adding a **Full-Body AI News Summarizer** to News Stand NG.

---

## 1. Executive Summary & Vision

While the current platform extracts 3 short bullet takeaways and timeline facts, users frequently need a **comprehensive narrative synthesis** of 1,500+ word news articles without reading dense, ad-cluttered external websites.

The **Full-Body AI Summarizer** transforms the full scraped text (`Article.Content`) stored in the database into a structured, executive briefing:
1. **Executive Overview**: 2–3 sentences summarizing what happened.
2. **Context & Background**: Why it matters within Nigeria's current socioeconomic or political climate.
3. **Key Figures & Quantifiable Facts**: Extracted Naira amounts, percentages, deadlines, and official quotes.
4. **Outlook & What Happens Next**: Projected decisions, hearings, or market impacts.

---

## 2. Regulatory & Google E-E-A-T Compliance

Per the [`seo-news`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/.agents/skills/seo-news/SKILL.md) guidelines:

1. **Aggregator Copyright Compliance**:
   - The raw scraped article body (`Article.Content`) must **never** be served publicly over any API or HTML view.
   - The AI summary is a **transformative synthesis**, not a copyright-infringing reprint or synthetic rewrite.
2. **Mandatory Publisher Attribution**:
   - Every AI summary must prominently display:
     > *"Synthesized by AI based on reporting from [Publisher]."*
   - A direct call-to-action button (`Read full story on [Publisher] ↗`) must be present with `target="_blank"` and `rel="noopener noreferrer"`.
3. **Factual Grounding & 0% Hallucination**:
   - Prompts must explicitly instruct the LLM to restrict claims to facts in the input text. No external assumptions or fabricated statements are allowed.
4. **Transparent AI Disclosure**:
   - Include an explicit badge (`⚡ AI Synthesis (Gemini 1.5 Flash)` or `Verified Extract`) linking to `/ai-disclosure`.

---

## 3. Data Contracts & Architecture

```
 ┌────────────────────────────────────────────────────────┐
 │                   Scraper Ingestion                    │
 │         Stores full scraped text in Article.Content    │
 └──────────────────────────┬─────────────────────────────┘
                            │
                            ▼
 ┌────────────────────────────────────────────────────────┐
 │           ArticleEnrichmentBackgroundService           │
 │  Calls ArticleEnrichmentService (Dual-Engine Mode)     │
 │  ┌──────────────────────────────────────────────────┐  │
 │  │ Primary: Google Gemini 1.5 Flash (temperature 0.2)│ │
 │  │ Fallback: TextRank-Extractive Graph Clusterer    │  │
 │  └──────────────────────────────────────────────────┘  │
 └──────────────────────────┬─────────────────────────────┘
                            │ Persists FullSummaryJson
                            ▼
 ┌────────────────────────────────────────────────────────┐
 │           PostgreSQL / SQLite Database                 │
 │     ArticleEnrichments Table: FullSummaryJson TEXT     │
 └──────────────────────────┬─────────────────────────────┘
                            │
                            ├────────────────────────────────┐
                            ▼                                ▼
 ┌──────────────────────────────────────┐  ┌──────────────────────────────────────┐
 │         SSR / Web API Route          │  │       On-Demand Generation API       │
 │   GET /api/v1/articles/{slug}        │  │  POST /api/v1/articles/{id}/ai-summary│
 │   Includes enrichment.fullSummary    │  │  Real-time fallback with rate limit  │
 └──────────────────┬───────────────────┘  └──────────────────┬───────────────────┘
                    │                                         │
                    ▼                                         ▼
 ┌────────────────────────────────────────────────────────────────────────────────┐
 │        React Router v7 Frontend (app/routes/news.$slug.jsx)                    │
 │        "⚡ AI Executive Briefing" Card + Shimmer Loading State                  │
 └────────────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Data Model: `FullSummaryPayload`
Stored inside `ArticleEnrichment.FullSummaryJson`:

```csharp
namespace NewsApi.Models;

public class FullSummaryPayload
{
    public string ExecutiveOverview { get; set; } = string.Empty;
    public List<string> ContextAndBackground { get; set; } = [];
    public List<string> KeyFiguresAndEntities { get; set; } = [];
    public string OutlookOrNextSteps { get; set; } = string.Empty;
    public int ReadingTimeSeconds { get; set; } = 45;
}
```

### 3.2 Update to `ArticleEnrichment.cs`
```csharp
public class ArticleEnrichment
{
    // Existing fields...
    public string KeyTakeawaysJson { get; set; } = "[]";
    public string TimelineFactsJson { get; set; } = "[]";
    public string FaqsJson { get; set; } = "[]";

    // New field
    public string? FullSummaryJson { get; set; }

    [NotMapped]
    public FullSummaryPayload? FullSummary
    {
        get => DeserializeJson<FullSummaryPayload>(FullSummaryJson ?? string.Empty);
        set => FullSummaryJson = JsonSerializer.Serialize(value);
    }
}
```

### 3.3 Update to `Dtos.cs`
```csharp
public class ArticleEnrichmentDto
{
    public List<string> KeyTakeaways { get; set; } = [];
    public List<TimelineFactDto> TimelineFacts { get; set; } = [];
    public List<FaqItemDto> Faqs { get; set; } = [];
    public FullSummaryDto? FullSummary { get; set; }
    public string Engine { get; set; } = "TextRank-Extractive";
}

public class FullSummaryDto
{
    public string ExecutiveOverview { get; set; } = string.Empty;
    public List<string> ContextAndBackground { get; set; } = [];
    public List<string> KeyFiguresAndEntities { get; set; } = [];
    public string OutlookOrNextSteps { get; set; } = string.Empty;
    public int ReadingTimeSeconds { get; set; } = 45;
}
```

---

## 4. Prompt Engineering & Dual-Engine Fallback

### 4.1 Gemini 1.5 Flash Prompt
```text
You are an executive news intelligence analyst for News Stand NG.
Analyze the following Nigerian news report and generate a high-value, structured executive summary.

GUARDRAILS:
1. Base your synthesis ONLY on the provided text. Never extrapolate, hallucinate, or add outside assumptions.
2. Extract all concrete numbers (e.g., Naira ₦ amounts, percentages, timelines) and named officials accurately.
3. Transform the story into an executive brief. Do NOT reproduce paragraphs verbatim.

Output strictly valid JSON matching this schema:
{
  "executiveOverview": "2 to 3 sentences summarizing what happened and why it matters right now.",
  "contextAndBackground": [
    "Preceding events, policy background, or historical trigger.",
    "Economic, legal, or social implications for Nigeria."
  ],
  "keyFiguresAndEntities": [
    "Entity / Official: Exact stance, statement, or monetary allocation."
  ],
  "outlookOrNextSteps": "Upcoming hearing date, policy rollout deadline, or anticipated market response.",
  "readingTimeSeconds": 45
}

Title: {Title}
Category: {Category}
Full Article Text:
{Content}
```

### 4.2 Offline Fallback: `TextRank-Extractive` Multi-Sentence Clustering
When `GEMINI_API_KEY` is not configured or rate-limited:
- The graph-centrality sentence ranker scores all sentences in `Content`.
- It selects the top 3 highest-scoring sentences as the `executiveOverview`.
- It clusters chronological and financial regex matches into `contextAndBackground` and `keyFiguresAndEntities`.
- `Engine` is labeled `"TextRank-Extractive"`, guaranteeing zero downtime and $0 operating cost.

---

## 5. API Endpoints

### 5.1 Public Read (Integrated in Article Response)
- `GET /api/v1/articles/{slug}`:
  - Returns `enrichment.fullSummary` directly in the article DTO.
  - Zero extra network roundtrips for the client.

### 5.2 On-Demand Generation Endpoint
- `POST /api/v1/articles/{id}/ai-summary`:
  - **Auth**: Protected by standard rate limiting (e.g., 10 req/min per IP).
  - **Behavior**:
    - If `Article.Enrichment?.FullSummary` already exists in DB, returns cached DTO immediately.
    - If missing, triggers real-time Gemini generation on the full body, saves to `ArticleEnrichments`, and returns HTTP 200.
    - Allows on-demand summary generation for older or newly scraped articles before background queue runs.

---

## 6. Frontend UI/UX Specification ([`app/routes/news.$slug.jsx`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NigerianNewsGrid.Web/app/routes/news.$slug.jsx))

### 6.1 Visual Component Hierarchy
```
┌────────────────────────────────────────────────────────┐
│ ⚡ AI Executive Briefing                 ⏱ 45s read    │
│ [Gemini 1.5 Flash]                                    │
├────────────────────────────────────────────────────────┤
│ Executive Overview:                                    │
│ The Central Bank of Nigeria (CBN) has announced ...    │
│                                                        │
│ ▼ Context & Impact                                     │
│   • Follows three consecutive MPC rate adjustments.    │
│   • Expected to impact commercial lending rates in Q4. │
│                                                        │
│ ▼ Key Figures & Named Officials                        │
│   • Olayemi Cardoso (CBN Governor): Announced ...      │
│   • ₦1.2 Trillion: Total liquidity absorbed.           │
│                                                        │
│ ℹ️ Synthesized by AI based on reporting from Punch.    │
│ [Read full story on Punch Newspapers ↗]               │
└────────────────────────────────────────────────────────┘
```

### 6.2 Key UI Features
1. **Collapsible Shelf**: Clean toggle to minimize or expand detailed context.
2. **On-Demand Shimmer State**: If an article does not yet have a summary, display a `"✨ Generate AI Summary"` button that initiates a shimmering loading skeleton.
3. **Copy / Share Integration**: Quick action button to copy the executive overview to the clipboard for sharing on WhatsApp or X.
4. **Light & Dark Mode**: Styled using the existing CSS token variables (`--bg-card`, `--border-color`, `--accent-primary`).

---

## 7. Implementation Roadmap & Milestones

| Phase | Tasks | Target Files |
| :--- | :--- | :--- |
| **Phase 1: Database & Contracts** | Add `FullSummaryPayload` to model, update `ArticleEnrichment.cs`, `Dtos.cs`, and `DbInitializer.cs`. | `src/NewsApi/Models/`, `src/NewsApi/Infrastructure/DbInitializer.cs` |
| **Phase 2: Service Engine** | Update `ArticleEnrichmentService.cs` with Gemini prompt and TextRank fallback for full summary. | `src/NewsApi/Services/ArticleEnrichmentService.cs` |
| **Phase 3: Background & API** | Update background worker and add `POST /api/v1/articles/{id}/ai-summary` endpoint. | `src/NewsApi/Services/`, `src/NewsApi/Controllers/` |
| **Phase 4: Web UI Integration** | Create `AiExecutiveSummary.jsx` component and wire into `news.$slug.jsx` with CSS styling. | `src/NigerianNewsGrid.Web/app/routes/news.$slug.jsx` |
| **Phase 5: Verification** | Test with live Gemini key, offline fallback, and verify Google News / E-E-A-T attribution rules. | Automated integration tests & browser validation |
