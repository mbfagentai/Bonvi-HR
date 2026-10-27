import { useLocale } from '@/shared/i18n/use-locale'
import { DateField, PhoneInput, Select } from '@/shared/ui'
import { LIMITS } from '../../lib/kiosk-draft'
import {
  DRIVER_LICENSE_CATEGORIES,
  HOUSING_TYPES,
  MARITAL_OPTIONS,
  MILITARY_OPTIONS,
  lowerFirst,
} from '../../lib/kiosk-options'
import {
  CheckRow,
  ChoiceGroup,
  DigitsField,
  FieldMessage,
  KioskField,
  KioskText,
  Label,
  MultiChoice,
  ScreenHint,
  YesNo,
  useFieldError,
} from '../kiosk-fields'
import { PhotoCapture } from '../photo-capture'
import { autofill, type ScreenProps } from './screen-props'

/** Up to this many choices the vacancy is a list of tiles; more — a picker. */
const VACANCY_TILES_MAX = 6

/* Blank sections 0–1, one topic per screen. */

/** Vacancy and the 3 × 4 photo. */
export function VacancyScreen({
  draft,
  patch,
  errors,
  session,
  photo,
  onPhoto,
}: ScreenProps & { photo: Blob | null; onPhoto: (photo: Blob | null) => void }) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  const options = [
    ...(session.generalPoolAvailable ? [{ value: 'general', label: t('kiosk.vacancy.general') }] : []),
    ...session.vacancies.map((vacancy) => ({ value: vacancy.id, label: vacancy.title })),
  ]

  return (
    <>
      {/* «Umumiy» is offered only while the general pool accepts forms. */}
      {session.generalPoolAvailable && <ScreenHint>{t('kiosk.vacancy.hint')}</ScreenHint>}

      {options.length <= VACANCY_TILES_MAX ? (
        <ChoiceGroup
          path="vacancy"
          label={t('kiosk.vacancy.label')}
          required
          columns={1}
          value={draft.vacancy === '' ? null : draft.vacancy}
          onChange={(vacancy) => patch({ vacancy })}
          error={fieldError('vacancy')}
          options={options}
        />
      ) : (
        <KioskField path="vacancy">
          <Select
            label={<Label required>{t('kiosk.vacancy.label')}</Label>}
            placeholder={t('kiosk.vacancy.placeholder')}
            options={options}
            value={draft.vacancy}
            onChange={(event) => patch({ vacancy: event.target.value })}
            error={fieldError('vacancy')}
          />
        </KioskField>
      )}

      <PhotoCapture photo={photo} onChange={onPhoto} error={fieldError('photo')} />
    </>
  )
}

export function PersonalScreen({ draft, patch, errors, today, tablet }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <div className="grid gap-5 sm:grid-cols-2">
      <KioskText
        path="lastName"
        label={t('kiosk.field.lastName')}
        required
        value={draft.lastName}
        maxLength={LIMITS.name}
        autoComplete={autofill(tablet, 'family-name')}
        onChange={(event) => patch({ lastName: event.target.value })}
        error={fieldError('lastName')}
      />
      <KioskText
        path="firstName"
        label={t('kiosk.field.firstName')}
        required
        value={draft.firstName}
        maxLength={LIMITS.name}
        autoComplete={autofill(tablet, 'given-name')}
        onChange={(event) => patch({ firstName: event.target.value })}
        error={fieldError('firstName')}
      />
      <KioskText
        path="middleName"
        label={t('kiosk.field.middleName')}
        value={draft.middleName}
        maxLength={LIMITS.name}
        autoComplete={autofill(tablet, 'additional-name')}
        onChange={(event) => patch({ middleName: event.target.value })}
        error={fieldError('middleName')}
      />
      <KioskField path="birthDate">
        <DateField
          size="lg"
          label={<Label required>{t('kiosk.field.birthDate')}</Label>}
          value={draft.birthDate}
          max={today}
          onChange={(value) => patch({ birthDate: value })}
          error={fieldError('birthDate')}
        />
      </KioskField>
    </div>
  )
}

export function AddressScreen({ draft, patch, errors, tablet }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <KioskText
        path="registeredAddress"
        label={t('kiosk.field.registeredAddress')}
        required
        value={draft.registeredAddress}
        maxLength={LIMITS.address}
        autoComplete={autofill(tablet, 'street-address')}
        onChange={(event) => patch({ registeredAddress: event.target.value })}
        error={fieldError('registeredAddress')}
      />

      <div className="space-y-3">
        <CheckRow
          checked={draft.sameAddress}
          onChange={(sameAddress) => patch({ sameAddress })}
          label={t('kiosk.field.sameAddress')}
        />
        {draft.sameAddress ? (
          <FieldMessage error={fieldError('actualAddress')} />
        ) : (
          <KioskText
            path="actualAddress"
            label={t('kiosk.field.actualAddress')}
            required
            value={draft.actualAddress}
            maxLength={LIMITS.address}
            onChange={(event) => patch({ actualAddress: event.target.value })}
            error={fieldError('actualAddress')}
          />
        )}
      </div>

      <ChoiceGroup
        path="housingType"
        label={t('kiosk.field.housingType')}
        columns={2}
        value={draft.housingType}
        onChange={(housingType) => patch({ housingType })}
        error={fieldError('housingType')}
        options={HOUSING_TYPES.map((value) => ({ value, label: t(`kiosk.housing.${lowerFirst(value)}`) }))}
      />

      <KioskText
        path="mahalla"
        label={t('kiosk.field.mahalla')}
        value={draft.mahalla}
        maxLength={LIMITS.short}
        onChange={(event) => patch({ mahalla: event.target.value })}
        error={fieldError('mahalla')}
      />
    </>
  )
}

