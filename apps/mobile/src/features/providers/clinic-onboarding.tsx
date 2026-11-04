import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { pickFile } from '@/features/providers/pick-file';
import { useCopy } from '@/i18n/copy';
import {
  addBranch,
  createClinic,
  getMyClinics,
  saveClinic,
  submitClinic,
  uploadClinicDocument,
  type ClinicProfile,
} from '@/services/api/provider-api';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function ClinicOnboarding() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const clinicsQuery = useQuery({ queryKey: ['clinics', 'mine'], queryFn: getMyClinics });
  const [selected, setSelected] = useState<string>();
  const [error, setError] = useState<string>();
  const [form, setForm] = useState({
    name: '',
    type: 'Clinic',
    phoneNumber: '',
    registrationNumber: '',
    establishmentYear: '',
    branchName: 'Main',
    addressLine: '',
    city: '',
    state: '',
    postalCode: '',
  });
  const clinic = clinicsQuery.data?.find((item) => item.id === selected) ?? clinicsQuery.data?.[0];

  const create = useMutation({
    mutationFn: createClinic,
    onSuccess: async (created) => {
      setSelected(created.id);
      await queryClient.invalidateQueries({ queryKey: ['clinics', 'mine'] });
    },
    onError: (caught) => setError(problemMessage(caught, copy.save)),
  });

  const save = async (action: () => Promise<ClinicProfile>) => {
    setError(undefined);
    try {
      const next = await action();
      setSelected(next.id);
      await queryClient.invalidateQueries({ queryKey: ['clinics', 'mine'] });
    } catch (caught) {
      setError(problemMessage(caught, copy.save));
    }
  };

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="secondary" onPress={() => router.back()} />
      <AppText variant="title">{copy.clinicTitle}</AppText>
      <Card>
        {(clinicsQuery.data ?? []).map((item) => (
          <Button key={item.id} label={`${item.name || item.reference} · ${item.status}`} variant="secondary" onPress={() => setSelected(item.id)} />
        ))}
        <Button label={copy.createClinic} loading={create.isPending} onPress={() => create.mutate()} />
        {clinic ? (
          <View style={{ gap: spacing.md }}>
            <AppText variant="muted">{copy.applicationReference}: {clinic.reference}</AppText>
            <TextField label={copy.clinicName} value={form.name} onChangeText={(name) => setForm({ ...form, name })} />
            <Button label={copy.individualPractice} variant={form.type === 'IndividualPractice' ? 'primary' : 'secondary'} onPress={() => setForm({ ...form, type: 'IndividualPractice' })} />
            <Button label={copy.clinicTypeClinic} variant={form.type === 'Clinic' ? 'primary' : 'secondary'} onPress={() => setForm({ ...form, type: 'Clinic' })} />
            <Button label={copy.hospital} variant={form.type === 'Hospital' ? 'primary' : 'secondary'} onPress={() => setForm({ ...form, type: 'Hospital' })} />
            <TextField label={copy.contactNumber} value={form.phoneNumber} onChangeText={(phoneNumber) => setForm({ ...form, phoneNumber })} keyboardType="phone-pad" />
            <TextField label={copy.registrationNumber} value={form.registrationNumber} onChangeText={(registrationNumber) => setForm({ ...form, registrationNumber })} />
            <TextField label={copy.establishmentYear} value={form.establishmentYear} onChangeText={(establishmentYear) => setForm({ ...form, establishmentYear })} keyboardType="number-pad" />
            <Button
              label={copy.save}
              onPress={() => void save(() => saveClinic(clinic.id, {
                name: form.name,
                type: form.type,
                phoneNumber: form.phoneNumber,
                registrationNumber: form.registrationNumber,
                establishmentYear: Number(form.establishmentYear),
              }))}
            />
            <TextField label={copy.branchName} value={form.branchName} onChangeText={(branchName) => setForm({ ...form, branchName })} />
            <TextField label={copy.address} value={form.addressLine} onChangeText={(addressLine) => setForm({ ...form, addressLine })} />
            <TextField label={copy.city} value={form.city} onChangeText={(city) => setForm({ ...form, city })} />
            <TextField label={copy.state} value={form.state} onChangeText={(state) => setForm({ ...form, state })} />
            <TextField label={copy.postalCode} value={form.postalCode} onChangeText={(postalCode) => setForm({ ...form, postalCode })} keyboardType="number-pad" />
            <Button
              label={copy.addBranch}
              variant="secondary"
              onPress={() => void save(() => addBranch(clinic.id, {
                name: form.branchName,
                addressLine: form.addressLine,
                city: form.city,
                state: form.state,
                postalCode: form.postalCode,
                isPrimary: clinic.branches.length === 0,
              }))}
            />
            {clinic.branches.map((branch) => (
              <AppText key={branch.id} variant="muted">{branch.name} · {branch.city} · {branch.postalCode}</AppText>
            ))}
            {form.type !== 'IndividualPractice' ? (
              <Button
                label={copy.uploadCertificate}
                variant="secondary"
                onPress={() => void uploadRegistration(clinic.id)}
              />
            ) : null}
            <Button label={copy.submitApplication} onPress={() => void save(() => submitClinic(clinic.id))} />
            {clinic.rejectionReason ? <AppText variant="muted">{clinic.rejectionReason}</AppText> : null}
          </View>
        ) : null}
        {error ? <AppText variant="muted">{error}</AppText> : null}
      </Card>
    </AuthScaffold>
  );

  async function uploadRegistration(clinicId: string) {
    const file = await pickFile('application/pdf,image/png,image/jpeg');
    if (!file) {
      return;
    }

    await save(() => uploadClinicDocument(clinicId, 'ClinicRegistration', file));
  }
}
