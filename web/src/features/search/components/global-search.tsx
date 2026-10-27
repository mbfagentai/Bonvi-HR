import { useNavigate } from '@tanstack/react-router'
import { Briefcase, ChevronRight, Search, SearchX } from 'lucide-react'
import {
  useEffect,
  useId,
  useRef,
  useState,
  type KeyboardEvent,
  type ReactNode,
} from 'react'

import { useAuth } from '@/features/auth'
import { FlagBadge, StatusBadge } from '@/features/employees/components/employee-badges'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { EMPLOYEES_VIEW } from '@/features/employees/lib/employee-presentation'
import { CandidatePhoto } from '@/features/recruiting/components/candidate-photo'
import {
  CANDIDATE_LOOK,
  RECRUITING_VIEW,
  VACANCY_LOOK,
} from '@/features/recruiting/lib/recruiting-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Badge, Input } from '@/shared/ui'
import {
  SEARCH_MIN_LENGTH,
  type GlobalSearchResult,
  type SearchCandidate,
  type SearchEmployee,
  type SearchVacancy,
} from '../api/search-api'
import { useGlobalSearch } from '../api/search-queries'

/**
 * Yozish to'xtagandan keyin qancha kutiladi.
 *
 * Xodimlar ro'yxatidagi 350 ms dan qisqaroq: bu yerda natija kichik oynada
 * chiqadi va HR uni «yozish bilan birga» kutadi; so'rov esa har guruhdan
 * bir nechta qator — arzon.
 */
const DEBOUNCE_MS = 250

/** Klaviatura bilan tanlanadigan bitta qator. */
interface Entry {
  key: string
  open: () => void
  render: (active: boolean) => ReactNode
}

/**
 * Yuqori paneldagi GLOBAL qidiruv — barcha bo'limlar bo'yicha.
 *
 * ⭐ Ilgari bu maydon Enter bosilganda shunchaki xodimlar ro'yxatini
 * ochardi va faqat HOZIR ISHLAYOTGANLAR orasidan qidirardi. Egasining
 * talabi (2026-10-15): u HAMMA joydan topishi kerak — ishdan ketganlar,
 * barcha holatdagi nomzodlar (rad etilgan, rezerv, qora ro'yxat, ishga
 * olingan), Excel arxivi va vakansiyalar.
 *
 * Natija yozish bilan birga maydon ostidagi oynada guruhlab chiqadi
 * (iOS'dagi Spotlight kabi): har qatorda rasm, ism, qayerdaligi va holat
 * yorlig'i; bosilsa — karta. Qidiruv qoidasi bo'limlardagi bilan AYNAN
 * bir xil (serverda o'sha o'qish modellari ishlatiladi), ya'ni bu yerda
 * topilgan odam bo'limida ham topiladi.
 *
 * Klaviatura: ↑/↓ — tanlash, Enter — ochish, Esc — yopish, ⌘K / Ctrl+K —
 * istalgan sahifadan maydonga o'tish.
 */
