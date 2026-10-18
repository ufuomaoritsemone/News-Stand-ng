# Nigerian News Grid — Production Deployment Guide (Google Cloud)

This guide provides a production-grade deployment walkthrough for the Nigerian News Grid backend microservices to Google Cloud Platform (GCP), cost estimates for testing/staging with the GCP $300 Free Trial credit, and instructions for connecting the cross-platform .NET MAUI mobile application.

---

## 1. System Architecture & Microservices Overview

The backend is composed of four containerized .NET 10 microservices and a PostgreSQL database:

| Component | Directory | Role & Technology |
| :--- | :--- | :--- |
| **News API** | `src/NewsApi` | ASP.NET Core 10 Web API, EF Core, PostgreSQL/SQLite, seekable HTTP 206 audio streaming, API key & rate limiting middleware, OpenTelemetry observability.<br>• **ML.NET Engine**: In-process Time-Series SSA Anomaly Detection (`Microsoft.ML.TimeSeries`) & FastTree Predictive CTR Regression (`Microsoft.ML.FastTree`).<br>• **AI Feedback Loop & SEO Monitor**: `SeoMonitoringBackgroundService` (4h autonomous cycles), `SeoFeedbackLoopService` (Gemini 1.5 Flash + offline algorithmic headline/FAQ generator), automated backfills, thin-content guardrails, and real-time IndexNow batch dispatcher.<br>• **Smart Enrichment & Vector Clustering**: TextRank/LexRank graph summarization, 768-dim embeddings, 72h Greedy Leader story clustering, and Dual-Mode Google Search Console & Crawl Diagnostics. |
| **Admin Dashboard** | `src/AdminDashboard` | ASP.NET Core 10 Razor Pages administration portal for source curation, sponsored content scheduling, video channel monitoring, and enterprise SEO management.<br>• **Google Search Console & Crawl Cockpit**: Real-time clicks, impressions, CTR, position time-series, and on-demand live URL inspector.<br>• **SEO Health Scorecard (0–100)**: Multi-factor grading (Indexation, Schema, Speed/Crawl, E-E-A-T).<br>• **ML.NET Anomaly Alert Center & CTR Opportunities**: Active ranking decay alerts and 1-click self-healing remediation triggers. |
| **News Stand NG Web** | `src/NigerianNewsGrid.Web` | React Router v7 Framework Mode (Node.js SSR) delivering server-rendered HTML, dynamic Open Graph / Twitter meta, JSON-LD structured data (`NewsArticle`, `Dataset`, `Place`, `FAQPage`), XML sitemaps, RSS 2.0 feeds, and IndexNow protocol verification. |
| **News Scraper Service** | `src/NewsScraperService` | Continuous background worker or scheduled Cloud Run Job. Features Google News sitemap discovery, RSS fallbacks, JSON-LD/SmartReader extraction, multi-tier entity extraction (Gazetteer + LLM), ML topical classification, and editorial/opinion detection. |
| **TTS Worker Service** | `src/TtsWorker` | Tri-Tier Neural Speech Synthesis service generating broadcast audio briefings for morning (8:00 AM WAT) and evening (6:00 PM WAT) broadcasts.<br>• **Tier 1:** Google Cloud Text-to-Speech (`en-NG-Neural2-A`).<br>• **Tier 2:** Microsoft Edge Neural (`en-NG-EzinneNeural` & `en-NG-AbeoNeural`) — 100% free with authentic Nigerian accents.<br>• **Tier 3:** Local offline safety net container ensuring 0% crash rate. |
| **Database** | Cloud SQL | Managed PostgreSQL 16 instance. |
| **Mobile Client** | `NigerianNewGrid/` | Cross-platform .NET MAUI mobile app (Android, iOS, macOS, Windows) featuring local caching, offline reading, on-device native TTS narration, and dynamic endpoint resolution. |

---

## 2. Cost & Free Trial Coverage ($300 Credit)

The $300 Google Cloud Free Trial credit (valid for 90 days) will easily cover 100% of your testing and staging operations.

### Estimated Monthly Testing / Staging Cost

| Service | Configuration | Estimated Cost |
| :--- | :--- | :--- |
| **Cloud Run** (News API & Admin Portal) | Serverless (Scales to 0 when idle) | **$0.00 – $2.00 / mo** (Well within the 2M requests/mo Free Tier) |
| **Cloud Run Jobs** (News Scraper) | Scheduled every 15–20 min (~30s run time) | **$0.50 – $1.50 / mo** (Bills only during active CPU cycles) |
| **Cloud Scheduler** | 1 cron job triggering scraper | **$0.00 / mo** (First 3 jobs/month are free) |
| **Cloud SQL** (PostgreSQL) | `db-f1-micro` (Shared CPU, 0.6 GB RAM, 10 GB SSD) | **$8.00 – $12.00 / mo** |
| **Artifact Registry** (Docker Images) | ~2 GB container image storage | **~$0.20 / mo** |
| **Cloud Storage** (Audio volume) | Standard bucket for shared audio briefings | **~$0.10 / mo** |
| **Total Estimated Cost** | | **~$10 – $16 / month** |

> **Summary:** Over a 3-month testing period, you will only spend approximately **$30 to $50 of your $300 credit**, leaving more than 80% of your free credits intact.

---

## 2.5 API Keys, Secrets & Configuration Requirements

News Stand NG implements a **dual-layer resiliency pattern**: all microservices and machine learning models are designed to operate out-of-the-box with **$0 cloud cost and zero mandatory API keys** via built-in local algorithms and fallback engines. When cloud API keys are supplied, the platform seamlessly upgrades to Google Gemini 2.5 Flash, Neural2 speech synthesis, and live Google Search Console telemetry.

### Comprehensive Configuration & Environment Variable Matrix

