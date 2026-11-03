import { Lock } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Badge, Card, CardContent, CardHeader } from '@/shared/ui'

import { sectionFill } from '../../lib/blank-presentation'
import type { Answers, JsonValue, Section } from '../../lib/definition-types'
import { FieldInput } from './field-input'

/**
 * One section as an inset card. Editable sections show inputs; the others
 * print their values (who fills them is shown as a badge: a step title or
 * «open»). Fields hidden by `when` are not rendered at all.
 */
export function SectionView({
  section,
  answers,
  editable,
  hiddenFields,
  stepTitle,
  onFieldChange,
}: {
  section: Section
  answers: Answers
  editable: boolean
  hiddenFields: ReadonlySet<string>
  /** Title of the step that fills this section (`fill.at = step`). */
  stepTitle: string | undefined
  onFieldChange: (fieldId: string, value: JsonValue) => void
}) {
  const { t } = useLocale()
  const fill = sectionFill(section)
  const fields = section.fields.filter((f) => !hiddenFields.has(f.id))
  if (fields.length === 0) return null

  const badge =
    fill.at === 'step' ? (
      <Badge tone="neutral">{t('blanks.form.fillAtStep', { step: stepTitle ?? fill.step })}</Badge>
    ) : fill.at === 'open' ? (
      <Badge tone="neutral">{t('blanks.form.fillOpen')}</Badge>
    ) : null

  return (
    <Card aria-labelledby={`blank-section-${section.id}`}>
      <CardHeader
        title={<span id={`blank-section-${section.id}`}>{section.title}</span>}
        subtitle={section.hint}
        action={
          <div className="flex items-center gap-2">
            {badge}
            {!editable && (
              <span className="text-[var(--fg-tertiary)]" title={t('blanks.form.readOnlySection')}>
                <Lock aria-label={t('blanks.form.readOnlySection')} className="size-4" />
              </span>
            )}
          </div>
        }
      />
      <CardContent className="space-y-5">
        {fields.map((field) => (
          <FieldInput
            key={field.id}
            field={field}
            path={field.id}
            value={answers[field.id]}
            onChange={(value) => onFieldChange(field.id, value)}
            readOnly={!editable}
          />
        ))}
      </CardContent>
    </Card>
  )
}
