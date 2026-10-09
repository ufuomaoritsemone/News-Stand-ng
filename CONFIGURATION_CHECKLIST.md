# Nigerian News Grid / News Stand NG — Configuration & API Keys Checklist

This checklist contains all required and optional inputs, credentials, connection strings, API keys, and environment variables needed to configure and run the **Nigerian News Grid / News Stand NG** platform across local development, Docker Compose, Linux VPS, and production cloud environments.

---

## 📋 Quick Setup Status

| Category | Total Items | Mandatory | Recommended / Optional |
| :--- | :---: | :---: | :---: |
| [1. Core Infrastructure & Inter-Service Security](#1-core-infrastructure--inter-service-security-mandatory) | 6 | 6 | 0 |
| [2. External AI & Media API Integrations](#2-external-ai--media-api-integrations) | 3 | 1 | 2 |
| [3. SEO, Search Engines & Analytics](#3-seo-search-engines--analytics) | 3 | 0 | 3 |
| [4. User Authentication & Email Notifications](#4-user-authentication--email-notifications) | 2 | 0 | 2 |
| [5. Web Frontend & Mobile App Settings](#5-web-frontend--mobile-app-settings) | 4 | 2 | 2 |
| [6. Service Tuning & Operational Controls](#6-service-tuning--operational-controls) | 4 | 0 | 4 |

---

## 1. Core Infrastructure & Inter-Service Security (Mandatory)

These inputs are required for the backend services to boot, connect to the database, communicate securely with each other, and authenticate write requests.

### - [ ] 1. Internal Service API Key (`API_KEY`)
- **Required For**: Inter-service authentication & rate-limit bypass.
- **Where to Provide**:
  - Root environment file: [`.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/.env) (`API_KEY=...`)
  - Docker Compose: [`docker-compose.yml`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/docker-compose.yml) (`ApiKey=${API_KEY}`)
  - Microservice configs:
    - [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) (`"ApiKey": "..."`)
    - [`src/NewsScraperService/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsScraperService/appsettings.json) (`"ApiKey": "..."`)
    - [`src/AdminDashboard/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/AdminDashboard/appsettings.json) (`"ApiKey": "..."`)
    - [`src/TtsWorker/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/TtsWorker/appsettings.json) (`"ApiKey": "..."`)
- **Format**: High-entropy 32+ character random string (e.g. `d8f93a10e82b4c199f7d4e3a2b1c098a5e7b3c2d1e0f`).
- **Purpose**: Authenticates internal write calls (`POST /api/v1/articles/ingest`, syndication sync, audio briefing uploads) via the `X-Api-Key` HTTP header.
- **Fallback**: Fails-closed in Production/Staging with `503 Service Unavailable` if missing. In Development mode, logs a warning.

---

### - [ ] 2. Database Provider (`DbProvider`)
- **Required For**: [NewsApi](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi).
- **Where to Provide**:
  - [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) (`"DbProvider": "sqlite"` or `"postgres"`)
  - Environment variable / [`.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/.env): `DbProvider=postgres`
- **Allowed Values**: `"sqlite"` or `"postgres"`
- **Purpose**: Dictates whether Entity Framework Core binds to SQLite (`Microsoft.EntityFrameworkCore.Sqlite`) or PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`).

---

### - [ ] 3. Database Connection String (`ConnectionStrings:NewsDb`)
- **Required For**: [NewsApi](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi).
- **Where to Provide**:
  - [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) (`ConnectionStrings:NewsDb`)
  - Environment variable / [`.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/.env): `ConnectionStrings__NewsDb=...`
- **Format**:
  - **SQLite (Local Dev)**: `Data Source=news.db`
  - **PostgreSQL (Docker / VPS / Prod)**: `Host=postgres;Port=5432;Database=newsgrid;Username=newsuser;Password=your_password`
- **Purpose**: Connects the API to the central relational store for articles, topics, embeddings, storylines, feedbacks, and users.

---

### - [ ] 4. PostgreSQL Database Container Credentials
- **Required For**: Database container in [`docker-compose.yml`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/docker-compose.yml).
- **Where to Provide**:
  - [`.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/.env) or VPS environment:
    - `POSTGRES_DB=newsgrid`
    - `POSTGRES_USER=newsuser`
    - `POSTGRES_PASSWORD=your_secure_password`
- **Purpose**: Initializes the PostgreSQL 16 Alpine container user, database, and authentication.

---

### - [ ] 5. Admin Dashboard Portal Credentials
- **Required For**: [AdminDashboard](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/AdminDashboard).
- **Where to Provide**:
  - [`src/AdminDashboard/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/AdminDashboard/appsettings.json) (`AdminAuth:Username` & `AdminAuth:Password`)
  - Environment variables: `ADMIN_USERNAME=admin` and `ADMIN_PASSWORD=your_secure_password`
- **Format**:
  - Username: string (default: `admin`)
  - Password: 10+ characters (default: `AdminPass123!`)
- **Purpose**: Authenticates administrative staff accessing the portal at `/Login` ([Login.cshtml.cs](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/AdminDashboard/Pages/Login.cshtml.cs)).

---

### - [ ] 6. Shared Audio Storage Directory
- **Required For**: [TtsWorker](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/TtsWorker) & [NewsApi](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi).
- **Where to Provide**:
  - Docker Compose: mapped volume `audio-data:/app/data/audio` in [`docker-compose.yml`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/docker-compose.yml)
  - Config key: `Tts:AudioStoragePath` or env var `Tts__AudioStoragePath`
- **Format**: Absolute or relative directory path (e.g. `/app/data/audio` in Linux/Docker, `data/audio` on Windows).
- **Purpose**: Directory where synthesized `.mp3` audio files are written by the background worker and served to readers by the API.

---

## 2. External AI & Media API Integrations

These keys power intelligent features: vector embeddings, storyline clustering, YouTube video news, and audio briefings.

### - [ ] 7. Google Gemini API Key (`GEMINI_API_KEY`)
- **Priority**: **Critical for AI & Semantic Story Clustering**
- **Where to Provide**:
  - [`.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/.env) (`GEMINI_API_KEY=AIzaSy...`)
  - [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) (`Gemini:ApiKey`)
  - Docker Compose: `Gemini__ApiKey=${GEMINI_API_KEY}`
- **How to Obtain**: Create a free API key at [Google AI Studio](https://aistudio.google.com/).
- **Used By**:
  1. [`GeminiEmbeddingProvider.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Services/Embeddings/GeminiEmbeddingProvider.cs): Generates `text-embedding-004` vector embeddings for semantic story deduplication and clustering.
  2. [`SeoFeedbackLoopService.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Services/SeoFeedbackLoopService.cs): Automated generative SEO titles, meta descriptions, and FAQ snippets.
  3. [`LlmEntityExtractor.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsScraperService/Services/LlmEntityExtractor.cs): Optional Tier-2 entity extraction from news articles (`gemini-1.5-flash`).
- **Fallback**: Falls back to deterministic title heuristics and rule-based Nigerian Gazetteer entity extraction.

---

### - [ ] 8. YouTube Data API v3 Key (`YOUTUBE_API_KEY`)
- **Priority**: **Recommended**
- **Where to Provide**:
  - [`.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/.env) (`YOUTUBE_API_KEY=AIzaSy...`)
  - [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) (`YouTube:ApiKey`)
  - Docker Compose: `YouTube__ApiKey=${YOUTUBE_API_KEY}`
- **How to Obtain**: Enable *YouTube Data API v3* in [Google Cloud Console](https://console.cloud.google.com/) and create an API Key.
- **Used By**:
  - [`YouTubeFeedService.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Services/YouTubeFeedService.cs): Fetches video uploads from Nigerian channels (Channels Television, Arise News, TVC News, BBC News Yoruba/Hausa, etc.) and Nigerian trending news videos (`regionCode=NG`, `videoCategoryId=25`).
- **Fallback**: Skips video news syndication if missing.

---

### - [ ] 9. Google Cloud Text-to-Speech API Key (`GCP_TTS_API_KEY`)
- **Priority**: **Optional** (Free fallback included)
- **Where to Provide**:
  - [`.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/.env) (`GCP_TTS_API_KEY=AIzaSy...`)
  - [`src/TtsWorker/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/TtsWorker/appsettings.json) (`GoogleCloud:ApiKey`)
  - Docker Compose: `GoogleCloud__ApiKey=${GCP_TTS_API_KEY}`
- **How to Obtain**: Enable *Cloud Text-to-Speech API* in [Google Cloud Console](https://console.cloud.google.com/) and create an API Key.
- **Used By**:
  - [`GoogleCloudTtsSynthesizer.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/TtsWorker/Synthesizers/GoogleCloudTtsSynthesizer.cs): Tier-1 neural voice synthesis in Nigerian English (`en-NG-Neural2-A`).
- **Fallback**: If empty, [CompositeTtsSynthesizer](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/TtsWorker/Synthesizers/CompositeTtsSynthesizer.cs) seamlessly uses [EdgeNeuralTtsSynthesizer](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/TtsWorker/Synthesizers/EdgeNeuralTtsSynthesizer.cs) (`en-NG-EzinneNeural`), which requires **no API key**.

---

## 3. SEO, Search Engines & Analytics

### - [ ] 10. IndexNow Protocol Key (`INDEXNOW_KEY`)
- **Priority**: **Recommended** (Fast Search Engine Indexing)
- **Where to Provide**:
  - [`.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/.env) (`INDEXNOW_KEY=3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d`)
  - [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) (`IndexNow:Key`)
  - [`src/NigerianNewsGrid.Web/.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NigerianNewsGrid.Web/.env) (`INDEXNOW_KEY=...`)
- **How to Obtain**: Generate any 32-character hex key at [IndexNow.org](https://www.indexnow.org/).
- **Used By**:
  - [`IndexNowBackgroundService.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Services/IndexNowBackgroundService.cs): Instantly informs Bing, Yandex, and Seznam whenever news is published.
  - Web route `/{key}.txt`: Serves verification payload to search crawlers.

---

### - [ ] 11. Google Search Console Service Account JSON (`GSC_CREDENTIALS_JSON`)
- **Priority**: **Optional**
- **Where to Provide**:
  - Environment variable: `GSC_CREDENTIALS_JSON=/path/to/service-account.json`
  - [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) (`SearchConsole:ServiceAccountKeyPath`)
- **How to Obtain**: Create a Service Account in Google Cloud, grant Search Console viewer permissions, and download the JSON key.
- **Used By**:
  - [`SearchConsoleService.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Services/SearchConsoleService.cs): Real-time impressions, CTR, average rankings, and index coverage diagnostics.
- **Fallback**: Built-in data-grounded simulation engine generates realistic metrics based on actual database articles.

---

### - [ ] 12. Google Analytics 4 Measurement ID (`VITE_GA_MEASUREMENT_ID`)
- **Priority**: **Recommended**
- **Where to Provide**:
  - [`src/NigerianNewsGrid.Web/.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NigerianNewsGrid.Web/.env) (`VITE_GA_MEASUREMENT_ID=G-XXXXXXXXXX`)
  - Vercel Project Settings > Environment Variables: `VITE_GA_MEASUREMENT_ID`
- **How to Obtain**: From Google Analytics Admin > Data Streams > Web Stream.
- **Used By**: Web frontend telemetry tracking pageviews, reading depth, and article interactions.

---

## 4. User Authentication & Email Notifications

### - [ ] 13. Google OAuth Client ID (`Google:ClientId`)
- **Priority**: **Recommended**
- **Where to Provide**:
  - [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) (`Google:ClientId`)
  - Web client environment (if custom Google Sign-In button used)
- **Format**: `*.apps.googleusercontent.com`
- **Current Default**: `434931027697-0ofh3nptkv409vju55od3fhrfbkccu15.apps.googleusercontent.com`
- **Used By**:
  - [`AuthController.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Controllers/AuthController.cs): Cryptographically validates Google ID tokens submitted by readers to leave comments, react, and personalize categories.

---

### - [ ] 14. SMTP Email Server Configuration
- **Priority**: **Optional**
- **Where to Provide**:
  - [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) under section `"Feedback:Smtp"`:
    - `"Host"`: SMTP host (e.g., `smtp.mailgun.org`, `smtp.sendgrid.net`, or `smtp.gmail.com`)
    - `"Port"`: `587` or `465`
    - `"EnableSsl"`: `true`
    - `"UserName"`: SMTP username / API key
    - `"Password"`: SMTP password / secret
    - `"FromEmail"`: `no-reply@nigeriannewsgrid.com`
    - `"FromName"`: `News Stand NG`
    - `"RecipientEmail"`: `feedback@nigeriannewsgrid.com`
- **Used By**:
  - [`FeedbackService.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Services/FeedbackService.cs): Dispatches user feedback messages directly to administrator email.
  - [`AuthController.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Controllers/AuthController.cs): Sends 6-digit email OTP login verification codes.
- **Fallback**: In development mode or when SMTP host is blank, feedback records are saved to the database, and OTP verification codes are included directly in the JSON response for debugging.

---

## 5. Web Frontend & Mobile App Settings

### - [ ] 15. Web App API Endpoints
- **Where to Provide**: [`src/NigerianNewsGrid.Web/.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NigerianNewsGrid.Web/.env) or Vercel Environment Variables:
  - `VITE_API_BASE`: `/api/v1` (client-side calls through reverse proxy) or `https://api.newsstand.ng/api/v1`
  - `API_BASE_URL`: `https://api.newsstand.ng` (SSR runtime calling the backend directly)
  - `SITE_URL`: `https://newsstand.ng` (canonical URL generation for sitemaps)

---

### - [ ] 16. CORS Allowed Origins
- **Where to Provide**:
  - [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) (`AllowedOrigins: [...]`)
  - Environment variables: `AllowedOrigins__0=https://newsstand.ng`, `AllowedOrigins__1=https://www.newsstand.ng`
- **Purpose**: Allows browser-based fetch calls from your web frontend domain without browser CORS blocking.

---

### - [ ] 17. Mobile App API Base URL
- **Where to Provide**:
  - Code constant: [`AppNetworkConstants.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Constants/AppNetworkConstants.cs) (`ProductionApiUrl = "https://api.nigeriannewsgrid.com"`)
  - Runtime override: Accessible inside the MAUI app via **Settings > Server Endpoint**.

---

### - [ ] 18. Google AdMob App IDs & Ad Units (Mobile)
- **Where to Provide**:
  - Android Manifest: [`NigerianNewGrid/Platforms/Android/AndroidManifest.xml`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Platforms/Android/AndroidManifest.xml)
    - `com.google.android.gms.ads.APPLICATION_ID` (`ca-app-pub-0810356418854639~3397453941`)
  - Centralized Code: [`AdConstants.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Constants/AdConstants.cs)
    - Production banner, native, video, and app open ad unit IDs for Android and iOS.
- **Note**: In `DEBUG` mode, the app automatically serves Google's official test ad unit IDs to avoid AdMob policy penalties during development.

---

## 6. Service Tuning & Operational Controls

| Check | Flag / Key | Default | Where to Provide | Description |
| :---: | :--- | :---: | :--- | :--- |
| [ ] | `SCRAPER_RUN_ONCE` | `false` | Env Var | Set to `true` to scrape feeds once and terminate immediately (useful for Cloud Run Jobs or cron). |
| [ ] | `Scraper:IntervalMinutes` | `15` | [`src/NewsScraperService/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsScraperService/appsettings.json) | Frequency in minutes between automated RSS and sitemap scraping cycles. |
| [ ] | `TTS_RUN_ONCE` | `false` | Env Var | Set to `true` to synthesize the current briefing cycle once and exit. |
| [ ] | `RateLimiting:PermitLimit` | `120` | [`src/NewsApi/appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) | Sliding-window request rate limit per minute per remote IP address. |

---

## 7. Ready-to-Copy Environment Templates

### Template A: Root `.env` (Docker Compose & Backend Services)
Create or update [`.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/.env) at the project root:

```env
# ══════════════════════════════════════════════════════════════
# DATABASE (PostgreSQL 16)
# ══════════════════════════════════════════════════════════════
POSTGRES_DB=newsgrid
POSTGRES_USER=newsuser
POSTGRES_PASSWORD=SetYourStrongPassword123!
DbProvider=postgres
ConnectionStrings__NewsDb=Host=postgres;Port=5432;Database=newsgrid;Username=newsuser;Password=SetYourStrongPassword123!

# ══════════════════════════════════════════════════════════════
# INTERNAL SERVICE SECURITY (Shared Secret)
# ══════════════════════════════════════════════════════════════
API_KEY=AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI

# ══════════════════════════════════════════════════════════════
# EXTERNAL APIS
# ══════════════════════════════════════════════════════════════
# Google Gemini (Google AI Studio Key)
GEMINI_API_KEY=your_gemini_api_key_here
AiEnrichment__GeminiEnabled=true

# YouTube Data API v3 Key
YOUTUBE_API_KEY=your_youtube_api_key_here

# Google Cloud Text-to-Speech (Leave empty to use free Edge Neural TTS)
GCP_TTS_API_KEY=

# IndexNow Key for Instant Search Indexing
INDEXNOW_KEY=3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d

# ══════════════════════════════════════════════════════════════
# ADMIN DASHBOARD LOGIN
# ══════════════════════════════════════════════════════════════
ADMIN_USERNAME=admin
ADMIN_PASSWORD=AdminPass123!

# ══════════════════════════════════════════════════════════════
# CORS ALLOWLIST (Frontends)
# ══════════════════════════════════════════════════════════════
AllowedOrigins__0=https://newsstand.ng
AllowedOrigins__1=https://www.newsstand.ng
AllowedOrigins__2=http://localhost:5173
```

---

### Template B: Web App `.env` ([`src/NigerianNewsGrid.Web/.env`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NigerianNewsGrid.Web/.env))
```env
# Public client-side API endpoint
VITE_API_BASE=/api/v1

# Google Analytics 4 Measurement ID
VITE_GA_MEASUREMENT_ID=G-7GBS0F0WZQ

# IndexNow Search Engine Key
INDEXNOW_KEY=3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d

# Server-Side Rendering (SSR) Backend URL
API_BASE_URL=http://localhost:56193
```
