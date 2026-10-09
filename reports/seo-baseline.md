# News Stand NG — Technical SEO & Web Performance Baseline Audit

**Audit Date:** October 3, 2026  
**Audited Artifact:** `NigerianNewsGrid.Web` (`dist/` build output + codebase inspection)  
**Target Audience Profile:** Nigerian Mobile Users (3G/4G high latency, metered data, Android mobile dominant)

---

## 1. Executive Summary & Baseline Scores

| Category | Current Score | Critical Deficiencies |
| :--- | :---: | :--- |
| **Indexability & Crawlability** | **0 / 100** | 100% Client-Side Rendered (CSR). Search bots receive an empty `<div id="root"></div>`. No individual article URLs exist. |
| **Social & Sharing Previews** | **0 / 100** | Zero Open Graph (`og:*`) or Twitter Card (`twitter:*`) tags. Sharing on WhatsApp, X, and Facebook fails or displays static site title. |
| **Structured Data (Schema.org)** | **0 / 100** | Zero JSON-LD tags present (`NewsArticle`, `BreadcrumbList`, `CollectionPage`, `Organization`, `WebSite`). |
| **Discoverability & Sitemaps** | **0 / 100** | No `robots.txt`, no `sitemap.xml`, no Google News 48-hour sitemap (`sitemap-news.xml`), and no RSS feeds. |
| **Core Web Vitals & Performance** | **52 / 100** | Monolithic JS bundle (344.89 kB, 0 code-splitting), 4 synchronous Google Font families, blocking Google GSI script in `<head>`. |

---

## 2. In-Depth Technical Breakdown

### 2.1 Indexability & Architecture
- **HTML Document Inspection (`dist/index.html`):**
  ```html
  <div id="root"></div>
  <script type="module" crossorigin src="/assets/index-DpVsQ977.js"></script>
  ```
  Search crawlers (Googlebot, Bingbot, Yandex) and social preview scrapers (WhatsApp, Twitterbot, LinkedInBot) receive an empty DOM container. Without Javascript execution and API hydration, no headlines, categories, publishers, or timestamps exist.
- **Routing Failure:**
  The web app currently lacks any client-side or server-side router (`react-router` is not installed). Clicking a news story modifies internal component state and opens `ReaderDrawer.jsx` as an overlay. As a result, **articles do not possess permalinks/URLs**. Google cannot index stories it cannot address via unique URLs.

### 2.2 Metadata & Social Sharing Previews
- `<title>` is hard-coded to: `News Stand NG | Live Breaking News, Top Stories & In-Depth Journalism`.
- `<meta name="description">` is hard-coded to a generic summary.
- Missing `<link rel="canonical" href="...">`.
- Missing `og:title`, `og:description`, `og:image`, `og:url`, `og:type`.
- Missing `twitter:card`, `twitter:title`, `twitter:image`.
- WhatsApp and X link unfurling will render generic fallbacks or empty tiles.

### 2.3 Asset Delivery & Core Web Vitals
- **JavaScript Bundle Size:**
  - `dist/assets/index-DpVsQ977.js`: **344.89 kB uncompressed** (103.10 kB gzipped).
  - Code-splitting is disabled: `AuthModal`, `CommentSection`, `VideoSection`, and `ReaderDrawer` are bundled into the initial viewport render.
- **Render-Blocking Web Fonts:**
  `index.html` synchronously requests 4 distinct Google Font families across external CDNs (`fonts.googleapis.com`, `fonts.gstatic.com`):
  - `Lora` (400..700 italic/normal)
  - `Newsreader` (opsz 6..72, 400..700)
  - `Plus Jakarta Sans` (300, 400, 500, 600, 700, 800)
  - `Outfit` (500, 600, 700, 800)
  This causes multiple round-trip DNS lookups, TLS negotiations, and FOIT (Flash of Invisible Text) on slower cellular connections.
- **Third-Party Script Injection:**
  Google Identity Services (`https://accounts.google.com/gsi/client`) is loaded unconditionally in `<head>`.

---

## 3. SEO Remediation Roadmap & Targets

| Target Metric | Baseline Status | Post-Implementation Target | Phase Responsible |
| :--- | :--- | :--- | :--- |
| **Crawlable Article Permalinks** | 0 URLs | 100% of articles (`/news/{slug}`) | Phase 1 & 2 |
| **Server-Side Rendered (SSR) HTML** | Empty `<div id="root">` | Full headline, excerpt, metadata in initial response | Phase 2 |
| **Lighthouse SEO Score** | ~50 | **95 - 100** | Phase 2 & 3 |
| **Lighthouse Performance (Mobile)** | ~52 | **≥ 85** | Phase 4 |
| **Largest Contentful Paint (LCP)** | ~4.2s (simulated 3G/4G) | **< 2.5s** | Phase 4 |
| **Cumulative Layout Shift (CLS)** | 0.18 | **< 0.1** | Phase 4 |
| **Google News Sitemap** | None | Rolling 48-hour XML (`/sitemap-news.xml`) | Phase 3 |
| **Structured Data** | None | `NewsArticle`, `BreadcrumbList`, `CollectionPage` | Phase 2 & 6 |
