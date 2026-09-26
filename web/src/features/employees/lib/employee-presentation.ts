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
import { EMPLOYEE_FLAG_TYPES } from '../api/employees-api'
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
export const LISTS_MANAGE = 'lists.manage'

/*
 * Mehnat munosabatlari amallari — har biri O'Z ruxsati bilan.
 *
 * Ilgari hammasi bitta `employment.manage` edi: «bo'shata oladi, lekin
 * oklad o'zgartira olmaydi» degan rolni tuzib bo'lmasdi. Endi rolni
 * egasi o'zi yig'adi (rollar shartnomasi §1) va har bir tugma faqat O'Z
 * amalining ruxsati bilan chiziladi — server ham aynan shu kodlarni
 * tekshiradi.
 */
export const EMPLOYMENT_HIRE = 'employment.hire'
export const EMPLOYMENT_TRANSFER = 'employment.transfer'
export const EMPLOYMENT_DUTY = 'employment.duty'
export const EMPLOYMENT_TERMINATE = 'employment.terminate'
export const EMPLOYMENT_PROBATION_EXTEND = 'employment.probation.extend'
/** Sinovni yakunlash VA «yo'qolgan» sinovlarni ommaviy tasdiqlash — ikkalasi ham. */
export const EMPLOYMENT_PROBATION_COMPLETE = 'employment.probation.complete'
export const EMPLOYMENT_CORRECT = 'employment.correct'

/**
 * Oklad o'zgarishi — va PUL QOIDASI (shartnoma §1): boshqa amal ichidagi
 * har qanday summa ham shu ruxsatni talab qiladi — ko'chirishdagi oklad,
 * qo'shimcha vazifa uchun ustama, sinov yakunidagi oklad, yozuv
 * tuzatishdagi summa. Ruxsat yo'q bo'lsa server butun so'rovni 403 bilan
 * rad etadi, shu sababli oynalar bu maydonlarni UMUMAN chizmaydi — server
 * rad etadigan formani taklif qilish HR ni aldash bo'lardi.
 *
 * Yagona istisno — ishga qabuldagi boshlang'ich oklad: u qabulning bir
 * qismi va `employment.hire` bilan keladi.
 */
export const EMPLOYMENT_SALARY = 'employment.salary'

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

/**
 * Xodim yo'lining bosqichi — belgi shu bosqichga qarab tanlanadi.
 *
 * `employed` — xodim hozir ishlab turibdi (sinovda yoki doimiy).
 * `departed` — barcha mehnat munosabatlari yakunlangan, yoki umuman yo'q.
 */
export type EmployeeStage = 'employed' | 'departed'

/**
 * Qaysi belgi qaysi bosqichda qo'yiladi.
 *
 * <b>Backend'dagi <c>EmployeeFlagStages</c> bilan AYNAN bir xil</b> —
 * ikkalasi bir xil bo'lishi shart, aks holda interfeys qo'yib bo'lmaydigan
 * belgini taklif qilib, server 409 bilan rad etardi.
 *
 * Bo'linish ma'nosi: ishlab turgan odam haqidagi savol «u bilan HOZIR
 * nima qilamiz?» (kuzatamizmi, ko'proq yuklama beramizmi), ketayotgan
 * odam haqidagisi esa «uni KEYIN qaytarib olamizmi?» (chaqirsa bo'ladimi,
 * umuman olmaymizmi). Aralashgan holat ma'nosiz: bizda ishlab turgan
 * odamni «rezerv» deb belgilash — «u bizda ishlayapti, lekin kerak bo'lsa
 * chaqiramiz» degani.
 */
export const FLAG_STAGE: Readonly<Record<EmployeeFlagType, EmployeeStage>> = {
  monitoring: 'employed',
  capable: 'employed',
  reserve: 'departed',
  blacklist: 'departed',
}

/**
 * Xodim hozir ishlayaptimi.
 *
 * Bitta ochiq munosabat yetarli: o'rindosh xodim bitta firmadan ketsa
 * ham bizda ishlashda davom etadi (backend ham shunday hisoblaydi).
 * Munosabati UMUMAN yo'q xodim `departed` — u hali ishga qabul
 * qilinmagan va unga «nazorat» qo'yish ma'nosiz.
 */
export function employeeStage(
  employments: readonly { status: EmployeeStatus }[],
): EmployeeStage {
  return employments.some((employment) => employment.status !== 'terminated')
    ? 'employed'
    : 'departed'
}

