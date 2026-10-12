import { ChevronRight } from 'lucide-react'
import type { ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import type { RoleCatalog } from '@/features/roles/api/roles-api'
import { useRoleCatalog } from '@/features/roles/api/roles-queries'
import { ROLES_MANAGE, useRoleLabels } from '@/features/roles/lib/role-presentation'
import { AuditAction } from '@/shared/api/generated/model'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Dialog, Skeleton } from '@/shared/ui'

import { useAuditEntryDetails, type AuditChange, type AuditEntry } from '../api/audit-queries'
import {
  EMPTY_TEXT,
  MASKED_TEXT,
  changeSummary,
  changeValueText,
  entityTypeLabel,
  entryTitle,
  fieldLabel,
  isRolePermissions,
  rolePermissionLines,
  simplifyUserAgent,
  useAuditFormatter,
  type AuditFormatter,
  type RoleLabelSet,
} from '../lib/audit-format'
import { AuditActionBadge, AuditActorCell } from './audit-parts'

/**
 * ⭐ Bitta yozuvning tafsiloti (SPEC §9.5): nima o'zgardi — «Maydon | Oldin |
 * Keyin», keyin qayerdan (IP va qurilma), keyin shu harakatning boshqa
 * qatorlari («Shu amal ichida»).
 *
 * Ro'yxatdagi yozuv darhol chiziladi (o'zgarishlar unda allaqachon bor),
 * IP, qurilma va bog'liq qatorlar esa alohida so'rov bilan keladi — oyna
 * bo'sh turmaydi. Bog'liq qator bosilsa, oyna o'sha yozuvga o'tadi.
 */
