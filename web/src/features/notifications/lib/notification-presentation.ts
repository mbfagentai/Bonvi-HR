import {
  Bot,
  Cake,
  CalendarClock,
  Gift,
  Timer,
  TimerOff,
  TriangleAlert,
  UserPlus,
  type LucideIcon,
} from 'lucide-react'

import { problemCode } from '@/shared/api/problem-details'

import type { AppNotification, NotificationSeverity, NotificationType } from '../api/notifications-api'

/**
 * Matn kalitlarining RO'YXATI — tarjima uchun yagona manba.
 *
 * Har bir yozuv ikkita kalit beradi: `notifications.type.<key>.title` va
 * `…​.text`. Kalitlar kodda SHABLON bilan yig'iladi
 * (`` t(`notifications.type.${key}.title`) ``), shu sababli shu massiv
 * `web/scripts/check-locales.mjs` dagi `DYNAMIC_KEY_SOURCES` ga ham
 * qo'shilgan — aks holda ular umuman tekshirilmay qolardi va tarjimasi
 * unutilgan tur ekranga XOM KALIT bo'lib chiqardi (loyihada aynan
 * shunday bo'shliqda `nav.users` xatosi ikki marta chiqqan).
 *
 * Bugun ro'yxat SERVER turlari bilan bir-bir mos (`NOTIFICATION_TYPES`),
 * lekin u SHART emas va shu sababli alohida massiv: bitta tur ikkita
 * matnga ajralishi mumkin — shunday hol ish taklifi turlarida bor edi
 * («2 kun qoldi» va «bugun tugaydi» bir jumla bilan yozilmaydi).
 *
 * Massivning to'liqligini TypeScript ta'minlaydi: pastdagi `TEXT_KEY`
 * har bir TUR uchun shu massivning A'ZOSINI talab qiladi.
 */
export const NOTIFICATION_TEXT_KEYS = [
  'probationEndingSoon',
  'probationEndsToday',
  'probationOverdue',
  'probationMissing',
  'birthdayToday',
  'birthdayTomorrow',
  'candidateNew',
  'candidateFromBot',
] as const

export type NotificationTextKey = (typeof NOTIFICATION_TEXT_KEYS)[number]

/**
 * Tur → matn kaliti.
 *
 * `Record<NotificationType, …>` ATAYLAB: yangi tur qo'shilib, bu yerga
 * yozilmasa — TypeScript kompilyatsiyani to'xtatadi. Ya'ni «tarjimasi
 * yo'q bildirishnoma» holati kodgacha ham yetib kelmaydi.
 */
const TEXT_KEY: Readonly<Record<NotificationType, NotificationTextKey>> = {
  ProbationEndingSoon: 'probationEndingSoon',
  ProbationEndsToday: 'probationEndsToday',
  ProbationOverdue: 'probationOverdue',
  ProbationMissing: 'probationMissing',
  BirthdayToday: 'birthdayToday',
  BirthdayTomorrow: 'birthdayTomorrow',
  CandidateNew: 'candidateNew',
  CandidateFromBot: 'candidateFromBot',
}

/** Bildirishnoma turiga mos matn kaliti. */
export function notificationTextKey(item: AppNotification): NotificationTextKey {
  return TEXT_KEY[item.type]
}

/* ════════════════════════════════════════════════ matn o'rinbosarlari */

/**
 * Sana va sonni foydalanuvchi tiliga o'giradigan funksiyalar.
 *
 * NEGA `t` BU YERDA YO'Q: `useLocale().t` — i18next'ning generik
 * `TFunction` i va uni oddiy `(key, values) => string` shakliga
 * keltirish TypeScript'da qat'iy rejimda ishonchsiz. Shu sababli bu
 * modul KALIT qaytaradi, `t` ni esa komponent o'zi chaqiradi — bu
 * loyihaning mavjud naqshi (`SEVERITY_LOOK.labelKey`, `safety-presentation.ts`).
 */
export interface NotificationFormatters {
  num: (value: number) => string
  date: (value: string) => string
}

/**
 * Matnga qo'yiladigan o'rinbosarlar.
 *
 * ⚠️ ATAYLAB `type` (interfeys emas) va ATAYLAB `Record<string, string>`
 * emas. i18next'ning `t(key, values)` ikkinchi argumenti indeks imzosi
 * bo'lgan tipni talab qiladi, lekin unda `count?: number` kabi O'Z
 * maydonlari ham bor. `Record<string, string>` berilsa, TypeScript
 * `count` ni `string` deb hisoblab, moslikni rad etadi; `interface`
 * berilsa esa indeks imzosi yashirin hosil bo'lmaydi. `type` bilan
 * e'lon qilingan aniq maydonlar ikkala shartni ham qanoatlantiradi.
 */
export type NotificationValues = {
  name: string
  date: string
  /** Faqat kun soni bor turlarda. */
  days?: string
}

