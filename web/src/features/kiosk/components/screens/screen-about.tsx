import { useLocale } from '@/shared/i18n/use-locale'
import { LIMITS } from '../../lib/kiosk-draft'
import { ABOUT_QUESTIONS, REQUIRED_ABOUT, type AboutQuestion } from '../../lib/kiosk-options'
import { KioskTextArea, ScreenHint, useFieldError } from '../kiosk-fields'
import type { ScreenProps } from './screen-props'

/**
 * Blank section 10 — open questions, a few per screen, numbered as on the
 * paper (1–17) so HR can match the PDF. The hint is shown on the first one.
 */
export function AboutScreen({
  draft,
  patch,
  errors,
  questions,
  showHint,
}: ScreenProps & { questions: readonly AboutQuestion[]; showHint: boolean }) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      {showHint && <ScreenHint>{t('kiosk.about.hint')}</ScreenHint>}

      {questions.map((key) => (
        <KioskTextArea
          key={key}
          path={`about.${key}`}
          label={`${ABOUT_QUESTIONS.indexOf(key) + 1}. ${t(`kiosk.about.q.${key}`)}`}
          required={REQUIRED_ABOUT.includes(key)}
          // Optional questions start smaller so a screen of them stays short on a phone; the box grows by hand.
          rows={REQUIRED_ABOUT.includes(key) ? 3 : 2}
          value={draft.about[key]}
          maxLength={key === 'extraNote' ? LIMITS.extraNote : LIMITS.long}
          onChange={(value) => patch({ about: { ...draft.about, [key]: value } })}
          error={fieldError(`about.${key}`)}
        />
      ))}
    </>
  )
}
