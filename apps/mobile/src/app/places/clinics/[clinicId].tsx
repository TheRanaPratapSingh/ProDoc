import { useLocalSearchParams } from 'expo-router';

import { PublicClinic } from '@/features/providers/public-clinic';

export default function PublicClinicScreen() {
  const { clinicId } = useLocalSearchParams<{ clinicId: string }>();
  return <PublicClinic clinicId={clinicId ?? ''} />;
}
