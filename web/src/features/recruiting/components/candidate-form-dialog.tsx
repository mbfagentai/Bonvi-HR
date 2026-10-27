import { Link } from '@tanstack/react-router'
import { LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState, type MouseEvent } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { uzPhoneDigits } from '@/shared/lib/phone'
import {
  Alert,
  Badge,
  Button,
  DateField,
  Dialog,
  PhoneInput,
  Select,
  TextArea,
  TextField,
  type SelectOption,
} from '@/shared/ui'
import type { Candidate, CandidatePhoneMatch } from '../api/recruiting-api'
import {
  useCandidatePhoneMatches,
  useReferralChannelOptions,
  useRegisterCandidate,
  useUpdateCandidate,
  type ReferralChannelOption,
} from '../api/recruiting-queries'
import { RECRUITING_ERROR_FIELD, recruitingErrorCode } from '../lib/recruiting-error'
import { CANDIDATE_LOOK, orNull } from '../lib/recruiting-presentation'

const FORM_ID = 'candidate-form'

/** `CandidateQuestionnaire`'s and the bot's limit for the referral detail (SPEC-qayerdan-bilgan §3). */
const REFERRAL_DETAIL_MAX = 200

/** Module-level — a fresh `[]` on every render would re-run the option memo. */
const NO_CHANNELS: ReferralChannelOption[] = []

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
  const [referralChannelId, setReferralChannelId] = useState('')
  const [referralDetail, setReferralDetail] = useState('')

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)

  /*
    Shu raqamli BOSHQA nomzodlar — faqat ogohlantirish. Hook `return null`
    dan OLDIN chaqiriladi (hook'lar tartibi), shu sababli oyna ochiqligi
    unga alohida beriladi.
  */
  const phoneMatches = useCandidatePhoneMatches(phone, item?.id ?? null, open)

  /*
    «Ish haqida qayerdan bilgan» — the «Qayerdan bilgan» catalog, ALL rows:
    the inactive ones are needed to show (and keep) the channel an older
    candidate already has. Only active channels are offered for a new choice
    (SPEC-qayerdan-bilgan §5). The server sends the list alphabetically.
  */
  const channels = useReferralChannelOptions()
  const channelList = channels.data ?? NO_CHANNELS
  const savedChannelId = item?.referralChannelId ?? null
  const savedChannelName = item?.referralChannelName ?? null

  const channelOptions = useMemo<SelectOption[]>(() => {
    const options: SelectOption[] = channelList
      .filter((channel) => channel.isActive || channel.id === savedChannelId)
      .map((channel) => ({
        value: channel.id,
        label: channel.isActive
          ? channel.name
          : t('recruiting.referralInactive', { name: channel.name }),
      }))

    /*
      The candidate's channel is missing from the catalog list (removed, or the
      list is still loading) — still show it as the selected value, by the name
      the candidate DTO carries, so the field never looks empty or changes.
    */
    if (
      savedChannelId !== null &&
      savedChannelName !== null &&
      channels.isSuccess &&
      !options.some((option) => option.value === savedChannelId)
    ) {
      options.push({
        value: savedChannelId,
        label: t('recruiting.referralInactive', { name: savedChannelName }),
      })
    }

    return options
  }, [channelList, channels.isSuccess, savedChannelId, savedChannelName, t])

  const chosenChannel = channelList.find((channel) => channel.id === referralChannelId)
  const needsDetail = chosenChannel?.asksDetail === true
  // The saved channel is not in the catalog list any more: send it back unchanged, detail and all
  const keepsSavedReferral = chosenChannel === undefined && referralChannelId !== '' && referralChannelId === savedChannelId

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
    setPhone(uzPhoneDigits(item?.phone ?? ''))
    setAddress(item?.address ?? '')
    setSpeciality(item?.speciality ?? '')
    setNote(item?.note ?? '')
    setVacancyId(item?.vacancyId ?? defaultVacancyId)
    setStageId('')
    setReferralChannelId(item?.referralChannelId ?? '')
    setReferralDetail(item?.referralDetail ?? '')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item])

  if (!open) return null

  /*
    The channel is required for a NEW candidate; on edit a legacy candidate
    without one may stay empty (a candidate that has one cannot clear it —
    the select then offers no empty row). The detail is required whenever the
    chosen channel asks for it.
  */
  const incomplete =
    lastName.trim() === '' ||
    firstName.trim() === '' ||
    (!isEdit && vacancyId === '') ||
    (!isEdit && referralChannelId === '') ||
    (needsDetail && referralDetail.trim() === '')

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
      referralChannelId: referralChannelId === '' ? null : referralChannelId,
      // A detail typed for another channel is not sent once the channel no longer asks for it
      referralDetail: needsDetail
        ? orNull(referralDetail)
        : keepsSavedReferral
          ? (item?.referralDetail ?? null)
          : null,
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
          <DateField
            label={t('employee.birthDate')}
            value={birthDate}
            error={fieldMessage('birthDate')}
            disabled={mutation.isPending}
            onChange={setBirthDate}
          />
          {/*
            «+998» o'zgarmas, HR faqat 9 xonani teradi (2026-09-26,
            foydalanuvchi qarori — tizimdagi hamma telefon maydoni bir xil).
            Maydon bo'sh qolishi mumkin: nomzod telefoni ixtiyoriy. Holatda
            9 xona saqlanadi, server uni ham qabul qiladi
            (`RegisterCandidateRequest` izohi).
          */}
          <PhoneInput
            label={t('employee.phone')}
            value={phone}
            error={fieldMessage('phone')}
            disabled={mutation.isPending}
            onChange={setPhone}
          />
        </div>

        {/*
          ⭐ Takroriy raqam — OGOHLANTIRISH, to'siq EMAS (egasining qarori,
          `docs/SPEC-qayta-ariza.md`: «HR qo'lda kiritganda to'siq YO'Q —
          u odamni ko'rib turibdi va qarorni o'zi qabul qiladi»). Shu
          sababli «Saqlash» bu yerga umuman qaramaydi.

          Telefon qatorining OSTIDA va butun kenglikda: maydon ikki
          ustunli qatorda tor, ro'yxat esa ism, holat va vakansiyani bir
          qatorda ko'rsatishi kerak.
        */}
        {phoneMatches.isError ? (
          <p className="text-footnote text-[var(--fg-tertiary)]">
            {t('recruiting.phoneMatchFailed')}
          </p>
        ) : phoneMatches.data !== undefined && phoneMatches.data.length > 0 ? (
          <PhoneMatchesAlert matches={phoneMatches.data} onOpen={onClose} />
        ) : null}

        {/*
          After the phone: HR asks «how did you hear about us?» while the person
          is still on the line or at the desk. Placeholder row only while
          nothing is chosen — once a channel is set it cannot be cleared.
        */}
        <Select
          label={t('recruiting.referralLabel')}
          placeholder={
            referralChannelId === '' || savedChannelId === null
              ? channels.isPending
                ? t('common.loading')
                : t('recruiting.chooseReferral')
              : undefined
          }
          options={channelOptions}
          value={referralChannelId}
          error={
            fieldMessage('referralChannelId') ??
            (channels.isError ? t('recruiting.referralLoadFailed') : undefined)
          }
          hint={isEdit && savedChannelId === null ? t('recruiting.referralLegacyHint') : undefined}
          disabled={mutation.isPending || channels.isPending}
          onChange={(event) => setReferralChannelId(event.target.value)}
        />

        {needsDetail && (
          <TextField
            label={t('recruiting.referralDetailLabel')}
            placeholder={t('recruiting.referralDetailPlaceholder')}
            autoComplete="off"
            maxLength={REFERRAL_DETAIL_MAX}
            value={referralDetail}
            error={fieldMessage('referralDetail')}
            disabled={mutation.isPending}
            onChange={(event) => setReferralDetail(event.target.value)}
          />
        )}

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

