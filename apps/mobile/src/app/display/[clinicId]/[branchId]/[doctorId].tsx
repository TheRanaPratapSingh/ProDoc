import { useLocalSearchParams } from 'expo-router';

import { WaitingDisplay } from '@/features/queue/waiting-display';

export default function DisplayRoute() {
  const { clinicId, branchId, doctorId } = useLocalSearchParams<{ clinicId: string; branchId: string; doctorId: string }>();
  return <WaitingDisplay clinicId={clinicId ?? ''} branchId={branchId ?? ''} doctorId={doctorId ?? ''} />;
}
