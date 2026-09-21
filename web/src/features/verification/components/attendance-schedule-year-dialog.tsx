import { ChevronLeft, ChevronRight, LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'
import { Alert, Button, Dialog, Skeleton, Switch, TextField } from '@/shared/ui'

import type { ScheduleYearDay, WorkSchedule } from '../api/attendance-api'
import { useScheduleYear, useSetScheduleDates } from '../api/attendance-queries'
import { formatDuration } from '../lib/attendance-presentation'

/** Oyning eng ko'p kuni — to'rning ustunlari. */
const DAY_COLUMNS = Array.from({ length: 31 }, (_, index) => index + 1)

const MONTHS = Array.from({ length: 12 }, (_, index) => index + 1)

/**
 * Jadvalning YILLIK TO'RI (SPEC-jadval-to'ri §D) — Verifix'ning
 * «Графики работы» ekranidagi 12 × 31 jadvalning o'zi.
 *
 * <b>Katak nimani ko'rsatadi.</b> Kun normasini soatda («8,5») yoki dam
 * olish kuni bo'lsa «D». Katak ichida esa VAQT saqlanadi: bizning
 * hisoblash kechikish va erta ketishni o'sha vaqtdan oladi, ya'ni faqat
 * soatni saqlash «Kechikkanlar» hisobotini o'ldirardi (§A dagi cheklov).
 *
 * <b>Kulrang va to'q katak.</b> Kulrang — qiymat haftalik naqshdan
 * keladi, to'q — shu sanaga QO'LDA yozilgan. HR o'zi nimani
 * o'zgartirganini bir qarashda ko'rishi kerak, aks holda to'r «kim
 * qachon nima qilgani» ni yashiradigan jadvalga aylanardi.
 *
 * <b>Tanlash va yozish.</b> Katak bosiladi (shift bilan — oraliq), oy
 * nomi butun oyni tanlaydi. Pastdagi panelda tanlanganlarga bitta amal
 * qo'llanadi: dam olish kuni, ish kuni (vaqt bilan) yoki naqshga
 * qaytarish. Bitta chaqiruv — bitta amal (server shartnomasi).
 */
export function AttendanceScheduleYearDialog({
  schedule,
  onClose,
}: {
  /** `null` — oyna yopiq. */
  schedule: WorkSchedule | null
  onClose: () => void
}) {
  const { t, locale, num } = useLocale()
  const errorMessage = useErrorMessage()

  const [year, setYear] = useState(() => Number(todayIso().slice(0, 4)))
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set())
  const [anchor, setAnchor] = useState<string | null>(null)

  const [isWorking, setIsWorking] = useState(true)
  const [start, setStart] = useState('09:00')
  const [end, setEnd] = useState('18:00')
  const [breakMinutes, setBreakMinutes] = useState('60')

  const open = schedule !== null
  const grid = useScheduleYear(schedule?.id ?? '', year, open)
  const save = useSetScheduleDates()

  /* Oyna har ochilganda tanlov va xato tozalanadi. */
  useEffect(() => {
    if (!open) return

    setSelected(new Set())
    setAnchor(null)
    save.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, schedule?.id])

  const byDate = useMemo(() => {
    const map = new Map<string, ScheduleYearDay>()
    for (const day of grid.data?.days ?? []) map.set(day.date, day)
    return map
  }, [grid.data])

  if (!open) return null

  const toggle = (date: string, range: boolean) => {
    setSelected((previous) => {
      const next = new Set(previous)

      /*
        Shift bilan — oraliq. Yakor (`anchor`) oldingi bosilgan katak:
        oraliq YILNING sanalari bo'yicha olinadi, ya'ni oy chegarasidan
        o'tib ketadi va bu ataylab — «12-dekabrdan 8-yanvargacha» kabi
        tanlov ham kerak bo'ladi.
      */
      if (range && anchor !== null) {
        const [from, to] = anchor <= date ? [anchor, date] : [date, anchor]
        for (const day of grid.data?.days ?? []) {
          if (day.date >= from && day.date <= to) next.add(day.date)
        }
        return next
      }

      if (next.has(date)) next.delete(date)
      else next.add(date)

      return next
    })

    setAnchor(date)
  }

  const selectMonth = (month: number) => {
    const prefix = `${year}-${String(month).padStart(2, '0')}-`
    const dates = (grid.data?.days ?? []).filter((day) => day.date.startsWith(prefix))
    const all = dates.every((day) => selected.has(day.date))

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
    if (schedule === null || selected.size === 0) return

    save.mutate(
      {
        scheduleId: schedule.id,
        body: reset
          ? { dates: [...selected], reset: true }
          : {
              dates: [...selected],
              isWorking,
              startTime: isWorking ? `${start}:00` : undefined,
              endTime: isWorking ? `${end}:00` : undefined,
              breakMinutes: isWorking ? Number(breakMinutes) || 0 : 0,
            },
      },
      { onSuccess: () => setSelected(new Set()) },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="lg"
      dismissible={!save.isPending}
      title={t('attendance.scheduleYear')}
      description={schedule.name}
      footer={
        <Button type="button" onClick={onClose} disabled={save.isPending}>
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

        {/* ── Yil tanlagich ── */}
        <div className="flex items-center gap-1" role="group" aria-label={t('attendance.scheduleYear')}>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={() => setYear((value) => value - 1)}
            aria-label={t('attendance.prevYear')}
          >
            <ChevronLeft />
          </Button>
          <span className="tabular min-w-[64px] text-center text-callout font-semibold">{year}</span>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={() => setYear((value) => value + 1)}
            aria-label={t('attendance.nextYear')}
          >
            <ChevronRight />
          </Button>
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
          <div className="overflow-x-auto">
            <table className="w-full border-separate border-spacing-0 text-caption2">
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
                  const days = (grid.data?.days ?? []).filter((day) => day.date.startsWith(prefix))
                  const minutes = days.reduce((sum, day) => sum + day.plannedMinutes, 0)

                  return (
                    <tr key={month}>
                      <th
                        scope="row"
                        className="sticky left-0 z-10 bg-[var(--bg-elevated)] py-px pr-3 text-left font-normal"
                      >
                        <button
                          type="button"
                          onClick={() => selectMonth(month)}
                          className="press rounded-sm px-1 hover:text-[var(--accent-text)]"
                        >
                          {monthName(month, locale)}
                        </button>
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
                              selected={selected.has(day.date)}
                              label={num(Math.round((day.plannedMinutes / 60) * 10) / 10)}
                              dayOff={t('attendance.codeDayOff')}
                              onClick={(range) => toggle(day.date, range)}
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

        {/* ── Tanlanganlarga amal ── */}
        <div className="rounded-lg border border-[var(--border)] bg-[var(--bg-sunken)] p-3">
          <p className="text-footnote font-medium">
            {selected.size === 0
              ? t('attendance.scheduleYearHint')
              : t('attendance.scheduleYearSelected', { n: num(selected.size) })}
          </p>

          <div className="mt-3 flex flex-wrap items-end gap-3">
            <Switch
              checked={isWorking}
              onChange={setIsWorking}
              label={t('attendance.scheduleWorkingDay')}
            />

            {isWorking && (
              <>
                <TextField
                  label={t('attendance.scheduleStart')}
                  type="time"
                  value={start}
                  onChange={(event) => setStart(event.target.value)}
                  className="w-[132px]"
                />
                <TextField
                  label={t('attendance.scheduleEnd')}
                  type="time"
                  value={end}
                  onChange={(event) => setEnd(event.target.value)}
                  className="w-[132px]"
                />
                <TextField
                  label={t('attendance.scheduleBreak')}
                  type="number"
                  min={0}
                  value={breakMinutes}
                  onChange={(event) => setBreakMinutes(event.target.value)}
                  className="w-[120px]"
                />
              </>
            )}

            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="primary"
                disabled={selected.size === 0 || save.isPending}
                onClick={() => apply(false)}
              >
                {save.isPending && <LoaderCircle className="animate-spin" />}
                {t('common.save')}
              </Button>
              <Button
                type="button"
                variant="secondary"
                disabled={selected.size === 0 || save.isPending}
                onClick={() => apply(true)}
              >
                {t('attendance.scheduleYearReset')}
              </Button>
            </div>
          </div>
        </div>
      </div>
    </Dialog>
  )
}

/**
 * Bitta katak.
 *
 * Naqshdan kelgan katak KULRANG va chegarasi yo'q, qo'lda yozilgani esa
 * to'q va chegarali — farq bir qarashda ko'rinishi kerak. Tanlangani
 * tint bilan belgilanadi (fon emas: fon holatning o'zi uchun band).
 */
function YearCell({
  day,
  selected,
  label,
  dayOff,
  onClick,
}: {
  day: ScheduleYearDay
  selected: boolean
  label: string
  dayOff: string
  onClick: (range: boolean) => void
}) {
  return (
    <button
      type="button"
      onClick={(event) => onClick(event.shiftKey)}
      aria-pressed={selected}
      className={cn(
        'tabular grid h-6 w-9 place-items-center rounded-xs text-caption2 transition-colors',
        day.isWorking
          ? 'text-[var(--fg)]'
          : 'bg-[var(--bg-sunken)] text-[var(--fg-tertiary)]',
        day.source === 'Date' && 'font-semibold ring-1 ring-[var(--border)] ring-inset',
        selected && 'ring-2 ring-[var(--accent)] ring-inset',
      )}
    >
      {day.isWorking ? label : dayOff}
    </button>
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