export function GlobalSearch() {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const navigate = useNavigate()

  const [text, setText] = useState('')
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)

  const rootRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  /** Natija hali kelmagan paytda bosilgan Enter — kelishi bilan bajariladi. */
  const pendingEnter = useRef(false)
  const listId = useId()

  // Kechiktirish: so'rov har harfda emas, yozish to'xtaganda ketadi.
  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(text.trim()), DEBOUNCE_MS)
    return () => window.clearTimeout(timer)
  }, [text])

  // Oynadan tashqariga bosilsa — yopiladi (yon menyu, sahifa, boshqa tugma).
  useEffect(() => {
    if (!open) return

    const onPointerDown = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false)
    }

    document.addEventListener('pointerdown', onPointerDown)
    return () => document.removeEventListener('pointerdown', onPointerDown)
  }, [open])

  /*
    ⌘K / Ctrl+K — istalgan sahifadan qidiruvga. `event.code` — tugmaning
    JOYI: rus/kirill tartibida `event.key` «л» bo'lardi va yorliq
    ishlamasdi. Maydon chizilmagan bo'lsa (ruxsat yo'q) brauzerning o'z
    Ctrl+K si tortib olinmaydi.
  */
  useEffect(() => {
    const onKeyDown = (event: globalThis.KeyboardEvent) => {
      if (!(event.metaKey || event.ctrlKey) || event.altKey || event.shiftKey) return
      if (event.code !== 'KeyK' || !inputRef.current) return

      event.preventDefault()
      inputRef.current.focus()
      inputRef.current.select()
      setOpen(true)
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  const search = useGlobalSearch(query)

  const close = () => {
    pendingEnter.current = false
    setOpen(false)
    setText('')
    setQuery('')
    setActive(0)
    inputRef.current?.blur()
  }

  const trimmed = text.trim()
  const showPanel = open && trimmed !== ''
  const data = trimmed.length >= SEARCH_MIN_LENGTH ? search.data : undefined

  /*
    ⭐ «Yangi» natija — AYNAN hozir maydonda turgan matnniki.

    `keepPreviousData` yangi harf yozilganda eski natijani ko'rsatib
    turadi (oyna miltillamasin). Lekin eski qatorni BOSISH yoki Enter
    bilan ochish boshqa odamning kartasini ochardi («Karimov» natijasi
    ustida «Aliyev» deb yozib Enter bosilsa — Karimov), bosish paytida
    ro'yxat yangilansa esa bosish umuman yo'qolardi. Shu sababli eski
    natija xira va BOSILMAYDIGAN bo'lib turadi, Enter esa yangisi kelishini
    kutadi.
  */
  const fresh = data !== undefined && query === trimmed && !search.isPlaceholderData
  const entries = data ? buildEntries(data, { close, navigate, query: data.query }) : []
  const activeIndex = Math.max(0, Math.min(active, entries.length - 1))

  // Kutilayotgan Enter — yangi natija kelishi bilan tanlangan qatorni ochadi.
  useEffect(() => {
    if (!pendingEnter.current || !fresh || !showPanel) return
    pendingEnter.current = false
    entries[activeIndex]?.open()
  })

  /*
    Ruxsat — hook'lardan KEYIN (React qoidasi). Maydon ikkala ruxsatdan
    birortasi bo'lsa chiziladi: server javobni ruxsatga qarab toraytiradi
    (ruxsati yo'q guruh `null` bo'lib keladi va bu yerda chizilmaydi).
  */
  if (!hasPermission(EMPLOYEES_VIEW) && !hasPermission(RECRUITING_VIEW)) return null

  // Tanlov o'zgarganda qator ko'rinadigan joyga suriladi — oyna balandligi
  // cheklangan va ↓ bilan pastga tushgan tanlov ko'zdan yo'qolmasin.
  const move = (next: number) => {
    setActive(next)
    document.getElementById(`${listId}-${next}`)?.scrollIntoView({ block: 'nearest' })
  }

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault()
        // Yopiq oynada birinchi ↓ — faqat ochadi, tanlovni surmaydi.
        if (!showPanel) {
          setOpen(true)
          return
        }
        if (fresh && entries.length > 0) move(Math.min(activeIndex + 1, entries.length - 1))
        break
      case 'ArrowUp':
        event.preventDefault()
        if (showPanel && fresh && entries.length > 0) move(Math.max(activeIndex - 1, 0))
        break
      case 'Enter':
        event.preventDefault()
        // Yopiq oyna — ko'rinmagan tanlovni ochmaymiz.
        if (!showPanel || trimmed.length < SEARCH_MIN_LENGTH) return
        if (fresh) {
          entries[activeIndex]?.open()
        } else {
          // Kechiktirishni kutmasdan so'raymiz va natija kelishi bilan ochamiz.
          pendingEnter.current = true
          setQuery(trimmed)
        }
        break
      case 'Escape':
        pendingEnter.current = false
        // Birinchi Esc — oynani yopadi (brauzer `type="search"` maydonini
        // o'zi tozalab yubormasin), ikkinchisi — matnni tozalaydi.
        if (open) {
          event.preventDefault()
          setOpen(false)
        } else {
          setText('')
        }
        break
    }
  }

  const note =
    trimmed.length < SEARCH_MIN_LENGTH
      ? t('search.minLength')
      : search.isError && !data
        ? t('search.failed')
        : !data
          ? t('search.loading')
          : null

  return (
    <div
      ref={rootRef}
      className="relative w-full"
      // Tab bilan maydondan chiqilsa — oyna yopiladi (aks holda u ochiq
      // qolib, Esc ham ishlamasdi: tugmalar maydonga ulangan).
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setOpen(false)
      }}
    >
      <Input
        ref={inputRef}
        icon={Search}
        shape="pill"
        size="lg"
        type="search"
        value={text}
        onChange={(event) => {
          setText(event.target.value)
          setActive(0)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        // Esc'dan keyin fokus maydonda qoladi — qayta bosish oynani ochsin.
        onClick={() => setOpen(true)}
        onKeyDown={onKeyDown}
        placeholder={t('search.placeholder')}
        aria-label={t('search.label')}
        role="combobox"
        aria-expanded={showPanel}
        aria-controls={showPanel && entries.length > 0 ? listId : undefined}
        aria-autocomplete="list"
        aria-activedescendant={
          showPanel && fresh && entries.length > 0 ? `${listId}-${activeIndex}` : undefined
        }
        autoComplete="off"
        spellCheck={false}
      />

      {showPanel && (
        <div
          data-state="open"
          // Oyna ichidagi bo'sh joyga (sarlavha, izoh) bosilganda ham fokus
          // maydonda qolsin — aks holda ↑/↓ ro'yxatni emas, sahifani surardi.
          onMouseDown={(event) => event.preventDefault()}
          className={cn(
            'pop-panel z-50 overflow-y-auto overscroll-contain rounded-2xl border p-1.5',
            'border-[var(--border)] bg-[var(--bg-elevated)] shadow-floating',
            // Telefonda maydon tor — oyna yuqori panel kengligida (panel
            // `backdrop-filter` tufayli `fixed` uchun asos bo'ladi)
            'fixed inset-x-3 top-[4.25rem] max-h-[calc(100dvh-5.5rem)]',
            'sm:absolute sm:inset-x-0 sm:top-full sm:mt-2 sm:max-h-[min(70vh,600px)]',
            // Maydon panelda ixcham — natijalar (ism + lavozim + bo'lim) siqilmasin
            'lg:right-auto lg:w-[max(100%,36rem)]',
          )}
          style={{ transformOrigin: 'top center' }}
        >
          {note !== null ? (
            <PanelNote>{note}</PanelNote>
          ) : entries.length === 0 ? (
            data && (fresh ? <EmptyResult /> : <PanelNote>{t('search.loading')}</PanelNote>)
          ) : (
            data && (
              <div
                id={listId}
                role="listbox"
                aria-label={t('search.label')}
                aria-busy={!fresh}
                className={cn(
                  'transition-opacity duration-150',
                  !fresh && 'pointer-events-none opacity-60',
                )}
              >
                {renderGroups(data, entries, activeIndex, listId, setActive)}
              </div>
            )
          )}

          {data && entries.length > 0 && (
            <p className="hidden px-3 pt-1.5 pb-1 text-caption2 text-[var(--fg-tertiary)] sm:block">
              {t('search.keyboardHint')}
            </p>
          )}
        </div>
      )}
    </div>
  )
}

