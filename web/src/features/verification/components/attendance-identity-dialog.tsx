import { ArrowLeftRight, History, LoaderCircle, ScanFace, UserRound } from 'lucide-react'
import { useEffect, useState } from 'react'

import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import {
  ATTENDANCE_IDENTITY_PROVIDERS,
  type AttendanceIdentityProvider,
  type UnmatchedGroup,
} from '../api/attendance-api'
import { useAttendancePassPhoto, useCreateIdentity } from '../api/attendance-queries'
import { identityProviderLabel } from '../lib/attendance-manage-presentation'
import { AttendanceEmployeePicker, type PickedEmployee } from './attendance-employee-picker'

/** `attendance_identities.external_person_id` — varchar(64). */
const PERSON_ID_MAX = 64
/** `display_name_in_device` — varchar(200). */
const DISPLAY_NAME_MAX = 200
/** `note` — varchar(500). */
const NOTE_MAX = 500

/**
 * Qaysi server kodi qaysi maydon ostida chiqadi.
 *
 * Ro'yxatda yo'q kod oynaning tepasidagi umumiy ogohlantirishga tushadi
 * (`catalog-error.ts` naqshi) — masalan tarmoq uzilishi.
 */
const FIELD_OF_CODE: Readonly<Record<string, 'personId' | 'employee'>> = {
  'davomat.shaxs_raqami_bosh': 'personId',
  // 409 — bu raqam allaqachon kimgadir bog'langan: tuzatiladigan joy raqam
  'davomat.moslik_takror': 'personId',
  'davomat.xodim_yoq': 'employee',
  'davomat.xodim_topilmadi': 'employee',
}

/**
 * Qurilmadagi shaxs raqamini xodimga bog'lash — moslik yaratish.
 *
 * Ikki holatda ochiladi:
 *   · MOSLANMAGAN guruhdan (`group` berilgan) — raqam va ID makoni
 *     qotirilgan, chunki ular qurilmaning o'zidan kelgan va o'zgartirilsa
 *     endi o'sha o'tishlarga tegishli bo'lmay qoladi;
 *   · QO'LDA (`group === null`) — terminalga hali o'tmagan odamni oldindan
 *     bog'lash uchun: birinchi o'tishidanoq tabelga tushadi.
 *
 * ⭐ Oynaning asosiy ishi — ikki YUZNI yonma-yon qo'yish: turniket surati
 * va xodim kartasidagi rasm. Ism bo'yicha bog'lash egasining talabiga zid
 * (ikki kishining ismi bir xil bo'lishi mumkin) va xato moslik oylab
 * tabelni jimgina buzadi.
 *
 * ⭐ Bog'lash ORQAGA ham ishlaydi (SPEC §4.3): server shu raqamning barcha
 * moslanmagan o'tishlarini xodimga yozadi va o'sha kunlar tabelini
 * o'zi qayta hisoblaydi. Oyna buni OCHIQ aytadi — aks holda HR «eski
 * o'tishlar yo'qoldi» deb o'ylab, qayta hisoblashni qo'lda qidirardi.
 */
