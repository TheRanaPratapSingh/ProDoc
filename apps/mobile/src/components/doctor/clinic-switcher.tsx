import { useEffect } from 'react';
import { Pressable, ScrollView, View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import type { DoctorClinicOption } from '@/services/api/doctor-portal-api';
import { useDoctorClinicStore } from '@/stores/doctor-clinic-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export function ClinicSwitcher({
  clinics,
  onChanged,
}: {
  clinics: DoctorClinicOption[];
  onChanged?: () => void;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const selectedClinicId = useDoctorClinicStore((state) => state.selectedClinicId);
  const setSelectedClinicId = useDoctorClinicStore((state) => state.setSelectedClinicId);

  if (clinics.length === 0) {
    return null;
  }

  const chips: { id: string | null; label: string }[] = [
    { id: null, label: copy.doctorAllClinics },
    ...clinics.map((clinic) => ({ id: clinic.clinicId, label: clinic.name })),
  ];

  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
      {chips.map((chip) => {
        const selected = selectedClinicId === chip.id;
        return (
          <Pressable
            key={chip.id ?? 'all'}
            onPress={() => {
              setSelectedClinicId(chip.id);
              onChanged?.();
            }}
            style={{
              paddingHorizontal: spacing.md,
              paddingVertical: spacing.sm,
              borderRadius: radius.pill,
              borderWidth: 1,
              borderColor: selected ? theme.colors.primary : theme.colors.border,
              backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
            }}>
            <AppText variant="caption" style={{ color: selected ? theme.colors.primary : theme.colors.text }}>
              {chip.label}
            </AppText>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

export function useSelectedDoctorClinicId(clinics?: DoctorClinicOption[]) {
  const selectedClinicId = useDoctorClinicStore((state) => state.selectedClinicId);
  const setSelectedClinicId = useDoctorClinicStore((state) => state.setSelectedClinicId);
  const clinicsLoaded = clinics != null;
  const selectionValid =
    !selectedClinicId
    || !clinicsLoaded
    || clinics.some((clinic) => clinic.clinicId === selectedClinicId);

  useEffect(() => {
    if (!clinicsLoaded || selectionValid || !selectedClinicId) {
      return;
    }
    setSelectedClinicId(null);
  }, [clinicsLoaded, selectionValid, selectedClinicId, setSelectedClinicId]);

  // Clinics not loaded yet — do not send a persisted id that may belong to another doctor.
  if (!clinicsLoaded || clinics.length === 0) {
    return null;
  }

  if (!selectionValid) {
    return null;
  }

  return selectedClinicId;
}

export function ClinicSwitcherHint({ clinics }: { clinics: DoctorClinicOption[] }) {
  const theme = useProdocTheme();
  const selected = useSelectedDoctorClinicId(clinics);
  const active = selected ? clinics.find((clinic) => clinic.clinicId === selected) : null;
  if (!active) {
    return null;
  }
  return (
    <View>
      <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
        {active.branchName ? `${active.name} · ${active.branchName}` : active.name}
      </AppText>
    </View>
  );
}
