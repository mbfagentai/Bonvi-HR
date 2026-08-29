import {
  ArrowRightLeft,
  Award,
  BadgeCheck,
  Briefcase,
  CircleDollarSign,
  Eye,
  Gavel,
  ListPlus,
  LogIn,
  LogOut,
  ShieldAlert,
  Star,
  Timer,
  TrendingUp,
  type LucideIcon,
} from 'lucide-react'

import type { BadgeTone, IconTone } from '@/shared/ui'
import type {
  EmployeeFlagType,
  EmployeeStatus,
  EmploymentEventType,
} from '../api/employees-api'
import type { IncentiveKindValue } from '../api/incentives-api'

/* ═════════════════════════════════════════════════════════════ RUXSATLAR */

/**
 * Ruxsat kodlari — backend'dagi `Bonvi.Domain.Identity.Permissions` bilan
 * AYNAN bir xil. Satrni har joyda qo'lda yozish o'rniga konstanta:
 * kod o'zgarganda bitta joy tuzatiladi va yozuv xatosi 401 ga aylanmaydi.
 */
export const EMPLOYEES_VIEW = 'employees.view'
export const EMPLOYEES_CREATE = 'employees.create'
export const EMPLOYEES_EDIT = 'employees.edit'
export const EMPLOYMENT_MANAGE = 'employment.manage'
export const LISTS_MANAGE = 'lists.manage'

/**
 * Oklad — ALOHIDA ruxsat.
 *
 * HR ning talabi: Excel'da oklad hamma ko'radigan ustunda edi, tizimda esa
 * ruxsatsiz odam uni UMUMAN ko'rmasligi kerak. «Bo'sh maydon» ham javob:
 * u «oklad kiritilmagan» degan yolg'on taassurot qoldiradi. Shu sababli
 * maydonning o'zi chizilmaydi.
 */
export const SALARY_VIEW = 'salary.view'

/**
 * Rag'bat/jarima — ALOHIDA ruxsat guruhi, `employees.*` emas.
 *
 * Sabab backend'dagi bilan bir xil (`Permissions.IncentivesView`
 * izohiga qarang): pul (mukofot/jarima summasi) va intizomiy chora bilan
 * bog'liq nozik ma'lumot.
 */
export const INCENTIVES_VIEW = 'incentives.view'
export const INCENTIVES_MANAGE = 'incentives.manage'

/**
 * Rekruting — ALOHIDA ruxsat guruhi, `employees.*` dan MUSTAQIL.
 *
 * Xodim kartasida u faqat bitta joyda kerak: «Kengash xulosasi» bo'limi
 * nomzodlik davridagi suhbat xulosalarini ko'rsatadi. Backend'da ham
 * shu ruxsat talab qilinadi (`Permissions.RecruitingView` izohi):
 * «kimni nega olmadik» degan yozuvlarni xodimlar ro'yxatini ochgan har
 * bir kishi ko'rishi shart emas. Ruxsat yo'q bo'lsa bo'lim umuman
 * chizilmaydi va so'rov ham yuborilmaydi.
 */
export const RECRUITING_VIEW = 'recruiting.view'

/**
 * Excelga yuklash — `employees.view` EMAS, alohida `reports.export`.
 *
 * Sabab backend'da yozilgan (`ReportsEndpoints` izohi): eksport bitta
 * so'rov bilan butun bazani tashqariga chiqaradigan og'ir amal — shu
 * sababli u alohida ruxsat va tezlik cheklovi ostida turadi. Ro'yxatni
 * ko'ra oladigan har bir foydalanuvchi uni faylga ko'chira olishi
 * SHART EMAS.
 */
export const REPORTS_EXPORT = 'reports.export'

/* ══════════════════════════════════════════════════════════════ HOLATLAR */

/**
 * Holat rangi.
 *
 * Rang ma'no tashiydi, bezak emas: yashil — ishlayapti, sariq — hali
 * sinovda (ya'ni HR e'tiborida turishi kerak), kulrang — tarixda qolgan.
 * Kulrang ataylab neytral: ishdan ketish xato emas, shu sababli qizil emas.
 */
export const STATUS_TONE: Readonly<Record<EmployeeStatus, BadgeTone>> = {
  active: 'success',
  probation: 'warning',
  terminated: 'neutral',
}

/** Tarjima kaliti: `employee.statusActive` va hokazo. */
export const STATUS_KEY: Readonly<Record<EmployeeStatus, string>> = {
  active: 'employee.statusActive',
  probation: 'employee.statusProbation',
  terminated: 'employee.statusTerminated',
}

/* ═══════════════════════════════════════════════════════════════ BELGILAR */

export interface FlagLook {
  tone: BadgeTone
  iconTone: IconTone
  icon: LucideIcon
  /** `employees.flags.<key>` tarjima kaliti. */
  key: string
}

/**
 * Belgilarning ko'rinishi.
 *
 * Qora ro'yxat — qizil, chunki u bo'shatish va qayta ishga olishni
 * to'sadi. Nazorat — sariq: to'smaydi, lekin ogohlantiradi. Rezerv va
 * qobiliyatli — ijobiy, shu sababli ko'k va binafsha.
 */
