# Giphy Integration – Server Implementation Plan

## Project Goals

Build a clean, production-like ASP.NET Core 8 Web API that exposes two endpoints:

- `GET /api/gifs/trending` – returns trending GIF URLs from Giphy
- `GET /api/gifs/search?term={term}` – returns GIF URLs matching a search term

The implementation is SOLID, layered, and designed so caching is a transparent decorator — controllers and the Giphy service are completely unaware of each other's existence beyond the `IGifService` interface.

---

## Directory Structure

```
server/
├── Dockerfile
├── .dockerignore
├── Docs/
│   └── PLAN.md
└── GiphyServer.Api/
    ├── Cache/
    │   ├── ICacheService.cs               # Generic get/set abstraction
    │   └── RedisCacheService.cs           # StackExchange.Redis implementation
    ├── Clients/
    │   ├── IGiphyClient.cs                # External-API abstraction
    │   └── GiphyHttpClient.cs             # HttpClient implementation + Giphy logging
    ├── Configuration/
    │   └── GiphyOptions.cs                # Strongly-typed Giphy config
    ├── Controllers/
    │   └── GifsController.cs              # Thin HTTP boundary
    ├── Models/
    │   ├── GiphyResponse.cs               # Internal Giphy API DTOs
    │   ├── GifUrlResponse.cs              # Single-GIF application model
    │   └── GifUrlsResponse.cs             # Collection application model
    ├── Services/
    │   ├── IGifService.cs                 # Application-level abstraction
    │   ├── GiphyGifService.cs             # Real Giphy-backed implementation
    │   └── CachingGiphyServiceDecorator.cs # Redis caching decorator
    ├── Startup/
    │   ├── AutoMapperProfile.cs           # DTO → model mappings
    │   ├── CacheOptions.cs                # Cache TTL config binding
    │   ├── CacheServiceRegistration.cs    # Decorator wiring helper
    │   ├── LoggingStartup.cs              # UseRequestLogging() extension
    │   ├── RedisStartup.cs                # AddRedis() builder extension
    │   ├── RequestLoggingMiddleware.cs    # Per-request structured log
    │   └── SerilogExtensions.cs          # AddSerilogLogging() builder extension
    ├── Program.cs                         # Composition root
    ├── appsettings.json
    └── GiphyServer.Api.csproj
```

---

## Architecture Overview

```
HTTP Request
     │
     ▼
┌─────────────────┐
│  GifsController │  ← thin: validate input, delegate, return
└────────┬────────┘
         │ IGifService
         ▼
┌──────────────────────────────┐
│ CachingGiphyServiceDecorator │  ← check Redis; return cached or fetch+store
└────────┬─────────────────────┘
         │ IGifService (inner)
         ▼
┌──────────────────┐
│  GiphyGifService │  ← call IGiphyClient, map DTOs to application models
└────────┬─────────┘
         │ IGiphyClient
         ▼
┌──────────────────┐
│ GiphyHttpClient  │  ← HTTP, auth, deserialization, Giphy logging
└──────────────────┘
         │ HTTP
         ▼
      Giphy API
```

Controllers depend only on `IGifService`. The decorator is inserted transparently at DI registration time — no controller or service code changes required.

---

## Docker Configuration

### Dockerfile (multi-stage)

```
Build stage:  mcr.microsoft.com/dotnet/sdk:8.0
Run stage:    mcr.microsoft.com/dotnet/aspnet:8.0
Exposed port: 8080
```

### Environment Variables

| Variable | Purpose | Default (appsettings) |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Development` |
| `ASPNETCORE_URLS` | Listening URL | `http://+:8080` |
| `GIPHY_API_KEY` | Giphy API key | set in appsettings |
| `GIPHY_BASE_URL` | Giphy base URL (mapped to `/v1/`) | `https://api.giphy.com/v1/` |
| `SEQ_URL` | Seq ingest URL | `http://localhost:5341` |
| `REDIS_CONNECTION_STRING` | Redis host:port | `localhost:6379` |
| `CACHE_TTL_MINUTES` | How long to cache responses | `60` |

Program.cs maps flat env vars (`GIPHY_API_KEY`, `GIPHY_BASE_URL`, `CACHE_TTL_MINUTES`) into the nested config sections consumed by `IOptions<T>`.

---

## Seq Logging

### Setup

`SerilogExtensions.AddSerilogLogging()` is called first in Program.cs:
- Reads `SEQ_URL` env var (falls back to `Seq:ServerUrl` in appsettings)
- Wires `Console` sink + `Seq` sink
- Calls `builder.Host.UseSerilog()` — replaces ASP.NET Core default logging

### Request Logging Middleware

`RequestLoggingMiddleware` logs every incoming request:

| Field | Logged |
|---|---|
| HTTP method | ✓ |
| Path | ✓ |
| Query string | ✓ |
| Route (endpoint display name) | ✓ |
| Safe headers | ✓ (Authorization, Cookie, X-Api-Key excluded) |
| Request body (≤ 64 KB) | ✓ |
| Response status code | ✓ |
| Elapsed milliseconds | ✓ |

