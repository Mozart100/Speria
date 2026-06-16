import type { GifUrlsResponse } from '@/models/gifModels';

// Empty string → relative URL (/api/...) so Next.js rewrites handle the proxy.
const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? '';

/** Typed error carrying the HTTP status code for differentiated UI messages. */
export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, { signal });
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') throw err;
    throw new ApiError('Unable to reach the server.', 0);
  }

  if (!response.ok) {
    throw new ApiError(statusMessage(response.status), response.status);
  }

  return response.json() as Promise<T>;
}

function statusMessage(status: number): string {
  switch (status) {
    case 401: return 'Giphy authentication failed.';
    case 429: return 'Rate limit exceeded. Please try again later.';
    case 502: return 'Giphy service is temporarily unavailable.';
    case 503: return 'Service is temporarily unavailable.';
    default:  return 'Unexpected server error.';
  }
}

/** Fetches currently trending GIFs from the server. */
export async function fetchTrendingGifs(signal?: AbortSignal): Promise<GifUrlsResponse> {
  return get<GifUrlsResponse>('/api/gifs/trending', signal);
}

/** Searches GIFs matching the given term via the server. */
export async function searchGifs(term: string, signal?: AbortSignal): Promise<GifUrlsResponse> {
  return get<GifUrlsResponse>(`/api/gifs/search?term=${encodeURIComponent(term)}`, signal);
}
