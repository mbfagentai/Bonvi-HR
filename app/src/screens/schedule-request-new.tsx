import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'

import { api } from '@/api/client'
import { ApiError } from '@/api/errors'
import type {
  CreateScheduleChangeRequest,
  DayPlanDto,
  ScheduleChangeDayInput,
  ScheduleChangeKind,
} from '@/api/types'
import { shortTime } from '@/data/attendance'
import { invalidateScheduleRequests, usePlan } from '@/data/queries'
import {
  MIN_REQUIRED_MINUTES,
  breakDiffersFromWindow,
  breakInsideShift,
  breakWindowMinutes,
  crossesMidnight,
  dayPlanText,
  hoursMinutes,
  maxRequiredMinutes,
  wireTime,
} from '@/data/schedule-requests'
import { addDays, parseIsoDate, todayIso } from '@/lib/time'
import { useErrorText } from '@/lib/use-error-message'
import { PushedScreen } from '@/ui/app-shell'
import { BottomAction, Button } from '@/ui/button'
import { Card } from '@/ui/card'
import { InlineMessage, Skeleton, SkeletonLines } from '@/ui/feedback'
import { DateRow, DurationRow, Field, SegmentedControl, SwitchRow, TextArea, TimeRow } from '@/ui/form'
import { ConfirmSheet } from '@/ui/sheet'
import { useToast } from '@/ui/toast'

/** Server chegaralari (`ScheduleChangeRequest.MaxDays`, `MaxSwapPairs`). */
const MAX_DAYS = 31
const MAX_PAIRS = 15

/** «Kunni o'zgartirish» dagi bitta sana — forma holati (vaqt «HH:mm» yoki bo'sh). */
interface DayDraft {
  key: number
  date: string
  isWorking: boolean
  startTime: string
  endTime: string
  hasBreak: boolean
  breakStart: string
  breakEnd: string
  /**
   * Jadvaldan olingan tanaffus uzunligi, daqiqa. Jadvalda u oynadan alohida
   * (60 daqiqalik tushlik 12:00–15:00 oynasida) va xodim tanaffusga
   * tegmaguncha aynan shu yuboriladi. `null` — server oyna uzunligini oladi:
   * reja qo'yilmagan blok yoki tanaffusni xodim o'zi yozgan.
   */
  breakMinutes: number | null
  /** «Erkin ish jadvali» (SPEC-erkin-jadval §7.2) — faqat ish kunida yuboriladi. */
  isFlexible: boolean
  /**
   * «Ish vaqti», daqiqa. `null` — SUKUT (smena − tanaffus): ko'rinadigan
   * qiymat smena yoki tanaffus bilan birga o'zgaradi va server uni o'zi
   * yozadi. Xodim sukutdan boshqa qiymat tanlasa — u qotadi.
   */
  requiredMinutes: number | null
  /**
   * Qaysi sana uchun blok «o'rnashgan»: reja qo'yilgan yoki xodim o'zi
   * tahrirlagan. Sana almashsa `null` bo'ladi va yangi sananing rejasi
   * qayta to'ldiriladi; xodim yozgan vaqt esa hech qachon ustidan
   * yozilmaydi.
   */
  settledFor: string | null
}

/** «Kun almashtirish» jufti: `date` ⇄ `withDate`. */
interface PairDraft {
  key: number
  date: string
  withDate: string
}

/** React kalitlari — sessiya ichida noyob bo'lsa yetarli. */
let draftSeq = 0
function nextKey(): number {
  draftSeq += 1
  return draftSeq
}

function emptyDay(date: string): DayDraft {
  return {
    key: nextKey(),
    date,
    isWorking: true,
    startTime: '',
    endTime: '',
    hasBreak: false,
    breakStart: '',
    breakEnd: '',
    breakMinutes: null,
    isFlexible: false,
    requiredMinutes: null,
    settledFor: null,
  }
}

