# Giphy Client – Frontend Implementation Plan

## Client Goals

Build a clean, professional React + Next.js frontend that lets users:

- Load trending GIFs with a single click.
- Search for GIFs by entering a text term.
- See loading, error, and empty states for every operation.

The UI is responsive, production-like, and organised so that API logic is fully separated from rendering logic.

---

## Directory Structure

```
client/
├── Dockerfile
├── .dockerignore
├── Docs/
│   └── PLAN.md
├── app/
│   ├── layout.tsx          # Root layout, metadata, global CSS import
│   ├── page.tsx            # Application shell — state + orchestration
│   └── globals.css         # All shared styles
├── components/
│   ├── GifSearch.tsx       # Trending button + search input/button
│   ├── GifGrid.tsx         # Responsive GIF grid + empty state
│   ├── LoadingState.tsx    # Spinner + label
│   └── ErrorMessage.tsx    # Styled error alert
├── services/
│   └── gifApi.ts           # All fetch calls — never inside components
├── models/
│   └── gifModels.ts        # TypeScript interfaces for API contracts
├── .gitignore              # Excludes node_modules/, .next/, build artefacts
├── .dockerignore           # Excludes node_modules/, .next/ from Docker build context
├── next.config.ts          # Next.js config with API rewrites
├── package.json
└── tsconfig.json
```

---

## Docker Configuration

### Dockerfile

```
Base image:   node:20-alpine
Install:      npm ci
Run mode:     next dev (development mode — reads env vars at startup)
Exposed port: 3000
```

### Backend Connection in Docker

The client communicates with the backend using the Docker service name. Because this is a `'use client'` component making browser-side `fetch` calls, `http://server:8080` is not directly reachable from the browser. Instead, Next.js rewrites are used as a proxy:

```
Browser → fetch('/api/gifs/trending')
        → Next.js server (localhost:3000)
        → rewrites to http://server:8080/api/gifs/trending   [Docker internal]
        → ASP.NET Core server
```

### Environment Variables

| Variable | Value in Docker | Purpose |
|---|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | `""` (empty) | Browser uses relative `/api/...` paths through the Next.js rewrite |
| `API_BASE_URL` | `http://server:8080` | Server-side only — rewrite destination inside Docker network |

**Why not `NEXT_PUBLIC_API_BASE_URL: http://server:8080`?**
`NEXT_PUBLIC_*` values are baked into the browser bundle at build time and are used directly in `fetch()` calls. The browser cannot resolve `server:8080` (a Docker-internal hostname). Setting `NEXT_PUBLIC_API_BASE_URL` to empty and using a Next.js rewrite is the correct approach.

### next.config.ts Rewrite

```typescript
async rewrites() {
  const apiUrl = process.env.API_BASE_URL || 'http://localhost:8080';
  return [{ source: '/api/:path*', destination: `${apiUrl}/api/:path*` }];
}
```

### Container Startup Order

All inter-service dependencies use `condition: service_healthy`, so Docker Compose enforces this sequence:

```
redis   → healthy (redis-cli ping)
seq     → healthy (curl http://localhost/api/diagnostics/status)
server  → healthy (curl http://localhost:8080/health)  ← reflects Redis + Giphy checks
client  → starts
```

The client container will not start until the server passes its health check. This prevents the Next.js dev server from starting and immediately failing with connection errors on first render.

### Local Development (without Docker)

```bash
# Start the ASP.NET Core server on port 8080, then:
cd client
npm install
npm run dev
```

The `API_BASE_URL` defaults to `http://localhost:8080` in `next.config.ts` when the env var is not set, so `npm run dev` works out of the box.

---

## Caching Note

**Redis caching exists only on the server.** The client has no knowledge of caching. From the client's perspective, every API call goes to the ASP.NET Core server which transparently returns cached or fresh results. The client always calls the same endpoints regardless of cache state.

---

## Main Pages and Components

### `app/page.tsx` — Application Shell
The only stateful component. Owns:
- `gifs` — the current list of GIF URLs.
- `loading` — drives the loading indicator.
- `error` — drives the error message.
- `hasLoaded` — controls whether the grid/empty state appears.

Passes callbacks (`onTrending`, `onSearch`) down to `GifSearch` so child components stay stateless.

### `components/GifSearch.tsx`
Contains the **Load Trending GIFs** button and the search input + **Search** button.
Manages only its own local input state (`term`). All async work is delegated upward via props.

### `components/GifGrid.tsx`
Accepts `gifs: GifUrlResponse[]`. Renders a responsive CSS-columns grid.
Shows an inline empty-state message when the array is empty.

### `components/LoadingState.tsx`
A pure presentational spinner with a label. No props.

