import { DoctorShell } from '@/components/layout/doctor-shell';
import { NotificationCenter } from '@/features/notifications/notification-center';
import { useCopy } from '@/i18n/copy';

export function DoctorNotificationsScreen() {
  const copy = useCopy();
  return (
    <NotificationCenter
      title={copy.doctorNotificationsTitle}
      shell={(children) => <DoctorShell active="notifications">{children}</DoctorShell>}
    />
  );
}