/**
 * Shu raqamli nomzodlar ro'yxati — forma ostidagi ogohlantirish.
 *
 * <b>Havola SHU tabda ochiladi</b> — ilovadagi boshqa ichki havolalar
 * kabi (nomzod kartasidan xodim kartasiga o'tish). Oyna shu bilan
 * yopiladi: aks holda kartadan kartaga o'tganda tahrirlash oynasi yangi
 * nomzodning ma'lumoti bilan qayta to'lib qolardi. Kiritilgan forma
 * kerak bo'lsa, HR havolani yangi tabda ochadi (Ctrl/⌘ bilan yoki
 * o'rta tugma) — bunday bosishda oyna ataylab YOPILMAYDI.
 *
 * Ohang `warning`: bu xato emas, lekin e'tibor talab qiladi — takroriy
 * yozuv loyihaning eng qimmat muammosi (`nav-items.ts`, «Ishdan
 * ketganlar» izohi).
 */
function PhoneMatchesAlert({
  matches,
  onOpen,
}: {
  matches: CandidatePhoneMatch[]
  onOpen: () => void
}) {
  const { t, num, date } = useLocale()

  const follow = (event: MouseEvent<HTMLAnchorElement>) => {
    // Yangi tabda ochish — forma shu yerda qoladi
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || event.button !== 0) {
      return
    }
    onOpen()
  }

  return (
    <Alert tone="warning" title={t('recruiting.phoneMatchTitle', { n: num(matches.length) })}>
      <ul className="space-y-1.5">
        {matches.map((match) => {
          const look = CANDIDATE_LOOK[match.status]

          return (
            <li key={match.id} className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <Link
                to="/nomzodlar/$candidateId"
                params={{ candidateId: match.id }}
                onClick={follow}
                className="font-medium text-[var(--accent-text)] hover:underline"
              >
                {match.fullName}
              </Link>

              <Badge tone={look.tone} size="sm">
                {t(`recruiting.candidateStatus.${look.key}`)}
              </Badge>

              <span className="text-caption1 text-[var(--fg-tertiary)]">
                {match.vacancyTitle ?? '—'}
                {match.createdAt !== '' && (
                  <span className="tabular"> · {date(match.createdAt)}</span>
                )}
              </span>
            </li>
          )
        })}
      </ul>

      <p className="mt-2">{t('recruiting.phoneMatchHint')}</p>
    </Alert>
  )
}
