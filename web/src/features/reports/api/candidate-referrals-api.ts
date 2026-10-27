/**
 * «Nomzodlar qayerdan bilgan» — how many candidates each referral channel
 * brought and how many of them were hired (docs/SPEC-qayerdan-bilgan.md §7).
 *
 * The call and the shapes are the generated contract (`./bonvi gen`); this
 * file only narrows the counts (`number | string` in the schema — .NET may
 * read numbers from strings, it always writes numbers) and keeps the reading
 * defensive like `turnover-api.ts`: the rows feed widths and percentages, so a
 * bad count must become 0, not a NaN bar.
 */
import { getCandidateReferralsReport } from '@/shared/api/generated/reports/reports'
import type {
  CandidateReferralReportDto,
  CandidateReferralRowDto,
  GetCandidateReferralsReportParams,
} from '@/shared/api/generated/model'

/** Query of the report and of its Excel export — the same three parameters. */
export interface CandidateReferralsParams {
  /** Application date (`created_at`, Tashkent day), `yyyy-MM-dd`; `''` — no bound. */
  from: string
  to: string
  /** `''` — every vacancy. */
  vacancyId: string
}

type Counted<T, K extends keyof T> = Omit<T, K> & { [P in K]: number }

/**
 * One channel row. `channelId: null` — the «Ko'rsatilmagan» row (legacy and
 * Excel candidates without a channel; `channelName` is `null` there and the
 * screen names it). An inactive channel still shows when it has candidates.
 */
export type CandidateReferralRow = Counted<CandidateReferralRowDto, 'candidates' | 'hired'>

/** Most candidates first — the server's order, not re-sorted here. */
export type CandidateReferrals = Omit<
  Counted<CandidateReferralReportDto, 'totalCandidates' | 'totalHired'>,
  'rows'
> & { rows: CandidateReferralRow[] }

export const CANDIDATE_REFERRALS_EXPORT_PATH = '/api/reports/candidate-referrals/export'

/** Empty values are not sent at all — the list-export pattern (`candidateListParams`). */
export function candidateReferralsQuery(params: CandidateReferralsParams): GetCandidateReferralsReportParams {
  const query: GetCandidateReferralsReportParams = {}
  if (params.from !== '') query.from = params.from
  if (params.to !== '') query.to = params.to
  if (params.vacancyId !== '') query.vacancyId = params.vacancyId
  return query
}

export async function fetchCandidateReferrals(
  params: CandidateReferralsParams,
  signal?: AbortSignal,
): Promise<CandidateReferrals> {
  const data: unknown = await getCandidateReferralsReport(candidateReferralsQuery(params), signal)
  return toReport(data)
}

/* ─────────────────────────────────────────────────────────── mapping */

function toReport(raw: unknown): CandidateReferrals {
  const box = (raw ?? {}) as Record<string, unknown>
  const rows = Array.isArray(box['rows']) ? box['rows'].map(toRow) : []

  return {
    rows,
    // Totals from the server; the rows' own sums only when they are missing
    totalCandidates: count(box['totalCandidates'], () => rows.reduce((sum, row) => sum + row.candidates, 0)),
    totalHired: count(box['totalHired'], () => rows.reduce((sum, row) => sum + row.hired, 0)),
  }
}

function toRow(raw: unknown): CandidateReferralRow {
  const box = (raw ?? {}) as Record<string, unknown>
  const channelId = text(box['channelId'])

  return {
    channelId,
    channelName: text(box['channelName']),
    // The «Ko'rsatilmagan» row is not a channel (the server sends `false`) — never mark it inactive
    isActive: channelId === null || box['isActive'] !== false,
    candidates: count(box['candidates']),
    hired: count(box['hired']),
  }
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/** A count (number, or a numeric string the schema allows); bad values become 0 or the fallback. */
function count(value: unknown, fallback?: () => number): number {
  const number = typeof value === 'string' && value.trim() !== '' ? Number(value) : value
  if (typeof number === 'number' && Number.isFinite(number) && number >= 0) return number
  return fallback === undefined ? 0 : fallback()
}
