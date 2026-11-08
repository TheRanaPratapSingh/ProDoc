import { ReceptionClinicInfoScreen } from '@/features/reception/reception-modules';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionClinicRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <ReceptionClinicInfoScreen />;
}
