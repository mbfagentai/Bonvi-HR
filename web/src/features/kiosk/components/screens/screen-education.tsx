import { Eraser } from 'lucide-react'
import { useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Button } from '@/shared/ui'
import {
  LIMITS,
  MAX_EDUCATIONS,
  MAX_OTHER_LANGUAGES,
  emptyEducation,
  emptyLanguage,
  entryErrors,
  type DraftEducation,
  type DraftLanguage,
} from '../../lib/kiosk-draft'
import {
  COMPUTER_LEVELS,
  COMPUTER_SKILLS,
  EDUCATION_OPTIONS,
  FIXED_LANGUAGES,
  LANGUAGE_ASPECTS,
  lowerFirst,
} from '../../lib/kiosk-options'
import { COMPUTER_LIST, computerRows, useKioskList, visibleValues } from '../../lib/kiosk-texts'
import {
  ChoiceGroup,
  DigitsField,
  FieldMessage,
  KioskText,
  KioskTextArea,
  LevelPicker,
  ScreenHint,
  useFieldError,
} from '../kiosk-fields'
import { AddRowButton, PickerSheet, RowSheet, SummaryRow } from '../kiosk-rows'
import { removeAt, replaceAt, useRowEditor, type ScreenProps } from './screen-props'

const LANGUAGE_LEVELS = [0, 1, 2, 3, 4, 5] as const

/* Blank sections 2–4. */

/** Education level and the schools / universities table. */
export function EducationScreen(props: ScreenProps) {
  const { draft, patch, errors } = props
  const { t } = useLocale()
  const levels = useKioskList('education')
  const fieldError = useFieldError(errors)
  const editor = useRowEditor('educations', props, emptyEducation)
  const sheetError = useFieldError(editor.errors)

  const base = editor.base
  const row = editor.editing?.value

  return (
    <>
      <ChoiceGroup
        path="educationLevel"
        label={t('kiosk.field.educationLevel')}
        required
        value={draft.educationLevel}
        onChange={(educationLevel) => patch({ educationLevel })}
        error={fieldError('educationLevel')}
        options={visibleValues(levels, EDUCATION_OPTIONS, (value) => value === draft.educationLevel).map((value) => ({
          value,
          label: t(`kiosk.education.${lowerFirst(value)}`),
        }))}
      />

      <section className="space-y-2.5">
        <p className="text-body font-medium">{t('kiosk.field.educations')}</p>

        {editor.rows.map((entry, index) => (
          <SummaryRow
            key={index}
            path={`educations.${index}`}
            title={entry.institution.trim() || t('kiosk.row.untitled')}
            subtitle={educationLine(entry) || undefined}
            invalid={Object.keys(entryErrors(errors, `educations.${index}`)).length > 0}
            invalidText={t('kiosk.row.invalid')}
            onOpen={() => editor.edit(index)}
            onRemove={() => editor.remove(index)}
          />
        ))}

        {editor.rows.length < MAX_EDUCATIONS && (
          <AddRowButton onClick={editor.add}>{t('kiosk.addEducation')}</AddRowButton>
        )}
      </section>

      <RowSheet
        open={editor.isOpen}
        title={t('kiosk.sheet.education')}
        onCancel={editor.cancel}
        onSave={editor.save}
      >
        {row !== undefined && (
          <>
            <KioskText
              path={`${base}.institution`}
              label={t('kiosk.field.institution')}
              required
              value={row.institution}
              maxLength={LIMITS.short}
              onChange={(event) => editor.set({ institution: event.target.value })}
              error={sheetError(`${base}.institution`)}
            />
            <KioskText
              path={`${base}.faculty`}
              label={t('kiosk.field.faculty')}
              value={row.faculty}
              maxLength={LIMITS.short}
              onChange={(event) => editor.set({ faculty: event.target.value })}
              error={sheetError(`${base}.faculty`)}
            />
            <div className="grid grid-cols-2 gap-4">
              <DigitsField
                path={`${base}.startYear`}
                label={t('kiosk.field.startYear')}
                placeholder="2015"
                value={row.startYear}
                maxLength={4}
                onChange={(startYear) => editor.set({ startYear })}
                error={sheetError(`${base}.startYear`)}
              />
              <DigitsField
                path={`${base}.endYear`}
                label={t('kiosk.field.endYear')}
                placeholder="2019"
                value={row.endYear}
                maxLength={4}
                onChange={(endYear) => editor.set({ endYear })}
                error={sheetError(`${base}.endYear`)}
              />
            </div>
            <KioskText
              path={`${base}.diplomaNumber`}
              label={t('kiosk.field.diplomaNumber')}
              value={row.diplomaNumber}
              maxLength={LIMITS.short}
              onChange={(event) => editor.set({ diplomaNumber: event.target.value })}
              error={sheetError(`${base}.diplomaNumber`)}
            />
          </>
        )}
      </RowSheet>
    </>
  )
}

