import { ReceptionFollowUpsScreen } from '@/features/reception/reception-follow-ups';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionFollowUpsRoute() {
  useRequireSurface('reception');
  return <ReceptionFollowUpsScreen />;
}
