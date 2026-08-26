/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employments-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Maydon nomlari taxmin emas — haqiqiy manbadan olingan:
 *   api/src/Bonvi.Api/Endpoints/ReportsEndpoints.cs (GET /api/reports/turnover)
 *   api/src/Bonvi.Application/Reports/Dtos/TurnoverDto.cs
 *   api/src/Bonvi.Application/Reports/Queries/GetTurnoverQuery.cs
 *
 * NEGA bu yerda `unknown` dan o'qish qatlami BOR, holbuki `dashboard-api.ts`
 * da u ATAYLAB yo'q: bosh sahifa javobi tekis sonlar to'plami — bitta maydon
 * yetishmasa ekranda `undefined` chiqadi, xolos. Bu yerda esa javob ikkita
 * MASSIVdan iborat va ular to'g'ridan-to'g'ri grafikka va bo'linmaga
 * uzatiladi: `months` massiv bo'lmasa `.map` butun sahifani yiqitadi, sonlar
 * o'rniga `null` kelsa esa recharts NaN koordinata chizib, konsolni xatoga
 * to'ldiradi. Ya'ni bu yerda himoya qatlami «ehtiyot chorasi» emas —
 * u oq ekranning oldini oladi.
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `reports-queries.ts` generatsiya qilingan hook'ga o'tkaziladi.
 */
import { api } from '@/shared/api/http-client'

/**
 * Server qabul qiladigan eng katta oy soni
 * (`GetTurnoverQueryHandler.MaxMonths`). Kattaroq qiymat `hisobot.oy_notogri`
 * kodi bilan 400 qaytaradi.
 *
 * Bu yerda TURIBDI, davr almashtirgichi yonida emas: bu UI tanlovi emas,
 * HTTP shartnomasining bir qismi — manzildan kelgan qiymatni tekshiradigan
 * joy ham (`reports-search.ts`) shu chegaraga tayanadi.
 */
export const MAX_TURNOVER_MONTHS = 24

/** Kadrlar oqimining bitta oylik nuqtasi. */
export interface TurnoverMonth {
  /** `DateOnly` → «2026-08-01». */
  monthStart: string
  /** Oy oxiri; joriy (tugamagan) oy uchun — so'ralgan davrning oxiri. */
  monthEnd: string
  hired: number
  left: number
  /** Oy boshidagi shtat soni — ODAM bo'yicha, shartnoma bo'yicha emas. */
  headcountStart: number
  headcountEnd: number
}

/** Ketish sababi bo'yicha bitta bo'lak. */
export interface TurnoverReason {
  /**
   * `null` — sabab ko'rsatilmagan. Bunday qatorning NOMI ham serverdan
   * keladi va klientda yasalmaydi: aks holda bir xil tushuncha ikki joyda —
   * C# da va bu yerda — nomlanardi va ular vaqt o'tib ajralib ketardi.
   */
  reasonId: string | null
  reasonName: string
  /** `null` — noma'lum (ma'lumotnomada belgilanmagan). */
  isVoluntary: boolean | null
  count: number
}

export interface Turnover {
  /** Davr boshi — har doim oyning 1-sanasi. */
  from: string
  /** Davr oxiri (odatda bugun). */
  to: string
  /** Xronologik tartibda — SERVER tartibi, klientda qayta saralanmaydi. */
  months: TurnoverMonth[]
  /** Ko'pdan kamga — bu ham server tartibi (shartnomaning bir qismi). */
  reasons: TurnoverReason[]
}

export async function fetchTurnover(
  months: number,
  signal?: AbortSignal,
): Promise<Turnover> {
  const { data } = await api.get<unknown>('/api/reports/turnover', {
    params: { months },
    signal,
  })

  return toTurnover(data)
}

/* ─────────────────────────────────────────────────── xaritalash (map) */

function toTurnover(raw: unknown): Turnover {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    from: text(box['from']) ?? '',
    to: text(box['to']) ?? '',
    months: list(box['months'], toMonth),
    reasons: list(box['reasons'], toReason),
  }
}

function toMonth(raw: unknown): TurnoverMonth {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    monthStart: text(box['monthStart']) ?? '',
    monthEnd: text(box['monthEnd']) ?? '',
    hired: count(box['hired']),
    left: count(box['left']),
    headcountStart: count(box['headcountStart']),
    headcountEnd: count(box['headcountEnd']),
  }
}

function toReason(raw: unknown): TurnoverReason {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    reasonId: text(box['reasonId']),
    // Nom bo'sh kelib qolsa qator NOMSIZ chiziladi — «—» ham bo'lmagandan
    // ko'ra ma'lumotli: qator borligi ko'rinadi, soni esa o'qiladi.
    reasonName: text(box['reasonName']) ?? '—',
    isVoluntary: flag(box['isVoluntary']),
    count: count(box['count']),
  }
}

function list<T>(value: unknown, map: (raw: unknown) => T): T[] {
  return Array.isArray(value) ? value.map(map) : []
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * Sanoq maydoni.
 *
 * `null`, `NaN` va manfiy qiymat — hammasi nolga tushadi. NEGA nol, `null`
 * emas: bu sonlar grafik koordinatasiga aylanadi va `null` u yerda uzilgan
 * ustun yoki chiziq degani. Server bunday qiymat qaytarmaydi (`int`), lekin
 * agar qaytarsa — bo'sh ustun to'g'riroq javob bo'ladi.
 */
function count(value: unknown): number {
  if (typeof value !== 'number' || !Number.isFinite(value) || value < 0) return 0
  return value
}

/** Uch holatli bayroq: `true` / `false` / `null` (noma'lum). */
function flag(value: unknown): boolean | null {
  return typeof value === 'boolean' ? value : null
}
