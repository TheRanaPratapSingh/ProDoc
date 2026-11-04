import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { Linking } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ResponsiveHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { ClinicReviewSection } from '@/features/reviews/review-list-section';
import { useCopy } from '@/i18n/copy';
import { getDiscoveredClinic } from '@/services/api/provider-api';
import { problemMessage } from '@/utils/problem';

export function PublicClinic({ clinicId }: { clinicId: string }) {
  const copy = useCopy();
  const router = useRouter();
  const clinic = useQuery({
    queryKey: ['discovery', 'clinic', clinicId],
    queryFn: () => getDiscoveredClinic(clinicId),
    enabled: Boolean(clinicId),
  });

  return (
    <AuthScaffold>
      <ResponsiveHeader title={clinic.data?.name ?? copy.clinics} onBack={() => router.back()} backLabel={copy.back} />
      {clinic.data ? (
        <>
          <Card elevated>
            <AppText variant="headline">{clinic.data.name}</AppText>
            <StatusBadge label={`${copy.verified} · ${clinic.data.type}`} tone="primary" />
            {clinic.data.description ? <AppText>{clinic.data.description}</AppText> : null}
            <AppText variant="muted">{clinic.data.specializations.join(', ')}</AppText>
          </Card>
          {clinic.data.facilities.length > 0 ? (
            <Card elevated>
              <AppText variant="label">{copy.facilities}</AppText>
              <AppText>{clinic.data.facilities.join(', ')}</AppText>
            </Card>
          ) : null}
          {clinic.data.branches.map((branch) => (
            <Card key={branch.id} elevated>
              <AppText variant="headline">{branch.name}</AppText>
              <AppText variant="muted">
                {branch.addressLine}, {branch.city} {branch.postalCode}
              </AppText>
              {branch.phoneNumber ? <AppText variant="muted">{branch.phoneNumber}</AppText> : null}
              {branch.openToday ? <StatusBadge label={copy.openToday} tone="success" /> : null}
              <AppText variant="muted">{branch.doctorCount}</AppText>
              {branch.directionsUrl ? (
                <Button label={copy.directions} variant="secondary" onPress={() => void Linking.openURL(branch.directionsUrl!)} />
              ) : null}
            </Card>
          ))}
          <Button label={copy.findDoctor} onPress={() => router.push('/doctors' as Href)} />
          <ClinicReviewSection clinicId={clinic.data.id} />
        </>
      ) : null}
      {clinic.error ? <AppText variant="muted">{problemMessage(clinic.error, copy.clinics)}</AppText> : null}
    </AuthScaffold>
  );
}
