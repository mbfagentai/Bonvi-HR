import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField, type SelectOption } from '@/shared/ui'
import type { Candidate } from '../api/recruiting-api'
import { useRegisterCandidate, useUpdateCandidate } from '../api/recruiting-queries'
import { RECRUITING_ERROR_FIELD, recruitingErrorCode } from '../lib/recruiting-error'
import { orNull } from '../lib/recruiting-presentation'

const FORM_ID = 'candidate-form'

/**
 * Nomzodni voronkaga kiritish va shaxsiy ma'lumotini tuzatish —
 * BITTA oyna (`incident-form-dialog.tsx` naqshi).
 *
 * <b>Vakansiya faqat YARATISHDA tanlanadi.</b> Backend'da
 * `UpdateCandidateRequest` da bunday maydon UMUMAN YO'Q va bu ataylab:
 * nomzodni boshqa o'ringa ko'chirish — tuzatish emas, alohida QAROR
 * va u alohida amal bilan bajariladi (`/reassign`, nomzod kartasida).
 * Formada ko'rsatib qo'yilsa, HR o'zgartirgan qiymat jimgina
 * yo'qolardi.
 *
 * <b>Boshlang'ich bosqich ham faqat yaratishda.</b> Bo'sh qoldirilsa
 * server voronkaning BIRINCHI faol bosqichini o'zi qo'yadi — odatiy
 * holat aynan shu. Tanlash imkoni esa saqlanadi: HR ba'zan nomzodni
 * o'rtadan boshlaydi (masalan tavsiya bilan kelgan odam anketani
 * to'ldirmasdan to'g'ridan-to'g'ri suhbatga tushadi).
 *
 * <b>Manba (`source`) formada YO'Q.</b> Bu ekrandan kiritilgan har bir
 * nomzod ta'rifi bo'yicha «qo'lda» va server sukut bo'yicha shuni
 * qo'yadi. `TelegramBot` ni faqat botning o'zi yozadi — tanlov
 * berilsa, HR uni xato belgilab, «bot qancha nomzod keltirdi» degan
 * yagona muhim raqamni buzardi.
 */
