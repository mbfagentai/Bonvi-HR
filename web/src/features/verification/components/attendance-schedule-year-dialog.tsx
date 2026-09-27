import {
  CalendarX,
  ChevronLeft,
  ChevronRight,
  Eraser,
  LoaderCircle,
  PaintBucket,
} from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'
import { Alert, Button, Dialog, Skeleton, Switch, TextField } from '@/shared/ui'

import type { ScheduleYearDay, WorkSchedule, WorkScheduleDayInput } from '../api/attendance-api'
import {
  useFillScheduleYear,
  useScheduleYear,
  useSetScheduleDates,
} from '../api/attendance-queries'
import { formatDuration } from '../lib/attendance-presentation'
import { MAX_BREAK_MINUTES, validateScheduleDay } from '../lib/attendance-schedule-rules'

/** Oyning eng ko'p kuni — to'rning ustunlari. */
const DAY_COLUMNS = Array.from({ length: 31 }, (_, index) => index + 1)

const MONTHS = Array.from({ length: 12 }, (_, index) => index + 1)

const NO_DAYS: ScheduleYearDay[] = []

/**
 * Pastdagi panelning FORMA holati — hammasi satr (`attendance-schedule-form-dialog.tsx`
 * dagi `DayDraft` bilan bir xil sabab: `<input>` bo'sh satr beradi).
 */
interface CellDraft {
  isWorking: boolean
  startTime: string
  endTime: string
  breakMinutes: string
  breakStart: string
  breakEnd: string
}

/** Naqshda birorta ham ish kuni bo'lmasa — jadval formasidagi yangi kunning qiymati. */
const FALLBACK_DRAFT: CellDraft = {
  isWorking: true,
  startTime: '09:00',
  endTime: '18:00',
  breakMinutes: '60',
  breakStart: '13:00',
  breakEnd: '14:00',
}

/** Sichqoncha bilan tortib tanlash — boshlangan katak va boshlanishdagi tanlov. */
interface DragState {
  anchor: string
  /** Tortish boshlanishidan OLDINGI tanlov: oraliq har safar shu ustiga qayta quriladi. */
  base: ReadonlySet<string>
  /** Tortish qo'shadimi yoki olib tashlaydimi — birinchi katakning holatiga qarab. */
  adding: boolean
}

type YearAction = 'fill' | 'clear'

/**
 * Jadvalning YILLIK TO'RI (SPEC-jadval-to'ri §D) — Verifix'ning
 * «Графики работы» ekranidagi 12 × 31 jadvalning o'zi.
 *
 * <b>Katak nimani ko'rsatadi.</b> Kun normasini soatda («8,5») yoki dam
 * olish kuni bo'lsa «D». Katak ichida esa VAQT saqlanadi: bizning
 * hisoblash kechikish va erta ketishni o'sha vaqtdan oladi, ya'ni faqat
 * soatni saqlash «Kechikkanlar» hisobotini o'ldirardi (§A dagi cheklov).
 * Vaqtlar va tanaffus oynasi katak ustiga olib borilganda ko'rinadi.
 *
 * <b>Kulrang va to'q katak.</b> Kulrang — qiymat haftalik naqshdan
 * keladi, to'q — shu sanaga QO'LDA yozilgan. HR o'zi nimani
 * o'zgartirganini bir qarashda ko'rishi kerak, aks holda to'r «kim
 * qachon nima qilgani» ni yashiradigan jadvalga aylanardi.
 *
 * <b>Tanlash va yozish.</b> Katak bosiladi, sichqoncha bilan tortiladi
 * yoki shift bilan oraliq olinadi; oy nomi butun oyni tanlaydi. Pastdagi
 * panelda tanlanganlarga bitta amal qo'llanadi: dam olish kuni, ish kuni
 * (vaqt va tanaffus OYNASI bilan) yoki naqshga qaytarish. Tepada — butun
 * yil uchun ikki amal: «naqshdan to'ldirish» va «tozalash» (tasdiq bilan).
 * Bitta imo — bitta chaqiruv (server shartnomasi).
 *
 * ⭐ <b>Tanaffus oynasi SHART joy.</b> Oynasiz kunda tanaffus ishlangan
 * vaqtdan UMUMAN ayirilmaydi (SPEC §5.5.1), reja esa uni ayiradi — faqat
 * «60 daqiqa» yuborilgan katak har kuni soxta +60 ortiqcha ish berardi.
 * Shu sababli panel oynani naqshdagidek so'raydi va jadval formasidagi
 * AYNAN o'sha qoidalar bilan tekshiradi (`validateScheduleDay`).
 *
 * <b>Faqat ko'rish (`readOnly`).</b> `attendance.view` egasi to'rni
 * o'qiydi (SPEC-jadval-to'ri §C) — kataklar bosilmaydi, panel va yil
 * amallari chizilmaydi: ruxsati yo'q tugma umuman ko'rsatilmaydi.
 */
