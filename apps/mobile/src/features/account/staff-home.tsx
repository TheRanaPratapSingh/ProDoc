import { RoleDashboard } from '@/features/account/role-dashboard';
import { useCopy } from '@/i18n/copy';

export function StaffHome() {
  const copy = useCopy();
  return (
    <RoleDashboard
      surface="staff"
      title={copy.staffDashboardTitle}
      subtitle={copy.staffDashboardBody}
      links={[
        {
          label: copy.queueMonitor,
          href: '/queue-monitor',
          description: copy.staffDashboardQueueHint,
        },
        {
          label: copy.practiceVisits,
          href: '/practice',
          description: copy.staffDashboardCalendarHint,
        },
        {
          label: copy.assistantTitle,
          href: '/assistant',
          description: copy.staffDashboardAssistantHint,
        },
        {
          label: copy.profileMenuHelp,
          href: '/help',
          description: copy.profileMenuHelpBody,
        },
        {
          label: copy.navProfile,
          href: '/home?tab=profile',
        },
      ]}
    />
  );
}
