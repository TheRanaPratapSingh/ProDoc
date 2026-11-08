import { ReceptionReviews } from '@/features/reception/reception-modules';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionReviewsRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <ReceptionReviews />;
}
