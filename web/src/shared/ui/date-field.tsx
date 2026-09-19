import { Calendar, ChevronDown, ChevronLeft, ChevronRight } from 'lucide-react'
import { useEffect, useId, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

import { MONTH_NAMES, MONTH_NAMES_SHORT, WEEKDAY_NAMES_SHORT } from '@/shared/i18n/calendar'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  DATE_MASK,
  addMonths,
  daysInMonth,
  displayToIso,
  isoToDisplay,
  isoWithin,
  maskDateInput,
  mondayFirstWeekday,
  parseIso,
  toIso,
  type DateParts,
} from '@/shared/lib/date-value'
import { todayIso } from '@/shared/lib/today'

import { TextField } from './text-field'

/** Taqvim panelining kengligi (px) — o'rnini hisoblashda ham ishlatiladi. */
const PANEL_WIDTH = 292

/**
 * Panelning TAXMINIY balandligi — birinchi o'lchovgacha.
 *
 * Haqiqiy balandlik oyga qarab o'zgaradi (besh yoki olti qator), shu
 * sababli chizilgandan keyin `offsetHeight` dan olinadi. Bu son faqat
 * birinchi hisobda kerak: usiz panel pastga sig'ishini bilmay turib
 * joylashardi.
 */
const PANEL_HEIGHT = 340

/** Maydon bilan panel orasidagi masofa (px). */
const GAP = 6

interface DateFieldProps {
  label?: ReactNode
  hint?: ReactNode
  /** Tayyor matn — tarjima chaqiruvchi tomonda bajariladi. */
  error?: string | undefined
  /** ISO qiymat: «2026-09-19» yoki bo'sh satr. */
  value: string
  /**
   * Yangi ISO qiymat. Maydon tozalansa — bo'sh satr.
   *
   * ⚠️ Hodisa EMAS, QIYMAT uzatiladi (`Select` va `TextField` dan
   * ataylab farq). Sabab: bu yerda `<input>` foydalanuvchi TERGAN
   * matnni tutadi («19.09.2026»), saqlanadigan qiymat esa boshqa
   * («2026-09-19»). Hodisani uzatish chaqiruvchini `event.target.value`
   * ni o'qishga undardi va u ekrandagi matnni bazaga yozib yuborardi.
   */
  onChange: (value: string) => void
  onBlur?: () => void
  /** Eng erta ruxsat etilgan sana — ISO. */
  min?: string
  /** Eng kech ruxsat etilgan sana — ISO. */
  max?: string
  name?: string
  id?: string
  disabled?: boolean
  required?: boolean
  autoFocus?: boolean
  className?: string
  variant?: 'outlined' | 'filled'
  size?: 'md' | 'lg'
}

/**
 * ⭐ Sana maydoni — O'ZIMIZNING taqvim bilan.
 *
 * <b>Nega `<input type="date">` almashtirildi.</b> Foydalanuvchi talabi
 * (2026-09-19): sana tanlash oynasi butun tizimda BIR XIL ko'rinsin.
 * Brauzerning o'z oynasi buni bermaydi — Chrome, Safari va Firefox
 * uchta boshqa-boshqa taqvim chizadi, ularning rangi, o'lchami va til
 * qoidasi tizimning sozlamasidan keladi (ilovaning tilidan EMAS), va
 * ularni uslublash imkoni yo'q. Natijada o'zbek tilidagi interfeysda
 * ruscha yoki inglizcha taqvim ochilardi.
 *
 * <b>Qiymat shartnomasi O'ZGARMADI.</b> Tashqariga baribir ISO satr
 * chiqadi («2026-09-19») — ya'ni forma sxemalari, `min`/`max`
 * chegaralari va backend'ning `DateOnly` i bilan hech narsa
 * o'zgarmaydi. O'zgargani — hodisa o'rniga qiymat uzatilishi
 * (<see cref="onChange"/> izohi).
 *
 * <b>Terish HAM ishlaydi, faqat tanlash emas.</b> HR ko'pincha sanani
 * hujjatdan ko'chiradi va klaviaturada terish sichqonchada oy-oy
 * varaqlashdan tezroq. Nuqtalar o'zi qo'yiladi («19092026» →
 * «19.09.2026»), ya'ni terish brauzernikidan kam bosish talab qiladi.
 *
 * <b>Klaviatura.</b> Asosiy yo'l — maydonga TERISH, u to'liq
 * klaviaturaviy. Taqvim ichida kunlar oddiy `<button>` bo'lgani uchun
 * Tab bilan yuriladi va Enter bilan tanlanadi; Escape oynani yopadi.
 * Strelkalar bilan kundan kunga sakrash ATAYLAB qo'shilmadi — u
 * fokusni qo'lda boshqarishni talab qiladi va terish yo'li uni
 * allaqachon ortiqcha qiladi.
 *
 * Oy va yil ro'yxati — HAQIQIY `<select>`, ko'rinmas qilib ustiga
 * qo'yilgan. Shu tufayli telefonda tizimning o'z g'ildiragi ochiladi va
 * klaviatura bilan boshqarish tekin keladi; ko'rinishni esa ostidagi
 * matn beradi.
 */
