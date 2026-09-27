import { ArrowDownUp, LogIn, LogOut, type LucideIcon } from 'lucide-react'

import type { BadgeTone } from '@/shared/ui'
import type {
  AttendanceIdentityProvider,
  DeviceEnrollmentState,
  AttendanceProvider,
  DeviceDirectionRole,
  DeviceLinkState,
  IdentitySource,
} from '../api/attendance-api'

/**
 * Boshqaruv sahifalarining (moslashtirish, qurilmalar) yorliqlari.
 *
 * NEGA `attendance-presentation.ts` da emas: u butun modulning umumiy
 * fayli va parallel ishlayotgan boshqa sahifalar ham unga yozadi; bu
 * yerdagi yorliqlar esa faqat qurilma va moslik ekranlariga tegishli.
 * Alohida fayl — bir-birining ustiga yozib yuborish xavfi yo'q.
 *
 * Komponent faylidan ham ALOHIDA: React Fast Refresh bitta modulda
 * komponent va oddiy eksport aralashsa ishlamaydi (`data-table-columns.ts`
 * izohidagi sabab).
 */

/**
 * ID makoni nomi.
 *
 * ⭐ Brend nomlari (Verifix, Hikvision, ZKTeco) TARJIMA QILINMAYDI — ular
 * to'rt tilda ham bir xil yoziladi va kalit ochish to'rtta bir xil satr
 * degani. Faqat `Manual` so'z, shu sababli u kalit orqali.
 */
export function identityProviderLabel(
  provider: AttendanceIdentityProvider,
  t: (key: string) => string,
): string {
  switch (provider) {
    case 'Verifix':
      return 'Verifix'
    case 'Hikvision':
      return 'Hikvision'
    case 'ZkTeco':
      return 'ZKTeco'
    case 'Manual':
      return t('attendance.identityProviderManual')
    case 'Mobile':
      return t('attendance.identityProviderMobile')
  }
}

/**
 * Qurilma hodisasini qaysi PARSER o'qiydi — `Generic` so'z, qolgani brend.
 *
 * `Mobile` — «Mobil ilova» qatori (xodim ilovasidagi belgi): u JSON emas,
 * telefon. Ilgari u «Umumiy JSON» deb chiqardi va xodim kartasidagi jurnalda
 * telefondan kelgan har bir belgi terminalnikiga o'xshab ko'rinardi.
 */
export function deviceProviderLabel(
  provider: AttendanceProvider,
  t: (key: string) => string,
): string {
  switch (provider) {
    case 'Hikvision':
      return 'Hikvision'
    case 'Mobile':
      return t('attendance.identityProviderMobile')
    case 'Generic':
      return t('attendance.providerGeneric')
  }
}

/**
 * Raqam tizim terminalga o'zi yozgan fazadami — 9 bilan boshlanadigan 9 belgi.
 *
 * Qoida serverdagi `DevicePersonNo.IsManaged` bilan AYNAN bir xil. Bunday
 * moslikni uzib bo'lmaydi (server `davomat.moslik_bonvi_raqami` bilan 409
 * beradi) — ekran tugmani ko'rsatmaydi.
 */
export function isManagedPersonNo(personNo: string): boolean {
  return personNo.length === 9 && personNo.startsWith('9')
}

/**
 * Sintetik «Mobil ilova» qatori — terminal EMAS.
 *
 * Uni tahrirlab, yoqib-o'chirib, tokenini almashtirib yoki o'chirib bo'lmaydi
 * (server har biriga 409 beradi) — shu sababli ro'yxatda u faqat O'QISH
 * uchun chiziladi: boshqaruv tugmalarisiz va qatori bosilmaydi.
 */
export function isSyntheticDevice(device: { provider: AttendanceProvider }): boolean {
  return device.provider === 'Mobile'
}

export interface RoleLook {
  icon: LucideIcon
  labelKey: string
}

/**
 * Qurilmaning yo'nalish roli (SPEC §3.7).
 *
 * Ikonka rangsiz: rol xato ham, yaxshi ham emas — shunchaki sozlama.
 * Rang faqat ma'no tashiydi (§8.5), bu yerda esa ma'no yo'q.
 */
export const DIRECTION_ROLE_LOOK: Readonly<Record<DeviceDirectionRole, RoleLook>> = {
  ReportsDirection: { icon: ArrowDownUp, labelKey: 'attendance.roleReports' },
  EntryOnly: { icon: LogIn, labelKey: 'attendance.roleEntryOnly' },
  ExitOnly: { icon: LogOut, labelKey: 'attendance.roleExitOnly' },
}

export interface SourceLook {
  tone: BadgeTone
  labelKey: string
}

