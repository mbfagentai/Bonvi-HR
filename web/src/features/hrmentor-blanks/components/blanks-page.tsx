import { Link } from '@tanstack/react-router'
import { Download, FileStack, LoaderCircle, PlugZap, RefreshCw, Search, SearchX } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { fileSizeParts } from '@/features/employees/lib/document-file'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { matchesSearch } from '@/shared/lib/search-fold'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  Skeleton,
  buttonVariants,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import { EMPLOYEES_VIEW, SETTINGS_MANAGE, type Blank } from '../api/blanks-api'
import { useBlanks, useDownloadBlank, useSyncBlanks } from '../api/blanks-queries'

const helper = dataColumnHelper<Blank>()

const EMPTY: Blank[] = []

/**
 * Blanka fayllari — HR bo'limining amaldagi hujjat shakllari (SPEC-blankalar-hrmentor §6).
 *
 * Blankalar HRMentor da yuritiladi; Bonvi HR ularni har soatda o'zi oladi
 * va o'z omborida saqlaydi, shu sababli HRMentor o'chiq bo'lsa ham
 * yuklab olinadi. Sahifa faqat o'qish uchun: tahrirlash HRMentor da.
 *
 * «Hozir yangilash» — faqat `settings.manage` da (sinxronlash HRMentor ga
 * so'rov yuboradi va katalogni yozadi); qolganlar uchun ro'yxatning o'zi.
 */
