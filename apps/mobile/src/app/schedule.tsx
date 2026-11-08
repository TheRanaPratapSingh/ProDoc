import { ScheduleEditor } from '@/features/providers/schedule-editor';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ScheduleScreen() {
  useRequireSurface('doctor');
  return <ScheduleEditor surface="doctor" />;
}
