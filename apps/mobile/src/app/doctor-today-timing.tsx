import { TodayTimingEditorScreen } from '@/features/providers/today-timing-editor';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorTodayTimingRoute() {
  useRequireSurface('doctor');
  return <TodayTimingEditorScreen />;
}
