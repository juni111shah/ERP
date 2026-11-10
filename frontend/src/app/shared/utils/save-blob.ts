/**
 * Hands a fetched file to the browser as a download named `fileName`, through a temporary object URL.
 * The URL is released at once, or `revokeAfterMs` later for callers that keep it alive a moment (the print page).
 */
export function saveBlob(blob: Blob, fileName: string, revokeAfterMs = 0): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  if (revokeAfterMs > 0) setTimeout(() => URL.revokeObjectURL(url), revokeAfterMs);
  else URL.revokeObjectURL(url);
}
