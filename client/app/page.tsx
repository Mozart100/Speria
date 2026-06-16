'use client';

import { useCallback, useRef, useState } from 'react';
import GifSearch from '@/components/GifSearch';
import GifGrid from '@/components/GifGrid';
import LoadingState from '@/components/LoadingState';
import ErrorMessage from '@/components/ErrorMessage';
import ThemeToggle from '@/components/ThemeToggle';
import { fetchTrendingGifs, searchGifs, ApiError } from '@/services/gifApi';
import type { GifUrlResponse } from '@/models/gifModels';

type LastAction = { type: 'trending' } | { type: 'search'; term: string };

export default function Home() {
  const [gifs, setGifs] = useState<GifUrlResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasLoaded, setHasLoaded] = useState(false);
  const [searchTerm, setSearchTerm] = useState<string | undefined>(undefined);
  const [status, setStatus] = useState<string | null>(null);

  const abortRef = useRef<AbortController | null>(null);
  const lastActionRef = useRef<LastAction | null>(null);

  const execute = useCallback(async (action: LastAction) => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    lastActionRef.current = action;

    setLoading(true);
    setError(null);
    setStatus(null);

    try {
      let data;
      if (action.type === 'trending') {
        setSearchTerm(undefined);
        data = await fetchTrendingGifs(controller.signal);
        setStatus('Showing trending GIFs');
      } else {
        setSearchTerm(action.term);
        data = await searchGifs(action.term, controller.signal);
        setStatus(`Showing results for "${action.term}"`);
      }
      setGifs(data.gifs);
      setHasLoaded(true);
    } catch (err) {
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
    <div className="app-wrapper">
      <ThemeToggle />

      <main className="main">
        <h1 className="title">Speria GIFs</h1>

        <GifSearch onTrending={handleTrending} onSearch={handleSearch} isLoading={loading} />

        {!loading && status && <p className="status-text">{status}</p>}

        {loading && <LoadingState />}
        {!loading && error && <ErrorMessage message={error} onRetry={handleRetry} />}
        {!loading && !error && hasLoaded && <GifGrid gifs={gifs} searchTerm={searchTerm} />}
      </main>
    </div>
  );
}
