/** Super Admin clinic/doctor administration capabilities. */
export function canCreateClinic(permissions: readonly string[] | undefined | null): boolean {
  const list = permissions ?? [];
  return list.includes('clinic.create') || list.includes('admin.users');
}

export function canCreateDoctor(permissions: readonly string[] | undefined | null): boolean {
  const list = permissions ?? [];
  return list.includes('doctor.create') || list.includes('admin.users');
}

export function canManageProviderApplications(permissions: readonly string[] | undefined | null): boolean {
  const list = permissions ?? [];
  return list.includes('provider.onboard') || canCreateClinic(list) || canCreateDoctor(list);
}