/**
 * Bildirishnoma matniga qo'yiladigan o'rinbosarlar.
 *
 * To'rttala til uchun BITTA nomlash sxemasi: `{{name}}`, `{{date}}`,
 * `{{days}}`. NEGA server nomlari («employeeName», «endDate»,
 * «daysLeft») to'g'ridan-to'g'ri ishlatilmaydi: u holda har bir turning
 * o'rinbosarlari boshqacha bo'lardi va tarjimon (yoki keyingi dasturchi)
 * har safar shartnoma jadvaliga qarab qaysi nomni yozishni tekshirishga
 * majbur bo'lardi. Bir xil uchta nom bilan har qanday turning matnini
 * jadvalga qaramay yozib bo'ladi.
 *
 * Sana va kun parametrlari TARTIB bilan qidiriladi: shartnoma bo'yicha
 * har bir turda ulardan AYNAN BITTASI bo'ladi (sinovda `endDate`,
 * qabulda `hireDate`, tug'ilgan kunda `birthDate` va h.k.), ya'ni
 * birinchi topilgani — o'sha turning yagona sanasi.
 */
export function notificationValues(
  item: AppNotification,
  fmt: NotificationFormatters,
): NotificationValues {
  const rawDate =
    item.params['endDate'] ??
    item.params['hireDate'] ??
    item.params['birthDate'] ??
    item.params['addedOn']

  const values: NotificationValues = {
    // Ism — xodimniki yoki nomzodniki. Ikkalasi bir vaqtda hech qachon
    // kelmaydi, shu sababli birini tanlash yetarli.
    name: item.params['employeeName'] ?? item.params['candidateName'] ?? '',

    // Sana topilmasa — bildirishnomaning O'Z sanasi ishlatiladi: u har
    // doim bor va aynan shu hodisaga ishora qiladi.
    date: fmt.date(rawDate ?? item.date),
  }

  const rawDays =
    item.params['daysLeft'] ?? item.params['daysOverdue'] ?? item.params['daysWorked']

  /*
   * Kunlar serverdan DOIM musbat keladi — ishorani («qoldi» yoki
   * «o'tdi») matnning o'zi tashiydi. Shu sababli bu yerda hech qanday
   * belgi qo'shilmaydi, faqat tilga mos formatlanadi.
   */
  const days = Number(rawDays)
  if (rawDays !== undefined && Number.isFinite(days)) {
    values.days = fmt.num(days)
  }

  return values
}

/* ═══════════════════════════════════════════════════ qo'shimcha satr */

/**
 * Matn ostidagi kichik satrning bitta bo'lagi.
 *
 * Ikki xil bo'lishining sababi: bo'lim, lavozim va vakansiya nomi —
 * BAZADAGI matn (tarjima qilinmaydi), «34 yosh» va «Qo'lda» esa
 * tarjima kaliti. Ikkalasini bitta tipda birlashtirish komponentda
 * `t()` chaqiruvini shartli qilib qo'yardi.
 */
export type NotificationMetaItem =
  | { kind: 'text'; value: string }
  /** `values` — faqat sonli o'rinbosar («{{n}} yosh»); tipi haqidagi izoh `NotificationValues` da. */
  | { kind: 'key'; key: string; values?: { n: string } }

/**
 * Nomzod manbasi — REKRUTING modulining mavjud tarjimalari.
 *
 * NEGA shablon (`` `recruiting.source.${…}` ``) emas, aniq xarita:
 * shablon bo'lsa `check-locales.mjs` uni ko'rmasdi (u faqat `t(...)`
 * ichidagi shablonlarni skanerlaydi, obyektda yig'ilgan satrni emas) va
 * kalit jimgina yo'qolib ketishi mumkin edi. Xarita bilan noma'lum
 * manba shunchaki satrga tushmaydi — xom kalit ekranga chiqmaydi.
 */
const SOURCE_KEY: Readonly<Record<string, string>> = {
  Manual: 'recruiting.source.manual',
  TelegramBot: 'recruiting.source.telegramBot',
}

/**
 * Matn ostidagi kontekst satri: bo'lim · lavozim · vakansiya · yosh ·
 * manba.
 *
 * NEGA bu asosiy matnga QO'SHILMAYDI. Ixtiyoriy parametrlarni jumlaga
 * kiritish uchun har bir tur bo'yicha bir nechta matn varianti kerak
 * bo'lardi («… bo'lim bilan», «… bo'limsiz») — o'nta o'rniga qirqta
 * kalit, to'rt tilda. Ular ALOHIDA satrda esa bittasi yo'q bo'lsa
 * shunchaki tushib qoladi va jumla buzilmaydi.
 *
 * Tartib qat'iy va sarlavhadagi ro'yxat bilan bir xil: avval odam
 * QAYERGA tegishli (bo'lim, lavozim, vakansiya), keyin uning O'ZI
 * haqidagi fakt (yosh) va oxirida yozuvning kelib chiqishi (manba).
 * Bir xil tartib ro'yxatda ko'zni o'rgatadi: qatordan qatorga o'tganda
 * bir xil ma'lumot bir xil joyda turadi.
 */
