import { useQueries } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Image, Pressable, ScrollView, View } from 'react-native';

import { SearchBar } from '@/components/ui/search-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { apiBaseUrl } from '@/services/api/client';
import {
  searchClinics,
  searchDoctors,
  type ClinicCard,
  type DoctorCard,
} from '@/services/api/provider-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';
import { formatYearsExperience } from '@/utils/years-of-experience';

type DoctorSearchAutocompleteProps = {
  placeholder?: string;
  /** When true, also navigate to the full directory on empty submit. */
  allowOpenDirectory?: boolean;
  autoFocus?: boolean;
  onSelectDoctor?: (doctor: DoctorCard) => void;
  onSelectClinic?: (clinic: ClinicCard) => void;
};

function photoUri(documentId: string | null | undefined, accessToken: string | null) {
  if (!documentId || !accessToken) {
    return null;
  }
  return {
    uri: `${apiBaseUrl}/api/v1/provider-documents/${documentId}`,
    headers: { Authorization: `Bearer ${accessToken}` },
  };
}

/** Live doctor + clinic autocomplete with ~300ms debounce against public directory APIs. */
export function DoctorSearchAutocomplete({
  placeholder,
  allowOpenDirectory = true,
  autoFocus = false,
  onSelectDoctor,
  onSelectClinic,
}: DoctorSearchAutocompleteProps) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const accessToken = useSessionStore((state) => state.accessToken);
  const [text, setText] = useState('');
  const [debounced, setDebounced] = useState('');
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(text.trim()), 300);
    return () => clearTimeout(handle);
  }, [text]);

  const enabled = debounced.length >= 1;
  const [doctorsQuery, clinicsQuery] = useQueries({
    queries: [
      {
        queryKey: ['doctors', 'live-search', debounced],
        queryFn: ({ signal }) => searchDoctors(debounced, signal),
        enabled,
      },
      {
        queryKey: ['clinics', 'live-search', debounced],
        queryFn: ({ signal }) => searchClinics(debounced, signal),
        enabled,
      },
    ],
  });

  const doctors = useMemo(() => doctorsQuery.data ?? [], [doctorsQuery.data]);
  const clinics = useMemo(() => clinicsQuery.data ?? [], [clinicsQuery.data]);
  const isInitialLoading =
    (doctorsQuery.isLoading && !doctorsQuery.data) || (clinicsQuery.isLoading && !clinicsQuery.data);
  const isError = doctorsQuery.isError || clinicsQuery.isError;
  const error = doctorsQuery.error ?? clinicsQuery.error;
  const hasResults = doctors.length > 0 || clinics.length > 0;
  const showPanel = open && enabled;

  const clearSearch = () => {
    setText('');
    setDebounced('');
    setOpen(false);
  };

  return (
    <View style={{ gap: spacing.sm, zIndex: 20 }}>
      <SearchBar
        placeholder={placeholder ?? copy.homeSearchPlaceholder}
        value={text}
        autoFocus={autoFocus}
        onClear={clearSearch}
        onChangeText={(value) => {
          setText(value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onSubmitEditing={() => {
          if (allowOpenDirectory) {
            const q = text.trim();
            router.push((q ? `/doctors?q=${encodeURIComponent(q)}` : '/doctors') as Href);
            setOpen(false);
          }
        }}
        returnKeyType="search"
      />

      {showPanel ? (
        <View
          style={{
            borderWidth: 1,
            borderColor: theme.colors.border,
            borderRadius: radius.lg,
            backgroundColor: theme.colors.surface,
            overflow: 'hidden',
            maxHeight: 360,
          }}>
          <ScrollView keyboardShouldPersistTaps="handled" nestedScrollEnabled>
            {isInitialLoading ? (
              <View style={{ padding: spacing.md, alignItems: 'center' }}>
                <ActivityIndicator color={theme.colors.primary} />
              </View>
            ) : null}

            {isError ? (
              <View style={{ padding: spacing.md }}>
                <AppText variant="muted" style={{ color: theme.colors.error }}>
                  {problemMessage(error, copy.phoneRequestFailed)}
                </AppText>
              </View>
            ) : null}

            {!isInitialLoading && !isError && !hasResults ? (
              <View style={{ padding: spacing.md }}>
                <AppText variant="muted">{copy.noSearchMatches}</AppText>
              </View>
            ) : null}

            {doctors.length > 0 ? (
              <View>
                <View style={{ paddingHorizontal: spacing.md, paddingTop: spacing.sm, paddingBottom: spacing.xs }}>
                  <AppText variant="caption" style={{ color: theme.colors.primary }}>
                    {copy.searchDoctorsSection}
                  </AppText>
                </View>
                {doctors.map((doctor) => {
                  const photo = photoUri(doctor.photoDocumentId, accessToken);
                  const meta = [
                    doctor.primarySpecialization,
                    doctor.qualification,
                    formatYearsExperience(doctor.yearsOfExperience, copy.yearsExp),
                    doctor.clinicName,
                  ]
                    .filter(Boolean)
                    .join(' · ');
                  return (
                    <Pressable
                      key={doctor.id}
                      onPress={() => {
                        setOpen(false);
                        setText(doctor.displayName);
                        onSelectDoctor?.(doctor);
                        router.push(`/practices/${doctor.id}` as Href);
                      }}
                      style={{
                        flexDirection: 'row',
                        gap: spacing.md,
                        paddingHorizontal: spacing.md,
                        paddingVertical: spacing.md,
                        borderBottomWidth: 1,
                        borderBottomColor: theme.colors.border,
                        alignItems: 'center',
                      }}>
                      <View
                        style={{
                          width: 44,
                          height: 44,
                          borderRadius: 22,
                          backgroundColor: theme.colors.primaryLight,
                          overflow: 'hidden',
                          alignItems: 'center',
                          justifyContent: 'center',
                        }}>
                        {photo ? (
                          <Image source={photo} style={{ width: 44, height: 44 }} />
                        ) : (
                          <AppText variant="label" style={{ color: theme.colors.primary }}>
                            {doctor.displayName.slice(0, 1).toUpperCase()}
                          </AppText>
                        )}
                      </View>
                      <View style={{ flex: 1, gap: 2 }}>
                        <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
                          <AppText variant="label" style={{ flexShrink: 1 }}>
                            {doctor.displayName}
                          </AppText>
                          {doctor.isVerified !== false ? (
                            <StatusBadge label={copy.verified} tone="success" />
                          ) : null}
                        </View>
                        {meta ? (
                          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                            {meta}
                          </AppText>
                        ) : null}
                      </View>
                    </Pressable>
                  );
                })}
              </View>
            ) : null}

            {clinics.length > 0 ? (
              <View>
                <View style={{ paddingHorizontal: spacing.md, paddingTop: spacing.sm, paddingBottom: spacing.xs }}>
                  <AppText variant="caption" style={{ color: theme.colors.primary }}>
                    {copy.searchClinicsSection}
                  </AppText>
                </View>
                {clinics.map((clinic) => {
                  const doctorsLine = (clinic.associatedDoctors ?? []).filter(Boolean).join(', ');
                  const place = [clinic.branchName, clinic.location || clinic.city].filter(Boolean).join(' · ');
                  return (
                    <Pressable
                      key={clinic.id}
                      onPress={() => {
                        setOpen(false);
                        setText(clinic.name);
                        onSelectClinic?.(clinic);
                        router.push(`/places/clinics/${clinic.id}` as Href);
                      }}
                      style={{
                        paddingHorizontal: spacing.md,
                        paddingVertical: spacing.md,
                        borderBottomWidth: 1,
                        borderBottomColor: theme.colors.border,
                        gap: 2,
                      }}>
                      <AppText variant="label">{clinic.name}</AppText>
                      {place ? (
                        <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                          {place}
                        </AppText>
                      ) : null}
                      {doctorsLine ? (
                        <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                          {copy.associatedDoctors}: {doctorsLine}
                        </AppText>
                      ) : null}
                    </Pressable>
                  );
                })}
              </View>
            ) : null}

            <Pressable
              onPress={() => setOpen(false)}
              style={{ padding: spacing.sm, alignItems: 'center' }}>
              <AppText variant="label" style={{ color: theme.colors.primary }}>
                {copy.cancel}
              </AppText>
            </Pressable>
          </ScrollView>
        </View>
      ) : null}
    </View>
  );
}
