import { ArrowLeftRight, Plus, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Badge,
  Button,
  Checkbox,
  DateField,
  Dialog,
  SegmentedControl,
  TextArea,
  TextField,
} from '@/shared/ui'

import {
  SCHEDULE_CHANGE_KINDS,
  type RequestBreakRule,
  type ScheduleChangeKind,
  type ScheduleChangeRequest,
  type ScheduleChangeRequestInput,
  type WorkScheduleDayInput,
} from '../api/attendance-api'
import {
  isStaleRequestError,
  useCreateScheduleChangeRequest,
  useScheduleRequestBreakRule,
  useScheduleResolution,
  useUpdateScheduleChangeRequest,
} from '../api/attendance-queries'
import {
  ATTENDANCE_VIEW,
  DAY_SOURCE_LOOK,
  MAX_SCHEDULE_REQUEST_DAYS,
  MAX_SCHEDULE_SWAP_PAIRS,
  SCHEDULE_KIND_LABEL_KEY,
  crossesMidnight,
  isoWeekday,
  scheduleSwapPairs,
} from '../lib/attendance-presentation'
import {
  FLEXIBLE_REQUIRED_CODE,
  NOT_FLEXIBLE,
  breakWindowMinutes,
  flexibleDraftOf,
  maxRequiredMinutes,
  requiredMinutesOf,
  requiredUnparsable,
  scheduleOutcome,
  validateRequestBreak,
  validateScheduleDay,
  type FlexibleDraft,
  type RequestBreakIssue,
} from '../lib/attendance-schedule-rules'
import { breakDiffersFromWindow, useScheduleRequestText } from '../lib/schedule-request-text'
import { FlexibleDayFields } from './attendance-flexible-day-fields'

/** `schedule_change_requests.note` — varchar(2000). */
const NOTE_MAX = 2000

/**
 * Formadagi bitta «kunni o'zgartirish» qatori. Vaqtlar «HH:mm», bo'sh — `''`.
 * «Erkin ish jadvali» va «Ish vaqti» — `FlexibleDraft` maydonlari
 * (SPEC-erkin-jadval §7.1, Verifix formasidagi belgi).
 */
interface ChangeRowDraft extends FlexibleDraft {
  /** React kaliti — sana bo'sh yoki takror bo'lishi mumkin, shu sababli alohida. */
  key: string
  date: string
  isWorking: boolean
  startTime: string
  endTime: string
  hasBreak: boolean
  breakStart: string
  breakEnd: string
  /**
   * Joriy rejadan olingan tanaffus uzunligi, daqiqa. Jadvalda u oynadan
   * alohida (60 daqiqalik tushlik 12:00–15:00 oynasida) va HR tanaffus
   * maydonlariga tegmaguncha aynan shu yuboriladi. `null` — server oyna
   * uzunligini oladi: qo'lda qo'shilgan qator yoki HR oynani o'zi yozgan.
   */
  breakMinutes: number | null
  /**
   * HR shu qatorning kunini o'zi o'zgartirganmi. O'zgartirgan bo'lsa, sana
   * almashganda joriy reja uning qiymatlari ustidan YOZILMAYDI.
   */
  touched: boolean
  /** Qaysi sananing rejasi bilan to'ldirilgan — sana o'zgarsa, qayta to'ldiriladi. */
  filledFor: string
}

/** «Kun almashtirish» jufti. */
interface SwapRowDraft {
  key: string
  date: string
  withDate: string
}

let rowSequence = 0

function nextKey(): string {
  rowSequence += 1
  return `row-${rowSequence}`
}

function emptyChangeRow(date: string): ChangeRowDraft {
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
    touched: false,
    filledFor: '',
    ...NOT_FLEXIBLE,
  }
}

function emptySwapRow(): SwapRowDraft {
  return { key: nextKey(), date: '', withDate: '' }
}

/**
 * Qatorni domen tekshiruvining kirishiga aylantiradi (`validateScheduleDay`).
 *
 * Tanaffus uzunligi alohida so'ralmaydi — rejadan saqlangani bo'lmasa, oynaning
 * o'zidan: serverdagi `ScheduleChangeRequest.BreakMinutes` bilan bir xil
 * (yarim tundan o'tsa ham).
 */
