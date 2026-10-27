import { useState } from 'react'

import type { KioskSession } from '../../api/kiosk-api'
import {
  rowErrors,
  type FieldErrors,
  type KioskDraft,
  type RowListKey,
  type RowLists,
  type ValidationContext,
} from '../../lib/kiosk-draft'
import { openSheet, scrollToFirstError } from '../../lib/scroll-to-error'

/** What every wizard screen receives from `KioskWizard`. */
export interface ScreenProps {
  draft: KioskDraft
  /** Shallow merge into the draft. */
  patch: (changes: Partial<KioskDraft>) => void
  /** This screen's errors — empty until «Keyingi» was pressed on it once. */
  errors: FieldErrors
  session: KioskSession
  /** Tashkent today, `yyyy-MM-dd`. */
  today: string
  /** For validating an entry inside its sheet with the same rules as the form. */
  context: ValidationContext
  /** Office tablet (SPEC §7) — the browser must not offer the previous candidate's answers. */
  tablet: boolean
}

/** `autocomplete` token on a personal device; `off` on the shared tablet. */
export function autofill(tablet: boolean, token: string): string {
  return tablet ? 'off' : token
}

/** Replaces one element of a list field immutably. */
export function replaceAt<T>(list: readonly T[], index: number, changes: Partial<T>): T[] {
  return list.map((item, at) => (at === index ? { ...item, ...changes } : item))
}

export function removeAt<T>(list: readonly T[], index: number): T[] {
  return list.filter((_, at) => at !== index)
}

type Row<K extends RowListKey> = RowLists[K][number]

interface Editing<K extends RowListKey> {
  /** Position in the list; `rows.length` for a new entry. */
  index: number
  value: Row<K>
  /** «Saqlash» was pressed once (or the entry was opened with an error) — errors are shown. */
  attempted: boolean
  /**
   * `false` once closed. The entry itself stays until the next one is opened,
   * so the sheet keeps its content while it slides away.
   */
  open: boolean
}

/**
 * State of the bottom sheet for one repeating table: a copy of the entry is
 * edited, «Saqlash» validates it with the form's own rules and only then
 * writes it into the draft.
 */
export function useRowEditor<K extends RowListKey>(
  list: K,
  { draft, patch, context }: Pick<ScreenProps, 'draft' | 'patch' | 'context'>,
  empty: () => Row<K>,
) {
  const [editing, setEditing] = useState<Editing<K> | null>(null)
  const rows: readonly Row<K>[] = draft[list]

  const write = (next: Row<K>[]) => {
    const changes: Partial<RowLists> = { [list]: next }
    patch(changes)
  }

  const errors: FieldErrors =
    editing === null || !editing.open || !editing.attempted
      ? {}
      : rowErrors(draft, context, list, editing.index, editing.value)

  const close = () => setEditing((current) => (current === null ? null : { ...current, open: false }))

  return {
    rows,
    /** The entry in the sheet (also while it is closing). */
    editing,
    isOpen: editing?.open === true,
    /** Field path prefix of the entry in the sheet: `jobs.2`. */
    base: editing === null ? '' : `${list}.${editing.index}`,
    errors,
    add: () => setEditing({ index: rows.length, value: empty(), attempted: false, open: true }),
    edit: (index: number) => {
      const row = rows[index]
      if (row === undefined) return
      const invalid = Object.keys(rowErrors(draft, context, list, index, row)).length > 0
      setEditing({ index, value: row, attempted: invalid, open: true })
    },
    set: (changes: Partial<Row<K>>) =>
      setEditing((current) => (current === null ? null : { ...current, value: { ...current.value, ...changes } })),
    cancel: close,
    save: () => {
      if (editing === null || !editing.open) return

      const found = rowErrors(draft, context, list, editing.index, editing.value)
      if (Object.keys(found).length > 0) {
        setEditing({ ...editing, attempted: true })
        scrollToFirstError(found, openSheet() ?? document)
        return
      }

      const next = [...rows]
      next[editing.index] = editing.value
      write(next)
      close()
    },
    remove: (index: number) => write(removeAt(rows, index)),
  }
}
