import { ScheduleEditor } from '@/features/providers/schedule-editor';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionAvailabilityRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <ScheduleEditor surface="reception" />;
}
