import { useLocalSearchParams } from 'expo-router';

import { PublicDoctor } from '@/features/providers/public-doctor';

export default function PublicDoctorScreen() {
  const { doctorId, clinicId, scheduleId, branchId, followUpId } = useLocalSearchParams<{
    doctorId: string;
    clinicId?: string;
    scheduleId?: string;
    branchId?: string;
    followUpId?: string;
  }>();
  return (
    <PublicDoctor
      doctorId={doctorId ?? ''}
      preferredClinicId={typeof clinicId === 'string' ? clinicId : null}
      preferredScheduleId={typeof scheduleId === 'string' ? scheduleId : null}
      preferredBranchId={typeof branchId === 'string' ? branchId : null}
      followUpId={typeof followUpId === 'string' ? followUpId : null}
    />
  );
}
