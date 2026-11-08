import { z } from 'zod';

import { normalizeIndianMobile } from '@/utils/phone';

/** Shared user-facing validation copy (keep in sync across modules). */
export const ValidationMessages = {
  required: 'This field is required.',
  selectRequired: 'Please select a required option.',
  mobile: 'Please enter a valid mobile number.',
  mobileMandatory: 'Mobile number is mandatory.',
  email: 'Please enter a valid email address.',
  emailMandatory: 'Email is mandatory.',
  otpRequired: 'OTP is mandatory.',
  otpInvalid: 'Please enter a valid OTP.',
  name: 'Enter a name using letters, spaces, apostrophes, or hyphens.',
  nameMandatory: 'Name is mandatory.',
  patientNameMandatory: 'Patient name is mandatory.',
  nameShort: 'Enter a name with at least 2 characters.',
  nameLong: 'Name must be 80 characters or fewer.',
  ageMandatory: 'Age is mandatory.',
  ageInvalid: 'Please enter a valid age.',
  addressMandatory: 'Address is mandatory.',
  clinicNameMandatory: 'Clinic name is mandatory.',
  doctorNameMandatory: 'Doctor name is mandatory.',
  registrationMandatory: 'Registration number is mandatory.',
  dobMandatory: 'Date of birth is mandatory.',
  date: 'Please enter a valid date.',
  dateMandatory: 'Date is mandatory.',
  pastDate: 'Past dates are not allowed.',
  futureDate: 'Please select a valid future date.',
  dob: 'Please enter a valid date of birth.',
  time: 'Please enter a valid time.',
  timeMandatory: 'Time is mandatory.',
  password: 'Password must be at least 8 characters.',
  passwordMandatory: 'Password is mandatory.',
  amount: 'Please enter a valid amount.',
  fee: 'Please enter a valid fee amount.',
  pincode: 'Please enter a valid 6-digit PIN code.',
  experience: 'Enter years of experience as a whole number (0–70).',
  graduationYear: 'Enter a valid graduation year.',
  slotDuration: 'Enter a slot length between 5 and 120 minutes.',
  buffer: 'Enter a buffer between 0 and 60 minutes.',
} as const;

export const requiredStringSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.required);

export const indianMobileSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.mobileMandatory)
  .refine((value) => normalizeIndianMobile(value) !== null, ValidationMessages.mobile);

export const indianMobileOptionalSchema = z
  .string()
  .trim()
  .refine(
    (value) => value.length === 0 || normalizeIndianMobile(value) !== null,
    ValidationMessages.mobile,
  );

export const emailOptionalSchema = z
  .string()
  .trim()
  .refine(
    (value) => value.length === 0 || z.string().email().safeParse(value).success,
    ValidationMessages.email,
  );

export const emailRequiredSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.emailMandatory)
  .email(ValidationMessages.email);

export const otpCodeSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.otpRequired)
  .regex(/^\d{6}$/, ValidationMessages.otpInvalid);

export const personNameSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.nameMandatory)
  .min(2, ValidationMessages.nameShort)
  .max(80, ValidationMessages.nameLong)
  .regex(/^[\p{L}\p{M}][\p{L}\p{M} .'-]{0,79}$/u, ValidationMessages.name);

/** Patient display name on appointment booking (Myself / family). */
export const patientBookingNameSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.patientNameMandatory)
  .min(2, ValidationMessages.nameShort)
  .max(80, ValidationMessages.nameLong)
  .regex(/^[\p{L}\p{M}][\p{L}\p{M} .'-]{0,79}$/u, ValidationMessages.name);

/** Completed years of age (1–120). */
export const ageYearsSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.ageMandatory)
  .refine((value) => /^\d{1,3}$/.test(value), ValidationMessages.ageInvalid)
  .refine((value) => {
    const age = Number(value);
    return Number.isInteger(age) && age >= 1 && age <= 120;
  }, ValidationMessages.ageInvalid);

export const genderOptionalSchema = z.enum(['male', 'female', 'other']).optional().nullable();

export const personNameOptionalSchema = z
  .string()
  .trim()
  .refine(
    (value) =>
      value.length === 0 ||
      (/^[\p{L}\p{M}][\p{L}\p{M} .'-]{1,79}$/u.test(value) && value.length <= 80),
    ValidationMessages.name,
  );

export const amountSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.required)
  .refine((value) => {
    const amount = Number(value);
    return Number.isFinite(amount) && amount >= 0 && amount <= 1_000_000;
  }, ValidationMessages.amount);

export const feeSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.required)
  .refine((value) => {
    const amount = Number(value);
    return Number.isFinite(amount) && amount >= 0 && amount <= 1_000_000;
  }, ValidationMessages.fee);

export const feeOptionalSchema = z
  .string()
  .trim()
  .refine((value) => {
    if (!value) return true;
    const amount = Number(value);
    return Number.isFinite(amount) && amount >= 0 && amount <= 1_000_000;
  }, ValidationMessages.fee);

export const isoDateSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.required)
  .regex(/^\d{4}-\d{2}-\d{2}$/, ValidationMessages.date)
  .refine((value) => {
    const date = new Date(`${value}T00:00:00`);
    return !Number.isNaN(date.getTime());
  }, ValidationMessages.date);

/** Accepts YYYY-MM-DD or empty; does not force optional DOB fields to become required. */
export const dateOfBirthOptionalSchema = z
  .string()
  .trim()
  .refine((value) => {
    if (!value) return true;
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
    const date = new Date(`${value}T00:00:00Z`);
    if (Number.isNaN(date.getTime())) return false;
    const now = new Date();
    return date.getUTCFullYear() >= 1900 && date.getTime() <= now.getTime();
  }, ValidationMessages.dob);

export const futureIsoDateSchema = isoDateSchema.refine((value) => {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const date = new Date(`${value}T00:00:00`);
  return date.getTime() >= today.getTime();
}, ValidationMessages.pastDate);

export const timeSlotSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.time)
  .refine((value) => /^\d{2}:\d{2}(:\d{2})?$/.test(value), ValidationMessages.time);

