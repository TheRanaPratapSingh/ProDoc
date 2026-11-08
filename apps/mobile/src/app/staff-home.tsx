import { StaffHome } from '@/features/account/staff-home';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function StaffHomeRoute() {
  const { ready } = useRequireSurface('staff');
  if (!ready) {
    return null;
  }
  return <StaffHome />;
}
