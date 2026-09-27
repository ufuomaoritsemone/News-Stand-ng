# Nigerian News Grid — Production Deployment Guide (Google Cloud)

This guide provides a production-grade deployment walkthrough for the Nigerian News Grid backend microservices to Google Cloud Platform (GCP), cost estimates for testing/staging with the GCP $300 Free Trial credit, and instructions for connecting the cross-platform .NET MAUI mobile application.

---

## 1. System Architecture & Microservices Overview

The backend is composed of four containerized .NET 10 microservices and a PostgreSQL database:

| Component | Directory | Role & Technology |
| :--- | :--- | :--- |
| **News API** | `src/NewsApi` | ASP.NET Core 10 Web API, EF Core, PostgreSQL/SQLite, seekable HTTP 206 audio streaming, API key & rate limiting middleware, OpenTelemetry observability. |
| **Admin Dashboard** | `src/AdminDashboard` | ASP.NET Core 10 Razor Pages administration portal for source curation, sponsored content scheduling, and video channel monitoring. |
| **News Scraper Service** | `src/NewsScraperService` | Continuous background worker or scheduled Cloud Run Job. Features Google News sitemap discovery, RSS fallbacks, JSON-LD/SmartReader extraction, ML topical classification, and editorial/opinion detection. |
| **TTS Worker Service** | `src/TtsWorker` | Tri-Tier Neural Speech Synthesis service generating broadcast audio briefings for morning (8:00 AM WAT) and evening (6:00 PM WAT) broadcasts. |
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

## 3. Approach A: Cloud Run + Cloud SQL (Recommended Serverless Architecture)

This setup is fully serverless, highly available, and autoscaling. Google Cloud automatically provisions and renews SSL/TLS certificates for custom domains and Cloud Run URLs.

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
└─────────────────────────────────┘   └────────────────┬────────────────┘
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

### Step 1: Google Cloud SDK Setup & API Activation

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

### Step 2: Create Artifact Registry for Docker Containers

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

### Step 3: Provision Managed PostgreSQL (Cloud SQL)

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

### Step 4: Build & Push Container Images via Cloud Build

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

### Step 5: Deploy Services to Cloud Run

#### 1. Deploy News API (Primary Backend)

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
    --set-env-vars="ConnectionStrings__NewsDb=Host=/cloudsql/YOUR_PROJECT_ID:us-central1:newsgrid-db;Database=newsgrid;Username=newsuser;Password=newspassword123!,DbProvider=postgres,ApiKey=YourSuperSecretProductionApiKey123!,ASPNETCORE_ENVIRONMENT=Production"
```

*Take note of the service URL emitted by the command (e.g., `https://news-api-xyz-uc.a.run.app`).*

#### 2. Deploy Admin Dashboard

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

#### 3. Deploy News Scraper as Cloud Run Job (Cost-Saving On-Demand Worker)

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

#### 4. Audio & Text-to-Speech (TTS) Architecture

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
   - In Cloud Run, mount a Cloud Storage bucket as a volume (`--add-volume=name=audio-vol,type=cloud-storage,bucket=YOUR_BUCKET --add-volume-mount=volume=audio-vol,mount-path=/app/data/audio`) to share synthesized audio files with `news-api`. Alternatively, running Approach B (Docker Compose) handles shared audio volumes out of the box.

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
```
