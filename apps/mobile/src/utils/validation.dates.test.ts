import {
  parseDateOnly,
  validateFollowUpDate,
  validateFutureAppointmentDate,
  validateInclusiveDateRange,
} from '@/utils/validation';

describe('date validation helpers', () => {
  it('rejects invalid calendar dates without throwing', () => {
    expect(parseDateOnly('2026-02-31')).toBeNull();
    expect(parseDateOnly('not-a-date')).toBeNull();
  });

  it('rejects start after end', () => {
    const result = validateInclusiveDateRange('2026-11-10', '2026-11-01');
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.code).toBe('date_range_invalid');
    }
  });

  it('rejects past appointment dates', () => {
    const today = new Date(2026, 10, 8);
    const result = validateFutureAppointmentDate('2026-11-01', today);
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.code).toBe('appointment_date_past');
    }
  });

  it('requires follow-up after today', () => {
    const today = new Date(2026, 10, 8);
    expect(validateFollowUpDate('2026-11-08', today).ok).toBe(false);
    expect(validateFollowUpDate('2026-11-09', today).ok).toBe(true);
  });
});