/**
 * Belgilar ro'yxatida turadigan, lekin BELGI BO'LMAGAN yozuvlar.
 *
 * Ular xodim yo'lidagi xuddi shunday nishonlar — «bu odam bilan nima
 * bo'lgan?» degan savolga javob beradi — lekin ularning ortida to'liq
 * yozuv turadi: baxtsiz hodisada joy, sabab, jabrlanish darajasi va
 * komissiya xulosasi; qayta aloqada esa suhbatning o'zi.
 *
 * <b>Nega belgi turiga aylantirilmadi.</b> Belgi — sana va sabab; bu
 * yozuvlar esa o'ndan ortiq maydon va o'z hayot sikliga ega (hodisaga
 * keyinroq komissiya xulosasi qo'shiladi). Ularni belgi qilib qo'yish
 * bir odam bo'yicha IKKITA manba yaratardi: `/hodisalar` jurnalidagi
 * to'liq yozuv va kartadagi bo'sh yorliq — va ular birinchi
 * o'zgarishdayoq bir-biriga mos kelmay qolardi.
 *
 * Shu sababli ular «Belgi qo'yish» ro'yxatida TURADI (HR ularni shu
 * yerdan izlaydi), lekin tanlanganda o'z formasiga olib boradi.
 */
export const ROUTED_MARKS = ['incident', 'interview'] as const
export type RoutedMark = (typeof ROUTED_MARKS)[number]

/**
 * Havolali yozuvni saqlash uchun kerak bo'ladigan ruxsat.
 *
 * Belgi qo'yish `lists.manage` bilan, hodisa va suhbat esa O'Z
 * ruxsatlari bilan himoyalangan (`safety.manage` / `feedback.manage`) —
 * ular alohida guruh, sabab `safety-presentation.ts` da yozilgan.
 */
export const ROUTED_MARK_PERMISSION: Readonly<Record<RoutedMark, string>> = {
  incident: 'safety.manage',
  interview: 'feedback.manage',
}

/**
 * «Belgi qo'yish» oynasidagi tanlov — bosqich bo'yicha.
 *
 * Avval o'sha bosqichning belgilari, oxirida havolali yozuv: ro'yxat
 * «tez-tez ishlatiladigandan kamdan-kamgacha» tartibida o'qiladi va
 * havola oxirida turgani uchun tasodifan tanlanmaydi.
 *
 * <b>Havola RUXSATGA qarab qo'shiladi.</b> `lists.manage` bor, lekin
 * `safety.manage` yo'q foydalanuvchi butun hodisa formasini to'ldirib,
 * «Saqlash» bosgandan keyin 403 olardi — ya'ni ish bekorga ketardi.
 * Belgilarning o'zi shartsiz: oyna allaqachon `lists.manage` ostida
 * ochiladi.
 */
export function stageChoices(
  stage: EmployeeStage,
  hasPermission: (code: string) => boolean,
): readonly (EmployeeFlagType | RoutedMark)[] {
  const flags = EMPLOYEE_FLAG_TYPES.filter((type) => FLAG_STAGE[type] === stage)
  const routed: RoutedMark = stage === 'employed' ? 'incident' : 'interview'

  return hasPermission(ROUTED_MARK_PERMISSION[routed]) ? [...flags, routed] : flags
}

/** Tanlangan yozuv belgi emas, havolami. */
export function isRoutedMark(choice: EmployeeFlagType | RoutedMark): choice is RoutedMark {
  return (ROUTED_MARKS as readonly string[]).includes(choice)
}

/**
 * Belgi shu SANADAN keyin ham kuchdami.
 *
 * Backend'dagi `EmployeeFlag.IsInForceAfter` bilan aynan bir xil
 * (`endDate > date`, `>=` emas): bugun yopilgan belgi bugundan keyin
 * kuchda EMAS. Kartadagi nishonlar shu shartga tayanadi — aks holda
 * almashtirilgan belgi almashtirilgan KUNI hali ham nishon bo'lib
 * turardi va HR ikkita belgi ko'rib, qoida buzilgan deb o'ylardi.
 */
export function flagInForce(
  flag: { startDate: string; endDate: string | null },
  date: string,
): boolean {
  return flag.startDate <= date && (flag.endDate === null || flag.endDate > date)
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