| Variable Name | Environment / Secret Variable | Target Service | Required in Prod? | Default / Offline Fallback if Omitted | Purpose & Feature Impact |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `Gemini:ApiKey` | `GEMINI_API_KEY`<br>`Gemini__ApiKey` | `NewsApi` | **Optional** (Highly Recommended) | • `LocalFallbackEmbeddingProvider` (768-dim)<br>• `TextRank-Extractive` graph centrality<br>• Algorithmic headline & FAQ rule engine | **Powers 3 AI Capabilities:**<br>1. **Vector Embeddings** (`gemini-embedding-001`) for 72h Greedy Leader story clustering & related coverage.<br>2. **AI Article Enrichment** (`gemini-2.5-flash`) for abstractive key takeaways & FAQPage rich snippet generation.<br>3. **Continuous SEO Optimization**: Generative AI headline rewrite suggestions ($\le 60$ chars) and SERP accordion FAQ pairs. |
| `AiEnrichment:GeminiEnabled` | `AiEnrichment__GeminiEnabled` | `NewsApi` | **Optional** | `false` | Master toggle enabling Gemini 2.5 Flash enrichment API calls in `ArticleEnrichmentService`. Set to `true` when `Gemini:ApiKey` is provided. |
| `Clustering:SimilarityThreshold` | `Clustering__SimilarityThreshold` | `NewsApi` | **Optional** | `0.48` (local embeddings)<br>`0.58` (Gemini embeddings) | Cosine similarity + title-keyword overlap threshold for Greedy Leader cross-publisher story clustering (`StoryClusteringService`). |
| `Scraper:MaxConcurrency` | `SCRAPER_MAX_CONCURRENCY`<br>`Scraper__MaxConcurrency` | `NewsScraperService` | **Optional** | `4` | Maximum concurrent publisher feed scrapers (`Parallel.ForEachAsync` degree of parallelism in `ScraperWorker`). |
| `ApiKey` | `API_KEY`<br>`ApiKey` | `NewsApi`<br>`AdminDashboard`<br>`NewsScraperService`<br>`TtsWorker` | **Required** | Fails closed with HTTP 503 if missing in `Production` / `Staging` | Shared secret protecting all internal write endpoints (`POST /api/v1/articles/ingest`, `/api/v1/video-stories/trending/sync`, `/api/v1/seo/trigger-remediation`). |
| `IndexNow:Key` | `IndexNow__Key`<br>`INDEXNOW_KEY` | `NewsApi`<br>`NigerianNewsGrid.Web` | **Recommended** | `3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d` | Hexadecimal API key used by `IndexNowBackgroundService` and `SeoFeedbackLoopService` to notify Bing and Yandex of published and modified URLs. The web app serves this key at `/{key}.txt`. |
| `GoogleCloud:ApiKey` | `GCP_TTS_API_KEY`<br>`GoogleCloud__ApiKey` | `TtsWorker`<br>`NewsApi` | **Optional** | Microsoft Edge Neural (`en-NG-EzinneNeural` & `en-NG-AbeoNeural`) | Activates Tier 1 Google Cloud Text-to-Speech (`en-NG-Neural2-A`). When omitted, Tier 2 Edge Neural delivers high-quality Nigerian English audio for **$0**. |
| `SearchConsole:ServiceAccountKeyPath` | `GSC_CREDENTIALS_JSON` | `NewsApi` | **Optional** | Grounded in-memory search telemetry engine based on active database articles & state registry | Connects to the official Google Search Console API (`searchconsole.googleapis.com`) for real-time clicks, impressions, CTR, position, and URL inspection. |
| `YouTube:ApiKey` | `YOUTUBE_API_KEY`<br>`YouTube__ApiKey` | `NewsApi` | **Optional** | Ingests from RSS & web scrapers only | Queries the YouTube Data API v3 for trending Nigerian video news broadcasts and channel uploads. |
| `ConnectionStrings:NewsDb` | `ConnectionStrings__NewsDb` | `NewsApi` | **Required** | `Data Source=news.db` (SQLite) | Database connection string for PostgreSQL in production (`Host=...;Database=newsgrid;Username=...;Password=...`). |
| `DbProvider` | `DbProvider` | `NewsApi` | **Required** | `sqlite` | Explicit database engine selector: `postgres` for production Cloud SQL or Docker Compose; `sqlite` for lightweight local development. |
| `ASPNETCORE_ENVIRONMENT` | `ASPNETCORE_ENVIRONMENT` | All .NET services | **Required** | `Development` | ASP.NET Core environment mode: set to `Production` to enable strict API key validation and production security middleware. |

---

### Step-by-Step: Obtaining and Entering Your Keys

#### 1. Google Gemini API Key (`Gemini:ApiKey` / `GEMINI_API_KEY`)