function toDayInput(row: ChangeRowDraft): WorkScheduleDayInput {
  const breakStart = row.isWorking && row.hasBreak && row.breakStart !== '' ? row.breakStart : null
  const breakEnd = row.isWorking && row.hasBreak && row.breakEnd !== '' ? row.breakEnd : null

  return {
    dayIndex: isoWeekday(row.date) ?? 1,
    isWorking: row.isWorking,
    startTime: row.isWorking && row.startTime !== '' ? row.startTime : null,
    endTime: row.isWorking && row.endTime !== '' ? row.endTime : null,
    breakMinutes: row.isWorking ? (row.breakMinutes ?? breakWindowMinutes(breakStart, breakEnd)) : 0,
    breakStart,
    breakEnd,
    // Eng kattasi shu samarali tanaffusdan (`maxRequiredMinutes`) — server formulasi (§2.5)
    isFlexible: row.isWorking && row.isFlexible,
    requiredMinutes: row.isWorking ? requiredMinutesOf(row) : null,
  }
}

/**
 * Saqlangan erkinlik → qator. R eng kattasiga teng bo'lsa maydon sukutda
 * turadi va HR vaqtni o'zgartirsa R ham ergashadi.
 */
function withFlexible(
  row: ChangeRowDraft,
  isFlexible: boolean,
  requiredMinutes: number | null,
): ChangeRowDraft {
  return {
    ...row,
    ...flexibleDraftOf(isFlexible, requiredMinutes, maxRequiredMinutes(toDayInput(row))),
  }
}

/** «HH:mm» → «HH:mm:ss» (server `TimeOnly`); bo'sh — `null`. */
function toWire(value: string | null): string | null {
  return value === null ? null : `${value}:00`
}

/**
 * «Bo'sh maydon» xatolari — faqat «Saqlash» bosilgandan keyin ko'rsatiladi:
 * hali to'ldirilmagan qatorga darhol qizil yozuv chiqarish shovqin.
 * Qolganlari (vaqt teng, oyna smenadan tashqarida) — yozayotgan paytda.
 */
const INCOMPLETE_CODES = new Set(['davomat.jadval_vaqt_bosh', 'davomat.jadval_tanaffus_toliq_emas'])

/**
 * Jadval so'rovini yaratish va tahrirlash oynasi (SPEC-jadval-sorovi §C4) —
 * Verifix «Запрос на изменение рабочего графика».
 *
 * <b>Ikki tur.</b> «Kunni o'zgartirish» — bir yoki bir necha sanaga yangi kun
 * (ish kuni: boshlanish, tugash, ixtiyoriy tanaffus; yoki dam olish kuni).
 * «Kun almashtirish» — ikki sana o'rin almashadi; ularning vaqtini server
 * yaratish paytida AMALDAGI rejadan o'zi oladi, shu sababli juftda faqat
 * sanalar bor.
 *
 * <b>Joriy reja oldindan to'ldiriladi.</b> Sana tanlanganda shu sanadagi reja
 * `GET /schedules/resolve` dan o'qiladi va qator shu bilan to'ladi: Verifix'dagi
 * 13 ming so'rovning aksari «shu kungi tanaffusni 12:23–14:23 ga ko'chirish»,
 * ya'ni HR rejaning faqat bitta joyini o'zgartiradi — qolganini qayta
 * terishga majbur bo'lmasligi kerak.
 *
 * <b>Tanaffus qoidasi</b> (SPEC-sorov-tahriri §4): «Kunni o'zgartirish» ning
 * ish kunida tanaffus ruxsat etilgan oraliq ichida va belgilangan uzunlikda,
 * jumada esa oraliqli uzunlikda (sukut: 12:00–15:00, 60 daqiqa, jumada 60–120;
 * sonlar sozlamadan, `GET /schedule-requests/break-rule`); tanaffussiz kun
 * ham mumkin. Qoida HR ga ham amal qiladi (§12 Q6) — forma uni server bilan
 * bir xil tekshiradi va xatoni yozayotgan paytda aytadi. Qoida hali kelmagan
 * bo'lsa klient tekshiruvi yo'q, server baribir tekshiradi.
 *
 * <b>Tahrir — HR ko'rgan nusxa ustida</b>: tana `version` bilan ketadi, 409
 * da oyna sababini aytadi va «Saqlash» ni o'chiradi (`AbsenceRequestDialog`
 * izohi).
 */
