import { ReceptionHome } from '@/features/reception/reception-home';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) {
    return null;
  }
  return <ReceptionHome />;
}