export const pincodeSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.required)
  .regex(/^[1-9][0-9]{5}$/, ValidationMessages.pincode);

export const pincodeOptionalSchema = z
  .string()
  .trim()
  .refine((value) => value.length === 0 || /^[1-9][0-9]{5}$/.test(value), ValidationMessages.pincode);

export const passwordSchema = z
  .string()
  .min(1, ValidationMessages.required)
  .min(8, ValidationMessages.password);

export const yearsOfExperienceSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.required)
  .refine((value) => {
    if (!/^\d{1,2}$/.test(value)) return false;
    const years = Number(value);
    return Number.isInteger(years) && years >= 0 && years <= 70;
  }, ValidationMessages.experience);

export const graduationYearSchema = z
  .string()
  .trim()
  .refine((value) => {
    if (!value) return true;
    if (!/^\d{4}$/.test(value)) return false;
    const year = Number(value);
    const current = new Date().getFullYear();
    return year >= 1950 && year <= current + 1;
  }, ValidationMessages.graduationYear);

export const slotDurationSchema = z
  .string()
  .trim()
  .min(1, ValidationMessages.required)
  .refine((value) => {
    const minutes = Number(value);
    return Number.isInteger(minutes) && minutes >= 5 && minutes <= 120;
  }, ValidationMessages.slotDuration);

export const bufferMinutesSchema = z
  .string()
  .trim()
  .refine((value) => {
    if (!value) return true;
    const minutes = Number(value);
    return Number.isInteger(minutes) && minutes >= 0 && minutes <= 60;
  }, ValidationMessages.buffer);

export const createUserFormSchema = z.object({
  fullName: personNameSchema,
  phoneNumber: indianMobileSchema,
  email: emailOptionalSchema,
});

export const clinicContactSchema = z.object({
  phoneNumber: indianMobileSchema,
  email: emailOptionalSchema,
});

export const doctorBasicsSchema = z.object({
  firstName: personNameSchema,
  lastName: personNameSchema,
  dateOfBirth: dateOfBirthOptionalSchema,
  email: emailOptionalSchema,
});

export const doctorProfessionalSchema = z.object({
  displayName: personNameSchema,
  yearsOfExperience: yearsOfExperienceSchema,
  qualification: z.string().trim(),
  institution: z.string().trim(),
  graduationYear: graduationYearSchema,
});

export const doctorRegistrationSchema = z.object({
  number: requiredStringSchema,
  council: requiredStringSchema,
  state: requiredStringSchema,
  registeredOn: isoDateSchema,
  expiresOn: z
    .string()
    .trim()
    .refine((value) => value.length === 0 || isoDateSchema.safeParse(value).success, ValidationMessages.date),
});

export const scheduleCreateSchema = z.object({
  fee: feeSchema,
  duration: slotDurationSchema,
  buffer: bufferMinutesSchema,
});

export const receptionPatientSchema = z.object({
  fullName: personNameSchema,
  phone: indianMobileSchema,
});

export type CreateUserFormValues = z.infer<typeof createUserFormSchema>;
export type ClinicContactValues = z.infer<typeof clinicContactSchema>;
export type DoctorBasicsValues = z.infer<typeof doctorBasicsSchema>;
export type DoctorProfessionalValues = z.infer<typeof doctorProfessionalSchema>;
export type DoctorRegistrationValues = z.infer<typeof doctorRegistrationSchema>;
export type ScheduleCreateValues = z.infer<typeof scheduleCreateSchema>;
export type ReceptionPatientValues = z.infer<typeof receptionPatientSchema>;

/** Aliases matching the product naming in the validation brief. */
export const MobileNumberSchema = indianMobileSchema;
export const EmailSchema = emailRequiredSchema;
export const OtpSchema = otpCodeSchema;
export const RequiredFieldSchema = requiredStringSchema;
export const DateSchema = isoDateSchema;
export const TimeSchema = timeSlotSchema;
export const AmountSchema = amountSchema;
export const PasswordSchema = passwordSchema;
export const PincodeSchema = pincodeSchema;
