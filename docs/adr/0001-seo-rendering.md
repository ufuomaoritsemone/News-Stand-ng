# ADR 0001: Web Rendering Strategy for Crawlability & SEO

* **Status:** Approved (Option A: React Router v7 with SSR)  
* **Date:** October 3, 2026  
* **Author:** Principal Software Architect Agent & Developer Team  
* **Deciders:** Engineering Lead / Repository Owner  

---

## 1. Context & Problem Statement

News Stand NG's web client (`src/NigerianNewsGrid.Web`) was initially architected as a Single Page Application (SPA) using Vite, React 19, and client-side REST data fetching. 

### Current Pain Points:
1. **Zero Organic Search Footprint:** When search crawlers (Googlebot, Bingbot) request any page, they receive an empty `<div id="root"></div>`. Googlebot defer-renders JavaScript using a headless Chromium queue, which incurs indexing delays of days or weeks, while other search engines and social bots (WhatsApp, Twitter/X, LinkedIn) do not execute JavaScript at all.
2. **Missing Addressable URLs:** Articles open via state mutations in `ReaderDrawer.jsx` without permalink routing.
3. **Social Sharing Failure:** Open Graph and Twitter cards cannot be dynamically generated per story in a static HTML file.

We require a rendering architecture that delivers fully-formed HTML with metadata and structured data in the initial HTTP response while preserving the rich, animated client UX.

---

## 2. Evaluated Options

### Option A: React Router v7 (Framework Mode) with SSR *(Recommended)*
Migrate the existing Vite + React 19 application to React Router v7 (formerly Remix). 
- **How it works:** Keeps Vite as the build tool and retains the entire component library (`ArticleCard`, `HeroCarousel`, `CategoryTabs`, `ReaderDrawer`, Vanilla CSS styles). React Router loaders fetch data on the server and render complete HTML on first paint, then hydrate into an interactive React app.
- **Effort:** **Medium** (~2 days). No rewrite of UI components, hooks, or CSS styles is required.
- **SEO Quality:** **Exceptional**. Full SSR HTML on every route, server-driven `meta` exports, dynamic JSON-LD injection, and standard HTTP response status codes (e.g., 404 for missing stories, 301 for updated slugs).
- **Hosting & Infrastructure:** Requires a lightweight Node.js container in Docker (`nigeriannewsgrid-web` in `docker-compose.yml`) or serverless hosting.
- **Risks:** Node runtime dependency in deployment; managing client-only browser globals (`window`, `localStorage`, `navigator`) during server rendering.

### Option B: Next.js (App Router)
Re-architect the web application as a Next.js 15+ application.
- **How it works:** Full framework migration to Next.js using React Server Components (RSC), App Router conventions, and built-in image/font optimization.
- **Effort:** **High** (~4 to 5 days). Requires rewriting state management, splitting components into client/server boundaries (`'use client'`), refactoring CSS imports, and adapting to Next.js routing paradigms.
- **SEO Quality:** **Exceptional**. Industry-standard SSR/SSG capabilities, native metadata API, and automatic sitemap generation.
- **Hosting & Infrastructure:** Requires a Node.js container or specialized Vercel runtime.
- **Risks:** High migration friction, component refactoring overhead, and potential disruption to current active features.

### Option C: ASP.NET Core Head Injection (Reverse Proxy / Hybrid)
Retain the existing Vite React SPA as static assets, but route requests through `NewsApi` (or a lightweight ASP.NET Razor endpoint) that intercepts bot traffic or serves `index.html` with injected `<title>`, `<meta>`, `<script type="application/ld+json">`, and a static `<noscript>` excerpt.
- **How it works:** When `/news/{slug}` is requested, ASP.NET Core queries the database, generates the meta tags, string-replaces `<head>` placeholders in `index.html`, and serves the response.
- **Effort:** **Low to Medium** (~1 to 2 days). Zero changes to the React build toolchain.
- **SEO Quality:** **Poor to Mediocre**. Social bots (WhatsApp/Twitter) receive tags, but search engine crawlers do not receive the actual DOM tree, internal links, breadcrumbs, or responsive layouts. Google will see a discrepancy between the static skeleton and client-rendered content.
- **Hosting & Infrastructure:** Minimal. Runs entirely inside the existing `NewsApi` service without introducing Node.js to Docker.
- **Risks:** Fragile string replacement; divergence between server-injected head and client-rendered state; poor long-term architecture for topic hubs and story clusters.

---

## 3. Comparison Matrix

| Criteria | Option A: React Router v7 SSR (Recommended) | Option B: Next.js 15 | Option C: ASP.NET Head Injection |
| :--- | :---: | :---: | :---: |
| **SEO & Crawler Fidelity** | 🟢 Complete HTML + Full DOM | 🟢 Complete HTML + Full DOM | 🔴 Partial (Head & noscript only) |
| **Migration Effort** | 🟡 Medium (Keep components/styles) | 🔴 High (RSC split, convention rewrite) | 🟢 Low (C# Controller injection) |
| **Component Code Reuse** | 🟢 ~95% code preserved | 🟡 ~60% code preserved | 🟢 100% code preserved |
| **Infrastructure Impact** | 🟡 Adds Node.js container to Compose | 🟡 Adds Node.js container to Compose | 🟢 Reuses existing ASP.NET container |
| **Long-Term Extensibility** | 🟢 High (Native loaders, route actions) | 🟢 High | 🔴 Low (Maintenance bottleneck) |

---

## 4. Architectural Recommendation & Decision Request

We recommend **Option A (React Router v7 Framework Mode with SSR)**:
1. It solves the indexing and social sharing challenge by delivering 100% server-rendered HTML.
2. It preserves our React 19 codebase, custom Vanilla CSS tokens, and component architecture without a painful RSC rewrite.
3. It integrates with Docker Compose as a lightweight container communicating internally with `news-api`.
