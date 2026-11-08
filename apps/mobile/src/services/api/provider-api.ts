import axios from 'axios';

import { api, refreshSession } from '@/services/api/client';
import { persistSession, useSessionStore } from '@/stores/session-store';

export type Specialization = { id: string; code: string; name: string; isPrimary: boolean };

export type ProviderDocument = {
  id: string;
  category: string;
  fileName: string;
  version: number;
  isCurrent: boolean;
  scanEngine: string;
  scanDetail: string;
  rejectionReason: string | null;
};

export type DoctorProfile = {
  id: string;
  reference: string;
  status: string;
  verificationBadge: string;
  profileCompletionPercent: number;
  photoDocumentId: string | null;
  isPubliclyBookable: boolean;
  isAcceptingPatients: boolean;
  firstName: string | null;
  lastName: string | null;
  phoneNumber: string | null;
  dateOfBirth: string | null;
  gender: string | null;
  email: string | null;
  preferredLanguage: string;
  displayName: string | null;
  biography: string | null;
  yearsOfExperience: number;
  languages: string[];
  expertise: string[];
  qualifications: { id: string; name: string; institution: string | null; graduationYear: number }[];
  specializations: Specialization[];
  registrations: {
    id: string;
    number: string;
    council: string;
    state: string;
    registrationType: string;
    registeredOn: string;
    expiresOn: string | null;
    isCurrent: boolean;
  }[];
  documents: ProviderDocument[];
  affiliations: { id: string; clinicId: string; clinicName: string; clinicStatus: string; kind: string; isPrimary: boolean }[];
  rejectionReason: string | null;
  submittedAtUtc: string | null;
};

export type ClinicProfile = {
  id: string;
  reference: string;
  status: string;
  isPubliclyListed: boolean;
  name: string | null;
  type: string | null;
  phoneNumber: string | null;
  registrationNumber: string | null;
  branches: {
    id: string;
    name: string;
    addressLine: string;
    addressLine2?: string | null;
    landmark?: string | null;
    locality?: string | null;
    city: string;
    district?: string | null;
    state: string;
    postalCode: string;
    country?: string;
    latitude?: number | null;
    longitude?: number | null;
    googlePlaceId?: string | null;
    formattedAddress?: string | null;
    compactAddress?: string | null;
    isPrimary: boolean;
    isActive: boolean;
    locationVerified?: boolean;
  }[];
  documents: ProviderDocument[];
  rejectionReason: string | null;
};

export type DoctorCard = {
  id: string;
  displayName: string;
  yearsOfExperience: number;
  primarySpecialization: string;
  photoDocumentId: string | null;
  clinicName?: string | null;
  city?: string | null;
  qualification?: string | null;
  isVerified?: boolean;
};

export type ClinicCard = {
  id: string;
  name: string;
  type: string;
  city: string;
  logoDocumentId: string | null;
  branchName?: string | null;
  location?: string | null;
  associatedDoctors?: string[] | null;
};

export type ReviewItem = {
  kind: string;
  id: string;
  reference: string;
  name: string;
  status: string;
  submittedAtUtc: string | null;
};

async function withAuthRetry<T>(run: () => Promise<T>): Promise<T> {
  try {
    return await run();
  } catch (error) {
    if (!axios.isAxiosError(error) || error.response?.status !== 403) {
      throw error;
    }

    const refreshToken = useSessionStore.getState().refreshToken;
    if (!refreshToken) {
      throw error;
    }

    await persistSession(await refreshSession(refreshToken));
    return run();
  }
}

export async function getSpecializations() {
  const response = await withAuthRetry(() => api.get<Specialization[]>('/api/v1/specializations'));
  return response.data;
}

export async function getMyDoctor() {
  try {
    const response = await withAuthRetry(() => api.get<DoctorProfile>('/api/v1/doctors/me'));
    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404) {
      return null;
    }

    throw error;
  }
}

export async function saveDoctorBasics(body: {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  gender: string;
  email: string;
  preferredLanguage: string;
}) {
  const response = await withAuthRetry(() => api.put<DoctorProfile>('/api/v1/doctors/me/basics', body));
  return response.data;
}

