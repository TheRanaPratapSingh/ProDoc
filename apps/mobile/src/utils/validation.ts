import type { ZodTypeAny } from 'zod';

/** Flatten Zod issues into a field → message map (first error wins per field). */
export function zodFieldErrors(error: { issues: { path: (string | number)[]; message: string }[] }): Record<string, string> {
  const mapped: Record<string, string> = {};
  for (const issue of error.issues) {
    const key = issue.path.length > 0 ? String(issue.path[0]) : '_form';
    if (!mapped[key]) {
      mapped[key] = issue.message;
    }
  }
  return mapped;
}

export function firstZodMessage(
  error: { issues: { message: string }[] } | null | undefined,
  fallback = 'This field is required.',
): string {
  return error?.issues[0]?.message ?? fallback;
}

/**
 * Validate a value against a Zod schema without throwing.
 * Returns the first error message, or null when valid.
 */
export function validateField<T extends ZodTypeAny>(
  schema: T,
  value: unknown,
): string | null {
  const parsed = schema.safeParse(value);
  if (parsed.success) {
    return null;
  }
  return firstZodMessage(parsed.error);
}

/**
 * Validate an object schema. Returns field errors (empty when valid).
 */
export function validateForm<T extends ZodTypeAny>(
  schema: T,
  values: unknown,
): { ok: true; data: T['_output'] } | { ok: false; errors: Record<string, string>; message: string } {
  const parsed = schema.safeParse(values);
  if (parsed.success) {
    return { ok: true, data: parsed.data };
  }
  const errors = zodFieldErrors(parsed.error);
  return {
    ok: false,
    errors,
    message: firstZodMessage(parsed.error),
  };
}

/** True when the value is empty for required-field UX. */
export function isBlank(value: unknown): boolean {
  if (value == null) return true;
  if (typeof value === 'string') return value.trim().length === 0;
  return false;
}

/** Parse YYYY-MM-DD (or Date) into a calendar date key without throwing. */
export function parseDateOnly(value: string | Date | null | undefined): Date | null {
  if (value == null) {
    return null;
  }
  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? null : value;
  }
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(trimmed);
  if (!match) {
    const parsed = new Date(trimmed);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }
  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const date = new Date(year, month - 1, day);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
    return null;
  }
  return date;
}

function startOfLocalDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

export type DateRangeValidation =
  | { ok: true }
  | { ok: false; code: string; message: string; field?: 'startDate' | 'endDate' | 'date' };

/** Shared clinic date-range rules (mirrors backend DateValidation). */
export function validateInclusiveDateRange(
  start: string | Date | null | undefined,
  end: string | Date | null | undefined,
  maxInclusiveDays = 31,
): DateRangeValidation {
  const from = parseDateOnly(start);
  const to = parseDateOnly(end);
  if (!from) {
    return { ok: false, code: 'date_invalid', message: 'Please select a valid date.', field: 'startDate' };
  }
  if (!to) {
    return { ok: false, code: 'date_invalid', message: 'Please select a valid date.', field: 'endDate' };
  }
  if (to < from) {
    return {
      ok: false,
      code: 'date_range_invalid',
      message: 'Start date cannot be later than end date.',
      field: 'endDate',
    };
  }
  const daySpan = Math.round((to.getTime() - from.getTime()) / 86_400_000);
  if (daySpan > maxInclusiveDays) {
    return {
      ok: false,
      code: 'date_range_too_large',
      message: `Choose a range of ${maxInclusiveDays} days or fewer.`,
      field: 'endDate',
    };
  }
  return { ok: true };
}

export function validateFutureAppointmentDate(
  value: string | Date | null | undefined,
  today = startOfLocalDay(new Date()),
): DateRangeValidation {
  const date = parseDateOnly(value);
  if (!date) {
    return { ok: false, code: 'date_invalid', message: 'Please select a valid date.', field: 'date' };
  }
  if (startOfLocalDay(date) < today) {
    return {
      ok: false,
      code: 'appointment_date_past',
      message: 'Please select an available future appointment date.',
      field: 'date',
    };
  }
  return { ok: true };
}

export function validateFollowUpDate(
  value: string | Date | null | undefined,
  today = startOfLocalDay(new Date()),
): DateRangeValidation {
  const date = parseDateOnly(value);
  if (!date) {
    return { ok: false, code: 'date_invalid', message: 'Please select a valid date.', field: 'followUpDate' as 'date' };
  }
  if (startOfLocalDay(date) <= today) {
    return {
      ok: false,
      code: 'follow_up_date_invalid',
      message: 'Follow-up date must be in the future.',
      field: 'date',
    };
  }
  return { ok: true };
}
