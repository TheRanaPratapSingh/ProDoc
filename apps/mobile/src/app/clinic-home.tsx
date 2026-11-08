import { ClinicHome } from '@/features/account/clinic-home';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ClinicHomeRoute() {
  const { ready } = useRequireSurface('clinic');
  if (!ready) {
    return null;
  }
  return <ClinicHome />;
}
