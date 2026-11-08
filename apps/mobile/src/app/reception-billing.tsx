import { ReceptionBilling } from '@/features/reception/reception-billing';
import { useRequireSurface } from '@/navigation/use-require-surface';

export default function ReceptionBillingRoute() {
  const { ready } = useRequireSurface('reception');
  if (!ready) return null;
  return <ReceptionBilling />;
}
