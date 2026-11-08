import { ReceptionAppointments } from '@/features/reception/reception-appointments';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionAppointmentsRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <ReceptionAppointments />;
}
