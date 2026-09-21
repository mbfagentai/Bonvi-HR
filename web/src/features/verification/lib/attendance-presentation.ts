import {
  AlarmClock,
  BriefcaseBusiness,
  ChartColumn,
  CalendarCheck,
  CalendarClock,
  CircleCheck,
  CircleHelp,
  CircleSlash,
  Globe,
  IdCard,
  LogIn,
  LogOut,
  Network,
  SearchX,
  Sparkles,
  UserCheck,
  UserMinus,
  UserRound,
  UserSearch,
  UserX,
  type LucideIcon,
} from 'lucide-react'

import { tashkentParts } from '@/shared/lib/timezone'
import type { LocaleCode } from '@/shared/i18n/locales'
import type { BadgeTone } from '@/shared/ui'
import type {
  AttendanceDayStatus,
  DashboardCounts,
  DashboardStatus,
  ExceptionKind,
  IdentityMatchKind,
  ImportPreviewRow,
  PassDirection,
  PassMethod,
  ScheduleScope,
  WorkScheduleDayInput,
} from '../api/attendance-api'

/* ═════════════════════════════════════════════════════════════ RUXSATLAR */

/**
 * Ruxsat kodlari — backend'dagi `Bonvi.Domain.Identity.Permissions` bilan
 * AYNAN bir xil (`employee-presentation.ts` naqshi). Satrni har joyda
 * qo'lda yozish o'rniga konstanta: yozuv xatosi 403 ga aylanmaydi.
 *
 * Butun modul IKKITA ruxsat bilan boshqariladi (SPEC §7.1):
 * `attendance.view` — statistika, jurnal, tabel, hisobotlar;
 * `attendance.manage` — qurilmalar, jadvallar, biriktirmalar, moslashtirish,
 * import va qayta hisoblash, ya'ni RAQAMLARNING MA'NOSINI o'zgartiradigan
 * hamma narsa.
 *
 * ⚠️ Qurilmalar va mosliklar O'QISH uchun ham `manage` talab qiladi
 * (SPEC §7.3): qurilma ro'yxatida token prefikslari, IP ro'yxati va
 * binoning kirish joylari; moslik ro'yxati esa terminal raqamidan aniq
 * odamga olib boruvchi xarita. Ikkalasi ham oddiy ko'ruvchiga kerak emas.
 */
export const ATTENDANCE_VIEW = 'attendance.view'
export const ATTENDANCE_MANAGE = 'attendance.manage'

/**
 * Yo'qlik so'rovlari bo'yicha QAROR — alohida ruxsat (SPEC-so'rovlar §B4).
 *
 * `attendance.manage` dan farqi: u qurilma va jadvalni sozlaydi, bu esa
 * kadrlar qarorini qabul qiladi. Ikkalasi bir odamda bo'lishi shart emas.
 */
export const ATTENDANCE_REQUESTS_MANAGE = 'attendance.requests.manage'

/*
 * Excelga yuklash — `attendance.view` EMAS, alohida `reports.export`
 * (SPEC §7.3). Konstanta xodimlar modulidan QAYTA EKSPORT qilinadi:
 * kod bitta va ikkinchi nusxa yozilsa, biri o'zgarganda ikkinchisi
 * jimgina eskirardi (`recruiting-queries.ts` dagi qayta eksportlar bilan
 * bir xil mulohaza).
 */
export { REPORTS_EXPORT } from '@/features/employees/lib/employee-presentation'

/* ══════════════════════════════════════════════════════════ CHEGARALAR */

/*
 * Serverdagi `AttendanceRules` ning KO'ZGUSI (SPEC §2.8).
 *
 * ⚠️ Haqiqat manbasi — SERVER. Bu yerdagi qiymatlar faqat interfeys
 * foydalanuvchini kutib turmasdan ogohlantirishi uchun: 92 kunlik
 * chegarani bosishdan OLDIN aytish 400 xatosini o'qishdan tushunarliroq.
 * Hisob-kitobning o'zi hech qachon bu yerda takrorlanmaydi.
 */

/*
 * ⚠️ Qurilma soati chegarasi (300 soniya) bu yerda ATAYLAB YO'Q: o'tish va
 * qurilma DTO sidagi `skewWarning` ni server hisoblaydi (SPEC §6.5). Uning
 * TypeScript'dagi nusxasi server chegarasi o'zgarganda jurnalni jimgina
 * boshqa gapga majbur qilardi.
 */

/**
 * Bugungi statistika va ishdan ketganlar ogohlantirishi 30 soniyada bir
 * yangilanadi (SPEC §1.2 #9 — jonli taxtaning maromi, statistika uning
 * o'rnini egalladi).
 */
export const BOARD_REFRESH_MS = 30_000

/**
 * Menyudagi «moslanmagan raqamlar» nishoni — daqiqada bir marta.
 *
 * Taxtadan sekinroq: bu xavfsizlik ogohlantirishi emas, ishlar navbati;
 * har ekrandagi har bir HR foydalanuvchisi uchun 30 soniyalik so'rov
 * foydasiz yuk bo'lardi.
 */
export const UNMATCHED_COUNT_REFRESH_MS = 60_000

/** Bitta hisobot so'rovidagi eng uzun davr — bir chorak (`davomat.davr_uzun`). */
export const MAX_REPORT_DAYS = 92

/** Import faylidagi qatorlar chegarasi (`davomat.import_qator_kop`). */
export const MAX_IMPORT_ROWS = 5000