export function DateField({
  label,
  hint,
  error,
  value,
  onChange,
  onBlur,
  min,
  max,
  name,
  id,
  disabled,
  required,
  autoFocus,
  className,
  variant = 'outlined',
  size = 'md',
}: DateFieldProps) {
  const { t } = useLocale()

  const generatedId = useId()
  const fieldId = id ?? generatedId

  const [open, setOpen] = useState(false)
  const [text, setText] = useState(() => isoToDisplay(value))

  const wrapRef = useRef<HTMLDivElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  /**
   * Taqvimning EKRANDAGI o'rni — `position: fixed` uchun.
   *
   * ⚠️ <b>Nega portal va fixed, oddiy `absolute` emas.</b> Sana
   * maydonlarining ko'pchiligi DIALOG ichida turadi, dialogning tanasi
   * esa `overflow-y-auto` (uzun forma aylantiriladi). Ichkarida
   * `absolute` bilan chizilgan panel o'sha aylantirgich chegarasida
   * KESILADI — pastdagi maydonlarda taqvimning yarmi ko'rinmasdi.
   * Portal uni `body` ga chiqaradi, o'rni esa maydonning o'lchovidan
   * hisoblanadi.
   */
  const [spot, setSpot] = useState<{ top: number; left: number } | null>(null)

  /**
   * Oxirgi marta YUQORIGA chiqarilgan qiymat.
   *
   * ⚠️ Usiz maydonni tahrirlab bo'lmasdi. «19.09.2026» dan bitta harf
   * o'chirilsa matn «19.09.202» bo'ladi — bu to'liq sana emas, ya'ni
   * saqlangan qiymat tozalanadi (`''`). O'sha zahoti quyidagi effekt
   * ishga tushib, matnni ham tozalab yuborardi: foydalanuvchi bitta
   * harfni o'chirdi, butun maydon bo'shab qoldi.
   *
   * Shu sababli effekt faqat TASHQARIDAN kelgan o'zgarishga javob
   * beradi: forma tozalanganda (`reset`), boshqa yozuv ochilganda yoki
   * qiymat dastur tomonidan qo'yilganda.
   */
  const emitted = useRef(value)

  const emit = (iso: string) => {
    emitted.current = iso
    onChange(iso)
  }

  useEffect(() => {
    if (value === emitted.current) return

    emitted.current = value
    setText(isoToDisplay(value))
  }, [value])

  /*
    O'rin har ochilganda VA aylantirilganda qayta hisoblanadi.

    `scroll` tinglagichi `capture` rejimida: dialog tanasi o'z ichida
    aylanadi va uning hodisasi `window` gacha KO'TARILMAYDI — ushlash
    bosqichisiz panel maydondan ajralib, joyida qotib qolardi.
  */
  useLayoutEffect(() => {
    if (!open) return

    const place = () => {
      // Maydonning O'ZI o'lchanadi, o'ram emas: o'ramga yorliq va izoh
      // ham kiradi va panel izohdan pastroqda osilib qolardi.
      const anchor = wrapRef.current?.querySelector('input')?.getBoundingClientRect()
      if (anchor === undefined) return

      const height = panelRef.current?.offsetHeight ?? PANEL_HEIGHT
      const roomBelow = window.innerHeight - anchor.bottom

      setSpot({
        // Pastga sig'masa — TEPAGA. Ikkalasiga ham sig'masa pastda
        // qoladi: u holda sahifa baribir aylantiriladi.
        top:
          roomBelow >= height + GAP || anchor.top < height + GAP
            ? anchor.bottom + GAP
            : anchor.top - height - GAP,

        // Chapga yoki o'ngga chiqib ketmasin — ekran chetidan 8px.
        left: Math.min(Math.max(8, anchor.left), window.innerWidth - PANEL_WIDTH - 8),
      })
    }

    place()

    window.addEventListener('scroll', place, true)
    window.addEventListener('resize', place)

    return () => {
      window.removeEventListener('scroll', place, true)
      window.removeEventListener('resize', place)
    }
  }, [open])

  // Tashqariga bosilsa yopiladi. `pointerdown` — `click` EMAS: bosish
  // boshlanishida yopilsa, ostidagi tugma o'z ishini baribir bajaradi.
  useEffect(() => {
    if (!open) return

    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node

      // Panel PORTALDA — u `wrapRef` ning ichida emas, shu sababli
      // ikkala daraxt ham tekshiriladi. Faqat birinchisi tekshirilsa,
      // taqvimdagi har bosish oynani yopib yuborardi.
      if (wrapRef.current?.contains(target)) return
      if (panelRef.current?.contains(target)) return

      setOpen(false)
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false)
    }

    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)

    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  const commit = (iso: string) => {
    emit(iso)
    setText(isoToDisplay(iso))
  }

  /**
   * ⚠️ Yarim terilgan matn («19.09») saqlangan qiymatni TOZALAYDI.
   *
   * Uni eski qiymat bilan qoldirish tabiiyroq ko'rinadi, lekin o'shanda
   * ekranda bir sana, formada boshqasi turardi: «19.09.2026» dan bitta
   * harf o'chirilsa, maydon «19.09.202» ni ko'rsatib, saqlashda esa
   * eski to'liq sana ketardi. Bunday farqni foydalanuvchi ko'rmaydi.
   */
  const handleType = (raw: string) => {
    const masked = maskDateInput(raw)
    setText(masked)

    emit(displayToIso(masked) ?? '')
  }

  /**
   * Fokus ketganda to'liqsiz matn tozalanadi.
   *
   * Maydonda «19.09.202» bo'lib qolsa, u saqlanmagan (yuqoriga bo'sh
   * qiymat chiqqan) — lekin ekranda turaveradi va HR uni saqlangan deb
   * o'ylardi. Saqlangan qiymat qaytariladi: u bo'sh bo'lsa maydon ham
   * bo'shaydi.
   */
  const handleBlur = () => {
    if (text !== '' && displayToIso(text) === null) setText(isoToDisplay(value))
    onBlur?.()
  }

  return (
    <div ref={wrapRef} className={cn('relative', className)}>
      <TextField
        id={fieldId}
        name={name}
        label={label}
        hint={hint}
        error={error}
        value={text}
        onChange={(event) => handleType(event.target.value)}
        onBlur={handleBlur}
        disabled={disabled}
        required={required}
        autoFocus={autoFocus}
        variant={variant}
        size={size}
        placeholder={DATE_MASK}
        inputMode="numeric"
        autoComplete="off"
        // Brauzerning o'z to'ldirgichi sana maydoniga tegmasin: u
        // niqobni buzib, «2026-09-19» ni matn maydoniga yozib qo'yardi.
        spellCheck={false}
        trailing={
          <button
            type="button"
            disabled={disabled}
            aria-label={t('common.chooseDate')}
            aria-expanded={open}
            onClick={() => setOpen((was) => !was)}
            className={cn(
              'shrink-0 rounded-sm p-0.5 transition-colors duration-150',
              'text-[var(--fg-tertiary)] hover:text-[var(--fg-secondary)]',
              open && 'text-[var(--accent-text)]',
              disabled && 'pointer-events-none',
            )}
          >
            <Calendar className="size-[18px]" strokeWidth={2} />
          </button>
        }
      />

      {open &&
        createPortal(
          <CalendarPanel
            ref={panelRef}
            spot={spot}
            value={value}
            min={min}
            max={max}
            onPick={(iso) => {
              commit(iso)
              setOpen(false)
            }}
          />,
          document.body,
        )}
    </div>
  )
}

