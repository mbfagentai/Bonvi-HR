import { useLocale } from '@/shared/i18n/use-locale'
import { formatUzPhone } from '@/shared/lib/phone'
import { PhoneInput, Select } from '@/shared/ui'
import type { FamilyRelation } from '../../api/kiosk-api'
import {
  LIMITS,
  MAX_FAMILY,
  MAX_JOBS,
  MAX_REFERENCES,
  emptyFamilyMember,
  emptyJob,
  emptyReference,
  entryErrors,
  type DraftJob,
} from '../../lib/kiosk-draft'
import { FAMILY_RELATIONS, lowerFirst } from '../../lib/kiosk-options'
import {
  CheckRow,
  DigitsField,
  FieldMessage,
  KioskField,
  KioskText,
  Label,
  MonthField,
  ScreenHint,
  useFieldError,
} from '../kiosk-fields'
import { AddRowButton, RowSheet, SummaryRow } from '../kiosk-rows'
import { useRowEditor, type ScreenProps } from './screen-props'

/* Blank sections 5–7: each table is a list of lines, an entry is edited in a sheet. */

const phoneText = (digits: string) => (digits === '' ? '' : `+998 ${formatUzPhone(digits)}`)

const joined = (...parts: string[]) =>
  parts
    .map((part) => part.trim())
    .filter(Boolean)
    .join(' · ')

/** Work history, newest first, and the total experience. */
export function WorkScreen(props: ScreenProps) {
  const { draft, patch, errors } = props
  const { t } = useLocale()
  const fieldError = useFieldError(errors)
  const editor = useRowEditor('jobs', props, emptyJob)
  const sheetError = useFieldError(editor.errors)
  const { base } = editor
  const row = editor.editing?.value

  const period = (job: DraftJob) => {
    const to = job.current ? t('kiosk.row.present') : job.to.trim()
    return job.from.trim() === '' && to === '' ? '' : `${job.from.trim() || '…'} – ${to || '…'}`
  }

  return (
    <>
      <ScreenHint>{t('kiosk.work.hint')}</ScreenHint>

      <div className="space-y-2.5">
        {editor.rows.map((job, index) => (
          <SummaryRow
            key={index}
            path={`jobs.${index}`}
            title={
              [job.organization.trim(), job.position.trim()].filter(Boolean).join(' — ') || t('kiosk.row.untitled')
            }
            subtitle={joined(period(job), job.leaveReason) || undefined}
            invalid={Object.keys(entryErrors(errors, `jobs.${index}`)).length > 0}
            invalidText={t('kiosk.row.invalid')}
            onOpen={() => editor.edit(index)}
            onRemove={() => editor.remove(index)}
          />
        ))}

        {editor.rows.length < MAX_JOBS && <AddRowButton onClick={editor.add}>{t('kiosk.addJob')}</AddRowButton>}
      </div>

      <div className="space-y-2.5">
        <p className="text-body font-medium">{t('kiosk.field.totalExperience')}</p>
        <div className="grid grid-cols-2 gap-4 sm:max-w-md">
          <DigitsField
            path="totalExperienceYears"
            aria-label={t('kiosk.field.years')}
            trailing={<span className="text-callout text-[var(--fg-tertiary)]">{t('kiosk.field.years')}</span>}
            value={draft.totalExperienceYears}
            maxLength={2}
            onChange={(totalExperienceYears) => patch({ totalExperienceYears })}
            error={fieldError('totalExperienceYears')}
          />
          <DigitsField
            path="totalExperienceMonths"
            aria-label={t('kiosk.field.months')}
            trailing={<span className="text-callout text-[var(--fg-tertiary)]">{t('kiosk.field.months')}</span>}
            value={draft.totalExperienceMonths}
            maxLength={2}
            onChange={(totalExperienceMonths) => patch({ totalExperienceMonths })}
            error={fieldError('totalExperienceMonths')}
          />
        </div>
      </div>

      <RowSheet open={editor.isOpen} title={t('kiosk.sheet.job')} onCancel={editor.cancel} onSave={editor.save}>
        {row !== undefined && (
          <>
            <KioskText
              path={`${base}.organization`}
              label={t('kiosk.field.organization')}
              required
              value={row.organization}
              maxLength={LIMITS.short}
              onChange={(event) => editor.set({ organization: event.target.value })}
              error={sheetError(`${base}.organization`)}
            />
            <KioskText
              path={`${base}.position`}
              label={t('kiosk.field.position')}
              value={row.position}
              maxLength={LIMITS.short}
              onChange={(event) => editor.set({ position: event.target.value })}
              error={sheetError(`${base}.position`)}
            />
            <div className="grid grid-cols-2 gap-4">
              <MonthField
                path={`${base}.from`}
                label={t('kiosk.field.jobFrom')}
                value={row.from}
                onChange={(from) => editor.set({ from })}
                error={sheetError(`${base}.from`)}
              />
              {!row.current && (
                <MonthField
                  path={`${base}.to`}
                  label={t('kiosk.field.jobTo')}
                  value={row.to}
                  onChange={(to) => editor.set({ to })}
                  error={sheetError(`${base}.to`)}
                />
              )}
            </div>
            <CheckRow
              checked={row.current}
              onChange={(current) => editor.set({ current, to: current ? '' : row.to })}
              label={t('kiosk.field.jobCurrent')}
            />
            <KioskText
              path={`${base}.leaveReason`}
              label={t('kiosk.field.leaveReason')}
              value={row.leaveReason}
              maxLength={LIMITS.short}
              onChange={(event) => editor.set({ leaveReason: event.target.value })}
              error={sheetError(`${base}.leaveReason`)}
            />
          </>
        )}
      </RowSheet>
    </>
  )
}

