import { z } from 'zod';

import {
  ageYearsSchema,
  genderOptionalSchema,
  indianMobileSchema,
  otpCodeSchema,
  personNameSchema,
} from '@/schemas/common';

export const phoneSchema = z.object({
  phoneNumber: indianMobileSchema,
});

export const otpSchema = z.object({
  code: otpCodeSchema,
});

/** Profile edit — name is mandatory when saving from account settings. */
export const profileSchema = z.object({
  fullName: personNameSchema,
});

/** First-time patient profile completion — Name + Age mandatory, Gender optional. */
export const completePatientProfileSchema = z.object({
  fullName: personNameSchema,
  ageYears: ageYearsSchema,
  gender: genderOptionalSchema,
});

export type PhoneFormValues = z.infer<typeof phoneSchema>;
export type OtpFormValues = z.infer<typeof otpSchema>;
export type ProfileFormValues = z.infer<typeof profileSchema>;
export type CompletePatientProfileValues = z.infer<typeof completePatientProfileSchema>;
