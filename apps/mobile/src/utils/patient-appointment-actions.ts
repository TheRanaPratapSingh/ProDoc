import type { AppointmentDetails } from '@/services/api/provider-api';
import { clinicDateIso } from '@/features/providers/schedule-day';

/** Matches backend AppointmentPolicy.PatientChangeCutoff (2 hours). */
export const PATIENT_CHANGE_CUTOFF_MS = 2 * 60 * 60 * 1000;

/** Statuses that count as an active/upcoming booking for the patient. */
export const PATIENT_ACTIVE_BOOKING_STATUSES = [
  'Confirmed',
  'Requested',
  'CheckedIn',
  'Held',
] as const;

export type PatientActiveBookingStatus = (typeof PATIENT_ACTIVE_BOOKING_STATUSES)[number];

export type PatientBookingActionKind = 'viewAppointment' | 'viewBooking' | 'continueBooking' | 'book';

export type PatientVisitTab = 'upcoming' | 'past' | 'cancelled';

export function isPatientActiveBookingStatus(status: string): status is PatientActiveBookingStatus {
  return (PATIENT_ACTIVE_BOOKING_STATUSES as readonly string[]).includes(status);
}

/** Cancelled / rejected / expired / completed are never treated as active bookings. */
export function isPatientBookingClosed(status: string) {
  return ['Cancelled', 'Rejected', 'Expired', 'Completed'].includes(status);
}

/**
 * Time-based My Visits tab classification.
 * Uses the appointment instant (UTC) vs now — timezone-safe for Asia/Kolkata storage.
 * Does not mutate appointment status when the start time has passed.
 */
export function classifyPatientVisitTab(
  visit: Pick<AppointmentDetails, 'status' | 'startsAtUtc'>,
  nowMs: number = Date.now(),
): PatientVisitTab {
  if (['Cancelled', 'Rejected', 'Expired'].includes(visit.status)) {
    return 'cancelled';
  }
  if (visit.status === 'Completed') {
    return 'past';
  }
  const startMs = new Date(visit.startsAtUtc).getTime();
  if (Number.isNaN(startMs) || startMs <= nowMs) {
    return 'past';
  }
  if (isPatientActiveBookingStatus(visit.status)) {
    return 'upcoming';
  }
  return 'past';
}

export function patientBookingActionKind(status: string): PatientBookingActionKind {
  if (status === 'Held') return 'continueBooking';
  if (status === 'Requested') return 'viewBooking';
  if (status === 'Confirmed' || status === 'CheckedIn') return 'viewAppointment';
  return 'book';
}

/** Next upcoming active appointment for the patient (optionally for one doctor). */
export function findNextPatientAppointment(
  appointments: AppointmentDetails[] | undefined | null,
  options?: { doctorId?: string; clinicId?: string; nowMs?: number },
): AppointmentDetails | null {
  const now = options?.nowMs ?? Date.now();
  return (
    (appointments ?? [])
      .filter((item) => !options?.doctorId || item.doctorId === options.doctorId)
      .filter((item) => !options?.clinicId || item.clinicId === options.clinicId)
      .filter((item) => classifyPatientVisitTab(item, now) === 'upcoming')
      .sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime())[0] ?? null
  );
}

export function patientHasActiveBookingWithDoctor(
  appointments: AppointmentDetails[] | undefined | null,
  doctorId: string,
): AppointmentDetails | null {
  return findNextPatientAppointment(appointments, { doctorId });
}

/** Clinic-local calendar date (YYYY-MM-DD) for an appointment start instant. */
export function appointmentClinicDateIso(
  startsAtUtc: string,
  timeZoneId: string = 'Asia/Kolkata',
): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: timeZoneId || 'Asia/Kolkata',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(new Date(startsAtUtc));
  const get = (type: string) => parts.find((part) => part.type === type)?.value ?? '00';
  return `${get('year')}-${get('month')}-${get('day')}`;
}