export async function saveDoctorProfessional(body: {
  displayName: string;
  biography: string;
  yearsOfExperience: number;
  languages: string[];
  expertise: string[];
}) {
  const response = await withAuthRetry(() => api.put<DoctorProfile>('/api/v1/doctors/me/professional', body));
  return response.data;
}

export async function addQualification(body: { name: string; institution: string; graduationYear: number }) {
  const response = await withAuthRetry(() => api.post<DoctorProfile>('/api/v1/doctors/me/qualifications', body));
  return response.data;
}

export async function setSpecializations(specializationIds: string[], primarySpecializationId: string) {
  const response = await withAuthRetry(() =>
    api.put<DoctorProfile>('/api/v1/doctors/me/specializations', { specializationIds, primarySpecializationId }),
  );
  return response.data;
}

export async function saveRegistration(body: {
  number: string;
  council: string;
  state: string;
  registrationType: string;
  registeredOn: string;
  expiresOn: string | null;
}) {
  const response = await withAuthRetry(() => api.put<DoctorProfile>('/api/v1/doctors/me/registration', body));
  return response.data;
}

export async function uploadDoctorDocument(category: string, file: File, onProgress?: (percent: number) => void) {
  const form = new FormData();
  form.append('file', file);
  form.append('category', category);
  const response = await withAuthRetry(() =>
    api.post<DoctorProfile>('/api/v1/doctors/me/documents', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
      onUploadProgress: (event) => {
        if (event.total) {
          onProgress?.(Math.round((event.loaded / event.total) * 100));
        }
      },
    }),
  );
  return response.data;
}

export async function submitDoctor() {
  const response = await withAuthRetry(() => api.post<DoctorProfile>('/api/v1/doctors/me/submit'));
  return response.data;
}

export async function linkClinic(clinicId: string, kind: string) {
  const response = await withAuthRetry(() => api.post<DoctorProfile>('/api/v1/doctors/me/affiliations', { clinicId, kind }));
  return response.data;
}

export async function searchDoctors(query: string, signal?: AbortSignal) {
  const response = await withAuthRetry(() =>
    api.get<DoctorCard[]>('/api/v1/doctors', { params: { query }, signal }),
  );
  return response.data;
}

export async function searchClinics(query: string, signal?: AbortSignal) {
  const response = await withAuthRetry(() =>
    api.get<ClinicCard[]>('/api/v1/clinics', { params: { query }, signal }),
  );
  return response.data;
}

export async function getMyClinics() {
  const response = await withAuthRetry(() => api.get<ClinicProfile[]>('/api/v1/clinics/mine'));
  return response.data;
}

export async function createClinic() {
  const response = await withAuthRetry(() => api.post<ClinicProfile>('/api/v1/clinics'));
  return response.data;
}

export async function saveClinic(clinicId: string, body: {
  name: string;
  type: string;
  phoneNumber: string;
  registrationNumber: string;
  establishmentYear: number;
}) {
  const response = await withAuthRetry(() => api.put<ClinicProfile>(`/api/v1/clinics/${clinicId}`, body));
  return response.data;
}

export async function addBranch(clinicId: string, body: {
  name: string;
  addressLine: string;
  addressLine2?: string | null;
  landmark?: string | null;
  locality?: string | null;
  city: string;
  district?: string | null;
  state: string;
  postalCode: string;
  country?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  googlePlaceId?: string | null;
  isPrimary: boolean;
}) {
  const response = await withAuthRetry(() => api.post<ClinicProfile>(`/api/v1/clinics/${clinicId}/branches`, body));
  return response.data;
}

export async function uploadClinicDocument(clinicId: string, category: string, file: File) {
  const form = new FormData();
  form.append('file', file);
  form.append('category', category);
  const response = await withAuthRetry(() =>
    api.post<ClinicProfile>(`/api/v1/clinics/${clinicId}/documents`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    }),
  );
  return response.data;
}

export async function submitClinic(clinicId: string) {
  const response = await withAuthRetry(() => api.post<ClinicProfile>(`/api/v1/clinics/${clinicId}/submit`));
  return response.data;
}

