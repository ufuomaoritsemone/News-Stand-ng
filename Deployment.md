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
    --set-env-vars="ConnectionStrings__NewsDb=Host=/cloudsql/YOUR_PROJECT_ID:us-central1:newsgrid-db;Database=newsgrid;Username=newsuser;Password=newspassword123!,DbProvider=postgres,ApiKey=YourSuperSecretProductionApiKey123!,ASPNETCORE_ENVIRONMENT=Production"
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
