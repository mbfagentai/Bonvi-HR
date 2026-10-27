import clsx from 'clsx'
import { useTranslation } from 'react-i18next'

import type { AttendanceDayStatus } from '@/api/types'
import type { AttendanceDay } from '@/data/attendance'
import { DAY_STATUS_LOOK } from '@/data/status-look'
import { useFormat } from '@/lib/format'
import { daysInMonth, firstWeekdayOfMonth, pad, todayIso } from '@/lib/time'
import { Skeleton } from './feedback'

/**
 * Oy to'ri (UX §E.4).
 *
 * Uchta qoida Verifix'dagi uchta nuqsonni tuzatadi:
 *   1. Oddiy ishlangan kun RANGSIZ — rang faqat e'tibor talab qiladigan
 *      kunga sarflanadi. Verifix'da 21 kunning 20 tasi sariq edi va
 *      sariq hech narsa anglatmay qolgandi.
 *   2. Kechikish — holat EMAS, katak ostidagi 3 px chiziq. Ikki fakt,
 *      ikki kanal: biri ikkinchisini yashirmaydi.
 *   3. BUGUN — halqa, TANLANGAN kun — to'ldirilgan katak. Verifix'da
 *      bugun to'ldirilgan ko'k quti edi va o'sha kunning o'z holati
 *      umuman ko'rinmasdi.
 */
interface MonthGridProps {
  year: number
  month: number
  days: readonly AttendanceDay[]
  selected: string
  loading: boolean
  onSelect: (dateIso: string) => void
  /**
   * Holat so'zini almashtirish (faqat ekran o'quvchi uchun) — jamoa a'zosining
   * `NotEmployed` kuni «Ishlamagan» emas, «Jamoada emas» deb o'qilsin: u kuni
   * odam boshqa bo'limda ishlagan bo'lishi mumkin. Rang o'zgarmaydi.
   */
  statusLabels?: Partial<Record<AttendanceDayStatus, string>>
}

export function MonthGrid({ year, month, days, selected, loading, onSelect, statusLabels }: MonthGridProps) {
  const { t } = useTranslation()
  const format = useFormat()

  const total = daysInMonth(year, month)
  const leading = firstWeekdayOfMonth(year, month) - 1
  const today = todayIso()

  const weekdays = [1, 2, 3, 4, 5, 6, 7]
  const cells: (AttendanceDay | null)[] = []

  for (let index = 0; index < leading; index += 1) cells.push(null)

  for (let day = 1; day <= total; day += 1) {
    const dateIso = `${year}-${pad(month)}-${pad(day)}`
    cells.push(days.filter((item) => item.date === dateIso)[0] ?? emptyCell(dateIso))
  }

  return (
    <div>
      <div className="mb-1 grid grid-cols-7 gap-1">
        {weekdays.map((weekday) => (
          <div
            key={weekday}
            className="py-1 text-center text-caption2 font-medium uppercase text-fg-tertiary"
          >
            {format.weekdayShort(weekday)}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7 gap-1">
        {cells.map((cell, index) => {
          if (!cell) return <div key={`bosh-${index}`} className="h-11" />

          if (loading) return <Skeleton key={cell.date} className="h-11 rounded-card" />

          const look = DAY_STATUS_LOOK[cell.status]
          const isToday = cell.date === today
          const isSelected = cell.date === selected
          const isFuture = cell.date > today
          const dayNumber = Number(cell.date.slice(8, 10))

          return (
            <button
              key={cell.date}
              type="button"
              onClick={() => onSelect(cell.date)}
              aria-current={isToday ? 'date' : undefined}
              aria-pressed={isSelected}
              aria-label={t('calendar.dayAria', {
                date: format.dateLong(cell.date),
                status: t((statusLabels ? statusLabels[cell.status] : undefined) ?? look.labelKey),
              })}
              className={clsx(
                'relative h-11 w-full rounded-card text-subhead transition-colors duration-150 ease-ios',
                isSelected ? 'bg-accent text-fg-on-accent' : look.tintClass,
                isSelected ? '' : isFuture ? 'text-fg-tertiary' : 'text-fg',
                isToday && !isSelected && 'ring-2 ring-accent',
                !isSelected && 'active:bg-bg-pressed',
              )}
            >
              <span className={clsx('nums', isToday && 'font-semibold')}>{dayNumber}</span>

              {cell.lateMinutes > 0 ? (
                <span className="absolute bottom-1 left-2 right-2 h-[3px] rounded-pill bg-hue-late" />
              ) : null}
            </button>
          )
        })}
      </div>
    </div>
  )
}

function emptyCell(dateIso: string): AttendanceDay {
  return {
    date: dateIso,
    status: 'NoSchedule',
    workedMinutes: 0,
    lateMinutes: 0,
    earlyLeaveMinutes: 0,
    breakTakenMinutes: 0,
    breakDeductionMinutes: 0,
    firstInAt: null,
    lastOutAt: null,
    planStartAt: null,
    planEndAt: null,
    mobileMarks: 0,
    marks: [],
  }
}
