import { z } from 'zod';

import { normalizeIndianMobile } from '@/utils/phone';

export const phoneSchema = z.object({
  phoneNumber: z
    .string()
    .trim()
    .refine((value) => normalizeIndianMobile(value) !== null, 'Enter a valid Indian mobile number.'),
});

export const otpSchema = z.object({
  code: z.string().trim().regex(/^\d{6}$/, 'Enter the 6-digit code.'),
});

export const profileSchema = z.object({
  fullName: z
    .string()
    .trim()
    .refine(
      (value) => value.length === 0 || (/^[\p{L}\p{M}][\p{L}\p{M} .'-]{1,79}$/u.test(value) && value.length <= 80),
      'Enter a name using letters, spaces, apostrophes, or hyphens.',
    ),
});

export type PhoneFormValues = z.infer<typeof phoneSchema>;
export type OtpFormValues = z.infer<typeof otpSchema>;
export type ProfileFormValues = z.infer<typeof profileSchema>;