/**
 * «Ish vaqti» sukuti va eng kattasi — serverning formulasi (SPEC-erkin-jadval
 * §2.5): smena minus ayiriladigan tanaffus; tanaffus — jadvaldagi uzunlik, u
 * bo'lmasa oynaning o'zi, oynasiz — 0 (`ScheduleChangeRequest.BreakMinutes`).
 * Smena to'liq tanlanmagan bo'lsa — `null`: hisoblaydigan narsa yo'q.
 */
function defaultRequired(draft: DayDraft): number | null {
  if (draft.startTime === '' || draft.endTime === '' || draft.startTime === draft.endTime) return null

  const breakMinutes =
    draft.breakMinutes ?? (draft.hasBreak ? breakWindowMinutes(draft.breakStart, draft.breakEnd) : 0)

  return maxRequiredMinutes(draft.startTime, draft.endTime, breakMinutes)
}

/**
 * Rejani blokka qo'yadi. Jadval biriktirilmagan sanada qiymatlar
 * tegilmaydi — to'ldiradigan narsa yo'q, xodim o'zi tanlaydi (tanaffus
 * uzunligi esa tushiriladi: u «jadvaldagi» deb ko'rsatilardi).
 */
function applyPlan(draft: DayDraft, plan: DayPlanDto): DayDraft {
  if (!plan.hasSchedule) return { ...draft, breakMinutes: null, settledFor: draft.date }

  const breakStart = shortTime(plan.breakStart) ?? ''
  const breakEnd = shortTime(plan.breakEnd) ?? ''

  const filled: DayDraft = {
    ...draft,
    isWorking: plan.isWorking,
    startTime: shortTime(plan.startTime) ?? '',
    endTime: shortTime(plan.endTime) ?? '',
    hasBreak: breakStart !== '' && breakEnd !== '',
    breakStart,
    breakEnd,
    breakMinutes: plan.breakMinutes,
    settledFor: draft.date,
  }

  // Erkinlik ham rejadan (eski server bermaydi — erkin emas). Sukutga teng
  // «Ish vaqti» `null` bo'lib qoladi: xodim smenani o'zgartirsa, u ham ergashadi.
  const flexible = plan.isWorking && plan.isFlexible === true
  const required = plan.requiredMinutes ?? null

  return {
    ...filled,
    isFlexible: flexible,
    requiredMinutes: flexible && required !== null && required !== defaultRequired(filled) ? required : null,
  }
}

/** `after` dan keyingi, ro'yxatda hali yo'q birinchi sana. */
function nextFreeDate(after: string, taken: readonly string[]): string {
  let candidate = addDays(after, 1)
  for (let guard = 0; guard < 400 && taken.indexOf(candidate) >= 0; guard += 1) {
    candidate = addDays(candidate, 1)
  }
  return candidate
}

function latest(dates: readonly string[]): string {
  return dates.reduce((max, date) => (date > max ? date : max), dates[0] ?? todayIso())
}

/** Blokning xatolari — `touched` bo'lgandagina ko'rsatiladi. */
interface DayErrors {
  date: string | null
  start: string | null
  end: string | null
  breakStart: string | null
  breakEnd: string | null
  /** «Ish vaqti» — erkin kunda. */
  required: string | null
}

type Translate = (key: string, options?: Record<string, unknown>) => string

