import { useCallback, useMemo } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'

import type { ScheduleChangeRequest } from '../api/attendance-api'
import { formatBirthday, scheduleSwapPairs } from './attendance-presentation'

/**
 * Bir kunning rejasi — so'rov kuni ham, jadval kuni ham (`/schedules/resolve`).
 * Vaqtlar «HH:mm».
 */
export interface DayPlanLike {
  isWorking: boolean
  startTime: string | null
  endTime: string | null
  breakStart: string | null
  breakEnd: string | null
  /** Faqat jadval kunida: oynasi belgilanmagan tanaffusning uzunligi. */
  breakMinutes?: number
}

/**
 * Jadval so'rovining matni — ro'yxat katagi, qaror oynasi va forma BITTA
 * joydan o'qiydi: «16.10 · 08:30–18:00, tanaffus 12:23–14:23»,
 * «17.10 · Dam olish kuni», «15.10 ⇄ 18.10».
 *
 * NEGA hook: matn tilga bog'liq, `t` ni har chaqiruvchiga uzatib yurish esa
 * uning tipini bu faylga sudrab kelardi.
 */
export function useScheduleRequestText() {
  const { t, date } = useLocale()

  /*
    Joriy yil sanasi — yilsiz («16.10»): navbatdagi so'rovlarning deyarli
    hammasi shu kunlar haqida va yil har qatorda takrorlanib, katakni
    ikki qatorga cho'zardi. Boshqa yil — to'liq sana, adashtirmasin.
  */
  const shortDate = useCallback(
    (iso: string): string =>
      iso.slice(0, 4) === todayIso().slice(0, 4) ? formatBirthday(iso) : date(iso),
    [date],
  )

  const plan = useCallback(
    (day: DayPlanLike): string => {
      if (!day.isWorking) return t('attendance.dayOff')

      let text = `${day.startTime ?? '—'}–${day.endTime ?? '—'}`

      // Tugash boshlanishdan oldin — tungi smena (jadvaldagi qoida)
      if (day.startTime !== null && day.endTime !== null && day.endTime <= day.startTime) {
        text += ` (${t('attendance.nextDay')})`
      }

      if (day.breakStart !== null && day.breakEnd !== null) {
        text += `, ${t('attendance.scheduleRequestBreakRange', { range: `${day.breakStart}–${day.breakEnd}` })}`
      } else if ((day.breakMinutes ?? 0) > 0) {
        text += `, ${t('attendance.scheduleRequestBreakLength', { n: day.breakMinutes ?? 0 })}`
      }

      return text
    },
    [t],
  )

  /** So'rovning qatorlari: har sana yoki har juft — bittadan. */
  const lines = useCallback(
    (request: ScheduleChangeRequest): string[] =>
      request.kind === 'Swap'
        ? scheduleSwapPairs(request.days).map(
            (pair) => `${shortDate(pair.date)} ⇄ ${shortDate(pair.withDate)}`,
          )
        : request.days.map((day) => `${shortDate(day.date)} · ${plan(day)}`),
    [plan, shortDate],
  )

  // Bitta obyekt — jadval ustunlari `useMemo` da unga tayanadi
  return useMemo(() => ({ shortDate, plan, lines }), [shortDate, plan, lines])
}
