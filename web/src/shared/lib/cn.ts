import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/** Tailwind sinflarini xavfsiz birlashtiradi (ziddiyatli sinflar to'g'ri yechiladi). */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs))
}
