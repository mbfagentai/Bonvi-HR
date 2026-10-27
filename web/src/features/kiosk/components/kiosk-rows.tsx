import { ChevronRight, Pencil, Plus, Trash2 } from 'lucide-react'
import type { ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button, Dialog } from '@/shared/ui'

/*
 * Repeating tables of the blank (education, jobs, references, family) and the
 * language rows: on the screen each entry is one compact line,
 * its fields open in a bottom sheet (`Dialog` is a sheet under 640 px). A long
 * table of inputs no longer pushes the rest of the screen off a phone.
 */

/**
 * One entry as a line: what was filled, and buttons to edit or remove it.
 * The line itself opens the entry as well — the pencil only makes that visible.
 */
export function SummaryRow({
  path,
  title,
  subtitle,
  invalid = false,
  invalidText,
  placeholder = false,
  onOpen,
  onRemove,
}: {
  /** Validation path (`jobs.1`) — the wizard scrolls here when the entry has an error. */
  path: string
  title: ReactNode
  subtitle?: ReactNode
  invalid?: boolean
  invalidText?: string | undefined
  /** Nothing filled yet — the subtitle is shown as a quiet prompt. */
  placeholder?: boolean
  onOpen: () => void
  onRemove?: (() => void) | undefined
}) {
  const { t } = useLocale()

  return (
    <div
      data-field={path}
      className={cn(
        'flex min-h-16 items-center gap-1 rounded-xl border bg-[var(--bg-elevated)] py-1 pr-1.5 pl-4',
        invalid ? 'border-ios-red/60' : 'border-[var(--border-strong)]',
      )}
    >
      <button type="button" onClick={onOpen} className="min-w-0 flex-1 py-2 text-left">
        <span className="block truncate text-body font-medium">{title}</span>
        {subtitle !== undefined && (
          <span
            className={cn(
              'block truncate text-subhead',
              placeholder ? 'text-[var(--fg-tertiary)]' : 'text-[var(--fg-secondary)]',
            )}
          >
            {subtitle}
          </span>
        )}
        {invalid && invalidText !== undefined && (
          <span role="alert" className="block text-subhead text-ios-red">
            {invalidText}
          </span>
        )}
      </button>

      {onRemove === undefined ? (
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={t('kiosk.row.edit')}
          onClick={onOpen}
          className="size-11 shrink-0"
        >
          <ChevronRight />
        </Button>
      ) : (
        <>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label={t('kiosk.row.edit')}
            onClick={onOpen}
            className="size-11 shrink-0"
          >
            <Pencil />
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label={t('kiosk.row.remove')}
            onClick={onRemove}
            className="size-11 shrink-0 text-[var(--danger-text)]"
          >
            <Trash2 />
          </Button>
        </>
      )}
    </div>
  )
}

/** «+ Ish joyi qo'shish» — a dashed tile under the list. */
export function AddRowButton({ onClick, children }: { onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'flex min-h-14 w-full items-center justify-center gap-2 rounded-xl border-2 border-dashed px-4',
        'border-[var(--border-strong)] text-callout font-semibold text-[var(--accent-text)]',
        'transition-colors hover:bg-[var(--bg-hover)] active:bg-[var(--bg-active)]',
      )}
    >
      <Plus className="size-5" />
      {children}
    </button>
  )
}

/**
 * The fields of one entry in a sheet. Changes are kept apart from the draft
 * until «Saqlash» passes the entry's validation; «Bekor qilish» drops them.
 * Not closed by a tap outside — a half-filled entry must not vanish by accident.
 */
export function RowSheet({
  open,
  title,
  onCancel,
  onSave,
  children,
}: {
  open: boolean
  title: ReactNode
  onCancel: () => void
  onSave: () => void
  children: ReactNode
}) {
  const { t } = useLocale()

  return (
    <Dialog
      open={open}
      onClose={onCancel}
      dismissible={false}
      title={title}
      footer={
        <>
          <Button type="button" variant="secondary" size="lg" pill onClick={onCancel} className="h-[52px] flex-1 sm:flex-none">
            {t('kiosk.row.cancel')}
          </Button>
          <Button type="button" variant="primary" size="lg" pill onClick={onSave} className="h-[52px] flex-1 sm:flex-none sm:min-w-36">
            {t('kiosk.row.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-5 pt-1">{children}</div>
    </Dialog>
  )
}

/** A sheet that edits in place (language levels) — only «Tayyor». */
export function PickerSheet({
  open,
  title,
  description,
  onClose,
  secondary,
  children,
}: {
  open: boolean
  title: ReactNode
  description?: ReactNode
  onClose: () => void
  /** Optional left-hand action («Tozalash»). */
  secondary?: ReactNode
  children: ReactNode
}) {
  const { t } = useLocale()

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={title}
      description={description}
      footer={
        <>
          {secondary}
          <Button type="button" variant="primary" size="lg" pill onClick={onClose} className="h-[52px] flex-1 sm:flex-none sm:min-w-36">
            {t('kiosk.row.done')}
          </Button>
        </>
      }
    >
      <div className="space-y-5 pt-1">{children}</div>
    </Dialog>
  )
}
