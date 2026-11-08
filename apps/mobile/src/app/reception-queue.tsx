import { ReceptionQueueScreen } from '@/features/reception/reception-queue';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionQueueRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <ReceptionQueueScreen />;
}
