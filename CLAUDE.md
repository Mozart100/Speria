# Claude Code — Project Context

## What this project is
Full-stack GIF browser: **ASP.NET Core 8** backend + **Next.js 15** frontend, orchestrated with Docker Compose.

## How to run
```bash
docker compose -f local-docker-compose.yaml up --build
```
Everything starts in the correct order via health-check dependencies. No manual steps.

## Local dev (without Docker)
```bash
# server
cd server && dotnet run --project GiphyServer.Api

# client (separate terminal)
cd client && npm install && npm run dev
```

## Build check (server)
```bash
cd server && dotnet build GiphyServer.Api/GiphyServer.Api.csproj
```

---

## Architecture decisions to preserve

### Backend
- **Decorator pattern** — `IGifService → CachingGiphyServiceDecorator → GiphyGifService`. Controllers depend only on `IGifService`. Never collapse this into the controller or service.
- **Typed exceptions** — Giphy errors are classified in `GiphyHttpClient` into `GiphyAuthenticationException`, `GiphyRateLimitException`, `GiphyUnavailableException`. The `GlobalExceptionHandler` maps them to RFC 7807 `ProblemDetails`. Do not catch and swallow these in controllers.
- **Redis failures are non-fatal** — `RedisCacheService` catches all exceptions and falls back gracefully. Never let a Redis error break a GIF request.
- **No sensitive values in logs** — API keys, Authorization headers, Cookies, tokens must never be logged. The Giphy API key is sanitised to `***` in `GiphyHttpClient.SanitizeUrl()`.
- **Polly on the HttpClient** — retry (exponential back-off 1 s → 2 s → 4 s) and circuit breaker (open after 5 failures, break for 30 s) are wired on the typed `GiphyHttpClient`. Use Polly v8 property names (`BreakDuration`, `FailureRatio`, `MinimumThroughput`) — not Polly v7 names.
- **Health checks via controller** — `HealthController` injects `HealthCheckService` and is visible in Swagger. `MapHealthChecks` is NOT used (it is invisible in Swagger).

### Frontend
- **No Redux / Zustand** — the app is small. All state lives in `page.tsx` with `useState` / `useRef`. Do not introduce a state library unless the feature set genuinely requires it.
- **No direct `fetch` in components** — all HTTP calls go through `services/gifApi.ts`. Components call callbacks passed as props.
- **AbortController** — every new search/trending request cancels the previous one. `AbortError` is silently swallowed. Preserve this pattern.
- **ApiError** carries HTTP `status` — the UI uses it to show specific messages per status code (401, 429, 502, 503). Do not replace it with a generic `Error`.
- **Next.js rewrites as proxy** — the browser calls relative `/api/*` paths; Next.js server-side rewrites forward them to `http://server:8080`. `NEXT_PUBLIC_API_BASE_URL` is empty (`""`). Do not hard-code Docker hostnames in client code.
- **Dark mode by default** — CSS variables on `:root` define the dark theme. `[data-theme="light"]` overrides them. Preference is persisted to `localStorage` by `ThemeToggle.tsx`.

---

## Key files

| File | Purpose |
|---|---|
| `local-docker-compose.yaml` | Full stack orchestration |
| `server/GiphyServer.Api/Program.cs` | Composition root — DI, Polly, health checks |
| `server/GiphyServer.Api/Clients/GiphyHttpClient.cs` | HTTP + exception classification |
| `server/GiphyServer.Api/Services/CachingGiphyServiceDecorator.cs` | Cache-aside decorator |
| `server/GiphyServer.Api/Startup/GlobalExceptionHandler.cs` | RFC 7807 error mapping |
| `client/app/page.tsx` | App shell — owns all state |
| `client/services/gifApi.ts` | All fetch calls + ApiError |
| `client/app/globals.css` | CSS variables for theming |

## Environment variables (server)

| Variable | Default | Notes |
|---|---|---|
| `GIPHY_API_KEY` | in appsettings | Never log this |
| `GIPHY_RESULT_LIMIT` | `20` | Clamped 1–50 server-side |
| `CACHE_TTL_MINUTES` | `60` | Redis TTL |
| `REDIS_CONNECTION_STRING` | `localhost:6379` | |
| `SEQ_URL` | `http://localhost:5341` | |

## Polly v8 note
`HttpCircuitBreakerStrategyOptions` uses **ratio-based** properties — not Polly v7 count-based ones:
- `BreakDuration` (not `DurationOfBreak`)
- `FailureRatio` + `MinimumThroughput` + `SamplingDuration` (not `HandledEventsAllowedBeforeBreaking`)