export async function getReviewQueue() {
  const response = await withAuthRetry(() => api.get<ReviewItem[]>('/api/v1/admin/verifications'));
  return response.data;
}

export type DiscoveryPlace = {
  scheduleId: string;
  doctorId: string;
  displayName: string;
  primarySpecialization: string;
  yearsOfExperience: number;
  photoDocumentId?: string | null;
  clinicId: string | null;
  clinicName: string | null;
  branchId: string | null;
  branchName: string | null;
  addressLine: string | null;
  addressLine2?: string | null;
  landmark?: string | null;
  locality?: string | null;
  city: string | null;
  district?: string | null;
  state?: string | null;
  postalCode: string | null;
  country?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  googlePlaceId?: string | null;
  formattedAddress?: string | null;
  compactAddress?: string | null;
  consultationFee: number;
  slotDurationMinutes: number;
  distanceKm: number | null;
  phoneNumber: string | null;
  qualification?: string | null;
  registrationNumber?: string | null;
  registrationCouncil?: string | null;
  mapsUrl?: string | null;
  directionsUrl: string | null;
  locationVerified?: boolean;
};

export type DiscoveredDoctor = {
  id: string;
  displayName: string;
  biography: string | null;
  yearsOfExperience: number;
  gender: string | null;
  primarySpecialization: string;
  specializations: string[];
  qualifications: string[];
  expertise: string[];
  languages: string[];
  registrationCouncil: string | null;
  registrationState: string | null;
  photoDocumentId?: string | null;
  practices: DiscoveryPlace[];
};

export type DiscoveredClinic = {
  id: string;
  name: string;
  type: string;
  description: string | null;
  logoDocumentId?: string | null;
  facilities: string[];
  specializations: string[];
  branches: {
    id: string;
    name: string;
    addressLine: string;
    addressLine2?: string | null;
    landmark?: string | null;
    locality?: string | null;
    city: string;
    district?: string | null;
    state?: string | null;
    postalCode: string;
    country?: string | null;
    phoneNumber: string | null;
    latitude?: number | null;
    longitude?: number | null;
    googlePlaceId?: string | null;
    formattedAddress?: string | null;
    compactAddress?: string | null;
    distanceKm: number | null;
    doctorCount: number;
    mapsUrl?: string | null;
    directionsUrl: string | null;
    locationVerified?: boolean;
    openToday: boolean;
  }[];
};

export type ScheduleBookingMode = 'TimedSlots' | 'HourlyQueue';

export type ScheduleDetails = {
  id: string;
  doctorId: string;
  clinicId: string | null;
  branchId: string | null;
  consultationFee: number;
  currency: string;
  slotDurationMinutes: number;
  bufferMinutes: number;
  maxPatients: number | null;
  bookingMode: ScheduleBookingMode;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  timeZoneId: string;
  isPublished: boolean;
  requiresApproval: boolean;
  rowVersion: string;
  hours: { id: string; day: number; startsAt: string; endsAt: string }[];
  breaks: { id: string; day: number; startsAt: string; endsAt: string }[];
  closures: { id: string; date: string; reason: string | null }[];
  extras: { id: string; date: string; startsAt: string; endsAt: string }[];
};

export type ScheduleSlot = {
  start: string;
  end: string;
  available?: boolean;
  bookedCount?: number | null;
  capacity?: number | null;
  remaining?: number | null;
  tokenPrefix?: string | null;
};

export type SlotAvailability = {
  scheduleId: string;
  date: string;
  bookingMode: ScheduleBookingMode;
  slots: ScheduleSlot[];
};

export type DiscoveryFilters = {
  query?: string;
  specializationId?: string;
  clinicId?: string;
  city?: string;
  latitude?: string;
  longitude?: string;
  radiusKm?: string;
  maxFee?: string;
  minExperience?: string;
  availability?: string;
  sort?: string;
  postalCode?: string;
  page?: number;
  pageSize?: number;
};

export async function discoverPlaces(filters: DiscoveryFilters, signal?: AbortSignal) {
  const response = await withAuthRetry(() =>
    api.get<DiscoveryPlace[]>('/api/v1/search/doctors', { params: filters, signal }),
  );
  return response.data;
}

