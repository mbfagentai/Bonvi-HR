import { CopyCheck, Eraser, LoaderCircle, MoonStar } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Badge, Button, Dialog, Switch, TextArea, TextField } from '@/shared/ui'

import type { WorkSchedule, WorkScheduleDayInput } from '../api/attendance-api'
import { useSaveSchedule } from '../api/attendance-queries'
import {
  WEEKDAY_INDEXES,
  WEEKDAY_LABEL_KEY,
  breakWindowCrossesMidnight,
  breakWindowLabel,
  crossesMidnight,
  formatDuration,
  weekSummary,
  type WeekdayIndex,
} from '../lib/attendance-presentation'
import {
  FLEXIBLE_REQUIRED_CODE,
  MAX_BREAK_MINUTES,
  MAX_GRACE_MINUTES,
  NOT_FLEXIBLE,
  SCHEDULE_NAME_MAX,
  SCHEDULE_NOTE_MAX,
  breakWindowPlacement,
  clockMinutes,
  flexibleDraftOf,
  isValidGrace,
  maxRequiredMinutes,
  plannedDayMinutes,
  requiredMinutesOf,
  validateScheduleDay,
  type FlexibleDraft,
} from '../lib/attendance-schedule-rules'
import { FlexibleDayFields } from './attendance-flexible-day-fields'

/**
 * Bir kunning FORMA holati — hammasi satr.
 *
 * NEGA `WorkScheduleDayInput` ning o'zi emas: `<input>` bo'sh satr
 * beradi, tipda esa `null` va son. Har bosishda o'girish kursorni
 * sakratadi va «6» ni yozib bo'lmaydigan «60» qilib qo'yardi. O'girish
 * bitta joyda — `toDayInput`.
 *
 * Dam olish kunining qiymatlari ham SAQLANIB turadi (yuborilmaydi):
 * operator kunni o'chirib-yoqsa, yozgan vaqtlari qaytib keladi.
 *
 * «Erkin ish jadvali» va «Ish vaqti» — `FlexibleDraft` maydonlari
 * (SPEC-erkin-jadval §7.1).
 */
interface DayDraft extends FlexibleDraft {
  dayIndex: WeekdayIndex
  isWorking: boolean
  startTime: string
  endTime: string
  breakMinutes: string
  breakStart: string
  breakEnd: string
}

/** Yangi jadvalning ish kuni — ofisning eng keng tarqalgan 9–18, tushlik 13–14. */
const DEFAULT_WORKDAY = {
  startTime: '09:00',
  endTime: '18:00',
  breakMinutes: '60',
  breakStart: '13:00',
  breakEnd: '14:00',
} as const

const DEFAULT_GRACE = '10'

/** Seshanba–juma: «dushanbani nusxalash» shu kunlarga yoziladi. */
const WEEKDAYS_AFTER_MONDAY: readonly WeekdayIndex[] = [2, 3, 4, 5]

/**
 * Qaysi server kodi qaysi maydonga tegishli. Kun darajasidagi kodlar bu
 * yerda YO'Q: server qaysi kun ekanini aytmaydi (ProblemDetails'da faqat
 * kod) — ular tepadagi ogohlantirishga tushadi va klient tekshiruvi o'sha
 * kodni bergan kun qizil bilan belgilanadi.
 */
const HEADER_FIELD_OF_CODE: Readonly<Record<string, 'name' | 'grace'>> = {
  'davomat.jadval_nom_bosh': 'name',
  'davomat.jadval_nom_takror': 'name',
  'davomat.jadval_yon_berish': 'grace',
}