export function ContactsScreen({ draft, patch, errors, tablet }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <KioskField path="phone">
        <PhoneInput
          size="lg"
          label={<Label required>{t('kiosk.field.phone')}</Label>}
          value={draft.phone}
          onChange={(phone) => patch({ phone })}
          error={fieldError('phone')}
        />
      </KioskField>
      <KioskField path="phoneExtra">
        <PhoneInput
          size="lg"
          label={t('kiosk.field.phoneExtra')}
          value={draft.phoneExtra}
          onChange={(phoneExtra) => patch({ phoneExtra })}
          error={fieldError('phoneExtra')}
        />
      </KioskField>
      <KioskText
        path="telegram"
        label={t('kiosk.field.telegram')}
        leading="@"
        placeholder="username"
        value={draft.telegram}
        maxLength={33}
        autoCapitalize="none"
        autoComplete="off"
        onChange={(event) => patch({ telegram: event.target.value.replace(/^@/u, '') })}
        error={fieldError('telegram')}
      />
      <KioskText
        path="email"
        type="email"
        inputMode="email"
        autoCapitalize="none"
        autoComplete={autofill(tablet, 'email')}
        label={t('kiosk.field.email')}
        value={draft.email}
        maxLength={LIMITS.short}
        onChange={(event) => patch({ email: event.target.value })}
        error={fieldError('email')}
      />
    </>
  )
}

/**
 * Marital status, children and military service. Military service is optional
 * for everyone: gender is not asked, so the label keeps the paper's «(erkaklar
 * uchun)» and the candidate decides (SPEC §3.1, 2026-10-26).
 */
export function MaritalScreen({ draft, patch, errors }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <ChoiceGroup
        path="maritalStatus"
        label={t('kiosk.field.maritalStatus')}
        required
        columns={2}
        value={draft.maritalStatus}
        onChange={(maritalStatus) => patch({ maritalStatus })}
        error={fieldError('maritalStatus')}
        options={MARITAL_OPTIONS.map((value) => ({ value, label: t(`kiosk.marital.${lowerFirst(value)}`) }))}
      />

      <DigitsField
        path="childrenCount"
        label={t('kiosk.field.childrenCount')}
        value={draft.childrenCount}
        maxLength={2}
        onChange={(childrenCount) => patch({ childrenCount })}
        error={fieldError('childrenCount')}
        className="sm:max-w-xs"
      />

      <ChoiceGroup
        path="militaryService"
        label={t('kiosk.field.militaryService')}
        columns={2}
        value={draft.militaryService}
        onChange={(militaryService) => patch({ militaryService })}
        error={fieldError('militaryService')}
        options={MILITARY_OPTIONS.map((value) => ({ value, label: t(`kiosk.military.${lowerFirst(value)}`) }))}
      />
      {draft.militaryService === 'Served' && (
        <DigitsField
          path="militaryServiceMonths"
          label={t('kiosk.field.militaryServiceMonths')}
          value={draft.militaryServiceMonths}
          maxLength={2}
          onChange={(militaryServiceMonths) => patch({ militaryServiceMonths })}
          error={fieldError('militaryServiceMonths')}
          className="sm:max-w-xs"
        />
      )}
    </>
  )
}

/** Driver licence and car. */
export function LicenseScreen({ draft, patch, errors }: ScreenProps) {
  const { t } = useLocale()
  const fieldError = useFieldError(errors)

  return (
    <>
      <YesNo
        path="hasDriverLicense"
        label={t('kiosk.field.hasDriverLicense')}
        required
        value={draft.hasDriverLicense}
        onChange={(hasDriverLicense) => patch({ hasDriverLicense })}
        error={fieldError('hasDriverLicense')}
      />
      {draft.hasDriverLicense === true && (
        <MultiChoice
          path="driverLicenseCategories"
          label={t('kiosk.field.driverLicenseCategories')}
          required
          options={DRIVER_LICENSE_CATEGORIES}
          value={draft.driverLicenseCategories}
          onChange={(driverLicenseCategories) => patch({ driverLicenseCategories })}
          error={fieldError('driverLicenseCategories')}
        />
      )}

      <YesNo
        path="hasCar"
        label={t('kiosk.field.hasCar')}
        value={draft.hasCar}
        onChange={(hasCar) => patch({ hasCar })}
        error={fieldError('hasCar')}
      />
      {draft.hasCar === true && (
        <KioskText
          path="carModel"
          label={t('kiosk.field.carModel')}
          value={draft.carModel}
          maxLength={LIMITS.short}
          onChange={(event) => patch({ carModel: event.target.value })}
          error={fieldError('carModel')}
        />
      )}
    </>
  )
}
