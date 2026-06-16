import type { GifUrlResponse } from '@/models/gifModels';

interface GifGridProps {
  gifs: GifUrlResponse[];
}

export default function GifGrid({ gifs }: GifGridProps) {
  if (gifs.length === 0) {
    return (
      <div className="empty-state">
        <p>No GIFs found. Try a different search term or load trending GIFs.</p>
      </div>
    );
  }

  return (
    <div className="gif-grid">
      {gifs.map((gif) => (
        <div key={gif.url} className="gif-item">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={gif.url} alt="GIF" loading="lazy" />
        </div>
      ))}
    </div>
  );
}
