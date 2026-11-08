import { DoctorScheduleHub } from '@/features/appointments/doctor-schedule-hub';
import { useRequireSurface } from '@/navigation/use-require-surface';

/** Keep legacy route; Schedule tab now uses the hub. */
export default function DoctorCalendarRoute() {
  useRequireSurface('doctor');
  return <DoctorScheduleHub />;
}
