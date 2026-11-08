/**
 * Shared TanStack Query helpers for stable screens (keep showing prior data
 * while a background refetch runs).
 */
export function keepPreviousQueryData<T>(previousData: T | undefined): T | undefined {
  return previousData;
}