/**
 * Ish jadvalini yaratish va tahrirlash — bitta oyna (`catalog-form-dialog.tsx`
 * naqshi).
 *
 * Haftalik muharrir: har bir hafta kuni uchun bitta qator — ish kuni
 * almashtirgichi, boshlanish, tugash, tanaffus daqiqalari va TANAFFUS
 * OYNASI (SPEC §2.4.0, AMENDED 2026-09-17).
 *
 * ⭐ Uchta narsa saqlashdan OLDIN ko'rinadi, chunki ularning har biri
 * tabeldagi soatni o'zgartiradi:
 *   · smena yarim tundan o'tsa — «→ ertasi kuni» belgisi va chiziqdagi
 *     yarim tun nuqtasi (SPEC §8.4);
 *   · tanaffus oynasi smenaning QAYERIGA tushadi — chiziqdagi bo'lak;
 *   · oyna YO'Q bo'lsa tanaffus ishlangan vaqtdan UMUMAN ayirilmaydi
 *     (§5.5.1) — buni alohida gap bilan aytadi, aks holda operator
 *     «60 daqiqa yozdim, ayiriladi» deb o'ylab qolardi.
 *
 * Tekshiruv — domen ko'zgusi (`attendance-schedule-rules.ts`); server
 * boshqacha javob bersa, serverning kodi ko'rsatiladi.
 */