function dayErrors(t: Translate, draft: DayDraft, duplicate: boolean): DayErrors {
  const errors: DayErrors = { date: null, start: null, end: null, breakStart: null, breakEnd: null, required: null }

  if (duplicate) errors.date = t('schedule.dateDuplicate')
  if (!draft.isWorking) return errors

  // Qoidalar `WorkScheduleDay.Validate` niki: tugash boshlanishdan oldin
  // bo'lishi MUMKIN (tungi smena), faqat teng bo'lmasin.
  if (draft.startTime === '') errors.start = t('schedule.timeRequired')
  if (draft.endTime === '') errors.end = t('schedule.timeRequired')
  else if (draft.startTime !== '' && draft.startTime === draft.endTime) errors.end = t('schedule.timesEqual')

  if (draft.hasBreak) {
    if (draft.breakStart === '') errors.breakStart = t('schedule.timeRequired')
    if (draft.breakEnd === '') errors.breakEnd = t('schedule.timeRequired')
    else if (
      draft.breakStart !== '' &&
      draft.startTime !== '' &&
      draft.endTime !== '' &&
      draft.startTime !== draft.endTime &&
      !breakInsideShift(draft.startTime, draft.endTime, draft.breakStart, draft.breakEnd)
    ) {
      errors.breakEnd = t('schedule.breakOutside')
    }
  }

  // «Ish vaqti» — serverdagi tartibda ENG OXIRGI qoida (SPEC-erkin-jadval
  // §2.5): smena va tanaffus to'g'ri bo'lgandagina, aks holda chegarasi
  // (smena − tanaffus) hali ma'lum emas.
  if (draft.isFlexible && !errors.start && !errors.end && !errors.breakStart && !errors.breakEnd) {
    const max = defaultRequired(draft)

    if (max !== null) {
      const value = draft.requiredMinutes ?? max

      if (max < MIN_REQUIRED_MINUTES) {
        // Tanaffus smenadan uzun — bu kunni erkin qilib bo'lmaydi
        errors.required = t('errors.davomat.jadval_erkin_vaqti')
      } else if (value < MIN_REQUIRED_MINUTES || value > max) {
        errors.required = t('schedule.requiredRange', { max: hoursMinutes(max) })
      }
    }
  }

  return errors
}

function hasAnyError(errors: DayErrors): boolean {
  return Boolean(
    errors.date || errors.start || errors.end || errors.breakStart || errors.breakEnd || errors.required,
  )
}

/** Juftning xatolari: kun o'zi bilan va boshqa juftda takrorlangan sana. */
function pairErrors(
  t: Translate,
  pairs: readonly PairDraft[],
  index: number,
): { date: string | null; withDate: string | null } {
  const pair = pairs[index]
  if (!pair) return { date: null, withDate: null }

  const earlier = pairs.slice(0, index).reduce<string[]>((list, item) => list.concat([item.date, item.withDate]), [])

  return {
    date: earlier.indexOf(pair.date) >= 0 ? t('schedule.swapDuplicate') : null,
    withDate:
      pair.withDate === pair.date
        ? t('schedule.swapSame')
        : earlier.indexOf(pair.withDate) >= 0
          ? t('schedule.swapDuplicate')
          : null,
  }
}

function toInput(draft: DayDraft): ScheduleChangeDayInput {
  const withBreak = draft.isWorking && draft.hasBreak
  const flexible = draft.isWorking && draft.isFlexible

  return {
    date: draft.date,
    isWorking: draft.isWorking,
    startTime: draft.isWorking ? wireTime(draft.startTime) : null,
    endTime: draft.isWorking ? wireTime(draft.endTime) : null,
    breakStart: withBreak ? wireTime(draft.breakStart) : null,
    breakEnd: withBreak ? wireTime(draft.breakEnd) : null,
    // Dam olish kunida server baribir 0 yozadi
    breakMinutes: draft.isWorking ? draft.breakMinutes : null,
    isFlexible: flexible,
    // `null` — sukut: server smena − tanaffusni o'zi yozadi
    requiredMinutes: flexible ? draft.requiredMinutes : null,
  }
}

/** Yuborishda birinchi xatoli maydonga — uzun formada xato ekrandan tashqarida qolmasin (UX §E.5). */
function scrollToFirstError(): void {
  window.setTimeout(() => {
    const first = document.querySelector('[data-invalid]')
    if (first) first.scrollIntoView({ block: 'center' })
  }, 0)
}