/** People who know the candidate's work. */
export function ReferencesScreen(props: ScreenProps) {
  const { errors } = props
  const { t } = useLocale()
  const editor = useRowEditor('references', props, emptyReference)
  const sheetError = useFieldError(editor.errors)
  const { base } = editor
  const row = editor.editing?.value

  return (
    <>
      <ScreenHint>{t('kiosk.references.hint')}</ScreenHint>

      <div className="space-y-2.5">
        {editor.rows.map((person, index) => (
          <SummaryRow
            key={index}
            path={`references.${index}`}
            title={person.fullName.trim() || t('kiosk.row.untitled')}
            subtitle={joined(person.organizationPosition, phoneText(person.phone)) || undefined}
            invalid={Object.keys(entryErrors(errors, `references.${index}`)).length > 0}
            invalidText={t('kiosk.row.invalid')}
            onOpen={() => editor.edit(index)}
            onRemove={() => editor.remove(index)}
          />
        ))}

        {editor.rows.length < MAX_REFERENCES && (
          <AddRowButton onClick={editor.add}>{t('kiosk.addReference')}</AddRowButton>
        )}
      </div>

      <RowSheet open={editor.isOpen} title={t('kiosk.sheet.reference')} onCancel={editor.cancel} onSave={editor.save}>
        {row !== undefined && (
          <>
            <KioskText
              path={`${base}.fullName`}
              label={t('kiosk.field.fullName')}
              required
              value={row.fullName}
              maxLength={LIMITS.short}
              onChange={(event) => editor.set({ fullName: event.target.value })}
              error={sheetError(`${base}.fullName`)}
            />
            <KioskText
              path={`${base}.organizationPosition`}
              label={t('kiosk.field.organizationPosition')}
              value={row.organizationPosition}
              maxLength={LIMITS.short}
              onChange={(event) => editor.set({ organizationPosition: event.target.value })}
              error={sheetError(`${base}.organizationPosition`)}
            />
            <KioskField path={`${base}.phone`}>
              <PhoneInput
                size="lg"
                label={t('kiosk.field.contactPhone')}
                value={row.phone}
                onChange={(phone) => editor.set({ phone })}
                error={sheetError(`${base}.phone`)}
              />
            </KioskField>
          </>
        )}
      </RowSheet>
    </>
  )
}