export function AttendanceScheduleFormDialog({
  open,
  onClose,
  schedule,
}: {
  open: boolean
  onClose: () => void
  /** `null` — yangi jadval. */
  schedule: WorkSchedule | null
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveSchedule()

  const [name, setName] = useState('')
  const [nameTouched, setNameTouched] = useState(false)
  const [graceIn, setGraceIn] = useState(DEFAULT_GRACE)
  const [graceOut, setGraceOut] = useState(DEFAULT_GRACE)
  const [note, setNote] = useState('')
  const [drafts, setDrafts] = useState<DayDraft[]>(() => defaultDrafts())

  const [failure, setFailure] = useState<string | null>(null)
  const [headerError, setHeaderError] = useState<{ field: 'name' | 'grace'; code: string } | null>(
    null,
  )

  useEffect(() => {
    if (!open) return
    setName(schedule?.name ?? '')
    setNameTouched(false)
    setGraceIn(String(schedule?.graceInMinutes ?? DEFAULT_GRACE))
    setGraceOut(String(schedule?.graceOutMinutes ?? DEFAULT_GRACE))
    setNote(schedule?.note ?? '')
    setDrafts(schedule === null ? defaultDrafts() : draftsOf(schedule))
    setFailure(null)
    setHeaderError(null)
    save.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, schedule])

  const days = useMemo(() => drafts.map(toDayInput), [drafts])
  const dayErrors = useMemo(() => days.map(validateScheduleDay), [days])
  const summary = useMemo(() => weekSummary(days), [days])

  const graceInValue = parseMinutes(graceIn)
  const graceOutValue = parseMinutes(graceOut)
  const graceInvalid = !isValidGrace(graceInValue) || !isValidGrace(graceOutValue)
  const nameEmpty = name.trim() === ''

  const hasClientErrors = nameEmpty || graceInvalid || dayErrors.some((code) => code !== null)
  const pending = save.isPending

  const updateDay = (dayIndex: WeekdayIndex, patch: Partial<DayDraft>) =>
    setDrafts((previous) =>
      previous.map((draft) => (draft.dayIndex === dayIndex ? { ...draft, ...patch } : draft)),
    )

  /**
   * Kun yoqilganda bo'sh vaqtlar birinchi ish kunidan (yoki standartdan)
   * to'ldiriladi: aks holda yangi yoqilgan kun darhol «vaqt kiritilmagan»
   * degan qizil xato bilan chiqib, operatorni cho'chitardi.
   */
  const toggleDay = (draft: DayDraft, working: boolean) => {
    if (!working || (draft.startTime !== '' && draft.endTime !== '')) {
      updateDay(draft.dayIndex, { isWorking: working })
      return
    }

    const template =
      drafts.find((other) => other.isWorking) ?? { ...DEFAULT_WORKDAY, ...NOT_FLEXIBLE }
    updateDay(draft.dayIndex, {
      isWorking: true,
      startTime: template.startTime,
      endTime: template.endTime,
      breakMinutes: template.breakMinutes,
      breakStart: template.breakStart,
      breakEnd: template.breakEnd,
      // Erkinlik ham namunadan: erkin haftaga qo'shilgan kun ham erkin bo'lsin
      isFlexible: template.isFlexible,
      required: template.required,
      requiredAuto: template.requiredAuto,
    })
  }

  const copyMonday = () => {
    const monday = drafts.find((draft) => draft.dayIndex === 1)
    if (monday === undefined) return

    setDrafts((previous) =>
      previous.map((draft) =>
        WEEKDAYS_AFTER_MONDAY.includes(draft.dayIndex) ? { ...monday, dayIndex: draft.dayIndex } : draft,
      ),
    )
  }

  const submit = () => {
    setNameTouched(true)
    if (hasClientErrors) return
    setFailure(null)
    setHeaderError(null)

    save.mutate(
      {
        id: schedule?.id ?? null,
        body: {
          name: name.trim(),
          graceInMinutes: graceInValue,
          graceOutMinutes: graceOutValue,
          note: note.trim() === '' ? null : note.trim(),
          days,
        },
      },
      {
        onSuccess: onClose,
        onError: (error) => {
          const code = problemCode(error)
          const field = HEADER_FIELD_OF_CODE[code]
          if (field === undefined) setFailure(code)
          else setHeaderError({ field, code })
        },
      },
    )
  }

  const nameError =
    headerError?.field === 'name'
      ? errorMessage(headerError.code)
      : nameTouched && nameEmpty
        ? errorMessage('davomat.jadval_nom_bosh')
        : undefined

  /*
    Imtiyoz xatosi buzilgan maydon ostida. Server rad etib, klient ikkalasini
    ham to'g'ri deb bilsa — birinchisi ostida: kod bitta, maydon ikkita.
  */
  const graceInError =
    !isValidGrace(graceInValue) || (headerError?.field === 'grace' && !graceInvalid)
      ? errorMessage(headerError?.field === 'grace' ? headerError.code : 'davomat.jadval_yon_berish')
      : undefined
  const graceOutError = !isValidGrace(graceOutValue)
    ? errorMessage('davomat.jadval_yon_berish')
    : undefined

  const hours = summary.plannedMinutes / 60

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!pending}
      title={schedule === null ? t('attendance.scheduleCreateTitle') : t('attendance.scheduleEditTitle')}
      description={schedule?.name}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={submit}
            disabled={pending || hasClientErrors}
          >
            {pending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}

        <TextField
          label={t('attendance.scheduleName')}
          value={name}
          maxLength={SCHEDULE_NAME_MAX}
          disabled={pending}
          error={nameError}
          onBlur={() => setNameTouched(true)}
          onChange={(event) => {
            setName(event.target.value)
            // «Bunday nom bor» xatosi eski nom haqida edi — yangi harf bilan u eskiradi
            if (headerError?.field === 'name') setHeaderError(null)
          }}
        />

        <div className="space-y-2">
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={t('attendance.graceIn')}
              type="number"
              inputMode="numeric"
              min={0}
              max={MAX_GRACE_MINUTES}
              value={graceIn}
              disabled={pending}
              error={graceInError}
              onChange={(event) => setGraceIn(event.target.value)}
            />
            <TextField
              label={t('attendance.graceOut')}
              type="number"
              inputMode="numeric"
              min={0}
              max={MAX_GRACE_MINUTES}
              value={graceOut}
              disabled={pending}
              error={graceOutError}
              onChange={(event) => setGraceOut(event.target.value)}
            />
          </div>
          <p className="text-footnote text-[var(--fg-tertiary)]">{t('attendance.graceHint')}</p>
        </div>

        {/* ───────────────────────── hafta */}
        <section className="space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="tabular text-subhead font-semibold">
              {t('attendance.weekSummary', {
                days: num(summary.workingDays),
                hours: num(Math.round(hours * 10) / 10),
              })}
            </p>
            <Button type="button" variant="ghost" size="sm" onClick={copyMonday} disabled={pending}>
              <CopyCheck />
              {t('attendance.copyMondayToWeekdays')}
            </Button>
          </div>

          <p className="text-footnote text-[var(--fg-tertiary)]">{t('attendance.breakWindowHint')}</p>

          <ul className="space-y-2.5">
            {drafts.map((draft, index) => {
              const day = days[index]
              if (day === undefined) return null
              return (
                <DayEditor
                  key={draft.dayIndex}
                  draft={draft}
                  day={day}
                  error={dayErrors[index] ?? null}
                  disabled={pending}
                  onToggle={(working) => toggleDay(draft, working)}
                  onChange={(patch) => updateDay(draft.dayIndex, patch)}
                />
              )
            })}
          </ul>
        </section>

        <TextArea
          label={t('employees.note')}
          rows={2}
          value={note}
          maxLength={SCHEDULE_NOTE_MAX}
          disabled={pending}
          onChange={(event) => setNote(event.target.value)}
        />
      </div>
    </Dialog>
  )
}

