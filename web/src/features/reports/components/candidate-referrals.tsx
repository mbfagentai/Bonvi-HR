import { Megaphone } from 'lucide-react'

import { useAuth } from '@/features/auth'
import { REPORTS_EXPORT } from '@/features/employees/lib/employee-presentation'
import { useVacancyOptions } from '@/features/recruiting/api/recruiting-queries'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { isDateRangeInverted } from '@/shared/lib/list-search'
import {
  Alert,
  Badge,
  Card,
  CardContent,
  CardHeader,
  DateRangeField,
  EmptyState,
  Select,
  Skeleton,
} from '@/shared/ui'

import {
  CANDIDATE_REFERRALS_EXPORT_PATH,
  candidateReferralsQuery,
  type CandidateReferralRow,
  type CandidateReferrals as CandidateReferralsReport,
} from '../api/candidate-referrals-api'
import { useListExport } from '../api/list-export'
import { useCandidateReferrals } from '../api/reports-queries'
import { percentText } from '../lib/reports-presentation'
import type { ReferralFilters } from '../lib/reports-search'
import { share } from '../lib/turnover-math'
import { ListExportButton, ListExportError } from './list-export'

/**
 * «Nomzodlar qayerdan bilgan» — the Reports section of
 * docs/SPEC-qayerdan-bilgan.md §7: per referral channel, how many candidates
 * applied in the period and how many of them were hired.
 *
 * Built like «Ketish sabablari» (`turnover-reasons.tsx`): one row per channel
 * with a horizontal bar. The bar's length is the channel's share of all
 * candidates in the period; its green part is the hired share of that
 * channel, so the two numbers of the row are both visible in it.
 *
 * The page renders this card only for users who may see recruiting data
 * (`recruiting.view` on top of `reports.view`) — the numbers are candidates.
 */
