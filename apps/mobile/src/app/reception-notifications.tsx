import { ReceptionNotifications } from '@/features/reception/reception-modules';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionNotificationsRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <ReceptionNotifications />;
}