/* ─────────────────────────────────────────────────────────── qatorlar */

type Navigate = ReturnType<typeof useNavigate>

/**
 * Javobni klaviatura bilan yuriladigan TEKIS qatorlar ro'yxatiga aylantiradi.
 *
 * Tartib ekrandagi bilan bir xil (xodimlar → nomzodlar → vakansiyalar):
 * ↓ tugmasi guruh chegarasidan o'tib keyingisiga tushadi.
 */
function buildEntries(
  data: GlobalSearchResult,
  ctx: { close: () => void; navigate: Navigate; query: string },
): Entry[] {
  const { close, navigate, query } = ctx
  const entries: Entry[] = []

  for (const employee of data.employees?.items ?? []) {
    entries.push({
      key: `e-${employee.id}`,
      open: () => {
        close()
        void navigate({ to: '/xodimlar/$employeeId', params: { employeeId: employee.id } })
      },
      render: (active) => <EmployeeRow item={employee} active={active} />,
    })
  }

  // Xodimlar ro'yxati BARCHA holatlarni ko'rsata oladi (`status=all`) —
  // ko'p topilganda to'liq ro'yxatga o'tish yo'li shu.
  const employees = data.employees
  if (employees && employees.items.length > 0 && employees.total > employees.items.length) {
    entries.push({
      key: 'e-all',
      open: () => {
        close()
        void navigate({ to: '/xodimlar', search: { q: query, status: 'all' } })
      },
      render: (active) => <ShowAllRow total={employees.total} active={active} />,
    })
  }

  for (const candidate of data.candidates?.items ?? []) {
    entries.push({
      key: `c-${candidate.id}`,
      open: () => {
        close()
        void navigate({ to: '/nomzodlar/$candidateId', params: { candidateId: candidate.id } })
      },
      render: (active) => <CandidateRow item={candidate} active={active} />,
    })
  }

  for (const vacancy of data.vacancies?.items ?? []) {
    entries.push({
      key: `v-${vacancy.id}`,
      open: () => {
        close()
        void navigate({ to: '/nomzodlar', search: { vacancy: vacancy.id } })
      },
      render: (active) => <VacancyRow item={vacancy} active={active} />,
    })
  }

  return entries
}

