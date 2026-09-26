import { LoaderCircle } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, SegmentedControl, TextField, type SegmentOption } from '@/shared/ui'

import {
  DEVICE_DIRECTION_ROLES,
  type AttendanceDevice,
  type AttendanceDeviceCreated,
  type DeviceDirectionRole,
} from '../api/attendance-api'
import { useCreateDevice, useUpdateDevice } from '../api/attendance-queries'
import { DIRECTION_ROLE_LOOK } from '../lib/attendance-manage-presentation'

/* Ustun uzunliklari — SPEC §2.1 (`attendance_devices`). */
const NAME_MAX = 200
const SERIAL_MAX = 100
const MODEL_MAX = 100

type Field = 'name' | 'serialNumber'

const FIELD_OF_CODE: Readonly<Record<string, Field>> = {
  'davomat.qurilma_nom_bosh': 'name',
  'davomat.qurilma_nom_takror': 'name',
  'davomat.qurilma_seriya_takror': 'serialNumber',
}

/**
 * Yangi terminalning ekranda so'ralmaydigan sozlamalari — AGENT modeli
 * (SPEC-agent §C, §E) uchun yagona to'g'ri qiymatlar.
 *
 * * `Generic` — agent terminal jurnalini o'qib, Umumiy JSON qilib yuboradi;
 *   «Hikvision» tahlilchisi terminalning o'z PUSH'i uchun edi va agent
 *   tanasini o'qiy olmaydi.
 * * `Hikvision` raqamlar fazosi — agent yozadigan `9` + 8 xonali raqamlar
 *   shu fazoda ajratiladi. «Verifix» bo'lsa, ular Verifix raqamlari bilan
 *   bir qatorga tushib, eksport bog'langanda aralashardi.
 * * `Token` — agent imzo (HMAC) qo'ymaydi; «Token + imzo» uni 401 ga
 *   uchratardi.
 *
 * Joy maydonlari (firma, bo'lim, joy izohi) hech qaysi qoidada
 * ishlatilmaydi: egasining qarori bilan HAR bir xodim HAR bir terminalga
 * yoziladi (2026-09-26 da formadan olindi).
 */
const NEW_DEVICE_DEFAULTS = {
  provider: 'Generic',
  identityProvider: 'Hikvision',
  authMode: 'Token',
  legalEntityId: null,
  departmentId: null,
  locationNote: null,
  allowedIps: null,
} as const

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
 * Ekranda faqat nom, seriya/model va yo'nalish. Qolgan sozlamalar
 * yaratishda {@link NEW_DEVICE_DEFAULTS} dan olinadi, tahrirda esa
 * qurilmaning O'Z qiymati qaytarib yuboriladi: so'rov to'liq qayta
 * yozadi va bo'sh qoldirilgan maydon serverda sukutga tushardi.
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

  const create = useCreateDevice()
  const update = useUpdateDevice()
  const isEdit = device !== null
  const mutation = isEdit ? update : create

  const [name, setName] = useState('')
  const [serialNumber, setSerialNumber] = useState('')
  const [model, setModel] = useState('')
  const [directionRole, setDirectionRole] = useState<DeviceDirectionRole>('ReportsDirection')

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: Field; code: string } | null>(null)

  useEffect(() => {
    if (!open) return
    setName(device?.name ?? '')
    setSerialNumber(device?.serialNumber ?? '')
    setModel(device?.model ?? '')
    setDirectionRole(device?.directionRole ?? 'ReportsDirection')
    setFailure(null)
    setFieldError(null)
    create.reset()
    update.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, device])

  const pending = mutation.isPending
  const incomplete = name.trim() === ''

  const submit = () => {
    if (incomplete) return
    setFailure(null)
    setFieldError(null)

    const hidden = isEdit
      ? {
          provider: device.provider,
          identityProvider: device.identityProvider,
          authMode: device.authMode,
          legalEntityId: device.legalEntityId,
          departmentId: device.departmentId,
          locationNote: device.locationNote,
          allowedIps: device.allowedIps,
        }
      : NEW_DEVICE_DEFAULTS

    const body = {
      ...hidden,
      name: name.trim(),
      serialNumber: orNull(serialNumber),
      model: orNull(model),
      directionRole,
      // `null` — saqlangan imzo siriga tegilmaydi (SPEC §6.2)
      hmacSecret: null,
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

  const roleOptions: Array<SegmentOption<DeviceDirectionRole>> = DEVICE_DIRECTION_ROLES.map(
    (value) => ({ value, label: t(DIRECTION_ROLE_LOOK[value].labelKey) }),
  )

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="md"
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
      </div>
    </Dialog>
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
