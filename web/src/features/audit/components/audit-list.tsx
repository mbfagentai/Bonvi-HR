import { ChevronRight } from 'lucide-react'
import { useMemo, type ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { DataTable, Skeleton, dataColumnHelper, type DataColumn } from '@/shared/ui'

import type { AuditEntry } from '../api/audit-queries'
import {
  changeSummary,
  entityTypeLabel,
  entryTitle,
  groupEntries,
  operationLabel,
  useAuditFormatter,
  type AuditFormatter,
  type AuditGroup,
} from '../lib/audit-format'
import { AuditActionBadge, AuditActorCell } from './audit-parts'

/**
 * Jadvaldagi qator: yakka yozuv, guruh sarlavhasi yoki guruh ichidagi yozuv.
 *
 * NEGA sarlavha alohida QATOR: bitta biznes amali (masalan «Ko'chirish»)
 * bir nechta yozuv qoldiradi — mehnat munosabati tahriri va yangi buyruq.
 * Ular bitta sarlavha ostida turganda HR «bu bitta harakat» ekanini darhol
 * ko'radi (SPEC §9.5). Umumiy `DataTable` qator guruhlashni bilmaydi va uni
 * o'zgartirish shart emas: sarlavha — kataklari boshqacha chiziladigan
 * oddiy qator.
 */
type AuditRow =
  | { kind: 'single'; entry: AuditEntry }
  | { kind: 'head'; group: AuditGroup }
  | { kind: 'child'; entry: AuditEntry }

const helper = dataColumnHelper<AuditRow>()

function rowsOf(groups: readonly AuditGroup[]): AuditRow[] {
  const rows: AuditRow[] = []

  for (const group of groups) {
    const [first] = group.entries
    if (first === undefined) continue

    if (group.entries.length === 1) {
      rows.push({ kind: 'single', entry: first })
      continue
    }

    rows.push({ kind: 'head', group })
    for (const entry of group.entries) rows.push({ kind: 'child', entry })
  }

  return rows
}

function rowId(row: AuditRow): string {
  return row.kind === 'head' ? `group-${row.group.key}` : row.entry.id
}

/** Qator bosilganda ochiladigan yozuv — sarlavhada guruhning birinchisi. */
function rowEntry(row: AuditRow): AuditEntry | undefined {
  return row.kind === 'head' ? row.group.entries[0] : row.entry
}

/**
 * Guruhning umumiy nomi — hamma yozuv bitta narsaga tegishli bo'lsa
 * (odatda xodim). Turli bo'lsa `null`: noto'g'ri «umumiy» nom chizilmasin.
 */
function commonLabel(group: AuditGroup): string | null {
  const labels = new Set(group.entries.map((entry) => entry.entityLabel))
  const [only] = labels
  return labels.size === 1 && only !== undefined ? only : null
}

/**
 * Ko'rinish: jurnal sahifasi yoki xodim kartasi. Kartada «nima» ustunida
 * yozuv TURI turadi — nomi doim o'sha xodim, uni har qatorda takrorlash shovqin.
 */
export type AuditListVariant = 'journal' | 'employee'

/**
 * ⭐ Jurnal ro'yxati — sahifada ham, xodim kartasida ham BITTA komponent.
 *
 * Keng ekranda `DataTable`, torroq ekranda kartalar (SPEC §9.5; davomat
 * taxtasidagi naqsh — `attendance-dashboard-page.tsx`): beshta ustun
 * telefonda va yon menyuli planshetda sig'maydi, gorizontal aylantirish
 * esa jurnalni o'qib bo'lmas qilardi. Chegara `xl`: jadval faqat to'liq
 * sig'adigan kenglikda chiziladi.
 */
export function AuditList({
  entries,
  loading,
  empty,
  onOpen,
  variant = 'journal',
  fill = false,
}: {
  entries: readonly AuditEntry[]
  loading: boolean
  /** Yozuv yo'qligida — `EmptyState`. */
  empty: ReactNode
  onOpen: (entry: AuditEntry) => void
  variant?: AuditListVariant
  /**
   * Ro'yxat ramkasi (`DataTable fill`, `audit-page.tsx`): jadval ham,
   * kartalar ham kartaning ichida aylanadi. Xodim kartasidagi tarixda —
   * yo'q, u yerda ro'yxat sahifaning bir qismi.
   */
  fill?: boolean
}) {
  const fmt = useAuditFormatter()
  const groups = useMemo(() => groupEntries(entries), [entries])
  const rows = useMemo(() => rowsOf(groups), [groups])

  const columns = useMemo(() => buildColumns(fmt, variant), [fmt, variant])

  return (
    <>
      {/* ---------- xl+: jadval ---------- */}
      <div
        className={cn(
          'hidden xl:block',
          fill &&
            'xl:frame:flex xl:frame:min-h-40 xl:frame:flex-1 xl:frame:flex-col xl:frame:[contain:size]',
        )}
      >
        <DataTable
          fill={fill}
          columns={columns}
          data={rows}
          getRowId={rowId}
          loading={loading}
          skeletonRows={8}
          empty={empty}
          onRowClick={(row) => {
            const entry = rowEntry(row)
            if (entry !== undefined) onOpen(entry)
          }}
          rowLabel={(row) => {
            const entry = rowEntry(row)
            return entry === undefined ? '' : entryTitle(fmt, entry)
          }}
        />
      </div>

      {/* ---------- xl dan tor: kartalar ---------- */}
      <div
        data-list-scroll={fill ? '' : undefined}
        className={cn(
          'xl:hidden',
          fill && 'frame:min-h-40 frame:flex-1 frame:overflow-y-auto frame:[contain:size]',
        )}
      >
        {loading ? (
          <ul className="divide-y divide-[var(--border)]">
            {Array.from({ length: 5 }, (_, index) => (
              <li key={`skeleton-${index}`} className="flex items-center gap-3 px-4 py-4">
                <Skeleton className="size-8 rounded-full" />
                <span className="flex-1 space-y-2">
                  <Skeleton className="h-4 w-40" />
                  <Skeleton className="h-3 w-56" />
                </span>
              </li>
            ))}
          </ul>
        ) : groups.length === 0 ? (
          empty
        ) : (
          <ul className="divide-y divide-[var(--border)]">
            {groups.map((group) => (
              <MobileGroup
                key={group.key}
                fmt={fmt}
                group={group}
                variant={variant}
                onOpen={onOpen}
              />
            ))}
          </ul>
        )}
      </div>
    </>
  )
}

/* ═══════════════════════════════════════════════════════════ jadval */

function buildColumns(fmt: AuditFormatter, variant: AuditListVariant): Array<DataColumn<AuditRow>> {
  const { t, num, dateTime } = fmt

  return [
    /*
      ⭐ Har qator BIR QATORDA (egasi 2026-10-22): kim + rol, amal + amal
      nomi, nima + turi — yonma-yon, ustma-ust emas. `width` lar endi
      PASTKI chegara: katak `nowrap`, ustun mazmunga yetguncha o'zi
      kengayadi (`data-table.tsx`), lekin ortgan joyni olmaydi — uni
      o'zgarishlar ustuni oladi va xulosa o'sha joyga sig'guncha kesiladi.
    */
    helper.display({
      id: 'time',
      header: t('audit.columnTime'),
      // «12.10.2026 14:30» — 16 belgi tabular ≈ 110px + to'ldirish (24px).
      meta: { width: '140px' },
      cell: ({ row }) => {
        const item = row.original
        if (item.kind === 'child') return <GroupRail />

        const entry = item.kind === 'head' ? item.group.entries[0] : item.entry
        if (entry === undefined) return null

        return (
          <span className="tabular text-footnote text-[var(--fg-secondary)]">
            {dateTime(entry.occurredAt)}
          </span>
        )
      },
    }),

    helper.display({
      id: 'actor',
      header: t('audit.columnActor'),
      meta: { width: '200px' },
      cell: ({ row }) => {
        const item = row.original
        if (item.kind === 'child') return null

        const entry = item.kind === 'head' ? item.group.entries[0] : item.entry
        return entry === undefined ? null : <AuditActorCell fmt={fmt} actor={entry.actor} inline />
      },
    }),

    helper.display({
      id: 'action',
      header: t('audit.columnAction'),
      meta: { width: '200px' },
      cell: ({ row }) => {
        const item = row.original

        if (item.kind === 'head') {
          const [first] = item.group.entries
          if (first === undefined) return null

          return (
            <span className="flex items-baseline gap-2">
              <span className="text-subhead font-semibold">{entryTitle(fmt, first)}</span>
              <span className="text-caption1 text-[var(--fg-tertiary)]">
                {t('audit.groupCount', { n: num(item.group.entries.length) })}
              </span>
            </span>
          )
        }

        const operation = item.kind === 'single' ? operationLabel(fmt, item.entry.operation) : null

        return (
          <span className="flex items-center gap-2">
            <AuditActionBadge fmt={fmt} action={item.entry.action} operation={item.entry.operation} />
            {operation !== null && (
              <span className="text-caption1 text-[var(--fg-secondary)]">{operation}</span>
            )}
          </span>
        )
      },
    }),

    helper.display({
      id: 'what',
      header: t('audit.columnWhat'),
      meta: { width: '210px' },
      cell: ({ row }) => {
        const item = row.original

        if (item.kind === 'head') {
          const types = [...new Set(item.group.entries.map((entry) => entityTypeLabel(fmt, entry.entityName)))]
          const label = variant === 'journal' ? commonLabel(item.group) : null

          return (
            <WhatCell primary={label ?? types.join(', ')} secondary={label === null ? null : types.join(', ')} />
          )
        }

        const entry = item.entry
        const type = entityTypeLabel(fmt, entry.entityName)

        return variant === 'employee' || entry.entityLabel === null ? (
          <WhatCell primary={type} secondary={null} />
        ) : (
          <WhatCell primary={entry.entityLabel} secondary={type} />
        )
      },
    }),

    helper.display({
      id: 'changes',
      header: t('audit.columnChanges'),
      /*
        YAGONA `minWidth`li ustun (`data-table.tsx` qoidasi): o'zgarishlar
        xulosasi uzunligi oldindan noma'lum, ortgan joyni shu ustun oladi.
        Matn bir qatorda, ustunga sig'magani uch nuqta bilan kesiladi —
        to'lig'i `title` da va yozuv oynasida. Ichki `absolute` qatlam
        matnni ustunning eng kichik kengligiga QO'SHMAYDI: aks holda bitta
        uzun xulosa (katak `nowrap`) butun jadvalni ekrandan chiqarib
        yuborardi; endi ustun ortgan joyni oladi, tor ekranda 200px gacha
        qisiladi.
      */
      meta: { minWidth: '200px' },
      cell: ({ row }) => {
        const item = row.original
        if (item.kind === 'head') return null

        const summary = changeSummary(fmt, item.entry)
        if (summary === '') return <span className="text-[var(--fg-tertiary)]">—</span>

        return (
          <span className="relative block h-5" title={summary}>
            <span className="absolute inset-0 truncate text-footnote leading-5 text-[var(--fg-secondary)]">
              {summary}
            </span>
          </span>
        )
      },
    }),
  ]
}

/** Guruh ichidagi qatorning chap chizig'i — «shu sarlavhaga tegishli». */
function GroupRail() {
  return <span aria-hidden className="ml-2 block h-8 w-0.5 rounded-full bg-[var(--border-strong)]" />
}

/** Nima — nomi va (bo'lsa) yozuv turi bir qatorda: «Abdullayev Ahad · Xodim». */
function WhatCell({ primary, secondary }: { primary: string; secondary: string | null }) {
  return (
    <span className="flex items-baseline gap-2">
      <span className="font-medium">{primary}</span>
      {secondary !== null && (
        <span className="text-caption1 text-[var(--fg-tertiary)]">{secondary}</span>
      )}
    </span>
  )
}

/* ═══════════════════════════════════════════════════════════ kartalar */

/**
 * Telefonda bitta guruh — sarlavhasida kim va qachon, ostida yozuvlar
 * (har biri bosiladigan qator). Yakka yozuv ham shu shaklda: telefonda
 * ikki xil ko'rinish chalkash bo'lardi.
 */
function MobileGroup({
  fmt,
  group,
  variant,
  onOpen,
}: {
  fmt: AuditFormatter
  group: AuditGroup
  variant: AuditListVariant
  onOpen: (entry: AuditEntry) => void
}) {
  const { t } = useLocale()
  const [first] = group.entries
  if (first === undefined) return null

  const grouped = group.entries.length > 1

  return (
    <li className="px-4 py-3.5">
      <div className="flex items-start justify-between gap-3">
        <AuditActorCell fmt={fmt} actor={first.actor} />
        <span className="tabular shrink-0 pt-0.5 text-caption1 text-[var(--fg-tertiary)]">
          {fmt.dateTime(first.occurredAt)}
        </span>
      </div>

      {grouped && (
        <p className="mt-2.5 text-footnote font-semibold">
          {entryTitle(fmt, first)}
          <span className="font-normal text-[var(--fg-tertiary)]">
            {' · '}
            {t('audit.groupCount', { n: fmt.num(group.entries.length) })}
          </span>
        </p>
      )}

      <ul className="mt-2 space-y-1.5">
        {group.entries.map((entry) => (
          <li key={entry.id}>
            <MobileEntry
              fmt={fmt}
              entry={entry}
              showOperation={!grouped}
              variant={variant}
              onOpen={() => onOpen(entry)}
            />
          </li>
        ))}
      </ul>
    </li>
  )
}

function MobileEntry({
  fmt,
  entry,
  showOperation,
  variant,
  onOpen,
}: {
  fmt: AuditFormatter
  entry: AuditEntry
  showOperation: boolean
  variant: AuditListVariant
  onOpen: () => void
}) {
  const type = entityTypeLabel(fmt, entry.entityName)
  const operation = showOperation ? operationLabel(fmt, entry.operation) : null
  const summary = changeSummary(fmt, entry)
  const label = variant === 'journal' ? entry.entityLabel : null

  return (
    <button
      type="button"
      onClick={onOpen}
      className={cn(
        'press flex w-full items-center gap-3 rounded-md px-3 py-2.5 text-left',
        'bg-[var(--bg-sunken)] transition-colors duration-[120ms] ease-[var(--ease-ios)] hover:bg-[var(--bg-hover)]',
      )}
    >
      <span className="min-w-0 flex-1 space-y-1">
        <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <AuditActionBadge fmt={fmt} action={entry.action} operation={entry.operation} />
          {operation !== null && <span className="text-caption1 font-medium">{operation}</span>}
        </span>

        <span className="truncate-1 block text-footnote">
          {label !== null ? (
            <>
              <span className="font-medium">{label}</span>
              <span className="text-[var(--fg-tertiary)]">{` · ${type}`}</span>
            </>
          ) : (
            <span className="font-medium">{type}</span>
          )}
        </span>

        {summary !== '' && (
          <span className="truncate-1 block text-caption1 text-[var(--fg-secondary)]">{summary}</span>
        )}
      </span>

      <ChevronRight aria-hidden className="size-4 shrink-0 text-[var(--fg-tertiary)]" />
    </button>
  )
}