export function ScheduleRequestDialog({
  open,
  employeeId,
  request,
  onClose,
}: {
  open: boolean
  /** Yangi so'rov uchun xodim — tahrirlashda `request` dan olinadi. */
  employeeId: string | null
  /** Tahrirlanayotgan (kutayotgan) so'rov; `null` — yangisi. */
  request: ScheduleChangeRequest | null
  onClose: () => void
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const create = useCreateScheduleChangeRequest()
  const update = useUpdateScheduleChangeRequest()

  // Endpoint `attendance.view` ostida — busiz so'rov 403 bo'lardi (server baribir tekshiradi)
  const breakRule = useScheduleRequestBreakRule(open && hasPermission(ATTENDANCE_VIEW)).data

  const [kind, setKind] = useState<ScheduleChangeKind>('Change')
  const [changeRows, setChangeRows] = useState<ChangeRowDraft[]>([])
  const [swapRows, setSwapRows] = useState<SwapRowDraft[]>([])
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  /** Server «nusxa eskirgan» dedi — saqlash endi ma'nosiz. */
  const [stale, setStale] = useState(false)
  /** «Saqlash» bosildimi — bo'sh maydon xatolari shundan keyin ko'rinadi. */
  const [attempted, setAttempted] = useState(false)

  const subjectId = request?.employeeId ?? employeeId ?? ''

  /*
    Oyna har ochilganda forma qayta to'ldiriladi (`AbsenceRequestDialog`
    naqshi). Ikkala turning qatorlari ALOHIDA saqlanadi: HR tur almashtirib
    ko'rib, qaytib kelsa — yozgani joyida turadi.

    Tahrirlashda qatorlar `touched` bilan keladi: saqlangan so'rovning
    vaqtlari joriy reja bilan almashtirilmasin. Tanaffus uzunligi ham
    saqlanganidan: PUT uni qaytarmasa, server oyna uzunligini olib, jadvaldan
    kelgan 60 daqiqani 12:00–15:00 oynasida 180 ga aylantirardi.
  */
  useEffect(() => {
    if (!open) return

    setKind(request?.kind ?? 'Change')
    setChangeRows(
      request !== null && request.kind === 'Change'
        ? request.days.map((day) =>
            withFlexible(
              {
                key: nextKey(),
                date: day.date,
                isWorking: day.isWorking,
                startTime: day.startTime ?? '',
                endTime: day.endTime ?? '',
                hasBreak: day.breakStart !== null && day.breakEnd !== null,
                breakStart: day.breakStart ?? '',
                breakEnd: day.breakEnd ?? '',
                breakMinutes: day.isWorking ? day.breakMinutes : null,
                touched: true,
                filledFor: day.date,
                ...NOT_FLEXIBLE,
              },
              day.isWorking && day.isFlexible,
              day.requiredMinutes,
            ),
          )
        : // Yangi so'rov — bugungi sana bilan: HR odatda shu kunning tanaffusini ko'chiradi
          [emptyChangeRow(todayIso())],
    )
    setSwapRows(
      request !== null && request.kind === 'Swap'
        ? scheduleSwapPairs(request.days).map((pair) => ({ key: nextKey(), ...pair }))
        : [emptySwapRow()],
    )
    setNote(request?.note ?? '')
    setError(null)
    setStale(false)
    setAttempted(false)
  }, [open, request])

  const saving = create.isPending || update.isPending

  /* ─────────────────────────────────────────── tekshiruv (server ko'zgusi) */

  /** Bir necha qatorda uchragan sanalar — `davomat.jadval_sorovi_sana_takror`. */
  const repeatedChangeDates = useMemo(
    () => repeated(changeRows.map((row) => row.date)),
    [changeRows],
  )
  const repeatedSwapDates = useMemo(
    () => repeated(swapRows.flatMap((row) => [row.date, row.withDate])),
    [swapRows],
  )

  /**
   * Tanaffus qoidasining buzilishi — so'z bilan, qoidaning o'z sonlari bilan
   * («aniq 60 daqiqa», «jumada 60–120»). Server ikkita kod bilan javob beradi,
   * forma esa HR nimani tuzatishi kerakligini aniq aytadi.
   */
  const breakRuleMessage = (issue: RequestBreakIssue, rule: RequestBreakRule): string => {
    switch (issue.reason) {
      case 'window':
        return t('attendance.requestBreakWindowRule', { from: rule.from, to: rule.to })
      case 'windowShort':
        return t('attendance.requestBreakWindowShort', { n: num(issue.minutes) })
      case 'lengthFriday':
        return t('attendance.requestBreakLengthRuleFriday', {
          min: num(rule.minutes),
          max: num(rule.fridayMaxMinutes),
        })
      case 'length':
        return t('attendance.requestBreakLengthRule', { n: num(rule.minutes) })
    }
  }

  /** `flexible` — xato «Ish vaqti» maydonida ko'rsatiladi, qatorning pastida emas. */
  const changeProblem = (
    row: ChangeRowDraft,
  ): { message: string; incomplete: boolean; flexible?: boolean } | null => {
    if (row.date === '') {
      return { message: t('attendance.scheduleRequestDateRequired'), incomplete: true }
    }
    if (repeatedChangeDates.has(row.date)) {
      return { message: errorMessage('davomat.jadval_sorovi_sana_takror'), incomplete: false }
    }

    const code = validateScheduleDay(toDayInput(row))
    if (code === FLEXIBLE_REQUIRED_CODE) {
      return {
        message: requiredUnparsable(row) ? t('attendance.durationFormat') : errorMessage(code),
        // Bo'shatilgan maydon — «bo'sh maydon» xatolari kabi faqat «Saqlash» dan keyin
        incomplete: requiredUnparsable(row) && row.required.trim() === '',
        flexible: true,
      }
    }
    if (code !== null) {
      return { message: errorMessage(code), incomplete: INCOMPLETE_CODES.has(code) }
    }

    /*
      «Tanaffus» belgilangan-u, ikkala vaqti ham bo'sh — domen buni «oynasiz kun»
      deb o'tkazib yuborardi va HR belgilagan tanaffus jimgina yo'qolardi.
    */
    if (row.isWorking && row.hasBreak && (row.breakStart === '' || row.breakEnd === '')) {
      return { message: errorMessage('davomat.jadval_tanaffus_toliq_emas'), incomplete: true }
    }

    // Tanaffus qoidasi — mavjud kun qoidalaridan KEYIN (server tartibi, §4.3)
    if (breakRule !== undefined) {
      const issue = validateRequestBreak(toDayInput(row), row.date, breakRule)
      if (issue !== null) return { message: breakRuleMessage(issue, breakRule), incomplete: false }
    }

    return null
  }

  const swapProblem = (row: SwapRowDraft): { message: string; incomplete: boolean } | null => {
    if (row.date === '' || row.withDate === '') {
      return { message: t('attendance.scheduleRequestPairRequired'), incomplete: true }
    }
    if (row.date === row.withDate) {
      return { message: errorMessage('davomat.jadval_sorovi_bir_xil_sana'), incomplete: false }
    }
    if (repeatedSwapDates.has(row.date) || repeatedSwapDates.has(row.withDate)) {
      return { message: errorMessage('davomat.jadval_sorovi_sana_takror'), incomplete: false }
    }
    return null
  }

  const visible = (problem: { message: string; incomplete: boolean } | null): string | null =>
    problem === null || (problem.incomplete && !attempted) ? null : problem.message

  /* ─────────────────────────────────────────────────────────── amallar */

  const updateChange = (key: string, patch: Partial<ChangeRowDraft>) =>
    setChangeRows((rows) => rows.map((row) => (row.key === key ? { ...row, ...patch } : row)))

  const updateSwap = (key: string, patch: Partial<SwapRowDraft>) =>
    setSwapRows((rows) => rows.map((row) => (row.key === key ? { ...row, ...patch } : row)))

  const submit = async () => {
    setError(null)
    setAttempted(true)

    const invalid =
      kind === 'Change'
        ? changeRows.some((row) => changeProblem(row) !== null)
        : swapRows.some((row) => swapProblem(row) !== null)
    if (invalid) return

    const body: ScheduleChangeRequestInput = {
      employeeId: subjectId,
      kind,
      ...(kind === 'Change'
        ? {
            days: changeRows.map((row) => {
              const day = toDayInput(row)
              return {
                date: row.date,
                isWorking: day.isWorking,
                startTime: toWire(day.startTime),
                endTime: toWire(day.endTime),
                breakStart: toWire(day.breakStart),
                breakEnd: toWire(day.breakEnd),
                // Dam olish kunida server baribir 0 yozadi
                breakMinutes: row.isWorking ? row.breakMinutes : null,
                // `requiredMinutes: null` — server sukuti (smena − tanaffus)
                isFlexible: day.isFlexible,
                requiredMinutes: day.requiredMinutes,
              }
            }),
          }
        : { swaps: swapRows.map((row) => ({ date: row.date, withDate: row.withDate })) }),
      note: note.trim() === '' ? undefined : note.trim(),
    }

    try {
      if (request === null) {
        await create.mutateAsync(body)
      } else {
        // HR ko'rgan nusxa — xodim shu orada o'zgartirgan bo'lsa server 409 beradi
        await update.mutateAsync({ id: request.id, body: { ...body, version: request.version } })
      }

      onClose()
    } catch (failure) {
      setStale(request !== null && isStaleRequestError(failure))
      setError(errorMessage(problemCode(failure), t('attendance.requestSaveFailed')))
    }
  }

  const canAddChange = changeRows.length < MAX_SCHEDULE_REQUEST_DAYS
  const canAddSwap = swapRows.length < MAX_SCHEDULE_SWAP_PAIRS

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!saving}
      title={request === null ? t('attendance.scheduleRequestAdd') : t('attendance.scheduleRequestEdit')}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={saving}>
            {stale ? t('common.close') : t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={() => void submit()}
            disabled={saving || stale}
          >
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {error !== null &&
          (stale ? (
            <Alert tone="warning" title={error}>
              {t('attendance.requestStaleHint')}
            </Alert>
          ) : (
            <Alert tone="danger">{error}</Alert>
          ))}

        <div className="space-y-2">
          <SegmentedControl
            className="w-full"
            value={kind}
            onChange={setKind}
            aria-label={t('attendance.scheduleRequestKind')}
            options={SCHEDULE_CHANGE_KINDS.map((value) => ({
              value,
              label: t(SCHEDULE_KIND_LABEL_KEY[value]),
            }))}
          />
          <p className="text-footnote text-[var(--fg-secondary)]">
            {kind === 'Change'
              ? t('attendance.scheduleRequestChangeHint')
              : t('attendance.scheduleRequestSwapHint')}
          </p>
        </div>

        {kind === 'Change' ? (
          <section className="space-y-3">
            <ul className="space-y-3">
              {changeRows.map((row) => {
                const problem = changeProblem(row)
                const shown = visible(problem)

                return (
                  <ChangeRowEditor
                    key={row.key}
                    employeeId={subjectId}
                    row={row}
                    breakRule={breakRule ?? null}
                    // Erkin kun xatosi «Ish vaqti» maydonining o'zida — qator pastida takrorlanmaydi
                    error={problem?.flexible === true ? null : shown}
                    flexibleCode={
                      problem?.flexible === true && shown !== null ? FLEXIBLE_REQUIRED_CODE : null
                    }
                    disabled={saving}
                    removable={changeRows.length > 1}
                    onChange={(patch) => updateChange(row.key, patch)}
                    onRemove={() =>
                      setChangeRows((rows) => rows.filter((item) => item.key !== row.key))
                    }
                  />
                )
              })}
            </ul>

            <AddRowButton
              label={t('attendance.scheduleRequestAddDate')}
              disabled={saving || !canAddChange}
              limit={
                canAddChange
                  ? null
                  : t('attendance.scheduleRequestMaxDays', { n: num(MAX_SCHEDULE_REQUEST_DAYS) })
              }
              onClick={() => setChangeRows((rows) => [...rows, emptyChangeRow('')])}
            />
          </section>
        ) : (
          <section className="space-y-3">
            <ul className="space-y-3">
              {swapRows.map((row) => (
                <SwapRowEditor
                  key={row.key}
                  employeeId={subjectId}
                  row={row}
                  error={visible(swapProblem(row))}
                  disabled={saving}
                  removable={swapRows.length > 1}
                  onChange={(patch) => updateSwap(row.key, patch)}
                  onRemove={() => setSwapRows((rows) => rows.filter((item) => item.key !== row.key))}
                />
              ))}
            </ul>

            <AddRowButton
              label={t('attendance.scheduleRequestAddPair')}
              disabled={saving || !canAddSwap}
              limit={
                canAddSwap
                  ? null
                  : t('attendance.scheduleRequestMaxPairs', { n: num(MAX_SCHEDULE_SWAP_PAIRS) })
              }
              onClick={() => setSwapRows((rows) => [...rows, emptySwapRow()])}
            />
          </section>
        )}

        <TextArea
          label={t('attendance.requestNote')}
          value={note}
          maxLength={NOTE_MAX}
          disabled={saving}
          onChange={(event) => setNote(event.target.value)}
          rows={3}
        />
      </div>
    </Dialog>
  )
}