export function AttendanceIdentityDialog({
  open,
  onClose,
  group,
}: {
  open: boolean
  onClose: () => void
  /** `null` — qo'lda bog'lash (raqam qo'lda kiritiladi). */
  group: UnmatchedGroup | null
}) {
  const { t, num, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const create = useCreateIdentity()

  const [provider, setProvider] = useState<AttendanceIdentityProvider>('Verifix')
  const [personId, setPersonId] = useState('')
  const [employee, setEmployee] = useState<PickedEmployee | null>(null)
  const [displayName, setDisplayName] = useState('')
  const [note, setNote] = useState('')

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)

  // Oyna har ochilganda noldan: oldingi guruhning tanlovi yangisiga
  // «yopishib» qolsa, boshqa odam boshqa raqamga bog'lanib ketardi
  useEffect(() => {
    if (!open) return
    setProvider(group?.provider ?? 'Verifix')
    setPersonId(group?.rawPersonId ?? '')
    setEmployee(null)
    setDisplayName('')
    setNote('')
    setFailure(null)
    setFieldError(null)
    create.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, group])

  const pending = create.isPending
  const trimmedId = personId.trim()
  const incomplete = trimmedId === '' || employee === null

  const submit = () => {
    if (incomplete || employee === null) return
    setFailure(null)
    setFieldError(null)

    create.mutate(
      {
        provider,
        externalPersonId: trimmedId,
        employeeId: employee.id,
        displayNameInDevice: displayName.trim() === '' ? null : displayName.trim(),
        note: note.trim() === '' ? null : note.trim(),
      },
      {
        onSuccess: onClose,
        onError: (error) => {
          const code = problemCode(error)
          const field = FIELD_OF_CODE[code]
          if (field === undefined) setFailure(code)
          else setFieldError({ field, code })
        },
      },
    )
  }

  const fieldMessage = (field: string): string | undefined =>
    fieldError?.field === field ? errorMessage(fieldError.code) : undefined

  const providerOptions = ATTENDANCE_IDENTITY_PROVIDERS.map((value) => ({
    value,
    label: identityProviderLabel(value, t),
  }))

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!pending}
      title={t('attendance.linkToEmployee')}
      description={
        group === null ? t('attendance.linkManualSubtitle') : t('attendance.linkSubtitle')
      }
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={submit}
            disabled={pending || incomplete}
          >
            {pending && <LoaderCircle className="animate-spin" />}
            {t('attendance.linkToEmployee')}
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}

        {group !== null ? (
          <FaceComparison group={group} employee={employee} />
        ) : (
          <div className="grid gap-4 sm:grid-cols-2">
            <Select
              label={t('attendance.identityProvider')}
              options={providerOptions}
              value={provider}
              disabled={pending}
              onChange={(event) => setProvider(event.target.value as AttendanceIdentityProvider)}
            />
            <TextField
              label={t('attendance.personId')}
              value={personId}
              maxLength={PERSON_ID_MAX}
              disabled={pending}
              inputClassName="font-mono"
              error={fieldMessage('personId')}
              onChange={(event) => setPersonId(event.target.value)}
            />
          </div>
        )}

        {/* Guruhdan kelganda raqam o'zgarmaydi, lekin 409 aynan shu raqamga tegishli */}
        {group !== null && fieldMessage('personId') !== undefined && (
          <Alert tone="danger">{fieldMessage('personId')}</Alert>
        )}

        <AttendanceEmployeePicker
          value={employee}
          onChange={setEmployee}
          disabled={pending}
          error={fieldMessage('employee')}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('attendance.displayNameInDevice')}
            hint={t('attendance.displayNameInDeviceHint')}
            value={displayName}
            maxLength={DISPLAY_NAME_MAX}
            disabled={pending}
            onChange={(event) => setDisplayName(event.target.value)}
          />
          <TextArea
            label={t('employees.note')}
            rows={2}
            value={note}
            maxLength={NOTE_MAX}
            disabled={pending}
            onChange={(event) => setNote(event.target.value)}
          />
        </div>

        {/*
          Orqaga bog'lash — ogohlantirish emas, VA'DA: nima bo'lishini
          oldindan aytadi. Shu sababli `info`, `warning` emas.
        */}
        <div className="flex items-start gap-3 rounded-xl bg-[var(--bg-sunken)] px-4 py-3">
          <History className="mt-0.5 size-[18px] shrink-0 text-[var(--fg-secondary)]" />
          <p className="text-footnote text-[var(--fg-secondary)]">
            {group === null
              ? t('attendance.linkRetroactiveHintManual')
              : t('attendance.linkRetroactiveHint', {
                  n: num(group.passCount),
                  from: dateTime(group.firstSeenAt),
                })}
          </p>
        </div>
      </div>
    </Dialog>
  )
}

/**
 * Ikki yuz yonma-yon: turniket ko'rgan odam va tanlangan xodim.
 *
 * Tanlov qilinmaguncha o'ng tomonda bo'sh joy turadi — operatorga
 * «bu yerga kimdir qo'yilishi kerak» degan ishora.
 */
