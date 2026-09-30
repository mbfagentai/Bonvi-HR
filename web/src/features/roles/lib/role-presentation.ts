import {
  Award,
  Briefcase,
  ChartColumn,
  ClipboardList,
  HeartPulse,
  History,
  Library,
  MessageSquareQuote,
  ScanFace,
  Shield,
  ShieldCheck,
  SlidersHorizontal,
  UserCog,
  UserSearch,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { useCallback } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import type { BadgeTone, IconTone } from '@/shared/ui'

import {
  ACCESS_LEVELS,
  ROLE_ACTION_CODES,
  ROLE_EXTRA_CODES,
  ROLE_MODULE_KEYS,
  type AccessLevel,
} from './role-keys'

/** «Rollar» menyusi va rolga tegishli har bir o'zgarish — backend'dagi bilan bir xil kod. */
export const ROLES_MANAGE = 'roles.manage'

/**
 * Yagona himoyalangan rol — tizim egasi.
 *
 * Kod bilan tanilishining ikki sababi bor: «Foydalanuvchilar» dagi «kim
 * kimga tega oladi» qoidasi (§5) uni alohida ko'radi va uning nomi
 * egasi yozgan matn emas, tizimning o'zi bergan nom — shu sababli u
 * to'rt tilga tarjima qilinadi (`roles.super_admin`).
 */
export const SUPER_ADMIN_ROLE_CODE = 'super_admin'

/**
 * Rolning ekrandagi nomi.
 *
 * Egasi yaratgan va qayta nomlagan rollar — o'z nomi bilan, O'ZGARISHSIZ:
 * «Kadr bo'limi boshlig'i» ni hech kim tarjima qilmaydi va qilmasligi
 * kerak. Yagona istisno — super administrator: uning nomini egasi
 * yozmagan, shu sababli u foydalanuvchi tilida chiqadi.
 */
export function roleDisplayName(
  t: (key: string) => string,
  role: { code: string; name: string },
): string {
  if (role.code !== SUPER_ADMIN_ROLE_CODE) return role.name
  const translated = t('roles.super_admin')
  return translated === 'roles.super_admin' ? role.name : translated
}

/**
 * Bo'lim qatorining ikonasi — yon menyudagi xuddi shu bo'limning ikonasi
 * (`nav-items.ts`). HR ikonani menyudan taniydi va muharrirda ham uni
 * izlaydi. Notanish bo'lim oddiy qalqon bilan chiziladi.
 */
const MODULE_LOOK: Readonly<Record<string, { icon: LucideIcon; tone: IconTone }>> = {
  employees: { icon: Users, tone: 'blue' },
  employment: { icon: Briefcase, tone: 'brand' },
  lists: { icon: ClipboardList, tone: 'orange' },
  incentives: { icon: Award, tone: 'purple' },
  feedback: { icon: MessageSquareQuote, tone: 'teal' },
  safety: { icon: HeartPulse, tone: 'red' },
  recruiting: { icon: UserSearch, tone: 'green' },
  attendance: { icon: ScanFace, tone: 'teal' },
  catalog: { icon: Library, tone: 'gray' },
  reports: { icon: ChartColumn, tone: 'blue' },
  users: { icon: UserCog, tone: 'gray' },
  roles: { icon: ShieldCheck, tone: 'gray' },
  audit: { icon: History, tone: 'gray' },
  settings: { icon: SlidersHorizontal, tone: 'gray' },
}

export function moduleLook(key: string): { icon: LucideIcon; tone: IconTone } {
  return MODULE_LOOK[key] ?? { icon: Shield, tone: 'gray' }
}

/**
 * Faqat o'qish rejimidagi daraja yorlig'i. Rang ma'no tashiydi: yashil —
 * to'liq, ko'k — ko'rish, kulrang — yopiq.
 */
export const LEVEL_TONE: Readonly<Record<AccessLevel, BadgeTone>> = {
  none: 'neutral',
  view: 'info',
  edit: 'brand',
  full: 'success',
}

const MODULE_KEYS: ReadonlySet<string> = new Set(ROLE_MODULE_KEYS)
const LEVEL_KEYS: ReadonlySet<string> = new Set(ACCESS_LEVELS)
const ACTION_CODES: ReadonlySet<string> = new Set(ROLE_ACTION_CODES)
const EXTRA_CODES: ReadonlySet<string> = new Set(ROLE_EXTRA_CODES)

/**
 * Katalog kalitlarining yorliqlari.
 *
 * Tarjima faqat `role-keys.ts` dagi TANISH kalit uchun so'raladi; qolgani
 * xom holida chiqadi. Sabab: i18next yo'q kalitga kalitning o'zini
 * qaytaradi va ekranda «roles.module.attendance» degan qator paydo
 * bo'lardi — xom `attendance` hech bo'lmasa o'qiladi va HR ni
 * adashtirmaydi.
 */
export function useRoleLabels() {
  const { t } = useLocale()

  return {
    module: useCallback(
      (key: string) => (MODULE_KEYS.has(key) ? t(`roles.module.${key}`) : key),
      [t],
    ),
    level: useCallback(
      (level: AccessLevel) => (LEVEL_KEYS.has(level) ? t(`roles.level.${level}`) : level),
      [t],
    ),
    levelHint: useCallback(
      (level: AccessLevel) => (LEVEL_KEYS.has(level) ? t(`roles.levelHint.${level}`) : ''),
      [t],
    ),
    action: useCallback(
      (code: string) => (ACTION_CODES.has(code) ? t(`roles.action.${code}`) : code),
      [t],
    ),
    extra: useCallback(
      (code: string) => (EXTRA_CODES.has(code) ? t(`roles.extra.${code}`) : code),
      [t],
    ),
  }
}
