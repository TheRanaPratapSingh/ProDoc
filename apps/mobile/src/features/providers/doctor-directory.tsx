import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useEffect, useState } from 'react';
import { Linking, Platform, View } from 'react-native';

import { AppShell } from '@/components/layout/app-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ClinicCard, DoctorCard } from '@/components/ui/doctor-card';
import { EmptyState } from '@/components/ui/empty-state';
import { SearchBar } from '@/components/ui/search-bar';
import { CardSkeleton } from '@/components/ui/skeleton';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { discoverClinics, discoverPlaces, getSpecializations, type DiscoveryFilters } from '@/services/api/provider-api';
import { readDiscoveryCache } from '@/services/offline/query-cache';
import { selectIsEffectivelyOnline, useNetworkStore } from '@/stores/network-store';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type Mode = 'doctors' | 'clinics';

export function DoctorDirectory() {
  const copy = useCopy();
  const router = useRouter();
  const [mode, setMode] = useState<Mode>('doctors');
  const [query, setQuery] = useState('');
  const [city, setCity] = useState('');
  const [postalCode, setPostalCode] = useState('');
  const [maxFee, setMaxFee] = useState('');
  const [latitude, setLatitude] = useState('');
  const [longitude, setLongitude] = useState('');
  const [radiusKm, setRadiusKm] = useState('25');
  const [specializationId, setSpecializationId] = useState('');
  const [availability, setAvailability] = useState('');
  const [sort, setSort] = useState('recommended');
  const [locationNote, setLocationNote] = useState('');
  const [recent, setRecent] = useState<string[]>([]);
  const [filters, setFilters] = useState<DiscoveryFilters>({});
  const online = useNetworkStore(selectIsEffectivelyOnline);
  const [cachedDoctors, setCachedDoctors] = useState<Awaited<ReturnType<typeof discoverPlaces>> | null>(null);
  const [cachedClinics, setCachedClinics] = useState<Awaited<ReturnType<typeof discoverClinics>> | null>(null);

  useEffect(() => {
    void readDiscoveryCache<Awaited<ReturnType<typeof discoverPlaces>>>('doctors', filters).then(setCachedDoctors);
    void readDiscoveryCache<Awaited<ReturnType<typeof discoverClinics>>>('clinics', filters).then(setCachedClinics);
  }, [filters]);

  const specializations = useQuery({ queryKey: ['specializations'], queryFn: getSpecializations });
  const doctors = useQuery({
    queryKey: ['discovery', 'doctors', filters],
    queryFn: () => discoverPlaces(filters),
    enabled: mode === 'doctors',
    placeholderData: cachedDoctors ?? undefined,
  });
  const clinics = useQuery({
    queryKey: ['discovery', 'clinics', filters],
    queryFn: () => discoverClinics(filters),
    enabled: mode === 'clinics',
    placeholderData: cachedClinics ?? undefined,
  });

  const search = (nextQuery = query) => {
    const text = nextQuery.trim();
    if (text) {
      setRecent((current) => [text, ...current.filter((item) => item !== text)].slice(0, 5));
    }
    setFilters({
      query: text || undefined,
      city: city.trim() || undefined,
      postalCode: postalCode.trim() || undefined,
      maxFee: maxFee.trim() || undefined,
      latitude: latitude.trim() || undefined,
      longitude: longitude.trim() || undefined,
      radiusKm: latitude.trim() && longitude.trim() ? radiusKm.trim() || '25' : undefined,
      specializationId: specializationId || undefined,
      availability: availability || undefined,
      sort,
    });
  };

  const useLocation = () => {
    const geolocation = Platform.OS === 'web' && typeof navigator !== 'undefined' ? navigator.geolocation : undefined;
    if (!geolocation) {
      setLocationNote(copy.locationDenied);
      return;
    }
    geolocation.getCurrentPosition(
      (position) => {
        setLatitude(position.coords.latitude.toFixed(5));
        setLongitude(position.coords.longitude.toFixed(5));
        setLocationNote('');
      },
      () => setLocationNote(copy.locationDenied),
      { enableHighAccuracy: false, maximumAge: 60000, timeout: 8000 },
    );
  };

  return (
    <AppShell active="doctors">
    <AuthScaffold showHero={false}>
      <Button label={copy.back} variant="ghost" onPress={() => router.back()} />
      <AppText variant="title">{copy.findDoctor}</AppText>
      {!online ? <AppText variant="muted">{copy.offlineCachedData}</AppText> : null}
      <Segmented
        value={mode}
        options={[
          { value: 'doctors', label: copy.doctors },
          { value: 'clinics', label: copy.clinics },
        ]}
        onChange={setMode}
      />
      <SearchBar
        placeholder={copy.search}
        value={query}
        onChangeText={setQuery}
        onSubmitEditing={() => search()}
        returnKeyType="search"
      />
      <Card>
        <TextField label={copy.search} value={query} onChangeText={setQuery} />
        <TextField label={copy.city} value={city} onChangeText={setCity} />
        <TextField label={copy.postalCode} value={postalCode} onChangeText={setPostalCode} keyboardType="number-pad" />
        {mode === 'doctors' ? <TextField label={copy.maxFee} value={maxFee} onChangeText={setMaxFee} keyboardType="decimal-pad" /> : null}
        <TextField label={copy.latitude} value={latitude} onChangeText={setLatitude} keyboardType="decimal-pad" />
        <TextField label={copy.longitude} value={longitude} onChangeText={setLongitude} keyboardType="decimal-pad" />
        <TextField label={copy.radius} value={radiusKm} onChangeText={setRadiusKm} keyboardType="decimal-pad" />
        <Button label={copy.useLocation} variant="secondary" onPress={useLocation} />
        {locationNote ? <AppText variant="muted">{locationNote}</AppText> : null}
        <AppText variant="label">{copy.specialization}</AppText>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          <Button label={copy.anySpecialization} variant={specializationId === '' ? 'primary' : 'secondary'} onPress={() => setSpecializationId('')} />
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
            <Button key={value || 'any'} label={label} variant={availability === value ? 'primary' : 'secondary'} onPress={() => setAvailability(value)} />
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
            <Button key={value} label={label} variant={sort === value ? 'primary' : 'secondary'} onPress={() => setSort(value)} />
          ))}
        </View>
        <Button label={copy.search} onPress={() => search()} />
        {recent.length > 0 ? <AppText variant="label">{copy.recentSearches}</AppText> : null}
        {recent.map((item) => (
          <Button key={item} label={item} variant="secondary" onPress={() => { setQuery(item); search(item); }} />
        ))}
      </Card>
      {(mode === 'doctors' ? doctors.isLoading : clinics.isLoading) && !(mode === 'doctors' ? doctors.data : clinics.data) ? (
        <Card>
          <CardSkeleton />
        </Card>
      ) : null}
      <View style={{ gap: spacing.md }}>
        {mode === 'doctors'
          ? (doctors.data ?? []).map((place) => (
              <DoctorCard
                key={place.scheduleId}
                name={place.displayName}
                specialization={place.primarySpecialization}
                experience={place.yearsOfExperience}
                fee={place.consultationFee}
                location={[place.clinicName, place.branchName, place.city, place.postalCode].filter(Boolean).join(' · ')}
                distance={place.distanceKm === null ? null : `${place.distanceKm.toFixed(1)} ${copy.distance}`}
                verifiedLabel={copy.verified}
                feeLabel={copy.consultationFee}
                viewLabel={copy.viewProfile}
                directionsLabel={copy.directions}
                onView={() => router.push(`/practices/${place.doctorId}` as Href)}
                onDirections={place.directionsUrl ? () => void Linking.openURL(place.directionsUrl!) : undefined}
              />
            ))
          : (clinics.data ?? []).map((clinic) => (
              <ClinicCard
                key={clinic.id}
                name={clinic.name}
                type={clinic.type}
                verifiedLabel={copy.verified}
                specializations={clinic.specializations.join(', ')}
                branches={clinic.branches.map(
                  (branch) =>
                    `${branch.name} · ${branch.city} ${branch.postalCode}${branch.openToday ? ` · ${copy.openToday}` : ''}${
                      branch.doctorCount ? ` · ${branch.doctorCount}` : ''
                    }`,
                )}
                viewLabel={copy.viewProfile}
                onView={() => router.push(`/places/clinics/${clinic.id}` as Href)}
              />
            ))}
      </View>
      {(mode === 'doctors' ? doctors.data : clinics.data)?.length === 0 ? (
        <EmptyState title={copy.noDoctors} body={copy.loginSubtitle} />
      ) : null}
      {doctors.error || clinics.error ? (
        <AppText variant="muted">{problemMessage(doctors.error ?? clinics.error, copy.search)}</AppText>
      ) : null}
    </AuthScaffold>
    </AppShell>
  );
}
