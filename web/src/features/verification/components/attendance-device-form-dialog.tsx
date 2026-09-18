import { KeyRound, LoaderCircle, RotateCcw } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'

import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Button,
  Dialog,
  SegmentedControl,
  Select,
  TextField,
  type SegmentOption,
} from '@/shared/ui'

import {
  ATTENDANCE_IDENTITY_PROVIDERS,
  ATTENDANCE_PROVIDERS,
  DEVICE_AUTH_MODES,
  DEVICE_DIRECTION_ROLES,
  type AttendanceDevice,
  type AttendanceDeviceCreated,
  type AttendanceIdentityProvider,
  type AttendanceProvider,
  type DeviceAuthMode,
  type DeviceDirectionRole,
} from '../api/attendance-api'
import { useCreateDevice, useUpdateDevice } from '../api/attendance-queries'
import {
  AUTH_MODE_LABEL_KEY,
  DIRECTION_ROLE_LOOK,
  deviceProviderLabel,
  identityProviderLabel,
} from '../lib/attendance-manage-presentation'

/* Ustun uzunliklari — SPEC §2.1 (`attendance_devices`). */
const NAME_MAX = 200
const SERIAL_MAX = 100
const MODEL_MAX = 100
const LOCATION_MAX = 200
const ALLOWED_IPS_MAX = 500
const SECRET_MAX = 200

type Field = 'name' | 'serialNumber' | 'secret'

const FIELD_OF_CODE: Readonly<Record<string, Field>> = {
  'davomat.qurilma_nom_bosh': 'name',
  'davomat.qurilma_nom_takror': 'name',
  'davomat.qurilma_seriya_takror': 'serialNumber',
  'davomat.imzo_siri_yoq': 'secret',
}

/**
 * Mavjud imzo siri bilan nima qilinadi — so'rovdagi UCH HOLATLI maydonning
 * ekrandagi ko'rinishi (SPEC §6.2: `null` — tegilmaydi, `""` — tozalanadi,
 * matn — almashtiriladi).
 */
type SecretAction = 'keep' | 'replace' | 'clear'

/** Yo'nalish rolining izohi — tanlangan rol ostida bitta gap. */
const ROLE_HINT_KEY: Readonly<Record<DeviceDirectionRole, string>> = {
  ReportsDirection: 'attendance.roleReportsHint',
  EntryOnly: 'attendance.roleEntryOnlyHint',
  ExitOnly: 'attendance.roleExitOnlyHint',
}

/**
 * Qurilmani ro'yxatga olish va sozlash.
 *
 * ⭐ Yaratishda server TOKEN qaytaradi va u hayotda bir marta ko'rinadi
 * (SPEC §3.2). Bu oyna tokenni o'zida ushlamaydi: javobni darhol
 * `onCreated` ga beradi, sahifa esa token oynasini ochadi. Tahrirlashda
 * token umuman tegilmaydi — uni almashtirish alohida, tasdiqlanadigan amal
 * (eski token shu zahoti o'ladi).
 *
 * Imzo siri (HMAC) — FAQAT YOZILADI: server uni hech qanday ko'rinishda,
 * hatto yulduzchalar bilan ham qaytarmaydi (`hasHmacSecret` xolos). Shu
 * sababli tahrirlashda «bor» deb ko'rsatiladi va uch yo'l beriladi:
 * qoldirish, almashtirish, o'chirish.
 */