export function AuditEntryDialog({
  entry,
  onClose,
  onOpen,
}: {
  entry: AuditEntry | null
  onClose: () => void
  onOpen: (entry: AuditEntry) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const fmt = useAuditFormatter()
  const roleLabels = useRoleLabels()
  const { hasPermission } = useAuth()

  const details = useAuditEntryDetails(entry?.id ?? null)

  /*
    Rol yozuvining ruxsatlari darajalar tilida chiziladi — buning uchun
    muharrirning katalogi kerak. Rol qatorlarini faqat `roles.manage`
    egasi ko'radi (bo'limning kirish kodi) va katalog ham shu ruxsat bilan
    ochiq, ya'ni bu yerda 403 bo'lmaydi.
  */
  const catalog = useRoleCatalog(entry?.entityName === 'Role' && hasPermission(ROLES_MANAGE))

  if (entry === null) return null

  // Server javobi kelgach — o'sha (yangiroq) nusxa; ungacha ro'yxatdagisi.
  const current = details.data?.entry ?? entry
  const type = entityTypeLabel(fmt, current.entityName)

  return (
    <Dialog
      open
      onClose={onClose}
      size="lg"
      title={entryTitle(fmt, current)}
      description={current.entityLabel === null ? type : `${current.entityLabel} · ${type}`}
    >
      <div className="space-y-6">
        {/* ---------- kim va qachon ---------- */}
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg bg-[var(--bg-sunken)] px-4 py-3">
          <AuditActorCell fmt={fmt} actor={current.actor} size="md" />
          <div className="flex flex-col items-end gap-1">
            <AuditActionBadge fmt={fmt} action={current.action} operation={current.operation} />
            <span className="tabular text-caption1 text-[var(--fg-secondary)]">
              {fmt.dateTime(current.occurredAt)}
            </span>
          </div>
        </div>

        {/* ---------- o'zgarishlar ---------- */}
        <section className="space-y-2.5">
          <SectionTitle>{t('audit.columnChanges')}</SectionTitle>
          <ChangeTable
            fmt={fmt}
            entry={current}
            catalog={catalog.data ?? null}
            labels={roleLabels}
          />
        </section>

        {/* ---------- qayerdan ---------- */}
        <section className="space-y-2.5">
          {details.isError ? (
            <Alert tone="danger" title={t('audit.detailsFailed')}>
              {errorMessage(problemCode(details.error))}
            </Alert>
          ) : (
            <dl className="divide-y divide-[var(--border)] rounded-lg border border-[var(--border)]">
              <MetaRow label={t('audit.ip')} loading={details.isPending}>
                {details.data?.ipAddress ?? EMPTY_TEXT}
              </MetaRow>
              <MetaRow
                label={t('audit.device')}
                loading={details.isPending}
                title={details.data?.userAgent ?? undefined}
              >
                {details.data?.userAgent == null ? EMPTY_TEXT : simplifyUserAgent(details.data.userAgent)}
              </MetaRow>
            </dl>
          )}
        </section>

        {/* ---------- shu amal ichida ---------- */}
        {details.data !== undefined && details.data.related.length > 0 && (
          <section className="space-y-2.5">
            <SectionTitle>{t('audit.related')}</SectionTitle>
            <ul className="space-y-1.5">
              {details.data.related.map((item) => (
                <li key={item.id}>
                  <RelatedRow fmt={fmt} entry={item} onOpen={() => onOpen(item)} />
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>
    </Dialog>
  )
}

function SectionTitle({ children }: { children: ReactNode }) {
  return (
    <h3 className="text-caption1 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase">
      {children}
    </h3>
  )
}

/* ═══════════════════════════════════════════════════ o'zgarishlar jadvali */

/**
 * Tahrirda uch ustun — «Maydon | Oldin | Keyin». Yaratish, o'chirish va
 * hodisalarda ikki ustun — «Maydon | Qiymat»: «Oldin» yoki «Keyin» ustuni u
 * yerda har qatorda bo'sh turardi va faqat shovqin bo'lardi.
 */
function ChangeTable({
  fmt,
  entry,
  catalog,
  labels,
}: {
  fmt: AuditFormatter
  entry: AuditEntry
  catalog: RoleCatalog | null
  labels: RoleLabelSet
}) {
  const { t } = fmt

  if (entry.changes.length === 0) {
    return <p className="text-footnote text-[var(--fg-tertiary)]">{t('audit.noFieldChanges')}</p>
  }

  const diff = entry.action === AuditAction.Update
  const grid = diff
    ? 'grid-cols-[minmax(0,0.9fr)_minmax(0,1.2fr)_minmax(0,1.2fr)]'
    : 'grid-cols-[minmax(0,0.9fr)_minmax(0,2fr)]'

  return (
    <div className="overflow-hidden rounded-lg border border-[var(--border)]">
      <div
        className={cn(
          'grid gap-3 border-b border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-2',
          'text-caption2 font-semibold tracking-wider text-[var(--fg-secondary)] uppercase',
          grid,
        )}
      >
        <span>{t('audit.field')}</span>
        {diff ? (
          <>
            <span>{t('audit.before')}</span>
            <span>{t('audit.after')}</span>
          </>
        ) : (
          <span>{t('audit.value')}</span>
        )}
      </div>

      <ul className="divide-y divide-[var(--border)]">
        {entry.changes.map((change) => (
          <li key={change.field} className={cn('grid gap-3 px-4 py-2.5 text-footnote', grid)}>
            <FieldCell fmt={fmt} field={change.field} />

            {isRolePermissions(entry, change) && !change.masked ? (
              <RoleLines
                lines={rolePermissionLines(change, catalog, labels)}
                className={diff ? 'col-span-2' : undefined}
                emptyText={t('audit.noFieldChanges')}
              />
            ) : diff ? (
              <>
                <ValueCell fmt={fmt} entry={entry} change={change} side="old" muted />
                <ValueCell fmt={fmt} entry={entry} change={change} side="new" />
              </>
            ) : (
              <ValueCell
                fmt={fmt}
                entry={entry}
                change={change}
                // Yaratish va hodisada — yangisi, o'chirishda — eskisi
                side={entry.action === AuditAction.Delete ? 'old' : 'new'}
              />
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}

/** Maydon nomi. Tarjimasi yo'q — xom nomi, ikkinchi darajali rangda (SPEC §9.9). */
function FieldCell({ fmt, field }: { fmt: AuditFormatter; field: string }) {
  const label = fieldLabel(fmt, field)

  return (
    <span
      className={cn(
        'min-w-0 break-words',
        label.known ? 'font-medium' : 'text-[var(--fg-tertiary)]',
      )}
    >
      {label.text}
    </span>
  )
}

function ValueCell({
  fmt,
  entry,
  change,
  side,
  muted = false,
}: {
  fmt: AuditFormatter
  entry: AuditEntry
  change: AuditChange
  side: 'old' | 'new'
  muted?: boolean
}) {
  if (change.masked) {
    // Qiymat yo'q — faqat «o'zgargani» ko'rinadi (SPEC §7.10)
    return (
      <span
        title={fmt.t('audit.masked')}
        aria-label={fmt.t('audit.masked')}
        className="w-fit cursor-help text-[var(--fg-tertiary)]"
      >
        {MASKED_TEXT}
      </span>
    )
  }

  const text = changeValueText(fmt, entry, change, side)
  if (text === null) return <span className="text-[var(--fg-tertiary)]">{EMPTY_TEXT}</span>

  return (
    <span
      className={cn(
        'tabular min-w-0 break-words whitespace-pre-wrap',
        muted && 'text-[var(--fg-secondary)]',
      )}
    >
      {text}
    </span>
  )
}

/** Rol ruxsatlarining farqi — har o'zgarish alohida qatorda. */
function RoleLines({
  lines,
  className,
  emptyText,
}: {
  lines: string[]
  className?: string
  emptyText: string
}) {
  if (lines.length === 0) {
    return <span className={cn('text-[var(--fg-tertiary)]', className)}>{emptyText}</span>
  }

  return (
    <ul className={cn('min-w-0 space-y-0.5', className)}>
      {lines.map((line) => (
        <li
          key={line}
          className={cn(
            'break-words',
            line.startsWith('+ ') && 'text-ios-green',
            line.startsWith('− ') && 'text-ios-red',
          )}
        >
          {line}
        </li>
      ))}
    </ul>
  )
}

/* ═══════════════════════════════════════════════════════════ qolganlari */

function MetaRow({
  label,
  loading,
  title,
  children,
}: {
  label: string
  loading: boolean
  title?: string
  children: ReactNode
}) {
  return (
    <div className="flex items-center justify-between gap-4 px-4 py-2.5 text-footnote">
      <dt className="shrink-0 text-[var(--fg-secondary)]">{label}</dt>
      <dd className="tabular min-w-0 truncate-1 text-right font-medium" title={title}>
        {loading ? <Skeleton className="h-4 w-28" /> : children}
      </dd>
    </div>
  )
}

function RelatedRow({
  fmt,
  entry,
  onOpen,
}: {
  fmt: AuditFormatter
  entry: AuditEntry
  onOpen: () => void
}) {
  const type = entityTypeLabel(fmt, entry.entityName)
  const summary = changeSummary(fmt, entry)

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
          <span className="truncate-1 text-footnote font-medium">
            {entry.entityLabel === null ? type : `${type} · ${entry.entityLabel}`}
          </span>
        </span>
        {summary !== '' && (
          <span className="truncate-1 block text-caption1 text-[var(--fg-secondary)]">{summary}</span>
        )}
      </span>
      <ChevronRight aria-hidden className="size-4 shrink-0 text-[var(--fg-tertiary)]" />
    </button>
  )
}
