export function normalizeIndianMobile(input: string): string | null {
  const digits = input.replace(/\D/g, '');
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