export async function discoverClinics(filters: DiscoveryFilters) {
  const response = await withAuthRetry(() => api.get<DiscoveredClinic[]>('/api/v1/search/clinics', { params: filters }));
  return response.data;
}

export async function getDiscoveredClinic(clinicId: string) {
  const response = await withAuthRetry(() => api.get<DiscoveredClinic>(`/api/v1/search/clinics/${clinicId}`));
  return response.data;
}

export async function getAvailability(doctorId: string, from: string, to: string, branchId?: string | null) {
  const response = await withAuthRetry(() =>
    api.get<{ date: string; status: string; slotCount: number; firstSlot: string | null; scheduleId: string }[]>(
      `/api/v1/availability/doctors/${doctorId}`,
      { params: { from, to, branchId: branchId || undefined } },
    ),
  );
  return response.data;
}

export async function listFavourites() {
  const response = await withAuthRetry(() => api.get<DiscoveredDoctor[]>('/api/v1/favourites'));
  return response.data;
}

export async function addFavourite(doctorId: string) {
  await withAuthRetry(() => api.put(`/api/v1/favourites/${doctorId}`));
}

export async function removeFavourite(doctorId: string) {
  await withAuthRetry(() => api.delete(`/api/v1/favourites/${doctorId}`));
}

export async function getDiscoveredDoctor(doctorId: string) {
  const response = await withAuthRetry(() => api.get<DiscoveredDoctor>(`/api/v1/discovery/doctors/${doctorId}`));
  return response.data;
}

export async function getSlots(scheduleId: string, date: string, exceptAppointmentId?: string | null) {
  const response = await withAuthRetry(() =>
    api.get<SlotAvailability>(`/api/v1/schedules/${scheduleId}/slots`, {
      params: { date, exceptAppointmentId: exceptAppointmentId || undefined },
    }),
  );
  return response.data;
}

export async function listMySchedules() {
  const response = await withAuthRetry(() => api.get<ScheduleDetails[]>('/api/v1/schedules/mine'));
  return response.data;
}

export async function createSchedule(body: {
  doctorId?: string;
  clinicId?: string;
  branchId?: string;
  consultationFee: number;
  slotDurationMinutes: number;
  bufferMinutes?: number;
}) {
  const response = await withAuthRetry(() => api.post<ScheduleDetails>('/api/v1/schedules', body));
  return response.data;
}

export async function saveScheduleHours(
  scheduleId: string,
  periods: { day: string; startsAt: string; endsAt: string }[],
  expectedRowVersion?: string,
) {
  const response = await withAuthRetry(() =>
    api.put<ScheduleDetails>(`/api/v1/schedules/${scheduleId}/hours`, { periods, expectedRowVersion }),
  );
  return response.data;
}

export async function saveScheduleBreaks(
  scheduleId: string,
  periods: { day: string; startsAt: string; endsAt: string }[],
  expectedRowVersion?: string,
) {
  const response = await withAuthRetry(() =>
    api.put<ScheduleDetails>(`/api/v1/schedules/${scheduleId}/breaks`, { periods, expectedRowVersion }),
  );
  return response.data;
}

export async function addScheduleClosure(
  scheduleId: string,
  date: string,
  reason: string,
  expectedRowVersion?: string,
) {
  const response = await withAuthRetry(() =>
    api.post<ScheduleDetails>(`/api/v1/schedules/${scheduleId}/closures`, { date, reason, expectedRowVersion }),
  );
  return response.data;
}

export async function removeScheduleClosure(scheduleId: string, closureId: string, expectedRowVersion?: string) {
  const response = await withAuthRetry(() =>
    api.delete<ScheduleDetails>(`/api/v1/schedules/${scheduleId}/closures/${closureId}`, {
      params: expectedRowVersion ? { expectedRowVersion } : undefined,
    }),
  );
  return response.data;
}

