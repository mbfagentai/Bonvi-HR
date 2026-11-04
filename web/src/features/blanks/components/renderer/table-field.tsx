import { Plus, Trash2 } from 'lucide-react'
import type { ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui'

import { TABLE_HARD_MAX_ROWS } from '../../lib/answers-zod'
import type { ColumnField, JsonValue, TableField as TableFieldDef } from '../../lib/definition-types'

type Row = { [key: string]: JsonValue }

export type RenderCell = (
  column: ColumnField,
  value: JsonValue | undefined,
  onChange: (value: JsonValue) => void,
  path: string,
) => ReactNode

function readRows(value: JsonValue | undefined): Row[] {
  if (!Array.isArray(value)) return []
  return value.filter((r): r is Row => typeof r === 'object' && r !== null && !Array.isArray(r))
}

/** Keep a fixed-row table aligned with its definition: exactly those rows, in order. */
function alignFixed(field: TableFieldDef, rows: Row[]): Row[] {
  return (field.fixedRows ?? []).map((fixed) => {
    const existing = rows.find((r) => r['_row'] === fixed.id)
    return { ...existing, _row: fixed.id }
  })
}

/**
 * `table` — repeating rows (`minRows` / `maxRows`) or a fixed-row matrix
 * (criteria × score + note). Cells are ordinary field inputs rendered by the
 * parent (`renderCell`), so a cell behaves exactly like a top-level field.
 * Fixed-row cells may be printed text (`cells[col].readOnly` + `text`).
 */
export function TableField({
  field,
  path,
  value,
  onChange,
  disabled = false,
  renderCell,
}: {
  field: TableFieldDef
  path: string
  value: JsonValue | undefined
  onChange: (value: JsonValue) => void
  disabled?: boolean
  renderCell: RenderCell
}) {
  const { t } = useLocale()
  const fixed = field.fixedRows !== undefined && field.fixedRows.length > 0
  const rows = fixed ? alignFixed(field, readRows(value)) : readRows(value)
  const minRows = field.minRows ?? 0
  const maxRows = Math.min(field.maxRows ?? TABLE_HARD_MAX_ROWS, TABLE_HARD_MAX_ROWS)

  const setCell = (index: number, columnId: string, cell: JsonValue) => {
    const next = rows.map((row, i) => {
      if (i !== index) return row
      const updated: Row = { ...row }
      if (cell === null) delete updated[columnId]
      else updated[columnId] = cell
      return updated
    })
    onChange(next)
  }

  const addRow = () => onChange([...rows, {}])
  const removeRow = (index: number) => {
    const next = rows.filter((_, i) => i !== index)
    onChange(next.length === 0 ? null : next)
  }

  const editableColumns = field.columns
  const showActions = !fixed && !disabled

  return (
    <div className="space-y-2">
      <div className="overflow-x-auto rounded-md border border-[var(--border)]">
        <table className="w-full min-w-[36rem] border-collapse text-left">
          <thead className="bg-[var(--bg-sunken)]">
            <tr>
              <th scope="col" className="w-10 px-3 py-2 text-footnote font-medium text-[var(--fg-secondary)]">
                {fixed ? '' : '№'}
              </th>
              {editableColumns.map((column) => (
                <th key={column.id} scope="col" className="px-3 py-2 text-footnote font-medium text-[var(--fg-secondary)]">
                  {column.label ?? column.id}
                  {column.required && !disabled && <span className="text-ios-red"> *</span>}
                </th>
              ))}
              {showActions && <th scope="col" className="w-12" aria-label={t('blanks.form.removeRow')} />}
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr>
                <td colSpan={editableColumns.length + 2} className="px-3 py-4 text-center text-subhead text-[var(--fg-tertiary)]">
                  {t('blanks.form.noRows')}
                </td>
              </tr>
            )}
            {rows.map((row, index) => {
              const fixedRow = fixed ? field.fixedRows?.[index] : undefined
              return (
                <tr key={fixedRow?.id ?? index} className="border-t border-[var(--border)] align-top">
                  <th
                    scope="row"
                    className={cn(
                      'px-3 py-2.5 text-subhead font-normal',
                      fixedRow ? 'min-w-40 text-[var(--fg)]' : 'text-[var(--fg-tertiary)] tabular-nums',
                    )}
                  >
                    {fixedRow ? fixedRow.label : index + 1}
                  </th>
                  {editableColumns.map((column) => {
                    const printed = fixedRow?.cells?.[column.id]
                    const cellPath = `${path}[${index}].${column.id}`
                    return (
                      <td key={column.id} className="min-w-36 px-2 py-1.5">
                        {printed?.readOnly ? (
                          <span className="block px-1 py-2 text-subhead text-[var(--fg-secondary)]">{printed.text ?? '—'}</span>
                        ) : (
                          renderCell(column, row[column.id], (cell) => setCell(index, column.id, cell), cellPath)
                        )}
                      </td>
                    )
                  })}
                  {showActions && (
                    <td className="px-1 py-1.5">
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        disabled={rows.length <= minRows}
                        aria-label={t('blanks.form.removeRowN', { n: index + 1 })}
                        onClick={() => removeRow(index)}
                      >
                        <Trash2 />
                      </Button>
                    </td>
                  )}
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      {showActions && (
        <div className="flex items-center gap-3">
          <Button type="button" variant="secondary" size="sm" disabled={rows.length >= maxRows} onClick={addRow}>
            <Plus />
            {t('blanks.form.addRow')}
          </Button>
          <span className="text-footnote text-[var(--fg-tertiary)]">
            {t('blanks.form.rowsLimit', { used: rows.length, max: maxRows })}
          </span>
        </div>
      )}
    </div>
  )
}