/** Import faylining hajmi — 10 MB (`davomat.import_fayl_katta`). */
export const MAX_IMPORT_FILE_BYTES = 10 * 1024 * 1024

/** Sahifalash — SPEC §6 (`SearchEmployeesQuery` bilan bir xil). */
export const DEFAULT_PAGE_SIZE = 25
export const MAX_PAGE_SIZE = 200

/* ══════════════════════════════════════════════════════════ KUN HOLATI */

export interface DayStatusLook {
  tone: BadgeTone
  icon: LucideIcon
  /** `attendance.status<Key>` tarjima kaliti (SPEC §8.8). */
  labelKey: string
  /**
   * Tabel katagining foni.
   *
   * ⭐ Holat CHEGARA yoki IKONA bilan emas, 10–14 % shaffoflikdagi
   * TO'LDIRISH bilan ko'rsatiladi (SPEC §8.6). Sabab tizimning mavjud
   * qoidasi: ajratish ingichka chiziq bilan emas, oraliq va yumshoq fon
   * bilan beriladi (`badge.tsx`, `icon-tile.tsx` ham shunday). 31 ustunli
   * gridda chegara chizig'i esa butun ekranni to'rga aylantirardi.
   */
  cellClass: string
}

/**
 * Kun holatining ko'rinishi (SPEC §5.6 — ma'nolari, §8.6 — ranglari).
 *
 * Rang ma'no tashiydi, bezak emas:
 *   · `Present` — OQ. Normal kun ko'zga tashlanmasligi kerak; gridda
 *     ko'rinadigan narsa faqat MUAMMO bo'lishi lozim.
 *   · `Absent` — qizil: kutilgan odam kelmagan.
 *   · `DayOff` — kulrang: kutilmagan ham.
 *   · `DayOffWorked` — binafsha: bu na muammо, na oddiy kun — dam olish
 *     kunida ishlangan va HR uni ALOHIDA ko'rishi kerak.
 *   · `MissingExit` / `MissingEntry` — sariq: ma'lumot to'liq emas, bu
 *     xodimning emas, TIZIMNING muammosi (SPEC §5.8 dagi to'rt tuzatish).
 *   · `NoSchedule` — chiziqli shtrix: rang emas, «bu katak hisoblanmagan»
 *     degan alohida vizual sinf. Kulrang bo'lsa dam olish kuni bilan
 *     chalkashardi.
 *   · `NotEmployed` — BO'SH: odam bu sanada umuman ishlamagan, ya'ni
 *     katakda ko'rsatiladigan hech narsa yo'q.
 *
 * ⚠️ Kechikish bu yerda YO'Q va bo'lmaydi ham: u holat emas, RAQAM
 * (SPEC §5.6). Odam kechikib kelib, erta ketib, baribir `Present`
 * bo'lishi mumkin. Gridda kechikish katakning pastki qirrasidagi
 * sariq chiziq bilan ko'rsatiladi (`LATE_BAR_CLASS`).
 */
export const DAY_STATUS_LOOK: Readonly<Record<AttendanceDayStatus, DayStatusLook>> = {
  Present: {
    tone: 'success',
    icon: CircleCheck,
    labelKey: 'attendance.statusPresent',
    cellClass: 'bg-[var(--bg-elevated)] text-[var(--fg)]',
  },
  Absent: {
    tone: 'danger',
    icon: UserX,
    labelKey: 'attendance.statusAbsent',
    cellClass: 'bg-ios-red/12 text-ios-red',
  },
  DayOff: {
    tone: 'neutral',
    icon: CircleSlash,
    labelKey: 'attendance.statusDayOff',
    cellClass: 'bg-[var(--bg-sunken)] text-[var(--fg-tertiary)]',
  },
  DayOffWorked: {
    tone: 'purple',
    icon: Sparkles,
    labelKey: 'attendance.statusDayOffWorked',
    cellClass: 'bg-ios-purple/12 text-ios-purple',
  },
  MissingExit: {
    tone: 'warning',
    icon: LogOut,
    labelKey: 'attendance.statusMissingExit',
    cellClass: 'bg-ios-orange/12 text-ios-orange',
  },
  MissingEntry: {
    tone: 'warning',
    icon: LogIn,
    labelKey: 'attendance.statusMissingEntry',
    cellClass: 'bg-ios-orange/12 text-ios-orange',
  },
  NoSchedule: {
    tone: 'neutral',
    icon: CircleHelp,
    labelKey: 'attendance.statusNoSchedule',
    /*
      Diagonal shtrix — `--border` rangida, ya'ni tungi rejimda ham
      o'z-o'zidan to'g'ri chiqadi. Matn rangi eng past darajada: katakda
      raqam bo'lsa ham u asosiy ma'lumot emas.
    */
    cellClass:
      'bg-[image:repeating-linear-gradient(45deg,transparent,transparent_4px,var(--border)_4px,var(--border)_5px)] text-[var(--fg-tertiary)]',
  },
  NotEmployed: {
    tone: 'neutral',
    icon: UserMinus,
    labelKey: 'attendance.statusNotEmployed',
    cellClass: 'bg-transparent text-[var(--fg-tertiary)]',
  },
  /*
    «Sababli» — ko'k: kelmagan, LEKIN bu muammo emas. Qizil (kelmadi) bilan
    yonma-yon turganda farq bir qarashda ko'rinishi kerak, kulrang (dam
    olish kuni) esa «bu kun umuman ish kuni emas» degan boshqa gapni
    aytardi. Tasdiqlangan so'rov — ma'lum va hujjatlashtirilgan yo'qlik,
    ya'ni «axborot» ohangida (SPEC-so'rovlar §B3).
  */
  Excused: {
    tone: 'info',
    icon: CalendarCheck,
    labelKey: 'attendance.statusExcused',
    cellClass: 'bg-ios-blue/12 text-ios-blue',
  },
  /*
    «Kun kelmagan» — kelajakdagi ish kuni. Katak BO'SH ko'rinadi (fon ham,
    matn rangi ham oddiy), chunki ayta oladigan gapimiz yo'q: ertaga
    kelmaganini bugun bilmaymiz. Nishon sifatida u deyarli chiqmaydi —
    bu holat faqat tabel katagida uchraydi.
  */
  Planned: {
    tone: 'neutral',
    icon: CalendarClock,
    labelKey: 'attendance.statusPlanned',
    cellClass: 'bg-[var(--bg-elevated)] text-[var(--fg-quaternary)]',
  },
}

