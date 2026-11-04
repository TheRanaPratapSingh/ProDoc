import { useLocalSearchParams } from 'expo-router';

import { PublicDoctor } from '@/features/providers/public-doctor';

export default function PublicDoctorScreen() {
  const { doctorId } = useLocalSearchParams<{ doctorId: string }>();
  return <PublicDoctor doctorId={doctorId ?? ''} />;
}
