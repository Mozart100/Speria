'use client';

import { useCallback, useRef, useState } from 'react';
import GifSearch from '@/components/GifSearch';
import GifGrid from '@/components/GifGrid';
import LoadingState from '@/components/LoadingState';
import ErrorMessage from '@/components/ErrorMessage';
import { fetchTrendingGifs, searchGifs, ApiError } from '@/services/gifApi';
import type { GifUrlResponse } from '@/models/gifModels';

type LastAction = { type: 'trending' } | { type: 'search'; term: string };

export default function Home() {
  const [gifs, setGifs] = useState<GifUrlResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasLoaded, setHasLoaded] = useState(false);
  const [searchTerm, setSearchTerm] = useState<string | undefined>(undefined);

  const abortRef = useRef<AbortController | null>(null);
  const lastActionRef = useRef<LastAction | null>(null);

  const execute = useCallback(async (action: LastAction) => {
    // Cancel any in-flight request before starting a new one.
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    lastActionRef.current = action;

    setLoading(true);
    setError(null);

    try {
      let data;
      if (action.type === 'trending') {
        setSearchTerm(undefined);
        data = await fetchTrendingGifs(controller.signal);
      } else {
        setSearchTerm(action.term);
        data = await searchGifs(action.term, controller.signal);
      }
      setGifs(data.gifs);
      setHasLoaded(true);
    } catch (err) {
      // Ignore aborted requests — a newer one is already in flight.
      if (err instanceof DOMException && err.name === 'AbortError') return;
      setError(
        err instanceof ApiError
          ? err.message
          : action.type === 'trending'
            ? 'Failed to load trending GIFs.'
            : 'Failed to search GIFs.',
      );
    } finally {
      setLoading(false);
    }
  }, []);

  const handleTrending = useCallback(() => execute({ type: 'trending' }), [execute]);
  const handleSearch   = useCallback((term: string) => execute({ type: 'search', term }), [execute]);
  const handleRetry    = useCallback(() => {
    if (lastActionRef.current) execute(lastActionRef.current);
  }, [execute]);

  return (
    <main className="main">
      <h1 className="title">GIF Explorer</h1>
      <p className="subtitle">Discover and search trending GIFs</p>

      <GifSearch onTrending={handleTrending} onSearch={handleSearch} isLoading={loading} />

      {loading && <LoadingState />}
      {!loading && error && <ErrorMessage message={error} onRetry={handleRetry} />}
      {!loading && !error && hasLoaded && <GifGrid gifs={gifs} searchTerm={searchTerm} />}
    </main>
  );
}
