'use client';

import { useState } from 'react';

interface GifSearchProps {
  onTrending: () => void;
  onSearch: (term: string) => void;
  isLoading: boolean;
}

export default function GifSearch({ onTrending, onSearch, isLoading }: GifSearchProps) {
  const [term, setTerm] = useState('');

  const handleSearch = () => {
    const trimmed = term.trim();
    if (trimmed) onSearch(trimmed);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') handleSearch();
  };

  return (
    <div className="gif-search">
      <button className="btn btn-primary" onClick={onTrending} disabled={isLoading}>
        Load Trending GIFs
      </button>

      <div className="search-row">
        <input
          type="text"
          className="search-input"
          placeholder="Search GIFs…"
          value={term}
          onChange={(e) => setTerm(e.target.value)}
          onKeyDown={handleKeyDown}
          disabled={isLoading}
          aria-label="Search term"
        />
        <button
          className="btn btn-secondary"
          onClick={handleSearch}
          disabled={isLoading || !term.trim()}
        >
          Search
        </button>
      </div>
    </div>
  );
}
