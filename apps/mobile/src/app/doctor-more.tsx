import { DoctorMoreScreen } from '@/features/account/doctor-more';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorMoreRoute() {
  useRequireSurface('doctor');
  return <DoctorMoreScreen />;
}
