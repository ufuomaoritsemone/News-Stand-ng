# News Stand NG — Stress & Load Testing Guide (NBomber Engine)

This document provides a comprehensive operational guide for benchmarking, stress-testing, and identifying points of failure across the **News Stand NG** distributed services using the dedicated **NBomber** testing suite.

---

## 1. Overview & Purpose

The stress testing suite is housed in [`tests/NewsApi.StressTests`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/tests/NewsApi.StressTests/Program.cs) and registered under [`NigerianNewGrid.slnx`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/NigerianNewGrid.slnx). Powered by **NBomber 6.6**, it evaluates:
- **Throughput Saturation**: Peak Requests Per Second (RPS) before response latency degrades.
- **Latency Distribution**: Detailed latency percentiles ($p_{50}, p_{75}, p_{95}, p_{99}$, and max latency).
- **Concurrency Bottlenecks**: Database connection starvation, thread pool exhaustion, and lock contention.
- **Rate-Limiter Resilience**: Validating that Kestrel sheds unauthorized overload cleanly via HTTP 429 without service crashes.

---

## 2. Potential Points of Failure

| Layer | Potential Failure Point | Root Cause & Failure Symptom |
| :--- | :--- | :--- |
| **Database Connection Pool** | PostgreSQL Connection Ceiling | Default Npgsql connection pool is 100. Under prolonged high concurrency (>500 req/sec), queries stall waiting for available connections, throwing `TimeoutException` or HTTP 500. |
| **Local SQLite Mode** | Single-Writer File Locks | If running against `news.db` in SQLite mode, concurrent `POST /api/v1/articles/ingest` while reading briefings triggers `SqliteException (0x80004005): database is locked`. |
| **Memory & Garbage Collection** | Large Object Heap (LOH) Allocations | Large article content strings serialized to JSON repeatedly during read bursts can cause Gen 2 GC pauses and high CPU spikes. |
| **Frontline Rate Limiting** | Sliding Window Saturation | Sliding window limiter is configured for 120 req/min per IP. Single-client load tests will encounter HTTP 429 responses unless bypassed or distributed. |
| **Audio Streaming I/O** | Range Header File Streaming | Multiple concurrent connections streaming `/api/v1/audio/` can exhaust Kestrel worker threads and disk I/O on the shared Docker volume. |

---

## 3. Test Scenarios

The suite defines four real-world load scenarios:

### 1. `feed_read_spike` (Curated Briefings Feed)
- **Target**: `GET /api/v1/articles/briefings?topPerCategory=5`
- **Simulates**: Morning or evening news traffic surges where hundreds of mobile and web clients open the app simultaneously.
- **Measures**: In-memory cache hit performance, database query latency on cache miss, and payload serialization speed.

### 2. `search_category_filter` (Query & Index Stress)
- **Target**: `GET /api/v1/articles?category={cat}&search={term}&page=1&pageSize=10`
- **Simulates**: Users filtering across categories (`politics`, `business`, `sports`, `tech`, `entertainment`) and searching keywords (`nigeria`, `lagos`, `president`, `economy`, `football`, `court`).
- **Measures**: Database indexing effectiveness, query execution plans, and memory pagination overhead.

### 3. `scraper_write_contention` (Ingestion Under Load)
- **Target**: `POST /api/v1/articles/ingest`
- **Simulates**: Scraper background workers aggressively ingesting news feeds while readers are querying the system.
- **Measures**: Database write locks, transaction commit latency, and duplicate title/URL hashing contention.

### 4. `audio_discovery_streaming` (Audio Briefings Discovery)
- **Target**: `GET /api/v1/audio/briefings/latest`
- **Simulates**: Client polling for audio news bulletin availability and streaming readiness.
- **Measures**: Storage volume checks and lightweight JSON metadata delivery.

---

## 4. Dual Testing Modes

### Mode A: Client Defense Mode (Default)
Enforces the standard production sliding-window rate limit (120 req/min per IP):
- **Use Case**: Verifies how the API protects itself under accidental floods or DDoS attacks.
- **Expected Outcome**: High fail count showing clean **HTTP 429 Too Many Requests**, zero server crashes (0 HTTP 500s).

