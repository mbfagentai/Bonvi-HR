import { useCallback, useMemo } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { isoToDisplay } from '@/shared/lib/date-value'
import { Alert } from '@/shared/ui'

import { validateAnswers, type FieldError, type FieldErrors } from '../../lib/answers-zod'
import { labelForPath } from '../../lib/blank-presentation'
import { evaluateComputed } from '../../lib/computed'
import { computeVisibility, dropHidden } from '../../lib/conditions'
import type {
  Answers,
  BlankAttachmentDto,
  BlankDefinition,
  ComputedValues,
  JsonValue,
} from '../../lib/definition-types'
import {
  RendererContext,
  type DocRefSearch,
  type RefSearch,
  type RemoveFile,
  type RendererContextValue,
  type UploadFile,
} from './renderer-context'
import { SectionView } from './section-view'

/** A problem+json error of the blanks API: `code` (`blanka.*`) + `detail` = field path. */
export interface BlankServerError {
  code: string
  detail?: string | null
}

export interface BlankFormProps {
  definition: BlankDefinition
  answers: Answers
  /** Every edit — the page debounces it into `PUT /answers` (autosave). */
  onChange: (answers: Answers) => void
  /** `detail.can.editableSections` — the other sections print read-only. */
  editableSections: readonly string[]
  /** Force everything read-only (view page, ended statuses). */
  readOnly?: boolean
  /** `detail.computed` — overlaid by the live client evaluation. */
  computed?: ComputedValues
  /** Extra errors keyed by path, e.g. the page's submit-mode validation. */
  errors?: FieldErrors
  /** The last server error; its `detail` path is shown inline and summarised on top. */
  serverError?: BlankServerError | null
  /** `detail.attachments`. */
  attachments?: readonly BlankAttachmentDto[]
  searchRef?: RefSearch
  searchDocRef?: DocRefSearch
  uploadFile?: UploadFile
  removeFile?: RemoveFile
  openFile?: (attachment: BlankAttachmentDto) => void
  /** `editable` renders only editable sections (step dialog); default `all`. */
  show?: 'all' | 'editable'
}

const EMPTY_ATTACHMENTS: readonly BlankAttachmentDto[] = []
const EMPTY_ERRORS: FieldErrors = {}

/**
 * Generic fill renderer for any blank definition (SPEC §4, §5, §8).
 *
 * Props-driven: it never fetches or saves. It renders visible sections
 * (`when`), edits only `editableSections`, evaluates `computed` live, shows
 * type/limit errors as the user types (save mode) plus whatever the page
 * passes in `errors` (submit mode) and the server's `detail` path.
 */
export function BlankForm({
  definition,
  answers,
  onChange,
  editableSections,
  readOnly = false,
  computed: serverComputed,
  errors: externalErrors = EMPTY_ERRORS,
  serverError = null,
  attachments = EMPTY_ATTACHMENTS,
  searchRef,
  searchDocRef,
  uploadFile,
  removeFile,
  openFile,
  show = 'all',
}: BlankFormProps) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const editable = useMemo(() => new Set(readOnly ? [] : editableSections), [readOnly, editableSections])

  const visibility = useMemo(() => computeVisibility(definition, answers, serverComputed), [definition, answers, serverComputed])

  const computed = useMemo<ComputedValues>(
    () => ({ ...serverComputed, ...evaluateComputed(definition, dropHidden(answers, visibility)) }),
    [definition, answers, visibility, serverComputed],
  )

  const liveErrors = useMemo(
    () =>
      editable.size === 0
        ? EMPTY_ERRORS
        : validateAnswers(definition, answers, { requireFilled: false, sections: [...editable] }),
    [definition, answers, editable],
  )

  const serverText = serverError ? errorMessage(serverError.code) : null
  const serverPath = serverError?.detail ?? null

  const errors = useMemo<FieldErrors>(() => {
    const merged: FieldErrors = { ...liveErrors, ...externalErrors }
    if (serverText && serverPath) merged[serverPath] = { key: 'server', message: serverText }
    return merged
  }, [liveErrors, externalErrors, serverText, serverPath])

  const errorText = useCallback(
    (error: FieldError | undefined): string | undefined => {
      if (!error) return undefined
      if (error.message) return error.message
      const params: Record<string, string | number> = {}
      for (const [key, value] of Object.entries(error.params ?? {}))
        params[key] = typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value) ? isoToDisplay(value) : value
      return t(`blanks.form.errors.${error.key}`, params)
    },
    [t],
  )

  const context = useMemo<RendererContextValue>(
    () => ({
      answers,
      computed,
      errors,
      attachments,
      searchRef,
      searchDocRef,
      uploadFile,
      removeFile,
      openFile,
      errorText,
    }),
    [answers, computed, errors, attachments, searchRef, searchDocRef, uploadFile, removeFile, openFile, errorText],
  )

  const stepTitles = useMemo(
    () => new Map((definition.steps ?? []).map((s) => [s.id, s.title] as const)),
    [definition.steps],
  )

  const setField = (fieldId: string, value: JsonValue) => {
    const next: Answers = { ...answers }
    if (value === null) delete next[fieldId]
    else next[fieldId] = value
    onChange(next)
  }

  const sections = definition.sections.filter(
    (s) => !visibility.hiddenSections.has(s.id) && (show === 'all' || editable.has(s.id)),
  )
  const serverLabel = serverPath ? labelForPath(definition, serverPath) : null

  return (
    <RendererContext.Provider value={context}>
      <div className="space-y-4">
        {serverText && (
          <Alert tone="danger">
            {serverLabel ? t('blanks.form.serverErrorAt', { field: serverLabel, message: serverText }) : serverText}
          </Alert>
        )}
        {sections.length === 0 && (
          <p className="py-6 text-center text-subhead text-[var(--fg-tertiary)]">{t('blanks.form.noSections')}</p>
        )}
        {sections.map((section) => (
          <SectionView
            key={section.id}
            section={section}
            answers={answers}
            editable={editable.has(section.id)}
            hiddenFields={visibility.hiddenFields}
            stepTitle={section.fill?.at === 'step' ? stepTitles.get(section.fill.step) : undefined}
            onFieldChange={setField}
          />
        ))}
      </div>
    </RendererContext.Provider>
  )
}
