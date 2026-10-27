import { useLocale } from '@/shared/i18n/use-locale'
import { MoneyInput, Select } from '@/shared/ui'
import { LIMITS } from '../../lib/kiosk-draft'
import { START_AFTER_OPTIONS, lowerFirst } from '../../lib/kiosk-options'
import { ChoiceGroup, KioskField, KioskText, KioskTextArea, Label, YesNo, useFieldError } from '../kiosk-fields'
import type { ScreenProps } from './screen-props'

/** `MoneyInput` keeps a decimal part; the form takes whole so'm. */
const wholeSom = (digits: string) => digits.split('.')[0] ?? ''

/**
 * Up to this many referral channels the question is a grid of tiles (two per
 * row — the names are short); more — a picker, so the screen stays one
 * phone-height (the vacancy screen's rule).
 */
const REFERRAL_TILES_MAX = 12

/* Blank sections 8–9. */

/**
 * Where the candidate heard of us — HR's «Qayerdan bilgan» catalog, sent by
 * the session (SPEC-qayerdan-bilgan §5) — and acquaintances at the company.
 */
export function ReferralScreen({ draft, patch, errors, session }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  const options = session.referralChannels.map((channel) => ({ value: channel.id, label: channel.name }))
  // A restored draft's channel that is no longer offered shows as not chosen
  const chosen = session.referralChannels.find((channel) => channel.id === draft.referralChannelId)
  const needsDetail = chosen?.asksDetail === true

  return (
    <>
      {options.length <= REFERRAL_TILES_MAX ? (
        <ChoiceGroup
          path="referralChannelId"
          label={t('kiosk.field.referralSource')}
          required
          columns={2}
          value={chosen === undefined ? null : chosen.id}
          onChange={(referralChannelId) => patch({ referralChannelId })}
          error={fieldError('referralChannelId')}
          options={options}
        />
      ) : (
        <KioskField path="referralChannelId">
          <Select
            label={<Label required>{t('kiosk.field.referralSource')}</Label>}
            placeholder={t('kiosk.field.referralPlaceholder')}
            options={options}
            value={chosen === undefined ? '' : chosen.id}
            onChange={(event) => patch({ referralChannelId: event.target.value })}
            error={fieldError('referralChannelId')}
          />
        </KioskField>
      )}
      {needsDetail && (
        <KioskText
          path="referralDetail"
          label={t('kiosk.field.referralDetail')}
          placeholder={t('kiosk.field.referralDetailPlaceholder')}
          required
          value={draft.referralDetail}
          maxLength={LIMITS.short}
          autoComplete="off"
          onChange={(event) => patch({ referralDetail: event.target.value })}
          error={fieldError('referralDetail')}
        />
      )}

      <Conditional
        path="hasAcquaintances"
        label={t('kiosk.field.hasAcquaintances')}
        required
        value={draft.hasAcquaintances}
        onChange={(hasAcquaintances) => patch({ hasAcquaintances })}
        detailPath="acquaintancesDetail"
        detailLabel={t('kiosk.field.acquaintancesDetail')}
        detailRequired
        detail={draft.acquaintancesDetail}
        onDetail={(acquaintancesDetail) => patch({ acquaintancesDetail })}
        fieldError={fieldError}
      />
    </>
  )
}

/** Salary, when the candidate can start (counted from the hire day) and loans. */
export function SalaryScreen({ draft, patch, errors }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <KioskField path="lastSalary">
        <MoneyInput
          size="lg"
          label={t('kiosk.field.lastSalary')}
          value={draft.lastSalary}
          onChange={(digits) => patch({ lastSalary: wholeSom(digits) })}
          error={fieldError('lastSalary')}
        />
      </KioskField>
      <KioskField path="expectedSalary">
        <MoneyInput
          size="lg"
          label={<Label required>{t('kiosk.field.expectedSalary')}</Label>}
          value={draft.expectedSalary}
          onChange={(digits) => patch({ expectedSalary: wholeSom(digits) })}
          error={fieldError('expectedSalary')}
        />
      </KioskField>
      <ChoiceGroup
        path="startAfter"
        label={t('kiosk.field.startAfter')}
        required
        value={draft.startAfter}
        onChange={(startAfter) => patch({ startAfter })}
        error={fieldError('startAfter')}
        options={START_AFTER_OPTIONS.map((value) => ({ value, label: t(`kiosk.startAfter.${lowerFirst(value)}`) }))}
      />

      <YesNo
        path="hasLoan"
        label={t('kiosk.field.hasLoan')}
        required
        value={draft.hasLoan}
        onChange={(hasLoan) => patch({ hasLoan })}
        error={fieldError('hasLoan')}
      />
      {draft.hasLoan === true && (
        <KioskField path="loanMonthlyPayment">
          <MoneyInput
            size="lg"
            label={<Label required>{t('kiosk.field.loanMonthlyPayment')}</Label>}
            value={draft.loanMonthlyPayment}
            onChange={(digits) => patch({ loanMonthlyPayment: wholeSom(digits) })}
            error={fieldError('loanMonthlyPayment')}
          />
        </KioskField>
      )}
    </>
  )
}