export function AttendanceScheduleYearDialog({
  schedule,
  readOnly = false,
  onClose,
}: {
  /** `null` — oyna yopiq. */
  schedule: WorkSchedule | null
  /** `true` — faqat ko'rish (`attendance.manage` yo'q). */
  readOnly?: boolean
  onClose: () => void
}) {
  const { t, locale, num, date: formatDate } = useLocale()
  const errorMessage = useErrorMessage()

  const [year, setYear] = useState(() => Number(todayIso().slice(0, 4)))
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set())
  const [anchor, setAnchor] = useState<string | null>(null)
  const [draft, setDraft] = useState<CellDraft>(FALLBACK_DRAFT)
  const [confirm, setConfirm] = useState<YearAction | null>(null)

  const drag = useRef<DragState | null>(null)
  /** Oxirgi bosishning qurilmasi: sichqoncha bosishi `pointerdown` da ishlanadi, `click` da emas. */
  const pointerType = useRef<string>('')

  const open = schedule !== null
  const grid = useScheduleYear(schedule?.id ?? '', year, open)
  const save = useSetScheduleDates()
  const clear = useSetScheduleDates()
  const fill = useFillScheduleYear()

  /* Oyna har ochilganda tanlov va xato tozalanadi, panel naqshdan to'ldiriladi. */
  useEffect(() => {
    if (!open || schedule === null) return

    setSelected(new Set())
    setAnchor(null)
    setConfirm(null)
    setDraft(patternDraft(schedule))
    save.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, schedule?.id])

  /*
    Tortish qayerda tugasa ham to'xtaydi — kursor to'rdan tashqarida
    qo'yib yuborilsa ham. Aks holda keyingi har bir katakka kursor
    tegishi tanlovni o'zgartirib yuraverardi.
  */
  useEffect(() => {
    const stop = () => {
      drag.current = null
    }
    window.addEventListener('pointerup', stop)
    window.addEventListener('pointercancel', stop)
    return () => {
      window.removeEventListener('pointerup', stop)
      window.removeEventListener('pointercancel', stop)
    }
  }, [])

  /*
    ⚠️ Faqat EKRANDAGI yilning kunlari. `useScheduleYear` yil almashganda
    oldingi javobni ushlab turadi (`placeholderData`) — shu paytda
    «Yilni tozalash» bosilsa, sarlavhadagi emas, OLDINGI yilning
    kataklari o'chib ketardi.
  */
  const days = grid.data !== undefined && grid.data.year === year ? grid.data.days : NO_DAYS

  const byDate = useMemo(() => {
    const map = new Map<string, ScheduleYearDay>()
    for (const day of days) map.set(day.date, day)
    return map
  }, [days])

  /** Yilning qo'lda yozilgan sanalari — «Yilni tozalash» aynan shularni o'chiradi. */
  const writtenDates = useMemo(
    () => days.filter((day) => day.source === 'Date').map((day) => day.date),
    [days],
  )

  const input = useMemo(() => toDayInput(draft), [draft])
  const draftError = useMemo(() => validateScheduleDay(input), [input])

  if (!open) return null

  const busy = save.isPending || clear.isPending || fill.isPending
  const loaded = days.length > 0 && !grid.isError
  const patternCount = days.length - writtenDates.length

  /*
    Yil almashganda tanlov TOZALANADI: boshqa yilning ko'rinmay qolgan
    kataklari «Saqlash» bilan birga yozilib ketmasin.
  */
  const changeYear = (delta: number) => {
    setYear((value) => value + delta)
    setSelected(new Set())
    setAnchor(null)
  }

  /** Yilning `from` … `to` oralig'idagi sanalari (ikki tomon ham kiradi, tartibdan qat'i nazar). */
  const between = (first: string, second: string): string[] => {
    const [from, to] = first <= second ? [first, second] : [second, first]
    return days.filter((day) => day.date >= from && day.date <= to).map((day) => day.date)
  }

  /**
   * Yangi tanlovning BIRINCHI katagi — panel shu kunning qiymatini oladi:
   * katakni bosgan HR avval uning hozirgi vaqtini ko'rishi kerak.
   * Tanlov davom etayotganda panel o'zgarmaydi — yozilgan qiymat
   * keyingi katak bosilganda yo'qolib qolmasin.
   */
  const prefillFrom = (date: string) => {
    const day = byDate.get(date)
    if (day !== undefined) setDraft((previous) => cellDraft(day, previous))
  }

  const toggle = (date: string, range: boolean) => {
    if (selected.size === 0) prefillFrom(date)

    setSelected((previous) => {
      const next = new Set(previous)

      /*
        Shift bilan — oraliq. Yakor (`anchor`) oldingi bosilgan katak:
        oraliq YILNING sanalari bo'yicha olinadi, ya'ni oy chegarasidan
        o'tib ketadi va bu ataylab — «12-dekabrdan 8-yanvargacha» kabi
        tanlov ham kerak bo'ladi.
      */
      if (range && anchor !== null) {
        for (const item of between(anchor, date)) next.add(item)
        return next
      }

      if (next.has(date)) next.delete(date)
      else next.add(date)

      return next
    })

    setAnchor(date)
  }

  /**
   * Sichqoncha (va qalam) — tortib tanlash `pointerdown` dan boshlanadi.
   *
   * ⚠️ Barmoq (touch) bu yerda ATAYLAB ishlanmaydi: telefonda to'r
   * gorizontal suriladi (SPEC §D) va barmoq bilan tortish tanlov bo'lsa,
   * to'rni surib bo'lmay qolardi. Barmoq bosishi oddiy `click` bo'lib
   * keladi va bitta katakni almashtiradi.
   */
  const startDrag = (event: ReactPointerEvent<HTMLButtonElement>, date: string) => {
    pointerType.current = event.pointerType
    if (event.pointerType === 'touch' || event.button !== 0 || busy) return

    // Matn belgilanmasin va brauzer tugmani «sudramasin»
    event.preventDefault()

    if (event.shiftKey) {
      toggle(date, true)
      return
    }

    const adding = !selected.has(date)
    drag.current = { anchor: date, base: selected, adding }
    toggle(date, false)
  }

  /** Tortish davomida — yakordan shu katakkacha bo'lgan oraliq. */
  const extendDrag = (date: string) => {
    const state = drag.current
    if (state === null) return

    const next = new Set(state.base)
    for (const item of between(state.anchor, date)) {
      if (state.adding) next.add(item)
      else next.delete(item)
    }

    setSelected(next)
  }

  /** `click` — faqat klaviatura (Enter/Space) va barmoq: sichqonchaniki `pointerdown` da ishlangan. */
  const clickCell = (detail: number, shiftKey: boolean, date: string) => {
    if (detail !== 0 && pointerType.current !== 'touch') return
    toggle(date, shiftKey)
  }

  const selectMonth = (month: number) => {
    const prefix = `${year}-${String(month).padStart(2, '0')}-`
    const dates = days.filter((day) => day.date.startsWith(prefix))
    const all = dates.every((day) => selected.has(day.date))

    const first = dates[0]
    if (selected.size === 0 && first !== undefined) prefillFrom(first.date)

    setSelected((previous) => {
      const next = new Set(previous)
      for (const day of dates) {
        if (all) next.delete(day.date)
        else next.add(day.date)
      }
      return next
    })
  }

  const apply = (reset: boolean) => {
    if (selected.size === 0 || (!reset && draftError !== null)) return

    const dates = [...selected]

    save.mutate(
      {
        scheduleId: schedule.id,
        body: reset
          ? { dates, reset: true }
          : input.isWorking
            ? {
                dates,
                isWorking: true,
                startTime: input.startTime,
                endTime: input.endTime,
                breakMinutes: input.breakMinutes,
                breakStart: input.breakStart,
                breakEnd: input.breakEnd,
              }
            : { dates, isWorking: false },
      },
      { onSuccess: () => setSelected(new Set()) },
    )
  }

  const openConfirm = (action: YearAction) => {
    fill.reset()
    clear.reset()
    setConfirm(action)
  }

  /** Yil amallari — muvaffaqiyatdan keyin tanlov ham tozalanadi (kataklar o'zgardi). */
  const runConfirmed = () => {
    const done = {
      onSuccess: () => {
        setConfirm(null)
        setSelected(new Set())
      },
    }

    if (confirm === 'fill') {
      fill.mutate({ scheduleId: schedule.id, year }, done)
    } else if (confirm === 'clear' && writtenDates.length > 0) {
      clear.mutate({ scheduleId: schedule.id, body: { dates: writtenDates, reset: true } }, done)
    }
  }

  const cellTitle = (day: ScheduleYearDay): string => {
    const parts = [formatDate(day.date)]

    if (!day.isWorking) {
      parts.push(t('attendance.dayOff'))
    } else {
      parts.push(`${clock(day.startTime)}–${clock(day.endTime)}`)
      const range =
        day.breakStart !== null && day.breakEnd !== null
          ? `${clock(day.breakStart)}–${clock(day.breakEnd)}`
          : null
      parts.push(
        range !== null
          ? t('attendance.breakWindow', { range })
          : day.breakMinutes > 0
            ? t('attendance.breakWindowNoneHint')
            : t('attendance.breakWindowNone'),
      )
    }

    parts.push(
      day.source === 'Date'
        ? t('attendance.scheduleYearLegendDate')
        : t('attendance.scheduleYearLegendPattern'),
    )

    return parts.join(' · ')
  }

  const actionError = confirm === 'fill' ? fill.error : clear.error
  const confirmPending = fill.isPending || clear.isPending
  const hasWindow = draft.breakStart !== '' || draft.breakEnd !== ''

  return (
    <>
      <Dialog
        open
        onClose={onClose}
        size="lg"
        dismissible={!busy}
        title={t('attendance.scheduleYear')}
        description={schedule.name}
        footer={
          <Button type="button" onClick={onClose} disabled={busy}>
            {t('common.close')}
          </Button>
        }
      >
        <div className="space-y-4">
          {save.isError && (
            <Alert tone="danger">
              {errorMessage(problemCode(save.error), t('attendance.scheduleYearSaveFailed'))}
            </Alert>
          )}

          {/* ── Yil tanlagich va yil amallari ── */}
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-1" role="group" aria-label={t('attendance.scheduleYear')}>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                disabled={busy}
                onClick={() => changeYear(-1)}
                aria-label={t('attendance.prevYear')}
              >
                <ChevronLeft />
              </Button>
              <span className="tabular min-w-[64px] text-center text-callout font-semibold">{year}</span>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                disabled={busy}
                onClick={() => changeYear(1)}
                aria-label={t('attendance.nextYear')}
              >
                <ChevronRight />
              </Button>
            </div>

            {!readOnly && (
              <div className="flex flex-wrap items-center gap-1.5">
                {/* Naqshdan kelayotgan kun qolmagan bo'lsa to'ldiradigan narsa yo'q */}
                <Button
                  type="button"
                  variant="soft"
                  size="sm"
                  disabled={!loaded || busy || patternCount === 0}
                  onClick={() => openConfirm('fill')}
                >
                  <PaintBucket />
                  {t('attendance.scheduleYearFill')}
                </Button>
                {/* Qo'lda yozilgan sana yo'q bo'lsa tozalaydigan narsa yo'q */}
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  disabled={!loaded || busy || writtenDates.length === 0}
                  onClick={() => openConfirm('clear')}
                >
                  <CalendarX className="text-[var(--danger-text)]" />
                  {t('attendance.scheduleYearClear')}
                </Button>
              </div>
            )}
          </div>

          {/* ── To'r ── */}
          {grid.isPending ? (
            <div aria-hidden className="space-y-1.5">
              {MONTHS.map((month) => (
                <Skeleton key={month} className="h-8" />
              ))}
            </div>
          ) : grid.isError ? (
            <Alert tone="danger">{errorMessage(problemCode(grid.error))}</Alert>
          ) : (
            <div className="overflow-x-auto overscroll-x-contain">
              <table className="w-full border-separate border-spacing-0 text-caption2 select-none">
                <thead>
                  <tr>
                    <th className="sticky left-0 z-10 bg-[var(--bg-elevated)] py-1.5 pr-3 text-left font-semibold text-[var(--fg-secondary)]">
                      {t('attendance.month')}
                    </th>
                    <th className="px-2 py-1.5 text-right font-semibold text-[var(--fg-secondary)]">
                      {t('attendance.totalWorked')}
                    </th>
                    {DAY_COLUMNS.map((day) => (
                      <th key={day} className="tabular px-px py-1.5 text-[var(--fg-tertiary)]">
                        {day}
                      </th>
                    ))}
                  </tr>
                </thead>

                <tbody>
                  {MONTHS.map((month) => {
                    const prefix = `${year}-${String(month).padStart(2, '0')}-`
                    const monthDays = days.filter((day) => day.date.startsWith(prefix))
                    const minutes = monthDays.reduce((sum, day) => sum + day.plannedMinutes, 0)

                    return (
                      <tr key={month}>
                        <th
                          scope="row"
                          className="sticky left-0 z-10 bg-[var(--bg-elevated)] py-px pr-3 text-left font-normal"
                        >
                          {readOnly ? (
                            <span className="px-1">{monthName(month, locale)}</span>
                          ) : (
                            <button
                              type="button"
                              disabled={busy}
                              onClick={() => selectMonth(month)}
                              className="press rounded-sm px-1 hover:text-[var(--accent-text)]"
                            >
                              {monthName(month, locale)}
                            </button>
                          )}
                        </th>

                        <td className="tabular px-2 py-px text-right text-[var(--fg-secondary)]">
                          {formatDuration(minutes)}
                        </td>

                        {DAY_COLUMNS.map((column) => {
                          const day = byDate.get(`${prefix}${String(column).padStart(2, '0')}`)

                          // Oyda bunday kun yo'q (31-fevral) — bo'sh katak.
                          if (day === undefined) {
                            return <td key={column} className="p-px" />
                          }

                          return (
                            <td key={column} className="p-px">
                              <YearCell
                                day={day}
                                readOnly={readOnly}
                                disabled={busy}
                                selected={selected.has(day.date)}
                                label={num(Math.round((day.plannedMinutes / 60) * 10) / 10)}
                                dayOff={t('attendance.codeDayOff')}
                                title={cellTitle(day)}
                                onPointerDown={(event) => startDrag(event, day.date)}
                                onPointerEnter={() => extendDrag(day.date)}
                                onClick={(detail, shiftKey) => clickCell(detail, shiftKey, day.date)}
                              />
                            </td>
                          )
                        })}
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}

          <YearLegend />

          {/* ── Tanlanganlarga amal ── */}
          {readOnly ? (
            <p className="text-footnote text-[var(--fg-tertiary)]">
              {t('attendance.scheduleYearReadOnlyHint')}
            </p>
          ) : (
            <div className="rounded-lg border border-[var(--border)] bg-[var(--bg-sunken)] p-3">
              <p className="text-footnote font-medium">
                {selected.size === 0
                  ? t('attendance.scheduleYearHint')
                  : t('attendance.scheduleYearSelected', { n: num(selected.size) })}
              </p>

              <div className="mt-3 flex flex-wrap items-end gap-3">
                <Switch
                  checked={draft.isWorking}
                  disabled={busy}
                  onChange={(isWorking) => setDraft((previous) => ({ ...previous, isWorking }))}
                  label={t('attendance.scheduleWorkingDay')}
                />

                {draft.isWorking && (
                  <>
                    <TextField
                      label={t('attendance.scheduleStart')}
                      type="time"
                      value={draft.startTime}
                      disabled={busy}
                      onChange={(event) =>
                        setDraft((previous) => ({ ...previous, startTime: event.target.value }))
                      }
                      className="w-[132px]"
                    />
                    <TextField
                      label={t('attendance.scheduleEnd')}
                      type="time"
                      value={draft.endTime}
                      disabled={busy}
                      onChange={(event) =>
                        setDraft((previous) => ({ ...previous, endTime: event.target.value }))
                      }
                      className="w-[132px]"
                    />
                    <TextField
                      label={t('attendance.scheduleBreak')}
                      type="number"
                      inputMode="numeric"
                      min={0}
                      max={MAX_BREAK_MINUTES}
                      step={5}
                      value={draft.breakMinutes}
                      disabled={busy}
                      onChange={(event) =>
                        setDraft((previous) => ({ ...previous, breakMinutes: event.target.value }))
                      }
                      className="w-[120px]"
                    />
                    <TextField
                      label={t('attendance.breakStart')}
                      type="time"
                      value={draft.breakStart}
                      disabled={busy}
                      onChange={(event) =>
                        setDraft((previous) => ({ ...previous, breakStart: event.target.value }))
                      }
                      className="w-[132px]"
                    />
                    <TextField
                      label={t('attendance.breakEnd')}
                      type="time"
                      value={draft.breakEnd}
                      disabled={busy}
                      onChange={(event) =>
                        setDraft((previous) => ({ ...previous, breakEnd: event.target.value }))
                      }
                      className="w-[132px]"
                    />
                    {/*
                      Oynani tozalash — ikkala maydon BIRGA (jadval formasidagi
                      kabi): yarim oyna serverda xato (`jadval_tanaffus_toliq_emas`).
                    */}
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      disabled={busy || !hasWindow}
                      title={t('attendance.clearBreakWindow')}
                      aria-label={t('attendance.clearBreakWindow')}
                      onClick={() =>
                        setDraft((previous) => ({ ...previous, breakStart: '', breakEnd: '' }))
                      }
                    >
                      <Eraser />
                    </Button>
                  </>
                )}

                <div className="flex items-center gap-2">
                  <Button
                    type="button"
                    variant="primary"
                    disabled={selected.size === 0 || busy || draftError !== null}
                    onClick={() => apply(false)}
                  >
                    {save.isPending && <LoaderCircle className="animate-spin" />}
                    {t('common.save')}
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={selected.size === 0 || busy}
                    onClick={() => apply(true)}
                  >
                    {t('attendance.scheduleYearReset')}
                  </Button>
                </div>
              </div>

              {/*
                Oqibat YOZAYOTGAN paytda ko'rinadi (jadval formasidagi qoida):
                xato bo'lsa — uning o'zi, oynasiz tanaffus bo'lsa — «ayirilmaydi».
              */}
              {draft.isWorking &&
                (draftError !== null ? (
                  <p role="alert" className="mt-2 text-footnote text-ios-red">
                    {errorMessage(draftError)}
                  </p>
                ) : input.breakStart === null && input.breakMinutes > 0 ? (
                  <p className="mt-2 text-footnote text-[var(--fg-secondary)]">
                    {t('attendance.breakWindowNoneHint')}
                  </p>
                ) : null)}
            </div>
          )}
        </div>
      </Dialog>

      {/*
        Tasdiq oynasi to'r oynasining YONIDA, ichida emas: Escape va fokus
        qopqoni faqat ustki oynaga tegishi kerak.
      */}
      {confirm !== null && (
        <Dialog
          open
          onClose={() => setConfirm(null)}
          size="sm"
          dismissible={!confirmPending}
          title={
            confirm === 'fill'
              ? t('attendance.scheduleYearFillTitle', { year })
              : t('attendance.scheduleYearClearTitle', { year })
          }
          description={schedule.name}
          footer={
            <>
              <Button type="button" onClick={() => setConfirm(null)} disabled={confirmPending}>
                {t('common.cancel')}
              </Button>
              <Button
                type="button"
                variant={confirm === 'clear' ? 'danger' : 'primary'}
                disabled={confirmPending}
                onClick={runConfirmed}
              >
                {confirmPending && <LoaderCircle className="animate-spin" />}
                {confirm === 'fill' ? t('attendance.scheduleYearFill') : t('attendance.scheduleYearClear')}
              </Button>
            </>
          }
        >
          <div className="space-y-4">
            <p className="text-subhead text-[var(--fg-secondary)]">
              {confirm === 'fill'
                ? t('attendance.scheduleYearFillConfirm', { n: num(patternCount), year })
                : t('attendance.scheduleYearClearConfirm', { n: num(writtenDates.length) })}
            </p>

            {actionError !== null && (
              <Alert tone="danger">
                {errorMessage(problemCode(actionError), t('attendance.scheduleYearSaveFailed'))}
              </Alert>
            )}
          </div>
        </Dialog>
      )}
    </>
  )
}

/**
 * Bitta katak.
 *
 * Naqshdan kelgan katak KULRANG va chegarasi yo'q, qo'lda yozilgani esa
 * to'q va chegarali — farq bir qarashda ko'rinishi kerak. Tanlangani
 * tint bilan belgilanadi (fon emas: fon holatning o'zi uchun band).
 *
 * Faqat ko'rishda katak TUGMA EMAS: bosiladigan, lekin hech narsa
 * qilmaydigan element bajarilmaydigan va'da bo'lardi.
 */
function YearCell({
  day,
  readOnly,
  disabled,
  selected,
  label,
  dayOff,
  title,
  onPointerDown,
  onPointerEnter,
  onClick,
}: {
  day: ScheduleYearDay
  readOnly: boolean
  disabled: boolean
  selected: boolean
  label: string
  dayOff: string
  title: string
  onPointerDown: (event: ReactPointerEvent<HTMLButtonElement>) => void
  onPointerEnter: () => void
  onClick: (detail: number, shiftKey: boolean) => void
}) {
  const look = cn(
    'tabular grid h-6 w-9 place-items-center rounded-xs text-caption2 transition-colors',
    day.isWorking ? 'text-[var(--fg)]' : 'bg-[var(--bg-sunken)] text-[var(--fg-tertiary)]',
    day.source === 'Date' && 'font-semibold ring-1 ring-[var(--border)] ring-inset',
    selected && 'ring-2 ring-[var(--accent)] ring-inset',
  )

  const text = day.isWorking ? label : dayOff

  if (readOnly) {
    return (
      <span title={title} className={look}>
        {text}
      </span>
    )
  }

  return (
    <button
      type="button"
      title={title}
      disabled={disabled}
      onPointerDown={onPointerDown}
      onPointerEnter={onPointerEnter}
      onClick={(event) => onClick(event.detail, event.shiftKey)}
      aria-pressed={selected}
      className={look}
    >
      {text}
    </button>
  )
}

/** Kulrang va to'q katak nimani anglatadi — ko'ruvchi uchun ham, yozuvchi uchun ham. */
function YearLegend() {
  const { t } = useLocale()

  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-caption1 text-[var(--fg-secondary)]">
      <span className="flex items-center gap-1.5">
        <span aria-hidden className="size-3 rounded-xs bg-[var(--bg-sunken)]" />
        {t('attendance.scheduleYearLegendPattern')}
      </span>
      <span className="flex items-center gap-1.5">
        <span aria-hidden className="size-3 rounded-xs ring-1 ring-[var(--border)] ring-inset" />
        <span className="font-semibold">{t('attendance.scheduleYearLegendDate')}</span>
      </span>
    </div>
  )
}

/**
 * Oy nomi — `01` … `12`, inglizchada ham shunday.
 *
 * NEGA nom emas: o'n ikki nomni to'rt tilga tarjima qilish qirq sakkizta
 * yangi kalit degani (`monthLabel` dagi bilan bir xil mulohaza).
 */
function monthName(month: number, locale: string): string {
  return locale === 'en' ? `${month}` : String(month).padStart(2, '0')
}

/** Server vaqti («09:00:00») → forma vaqti («09:00»); yo'q bo'lsa bo'sh satr. */
function clock(value: string | null): string {
  return value === null ? '' : value.slice(0, 5)
}

/**
 * Panelning boshlang'ich qiymati — naqshdagi BIRINCHI ish kuni (tanaffus
 * oynasi bilan). Naqshda ish kuni yo'q bo'lsa — jadval formasidagi
 * yangi kunning qiymati.
 */
function patternDraft(schedule: WorkSchedule): CellDraft {
  const day = [...schedule.days]
    .sort((left, right) => left.dayIndex - right.dayIndex)
    .find((item) => item.isWorking)

  if (day === undefined) return FALLBACK_DRAFT

  return {
    isWorking: true,
    startTime: clock(day.startTime),
    endTime: clock(day.endTime),
    breakMinutes: String(day.breakMinutes),
    breakStart: clock(day.breakStart),
    breakEnd: clock(day.breakEnd),
  }
}

/**
 * Katakning hozirgi qiymati → panel. Dam olish kunida vaqtlar yo'q —
 * oldingi vaqtlar SAQLANADI (almashtirgich qayta yoqilsa ular qaytadi).
 */
function cellDraft(day: ScheduleYearDay, previous: CellDraft): CellDraft {
  if (!day.isWorking) return { ...previous, isWorking: false }

  return {
    isWorking: true,
    startTime: clock(day.startTime),
    endTime: clock(day.endTime),
    breakMinutes: String(day.breakMinutes),
    breakStart: clock(day.breakStart),
    breakEnd: clock(day.breakEnd),
  }
}

/**
 * Panel → tekshiruv va so'rov qiymati (`attendance-schedule-form-dialog.tsx`
 * dagi `toDayInput` bilan bir xil qoida). Kun indeksi bu yerda ma'nosiz —
 * tekshiruv uni o'qimaydi.
 */
function toDayInput(draft: CellDraft): WorkScheduleDayInput {
  if (!draft.isWorking) {
    return {
      dayIndex: 1,
      isWorking: false,
      startTime: null,
      endTime: null,
      breakMinutes: 0,
      breakStart: null,
      breakEnd: null,
    }
  }

  return {
    dayIndex: 1,
    isWorking: true,
    startTime: draft.startTime === '' ? null : draft.startTime,
    endTime: draft.endTime === '' ? null : draft.endTime,
    breakMinutes: parseMinutes(draft.breakMinutes === '' ? '0' : draft.breakMinutes),
    breakStart: draft.breakStart === '' ? null : draft.breakStart,
    breakEnd: draft.breakEnd === '' ? null : draft.breakEnd,
  }
}

/** «60» → 60; butun manfiy bo'lmagan son emas — `NaN` (tekshiruv uni ushlaydi). */
function parseMinutes(value: string): number {
  return /^\d+$/u.test(value.trim()) ? Number(value.trim()) : Number.NaN
}
