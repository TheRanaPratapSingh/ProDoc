/** Normalize DayOfWeek from API (number or enum name) to JS getDay() 0–6. */
export function normalizeScheduleDay(value: number | string): number {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return ((value % 7) + 7) % 7;
  }
  const name = String(value).trim().toLowerCase();
  const map: Record<string, number> = {
    sunday: 0,
    monday: 1,
    tuesday: 2,
    wednesday: 3,
    thursday: 4,
    friday: 5,
    saturday: 6,
  };
  if (name in map) return map[name];
  const parsed = Number(value);
  if (Number.isFinite(parsed)) return ((parsed % 7) + 7) % 7;
  return -1;
}

export const WEEKDAY_NAMES = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const;

export function weekdayName(day: number) {
  return WEEKDAY_NAMES[((day % 7) + 7) % 7];
}

export const CLINIC_TIME_ZONE = 'Asia/Kolkata';

/** Calendar date in the clinic timezone (YYYY-MM-DD). Avoids UTC day-shift. */
export function clinicDateIso(date: Date = new Date()) {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: CLINIC_TIME_ZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(date);
  const get = (type: string) => parts.find((part) => part.type === type)?.value ?? '00';
  return `${get('year')}-${get('month')}-${get('day')}`;
}

export function todayIsoLocal() {
  return clinicDateIso(new Date());
}

/** Parse YYYY-MM-DD as a local calendar Date at noon (no UTC shift). */
export function parseCalendarDate(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, (month || 1) - 1, day || 1, 12, 0, 0, 0);
}

export function addCalendarDays(isoDate: string, days: number) {
  const date = parseCalendarDate(isoDate);
  date.setDate(date.getDate() + days);
  return clinicDateIso(date);
}

export function isValidCalendarDate(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(parseCalendarDate(value).getTime());
}

/** Monday of the clinic week containing `isoDate` (Asia/Kolkata calendar day). */
export function startOfClinicWeekMonday(isoDate: string = clinicDateIso()) {
  const date = parseCalendarDate(isoDate);
  const day = date.getDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  date.setDate(date.getDate() + mondayOffset);
  return `${date.getFullYear()}-${`${date.getMonth() + 1}`.padStart(2, '0')}-${`${date.getDate()}`.padStart(2, '0')}`;
}

/** Patient booking window: current week Mon–Sun + next week Mon–Sun (14 days). */
export function patientBookingWindow(todayIso: string = clinicDateIso()) {
  const start = startOfClinicWeekMonday(todayIso);
  const end = addCalendarDays(start, 13);
  return { start, end, currentWeekStart: start, nextWeekStart: addCalendarDays(start, 7) };
}

export function sliceClock(value: string) {
  return value.length >= 5 ? value.slice(0, 5) : value;
}

export function parseClockToMinutes(value: string) {
  const [h, m] = sliceClock(value).split(':').map(Number);
  return (h || 0) * 60 + (m || 0);
}

export function clocksOverlap(startA: string, endA: string, startB: string, endB: string) {
  const a0 = parseClockToMinutes(startA);
  const a1 = parseClockToMinutes(endA);
  const b0 = parseClockToMinutes(startB);
  const b1 = parseClockToMinutes(endB);
  return a0 < b1 && b0 < a1;
}

export function isValidClockRange(start: string, end: string) {
  return parseClockToMinutes(start) < parseClockToMinutes(end);
}
