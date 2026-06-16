import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  // Proxy /api/* from the browser through Next.js to the backend container.
  // API_BASE_URL is a server-side env var (not exposed to the browser) pointing at
  // http://server:8080 inside Docker, or http://localhost:8080 for local runs.
  async rewrites() {
    const apiUrl = process.env.API_BASE_URL || 'http://localhost:8080';
    return [
      {
        source: '/api/:path*',
        destination: `${apiUrl}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;
