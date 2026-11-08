import { FlashList } from '@shopify/flash-list';
import { useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Platform, Pressable, ScrollView, View } from 'react-native';
import { KeyboardAvoidingView } from 'react-native-keyboard-controller';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { Screen } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ClinicCard } from '@/components/ui/doctor-card';
import { EmptyState } from '@/components/ui/empty-state';
import { FindDoctorCard } from '@/components/ui/find-doctor-card';
import { SearchBar } from '@/components/ui/search-bar';
import { CardSkeleton } from '@/components/ui/skeleton';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { useCopy } from '@/i18n/copy';
import {
  addFavourite,
  discoverClinics,
  discoverPlaces,
  getSpecializations,
  listFavourites,
  removeFavourite,
  searchDoctors,
  type DiscoveryFilters,
  type DiscoveryPlace,
} from '@/services/api/provider-api';
import { getDoctorRatingSummary } from '@/services/api/review-api';
import { readDiscoveryCache } from '@/services/offline/query-cache';
import { selectIsEffectivelyOnline, useNetworkStore } from '@/stores/network-store';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';
import { photoUriFor } from '@/utils/provider-photo';
import { formatYearsExperience } from '@/utils/years-of-experience';

type Mode = 'doctors' | 'clinics';
type Chip = 'all' | 'nearby' | 'topRated';

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
        paddingHorizontal: 16,
        paddingVertical: 9,
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

