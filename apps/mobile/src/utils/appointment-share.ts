import type { AppointmentDetails } from '@/services/api/provider-api';
import {
  clinicPlaceLabel,
  formatDoctorDisplayName,
  formatVisitDate,
  formatVisitTime,
} from '@/utils/visit-format';
import { buildMapsSearchUrl } from '@/utils/maps';

function formatClock(value: string) {
  const [hourPart, minutePart = '0'] = value.split(':');
  const hour = Number(hourPart);
  const minute = Number(minutePart);
  if (!Number.isFinite(hour) || !Number.isFinite(minute)) {
    return value;
  }
  const suffix = hour >= 12 ? 'PM' : 'AM';
  const twelve = ((hour + 11) % 12) + 1;
  if (minute === 0) {
    return `${twelve} ${suffix}`;
  }
  return `${twelve}:${String(minute).padStart(2, '0')} ${suffix}`;
}

function formatHourlyRangeLabel(start: string, end: string) {
  return `${formatClock(start)} – ${formatClock(end)}`;
}

export type ShareableAppointment = Pick<
  AppointmentDetails,
  | 'id'
  | 'reference'
  | 'status'
  | 'patientName'
  | 'doctorName'
  | 'doctorSpecialization'
  | 'clinicName'
  | 'branchName'
  | 'address'
  | 'latitude'
  | 'longitude'
  | 'googlePlaceId'
  | 'mapsUrl'
  | 'startsAtUtc'
  | 'endsAtUtc'
  | 'timeZoneId'
  | 'bookingMode'
  | 'tokenLabel'
  | 'tokenNumber'
>;

export type SharedSummaryLike = {
  bookingReference: string;
  status: string;
  patientName: string;
  doctorName: string;
  specialty?: string | null;
  clinicName: string;
  branchName?: string | null;
  address?: string | null;
  mapsUrl?: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  timeZoneId: string;
  bookingMode: string;
  tokenLabel?: string | null;
  tokenNumber?: number | null;
};

function timeLabel(visit: {
  startsAtUtc: string;
  endsAtUtc: string;
  timeZoneId: string;
  bookingMode: string;
}) {
  if (visit.bookingMode === 'HourlyQueue') {
    const start = new Date(visit.startsAtUtc).toLocaleTimeString('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      timeZone: visit.timeZoneId || undefined,
    });
    const end = new Date(visit.endsAtUtc).toLocaleTimeString('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      timeZone: visit.timeZoneId || undefined,
    });
    return formatHourlyRangeLabel(start, end);
  }

  const start = formatVisitTime(visit.startsAtUtc, visit.timeZoneId);
  const end = formatVisitTime(visit.endsAtUtc, visit.timeZoneId);
  return `${start} – ${end}`;
}

export function appointmentToShareSummary(visit: ShareableAppointment): SharedSummaryLike {
  return {
    bookingReference: visit.reference ?? visit.id.slice(0, 8).toUpperCase(),
    status: visit.status,
    patientName: visit.patientName || 'Patient',
    doctorName: formatDoctorDisplayName(visit.doctorName),
    specialty: visit.doctorSpecialization,
    clinicName: visit.clinicName,
    branchName: visit.branchName,
    address: visit.address,
    mapsUrl:
      visit.mapsUrl ||
      buildMapsSearchUrl({
        name: visit.clinicName,
        address: visit.address,
        latitude: visit.latitude,
        longitude: visit.longitude,
        googlePlaceId: visit.googlePlaceId,
      }) ||
      null,
    startsAtUtc: visit.startsAtUtc,
    endsAtUtc: visit.endsAtUtc,
    timeZoneId: visit.timeZoneId,
    bookingMode: visit.bookingMode ?? 'TimedSlots',
    tokenLabel: visit.tokenLabel,
    tokenNumber: visit.tokenNumber,
  };
}

export function buildAppointmentShareMessage(
  summary: SharedSummaryLike,
  options?: { recipientName?: string | null; shareUrl?: string | null },
): string {
  const clinic = clinicPlaceLabel(summary.clinicName, summary.branchName);
  const lines = [
    'ProDoc – Appointment Details',
    '',
    `Patient Name: ${summary.patientName}`,
    `Doctor: ${summary.doctorName}`,
  ];

  if (summary.specialty?.trim()) {
    lines.push(`Specialty: ${summary.specialty.trim()}`);
  }

  if (clinic) {
    lines.push(`Clinic: ${clinic}`);
  }

  lines.push(`Date: ${formatVisitDate(summary.startsAtUtc, summary.timeZoneId)}`);
  lines.push(`Time: ${timeLabel(summary)}`);

  if (summary.bookingMode === 'HourlyQueue' && summary.tokenLabel) {
    lines.push(`Token: ${summary.tokenLabel}`);
  } else if (summary.tokenLabel) {
    lines.push(`Token: ${summary.tokenLabel}`);
  }

  if (summary.address?.trim()) {
    lines.push(`Clinic Address: ${summary.address.trim()}`);
  }

  lines.push(`Booking ID: ${summary.bookingReference}`);

  if (summary.mapsUrl?.trim()) {
    lines.push(`Location: ${summary.mapsUrl.trim()}`);
  }

  lines.push(`Status: ${summary.status}`);

  if (options?.shareUrl?.trim()) {
    lines.push('');
    lines.push(
      options.recipientName?.trim()
        ? `Hi ${options.recipientName.trim()}, here are my appointment details booked through ProDoc. You can view the appointment information here: ${options.shareUrl.trim()}`
        : `Here are my appointment details booked through ProDoc. You can view the appointment information here: ${options.shareUrl.trim()}`,
    );
  }

  return lines.join('\n');
}
