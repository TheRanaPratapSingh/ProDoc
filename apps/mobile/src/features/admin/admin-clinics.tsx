import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { Pressable, View } from 'react-native';

import { AdminFeedbackBanner } from '@/components/admin/admin-feedback';
import { AdminFormSection } from '@/components/admin/admin-form-section';
import { AdminKpiCard } from '@/components/admin/admin-kpi-card';
import { AdminPanel } from '@/components/admin/admin-shell';
import { Button } from '@/components/ui/button';
import { PhoneInput } from '@/components/ui/phone-input';
import { SearchableMultiSelect } from '@/components/ui/searchable-multi-select';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { cityOptionsForState, INDIA_STATE_OPTIONS } from '@/data/india-locations';
import { useCopy } from '@/i18n/copy';
import { clinicContactSchema } from '@/schemas/common';
import {
  activateAdminDoctorDiscovery,
  assignAdminClinicDoctor,
  createAdminClinic,
  getAdminClinic,
  listAdminClinics,
  removeAdminClinicDoctor,
  removeAdminClinicStaff,
  searchAdminUsers,
  updateAdminClinic,
} from '@/services/api/admin-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { normalizeIndianMobile } from '@/utils/phone';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type ClinicFieldErrors = Partial<
  Record<
    'name' | 'phone' | 'email' | 'addressLine' | 'city' | 'state' | 'postalCode',
    string
  >
>;

function TypeChip({
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
      <AppText variant="label" style={{ fontSize: 12, color: selected ? theme.colors.primary : theme.colors.text }}>
        {label}
      </AppText>
    </Pressable>
  );
}

