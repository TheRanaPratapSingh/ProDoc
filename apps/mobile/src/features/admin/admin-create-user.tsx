import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { Pressable, View } from 'react-native';

import { AdminFeedbackBanner } from '@/components/admin/admin-feedback';
import { AdminFormSection } from '@/components/admin/admin-form-section';
import { AdminPanel } from '@/components/admin/admin-shell';
import { Button } from '@/components/ui/button';
import { OtpVerificationPanel } from '@/components/ui/otp-verification-panel';
import { PhoneInput } from '@/components/ui/phone-input';
import { SearchableMultiSelect } from '@/components/ui/searchable-multi-select';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { createUserFormSchema, otpCodeSchema } from '@/schemas/common';
import {
  cancelStaffInvitation,
  inviteStaff,
  listAdminClinics,
  listStaffInvitations,
  resendStaffInvitation,
  searchAdminUsers,
  verifyStaffInvitation,
  type StaffInvitation,
} from '@/services/api/admin-api';
import { getSpecializations } from '@/services/api/provider-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { normalizeIndianMobile } from '@/utils/phone';
import { problemMessage } from '@/utils/problem';
import { parseYearsOfExperience } from '@/utils/years-of-experience';

const ROLES = [
  { id: 'Doctor', label: 'Doctor' },
  { id: 'Receptionist', label: 'Receptionist' },
  { id: 'ClinicStaff', label: 'Clinic Staff' },
] as const;

type RoleId = (typeof ROLES)[number]['id'];

type FieldErrors = Partial<
  Record<
    | 'fullName'
    | 'phone'
    | 'email'
    | 'specializationId'
    | 'yearsOfExperience'
    | 'registrationNumber'
    | 'clinicIds'
    | 'doctorIds'
    | 'designation',
    string
  >
>;

function Chip({
  label,
  selected,
  onPress,
}: {
  label: string;
  selected: boolean;
  onPress: () => void;
}) {
  const theme = useProdocTheme();
  return (
    <Pressable
      onPress={onPress}
      style={{
        paddingHorizontal: 10,
        paddingVertical: 6,
        borderRadius: 999,
        borderWidth: 1,
        borderColor: selected ? theme.colors.primary : theme.colors.border,
        backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
      }}>
      <AppText variant="label" style={{ color: selected ? theme.colors.primary : theme.colors.text, fontSize: 12 }}>
        {label}
      </AppText>
    </Pressable>
  );
}

