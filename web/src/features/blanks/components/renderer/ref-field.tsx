import { Search, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button, Input, Skeleton } from '@/shared/ui'

import type { DocRefAnswer, JsonValue, RefAnswer, RefKind } from '../../lib/definition-types'
import { useRenderer } from './renderer-context'

const SEARCH_DEBOUNCE_MS = 300

type SearchState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'done'; items: T[] }

/**
 * Generic «search and pick one» control. The search function is injected by
 * the page (employees API, departments, …): the renderer never fetches itself.
 * Without a search function the field degrades to read-only text.
 */
function SearchPicker<T>({
  selectedLabel,
  search,
  itemKey,
  itemLabel,
  onPick,
  onClear,
  disabled,
  ariaLabel,
  invalid,
}: {
  selectedLabel: string | null
  search: ((query: string) => Promise<T[]>) | undefined
  itemKey: (item: T) => string
  itemLabel: (item: T) => string
  onPick: (item: T) => void
  onClear: () => void
  disabled: boolean
  ariaLabel: string | undefined
  invalid: boolean
}) {
  const { t } = useLocale()
  const [editing, setEditing] = useState(false)
  const [query, setQuery] = useState('')
  const [state, setState] = useState<SearchState<T>>({ status: 'idle' })
  /** Latest search function — callers pass inline lambdas; they must not restart the search. */
  const latestSearch = useRef(search)
  useEffect(() => {
    latestSearch.current = search
  })
  const canSearch = search !== undefined

  useEffect(() => {
    const run = latestSearch.current
    if (!editing || !run || query.trim().length < 2) {
      setState({ status: 'idle' })
      return
    }
    let cancelled = false
    setState({ status: 'loading' })
    const timer = window.setTimeout(() => {
      run(query.trim()).then(
        (items) => !cancelled && setState({ status: 'done', items }),
        () => !cancelled && setState({ status: 'error' }),
      )
    }, SEARCH_DEBOUNCE_MS)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [editing, query, canSearch])

  if (selectedLabel !== null && !editing) {
    return (
      <div
        className={cn(
          'flex min-h-12 items-center gap-2 rounded-md border bg-[var(--bg-elevated)] px-3.5',
          invalid ? 'border-ios-red' : 'border-[var(--border)]',
        )}
      >
        <span className="min-w-0 flex-1 truncate text-body">{selectedLabel}</span>
        {!disabled && search && (
          <>
            <Button type="button" variant="ghost" size="sm" onClick={() => setEditing(true)}>
              {t('blanks.form.change')}
            </Button>
            <Button type="button" variant="ghost" size="icon" aria-label={t('blanks.form.clear')} onClick={onClear}>
              <X />
            </Button>
          </>
        )}
      </div>
    )
  }

  if (!search || disabled) {
    return <p className="text-body text-[var(--fg-tertiary)]">{selectedLabel ?? t('blanks.form.notSelected')}</p>
  }

  return (
    <div className="space-y-1.5">
      <div className="relative">
        <Search
          aria-hidden
          className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[var(--fg-tertiary)]"
        />
        <Input
          value={query}
          onChange={(e) => {
            setQuery(e.target.value)
            setEditing(true)
          }}
          onFocus={() => setEditing(true)}
          placeholder={t('blanks.form.searchPlaceholder')}
          aria-label={ariaLabel}
          aria-invalid={invalid || undefined}
          className="pl-9"
        />
      </div>
      {state.status === 'loading' && (
        <div className="space-y-1">
          <Skeleton className="h-9 w-full" />
          <Skeleton className="h-9 w-2/3" />
        </div>
      )}
      {state.status === 'error' && <p className="text-footnote text-ios-red">{t('blanks.form.searchFailed')}</p>}
      {state.status === 'done' && state.items.length === 0 && (
        <p className="text-footnote text-[var(--fg-secondary)]">{t('blanks.form.searchEmpty')}</p>
      )}
      {state.status === 'done' && state.items.length > 0 && (
        <ul className="max-h-64 overflow-y-auto rounded-md border border-[var(--border)] bg-[var(--bg-elevated)]">
          {state.items.map((item) => (
            <li key={itemKey(item)} className="border-b border-[var(--border)] last:border-b-0">
              <button
                type="button"
                onClick={() => {
                  onPick(item)
                  setEditing(false)
                  setQuery('')
                }}
                className="w-full px-3.5 py-2.5 text-left text-body hover:bg-[var(--bg-hover)]"
              >
                {itemLabel(item)}
              </button>
            </li>
          ))}
        </ul>
      )}
      {selectedLabel !== null && (
        <Button type="button" variant="ghost" size="sm" onClick={() => setEditing(false)}>
          {t('common.cancel')}
        </Button>
      )}
    </div>
  )
}

function readRef(value: JsonValue | undefined): RefAnswer | null {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return null
  const id = value['id']
  const label = value['label']
  return typeof id === 'string' ? { id, label: typeof label === 'string' ? label : '' } : null
}

function readDocRef(value: JsonValue | undefined): DocRefAnswer | null {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return null
  const { submissionId, templateCode, number } = value
  return typeof submissionId === 'string' && typeof templateCode === 'string'
    ? { submissionId, templateCode, number: typeof number === 'string' ? number : '' }
    : null
}

/**
 * `ref` field — employee / candidate / department / position / direction.
 * Stores `{ id, label }`; the label is a name snapshot (§4.3).
 *
 * The existing `AttendanceEmployeePicker` fetches through react-query inside
 * itself, which the props-only renderer must not do; the page injects a
 * `searchRef(kind, query)` instead (it can call the same employees API).
 */
export function RefField({
  refKind,
  value,
  onChange,
  disabled = false,
  label,
  invalid = false,
}: {
  refKind: RefKind
  value: JsonValue | undefined
  onChange: (value: JsonValue) => void
  disabled?: boolean
  label?: string
  invalid?: boolean
}) {
  const { searchRef } = useRenderer()
  const current = readRef(value)
  return (
    <SearchPicker<RefAnswer>
      selectedLabel={current ? current.label || current.id : null}
      search={searchRef ? (q) => searchRef(refKind, q) : undefined}
      itemKey={(item) => item.id}
      itemLabel={(item) => item.label}
      onPick={(item) => onChange({ id: item.id, label: item.label })}
      onClear={() => onChange(null)}
      disabled={disabled}
      ariaLabel={label}
      invalid={invalid}
    />
  )
}

/** `docRef` — a link to another blank (`{ submissionId, templateCode, number }`). */
export function DocRefField({
  templateCodes,
  value,
  onChange,
  disabled = false,
  label,
  invalid = false,
}: {
  templateCodes: readonly string[]
  value: JsonValue | undefined
  onChange: (value: JsonValue) => void
  disabled?: boolean
  label?: string
  invalid?: boolean
}) {
  const { searchDocRef } = useRenderer()
  const current = readDocRef(value)
  const text = (item: DocRefAnswer) => `${item.templateCode} № ${item.number}`
  return (
    <SearchPicker<DocRefAnswer>
      selectedLabel={current ? text(current) : null}
      search={searchDocRef ? (q) => searchDocRef(templateCodes, q) : undefined}
      itemKey={(item) => item.submissionId}
      itemLabel={text}
      onPick={(item) => onChange({ submissionId: item.submissionId, templateCode: item.templateCode, number: item.number })}
      onClear={() => onChange(null)}
      disabled={disabled}
      ariaLabel={label}
      invalid={invalid}
    />
  )
}
