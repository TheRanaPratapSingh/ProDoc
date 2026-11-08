import { DoctorAppointmentDetailScreen } from '@/features/appointments/doctor-appointment-detail';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorAppointmentRoute() {
  useRequireSurface('doctor');
  return <DoctorAppointmentDetailScreen />;
}
