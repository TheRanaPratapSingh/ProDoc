import { DoctorScheduleHub } from '@/features/appointments/doctor-schedule-hub';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorScheduleRoute() {
  useRequireSurface('doctor');
  return <DoctorScheduleHub />;
}