export function CandidateReferrals({
  filters,
  onChange,
}: {
  filters: ReferralFilters
  onChange: (next: ReferralFilters) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canExport = hasPermission(REPORTS_EXPORT)

  // All vacancies, closed ones too: the period may reach back to them
  const vacancies = useVacancyOptions()

  // An inverted range is not sent — the server would answer 400; the card says why instead
  const rangeInvalid = isDateRangeInverted(filters)
  const query = useCandidateReferrals(filters, !rangeInvalid)
  const data = rangeInvalid ? undefined : query.data

  const exportList = useListExport()
  const exportReport = () =>
    exportList.mutate({
      path: CANDIDATE_REFERRALS_EXPORT_PATH,
      params: candidateReferralsQuery(filters),
      slug: 'nomzodlar_qayerdan_bilgan',
    })

  const filtered = filters.from !== '' || filters.to !== '' || filters.vacancyId !== ''

  return (
    <Card>
      <CardHeader
        title={t('reports.referrals.title')}
        subtitle={t('reports.referrals.hint')}
        action={
          canExport ? (
            <ListExportButton onClick={exportReport} pending={exportList.isPending} disabled={rangeInvalid} />
          ) : undefined
        }
      />

      <div className="grid gap-3 border-y border-[var(--border)] bg-[var(--bg-sunken)] px-5 py-4 sm:grid-cols-2">
        <DateRangeField
          label={t('dateRange.applied')}
          fromLabel={t('recruiting.appliedFrom')}
          toLabel={t('recruiting.appliedTo')}
          from={filters.from}
          to={filters.to}
          onFromChange={(from) => onChange({ ...filters, from })}
          onToChange={(to) => onChange({ ...filters, to })}
        />
        <Select
          size="sm"
          label={t('recruiting.vacancy')}
          placeholder={t('recruiting.anyVacancy')}
          options={vacancies.options}
          value={filters.vacancyId}
          disabled={vacancies.isPending}
          onChange={(event) => onChange({ ...filters, vacancyId: event.target.value })}
        />
      </div>

      <ListExportError error={exportList.error} />

      <CardContent className="pt-4">
        {rangeInvalid ? (
          <Alert tone="warning">{errorMessage('royxat.sana_oraligi_notogri')}</Alert>
        ) : query.isError && data === undefined ? (
          <Alert tone="danger" title={t('reports.loadFailed')}>
            {errorMessage(problemCode(query.error))}
          </Alert>
        ) : data === undefined ? (
          <div className="space-y-4" aria-busy>
            <Skeleton className="h-9" />
            <Skeleton className="h-9" />
            <Skeleton className="h-9" />
          </div>
        ) : data.totalCandidates === 0 || data.rows.length === 0 ? (
          <EmptyState
            icon={Megaphone}
            title={t('reports.referrals.emptyTitle')}
            description={t(filtered ? 'reports.referrals.emptyFilteredText' : 'reports.referrals.emptyText')}
          />
        ) : (
          // Old numbers stay while the next period loads — dimmed, so the change is visible
          <div className={cn('transition-opacity duration-200', query.isFetching && 'opacity-60')}>
            <ReferralTable report={data} />
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function ReferralTable({ report }: { report: CandidateReferralsReport }) {
  const { t, num } = useLocale()

  const rate = (hired: number, candidates: number) =>
    candidates === 0 ? '—' : percentText(share(hired, candidates), num)

  return (
    <>
      <table className="w-full border-collapse text-footnote">
        <thead>
          <tr className="text-caption1 text-[var(--fg-tertiary)]">
            <th scope="col" className="pb-2 text-left font-medium">
              {t('reports.referrals.columnChannel')}
            </th>
            <th scope="col" className="w-20 pb-2 pl-3 text-right font-medium sm:w-24">
              {t('reports.referrals.columnCandidates')}
            </th>
            <th scope="col" className="w-20 pb-2 pl-3 text-right font-medium sm:w-28">
              {t('reports.referrals.columnHired')}
            </th>
            <th
              scope="col"
              className="w-14 pb-2 pl-3 text-right font-medium"
              title={t('reports.referrals.columnRateHint')}
            >
              %
            </th>
          </tr>
        </thead>

        <tbody>
          {report.rows.map((row) => (
            <Row key={row.channelId ?? 'missing'} row={row} total={report.totalCandidates} rate={rate} />
          ))}
        </tbody>

        <tfoot>
          <tr className="border-t border-[var(--border)] font-semibold">
            <th scope="row" className="pt-3 text-left">
              {t('reports.referrals.total')}
            </th>
            <td className="tabular pt-3 pl-3 text-right">{num(report.totalCandidates)}</td>
            <td className="tabular pt-3 pl-3 text-right">{num(report.totalHired)}</td>
            <td className="tabular pt-3 pl-3 text-right text-caption1 text-[var(--fg-secondary)]">
              {rate(report.totalHired, report.totalCandidates)}
            </td>
          </tr>
        </tfoot>
      </table>

      <p className="mt-4 flex flex-wrap items-center gap-x-4 gap-y-1 text-caption1 text-[var(--fg-tertiary)]">
        <span className="flex items-center gap-1.5">
          <span aria-hidden className="size-2.5 rounded-full bg-[var(--accent)]" />
          {t('reports.referrals.legendCandidates')}
        </span>
        <span className="flex items-center gap-1.5">
          <span aria-hidden className="size-2.5 rounded-full bg-ios-green" />
          {t('reports.referrals.legendHired')}
        </span>
      </p>
    </>
  )
}

function Row({
  row,
  total,
  rate,
}: {
  row: CandidateReferralRow
  total: number
  rate: (hired: number, candidates: number) => string
}) {
  const { t, num } = useLocale()

  const name = row.channelId === null ? t('reports.referrals.missing') : (row.channelName ?? '—')
  const candidatesShare = share(row.candidates, total)
  // The hired part is drawn inside the channel's bar: hired of ALL candidates
  const hiredShare = share(row.hired, total)

  return (
    <tr className="align-top">
      <th scope="row" className="py-2 text-left font-normal">
        <span className="flex min-w-0 items-center gap-2">
          <span
            className={cn('min-w-0 truncate-1', row.channelId === null && 'text-[var(--fg-secondary)]')}
            title={name}
          >
            {name}
          </span>
          {!row.isActive && (
            <Badge tone="neutral" size="sm" className="shrink-0">
              {t('reports.referrals.inactive')}
            </Badge>
          )}
        </span>

        {/*
          SVG, not a styled div: the length is data, and a viewBox of 0…100
          takes the percentage as plain attributes (no inline style).
        */}
        <svg
          aria-hidden
          viewBox="0 0 100 1"
          preserveAspectRatio="none"
          className="mt-1.5 block h-1.5 w-full overflow-hidden rounded-full bg-[var(--bg-sunken)]"
        >
          <rect x={0} y={0} width={candidatesShare} height={1} className="fill-[var(--accent)]" />
          <rect x={0} y={0} width={hiredShare} height={1} className="fill-ios-green" />
        </svg>
      </th>
      <td
        className="tabular py-2 pl-3 text-right font-semibold"
        title={t('reports.referrals.shareHint', { percent: percentText(candidatesShare, num) })}
      >
        {num(row.candidates)}
      </td>
      <td className="tabular py-2 pl-3 text-right">{num(row.hired)}</td>
      <td className="tabular py-2 pl-3 text-right text-caption1 text-[var(--fg-tertiary)]">
        {rate(row.hired, row.candidates)}
      </td>
    </tr>
  )
}
