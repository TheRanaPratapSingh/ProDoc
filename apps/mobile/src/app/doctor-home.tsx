import { DoctorHome } from '@/features/account/doctor-home';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorHomeRoute() {
  const { ready } = useRequireSurface('doctor');
  if (!ready) {
    return null;
  }
  return <DoctorHome />;
}
