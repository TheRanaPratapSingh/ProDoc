import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { isExpired, isOfflineActionBlocked, makeEnvelope } from '../offline/cache-policy.ts';
import { isSafeAppPath, resolveNotificationRoute } from './notification-routes.ts';

describe('deep links', () => {
  it('maps notification kinds to safe in-app routes', () => {
    assert.equal(resolveNotificationRoute('appointment.confirmed', 'abc'), '/visits');
    assert.equal(resolveNotificationRoute('queue.called'), '/queue');
    assert.equal(
      resolveNotificationRoute('prescription.available', '11111111-1111-1111-1111-111111111111'),
      '/prescription/11111111-1111-1111-1111-111111111111',
    );
    assert.equal(resolveNotificationRoute('payment.paid'), '/visits');
  });

  it('rejects unsafe external paths', () => {
    assert.equal(isSafeAppPath('https://evil.example/records.pdf'), false);
    assert.equal(isSafeAppPath('/visits'), true);
  });
});

describe('offline policy', () => {
  it('blocks bookings and payments offline', () => {
    assert.equal(isOfflineActionBlocked('booking.confirm'), true);
    assert.equal(isOfflineActionBlocked('payment.verify'), true);
    assert.equal(isOfflineActionBlocked('draft.review'), false);
  });

  it('expires cache envelopes', () => {
    const envelope = makeEnvelope('public', 1000, { ok: true }, 0);
    assert.equal(isExpired(envelope.expiresAtUtc, 500), false);
    assert.equal(isExpired(envelope.expiresAtUtc, 1001), true);
  });
});
