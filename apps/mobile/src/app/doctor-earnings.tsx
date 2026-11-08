import { DoctorEarningsScreen } from '@/features/billing/doctor-earnings';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorEarningsRoute() {
  useRequireSurface('doctor');
  return <DoctorEarningsScreen />;
}