/**
 * Guruhlarni sarlavhasi bilan chizadi. Qatorlar `entries` dan KETMA-KET
 * olinadi — indeks klaviatura tanlovi bilan bir xil bo'lishi uchun.
 */
function renderGroups(
  data: GlobalSearchResult,
  entries: Entry[],
  activeIndex: number,
  listId: string,
  setActive: (index: number) => void,
) {
  let cursor = 0

  const take = (count: number) => {
    const slice = entries.slice(cursor, cursor + count).map((entry, offset) => {
      const index = cursor + offset
      return (
        <div
          key={entry.key}
          id={`${listId}-${index}`}
          role="option"
          aria-selected={index === activeIndex}
          // Fokus maydonda qolsin — aks holda klaviatura tanlovi uziladi
          onMouseDown={(event) => event.preventDefault()}
          onMouseMove={() => {
            if (index !== activeIndex) setActive(index)
          }}
          onClick={entry.open}
        >
          {entry.render(index === activeIndex)}
        </div>
      )
    })

    cursor += count
    return slice
  }

  const employees = data.employees
  const candidates = data.candidates
  const vacancies = data.vacancies

  const employeeRows =
    employees && employees.items.length > 0
      ? employees.items.length + (employees.total > employees.items.length ? 1 : 0)
      : 0

  return (
    <>
      {employees && employees.items.length > 0 && (
        <Group titleKey="search.employees" total={employees.total}>
          {take(employeeRows)}
        </Group>
      )}

      {candidates && candidates.items.length > 0 && (
        <Group titleKey="search.candidates" total={candidates.total}>
          {take(candidates.items.length)}
          {candidates.total > candidates.items.length && (
            <MoreHint count={candidates.total - candidates.items.length} />
          )}
        </Group>
      )}

      {vacancies && vacancies.items.length > 0 && (
        <Group titleKey="search.vacancies" total={vacancies.total}>
          {take(vacancies.items.length)}
          {vacancies.total > vacancies.items.length && (
            <MoreHint count={vacancies.total - vacancies.items.length} />
          )}
        </Group>
      )}
    </>
  )
}

function Group({
  titleKey,
  total,
  children,
}: {
  titleKey: string
  total: number
  children: ReactNode
}) {
  const { t, num } = useLocale()
  const titleId = useId()

  return (
    <div
      role="group"
      aria-labelledby={titleId}
      className="py-1 [&+&]:mt-1 [&+&]:border-t [&+&]:border-[var(--border)] [&+&]:pt-2"
    >
      <div
        role="presentation"
        className="flex items-baseline justify-between px-3 pt-1 pb-1.5 text-caption1 font-semibold tracking-wide text-[var(--fg-tertiary)] uppercase"
      >
        <span id={titleId}>{t(titleKey)}</span>
        <span className="font-medium normal-case tabular-nums">{num(total)}</span>
      </div>
      {children}
    </div>
  )
}