function educationLine(row: DraftEducation): string {
  const years = [row.startYear, row.endYear].filter(Boolean).join(' – ')
  return [row.faculty.trim(), years].filter(Boolean).join(' · ')
}

/** School, strong subjects, courses. */
export function SchoolScreen({ draft, patch, errors }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <KioskText
        path="schoolName"
        label={t('kiosk.field.schoolName')}
        value={draft.schoolName}
        maxLength={LIMITS.short}
        onChange={(event) => patch({ schoolName: event.target.value })}
        error={fieldError('schoolName')}
      />
      <KioskText
        path="strongSubjects"
        label={t('kiosk.field.strongSubjects')}
        value={draft.strongSubjects}
        maxLength={LIMITS.short}
        onChange={(event) => patch({ strongSubjects: event.target.value })}
        error={fieldError('strongSubjects')}
      />
      <KioskTextArea
        path="courses"
        label={t('kiosk.field.courses')}
        value={draft.courses}
        maxLength={LIMITS.long}
        onChange={(courses) => patch({ courses })}
        error={fieldError('courses')}
      />
    </>
  )
}

/**
 * Languages — one line per language, the three levels in a sheet. The four
 * fixed rows always exist (Russian is required); «Boshqa» rows are added on
 * demand and dropped again if left empty.
 */
export function LanguagesScreen({ draft, patch, errors }: ScreenProps) {
  const { t } = useLocale()
  const languageList = useKioskList('language')
  const fieldError = useFieldError(errors)
  // `shown` outlives `open` so the sheet keeps its content while it slides away.
  const [shown, setShown] = useState<number | null>(null)
  const [isOpen, setIsOpen] = useState(false)
  const show = (index: number) => {
    setShown(index)
    setIsOpen(true)
  }

  const languages = draft.languages

  /*
    The four fixed rows in HR's order (the PDF prints them the same way), the
    «Other» rows after them; the draft keeps its own order, only the display
    follows HR — every handler still works with the row's draft index.
  */
  const fixedOrder: readonly string[] = visibleValues(languageList, FIXED_LANGUAGES)
  const rank = (row: DraftLanguage) => {
    const at = fixedOrder.indexOf(row.language)
    return row.language === 'Other' || at < 0 ? fixedOrder.length : at
  }
  const ordered = languages.map((row, index) => ({ row, index })).sort((a, b) => rank(a.row) - rank(b.row))

  const otherCount = languages.filter((row) => row.language === 'Other').length
  const open = isOpen ? shown : null
  const current = shown === null ? undefined : languages[shown]

  const setLanguage = (index: number, changes: Partial<DraftLanguage>) =>
    patch({ languages: replaceAt(languages, index, changes) })

  const untouched = (row: DraftLanguage) =>
    row.speaking === null && row.reading === null && row.writing === null && row.otherName.trim() === ''

  const close = () => {
    // An «Other» row opened and left empty is not kept as a blank line.
    if (open !== null && current?.language === 'Other' && untouched(current)) {
      patch({ languages: removeAt(languages, open) })
      setShown(null)
    }
    setIsOpen(false)
  }

  const nameOf = (row: DraftLanguage) =>
    row.language === 'Other'
      ? row.otherName.trim() || t('kiosk.language.other')
      : t(`kiosk.language.${lowerFirst(row.language)}`)

  const levelsLine = (row: DraftLanguage) =>
    LANGUAGE_ASPECTS.map((aspect) => `${t(`kiosk.languages.${aspect}`)} ${row[aspect] ?? '—'}`).join(' · ')

  const base = shown === null ? '' : `languages.${shown}`
  const invalidLevels = shown !== null && fieldError(base) !== undefined

  return (
    <>
      <ScreenHint>{t('kiosk.languages.legend')}</ScreenHint>

      <div className="space-y-2.5">
        {ordered.map(({ row, index }) => {
          const blank = row.speaking === null && row.reading === null && row.writing === null
          const rowBase = `languages.${index}`

          return (
            <SummaryRow
              key={index}
              path={rowBase}
              title={
                <>
                  {nameOf(row)}
                  {row.language === 'Russian' && (
                    <span aria-hidden className="text-ios-red">
                      {' *'}
                    </span>
                  )}
                </>
              }
              subtitle={blank ? t('kiosk.row.notSet') : levelsLine(row)}
              placeholder={blank}
              invalid={Object.keys(entryErrors(errors, rowBase)).length > 0}
              invalidText={fieldError(rowBase) ?? fieldError(`${rowBase}.otherName`)}
              onOpen={() => show(index)}
              onRemove={row.language === 'Other' ? () => patch({ languages: removeAt(languages, index) }) : undefined}
            />
          )
        })}

        {otherCount < MAX_OTHER_LANGUAGES && (
          <AddRowButton
            onClick={() => {
              patch({ languages: [...languages, emptyLanguage('Other')] })
              show(languages.length)
            }}
          >
            {t('kiosk.languages.add')}
          </AddRowButton>
        )}
      </div>

      <PickerSheet
        open={isOpen && current !== undefined}
        title={current === undefined ? '' : nameOf(current)}
        description={t('kiosk.languages.scale')}
        onClose={close}
        secondary={
          current !== undefined && !untouched(current) ? (
            <Button
              type="button"
              variant="ghost"
              size="lg"
              onClick={() =>
                shown !== null && setLanguage(shown, { speaking: null, reading: null, writing: null, otherName: '' })
              }
              className="mr-auto"
            >
              <Eraser />
              {t('kiosk.row.clear')}
            </Button>
          ) : undefined
        }
      >
        {current !== undefined && shown !== null && (
          <>
            {current.language === 'Other' && (
              <KioskText
                path={`${base}.otherName`}
                label={t('kiosk.languages.otherName')}
                value={current.otherName}
                maxLength={LIMITS.name}
                onChange={(event) => setLanguage(shown, { otherName: event.target.value })}
                error={fieldError(`${base}.otherName`)}
              />
            )}

            {LANGUAGE_ASPECTS.map((aspect) => (
              <div key={aspect} className="space-y-2">
                <p className="text-body font-medium">{t(`kiosk.languages.${aspect}`)}</p>
                <LevelPicker
                  label={t(`kiosk.languages.${aspect}`)}
                  levels={LANGUAGE_LEVELS}
                  value={current[aspect]}
                  invalid={invalidLevels && current[aspect] === null}
                  onChange={(level) => {
                    const changes: Partial<DraftLanguage> = {}
                    changes[aspect] = level
                    setLanguage(shown, changes)
                  }}
                />
              </div>
            ))}

            <FieldMessage error={fieldError(base)} />
          </>
        )}
      </PickerSheet>
    </>
  )
}