export function AttendanceDeviceFormDialog({
  open,
  onClose,
  device,
  onCreated,
}: {
  open: boolean
  onClose: () => void
  /** `null` — yangi qurilma. */
  device: AttendanceDevice | null
  /** Faqat yaratishda: token bilan birga qaytgan javob. */
  onCreated: (created: AttendanceDeviceCreated) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const options = useFilterOptions()

  const create = useCreateDevice()
  const update = useUpdateDevice()
  const isEdit = device !== null
  const mutation = isEdit ? update : create

  const [name, setName] = useState('')
  const [serialNumber, setSerialNumber] = useState('')
  const [model, setModel] = useState('')
  const [provider, setProvider] = useState<AttendanceProvider>('Hikvision')
  const [identityProvider, setIdentityProvider] = useState<AttendanceIdentityProvider>('Verifix')
  const [directionRole, setDirectionRole] = useState<DeviceDirectionRole>('ReportsDirection')
  const [legalEntityId, setLegalEntityId] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [locationNote, setLocationNote] = useState('')
  const [authMode, setAuthMode] = useState<DeviceAuthMode>('Token')
  const [allowedIps, setAllowedIps] = useState('')
  const [secret, setSecret] = useState('')
  const [secretAction, setSecretAction] = useState<SecretAction>('keep')

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: Field; code: string } | null>(null)

  useEffect(() => {
    if (!open) return
    setName(device?.name ?? '')
    setSerialNumber(device?.serialNumber ?? '')
    setModel(device?.model ?? '')
    setProvider(device?.provider ?? 'Hikvision')
    setIdentityProvider(device?.identityProvider ?? 'Verifix')
    setDirectionRole(device?.directionRole ?? 'ReportsDirection')
    setLegalEntityId(device?.legalEntityId ?? '')
    setDepartmentId(device?.departmentId ?? '')
    setLocationNote(device?.locationNote ?? '')
    setAuthMode(device?.authMode ?? 'Token')
    setAllowedIps(device?.allowedIps ?? '')
    setSecret('')
    setSecretAction('keep')
    setFailure(null)
    setFieldError(null)
    create.reset()
    update.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, device])

  const pending = mutation.isPending
  const storedSecret = device?.hasHmacSecret === true

  /** Serverga nima ketadi (`null` / `""` / matn) — bitta joyda hisoblanadi. */
  const secretToSend: string | null = (() => {
    if (storedSecret) {
      if (secretAction === 'clear') return ''
      if (secretAction === 'replace') return secret.trim() === '' ? null : secret
      return null
    }
    return authMode === 'TokenAndHmac' && secret.trim() !== '' ? secret : null
  })()

  /*
    `TokenAndHmac` sirsiz — serverda `davomat.imzo_siri_yoq`. Oldindan
    aytiladi: saqlangan sir bor va «qoldirish» tanlangan bo'lsa — bor;
    almashtirish yoki yangi qurilma — maydon bo'sh bo'lmasligi kerak;
    tozalash esa bu rejimda umuman ma'nosiz.
  */
  const secretMissing =
    authMode === 'TokenAndHmac' &&
    (storedSecret
      ? secretAction === 'clear' || (secretAction === 'replace' && secret.trim() === '')
      : secret.trim() === '')

  const incomplete = name.trim() === '' || secretMissing

  const submit = () => {
    if (incomplete) return
    setFailure(null)
    setFieldError(null)

    const body = {
      name: name.trim(),
      serialNumber: orNull(serialNumber),
      model: orNull(model),
      provider,
      identityProvider,
      directionRole,
      legalEntityId: legalEntityId === '' ? null : legalEntityId,
      departmentId: departmentId === '' ? null : departmentId,
      locationNote: orNull(locationNote),
      authMode,
      allowedIps: orNull(allowedIps),
      hmacSecret: secretToSend,
    }

    const onError = (error: unknown) => {
      const code = problemCode(error)
      const field = FIELD_OF_CODE[code]
      if (field === undefined) setFailure(code)
      else setFieldError({ field, code })
    }

    if (isEdit) {
      update.mutate({ id: device.id, body }, { onSuccess: onClose, onError })
    } else {
      create.mutate(body, { onSuccess: onCreated, onError })
    }
  }

  const fieldMessage = (field: Field): string | undefined =>
    fieldError?.field === field ? errorMessage(fieldError.code) : undefined

  const providerOptions = ATTENDANCE_PROVIDERS.map((value) => ({
    value,
    label: deviceProviderLabel(value, t),
  }))

  const identityOptions = ATTENDANCE_IDENTITY_PROVIDERS.map((value) => ({
    value,
    label: identityProviderLabel(value, t),
  }))

  const roleOptions: Array<SegmentOption<DeviceDirectionRole>> = DEVICE_DIRECTION_ROLES.map(
    (value) => ({ value, label: t(DIRECTION_ROLE_LOOK[value].labelKey) }),
  )

  const authOptions: Array<SegmentOption<DeviceAuthMode>> = DEVICE_AUTH_MODES.map((value) => ({
    value,
    label: t(AUTH_MODE_LABEL_KEY[value]),
  }))

  const secretError =
    fieldMessage('secret') ?? (secretMissing ? errorMessage('davomat.imzo_siri_yoq') : undefined)

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!pending}
      title={isEdit ? t('attendance.deviceEditTitle') : t('attendance.deviceCreateTitle')}
      description={isEdit ? device.name : t('attendance.deviceCreateSubtitle')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button type="button" variant="primary" onClick={submit} disabled={pending || incomplete}>
            {pending && <LoaderCircle className="animate-spin" />}
            {isEdit ? t('common.save') : t('attendance.deviceCreate')}
          </Button>
        </>
      }
    >
      <div className="space-y-6">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}

        <Section title={t('attendance.deviceSectionBasics')}>
          <TextField
            label={t('attendance.deviceName')}
            hint={t('attendance.deviceNameHint')}
            value={name}
            maxLength={NAME_MAX}
            disabled={pending}
            error={fieldMessage('name')}
            onChange={(event) => {
              setName(event.target.value)
              if (fieldError?.field === 'name') setFieldError(null)
            }}
          />
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={t('attendance.serialNumber')}
              value={serialNumber}
              maxLength={SERIAL_MAX}
              disabled={pending}
              inputClassName="font-mono"
              error={fieldMessage('serialNumber')}
              onChange={(event) => {
                setSerialNumber(event.target.value)
                if (fieldError?.field === 'serialNumber') setFieldError(null)
              }}
            />
            <TextField
              label={t('attendance.model')}
              value={model}
              maxLength={MODEL_MAX}
              disabled={pending}
              onChange={(event) => setModel(event.target.value)}
            />
          </div>
        </Section>

        <Section title={t('attendance.deviceSectionLocation')}>
          <div className="grid gap-4 sm:grid-cols-2">
            <Select
              label={t('employee.legalEntity')}
              options={options.legalEntities}
              placeholder={options.isPending ? t('common.loading') : t('attendance.notSpecified')}
              value={legalEntityId}
              disabled={pending || options.isPending}
              onChange={(event) => setLegalEntityId(event.target.value)}
            />
            <Select
              label={t('employee.department')}
              options={options.departments}
              placeholder={options.isPending ? t('common.loading') : t('attendance.notSpecified')}
              value={departmentId}
              disabled={pending || options.isPending}
              onChange={(event) => setDepartmentId(event.target.value)}
            />
          </div>
          <TextField
            label={t('attendance.location')}
            hint={t('attendance.locationHint')}
            value={locationNote}
            maxLength={LOCATION_MAX}
            disabled={pending}
            onChange={(event) => setLocationNote(event.target.value)}
          />
        </Section>

        <Section title={t('attendance.deviceSectionEvents')}>
          <div className="grid gap-4 sm:grid-cols-2">
            <Select
              label={t('attendance.provider')}
              hint={t('attendance.providerHint')}
              options={providerOptions}
              value={provider}
              disabled={pending}
              onChange={(event) => setProvider(event.target.value as AttendanceProvider)}
            />
            <Select
              label={t('attendance.identityProvider')}
              hint={t('attendance.identityProviderHint')}
              options={identityOptions}
              value={identityProvider}
              disabled={pending}
              onChange={(event) =>
                setIdentityProvider(event.target.value as AttendanceIdentityProvider)
              }
            />
          </div>

          <div className="space-y-2">
            <p className="text-footnote font-medium text-[var(--fg-secondary)]">
              {t('attendance.directionRole')}
            </p>
            <SegmentedControl
              options={roleOptions}
              value={directionRole}
              onChange={setDirectionRole}
              className="w-full"
              aria-label={t('attendance.directionRole')}
            />
            <p className="text-footnote text-[var(--fg-tertiary)]">
              {t(ROLE_HINT_KEY[directionRole])}
            </p>
          </div>
        </Section>

        <Section title={t('attendance.deviceSectionSecurity')}>
          <div className="space-y-2">
            <p className="text-footnote font-medium text-[var(--fg-secondary)]">
              {t('attendance.authMode')}
            </p>
            <SegmentedControl
              options={authOptions}
              value={authMode}
              onChange={setAuthMode}
              aria-label={t('attendance.authMode')}
            />
            <p className="text-footnote text-[var(--fg-tertiary)]">
              {authMode === 'Token'
                ? t('attendance.authModeTokenHint')
                : t('attendance.authModeTokenAndHmacHint')}
            </p>
          </div>

          {storedSecret ? (
            <StoredSecret
              action={secretAction}
              authMode={authMode}
              secret={secret}
              error={secretError}
              disabled={pending}
              onAction={(next) => {
                setSecretAction(next)
                setSecret('')
              }}
              onSecret={setSecret}
            />
          ) : (
            authMode === 'TokenAndHmac' && (
              <TextField
                label={t('attendance.hmacSecret')}
                hint={t('attendance.hmacSecretHint')}
                type="password"
                autoComplete="new-password"
                value={secret}
                maxLength={SECRET_MAX}
                disabled={pending}
                error={secret === '' && fieldError?.field !== 'secret' ? undefined : secretError}
                onChange={(event) => setSecret(event.target.value)}
              />
            )
          )}

          <TextField
            label={t('attendance.allowedIps')}
            hint={t('attendance.allowedIpsHint')}
            value={allowedIps}
            maxLength={ALLOWED_IPS_MAX}
            disabled={pending}
            inputClassName="font-mono"
            onChange={(event) => setAllowedIps(event.target.value)}
          />
        </Section>
      </div>
    </Dialog>
  )
}

