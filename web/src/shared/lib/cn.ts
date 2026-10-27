import { clsx, type ClassValue } from 'clsx'
import { extendTailwindMerge } from 'tailwind-merge'

/**
 * Loyihaning iOS shrift shkalasi (`globals.css` → `--text-*`).
 *
 * ⚠️ tailwind-merge ularni bilmaydi va `text-caption2` ni RANG deb hisoblaydi:
 * `cn('text-caption2', 'text-[var(--fg-tertiary)]')` o'lchamni tashlab yuborardi
 * (2026-10-22 da topildi — jadval sarlavhalari, sahifalash, maydonlar mo'ljaldagidan
 * katta chiqardi). Ro'yxatga olingach ular o'lcham guruhida — rang bilan to'qnashmaydi.
 */
const twMerge = extendTailwindMerge({
  extend: {
    classGroups: {
      'font-size': [
        {
          text: [
            'caption2',
            'caption1',
            'footnote',
            'subhead',
            'callout',
            'body',
            'title3',
            'title2',
            'title1',
            'largetitle',
          ],
        },
      ],
    },
  },
})

/** Tailwind sinflarini xavfsiz birlashtiradi (ziddiyatli sinflar to'g'ri yechiladi). */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs))
}
