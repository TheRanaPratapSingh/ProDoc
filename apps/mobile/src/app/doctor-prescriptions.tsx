import { DoctorPrescriptionsScreen } from '@/features/clinical/doctor-prescriptions';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorPrescriptionsRoute() {
  useRequireSurface('doctor');
  return <DoctorPrescriptionsScreen />;
}
