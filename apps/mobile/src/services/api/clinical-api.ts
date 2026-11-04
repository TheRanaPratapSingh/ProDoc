import axios from 'axios';

import { api, refreshSession } from '@/services/api/client';
import { persistSession, useSessionStore } from '@/stores/session-store';

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

export type Diagnosis = { id: string; description: string; code: string | null; isPrimary: boolean; sortOrder: number };
export type Medicine = {
  id: string;
  catalogEntryId: string | null;
  medicineName: string;
  dosage: string;
  frequency: string;
  duration: string;
  route: string;
  instructions: string | null;
  sortOrder: number;
};

export type PrescriptionVersion = {
  id: string;
  versionNumber: number;
  status: string;
  diagnosis: string | null;
  symptoms: string | null;
  clinicalObservations: string | null;
  advice: string | null;
  followUpDate: string | null;
  patientName: string;
  doctorName: string;
  doctorRegistration: string | null;
  clinicName: string;
  branchName: string;
  branchAddress: string | null;
  consultationDateUtc: string;
  amendedFromVersionId: string | null;
  amendmentReason: string | null;
  createdAtUtc: string;
  finalizedAtUtc: string | null;
  hasPdf: boolean;
  medicines: Medicine[];
};

export type Prescription = {
  id: string;
  number: string;
  medicalRecordId: string;
  consultationRecordId: string | null;
  doctorId: string;
  clinicId: string;
  branchId: string;
  status: string;
  currentVersionNumber: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  current: PrescriptionVersion;
  versions: PrescriptionVersion[];
};

export type Consultation = {
  id: string;
  medicalRecordId: string;
  doctorId: string;
  doctorName: string;
  clinicId: string;
  clinicName: string;
  branchId: string;
  branchName: string;
  appointmentId: string | null;
  patientName: string;
  symptoms: string | null;
  clinicalObservations: string | null;
  doctorNotes: string | null;
  advice: string | null;
  followUpDate: string | null;
  status: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  diagnoses: Diagnosis[];
  prescriptions: { id: string; number: string; status: string; diagnosis: string | null }[];
};

export type MedicalRecord = {
  id: string;
  accountUserId: string;
  familyMemberId: string | null;
  subjectName: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  allergies: { id: string; substance: string; reaction: string | null; severity: string; isActive: boolean }[];
  conditions: { id: string; name: string; notes: string | null; isActive: boolean }[];
  medications: { id: string; name: string; dosage: string | null; isCurrent: boolean }[];
  consultations: {
    id: string;
    doctorName: string;
    clinicName: string;
    status: string;
    startedAtUtc: string;
    diagnoses: string[];
  }[];
  prescriptions: { id: string; number: string; status: string; diagnosis: string | null; finalizedAtUtc: string | null }[];
  documents: { id: string; title: string; fileName: string; contentType: string; sizeBytes: number; uploadedAtUtc: string }[];
};

export type Workspace = {
  appointmentId: string | null;
  doctorId: string;
  clinicId: string;
  branchId: string;
  patientName: string;
  appointmentStatus: string;
  appointmentStartsAtUtc: string | null;
  appointmentReason: string | null;
  record: MedicalRecord;
  activeConsultation: Consultation | null;
};

export type MedicalRecordSummary = {
  id: string;
  subjectName: string;
  familyMemberId: string | null;
  updatedAtUtc: string;
  consultationCount: number;
  prescriptionCount: number;
  documentCount: number;
};

export function openWorkspace(appointmentId: string) {
  return withAuthRetry(async () => (await api.get<Workspace>(`/api/v1/appointments/${appointmentId}/workspace`)).data);
}

export function startConsultation(appointmentId: string) {
  return withAuthRetry(async () => (await api.post<Consultation>(`/api/v1/appointments/${appointmentId}/consultations`)).data);
}

export function updateConsultation(
  consultationId: string,
  body: {
    symptoms?: string;
    clinicalObservations?: string;
    doctorNotes?: string;
    advice?: string;
    followUpDate?: string;
    diagnoses?: { description: string; code?: string; isPrimary: boolean }[];
    complete?: boolean;
  },
) {
  return withAuthRetry(async () => (await api.put<Consultation>(`/api/v1/consultations/${consultationId}`, body)).data);
}

export function createPrescription(consultationId: string) {
  return withAuthRetry(async () => (await api.post<Prescription>(`/api/v1/consultations/${consultationId}/prescriptions`)).data);
}

export function updatePrescription(
  prescriptionId: string,
  body: {
    diagnosis?: string;
    symptoms?: string;
    clinicalObservations?: string;
    advice?: string;
    followUpDate?: string;
    medicines: {
      catalogEntryId?: string;
      medicineName: string;
      dosage: string;
      frequency: string;
      duration: string;
      route: string;
      instructions?: string;
    }[];
  },
) {
  return withAuthRetry(async () => (await api.put<Prescription>(`/api/v1/prescriptions/${prescriptionId}`, body)).data);
}

export function finalizePrescription(prescriptionId: string) {
  return withAuthRetry(async () => (await api.post<Prescription>(`/api/v1/prescriptions/${prescriptionId}/finalize`)).data);
}

export function amendPrescription(prescriptionId: string, reason: string) {
  return withAuthRetry(async () => (await api.post<Prescription>(`/api/v1/prescriptions/${prescriptionId}/amend`, { reason })).data);
}

export function getPrescription(prescriptionId: string) {
  return withAuthRetry(async () => (await api.get<Prescription>(`/api/v1/prescriptions/${prescriptionId}`)).data);
}

export async function downloadPrescriptionPdf(prescriptionId: string) {
  return withAuthRetry(async () => {
    const response = await api.get(`/api/v1/prescriptions/${prescriptionId}/pdf`, { responseType: 'blob' });
    return response.data as Blob;
  });
}

export function listMyMedicalRecords() {
  return withAuthRetry(async () => (await api.get<MedicalRecordSummary[]>('/api/v1/patients/me/medical-records')).data);
}

export function getMedicalRecord(recordId: string) {
  return withAuthRetry(async () => (await api.get<MedicalRecord>(`/api/v1/medical-records/${recordId}`)).data);
}

export function upsertAllergy(
  recordId: string,
  body: { allergyId?: string; substance: string; reaction?: string; severity: string; isActive?: boolean },
) {
  return withAuthRetry(async () => (await api.post(`/api/v1/medical-records/${recordId}/allergies`, body)).data);
}

export function searchMedicines(q: string) {
  return withAuthRetry(async () => (await api.get<{ id: string; name: string; form: string | null }[]>('/api/v1/medicines', { params: { q } })).data);
}

export async function uploadMedicalDocument(recordId: string, file: Blob | { uri: string; name: string; type: string }, title: string, fileName?: string) {
  return withAuthRetry(async () => {
    const form = new FormData();
    form.append('title', title);
    if (file instanceof Blob) {
      form.append('file', file, fileName ?? 'report.bin');
    } else {
      form.append('file', { uri: file.uri, name: file.name, type: file.type } as unknown as Blob);
    }
    const response = await api.post(`/api/v1/medical-records/${recordId}/documents`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data;
  });
}

export async function downloadMedicalDocument(documentId: string) {
  return withAuthRetry(async () => {
    const response = await api.get(`/api/v1/medical-documents/${documentId}/content`, { responseType: 'blob' });
    return response.data as Blob;
  });
}