export function notificationMeta(
  item: AppNotification,
  fmt: NotificationFormatters,
): NotificationMetaItem[] {
  const meta: NotificationMetaItem[] = []

  const push = (value: string | undefined) => {
    if (value !== undefined && value !== '') meta.push({ kind: 'text', value })
  }

  push(item.params['departmentName'])
  push(item.params['positionName'])
  push(item.params['vacancyTitle'])

  const age = Number(item.params['age'])
  if (item.params['age'] !== undefined && Number.isFinite(age)) {
    meta.push({ kind: 'key', key: 'notifications.age', values: { n: fmt.num(age) } })
  }

  const sourceKey = SOURCE_KEY[item.params['source'] ?? '']
  if (sourceKey !== undefined) meta.push({ kind: 'key', key: sourceKey })

  return meta
}

/* ═══════════════════════════════════════════════════════════ ko'rinish */

/**
 * Muhimlik darajasining rangi — ikona, nuqta va O'QILMAGAN qator foni.
 *
 * `Info` xom `ios-blue` emas, tint tokenlarini ishlatadi — `alert.tsx`
 * dagi bilan AYNAN bir xil sabab: `#007AFF` yorug' fonda 3.4:1 beradi
 * (AA dan past), `--accent-on-soft` esa 4.76:1.
 *
 * <b>`unreadRow` — o'qilmagan qatorning foni.</b> O'qilgan qator OQ
 * qoladi, o'qilmagani esa o'z darajasining rangida tiniqlashadi. Nega
 * kerak: ro'yxat endi VAQT bo'yicha tartiblanadi (foydalanuvchi
 * qarori), ya'ni jiddiy yozuv o'rtada turishi mumkin. Ilgari uni
 * TARTIB ajratib turardi, endi — RANG.
 *
 * <b>Nega har bir TUR uchun alohida rang emas.</b> Turlar sakkizta;
 * sakkizta fon rangi ro'yxatni chiziqli bezakka aylantirardi va hech
 * qanday savolga javob bermasdi. Turni SHAKL (ikona) aytadi, rang esa
 * shoshilinchlikni — bu loyihaning mavjud qoidasi (`TYPE_ICON` izohi)
 * va u shu yerda ham buzilmaydi.
 *
 * Shaffoflik ATAYLAB past (8–12%): fon matnni o'qishga xalaqit
 * bermasligi kerak, uning vazifasi — ko'zni tortish, e'tiborni
 * o'g'irlash emas. Uchta qiymat teng emas, chunki ranglarning o'z
 * to'yinganligi boshqa: qizil sariqdan quyuqroq ko'rinadi.
 */
export const SEVERITY_LOOK: Readonly<
  Record<NotificationSeverity, { icon: string; dot: string; unreadRow: string }>
> = {
  Info: {
    icon: 'text-[var(--accent-on-soft)]',
    dot: 'bg-[var(--accent-on-soft)]',
    unreadRow: 'bg-[var(--accent)]/8',
  },
  Warning: {
    icon: 'text-ios-orange',
    dot: 'bg-ios-orange',
    unreadRow: 'bg-ios-orange/12',
  },
  Critical: {
    icon: 'text-ios-red',
    dot: 'bg-ios-red',
    unreadRow: 'bg-ios-red/10',
  },
}

/**
 * Har bir tur uchun ikona.
 *
 * NEGA rang emas, SHAKL ham kerak: ranglar atigi uchta (uchta daraja),
 * turlar esa sakkizta — bir xil to'q sariq nuqta «sinov tugayapti» ni
 * ham, «sinov yozuvi yo'q» ni ham bildirardi. Ikona ro'yxatni matnni
 * o'qimasdan ajratish imkonini beradi; rang esa qanchalik shoshilinch
 * ekanini aytadi. Ikkalasi bir-birini takrorlamaydi.
 */
export const TYPE_ICON: Readonly<Record<NotificationType, LucideIcon>> = {
  ProbationEndingSoon: Timer,
  ProbationEndsToday: CalendarClock,
  ProbationOverdue: TimerOff,
  ProbationMissing: TriangleAlert,
  BirthdayToday: Cake,
  BirthdayTomorrow: Gift,
  CandidateNew: UserPlus,

  /*
    Bot ikonasi ATAYLAB odam ikonasidan boshqa. Ro'yxatda ikkalasi
    yonma-yon tursa, HR «kim qo'shdi?» degan savolga matnni o'qimasdan
    javob oladi — bu esa aynan shu ikkisining yagona muhim farqi.
  */
  CandidateFromBot: Bot,
}

/**
 * Xatodan tarjima qilinadigan kodni ajratib oladi.
 *
 * Mantiqning o'zi `shared/api/problem-details.ts` da — u HTTP
 * shartnomasiga tegishli. Shu nom esa qoladi (`recruiting-error.ts`,
 * `catalog-error.ts` bilan bir xil naqsh): chaqiruv joyida «qaysi
 * bo'limning xatosi» degan savol ko'rinib tursin.
 *
 * ⚠️ Bildirishnoma xato kodlari (`errors.bildirishnoma.*`) TO'RT TILGA
 * ALLAQACHON tarjima qilingan — `useErrorMessage` ularni o'zi topadi.
 */
export function notificationErrorCode(error: unknown): string {
  return problemCode(error)
}
