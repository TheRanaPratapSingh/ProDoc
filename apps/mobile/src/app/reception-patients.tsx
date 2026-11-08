import { ReceptionPatients } from '@/features/reception/reception-patients';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionPatientsRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <ReceptionPatients />;
}
