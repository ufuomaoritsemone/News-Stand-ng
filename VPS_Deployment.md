# News Stand NG — Linux VPS & Vercel Production Deployment Guide

This guide provides end-to-end deployment instructions for running the **Nigerian News Grid / News Stand NG** backend microservices on a **Linux VPS (Ubuntu 22.04 / 24.04 LTS)** using Docker Compose and Caddy (Reverse Proxy + automated Let's Encrypt SSL/TLS), alongside the frontend website ([`src/NigerianNewsGrid.Web`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NigerianNewsGrid.Web)) deployed to **Vercel** with full Server-Side Rendering (SSR).

---

## Architecture Topology

```
 ┌────────────────────────────────────────────────────────┐
 │                      Vercel Edge                       │
 │   NigerianNewsGrid.Web (React Router v7 / SSR)        │
 │   Public Domain: https://newsstand.ng                  │
 └──────────────────────────┬─────────────────────────────┘
                            │ HTTPS API Requests (SSR & Client)
                            ▼
 ┌────────────────────────────────────────────────────────┐
 │                 Linux VPS (Ubuntu Server)              │
 │   Public Ports: 80 (HTTP), 443 (HTTPS), 22 (SSH)       │
 │   Reverse Proxy: Caddy (Automatic HTTPS / SSL)         │
 │   Domains: api.newsstand.ng | admin.newsstand.ng       │
 │                                                        │
 │  ┌──────────────────────────────────────────────────┐  │
 │  │             Docker Compose Network               │  │
 │  │                                                  │  │
 │  │  • news-api          (Container 8080 -> VPS 5000)│  │
 │  │  • admin-dashboard   (Container 8080 -> VPS 5001)│  │
 │  │  • news-scraper-service (Background RSS/Sitemap) │  │
 │  │  • tts-worker        (Audio Briefing Engine)     │  │
 │  │  • postgres          (Port 5432, Volume: pgdata) │  │
 │  │  • Persistent Audio: Volume: audio-data          │  │
 │  └──────────────────────────────────────────────────┘  │
 └────────────────────────────────────────────────────────┘
```

---

## Part 1: Backend Deployment to Linux VPS

### 1.1 VPS Requirements & Initial Hardening
- **Hardware Spec**: 2 vCPU, 4GB RAM, 40GB+ SSD (Hetzner, DigitalOcean, Linode, or AWS Lightsail).
- **OS**: Ubuntu 22.04 LTS or Ubuntu 24.04 LTS.

#### Configure UFW Firewall
Secure the VPS so internal container ports are inaccessible from the public internet:
```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow ssh
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

---

### 1.2 Install Docker & Docker Compose
```bash
# Install dependencies & Docker official engine
sudo apt update && sudo apt install -y curl git ufw jq
curl -fsSL https://get.docker.com -o get-docker.sh && sudo sh get-docker.sh

# Add current user to docker group
sudo usermod -aG docker $USER
newgrp docker

# Verify installation
docker compose version
```

---

### 1.3 Deploy Repository & Setup Production Environment

1. **Clone repository to `/opt/newsgrid`**:
   ```bash
   sudo mkdir -p /opt/newsgrid
   sudo chown $USER:$USER /opt/newsgrid
   cd /opt/newsgrid
   git clone https://github.com/ufuomaoritsemone/News-Stand-ng.git .
   ```

2. **Create the Production Environment File (`/opt/newsgrid/.env`)**:
   ```bash
   cat << 'EOF' > /opt/newsgrid/.env
   # PostgreSQL Configuration
   POSTGRES_DB=newsgrid
   POSTGRES_USER=newsuser
   POSTGRES_PASSWORD=YourStrongDatabasePasswordHere123!

   # Shared Secret for Ingestion & Worker API Communication
   API_KEY=YourSuperSecretApiKey64CharsMin!

   # AI Enrichment & Gemini
   GEMINI_API_KEY=AIzaSy...YOUR_GOOGLE_AI_STUDIO_KEY
   AiEnrichment__GeminiEnabled=true

   # Search Engines, Scraper Concurrency & Telemetry
   INDEXNOW_KEY=3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d
   SCRAPER_MAX_CONCURRENCY=4
   YOUTUBE_API_KEY=
   GCP_TTS_API_KEY=

   # CORS Configuration (Vercel Frontend Domains)
   AllowedOrigins__0=https://newsstand.ng
   AllowedOrigins__1=https://www.newsstand.ng
   AllowedOrigins__2=https://your-preview-app.vercel.app
   EOF
   ```

3. **Verify docker-compose.yml port bindings**:
   Ports are bound locally to `127.0.0.1` in `docker-compose.yml` so they are shielded behind the Caddy reverse proxy:
   - `127.0.0.1:5432:5432` for `postgres`
   - `127.0.0.1:5000:8080` for `news-api`
   - `127.0.0.1:5001:8080` for `admin-dashboard`
   - `127.0.0.1:3000:3000` for `news-web` (optional self-hosted SSR container when not using Vercel)

4. **Build and Launch Containers**:
   ```bash
   docker compose up -d --build
   ```

5. **Verify Service Health**:
   ```bash
   docker compose ps
   curl -I http://127.0.0.1:5000/healthz/readiness
   ```
   Both the PostgreSQL database and News API will return healthy status.

---

### 1.4 Setup Caddy Reverse Proxy & Automatic SSL

Caddy provides high-performance reverse proxying with built-in, zero-configuration Let's Encrypt certificate issuance and renewals.

1. **Install Caddy**:
   ```bash
   sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
   curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
   curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
   sudo apt update && sudo apt install caddy -y
   ```

2. **Configure `/etc/caddy/Caddyfile`**:
   ```caddy
   # Public API Gateway
   api.newsstand.ng {
       reverse_proxy 127.0.0.1:5000 {
           header_up X-Real-IP {remote_host}
           header_up X-Forwarded-Proto {scheme}
           header_up X-Forwarded-For {remote_host}
       }

       # Enable response compression
       encode zstd gzip
   }

   # Admin Dashboard Portal
   admin.newsstand.ng {
       reverse_proxy 127.0.0.1:5001 {
           header_up X-Real-IP {remote_host}
           header_up X-Forwarded-Proto {scheme}
       }

       encode zstd gzip
   }
   ```

3. **Reload Caddy**:
   ```bash
   sudo systemctl reload caddy
   ```

---

### 1.5 Systemd Auto-Restart on VPS Reboot

Create a systemd unit so Docker Compose starts automatically after host reboot:

1. Create `/etc/systemd/system/newsgrid-docker.service`:
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

2. Enable service:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable newsgrid-docker.service
   ```

---

### 1.6 Database Backups & Maintenance
Set up an automated daily backup cron for PostgreSQL:
```bash
sudo mkdir -p /var/backups/newsgrid
```
Add to `crontab -e`:
```cron
0 2 * * * docker exec nigeriannewsgrid-postgres pg_dump -U newsuser newsgrid | gzip > /var/backups/newsgrid/newsgrid_$(date +\%F).sql.gz
```

---

## Part 2: Frontend Deployment to Vercel

The frontend website ([`src/NigerianNewsGrid.Web`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NigerianNewsGrid.Web)) is built with **React Router v7 Framework Mode (Vite & Node.js SSR)**.

### 2.1 Import Repository into Vercel
1. Log in to your [Vercel Dashboard](https://vercel.com/dashboard).
2. Click **Add New...** > **Project** and select your GitHub repository: `News-Stand-ng`.
3. In **Root Directory**, click **Edit** and set:
   ```
   src/NigerianNewsGrid.Web
   ```
4. **Framework Preset**: Vercel will detect Vite / React Router.
5. **Build Command**: `npm run build`
6. **Output Directory**: `build`

---

### 2.2 Configure Environment Variables in Vercel
In **Project Settings > Environment Variables**, configure:

| Variable Name | Production Value | Description |
| :--- | :--- | :--- |
| `API_BASE_URL` | `https://api.newsstand.ng` | Backend base URL for SSR (Node.js runtime executing loaders) |
| `NEWS_API_URL` | `https://api.newsstand.ng` | Secondary fallback for SSR fetch calls |
| `VITE_API_BASE` | `https://api.newsstand.ng/api/v1` | Public API base for client-side fetches |
| `INDEXNOW_KEY` | `3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d` | Bing/Yandex verification key served at `/{key}.txt` |

---

### 2.3 Custom Domain & DNS Mapping

1. In Vercel Project Settings > **Domains**, add:
   - `newsstand.ng`
   - `www.newsstand.ng`
2. Update your DNS registrar (Namecheap, Cloudflare, etc.):
   - `A Record`: `@` pointing to `76.76.21.21` (Vercel IP)
   - `CNAME Record`: `www` pointing to `cname.vercel-dns.com`
   - `A Record`: `api` pointing to `<YOUR_VPS_IP>`
   - `A Record`: `admin` pointing to `<YOUR_VPS_IP>`

---

## Part 3: Production Verification Checklist

- [ ] `curl -I https://api.newsstand.ng/healthz/readiness` returns `HTTP/1.1 200 OK`.
- [ ] `curl -I https://newsstand.ng` returns `HTTP/2 200` with server-rendered HTML.
- [ ] Inspect raw HTML on `https://newsstand.ng/news/<slug>`:
  - `<title>`, `<meta name="description">`, and JSON-LD schema are populated in initial HTML.
- [ ] Client browser console shows zero CORS errors when fetching `/api/v1` endpoints.
- [ ] Image proxy (`https://api.newsstand.ng/img?url=...&w=800`) serves WebP images.
- [ ] Bing IndexNow key route returns 200 at `https://newsstand.ng/3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d.txt`.