/**
 * Kechikish belgisi — katakning pastki qirrasidagi 3 px sariq chiziq.
 *
 * NEGA ikkinchi RANG emas: «kechikdi, lekin keldi» ikki xabar va ular
 * bitta katakda birga turishi kerak (SPEC §8.6). Fon holatni aytadi,
 * chiziq esa kechikishni — ular bir-birini bosmaydi.
 *
 * ⚠️ `relative` va `content-['']` sinfning O'ZIDA turadi: birinchisisiz
 * chiziq katakka emas, eng yaqin joylashtirilgan ajdodga yopishardi,
 * ikkinchisisiz esa psevdo-element umuman chizilmaydi (Tailwind qoidasi).
 * Ikkalasini chaqiruvchiga qoldirish — bittasini unutish demak.
 */
export const LATE_BAR_CLASS =
  "relative after:absolute after:inset-x-0 after:bottom-0 after:h-[3px] after:bg-ios-orange after:content-['']"

/**
 * Legenda tartibi — sahifa tepasida DOIM ko'rinib turadi (SPEC §8.6).
 *
 * Tartib tasodifiy emas: avval kundalik holatlar, keyin muammolar,
 * oxirida «hisoblanmagan» kataklar — ya'ni chapdan o'ngga
 * «hammasi joyida» dan «e'tibor kerak» ga.
 */
export const DAY_STATUS_LEGEND: readonly AttendanceDayStatus[] = [
  'Present',
  'Absent',
  'DayOff',
  'DayOffWorked',
  'MissingExit',
  'MissingEntry',
  'NoSchedule',
  'NotEmployed',
]

/* ═══════════════════════════════════════════════════════ O'TISH JURNALI */

export interface ChipLook {
  tone: BadgeTone
  icon: LucideIcon
  labelKey: string
}

/**
 * Yo'nalish.
 *
 * `Unknown` — kulrang va bu MUHIM: u xato emas, «qurilma aytmadi»
 * degani (SPEC §3.7). Qizil bo'lsa operator uni nosozlik deb o'ylab,
 * bor-yo'g'i roli noto'g'ri qo'yilgan terminalni almashtirishga
 * urinardi.
 */
export const DIRECTION_LOOK: Readonly<Record<PassDirection, ChipLook>> = {
  In: { tone: 'success', icon: LogIn, labelKey: 'attendance.directionIn' },
  Out: { tone: 'info', icon: LogOut, labelKey: 'attendance.directionOut' },
  Unknown: { tone: 'neutral', icon: CircleHelp, labelKey: 'attendance.directionUnknown' },
}

/**
 * O'tish usuli — faqat YORLIQ, ikonasiz.
 *
 * NEGA ikona yo'q: jurnal qatorida allaqachon surat, yo'nalish chipi va
 * bayroqlar bor. Oltita usul uchun oltita ikona qo'shish qatorni
 * shovqinga to'ldirardi va hech qanday savolga javob bermasdi — usul
 * kamdan-kam qaraladigan ustun.
 */
export const METHOD_LABEL_KEY: Readonly<Record<PassMethod, string>> = {
  Face: 'attendance.methodFace',
  Card: 'attendance.methodCard',
  Fingerprint: 'attendance.methodFingerprint',
  Password: 'attendance.methodPassword',
  Other: 'attendance.methodOther',
  Unknown: 'attendance.methodUnknown',
}

/* ═══════════════════════════════════════════════════ DAVOMAT STATISTIKASI */

export interface DashboardStatusLook {
  /** `DashboardCounts` dagi mos maydon — bo'lak va legenda raqami shundan. */
  count: keyof DashboardCounts
  /** Legenda va diagramma yorlig'i — guruh nomi («Kechikkanlar»). */
  legendKey: string
  /** Jadval qatoridagi holat matni — bitta odam haqida («Kelmadi»). */
  rowKey: string
  /**
   * recharts uchun rang — `var(--color-ios-…)` ko'rinishida, JS da
   * o'qilmaydi: tungi rejim almashganda SVG o'zi qayta bo'yaladi
   * (`turnover-chart.tsx` boshidagi izoh).
   */
  fill: string
  dotClass: string
  textClass: string
}

