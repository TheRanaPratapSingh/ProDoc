/** Shared patient-portal date/time and place formatting for appointments. */

export function clinicPlaceLabel(clinicName?: string | null, branchName?: string | null): string {
  const clinic = (clinicName ?? '').trim();
  const branch = (branchName ?? '').trim();
  if (!clinic) return branch;
  if (!branch) return clinic;
  if (clinic.toLowerCase() === branch.toLowerCase()) return clinic;
  return `${clinic} · ${branch}`;
}

function resolveTimeZone(timeZoneId?: string | null): string | undefined {
  if (!timeZoneId?.trim()) return undefined;
  try {
    Intl.DateTimeFormat(undefined, { timeZone: timeZoneId });
    return timeZoneId;
  } catch {
    return undefined;
  }
}

/** e.g. "6 November 2026" */
export function formatVisitDate(iso: string, timeZoneId?: string | null): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    timeZone: resolveTimeZone(timeZoneId),
  });
}

/** e.g. "11 AM" (minutes omitted when :00) */
export function formatVisitTime(iso: string, timeZoneId?: string | null): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  const timeZone = resolveTimeZone(timeZoneId);
  const parts = new Intl.DateTimeFormat(undefined, {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
    timeZone,
  }).formatToParts(date);

  const hour = parts.find((part) => part.type === 'hour')?.value;
  const minute = parts.find((part) => part.type === 'minute')?.value;
  const dayPeriod = parts.find((part) => part.type === 'dayPeriod')?.value?.toUpperCase();

  if (!hour) {
    return date.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit', hour12: true, timeZone });
  }

  if (minute && minute !== '00') {
    return dayPeriod ? `${hour}:${minute} ${dayPeriod}` : `${hour}:${minute}`;
  }

  return dayPeriod ? `${hour} ${dayPeriod}` : hour;
}

export function formatDoctorDisplayName(name?: string | null): string {
  const trimmed = (name ?? '').trim();
  if (!trimmed) return 'Doctor';
  if (/^dr\.?\s/i.test(trimmed) || /^doctor\s/i.test(trimmed)) return trimmed;
  return `Dr. ${trimmed}`;
}

export function paymentStatusLabel(
  paymentStatus: string,
  paymentMethod: string,
  copy: { paid: string; payAtClinic: string; paymentPending: string },
): string {
  if (paymentStatus === 'Paid') {
    return copy.paid;
  }
  if (paymentMethod === 'PayAtClinic') {
    return copy.payAtClinic;
  }
  if (paymentStatus === 'Pending' || paymentStatus === 'Created' || paymentStatus === 'Unpaid') {
    return copy.paymentPending;
  }
  return paymentStatus;
}
