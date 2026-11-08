import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Alert, ScrollView, View } from 'react-native';

import { ReceptionClinicSwitcher, useReceptionDeskSelection } from '@/components/reception/clinic-switcher';
import { ReceptionShell } from '@/components/layout/reception-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { PhoneInput } from '@/components/ui/phone-input';
import { SearchBar } from '@/components/ui/search-bar';
import { SectionHeader } from '@/components/ui/section-header';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useFormFieldErrors } from '@/hooks/use-form-field-errors';
import { useCopy } from '@/i18n/copy';
import { indianMobileSchema, personNameSchema, receptionPatientSchema } from '@/schemas/common';
import { registerReceptionPatient, searchReceptionPatients } from '@/services/api/reception-api';
import { notifySuccess } from '@/stores/feedback-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { normalizeIndianMobile } from '@/utils/phone';
import { notifyApiError, problemMessage } from '@/utils/problem';

export function ReceptionPatients() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const { clinicId, branchId } = useReceptionDeskSelection();
  const [query, setQuery] = useState('');
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const fieldErrors = useFormFieldErrors<'fullName' | 'phone'>();

  const patients = useQuery({
    queryKey: ['reception', 'patients', clinicId, branchId, query],
    queryFn: () => searchReceptionPatients(clinicId, branchId, query || undefined),
    enabled: Boolean(clinicId),
  });

  const register = useMutation({
    mutationFn: async () => {
      const validated = fieldErrors.validateObject(receptionPatientSchema, { fullName, phone });
      if (!validated.ok) {
        throw new Error(validated.message);
      }
      const normalized = normalizeIndianMobile(validated.data.phone);
      if (!normalized) {
        fieldErrors.setFieldError('phone', 'Please enter a valid 10-digit mobile number.');
        throw new Error('Please enter a valid 10-digit mobile number.');
      }
      return registerReceptionPatient({
        clinicId,
        fullName: validated.data.fullName,
        phone: normalized,
      });
    },
    onSuccess: async () => {
      setFullName('');
      setPhone('');
      fieldErrors.clearAll();
      notifySuccess('Patient registered.');
      Alert.alert(copy.registerPatient, 'Patient registered.');
      await queryClient.invalidateQueries({ queryKey: ['reception', 'patients'] });
    },
    onError: (error) => {
      notifyApiError(error, copy.receptionPatientsTitle);
    },
  });

  return (
    <ReceptionShell active="patients">
      <AuthScaffold>
        <ScreenHeader title={copy.receptionPatientsTitle} />
        <ScrollView contentContainerStyle={{ gap: spacing.lg, paddingBottom: spacing.xxl }}>
          <ReceptionClinicSwitcher />
          <SearchBar value={query} onChangeText={setQuery} placeholder="Search by name or reference" />

          <SectionHeader title={copy.registerPatient} />
          <TextField
            label={copy.inviteFullName}
            required
            value={fullName}
            error={fieldErrors.errors.fullName}
            onChangeText={(value) => {
              fieldErrors.clearField('fullName');
              setFullName(value);
            }}
            onBlur={() => fieldErrors.validateOnBlur('fullName', personNameSchema, fullName)}
          />
          <PhoneInput
            label={copy.phoneLabel}
            required
            value={phone}
            error={fieldErrors.errors.phone}
            onChangeText={(value) => {
              fieldErrors.clearField('phone');
              setPhone(value);
            }}
            onBlur={() => fieldErrors.validateOnBlur('phone', indianMobileSchema, phone)}
          />
          {register.error ? (
            <AppText style={{ color: theme.colors.error }}>
              {problemMessage(register.error, copy.receptionPatientsTitle)}
            </AppText>
          ) : null}
          <Button
            label={copy.registerPatient}
            onPress={() => {
              if (register.isPending) return;
              register.mutate();
            }}
            loading={register.isPending}
            disabled={register.isPending || !clinicId}
          />

          <SectionHeader title={copy.receptionPatientsTitle} />
          {(patients.data ?? []).length === 0 ? <EmptyState title={copy.receptionEmptyPatients} /> : null}
          {(patients.data ?? []).map((patient) => (
            <Card key={`${patient.fullName}-${patient.lastAppointmentId}`} style={{ gap: 4 }}>
              <AppText variant="label">{patient.fullName}</AppText>
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                {patient.visitCount} visits · last {patient.lastStatus} with {patient.doctorName}
              </AppText>
            </Card>
          ))}
        </ScrollView>
      </AuthScaffold>
    </ReceptionShell>
  );
}
