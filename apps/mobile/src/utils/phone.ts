/** Strip non-digits from any phone input (paste-safe). */
export function digitsOnly(input: string): string {
  return input.replace(/\D/g, '');
}

/**
 * Normalize typed/pasted Indian mobile input to at most 10 national digits.
 * Accepts +91 / 91 / 0 prefixes, then caps at 10 digits for the field.
 */
export function nationalMobileDigits(input: string): string {
  let digits = digitsOnly(input);
  if (digits.length >= 12 && digits.startsWith('91')) {
    digits = digits.slice(2);
  } else if (digits.length >= 11 && digits.startsWith('0')) {
    digits = digits.slice(1);
  }

  return digits.slice(0, 10);
}

/** Returns E.164 (+91XXXXXXXXXX) or null when invalid. */
export function normalizeIndianMobile(input: string): string | null {
  const digits = digitsOnly(input);
  let national = digits;
  if (national.length === 12 && national.startsWith('91')) {
    national = national.slice(2);
  } else if (national.length === 11 && national.startsWith('0')) {
    national = national.slice(1);
  }

  if (national.length !== 10 || national[0] < '6' || national[0] > '9') {
    return null;
  }

  return `+91${national}`;
}