/**
 * Oltita holatning ko'rinishi (Verifix'dagi «Статистика посещений» bilan
 * bir xil to'plam va tartib).
 *
 * Ranglar faqat iOS tizim ranglari va har biri MA'NO tashiydi:
 *   · yashil — vaqtida, sariq (to'q sariq) — kechikkan, qizil — kelmagan:
 *     HR ko'zi birinchi shu uchtasini qidiradi;
 *   · indigo — ish kuni hali boshlanmagan: bu muammo EMAS, lekin «dam
 *     olish» ham emas — alohida, sokin rang;
 *   · teal (och ko'k) — dam olish kuni: kutilmagan odam;
 *   · kulrang — jadval yo'q: bu kun hisoblanmagan, rang bermaslik uning
 *     o'zi xabar.
 *
 * ⚠️ `ios-blue` ATAYLAB yo'q — tizimda ko'k «bu bosiladigan narsa»
 * degani (`globals.css` dagi tint izohi) va uni holat rangi qilish
 * ekrandagi yagona tint signalini zaiflashtirardi.
 */
export const DASHBOARD_STATUS_LOOK: Readonly<Record<DashboardStatus, DashboardStatusLook>> = {
  OnTime: {
    count: 'onTime',
    legendKey: 'attendance.statsOnTime',
    rowKey: 'attendance.statsRowOnTime',
    fill: 'var(--color-ios-green)',
    dotClass: 'bg-ios-green',
    textClass: 'text-ios-green',
  },
  Late: {
    count: 'late',
    legendKey: 'attendance.statsLate',
    rowKey: 'attendance.statsRowLate',
    fill: 'var(--color-ios-orange)',
    dotClass: 'bg-ios-orange',
    textClass: 'text-ios-orange',
  },
  Absent: {
    count: 'absent',
    legendKey: 'attendance.statsAbsent',
    rowKey: 'attendance.statsRowAbsent',
    fill: 'var(--color-ios-red)',
    dotClass: 'bg-ios-red',
    textClass: 'text-ios-red',
  },
  NotStarted: {
    count: 'notStarted',
    legendKey: 'attendance.statsNotStarted',
    rowKey: 'attendance.statsNotStarted',
    fill: 'var(--color-ios-indigo)',
    dotClass: 'bg-ios-indigo',
    textClass: 'text-ios-indigo',
  },
  DayOff: {
    count: 'dayOff',
    legendKey: 'attendance.statsDayOff',
    rowKey: 'attendance.statsDayOff',
    fill: 'var(--color-ios-teal)',
    dotClass: 'bg-ios-teal',
    textClass: 'text-ios-teal',
  },
  NoSchedule: {
    count: 'noSchedule',
    legendKey: 'attendance.statsNoSchedule',
    rowKey: 'attendance.statsNoSchedule',
    fill: 'var(--color-ios-gray)',
    dotClass: 'bg-ios-gray',
    textClass: 'text-[var(--fg-secondary)]',
  },
  // Rang tabeldagi «Sababli» bilan AYNAN bir xil (ko'k): bir kun ikki
  // ekranda ikki xil rangda ko'rinmasligi kerak.
  Excused: {
    count: 'excused',
    legendKey: 'attendance.statsExcused',
    rowKey: 'attendance.statsRowExcused',
    fill: 'var(--color-ios-blue)',
    dotClass: 'bg-ios-blue',
    textClass: 'text-ios-blue',
  },
}

/**
 * Legenda, diagramma va manzil tekshiruvi uchun YAGONA tartib.
 *
 * Nol sanoqli holat ham ro'yxatdan TUSHMAYDI (shartnoma): «Kechikkanlar
 * 0» — ham ma'lumot, legenda esa kun sayin shaklini o'zgartirmasligi
 * kerak.
 */
export const DASHBOARD_STATUSES: readonly DashboardStatus[] = [
  'OnTime',
  'Late',
  'Absent',
  // «Sababli» — «Kelmadi» dan keyin: ular bitta savolning ikki javobi.
  'Excused',
  'NotStarted',
  'DayOff',
  'NoSchedule',
]

/**
 * Panelning ko'rinish turi.
 *
 * `donut` — sukut (Verifix'dagi «Круговая диаграмма»), `bars` — ustunli,
 * `list` — DIAGRAMMASIZ: jadval kartaning butun kengligini oladi.
 *
 * ⚠️ `list` «yashirilgan diagramma» EMAS. Diagramma ustuni umuman
 * chizilmaydi: nol balandlikdagi recharts konteyneri hisoblanishda
 * davom etardi va jadval o'sha ustunning kengligini baribir yo'qotmasdi.
 */
export const DASHBOARD_VIEWS = ['donut', 'bars', 'list'] as const

export type DashboardView = (typeof DASHBOARD_VIEWS)[number]

export const DEFAULT_DASHBOARD_VIEW: DashboardView = 'donut'

/**
 * Davr grafigida sukut bo'yicha YONIQ qatorlar (egasining qarori, 2026-09-18).
 *
 * Oltalasi birdan chizilsa grafik o'qib bo'lmaydigan chigal bo'ladi. Uchtasi
 * — HR har kuni qaraydigan savol: «vaqtida keldimi, kechikdimi, kelmadimi».
 * Qolgan uchtasi («boshlanmagan», «dam olish», «grafik yo'q») kunning
 * SHAKLINI tasvirlaydi va kerak bo'lganda qo'lda yoqiladi.
 */
export const DEFAULT_TREND_SERIES: readonly DashboardStatus[] = ['OnTime', 'Late', 'Absent']

/**
 * Ulush, foizda, BITTA kasr xonasi bilan: 75 / 157 → 47.8.
 *
 * `null` — xodim yo'q (`total = 0`): «0 %» bu yerda yolg'on bo'lardi,
 * ekran «—» ko'rsatadi. Hisob klientda, chunki server faqat sanoq beradi
 * (shartnoma) — foiz ularning ko'rinishi, yangi raqam emas.
 */
