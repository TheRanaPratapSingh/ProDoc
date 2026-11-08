import { DoctorOnboarding } from '@/features/providers/doctor-onboarding';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function DoctorScreen() {
  useRequireSurface('doctor');
  return <DoctorOnboarding />;
}
