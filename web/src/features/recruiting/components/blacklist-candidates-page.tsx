import { useNavigate } from '@tanstack/react-router'
import { Search, SearchX } from 'lucide-react'
import { useMemo, type ReactNode } from 'react'

import { FLAG_LOOK } from '@/features/employees/lib/employee-presentation'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { useSearchDraft } from '@/shared/lib/list-search'
import { formatPhone } from '@/shared/lib/phone'
import { useClampPage, usePageSize } from '@/shared/lib/use-page-size'
import {
  Alert,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  Pagination,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import type { CandidateListItem } from '../api/recruiting-api'
import { useCandidates } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'

const helper = dataColumnHelper<CandidateListItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: CandidateListItem[] = []

/**
 * ⭐ «Qora ro'yxat» bo'limidagi NOMZODLAR (egasining qarori, 2026-10-07:
 * «qora ro'yxatga nomzodlar tabini qo'sh, xuddi rezerv bo'limi kabi»).
 *
 * `ReserveCandidatesPage` ning egizagi — tuzilishi, qidiruvi va sahifalashi
 * bir xil, farqi ustunlarda: bu yerda eng muhim ustun SABAB («nega bu odam
 * olinmaydi»), keyingi aloqa sanasi esa umuman yo'q — qora ro'yxatdagiga
 * qo'ng'iroq rejalashtirilmaydi.
 *
 * Qator bosilganda NOMZOD kartasi ochiladi; xato qo'yilgan bo'lsa o'sha
 * yerdan «Voronkaga qaytarish» (sabab majburiy).
 *
 * Qora ro'yxatdagi nomzod boshqa hech bir ro'yxatda ko'rinmaydi
 * (`CandidateSearchFilter.HideBlacklisted`), ya'ni bu uning YAGONA o'rni.
 */
export function BlacklistCandidatesPage({
  search,
  onSearchChange,
  page,
  onPageChange,
  tabs,
}: {
  /** Manzildagi qidiruv matni. Bo'sh satr — qidiruvsiz. */
  search: string
  onSearchChange: (next: string) => void
  /** 1 dan. */
  page: number
  onPageChange: (next: number) => void
  /** «Nomzodlar | Xodimlar» almashtirgichi — sarlavhada turadi. */
  tabs: ReactNode
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()
  const look = FLAG_LOOK.blacklist

  const [draft, setDraft] = useSearchDraft(search, onSearchChange)
  const { pageSize, setPageSize, allowAll } = usePageSize('blacklist-candidates', true)

  /** Hajm o'zgarsa birinchi sahifadan — eski raqam yangi hajmda boshqa odamlar. */
  const changePageSize = (next: number) => {
    setPageSize(next)
    onPageChange(1)
  }

  // `includeArchive` — rezerv sahifasidagi bilan bir xil sabab: arxiv
  // manbasidagi yozuv ham shu yerda ko'rinishi kerak.
  const query = useCandidates({
    status: 'Blacklisted',
    includeArchive: true,
    search: search === '' ? undefined : search,
    page,
    pageSize,
  })
  const rows = query.data?.items ?? EMPTY
  const total = query.data?.total ?? 0

  // Eski sahifa raqami yangi hajmda bo'sh bo'lsa — oxirgi sahifaga (`useClampPage`)
  useClampPage(page, pageSize, query.isPlaceholderData ? undefined : query.data?.total, onPageChange)

  const columns = useMemo<Array<DataColumn<CandidateListItem>>>(
    () => [
      helper.accessor('fullName', {
        header: t('employee.fullName'),
        enableSorting: false,
        meta: { minWidth: '240px' },
        cell: ({ getValue }) => <p className="truncate-1 font-medium">{getValue()}</p>,
      }),

      helper.accessor('closingNote', {
        header: t('recruiting.blacklistSection.reason'),
        enableSorting: false,
        meta: { minWidth: '260px' },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('vacancy', {
        header: t('recruiting.vacancy'),
        enableSorting: false,
        meta: { width: '200px', hideOnMobile: true },
        cell: ({ getValue }) => <TextCell value={getValue()} />,
      }),

      helper.accessor('phone', {
        header: t('employee.phone'),
        enableSorting: false,
        meta: { width: '170px', hideOnMobile: true },
        cell: ({ getValue }) => <TextCell value={formatPhone(getValue())} tabular />,
      }),

      helper.accessor('closedAt', {
        header: t('recruiting.closedAt'),
        enableSorting: false,
        meta: { width: '130px' },
        cell: ({ getValue }) => <DateCell value={getValue()} />,
      }),
    ],
    [t],
  )

  return (
    <div data-list-frame className="space-y-5 frame:flex frame:h-full frame:flex-col">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={look.icon} tone={look.iconTone} />
            {t('flags.page.blacklist.title')}
          </span>
        }
        subtitle={
          query.isSuccess
            ? t('flags.subtitle', { n: num(total) })
            : t('flags.page.blacklist.hint')
        }
        actions={tabs}
      />

      {query.isError && (
        <Alert tone="danger">{errorMessage(recruitingErrorCode(query.error))}</Alert>
      )}

      <Card className="overflow-hidden frame:flex frame:flex-1 frame:flex-col">
        <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
          <Input
            icon={Search}
            shape="pill"
            type="search"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder={t('common.searchPlaceholder')}
            aria-label={t('common.search')}
          />
        </div>

        <DataTable
          fill
          columns={columns}
          data={rows}
          getRowId={(row) => row.id}
          loading={query.isPending}
          rowLabel={(row) => row.fullName}
          onRowClick={(row) =>
            void navigate({
              to: '/nomzodlar/$candidateId',
              params: { candidateId: row.id },
            })
          }
          // Uch holat — rezerv sahifasidagi bilan bir xil
          // (`ReserveCandidatesPage` izohi).
          empty={
            query.isError ? null : search !== '' ? (
              <EmptyState
                icon={SearchX}
                title={t('recruiting.candidateNotFoundTitle')}
                description={t('recruiting.blacklistSection.notFoundText', { query: search.trim() })}
              />
            ) : (
              <EmptyState
                icon={look.icon}
                title={t('recruiting.blacklistSection.emptyTitle')}
                description={t('recruiting.blacklistSection.emptyText')}
              />
            )
          }
        />

        {/* `total > 0`: hajm tanlovi kichik ro'yxatda ham topilsin (`Pagination`) */}
        {total > 0 && (
          <Pagination
            page={page}
            pageSize={pageSize}
            total={total}
            onPageChange={onPageChange}
            onPageSizeChange={changePageSize}
            allowAll={allowAll}
            disabled={query.isFetching}
            className="border-t border-[var(--border)]"
          />
        )}
      </Card>
    </div>
  )
}

function TextCell({ value, tabular = false }: { value: string | null; tabular?: boolean }) {
  if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
  return (
    <span
      className={
        tabular
          ? 'tabular whitespace-nowrap text-[var(--fg-secondary)]'
          : 'truncate-1 text-[var(--fg-secondary)]'
      }
      title={value}
    >
      {value}
    </span>
  )
}

function DateCell({ value }: { value: string | null }) {
  const { date } = useLocale()

  if (value === null || value === '') return <span className="text-[var(--fg-tertiary)]">—</span>
  return <span className="tabular text-[var(--fg-secondary)]">{date(value)}</span>
}
