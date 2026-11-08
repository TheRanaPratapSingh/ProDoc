import { useLocalSearchParams } from 'expo-router';

import { BookVisit } from '@/features/appointments/book-visit';

export default function BookScreen() {
  const { scheduleId, doctorId, branchId, date, start, followUpId } = useLocalSearchParams<{
    scheduleId: string;
    doctorId?: string;
    branchId?: string;
    date?: string;
    start?: string;
    followUpId?: string;
  }>();
  return (
    <BookVisit
      scheduleId={scheduleId ?? ''}
      doctorId={doctorId ?? ''}
      branchId={branchId ?? ''}
      initialDate={typeof date === 'string' ? date : undefined}
      initialStart={typeof start === 'string' ? start : undefined}
      followUpId={typeof followUpId === 'string' ? followUpId : undefined}
    />
  );
}