/**
 * Active booking for the same patient + doctor on one clinic-local calendar date.
 * Different dates with the same doctor are allowed.
 */
export function patientHasActiveBookingWithDoctorOnDate(
  appointments: AppointmentDetails[] | undefined | null,
  doctorId: string,
  clinicLocalDate: string,
  nowMs: number = Date.now(),
): AppointmentDetails | null {
  return (
    (appointments ?? [])
      .filter((item) => item.doctorId === doctorId)
      .filter((item) => isPatientActiveBookingStatus(item.status))
      .filter((item) => classifyPatientVisitTab(item, nowMs) === 'upcoming')
      .filter((item) => appointmentClinicDateIso(item.startsAtUtc, item.timeZoneId) === clinicLocalDate)
      .sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime())[0] ?? null
  );
}

/** Pre-start cancel/reschedule under the 2-hour clinic policy. */
export function canPatientCancelVisit(
  visit: Pick<AppointmentDetails, 'status' | 'startsAtUtc'>,
  nowMs: number = Date.now(),
): boolean {
  if (visit.status !== 'Confirmed' && visit.status !== 'Requested') {
    return false;
  }
  const startMs = new Date(visit.startsAtUtc).getTime();
  if (Number.isNaN(startMs) || startMs <= nowMs) return false;
  return startMs > nowMs + PATIENT_CHANGE_CUTOFF_MS;
}

/**
 * Same-day missed-appointment reschedule: after start, still Confirmed, same clinic day,
 * under backend reschedule limit, before end of day (server flag preferred).
 */
export function canPatientSameDayReschedule(
  visit: Pick<AppointmentDetails, 'status' | 'startsAtUtc' | 'canSameDayReschedule' | 'sameDayRescheduleCount'>,
  nowMs: number = Date.now(),
): boolean {
  if (visit.status !== 'Confirmed') return false;
  if (visit.canSameDayReschedule === false) return false;
  if (typeof visit.sameDayRescheduleCount === 'number' && visit.sameDayRescheduleCount >= 2) return false;
  const startMs = new Date(visit.startsAtUtc).getTime();
  if (Number.isNaN(startMs) || startMs > nowMs) return false;
  // Local calendar day of the appointment must be clinic "today".
  const visitDay = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Kolkata',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date(visit.startsAtUtc));
  return visitDay === clinicDateIso(new Date(nowMs));
}

/** Show Reschedule when pre-start policy allows OR same-day missed path is open. */
export function canPatientRescheduleVisit(
  visit: Pick<AppointmentDetails, 'status' | 'startsAtUtc' | 'canSameDayReschedule' | 'sameDayRescheduleCount'>,
  nowMs: number = Date.now(),
): boolean {
  return canPatientCancelVisit(visit, nowMs) || canPatientSameDayReschedule(visit, nowMs);
}

/** @deprecated Use canPatientCancelVisit / canPatientRescheduleVisit */
export function canPatientModifyVisit(
  visit: Pick<AppointmentDetails, 'status' | 'startsAtUtc'>,
  nowMs: number = Date.now(),
): boolean {
  return canPatientCancelVisit(visit, nowMs);
}

/** Earliest ms when modify actions should refresh for the given visits (start or 2h cutoff). */
export function nextPatientModifyRefreshAt(
  visits: Pick<AppointmentDetails, 'status' | 'startsAtUtc'>[],
  nowMs: number = Date.now(),
): number | null {
  let soonest: number | null = null;
  for (const visit of visits) {
    if (visit.status !== 'Confirmed' && visit.status !== 'Requested') continue;
    const startMs = new Date(visit.startsAtUtc).getTime();
    if (Number.isNaN(startMs)) continue;
    for (const boundary of [startMs - PATIENT_CHANGE_CUTOFF_MS, startMs]) {
      if (boundary <= nowMs) continue;
      if (soonest === null || boundary < soonest) soonest = boundary;
    }
  }
  return soonest;
}