/** Bir necha marta uchragan (bo'sh bo'lmagan) qiymatlar. */
function repeated(values: readonly string[]): Set<string> {
  const seen = new Set<string>()
  const twice = new Set<string>()

  for (const value of values) {
    if (value === '') continue
    if (seen.has(value)) twice.add(value)
    seen.add(value)
  }

  return twice
}

function AddRowButton({
  label,
  disabled,
  limit,
  onClick,
}: {
  label: string
  disabled: boolean
  /** Chegaraga yetilgan bo'lsa — nega tugma o'chiq. */
  limit: string | null
  onClick: () => void
}) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <Button type="button" size="sm" variant="secondary" disabled={disabled} onClick={onClick}>
        <Plus />
        {label}
      </Button>
      {limit !== null && <p className="text-footnote text-[var(--fg-tertiary)]">{limit}</p>}
    </div>
  )
}

/* ═════════════════════════════════════════════════ «kunni o'zgartirish» */

function ChangeRowEditor({
  employeeId,
  row,
  breakRule,
  error,
  flexibleCode,
  disabled,
  removable,
  onChange,
  onRemove,
}: {
  employeeId: string
  row: ChangeRowDraft
  /** Tanaffus qoidasi — tanaffus maydonlari ostidagi eslatma uchun; `null` — hali kelmagan. */
  breakRule: RequestBreakRule | null
  error: string | null
  /** «Ish vaqti» maydonining xatosi — `FLEXIBLE_REQUIRED_CODE` yoki `null`. */
  flexibleCode: string | null
  disabled: boolean
  removable: boolean
  onChange: (patch: Partial<ChangeRowDraft>) => void
  onRemove: () => void
}) {
  const { t, num } = useLocale()

  const resolution = useScheduleResolution(employeeId, row.date, row.date !== '')
  const plan = resolution.data

  /*
    Joriy reja kelganda qator shu bilan to'ldiriladi — faqat HR qatorga hali
    qo'l tekkizmagan bo'lsa va shu sana uchun hali to'ldirilmagan bo'lsa.
    Jadvali yo'q sanada qiymatlar o'zgarmaydi: ko'chiradigan reja yo'q
    (tanaffus uzunligi esa tushiriladi — u «jadvaldagi» deb ko'rsatilardi).
  */
  useEffect(() => {
    if (plan === undefined || plan.date !== row.date) return
    if (row.touched || row.filledFor === row.date) return

    const day = scheduleOutcome(plan) === 'resolved' ? plan.day : null
    if (day === null) {
      onChange({ filledFor: row.date, breakMinutes: null })
      return
    }

    onChange({
      filledFor: row.date,
      isWorking: day.isWorking,
      startTime: day.startTime ?? '',
      endTime: day.endTime ?? '',
      hasBreak: day.breakStart !== null && day.breakEnd !== null,
      breakStart: day.breakStart ?? '',
      breakEnd: day.breakEnd ?? '',
      breakMinutes: day.breakMinutes,
      /*
        Erkinlik ham joriy rejadan: erkin kunning tanaffusini ko'chirayotgan HR
        uni qayta belgilamasin. Eng kattasi rejaning o'z vaqtlaridan — qator
        aynan shu vaqtlar bilan to'ladi.
      */
      ...flexibleDraftOf(
        day.isWorking && day.isFlexible,
        day.requiredMinutes,
        maxRequiredMinutes(day),
      ),
    })
  }, [plan, row.date, row.touched, row.filledFor, onChange])

  /** HR o'zgartirgan maydon — endi joriy reja uning ustidan yozilmaydi. */
  const edit = (patch: Partial<ChangeRowDraft>) => onChange({ ...patch, touched: true })

  /*
    Tanaffusga tegildi — rejadagi uzunlik endi bu oynaga tegishli emas: oynani
    o'zi yozgan HR uni tanaffusning o'zi deb biladi, server oyna uzunligini oladi.
  */
  const editBreak = (patch: Pick<Partial<ChangeRowDraft>, 'hasBreak' | 'breakStart' | 'breakEnd'>) =>
    edit({ ...patch, breakMinutes: null })

  const day = toDayInput(row)
  const overnight = crossesMidnight(day)

  /** Saqlanayotgan uzunlik oynadan farq qilsa — HR nima yuborilishini ko'rsin. */
  const keptBreak = row.breakMinutes !== null && breakDiffersFromWindow(day) ? row.breakMinutes : null

  return (
    <li
      className={cn(
        'rounded-xl border px-4 py-3',
        row.isWorking ? 'bg-[var(--bg-elevated)]' : 'bg-[var(--bg-sunken)]',
        error !== null ? 'border-ios-red/60' : 'border-[var(--border)]',
      )}
    >
      <div className="flex flex-wrap items-end gap-3">
        <DateField
          className="min-w-[180px] flex-1"
          label={t('attendance.requestDate')}
          value={row.date}
          disabled={disabled}
          onChange={(date) => onChange({ date })}
        />

        <SegmentedControl
          className="h-12"
          value={row.isWorking ? 'working' : 'off'}
          onChange={(value) => edit({ isWorking: value === 'working' })}
          aria-label={t('attendance.scheduleRequestDayType')}
          options={[
            { value: 'working', label: t('attendance.working') },
            { value: 'off', label: t('attendance.dayOff') },
          ]}
        />

        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="mb-1"
          disabled={disabled || !removable}
          title={t('attendance.scheduleRequestRemoveRow')}
          aria-label={t('attendance.scheduleRequestRemoveRow')}
          onClick={onRemove}
        >
          <Trash2 />
        </Button>
      </div>

      {row.isWorking && (
        <div className="mt-3 space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <TextField
              label={t('attendance.startTime')}
              type="time"
              required
              value={row.startTime}
              disabled={disabled}
              onChange={(event) => edit({ startTime: event.target.value })}
            />
            <TextField
              label={
                <span className="flex items-center gap-1.5">
                  {t('attendance.endTime')}
                  {/* Oqibat YOZAYOTGAN paytda ko'rinadi (jadval muharriridagi qoida) */}
                  {overnight && (
                    <Badge tone="purple" size="sm">
                      {t('attendance.nextDay')}
                    </Badge>
                  )}
                </span>
              }
              type="time"
              required
              value={row.endTime}
              disabled={disabled}
              onChange={(event) => edit({ endTime: event.target.value })}
            />
          </div>

          <Checkbox
            checked={row.hasBreak}
            disabled={disabled}
            onChange={(hasBreak) => editBreak({ hasBreak })}
            label={t('attendance.scheduleRequestBreak')}
          />

          {row.hasBreak && (
            <div className="grid grid-cols-2 gap-3">
              <TextField
                label={t('attendance.breakStart')}
                type="time"
                required
                value={row.breakStart}
                disabled={disabled}
                onChange={(event) => editBreak({ breakStart: event.target.value })}
              />
              <TextField
                label={t('attendance.breakEnd')}
                type="time"
                required
                value={row.breakEnd}
                disabled={disabled}
                onChange={(event) => editBreak({ breakEnd: event.target.value })}
              />
            </div>
          )}

          {/* Qoida tanaffus belgilanganda — HR uni xato chiqmasdan OLDIN ko'rsin */}
          {row.hasBreak && breakRule !== null && (
            <p className="tabular text-footnote text-[var(--fg-tertiary)]">
              {t('attendance.requestBreakRuleHint', {
                from: breakRule.from,
                to: breakRule.to,
                n: num(breakRule.minutes),
                max: num(breakRule.fridayMaxMinutes),
              })}
            </p>
          )}

          {keptBreak !== null && (
            <p className="tabular text-footnote text-[var(--fg-secondary)]">
              {t('attendance.scheduleRequestBreakKept', { n: num(keptBreak) })}
            </p>
          )}

          <FlexibleDayFields
            value={row}
            max={maxRequiredMinutes(day)}
            code={flexibleCode}
            disabled={disabled}
            onChange={({ isFlexible, required, requiredAuto }) =>
              edit({ isFlexible, required, requiredAuto })
            }
          />
        </div>
      )}

      <PlanLine
        date={row.date}
        loading={resolution.isPending && row.date !== ''}
        failed={resolution.isError}
        resolution={plan}
        className="mt-3"
      />

      {error !== null && (
        <p role="alert" className="mt-2 text-footnote text-ios-red">
          {error}
        </p>
      )}
    </li>
  )
}

