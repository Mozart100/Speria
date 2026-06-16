/** URL of a single GIF as returned by the server. */
export interface GifUrlResponse {
  url: string;
}

/** Top-level response wrapping a list of GIF URLs. */
export interface GifUrlsResponse {
  gifs: GifUrlResponse[];
}
