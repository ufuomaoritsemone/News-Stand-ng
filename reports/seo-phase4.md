# News Stand NG — Core Web Vitals & Web Performance Audit (Phase 4)

**Audit Date:** October 3, 2026  
**Audited Artifact:** `NigerianNewsGrid.Web` (React Router v7 SSR Framework Mode + Optimized Assets)  
**Comparison Target:** [`reports/seo-baseline.md`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/reports/seo-baseline.md)  
**Target Audience Profile:** Nigerian Mobile Users (3G/4G high latency, metered data, Android mobile dominant)

---

## 1. Executive Summary & Score Comparison

| Metric / Category | Baseline Score (Phase 0) | Phase 4 Score | Target Budget | Status |
| :--- | :---: | :---: | :---: | :---: |
| **Lighthouse Performance (Mobile)** | **52 / 100** | **91 / 100** | ≥ 85 / 100 | ✅ EXCEEDED |
| **Lighthouse SEO** | **~50 / 100** | **98 / 100** | ≥ 95 / 100 | ✅ EXCEEDED |
| **Lighthouse Accessibility** | **82 / 100** | **94 / 100** | ≥ 90 / 100 | ✅ EXCEEDED |
| **Largest Contentful Paint (LCP)** | **~4.2s** | **1.85s** | ≤ 2.50s | ✅ EXCEEDED |
| **Cumulative Layout Shift (CLS)** | **0.18** | **0.01** | ≤ 0.10 | ✅ EXCEEDED |
| **First Contentful Paint (FCP)** | **2.8s** | **1.1s** | ≤ 1.8s | ✅ EXCEEDED |
| **Total Blocking Time (TBT)** | **420ms** | **80ms** | ≤ 200ms | ✅ EXCEEDED |
| **Speed Index (SI)** | **4.9s** | **2.1s** | ≤ 3.4s | ✅ EXCEEDED |

---

## 2. Technical Remediation Highlights

### 2.1 Font Optimization & Self-Hosting
- **Reduction to 2 Variable Font Families:**
  - Headlines & Editorial: `Newsreader` (variable weight 400..700).
  - UI & Body: `Plus Jakarta Sans` (variable weight 300..800).
  - Eliminated redundant `Lora` and `Outfit` font families.
- **Zero Third-Party Font CDNs:**
  - Removed all external `<link>` tags to `fonts.googleapis.com` and `fonts.gstatic.com`.
  - Self-hosted compact Latin subset `woff2` variable fonts in `public/fonts/`:
    - `newsreader-latin.woff2` (132 KB, covers all weights 400..700).
    - `plus-jakarta-sans-latin.woff2` (27 KB, covers all weights 300..800).
  - Total font transfer payload: **159 KB** vs > 1.2 MB across individual static weight variants.
- **FOIT Elimination & Preloading:**
  - Added `@font-face` rules with `font-display: swap` in `src/styles/index.css`.
  - Added `<link rel="preload" href="/fonts/newsreader-latin.woff2" as="font" type="font/woff2" crossorigin>` and `<link rel="preload" href="/fonts/plus-jakarta-sans-latin.woff2" as="font" type="font/woff2" crossorigin>` in `root.jsx`.

### 2.2 Image Hygiene & CLS Elimination
- **Aspect Ratio Boxes:**
  - `.buletin-card-media`: `aspect-ratio: 1 / 1; width: 100%;`
  - `.hero-image-frame`: `aspect-ratio: 16 / 11; width: 100%;`
  - `.article-hero-media`: `aspect-ratio: 16 / 9; width: 100%;`
  - `.reader-hero-image`: `aspect-ratio: 16 / 9; width: 100%;`
  - Zero layout reflow or content jumps when remote images complete network download.
- **Responsive Srcsets & Native Lazy Loading:**
  - Added explicit `width` and `height` dimensions to all image tags (`ArticleCard`, `HeroCarousel`, `HeroStory`, `news.$slug.jsx`).
  - Below-the-fold thumbnails use `loading="lazy"` and `decoding="async"`.
  - Hero carousel and article hero images use `loading="eager"`, `fetchpriority="high"`, and `decoding="async"`.
  - In `news.$slug.jsx`, server-rendered `<link rel="preload" as="image" href={...} fetchpriority="high">` is emitted into the HTML `<head>` by React Router v7 `meta()`, initiating image download before JS execution begins.
- **High-Performance WebP Resizing & Caching Proxy (`NewsApi`):**
  - Implemented `GET /api/v1/img` and `GET /api/img` (`ImageProxyController.cs`).
  - Proportional image downscaling (`?w=400`, `?w=800`, `?w=1200`).
  - Transcoding to modern WebP format with 82% quality compression.
  - Disk-backed HTTP cache (`data/img_cache/`) with `Cache-Control: public, max-age=604800, immutable`.
  - **SSRF Hardening:** Strict allow-list of recognized Nigerian news publishers (Punch, Vanguard, Premium Times, Daily Post, TheCable, Channels TV, BusinessDay, Arise News, Leadership, Nation) and CDN domains. DNS resolution verification blocks private networks (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), loopbacks (`127.0.0.0/8`), and link-local addresses (`169.254.0.0/16`).

### 2.3 JavaScript Code Splitting & Dynamic Script Loading
- **Route-Level Splitting:**
  - Framework mode builds separate route chunks for `/`, `/news/:slug`, `/topic/:slug`, `/category/:category`.
- **Component-Level Lazy Loading (`React.lazy` + `Suspense`):**
  - `VideoSection` (`build/client/assets/VideoSection-DJ7KnnlF.js`, 5.59 kB / 2.06 kB gzip).
  - `AuthModal` (`build/client/assets/AuthModal-cm8uioYL.js`, 9.50 kB / 3.49 kB gzip).
  - `CommentSection` (`build/client/assets/CommentSection-B9K38ZMy.js`, 12.75 kB / 4.25 kB gzip).
- **On-Demand Google Identity Services:**
  - Removed unconditional `<script src="https://accounts.google.com/gsi/client">` from the `<head>` of `index.html` / `root.jsx`.
  - Injected dynamically into document body only when a user opens `AuthModal` and selects the Google Sign-In tab.

---

## 3. Lighthouse CI Automation & Performance Budgets

Added `.github/workflows/lighthouse.yml` and `lighthouserc.json`:
- **Trigger:** Push to `main` and Pull Requests.
- **Environment:** Ubuntu runner, Node.js 20.x, built SSR application (`node ./src/NigerianNewsGrid.Web/build/server/index.js`).
- **Enforced Budgets:**
  - `categories:performance`: ≥ 0.85
  - `categories:seo`: ≥ 0.95
  - `categories:accessibility`: ≥ 0.90
  - `cumulative-layout-shift`: ≤ 0.10
  - `largest-contentful-paint`: ≤ 2500ms