/**
 * JADVALNI O'ZGARTIRISH so'rovi (SPEC-jadval-sorovi §C4) — Verifix'dagi
 * «Запрос на изменение рабочего графика».
 *
 * Ikki tur: «Kunni o'zgartirish» — bir yoki bir necha sanaga yangi ish
 * vaqti (yoki dam olish kuni, yoki «Erkin ish jadvali»); «Kun almashtirish» —
 * ikki sananing rejalari o'rin almashadi (rejani server o'zi oladi).
 * Tasdiqlanmaguncha hisobga hech narsa ta'sir qilmaydi.
 *
 * Varaqdagi ikki qator shu bitta ekranni ochadi (SPEC-erkin-jadval §7.2):
 * `?tur=almashtirish` — «Kun almashtirish», qolgani — «Jadval o'zgartirish».
 * Segment joyida qoladi, sarlavha turga ergashadi.
 */
export function ScheduleRequestNewScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const errorText = useErrorText()
  const [params] = useSearchParams()

  const prefill = parseIsoDate(params.get('kun')) ? `${params.get('kun')}` : todayIso()

  const [kind, setKind] = useState<ScheduleChangeKind>(() =>
    params.get('tur') === 'almashtirish' ? 'Swap' : 'Change',
  )
  const [days, setDays] = useState<DayDraft[]>(() => [emptyDay(prefill)])
  const [pairs, setPairs] = useState<PairDraft[]>(() => [
    { key: nextKey(), date: prefill, withDate: addDays(prefill, 1) },
  ])
  const [note, setNote] = useState('')

  const [edited, setEdited] = useState(false)
  const [touched, setTouched] = useState(false)
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState<ApiError | null>(null)
  const [leaveOpen, setLeaveOpen] = useState(false)

  const tr: Translate = (key, options) => t(key, options)

  const dayErrorList = days.map((draft, index) =>
    dayErrors(tr, draft, days.slice(0, index).some((other) => other.date === draft.date)),
  )
  const pairErrorList = pairs.map((_pair, index) => pairErrors(tr, pairs, index))

  const valid =
    kind === 'Change'
      ? !dayErrorList.some(hasAnyError)
      : !pairErrorList.some((errors) => errors.date !== null || errors.withDate !== null)

  // Reja kelganda — tahrir emas: «iflos» bayrog'i tegilmaydi.
  const settleDay = (key: number, next: DayDraft): void => {
    setDays((list) => list.map((item) => (item.key === key ? next : item)))
  }

  const editDay = (key: number, next: DayDraft): void => {
    setEdited(true)
    settleDay(key, next)
  }

  const addDay = (): void => {
    setEdited(true)
    setDays((list) => {
      const taken = list.map((item) => item.date)
      return list.concat([emptyDay(nextFreeDate(latest(taken), taken))])
    })
  }

  const removeDay = (key: number): void => {
    setEdited(true)
    setDays((list) => (list.length > 1 ? list.filter((item) => item.key !== key) : list))
  }

  const editPair = (key: number, next: PairDraft): void => {
    setEdited(true)
    setPairs((list) => list.map((item) => (item.key === key ? next : item)))
  }

  const addPair = (): void => {
    setEdited(true)
    setPairs((list) => {
      const taken = list.reduce<string[]>((all, item) => all.concat([item.date, item.withDate]), [])
      const date = nextFreeDate(latest(taken), taken)
      return list.concat([{ key: nextKey(), date, withDate: nextFreeDate(date, taken.concat([date])) }])
    })
  }

  const removePair = (key: number): void => {
    setEdited(true)
    setPairs((list) => (list.length > 1 ? list.filter((item) => item.key !== key) : list))
  }

  const submit = async (): Promise<void> => {
    setTouched(true)
    if (!valid) {
      scrollToFirstError()
      return
    }

    setSending(true)
    setSendError(null)

    const trimmed = note.trim() === '' ? null : note.trim()
    const body: CreateScheduleChangeRequest =
      kind === 'Change'
        ? { kind, days: days.map(toInput), swaps: [], note: trimmed }
        : {
            kind,
            days: [],
            swaps: pairs.map((pair) => ({ date: pair.date, withDate: pair.withDate })),
            note: trimmed,
          }

    try {
      await api.createScheduleRequest(body)

      invalidateScheduleRequests()
      toast.show(t('requests.sent'), 'success')
      navigate('/sorovlar', { replace: true })
    } catch (cause: unknown) {
      setSendError(ApiError.from(cause))
    } finally {
      setSending(false)
    }
  }

  const goBack = (): void => {
    if (edited || note.length > 0) {
      setLeaveOpen(true)
      return
    }
    navigate(-1)
  }

  return (
    <>
      <PushedScreen
        title={kind === 'Swap' ? t('schedule.newTitleSwap') : t('schedule.newTitle')}
        onBack={goBack}
        bottomSpace
      >
        <Card>
          <SegmentedControl<ScheduleChangeKind>
            label={t('schedule.kind')}
            value={kind}
            options={[
              { value: 'Change', label: t('schedule.kindChange') },
              { value: 'Swap', label: t('schedule.kindSwap') },
            ]}
            onChange={setKind}
          />
          <p className="mt-3 text-footnote text-fg-secondary">
            {kind === 'Change' ? t('schedule.changeHint') : t('schedule.swapHint')}
          </p>
        </Card>

        {kind === 'Change' ? (
          <>
            {days.map((draft, index) => (
              <DayBlock
                key={draft.key}
                draft={draft}
                number={index + 1}
                showHeader={days.length > 1}
                errors={touched ? dayErrorList[index] ?? null : null}
                onSettle={(next) => settleDay(draft.key, next)}
                onEdit={(next) => editDay(draft.key, next)}
                onRemove={days.length > 1 ? () => removeDay(draft.key) : undefined}
              />
            ))}

            {days.length < MAX_DAYS ? (
              <Button variant="secondary" block onClick={addDay}>
                {t('schedule.addDate')}
              </Button>
            ) : (
              <p className="px-1 text-footnote text-fg-tertiary">{t('schedule.maxDates', { n: MAX_DAYS })}</p>
            )}
          </>
        ) : (
          <>
            {pairs.map((pair, index) => (
              <PairBlock
                key={pair.key}
                pair={pair}
                number={index + 1}
                showHeader={pairs.length > 1}
                errors={touched ? pairErrorList[index] ?? null : null}
                onEdit={(next) => editPair(pair.key, next)}
                onRemove={pairs.length > 1 ? () => removePair(pair.key) : undefined}
              />
            ))}

            {pairs.length < MAX_PAIRS ? (
              <Button variant="secondary" block onClick={addPair}>
                {t('schedule.addPair')}
              </Button>
            ) : (
              <p className="px-1 text-footnote text-fg-tertiary">{t('schedule.maxPairs', { n: MAX_PAIRS })}</p>
            )}
          </>
        )}

        <Card>
          <Field label={t('requests.note')}>
            <TextArea value={note} onChange={setNote} placeholder={t('requests.notePlaceholder')} />
          </Field>
        </Card>

        {sendError ? (
          <InlineMessage tone="danger" title={t('requests.sendFailed')} assertive>
            {errorText(sendError).body}
          </InlineMessage>
        ) : null}
      </PushedScreen>

      <BottomAction>
        <Button block busy={sending} onClick={() => void submit()}>
          {t('common.submit')}
        </Button>
      </BottomAction>

      <ConfirmSheet
        open={leaveOpen}
        title={t('requests.dirtyTitle')}
        confirmLabel={t('requests.dirtyLeave')}
        cancelLabel={t('common.stay')}
        danger
        onConfirm={() => {
          setLeaveOpen(false)
          navigate(-1)
        }}
        onClose={() => setLeaveOpen(false)}
      />
    </>
  )
}