function RowShell({
  active,
  leading,
  title,
  meta,
  trailing,
}: {
  active: boolean
  leading: ReactNode
  title: string
  meta: string
  trailing?: ReactNode
}) {
  return (
    <div
      className={cn(
        'flex cursor-pointer items-center gap-3 rounded-xl px-2.5 py-2',
        'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
        active && 'bg-[var(--bg-hover)]',
      )}
    >
      {leading}

      <span className="min-w-0 flex-1">
        <span className="block truncate text-subhead font-semibold">{title}</span>
        {meta !== '' && (
          <span className="mt-0.5 block truncate text-footnote text-[var(--fg-secondary)]">
            {meta}
          </span>
        )}
      </span>

      {trailing && <span className="flex shrink-0 flex-wrap justify-end gap-1">{trailing}</span>}
    </div>
  )
}

function EmployeeRow({ item, active }: { item: SearchEmployee; active: boolean }) {
  const { t } = useLocale()

  const meta = [item.position, item.department].filter(Boolean).join(' · ')

  return (
    <RowShell
      active={active}
      leading={
        <EmployeePhoto employeeId={item.id} photoKey={item.photoKey} name={item.fullName} size="md" />
      }
      title={item.fullName}
      meta={meta}
      trailing={
        <>
          {item.status ? (
            <StatusBadge status={item.status} size="sm" />
          ) : (
            <Badge tone="neutral" size="sm">
              {t('search.noEmployment')}
            </Badge>
          )}
          {item.flags.map((flag) => (
            <FlagBadge key={flag} type={flag} size="sm" />
          ))}
        </>
      }
    />
  )
}

function CandidateRow({ item, active }: { item: SearchCandidate; active: boolean }) {
  const { t, date } = useLocale()
  const look = CANDIDATE_LOOK[item.status]

  const meta = [item.vacancy, item.stage, t('search.applied', { date: date(item.createdAt) })]
    .filter(Boolean)
    .join(' · ')

  return (
    <RowShell
      active={active}
      leading={
        <CandidatePhoto candidateId={item.id} hasPhoto={item.hasPhoto} name={item.fullName} size="md" />
      }
      title={item.fullName}
      meta={meta}
      trailing={
        <Badge tone={look.tone} size="sm" dot>
          {t(`recruiting.candidateStatus.${look.key}`)}
        </Badge>
      }
    />
  )
}

function VacancyRow({ item, active }: { item: SearchVacancy; active: boolean }) {
  const { t, num } = useLocale()
  const look = VACANCY_LOOK[item.status]

  return (
    <RowShell
      active={active}
      leading={
        <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-[var(--accent-soft)] text-[var(--accent-text)]">
          <Briefcase className="size-[18px]" strokeWidth={2} />
        </span>
      }
      title={item.title}
      meta={t('search.vacancyMeta', {
        openings: num(item.openings),
        candidates: num(item.candidateCount),
      })}
      trailing={
        <Badge tone={look.tone} size="sm" dot>
          {t(`recruiting.vacancyStatus.${look.key}`)}
        </Badge>
      }
    />
  )
}

function ShowAllRow({ total, active }: { total: number; active: boolean }) {
  const { t, num } = useLocale()

  return (
    <div
      className={cn(
        'flex cursor-pointer items-center justify-between gap-3 rounded-xl px-3 py-2 text-subhead text-[var(--accent-text)]',
        'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
        active && 'bg-[var(--bg-hover)]',
      )}
    >
      <span>{t('search.showAllEmployees', { count: num(total) })}</span>
      <ChevronRight className="size-4 shrink-0" />
    </div>
  )
}

function MoreHint({ count }: { count: number }) {
  const { t, num } = useLocale()

  return (
    <p role="presentation" className="px-3 pt-1 pb-1.5 text-footnote text-[var(--fg-tertiary)]">
      {t('search.more', { count: num(count) })}
    </p>
  )
}

function PanelNote({ children }: { children: ReactNode }) {
  return (
    <p role="status" aria-live="polite" className="px-3 py-3 text-subhead text-[var(--fg-secondary)]">
      {children}
    </p>
  )
}

function EmptyResult() {
  const { t } = useLocale()

  return (
    <div role="status" aria-live="polite" className="flex flex-col items-center gap-1.5 px-4 py-6 text-center">
      <SearchX className="size-6 text-[var(--fg-tertiary)]" />
      <p className="text-subhead font-semibold">{t('search.empty')}</p>
      <p className="text-footnote text-[var(--fg-secondary)]">{t('search.emptyHint')}</p>
    </div>
  )
}
