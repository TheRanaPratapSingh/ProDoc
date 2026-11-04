import { useLocalSearchParams } from 'expo-router';

import { CheckInQr } from '@/features/queue/check-in-qr';

export default function CheckInScreen() {
  const { appointmentId } = useLocalSearchParams<{ appointmentId: string }>();
  return <CheckInQr appointmentId={appointmentId ?? ''} />;
}