/* ═════════════════════════════════════════════════════ «kun almashtirish» */

function SwapRowEditor({
  employeeId,
  row,
  error,
  disabled,
  removable,
  onChange,
  onRemove,
}: {
  employeeId: string
  row: SwapRowDraft
  error: string | null
  disabled: boolean
  removable: boolean
  onChange: (patch: Partial<SwapRowDraft>) => void
  onRemove: () => void
}) {
  const { t } = useLocale()

  return (
    <li
      className={cn(
        'rounded-xl border bg-[var(--bg-elevated)] px-4 py-3',
        error !== null ? 'border-ios-red/60' : 'border-[var(--border)]',
      )}
    >
      <div className="grid items-start gap-3 sm:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)_auto]">
        <SwapSide
          employeeId={employeeId}
          label={t('attendance.requestDate')}
          value={row.date}
          disabled={disabled}
          onChange={(date) => onChange({ date })}
        />

        <ArrowLeftRight
          aria-hidden
          className="mx-auto size-4 rotate-90 text-[var(--fg-tertiary)] sm:mt-11 sm:rotate-0"
        />

        <SwapSide
          employeeId={employeeId}
          label={t('attendance.scheduleRequestSwapWith')}
          value={row.withDate}
          disabled={disabled}
          onChange={(withDate) => onChange({ withDate })}
        />

        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="justify-self-end sm:mt-7"
          disabled={disabled || !removable}
          title={t('attendance.scheduleRequestRemoveRow')}
          aria-label={t('attendance.scheduleRequestRemoveRow')}
          onClick={onRemove}
        >
          <Trash2 />
        </Button>
      </div>

      {error !== null && (
        <p role="alert" className="mt-2 text-footnote text-ios-red">
          {error}
        </p>
      )}
    </li>
  )
}

