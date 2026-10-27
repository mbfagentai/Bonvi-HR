import { ClipboardPen, FileText, LoaderCircle } from 'lucide-react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { LOCALE_CODES, localeMeta } from '@/shared/i18n/locales'
import { Alert, Button, Card, CardContent, CardDivider, CardHeader } from '@/shared/ui'
import { useCandidateForms, useDownloadCandidateFormPdf } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'

/** «O'zbekcha», «Ўзбекча», «Русский» — the language in its own name; an unknown code as is. */
function languageName(code: string): string {
  return (LOCALE_CODES as readonly string[]).includes(code) ? localeMeta(code).label : code
}

/**
 * «Onlayn anketa» — the HR-F02 forms this candidate filled through the public
 * form (office tablet or own phone). Nothing is drawn when there are none (most
 * candidates): an empty card would sit on every old candidate's page saying
 * nothing.
 *
 * The PDF button renders the form fresh on the server, so it works even when
 * the attached copy is missing from «Fayllar» (files cap, or deleted by HR).
 */
export function CandidateFormsCard({ candidateId }: { candidateId: string }) {
  const { t, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const forms = useCandidateForms(candidateId)
  const download = useDownloadCandidateFormPdf()

  if (forms.data === undefined || forms.data.length === 0) return null

  return (
    <Card>
      <CardHeader title={t('recruiting.forms.title')} subtitle={t('recruiting.forms.subtitle')} />
      <CardDivider />

      <CardContent className="space-y-3 pt-4">
        {download.isError && <Alert tone="danger">{errorMessage(recruitingErrorCode(download.error))}</Alert>}

        <ul className="space-y-2">
          {forms.data.map((form) => {
            const pending = download.isPending && download.variables?.formId === form.id

            return (
              <li
                key={form.id}
                className="flex flex-wrap items-center gap-3 rounded-md bg-[var(--bg-sunken)] px-3.5 py-2.5"
              >
                <ClipboardPen className="size-5 shrink-0 text-[var(--fg-secondary)]" />

                {/* At least 12rem for the line; on a narrow card the PDF button moves under it */}
                <div className="min-w-48 flex-1">
                  {/* «Anketa № 123 · 24.10.2026 14:41 · O'zbekcha» — a narrow card wraps between the parts, never inside one */}
                  <p className="tabular text-subhead">
                    <span className="font-semibold whitespace-nowrap">
                      {t('recruiting.forms.number', { n: form.number })}
                    </span>{' '}
                    <span className="whitespace-nowrap text-[var(--fg-secondary)]">
                      · {form.submittedAt === '' ? '—' : dateTime(form.submittedAt)}
                    </span>
                    {form.language !== '' && (
                      <>
                        {' '}
                        <span className="whitespace-nowrap text-[var(--fg-secondary)]">
                          · {languageName(form.language)}
                        </span>
                      </>
                    )}
                  </p>
                  {form.fileId === null && (
                    <p className="mt-0.5 text-caption1 text-[var(--fg-tertiary)]">{t('recruiting.forms.notAttached')}</p>
                  )}
                </div>

                <Button
                  type="button"
                  size="sm"
                  variant="secondary"
                  disabled={pending}
                  onClick={() =>
                    download.mutate({
                      id: candidateId,
                      formId: form.id,
                      fallbackName: `Anketa-${form.formCode}-${form.number}.pdf`,
                    })
                  }
                >
                  {pending ? <LoaderCircle className="animate-spin" /> : <FileText />}
                  {t('recruiting.forms.pdf')}
                </Button>
              </li>
            )
          })}
        </ul>
      </CardContent>
    </Card>
  )
}