export function AdminCreateUserPanel({ onActivated }: { onActivated?: () => void }) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const [role, setRole] = useState<RoleId>('Doctor');
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [specializationId, setSpecializationId] = useState('');
  const [yearsOfExperience, setYearsOfExperience] = useState('');
  const [qualifications, setQualifications] = useState('');
  const [registrationNumber, setRegistrationNumber] = useState('');
  const [designation, setDesignation] = useState('');
  const [clinicIds, setClinicIds] = useState<string[]>([]);
  const [doctorIds, setDoctorIds] = useState<string[]>([]);
  const [pendingInvite, setPendingInvite] = useState<StaffInvitation | null>(null);
  const [feedback, setFeedback] = useState<{ message: string; tone: 'success' | 'error' } | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [verifyError, setVerifyError] = useState<string | null>(null);

  const clinics = useQuery({
    queryKey: ['admin', 'clinics', 'active'],
    queryFn: () => listAdminClinics(),
  });
  const specializations = useQuery({ queryKey: ['specializations'], queryFn: getSpecializations });
  const invitations = useQuery({
    queryKey: ['admin', 'invitations', 'Pending'],
    queryFn: () => listStaffInvitations('Pending'),
  });
  const doctors = useQuery({
    queryKey: ['admin', 'users', 'doctors-for-invite'],
    queryFn: () => searchAdminUsers({ role: 'Doctor', suspended: false }),
    enabled: role === 'Receptionist',
  });

  const clinicOptions = useMemo(
    () =>
      (clinics.data ?? []).map((clinic) => ({
        id: clinic.id,
        label: clinic.name ?? clinic.reference,
        subtitle: [clinic.city, clinic.state].filter(Boolean).join(', ') || clinic.reference,
      })),
    [clinics.data],
  );

  const specializationOptions = useMemo(
    () =>
      (specializations.data ?? []).map((item) => ({
        id: item.id,
        label: item.name,
      })),
    [specializations.data],
  );

  const doctorOptions = useMemo(() => {
    const selected = new Set(clinicIds);
    return (doctors.data ?? [])
      .filter((item) => {
        if (!item.doctorId) return false;
        if (selected.size === 0) return true;
        return item.ownedClinicId ? selected.has(item.ownedClinicId) : true;
      })
      .map((item) => ({
        id: item.doctorId!,
        label: item.fullName || item.phoneNumber,
        subtitle: item.phoneNumber,
      }));
  }, [clinicIds, doctors.data]);

  const resetForm = () => {
    setFullName('');
    setPhone('');
    setEmail('');
    setQualifications('');
    setRegistrationNumber('');
    setYearsOfExperience('');
    setDesignation('');
    setSpecializationId('');
    setClinicIds([]);
    setDoctorIds([]);
    setFieldErrors({});
  };

  const invite = useMutation({
    mutationFn: async () => {
      const errors: FieldErrors = {};
      const parsed = createUserFormSchema.safeParse({
        fullName,
        phoneNumber: phone,
        email,
      });
      if (!parsed.success) {
        for (const issue of parsed.error.issues) {
          const key = issue.path[0];
          if (key === 'fullName') errors.fullName = issue.message;
          if (key === 'phoneNumber') errors.phone = issue.message;
          if (key === 'email') errors.email = issue.message;
        }
      }

      const phoneNumber = parsed.success ? normalizeIndianMobile(parsed.data.phoneNumber) : null;
      if (parsed.success && !phoneNumber) errors.phone = 'Please enter a valid 10-digit mobile number.';
      if (clinicIds.length === 0) errors.clinicIds = 'Assign at least one clinic.';
      if (role === 'Doctor' && !specializationId) errors.specializationId = 'Choose a specialization.';
      const experienceYears = role === 'Doctor' ? parseYearsOfExperience(yearsOfExperience) : null;
      if (role === 'Doctor' && experienceYears == null) {
        errors.yearsOfExperience = copy.yearsExperienceRequired;
      }
      if (role === 'Doctor' && !registrationNumber.trim()) {
        errors.registrationNumber = 'Enter the medical registration number so the doctor can appear in patient search.';
      }
      if (role === 'Receptionist' && doctorIds.length === 0) errors.doctorIds = 'Assign at least one doctor.';
      if (role === 'ClinicStaff' && !designation.trim()) errors.designation = 'Enter a staff designation.';

      if (Object.keys(errors).length > 0) {
        setFieldErrors(errors);
        throw new Error(Object.values(errors)[0] ?? 'Check the form and try again.');
      }

      setFieldErrors({});
      return inviteStaff({
        fullName: parsed.data!.fullName,
        phoneNumber: phoneNumber!,
        email: parsed.data!.email.trim() || null,
        roleName: role,
        clinicId: clinicIds[0],
        clinicIds,
        specializationId: role === 'Doctor' ? specializationId : null,
        yearsOfExperience: role === 'Doctor' ? experienceYears : null,
        qualifications: role === 'Doctor' ? qualifications.trim() || null : null,
        registrationNumber: role === 'Doctor' ? registrationNumber.trim() || null : null,
        designation: role === 'ClinicStaff' ? designation.trim() : null,
        doctorIds: role === 'Receptionist' ? doctorIds : undefined,
      });
    },
    onSuccess: async (invitation) => {
      setFeedback({ message: copy.inviteSent, tone: 'success' });
      setVerifyError(null);
      setPendingInvite(invitation);
      await queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinics'] });
    },
    onError: (error) => {
      setFeedback({ message: problemMessage(error, copy.phoneRequestFailed), tone: 'error' });
    },
  });

  const verify = useMutation({
    mutationFn: (code: string) => {
      if (!pendingInvite) throw new Error('No pending invitation.');
      const parsed = otpCodeSchema.safeParse(code);
      if (!parsed.success) {
        throw new Error(parsed.error.issues[0]?.message ?? 'Enter the 6-digit code.');
      }
      return verifyStaffInvitation(pendingInvite.id, parsed.data);
    },
    onSuccess: async (activation) => {
      const isDoctor = activation.roleName === 'Doctor';
      const discoverable = Boolean(activation.isPubliclyDiscoverable);
      setFeedback({
        message: isDoctor
          ? discoverable
            ? copy.inviteVerifiedDoctor
            : copy.inviteVerifiedDoctorPending
          : copy.inviteVerified,
        tone: isDoctor && !discoverable ? 'error' : 'success',
      });
      setPendingInvite(null);
      setVerifyError(null);
      resetForm();
      await queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
      await queryClient.invalidateQueries({ queryKey: ['doctors'] });
      await queryClient.invalidateQueries({ queryKey: ['discovery'] });
      onActivated?.();
    },
    onError: (error) => {
      setVerifyError(problemMessage(error, copy.otpExpired));
    },
  });

  const resend = useMutation({
    mutationFn: (invitationId: string) => resendStaffInvitation(invitationId),
    onSuccess: async (invitation) => {
      setFeedback({ message: copy.inviteResend, tone: 'success' });
      setVerifyError(null);
      if (pendingInvite?.id === invitation.id) {
        setPendingInvite(invitation);
      }
      await queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
    },
    onError: (error) => setFeedback({ message: problemMessage(error, copy.inviteResend), tone: 'error' }),
  });

  const cancel = useMutation({
    mutationFn: cancelStaffInvitation,
    onSuccess: async () => {
      setFeedback({ message: copy.inviteCancel, tone: 'success' });
      setPendingInvite(null);
      setVerifyError(null);
      await queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
    },
    onError: (error) => setFeedback({ message: problemMessage(error, copy.inviteCancel), tone: 'error' }),
  });

  if (pendingInvite) {
    return (
      <View style={{ gap: spacing.lg, maxWidth: '100%' }}>
        <AdminFeedbackBanner
          message={feedback?.message}
          tone={feedback?.tone ?? 'info'}
          onDismiss={() => setFeedback(null)}
        />
        <AdminPanel title={copy.inviteVerifyTitle} description={`${pendingInvite.fullName} · ${pendingInvite.roleName}`}>
          <OtpVerificationPanel
            title={copy.inviteVerifyTitle}
            phoneNumber={pendingInvite.phoneNumber}
            expiresAtUtc={pendingInvite.otpExpiresAtUtc}
            developmentCode={pendingInvite.developmentCode}
            verifying={verify.isPending}
            resending={resend.isPending}
            error={verifyError}
            onVerify={(code) => {
              setVerifyError(null);
              verify.mutate(code);
            }}
            onResend={() => resend.mutate(pendingInvite.id)}
            onCancel={() => {
              setPendingInvite(null);
              setVerifyError(null);
            }}
          />
        </AdminPanel>
      </View>
    );
  }

  return (
    <View style={{ gap: spacing.lg, maxWidth: '100%' }}>
      <AdminFeedbackBanner
        message={feedback?.message}
        tone={feedback?.tone ?? 'info'}
        onDismiss={() => setFeedback(null)}
      />

      <View style={{ gap: spacing.md }}>
        <View style={{ gap: 2 }}>
          <AppText variant="label" style={{ fontSize: 16, color: theme.colors.text }}>
            {copy.adminCreateUser}
          </AppText>
          <AppText variant="muted" style={{ fontSize: 13, lineHeight: 18 }}>
            {copy.adminCreateUserBody}
          </AppText>
        </View>

        <AdminFormSection title={copy.inviteRole}>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            {ROLES.map((item) => (
              <Chip
                key={item.id}
                label={item.label}
                selected={role === item.id}
                onPress={() => {
                  setRole(item.id);
                  setDoctorIds([]);
                  setFieldErrors({});
                }}
              />
            ))}
          </View>
        </AdminFormSection>

        <AdminFormSection title={copy.adminSectionPersonalDetails}>
          <TextField
            label={copy.inviteFullName}
            value={fullName}
            onChangeText={(value) => {
              setFullName(value);
              setFieldErrors((prev) => ({ ...prev, fullName: undefined }));
            }}
            error={fieldErrors.fullName}
          />
          <PhoneInput
            label={copy.phoneLabel}
            value={phone}
            onChangeText={(value) => {
              setPhone(value);
              setFieldErrors((prev) => ({ ...prev, phone: undefined }));
            }}
            placeholder={copy.phonePlaceholder}
            error={fieldErrors.phone}
          />
          <TextField
            label={copy.inviteEmail}
            value={email}
            onChangeText={(value) => {
              setEmail(value);
              setFieldErrors((prev) => ({ ...prev, email: undefined }));
            }}
            keyboardType="email-address"
            autoCapitalize="none"
            error={fieldErrors.email}
          />
        </AdminFormSection>

        {role === 'Doctor' ? (
          <AdminFormSection
            title={copy.adminSectionProfessionalDetails}
            description={copy.adminSectionConsultationNote}>
            <SearchableMultiSelect
              label={copy.specialization}
              options={specializationOptions}
              selectedIds={specializationId ? [specializationId] : []}
              onChange={(ids) => {
                setSpecializationId(ids[0] ?? '');
                setFieldErrors((prev) => ({ ...prev, specializationId: undefined }));
              }}
              placeholder={copy.adminSpecializationSearch}
              multiple={false}
            />
            {fieldErrors.specializationId ? (
              <AppText variant="muted" style={{ color: theme.colors.error, fontSize: 12 }}>
                {fieldErrors.specializationId}
              </AppText>
            ) : null}
            <TextField
              label={copy.experience}
              value={yearsOfExperience}
              onChangeText={(value) => {
                setYearsOfExperience(value.replace(/[^\d]/g, '').slice(0, 2));
                setFieldErrors((prev) => ({ ...prev, yearsOfExperience: undefined }));
              }}
              keyboardType="number-pad"
              error={fieldErrors.yearsOfExperience}
            />
            <TextField label={copy.adminQualifications} value={qualifications} onChangeText={setQualifications} />
            <TextField
              label={copy.adminRegistrationNumber}
              value={registrationNumber}
              onChangeText={(value) => {
                setRegistrationNumber(value);
                setFieldErrors((prev) => ({ ...prev, registrationNumber: undefined }));
              }}
              error={fieldErrors.registrationNumber}
            />
          </AdminFormSection>
        ) : null}

        {role === 'ClinicStaff' ? (
          <AdminFormSection title={copy.adminSectionProfessionalDetails}>
            <TextField
              label={copy.adminDesignation}
              value={designation}
              onChangeText={(value) => {
                setDesignation(value);
                setFieldErrors((prev) => ({ ...prev, designation: undefined }));
              }}
              error={fieldErrors.designation}
            />
          </AdminFormSection>
        ) : null}

        <AdminFormSection title={copy.adminSectionClinicAssociation}>
          <SearchableMultiSelect
            label={copy.adminAssignClinics}
            options={clinicOptions}
            selectedIds={clinicIds}
            onChange={(ids) => {
              setClinicIds(ids);
              setFieldErrors((prev) => ({ ...prev, clinicIds: undefined }));
            }}
            placeholder={copy.clinicSearchPlaceholder}
            emptyMessage={copy.adminEmptyClinics}
            multiple
          />
          {fieldErrors.clinicIds ? (
            <AppText variant="muted" style={{ color: theme.colors.error, fontSize: 12 }}>
              {fieldErrors.clinicIds}
            </AppText>
          ) : null}

          {role === 'Receptionist' ? (
            <>
              <SearchableMultiSelect
                label={copy.adminAssignDoctors}
                options={doctorOptions}
                selectedIds={doctorIds}
                onChange={(ids) => {
                  setDoctorIds(ids);
                  setFieldErrors((prev) => ({ ...prev, doctorIds: undefined }));
                }}
                placeholder={copy.doctorSearchPlaceholder}
                emptyMessage={copy.adminEmptyDoctors}
                multiple
              />
              {fieldErrors.doctorIds ? (
                <AppText variant="muted" style={{ color: theme.colors.error, fontSize: 12 }}>
                  {fieldErrors.doctorIds}
                </AppText>
              ) : null}
            </>
          ) : null}
        </AdminFormSection>

        <Button
          label={copy.inviteSend}
          loading={invite.isPending}
          disabled={invite.isPending || clinicOptions.length === 0}
          onPress={() => {
            setFeedback(null);
            invite.mutate();
          }}
        />
      </View>

      <AdminPanel title={copy.invitePending}>
        {(invitations.data ?? []).map((item) => (
          <View
            key={item.id}
            style={{
              borderWidth: 1,
              borderColor: theme.colors.border,
              borderRadius: 14,
              padding: spacing.md,
              gap: spacing.sm,
            }}>
            <AppText>
              {item.fullName} · {item.phoneNumber}
            </AppText>
            <AppText variant="muted">
              {item.roleName} · {item.status} · expires {new Date(item.expiresAtUtc).toLocaleString()}
            </AppText>
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
              <Button
                label={copy.verify}
                variant="secondary"
                loading={resend.isPending}
                onPress={() => {
                  setVerifyError(null);
                  setFeedback(null);
                  resend.mutate(item.id, {
                    onSuccess: (invitation) => setPendingInvite(invitation),
                  });
                }}
              />
              <Button
                label={copy.inviteResend}
                variant="secondary"
                loading={resend.isPending}
                onPress={() => resend.mutate(item.id)}
              />
              <Button
                label={copy.inviteCancel}
                variant="danger"
                loading={cancel.isPending}
                onPress={() => cancel.mutate(item.id)}
              />
            </View>
          </View>
        ))}
        {(invitations.data?.length ?? 0) === 0 ? <AppText variant="muted">{copy.inviteEmpty}</AppText> : null}
      </AdminPanel>
    </View>
  );
}