export const FLAG_LOOK: Readonly<Record<EmployeeFlagType, FlagLook>> = {
  blacklist: {
    tone: 'danger',
    iconTone: 'red',
    icon: ShieldAlert,
    key: 'blacklist',
  },
  monitoring: {
    tone: 'warning',
    iconTone: 'orange',
    icon: Eye,
    key: 'monitoring',
  },
  reserve: {
    tone: 'info',
    iconTone: 'blue',
    icon: Star,
    key: 'reserve',
  },
  capable: {
    tone: 'purple',
    iconTone: 'purple',
    icon: Award,
    key: 'capable',
  },
}

/* ═══════════════════════════════════════════════════ RAG'BAT VA JARIMA */

export interface IncentiveKindLook {
  tone: BadgeTone
  iconTone: IconTone
  icon: LucideIcon
  /** `incentives.kind<Key>` tarjima kaliti. */
  labelKey: string
}

/** Rag'bat — yashil (ijobiy), jarima — qizil (intizomiy chora). */
export const INCENTIVE_KIND_LOOK: Readonly<Record<IncentiveKindValue, IncentiveKindLook>> = {
  award: {
    tone: 'success',
    iconTone: 'green',
    icon: Award,
    labelKey: 'incentives.kindAward',
  },
  penalty: {
    tone: 'danger',
    iconTone: 'red',
    icon: Gavel,
    labelKey: 'incentives.kindPenalty',
  },
}

/* ══════════════════════════════════════════════════════════════ HODISALAR */

export interface EventLook {
  icon: LucideIcon
  tone: IconTone
  /** `employees.events.<key>` tarjima kaliti. */
  key: string
}

/**
 * Lenta hodisalarining ko'rinishi.
 *
 * Har bir tur o'z ikonasi va rangi bilan: HR lentani o'qimasdan, faqat
 * chap ustundagi ranglar ketma-ketligiga qarab ham mehnat yo'lini
 * taxminan tushunadi — kirdi, ko'tarildi, ketdi.
 */
export const EVENT_LOOK: Readonly<Record<EmploymentEventType, EventLook>> = {
  hire: { icon: LogIn, tone: 'green', key: 'hire' },
  transfer: { icon: ArrowRightLeft, tone: 'blue', key: 'transfer' },
  promotion: { icon: TrendingUp, tone: 'purple', key: 'promotion' },
  additionalDuty: { icon: ListPlus, tone: 'teal', key: 'additionalDuty' },
  salaryChange: { icon: CircleDollarSign, tone: 'orange', key: 'salaryChange' },
  probationResult: { icon: BadgeCheck, tone: 'green', key: 'probationResult' },
  termination: { icon: LogOut, tone: 'red', key: 'termination' },
  rehire: { icon: Briefcase, tone: 'brand', key: 'rehire' },
}

/** Sinov muddati ikonasi — kartaning yuqorisidagi ogohlantirish uchun. */
export const PROBATION_ICON: LucideIcon = Timer

/* ═════════════════════════════════════════════════════════════ YORDAMCHI */

/**
 * JSHSHIR ni MA'NOSI bo'yicha ajratadi:
 * «32105897046171» → «3 210589 704617 1».
 *
 * NEGA GURUHLANADI: 14 ta raqam qatori ko'z bilan solishtirib
 * bo'lmaydigan blok. HR uni pasportdagi raqam bilan taqqoslaydi.
 *
 * ⚠️ NEGA 4-4-4-2 EMAS. Ilgari shunday edi va u XATO edi: JSHSHIR
 * tasodifiy raqam emas, uning tuzilishi bor va to'rttalab bo'lish
 * ma'noli bo'laklarni o'rtasidan kesib tashlardi.
 *
 *     3      210589      704617      1
 *     │      │           │           └─ nazorat raqami
 *     │      │           └───────────── tartib raqami
 *     │      └───────────────────────── tug'ilgan sana (KKOOYY)
 *     └──────────────────────────────── jins va tug'ilgan asr
 *                                       (3/4 — 1900-yillar, 5/6 — 2000-yillar)
 *
 * Tuzilish TAXMIN emas: joriy bazadagi 44 ta JSHSHIR tekshirilgan va
 * BARCHASIDA 2–7-raqamlar xodimning tug'ilgan sanasiga aynan mos
 * kelgan (bittasi ham istisno emas).
 *
 * Amaliy foydasi: HR endi o'rtadagi olti raqamni tug'ilgan sana bilan
 * bir qarashda solishtira oladi — bu Excel'dan ko'chirilgan noto'g'ri
 * JSHSHIR ni topishning eng tez yo'li.
 */
export function formatPinfl(pinfl: string | null): string | null {
  if (pinfl === null) return null
  const digits = pinfl.replaceAll(/\D/gu, '')
  if (digits.length !== 14) return pinfl
  return digits.replace(/(\d)(\d{6})(\d{6})(\d)/u, '$1 $2 $3 $4')
}

/**
 * Stavka: `1` → «1», `0.5` → «0,5».
 *
 * Kasr qismi faqat kerak bo'lganda ko'rsatiladi — «1,0 stavka» ortiqcha
 * shovqin, «0,5 stavka» esa muhim ma'lumot (o'rindoshlik).
 */
export function formatRate(rate: number): string {
  return Number.isInteger(rate) ? String(rate) : rate.toFixed(1).replace('.', ',')
}