export async function saveScheduleTerms(
  schedule: ScheduleDetails,
  requiresApproval: boolean,
  options?: { bookingMode?: ScheduleBookingMode; confirmBookingModeChange?: boolean },
) {
  const response = await withAuthRetry(() =>
    api.put<ScheduleDetails>(`/api/v1/schedules/${schedule.id}`, {
      consultationFee: schedule.consultationFee,
      slotDurationMinutes: schedule.slotDurationMinutes,
      bufferMinutes: schedule.bufferMinutes,
      maxPatients: schedule.maxPatients,
      bookingMode: options?.bookingMode ?? schedule.bookingMode,
      confirmBookingModeChange: options?.confirmBookingModeChange ?? false,
      effectiveFrom: schedule.effectiveFrom,
      effectiveTo: schedule.effectiveTo,
      timeZoneId: schedule.timeZoneId,
      requiresApproval,
      expectedRowVersion: schedule.rowVersion || undefined,
    }),
  );
  return response.data;
}

export async function setSchedulePublished(
  scheduleId: string,
  isPublished: boolean,
  expectedRowVersion?: string,
) {
  const response = await withAuthRetry(() =>
    api.post<ScheduleDetails>(`/api/v1/schedules/${scheduleId}/publication`, {
      isPublished,
      expectedRowVersion,
    }),
  );
  return response.data;
}

export async function addScheduleExtra(
  scheduleId: string,
  date: string,
  startsAt: string,
  endsAt: string,
  expectedRowVersion?: string,
) {
  const response = await withAuthRetry(() =>
    api.post<ScheduleDetails>(`/api/v1/schedules/${scheduleId}/extras`, {
      date,
      startsAt,
      endsAt,
      expectedRowVersion,
    }),
  );
  return response.data;
}

export async function replaceDayOverride(
  scheduleId: string,
  body: {
    date: string;
    available: boolean;
    unavailableReason?: string;
    windows: { startsAt: string; endsAt: string }[];
    expectedRowVersion?: string;
  },
) {
  const response = await withAuthRetry(() =>
    api.put<ScheduleDetails>(`/api/v1/schedules/${scheduleId}/day-override`, body),
  );
  return response.data;
}

export async function resetDayOverride(scheduleId: string, date: string, expectedRowVersion?: string) {
  const response = await withAuthRetry(() =>
    api.post<ScheduleDetails>(`/api/v1/schedules/${scheduleId}/day-override/reset`, {
      date,
      expectedRowVersion,
    }),
  );
  return response.data;
}

export async function copyDaySchedule(
  scheduleId: string,
  body: {
    sourceDate: string;
    targetDates: string[];
    overwriteExisting: boolean;
    expectedRowVersion?: string;
  },
) {
  const response = await withAuthRetry(() =>
    api.post<ScheduleDetails>(`/api/v1/schedules/${scheduleId}/copy-day`, body),
  );
  return response.data;
}

export async function getClosureImpact(scheduleId: string, date: string) {
  const response = await withAuthRetry(() =>
    api.get<{ confirmedAppointments: number }>(`/api/v1/schedules/${scheduleId}/closure-impact`, {
      params: { date },
    }),
  );
  return response.data;
}

export type AppointmentDetails = {
  id: string;
  reference: string | null;
  status: string;
  patientName: string;
  familyMemberId: string | null;
  accountUserId: string | null;
  doctorId: string;
  doctorName: string;
  doctorSpecialization: string | null;
  doctorVerified: boolean;
  scheduleId: string;
  clinicId: string;
  clinicName: string;
  branchId: string;
  branchName: string;
  address: string;
  latitude: number | null;
  longitude: number | null;
  googlePlaceId?: string | null;
  mapsUrl?: string | null;
  directionsUrl?: string | null;
  locationVerified?: boolean;
  startsAtUtc: string;
  endsAtUtc: string;
  timeZoneId: string;
  consultationFee: number;
  currency: string;
  paymentMethod: string;
  paymentStatus: string;
  paidAmount: number;
  requiresApproval: boolean;
  isWalkIn: boolean;
  holdExpiresAtUtc: string | null;
  reason: string | null;
  notes: string | null;
  decisionNote: string | null;
  cancellationPolicy: string;
  tokenLabel: string | null;
  tokenNumber: number | null;
  bookingMode?: ScheduleBookingMode;
  sameDayRescheduleCount?: number;
  canSameDayReschedule?: boolean;
};

