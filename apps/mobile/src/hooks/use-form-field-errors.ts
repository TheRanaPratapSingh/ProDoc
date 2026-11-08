import { useCallback, useState } from 'react';
import type { ZodTypeAny } from 'zod';

import { validateField, validateForm, zodFieldErrors } from '@/utils/validation';

/**
 * Field-level error state for screens that use local useState forms
 * (not React Hook Form). Clears a field's error as soon as the user edits it.
 */
export function useFormFieldErrors<TField extends string = string>() {
  const [errors, setErrors] = useState<Partial<Record<TField, string>>>({});

  const setFieldError = useCallback((field: TField, message?: string | null) => {
    setErrors((current) => {
      if (!message) {
        if (!(field in current)) return current;
        const next = { ...current };
        delete next[field];
        return next;
      }
      return { ...current, [field]: message };
    });
  }, []);

  const clearField = useCallback((field: TField) => {
    setFieldError(field, null);
  }, [setFieldError]);

  const clearAll = useCallback(() => {
    setErrors({});
  }, []);

  const applyErrors = useCallback((next: Record<string, string>) => {
    setErrors(next as Partial<Record<TField, string>>);
  }, []);

  const bindChange = useCallback(
    <T,>(field: TField, setter: (value: T) => void) =>
      (value: T) => {
        clearField(field);
        setter(value);
      },
    [clearField],
  );

  const validateOnBlur = useCallback(
    (field: TField, schema: ZodTypeAny, value: unknown) => {
      const message = validateField(schema, value);
      setFieldError(field, message);
      return message == null;
    },
    [setFieldError],
  );

  const validateObject = useCallback(
    <TSchema extends ZodTypeAny>(schema: TSchema, values: unknown) => {
      const result = validateForm(schema, values);
      if (result.ok) {
        clearAll();
        return result;
      }
      applyErrors(result.errors);
      return result;
    },
    [applyErrors, clearAll],
  );

  const applyZodError = useCallback(
    (error: { issues: { path: (string | number)[]; message: string }[] }) => {
      applyErrors(zodFieldErrors(error));
    },
    [applyErrors],
  );

  return {
    errors,
    setFieldError,
    clearField,
    clearAll,
    applyErrors,
    applyZodError,
    bindChange,
    validateOnBlur,
    validateObject,
    hasErrors: Object.keys(errors).length > 0,
  };
}