export function CandidateFormDialog({
  open,
  onClose,
  item,
  vacancyOptions,
  stageOptions,
  /** Yangi nomzod uchun oldindan tanlangan vakansiya (ro'yxat filtridan). */
  defaultVacancyId = '',
}: {
  open: boolean
  onClose: () => void
  /** `null` — yangi nomzod. */
  item: Candidate | null
  vacancyOptions: SelectOption[]
  stageOptions: SelectOption[]
  defaultVacancyId?: string
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const create = useRegisterCandidate()
  const update = useUpdateCandidate()
  const isEdit = item !== null
  const mutation = isEdit ? update : create

  const [lastName, setLastName] = useState('')
  const [firstName, setFirstName] = useState('')
  const [middleName, setMiddleName] = useState('')
  const [birthDate, setBirthDate] = useState('')
  const [phone, setPhone] = useState('')
  const [address, setAddress] = useState('')
  const [speciality, setSpeciality] = useState('')
  const [note, setNote] = useState('')
  const [vacancyId, setVacancyId] = useState('')
  const [stageId, setStageId] = useState('')

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)

  useEffect(() => {
    if (!open) return
    setFailure(null)
    setFieldError(null)
    create.reset()
    update.reset()

    setLastName(item?.lastName ?? '')
    setFirstName(item?.firstName ?? '')
    setMiddleName(item?.middleName ?? '')
    setBirthDate(item?.birthDate ?? '')
    setPhone(item?.phone ?? '')
    setAddress(item?.address ?? '')
    setSpeciality(item?.speciality ?? '')
    setNote(item?.note ?? '')
    setVacancyId(item?.vacancyId ?? defaultVacancyId)
    setStageId('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item])

  if (!open) return null

  const incomplete =
    lastName.trim() === '' || firstName.trim() === '' || (!isEdit && vacancyId === '')

  const fieldMessage = (field: string): string | undefined =>
    fieldError?.field === field ? errorMessage(fieldError.code) : undefined

  const onError = (error: unknown) => {
    const code = recruitingErrorCode(error)
    const field = RECRUITING_ERROR_FIELD[code]
    if (field === undefined) setFailure(code)
    else setFieldError({ field, code })
  }

  const submit = () => {
    if (incomplete) return
    setFailure(null)
    setFieldError(null)

    const shared = {
      lastName: lastName.trim(),
      firstName: firstName.trim(),
      middleName: orNull(middleName),
      birthDate: orNull(birthDate),
      phone: orNull(phone),
      address: orNull(address),
      speciality: orNull(speciality),
      note: orNull(note),
    }

    if (isEdit) {
      update.mutate({ id: item.id, body: shared }, { onSuccess: onClose, onError })
      return
    }

    create.mutate(
      { ...shared, vacancyId, stageId: stageId === '' ? null : stageId },
      { onSuccess: onClose, onError },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!mutation.isPending}
      title={isEdit ? t('recruiting.candidateEdit') : t('recruiting.candidateCreate')}
      description={isEdit ? item.fullName : t('recruiting.candidateCreateHint')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={mutation.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            variant="primary"
            disabled={mutation.isPending || incomplete}
          >
            {mutation.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <form
        id={FORM_ID}
        noValidate
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault()
          submit()
        }}
      >
        {failure !== null && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(failure)}
          </Alert>
        )}

        {!isEdit && (
          <>
            <Select
              label={t('recruiting.vacancy')}
              placeholder={t('recruiting.chooseVacancy')}
              hint={t('recruiting.vacancyOpenHint')}
              options={vacancyOptions}
              value={vacancyId}
              error={fieldMessage('vacancyId')}
              disabled={mutation.isPending}
              onChange={(event) => setVacancyId(event.target.value)}
            />

            <Select
              label={t('recruiting.startStage')}
              placeholder={t('recruiting.startStageDefault')}
              hint={t('recruiting.startStageHint')}
              options={stageOptions}
              value={stageId}
              disabled={mutation.isPending}
              onChange={(event) => setStageId(event.target.value)}
            />
          </>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('employees.lastName')}
            autoComplete="off"
            value={lastName}
            error={fieldMessage('lastName')}
            disabled={mutation.isPending}
            onChange={(event) => setLastName(event.target.value)}
          />
          <TextField
            label={t('employees.firstName')}
            autoComplete="off"
            value={firstName}
            error={fieldMessage('firstName')}
            disabled={mutation.isPending}
            onChange={(event) => setFirstName(event.target.value)}
          />
        </div>

        <TextField
          label={t('employees.middleName')}
          autoComplete="off"
          value={middleName}
          disabled={mutation.isPending}
          onChange={(event) => setMiddleName(event.target.value)}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('employee.birthDate')}
            type="date"
            value={birthDate}
            error={fieldMessage('birthDate')}
            disabled={mutation.isPending}
            onChange={(event) => setBirthDate(event.target.value)}
          />
          {/*
            Telefon oddiy matn maydoni: server «+998…» ni ham, 9 xonali
            raqamni ham qabul qiladi (`RegisterCandidateRequest` izohi).
            `PhoneField` (auth moduli) ATAYLAB ishlatilmadi — u kirish
            formasining maydoni va +998 prefiksini majburlaydi, nomzod
            telefoni esa IXTIYORIY: bo'sh qoldirilishi kerak bo'lgan
            maydonni majburiy shaklga solish xato bo'lardi.
          */}
          <TextField
            label={t('employee.phone')}
            type="tel"
            inputMode="tel"
            autoComplete="off"
            placeholder="+998 90 123 45 67"
            value={phone}
            error={fieldMessage('phone')}
            disabled={mutation.isPending}
            onChange={(event) => setPhone(event.target.value)}
          />
        </div>

        <TextField
          label={t('employee.speciality')}
          autoComplete="off"
          value={speciality}
          disabled={mutation.isPending}
          onChange={(event) => setSpeciality(event.target.value)}
        />

        <TextField
          label={t('employee.address')}
          autoComplete="off"
          value={address}
          disabled={mutation.isPending}
          onChange={(event) => setAddress(event.target.value)}
        />

        <TextArea
          label={t('recruiting.note')}
          placeholder={t('recruiting.candidateNotePlaceholder')}
          value={note}
          disabled={mutation.isPending}
          onChange={(event) => setNote(event.target.value)}
        />
      </form>
    </Dialog>
  )
}