/**
 * Juftning bir tomoni — sana va uning joriy rejasi. Almashtirishda server
 * aynan shu rejalarni o'rin almashtiradi, ya'ni HR nimani nimaga
 * almashtirayotganini saqlashdan OLDIN ko'radi.
 */
function SwapSide({
  employeeId,
  label,
  value,
  disabled,
  onChange,
}: {
  employeeId: string
  label: string
  value: string
  disabled: boolean
  onChange: (value: string) => void
}) {
  const resolution = useScheduleResolution(employeeId, value, value !== '')

  return (
    <div className="min-w-0 space-y-2">
      <DateField label={label} value={value} disabled={disabled} onChange={onChange} />
      <PlanLine
        date={value}
        loading={resolution.isPending && value !== ''}
        failed={resolution.isError}
        resolution={resolution.data}
        swap
      />
    </div>
  )
}

/**
 * «Joriy reja: 08:30–18:00, tanaffus 13:00–14:00» — shu sanada xodim HOZIR
 * qaysi reja bilan hisoblanadi.
 *
 * Jadvali yo'q sana alohida aytiladi: «kunni o'zgartirish» unga qo'llanmaydi
 * (SPEC-jadval-sorovi §C2 — o'zgartiriladigan reja yo'q), «kun almashtirish»
 * esa serverda rad etiladi (`davomat.jadval_sorovi_jadval_yoq`).
 */