/** Mavjud sir: «bor» belgisi va uch yo'l — qoldirish, almashtirish, o'chirish. */
function StoredSecret({
  action,
  authMode,
  secret,
  error,
  disabled,
  onAction,
  onSecret,
}: {
  action: SecretAction
  authMode: DeviceAuthMode
  secret: string
  error: string | undefined
  disabled: boolean
  onAction: (next: SecretAction) => void
  onSecret: (value: string) => void
}) {
  const { t } = useLocale()

  return (
    <div className="space-y-3 rounded-xl border border-[var(--border)] bg-[var(--bg-elevated)] px-4 py-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="flex items-center gap-2 text-subhead font-medium">
          <KeyRound className="size-4 text-[var(--fg-secondary)]" />
          {action === 'clear' ? t('attendance.hmacSecretWillClear') : t('attendance.hmacSecretStored')}
        </p>

        {action === 'keep' ? (
          <div className="flex gap-1.5">
            <Button type="button" size="sm" disabled={disabled} onClick={() => onAction('replace')}>
              {t('attendance.hmacSecretReplace')}
            </Button>
            {/* Imzo rejimida sirni o'chirish — serverda xato; tugma o'sha holatda yo'q */}
            {authMode === 'Token' && (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                disabled={disabled}
                onClick={() => onAction('clear')}
              >
                {t('attendance.hmacSecretClear')}
              </Button>
            )}
          </div>
        ) : (
          <Button type="button" size="sm" variant="ghost" disabled={disabled} onClick={() => onAction('keep')}>
            <RotateCcw />
            {t('attendance.hmacSecretKeep')}
          </Button>
        )}
      </div>

      {action === 'replace' && (
        <TextField
          label={t('attendance.hmacSecretNew')}
          type="password"
          autoComplete="new-password"
          value={secret}
          maxLength={SECRET_MAX}
          disabled={disabled}
          error={error}
          onChange={(event) => onSecret(event.target.value)}
        />
      )}

      {action !== 'replace' && error !== undefined && (
        <p role="alert" className="text-footnote text-ios-red">
          {error}
        </p>
      )}
    </div>
  )
}

/** Bo'lim — `candidate-hire-dialog.tsx` dagi `Section` bilan bir xil ko'rinish. */
function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-4 rounded-lg border border-[var(--border)] bg-[var(--bg-sunken)] p-4">
      <h3 className="text-caption1 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase">
        {title}
      </h3>
      {children}
    </section>
  )
}

function orNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}
