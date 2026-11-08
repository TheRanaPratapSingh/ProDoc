import { FlashList } from '@shopify/flash-list';
import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Platform, Pressable, ScrollView, View } from 'react-native';
import { KeyboardAvoidingView } from 'react-native-keyboard-controller';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ClinicCard } from '@/components/ui/doctor-card';
import { EmptyState } from '@/components/ui/empty-state';
import { ErrorState } from '@/components/ui/error-state';
import { SearchBar } from '@/components/ui/search-bar';
import { CardSkeleton } from '@/components/ui/skeleton';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import {
  discoverClinics,
  getSpecializations,
  type DiscoveryFilters,
  type DiscoveredClinic,
} from '@/services/api/provider-api';
import { readDiscoveryCache } from '@/services/offline/query-cache';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';
import { photoUriFor } from '@/utils/provider-photo';

function FilterChip({
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
        paddingHorizontal: 14,
        paddingVertical: 8,
        borderRadius: 999,
        backgroundColor: selected ? theme.colors.primary : '#EEF2F6',
      }}>
      <AppText
        variant="label"
        style={{
          fontSize: 13,
          lineHeight: 16,
          color: selected ? theme.colors.onPrimary : theme.colors.text,
        }}>
        {label}
      </AppText>
    </Pressable>
  );
}

function primaryBranch(clinic: DiscoveredClinic) {
  return [...clinic.branches].sort((a, b) => (a.distanceKm ?? Number.MAX_VALUE) - (b.distanceKm ?? Number.MAX_VALUE))[0];
}

function doctorCountFor(clinic: DiscoveredClinic) {
  return clinic.branches.reduce((sum, branch) => sum + branch.doctorCount, 0);
}

function formatDoctorCount(count: number, one: string, many: string) {
  if (count === 1) return one;
  return many.replace('{count}', String(count));
}