export function dashboardPercent(count: number, total: number): number | null {
  if (!Number.isFinite(total) || total <= 0) return null
  return Math.round((count / total) * 1000) / 10
}

/**
 * «47,8%» (inglizchada «47.8%»), `null` → «—».
 *
 * Kasr xonasi DOIM bitta («50,0%»): legenda katakchalarida raqamlar bir
 * xil kenglikda turib, ko'z ularni ustun bo'ylab solishtiradi. Ajratgich
 * `formatHours` dagi qoida bilan: o'zbek va rus tilida vergul.
 */
export function formatPercent(value: number | null, locale: LocaleCode): string {
  if (value === null) return '—'

  const fixed = value.toFixed(1)
  return `${locale === 'en' ? fixed : fixed.replace('.', ',')}%`
}

/* ══════════════════════════════════════════════════════════ HISOBOTLAR */

/**
 * Hisobotlar sahifasidagi BESHTA tab (SPEC §8.4).
 *
 * Birinchi to'rttasi `/reports/exceptions` ga `kind` bilan boradi,
 * beshinchisi esa butunlay boshqa endpoint'ga (`/reports/summary`) —
 * shu sababli ular bitta sanoq emas, balki shu yerda birlashtirilgan
 * ro'yxat. Tab manzilda yashaydi (`attendance-search.ts`), ya'ni
 * havolani bo'lishish mumkin.
 */
export type ReportKind = ExceptionKind | 'Summary'

export const REPORT_KINDS: readonly ReportKind[] = [
  'Late',
  'EarlyLeave',
  'Absent',
  'Missing',
  'Summary',
]

export const REPORT_KIND_LOOK: Readonly<Record<ReportKind, ChipLook>> = {
  Late: { tone: 'warning', icon: AlarmClock, labelKey: 'attendance.kindLate' },
  EarlyLeave: { tone: 'warning', icon: LogOut, labelKey: 'attendance.kindEarlyLeave' },
  Absent: { tone: 'danger', icon: UserX, labelKey: 'attendance.kindAbsent' },
  /*
    «Muammoli kunlar» UCHTA holatni qamraydi: `MissingExit`,
    `MissingEntry` va `NoSchedule` (SPEC §6.6) — «bu kunni hisoblab
    bo'lmadi» degan yagona ro'yxat, HR aynan shuni quvadi.
  */
  Missing: { tone: 'neutral', icon: CircleHelp, labelKey: 'attendance.kindMissing' },
  Summary: { tone: 'info', icon: ChartColumn, labelKey: 'attendance.kindSummary' },
}

/** Faqat `Late` va `EarlyLeave` da «eng kam daqiqa» filtri ma'noli (SPEC §6.6). */
export function supportsMinMinutes(kind: ReportKind): boolean {
  return kind === 'Late' || kind === 'EarlyLeave'
}

/* ══════════════════════════════════════════════════════ MOSLASHTIRISH */

export type ImportDecision = 'confirm' | 'choose' | 'skip'

export interface MatchLook {
  tone: BadgeTone
  icon: LucideIcon
  labelKey: string
  /**
   * Qator OCHILGANDA qanday holatda turadi (SPEC §8.4).
   *
   * `confirm` — tayyor, operator faqat ko'z yugurtiradi;
   * `choose` — QAROR kerak, oldindan hech narsa tanlanmaydi;
   * `skip` — allaqachon bog'langan, qayta yozilmaydi.
   */
  decision: ImportDecision
}

/**
 * Import natijasining ko'rinishi va standart qarori.
 *
 * ⚠️ `Ambiguous` va `None` uchun standart qaror ATAYLAB `choose`:
 * ularni oldindan `confirm` qilib qo'yish operatorga bir bosishda
 * «hammasini tasdiqlash» imkonini berardi va noto'g'ri odamga bog'langan
 * terminal raqami butun tabelni jimgina buzardi. Moslikni tuzatish esa
 * arzon emas — u orqaga qayta hisoblashni talab qiladi (SPEC §4.3).
 */
export const MATCH_LOOK: Readonly<Record<IdentityMatchKind, MatchLook>> = {
  ExactPinfl: {
    tone: 'success',
    icon: IdCard,
    labelKey: 'attendance.matchExactPinfl',
    decision: 'confirm',
  },
  ExactName: {
    tone: 'success',
    icon: UserCheck,
    labelKey: 'attendance.matchExactName',
    decision: 'confirm',
  },
  Ambiguous: {
    tone: 'warning',
    icon: UserSearch,
    labelKey: 'attendance.matchAmbiguous',
    decision: 'choose',
  },
  None: {
    tone: 'neutral',
    icon: SearchX,
    labelKey: 'attendance.matchNone',
    decision: 'choose',
  },
  AlreadyMapped: {
    tone: 'info',
    icon: CircleCheck,
    labelKey: 'attendance.matchAlreadyMapped',
    decision: 'skip',
  },
}

/**
 * Oldindan ko'rish jadvalining tartibi: QAROR KERAK bo'lgan qatorlar
 * tepada (SPEC §8.4).
 *
 * NEGA: 5000 qatorli faylda tasdiqlashga tayyor qatorlar oxirigacha
 * cho'zilib ketadi va qaror talab qiladigan o'ntasi o'rtada yo'qoladi.
 * Ularni tepaga chiqarish — operatorning butun ishini shu o'nta qatorga
 * jamlashning yagona yo'li.
 *
 * Guruh ichida FAYLDAGI tartib saqlanadi (`rowNumber`): operator ekranni
 * fayl bilan yonma-yon solishtiradi.
 */
