import { useEffect, useMemo } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useQuery } from '@tanstack/react-query';

import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { listDeskClinics, type DeskClinic } from '@/services/api/queue-api';
import { useReceptionClinicStore } from '@/stores/reception-clinic-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export function useReceptionDeskSelection() {
  const clinics = useQuery({ queryKey: ['clinics', 'desk'], queryFn: listDeskClinics });
  const selectedClinicId = useReceptionClinicStore((state) => state.selectedClinicId);
  const selectedBranchId = useReceptionClinicStore((state) => state.selectedBranchId);
  const setSelection = useReceptionClinicStore((state) => state.setSelection);

  const clinic = useMemo(() => {
    const rows = clinics.data ?? [];
    if (selectedClinicId) {
      return rows.find((item) => item.id === selectedClinicId) ?? rows[0];
    }
    return rows[0];
  }, [clinics.data, selectedClinicId]);

  const branch = useMemo(() => {
    const branches = clinic?.branches ?? [];
    if (selectedBranchId) {
      return branches.find((item) => item.id === selectedBranchId) ?? branches[0];
    }
    return branches[0];
  }, [clinic, selectedBranchId]);

  useEffect(() => {
    if (!clinic) return;
    if (clinic.id !== selectedClinicId || (branch && branch.id !== selectedBranchId)) {
      setSelection(clinic.id, branch?.id ?? null);
    }
  }, [clinic, branch, selectedClinicId, selectedBranchId, setSelection]);

  return {
    clinicsQuery: clinics,
    clinics: clinics.data ?? ([] as DeskClinic[]),
    clinic,
    branch,
    clinicId: clinic?.id ?? '',
    branchId: branch?.id ?? '',
    setSelection,
  };
}

export function ReceptionClinicSwitcher() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const { clinics, clinic, branch, setSelection } = useReceptionDeskSelection();

  if (clinics.length === 0) {
    return null;
  }

  return (
    <View style={{ gap: spacing.sm }}>
      <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
        {copy.receptionSelectClinic}
      </AppText>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
        {clinics.map((item) => {
          const selected = clinic?.id === item.id;
          return (
            <Pressable
              key={item.id}
              onPress={() => setSelection(item.id, item.branches[0]?.id ?? null)}
              style={{
                paddingHorizontal: spacing.md,
                paddingVertical: spacing.sm,
                borderRadius: radius.pill,
                borderWidth: 1,
                borderColor: selected ? theme.colors.primary : theme.colors.border,
                backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
              }}>
              <AppText variant="caption" style={{ color: selected ? theme.colors.primary : theme.colors.text }}>
                {item.name}
              </AppText>
            </Pressable>
          );
        })}
      </ScrollView>
      {(clinic?.branches.length ?? 0) > 1 ? (
        <>
          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
            {copy.receptionSelectBranch}
          </AppText>
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
            {(clinic?.branches ?? []).map((item) => {
              const selected = branch?.id === item.id;
              return (
                <Pressable
                  key={item.id}
                  onPress={() => setSelection(clinic!.id, item.id)}
                  style={{
                    paddingHorizontal: spacing.md,
                    paddingVertical: spacing.sm,
                    borderRadius: radius.pill,
                    borderWidth: 1,
                    borderColor: selected ? theme.colors.primary : theme.colors.border,
                    backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
                  }}>
                  <AppText variant="caption" style={{ color: selected ? theme.colors.primary : theme.colors.text }}>
                    {item.name}
                  </AppText>
                </Pressable>
              );
            })}
          </ScrollView>
        </>
      ) : null}
    </View>
  );
}