export type FamilyMember = {
  id: string;
  fullName: string;
  dateOfBirth: string | null;
  gender: string | null;
  relationship: string;
};

export async function holdAppointment(scheduleId: string, date: string, start: string) {
  const response = await withAuthRetry(() =>
    api.post<AppointmentDetails>('/api/v1/appointments/holds', { scheduleId, date, start }),
  );
  return response.data;
}

export async function confirmAppointment(
  appointmentId: string,
  body: { familyMemberId?: string; reason?: string; notes?: string; followUpId?: string },
) {
  const response = await withAuthRetry(() => api.post<AppointmentDetails>(`/api/v1/appointments/${appointmentId}/confirm`, body));
  return response.data;
}

export async function releaseAppointment(appointmentId: string) {
  await withAuthRetry(() => api.post(`/api/v1/appointments/${appointmentId}/release`));
}

export async function listAppointments() {
  const response = await withAuthRetry(() => api.get<AppointmentDetails[]>('/api/v1/appointments'));
  return response.data;
}

export async function getAppointment(appointmentId: string) {
  const response = await withAuthRetry(() => api.get<AppointmentDetails>(`/api/v1/appointments/${appointmentId}`));
  return response.data;
}

export async function getPracticeAppointment(appointmentId: string) {
  const response = await withAuthRetry(() =>
    api.get<AppointmentDetails>(`/api/v1/appointments/${appointmentId}/practice`),
  );
  return response.data;
}

export async function cancelAppointment(appointmentId: string, reason: string) {
  const response = await withAuthRetry(() =>
    api.post<AppointmentDetails>(`/api/v1/appointments/${appointmentId}/cancellation`, { reason }),
  );
  return response.data;
}

export async function rescheduleAppointment(appointmentId: string, date: string, start: string, reason?: string) {
  const response = await withAuthRetry(() =>
    api.post<AppointmentDetails>(`/api/v1/appointments/${appointmentId}/reschedule`, {
      date,
      start,
      reason: reason?.trim() || undefined,
    }),
  );
  return response.data;
}

export async function listPracticeAppointments(
  from: string,
  to: string,
  options?: { clinicId?: string | null; assignedOnly?: boolean },
) {
  const response = await withAuthRetry(() =>
    api.get<AppointmentDetails[]>('/api/v1/appointments/practice', {
      params: {
        from,
        to,
        assignedOnly: options?.assignedOnly ?? false,
        ...(options?.clinicId ? { clinicId: options.clinicId } : {}),
      },
    }),
  );
  return response.data;
}

export async function decideAppointment(appointmentId: string, decision: 'Approve' | 'Reject', reason: string) {
  const response = await withAuthRetry(() =>
    api.post<AppointmentDetails>(`/api/v1/appointments/${appointmentId}/decision`, { decision, reason }),
  );
  return response.data;
}

export async function bookWalkIn(body: { scheduleId: string; date: string; start: string; patientName: string }) {
  const response = await withAuthRetry(() => api.post<AppointmentDetails>('/api/v1/appointments/walk-ins', body));
  return response.data;
}

export async function listAdminAppointments(from: string, to: string) {
  const response = await withAuthRetry(() =>
    api.get<
      {
        id: string;
        reference: string | null;
        status: string;
        doctorName: string;
        clinicName: string;
        startsAtUtc: string;
        consultationFee: number;
        currency: string;
      }[]
    >('/api/v1/admin/appointments', { params: { from, to } }),
  );
  return response.data;
}

export async function listFamily() {
  const response = await withAuthRetry(() => api.get<FamilyMember[]>('/api/v1/family-members'));
  return response.data;
}

export async function addFamily(body: { fullName: string; relationship: string; dateOfBirth?: string; gender?: string }) {
  const response = await withAuthRetry(() => api.post<FamilyMember>('/api/v1/family-members', body));
  return response.data;
}

export async function reviewApplication(kind: string, id: string, decision: string, reason: string) {
  const path = kind === 'clinic' ? 'clinics' : 'doctors';
  const response = await withAuthRetry(() =>
    api.post(`/api/v1/admin/verifications/${path}/${id}`, { decision, reason }),
  );
  return response.data;
}
