import { ReceptionDoctors } from '@/features/reception/reception-modules';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionDoctorsRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <ReceptionDoctors />;
}
