import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { pickFile } from '@/features/providers/pick-file';
import { useCopy, type Copy } from '@/i18n/copy';
import {
  addQualification,
  getMyClinics,
  getMyDoctor,
  getSpecializations,
  linkClinic,
  saveDoctorBasics,
  saveDoctorProfessional,
  saveRegistration,
  setSpecializations,
  submitDoctor,
  uploadDoctorDocument,
  type DoctorProfile,
} from '@/services/api/provider-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

const steps = ['basics', 'professional', 'registration', 'documents', 'clinics', 'review'] as const;

export function DoctorOnboarding() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const account = useSessionStore((state) => state.account);
  const doctorQuery = useQuery({ queryKey: ['doctor', 'me'], queryFn: getMyDoctor });
  const catalogQuery = useQuery({ queryKey: ['specializations'], queryFn: getSpecializations });
  const clinicsQuery = useQuery({ queryKey: ['clinics', 'mine'], queryFn: getMyClinics });
  const [step, setStep] = useState(0);
  const [error, setError] = useState<string>();
  const [progress, setProgress] = useState<number>();
  const [form, setForm] = useState({
    firstName: '',
    lastName: '',
    dateOfBirth: '',
    gender: 'female',
    email: '',
    displayName: '',
    biography: '',
    yearsOfExperience: '',
    qualification: '',
    institution: '',
    graduationYear: '',
    number: '',
    council: '',
    state: '',
    registeredOn: '',
    expiresOn: '',
    selected: [] as string[],
    primary: '',
  });

  useEffect(() => {
    const profile = doctorQuery.data;
    if (!profile) {
      if (doctorQuery.isSuccess && account?.fullName) {
        const [first, ...rest] = account.fullName.split(' ');
        setForm((current) => ({
          ...current,
          firstName: current.firstName || (first ?? ''),
          lastName: current.lastName || rest.join(' '),
        }));
      }
      return;
    }

    const registration = profile.registrations.find((item) => item.isCurrent);
    setForm((current) => ({
      ...current,
      firstName: profile.firstName ?? '',
      lastName: profile.lastName ?? '',
      dateOfBirth: profile.dateOfBirth ?? '',
      gender: profile.gender ?? 'female',
      email: profile.email ?? '',
      displayName: profile.displayName ?? '',
      biography: profile.biography ?? '',
      yearsOfExperience: profile.yearsOfExperience?.toString() ?? '',
      number: registration?.number ?? '',
      council: registration?.council ?? '',
      state: registration?.state ?? '',
      registeredOn: registration?.registeredOn ?? '',
      expiresOn: registration?.expiresOn ?? '',
      selected: profile.specializations.map((item) => item.id),
      primary: profile.specializations.find((item) => item.isPrimary)?.id ?? '',
    }));
  }, [account?.fullName, doctorQuery.data, doctorQuery.isSuccess]);

  const remember = (profile: DoctorProfile) => {
    queryClient.setQueryData(['doctor', 'me'], profile);
  };

  const run = async (action: () => Promise<DoctorProfile>, advance = true) => {
    setError(undefined);
    try {
      remember(await action());
      if (advance) {
        setStep((current) => Math.min(current + 1, steps.length - 1));
      }
    } catch (caught) {
      setError(problemMessage(caught, copy.save));
    }
  };

  const profile = doctorQuery.data;
  const locked = profile?.status === 'Submitted' || profile?.status === 'UnderReview' || profile?.status === 'Suspended';
  const titles = [copy.stepBasics, copy.stepProfessional, copy.stepRegistration, copy.stepDocuments, copy.stepClinics, copy.stepReview];

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="secondary" onPress={() => (step === 0 ? router.back() : setStep((current) => current - 1))} />
      <AppText variant="title">{copy.doctorTitle}</AppText>
      <AppText variant="muted">{titles[step]}</AppText>
      {profile ? <StatusBanner copy={copy} profile={profile} /> : null}
      {locked ? <AppText variant="muted">{copy.lockedNotice}</AppText> : null}
      <Card>
        {step === 0 ? (
          <View style={{ gap: spacing.md }}>
            <AppText variant="muted">{profile?.phoneNumber ?? account?.phoneNumber}</AppText>
            <TextField label={copy.firstName} value={form.firstName} onChangeText={(firstName) => setForm({ ...form, firstName })} />
            <TextField label={copy.lastName} value={form.lastName} onChangeText={(lastName) => setForm({ ...form, lastName })} />
            <TextField label={copy.dateOfBirth} placeholder="1990-01-15" value={form.dateOfBirth} onChangeText={(dateOfBirth) => setForm({ ...form, dateOfBirth })} />
            <AppText variant="label">{copy.gender}</AppText>
            <Segmented
              value={form.gender}
              options={[
                { value: 'female', label: copy.female },
                { value: 'male', label: copy.male },
                { value: 'other', label: copy.other },
                { value: 'undisclosed', label: copy.undisclosed },
              ]}
              onChange={(gender) => setForm({ ...form, gender })}
            />
            <TextField label={copy.email} value={form.email} onChangeText={(email) => setForm({ ...form, email })} autoCapitalize="none" />
          </View>
        ) : null}
        {step === 1 ? (
          <View style={{ gap: spacing.md }}>
            <TextField label={copy.displayName} value={form.displayName} onChangeText={(displayName) => setForm({ ...form, displayName })} />
            <TextField label={copy.biography} value={form.biography} onChangeText={(biography) => setForm({ ...form, biography })} multiline />
            <TextField label={copy.experience} value={form.yearsOfExperience} onChangeText={(yearsOfExperience) => setForm({ ...form, yearsOfExperience })} keyboardType="number-pad" />
            {(catalogQuery.data ?? []).map((item) => {
              const selected = form.selected.includes(item.id);
              return (
                <Pressable
                  key={item.id}
                  accessibilityRole="button"
                  onPress={() => {
                    const selectedIds = selected ? form.selected.filter((id) => id !== item.id) : [...form.selected, item.id];
                    setForm({ ...form, selected: selectedIds, primary: selectedIds.includes(form.primary) ? form.primary : selectedIds[0] ?? '' });
                  }}>
                  <AppText>
                    {selected ? '● ' : '○ '}
                    {item.name}
                    {form.primary === item.id ? ' · primary' : ''}
                  </AppText>
                </Pressable>
              );
            })}
            <TextField label={copy.qualification} value={form.qualification} onChangeText={(qualification) => setForm({ ...form, qualification })} />
            <TextField label={copy.institution} value={form.institution} onChangeText={(institution) => setForm({ ...form, institution })} />
            <TextField label={copy.graduationYear} value={form.graduationYear} onChangeText={(graduationYear) => setForm({ ...form, graduationYear })} keyboardType="number-pad" />
            {(profile?.qualifications ?? []).map((item) => (
              <AppText key={item.id} variant="muted">{item.name} · {item.institution} · {item.graduationYear}</AppText>
            ))}
          </View>
        ) : null}
        {step === 2 ? (
          <View style={{ gap: spacing.md }}>
            <AppText variant="muted">{copy.credentialNotice}</AppText>
            <TextField label={copy.registrationNumber} value={form.number} onChangeText={(number) => setForm({ ...form, number })} />
            <TextField label={copy.council} value={form.council} onChangeText={(council) => setForm({ ...form, council })} />
            <TextField label={copy.state} value={form.state} onChangeText={(state) => setForm({ ...form, state })} />
            <TextField label={copy.registeredOn} placeholder="2014-05-01" value={form.registeredOn} onChangeText={(registeredOn) => setForm({ ...form, registeredOn })} />
            <TextField label={copy.expiresOn} placeholder="2030-05-01" value={form.expiresOn} onChangeText={(expiresOn) => setForm({ ...form, expiresOn })} />
          </View>
        ) : null}
        {step === 3 ? (
          <View style={{ gap: spacing.md }}>
            <AppText variant="muted">{copy.scanNotice}</AppText>
            <Button
              label={progress ? `${copy.uploadPhoto} ${progress}%` : copy.uploadPhoto}
              variant="secondary"
              onPress={() => void upload('ProfilePhoto', 'image/png,image/jpeg,image/webp')}
            />
            <Button label={copy.uploadCertificate} variant="secondary" onPress={() => void upload('RegistrationCertificate', 'application/pdf,image/png,image/jpeg')} />
            {(profile?.documents ?? []).filter((item) => item.isCurrent).map((item) => (
              <AppText key={item.id} variant="muted">{item.category} · v{item.version} · {item.fileName}</AppText>
            ))}
          </View>
        ) : null}
        {step === 4 ? (
          <View style={{ gap: spacing.md }}>
            {(clinicsQuery.data ?? []).map((clinic) => (
              <Button
                key={clinic.id}
                label={clinic.name || clinic.reference}
                variant="secondary"
                onPress={() => void run(() => linkClinic(clinic.id, 'Independent'), false)}
              />
            ))}
            {(profile?.affiliations ?? []).map((item) => (
              <AppText key={item.id} variant="muted">{item.clinicName} · {item.clinicStatus}</AppText>
            ))}
            <Button label={copy.createClinic} variant="secondary" onPress={() => router.push('/clinic' as Href)} />
            <Button label={copy.skipClinic} variant="secondary" onPress={() => setStep(5)} />
          </View>
        ) : null}
        {step === 5 && profile ? (
          <View style={{ gap: spacing.sm }}>
            <AppText>{profile.displayName}</AppText>
            <AppText variant="muted">{profile.specializations.map((item) => item.name).join(', ')}</AppText>
            <AppText variant="muted">{profile.yearsOfExperience ?? 0}</AppText>
            <AppText variant="muted">{profile.biography}</AppText>
            <AppText variant="muted">{profile.registrations.find((item) => item.isCurrent)?.number}</AppText>
            {profile.rejectionReason ? <AppText variant="muted">{profile.rejectionReason}</AppText> : null}
          </View>
        ) : null}
        {error ? <AppText variant="muted">{error}</AppText> : null}
        {step < 4 ? (
          <Button label={copy.saveAndContinue} disabled={locked} onPress={() => void saveStep()} />
        ) : null}
        {step === 5 ? (
          <Button label={copy.submitApplication} disabled={locked || !profile} onPress={() => void run(() => submitDoctor(), false)} />
        ) : null}
      </Card>
    </AuthScaffold>
  );

  async function saveStep() {
    if (step === 0) {
      await run(() => saveDoctorBasics({
        firstName: form.firstName,
        lastName: form.lastName,
        dateOfBirth: form.dateOfBirth,
        gender: form.gender,
        email: form.email,
        preferredLanguage: account?.preferredLanguage ?? 'en',
      }));
      return;
    }

    if (step === 1) {
      await run(async () => {
        let next = await saveDoctorProfessional({
          displayName: form.displayName,
          biography: form.biography,
          yearsOfExperience: Number(form.yearsOfExperience),
          languages: ['en', 'hi'],
          expertise: [],
        });
        if (form.primary) {
          next = await setSpecializations(form.selected, form.primary);
        }
        if (form.qualification && form.graduationYear) {
          next = await addQualification({
            name: form.qualification,
            institution: form.institution,
            graduationYear: Number(form.graduationYear),
          });
        }
        return next;
      });
      return;
    }

    await run(() => saveRegistration({
      number: form.number,
      council: form.council,
      state: form.state,
      registrationType: 'permanent',
      registeredOn: form.registeredOn,
      expiresOn: form.expiresOn || null,
    }));
  }

  async function upload(category: string, accept: string) {
    const file = await pickFile(accept);
    if (!file) {
      setError(copy.uploadPhoto);
      return;
    }

    setProgress(0);
    await run(() => uploadDoctorDocument(category, file, setProgress), false);
    setProgress(undefined);
  }
}

function StatusBanner({ copy, profile }: { copy: Copy; profile: DoctorProfile }) {
  const theme = useProdocTheme();
  const labels: Record<string, string> = {
    Draft: copy.statusDraft,
    Submitted: copy.statusSubmitted,
    UnderReview: copy.statusUnderReview,
    AdditionalInformationRequired: copy.statusAdditionalInformationRequired,
    Approved: copy.statusApproved,
    Rejected: copy.statusRejected,
    Suspended: copy.statusSuspended,
    Expired: copy.statusExpired,
  };

  return (
    <View style={{ gap: spacing.xs }}>
      <AppText variant="label">{copy.verificationStatus}</AppText>
      <AppText style={{ color: profile.isPubliclyBookable ? theme.colors.success : theme.colors.text }}>{labels[profile.status] ?? profile.status}</AppText>
      <AppText variant="muted">{copy.applicationReference}: {profile.reference}</AppText>
    </View>
  );
}
