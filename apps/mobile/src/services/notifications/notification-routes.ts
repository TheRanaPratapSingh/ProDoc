/** Allowed in-app paths for push / deep-link navigation. No medical payload is required. */
const routes: Record<string, (subjectId?: string | null) => string> = {
  'appointment.confirmed': () => '/visits',
  'appointment.reminder': () => '/visits',
  'appointment.cancelled': () => '/visits',
  'queue.updated': () => '/queue',
  'queue.called': () => '/queue',
  check_in: () => '/queue',
  'prescription.available': (id) => (id ? `/prescription/${id}` : '/health-records'),
  'payment.status': () => '/visits',
  'payment.paid': () => '/visits',
  'payment.failed': () => '/visits',
  invite: () => '/rewards',
};

export function resolveNotificationRoute(kind: string, subjectId?: string | null): string {
  const matcher = Object.keys(routes).find((key) => kind === key || kind.startsWith(key) || kind.includes(key));
  if (matcher) {
    return routes[matcher]!(subjectId);
  }
  if (kind.startsWith('campaign:')) {
    return '/home';
  }
  return '/home';
}

export function isSafeAppPath(path: string | null | undefined): path is string {
  return !!path && path.startsWith('/') && !path.includes('://');
}
