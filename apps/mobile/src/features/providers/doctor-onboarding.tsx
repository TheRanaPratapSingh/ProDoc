import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, View } from 'react-native';

import { DoctorShell } from '@/components/layout/doctor-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { StatusBadge } from '@/components/ui/status-badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { pickFile } from '@/features/providers/pick-file';
import { useFormFieldErrors } from '@/hooks/use-form-field-errors';
import { useCopy, type Copy } from '@/i18n/copy';
import {
  doctorBasicsSchema,
  doctorProfessionalSchema,
  doctorRegistrationSchema,
  emailOptionalSchema,
  personNameSchema,
  yearsOfExperienceSchema,
} from '@/schemas/common';
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
import { notifyWarning } from '@/stores/feedback-store';
import { useAccountPermissions, useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { canCreateClinic } from '@/utils/admin-permissions';
import { problemMessage } from '@/utils/problem';
import { formatYearsExperience, parseYearsOfExperience } from '@/utils/years-of-experience';

const steps = ['basics', 'professional', 'registration', 'documents', 'clinics', 'review'] as const;

export function DoctorOnboarding() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const queryClient = useQueryClient();
  const account = useSessionStore((state) => state.account);
  const permissions = useAccountPermissions();
  const allowCreateClinic = canCreateClinic(permissions);
  const doctorQuery = useQuery({ queryKey: ['doctor', 'me'], queryFn: getMyDoctor });
  const catalogQuery = useQuery({ queryKey: ['specializations'], queryFn: getSpecializations });
  const clinicsQuery = useQuery({ queryKey: ['clinics', 'mine'], queryFn: getMyClinics });
  const [step, setStep] = useState(0);
  const [error, setError] = useState<string>();
  const [progress, setProgress] = useState<number>();
  const fieldErrors = useFormFieldErrors<
    | 'firstName'
    | 'lastName'
    | 'dateOfBirth'
    | 'email'
    | 'displayName'
    | 'yearsOfExperience'
    | 'qualification'
    | 'institution'
    | 'graduationYear'
    | 'number'
    | 'council'
    | 'state'
    | 'registeredOn'
    | 'expiresOn'
  >();
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
  const setField = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => {
    fieldErrors.clearField(key as Parameters<typeof fieldErrors.clearField>[0]);
    setForm((current) => ({ ...current, [key]: value }));
  };

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
      yearsOfExperience: profile.yearsOfExperience != null ? String(profile.yearsOfExperience) : '',
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
      setError(problemMessage(caught, copy.saveFailed));
    }
  };

  const profile = doctorQuery.data;
  const locked = profile?.status === 'Submitted' || profile?.status === 'UnderReview' || profile?.status === 'Suspended';
  const titles = [copy.stepBasics, copy.stepProfessional, copy.stepRegistration, copy.stepDocuments, copy.stepClinics, copy.stepReview];

  return (
    <DoctorShell active="profile">
    <AuthScaffold>
      <ScreenHeader
        title={copy.doctorTitle}
        subtitle={titles[step]}
        showBack
        onBack={() => (step === 0 ? router.back() : setStep((current) => current - 1))}
      />
      {profile ? <StatusBanner copy={copy} profile={profile} /> : null}
      {locked ? <AppText variant="muted">{copy.lockedNotice}</AppText> : null}
      <Card>
        {step === 0 ? (
          <View style={{ gap: spacing.md }}>
            <AppText variant="muted">{profile?.phoneNumber ?? account?.phoneNumber}</AppText>
            <TextField
              label={copy.firstName}
              required
              value={form.firstName}
              error={fieldErrors.errors.firstName}
              onChangeText={(firstName) => setField('firstName', firstName)}
              onBlur={() => fieldErrors.validateOnBlur('firstName', personNameSchema, form.firstName)}
            />
            <TextField
              label={copy.lastName}
              required
              value={form.lastName}
              error={fieldErrors.errors.lastName}
              onChangeText={(lastName) => setField('lastName', lastName)}
              onBlur={() => fieldErrors.validateOnBlur('lastName', personNameSchema, form.lastName)}
            />
            <TextField
              label={copy.dateOfBirth}
              placeholder="1990-01-15"
              value={form.dateOfBirth}
              error={fieldErrors.errors.dateOfBirth}
              onChangeText={(dateOfBirth) => setField('dateOfBirth', dateOfBirth)}
            />
            <AppText variant="label">{copy.gender}</AppText>
            <Segmented
              value={form.gender}
              options={[
                { value: 'female', label: copy.female },
                { value: 'male', label: copy.male },
                { value: 'other', label: copy.other },
                { value: 'undisclosed', label: copy.undisclosed },
              ]}
              onChange={(gender) => setField('gender', gender)}
            />
            <TextField
              label={copy.email}
              value={form.email}
              error={fieldErrors.errors.email}
              onChangeText={(email) => setField('email', email)}
              onBlur={() => fieldErrors.validateOnBlur('email', emailOptionalSchema, form.email)}
              autoCapitalize="none"
              keyboardType="email-address"
            />
          </View>
        ) : null}
        {step === 1 ? (
          <View style={{ gap: spacing.md }}>
            <TextField
              label={copy.displayName}
              required
              value={form.displayName}
              error={fieldErrors.errors.displayName}
              onChangeText={(displayName) => setField('displayName', displayName)}
              onBlur={() => fieldErrors.validateOnBlur('displayName', personNameSchema, form.displayName)}
            />
            <TextField label={copy.biography} value={form.biography} onChangeText={(biography) => setField('biography', biography)} multiline />
            <TextField
              label={copy.experience}
              required
              value={form.yearsOfExperience}
              error={fieldErrors.errors.yearsOfExperience}
              onChangeText={(yearsOfExperience) =>
                setField('yearsOfExperience', yearsOfExperience.replace(/[^\d]/g, '').slice(0, 2))
              }
              onBlur={() =>
                fieldErrors.validateOnBlur('yearsOfExperience', yearsOfExperienceSchema, form.yearsOfExperience)
              }
              keyboardType="number-pad"
            />
            {(catalogQuery.data ?? []).map((item) => {
              const selected = form.selected.includes(item.id);
              return (
                <Pressable
                  key={item.id}
                  accessibilityRole="button"
                  onPress={() => {
                    const selectedIds = selected ? form.selected.filter((id) => id !== item.id) : [...form.selected, item.id];
                    setForm((current) => ({
                      ...current,
                      selected: selectedIds,
                      primary: selectedIds.includes(current.primary) ? current.primary : selectedIds[0] ?? '',
                    }));
                  }}>
                  <AppText>
                    {selected ? '● ' : '○ '}
                    {item.name}
                    {form.primary === item.id ? ' · primary' : ''}
                  </AppText>
                </Pressable>
              );
            })}
            <TextField
              label={copy.qualification}
              value={form.qualification}
              error={fieldErrors.errors.qualification}
              onChangeText={(qualification) => setField('qualification', qualification)}
            />
            <TextField
              label={copy.institution}
              value={form.institution}
              error={fieldErrors.errors.institution}
              onChangeText={(institution) => setField('institution', institution)}
            />
            <TextField
              label={copy.graduationYear}
              value={form.graduationYear}
              error={fieldErrors.errors.graduationYear}
              onChangeText={(graduationYear) => setField('graduationYear', graduationYear)}
              keyboardType="number-pad"
            />
            {(profile?.qualifications ?? []).map((item) => (
              <AppText key={item.id} variant="muted">{item.name} · {item.institution} · {item.graduationYear}</AppText>
            ))}
          </View>
        ) : null}
        {step === 2 ? (
          <View style={{ gap: spacing.md }}>
            <AppText variant="muted">{copy.credentialNotice}</AppText>
            <TextField
              label={copy.registrationNumber}
              required
              value={form.number}
              error={fieldErrors.errors.number}
              onChangeText={(number) => setField('number', number)}
            />
            <TextField
              label={copy.council}
              required
              value={form.council}
              error={fieldErrors.errors.council}
              onChangeText={(council) => setField('council', council)}
            />
            <TextField
              label={copy.state}
              required
              value={form.state}
              error={fieldErrors.errors.state}
              onChangeText={(state) => setField('state', state)}
            />
            <TextField
              label={copy.registeredOn}
              required
              placeholder="2014-05-01"
              value={form.registeredOn}
              error={fieldErrors.errors.registeredOn}
              onChangeText={(registeredOn) => setField('registeredOn', registeredOn)}
            />
            <TextField
              label={copy.expiresOn}
              placeholder="2030-05-01"
              value={form.expiresOn}
              error={fieldErrors.errors.expiresOn}
              onChangeText={(expiresOn) => setField('expiresOn', expiresOn)}
            />
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
            {allowCreateClinic ? (
              <Button label={copy.createClinic} variant="secondary" onPress={() => router.push('/clinic' as Href)} />
            ) : null}
            <Button label={copy.skipClinic} variant="secondary" onPress={() => setStep(5)} />
          </View>
        ) : null}
        {step === 5 && profile ? (
          <View style={{ gap: spacing.sm }}>
            <AppText>{profile.displayName}</AppText>
            <AppText variant="muted">{profile.specializations.map((item) => item.name).join(', ')}</AppText>
            <AppText variant="muted">{formatYearsExperience(profile.yearsOfExperience, copy.yearsExp)}</AppText>
            <AppText variant="muted">{profile.biography}</AppText>
            <AppText variant="muted">{profile.registrations.find((item) => item.isCurrent)?.number}</AppText>
            {profile.rejectionReason ? <AppText variant="muted">{profile.rejectionReason}</AppText> : null}
          </View>
        ) : null}
        {error ? (
          <AppText variant="muted" style={{ color: theme.colors.error }}>
            {error}
          </AppText>
        ) : null}
        {step < 4 ? (
          <Button label={copy.saveAndContinue} disabled={locked} onPress={() => void saveStep()} />
        ) : null}
        {step === 5 ? (
          <Button label={copy.submitApplication} disabled={locked || !profile} onPress={() => void run(() => submitDoctor(), false)} />
        ) : null}
      </Card>
    </AuthScaffold>
    </DoctorShell>
  );

  async function saveStep() {
    if (step === 0) {
      const basics = fieldErrors.validateObject(doctorBasicsSchema, {
        firstName: form.firstName,
        lastName: form.lastName,
        dateOfBirth: form.dateOfBirth,
        email: form.email,
      });
      if (!basics.ok) {
        setError(basics.message);
        notifyWarning(basics.message);
        return;
      }
      await run(() => saveDoctorBasics({
        firstName: basics.data.firstName,
        lastName: basics.data.lastName,
        dateOfBirth: basics.data.dateOfBirth || '',
        gender: form.gender,
        email: basics.data.email || '',
        preferredLanguage: account?.preferredLanguage ?? 'en',
      }));
      return;
    }

    if (step === 1) {
      const professional = fieldErrors.validateObject(doctorProfessionalSchema, {
        displayName: form.displayName,
        yearsOfExperience: form.yearsOfExperience,
        qualification: form.qualification,
        institution: form.institution,
        graduationYear: form.graduationYear,
      });
      if (!professional.ok) {
        setError(professional.message);
        notifyWarning(professional.message);
        return;
      }
      const experience = parseYearsOfExperience(professional.data.yearsOfExperience);
      if (experience == null) {
        fieldErrors.setFieldError('yearsOfExperience', copy.yearsExperienceRequired);
        setError(copy.yearsExperienceRequired);
        notifyWarning(copy.yearsExperienceRequired);
        return;
      }
      await run(async () => {
        let next = await saveDoctorProfessional({
          displayName: professional.data.displayName,
          biography: form.biography,
          yearsOfExperience: experience,
          languages: ['en', 'hi'],
          expertise: [],
        });
        if (form.primary) {
          next = await setSpecializations(form.selected, form.primary);
        }
        if (professional.data.qualification && professional.data.graduationYear) {
          next = await addQualification({
            name: professional.data.qualification,
            institution: professional.data.institution,
            graduationYear: Number(professional.data.graduationYear),
          });
        }
        return next;
      });
      return;
    }

    if (step === 2) {
      const registration = fieldErrors.validateObject(doctorRegistrationSchema, {
        number: form.number,
        council: form.council,
        state: form.state,
        registeredOn: form.registeredOn,
        expiresOn: form.expiresOn,
      });
      if (!registration.ok) {
        setError(registration.message);
        notifyWarning(registration.message);
        return;
      }
      await run(() =>
        saveRegistration({
          number: registration.data.number,
          council: registration.data.council,
          state: registration.data.state,
          registrationType: 'permanent',
          registeredOn: registration.data.registeredOn,
          expiresOn: registration.data.expiresOn || null,
        }),
      );
      return;
    }

    if (step === 3) {
      setStep(4);
    }
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

  const badge =
    profile.verificationBadge === 'Verified'
      ? copy.doctorVerifiedBadge
      : profile.verificationBadge === 'Rejected'
        ? copy.doctorRejectedBadge
        : copy.doctorPendingBadge;
  const tone =
    profile.verificationBadge === 'Verified' ? 'success' : profile.verificationBadge === 'Rejected' ? 'error' : 'warning';

  return (
    <View style={{ gap: spacing.xs }}>
      <AppText variant="label">{copy.verificationStatus}</AppText>
      <StatusBadge label={badge} tone={tone} />
      <AppText style={{ color: profile.isPubliclyBookable ? theme.colors.success : theme.colors.text }}>
        {labels[profile.status] ?? profile.status}
      </AppText>
      <AppText variant="muted">
        {copy.doctorProfileCompletion}: {profile.profileCompletionPercent ?? 0}%
      </AppText>
      <AppText variant="muted">{copy.applicationReference}: {profile.reference}</AppText>
    </View>
  );
}
