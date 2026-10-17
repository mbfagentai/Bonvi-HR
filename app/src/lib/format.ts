import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'

import { DEFAULT_LOCALE, isLocale, type LocaleCode } from '@/i18n/locales'
import { pad, parseIsoDate, tashkentParts } from './time'

/**
 * Formatlashning YAGONA joyi (UX §F.6).
 *
 * ⚠️ Oy va hafta kunlarining nomlari `Intl` dan OLINMAYDI: brauzerlar
 * `uz-Latn-UZ` va `uz-Cyrl-UZ` teglarini noto'g'ri ishlaydi va jim
 * turib boshqa ajratgich qo'yib yuboradi. Shu sababli nomlar ham,
 * naqshlar ham tarjima fayllarida yotadi — HR ilovasi ham xuddi shu
 * sababdan qo'lda formatlaydi.
 *
 * ⚠️ Nisbiy vaqt YO'Q («2 soat oldin»). Bu ilova aniq vaqtlar haqida,
 * va keshdan chizilgan nisbiy vaqt oflaynda soddagina YOLG'ON bo'ladi.
 */
export interface Formatter {
  /** «22.09.2026» (en: «2026-09-22»). */
  date: (value: Date | string | null | undefined) => string
  /** «22.09» (en: «09-22») — ro'yxat qatoridagi qisqa sana, yil yo'q. */
  dayMonth: (value: string | null | undefined) => string
  /** «22-sentabr, seshanba». */
  dateLong: (value: Date | string | null | undefined) => string
  /** «Sentabr 2026». */
  monthTitle: (year: number, month: number) => string
  /** «08:35». */
  time: (value: Date | string | null | undefined) => string
  /** «7 soat 12 daq.» · «48 daq.» */
  duration: (minutes: number | null | undefined) => string
  /** «12 daqiqa kechikdi». */
  late: (minutes: number) => string
  /** «420 m» · «3.4 km». */
  distance: (meters: number) => string
  /** «±35 m». */
  accuracy: (meters: number) => string
  /** «1 250». */
  number: (value: number) => string
  /** Taqvim sarlavhasidagi qisqa kun nomi: «Du». */
  weekdayShort: (weekday: number) => string
  locale: LocaleCode
}

export function useFormat(): Formatter {
  const { t, i18n } = useTranslation()
  const language = i18n.language

  return useMemo<Formatter>(() => {
    const locale: LocaleCode = isLocale(language) ? language : DEFAULT_LOCALE

    const date: Formatter['date'] = (value) => {
      const parts = value ? tashkentParts(value) : null
      if (!parts) return '—'

      return locale === 'en'
        ? `${parts.year}-${pad(parts.month)}-${pad(parts.day)}`
        : `${pad(parts.day)}.${pad(parts.month)}.${parts.year}`
    }

    // Faqat «2026-09-22» kalitidan: bu yerga lahza emas, KUN keladi.
    const dayMonth: Formatter['dayMonth'] = (value) => {
      const parts = parseIsoDate(value)
      if (!parts) return '—'

      return locale === 'en' ? `${pad(parts.month)}-${pad(parts.day)}` : `${pad(parts.day)}.${pad(parts.month)}`
    }

    const dateLong: Formatter['dateLong'] = (value) => {
      const parts =
        typeof value === 'string' && value.length === 10
          ? parseIsoDate(value)
          : value
            ? tashkentParts(value)
            : null
      if (!parts) return '—'

      return t('date.longPattern', {
        day: parts.day,
        month: t(`date.monthDay.${parts.month}`),
        weekday: t(`date.weekday.${parts.weekday}`),
      })
    }

    const number: Formatter['number'] = (value) => {
      const text = String(Math.round(Math.abs(value)))
      const groups: string[] = []
      for (let end = text.length; end > 0; end -= 3) {
        groups.unshift(text.slice(Math.max(0, end - 3), end))
      }
      // Ming ajratgichi: o'zbek va rus tilida bo'sh joy, ingliz tilida vergul
      const joined = groups.join(locale === 'en' ? ',' : ' ')
      return value < 0 ? `-${joined}` : joined
    }

    return {
      locale,
      date,
      dayMonth,
      dateLong,
      number,

      monthTitle: (year, month) =>
        t('date.monthTitlePattern', { month: t(`date.month.${month}`), year }),

      time: (value) => {
        const parts = value ? tashkentParts(value) : null
        if (!parts) return '—'
        return `${pad(parts.hour)}:${pad(parts.minute)}`
      },

      duration: (minutes) => {
        if (minutes === null || minutes === undefined || minutes <= 0) return '—'

        const hours = Math.floor(minutes / 60)
        const rest = Math.round(minutes % 60)

        if (hours === 0) return t('format.durationM', { m: rest })
        if (rest === 0) return t('format.durationH', { h: hours })
        return t('format.durationHm', { h: hours, m: rest })
      },

      late: (minutes) => t('format.lateMinutes', { n: Math.round(minutes) }),

      distance: (meters) =>
        meters < 1000
          ? t('format.meters', { n: Math.round(meters) })
          : // Kilometrda bitta kasr xona — «3.4 km». Nuqta ataylab: vergul
            // masofani raqamlar ro'yxatidek ko'rsatardi.
            t('format.kilometers', { n: (meters / 1000).toFixed(1) }),

      accuracy: (meters) => t('format.accuracy', { n: Math.round(meters) }),

      weekdayShort: (weekday) => t(`date.weekdayShort.${weekday}`),
    }
  }, [t, language])
}
