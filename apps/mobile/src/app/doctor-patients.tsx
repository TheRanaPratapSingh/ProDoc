import { DoctorPatientsScreen } from '@/features/account/doctor-patients';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorPatientsRoute() {
  useRequireSurface('doctor');
  return <DoctorPatientsScreen />;
}