/**
 * Computer skills — the eleven skills of the shortened table (SPEC §3.4) as
 * one inline list in three groups, a level picker on every line. Few enough
 * to scan on a phone without opening a sheet per group. A skill without a
 * level is «does not know»; tapping the chosen level again clears it.
 */
export function ComputerScreen({ draft, patch, errors }: ScreenProps) {
  const { t, locale } = useLocale()
  const fieldError = useFieldError(errors)
  const list = useKioskList(COMPUTER_LIST)

  // HR's order and HR-added skills; a hidden skill stays while it has a level
  const groups = computerRows(list, COMPUTER_SKILLS, t, locale, (key) => draft.computerSkills[key] !== undefined)

  const setSkill = (key: string, level: number | null) => {
    const next = { ...draft.computerSkills }
    if (level === null) delete next[key]
    else next[key] = level
    patch({ computerSkills: next })
  }

  return (
    <>
      <div className="space-y-1.5">
        <ScreenHint>{t('kiosk.computer.legend')}</ScreenHint>
        <p className="text-subhead font-medium text-balance text-[var(--fg-secondary)]">{t('kiosk.computer.scale')}</p>
      </div>

      {groups.filter((group) => group.skills.length > 0).map(({ id: group, skills }) => (
        <section key={group} className="space-y-2.5">
          <h2 className="text-body font-medium">{t(`kiosk.computer.group.${group}`)}</h2>

          <ul className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border-strong)] bg-[var(--bg-elevated)]">
            {skills.map(({ key, label }) => {
              // «Google Docs / Sheets» wraps after the slash, never before it.
              const shown = label.replaceAll(' / ', '\u00a0/ ')

              return (
                <li
                  key={key}
                  data-field={`computerSkills.${key}`}
                  className="flex min-h-14 items-center gap-2 py-1 pr-1 pl-3.5 sm:gap-4 sm:py-2 sm:pr-2 sm:pl-4"
                >
                  <span className="min-w-0 flex-1 text-body leading-snug">{shown}</span>
                  <div className="w-[188px] shrink-0 sm:w-64">
                    <LevelPicker
                      label={label}
                      levels={COMPUTER_LEVELS}
                      value={draft.computerSkills[key] ?? null}
                      onChange={(level) => setSkill(key, level)}
                    />
                  </div>
                </li>
              )
            })}
          </ul>
        </section>
      ))}

      <KioskText
        path="computerSkillsOther"
        label={t('kiosk.field.computerSkillsOther')}
        placeholder={t('kiosk.computer.otherPlaceholder')}
        value={draft.computerSkillsOther}
        maxLength={LIMITS.short}
        onChange={(event) => patch({ computerSkillsOther: event.target.value })}
        error={fieldError('computerSkillsOther')}
      />
    </>
  )
}