##### How to Obtain:
1. Visit [Google AI Studio](https://aistudio.google.com/) (or [Google Cloud Vertex AI Console](https://console.cloud.google.com/vertex-ai)).
2. Sign in with your Google Account.
3. Click **Get API key** > **Create API key in new project** (or link your existing GCP project).
4. Copy the generated API key (starts with `AIzaSy...`).

##### Where to Enter It:
* **Option A: Google Cloud Run (Console Web UI)**:
  1. Open [Google Cloud Run](https://console.cloud.google.com/run).
  2. Select the `news-api` service and click **Edit & Deploy New Revision**.
  3. Expand **Container, Volumes, Networking, Security** > select the **Container** tab.
  4. Under **Environment variables**, click **+ Add Variable**:
     - **Name:** `Gemini__ApiKey` | **Value:** `AIzaSy...YOUR_KEY`
     - **Name:** `AiEnrichment__GeminiEnabled` | **Value:** `true`
  5. Click **Deploy**.

* **Option B: Google Cloud Run (CLI)**:
  ```bash
  gcloud run services update news-api \
      --region=us-central1 \
      --update-env-vars="Gemini__ApiKey=AIzaSy...YOUR_KEY,AiEnrichment__GeminiEnabled=true"
  ```

* **Option C: Google Secret Manager (Enterprise Production Recommended)**:
  ```bash
  # 1. Store secret in Secret Manager
  echo -n "AIzaSy...YOUR_KEY" | gcloud secrets create gemini-api-key --data-file=-

  # 2. Grant Cloud Run service account permission to read the secret
  PROJECT_NUMBER=$(gcloud projects describe YOUR_PROJECT_ID --format="value(projectNumber)")
  gcloud secrets add-iam-policy-binding gemini-api-key \
      --member="serviceAccount:${PROJECT_NUMBER}-compute@developer.gserviceaccount.com" \
      --role="roles/secretmanager.secretAccessor"

  # 3. Mount secret as environment variable in Cloud Run
  gcloud run services update news-api \
      --region=us-central1 \
      --set-secrets="Gemini__ApiKey=gemini-api-key:latest" \
      --update-env-vars="AiEnrichment__GeminiEnabled=true"
  ```

* **Option D: Docker Compose (`.env` file)**:
  Add to `.env` in the repository root:
  ```bash
  GEMINI_API_KEY=AIzaSy...YOUR_KEY
  ```

* **Option E: Local Development (.NET User Secrets)**:
  Run directly inside `src/NewsApi/`:
  ```bash
  dotnet user-secrets set "Gemini:ApiKey" "AIzaSy...YOUR_KEY"
  dotnet user-secrets set "AiEnrichment:GeminiEnabled" "true"
  ```

---

#### 2. IndexNow Protocol Domain Key (`IndexNow:Key` / `IndexNow__Key`)

##### How to Obtain / Generate:
IndexNow accepts any random hexadecimal string between 8 and 128 characters (e.g. 32-character MD5/UUID hex).
```bash
# Generate a 32-character hex key via terminal
openssl rand -hex 16
# Example output: 3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d
```

##### Where to Enter It:
* **In `news-api`**:
  Set environment variable `IndexNow__Key=3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d`.
* **In `news-web` (`NigerianNewsGrid.Web`)**:
  Set environment variable `IndexNow__Key=3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d`.
* **Verification**:
  The web application will automatically serve this key file at:
  `https://newsstand.ng/3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d.txt`
  This satisfies Bing, Yandex, and IndexNow ownership verification requirements.

---

#### 3. Inter-Service Security Key (`ApiKey` / `API_KEY`)

##### How to Obtain / Generate:
Generate a cryptographically secure 256-bit base64 or alphanumeric secret string:
```bash
openssl rand -base64 32
# Example output: P+7xG5tYzW9...YourSuperSecretProductionApiKey123!
```

##### Where to Enter It:
Provide the **exact same key** to all communicating microservices:
* **`news-api`**: `--set-env-vars="ApiKey=YourSuperSecretProductionApiKey123!"`
* **`admin-dashboard`**: `--set-env-vars="ApiKey=YourSuperSecretProductionApiKey123!"`
* **`news-scraper-job`**: `--set-env-vars="ApiKey=YourSuperSecretProductionApiKey123!"`
* **`tts-worker-job`**: `--set-env-vars="ApiKey=YourSuperSecretProductionApiKey123!"`

---

#### 4. Google Search Console API Credentials (`GSC_CREDENTIALS_JSON`)

##### How to Obtain:
1. In the Google Cloud Console, enable the **Google Search Console API** (`searchconsole.googleapis.com`).
2. Navigate to **IAM & Admin** > **Service Accounts** > click **+ Create Service Account**.
   - **Name:** `newsgrid-gsc-reader`
   - **Role:** No project-wide role required (access is granted within Search Console).
3. Click the created service account > **Keys** > **Add Key** > **Create new key** > select **JSON** > download the key file.
4. Copy the service account email (e.g. `newsgrid-gsc-reader@YOUR_PROJECT_ID.iam.gserviceaccount.com`).
5. Open [Google Search Console](https://search.google.com/search-console).
6. Select property `newsstand.ng` > **Settings** > **Users and permissions** > **Add user**.
7. Paste the service account email, select **Full** or **Restricted** permission, and click **Add**.

##### Where to Enter It:
* Set `GSC_CREDENTIALS_JSON` environment variable in `news-api` to the raw JSON string, or mount the JSON file and set `SearchConsole__ServiceAccountKeyPath=/path/to/key.json`.
* *(Note: If omitted, `SearchConsoleService` automatically engages the built-in grounded simulation engine using your active database entities, sitemaps, and state hubs with 0 external dependencies).*

---

## 2.6 Pre-Deployment Action Items & Readiness Checklist (Tasks Before Go-Live)

Before executing production deployment to Google Cloud Run, Compute Engine, or custom infrastructure, complete and verify the following prioritized pre-deployment tasks:

### Priority 1: Production Secrets & Cloud API Keys (Mandatory & Recommended)

- [ ] **1. Google Gemini AI API Key (`Gemini:ApiKey` / `GEMINI_API_KEY`)**
  - **Purpose**: Powers abstractive takeaway synthesis, `FAQPage` rich snippets, and 768-dimensional embeddings for 72-hour Greedy Leader story clustering.
  - **Action**: Obtain key from [Google AI Studio](https://aistudio.google.com/) and configure `Gemini__ApiKey=AIzaSy...` and `AiEnrichment__GeminiEnabled=true` in Cloud Run environment variables or Secret Manager.
  - *Fallback*: If omitted, `ArticleEnrichmentService` runs on the local 0-cost extractive TextRank engine with 0% hallucination risk.

- [ ] **2. YouTube Data API Key (`YouTube:ApiKey` / `YOUTUBE_API_KEY`)**
  - **Purpose**: Syncs trending Nigerian video news broadcasts from Channels TV, Arise News, TVC News, and AIT.
  - **Action**: Add a valid YouTube Data API v3 key in [`appsettings.json`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/appsettings.json) or set `YouTube__ApiKey` in environment variables to eliminate HTTP 400 Bad Request warnings in [`YouTubeFeedService.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Services/YouTubeFeedService.cs).
  - *Fallback*: The automated XML Atom feed crawler will run as a secondary background fallback.

- [ ] **3. Google Search Console Service Account (`GSC_CREDENTIALS_JSON` / `SearchConsole:ServiceAccountKeyPath`)**
  - **Purpose**: Streams real-time impressions, click-through rates (CTR), search positions, and automated URL inspection into the Admin Dashboard.
  - **Action**: Create a GCP service account, delegate read permissions in [Google Search Console](https://search.google.com/search-console) for `newsstand.ng`, and provide the credentials via Secret Manager or file path.
  - *Fallback*: Built-in deterministic simulation engine generates grounded telemetry based on active database records.

- [ ] **4. Inter-Service Security Secret (`ApiKey` / `API_KEY`)**
  - **Purpose**: Protects internal ingestion pipelines (`POST /api/v1/articles/ingest`) and trigger endpoints from unauthorized external access.
  - **Action**: Generate a cryptographically secure 64-character token (`openssl rand -hex 32`) and inject into `NewsApi`, `AdminDashboard`, `NewsScraperService`, and `TtsWorker`.

- [ ] **5. IndexNow Protocol Verification Key (`IndexNow:Key` / `INDEXNOW_KEY`)**
  - **Purpose**: Immediate URL indexing by Bing and Yandex upon story publication.
  - **Action**: Confirm the key `3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d` (or custom 32-hex key) is configured in both `NewsApi` and `NigerianNewsGrid.Web`, and verified at `https://newsstand.ng/{key}.txt`.

---

### Priority 2: Video Player & Audio Experience Polish (Completed)

- [x] **6. Video Player Modal / Inline Embed Integration ([`VideoSection.jsx`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NigerianNewsGrid.Web/src/components/VideoSection.jsx))**
  - **Status**: Implemented — video cards open a responsive `16:9` YouTube modal/inline player for Arise TV, Channels TV, and TVC broadcasts.

- [x] **7. Neural Audio Briefing Player Wiring ([`AudioBriefingBar.jsx`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NigerianNewsGrid.Web/src/components/AudioBriefingBar.jsx))**
  - **Status**: Implemented — wired to `GET /api/v1/audio/briefings/latest` (`HTTP 206 Partial Content`) with compact inline presentation inside [`WelcomeBanner.jsx`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NigerianNewsGrid.Web/src/components/AudioBriefingBar.jsx), live scrubbing, and pause/resume controls.

---

### Priority 3: Local Staging Verification & Container Validation

- [ ] **8. Local Docker Compose Containerization Test**
  - **Purpose**: Validate container images, dependency resolution, internal network routing, and health checks before pushing to Google Artifact Registry.
  - **Action**: Execute local build and compose smoke test from the repository root:
    ```bash
    docker compose up --build -d
    ```
  - **Verification**: Check container status with `docker compose ps` to ensure all 6 services (`postgres`, `news-api`, `admin-dashboard`, `news-web`, `news-scraper-service`, `tts-worker`) reach healthy status.

- [ ] **9. Database Schema Migration Verification**
  - **Purpose**: Ensure PostgreSQL schema and entity tables (`Articles`, `StoryClusters`, `ClusterArticles`, `Topics`, `TopicArticles`, `AudioBriefings`, `VideoStories`, `SeoAnomalyEvents`, `MarketDataRecords`) initialize cleanly.
  - **Action**: Run `dotnet ef database update --project src/NewsApi` against the target PostgreSQL instance or verify automated initialization logic on container startup.

- [ ] **10. Pre-Flight API & SSR Smoke Testing**
  - **Action**: Run local or staging verification curls:
    ```bash
    curl -s http://localhost:56193/healthz/readiness
    curl -s http://localhost:56193/api/v1/articles?pageSize=5
    curl -s http://localhost:56193/api/v1/stories/trending?limit=6&windowHours=72
    curl -s http://localhost:56193/api/v1/data/naira-fx
    curl -s http://localhost:56193/api/v1/states
    curl -s -i http://localhost:5173/ | grep "HTTP/1.1 200"
    ```

---

## 3. Approach A: Cloud Run + Cloud SQL (Recommended Serverless Architecture)

This setup is fully serverless, highly available, and autoscaling. Google Cloud automatically provisions and renews SSL/TLS certificates for custom domains and Cloud Run URLs.

### 3.1 Architecture Diagram

```
                   ┌──────────────────────────────────────┐
                   │       .NET MAUI Mobile App           │
                   └──────────────────┬───────────────────┘
                                      │ HTTPS
                                      ▼
                   ┌──────────────────────────────────────┐
                   │        Cloud Run: news-api           │
                   │  - EF Core Migrations                │
                   │  - API Key & Rate Limiter            │
                   │  - Seekable HTTP 206 Audio Streaming │
                   └───────┬──────────────────────┬───────┘
                           │ Unix Domain Socket   │ Shared GCS Mount
                           ▼                      ▼
┌─────────────────────────────────┐   ┌─────────────────────────────────┐
│     Cloud SQL (PostgreSQL 16)   │   │ Google Cloud Storage (Audio)    │
│  - News Articles & Categories   │   │  - Daily Briefing MP3/WAV       │
│  - Video Stories & Sources      │   │  - Audio Transcripts            │
│  └──────────────────────────────┘   └────────────────┬────────────────┘
                                                       │
                           ┌───────────────────────────┴──┐
                           │   Cloud Run: tts-worker      │
                           │ (Scheduled Cloud Run Job)    │
                           └──────────────────────────────┘
                                       ▲
                                       │ POST /api/v1/articles/ingest
┌────────────────────────────────┐     │ (Authenticated via X-Api-Key)
│      Cloud Scheduler           ├─────┼──────────────────────────────┐
│  - Runs every 15-20 minutes    │     │                              │
└────────────────────────────────┘     ▼                              ▼
                           ┌──────────────────────────┐  ┌──────────────────────────┐
                           │ Cloud Run: news-scraper  │  │ Cloud Run: admin-portal  │
                           │  - Sitemap & RSS Scrape  │  │  - Source Management     │
                           │  - ML Categorizer        │  │  - Analytics & Sponsors  │
                           └──────────────────────────┘  └──────────────────────────┘
```

---

### Option 1: Google Cloud Console (Web GUI Walkthrough — Zero Local CLI)

Follow these browser-based steps in the [Google Cloud Console](https://console.cloud.google.com/) to deploy the entire stack visually.

---

#### Step 1: Create Project & Enable APIs

1. Open the [Google Cloud Console](https://console.cloud.google.com/).
2. In the top navigation bar, click the project dropdown and click **New Project**.
   - **Project Name:** `nigerian-news-grid` (or your preferred name).
   - Click **Create** and ensure it is selected in the top bar.
3. Open the **Navigation Menu** (top-left ☰) > **APIs & Services** > **Library**.
4. Search for and click **Enable** for each of the following APIs:
   - **Cloud Run Admin API** (`run.googleapis.com`)
   - **Cloud SQL Admin API** (`sqladmin.googleapis.com`)
   - **Artifact Registry API** (`artifactregistry.googleapis.com`)
   - **Cloud Build API** (`cloudbuild.googleapis.com`)
   - **Cloud Scheduler API** (`cloudscheduler.googleapis.com`)

---

#### Step 2: Build & Store Container Images in Artifact Registry

You can store images in Artifact Registry using either **Cloud Shell** (in-browser terminal) or **Continuous Deployment from GitHub**:

##### Method A: In-Browser Cloud Shell (Fastest & Simplest)
1. Click the **Activate Cloud Shell** button (`>_`) in the top-right header of the console.
2. In the bottom terminal window that opens, run:
   ```bash
   # 1. Clone repository
   git clone https://github.com/YOUR_GITHUB_USERNAME/NigerianNewGrid.git
   cd NigerianNewGrid

   # 2. Create Artifact Registry repository
   gcloud artifacts repositories create newsgrid-repo \
       --repository-format=docker \
       --location=us-central1 \
       --description="Nigerian News Grid Docker Repository"

   # 3. Build & push all 4 service container images via Cloud Build
   gcloud builds submit --tag us-central1-docker.pkg.dev/$DEVSHELL_PROJECT_ID/newsgrid-repo/news-api:latest -f src/NewsApi/Dockerfile .
   gcloud builds submit --tag us-central1-docker.pkg.dev/$DEVSHELL_PROJECT_ID/newsgrid-repo/admin-dashboard:latest -f src/AdminDashboard/Dockerfile .
   gcloud builds submit --tag us-central1-docker.pkg.dev/$DEVSHELL_PROJECT_ID/newsgrid-repo/news-scraper:latest -f src/NewsScraperService/Dockerfile .
   gcloud builds submit --tag us-central1-docker.pkg.dev/$DEVSHELL_PROJECT_ID/newsgrid-repo/tts-worker:latest -f src/TtsWorker/Dockerfile .
   ```
3. Minimize or close Cloud Shell.

##### Method B: Via Artifact Registry Web GUI
1. Navigate to ☰ > **Artifact Registry** > **Repositories**.
2. Click **+ Create Repository**:
   - **Name:** `newsgrid-repo`
   - **Format:** `Docker`
   - **Mode:** `Standard`
   - **Location type:** `Region` > `us-central1 (Iowa)`
   - Click **Create**.

---

#### Step 3: Provision Cloud SQL (Managed PostgreSQL 16)

1. Navigate to ☰ > **SQL** > click **Create Instance**.
2. Choose **PostgreSQL**.
3. Configure Instance settings:
   - **Instance ID:** `newsgrid-db`
   - **Password:** Enter a strong password for the default `postgres` user.
   - **Database version:** `PostgreSQL 16`.
   - **Cloud SQL edition:** Select **Enterprise** > Preset: **Development** (Sandbox).
   - Under **Customize your instance**:
     - **Machine configuration:** Choose **Shared core** > `db-f1-micro` (1 vCPU, 0.614 GB RAM) to minimize cost within free trial credits.
     - **Storage:** `10 GB SSD`, keep **Enable automatic storage increases** checked.
     - **Connections:** Ensure **Public IP** is checked.
4. Click **Create Instance** (provisioning takes ~3–5 minutes).
5. **Create Application Database**:
   - Once the instance status turns green, click into `newsgrid-db`.
   - In the left sub-menu, select **Databases** > click **Create Database**.
   - **Database name:** `newsgrid` > click **Create**.
6. **Create Application User**:
   - In the left sub-menu, select **Users** > click **Add User Account**.
   - **User name:** `newsuser`
   - **Password:** `newspassword123!` (or your chosen password).
   - Click **Add**.
7. **Copy Connection Name**:
   - Return to the **Overview** tab of `newsgrid-db`.
   - Locate and copy the **Instance connection name** (format: `YOUR_PROJECT_ID:us-central1:newsgrid-db`).

---

#### Step 4: Create Cloud Storage Bucket for Audio Briefings

1. Navigate to ☰ > **Cloud Storage** > **Buckets** > click **+ Create**.
2. Configure bucket:
   - **Name:** `newsgrid-audio-briefings-YOUR_PROJECT_ID` (must be globally unique).
   - **Location type:** `Region` > `us-central1`.
   - **Storage class:** `Standard`.
   - **Access control:** `Uniform` (enforce public access prevention).
3. Click **Create**.

---

#### Step 5: Deploy News API (Cloud Run Service)

1. Navigate to ☰ > **Cloud Run** > click **+ Create Service**.
2. **Service Details**:
   - Select **Deploy one revision from an existing container image**.
   - Click **Select** > expand `newsgrid-repo` > select `news-api:latest`.
   - **Service name:** `news-api`
   - **Region:** `us-central1`
   - **Authentication:** Select **Allow unauthenticated invocations**.
3. Expand **Container(s), Volumes, Networking, Security**:
   - **Container Tab**:
     - **Container port:** `8080` (or `5000`).
     - Under **Environment variables**, click **+ Add Variable** for each:
       | Name | Value |
       | :--- | :--- |
       | `ConnectionStrings__NewsDb` | `Host=/cloudsql/YOUR_PROJECT_ID:us-central1:newsgrid-db;Database=newsgrid;Username=newsuser;Password=newspassword123!` |
       | `DbProvider` | `postgres` |
       | `ApiKey` | `YourSuperSecretProductionApiKey123!` |
       | `Gemini__ApiKey` | `YourGoogleGeminiApiKey` (or reference Secret Manager `gemini-api-key`) |
       | `AiEnrichment__GeminiEnabled` | `true` |
       | `IndexNow__Key` | `3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d` |
       | `GoogleCloud__ApiKey` | `YourOptionalGoogleCloudTtsKey` |
       | `ASPNETCORE_ENVIRONMENT` | `Production` |
     - Under **Cloud SQL connections**:
       - Click **+ Add Connection**.
       - Select `newsgrid-db` from the dropdown.
   - **Volumes Tab**:
     - Click **+ Add Volume**.
     - **Volume type:** `Cloud Storage bucket`.
     - **Volume name:** `audio-vol`.
     - **Bucket:** Browse and select your `newsgrid-audio-briefings-...` bucket.
   - Under **Container Tab** > **Volume Mounts**:
     - Click **+ Mount Volume**.
     - **Volume:** `audio-vol`.
     - **Mount path:** `/app/data/audio`.
4. Click **Create**.
5. Once deployment completes, copy the generated **Service URL** displayed at the top (e.g., `https://news-api-xyz-uc.a.run.app`).

---

#### Step 6: Deploy Admin Dashboard (Cloud Run Service)

1. Navigate to ☰ > **Cloud Run** > click **+ Create Service**.
2. **Service Details**:
   - Container Image: Select `newsgrid-repo/admin-dashboard:latest`.
   - **Service name:** `admin-dashboard`
   - **Region:** `us-central1`
   - **Authentication:** Select **Allow unauthenticated invocations**.
3. Expand **Container(s), Volumes, Networking, Security**:
   - Under **Environment variables**, click **+ Add Variable** for each:
     | Name | Value |
     | :--- | :--- |
     | `ApiBaseUrl` | `https://news-api-xyz-uc.a.run.app` (from Step 5) |
     | `NewsApi__BaseUrl` | `https://news-api-xyz-uc.a.run.app` |
     | `ApiKey` | `YourSuperSecretProductionApiKey123!` |
     | `ASPNETCORE_ENVIRONMENT` | `Production` |
4. Click **Create**.
5. Copy the generated **Admin Dashboard URL** (e.g., `https://admin-dashboard-xyz-uc.a.run.app`).
6. *(Optional CORS update)*: Return to `news-api` service > click **Edit & Deploy New Revision** > add environment variable `AllowedOrigins__0` = `https://admin-dashboard-xyz-uc.a.run.app` > click **Deploy**.

---

#### Step 7: Deploy News Scraper Job & Schedule Triggers

1. Navigate to ☰ > **Cloud Run** > click the **Jobs** tab at the top.
2. Click **+ Create Job**:
   - **Job name:** `news-scraper-job`
   - **Region:** `us-central1`
   - **Container image:** Select `newsgrid-repo/news-scraper:latest`.
3. Expand **Container(s), Volumes, Networking, Security**:
   - Under **Environment variables**, add:
     | Name | Value |
     | :--- | :--- |
     | `ApiBaseUrl` | `https://news-api-xyz-uc.a.run.app` |
     | `NewsApi__BaseUrl` | `https://news-api-xyz-uc.a.run.app` |
     | `ApiKey` | `YourSuperSecretProductionApiKey123!` |
     | `SCRAPER_RUN_ONCE` | `true` |
     | `DOTNET_ENVIRONMENT` | `Production` |
4. Click **Create**.
5. **Add Scheduled Recurring Trigger**:
   - On the `news-scraper-job` details page, click the **Triggers** tab.
   - Click **+ Add Scheduler Trigger**.
   - **Trigger name:** `news-scraper-cron`
   - **Frequency:** `*/20 * * * *` (runs every 20 minutes).
   - **Timezone:** Select `Africa/Lagos` (or your local timezone).
   - Under **Configure the execution**, select your project's default Compute Engine service account.
   - Click **Create**.

---

#### Step 8: Deploy TTS Audio Worker Job & Schedule Daily Briefings

1. Navigate to ☰ > **Cloud Run** > **Jobs** tab > click **+ Create Job**:
   - **Job name:** `tts-worker-job`
   - **Region:** `us-central1`
   - **Container image:** Select `newsgrid-repo/tts-worker:latest`.
2. Expand **Container(s), Volumes, Networking, Security**:
   - **Volumes Tab**:
     - Click **+ Add Volume** > choose `Cloud Storage bucket`.
     - Name: `audio-vol` > select `newsgrid-audio-briefings-...`.
   - **Container Tab**:
     - Under **Volume Mounts**, select `audio-vol` > Mount path: `/app/data/audio`.
     - Under **Environment variables**, add:
       | Name | Value |
       | :--- | :--- |
       | `ApiBaseUrl` | `https://news-api-xyz-uc.a.run.app` |
       | `NewsApi__BaseUrl` | `https://news-api-xyz-uc.a.run.app` |
       | `ApiKey` | `YourSuperSecretProductionApiKey123!` |
       | `TTS_RUN_ONCE` | `true` |
       | `DOTNET_ENVIRONMENT` | `Production` |
3. Click **Create**.
4. **Schedule Morning & Evening Briefings**:
   - On the `tts-worker-job` page, click the **Triggers** tab > **+ Add Scheduler Trigger**.
   - **Morning Trigger**:
     - **Name:** `tts-morning-cron`
     - **Frequency:** `0 8 * * *` (8:00 AM daily)
     - **Timezone:** `Africa/Lagos`
     - Click **Create**.
   - **Evening Trigger**:
     - Click **+ Add Scheduler Trigger** again.
     - **Name:** `tts-evening-cron`
     - **Frequency:** `0 18 * * *` (6:00 PM daily)
     - **Timezone:** `Africa/Lagos`
     - Click **Create**.

---

### Option 2: Google Cloud CLI (`gcloud`) Walkthrough

If you prefer deploying via terminal commands and scripts, use the following `gcloud` automated commands:

#### Step 1: Google Cloud SDK Setup & API Activation

1. Install the [Google Cloud SDK (`gcloud`)](https://cloud.google.com/sdk/docs/install).
2. Authenticate with your Google Cloud account and configure your project:
   ```bash
   gcloud auth login
   gcloud config set project YOUR_PROJECT_ID
   gcloud config set run/region us-central1
   ```
3. Enable all required GCP service APIs:
   ```bash
   gcloud services enable \
       run.googleapis.com \
       artifactregistry.googleapis.com \
       sqladmin.googleapis.com \
       cloudbuild.googleapis.com \
       cloudscheduler.googleapis.com \
       iam.googleapis.com \
       secretmanager.googleapis.com
   ```

---

#### Step 2: Create Artifact Registry for Docker Containers

Provision a regional Docker repository:
```bash
gcloud artifacts repositories create newsgrid-repo \
    --repository-format=docker \
    --location=us-central1 \
    --description="Nigerian News Grid Docker Repository"
```

Configure Docker local authentication if building locally (optional):
```bash
gcloud auth configure-docker us-central1-docker.pkg.dev
```

---

#### Step 3: Provision Managed PostgreSQL (Cloud SQL)

1. Create a lightweight, cost-optimized PostgreSQL 16 instance:
   ```bash
   gcloud sql instances create newsgrid-db \
       --database-version=POSTGRES_16 \
       --tier=db-f1-micro \
       --region=us-central1 \
       --storage-size=10GB \
       --storage-type=SSD \
       --storage-auto-increase \
       --root-password="YourStrongRootPassword123!"
   ```
2. Create the application database and user:
   ```bash
   gcloud sql databases create newsgrid --instance=newsgrid-db
   gcloud sql users create newsuser --instance=newsgrid-db --password="newspassword123!"
   ```
3. Retrieve your Cloud SQL Instance Connection Name:
   ```bash
   gcloud sql instances describe newsgrid-db --format="value(connectionName)"
   # Output format: YOUR_PROJECT_ID:us-central1:newsgrid-db
   ```

---

#### Step 4: Build & Push Container Images via Cloud Build

Execute Cloud Build from the repository root to containerize the services without needing local Docker:

```bash
# 1. News API
gcloud builds submit --tag us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/news-api:latest -f src/NewsApi/Dockerfile .

# 2. Admin Dashboard
gcloud builds submit --tag us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/admin-dashboard:latest -f src/AdminDashboard/Dockerfile .

# 3. News Scraper Service (Job Image)
gcloud builds submit --tag us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/news-scraper:latest -f src/NewsScraperService/Dockerfile .

# 4. TTS Worker Service (Audio Synthesis Job)
gcloud builds submit --tag us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/tts-worker:latest -f src/TtsWorker/Dockerfile .
```

---

#### Step 5: Deploy Services to Cloud Run via CLI

##### 1. Deploy News API (Primary Backend)

The News API executes database migrations on startup (`DbInitializer.cs`), verifies API keys, and handles health probes at `/healthz/liveness` and `/healthz/readiness`.

> [!IMPORTANT]
> **API Key Hardening:** In `Production` and `Staging` environments, `ApiKeyMiddleware` fails closed (returns HTTP 503) if `ApiKey` is not configured. Always specify a secret key for `ApiKey`.

```bash
gcloud run deploy news-api \
    --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/news-api:latest \
    --platform=managed \
    --region=us-central1 \
    --allow-unauthenticated \
    --add-cloudsql-instances=YOUR_PROJECT_ID:us-central1:newsgrid-db \
    --set-env-vars="ConnectionStrings__NewsDb=Host=/cloudsql/YOUR_PROJECT_ID:us-central1:newsgrid-db;Database=newsgrid;Username=newsuser;Password=newspassword123!,DbProvider=postgres,ApiKey=YourSuperSecretProductionApiKey123!,ASPNETCORE_ENVIRONMENT=Production,Gemini__ApiKey=YOUR_GEMINI_KEY,AiEnrichment__GeminiEnabled=true,IndexNow__Key=3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d"
```

*Take note of the service URL emitted by the command (e.g., `https://news-api-xyz-uc.a.run.app`).*

##### 2. Deploy Admin Dashboard

Deploy the Razor Pages management portal, pointing it to the News API:

```bash
gcloud run deploy admin-dashboard \
    --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/admin-dashboard:latest \
    --platform=managed \
    --region=us-central1 \
    --allow-unauthenticated \
    --set-env-vars="ApiBaseUrl=https://news-api-xyz-uc.a.run.app,NewsApi__BaseUrl=https://news-api-xyz-uc.a.run.app,ApiKey=YourSuperSecretProductionApiKey123!,ASPNETCORE_ENVIRONMENT=Production"
```

*Update CORS on `news-api` if accessing API resources from web clients:*
```bash
gcloud run services update news-api \
    --region=us-central1 \
    --update-env-vars="AllowedOrigins__0=https://admin-dashboard-xyz-uc.a.run.app"
```

##### 3. Deploy News Scraper as Cloud Run Job (Cost-Saving On-Demand Worker)

Cloud Run Jobs run to completion and terminate, incurring **$0 idle cost**.

1. Create the Scraper Job:
   ```bash
   gcloud run jobs create news-scraper-job \
       --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/news-scraper:latest \
       --region=us-central1 \
       --set-env-vars="ApiBaseUrl=https://news-api-xyz-uc.a.run.app,NewsApi__BaseUrl=https://news-api-xyz-uc.a.run.app,ApiKey=YourSuperSecretProductionApiKey123!,SCRAPER_RUN_ONCE=true,DOTNET_ENVIRONMENT=Production"
   ```

2. Create a dedicated Service Account for Cloud Scheduler:
   ```bash
   gcloud iam service-accounts create newsgrid-scheduler-sa \
       --display-name="NewsGrid Scheduler Service Account"

   gcloud projects add-iam-policy-binding YOUR_PROJECT_ID \
       --member="serviceAccount:newsgrid-scheduler-sa@YOUR_PROJECT_ID.iam.gserviceaccount.com" \
       --role="roles/run.invoker"
   ```

3. Schedule the Scraper Job to run every 20 minutes via Cloud Scheduler:
   ```bash
   gcloud scheduler jobs create http news-scraper-cron \
       --schedule="*/20 * * * *" \
       --uri="https://us-central1-run.googleapis.com/v2/projects/YOUR_PROJECT_ID/locations/us-central1/jobs/news-scraper-job:run" \
       --http-method=POST \
       --oauth-service-account-email="newsgrid-scheduler-sa@YOUR_PROJECT_ID.iam.gserviceaccount.com"
   ```

##### 4. Audio & Text-to-Speech (TTS) Architecture

The application implements a dual-tier speech architecture:

1. **On-Device Article Narration (.NET MAUI)**:
   - When users tap the speaker icon on individual articles, narration runs 100% on the user's mobile device via `Microsoft.Maui.Media.TextToSpeech`.
   - **Cost:** **$0.00** server cost.
   - **Latency:** Instant playback with zero cloud dependencies.

2. **Server-Side Audio Briefings (`TtsWorker`)**:
   - `TtsWorker` generates broadcast-style daily briefing bulletins (morning at 8:00 AM WAT and evening at 6:00 PM WAT).
   - Utilizes a **Tri-Tier Speech Engine**:
     - **Tier 1:** Google Cloud Text-to-Speech (`en-NG-Neural2-A`) when `GCP_TTS_API_KEY` is provided.
     - **Tier 2:** Microsoft Edge Neural TTS (`en-NG-EzinneNeural`), a **100% free**, authentic Nigerian voice requiring zero credentials.
     - **Tier 3:** Local offline safety net container ensuring 0% crash rate.
   - Deploy `tts-worker` as a Cloud Run Job that executes on-demand and exits cleanly via `TTS_RUN_ONCE=true`:
     ```bash
     # 1. Create TTS Worker Job with Cloud Storage volume mount for shared audio
     gcloud run jobs create tts-worker-job \
         --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/tts-worker:latest \
         --region=us-central1 \
         --add-volume=name=audio-vol,type=cloud-storage,bucket=YOUR_AUDIO_BUCKET \
         --add-volume-mount=volume=audio-vol,mount-path=/app/data/audio \
         --set-env-vars="ApiBaseUrl=https://news-api-xyz-uc.a.run.app,NewsApi__BaseUrl=https://news-api-xyz-uc.a.run.app,ApiKey=YourSuperSecretProductionApiKey123!,TTS_RUN_ONCE=true,DOTNET_ENVIRONMENT=Production"

     # 2. Schedule Morning Briefing at 8:00 AM WAT (07:00 UTC)
     gcloud scheduler jobs create http tts-morning-cron \
         --schedule="0 7 * * *" \
         --uri="https://us-central1-run.googleapis.com/v2/projects/YOUR_PROJECT_ID/locations/us-central1/jobs/tts-worker-job:run" \
         --http-method=POST \
         --oauth-service-account-email="newsgrid-scheduler-sa@YOUR_PROJECT_ID.iam.gserviceaccount.com"

     # 3. Schedule Evening Briefing at 6:00 PM WAT (17:00 UTC)
     gcloud scheduler jobs create http tts-evening-cron \
         --schedule="0 17 * * *" \
         --uri="https://us-central1-run.googleapis.com/v2/projects/YOUR_PROJECT_ID/locations/us-central1/jobs/tts-worker-job:run" \
         --http-method=POST \
         --oauth-service-account-email="newsgrid-scheduler-sa@YOUR_PROJECT_ID.iam.gserviceaccount.com"
     ```
   - In Cloud Run, mount the same Cloud Storage bucket as a volume on `news-api` (`--add-volume=name=audio-vol,type=cloud-storage,bucket=YOUR_AUDIO_BUCKET --add-volume-mount=volume=audio-vol,mount-path=/app/data/audio`) to serve synthesized audio files with seekable HTTP 206 streaming. Alternatively, Approach B (Docker Compose) handles shared audio volumes automatically on disk.

---

## 4. Approach B: Single Compute Engine VM (Docker Compose)

If you prefer running all four services and PostgreSQL together on a single virtual machine with standard Docker Compose:

### Step 1: Provision Compute Engine VM
```bash
gcloud compute instances create newsgrid-server \
    --image-family=ubuntu-2204-lts \
    --image-project=ubuntu-os-cloud \
    --machine-type=e2-medium \
    --tags=http-server,https-server \
    --zone=us-central1-a
```

### Step 2: Configure Firewall Rules
```bash
gcloud compute firewall-rules create allow-newsgrid-ports \
    --allow=tcp:5000,tcp:5001,tcp:80,tcp:443 \
    --target-tags=http-server \
    --description="Allow Nigerian News Grid API and Admin Dashboard ports"
```

### Step 3: Deploy via Docker Compose
1. SSH into your VM instance:
   ```bash
   gcloud compute ssh newsgrid-server --zone=us-central1-a
   ```
2. Install Docker and Docker Compose plugin:
   ```bash
   sudo apt-get update && sudo apt-get install -y docker.io docker-compose-v2 git
   sudo systemctl enable --now docker
   sudo usermod -aG docker $USER
   ```
3. Clone repository and create `.env` file:
   ```bash
   git clone https://github.com/YOUR_REPO/NigerianNewGrid.git
   cd NigerianNewGrid

   # Create production environment variables
   cat << 'EOF' > .env
   API_KEY=YourSuperSecretProductionApiKey123!
   GEMINI_API_KEY=YourGoogleGeminiApiKey
   INDEXNOW_KEY=3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d
   YOUTUBE_API_KEY=YourOptionalYouTubeDataApiKey
   GCP_TTS_API_KEY=YourOptionalGoogleCloudTtsKey
   EOF
   ```
4. Start all containers:
   ```bash
   docker compose up -d --build
   ```
5. Verify services:
   ```bash
   docker compose ps
   ```
   - **News API:** `http://VM_EXTERNAL_IP:5000`
   - **Admin Dashboard:** `http://VM_EXTERNAL_IP:5001`

---

## 5. Connecting the Mobile App (.NET MAUI)

Once the backend is live, configure the .NET MAUI mobile app to communicate with the production API.

### How Dynamic API Discovery Works

The mobile app implements resilient zero-config discovery in [`NigerianNewGrid/ViewModels/MainViewModel.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ViewModels/MainViewModel.cs):
1. **Cached Preference Probe:** It first checks `Preferences.Get(AppPreferenceKeys.ApiBaseUrl, string.Empty)`. If a cached endpoint responds successfully to `/healthz/liveness`, it is selected instantly with 0ms delay.
2. **Concurrent Multi-Candidate Probe:** If no cached endpoint responds, `MainViewModel.ResolveApiBaseUrlAsync` launches parallel health probes against candidate URLs with a 5-second timeout (`Task.WhenAny`).
3. **Automatic Persistence:** The first responder is saved to device `Preferences` for subsequent app launches.

### Adding Your Live Cloud URL

Open [`NigerianNewGrid/ViewModels/MainViewModel.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/ViewModels/MainViewModel.cs) and add your deployed Cloud Run URL (or VM IP) at the top of the `candidates` array in `ResolveApiBaseUrlAsync`:

```csharp
string[] candidates =
[
    "https://news-api-xyz-uc.a.run.app", // <── YOUR LIVE PRODUCTION CLOUD RUN URL
    "http://localhost:56193",
    "http://10.0.2.2:56193",
    "http://127.0.0.1:56193",
    "http://host.docker.internal:56193",
    "http://10.0.2.2:5000",
    "http://localhost:5000",
    "http://127.0.0.1:5000",
    "http://host.docker.internal:5000",
    "http://10.0.2.2:8080",
    "http://localhost:8080",
    "http://host.docker.internal:8080"
];
```

> [!TIP]
> **Android Network Security:** Cloud Run provides automatic HTTPS (`https://...`), which is permitted by default on Android. If testing HTTP endpoints with raw IP addresses, cleartext HTTP is already enabled in [`Platforms/Android/Resources/xml/network_security_config.xml`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid/Platforms/Android/Resources/xml/network_security_config.xml).

---

## 6. Post-Deployment Verification & Smoke Tests

Run these checks to confirm end-to-end operational health:

```bash
# 1. News API Liveness Probe
curl -I https://YOUR-NEWS-API-URL/healthz/liveness
# Expected: HTTP/1.1 200 OK

# 2. News API Readiness & Database Probe (tests PostgreSQL connection & migrations)
curl -I https://YOUR-NEWS-API-URL/healthz/readiness
# Expected: HTTP/1.1 200 OK

# 3. Verify Article Feed Retrieval
curl -s https://YOUR-NEWS-API-URL/api/v1/articles/briefings | head -c 200

# 4. Trigger Scraper Job Manually (Cloud Run)
gcloud run jobs execute news-scraper-job --region=us-central1

# 5. Verify Authenticated Write Endpoint Protection
curl -X POST https://YOUR-NEWS-API-URL/api/v1/video-stories/trending/sync
# Expected: HTTP 401 Unauthorized (Protected by ApiKeyMiddleware)

curl -X POST https://YOUR-NEWS-API-URL/api/v1/video-stories/trending/sync \
     -H "X-Api-Key: YourSuperSecretProductionApiKey123!"
# Expected: HTTP 200 OK

# 6. Verify Multi-Factor SEO Health Scorecard (0–100)
curl -s https://YOUR-NEWS-API-URL/api/v1/seo/health-scorecard | jq .
# Expected: HTTP 200 OK with overallScore (~94.5), grade ("EXCELLENT"), and 4 weighted pillars (25% each)

# 7. Verify ML.NET Time-Series Ranking Anomaly Engine
curl -s https://YOUR-NEWS-API-URL/api/v1/seo/anomalies | jq .
# Expected: HTTP 200 OK with active SSA change-point detection alerts or empty array if rankings are stable

# 8. Verify ML.NET Predictive CTR Opportunities & AI Headline Suggestions
curl -s https://YOUR-NEWS-API-URL/api/v1/seo/ctr-opportunities | jq .
# Expected: HTTP 200 OK with queries where actual CTR lags ML benchmark expected CTR, plus AI headline rewrites (<=60 chars)

# 9. Trigger Autonomous Remediation Cycle (Self-Healing Action)
curl -X POST https://YOUR-NEWS-API-URL/api/v1/seo/trigger-remediation \
     -H "X-Api-Key: YourSuperSecretProductionApiKey123!" \
     -H "Content-Type: application/json" \
     -d '{"actionType":"BackfillEnrichment"}' | jq .
# Expected: HTTP 200 OK with status: "Completed" and details of enriched article / IndexNow submission

# 10. Access Admin Dashboard Search Console & Crawl Cockpit
# Open your browser to: https://YOUR-ADMIN-DASHBOARD-URL/SearchConsole
# Verify live 0–100 Health Scorecard, 4 pillar bars, ML.NET anomaly cards, and 1-Click Self-Healing trigger button.
```

---

## 7. Web Application Deployment (React Router v7 SSR) & Search Engine Onboarding

The public web tier (`src/NigerianNewsGrid.Web`) runs React Router v7 in Framework Mode with Node.js Server-Side Rendering (SSR). It serves public readers, crawlers, RSS aggregators, and search engine verification endpoints directly from the root domain (`https://newsstand.ng`).

### 7.1 Containerization & Cloud Run Deployment

Deploy `NigerianNewsGrid.Web` as a standalone autoscaling Cloud Run service:

```bash
# 1. Build & Push Web Container Image via Cloud Build
gcloud builds submit --tag us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/news-web:latest src/NigerianNewsGrid.Web

# 2. Deploy Web Service to Cloud Run
gcloud run deploy news-web \
    --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/news-web:latest \
    --platform=managed \
    --region=us-central1 \
    --allow-unauthenticated \
    --port=3000 \
    --set-env-vars="API_BASE_URL=https://news-api-xyz-uc.a.run.app,SITE_URL=https://newsstand.ng,IndexNow__Key=3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d,NODE_ENV=production"
```

### 7.2 Custom Domain Mapping (`newsstand.ng`)

1. In the Google Cloud Console, navigate to **Cloud Run** > **Custom Domains** (or **Manage Custom Domains**).
2. Click **Add Mapping**:
   - **Service to route to:** `news-web`
   - **Domain:** `newsstand.ng` (and optionally `www.newsstand.ng`)
3. Add the DNS records (A/AAAA or CNAME) provided by GCP to your domain registrar (e.g., Cloudflare, Namecheap, Google Domains).
4. Google Cloud will automatically provision and renew a managed SSL/TLS certificate.

### 7.3 Web Smoke Tests & Verification

Once DNS propagation completes, verify the server-rendered and crawler endpoints:

```bash
# 1. Verify Raw Server-Rendered HTML (must include headline & JSON-LD schema without JS execution)
curl -sL https://newsstand.ng | grep -E "(<title>|application/ld\+json)"

# 2. Verify Edge Robots Directives
curl -sL https://newsstand.ng/robots.txt
# Expected: Allow: /, Disallow: /api/, Sitemap: https://newsstand.ng/sitemap.xml

# 3. Verify XML Sitemap Index & Google News Sitemap
curl -I https://newsstand.ng/sitemap.xml
# Expected: HTTP/1.1 200 OK, Content-Type: application/xml; charset=utf-8

curl -I https://newsstand.ng/sitemap-news.xml
# Expected: HTTP/1.1 200 OK, Content-Type: application/xml; charset=utf-8

# 4. Verify RSS 2.0 Syndication Feed
curl -I https://newsstand.ng/feed.xml
# Expected: HTTP/1.1 200 OK, Content-Type: application/xml; charset=utf-8

# 5. Verify IndexNow Domain Ownership Key
curl -sL https://newsstand.ng/3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d.txt
# Expected: 3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d
```

---

### 7.4 Manual Search Engine Onboarding Steps (Post-Go-Live)

Complete these manual onboarding steps once the production domain (`newsstand.ng`) is live and accessible over the internet:

#### 1. Google Search Console Onboarding
1. Open the [Google Search Console](https://search.google.com/search-console).
2. Click **Add Property**:
   - Choose **Domain property** and enter `newsstand.ng` (requires DNS TXT record verification), or **URL prefix** (`https://newsstand.ng`).
3. Complete ownership verification using the recommended DNS TXT record or HTML tag.
4. Once verified, in the left navigation sidebar, navigate to **Index** > **Sitemaps**.
5. Under **Add a new sitemap**:
   - Enter `sitemap.xml` and click **Submit**.
   - Enter `sitemap-news.xml` (Google News 48h sitemap) and click **Submit**.
6. Verify that the submission status transitions to **Success** and discovered URLs appear within 24–48 hours.

#### 2. Bing Webmaster Tools & IndexNow Activation
1. Open [Bing Webmaster Tools](https://www.bing.com/webmasters).
2. Sign in and import your site verification directly from Google Search Console (or verify via DNS).
3. In the left navigation, open **Sitemaps** and submit `https://newsstand.ng/sitemap.xml`.
4. Navigate to **IndexNow**:
   - Bing will automatically discover the ownership verification key served at `https://newsstand.ng/3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d.txt`.
   - As new articles are ingested by `NewsScraperService`, `NewsApi`'s background dispatcher will submit URLs in real-time batches, and live submission statistics will reflect on the IndexNow dashboard.

#### 3. Google Publisher Center (Post-Compliance Registration)
1. Open the [Google Publisher Center](https://publishercenter.google.com/).
2. Click **Add Publication**:
   - **Publication Name:** `News Stand NG`
   - **Primary Website:** `https://newsstand.ng`
   - **Location:** Nigeria
   - **Primary Language:** English (`en`)
3. Under **Publication Settings**:
   - Upload high-resolution square logo (512x512 PNG) and rectangular light/dark masthead logos.
4. Under **Content Sections**:
   - Add sections powered by the web RSS feeds:
     - `Top News`: `https://newsstand.ng/feed.xml`
     - `Politics`: `https://newsstand.ng/category/politics/feed.xml`
     - `Business`: `https://newsstand.ng/category/business/feed.xml`
     - `Technology`: `https://newsstand.ng/category/technology/feed.xml`
5. Submit publication for editorial review.

