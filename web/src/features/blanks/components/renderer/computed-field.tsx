import { Sigma } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Badge } from '@/shared/ui'

import { EMPTY_MARK, formatComputed } from '../../lib/blank-presentation'
import type { ComputedField as ComputedFieldDef, JsonValue } from '../../lib/definition-types'

/**
 * `computed` — read-only, live. The value comes from `<BlankForm>`: the
 * server's last value overlaid with the client evaluation of the current
 * answers, so it changes while the user types (§4.5). A boolean prints as a
 * coloured «Ha / Yo'q» badge; `band` labels and numbers as plain text.
 */
export function ComputedFieldView({
  field,
  value,
  compact = false,
}: {
  field: ComputedFieldDef
  value: JsonValue | undefined
  compact?: boolean
}) {
  const { t, locale } = useLocale()
  const text = formatComputed(field, value, locale, [t('blanks.form.yes'), t('blanks.form.no')])

  const content =
    field.valueType === 'bool' && typeof value === 'boolean' ? (
      <Badge tone={value ? 'success' : 'danger'}>{text}</Badge>
    ) : (
      <span className={text === EMPTY_MARK ? 'text-[var(--fg-tertiary)]' : 'font-semibold tabular-nums'}>{text}</span>
    )

  if (compact) return <div aria-live="polite">{content}</div>

  return (
    <div className="space-y-1.5">
      <p className="text-footnote font-medium text-[var(--fg-secondary)]">{field.label}</p>
      <div
        aria-live="polite"
        className="flex min-h-12 items-center gap-2 rounded-md bg-[var(--bg-sunken)] px-3.5 text-body"
      >
        <Sigma aria-hidden className="size-4 shrink-0 text-[var(--fg-tertiary)]" />
        {content}
      </div>
      <p className="text-footnote text-[var(--fg-tertiary)]">{t('blanks.form.computedHint')}</p>
    </div>
  )
}
