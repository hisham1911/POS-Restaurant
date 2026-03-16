import { useState, useCallback } from 'react';

/**
 * Custom hook للتعامل مع حقول الأرقام بشكل صحيح
 * يحل مشكلة ظهور الصفر كقيمة افتراضية بدلاً من placeholder
 */
export function useNumberInput(initialValue?: number) {
  // نخزن القيمة كـ string عشان نقدر نعرض empty string
  const [displayValue, setDisplayValue] = useState<string>(
    initialValue !== undefined && initialValue !== 0 ? String(initialValue) : ''
  );

  // القيمة الفعلية كرقم
  const numericValue = displayValue === '' ? 0 : Number(displayValue);

  const handleChange = useCallback((value: string) => {
    // نسمح بـ empty string أو أرقام صحيحة
    if (value === '' || !isNaN(Number(value))) {
      setDisplayValue(value);
    }
  }, []);

  const setValue = useCallback((value: number | string) => {
    if (typeof value === 'number') {
      setDisplayValue(value === 0 ? '' : String(value));
    } else {
      setDisplayValue(value);
    }
  }, []);

  const reset = useCallback(() => {
    setDisplayValue('');
  }, []);

  return {
    displayValue,
    numericValue,
    handleChange,
    setValue,
    reset,
  };
}

/**
 * Helper function لتحويل القيمة من number إلى display string
 */
export function numberToDisplay(value: number | null | undefined): string {
  if (value === null || value === undefined || value === 0) {
    return '';
  }
  return String(value);
}

/**
 * Helper function لتحويل القيمة من display string إلى number
 */
export function displayToNumber(value: string): number {
  return value === '' ? 0 : Number(value);
}