function PlanLine({
  date,
  loading,
  failed,
  resolution,
  swap = false,
  className,
}: {
  date: string
  loading: boolean
  failed: boolean
  resolution: ReturnType<typeof useScheduleResolution>['data']
  swap?: boolean
  className?: string
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const text = useScheduleRequestText()

  if (date === '') return null

  if (loading) {
    return (
      <p className={cn('text-footnote text-[var(--fg-tertiary)]', className)}>
        {t('attendance.scheduleRequestPlanLoading')}
      </p>
    )
  }

  if (failed || resolution === undefined) {
    return (
      <p className={cn('text-footnote text-[var(--fg-tertiary)]', className)}>
        {t('attendance.scheduleRequestPlanFailed')}
      </p>
    )
  }

  const day = scheduleOutcome(resolution) === 'resolved' ? resolution.day : null

  if (day === null) {
    return (
      <p className={cn('text-footnote text-ios-orange', className)}>
        {swap
          ? errorMessage('davomat.jadval_sorovi_jadval_yoq')
          : t('attendance.scheduleRequestPlanNoSchedule')}
      </p>
    )
  }

  return (
    <p
      className={cn(
        'tabular flex flex-wrap items-center gap-1.5 text-footnote text-[var(--fg-secondary)]',
        className,
      )}
    >
      {t('attendance.scheduleRequestPlanCurrent', { plan: text.plan(day) })}
      {/* Shu sanaga allaqachon tasdiqlangan so'rov bor — reja uniki */}
      {resolution.daySource === 'Request' && (
        <Badge tone={DAY_SOURCE_LOOK.Request.tone} size="sm">
          {t(DAY_SOURCE_LOOK.Request.labelKey)}
        </Badge>
      )}
    </p>
  )
}
