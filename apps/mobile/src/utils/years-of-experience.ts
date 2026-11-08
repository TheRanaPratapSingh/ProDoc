/** Whole-number years of experience (0–70). Invalid input returns null. */
export function parseYearsOfExperience(value: string): number | null {
  const trimmed = value.trim();
  if (!/^\d{1,2}$/.test(trimmed)) return null;
  const years = Number(trimmed);
  if (!Number.isInteger(years) || years < 0 || years > 70) return null;
  return years;
}

/** Patient-facing label, e.g. "5 Years Experience". */
export function formatYearsExperience(years: number | null | undefined, label = 'Years Experience'): string {
  const value = typeof years === 'number' && Number.isFinite(years) ? Math.max(0, Math.trunc(years)) : 0;
  return `${value} ${label}`;
}
