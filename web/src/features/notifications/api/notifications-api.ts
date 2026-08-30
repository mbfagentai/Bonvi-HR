/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`incidents-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Endpoint ALLAQACHON TAYYOR (boshqa agent backend'ni yozdi) va maydon
 * nomlari TAXMIN emas — ular jonli javobdan olingan
 * (`GET /api/notifications` chaqirildi va solishtirildi):
 *   api/src/Bonvi.Api/Endpoints/NotificationEndpoints.cs
 *   api/src/Bonvi.Application/Notifications/Dtos/NotificationDtos.cs
 *   api/src/Bonvi.Domain/Notifications/NotificationType.cs
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `notifications-queries.ts` generatsiya qilingan hook'larga o'tkaziladi.
 */
import { api } from '@/shared/api/http-client'

/* ═══════════════════════════════════════════════════════════════ sanoqlar */

/*
 * Sanoqlar JSON'da NOM bo'lib yuradi (`JsonStringEnumConverter`,
 * `Program.cs`) va klientda ham AYNAN shu ko'rinishda saqlanadi —
 * `recruiting-api.ts` dagi bilan bir xil qaror va bir xil sabab: server
 * nomni o'zi beradi, o'rtada yana bir xarita qo'yish ikkita nomlash
 * tizimi hosil qiladi. Tarjima kalitlari uchun birinchi harf kichraytiriladi
 * va bu O'GIRISH BITTA joyda turadi (`notification-presentation.ts`).
 *
 * Massivlar tartibi C# sanoq qiymatlariga mos (1 dan boshlanadi) —
 * pastdagi `enumValue` shunga tayanadi.
 */

/** To'qqizta tur. Yangisi OXIRIGA qo'shiladi (backend'da ham shunday). */
export const NOTIFICATION_TYPES = [
  'ProbationEndingSoon',
  'ProbationEndsToday',
  'ProbationOverdue',
  'ProbationMissing',
  'BirthdayToday',
  'BirthdayTomorrow',
  'CandidateNew',
  'JobOfferExpiring',
  'JobOfferExpired',
  'CandidateFromBot',
] as const

export type NotificationType = (typeof NOTIFICATION_TYPES)[number]

/** Muhimlik darajasi — rang va serverdagi tartib shu bo'yicha. */
export const NOTIFICATION_SEVERITIES = ['Info', 'Warning', 'Critical'] as const

export type NotificationSeverity = (typeof NOTIFICATION_SEVERITIES)[number]

/**
 * «Kirib ko'rish» qaysi ekranga olib boradi.
 *
 * Server tayyor YO'L SATRI bermaydi va bu ataylab: marshrutlar frontendda,
 * o'zbekcha yo'llar bilan yozilgan (`/xodimlar`, `/sinov`, `/nomzodlar`).
 * Sanoqdan haqiqiy manzilga o'girish — `notifications-dialog.tsx` dagi
 * yagona `switch`.
 */
export const NOTIFICATION_ROUTES = [
  'Employee',
  'Probations',
  'MissingProbations',
  'Candidate',
] as const

export type NotificationRoute = (typeof NOTIFICATION_ROUTES)[number]

/* ═══════════════════════════════════════════════════════════════ tiplar */

/**
 * Havola: qaysi ekran va u yerdagi qaysi yozuv.
 *
 * `id` ning MA'NOSI `route` ga bog'liq: `Employee` — xodim,
 * `Probations` — sinov yozuvi, `MissingProbations` — mehnat munosabati,
 * `Candidate` — nomzod.
 */
export interface NotificationLink {
  route: NotificationRoute
  id: string | null
}

/**
 * Bitta bildirishnoma.
 *
 * NEGA nomi `AppNotification`, oddiy `Notification` emas: `Notification` —
 * brauzerning O'Z global tipi (Web Notifications API). Bir xil nom
 * ishlatilsa, bu faylni import qilmagan modulda `Notification` yozgan odam
 * jimgina boshqa tipni olardi va TypeScript hech narsa demasdi.
 *
 * ⚠️ Tayyor MATN yo'q — server tur va parametrlarni beradi, jumlani
 * frontend yig'adi (tizim to'rt tilli). Sabab backend'dagi
 * `NotificationType` izohida batafsil, matnlar esa
 * `notification-presentation.ts` da.
 */
export interface AppNotification {
  /** Barqaror identifikator: «o'qildi» va «o'chirish» so'rovlarida AYNAN shu yuboriladi. */
  key: string
  type: NotificationType
  severity: NotificationSeverity
  /**
   * Bildirishnoma ISHORA QILAYOTGAN sana (sinov tugashi, tug'ilgan kun,
   * taklif muddati) — «xabar kelgan vaqt» EMAS: bildirishnoma
   * saqlanmaydi, u so'rov kelganda hisoblanadi.
   */
  date: string
  /**
   * Matnni yig'ish uchun qiymatlar; kalitlari turga bog'liq.
   *
   * ⚠️ `salaryAmount`/`salaryCurrency` `salary.view` ruxsati yo'q
   * foydalanuvchida UMUMAN kelmaydi — matn ularsiz ham to'liq bo'lishi
   * shart (`notification-presentation.ts` dagi qoida).
   */
  params: Readonly<Record<string, string>>
  link: NotificationLink
  isRead: boolean
  /** Qachon o'qilgan. Ekranda ko'rsatilmaydi, lekin shartnomaning bir qismi. */
  readAt: string | null
}

/** Ro'yxat javobi — sanoq ham shu yerda (qo'shimcha so'rov shart emas). */
export interface NotificationList {
  items: AppNotification[]
  /** Foydalanuvchi ko'ra oladigan (o'chirilmagan) bildirishnomalarning UMUMIY soni. */
  total: number
  unreadCount: number
}

/** «O'qildi» yoki «o'chirildi» amalining natijasi. */
export interface NotificationActionResult {
  affected: number
  /** Amaldan KEYINGI o'qilmaganlar soni. */
  unreadCount: number
}

/** «O'qildi» so'rovi: aniq kalitlar YOKI `all: true`. */
export interface MarkReadInput {
  keys?: string[]
  /** `true` — kalitlar kerak emas, serverning o'zi hisoblaydi. */
  all?: boolean
}

/* ═════════════════════════════════════════════════ xaritalash (map) */

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * `names` tartibi C# sanoq qiymatlariga mos (1 dan boshlanadi).
 * Ikkalasi ham qabul qilinadi (son va nom) — `incidents-api.ts` dagi
 * bilan bir xil sabab: sanoq JSON sozlamasi o'zgarsa, ekran buzilmaydi.
 */
function enumValue<T extends string>(raw: unknown, names: readonly T[], fallback: T): T {
  if (typeof raw === 'number') return names[raw - 1] ?? fallback

  if (typeof raw === 'string') {
    const needle = raw.toLowerCase()
    return names.find((name) => name.toLowerCase() === needle) ?? fallback
  }

  return fallback
}

/**
 * Parametrlar lug'ati.
 *
 * FAQAT satr qiymatlar olinadi: server ham shunday yuboradi (son ham,
 * sana ham satr ko'rinishida). Boshqa tur kelib qolsa — u shunchaki
 * tushib qoladi va matnda o'rinbosar to'ldirilmay qoladi; bu «[object
 * Object]» chiqishidan afzal.
 */
function toParams(raw: unknown): Record<string, string> {
  if (raw === null || typeof raw !== 'object') return {}

  const out: Record<string, string> = {}
  for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
    if (typeof value === 'string') out[key] = value
  }
  return out
}

function toLink(raw: unknown): NotificationLink {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    route: enumValue(box['route'], NOTIFICATION_ROUTES, 'Employee'),
    id: text(box['id']),
  }
}

function toNotification(raw: unknown): AppNotification {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    key: text(box['key']) ?? '',
    type: enumValue(box['type'], NOTIFICATION_TYPES, 'ProbationEndingSoon'),
    severity: enumValue(box['severity'], NOTIFICATION_SEVERITIES, 'Info'),
    date: text(box['date']) ?? '',
    params: toParams(box['params']),
    link: toLink(box['link']),
    isRead: box['isRead'] === true,
    readAt: text(box['readAt']),
  }
}

function toActionResult(raw: unknown): NotificationActionResult {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    affected: typeof box['affected'] === 'number' ? box['affected'] : 0,
    unreadCount: typeof box['unreadCount'] === 'number' ? box['unreadCount'] : 0,
  }
}

/* ═══════════════════════════════════════════════════════════════ so'rovlar */

/**
 * Ro'yxat: o'qilganlari ham, o'qilmaganlari ham.
 *
 * NEGA `onlyUnread` parametri BERILMAYDI (server uni qo'llab-quvvatlasa
 * ham): modal oyna ikkita ko'rinishga bo'linmaydi. O'qilganlar o'sha
 * ro'yxatda, xiraroq bo'lib qoladi — «o'qilgan» degani «bajarilgan»
 * degani emas, va sinov muddati o'tib ketgan xodimni ro'yxatdan
 * yo'qotib qo'yish aynan shu tizim to'g'rilashi kerak bo'lgan xato.
 * Filtrni ko'rsatish uchun esa segment boshqaruvi, uning holati va
 * ikkinchi kesh kaliti kerak bo'lardi — «minimalistik» talabiga zid.
 */
export async function fetchNotifications(
  limit: number,
  signal?: AbortSignal,
): Promise<NotificationList> {
  const { data } = await api.get<unknown>('/api/notifications', {
    params: { limit },
    signal,
  })

  return toNotificationList(data)
}

/**
 * Xom javobni ro'yxatga o'giradi.
 *
 * NEGA eksport qilingan: AYNAN shu shakl JONLI OQIMDAN ham keladi
 * (`GET /api/notifications/stream`, `event: notifications`) va u
 * `fetch` orqali qo'lda o'qiladi — ya'ni axios'ning yo'lidan o'tmaydi.
 * Xaritalash ikkinchi marta yozilsa, bir kun ikkalasi ajralib ketardi:
 * oynadagi ro'yxat bir xil, jonli kelgani boshqacha bo'lardi.
 */
export function toNotificationList(raw: unknown): NotificationList {
  const box = (raw ?? {}) as Record<string, unknown>
  const items = Array.isArray(box['items']) ? box['items'].map(toNotification) : []

  return {
    items,
    // `total` yetib kelmasa ham ro'yxat uzunligi to'g'ri javob beradi
    total: typeof box['total'] === 'number' ? box['total'] : items.length,
    unreadCount:
      typeof box['unreadCount'] === 'number'
        ? box['unreadCount']
        : items.filter((item) => !item.isRead).length,
  }
}

/** Faqat sanoq — qo'ng'iroq ustidagi nishon uchun (butun ro'yxat tarmoqdan o'tmaydi). */
export async function fetchUnreadCount(signal?: AbortSignal): Promise<number> {
  const { data } = await api.get<unknown>('/api/notifications/unread-count', { signal })
  const box = (data ?? {}) as Record<string, unknown>

  return typeof box['unreadCount'] === 'number' ? box['unreadCount'] : 0
}

/** «O'qildi» belgisi. Idempotent: takroriy so'rov birinchi vaqtni o'zgartirmaydi. */
export async function markNotificationsRead(
  body: MarkReadInput,
): Promise<NotificationActionResult> {
  const { data } = await api.post<unknown>('/api/notifications/read', body)
  return toActionResult(data)
}

/**
 * Ro'yxatdan butunlay olib tashlash.
 *
 * ⚠️ QAYTARIB BO'LMAYDI: o'chirilgan bildirishnoma o'sha foydalanuvchi
 * uchun boshqa hech qachon chiqmaydi — sharti hamon rost bo'lsa ham.
 * Shu sababli chaqiruvchi tomonda tasdiqlash oynasi bor.
 */
export async function deleteNotifications(keys: string[]): Promise<NotificationActionResult> {
  const { data } = await api.post<unknown>('/api/notifications/delete', { keys })
  return toActionResult(data)
}