/** Shifts, overtime, travel, trial period. */
export function ConditionsScreen({ draft, patch, errors }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <YesNo
        path="canWorkShifts"
        label={t('kiosk.field.canWorkShifts')}
        required
        value={draft.canWorkShifts}
        onChange={(canWorkShifts) => patch({ canWorkShifts })}
        error={fieldError('canWorkShifts')}
      />
      <YesNo
        path="canWorkOvertime"
        label={t('kiosk.field.canWorkOvertime')}
        required
        value={draft.canWorkOvertime}
        onChange={(canWorkOvertime) => patch({ canWorkOvertime })}
        error={fieldError('canWorkOvertime')}
      />
      <YesNo
        path="canTravel"
        label={t('kiosk.field.canTravel')}
        required
        value={draft.canTravel}
        onChange={(canTravel) => patch({ canTravel })}
        error={fieldError('canTravel')}
      />
      <YesNo
        path="agreesToTrial"
        label={t('kiosk.field.agreesToTrial')}
        required
        value={draft.agreesToTrial}
        onChange={(agreesToTrial) => patch({ agreesToTrial })}
        error={fieldError('agreesToTrial')}
      />
    </>
  )
}

/** Other job, inviting parents, conditions to start. */
export function TermsScreen({ draft, patch, errors }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <Conditional
        path="hasOtherJob"
        label={t('kiosk.field.hasOtherJob')}
        required
        value={draft.hasOtherJob}
        onChange={(hasOtherJob) => patch({ hasOtherJob })}
        detailPath="otherJobDetail"
        detailLabel={t('kiosk.field.otherJobDetail')}
        detailRequired
        detail={draft.otherJobDetail}
        onDetail={(otherJobDetail) => patch({ otherJobDetail })}
        fieldError={fieldError}
      />

      <YesNo
        path="agreesParentsInvite"
        label={t('kiosk.field.agreesParentsInvite')}
        value={draft.agreesParentsInvite}
        onChange={(agreesParentsInvite) => patch({ agreesParentsInvite })}
        error={fieldError('agreesParentsInvite')}
      />

      <KioskTextArea
        path="startConditions"
        label={t('kiosk.field.startConditions')}
        value={draft.startConditions}
        maxLength={LIMITS.long}
        onChange={(startConditions) => patch({ startConditions })}
        error={fieldError('startConditions')}
      />
    </>
  )
}

/** Health restrictions and a patient in the family. */
export function HealthScreen({ draft, patch, errors }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <Conditional
        path="hasHealthRestriction"
        label={t('kiosk.field.hasHealthRestriction')}
        required
        hint={t('kiosk.field.healthHint')}
        value={draft.hasHealthRestriction}
        onChange={(hasHealthRestriction) => patch({ hasHealthRestriction })}
        detailPath="healthRestrictionDetail"
        detailLabel={t('kiosk.field.healthRestrictionDetail')}
        detailRequired
        detail={draft.healthRestrictionDetail}
        onDetail={(healthRestrictionDetail) => patch({ healthRestrictionDetail })}
        fieldError={fieldError}
      />

      <YesNo
        path="hasFamilyPatient"
        label={t('kiosk.field.hasFamilyPatient')}
        value={draft.hasFamilyPatient}
        onChange={(hasFamilyPatient) => patch({ hasFamilyPatient })}
        error={fieldError('hasFamilyPatient')}
      />
    </>
  )
}

/** Convictions and trips abroad. */
export function BackgroundScreen({ draft, patch, errors }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <Conditional
        path="hasConviction"
        label={t('kiosk.field.hasConviction')}
        required
        value={draft.hasConviction}
        onChange={(hasConviction) => patch({ hasConviction })}
        detailPath="convictionDetail"
        detailLabel={t('kiosk.field.convictionDetail')}
        detailRequired
        detail={draft.convictionDetail}
        onDetail={(convictionDetail) => patch({ convictionDetail })}
        fieldError={fieldError}
      />

      <Conditional
        path="relativesConviction"
        label={t('kiosk.field.relativesConviction')}
        value={draft.relativesConviction}
        onChange={(relativesConviction) => patch({ relativesConviction })}
        detailPath="relativesConvictionDetail"
        detailLabel={t('kiosk.field.relativesConvictionDetail')}
        detail={draft.relativesConvictionDetail}
        onDetail={(relativesConvictionDetail) => patch({ relativesConvictionDetail })}
        fieldError={fieldError}
      />

      <KioskTextArea
        path="abroadTrips"
        label={t('kiosk.field.abroadTrips')}
        value={draft.abroadTrips}
        maxLength={LIMITS.long}
        onChange={(abroadTrips) => patch({ abroadTrips })}
        error={fieldError('abroadTrips')}
      />
    </>
  )
}

/** Yes/no with a detail field that appears on «Ha» (the paper's «☐ Ha: ______»). */
function Conditional({
  path,
  label,
  required = false,
  hint,
  value,
  onChange,
  detailPath,
  detailLabel,
  detailRequired = false,
  detail,
  onDetail,
  fieldError,
}: {
  path: string
  label: string
  required?: boolean
  hint?: string
  value: boolean | null
  onChange: (value: boolean) => void
  detailPath: string
  detailLabel: string
  detailRequired?: boolean
  detail: string
  onDetail: (value: string) => void
  fieldError: (path: string) => string | undefined
}) {
  return (
    <div className="space-y-4">
      <YesNo
        path={path}
        label={label}
        required={required}
        hint={hint}
        value={value}
        onChange={onChange}
        error={fieldError(path)}
      />
      {value === true && (
        <KioskText
          path={detailPath}
          label={detailLabel}
          required={detailRequired}
          value={detail}
          maxLength={LIMITS.short}
          onChange={(event) => onDetail(event.target.value)}
          error={fieldError(detailPath)}
        />
      )}
    </div>
  )
}
