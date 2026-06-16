# Giphy Integration – Server Implementation Plan

## Project Goals

Build a clean, production-like ASP.NET Core 8 Web API that exposes two endpoints:

- `GET /api/gifs/trending` – returns trending GIF URLs from Giphy
- `GET /api/gifs/search?term={term}` – returns GIF URLs matching a search term

The implementation must be SOLID, layered, easily testable, and designed so caching or other cross-cutting concerns can be introduced later without touching the controller layer.

---

## Directory Structure

```
server/
├── PLAN.md
└── GiphyServer.Api/
    ├── Controllers/
    │   └── GifsController.cs          # Thin HTTP boundary
    ├── Services/
    │   ├── IGifService.cs             # Application-level abstraction
    │   └── GiphyGifService.cs         # Giphy-backed implementation
    ├── Clients/
    │   ├── IGiphyClient.cs            # External-API abstraction
    │   └── GiphyHttpClient.cs         # HttpClient implementation
    ├── Models/
    │   ├── GiphyResponse.cs           # Internal Giphy API DTOs
    │   ├── GifUrlResponse.cs          # Single-GIF application model
    │   └── GifUrlsResponse.cs         # Collection application model
    ├── Configuration/
    │   └── GiphyOptions.cs            # Strongly-typed config
    ├── Startup/
    │   └── AutoMapperProfile.cs       # Mapping definitions
    ├── Program.cs                     # Composition root
    ├── appsettings.json
    └── GiphyServer.Api.csproj
```

---

## Architecture Overview

```
HTTP Request
     │
     ▼
┌─────────────┐
│ GifsController │  ← thin: receive, delegate, return
└──────┬──────┘
       │ IGifService
       ▼
┌──────────────────┐
│ GiphyGifService  │  ← orchestrates client + mapping
└──────┬───────────┘
       │ IGiphyClient
       ▼
┌──────────────────┐
│ GiphyHttpClient  │  ← HTTP, auth, deserialization
└──────────────────┘
       │ HTTP
       ▼
   Giphy API
```

The controller depends only on `IGifService`. The service depends only on `IGiphyClient` and `IMapper`. This means:
- A caching decorator can be inserted between the controller and the service by wrapping `IGifService`.
- The Giphy provider can be swapped by swapping `IGiphyClient`.
- Everything is mockable for unit tests.

---

## Responsibilities of Each Layer

| Layer | File(s) | Responsibility |
|---|---|---|
| **Controller** | `GifsController` | Validate HTTP input, call service, return typed response |
| **Service** | `IGifService`, `GiphyGifService` | Orchestrate client calls, map DTOs to application models |
| **Client** | `IGiphyClient`, `GiphyHttpClient` | Make HTTP requests, handle transport errors, deserialize |
| **Models** | `GiphyResponse`, `GifUrlResponse`, `GifUrlsResponse` | Data contracts — internal and external |
| **Configuration** | `GiphyOptions` | Bind `Giphy` config section; surface `BaseUrl` and `ApiKey` |
| **Mapping** | `AutoMapperProfile` | Define `GiphyResponse → GifUrlsResponse` projections |
| **Startup** | `Program.cs` | Compose DI container, middleware pipeline |

---

## Step-by-Step Implementation Plan

1. **Create `.csproj`** — target `net8.0`, add AutoMapper and Swashbuckle packages.
2. **Create `GiphyOptions`** — strongly-typed config class with `BaseUrl` and `ApiKey`.
3. **Create internal DTOs** (`GiphyResponse.cs`) — mirror only the fields we need from the Giphy API response (`data[].images.original.url`).
4. **Create application response models** (`GifUrlResponse`, `GifUrlsResponse`) — what callers receive.
5. **Create `IGiphyClient`** — two methods: `GetTrendingAsync`, `SearchAsync`.
6. **Create `GiphyHttpClient`** — uses `HttpClient` (via HttpClientFactory), builds query strings, deserializes JSON, throws on non-success status.
7. **Create `IGifService`** — mirrors the two methods, returns application models.
8. **Create `GiphyGifService`** — calls `IGiphyClient`, runs AutoMapper, returns `GifUrlsResponse`.
9. **Create `AutoMapperProfile`** — maps `GiphyGifDto → GifUrlResponse` and `GiphyResponse → GifUrlsResponse`.
10. **Create `GifsController`** — two actions, each returns `ActionResult<GifUrlsResponse>`.
11. **Create `Program.cs`** — register options, AutoMapper, typed HttpClient, services, controllers, Swagger.
12. **Create `appsettings.json`** — Giphy section with `BaseUrl`; `ApiKey` left empty (supplied via `GIPHY_API_KEY` env var).

---

## Future Improvements

### Caching
Introduce a `CachingGifService` decorator that wraps `IGifService`. The controller requires no changes — only the DI registration changes (Scrutor's `Decorate<IGifService, CachingGifService>()` or manual wrapping). Use `IDistributedCache` (Redis) as the backing store. Cache keys: `gifs:trending` and `gifs:search:{term}`.

### Resilience & Retries
Add Polly policies (via `Microsoft.Extensions.Http.Resilience`) to `GiphyHttpClient`:
- Retry with exponential back-off on transient failures.
- Circuit breaker to avoid hammering a down provider.
- Timeout policy per request.

### Structured Logging
Inject `ILogger<T>` into the service and client layers. Log cache hits/misses, Giphy response times, and error details. Forward to Seq or Application Insights.

### Rate Limiting
The Giphy free tier is rate-limited. Add a token-bucket rate limiter (via `System.Threading.RateLimiting`) to stay within quota.

### Validation
Use `FluentValidation` or built-in model validation to enforce `term` length/format constraints.

### Tests
- **Unit**: mock `IGiphyClient` and `IMapper` to test `GiphyGifService` in isolation.
- **Integration**: use `WebApplicationFactory<Program>` with a fake `IGiphyClient` to test the full pipeline.
- **Contract**: verify `GiphyHttpClient` correctly deserializes real Giphy API responses.

### Pagination
Expose `limit` and `offset` query parameters on both endpoints and pass them through to Giphy.

### API Key Security
Move `ApiKey` out of configuration entirely and source it from a secrets store (Azure Key Vault, AWS Secrets Manager, or .NET User Secrets in development).
