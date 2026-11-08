import assert from 'node:assert/strict';
import { test } from 'node:test';

import { digitsOnly, nationalMobileDigits, normalizeIndianMobile } from './phone.ts';

test('indian mobile numbers normalize to E.164', () => {
  assert.equal(normalizeIndianMobile('9876543210'), '+919876543210');
  assert.equal(normalizeIndianMobile('09876543210'), '+919876543210');
  assert.equal(normalizeIndianMobile('+91 98765 43210'), '+919876543210');
  assert.equal(normalizeIndianMobile('919876543210'), '+919876543210');
});

test('invalid numbers are rejected including 9 and 11+ digit lengths', () => {
  assert.equal(normalizeIndianMobile('5876543210'), null);
  assert.equal(normalizeIndianMobile('12345'), null);
  assert.equal(normalizeIndianMobile(''), null);
  assert.equal(normalizeIndianMobile('987654321'), null); // 9 digits
  assert.equal(normalizeIndianMobile('98765432101'), null); // 11 digits
  assert.equal(normalizeIndianMobile('9198765432101'), null); // oversized
});

test('nationalMobileDigits strips and caps at 10 for paste-safe inputs', () => {
  assert.equal(nationalMobileDigits('+91 98765 43210'), '9876543210');
  assert.equal(nationalMobileDigits('09876543210'), '9876543210');
  assert.equal(nationalMobileDigits('98765432101234'), '9876543210');
  assert.equal(digitsOnly('a9b8c7'), '987');
});
