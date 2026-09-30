import { Link, getRouteApi } from '@tanstack/react-router'
import {
  CircleCheck,
  FileSpreadsheet,
  Link2,
  LoaderCircle,
  Lock,
  Plus,
  ScanFace,
  Search,
  Unlink,
  UserRoundSearch,
} from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { toSearchParam, useSearchDraft } from '@/shared/lib/list-search'
import {
  Alert,
  Badge,
  Button,
  Card,
  CardHeader,
  DataTable,
  Dialog,
  EmptyState,
  Input,
  PageHeader,
  Pagination,
  Skeleton,
  buttonVariants,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import type { AttendanceIdentity, UnmatchedGroup } from '../api/attendance-api'
import {
  useAttendanceIdentities,
  useDeleteIdentity,
  useUnmatchedPasses,
} from '../api/attendance-queries'
import {
  IDENTITY_SOURCE_LOOK,
  identityProviderLabel,
  isManagedPersonNo,
} from '../lib/attendance-manage-presentation'
import { ATTENDANCE_MANAGE, DEFAULT_PAGE_SIZE } from '../lib/attendance-presentation'
import { AttendanceIdentityDialog, AttendancePassFace } from './attendance-identity-dialog'
import { AttendanceImportDialog } from './attendance-import-dialog'
import {
  AttendanceLoadError,
  AttendanceNoAccess,
  AttendancePageTitle,
} from './attendance-manage-states'

const helper = dataColumnHelper<AttendanceIdentity>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: AttendanceIdentity[] = []

/**
 * Moslanmagan guruhlar sahifasi — 12 ta: ikki va uch ustunli to'rda
 * teng qatorlar chiqadi, bitta ekranga ham sig'adi.
 */
const UNMATCHED_PAGE_SIZE = 12

/** Aylanma bog'liqlikdan qochish uchun (`employees-page.tsx` dagi izoh). */
const route = getRouteApi('/_app/verifikatsiya/moslashtirish')

/**
 * Moslashtirish — modulning «o't oldirish kaliti».
 *
 * ⭐ Turniketlarda Verifix bergan shaxs raqamlari allaqachon turibdi; ular
 * xodimga bog'lanmaguncha HAR BIR o'tish «moslanmagan» bo'lib yotadi va
 * tabelga tushmaydi. Shu sababli sahifaning BIRINCHI bloki — navbat:
 * nechta raqam kutmoqda, har biri qachondan beri va qaysi yuz bilan. HR
 * yuzni taniydi, raqamni emas.
 *
 * Ikkinchi blok — mavjud mosliklar (qidiruv va uzish). Uchinchi yo'l —
 * Verifix ro'yxatini butunlay import qilish: yuzlab raqamni bittalab
 * bog'lash o'rniga.
 *
 * Ruxsat: HAMMASI `attendance.manage` (SPEC §7.3) — moslik ro'yxati
 * terminal raqamidan aniq odamga olib boruvchi xarita, oddiy ko'ruvchiga
 * kerak emas. Ruxsati yo'q foydalanuvchida so'rovlar UMUMAN ketmaydi.
 */
export function AttendanceMappingPage() {
  const { t, num, date, dateTime } = useLocale()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(ATTENDANCE_MANAGE)

  const searchParams = route.useSearch()
  const navigate = route.useNavigate()
  const search = searchParams.q ?? ''

  const [draft, setDraft] = useSearchDraft(search, (value) => {
    void navigate({ search: { q: toSearchParam(value) }, replace: true })
  })

  const [unmatchedPage, setUnmatchedPage] = useState(1)
  const [identityPage, setIdentityPage] = useState(1)

  const [linkOpen, setLinkOpen] = useState(false)
  /*
    Guruh oyna yopilganda TOZALANMAYDI: yopilish animatsiyasi paytida oyna
    ichidagi matn «qo'lda bog'lash» ga sakrab o'tib ketmasin. Keyingi
    ochilishda baribir yangi qiymat yoziladi.
  */
  const [linkGroup, setLinkGroup] = useState<UnmatchedGroup | null>(null)
  const [unlinking, setUnlinking] = useState<AttendanceIdentity | null>(null)
  const [importOpen, setImportOpen] = useState(false)

  // Qidiruv o'zgarsa — birinchi sahifadan: 7-sahifada qolib «hech narsa yo'q» ko'rinmasin
  useEffect(() => {
    setIdentityPage(1)
  }, [search])

  const unmatched = useUnmatchedPasses(
    { page: unmatchedPage, pageSize: UNMATCHED_PAGE_SIZE },
    canManage,
  )
  const identities = useAttendanceIdentities(
    {
      search: search === '' ? undefined : search,
      page: identityPage,
      pageSize: DEFAULT_PAGE_SIZE,
    },
    canManage,
  )

  /*
    Oxirgi sahifadagi yagona guruh bog'langach, sahifa bo'shab qoladi —
    oldingisiga qaytiladi. Busiz HR «hammasi bog'landi» degan bo'sh
    holatni ko'rardi, garchi oldingi sahifalarda yana 20 ta raqam kutsa.
  */
  const unmatchedItems = unmatched.data?.items
  useEffect(() => {
    if (unmatchedItems !== undefined && unmatchedItems.length === 0 && unmatchedPage > 1) {
      setUnmatchedPage((page) => Math.max(1, page - 1))
    }
  }, [unmatchedItems, unmatchedPage])

  const openLink = (group: UnmatchedGroup | null) => {
    setLinkGroup(group)
    setLinkOpen(true)
  }

  const columns = useMemo<Array<DataColumn<AttendanceIdentity>>>(
    () => [
      helper.accessor('employeeName', {
        header: t('attendance.employee'),
        // `incidents-page.tsx` dagi hisob: rasm (48) + oraliq (12) + matn — 320px
        meta: { width: '320px' },
        cell: ({ row }) => (
          <div className="flex min-w-0 items-center gap-3">
            <EmployeePhoto
              employeeId={row.original.employeeId}
              photoKey={row.original.photoKey}
              name={row.original.employeeName ?? ''}
              size="lg"
            />
            <div className="min-w-0">
              <p className="truncate-1 font-medium">{row.original.employeeName ?? '—'}</p>
              {/*
                Qurilmadagi F.I.Sh. — xodim ismi bilan YONMA-YON: ikkalasi
                farq qilsa, noto'g'ri bog'lanish bir qarashda ko'rinadi.
              */}
              {row.original.displayNameInDevice !== null && (
                <p className="truncate-1 text-caption1 text-[var(--fg-secondary)]">
                  {t('attendance.inDeviceAs', { name: row.original.displayNameInDevice })}
                </p>
              )}
            </div>
          </div>
        ),
      }),

      helper.accessor('externalPersonId', {
        header: t('attendance.personId'),
        // Verifix raqamlari 5–10 belgi, monoshrift: ~110px + belgi ichki bo'shlig'i + katak
        meta: { width: '180px' },
        cell: ({ row }) => (
          <div className="min-w-0 space-y-1">
            <Badge size="sm" className="font-mono">
              {row.original.externalPersonId}
            </Badge>
            <p className="text-caption2 text-[var(--fg-tertiary)]">
              {identityProviderLabel(row.original.provider, t)}
            </p>
          </div>
        ),
      }),

      helper.accessor('source', {
        header: t('attendance.source'),
        meta: { width: '130px', hideOnMobile: true },
        cell: ({ row }) => {
          const look = IDENTITY_SOURCE_LOOK[row.original.source]
          return (
            <Badge tone={look.tone} size="sm">
              {t(look.labelKey)}
            </Badge>
          )
        },
      }),

      helper.accessor('createdAt', {
        header: t('attendance.linkedAt'),
        // «17.09.2026» — `incidents-page.tsx` dagi sana ustuni hisobi
        meta: { width: '130px', hideOnMobile: true },
        cell: ({ row }) => (
          <span className="tabular text-[var(--fg-secondary)]">
            {date(row.original.createdAt)}
          </span>
        ),
      }),

      helper.accessor('note', {
        header: t('employees.note'),
        enableSorting: false,
        // Ortgan joyni izoh oladi — erkin matn, uzunligi oldindan noma'lum
        meta: { minWidth: '180px', hideOnMobile: true },
        cell: ({ row }) => (
          <span
            className="truncate-1 block text-[var(--fg-secondary)]"
            title={row.original.note ?? undefined}
          >
            {row.original.note ?? '—'}
          </span>
        ),
      }),

      helper.display({
        id: 'actions',
        header: '',
        // Bitta ikonkali tugma (32px) + katak to'ldirishi (32px) ≈ 64px, zaxira bilan 72px
        meta: { align: 'end', width: '72px' },
        cell: ({ row }) =>
          /*
            Tizim terminalga o'zi yozgan raqam (Bonvi fazasi) UZILMAYDI — server
            409 beradi: raqam o'zgarsa odam terminalda qayta yaratilardi va
            oraliqda eshikni ocha olmasdi (SPEC-agent §E). Tugma o'rniga qulf.
          */
          isManagedPersonNo(row.original.externalPersonId) ? (
            <span
              className="inline-flex size-8 items-center justify-center text-[var(--fg-tertiary)]"
              title={t('attendance.managedNumberLocked')}
              aria-label={t('attendance.managedNumberLocked')}
            >
              <Lock className="size-4" />
            </span>
          ) : (
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              title={t('attendance.unlink')}
              aria-label={t('attendance.unlink')}
              onClick={(event) => {
                event.stopPropagation()
                setUnlinking(row.original)
              }}
            >
              <Unlink className="text-[var(--danger-text)]" />
            </Button>
          ),
      }),
    ],
    [t, date],
  )

  if (!canManage) {
    return <AttendanceNoAccess icon={Link2} title={t('attendance.mappingTitle')} />
  }

  const unmatchedTotal = unmatched.data?.total ?? 0
  const identityRows = identities.data?.items ?? EMPTY
  const identityTotal = identities.data?.total ?? 0

  return (
    <div className="space-y-6">
      <PageHeader
        title={<AttendancePageTitle icon={Link2} title={t('attendance.mappingTitle')} />}
        subtitle={t('attendance.mappingSubtitle')}
        actions={
          <Button variant="create" onClick={() => setImportOpen(true)}>
            <FileSpreadsheet />
            {t('attendance.importTitle')}
          </Button>
        }
      />

      {/* ─────────────────────────── moslanmagan o'tishlar — navbat */}
      <Card className="overflow-hidden">
        <CardHeader
          title={
            <span className="flex items-center gap-2">
              {t('attendance.unmatchedTitle')}
              {/*
                Sariq, qizil emas (SPEC §8.4): moslanmagan o'tish — qilinadigan
                ISH, xavfsizlik hodisasi emas. Qizil faqat ishdan ketgan
                odamning o'tishi uchun.
              */}
              {unmatchedTotal > 0 && (
                <Badge tone="warning" size="sm">
                  <span className="tabular">{num(unmatchedTotal)}</span>
                </Badge>
              )}
            </span>
          }
          subtitle={t('attendance.unmatchedSubtitle')}
        />

        {unmatched.isPending ? (
          <div className={UNMATCHED_GRID} aria-busy>
            {Array.from({ length: 6 }, (_, index) => (
              <div
                key={`skeleton-${index}`}
                className="space-y-3 rounded-xl border border-[var(--border)] p-4"
              >
                <div className="flex items-center gap-3">
                  <Skeleton className="size-14 rounded-md" />
                  <div className="flex-1 space-y-2">
                    <Skeleton className="h-4 w-24" />
                    <Skeleton className="h-3 w-36" />
                  </div>
                </div>
                <Skeleton className="h-8 w-full" />
              </div>
            ))}
          </div>
        ) : unmatched.isError ? (
          <AttendanceLoadError
            error={unmatched.error}
            onRetry={() => void unmatched.refetch()}
            retrying={unmatched.isFetching}
          />
        ) : (unmatchedItems ?? []).length === 0 ? (
          <EmptyState
            icon={CircleCheck}
            title={t('attendance.unmatchedEmptyTitle')}
            description={t('attendance.unmatchedEmptyText')}
          />
        ) : (
          <>
            <ul className={UNMATCHED_GRID}>
              {(unmatchedItems ?? []).map((group) => (
                <UnmatchedTile
                  key={`${group.provider}:${group.rawPersonId}`}
                  group={group}
                  onLink={() => openLink(group)}
                  firstSeen={dateTime(group.firstSeenAt)}
                  lastSeen={dateTime(group.lastSeenAt)}
                />
              ))}
            </ul>

            {unmatchedTotal > UNMATCHED_PAGE_SIZE && (
              <Pagination
                page={unmatchedPage}
                pageSize={UNMATCHED_PAGE_SIZE}
                total={unmatchedTotal}
                onPageChange={setUnmatchedPage}
                disabled={unmatched.isFetching}
                className="border-t border-[var(--border)]"
              />
            )}
          </>
        )}
      </Card>

      {/* ─────────────────────────── mavjud mosliklar */}
      <Card className="overflow-hidden">
        <CardHeader
          title={t('attendance.identitiesTitle')}
          subtitle={
            identities.isPending
              ? t('common.loading')
              : t('attendance.identitiesCount', { n: num(identityTotal) })
          }
          action={
            <Button type="button" variant="soft" size="sm" onClick={() => openLink(null)}>
              <Plus />
              {t('attendance.linkManual')}
            </Button>
          }
        />

        <div className="border-y border-[var(--border)] px-4 py-3.5 sm:px-5">
          <Input
            icon={Search}
            shape="pill"
            type="search"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder={t('attendance.identitiesSearchPlaceholder')}
            aria-label={t('common.search')}
          />
        </div>

        {identities.isError ? (
          <AttendanceLoadError
            error={identities.error}
            onRetry={() => void identities.refetch()}
            retrying={identities.isFetching}
          />
        ) : (
          <>
            <DataTable
              columns={columns}
              data={identityRows}
              getRowId={(item) => item.id}
              loading={identities.isPending}
              rowLabel={(item) => item.employeeName ?? item.externalPersonId}
              empty={
                search === '' ? (
                  <EmptyState
                    icon={UserRoundSearch}
                    title={t('attendance.identitiesEmptyTitle')}
                    description={t('attendance.identitiesEmptyText')}
                    action={
                      <Button variant="create" onClick={() => setImportOpen(true)}>
                        <FileSpreadsheet />
                        {t('attendance.importTitle')}
                      </Button>
                    }
                  />
                ) : (
                  <EmptyState icon={Search} title={t('common.empty')} />
                )
              }
            />

            {identityTotal > DEFAULT_PAGE_SIZE && (
              <Pagination
                page={identityPage}
                pageSize={DEFAULT_PAGE_SIZE}
                total={identityTotal}
                onPageChange={setIdentityPage}
                disabled={identities.isFetching}
                className="border-t border-[var(--border)]"
              />
            )}
          </>
        )}
      </Card>

      <AttendanceIdentityDialog
        open={linkOpen}
        onClose={() => setLinkOpen(false)}
        group={linkGroup}
      />
      <AttendanceImportDialog open={importOpen} onClose={() => setImportOpen(false)} />
      <UnlinkDialog item={unlinking} onClose={() => setUnlinking(null)} />
    </div>
  )
}

const UNMATCHED_GRID = 'grid gap-3 px-4 pb-4 sm:grid-cols-2 sm:px-5 sm:pb-5 xl:grid-cols-3'

/**
 * Bitta moslanmagan raqam: YUZ, raqam, qancha o'tish, qachondan beri.
 *
 * «Birinchi ko'rilgan» ataylab ko'rsatiladi: bog'lash ORQAGA ishlaydi
 * (SPEC §4.3), ya'ni aynan shu sanadan boshlab tabel qayta hisoblanadi —
 * HR qancha tarix o'zgarishini oldindan biladi.
 */
function UnmatchedTile({
  group,
  onLink,
  firstSeen,
  lastSeen,
}: {
  group: UnmatchedGroup
  onLink: () => void
  firstSeen: string
  lastSeen: string
}) {
  const { t, num } = useLocale()

  return (
    <li className="flex flex-col gap-3 rounded-xl border border-[var(--border)] bg-[var(--bg-elevated)] p-4">
      <div className="flex items-start gap-3">
        <AttendancePassFace passId={group.samplePassId} size="md" />

        <div className="min-w-0 flex-1">
          <p className="truncate-1 font-mono text-callout font-semibold">{group.rawPersonId}</p>
          <p className="truncate-1 text-caption1 text-[var(--fg-secondary)]">
            {[identityProviderLabel(group.provider, t), group.lastDeviceName]
              .filter(Boolean)
              .join(' · ')}
          </p>
        </div>

        <Badge tone="warning" size="sm">
          <span className="tabular">{t('attendance.passCountShort', { n: num(group.passCount) })}</span>
        </Badge>
      </div>

      <dl className="grid grid-cols-2 gap-3 rounded-lg bg-[var(--bg-sunken)] px-3 py-2 text-caption1">
        <div className="min-w-0">
          <dt className="text-[var(--fg-tertiary)]">{t('attendance.firstSeen')}</dt>
          <dd className="tabular truncate-1 font-medium">{firstSeen}</dd>
        </div>
        <div className="min-w-0">
          <dt className="text-[var(--fg-tertiary)]">{t('attendance.lastSeen')}</dt>
          <dd className="tabular truncate-1 font-medium">{lastSeen}</dd>
        </div>
      </dl>

      <div className="mt-auto flex gap-2">
        <Button type="button" variant="soft" size="sm" onClick={onLink} className="flex-1">
          <Link2 />
          {t('attendance.linkToEmployee')}
        </Button>
        {/*
          Shu raqamning barcha o'tishlari — SURATLARI bilan. Bitta namuna
          surat yetmasa (qorong'i, yon tomondan), HR boshqa kadrlarga qaraydi;
          jurnal qidiruvi shaxs raqamini ham qidiradi (§6.5).
        */}
        <Link
          to="/verifikatsiya/otishlar"
          search={{ q: group.rawPersonId }}
          className={buttonVariants({ variant: 'ghost', size: 'sm' })}
        >
          <ScanFace />
          {t('attendance.showPasses')}
        </Link>
      </div>
    </li>
  )
}

/**
 * Moslikni uzish.
 *
 * ⚠️ O'tgan o'tishlar o'sha xodimda QOLADI (SPEC §4.3): ular haqiqatan
 * o'sha odam edi va uzish oylar oldin topshirilgan tabelni jimgina qayta
 * yozmasligi kerak. Bog'lanish umuman XATO bo'lgan bo'lsa — to'g'risi
 * yaratiladi va davr qo'lda qayta hisoblanadi. Oyna buni aytadi, aks
 * holda HR «uzdim — tuzaldi» deb o'ylab qolardi.
 */
function UnlinkDialog({
  item,
  onClose,
}: {
  item: AttendanceIdentity | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useDeleteIdentity()

  useEffect(() => {
    if (item !== null) remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!remove.isPending}
      title={t('attendance.unlink')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={remove.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            disabled={remove.isPending}
            onClick={() => remove.mutate(item.id, { onSuccess: onClose })}
          >
            {remove.isPending && <LoaderCircle className="animate-spin" />}
            {t('attendance.unlink')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="flex items-center gap-3 rounded-xl bg-[var(--bg-sunken)] px-4 py-3">
          <EmployeePhoto
            employeeId={item.employeeId}
            photoKey={item.photoKey}
            name={item.employeeName ?? ''}
            size="md"
          />
          <div className="min-w-0">
            <p className="truncate-1 font-medium">{item.employeeName ?? '—'}</p>
            <p className="font-mono text-caption1 text-[var(--fg-secondary)]">
              {item.externalPersonId}
            </p>
          </div>
        </div>

        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('attendance.unlinkConfirm', {
            personId: item.externalPersonId,
            name: item.employeeName ?? '—',
          })}
        </p>

        <Alert tone="info">{t('attendance.unlinkHint')}</Alert>

        {remove.isError && (
          <Alert tone="danger">{errorMessage(problemCode(remove.error))}</Alert>
        )}
      </div>
    </Dialog>
  )
}
