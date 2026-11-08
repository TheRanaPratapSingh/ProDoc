import { DoctorNotificationsScreen } from '@/features/account/doctor-notifications';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorNotificationsRoute() {
  useRequireSurface('doctor');
  return <DoctorNotificationsScreen />;
}