export function AdminClinicsPanel() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const [query, setQuery] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [mode, setMode] = useState<'list' | 'create' | 'edit'>('list');
  const [feedback, setFeedback] = useState<{ message: string; tone: 'success' | 'error' } | null>(null);
  const [fieldErrors, setFieldErrors] = useState<ClinicFieldErrors>({});

  const [name, setName] = useState('');
  const [type, setType] = useState('Clinic');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [website, setWebsite] = useState('');
  const [description, setDescription] = useState('');
  const [registrationNumber, setRegistrationNumber] = useState('');
  const [branchName, setBranchName] = useState('');
  const [addressLine, setAddressLine] = useState('');
  const [addressLine2, setAddressLine2] = useState('');
  const [landmark, setLandmark] = useState('');
  const [locality, setLocality] = useState('');
  const [district, setDistrict] = useState('');
  const [city, setCity] = useState('');
  const [state, setState] = useState('');
  const [postalCode, setPostalCode] = useState('');
  const [latitude, setLatitude] = useState('');
  const [longitude, setLongitude] = useState('');
  const [googlePlaceId, setGooglePlaceId] = useState('');

  const clinics = useQuery({
    queryKey: ['admin', 'clinics', query],
    queryFn: () => listAdminClinics(query || undefined),
  });
  const details = useQuery({
    queryKey: ['admin', 'clinic', selectedId],
    queryFn: () => getAdminClinic(selectedId!),
    enabled: Boolean(selectedId),
  });
  const doctors = useQuery({
    queryKey: ['admin', 'users', 'doctors'],
    queryFn: () => searchAdminUsers({ role: 'Doctor', suspended: false }),
    enabled: Boolean(selectedId),
  });

  const cityOptions = useMemo(() => cityOptionsForState(state), [state]);

  const resetForm = () => {
    setName('');
    setType('Clinic');
    setPhone('');
    setEmail('');
    setWebsite('');
    setDescription('');
    setRegistrationNumber('');
    setBranchName('');
    setAddressLine('');
    setAddressLine2('');
    setLandmark('');
    setLocality('');
    setDistrict('');
    setCity('');
    setState('');
    setPostalCode('');
    setLatitude('');
    setLongitude('');
    setGooglePlaceId('');
    setFieldErrors({});
  };

  const fillFromDetails = () => {
    const clinic = details.data;
    if (!clinic) return;
    setName(clinic.name ?? '');
    setType(clinic.type ?? 'Clinic');
    setPhone(clinic.phoneNumber?.replace('+91', '') ?? '');
    setEmail(clinic.email ?? '');
    setWebsite(clinic.website ?? '');
    setDescription(clinic.description ?? '');
    setRegistrationNumber(clinic.registrationNumber ?? '');
    setFieldErrors({});
  };

  const validateCreate = (): ClinicFieldErrors => {
    const errors: ClinicFieldErrors = {};
    if (!name.trim()) errors.name = 'Enter the clinic name.';

    const contacts = clinicContactSchema.safeParse({ phoneNumber: phone, email });
    if (!contacts.success) {
      for (const issue of contacts.error.issues) {
        const key = issue.path[0];
        if (key === 'phoneNumber') errors.phone = issue.message;
        if (key === 'email') errors.email = issue.message;
      }
    } else if (!normalizeIndianMobile(contacts.data.phoneNumber)) {
      errors.phone = 'Enter a valid clinic mobile number.';
    }

    if (!addressLine.trim()) errors.addressLine = 'Enter the street address.';
    if (!state.trim()) errors.state = 'Select a state.';
    if (!city.trim()) errors.city = 'Select a city.';
    if (!/^\d{6}$/.test(postalCode.trim())) errors.postalCode = 'Enter a valid 6-digit PIN code.';
    return errors;
  };

  const create = useMutation({
    mutationFn: async () => {
      const errors = validateCreate();
      if (Object.keys(errors).length > 0) {
        setFieldErrors(errors);
        throw new Error(Object.values(errors)[0] ?? 'Check the form and try again.');
      }
      setFieldErrors({});
      const contacts = clinicContactSchema.parse({ phoneNumber: phone, email });
      const phoneNumber = normalizeIndianMobile(contacts.phoneNumber)!;
      const lat = latitude.trim() ? Number(latitude.trim()) : null;
      const lng = longitude.trim() ? Number(longitude.trim()) : null;
      if ((lat == null) !== (lng == null) || (lat != null && (Number.isNaN(lat) || lat < -90 || lat > 90))) {
        setFieldErrors({ ...errors, addressLine: 'Enter both latitude and longitude, or leave both empty.' });
        throw new Error('Enter both latitude and longitude, or leave both empty.');
      }
      if (lng != null && (Number.isNaN(lng) || lng < -180 || lng > 180)) {
        setFieldErrors({ ...errors, addressLine: 'Enter a valid longitude.' });
        throw new Error('Enter a valid longitude.');
      }
      return createAdminClinic({
        name: name.trim(),
        type,
        phoneNumber,
        email: contacts.email.trim() || null,
        website: website.trim() || null,
        description: description.trim() || null,
        registrationNumber: registrationNumber.trim() || null,
        branchName: branchName.trim() || null,
        addressLine: addressLine.trim(),
        addressLine2: addressLine2.trim() || null,
        landmark: landmark.trim() || null,
        locality: locality.trim() || null,
        city: city.trim(),
        district: district.trim() || null,
        state: state.trim(),
        postalCode: postalCode.trim(),
        country: 'India',
        latitude: lat,
        longitude: lng,
        googlePlaceId: googlePlaceId.trim() || null,
        branchPhoneNumber: phoneNumber,
      });
    },
    onSuccess: async (clinic) => {
      setFeedback({ message: copy.adminClinicCreated, tone: 'success' });
      resetForm();
      setMode('list');
      setSelectedId(clinic.id);
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinics'] });
    },
    onError: (error) => setFeedback({ message: problemMessage(error, copy.adminCreateClinic), tone: 'error' }),
  });

  const update = useMutation({
    mutationFn: async () => {
      if (!selectedId) throw new Error('Select a clinic.');
      const errors: ClinicFieldErrors = {};
      if (!name.trim()) errors.name = 'Enter the clinic name.';
      const contacts = clinicContactSchema.safeParse({ phoneNumber: phone, email });
      if (!contacts.success) {
        for (const issue of contacts.error.issues) {
          const key = issue.path[0];
          if (key === 'phoneNumber') errors.phone = issue.message;
          if (key === 'email') errors.email = issue.message;
        }
      }
      if (Object.keys(errors).length > 0) {
        setFieldErrors(errors);
        throw new Error(Object.values(errors)[0] ?? 'Check the form and try again.');
      }
      setFieldErrors({});
      const parsed = clinicContactSchema.parse({ phoneNumber: phone, email });
      const phoneNumber = normalizeIndianMobile(parsed.phoneNumber)!;
      return updateAdminClinic(selectedId, {
        name: name.trim(),
        type,
        phoneNumber,
        email: parsed.email.trim() || null,
        website: website.trim() || null,
        description: description.trim() || null,
        registrationNumber: registrationNumber.trim() || null,
      });
    },
    onSuccess: async () => {
      setFeedback({ message: copy.adminClinicUpdated, tone: 'success' });
      setMode('list');
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinics'] });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinic', selectedId] });
    },
    onError: (error) => setFeedback({ message: problemMessage(error, copy.adminEditClinic), tone: 'error' }),
  });

  const assignDoctor = useMutation({
    mutationFn: (doctorId: string) => assignAdminClinicDoctor(selectedId!, doctorId),
    onSuccess: async () => {
      setFeedback({ message: copy.adminDoctorAssigned, tone: 'success' });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinic', selectedId] });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinics'] });
    },
    onError: (error) => setFeedback({ message: problemMessage(error, copy.adminAssignDoctors), tone: 'error' }),
  });

  const removeDoctor = useMutation({
    mutationFn: (doctorId: string) => removeAdminClinicDoctor(selectedId!, doctorId),
    onSuccess: async () => {
      setFeedback({ message: copy.adminDoctorRemoved, tone: 'success' });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinic', selectedId] });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinics'] });
    },
    onError: (error) => setFeedback({ message: problemMessage(error, copy.adminAssignDoctors), tone: 'error' }),
  });

  const activateDiscovery = useMutation({
    mutationFn: (doctorId: string) => activateAdminDoctorDiscovery(doctorId),
    onSuccess: async () => {
      setFeedback({ message: copy.adminDoctorActivatedDiscovery, tone: 'success' });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinic', selectedId] });
      await queryClient.invalidateQueries({ queryKey: ['discovery'] });
      await queryClient.invalidateQueries({ queryKey: ['doctors'] });
    },
    onError: (error) =>
      setFeedback({ message: problemMessage(error, copy.adminDoctorActivatedDiscovery), tone: 'error' }),
  });

  const removeStaff = useMutation({
    mutationFn: (userId: string) => removeAdminClinicStaff(selectedId!, userId),
    onSuccess: async () => {
      setFeedback({ message: copy.adminStaffRemoved, tone: 'success' });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinic', selectedId] });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'clinics'] });
    },
    onError: (error) => setFeedback({ message: problemMessage(error, copy.adminAssignClinics), tone: 'error' }),
  });

  const totals = {
    clinics: clinics.data?.length ?? 0,
    doctors: clinics.data?.reduce((sum, item) => sum + item.doctorCount, 0) ?? 0,
    staff: clinics.data?.reduce((sum, item) => sum + item.staffCount, 0) ?? 0,
  };

  const clinicForm = (
    <View style={{ gap: spacing.md, maxWidth: '100%' }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm }}>
        <AppText variant="label" style={{ color: theme.colors.text, fontSize: 16 }}>
          {mode === 'create' ? copy.adminCreateClinic : copy.adminEditClinic}
        </AppText>
        <Button label={copy.cancel} variant="ghost" onPress={() => setMode('list')} style={{ minHeight: 40, paddingHorizontal: 12 }} />
      </View>

      <AdminFormSection title={copy.adminSectionBasicInfo}>
        <TextField
          label={copy.clinicName}
          value={name}
          onChangeText={(value) => {
            setName(value);
            setFieldErrors((prev) => ({ ...prev, name: undefined }));
          }}
          error={fieldErrors.name}
        />
        <AppText variant="label" style={{ fontSize: 13, lineHeight: 18 }}>
          {copy.adminClinicType}
        </AppText>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          {['Clinic', 'Hospital', 'IndividualPractice'].map((item) => (
            <TypeChip key={item} label={item} selected={type === item} onPress={() => setType(item)} />
          ))}
        </View>
        <TextField
          label={copy.adminRegistrationNumberOptional}
          value={registrationNumber}
          onChangeText={setRegistrationNumber}
        />
        <TextField
          label={copy.adminDescriptionOptional}
          value={description}
          onChangeText={setDescription}
          multiline
          numberOfLines={3}
        />
      </AdminFormSection>

      <AdminFormSection title={copy.adminSectionContactDetails}>
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
          label={copy.adminEmailOptional}
          value={email}
          onChangeText={(value) => {
            setEmail(value);
            setFieldErrors((prev) => ({ ...prev, email: undefined }));
          }}
          keyboardType="email-address"
          autoCapitalize="none"
          error={fieldErrors.email}
        />
        <TextField label={copy.adminWebsiteOptional} value={website} onChangeText={setWebsite} autoCapitalize="none" />
      </AdminFormSection>

      {mode === 'create' ? (
        <AdminFormSection title={copy.adminSectionClinicAddress}>
          <TextField label={copy.adminBranchNameOptional} value={branchName} onChangeText={setBranchName} />
          <TextField
            label={copy.adminAddressLine}
            value={addressLine}
            onChangeText={(value) => {
              setAddressLine(value);
              setFieldErrors((prev) => ({ ...prev, addressLine: undefined }));
            }}
            error={fieldErrors.addressLine}
          />
          <TextField label={copy.adminAddressLine2Optional} value={addressLine2} onChangeText={setAddressLine2} />
          <TextField label={copy.adminLandmarkOptional} value={landmark} onChangeText={setLandmark} />
          <TextField label={copy.adminLocalityOptional} value={locality} onChangeText={setLocality} />
          <TextField label={copy.adminDistrictOptional} value={district} onChangeText={setDistrict} />
          <SearchableMultiSelect
            label={copy.state}
            options={INDIA_STATE_OPTIONS}
            selectedIds={state ? [state] : []}
            onChange={(ids) => {
              const next = ids[0] ?? '';
              setState(next);
              setCity('');
              setFieldErrors((prev) => ({ ...prev, state: undefined, city: undefined }));
            }}
            placeholder={copy.adminStateSearch}
            multiple={false}
          />
          {fieldErrors.state ? (
            <AppText variant="muted" style={{ color: theme.colors.error, fontSize: 12 }}>
              {fieldErrors.state}
            </AppText>
          ) : null}
          <SearchableMultiSelect
            label={copy.city}
            options={cityOptions}
            selectedIds={city ? [city] : []}
            onChange={(ids) => {
              setCity(ids[0] ?? '');
              setFieldErrors((prev) => ({ ...prev, city: undefined }));
            }}
            placeholder={copy.adminCitySearch}
            emptyMessage={state ? 'No cities listed for this state.' : 'Select a state first.'}
            multiple={false}
            disabled={!state}
          />
          {fieldErrors.city ? (
            <AppText variant="muted" style={{ color: theme.colors.error, fontSize: 12 }}>
              {fieldErrors.city}
            </AppText>
          ) : null}
          <TextField
            label={copy.adminPinCode}
            value={postalCode}
            onChangeText={(value) => {
              setPostalCode(value.replace(/\D/g, '').slice(0, 6));
              setFieldErrors((prev) => ({ ...prev, postalCode: undefined }));
            }}
            keyboardType="number-pad"
            maxLength={6}
            error={fieldErrors.postalCode}
          />
          <TextField
            label={copy.adminLatitudeOptional}
            value={latitude}
            onChangeText={setLatitude}
            keyboardType="decimal-pad"
          />
          <TextField
            label={copy.adminLongitudeOptional}
            value={longitude}
            onChangeText={setLongitude}
            keyboardType="decimal-pad"
          />
          <TextField
            label={copy.adminGooglePlaceIdOptional}
            value={googlePlaceId}
            onChangeText={setGooglePlaceId}
            autoCapitalize="none"
          />
        </AdminFormSection>
      ) : null}

      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
        <Button
          label={mode === 'create' ? copy.adminCreateClinic : copy.save}
          loading={create.isPending || update.isPending}
          onPress={() => (mode === 'create' ? create.mutate() : update.mutate())}
          style={{ flexGrow: 1, minWidth: 140 }}
        />
        <Button label={copy.cancel} variant="secondary" onPress={() => setMode('list')} style={{ flexGrow: 1, minWidth: 120 }} />
      </View>
    </View>
  );

  return (
    <View style={{ gap: spacing.lg, maxWidth: '100%' }}>
      <AdminFeedbackBanner
        message={feedback?.message}
        tone={feedback?.tone ?? 'info'}
        onDismiss={() => setFeedback(null)}
      />

      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
        <AdminKpiCard label={copy.adminClinics} value={String(totals.clinics)} />
        <AdminKpiCard label={copy.adminFilterDoctors} value={String(totals.doctors)} />
        <AdminKpiCard label={copy.adminFilterStaff} value={String(totals.staff)} />
      </View>

      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
        <Button
          label={copy.adminCreateClinic}
          onPress={() => {
            resetForm();
            setMode('create');
            setSelectedId(null);
            setFeedback(null);
          }}
        />
        {selectedId ? (
          <Button
            label={copy.adminEditClinic}
            variant="secondary"
            onPress={() => {
              fillFromDetails();
              setMode('edit');
              setFeedback(null);
            }}
          />
        ) : null}
      </View>

      {mode === 'create' || mode === 'edit' ? clinicForm : null}

      <AdminPanel title={copy.adminClinics} description={copy.adminClinicsBody}>
        <TextField label={copy.search} value={query} onChangeText={setQuery} placeholder={copy.adminClinicSearchPlaceholder} />
        {(clinics.data ?? []).map((clinic) => (
          <Pressable
            key={clinic.id}
            onPress={() => {
              setSelectedId(clinic.id);
              setMode('list');
            }}
            style={{
              borderWidth: 1,
              borderColor: selectedId === clinic.id ? theme.colors.primary : theme.colors.border,
              borderRadius: 14,
              padding: spacing.md,
              gap: spacing.xs,
              backgroundColor: selectedId === clinic.id ? theme.colors.primaryLight : theme.colors.surface,
            }}>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm }}>
              <AppText style={{ flex: 1 }}>{clinic.name ?? clinic.reference}</AppText>
              <StatusBadge label={clinic.status} tone={clinic.status === 'Approved' ? 'success' : 'primary'} />
            </View>
            <AppText variant="muted">
              {[clinic.city, clinic.state].filter(Boolean).join(', ') || clinic.reference}
            </AppText>
            <AppText variant="muted">
              {clinic.doctorCount} {copy.adminFilterDoctors} · {clinic.staffCount} {copy.adminFilterStaff}
            </AppText>
          </Pressable>
        ))}
        {(clinics.data?.length ?? 0) === 0 ? <AppText variant="muted">{copy.adminEmptyClinics}</AppText> : null}
      </AdminPanel>

      {details.data ? (
        <AdminPanel title={details.data.name ?? details.data.reference} description={details.data.reference}>
          <AppText variant="muted">
            {details.data.phoneNumber} {details.data.email ? `· ${details.data.email}` : ''}
          </AppText>
          {(details.data.branches ?? []).map((branch) => (
            <AppText key={branch.id} variant="muted">
              {branch.name}:{' '}
              {branch.formattedAddress ||
                [branch.addressLine, branch.locality, branch.city, branch.state, branch.postalCode]
                  .filter(Boolean)
                  .join(', ')}
              {branch.locationVerified ? ' · pin verified' : ''}
            </AppText>
          ))}

          <AppText variant="label">{copy.adminFilterDoctors}</AppText>
          {details.data.doctors.map((doctor) => (
            <View key={doctor.userId} style={{ gap: spacing.sm }}>
              <AppText>
                {doctor.fullName} · {doctor.phoneNumber}
              </AppText>
              {doctor.doctorId ? (
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                  <Button
                    label={copy.adminActivateDoctorDiscovery}
                    variant="secondary"
                    loading={activateDiscovery.isPending}
                    onPress={() => activateDiscovery.mutate(doctor.doctorId!)}
                  />
                  <Button
                    label={copy.remove}
                    variant="danger"
                    onPress={() => removeDoctor.mutate(doctor.doctorId!)}
                  />
                </View>
              ) : null}
            </View>
          ))}
          {details.data.doctors.length === 0 ? <AppText variant="muted">{copy.adminEmptyDoctors}</AppText> : null}

          <AppText variant="label">{copy.adminAssignDoctors}</AppText>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            {(doctors.data ?? [])
              .filter((item) => item.doctorId && !details.data?.doctors.some((doc) => doc.doctorId === item.doctorId))
              .map((doctor) => (
                <Button
                  key={doctor.userId}
                  label={doctor.fullName || doctor.phoneNumber}
                  variant="secondary"
                  onPress={() => doctor.doctorId && assignDoctor.mutate(doctor.doctorId)}
                />
              ))}
          </View>

          <AppText variant="label">{copy.adminFilterStaff}</AppText>
          {details.data.staff.map((member) => (
            <View key={member.userId} style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm, alignItems: 'center' }}>
              <AppText style={{ flex: 1 }}>
                {member.fullName} · {member.role} · {member.phoneNumber}
              </AppText>
              <Button label={copy.remove} variant="danger" onPress={() => removeStaff.mutate(member.userId)} />
            </View>
          ))}
          {details.data.staff.length === 0 ? <AppText variant="muted">{copy.adminEmptyStaff}</AppText> : null}
        </AdminPanel>
      ) : null}
    </View>
  );
}