/** Taqvim oynasi — `body` ga portal bilan chiqariladi (sababi `spot` izohida). */
function CalendarPanel({
  ref,
  spot,
  value,
  min,
  max,
  onPick,
}: {
  ref: React.Ref<HTMLDivElement>
  spot: { top: number; left: number } | null
  value: string
  min?: string
  max?: string
  onPick: (iso: string) => void
}) {
  const { t, locale } = useLocale()

  const today = todayIso()
  const selected = parseIso(value)

  /*
    Ko'rinib turgan oy. Boshlang'ich holat — tanlangan sana, u
    bo'lmasa bugungi kun. Chegaradan tashqarida qolsa, chegaraga
    tortiladi: `max={bugun}` bo'lgan tug'ilgan sana maydoni bo'sh
    ochilganda ham kelajakdagi oyni ko'rsatmasligi kerak.
  */
  const [view, setView] = useState<DateParts>(() => {
    const start = selected ?? parseIso(today) ?? { year: 2000, month: 1, day: 1 }

    if (min !== undefined && min !== '' && toIso(start) < min) {
      return parseIso(min) ?? start
    }

    if (max !== undefined && max !== '' && toIso(start) > max) {
      return parseIso(max) ?? start
    }

    return start
  })

  const months = MONTH_NAMES[locale]
  const monthsShort = MONTH_NAMES_SHORT[locale]
  const weekdays = WEEKDAY_NAMES_SHORT[locale]

  const years = useMemo(() => {
    const current = Number(today.slice(0, 4))
    // Chegara berilmagan bo'lsa: 80 yil orqaga (tug'ilgan sanalar) va
    // 10 yil oldinga (kelajakdagi buyruq sanalari).
    const first = min !== undefined && min !== '' ? Number(min.slice(0, 4)) : current - 80
    const last = max !== undefined && max !== '' ? Number(max.slice(0, 4)) : current + 10

    // Ko'rinib turgan yil ro'yxatdan tushib qolmasin: tanlangan sana
    // chegaradan tashqarida bo'lsa (eski yozuv), `<select>` bo'sh
    // qiymatga tushib qolardi.
    const from = Math.min(first, view.year)
    const to = Math.max(last, view.year)

    return Array.from({ length: to - from + 1 }, (_, index) => from + index)
  }, [min, max, today, view.year])

  const total = daysInMonth(view.year, view.month)
  const offset = mondayFirstWeekday(view.year, view.month)

  const step = (delta: number) => setView((current) => addMonths(current, delta))

  const dayIso = (day: number) => toIso({ year: view.year, month: view.month, day })

  const todayAllowed = isoWithin(today, min, max)

  return (
    <div
      ref={ref}
      style={{
        top: spot?.top ?? -9999,
        left: spot?.left ?? -9999,
        width: PANEL_WIDTH,
      }}
      className={cn(
        // `z-60` — dialog `z-50` da turadi va panel undan TEPADA
        // bo'lishi kerak (sana maydonlarining ko'pchiligi dialogda).
        'fixed z-[60] rounded-2xl border p-3.5',
        'border-[var(--border)] bg-[var(--bg-elevated)] shadow-floating',
        // O'lchov olinmaguncha ko'rinmaydi: aks holda panel bir kadr
        // ekranning chap yuqori burchagida chaqnab o'tardi.
        spot === null && 'invisible',
      )}
    >
      {/* ───────── Sarlavha: oy, yil va o'qlar ───────── */}
      <div className="flex items-center gap-1.5">
        <Picker
          label={monthsShort[view.month - 1] ?? ''}
          value={String(view.month)}
          onChange={(next) => setView((current) => ({ ...current, month: Number(next) }))}
          options={months.map((name, index) => ({ value: String(index + 1), label: name }))}
        />

        <Picker
          label={String(view.year)}
          value={String(view.year)}
          onChange={(next) =>
            setView((current) => {
              const year = Number(next)

              // Kun oyga sig'masa qisiladi: 29-fevraldan kabisa
              // bo'lmagan yilga o'tganda 1-martga sakramasligi kerak.
              return { year, month: current.month, day: Math.min(current.day, daysInMonth(year, current.month)) }
            })
          }
          options={years.map((year) => ({ value: String(year), label: String(year) }))}
        />

        <span className="flex-1" />

        <Arrow icon={ChevronLeft} label={t('common.prevMonth')} onClick={() => step(-1)} />
        <Arrow icon={ChevronRight} label={t('common.nextMonth')} onClick={() => step(1)} />
      </div>

      {/* ───────── Hafta kunlari ───────── */}
      <div className="mt-3 grid grid-cols-7 gap-0.5">
        {weekdays.map((day) => (
          <span
            key={day}
            className="flex h-7 items-center justify-center text-caption2 font-semibold tracking-wide text-[var(--fg-tertiary)] uppercase"
          >
            {day}
          </span>
        ))}
      </div>

      {/* ───────── Kunlar ───────── */}
      <div className="grid grid-cols-7 gap-0.5">
        {/* Oyning birinchi kunigacha bo'sh kataklar */}
        {Array.from({ length: offset }, (_, index) => (
          <span key={`bosh-${index}`} />
        ))}

        {Array.from({ length: total }, (_, index) => {
          const day = index + 1
          const iso = dayIso(day)
          const allowed = isoWithin(iso, min, max)
          const isSelected = iso === value
          const isToday = iso === today

          return (
            <button
              key={day}
              type="button"
              disabled={!allowed}
              aria-current={isToday ? 'date' : undefined}
              aria-pressed={isSelected}
              onClick={() => onPick(iso)}
              className={cn(
                'flex h-9 items-center justify-center rounded-lg text-subhead tabular-nums',
                'transition-colors duration-100',
                isSelected
                  ? 'bg-[var(--accent)] font-semibold text-[var(--fg-on-accent)]'
                  : isToday
                    ? 'font-semibold text-[var(--accent-text)] hover:bg-[var(--bg-hover)]'
                    : 'text-[var(--fg)] hover:bg-[var(--bg-hover)]',
                !allowed && 'pointer-events-none opacity-30',
              )}
            >
              {day}
            </button>
          )
        })}
      </div>

      {/* ───────── «Bugun» ───────── */}
      <button
        type="button"
        disabled={!todayAllowed}
        onClick={() => onPick(today)}
        className={cn(
          'mt-2.5 h-9 w-full rounded-lg text-subhead font-medium transition-colors duration-100',
          'text-[var(--accent-text)] hover:bg-[var(--bg-hover)]',
          !todayAllowed && 'pointer-events-none opacity-30',
        )}
      >
        {t('common.today')}
      </button>
    </div>
  )
}