export function ClinicDirectory() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const accessToken = useSessionStore((state) => state.accessToken);

  const [query, setQuery] = useState('');
  const [city, setCity] = useState('');
  const [specializationId, setSpecializationId] = useState('');
  const [availability, setAvailability] = useState('');
  const [latitude, setLatitude] = useState('');
  const [longitude, setLongitude] = useState('');
  const [radiusKm, setRadiusKm] = useState('25');
  const [sortNearest, setSortNearest] = useState(false);
  const [showFilters, setShowFilters] = useState(false);
  const [locationNote, setLocationNote] = useState('');
  const [filters, setFilters] = useState<DiscoveryFilters>({ pageSize: 40 });
  const [cached, setCached] = useState<DiscoveredClinic[] | null>(null);

  useEffect(() => {
    void readDiscoveryCache<DiscoveredClinic[]>('clinics', filters).then(setCached);
  }, [filters]);

  useEffect(() => {
    const handle = setTimeout(() => {
      setFilters((current) => {
        const nextQuery = query.trim() || undefined;
        if (current.query === nextQuery) return current;
        return { ...current, query: nextQuery };
      });
    }, 320);
    return () => clearTimeout(handle);
  }, [query]);

  const applyFilters = (next?: { lat?: string; lng?: string; availability?: string }) => {
    const lat = next?.lat ?? latitude;
    const lng = next?.lng ?? longitude;
    setFilters({
      query: query.trim() || undefined,
      city: city.trim() || undefined,
      specializationId: specializationId || undefined,
      availability: (next?.availability ?? availability) || undefined,
      latitude: lat.trim() || undefined,
      longitude: lng.trim() || undefined,
      radiusKm: lat.trim() && lng.trim() ? radiusKm.trim() || '25' : undefined,
      pageSize: 40,
    });
  };

  const specializations = useQuery({ queryKey: ['specializations'], queryFn: getSpecializations });
  const clinics = useQuery({
    queryKey: ['discovery', 'clinics', 'directory', filters],
    queryFn: () => discoverClinics(filters),
    placeholderData: cached ?? undefined,
  });

  const requestLocation = () => {
    const geolocation = typeof navigator !== 'undefined' ? navigator.geolocation : undefined;
    if (!geolocation) {
      setLocationNote(copy.locationDenied);
      return;
    }
    geolocation.getCurrentPosition(
      (position) => {
        const lat = position.coords.latitude.toFixed(5);
        const lng = position.coords.longitude.toFixed(5);
        setLatitude(lat);
        setLongitude(lng);
        setLocationNote('');
        setSortNearest(true);
        applyFilters({ lat, lng });
      },
      () => setLocationNote(copy.locationDenied),
      { enableHighAccuracy: false, maximumAge: 60_000, timeout: 8000 },
    );
  };

  const rows = useMemo(() => {
    const list = [...(clinics.data ?? [])];
    if (!sortNearest) return list;
    return list.sort((a, b) => {
      const left = primaryBranch(a)?.distanceKm ?? Number.MAX_VALUE;
      const right = primaryBranch(b)?.distanceKm ?? Number.MAX_VALUE;
      return left - right;
    });
  }, [clinics.data, sortNearest]);

  const loading = clinics.isLoading && !clinics.data;
  const enter = theme.reduceMotion ? undefined : FadeInDown.duration(duration.enter);

  return (
    <Screen edges={['top', 'left', 'right', 'bottom']}>
      <View style={{ flex: 1, backgroundColor: theme.colors.background }}>
        <ScreenHeader title={copy.findClinicsTitle} showBack />
        <KeyboardAvoidingView
          style={{ flex: 1 }}
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
          keyboardVerticalOffset={8}>
          <FlashList
            data={rows}
            keyExtractor={(item) => item.id}
            contentContainerStyle={{ paddingHorizontal: spacing.lg, paddingBottom: spacing.xxl }}
            keyboardShouldPersistTaps="handled"
            ItemSeparatorComponent={() => <View style={{ height: 12 }} />}
            ListHeaderComponent={
              <Animated.View entering={enter} style={{ gap: spacing.md, paddingTop: spacing.sm, paddingBottom: spacing.md }}>
                <SearchBar
                  value={query}
                  onChangeText={setQuery}
                  placeholder={`${copy.search} · ${copy.clinics}, ${copy.doctors}, ${copy.specialization}`}
                />
                <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8 }}>
                  <FilterChip
                    label={copy.filterVerified}
                    selected
                    onPress={() => undefined}
                  />
                  <FilterChip
                    label={copy.filterAvailableDoctors}
                    selected={availability === 'today'}
                    onPress={() => {
                      const next = availability === 'today' ? '' : 'today';
                      setAvailability(next);
                      applyFilters({ availability: next });
                    }}
                  />
                  <FilterChip
                    label={copy.nearest}
                    selected={sortNearest}
                    onPress={() => {
                      if (sortNearest) {
                        setSortNearest(false);
                        setLatitude('');
                        setLongitude('');
                        applyFilters({ lat: '', lng: '' });
                        return;
                      }
                      requestLocation();
                    }}
                  />
                  <FilterChip
                    label={copy.anySpecialization}
                    selected={!specializationId && showFilters}
                    onPress={() => setShowFilters((value) => !value)}
                  />
                </ScrollView>

                {showFilters ? (
                  <Card elevated style={{ gap: spacing.md }}>
                    <TextField
                      label={copy.city}
                      value={city}
                      onChangeText={setCity}
                      placeholder="Bengaluru"
                      autoCapitalize="words"
                    />
                    <AppText variant="label">{copy.specialization}</AppText>
                    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                      <Button
                        label={copy.anySpecialization}
                        variant={!specializationId ? 'primary' : 'secondary'}
                        onPress={() => setSpecializationId('')}
                      />
                      {(specializations.data ?? []).slice(0, 12).map((item) => (
                        <Button
                          key={item.id}
                          label={item.name}
                          variant={specializationId === item.id ? 'primary' : 'secondary'}
                          onPress={() => setSpecializationId(item.id)}
                        />
                      ))}
                    </View>
                    <AppText variant="label">{copy.openTimes}</AppText>
                    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                      {[
                        ['', copy.anyDay],
                        ['today', copy.availableToday],
                        ['tomorrow', copy.availableTomorrow],
                        ['week', copy.thisWeek],
                      ].map(([value, label]) => (
                        <Button
                          key={value || 'any'}
                          label={label}
                          variant={availability === value ? 'primary' : 'secondary'}
                          onPress={() => setAvailability(value)}
                        />
                      ))}
                    </View>
                    <Button
                      label={copy.useLocation}
                      variant="secondary"
                      onPress={requestLocation}
                    />
                    {locationNote ? <AppText variant="muted">{locationNote}</AppText> : null}
                    <Button label={copy.search} onPress={() => applyFilters()} />
                  </Card>
                ) : null}

                {loading ? (
                  <Card>
                    <CardSkeleton />
                  </Card>
                ) : null}
              </Animated.View>
            }
            renderItem={({ item, index }) => {
              const branch = primaryBranch(item);
              const count = doctorCountFor(item);
              const logo = photoUriFor(item.logoDocumentId, accessToken);
              const address = branch
                ? branch.formattedAddress ||
                  branch.compactAddress ||
                  [branch.locality || branch.addressLine, branch.city, branch.postalCode].filter(Boolean).join(', ')
                : null;
              return (
                <Animated.View
                  entering={
                    theme.reduceMotion ? undefined : FadeInDown.delay(Math.min(index, 6) * 40).duration(220)
                  }>
                  <ClinicCard
                    name={item.name}
                    type={item.type}
                    verifiedLabel={copy.verified}
                    specializations={item.specializations.slice(0, 4).join(', ')}
                    address={address}
                    city={branch?.city}
                    landmark={branch?.landmark}
                    mapLocation={
                      branch
                        ? {
                            name: item.name,
                            address:
                              branch.formattedAddress ||
                              [branch.addressLine, branch.city, branch.postalCode].filter(Boolean).join(', '),
                            latitude: branch.latitude,
                            longitude: branch.longitude,
                            googlePlaceId: branch.googlePlaceId,
                            mapsUrl: branch.mapsUrl,
                            directionsUrl: branch.directionsUrl,
                          }
                        : null
                    }
                    doctorCount={count}
                    doctorCountLabel={formatDoctorCount(count, copy.doctorCountOne, copy.doctorsCount)}
                    openTodayLabel={branch?.openToday ? copy.openToday : undefined}
                    logoUri={logo?.uri}
                    logoHeaders={logo?.headers}
                    viewLabel={copy.viewClinic}
                    onView={() => router.push(`/places/clinics/${item.id}` as Href)}
                  />
                </Animated.View>
              );
            }}
            ListEmptyComponent={
              !loading ? (
                clinics.error ? (
                  <ErrorState
                    title={copy.findClinicsTitle}
                    body={problemMessage(clinics.error, copy.findClinicsTitle)}
                    actionLabel={copy.continue}
                    onAction={() => void clinics.refetch()}
                  />
                ) : (
                  <EmptyState title={copy.noClinics} body={copy.loginSubtitle} />
                )
              ) : undefined
            }
          />
        </KeyboardAvoidingView>
      </View>
    </Screen>
  );
}