export function sortImportRows(rows: readonly ImportPreviewRow[]): ImportPreviewRow[] {
  const weight = (row: ImportPreviewRow): number =>
    MATCH_LOOK[row.matchKind].decision === 'choose' ? 0 : 1

  return [...rows].sort(
    (left, right) => weight(left) - weight(right) || left.rowNumber - right.rowNumber,
  )
}

/* ══════════════════════════════════════════════════════════ JADVALLAR */

export const SCOPE_LOOK: Readonly<Record<ScheduleScope, ChipLook>> = {
  Employee: { tone: 'info', icon: UserRound, labelKey: 'attendance.scopeEmployee' },
  Position: {
    tone: 'purple',
    icon: BriefcaseBusiness,
    labelKey: 'attendance.scopePosition',
  },
  Department: { tone: 'brand', icon: Network, labelKey: 'attendance.scopeDepartment' },
  Default: { tone: 'neutral', icon: Globe, labelKey: 'attendance.scopeDefault' },
}

/**
 * Ustunlik tartibi — kuchliroqdan kuchsizga (SPEC §5.2).
 *
 * Biriktirmalar ro'yxati SHU tartibda guruhlanadi: ekran serverdagi
 * qoidani takrorlab ko'rsatadi, ya'ni «nega bu odamda shu jadval?»
 * degan savolga qarashning o'ziyoq javob beradi.
 */
export const SCOPE_ORDER: readonly ScheduleScope[] = [
  'Employee',
  'Position',
  'Department',
  'Default',
]

export type WeekdayIndex = 1 | 2 | 3 | 4 | 5 | 6 | 7

/** ISO tartibi: dushanbadan yakshanbagacha — jadval muharriri shu tartibda chiziladi. */
export const WEEKDAY_INDEXES: readonly WeekdayIndex[] = [1, 2, 3, 4, 5, 6, 7]

/**
 * Hafta kunlarining tarjima kalitlari.
 *
 * ⚠️ Kalitlar SPEC §8.8 ro'yxatida YO'Q, lekin jadval muharriri ettita
 * qatorni nomlashi shart. Ular W (tarjimalar) bo'linmasiga topshirilgan;
 * `./bonvi check` ularsiz yiqiladi, ya'ni unutib bo'lmaydi.
 *
 * NEGA `Intl.DateTimeFormat` bilan hosil qilinmaydi: brauzerlar
 * `uz-Latn-UZ` va `uz-Cyrl-UZ` teglarini to'liq qo'llab-quvvatlamaydi
 * (`format.ts` dagi `formatNumber` izohidagi bilan aynan bir sabab) va
 * ba'zi brauzerda hafta kuni inglizcha chiqib qolardi.
 */
export const WEEKDAY_LABEL_KEY: Readonly<Record<WeekdayIndex, string>> = {
  1: 'attendance.weekdayMon',
  2: 'attendance.weekdayTue',
  3: 'attendance.weekdayWed',
  4: 'attendance.weekdayThu',
  5: 'attendance.weekdayFri',
  6: 'attendance.weekdaySat',
  7: 'attendance.weekdaySun',
}

/**
 * Hafta kunining QISQA yorlig'i — xodim kartasidagi kalendar sarlavhasi.
 *
 * NEGA alohida kalitlar, to'liq nomning birinchi ikki harfi emas: ruscha
 * «Понедельник» dan «По» chiqardi, to'g'risi esa «Пн». Har tilda qisqartma
 * o'z qoidasiga ega va uni koddan yasab bo'lmaydi.
 */
export const WEEKDAY_SHORT_KEY: Readonly<Record<WeekdayIndex, string>> = {
  1: 'attendance.weekdayShortMon',
  2: 'attendance.weekdayShortTue',
  3: 'attendance.weekdayShortWed',
  4: 'attendance.weekdayShortThu',
  5: 'attendance.weekdayShortFri',
  6: 'attendance.weekdayShortSat',
  7: 'attendance.weekdayShortSun',
}

/** Shanba va yakshanba — tabelda ustun sarlavhasi kulrang fonda (SPEC §8.6). */
export function isWeekend(dayIndex: number): boolean {
  return dayIndex === 6 || dayIndex === 7
}

/**
 * `DateOnly` satridan ISO hafta kuni: «2026-09-17» → 4 (payshanba).
 *
 * ⚠️ Sana UTC yarim tuniga bog'lanadi va `getUTCDay()` o'qiladi.
 * `new Date('2026-09-17').getDay()` BRAUZER mintaqasini oladi va
 * UTC dan ORQADA turgan mashinada (masalan Amerikada ochilgan
 * brauzerda) butun tabel ustunlarini bir kunga surib yuborardi —
 * dam olish kunlari chorshanbaga tushardi. Toshkent UTC dan doim
 * OLDINDA, ya'ni UTC yarim tuni bilan hisoblash shu kalendarda
 * hech qachon adashmaydi (`format.ts` dagi `formatDate` izohidagi
 * bilan bir xil mulohaza).
 *
 * `null` — sana yaroqsiz; chaqiruvchi ustunni oddiy ish kuni sifatida
 * chizadi, ya'ni buzilgan qiymat ekranni yiqitmaydi.
 */