### Giphy Client Logging (`GiphyHttpClient`)

Before each call:
```
Calling Giphy {GiphyEndpoint} | SearchTerm: {SearchTerm} | Url: {GiphyUrl}
```
*(API key sanitized to `***` in the logged URL)*

After success:
```
Giphy {GiphyEndpoint} succeeded | StatusCode: {StatusCode} | GifCount: {GifCount}
```

On failure:
```
Giphy request failed for {GiphyEndpoint} | Error: {ErrorMessage}
Giphy {GiphyEndpoint} returned an error | StatusCode: {StatusCode}
```

---

## Redis Cache

### Packages

- `StackExchange.Redis` 2.8.24

### ICacheService

```csharp
Task<T?> GetAsync<T>(string key);
Task SetAsync<T>(string key, T value, TimeSpan ttl);
```

`RedisCacheService` serializes values as JSON via `System.Text.Json`. Redis failures are caught and logged as warnings — the app continues without cache rather than failing.

### Cache Keys

| Operation | Key |
|---|---|
| Trending | `giphy:trending:today` |
| Search | `giphy:search:{normalizedTerm}` |

**Normalization** (for search keys): trim → lowercase → collapse duplicate spaces.

### Cache TTL

Configured via `CACHE_TTL_MINUTES` env var (default: 60 minutes).

---

## Decorator Pattern

`CacheServiceRegistration.AddGifServices()` wires the decorator chain:

```csharp
// Concrete type registered so the decorator can resolve it directly
services.AddScoped<GiphyGifService>();

// IGifService → decorator wrapping GiphyGifService
services.AddScoped<IGifService>(sp => new CachingGiphyServiceDecorator(
    inner:   sp.GetRequiredService<GiphyGifService>(),
    cache:   sp.GetRequiredService<ICacheService>(),
    options: sp.GetRequiredService<IOptions<CacheOptions>>(),
    logger:  sp.GetRequiredService<ILogger<CachingGiphyServiceDecorator>>()));
```

### Cache Hit / Miss Flow

```
Request arrives
    │
    ▼
CachingGiphyServiceDecorator
    │
    ├─ GetAsync(cacheKey)
    │       │
    │   [HIT] ──→ log "Cache hit for key {CacheKey}"
    │       │     log "GIF URL {url} source = Cache" (per URL)
    │       │     return cached result
    │       │
    │   [MISS] ─→ log "Cache miss for key {CacheKey}. Calling Giphy API."
    │             call inner.GetTrendingAsync / SearchAsync
    │             log "GIF URL {url} source = Giphy API" (per URL)
    │             log "Writing {Count} GIF URLs into Redis cache ..."
    │             log "Caching URL: {url}" (per URL)
    │             SetAsync(cacheKey, result, ttl)
    │             return result
```

### Cache Logging (exact log messages)

```csharp
// Cache hit
logger.LogInformation("Cache hit for key {CacheKey}", cacheKey);
logger.LogInformation("GIF URL {GifUrl} source = Cache", gif.Url);  // per URL

// Cache miss
logger.LogInformation("Cache miss for key {CacheKey}. Calling Giphy API.", cacheKey);

// After Giphy response
logger.LogInformation("GIF URL {GifUrl} source = Giphy API", gif.Url);  // per URL

// Writing to cache
logger.LogInformation(
    "Writing {Count} GIF URLs into Redis cache using key {CacheKey}. TTL={TTL}",
    result.Gifs.Count, cacheKey, ttl);
logger.LogInformation("Caching URL: {GifUrl}", gif.Url);  // per URL
```

Seq will show clearly: which URLs came from cache, which came from Giphy, which were written to cache, the cache key, and the TTL.

---

## Responsibilities of Each Layer

| Layer | File(s) | Responsibility |
|---|---|---|
| **Controller** | `GifsController` | Validate HTTP input, call service, log request/result, return typed response |
| **Decorator** | `CachingGiphyServiceDecorator` | Check Redis cache; return hit or call inner service, populate cache |
| **Service** | `IGifService`, `GiphyGifService` | Orchestrate client calls, map DTOs to application models |
| **Client** | `IGiphyClient`, `GiphyHttpClient` | HTTP requests, Giphy auth, deserialization, Giphy logging |
| **Cache** | `ICacheService`, `RedisCacheService` | Generic Redis get/set with JSON serialization |
| **Models** | `GiphyResponse`, `GifUrlResponse`, `GifUrlsResponse` | Data contracts |
| **Configuration** | `GiphyOptions`, `CacheOptions` | Strongly-typed config sections |
| **Mapping** | `AutoMapperProfile` | `GiphyResponse → GifUrlsResponse` projections |
| **Startup** | `Program.cs` + Startup/ | Compose DI, middleware pipeline, env var mapping |

---

## Final Endpoints

| Endpoint | Description |
|---|---|
| `GET /api/gifs/trending` | Returns trending GIF URLs (cached) |
| `GET /api/gifs/search?term=cats` | Returns GIFs matching a term (cached per term) |
| `GET /swagger` | Swagger UI (Development only) |
