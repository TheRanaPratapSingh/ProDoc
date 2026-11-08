import { Redirect } from 'expo-router';

import { useRequireSurface } from '@/navigation/use-require-surface';

/** Legacy calendar route — appointment calendar lives inside Appointments & Queue. */
export default function ReceptionCalendarRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <Redirect href="/reception-queue" />;
}