export function isoWeekday(date: string): WeekdayIndex | null {
  const parsed = new Date(`${date}T00:00:00Z`)
  if (Number.isNaN(parsed.getTime())) return null

  // `getUTCDay()`: 0 — yakshanba. ISO da esa yakshanba — 7.
  const day = parsed.getUTCDay()
  return (day === 0 ? 7 : day) as WeekdayIndex
}

/**
 * Smena yarim tundan o'tadimi.
 *
 * ⭐ Bu SAQLANADIGAN bayroq EMAS — na bazada, na bu yerda (SPEC §2.4.1).
 * Domen uni aynan shu formula bilan hisoblaydi va sabab ham o'sha:
 * saqlangan bayroq o'zi tasvirlagan ikki vaqtga zid bo'lib qolishi
 * mumkin. Kimdir `endTime` ni 18:00 dan 02:00 ga o'zgartirib, bayroqqa
 * tegmasa — tabel manfiy uzunlikni ma'lumotdek ko'rsatardi.
 *
 * Muharrirda bu «→ ertasi kuni» belgisini chiqaradi: operator oqibatni
 * saqlashdan OLDIN ko'radi (SPEC §8.4).
 */
export function crossesMidnight(day: WorkScheduleDayInput): boolean {
  if (!day.isWorking || day.startTime === null || day.endTime === null) return false
  return day.endTime <= day.startTime
}

export interface WeekSummary {
  workingDays: number
  /** Smenalarning umumiy uzunligi, tanaffus AYIRILMAGAN holda. */
  shiftMinutes: number
  breakMinutes: number
  /** `shiftMinutes − breakMinutes` — serverdagi `planned_minutes` formulasi. */
  plannedMinutes: number
}

/**
 * Jadval muharriridagi jonli xulosa: «Haftada 5 kun · 40 soat».
 *
 * ⚠️ Bu YAGONA joyda turadi va serverdagi `planned_minutes` formulasini
 * (SPEC §2.6, §5.5 6-qadam) takrorlaydi. Formula o'zgarsa — tuzatiladigan
 * joy shu funksiya. Hisobning boshqa hech bir nusxasi klientda YO'Q va
 * bo'lmasligi ham kerak: tabeldagi raqamlar server hisobidan keladi.
 *
 * Tanaffus bu yerda REJADAN ayiriladi — 2026-09-17 tuzatishidan keyin
 * ham (§5.5 6-qadam «unchanged»): reja va fakt tanaffusni bir xil
 * shartda chiqarib tashlaydi, shuning uchun ular solishtiriladi.
 * Faktdan ayirish (oyna ichida haqiqatan tashqarida bo'lmagan qism,
 * `WorkScheduleDayInput.breakStart` izohi) butunlay serverda va bu
 * xulosaga KIRMAYDI.
 */
export function weekSummary(days: readonly WorkScheduleDayInput[]): WeekSummary {
  let workingDays = 0
  let shiftMinutes = 0
  let breakMinutes = 0

  for (const day of days) {
    if (!day.isWorking) continue

    workingDays += 1
    shiftMinutes += shiftSpanMinutes(day)
    breakMinutes += day.breakMinutes
  }

  return {
    workingDays,
    shiftMinutes,
    breakMinutes,
    plannedMinutes: Math.max(0, shiftMinutes - breakMinutes),
  }
}

/**
 * Smena uzunligi, daqiqada.
 *
 * Yarim tundan o'tadigan smena uchun sutka qo'shiladi (20:00 → 08:00 =
 * 720 daqiqa): aks holda ayirma manfiy chiqardi va tungi smena haftalik
 * xulosani butunlay buzardi.
 */
export function shiftSpanMinutes(day: WorkScheduleDayInput): number {
  const start = minutesOfDay(day.startTime)
  const end = minutesOfDay(day.endTime)
  if (!day.isWorking || start === null || end === null) return 0

  return end > start ? end - start : end + 24 * 60 - start
}

/** «13:00–14:00» yoki `null` — tanaffus oynasi belgilanmagan. */
export function breakWindowLabel(day: WorkScheduleDayInput): string | null {
  if (day.breakStart === null || day.breakEnd === null) return null
  return `${day.breakStart}–${day.breakEnd}`
}

/**
 * Tanaffus oynasi — tungi smenada — yarim tundan o'tadimi
 * (ciferblat bo'yicha `breakEnd < breakStart`).
 *
 * Saqlangan kun uchun server bu bayroqni o'zi beradi
 * (`WorkScheduleDay.breakCrossesMidnight`, SPEC §6.4). Bu funksiya
 * SAQLANMAGAN forma uchun: muharrir «→ ertasi kuni» belgisini operator
 * yozayotgan paytda ko'rsatishi kerak, saqlashdan keyin emas. Formula
 * serverniki bilan bir xil va shu qadar oddiyki, ikki nusxa
 * ajralib keta olmaydi.
 *
 * ⚠️ Bu oynaning SMENA ICHIDA ekanini TEKSHIRMAYDI — u
 * `davomat.jadval_tanaffus_oynasi` (§2.4.0.1): haqiqat manbasi server,
 * muharrirdagi oldindan ogohlantirish esa `breakWindowPlacement`
 * (`attendance-schedule-rules.ts`).
 */
export function breakWindowCrossesMidnight(day: WorkScheduleDayInput): boolean {
  if (day.breakStart === null || day.breakEnd === null) return false
  return day.breakEnd < day.breakStart
}

