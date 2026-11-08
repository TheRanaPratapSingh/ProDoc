import type { Href } from 'expo-router';

import { RoleDashboard } from '@/features/account/role-dashboard';
import { useCopy } from '@/i18n/copy';
import { useAccountPermissions } from '@/stores/session-store';
import { canCreateClinic } from '@/utils/admin-permissions';

export function ClinicHome() {
  const copy = useCopy();
  const permissions = useAccountPermissions();
  const allowCreate = canCreateClinic(permissions);

  const links: { label: string; href: Href; description?: string }[] = [
    {
      label: copy.practiceVisits,
      href: '/practice',
      description: copy.clinicDashboardCalendarHint,
    },
    {
      label: copy.receptionDesk,
      href: '/reception',
      description: copy.clinicDashboardReceptionHint,
    },
    {
      label: copy.queueMonitor,
      href: '/queue-monitor',
    },
    {
      label: copy.scheduleTitle,
      href: '/schedule',
    },
  ];

  if (allowCreate) {
    links.push({
      label: copy.createClinic,
      href: '/clinic',
      description: copy.clinicDashboardOnboardingHint,
    });
  } else if (permissions.includes('provider.onboard')) {
    links.push({
      label: copy.clinicProfile,
      href: '/clinic',
      description: copy.clinicDashboardOnboardingHint,
    });
  }

  links.push(
    {
      label: copy.notifications,
      href: '/notifications',
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
  );

  return (
    <RoleDashboard
      surface="clinic"
      title={copy.clinicDashboardTitle}
      subtitle={copy.clinicDashboardBody}
      links={links}
    />
  );
}