### `components/ErrorMessage.tsx`
Receives `message: string` and renders a styled error alert with ARIA role.

---

## API Integration Approach

All HTTP calls live in `services/gifApi.ts`. Components never call `fetch` directly.

```
GifSearch (user interaction)
    ↓ callback
page.tsx (state owner)
    ↓ calls (with AbortSignal)
gifApi.ts (fetch + ApiError classification)
    ↓ /api/gifs/trending  (relative — goes through Next.js rewrite)
ASP.NET Core server
    ↓ CachingGiphyServiceDecorator checks Redis
    ↓ GiphyGifService + GiphyHttpClient (on cache miss)
Giphy API
```

`gifApi.ts` exports `ApiError` (extends `Error`) which carries the HTTP `status` code. `page.tsx` reads the status to show the right message. If `fetch` throws a network error (no response at all), an `ApiError` with `status: 0` is thrown with the message "Unable to reach the server."

---

## Error Handling

### Status-specific messages

| HTTP status | Message shown |
|---|---|
| Network failure (0) | Unable to reach the server. |
| 401 | Giphy authentication failed. |
| 429 | Rate limit exceeded. Please try again later. |
| 502 | Giphy service is temporarily unavailable. |
| 503 | Service is temporarily unavailable. |
| Other | Unexpected server error. |

### Retry Button

`ErrorMessage` accepts an optional `onRetry` callback. When provided, a **Retry** button is rendered inside the error banner. Clicking it re-executes the last action (`trending` or `search`) without the user having to click the original button again.

`page.tsx` tracks the last action in a `useRef<LastAction>` and passes `handleRetry` to `ErrorMessage`.

### Request Cancellation (AbortController)

`page.tsx` holds an `AbortController` ref. Before each new request:
1. The previous controller is aborted (cancels the in-flight `fetch`).
2. A new controller is created and its signal is passed to `fetchTrendingGifs` / `searchGifs`.
3. `AbortError` is silently swallowed — it means a newer request replaced this one.

This prevents stale responses from a slow earlier search overwriting a faster newer one.

---

## API Response Models

The server returns strongly typed POCOs serialised to JSON. TypeScript interfaces mirror the exact shape:

```typescript
// models/gifModels.ts
interface GifUrlResponse {
  url: string;
}

interface GifUrlsResponse {
  gifs: GifUrlResponse[];
}
```

---

## State Management

React built-in `useState` and `useRef` only — no Redux, Zustand, or any external state library. The app is intentionally small and all state fits cleanly in a single page component. If the feature set grows (favorites, pagination, user sessions), a dedicated state library can be introduced at that point.

| State | Type | Owner |
|---|---|---|
| `gifs` | `GifUrlResponse[]` | `page.tsx` |
| `loading` | `boolean` | `page.tsx` |
| `error` | `string \| null` | `page.tsx` |
| `hasLoaded` | `boolean` | `page.tsx` |
| `term` (input value) | `string` | `GifSearch.tsx` |

---

## Loading, Error, and Empty States

| Condition | What is shown |
|---|---|
| `loading === true` | `<LoadingState />` (spinner) |
| `error !== null` | `<ErrorMessage message={error} onRetry={handleRetry} />` with Retry button |
| `hasLoaded && gifs.length === 0 && searchTerm` | `No results found for "{searchTerm}".` inside `<GifGrid />` |
| `hasLoaded && gifs.length === 0 && !searchTerm` | Generic empty-state message inside `<GifGrid />` |
| `hasLoaded && gifs.length > 0` | Responsive `<GifGrid />` |
| Initial (nothing loaded yet) | Nothing below the search bar |

---

## Final URLs

| Service | URL |
|---|---|
| Client | http://localhost:3000 |
| Server | http://localhost:8080 |
| Seq | http://localhost:5341 |
| RedisInsight | http://localhost:5540 |

Run everything:
```bash
docker compose -f local-docker-compose.yaml up --build
```

---

## Future Improvements

### Pagination & Infinite Scroll
Add `limit` / `offset` parameters to the service functions. `GifGrid` can emit a "load more" trigger.

### Search History
Store recent search terms in `localStorage` and surface them as suggestions below the input.

### Skeleton Loaders
Replace `<LoadingState />` with a `<GifGridSkeleton />` that renders placeholder cards.

### Favorites
Let users star GIFs. Persist starred URLs to `localStorage`. Add a "Favorites" tab.

### React Query / SWR
Replace manual `loading` / `error` state with `useQuery` for automatic background refetch and retry logic.

### Unit Tests
- Test `gifApi.ts` with `fetch` mocked via MSW.
- Test components with React Testing Library.
- Test empty/loading/error state rendering.