### Mode B: Backend Saturation Mode (`--bypass-rate-limit`)
Injects `X-Bypass-Rate-Limit: <ApiKey>` to bypass the IP limiter:
- **Use Case**: Directly stresses PostgreSQL, EF Core, and Kestrel hardware capacity to find raw breaking limits.
- **Expected Outcome**: Hundreds of sustained RPS; allows measuring the true maximum concurrency of your database and CPU.

> [!NOTE]
> Health endpoints (`/health` and `/healthz`) are permanently exempted from rate limiting in [`ServiceCollectionExtensions.cs`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/src/NewsApi/Extensions/ServiceCollectionExtensions.cs) so Docker and Kubernetes health probes are never throttled during high load.

---

## 5. Command Reference

### Quick Start Commands

```powershell
# 1. Baseline Test (30 seconds, all 4 scenarios, port 5000)
dotnet run --project tests/NewsApi.StressTests

# 2. Target Standalone NewsApi Process (port 56193)
dotnet run --project tests/NewsApi.StressTests -- --url http://localhost:56193

# 3. Read Spike Saturation Test (300 req/sec for 30s, bypassing rate limit)
dotnet run --project tests/NewsApi.StressTests -- --scenario feed --rate 300 --duration 30 --bypass-rate-limit

# 4. Search & Filter Index Stress Test (100 req/sec for 20s)
dotnet run --project tests/NewsApi.StressTests -- --scenario search --rate 100 --duration 20 --bypass-rate-limit

# 5. Ingestion Write-Lock Stress Test (50 inserts/sec for 20s)
dotnet run --project tests/NewsApi.StressTests -- --scenario ingest --rate 50 --duration 20 --bypass-rate-limit
```

### CLI Parameters

| Flag | Default | Description |
| :--- | :--- | :--- |
| `--url <url>` | `http://localhost:5000` | Target Base URL (`http://localhost:5000` for Docker, `http://localhost:56193` for standalone). |
| `--key <key>` | `AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI` | API Key passed in `X-Api-Key` header. |
| `--scenario <name>` | `all` | Specific scenario to execute: `all`, `feed`, `search`, `ingest`, or `audio`. |
| `--rate <n>` | `150` | Maximum injected requests per second. |
| `--duration <n>` | `30` | Duration of the active load phase in seconds. |
| `--warmup <n>` | `5` | Warm-up phase duration in seconds before recording stats. |
| `--bypass-rate-limit` | `false` | Bypasses sliding-window 120 req/min rate limit using `X-Bypass-Rate-Limit`. |

---

## 6. Live Telemetry Monitoring

While running a stress test, open separate terminal windows to monitor real-time system metrics:

### 1. Docker Resource Utilization
```powershell
docker stats nigeriannewsgrid-newsapi nigeriannewsgrid-postgres
```
*Look for:* CPU pinning at 100%, memory creeping upward without recovery (leak).

### 2. .NET Runtime Diagnostics
```powershell
# Get Process ID of NewsApi:
Get-Process -Name NewsApi

# Monitor thread pool and garbage collection counters:
dotnet-counters monitor -p <NewsApi_PID> --counters System.Runtime,Microsoft.AspNetCore.Hosting
```
*Key indicators:*
- **Thread Pool Queue Length**: If $>0$, requests are blocking on thread availability.
- **Gen 2 Collections**: High frequency indicates excessive memory allocation.
- **Active Requests**: Number of requests in flight inside Kestrel.

### 3. PostgreSQL Active Connections
```powershell
docker exec -it nigeriannewsgrid-postgres psql -U newsuser -d newsgrid -c "SELECT count(*), state FROM pg_stat_activity GROUP BY state;"
```
*Look for:* Connection count approaching the pool limit (default 100).

---

## 7. Analyzing Reports

After every test execution, NBomber generates reports saved in:
```
reports/stress_tests/
```
Inside this folder, you will find:
- **`report.html`**: Interactive web dashboard featuring latency distribution charts, status code pie charts, and throughput graphs.
- **`report.md`**: Clean markdown table summary for documentation or CI pipelines.
- **`report.txt`**: Plain text CLI log.

### Metric Targets for Healthy Operation
- **$p_{95}$ Latency**: $\le 100\text{ ms}$ for cached feeds, $\le 300\text{ ms}$ for search queries.
- **$p_{99}$ Latency**: $\le 500\text{ ms}$.
- **Error Rate**: $0\%$ HTTP 500s. In bypass mode, all requests should return HTTP 200/206.
