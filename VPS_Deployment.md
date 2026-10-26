# News Stand NG — AWS Lightsail All-in-One Linux VPS Deployment Guide

This guide provides end-to-end instructions for deploying the complete **Nigerian News Grid / News Stand NG** ecosystem on a single **AWS Lightsail Linux VPS (Ubuntu 22.04 / 24.04 LTS)** using Docker Compose and Caddy (Reverse Proxy with automated Let's Encrypt SSL/TLS).

The entire production stack runs self-hosted on your VPS:
- **Web Frontend (`news-web`)**: React Router v7 Server-Side Rendered (SSR) Node.js 22 web application.
- **Backend API (`news-api`)**: ASP.NET Core 9 Web API & Image Proxy.
- **Admin Dashboard (`admin-dashboard`)**: Web editorial & monitoring interface.
- **Background Scraper (`news-scraper-service`)**: RSS & sitemap aggregator with Gemini NER enrichment.
- **TTS Worker (`tts-worker`)**: Audio briefing synthesis engine.
- **Database (`postgres`)**: PostgreSQL 16 Alpine database with persistent volume.

---

## Architecture Topology

```
                       Public Internet (HTTPS: 443, HTTP: 80)
                                      │
                                      ▼
 ┌────────────────────────────────────────────────────────────────────────┐
 │                   AWS Lightsail VPS (Static IPv4)                      │
 │                                                                        │
 │  AWS Lightsail Firewall: TCP 22 (SSH), TCP 80 (HTTP), TCP 443 (HTTPS)  │
 │  OS Firewall (UFW):      Default Deny Inbound, Allow 22, 80, 443       │
 │                                                                        │
 │  ┌──────────────────────────────────────────────────────────────────┐  │
 │  │             Caddy Reverse Proxy (Auto-HTTPS / Let's Encrypt)     │  │
 │  │  • newsstand.ng / www.newsstand.ng ──► 127.0.0.1:3000 (Web SSR)  │  │
 │  │  • api.newsstand.ng                ──► 127.0.0.1:5000 (NewsApi)  │  │
 │  │  • admin.newsstand.ng              ──► 127.0.0.1:5001 (Admin)    │  │
 │  └─────────────────────────────────┬────────────────────────────────┘  │
 │                                    │ Internal Loopback                 │
 │  ┌─────────────────────────────────▼────────────────────────────────┐  │
 │  │                   Docker Compose Network                         │  │
 │  │                                                                  │  │
 │  │  • news-web             (React Router v7 SSR Node 22, Port 3000) │  │
 │  │  • news-api             (ASP.NET Core 9 Web API, Port 8080)      │  │
 │  │  • admin-dashboard      (Blazor / Web Admin, Port 8080)          │  │
 │  │  • news-scraper-service (Background RSS/Sitemap & Gemini NER)    │  │
 │  │  • tts-worker           (Tri-Tier Audio Synthesis Engine)        │  │
 │  │  • postgres             (PostgreSQL 16 Alpine, Volume: pgdata)   │  │
 │  │  • Persistent Volumes   (pgdata, audio-data, ./secrets)          │  │
 │  └──────────────────────────────────────────────────────────────────┘  │
 └────────────────────────────────────────────────────────────────────────┘
```

---

## Part 1: AWS Lightsail Instance Setup & Server Hardening

### 1.1 AWS Lightsail Instance Creation
1. Go to the [AWS Lightsail Console](https://lightsail.aws.amazon.com/).
2. Click **Create instance**.
3. Select your preferred AWS Region (e.g., `us-east-1` or `eu-west-1` for low latency to Africa/Europe).
4. Platform: **Linux/Unix**.
5. Blueprint: **OS Only** > **Ubuntu 24.04 LTS** (or 22.04 LTS).
6. Choose your instance plan:
   - **Recommended**: **2 vCPU, 4 GB RAM** ($20/month) or **2 vCPU, 2 GB RAM** ($10/month).
   - *Note*: If choosing a 2 GB RAM instance, setting up swap space (Section 1.4) is **mandatory** to prevent compiler out-of-memory errors during `docker compose build`.
7. Name your instance (e.g. `newsgrid-production`) and click **Create instance**.

---

### 1.2 Attach an AWS Lightsail Static IP (Mandatory)
By default, AWS Lightsail instances use dynamic public IP addresses that rotate whenever the instance is stopped and restarted.
1. In the Lightsail console, click the **Networking** tab.
2. Click **Create static IP**.
3. Select your instance and name the static IP (e.g. `newsgrid-static-ip`).
4. Click **Create and attach**.
5. Note your new **Public IPv4 address** (e.g. `x.x.x.x`). You will point your DNS records to this IP.

---

### 1.3 Configure AWS Lightsail Cloud Firewall
AWS Lightsail has an external cloud-level firewall in addition to the instance's OS firewall:
1. In Lightsail, click on your instance name and go to the **Networking** tab.
2. Under **IPv4 Firewall**, ensure the following rules are present:
   - **SSH**: TCP port `22` (restrict to your IP if preferred, or keep open if using Lightsail browser SSH).
   - **HTTP**: TCP port `80` (required for Caddy HTTP-to-HTTPS redirect & ACME SSL challenges).
   - **HTTPS**: TCP port `443` (required for secure web & API traffic).
3. **DO NOT** open ports `5000`, `5001`, `3000`, or `5432` in the Lightsail Firewall. These services are bound to `127.0.0.1` and proxied securely through Caddy.

---

### 1.4 Connect via SSH & Configure Swap Space
Connect via SSH using your terminal (`ssh -i LightsailDefaultKey.pem ubuntu@<STATIC_IP>`) or the Lightsail in-browser SSH client.

Because building multi-stage .NET 9 and Node 22 Docker images compiles binaries simultaneously, allocate a **4GB Swap file** to ensure smooth builds without triggering the Linux OOM (Out-of-Memory) killer:

```bash
# Verify existing swap (usually 0 on fresh Lightsail)
sudo swapon --show

# Create a 4GB swapfile
sudo fallocate -l 4G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile

# Make swap permanent across reboots
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab

# Optimize swappiness for server workloads
sudo sysctl vm.swappiness=10
echo 'vm.swappiness=10' | sudo tee -a /etc/sysctl.conf
```

---

### 1.5 Configure UFW (Host Firewall)
```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw --force enable
sudo ufw status
```

---

## Part 2: Install Docker Engine & Caddy

### 2.1 Install Docker Engine & Docker Compose
```bash
# Update repositories and install utility packages
sudo apt update && sudo apt install -y curl git ufw jq debian-keyring debian-archive-keyring apt-transport-https

# Install official Docker Engine
curl -fsSL https://get.docker.com -o get-docker.sh && sudo sh get-docker.sh

# Allow the 'ubuntu' user to execute docker commands without sudo
sudo usermod -aG docker ubuntu
newgrp docker

# Verify Docker and Compose V2 installation
docker compose version
```

---

### 2.2 Install Caddy Web Server
Caddy acts as your front-facing reverse proxy with automated Let's Encrypt zero-touch SSL certificate issuance and auto-renewal:
```bash
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update && sudo apt install -y caddy
```

---

## Part 3: Deploy Repository & Configure Services

### 3.1 Clone the Codebase
```bash
sudo mkdir -p /opt/newsgrid
sudo chown -R ubuntu:ubuntu /opt/newsgrid
cd /opt/newsgrid

# Clone project repository
git clone https://github.com/ufuomaoritsemone/News-Stand-ng.git .

# Create directory for optional GCP service account key
mkdir -p /opt/newsgrid/secrets
```

---

### 3.2 Create Production Environment File (`/opt/newsgrid/.env`)
Create `/opt/newsgrid/.env` with your secure production keys:

```bash
cat << 'EOF' > /opt/newsgrid/.env
# ==============================================================================
# DATABASE (PostgreSQL)
# ==============================================================================
POSTGRES_DB=newsgrid
POSTGRES_USER=newsuser
POSTGRES_PASSWORD=CreateYourStrongPasswordHere_12345!

# ==============================================================================
# SECURITY & SHARED WORKER AUTHENTICATION
# ==============================================================================
# 64+ char secret used by NewsScraperService & TtsWorker to authenticate with NewsApi
API_KEY=GenerateSecureRandom64CharApiKey_ForServiceToServiceAuth_2026!

# ==============================================================================
# AI & SEARCH ENRICHMENT
# ==============================================================================
GEMINI_API_KEY=AIzaSy...YourGoogleAiStudioGeminiKey
GEMINI_MODEL=gemini-3.1-flash-lite
AiEnrichment__GeminiEnabled=true

# Google Cloud / Vertex AI (Optional - if using GCP service account)
GCP_PROJECT_ID=project-00ae160d-e8ea-44ec-9ff
GOOGLE_CLOUD_PROJECT=project-00ae160d-e8ea-44ec-9ff
GCP_LOCATION=us-central1
GOOGLE_GENAI_USE_VERTEXAI=true

# Bing / Yandex IndexNow Protocol
INDEXNOW_KEY=3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d

# ==============================================================================
# SCRAPER & AUDIO CONFIGURATION
# ==============================================================================
SCRAPER_MAX_CONCURRENCY=4
YOUTUBE_API_KEY=
GCP_TTS_API_KEY=

# ==============================================================================
# CORS & FRONTEND CONFIGURATION
# ==============================================================================
AllowedOrigins__0=https://newsstand.ng
AllowedOrigins__1=https://www.newsstand.ng
AllowedOrigins__2=https://admin.newsstand.ng
AllowedOrigins__3=http://localhost:3000

# Client API endpoint for browser calls
VITE_API_BASE=https://api.newsstand.ng/api/v1
EOF
```

> [!TIP]
> If you have a Google Cloud Service Account JSON key for Vertex AI / Cloud TTS, place it at `/opt/newsgrid/secrets/gcp-service-account.json`. If not using Vertex AI, the system automatically uses `GEMINI_API_KEY` for AI enrichment and Microsoft Edge Neural TTS for zero-cost audio briefing synthesis.

---

### 3.3 Build and Launch Docker Services
Launch the entire containerized fleet using Docker Compose:

```bash
cd /opt/newsgrid
docker compose up -d --build
```

Verify that all 6 containers are healthy and running:
```bash
docker compose ps
```

Expected containers:
- `nigeriannewsgrid-postgres` (PostgreSQL 16) — Up (healthy)
- `nigeriannewsgrid-newsapi` (NewsApi) — Up (healthy)
- `nigeriannewsgrid-web` (React Router v7 SSR) — Up
- `nigeriannewsgrid-admindashboard` (Admin UI) — Up
- `nigeriannewsgrid-scraperservice` (News Scraper) — Up
- `nigeriannewsgrid-ttsworker` (TTS Worker) — Up

Verify backend and frontend health endpoints locally:
```bash
curl -I http://127.0.0.1:5000/healthz/readiness
# Returns: HTTP/1.1 200 OK

curl -I http://127.0.0.1:3000
# Returns: HTTP/1.1 200 OK (Web SSR)
```

---

## Part 4: Configure Reverse Proxy (Caddy)

Configure Caddy to manage public domains, route incoming HTTP/HTTPS requests to internal containers, and automatically issue Let's Encrypt SSL certificates.

Edit `/etc/caddy/Caddyfile`:
```bash
sudo nano /etc/caddy/Caddyfile
```

Paste the following production configuration:

```caddy
# ==============================================================================
# 1. Main Website Frontend (React Router v7 SSR on Node 22)
# ==============================================================================
newsstand.ng, www.newsstand.ng {
    # Normalize www to apex (or vice versa)
    @www host www.newsstand.ng
    handle @www {
        redir https://newsstand.ng{uri} permanent
    }

    # Proxy all web requests to the news-web SSR container
    reverse_proxy 127.0.0.1:3000 {
        header_up Host {host}
        header_up X-Real-IP {remote_host}
        header_up X-Forwarded-For {remote_host}
        header_up X-Forwarded-Proto {scheme}
    }

    # Enable modern compression
    encode zstd gzip
}

# ==============================================================================
# 2. Public News API Gateway & Image Proxy
# ==============================================================================
api.newsstand.ng {
    reverse_proxy 127.0.0.1:5000 {
        header_up Host {host}
        header_up X-Real-IP {remote_host}
        header_up X-Forwarded-For {remote_host}
        header_up X-Forwarded-Proto {scheme}
    }

    encode zstd gzip
}

# ==============================================================================
# 3. Admin Dashboard Portal
# ==============================================================================
admin.newsstand.ng {
    reverse_proxy 127.0.0.1:5001 {
        header_up Host {host}
        header_up X-Real-IP {remote_host}
        header_up X-Forwarded-For {remote_host}
        header_up X-Forwarded-Proto {scheme}
    }

    encode zstd gzip
}
```

Reload Caddy to apply the changes and automatically request Let's Encrypt certificates:
```bash
sudo systemctl reload caddy
```

---

## Part 5: Domain & DNS Configuration for `newsstand.ng`

Because you have purchased the **`newsstand.ng`** domain (Nigerian `.ng` ccTLD managed by NiRA registrars like WhoGoHost, QServers, Upperlink, Namecheap, Web4Africa, etc.), configure your DNS records to direct all web, API, and admin traffic to your **AWS Lightsail Static IP**.

There are two primary ways to manage your domain:
- **Method 1 (Strongly Recommended)**: Point domain nameservers to **Cloudflare (Free)** for instant DNS propagation, free Anycast CDN edge caching, and DDoS shielding.
- **Method 2 (AWS Native)**: Use **AWS Lightsail DNS Zones** or your registrar's native DNS management.

---

### 5.1 Required DNS Records

Whichever DNS provider you choose, create these records pointing to your AWS Lightsail Static IP:

| Record Type | Host / Name | Target / Value | TTL | Description |
| :--- | :--- | :--- | :--- | :--- |
| **A** | `@` (or `newsstand.ng`) | `<YOUR_LIGHTSAIL_STATIC_IP>` | Auto / 300 | Main Web Application (SSR) |
| **A** | `www` | `<YOUR_LIGHTSAIL_STATIC_IP>` | Auto / 300 | Canonical WWW subdomain |
| **A** | `api` | `<YOUR_LIGHTSAIL_STATIC_IP>` | Auto / 300 | Public REST API & Image Proxy |
| **A** | `admin` | `<YOUR_LIGHTSAIL_STATIC_IP>` | Auto / 300 | Editorial Dashboard Portal |

> [!NOTE]
> `www.newsstand.ng` can also be configured as a **CNAME** pointing to `newsstand.ng`. Caddy is already configured in Part 4 to automatically redirect `www.newsstand.ng` to `https://newsstand.ng`.

---

### 5.2 Method 1: Cloudflare Setup (Recommended for `.ng` Domains)

`.ng` registrars often have slower DNS propagation (up to 24–48 hours) when updating individual A records. Delegating nameservers to Cloudflare provides near-instant DNS updates, global asset caching, and bot mitigation.

#### Step 1: Add Site to Cloudflare
1. Create a free account at [Cloudflare.com](https://dash.cloudflare.com/) and click **Add a site**.
2. Enter `newsstand.ng` and select the **Free** tier.
3. Cloudflare will scan for existing records.

#### Step 2: Configure Nameservers at Your `.ng` Registrar
1. Cloudflare will assign two nameservers (for example: `ada.ns.cloudflare.com` and `bob.ns.cloudflare.com`).
2. Log into the registrar where you bought `newsstand.ng` (WhoGoHost, QServers, Upperlink, etc.).
3. Navigate to **Domains** > **Manage Domain** > **Nameservers**.
4. Select **Use Custom Nameservers** and enter the two Cloudflare nameserver addresses.
5. Save changes. (Registrar nameserver propagation typically takes 15 minutes to 2 hours).

#### Step 3: Create DNS Records in Cloudflare
In your Cloudflare Dashboard under **DNS** > **Records**, add the 4 records:
- `A` record: Name `@`, IPv4 address `<YOUR_LIGHTSAIL_STATIC_IP>`, Proxy status: **Proxied (Orange Cloud)**.
- `A` record: Name `www`, IPv4 address `<YOUR_LIGHTSAIL_STATIC_IP>`, Proxy status: **Proxied (Orange Cloud)**.
- `A` record: Name `api`, IPv4 address `<YOUR_LIGHTSAIL_STATIC_IP>`, Proxy status: **Proxied (Orange Cloud)**.
- `A` record: Name `admin`, IPv4 address `<YOUR_LIGHTSAIL_STATIC_IP>`, Proxy status: **Proxied (Orange Cloud)**.

#### Step 4: Configure Cloudflare SSL/TLS Encryption Mode (CRITICAL)
> [!CAUTION]
> **Do NOT use "Flexible" SSL mode in Cloudflare.**
> Because Caddy on your VPS automatically serves HTTPS, setting Cloudflare to *Flexible* causes an infinite redirect loop (`ERR_TOO_MANY_REDIRECTS`).
1. In Cloudflare, navigate to **SSL/TLS** > **Overview**.
2. Select **Full (Strict)** or **Full**.
3. Under **SSL/TLS** > **Edge Certificates**, enable **Always Use HTTPS** and **Automatic HTTPS Rewrites**.

---

### 5.3 Method 2: AWS Lightsail DNS Zones (AWS Native)

If you prefer to manage DNS entirely inside your AWS console:

1. In the [AWS Lightsail Console](https://lightsail.aws.amazon.com/), click the **Networking** tab.
2. Click **Create DNS zone**.
3. Enter your domain: `newsstand.ng` and click **Create DNS zone**.
4. AWS will display **4 Name servers** (e.g. `ns-123.awsdns-xx.com`, `ns-456.awsdns-xx.org`, etc.).
5. Copy these 4 nameservers and paste them into your `.ng` registrar's **Custom Nameservers** section.
6. Back in Lightsail DNS Zone, click **Add record** and create:
   - A record: Subdomain `None` (or `@`) pointing to your Static IP.
   - A record: Subdomain `www` pointing to your Static IP.
   - A record: Subdomain `api` pointing to your Static IP.
   - A record: Subdomain `admin` pointing to your Static IP.

---

### 5.4 Method 3: Direct Registrar DNS (No Nameserver Change)

If your registrar's portal allows adding DNS records without changing nameservers:
1. Go to your registrar's **DNS Management** or **Zone Editor**.
2. Ensure existing default parking or placeholder records for `@` and `www` are deleted.
3. Add the four `A` records from Section 5.1 pointing to your Lightsail Static IP with a TTL of `300` seconds (5 minutes).

---

### 5.5 Verify DNS Propagation

Before restarting Caddy, verify that your domain resolves to your Lightsail Static IP:

```bash
# Verify apex domain resolution
nslookup newsstand.ng

# Verify API subdomain resolution
nslookup api.newsstand.ng

# Using dig (Linux/Mac)
dig +short newsstand.ng
dig +short api.newsstand.ng
```

Once the response returns your AWS Lightsail Static IP (or Cloudflare proxy IP if proxied), Caddy will automatically succeed in obtaining Let's Encrypt SSL certificates.

---

## Part 6: Systemd Auto-Restart on VPS Reboot

Ensure your Docker services automatically start after any server maintenance or host reboot:

1. Create `/etc/systemd/system/newsgrid-docker.service`:
   ```bash
   sudo nano /etc/systemd/system/newsgrid-docker.service
   ```

2. Add the following unit configuration:
   ```ini
   [Unit]
   Description=News Stand NG Docker Services
   Requires=docker.service
   After=docker.service

   [Service]
   Type=oneshot
   RemainAfterExit=yes
   WorkingDirectory=/opt/newsgrid
   ExecStart=/usr/bin/docker compose up -d
   ExecStop=/usr/bin/docker compose down
   TimeoutStartSec=0

   [Install]
   WantedBy=multi-user.target
   ```

3. Enable and activate the service:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable newsgrid-docker.service
   ```

---

## Part 7: Automated Database Backups

Set up an automated daily cron job to back up the PostgreSQL database to local storage:

1. Create backup directory:
   ```bash
   sudo mkdir -p /var/backups/newsgrid
   sudo chown -R ubuntu:ubuntu /var/backups/newsgrid
   ```

2. Open crontab:
   ```bash
   crontab -e
   ```

3. Add daily backup at 2:00 AM WAT (with automatic 14-day retention cleanup):
   ```cron
   0 2 * * * docker exec nigeriannewsgrid-postgres pg_dump -U newsuser newsgrid | gzip > /var/backups/newsgrid/newsgrid_$(date +\%F).sql.gz
   0 3 * * * find /var/backups/newsgrid -name "*.sql.gz" -type f -mtime +14 -delete
   ```

---

## Part 8: Production Verification Checklist

Run these commands from your local machine or terminal to verify complete functionality:

- [ ] **DNS & SSL Resolution**:
  ```bash
  curl -I https://newsstand.ng
  # Returns: HTTP/2 200 with Server-Side Rendered HTML
  ```
- [ ] **API Health Check**:
  ```bash
  curl -I https://api.newsstand.ng/healthz/readiness
  # Returns: HTTP/2 200 OK
  ```
- [ ] **SEO HTML Hydration**:
  Inspect raw HTML on `https://newsstand.ng/news/<article-slug>`:
  - `<title>`, `<meta name="description">`, and JSON-LD schema are present in the response body.
- [ ] **Image Proxy Delivery**:
  ```bash
  curl -I "https://api.newsstand.ng/img?url=https%3A%2F%2Fimages.unsplash.com%2Fphoto-1585829365295-ab7cd400c167&w=800"
  # Returns: Content-Type: image/webp
  ```
- [ ] **Bing IndexNow Verification**:
  ```bash
  curl https://newsstand.ng/3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d.txt
  # Returns the raw IndexNow key string
  ```
- [ ] **Audio Streaming (HTTP 206 Range Processing)**:
  ```bash
  curl -I -H "Range: bytes=0-1024" https://api.newsstand.ng/api/v1/audio/briefings/latest/stream
  # Returns: HTTP/2 206 Partial Content with Accept-Ranges: bytes
  ```
- [ ] **Admin Dashboard Portal**:
  Navigate to `https://admin.newsstand.ng` and verify authentication and system metrics.