export function DoctorDirectory() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const queryClient = useQueryClient();
  const accessToken = useSessionStore((state) => state.accessToken);
  const params = useLocalSearchParams<{ mode?: string; specializationId?: string; q?: string }>();
  const initialMode: Mode = params.mode === 'clinics' ? 'clinics' : 'doctors';
  const initialSpecialization = typeof params.specializationId === 'string' ? params.specializationId : '';
  const initialQuery = typeof params.q === 'string' ? params.q : '';

  const [mode, setMode] = useState<Mode>(initialMode);
  const [chip, setChip] = useState<Chip>('all');
  const [query, setQuery] = useState(initialQuery);
  const [city, setCity] = useState('');
  const [postalCode, setPostalCode] = useState('');
  const [maxFee, setMaxFee] = useState('');
  const [latitude, setLatitude] = useState('');
  const [longitude, setLongitude] = useState('');
  const [radiusKm, setRadiusKm] = useState('25');
  const [specializationId, setSpecializationId] = useState(initialSpecialization);
  const [availability, setAvailability] = useState('');
  const [sort, setSort] = useState('recommended');
  const [locationNote, setLocationNote] = useState('');
  const [recent, setRecent] = useState<string[]>([]);
  const [showMoreFilters, setShowMoreFilters] = useState(false);
  const [favouriteBusy, setFavouriteBusy] = useState<string | null>(null);
  const [filters, setFilters] = useState<DiscoveryFilters>(() => ({
    sort: 'recommended',
    specializationId: initialSpecialization || undefined,
    query: initialQuery.trim() || undefined,
  }));

  const online = useNetworkStore(selectIsEffectivelyOnline);
  const [cachedDoctors, setCachedDoctors] = useState<Awaited<ReturnType<typeof discoverPlaces>> | null>(null);
  const [cachedClinics, setCachedClinics] = useState<Awaited<ReturnType<typeof discoverClinics>> | null>(null);

  useEffect(() => {
    void readDiscoveryCache<Awaited<ReturnType<typeof discoverPlaces>>>('doctors', filters).then(setCachedDoctors);
    void readDiscoveryCache<Awaited<ReturnType<typeof discoverClinics>>>('clinics', filters).then(setCachedClinics);
  }, [filters]);

  useEffect(() => {
    if (params.mode === 'clinics') {
      router.replace('/clinics' as Href);
      return;
    }
    if (params.mode === 'doctors') {
      setMode('doctors');
    }
    if (typeof params.specializationId === 'string' && params.specializationId) {
      setSpecializationId(params.specializationId);
      setFilters((current) => ({ ...current, specializationId: params.specializationId }));
    }
  }, [params.mode, params.specializationId, router]);

  const applyFilters = (next: {
    queryText?: string;
    sortValue?: string;
    lat?: string;
    lng?: string;
    specialization?: string;
    remember?: boolean;
  } = {}) => {
    const text = (next.queryText ?? query).trim();
    if (next.remember && text) {
      setRecent((current) => [text, ...current.filter((item) => item !== text)].slice(0, 5));
    }
    const lat = next.lat ?? latitude;
    const lng = next.lng ?? longitude;
    const sortValue = next.sortValue ?? sort;
    setFilters({
      query: text || undefined,
      city: city.trim() || undefined,
      postalCode: postalCode.trim() || undefined,
      maxFee: maxFee.trim() || undefined,
      latitude: lat.trim() || undefined,
      longitude: lng.trim() || undefined,
      radiusKm: lat.trim() && lng.trim() ? radiusKm.trim() || '25' : undefined,
      specializationId: (next.specialization ?? specializationId) || undefined,
      availability: availability || undefined,
      sort: sortValue,
    });
  };

  // Live search debounce — updates query filter only
  useEffect(() => {
    const handle = setTimeout(() => {
      setFilters((current) => {
        const nextQuery = query.trim() || undefined;
        if (current.query === nextQuery) {
          return current;
        }
        return { ...current, query: nextQuery };
      });
    }, 320);
    return () => clearTimeout(handle);
  }, [query]);

  const specializations = useQuery({ queryKey: ['specializations'], queryFn: getSpecializations });
  const doctors = useQuery({
    queryKey: ['discovery', 'doctors', filters],
    queryFn: ({ signal }) => discoverPlaces(filters, signal),
    enabled: mode === 'doctors',
    placeholderData: cachedDoctors ?? undefined,
  });
  const liveMatches = useQuery({
    queryKey: ['doctors', 'live-search', filters.query ?? ''],
    queryFn: ({ signal }) => searchDoctors(filters.query ?? '', signal),
    enabled: mode === 'doctors' && Boolean(filters.query?.trim()),
  });
  const clinics = useQuery({
    queryKey: ['discovery', 'clinics', filters],
    queryFn: () => discoverClinics(filters),
    enabled: mode === 'clinics',
    placeholderData: cachedClinics ?? undefined,
  });
  const favourites = useQuery({
    queryKey: ['favourites'],
    queryFn: listFavourites,
    enabled: mode === 'doctors',
  });
  const favouriteIds = useMemo(
    () => new Set((favourites.data ?? []).map((item) => item.id)),
    [favourites.data],
  );

  const uniqueDoctors = useMemo(() => {
    const seen = new Set<string>();
    const rows: DiscoveryPlace[] = [];
    for (const place of doctors.data ?? []) {
      if (seen.has(place.doctorId)) {
        continue;
      }
      seen.add(place.doctorId);
      rows.push(place);
    }
    return rows;
  }, [doctors.data]);

  const ratingQueries = useQueries({
    queries: uniqueDoctors.map((place) => ({
      queryKey: ['rating', 'doctor', place.doctorId],
      queryFn: () => getDoctorRatingSummary(place.doctorId),
      staleTime: 60_000,
      enabled: mode === 'doctors' && uniqueDoctors.length > 0,
    })),
  });

  const ratingByDoctor = useMemo(() => {
    const map = new Map<string, { average: number | null; count: number; ready: boolean }>();
    uniqueDoctors.forEach((place, index) => {
      const query = ratingQueries[index];
      const summary = query?.data;
      const publicAverage =
        summary?.averageIsPublic && summary.averageOverall != null ? summary.averageOverall : null;
      map.set(place.doctorId, {
        average: publicAverage,
        count: summary?.publishedReviewCount ?? 0,
        ready: Boolean(query?.isFetched),
      });
    });
    return map;
  }, [ratingQueries, uniqueDoctors]);

  const sortedDoctors = useMemo(() => {
    if (chip !== 'topRated') {
      return uniqueDoctors;
    }
    return [...uniqueDoctors].sort((a, b) => {
      const left = ratingByDoctor.get(a.doctorId)?.average ?? -1;
      const right = ratingByDoctor.get(b.doctorId)?.average ?? -1;
      if (right !== left) {
        return right - left;
      }
      return (ratingByDoctor.get(b.doctorId)?.count ?? 0) - (ratingByDoctor.get(a.doctorId)?.count ?? 0);
    });
  }, [chip, ratingByDoctor, uniqueDoctors]);

  const requestLocation = (onReady?: (lat: string, lng: string) => void) => {
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
        onReady?.(lat, lng);
      },
      () => setLocationNote(copy.locationDenied),
      { enableHighAccuracy: false, maximumAge: 60_000, timeout: 8000 },
    );
  };

  const selectChip = (next: Chip) => {
    setChip(next);
    if (next === 'all') {
      setSort('recommended');
      setLatitude('');
      setLongitude('');
      setFilters((current) => ({
        ...current,
        sort: 'recommended',
        latitude: undefined,
        longitude: undefined,
        radiusKm: undefined,
        query: query.trim() || undefined,
      }));
      return;
    }
    if (next === 'nearby') {
      setSort('nearest');
      requestLocation((lat, lng) => {
        setSort('nearest');
        applyFilters({ sortValue: 'nearest', lat, lng });
      });
      return;
    }
    // Top rated — keep discovery results, sort by live rating summaries
    setSort('recommended');
    applyFilters({ sortValue: 'recommended' });
  };

  const toggleFavourite = async (doctorId: string) => {
    setFavouriteBusy(doctorId);
    try {
      if (favouriteIds.has(doctorId)) {
        await removeFavourite(doctorId);
      } else {
        await addFavourite(doctorId);
      }
      await queryClient.invalidateQueries({ queryKey: ['favourites'] });
    } finally {
      setFavouriteBusy(null);
    }
  };

  const title = mode === 'clinics' ? copy.clinics : copy.findDoctorsTitle;
  const loading = mode === 'doctors' ? doctors.isLoading && !doctors.data : clinics.isLoading && !clinics.data;
  const listError = doctors.error ?? clinics.error;

  return (
    <Screen edges={['top', 'left', 'right', 'bottom']}>
      <View style={{ flex: 1, backgroundColor: theme.colors.background }}>
        <ScreenHeader title={title} showBack />

        <KeyboardAvoidingView style={{ flex: 1 }} behavior="padding" automaticOffset>
        <FlashList
          data={mode === 'doctors' ? sortedDoctors : []}
          keyExtractor={(item) => item.doctorId}
          contentContainerStyle={{ paddingHorizontal: 20, paddingTop: 16, paddingBottom: 32 }}
          keyboardShouldPersistTaps="handled"
          keyboardDismissMode={Platform.OS === 'ios' ? 'interactive' : 'on-drag'}
          ItemSeparatorComponent={() => <View style={{ height: 12 }} />}
          ListHeaderComponent={
            <Animated.View entering={theme.reduceMotion ? undefined : FadeInDown.duration(240)} style={{ gap: 14, marginBottom: 4 }}>
              <SearchBar
                pill
                placeholder={copy.findDoctorsSearchPlaceholder}
                value={query}
                onChangeText={setQuery}
                onSubmitEditing={() => applyFilters({ queryText: query, remember: true })}
                returnKeyType="search"
                autoCorrect={false}
              />

              {mode === 'doctors' && query.trim().length > 0 ? (
                <View
                  style={{
                    borderWidth: 1,
                    borderColor: theme.colors.border,
                    borderRadius: 14,
                    backgroundColor: theme.colors.surface,
                    overflow: 'hidden',
                  }}>
                  {liveMatches.isLoading && !liveMatches.data ? (
                    <View style={{ padding: 12 }}>
                      <CardSkeleton />
                    </View>
                  ) : null}
                  {(liveMatches.data ?? []).slice(0, 6).map((doctor) => (
                    <Pressable
                      key={doctor.id}
                      onPress={() => router.push(`/practices/${doctor.id}` as Href)}
                      style={{
                        paddingHorizontal: 14,
                        paddingVertical: 12,
                        borderBottomWidth: 1,
                        borderBottomColor: theme.colors.border,
                        gap: 2,
                      }}>
                      <AppText variant="label">{doctor.displayName}</AppText>
                      <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                        {[doctor.primarySpecialization, doctor.clinicName, doctor.city]
                          .filter(Boolean)
                          .join(' · ')}
                      </AppText>
                    </Pressable>
                  ))}
                  {!(liveMatches.isLoading && !liveMatches.data) && (liveMatches.data?.length ?? 0) === 0 ? (
                    <View style={{ padding: 12 }}>
                      <AppText variant="muted">{copy.noDoctors}</AppText>
                    </View>
                  ) : null}
                </View>
              ) : null}

              {mode === 'doctors' ? (
                <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8 }}>
                  <FilterChip label={copy.filterAll} selected={chip === 'all'} onPress={() => selectChip('all')} />
                  <FilterChip label={copy.filterNearby} selected={chip === 'nearby'} onPress={() => selectChip('nearby')} />
                  <FilterChip
                    label={copy.filterTopRated}
                    selected={chip === 'topRated'}
                    onPress={() => selectChip('topRated')}
                  />
                  <FilterChip
                    label={copy.moreFilters}
                    selected={showMoreFilters}
                    onPress={() => setShowMoreFilters((value) => !value)}
                  />
                  <FilterChip
                    label={copy.clinics}
                    selected={false}
                    onPress={() => {
                      setMode('clinics');
                      applyFilters();
                    }}
                  />
                </ScrollView>
              ) : (
                <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8 }}>
                  <FilterChip
                    label={copy.doctors}
                    selected={false}
                    onPress={() => {
                      setMode('doctors');
                      applyFilters();
                    }}
                  />
                  <FilterChip
                    label={copy.moreFilters}
                    selected={showMoreFilters}
                    onPress={() => setShowMoreFilters((value) => !value)}
                  />
                </ScrollView>
              )}

              {!online ? <AppText variant="muted">{copy.offlineCachedData}</AppText> : null}
              {locationNote ? <AppText variant="muted">{locationNote}</AppText> : null}

              {showMoreFilters ? (
                <Card elevated style={{ gap: spacing.md }}>
                  <TextField label={copy.city} value={city} onChangeText={setCity} />
                  <TextField label={copy.postalCode} value={postalCode} onChangeText={setPostalCode} keyboardType="number-pad" />
                  {mode === 'doctors' ? (
                    <TextField label={copy.maxFee} value={maxFee} onChangeText={setMaxFee} keyboardType="decimal-pad" />
                  ) : null}
                  <TextField label={copy.latitude} value={latitude} onChangeText={setLatitude} keyboardType="decimal-pad" />
                  <TextField label={copy.longitude} value={longitude} onChangeText={setLongitude} keyboardType="decimal-pad" />
                  <TextField label={copy.radius} value={radiusKm} onChangeText={setRadiusKm} keyboardType="decimal-pad" />
                  <Button label={copy.useLocation} variant="secondary" onPress={() => requestLocation()} />
                  <AppText variant="label">{copy.specialization}</AppText>
                  <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                    <Button
                      label={copy.anySpecialization}
                      variant={specializationId === '' ? 'primary' : 'secondary'}
                      onPress={() => setSpecializationId('')}
                    />
                    {(specializations.data ?? []).map((item) => (
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
                  <AppText variant="label">{copy.search}</AppText>
                  <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                    {[
                      ['recommended', copy.verified],
                      ['nearest', copy.nearest],
                      ['fee', copy.lowestFee],
                      ['experience', copy.mostExperience],
                    ].map(([value, label]) => (
                      <Button
                        key={value}
                        label={label}
                        variant={sort === value ? 'primary' : 'secondary'}
                        onPress={() => setSort(value)}
                      />
                    ))}
                  </View>
                  <Button label={copy.search} onPress={() => applyFilters({ remember: true })} />
                  {recent.length > 0 ? <AppText variant="label">{copy.recentSearches}</AppText> : null}
                  {recent.map((item) => (
                    <Button
                      key={item}
                      label={item}
                      variant="secondary"
                      onPress={() => {
                        setQuery(item);
                        applyFilters({ queryText: item, remember: true });
                      }}
                    />
                  ))}
                </Card>
              ) : null}

              {loading ? (
                <Card>
                  <CardSkeleton />
                </Card>
              ) : null}

              {mode === 'clinics' ? (
                <View style={{ gap: 12 }}>
                  {(clinics.data ?? []).map((clinic) => {
                    const branch = clinic.branches[0];
                    const doctorCount = clinic.branches.reduce((sum, item) => sum + item.doctorCount, 0);
                    const logo = photoUriFor(clinic.logoDocumentId, accessToken);
                    return (
                      <ClinicCard
                        key={clinic.id}
                        name={clinic.name}
                        type={clinic.type}
                        verifiedLabel={copy.verified}
                        specializations={clinic.specializations.join(', ')}
                        address={
                          branch
                            ? branch.formattedAddress ||
                              branch.compactAddress ||
                              [branch.locality || branch.addressLine, branch.city, branch.postalCode]
                                .filter(Boolean)
                                .join(', ')
                            : null
                        }
                        city={branch?.city}
                        landmark={branch?.landmark}
                        mapLocation={
                          branch
                            ? {
                                name: clinic.name,
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
                        doctorCount={doctorCount}
                        doctorCountLabel={
                          doctorCount === 1
                            ? copy.doctorCountOne
                            : copy.doctorsCount.replace('{count}', String(doctorCount))
                        }
                        openTodayLabel={branch?.openToday ? copy.openToday : undefined}
                        logoUri={logo?.uri}
                        logoHeaders={logo?.headers}
                        viewLabel={copy.viewClinic}
                        onView={() => router.push(`/places/clinics/${clinic.id}` as Href)}
                      />
                    );
                  })}
                  {clinics.data?.length === 0 ? (
                    <EmptyState title={copy.noClinics} body={copy.loginSubtitle} />
                  ) : null}
                </View>
              ) : null}
            </Animated.View>
          }
          renderItem={({ item, index }) => {
            if (mode !== 'doctors') {
              return null;
            }
            const rating = ratingByDoctor.get(item.doctorId);
            const photo = photoUriFor(item.photoDocumentId, accessToken);
            return (
              <Animated.View entering={theme.reduceMotion ? undefined : FadeInDown.delay(Math.min(index, 6) * 40).duration(220)}>
                <FindDoctorCard
                  name={item.displayName}
                  specialization={item.primarySpecialization}
                  experience={formatYearsExperience(item.yearsOfExperience, copy.yearsExp)}
                  clinicName={item.clinicName}
                  locationLabel={
                    item.compactAddress ||
                    [item.locality || item.addressLine, item.city].filter(Boolean).join(', ') ||
                    null
                  }
                  mapLocation={{
                    name: item.clinicName,
                    address: item.formattedAddress || [item.addressLine, item.city].filter(Boolean).join(', '),
                    latitude: item.latitude,
                    longitude: item.longitude,
                    googlePlaceId: item.googlePlaceId,
                    mapsUrl: item.mapsUrl,
                    directionsUrl: item.directionsUrl,
                  }}
                  photoUri={photo?.uri}
                  photoHeaders={photo?.headers}
                  rating={rating?.ready ? rating.average : undefined}
                  reviewCount={rating?.ready ? rating.count : undefined}
                  favourited={favouriteIds.has(item.doctorId)}
                  onPress={() => router.push(`/practices/${item.doctorId}` as Href)}
                  onToggleFavourite={() => {
                    if (favouriteBusy) {
                      return;
                    }
                    void toggleFavourite(item.doctorId);
                  }}
                />
              </Animated.View>
            );
          }}
          ListEmptyComponent={
            mode === 'doctors' && !loading ? (
              <EmptyState title={copy.noDoctors} body={copy.loginSubtitle} />
            ) : undefined
          }
          ListFooterComponent={
            listError ? <AppText variant="muted">{problemMessage(listError, copy.search)}</AppText> : undefined
          }
        />
        </KeyboardAvoidingView>
      </View>
    </Screen>
  );
}