/** Blok sarlavhasi: «2-sana» va «Olib tashlash» — faqat bir nechta blok bo'lganda. */
function BlockHeader({ title, onRemove }: { title: string; onRemove?: () => void }) {
  const { t } = useTranslation()

  return (
    <div className="-mt-2 mb-2 flex min-h-[44px] items-center justify-between">
      <h2 className="text-subhead font-semibold text-fg-secondary">{title}</h2>
      {onRemove ? (
        <button
          type="button"
          aria-label={`${title} — ${t('schedule.remove')}`}
          onClick={onRemove}
          className="-mr-2 min-h-[44px] rounded-control px-2 text-subhead font-medium text-danger-text active:bg-bg-pressed"
        >
          {t('schedule.remove')}
        </button>
      ) : null}
    </div>
  )
}

/**
 * «Kunni o'zgartirish» dagi bitta sana. Sana tanlanganda shu sananing
 * AMALDAGI rejasi (`/api/me/plan`) olinadi va vaqtlar undan to'ladi —
 * Verifix'da ham xodim tayyor jadvaldan boshlaydi, bo'sh formadan emas.
 */
function DayBlock({
  draft,
  number,
  showHeader,
  errors,
  onSettle,
  onEdit,
  onRemove,
}: {
  draft: DayDraft
  number: number
  showHeader: boolean
  errors: DayErrors | null
  onSettle: (next: DayDraft) => void
  onEdit: (next: DayDraft) => void
  onRemove?: () => void
}) {
  const { t } = useTranslation()
  const plan = usePlan(draft.date)

  const planForDate = plan.data && plan.data.date === draft.date ? plan.data : null
  const settled = draft.settledFor === draft.date
  const planFailed = plan.error !== null && planForDate === null

  // Reja kelishi bilan blok to'ladi — bir marta, shu sana uchun.
  useEffect(() => {
    if (planForDate && !settled) onSettle(applyPlan(draft, planForDate))
  }, [planForDate, settled, draft, onSettle])

  // Xodim tegsa — blok o'rnashgan: kechikib kelgan reja uning vaqtini
  // ustidan yozmaydi.
  const edit = (patch: Partial<DayDraft>): void => onEdit({ ...draft, ...patch, settledFor: draft.date })

  // Tanaffusga tegildi — jadvaldagi uzunlik endi bu oynaga tegishli emas: oynani
  // o'zi tanlagan xodim uni tanaffusning o'zi deb biladi, server oyna uzunligini
  // oladi. Varaqni o'sha vaqt bilan tasdiqlash — tegish emas.
  const editBreak = (patch: Pick<Partial<DayDraft>, 'hasBreak' | 'breakStart' | 'breakEnd'>): void => {
    const changed =
      (patch.hasBreak !== undefined && patch.hasBreak !== draft.hasBreak) ||
      (patch.breakStart !== undefined && patch.breakStart !== draft.breakStart) ||
      (patch.breakEnd !== undefined && patch.breakEnd !== draft.breakEnd)

    edit(changed ? { ...patch, breakMinutes: null } : patch)
  }

  // Saqlanayotgan uzunlik oynadan farq qilsa — xodim nima yuborilishini ko'rsin.
  const keptBreak =
    draft.breakMinutes !== null &&
    breakDiffersFromWindow({
      isWorking: draft.isWorking,
      breakStart: draft.hasBreak ? draft.breakStart : null,
      breakEnd: draft.hasBreak ? draft.breakEnd : null,
      breakMinutes: draft.breakMinutes,
    })
      ? draft.breakMinutes
      : null

  const current =
    planForDate && planForDate.hasSchedule ? t('schedule.currentPlan', { plan: dayPlanText(t, planForDate) }) : undefined

  // «Ish vaqti»: tanlanmagan bo'lsa — sukut (smena − tanaffus), u smena bilan birga o'zgaradi
  const maxRequired = defaultRequired(draft)
  const requiredValue = draft.requiredMinutes ?? maxRequired ?? 0
  const requiredHint =
    maxRequired !== null
      ? t('schedule.requiredHint', { range: `${draft.startTime}–${draft.endTime}`, max: hoursMinutes(maxRequired) })
      : undefined

  return (
    <Card>
      {showHeader ? <BlockHeader title={t('schedule.dayNumber', { n: number })} onRemove={onRemove} /> : null}

      <div className="space-y-4">
        <Field label={t('schedule.date')} required error={errors ? errors.date : null} helper={current}>
          <DateRow
            sheetTitle={t('schedule.date')}
            value={draft.date}
            invalid={errors !== null && errors.date !== null}
            onChange={(iso) => {
              if (iso !== draft.date) onEdit({ ...draft, date: iso, settledFor: null })
            }}
          />
        </Field>

        {!settled && !planFailed ? (
          // Reja kelguncha — skelet: bo'sh vaqtlar ko'rinib, keyin
          // birdan to'lib qolsa, xodim o'zi yozganini yo'qotgandek bo'lardi.
          <div className="space-y-3">
            <Skeleton className="h-11 w-full" />
            <SkeletonLines rows={2} />
          </div>
        ) : (
          <>
            {planFailed && !settled ? (
              <InlineMessage
                tone="warning"
                action={
                  <Button variant="secondary" size="compact" onClick={plan.refetch}>
                    {t('common.retry')}
                  </Button>
                }
              >
                {t('schedule.planFailedManual')}
              </InlineMessage>
            ) : null}

            <SegmentedControl<'work' | 'off'>
              label={t('schedule.dayTypeLabel')}
              value={draft.isWorking ? 'work' : 'off'}
              options={[
                { value: 'work', label: t('schedule.working') },
                { value: 'off', label: t('schedule.dayOff') },
              ]}
              onChange={(value) => edit({ isWorking: value === 'work' })}
            />

            {draft.isWorking ? (
              <>
                <Field label={t('schedule.startTime')} required error={errors ? errors.start : null}>
                  <TimeRow
                    sheetTitle={t('schedule.startTime')}
                    value={draft.startTime}
                    placeholder={t('common.select')}
                    invalid={errors !== null && errors.start !== null}
                    onChange={(value) => edit({ startTime: value })}
                  />
                </Field>

                <Field
                  label={t('schedule.endTime')}
                  required
                  error={errors ? errors.end : null}
                  helper={crossesMidnight(draft.startTime, draft.endTime) ? t('schedule.nightShift') : undefined}
                >
                  <TimeRow
                    sheetTitle={t('schedule.endTime')}
                    value={draft.endTime}
                    placeholder={t('common.select')}
                    invalid={errors !== null && errors.end !== null}
                    onChange={(value) => edit({ endTime: value })}
                  />
                </Field>

                <SwitchRow
                  label={t('schedule.break')}
                  checked={draft.hasBreak}
                  onChange={(checked) => editBreak({ hasBreak: checked })}
                />

                {draft.hasBreak ? (
                  <>
                    <Field label={t('schedule.breakStart')} required error={errors ? errors.breakStart : null}>
                      <TimeRow
                        sheetTitle={t('schedule.breakStart')}
                        value={draft.breakStart}
                        placeholder={t('common.select')}
                        invalid={errors !== null && errors.breakStart !== null}
                        onChange={(value) => editBreak({ breakStart: value })}
                      />
                    </Field>

                    <Field label={t('schedule.breakEnd')} required error={errors ? errors.breakEnd : null}>
                      <TimeRow
                        sheetTitle={t('schedule.breakEnd')}
                        value={draft.breakEnd}
                        placeholder={t('common.select')}
                        invalid={errors !== null && errors.breakEnd !== null}
                        onChange={(value) => editBreak({ breakEnd: value })}
                      />
                    </Field>
                  </>
                ) : null}

                {keptBreak !== null ? (
                  <p className="nums text-footnote text-fg-tertiary">{t('schedule.breakKept', { n: keptBreak })}</p>
                ) : null}

                {/* «Erkin ish jadvali» (SPEC-erkin-jadval §7.2): kun «Ish vaqti» bo'yicha
                    baholanadi, kechikish va erta ketish hisoblanmaydi. Smena — faqat
                    ma'lumot uchun oyna: undan tashqaridagi vaqt ham sanaladi (egasi). */}
                <SwitchRow
                  label={t('schedule.flexible')}
                  checked={draft.isFlexible}
                  onChange={(checked) => edit({ isFlexible: checked })}
                />

                {draft.isFlexible ? (
                  <Field
                    label={t('schedule.required')}
                    required
                    error={errors ? errors.required : null}
                    helper={requiredHint}
                  >
                    <DurationRow
                      sheetTitle={t('schedule.required')}
                      value={requiredValue}
                      hoursLabel={t('schedule.durationHours')}
                      minutesLabel={t('schedule.durationMinutes')}
                      invalid={errors !== null && errors.required !== null}
                      // Sukutning o'zi tanlansa — yana sukut (`null`), smenaga ergashadi
                      onChange={(minutes) => edit({ requiredMinutes: minutes === maxRequired ? null : minutes })}
                    />
                  </Field>
                ) : null}
              </>
            ) : null}
          </>
        )}

        {planForDate && !planForDate.hasSchedule ? (
          <p className="text-footnote text-fg-tertiary">{t('schedule.noSchedule')}</p>
        ) : null}
      </div>
    </Card>
  )
}

