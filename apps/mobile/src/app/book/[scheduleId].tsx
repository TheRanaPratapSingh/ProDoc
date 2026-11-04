import { useLocalSearchParams } from 'expo-router';

import { BookVisit } from '@/features/appointments/book-visit';

export default function BookScreen() {
  const { scheduleId, doctorId, branchId } = useLocalSearchParams<{ scheduleId: string; doctorId?: string; branchId?: string }>();
  return <BookVisit scheduleId={scheduleId ?? ''} doctorId={doctorId ?? ''} branchId={branchId ?? ''} />;
}