/**
 * Moslik qayerdan kelgan: import (Verifix ro'yxati) yoki qo'lda.
 *
 * Ikkalasi ham neytral-ko'k emas: `Import` — «tizim taklif qildi, odam
 * tasdiqladi», `Manual` — «odam o'zi bog'ladi». Farq tekshiruvda muhim,
 * lekin hech biri xato emas.
 */
export const IDENTITY_SOURCE_LOOK: Readonly<Record<IdentitySource, SourceLook>> = {
  Import: { tone: 'neutral', labelKey: 'attendance.sourceImport' },
  Manual: { tone: 'brand', labelKey: 'attendance.sourceManual' },
}

/**
 * Qurilmaga yozish holati (SPEC-agent §F).
 *
 * ⭐ «Kutilmoqda» SARIQ EMAS, neytral: agent xodimni bir daqiqa ichida
 * yozadi va bu NORMAL oraliq holat, nosozlik emas. Sariq va qizil faqat
 * odam aralashuvi kerak bo'lganda: rasm qo'yish yoki xatoni o'qish.
 *
 * «O'chirilgan» ham neytral — ishdan ketgan odamning qurilmadan
 * chiqarilishi kutilgan natija.
 */
export const ENROLLMENT_STATE_LOOK: Readonly<Record<DeviceEnrollmentState, SourceLook>> = {
  Synced: { tone: 'success', labelKey: 'attendance.enrollmentSynced' },
  Pending: { tone: 'neutral', labelKey: 'attendance.enrollmentPending' },
  Failed: { tone: 'danger', labelKey: 'attendance.enrollmentFailed' },
  PhotoMissing: { tone: 'warning', labelKey: 'attendance.enrollmentPhotoMissing' },
  Removed: { tone: 'neutral', labelKey: 'attendance.enrollmentRemoved' },
}

/**
 * «5 daqiqa oldin» — so'nggi ko'rinish uchun, qisqa va qo'pol.
 *
 * NEGA qo'pol: qurilmalar sahifasida savol «terminal TIRIKMI?», ya'ni
 * «hozirgina / bir necha daqiqa / soat / kun» farqi kerak, soniyalar
 * emas. Aniq vaqt `title` da turadi.
 *
 * `null` — bir sutkadan eski: chaqiruvchi to'liq sana-vaqtni chizadi
 * («3 kun oldin» dan ko'ra «14.09.2026 18:02» tekshirish uchun qulayroq).
 */
export type AgoParts =
  | { unit: 'now' }
  | { unit: 'minutes'; value: number }
  | { unit: 'hours'; value: number }

export function agoParts(value: string, now: Date = new Date()): AgoParts | null {
  const at = new Date(value).getTime()
  if (Number.isNaN(at)) return null

  // Kelajakdagi vaqt (qurilma soati oldinda) — «hozirgina»: manfiy son savol tug'dirardi
  const minutes = Math.max(0, Math.trunc((now.getTime() - at) / 60_000))

  if (minutes < 1) return { unit: 'now' }
  if (minutes < 60) return { unit: 'minutes', value: minutes }
  if (minutes < 24 * 60) return { unit: 'hours', value: Math.trunc(minutes / 60) }
  return null
}

/*
 * «Ulangan, lekin hodisalari rad etilyapti» belgisi endi SERVERDA
 * (`AttendanceDevice.eventsRejected`): u aynan rad etilgan so'rovdan
 * yoqiladi. Ilgari u shu yerda `lastSeenAt − lastEventAt > 30 daqiqa` dan
 * taxmin qilinardi va agent qurilmasida HAR KECHASI va dam olish kuni
 * yolg'on yonardi — agent o'tish bo'lmasa ham har daqiqada ro'yxat so'raydi.
 */

/**
 * Qurilmaning aloqa holati (SPEC-agent §L) — uzilgan bo'lsa qizil izoh kaliti.
 *
 * Holatni SERVER hisoblaydi (`linkState`, chegarasi o'n daqiqa, jadvalga
 * bog'liq emas); bu yerda faqat matn tanlanadi.
 */
export function linkProblemKey(state: DeviceLinkState): string | null {
  switch (state) {
    case 'AgentOffline':
      return 'attendance.linkAgentOffline'
    case 'TerminalUnreachable':
      return 'attendance.linkTerminalUnreachable'
    case 'NeverConnected':
    case 'Online':
      return null
  }
}

/** Uzilgan (qizil ogohlantirishga tushadigan) holatmi. */
export function isOutage(state: DeviceLinkState): boolean {
  return state === 'AgentOffline' || state === 'TerminalUnreachable'
}