/** «Kun almashtirish» jufti: ikki sana va har birining hozirgi rejasi. */
function PairBlock({
  pair,
  number,
  showHeader,
  errors,
  onEdit,
  onRemove,
}: {
  pair: PairDraft
  number: number
  showHeader: boolean
  errors: { date: string | null; withDate: string | null } | null
  onEdit: (next: PairDraft) => void
  onRemove?: () => void
}) {
  const { t } = useTranslation()

  return (
    <Card>
      {showHeader ? <BlockHeader title={t('schedule.pairNumber', { n: number })} onRemove={onRemove} /> : null}

      <div className="space-y-4">
        <Field label={t('schedule.date')} required error={errors ? errors.date : null}>
          <DateRow
            sheetTitle={t('schedule.date')}
            value={pair.date}
            invalid={errors !== null && errors.date !== null}
            onChange={(iso) => onEdit({ ...pair, date: iso })}
          />
          <PlanLine date={pair.date} />
        </Field>

        <div className="text-center text-title3 text-fg-tertiary" aria-hidden="true">
          ⇅
        </div>

        <Field label={t('schedule.withDate')} required error={errors ? errors.withDate : null}>
          <DateRow
            sheetTitle={t('schedule.withDate')}
            value={pair.withDate}
            invalid={errors !== null && errors.withDate !== null}
            onChange={(iso) => onEdit({ ...pair, withDate: iso })}
          />
          <PlanLine date={pair.withDate} />
        </Field>
      </div>
    </Card>
  )
}

