import { ReceptionSettings } from '@/features/reception/reception-modules';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionSettingsRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <ReceptionSettings />;
}
