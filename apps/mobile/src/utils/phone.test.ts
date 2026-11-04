import assert from 'node:assert/strict';
import { test } from 'node:test';

import { normalizeIndianMobile } from './phone.ts';

test('indian mobile numbers normalize to E.164', () => {
  assert.equal(normalizeIndianMobile('9876543210'), '+919876543210');
  assert.equal(normalizeIndianMobile('09876543210'), '+919876543210');
  assert.equal(normalizeIndianMobile('+91 98765 43210'), '+919876543210');
  assert.equal(normalizeIndianMobile('919876543210'), '+919876543210');
});

test('invalid numbers are rejected', () => {
  assert.equal(normalizeIndianMobile('5876543210'), null);
  assert.equal(normalizeIndianMobile('12345'), null);
  assert.equal(normalizeIndianMobile(''), null);
});
