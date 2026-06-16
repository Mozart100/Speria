'use client';

import { useState } from 'react';
import GifSearch from '@/components/GifSearch';
import GifGrid from '@/components/GifGrid';
import LoadingState from '@/components/LoadingState';
import ErrorMessage from '@/components/ErrorMessage';
import { fetchTrendingGifs, searchGifs } from '@/services/gifApi';
import type { GifUrlResponse } from '@/models/gifModels';

export default function Home() {
  const [gifs, setGifs] = useState<GifUrlResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasLoaded, setHasLoaded] = useState(false);

  const handleTrending = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchTrendingGifs();
      setGifs(data.gifs);
      setHasLoaded(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load trending GIFs.');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = async (term: string) => {
    setLoading(true);
    setError(null);
    try {
      const data = await searchGifs(term);
      setGifs(data.gifs);
      setHasLoaded(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to search GIFs.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="main">
      <h1 className="title">GIF Explorer</h1>
      <p className="subtitle">Discover and search trending GIFs</p>

      <GifSearch onTrending={handleTrending} onSearch={handleSearch} isLoading={loading} />

      {loading && <LoadingState />}
      {!loading && error && <ErrorMessage message={error} />}
      {!loading && !error && hasLoaded && <GifGrid gifs={gifs} />}
    </main>
  );
}