/** «09:30» → 570. Yaroqsiz qiymat — `null`, chaqiruvchi uni 0 deb hisoblaydi. */
function minutesOfDay(value: string | null): number | null {
  if (value === null) return null

  const match = /^(\d{1,2}):(\d{2})/u.exec(value)
  if (match === null) return null

  const hours = Number(match[1])
  const minutes = Number(match[2])
  if (!Number.isFinite(hours) || !Number.isFinite(minutes)) return null

  return hours * 60 + minutes
}

/* ════════════════════════════════════════════════════════ VAQT VA DAVOM */

/**
 * O'tish vaqti: «08:03» — TOSHKENT mintaqasida.
 *
 * ⚠️ Mintaqa hisobi bu yerda QAYTA yozilmaydi: bo'laklar
 * `tashkentParts` dan olinadi — bu `format.ts` dagi `formatDate` va
 * `formatDateTime` ishlatadigan AYNI formatlagich. Tizimda vaqt
 * o'girish nuqtasi bitta bo'lishi kerak: server UTC yozadi, ekran +5 da
 * ko'rsatadi, uchinchi joy YO'Q (`BonviTime` va `timezone.ts`
 * izohlaridagi qoida — SPEC §3.8 ham shuni takrorlaydi).
 *
 * NEGA `formatDateTime` ning o'zi yaramaydi: jurnalda, statistikada va
 * tabel popover'ida SANA emas, faqat SOAT kerak. Sana ustunning o'zida
 * yoki filtr oralig'ida allaqachon turibdi va har qatorda takrorlanishi
 * shovqindan boshqa narsa emas.
 */
export function formatPassTime(value: string | Date | null): string {
  if (value === null) return '—'

  const date = typeof value === 'string' ? new Date(value) : value
  if (Number.isNaN(date.getTime())) return '—'

  const { hour, minute } = tashkentParts(date)
  return `${hour}:${minute}`
}

/**
 * Tug'ilgan kun sanasi: «2026-09-20» → «20.09».
 *
 * Yil ATAYLAB yo'q: panelda sana yaqin kelajakni bildiradi va yil har
 * doim joriy (yoki keyingi) — uni chizish ustunda joy egallab, hech
 * narsa aytmasdi. Shakl tizimning boshqa joylaridagi `dd.MM.yyyy` ning
 * kesilgani, ya'ni yangi format kiritilmaydi (`format.ts` izohi:
 * sana barcha tillarda bir xil ko'rinishda).
 *
 * ⚠️ Qiymat — server `DateOnly` si («yyyy-MM-dd»), lahza EMAS: bu yerda
 * mintaqa o'girish YO'Q va bo'lmasligi ham kerak. `new Date(...)` orqali
 * o'tkazish uni UTC yarim tuniga bog'lab, brauzer mintaqasida bir kunga
 * siljitib yuborishi mumkin edi.
 */
export function formatBirthday(value: string): string {
  const [, month, day] = value.split('-')

  return month === undefined || day === undefined ? value : `${day}.${month}`
}

/**
 * Davomiylik: 479 → «7:59».
 *
 * NEGA soat:daqiqa, o'nlik kasr emas: HR «7 soat 59 daqiqa» deb
 * o'ylaydi va shu ko'rinishda tekshiradi. Excel eksportida ham aynan shu
 * shakl bor (SPEC §6.7 — u yerda daqiqa ustuni bilan YONMA-YON, chunki
 * matn ustunini qo'shib bo'lmaydi).
 */
export function formatDuration(minutes: number): string {
  if (!Number.isFinite(minutes) || minutes <= 0) return '0:00'

  const whole = Math.trunc(minutes)
  return `${Math.trunc(whole / 60)}:${String(whole % 60).padStart(2, '0')}`
}

/**
 * Tabel katagidagi yagona raqam: 479 → «7,9» (inglizchada «7.9»).
 *
 * ⭐ Katakda BITTA raqam turadi va u — soat, bitta kasr xonasi bilan
 * (SPEC §8.6). Qolgan hamma narsa (aniq daqiqalar, kirish, chiqish,
 * kechikish) popover'da: 540 × 31 katakning har biriga to'rtta raqam
 * sig'maydi va bu iOS ning bosqichma-bosqich ochish qoidasi.
 *
 * Kasr ajratgichi tilga qarab (`formatNumber` dagi bilan bir xil
 * qoida): o'zbek va rus tilida vergul, inglizchada nuqta.
 */
export function formatHours(minutes: number, locale: LocaleCode): string {
  if (!Number.isFinite(minutes) || minutes <= 0) return '0'

  const hours = (minutes / 60).toFixed(1)
  return locale === 'en' ? hours : hours.replace('.', ',')
}

/**
 * Oy yorlig'i: «09.2026», inglizchada «2026-09».
 *
 * NEGA oy NOMI emas: o'n ikki nomni to'rt tilga tarjima qilish qirq
 * sakkizta yangi kalit degani, `Intl` esa `uz-Latn`/`uz-Cyrl` ni to'liq
 * bilmaydi (`reports-presentation.ts` dagi `monthTick` bilan bir xil
 * mulohaza va bir xil tartib qoidasi).
 *
 * Tabel sahifasida ham, xodim kartasidagi kalendarda ham SHU funksiya —
 * ikki ekranda oy ikki xil yozilmasin.
 */
export function monthLabel(value: { year: number; month: number }, locale: LocaleCode): string {
  const month = String(value.month).padStart(2, '0')
  return locale === 'en' ? `${value.year}-${month}` : `${month}.${value.year}`
}