/**
 * Sananing hozirgi rejasi bir satrda — almashtirishdan oldin xodim nima
 * bilan nimani almashtirayotganini ko'rsin. Jadval yo'q sanani server
 * rad etadi (`davomat.jadval_sorovi_jadval_yoq`), shu sababli bu yerda
 * oldindan aytiladi.
 */
function PlanLine({ date }: { date: string }) {
  const { t } = useTranslation()
  const plan = usePlan(date)
  const data = plan.data && plan.data.date === date ? plan.data : null

  if (plan.isLoading && !data) {
    return <Skeleton className="mt-2 h-4 w-40" />
  }

  if (!data) {
    return plan.error ? (
      <div className="mt-1.5 flex min-w-0 items-center">
        <p className="min-w-0 flex-1 text-footnote text-fg-tertiary">{t('schedule.planFailed')}</p>
        <Button variant="plain" size="compact" onClick={plan.refetch}>
          {t('common.retry')}
        </Button>
      </div>
    ) : null
  }

  if (!data.hasSchedule) {
    return <p className="mt-1.5 text-footnote text-danger-text">{t('schedule.swapNoSchedule')}</p>
  }

  return (
    <p className="nums mt-1.5 text-footnote text-fg-secondary">
      {t('schedule.currentPlan', { plan: dayPlanText(t, data) })}
    </p>
  )
}
