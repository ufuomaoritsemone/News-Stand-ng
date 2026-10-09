---
name: seo-news
description: Enforces technical SEO, Google News standards, JSON-LD structured data, aggregator copyright compliance, Core Web Vitals, and AI content guardrails for News Stand NG.
---

# News Stand NG — Technical & News SEO Guidelines

## 1. System Role & Philosophy
This skill governs all Search Engine Optimization (SEO), structured data, crawlability, and content compliance across the `NigerianNewGrid` platform (API, Web, and Background Workers).

As a news aggregation platform, **News Stand NG does not produce raw original reporting**. Therefore, the site’s SEO authority relies on:
1. **Flawless Technical Hygiene:** Server-rendered HTML, valid schema, ultra-fast mobile loading, and clean URL architectures.
2. **Aggregator Integrity:** Excerpts only, crystal-clear source attribution, and zero duplicate-content penalties.
3. **High-Value Synthesis:** Curated topic hubs, multi-source story clusters, evergreen data trackers, and editorial AI summaries.

---

## 2. URL Architecture & Slug Standards

All public web endpoints must follow strict, canonical URL conventions:

| Page Type | Canonical Pattern | Rules & Constraints |
| :--- | :--- | :--- |
| **Article Page** | `/news/{slug}` | ASCII kebab-case, max 80 chars, suffixed with a 6-char unique hash (e.g., `/news/cbn-adjusts-interest-rate-8f2a1b`). |
| **Topic Hub** | `/topic/{slug}` | Clean kebab-case entity name (e.g., `/topic/bola-tinubu`, `/topic/dangote-refinery`). |
| **Category Feed** | `/category/{slug}` | Predefined slugs: `politics`, `business`, `technology`, `entertainment`, `sports`, `metro`, `opinion`. |
| **Source Directory** | `/source/{slug}` | Publisher slug (e.g., `/source/punch`, `/source/vanguard`). |
| **State Hub** | `/state/{slug}` | One of 36 Nigerian states or FCT (e.g., `/state/lagos`, `/state/kano`). |
| **Data Page** | `/{data-metric}` | High-intent evergreen slugs: `/naira-exchange-rate`, `/fuel-price-nigeria`, `/jamb-waec-news`. |

### Slug Formatting Rules
- Strictly lowercase alphanumeric and hyphens (`^[a-z0-9-]+$`).
- Strip accents, Nigerian currency symbols (₦), emojis, and special punctuation.
- Never change an article's slug after publication. If an article slug must change, return an HTTP `301 Moved Permanently` to the new canonical URL.

---

## 3. On-Page HTML & Meta Tag Specifications

Every server-rendered route must output complete, distinct meta tags in the raw initial HTML response:

```html
<!-- Title & Description -->
<title>Title ≤ 60 Characters | News Stand NG</title>
<meta name="description" content="Concise, informative summary between 120 and 155 characters." />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<link rel="canonical" href="https://newsstand.ng/news/canonical-slug" />

<!-- Google Discover & Robots Directives -->
<meta name="robots" content="index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1" />

<!-- Open Graph (Facebook / WhatsApp / LinkedIn) -->
<meta property="og:type" content="article" />
<meta property="og:site_name" content="News Stand NG" />
<meta property="og:title" content="Engaging Social Title (≤ 70 chars)" />
<meta property="og:description" content="Engaging social excerpt." />
<meta property="og:url" content="https://newsstand.ng/news/canonical-slug" />
<meta property="og:image" content="https://newsstand.ng/api/img/og-preview.jpg" />
<meta property="og:image:width" content="1200" />
<meta property="og:image:height" content="630" />

<!-- Twitter / X Cards -->
<meta name="twitter:card" content="summary_large_image" />
<meta name="twitter:title" content="Social Title" />
<meta name="twitter:description" content="Social excerpt" />
<meta name="twitter:image" content="https://newsstand.ng/api/img/og-preview.jpg" />
```

---

## 4. Structured Data (JSON-LD) Specifications

All pages must inject typed JSON-LD scripts inside the `<head>` tag.

### 4.1 Article Page (`NewsArticle`)
```json
{
  "@context": "https://schema.org",
  "@type": "NewsArticle",
  "mainEntityOfPage": {
    "@type": "WebPage",
    "@id": "https://newsstand.ng/news/article-slug"
  },
  "headline": "Full Headline of the Aggregated Story",
  "image": ["https://newsstand.ng/images/article-1200x675.jpg"],
  "datePublished": "2026-10-03T14:30:00+01:00",
  "dateModified": "2026-10-03T15:10:00+01:00",
  "author": {
    "@type": "Person",
    "name": "Reported by [Journalist Name]"
  },
  "publisher": {
    "@type": "Organization",
    "name": "News Stand NG",
    "logo": {
      "@type": "ImageObject",
      "url": "https://newsstand.ng/logo.png",
      "width": 600,
      "height": 60
    }
  },
  "isBasedOn": "https://original-publisher.com/full-story-url",
  "description": "Short excerpt highlighting the story's core takeaways."
}
```

### 4.2 BreadcrumbList (All Inner Pages)
```json
{
  "@context": "https://schema.org",
  "@type": "BreadcrumbList",
  "itemListElement": [
    { "@type": "ListItem", "position": 1, "name": "Home", "item": "https://newsstand.ng" },
    { "@type": "ListItem", "position": 2, "name": "Business", "item": "https://newsstand.ng/category/business" },
    { "@type": "ListItem", "position": 3, "name": "Article Title", "item": "https://newsstand.ng/news/article-slug" }
  ]
}
```