/** Family members — at least one (SPEC §3.7). */
export function FamilyScreen(props: ScreenProps) {
  const { errors } = props
  const { t } = useLocale()
  const fieldError = useFieldError(errors)
  const editor = useRowEditor('familyMembers', props, emptyFamilyMember)
  const sheetError = useFieldError(editor.errors)
  const { base } = editor
  const row = editor.editing?.value

  const relationLabel = (relation: FamilyRelation | '') =>
    relation === '' ? '' : t(`kiosk.relation.${lowerFirst(relation)}`)

  return (
    <>
      <ScreenHint>{t('kiosk.family.hint')}</ScreenHint>

      <div className="space-y-2.5">
        {editor.rows.map((member, index) => (
          <SummaryRow
            key={index}
            path={`familyMembers.${index}`}
            title={member.fullName.trim() || t('kiosk.row.untitled')}
            subtitle={joined(relationLabel(member.relation), member.birthYear, member.workplace) || undefined}
            invalid={Object.keys(entryErrors(errors, `familyMembers.${index}`)).length > 0}
            invalidText={t('kiosk.row.invalid')}
            onOpen={() => editor.edit(index)}
            onRemove={() => editor.remove(index)}
          />
        ))}

        {editor.rows.length < MAX_FAMILY && (
          <AddRowButton onClick={editor.add}>{t('kiosk.addFamilyMember')}</AddRowButton>
        )}

        {/* After the lines, so «scroll to error» reaches an entry's error before this one. */}
        <div data-field="familyMembers">
          <FieldMessage error={fieldError('familyMembers')} />
        </div>
      </div>

      <RowSheet open={editor.isOpen} title={t('kiosk.sheet.family')} onCancel={editor.cancel} onSave={editor.save}>
        {row !== undefined && (
          <>
            <KioskField path={`${base}.relation`}>
              <Select
                label={<Label required>{t('kiosk.field.relation')}</Label>}
                placeholder={t('kiosk.choose')}
                options={FAMILY_RELATIONS.map((value) => ({ value, label: relationLabel(value) }))}
                value={row.relation}
                onChange={(event) => editor.set({ relation: event.target.value as FamilyRelation | '' })}
                error={sheetError(`${base}.relation`)}
              />
            </KioskField>
            <KioskText
              path={`${base}.fullName`}
              label={t('kiosk.field.fullName')}
              required
              value={row.fullName}
              maxLength={LIMITS.short}
              onChange={(event) => editor.set({ fullName: event.target.value })}
              error={sheetError(`${base}.fullName`)}
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <DigitsField
                path={`${base}.birthYear`}
                label={t('kiosk.field.birthYear')}
                placeholder="1975"
                value={row.birthYear}
                maxLength={4}
                onChange={(birthYear) => editor.set({ birthYear })}
                error={sheetError(`${base}.birthYear`)}
              />
              <KioskField path={`${base}.phone`}>
                <PhoneInput
                  size="lg"
                  label={t('kiosk.field.contactPhone')}
                  value={row.phone}
                  onChange={(phone) => editor.set({ phone })}
                  error={sheetError(`${base}.phone`)}
                />
              </KioskField>
            </div>
            <KioskText
              path={`${base}.workplace`}
              label={t('kiosk.field.workplace')}
              value={row.workplace}
              maxLength={LIMITS.short}
              onChange={(event) => editor.set({ workplace: event.target.value })}
              error={sheetError(`${base}.workplace`)}
            />
            <KioskText
              path={`${base}.address`}
              label={t('kiosk.field.address')}
              value={row.address}
              maxLength={LIMITS.address}
              onChange={(event) => editor.set({ address: event.target.value })}
              error={sheetError(`${base}.address`)}
            />
          </>
        )}
      </RowSheet>
    </>
  )
}