function FaceComparison({
  group,
  employee,
}: {
  group: UnmatchedGroup
  employee: PickedEmployee | null
}) {
  const { t, num } = useLocale()

  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--bg-sunken)] p-4">
      <div className="flex items-center justify-center gap-4 sm:gap-6">
        <figure className="flex min-w-0 flex-col items-center gap-2 text-center">
          <AttendancePassFace passId={group.samplePassId} size="lg" />
          <figcaption className="text-caption1 text-[var(--fg-secondary)]">
            {t('attendance.deviceSnapshot')}
          </figcaption>
        </figure>

        <ArrowLeftRight className="size-5 shrink-0 text-[var(--fg-tertiary)]" />

        <figure className="flex min-w-0 flex-col items-center gap-2 text-center">
          {employee === null ? (
            <span className="grid size-24 place-items-center rounded-full border-2 border-dashed border-[var(--border-strong)] text-[var(--fg-tertiary)]">
              <UserRound className="size-9" strokeWidth={1.6} />
            </span>
          ) : (
            <EmployeePhoto
              employeeId={employee.id}
              photoKey={employee.photoKey}
              name={employee.fullName}
              size="xl"
              className="size-24"
            />
          )}
          <figcaption className="text-caption1 text-[var(--fg-secondary)]">
            {t('attendance.employeePhoto')}
          </figcaption>
        </figure>
      </div>

      <dl className="mt-4 grid grid-cols-2 gap-3 border-t border-[var(--border)] pt-3 text-footnote sm:grid-cols-3">
        <div className="min-w-0">
          <dt className="text-[var(--fg-tertiary)]">{t('attendance.personId')}</dt>
          <dd className="truncate-1 font-mono font-medium">{group.rawPersonId}</dd>
        </div>
        <div className="min-w-0">
          <dt className="text-[var(--fg-tertiary)]">{t('attendance.passCount')}</dt>
          <dd className="tabular font-medium">{num(group.passCount)}</dd>
        </div>
        <div className="col-span-2 min-w-0 sm:col-span-1">
          <dt className="text-[var(--fg-tertiary)]">{t('attendance.device')}</dt>
          <dd className="truncate-1 font-medium">{group.lastDeviceName ?? '—'}</dd>
        </div>
      </dl>
    </div>
  )
}

const FACE_SIZE = {
  md: 'size-14 rounded-md',
  lg: 'size-24 rounded-lg',
} as const

/**
 * Turniket SURATI (o'tish snapshot'i) — xodim rasmi emas.
 *
 * Baytlar `GET /passes/{id}/photo` dan (`Authorization` sarlavhasi bilan,
 * `<img src>` ishlamaydi — `fetchPassPhoto` izohi). Surat yo'q yoki
 * tozalash vazifasi o'chirgan bo'lsa — yuz belgisi, bosh harflar emas:
 * bu yerda ism umuman NOMA'LUM, aynan shu sababli oyna ochilgan.
 *
 * Kvadrat, doira emas: bu kameraning kadri, profil rasmi emas — shakl
 * ikkalasini bir qarashda ajratib turadi.
 */
export function AttendancePassFace({
  passId,
  size = 'md',
  className,
}: {
  /** `UnmatchedGroup.samplePassId` — rasmi BOR eng yangi o'tish; `null` — rasmi yo'q. */
  passId: string | null
  size?: keyof typeof FACE_SIZE
  className?: string
}) {
  const { t } = useLocale()
  /*
    `samplePassId` ning o'zi «rasm bor» degani (§4.2: «newest pass that has
    a photo»), shu sababli u kalit o'rnida ham turadi. Surat keyin
    tozalangan bo'lsa server 404 beradi va hook `null` qaytaradi — belgi
    chiziladi, xato emas.
  */
  const photo = useAttendancePassPhoto(passId ?? '', passId)
  const src = useObjectUrl(photo.data ?? null)

  if (src === null) {
    return (
      <span
        role="img"
        aria-label={t('attendance.photoMissing')}
        title={t('attendance.photoMissing')}
        className={cn(
          'grid shrink-0 place-items-center bg-[var(--bg-elevated)] text-[var(--fg-tertiary)]',
          'border border-[var(--border)]',
          FACE_SIZE[size],
          className,
        )}
      >
        <ScanFace className={size === 'lg' ? 'size-9' : 'size-6'} strokeWidth={1.6} />
      </span>
    )
  }

  return (
    <img
      src={src}
      alt={t('attendance.deviceSnapshot')}
      loading="lazy"
      className={cn(
        'shrink-0 border border-[var(--border)] object-cover',
        FACE_SIZE[size],
        className,
      )}
    />
  )
}
