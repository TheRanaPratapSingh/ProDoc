import { AdminControlCenter } from '@/features/admin/admin-control-center';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function AdminRoute() {
  const { ready } = useRequireSurface('admin', '/admin-sign-in');
  if (!ready) {
    return null;
  }
  return <AdminControlCenter />;
}
