# Speria GIFs

A full-stack GIF browser powered by the [Giphy API](https://developers.giphy.com/). Browse trending GIFs and search by keyword — with Redis caching, Polly resilience, structured logging via Seq, and a dark/light themed Next.js frontend.

> **To launch the entire stack — client, server, Redis, Seq, and RedisInsight — run a single command:**
> ```bash
> docker compose -f local-docker-compose.yaml up --build
> ```
> That's it. Everything starts automatically in the correct order.

---

## Architecture

```
Browser
  │
  ▼
Next.js Client  (port 3000)
  │  /api/* rewrite
  ▼
ASP.NET Core 8 Server  (port 8080)
  │
  ├── CachingGiphyServiceDecorator
  │     ├── Redis HIT  → return cached GIF URLs
  │     └── Redis MISS → call GiphyGifService
  │                           │
  │                      GiphyHttpClient  (Polly retry + circuit breaker)
  │                           │
  │                        Giphy API
  │
  ├── Redis  (port 6379)   — response cache
  ├── Seq    (port 5341)   — structured log ingestion
  └── RedisInsight (port 5540) — Redis UI
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Next.js 15, React, TypeScript |
| Backend | ASP.NET Core 8 Web API, C# |
| Cache | Redis 7, StackExchange.Redis |
| Resilience | Polly v8 (retry + circuit breaker) |
| Logging | Serilog → Seq |
| Mapping | AutoMapper |
| API Docs | Swagger / Swashbuckle |
| Container | Docker Compose |

---

## Quick Start

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- A [Giphy API key](https://developers.giphy.com/) (a working key is pre-configured in `appsettings.json` for convenience)

### Run everything

```bash
docker compose -f local-docker-compose.yaml up --build
```

| Service | URL |
|---|---|
| **Client** (Next.js) | http://localhost:3000 |
| **Server** (Swagger) | http://localhost:8080/swagger |
| **Seq** (log viewer) | http://localhost:5341 |
| **RedisInsight** | http://localhost:5540 |

### Stop

```bash
docker compose -f local-docker-compose.yaml down
```

---

## Project Structure

```
Speria/
├── client/                        # Next.js frontend
│   ├── app/
│   │   ├── layout.tsx             # Root layout + metadata
│   │   ├── page.tsx               # App shell — state, AbortController, retry
│   │   └── globals.css            # CSS variables, dark/light themes
│   ├── components/
│   │   ├── GifSearch.tsx          # Search input + Search/Trending buttons
│   │   ├── GifGrid.tsx            # Masonry GIF grid + empty state
│   │   ├── ErrorMessage.tsx       # Error banner + Retry button
│   │   ├── LoadingState.tsx       # Spinner
│   │   └── ThemeToggle.tsx        # Dark/light mode toggle (persisted)
│   ├── services/
│   │   └── gifApi.ts              # fetch wrapper — ApiError, AbortSignal
│   ├── models/
│   │   └── gifModels.ts           # TypeScript interfaces
│   └── Dockerfile
│
├── server/                        # ASP.NET Core 8 API
│   └── GiphyServer.Api/
│       ├── Cache/
│       │   ├── ICacheService.cs
│       │   └── RedisCacheService.cs
│       ├── Clients/
│       │   ├── IGiphyClient.cs
│       │   └── GiphyHttpClient.cs
│       ├── Configuration/
│       │   └── GiphyOptions.cs    # BaseUrl, ApiKey, ResultLimit
│       ├── Controllers/
│       │   ├── GifsController.cs  # GET /api/gifs/trending|search
│       │   ├── HealthController.cs # GET /health
│       │   └── CacheController.cs # DELETE /api/cache
│       ├── Exceptions/
│       │   └── GiphyException.cs  # Auth / RateLimit / Unavailable
│       ├── HealthChecks/
│       │   └── GiphyHealthCheck.cs
│       ├── Services/
│       │   ├── IGifService.cs
│       │   ├── GiphyGifService.cs
│       │   └── CachingGiphyServiceDecorator.cs
│       ├── Startup/
│       │   ├── GlobalExceptionHandler.cs
│       │   ├── SerilogExtensions.cs
│       │   ├── RequestLoggingMiddleware.cs
│       │   └── RedisStartup.cs
│       └── Program.cs
│
└── local-docker-compose.yaml
```

---

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/gifs/trending` | Trending GIF URLs (Redis-cached) |
| `GET` | `/api/gifs/search?term={q}` | Search GIF URLs (Redis-cached per term) |
| `DELETE` | `/api/cache` | Flush all cached GIF responses |
| `GET` | `/health` | Application + dependency health (Redis, Giphy) |
| `GET` | `/swagger` | Interactive API docs (Development only) |

---

## Configuration

All values can be overridden via environment variables without rebuilding.

| Variable | Default | Description |
|---|---|---|
| `GIPHY_API_KEY` | *(in appsettings)* | Giphy API key |
| `GIPHY_BASE_URL` | `https://api.giphy.com` | Giphy base URL |
| `GIPHY_RESULT_LIMIT` | `20` | GIFs returned per request (1–50) |
| `REDIS_CONNECTION_STRING` | `localhost:6379` | Redis host:port |
| `CACHE_TTL_MINUTES` | `60` | How long responses are cached |
| `SEQ_URL` | `http://localhost:5341` | Seq structured log ingest URL |
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment |

Edit `local-docker-compose.yaml` to change values in Docker without a rebuild.

---

## Features

### Caching (Redis)
GIF responses are cached using the decorator pattern. The `CachingGiphyServiceDecorator` wraps `GiphyGifService` transparently — controllers have no knowledge of caching.

| Cache Key | Scope |
|---|---|
| `giphy:trending:today` | Trending results |
| `giphy:search:{term}` | Per normalised search term |

Click **Reset Cache** in the UI (or call `DELETE /api/cache`) to flush all keys immediately.

### Resilience (Polly)
The Giphy `HttpClient` runs through a two-stage Polly pipeline:

- **Retry** — exponential back-off (1 s → 2 s → 4 s) on connection errors and 5xx responses
- **Circuit Breaker** — opens after 5 consecutive failures, stays open for 30 seconds

### Error Classification
Giphy errors are translated to typed exceptions and mapped to RFC 7807 `ProblemDetails`:

| Giphy status | HTTP returned | Problem type |
|---|---|---|
| 401 / 403 | 401 | `/problems/giphy-authentication` |
| 429 | 429 | `/problems/giphy-rate-limit` |
| 5xx / timeout | 502 | `/problems/giphy-unavailable` |
| Unexpected | 500 | `/problems/internal-error` |

### Structured Logging (Seq)
Every request, cache hit/miss, Giphy call, and error is logged as a structured event to Seq at http://localhost:5341. Sensitive headers (Authorization, Cookie, API keys) are never logged.

### Health Checks
`GET /health` reports the live status of Redis and the Giphy API — visible in Swagger and used by Docker's health-check probe.

### Dark / Light Mode
The frontend ships in dark mode by default. Click the ☀ / ☾ toggle (top-right) to switch — preference is persisted in `localStorage`.

### State Management
No Redux, Zustand, or any external state library is used. The app is intentionally small — React's built-in `useState` and `useRef` are sufficient and keep the bundle lean. If the feature set grows (favorites, pagination, user sessions), a dedicated state library can be introduced at that point.

---

## Development (without Docker)

### Server

```bash
cd server
dotnet run --project GiphyServer.Api
# → http://localhost:8080/swagger
```

### Client

```bash
cd client
npm install
npm run dev
# → http://localhost:3000
```

> The Next.js dev server proxies `/api/*` to `http://localhost:8080` via `next.config.ts` rewrites.
