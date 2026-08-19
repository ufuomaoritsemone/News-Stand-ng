# Nigerian News Grid — Deployment Guide (Google Cloud)

This guide details how to deploy the Nigerian News Grid backend microservices to Google Cloud Platform (GCP), cost estimates for testing/staging, and instructions for connecting the .NET MAUI mobile app.

---

## 1. Cost & Free Trial Coverage ($300 Credit)

The $300 Google Cloud Free Trial credit (valid for 90 days) will easily cover 100% of your testing and staging costs.

### Estimated Monthly Testing Cost

| Service | Configuration | Estimated Cost |
| :--- | :--- | :--- |
| **Cloud Run** (API & Admin Dashboard) | Serverless (Scales to 0 when idle) | **$0.00 – $2.00 / month** (Within the 2M requests/mo Free Tier) |
| **Cloud Run** (Scraper & Worker Tasks) | Periodic tasks / min-instances=1 | **$2.00 – $5.00 / month** |
| **Cloud SQL** (PostgreSQL) | `db-f1-micro` or `db-g1-small` | **$8.00 – $15.00 / month** |
| **Artifact Registry** (Docker Storage) | ~2 GB container images | **~$0.20 / month** |
| **Total Estimated Cost** | | **~$10 – $22 / month** |

> Over 3 months of active testing, you will only spend approximately **$30 to $65 of your $300 credit**.

---

## 2. Approach A: Cloud Run + Cloud SQL (Recommended & Serverless)

This is the modern, scalable serverless setup. Services auto-scale and SSL/TLS certificates are managed automatically.

### Step 1: Initial GCP Setup & Authentication
1. Install the [Google Cloud SDK (`gcloud`)](https://cloud.google.com/sdk/docs/install).
2. Authenticate and select your project:
   ```bash
   gcloud auth login
   gcloud config set project YOUR_PROJECT_ID
   ```
3. Enable the required APIs:
   ```bash
   gcloud services enable run.googleapis.com \
       artifactregistry.googleapis.com \
       sqladmin.googleapis.com \
       cloudbuild.googleapis.com
   ```

### Step 2: Create Artifact Registry
Create a Docker registry repository in your target region:
```bash
gcloud artifacts repositories create newsgrid-repo \
    --repository-format=docker \
    --location=us-central1 \
    --description="Nigerian News Grid Docker Repository"
```

### Step 3: Create Managed PostgreSQL (Cloud SQL)
1. Provision a lightweight PostgreSQL 16 instance:
   ```bash
   gcloud sql instances create newsgrid-db \
       --database-version=POSTGRES_16 \
       --tier=db-f1-micro \
       --region=us-central1 \
       --root-password="YourStrongRootPassword123!"
   ```
2. Create the application database and user:
   ```bash
   gcloud sql databases create newsgrid --instance=newsgrid-db
   gcloud sql users create newsuser --instance=newsgrid-db --password="newspassword123!"
   ```

### Step 4: Build & Push Container Images
Build the container images directly in Google Cloud using Cloud Build:

```bash
# 1. News API
gcloud builds submit --tag us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/news-api:latest -f src/NewsApi/Dockerfile .

# 2. Admin Dashboard
gcloud builds submit --tag us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/admin-dashboard:latest -f src/AdminDashboard/Dockerfile .

# 3. News Scraper Service
gcloud builds submit --tag us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/news-scraper:latest -f src/NewsScraperService/Dockerfile .

# 4. TTS Worker
gcloud builds submit --tag us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/tts-worker:latest -f src/TtsWorker/Dockerfile .
```

### Step 5: Deploy Services to Cloud Run

1. **Deploy News API:**
   ```bash
   gcloud run deploy news-api \
       --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/news-api:latest \
       --platform=managed \
       --region=us-central1 \
       --allow-unauthenticated \
       --add-cloudsql-instances=YOUR_PROJECT_ID:us-central1:newsgrid-db \
       --set-env-vars="ConnectionStrings__NewsDb=Host=/cloudsql/YOUR_PROJECT_ID:us-central1:newsgrid-db;Database=newsgrid;Username=newsuser;Password=newspassword123!,ASPNETCORE_ENVIRONMENT=Production"
   ```
   *Take note of the service URL generated (e.g. `https://news-api-xyz-uc.a.run.app`).*

2. **Deploy Admin Dashboard:**
   ```bash
   gcloud run deploy admin-dashboard \
       --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/admin-dashboard:latest \
       --platform=managed \
       --region=us-central1 \
       --allow-unauthenticated \
       --set-env-vars="ApiBaseUrl=https://YOUR-NEWS-API-URL,NewsApi__BaseUrl=https://YOUR-NEWS-API-URL,ASPNETCORE_ENVIRONMENT=Production"
   ```

3. **Deploy News Scraper Service:**
   ```bash
   gcloud run deploy news-scraper \
       --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/news-scraper:latest \
       --platform=managed \
       --region=us-central1 \
       --no-cpu-throttling \
       --min-instances=1 \
       --set-env-vars="ApiBaseUrl=https://YOUR-NEWS-API-URL,NewsApi__BaseUrl=https://YOUR-NEWS-API-URL,DOTNET_ENVIRONMENT=Production"
   ```

4. **Deploy TTS Worker:**
   ```bash
   gcloud run deploy tts-worker \
       --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/newsgrid-repo/tts-worker:latest \
       --platform=managed \
       --region=us-central1 \
       --no-cpu-throttling \
       --min-instances=1 \
       --set-env-vars="ApiBaseUrl=https://YOUR-NEWS-API-URL,NewsApi__BaseUrl=https://YOUR-NEWS-API-URL,DOTNET_ENVIRONMENT=Production"
   ```

---

## 3. Approach B: Single Compute Engine VM (Docker Compose)

If you want a 1:1 migration of your local `docker-compose.yml` into a single virtual machine:

### Step 1: Provision VM
```bash
gcloud compute instances create newsgrid-server \
    --image-family=ubuntu-2204-lts \
    --image-project=ubuntu-os-cloud \
    --machine-type=e2-medium \
    --tags=http-server,https-server \
    --zone=us-central1-a
```

### Step 2: Open Firewall Ports
```bash
gcloud compute firewall-rules create allow-newsgrid-ports \
    --allow=tcp:5000,tcp:5001,tcp:80,tcp:443 \
    --target-tags=http-server
```

### Step 3: Connect & Launch Containers
1. SSH into the instance:
   ```bash
   gcloud compute ssh newsgrid-server --zone=us-central1-a
   ```
2. Install Docker & Git:
   ```bash
   sudo apt update && sudo apt install -y docker.io docker-compose git
   ```
3. Clone and launch:
   ```bash
   git clone https://github.com/YOUR_REPO/NigerianNewGrid.git
   cd NigerianNewGrid
   sudo docker-compose up -d --build
   ```
4. Access endpoints at:
   - **News API:** `http://VM_EXTERNAL_IP:5000`
   - **Admin Dashboard:** `http://VM_EXTERNAL_IP:5001`

---

## 4. Connecting the Mobile App (.NET MAUI)

Once your API is deployed and live, configure the client app to discover it:

In `NigerianNewGrid/MainPage.xaml.cs`, add your live production URL to the `candidates` list in `ResolveApiBaseUrlAsync`:

```csharp
string[] candidates =
[
    "https://news-api-xyz-uc.a.run.app", // Your deployed Google Cloud API URL
    "http://localhost:56193",
    "http://10.0.2.2:56193"
];
```

The health check system will automatically ping and select the live Cloud Run or VM endpoint.