### 4.3 Topic Hubs (`CollectionPage`)
Topic hubs (`/topic/{slug}`) and Story Clusters (`/story/{slug}`) must use `@type: "CollectionPage"` with `hasPart` listing the child aggregated stories.

---

## 5. Aggregator Copyright & Google Policy Compliance

To prevent copyright infringement, DMCA notices, and Google Search "Scraped Content" algorithmic penalties:

1. **Excerpt-Only Display:**
   - Publicly render **only** a teaser/excerpt (maximum 250–300 characters or 2–3 sentences).
   - **NEVER** render or serve the full scraped `Content` on any public HTML or anonymous API endpoint.
2. **Prominent Publisher Attribution:**
   - Display publisher branding and name prominently above the fold (e.g., *"Originally reported by Vanguard Newspapers"*).
   - Provide a prominent CTA button: `Read full story on Vanguard` pointing directly to the original publisher URL with `target="_blank"` and `rel="noopener noreferrer"`.
3. **Sponsored & Monetized Content:**
   - Paid promotions or sponsored stories (`IsSponsored == true`) must have a clear visual `"Sponsored"` badge.
   - Any links to sponsors must strictly use `rel="sponsored noopener"`.
   - Sponsored stories must be **excluded** from `sitemap-news.xml` and omitted from `NewsArticle` schema.

---

## 6. Sitemaps & Crawlability Architecture

### 6.1 `robots.txt`
```text
User-agent: *
Allow: /
Disallow: /api/
Disallow: /admin/
Disallow: /auth/
Disallow: /search

Sitemap: https://newsstand.ng/sitemap.xml
```

### 6.2 Google News Sitemap (`/sitemap-news.xml`)
Google News requires a specialized XML format:
- Must only contain articles published within the **last 48 hours**.
- Must contain at most 1,000 URLs per file.
- Format:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"
        xmlns:news="http://www.google.com/schemas/sitemap-news/0.9">
  <url>
    <loc>https://newsstand.ng/news/article-slug</loc>
    <news:news>
      <news:publication>
        <news:name>News Stand NG</news:name>
        <news:language>en</news:language>
      </news:publication>
      <news:publication_date>2026-10-03T16:00:00+01:00</news:publication_date>
      <news:title>Accurate Article Title</news:title>
    </news:news>
  </url>
</urlset>
```

### 6.3 Main Sitemap Index (`/sitemap.xml`)
Directs crawlers to partitioned sub-sitemaps:
- `/sitemap-news.xml` (Rolling 48h news)
- `/sitemap-articles-1.xml` (Historical article archives, capped at 50,000 URLs)
- `/sitemap-topics.xml` (All topic hub pages with >= 5 articles)
- `/sitemap-categories.xml` (Static category hubs)
- `/sitemap-sources.xml` (Publisher profile hubs)

---

## 7. Performance & Core Web Vitals (African Mobile Profile)

Given regional network bandwidth realities (3G/4G high latency and metered data in Nigeria):
- **LCP (Largest Contentful Paint):** `< 2.5s` on mobile. Preload the hero image and reserve explicit image aspect ratios to avoid layout shifts.
- **CLS (Cumulative Layout Shift):** `< 0.1`. All ad slots, dynamic banners, and article cards must have reserved CSS min-heights.
- **INP (Interaction to Next Paint):** `< 200ms`. Lazy-load heavy third-party bundles (Google OAuth, audio players, comment streams) until user intent is shown.
- **Font Subsetting:** Restrict web fonts to maximum 2 families (e.g., Lora + Plus Jakarta Sans). Preload WOFF2 formats; eliminate Google Fonts runtime blocking.

---

## 8. AI Content & Editorial Guardrails (Google E-E-A-T)

When using AI (Gemini / LLMs) to generate summaries, key takeaways, or topic descriptions:
1. **Factual Grounding Only:** Generative prompts must only reference the scraped excerpt and title. Forbid extrapolation, invented quotes, or fabricated statistics.
2. **Substantial Added Value:** AI must summarize, categorize, extract entities, or compare multiple perspectives. Never generate synthetic "rewrites" designed to disguise scraped text.
3. **Editorial Review Workflow:**
   - Automated enrichment (Meta titles, alt text, bullets) goes live automatically with strict length and profanity validators.
   - Long-form topic intros and story cluster overviews must default to `Status = PendingReview` until approved in the Admin Dashboard.
4. **Transparency & AI Disclosure:** Include a link to the `/ai-disclosure` page explaining how machine learning is used to cluster and summarize news.

---

## 9. Verification & Code Review Checklist

Before declaring any SEO-related feature complete, the AI agent must verify:
- [ ] Raw HTML response (`curl -s <url>`) contains populated `<title>`, `<meta name="description">`, and valid JSON-LD.
- [ ] Canonical URLs are absolute, point to the current production domain, and use HTTPS.
- [ ] No public endpoint returns the full scraped body (`Article.Content`).
- [ ] Outbound links to external publishers include `rel="noopener noreferrer"`.
- [ ] Sponsored articles include `rel="sponsored noopener"` and are omitted from the Google News sitemap.
- [ ] XML sitemaps pass standard XML parsing without syntax or encoding errors.
- [ ] Mobile Lighthouse performance budget meets LCP ≤ 2.5s and CLS ≤ 0.1.
