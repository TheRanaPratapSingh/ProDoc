import { DoctorSettingsScreen } from '@/features/account/doctor-settings';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorSettingsRoute() {
  useRequireSurface('doctor');
  return <DoctorSettingsScreen />;
}