export function HrMentorBlanksPage() {
  const { t, num, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const canView = hasPermission(EMPLOYEES_VIEW)
  const canSync = hasPermission(SETTINGS_MANAGE)

  const blanks = useBlanks(canView)
  const sync = useSyncBlanks()
  const download = useDownloadBlank()

  const [query, setQuery] = useState('')

  const columns = useMemo<Array<DataColumn<Blank>>>(
    () => [
      helper.accessor('code', {
        header: t('hrMentorBlanks.columnCode'),
        meta: { width: '120px' },
        cell: ({ getValue }) => <span className="tabular font-medium">{getValue()}</span>,
      }),
      helper.accessor('title', {
        header: t('hrMentorBlanks.columnTitle'),
        meta: { minWidth: '260px' },
      }),
      helper.accessor('versionNo', {
        header: t('hrMentorBlanks.columnVersion'),
        meta: { width: '110px' },
        cell: ({ getValue }) => (
          <Badge tone="neutral" size="sm">
            {t('hrMentorBlanks.version', { number: num(getValue()) })}
          </Badge>
        ),
      }),
      helper.accessor('publishedAt', {
        header: t('hrMentorBlanks.columnUpdated'),
        meta: { width: '170px', hideOnMobile: true },
        cell: ({ getValue }) => {
          const value = getValue()
          return value === null ? null : <span className="tabular">{dateTime(value)}</span>
        },
      }),
      helper.accessor('sizeBytes', {
        header: t('hrMentorBlanks.columnSize'),
        meta: { width: '100px', align: 'end', hideOnMobile: true },
        cell: ({ getValue }) => {
          const { value, unit } = fileSizeParts(getValue())
          return <span className="tabular text-[var(--fg-secondary)]">{num(value)} {unit}</span>
        },
      }),
      helper.display({
        id: 'download',
        header: '',
        meta: { align: 'end', width: '150px' },
        cell: ({ row }) => {
          const busy = download.isPending && download.variables?.id === row.original.id
          return (
            <Button
              size="sm"
              variant="soft"
              onClick={() => download.mutate(row.original)}
              disabled={busy}
              aria-label={t('hrMentorBlanks.downloadOne', { code: row.original.code })}
            >
              {busy ? <LoaderCircle className="animate-spin" /> : <Download />}
              {t('hrMentorBlanks.download')}
            </Button>
          )
        },
      }),
    ],
    [t, num, dateTime, download],
  )

  const state = blanks.data
  const rows = useMemo(() => {
    const source = state?.items ?? EMPTY
    const needle = query.trim()
    if (needle === '') return source

    return source.filter((item) => matchesSearch(`${item.code} ${item.title}`, needle))
  }, [state, query])

  const searching = query.trim() !== ''

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={FileStack} tone="teal" size="lg" />
            {t('hrMentorBlanks.title')}
          </span>
        }
        description={t('hrMentorBlanks.subtitle')}
        actions={
          canSync && state?.connected === true ? (
            <Button variant="secondary" onClick={() => sync.mutate()} disabled={sync.isPending}>
              {sync.isPending ? <LoaderCircle className="animate-spin" /> : <RefreshCw />}
              {sync.isPending ? t('hrMentorBlanks.syncing') : t('hrMentorBlanks.syncNow')}
            </Button>
          ) : undefined
        }
      />

      {!canView ? (
        <Card>
          <EmptyState icon={FileStack} title={t('hrMentorBlanks.noAccessTitle')} description={t('hrMentorBlanks.noAccessText')} />
        </Card>
      ) : blanks.isPending ? (
        <Skeleton className="h-96 w-full" />
      ) : blanks.isError || state === undefined ? (
        <Alert tone="danger" title={t('hrMentorBlanks.loadFailed')}>
          {errorMessage(problemCode(blanks.error))}
        </Alert>
      ) : (
        <>
          {sync.isError && <Alert tone="danger">{errorMessage(problemCode(sync.error))}</Alert>}
          {download.isError && <Alert tone="danger">{errorMessage(problemCode(download.error))}</Alert>}

          {!state.connected && (
            /*
              Ulanmagan — ro'yxat bo'lsa ham ko'rsatiladi (oldin olingan
              fayllar o'z omborimizda), lekin yangilanmaydi: kartochka buni
              va qayerdan ulanishini aytadi.
            */
            <Card>
              <EmptyState
                icon={PlugZap}
                title={t('hrMentorBlanks.notConnectedTitle')}
                description={t('hrMentorBlanks.notConnectedText')}
                action={
                  canSync ? (
                    <Link to="/sozlamalar" className={buttonVariants({ variant: 'secondary' })}>
                      {t('hrMentorBlanks.openSettings')}
                    </Link>
                  ) : undefined
                }
              />
            </Card>
          )}

          {state.connected && <SyncStatus lastSuccessAt={state.lastSuccessAt} lastError={state.lastError} />}

          {state.connected && state.source === 'legacy' && (
            <Alert tone="info" title={t('hrMentorBlanks.legacySourceTitle')}>
              {t('hrMentorBlanks.legacySourceText')}
            </Alert>
          )}

          {(state.connected || state.items.length > 0) && (
            <Card className="overflow-hidden">
              <div className="flex flex-wrap items-center gap-3 border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
                <div className="min-w-[200px] flex-1">
                  <Input
                    icon={Search}
                    shape="pill"
                    type="search"
                    value={query}
                    onChange={(event) => setQuery(event.target.value)}
                    placeholder={t('hrMentorBlanks.searchPlaceholder')}
                    aria-label={t('common.search')}
                  />
                </div>
              </div>

              <DataTable
                columns={columns}
                data={rows}
                getRowId={(item) => item.id}
                rowLabel={(item) => `${item.code} ${item.title}`}
                empty={
                  searching ? (
                    <EmptyState
                      icon={SearchX}
                      title={t('hrMentorBlanks.notFoundTitle')}
                      description={t('hrMentorBlanks.notFoundText', { query: query.trim() })}
                    />
                  ) : (
                    <EmptyState icon={FileStack} title={t('hrMentorBlanks.emptyTitle')} description={t('hrMentorBlanks.emptyText')} />
                  )
                }
              />
            </Card>
          )}
        </>
      )}
    </div>
  )
}

/**
 * «HRMentor dan oxirgi yangilanish: 31.10.2026 10:41» va oxirgi urinish
 * muvaffaqiyatsiz bo'lsa — sababi. Xato bo'lsa ham ro'yxat o'sha:
 * oxirgi muvaffaqiyatli yangilanishdagi fayllar.
 */
function SyncStatus({ lastSuccessAt, lastError }: { lastSuccessAt: string | null; lastError: string | null }) {
  const { t, dateTime } = useLocale()
  const errorMessage = useErrorMessage()

  return (
    <div className="space-y-3">
      <p className="text-footnote text-[var(--fg-secondary)]">
        {lastSuccessAt === null
          ? t('hrMentorBlanks.neverSynced')
          : t('hrMentorBlanks.lastSync', { time: dateTime(lastSuccessAt) })}
      </p>

      {lastError !== null && (
        <Alert tone="warning" title={t('hrMentorBlanks.lastErrorTitle')}>
          {errorMessage(lastError)}
        </Alert>
      )}
    </div>
  )
}