/**
 * Sarlavhadagi ro'yxat: ko'rinadigan matn + ustida ko'rinmas `<select>`.
 *
 * Shunday qilingani uchun mobil qurilmada tizimning o'z g'ildiragi
 * ochiladi, klaviatura bilan boshqarish esa hech qanday kod talab
 * qilmaydi. O'zimizning ro'yxatni chizish 90 ta yil uchun ichki
 * skrollni, fokus tuzog'ini va tashqariga bosishni qaytadan yozish
 * degani bo'lardi — `Select` komponenti esa yorliq bilan keladi va
 * bu yerga sig'maydi.
 */
function Picker({
  label,
  value,
  onChange,
  options,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  options: { value: string; label: string }[]
}) {
  return (
    <span
      className={cn(
        'relative inline-flex items-center gap-1 rounded-md px-2 py-1',
        'text-subhead font-semibold text-[var(--fg)]',
        'transition-colors duration-100 hover:bg-[var(--bg-hover)]',
        'focus-within:ring-2 focus-within:ring-[var(--accent)]/30',
      )}
    >
      {label}
      <ChevronDown className="size-3.5 text-[var(--fg-tertiary)]" strokeWidth={2.5} />

      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="absolute inset-0 cursor-pointer opacity-0"
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </span>
  )
}

function Arrow({
  icon: Icon,
  label,
  onClick,
}: {
  icon: typeof ChevronLeft
  label: string
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-label={label}
      onClick={onClick}
      className={cn(
        'flex size-8 items-center justify-center rounded-lg',
        'text-[var(--accent-text)] transition-colors duration-100 hover:bg-[var(--bg-hover)]',
      )}
    >
      <Icon className="size-[18px]" strokeWidth={2.5} />
    </button>
  )
}
