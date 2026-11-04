import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'expo-router';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { listAdminAppointments } from '@/services/api/provider-api';
import { problemMessage } from '@/utils/problem';

function localDate(offset = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offset);
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function AppointmentActivity() {
  const copy = useCopy();
  const router = useRouter();
  const from = localDate(0);
  const to = localDate(7);
  const activity = useQuery({
    queryKey: ['admin', 'appointments', from],
    queryFn: () => listAdminAppointments(from, to),
  });

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="secondary" onPress={() => router.back()} />
      <AppText variant="title">{copy.appointmentActivity}</AppText>
      {(activity.data ?? []).map((visit) => (
        <Card key={visit.id}>
          <AppText>{visit.reference ?? visit.id}</AppText>
          <AppText variant="muted">{visit.status}</AppText>
          <AppText>{[visit.doctorName, visit.clinicName].join(' · ')}</AppText>
          <AppText>{new Date(visit.startsAtUtc).toLocaleString()}</AppText>
          <AppText>
            {visit.currency} {visit.consultationFee}
          </AppText>
        </Card>
      ))}
      {activity.data?.length === 0 ? <AppText variant="muted">{copy.noVisits}</AppText> : null}
      {activity.error ? <AppText variant="muted">{problemMessage(activity.error, copy.appointmentActivity)}</AppText> : null}
    </AuthScaffold>
  );
}