/* ═══════════════════════════════════════════════════════════ bir kun */

function DayEditor({
  draft,
  day,
  error,
  disabled,
  onToggle,
  onChange,
}: {
  draft: DayDraft
  day: WorkScheduleDayInput
  error: string | null
  disabled: boolean
  onToggle: (working: boolean) => void
  onChange: (patch: Partial<DayDraft>) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const weekday = t(WEEKDAY_LABEL_KEY[draft.dayIndex])
  const overnight = crossesMidnight(day)
  const windowRange = breakWindowLabel(day)
  const planned = plannedDayMinutes(day)
  const hasWindow = draft.breakStart !== '' || draft.breakEnd !== ''
  // Erkin kun xatosi «Ish vaqti» maydonining o'zida — pastdagi qatorda takrorlanmaydi
  const lineError = error === FLEXIBLE_REQUIRED_CODE ? null : error

  return (
    <li
      className={cn(
        'rounded-xl border px-4 py-3',
        day.isWorking ? 'bg-[var(--bg-elevated)]' : 'bg-[var(--bg-sunken)]',
        error !== null ? 'border-ios-red/60' : 'border-[var(--border)]',
      )}
    >
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="text-subhead font-semibold">{weekday}</p>
          <p className="tabular text-caption1 text-[var(--fg-secondary)]">
            {!day.isWorking
              ? t('attendance.dayOff')
              : day.isFlexible
                ? t('attendance.dayPlannedFlexible', { hours: formatDuration(planned) })
                : t('attendance.dayPlanned', { hours: formatDuration(planned) })}
          </p>
        </div>
        <Switch
          checked={draft.isWorking}
          disabled={disabled}
          onChange={onToggle}
          aria-label={`${weekday} — ${t('attendance.working')}`}
        />
      </div>

      {draft.isWorking && (
        <div className="mt-3 space-y-3">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <TextField
              label={t('attendance.startTime')}
              type="time"
              value={draft.startTime}
              disabled={disabled}
              onChange={(event) => onChange({ startTime: event.target.value })}
            />
            <TextField
              label={
                <span className="flex items-center gap-1.5">
                  {t('attendance.endTime')}
                  {/* Oqibat YOZAYOTGAN paytda ko'rinadi, saqlashdan keyin emas (§8.4) */}
                  {overnight && <NextDayMark />}
                </span>
              }
              type="time"
              value={draft.endTime}
              disabled={disabled}
              onChange={(event) => onChange({ endTime: event.target.value })}
            />
            <TextField
              className="col-span-2 sm:col-span-1"
              label={t('attendance.breakMinutes')}
              type="number"
              inputMode="numeric"
              min={0}
              max={MAX_BREAK_MINUTES}
              step={5}
              value={draft.breakMinutes}
              disabled={disabled}
              onChange={(event) => onChange({ breakMinutes: event.target.value })}
            />
          </div>

          <div className="grid grid-cols-[1fr_1fr_auto] items-end gap-3">
            <TextField
              label={t('attendance.breakStart')}
              type="time"
              value={draft.breakStart}
              disabled={disabled}
              onChange={(event) => onChange({ breakStart: event.target.value })}
            />
            <TextField
              label={
                <span className="flex items-center gap-1.5">
                  {t('attendance.breakEnd')}
                  {breakWindowCrossesMidnight(day) && <NextDayMark />}
                </span>
              }
              type="time"
              value={draft.breakEnd}
              disabled={disabled}
              onChange={(event) => onChange({ breakEnd: event.target.value })}
            />
            {/*
              Oynani tozalash — ikkala maydon BIRGA: yarim oyna serverda
              xato (`jadval_tanaffus_toliq_emas`), bitta tugma esa uni
              yaratishning iloji qoldirmaydi.
            */}
            <Button
              type="button"
              variant="ghost"
              size="icon"
              disabled={disabled || !hasWindow}
              title={t('attendance.clearBreakWindow')}
              aria-label={t('attendance.clearBreakWindow')}
              onClick={() => onChange({ breakStart: '', breakEnd: '' })}
            >
              <Eraser />
            </Button>
          </div>

          <ShiftBar day={day} />

          <p className="text-footnote text-[var(--fg-secondary)]">
            {windowRange !== null
              ? t('attendance.breakWindow', { range: windowRange })
              : day.breakMinutes > 0
                ? t('attendance.breakWindowNoneHint')
                : t('attendance.breakWindowNone')}
          </p>

          <FlexibleDayFields
            value={draft}
            max={maxRequiredMinutes(day)}
            code={error}
            disabled={disabled}
            onChange={({ isFlexible, required, requiredAuto }) =>
              onChange({ isFlexible, required, requiredAuto })
            }
          />

          {lineError !== null && (
            <p role="alert" className="text-footnote text-ios-red">
              {errorMessage(lineError)}
            </p>
          )}
        </div>
      )}
    </li>
  )
}

/** «→ ertasi kuni» — yarim tundan o'tgan vaqt yonidagi kichik belgi. */
function NextDayMark() {
  const { t } = useLocale()

  return (
    <Badge tone="purple" size="sm">
      <MoonStar className="size-3" strokeWidth={2.5} />
      {t('attendance.nextDay')}
    </Badge>
  )
}

/**
 * Smena chizig'i — SMENANING O'Z O'QIDA (SPEC §2.4.0.1): chap chet —
 * boshlanish, o'ng chet — tugash, oraliqdagi bo'lak — tanaffus oynasi,
 * ingichka chiziq — yarim tun.
 *
 * NEGA ciferblat o'qida emas: 20:00→08:00 smena 0–24 soatlik chiziqda
 * ikki bo'lakka ajralib, «ertalab ham, kechqurun ham ishlaydi» degandek
 * ko'rinardi. Smena o'qida u bitta yaxlit bo'lak, 00:30 dagi tanaffus esa
 * uning o'rtasida — operator nimani kutsa, aynan shu.
 *
 * SVG atributlari bilan chiziladi (`x`, `width`), `style` bilan emas:
 * kenglik foizi dinamik, Tailwind sinfi esa statik bo'lishi shart.
 */
function ShiftBar({ day }: { day: WorkScheduleDayInput }) {
  const start = clockMinutes(day.startTime)
  const end = clockMinutes(day.endTime)
  if (!day.isWorking || start === null || end === null || start === end) return null

  const length = (end - start + 24 * 60) % (24 * 60)
  const placement = breakWindowPlacement(day)
  const midnight = crossesMidnight(day) ? (24 * 60 - start) % (24 * 60) : null
  // Yarim tun chizig'i — smena uzunligining ~0,6 % i: tor ekranda ham ko'rinadi, keng ekranda qalin emas
  const tick = length / 160

  return (
    <span className="block overflow-hidden rounded-full" aria-hidden>
      <svg
        viewBox={`0 0 ${length} 10`}
        preserveAspectRatio="none"
        className="block h-2 w-full"
      >
        <rect x={0} y={0} width={length} height={10} className="fill-ios-green/25" />
        {placement !== null && placement.inside && (
          <rect
            x={placement.start}
            y={0}
            width={placement.end - placement.start}
            height={10}
            className="fill-ios-teal"
          />
        )}
        {midnight !== null && midnight > 0 && (
          <rect x={midnight - tick / 2} y={0} width={tick} height={10} className="fill-ios-purple" />
        )}
      </svg>
    </span>
  )
}

/* ═══════════════════════════════════════════════════════════ o'girishlar */

function defaultDrafts(): DayDraft[] {
  return WEEKDAY_INDEXES.map((dayIndex) => ({
    dayIndex,
    isWorking: dayIndex <= 5,
    ...DEFAULT_WORKDAY,
    ...NOT_FLEXIBLE,
  }))
}

function draftsOf(schedule: WorkSchedule): DayDraft[] {
  return WEEKDAY_INDEXES.map((dayIndex) => {
    const day = schedule.days.find((candidate) => candidate.dayIndex === dayIndex)
    /*
      Haftalik jadvalda 7 ta qator DOIM bor (SPEC §2.4). Qator yo'q
      bo'lsa — dam olish kuni sifatida ko'rsatiladi: saqlashda 7 ta kun
      baribir yuboriladi va jadval o'z-o'zidan tuzaladi.
    */
    if (day === undefined) {
      return { dayIndex, isWorking: false, ...DEFAULT_WORKDAY, ...NOT_FLEXIBLE }
    }

    return {
      dayIndex,
      isWorking: day.isWorking,
      startTime: day.startTime ?? '',
      endTime: day.endTime ?? '',
      breakMinutes: String(day.breakMinutes),
      breakStart: day.breakStart ?? '',
      breakEnd: day.breakEnd ?? '',
      ...flexibleDraftOf(
        day.isWorking && day.isFlexible,
        day.requiredMinutes,
        maxRequiredMinutes(day),
      ),
    }
  })
}

/**
 * Forma → so'rov tanasi.
 *
 * Dam olish kuni vaqtsiz va oynasiz yuboriladi: server ularni baribir
 * TASHLAYDI (§2.4.0), yuborish esa «dam olish kunida 09:00–18:00» degan
 * ma'nosiz qiymatni tarmoqda tashirdi.
 */
function toDayInput(draft: DayDraft): WorkScheduleDayInput {
  if (!draft.isWorking) {
    return {
      dayIndex: draft.dayIndex,
      isWorking: false,
      startTime: null,
      endTime: null,
      breakMinutes: 0,
      breakStart: null,
      breakEnd: null,
      isFlexible: false,
      requiredMinutes: null,
    }
  }

  return {
    dayIndex: draft.dayIndex,
    isWorking: true,
    startTime: draft.startTime === '' ? null : draft.startTime,
    endTime: draft.endTime === '' ? null : draft.endTime,
    breakMinutes: parseMinutes(draft.breakMinutes === '' ? '0' : draft.breakMinutes),
    breakStart: draft.breakStart === '' ? null : draft.breakStart,
    breakEnd: draft.breakEnd === '' ? null : draft.breakEnd,
    // `null` — server sukuti (smena − tanaffus); o'qib bo'lmaydigan matn `NaN`, tekshiruv ushlaydi
    isFlexible: draft.isFlexible,
    requiredMinutes: requiredMinutesOf(draft),
  }
}

/** «60» → 60; butun manfiy bo'lmagan son emas — `NaN` (tekshiruv uni ushlaydi). */
function parseMinutes(value: string): number {
  return /^\d+$/u.test(value.trim()) ? Number(value.trim()) : Number.NaN
}
