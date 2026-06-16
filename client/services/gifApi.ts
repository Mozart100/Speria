import type { GifUrlsResponse } from '@/models/gifModels';

// Empty string → relative URL (/api/...) so Next.js rewrites handle the proxy.
// Set NEXT_PUBLIC_API_BASE_URL to a full URL to bypass rewrites (e.g. direct localhost access).
const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? '';

async function get<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`);
  if (!response.ok) {
    throw new Error(`Request failed: ${response.status} ${response.statusText}`);
  }
  return response.json() as Promise<T>;
}

/** Fetches currently trending GIFs from the server. */
export async function fetchTrendingGifs(): Promise<GifUrlsResponse> {
  return get<GifUrlsResponse>('/api/gifs/trending');
}

/** Searches GIFs matching the given term via the server. */
export async function searchGifs(term: string): Promise<GifUrlsResponse> {
  return get<GifUrlsResponse>(`/api/gifs/search?term=${encodeURIComponent(term)}`);
}
