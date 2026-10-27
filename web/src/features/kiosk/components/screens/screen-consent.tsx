import { Check } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { LIMITS } from '../../lib/kiosk-draft'
import { CONSENTS } from '../../lib/kiosk-options'
import { FieldMessage, KioskText, Label, useFieldError } from '../kiosk-fields'
import { SignaturePad } from '../signature-pad'
import type { ScreenProps } from './screen-props'

/* Blank section 11. */

/** The five statements — each must be ticked on its own (no «tick all»). */
export function ConsentScreen({ draft, patch, errors }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  const fullName = [draft.lastName, draft.firstName, draft.middleName]
    .map((part) => part.trim())
    .filter(Boolean)
    .join(' ')

  return (
    <>
      <p className="text-body font-medium">{t('kiosk.consent.intro', { name: fullName })}</p>

      <div className="space-y-2.5">
        {CONSENTS.map((key) => {
          const checked = draft.consents[key]
          const error = fieldError(`consents.${key}`)

          return (
            <div key={key} data-field={`consents.${key}`} className="space-y-1.5">
              <label
                className={cn(
                  'flex cursor-pointer items-start gap-3 rounded-xl border p-4 select-none',
                  'transition-colors duration-150 has-[:focus-visible]:ring-4 has-[:focus-visible]:ring-[var(--accent)]/20',
                  checked
                    ? 'border-[var(--accent)] bg-[var(--accent-soft)]'
                    : error
                      ? 'border-ios-red/60 bg-[var(--bg-elevated)]'
                      : 'border-[var(--border-strong)] bg-[var(--bg-elevated)]',
                )}
              >
                <input
                  type="checkbox"
                  checked={checked}
                  onChange={(event) => patch({ consents: { ...draft.consents, [key]: event.target.checked } })}
                  className="sr-only"
                />
                <span
                  aria-hidden
                  className={cn(
                    'mt-0.5 grid size-6 shrink-0 place-items-center rounded-md border-2 transition-colors',
                    checked ? 'border-[var(--accent)] bg-[var(--accent)]' : 'border-[var(--border-strong)]',
                  )}
                >
                  {checked && <Check className="size-4 text-[var(--fg-on-accent)]" strokeWidth={3.5} />}
                </span>
                <span className="text-callout leading-snug">{t(`kiosk.consent.${key}`)}</span>
              </label>
              <FieldMessage error={error} />
            </div>
          )
        })}
      </div>
    </>
  )
}

/** Typed full name, the finger signature and today's date. */
export function SignatureScreen({
  draft,
  patch,
  errors,
  today,
  hasSignature,
  onSignature,
}: ScreenProps & { hasSignature: boolean; onSignature: (signature: Blob | null) => void }) {
  const { t, date } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <KioskText
        path="signatureName"
        label={t('kiosk.field.signatureName')}
        required
        hint={t('kiosk.field.signatureNameHint')}
        value={draft.signatureName}
        maxLength={LIMITS.short}
        autoComplete="off"
        onChange={(event) => patch({ signatureName: event.target.value })}
        error={fieldError('signatureName')}
      />

      <div className="space-y-2.5">
        <p className="text-body font-medium">
          <Label required>{t('kiosk.signature.label')}</Label>
        </p>
        <SignaturePad hasSignature={hasSignature} onChange={onSignature} error={fieldError('signature')} />
      </div>

      <p className="text-callout text-[var(--fg-secondary)]">{t('kiosk.consent.date', { date: date(today) })}</p>
    </>
  )
}
