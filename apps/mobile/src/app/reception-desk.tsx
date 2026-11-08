import { ReceptionDesk } from '@/features/queue/reception-desk';
import { ReceptionShell } from '@/components/layout/reception-shell';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionDeskRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) {
    return null;
  }
  return (
    <ReceptionShell active="desk">
      <ReceptionDesk />
    </ReceptionShell>
  );
}
