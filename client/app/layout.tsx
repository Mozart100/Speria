import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'Speria GIFs',
  description: 'Discover and search trending GIFs powered by Giphy.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
