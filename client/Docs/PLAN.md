# Giphy Client – Frontend Implementation Plan

## Client Goals

Build a clean, professional React + Next.js frontend that lets users:

- Load trending GIFs with a single click.
- Search for GIFs by entering a text term.
- See loading, error, and empty states for every operation.

The UI must be responsive, production-like, and organised so that API logic is fully separated from rendering logic.

---

## Expected Directory Structure

```
client/
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
├── .env.local              # NEXT_PUBLIC_API_BASE_URL
├── package.json
├── next.config.ts
└── tsconfig.json
```

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
Receives `message: string` and renders a styled error alert.

---

## API Integration Approach

All HTTP calls live in `services/gifApi.ts`. Components never call `fetch` directly.

```
GifSearch (user interaction)
    ↓ callback
page.tsx (state owner)
    ↓ calls
gifApi.ts (fetch + error handling)
    ↓ HTTP
ASP.NET Core server → Giphy API
```

The service layer throws a typed `Error` on non-OK responses, which the page catches and surfaces via the `error` state.

---

## Environment Variables

| Variable | Purpose |
|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | Base URL of the ASP.NET Core server. Never hardcoded in components. |

Set in `.env.local`:
```
NEXT_PUBLIC_API_BASE_URL=http://localhost:5000
```

> **Note:** The `NEXT_PUBLIC_` prefix makes the variable available in browser bundles. Keep any secrets server-side only (without the prefix).

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

> **CORS:** The server must allow `http://localhost:3000` during development. Add `builder.Services.AddCors(...)` and `app.UseCors(...)` in the server's `Program.cs` if requests are blocked.

---

## State Management Approach

React built-in (`useState`) — no external library needed at this scale.

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
| `error !== null` | `<ErrorMessage message={error} />` |
| `hasLoaded && gifs.length === 0` | Empty state message inside `<GifGrid />` |
| `hasLoaded && gifs.length > 0` | Responsive `<GifGrid />` |
| Initial (nothing loaded yet) | Nothing below the search bar |

---

## Step-by-Step Implementation Plan

1. **Create `models/gifModels.ts`** — TypeScript interfaces matching server response.
2. **Create `services/gifApi.ts`** — `fetchTrendingGifs` and `searchGifs`, reading `NEXT_PUBLIC_API_BASE_URL`.
3. **Create `components/LoadingState.tsx`** — spinner, no props.
4. **Create `components/ErrorMessage.tsx`** — receives `message: string`.
5. **Create `components/GifGrid.tsx`** — receives `gifs[]`, renders grid + empty state.
6. **Create `components/GifSearch.tsx`** — trending button, search input + button; delegates via props.
7. **Create `app/globals.css`** — reset, layout, grid, button, spinner, error, empty state styles.
8. **Create `app/layout.tsx`** — root layout with metadata and CSS import.
9. **Create `app/page.tsx`** — state management, async handlers, composition of all components.
10. **Create `.env.local`** — `NEXT_PUBLIC_API_BASE_URL=http://localhost:5000`.
11. **Create `next.config.ts`** — minimal config.
12. **Create `tsconfig.json`** — strict TypeScript, `@/*` path alias.
13. **Create `package.json`** — Next.js 15, React 19, TypeScript.
14. **Verify** `npm install && npm run dev` starts cleanly on `http://localhost:3000`.

---

## Future Improvements

### Pagination & Infinite Scroll
Add `limit` / `offset` parameters to the service functions. `GifGrid` can emit an "load more" trigger. Infinite scroll via `IntersectionObserver`.

### Client-Side Caching
Wrap service calls with an in-memory map (`term → GifUrlsResponse`) so repeated searches skip the network. Or adopt **SWR** or **React Query** for cache + revalidation out of the box.

### Search History
Store recent search terms in `localStorage` and surface them as suggestions below the input.

### Skeleton Loaders
Replace `<LoadingState />` with a `<GifGridSkeleton />` that renders placeholder cards matching the grid layout.

### Favorites
Let users star GIFs. Persist starred URLs to `localStorage`. Add a "Favorites" tab.

### Dark Mode
Respect `prefers-color-scheme` via CSS custom properties. Add a toggle button stored in `localStorage`.

### Unit Tests
- Test `gifApi.ts` with `fetch` mocked via MSW or `jest.fn()`.
- Test components with React Testing Library (`@testing-library/react`).
- Test empty/loading/error state rendering.

### Better Error Handling
Distinguish network errors from server errors (4xx vs 5xx). Show actionable messages ("Check your connection" vs "Server error, try again later").

### React Query / SWR
Replace manual `loading` / `error` state with `useQuery` for automatic background refetch, stale-while-revalidate, and retry logic.
