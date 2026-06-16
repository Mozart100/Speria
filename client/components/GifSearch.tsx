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
      <input
        type="text"
        className="search-input"
        placeholder="Search GIFs..."
        value={term}
        onChange={(e) => setTerm(e.target.value)}
        onKeyDown={handleKeyDown}
        disabled={isLoading}
        aria-label="Search term"
      />
      <button
        className="btn btn-primary"
        onClick={handleSearch}
        disabled={isLoading || !term.trim()}
      >
        Search
      </button>
      <button
        className="btn btn-secondary"
        onClick={onTrending}
        disabled={isLoading}
      >
        Trending
      </button>
    </div>
  );
}
