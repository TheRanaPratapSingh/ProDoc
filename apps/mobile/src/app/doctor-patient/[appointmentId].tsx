import { DoctorPatientProfileScreen } from '@/features/account/doctor-patient-profile';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorPatientRoute() {
  useRequireSurface('doctor');
  return <DoctorPatientProfileScreen />;
}
