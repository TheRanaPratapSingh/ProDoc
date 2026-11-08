import { DoctorFollowUpsScreen } from '@/features/clinical/doctor-follow-ups';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorFollowUpsRoute() {
  useRequireSurface('doctor');
  return <DoctorFollowUpsScreen />;
}
