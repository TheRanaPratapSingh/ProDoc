import type { Href } from 'expo-router';

import type { Account } from '@/services/api/types';

export type HomeSurface = 'admin' | 'doctor' | 'clinic' | 'reception' | 'staff' | 'patient';

const SURFACE_HREFS: Record<HomeSurface, Href> = {
  admin: '/admin',
  doctor: '/doctor-home',
  clinic: '/clinic-home',
  reception: '/reception', // Reception desk is the receptionist landing workspace
  staff: '/staff-home',
  patient: '/home',
};

export function normalizeHomeSurface(value: string | null | undefined): HomeSurface {
  switch ((value ?? '').toLowerCase()) {
    case 'admin':
      return 'admin';
    case 'doctor':
      return 'doctor';
    case 'clinic':
      return 'clinic';
    case 'reception':
      return 'reception';
    case 'staff':
      return 'staff';
    default:
      return 'patient';
  }
}

export function surfacesForAccount(account: Account | null | undefined): HomeSurface[] {
  const fromApi = (account?.availableSurfaces ?? [])
    .map((item) => normalizeHomeSurface(item))
    .filter((item, index, all) => all.indexOf(item) === index);

  if (fromApi.length > 0) {
    return fromApi;
  }

  // Fallback for older sessions: derive from roles, never invent privileged access.
  const roles = new Set((account?.roles ?? []).map((role) => role.toLowerCase()));
  const surfaces: HomeSurface[] = [];
  if (roles.has('platformadmin')) surfaces.push('admin');
  if (roles.has('doctor')) surfaces.push('doctor');
  if (roles.has('clinicadmin') || roles.has('organizationadmin')) surfaces.push('clinic');
  if (roles.has('receptionist')) surfaces.push('reception');
  if (roles.has('clinicstaff')) surfaces.push('staff');
  if (surfaces.length === 0) surfaces.push('patient');
  return surfaces;
}

/** True when the account can open more than one distinct dashboard. */
export function needsRoleSelection(account: Account | null | undefined): boolean {
  return surfacesForAccount(account).length > 1;
}

export function hrefForSurface(surface: string | null | undefined): Href {
  return SURFACE_HREFS[normalizeHomeSurface(surface)];
}

/**
 * Backend-assigned post-login surface. Prefer an explicit active surface when the
 * user already chose one from a multi-role picker; otherwise use homeSurface.
 * Never invent privileged dashboards on the client.
 */
export function homeHrefForAccount(
  account: Account | null | undefined,
  activeSurface?: string | null,
): Href {
  if (!account) {
    return '/sign-in';
  }

  const allowed = surfacesForAccount(account);
  if (allowed.length > 1 && !activeSurface) {
    return '/select-role';
  }

  const preferred = normalizeHomeSurface(activeSurface || account.homeSurface);
  // Incomplete patient profile must finish Name + Age before Patient Home.
  if (preferred === 'patient' && needsProfileCompletion(account)) {
    return '/complete-profile';
  }

  if (allowed.includes(preferred)) {
    return hrefForSurface(preferred);
  }

  if ((allowed[0] ?? 'patient') === 'patient' && needsProfileCompletion(account)) {
    return '/complete-profile';
  }

  return hrefForSurface(allowed[0] ?? 'patient');
}

export function accountCanAccessSurface(
  account: Account | null | undefined,
  surface: string,
): boolean {
  return surfacesForAccount(account).includes(normalizeHomeSurface(surface));
}

/** Backend-persisted completion flag; falls back to Name for older sessions. */
export function needsProfileCompletion(account: Account | null | undefined): boolean {
  if (!account) {
    return false;
  }
  if (typeof account.profileCompleted === 'boolean') {
    return !account.profileCompleted;
  }
  return !account.fullName?.trim();
}

export function surfaceLabel(
  surface: HomeSurface,
  copy: {
    roleSurfaceAdmin: string;
    roleSurfaceDoctor: string;
    roleSurfaceClinic: string;
    roleSurfaceReception: string;
    roleSurfaceStaff: string;
    roleSurfacePatient: string;
  },
): string {
  switch (surface) {
    case 'admin':
      return copy.roleSurfaceAdmin;
    case 'doctor':
      return copy.roleSurfaceDoctor;
    case 'clinic':
      return copy.roleSurfaceClinic;
    case 'reception':
      return copy.roleSurfaceReception;
    case 'staff':
      return copy.roleSurfaceStaff;
    default:
      return copy.roleSurfacePatient;
  }
}
