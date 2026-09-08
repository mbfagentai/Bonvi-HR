import { Link, useRouterState } from '@tanstack/react-router'
import { ChevronRight, Clock, PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useDashboardStats } from '@/features/dashboard/api/dashboard-queries'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Badge, BonviMark } from '@/shared/ui'
import { NAV, type NavItem } from './nav-items'

/**
 * Yon menyu.
 *
 * Yuqorida logotip, o'rtada bo'limlar, pastda menyuni yig'ish tugmasi.
 * Foydalanuvchi kartasi bu yerda emas — u yuqori panelning o'ng burchagida
 * (hisob bilan bog'liq narsalar bir joyda).
 */
export function Sidebar({
  collapsed,
  onToggle,
}: {
  collapsed: boolean
  onToggle: () => void
}) {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const pathname = useRouterState({ select: (s) => s.location.pathname })
  const search = useRouterState({ select: (s) => s.location.search })

  /**
   * «Sinov muddati» yozuvi yonidagi belgi.
   *
   * Davr sifatida ATAYLAB bosh sahifaning standart qiymati — `'month'`
   * (`_app/index.tsx` dagi `useState<DashboardPeriod>('month')`) —
   * olingan: kesh kaliti ikkalasida bir xil bo'lib, qaysi sahifa oldin
   * ochilishidan qat'i nazar so'rov BIR MARTA ketadi. `onProbation`ning
   * o'zi davrga bog'liq emas (joriy holat), lekin endpoint baribir
   * parametr talab qiladi.
   *
   * Yon menyu `AppShell` ichida BIR MARTA o'rnatiladi va sahifadan
   * sahifaga o'tishda qayta chizilmaydi, ya'ni bu hook ham faqat bir
   * marta ishga tushadi; keyingi navigatsiyalarda `useDashboardStats`
   * ning `staleTime`i (1 daqiqa) so'rovni takrorlashning oldini oladi.
   */
  const stats = useDashboardStats('month')

  /*
   * Band nomi → sanoq. `0` yoki hali yuklanmagan holat ATAYLAB xarita
   * ichiga tushmaydi: `LinkRow` faqat xaritada BOR kalitga belgi chizadi,
   * ya'ni bo'sh natija — hech qanday shovqinsiz «oddiy» yorliq.
   */
  const counts: Partial<Record<string, number>> = {}
  if (stats.data && stats.data.onProbation > 0) {
    counts['probation'] = stats.data.onProbation
  }

  /**
   * Ochiq guruhlar.
   *
   * Sukut bo'yicha BO'SH — barcha guruhlar yopiq. Ichidagi sahifaga
   * o'tilganda guruh o'zi ochiladi (pastdagi effekt), lekin keyin uni
   * qo'lda yopish mumkin: «qayerdaman» degan savolga javob bir marta
   * kerak, doimiy ochiq turish emas.
   */
  const [openGroups, setOpenGroups] = useState<ReadonlySet<string>>(new Set())

  useEffect(() => {
    const inside = groups().filter((group) =>
      group.children!.some((child) => onPath(child, pathname)),
    )

    if (inside.length === 0) return

    setOpenGroups((previous) => {
      const missing = inside.filter((group) => !previous.has(group.key))
      if (missing.length === 0) return previous

      const next = new Set(previous)
      for (const group of missing) next.add(group.key)
      return next
    })
  }, [pathname])

  const toggleGroup = (key: string) => {
    setOpenGroups((previous) => {
      const next = new Set(previous)
      if (!next.delete(key)) next.add(key)
      return next
    })
  }

  return (
    <aside
      className={cn(
        'flex h-dvh shrink-0 flex-col border-r transition-[width] duration-300 ease-[var(--ease-ios-out)]',
        'border-[var(--border)] bg-[var(--bg-elevated)]',
        collapsed ? 'w-[76px]' : 'w-[264px]',
      )}
    >
      {/* ---------- Logotip ---------- */}
      <div className="p-3">
        <Link
          to="/"
          className={cn(
            'press flex items-center gap-3 rounded-md p-2 hover:bg-[var(--bg-hover)]',
            collapsed && 'justify-center',
          )}
        >
          <BonviMark className="size-8 shrink-0 text-[var(--brand)]" />
          {!collapsed && (
            <span className="min-w-0">
              <span className="block truncate-1 text-subhead font-semibold">
                {t('app.name')}
              </span>
              <span className="block truncate-1 text-caption2 text-[var(--fg-secondary)]">
                {t('app.company')}
              </span>
            </span>
          )}
        </Link>
      </div>

      {/* ---------- Menyu ---------- */}
      <nav className="flex-1 space-y-5 overflow-y-auto px-3 pb-4">
        {NAV.map((section, index) => {
          /*
           * Ruxsatga bog'liq bandlar shu yerda filtrlanadi
           * (`nav-items.ts` dagi `permission` izohiga qarang). Filtrdan
           * keyin bo'sh qolgan bo'lim UMUMAN chizilmaydi — aks holda
           * HR menejeri uchun bo'sh sarlavhali («Tizim») guruh osilib
           * qolardi, garchi ichida bironta ham band ko'rinmasa ham.
           */
          const items = section.items.filter(
            (item) => item.permission === undefined || hasPermission(item.permission),
          )

          if (items.length === 0) return null

          return (
            <div key={section.key ?? `group-${index}`}>
              {section.key && !collapsed && (
                <p className="mb-1.5 px-3 text-caption2 font-semibold tracking-wider text-[var(--fg-secondary)] uppercase">
                  {t(`nav.${section.key}`)}
                </p>
              )}
              {section.key && collapsed && (
                <div className="mx-3 mb-2 h-px bg-[var(--border)]" />
              )}

              <ul className="space-y-0.5">
                {items.map((item) =>
                  item.children ? (
                    <li key={item.key}>
                      <GroupRow
                        item={item}
                        collapsed={collapsed}
                        open={openGroups.has(item.key)}
                        onToggle={() => {
                          // Yig'ilgan menyuda yorliqlar ko'rinmaydi, ya'ni
                          // guruhni ochishning ma'nosi yo'q. Shu sababli
                          // avval menyuning o'zi yoziladi.
                          if (collapsed) onToggle()
                          toggleGroup(item.key)
                        }}
                      />

                      {!collapsed && openGroups.has(item.key) && (
                        <ul className="mt-0.5 space-y-0.5">
                          {item.children.map((child) => (
                            <li key={child.key}>
                              <LinkRow
                                item={child}
                                collapsed={false}
                                active={isActive(child, pathname, search)}
                                nested
                              />
                            </li>
                          ))}
                        </ul>
                      )}
                    </li>
                  ) : (
                    <li key={item.key}>
                      <LinkRow
                        item={counts[item.key] ? { ...item, count: counts[item.key] } : item}
                        collapsed={collapsed}
                        active={isActive(item, pathname, search)}
                      />
                    </li>
                  ),
                )}
              </ul>
            </div>
          )
        })}
      </nav>

      {/* ---------- Yig'ish ---------- */}
      <div className="border-t border-[var(--border)] p-3">
        <button
          type="button"
          onClick={onToggle}
          title={t(collapsed ? 'common.expandSidebar' : 'common.collapseSidebar')}
          aria-label={t(collapsed ? 'common.expandSidebar' : 'common.collapseSidebar')}
          className={cn(
            'press flex h-10 w-full items-center gap-3 rounded-md px-3',
            'text-[var(--fg-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)]',
            collapsed && 'justify-center px-0',
          )}
        >
          {collapsed ? (
            <PanelLeftOpen className="size-[18px] shrink-0" />
          ) : (
            <PanelLeftClose className="size-[18px] shrink-0" />
          )}
          {!collapsed && (
            <span className="truncate-1 text-subhead">{t('common.collapseSidebar')}</span>
          )}
        </button>
      </div>
    </aside>
  )
}

/**
 * Menyu bandining umumiy ko'rinishi — havola ham, guruh sarlavhasi ham
 * shu sinflar bilan chiziladi.
 *
 * Ajratilgan sabab: ikkalasi bir xil balandlik, bir xil ichki bo'shliq va
 * bir xil holat ranglariga ega bo'lishi kerak. Nusxa qilinsa, ular
 * ertami-kechmi bir-biridan ajralib, menyu «qaltirab» ko'rinardi.
 */
function rowClass(collapsed: boolean, active: boolean, nested = false, disabled = false) {
  return cn(
    'group flex h-10 w-full items-center gap-3 rounded-md px-3 text-subhead',
    // «press» — bosilganda kichraytiruvchi animatsiya. O'chirilgan qatorda
    // ATAYLAB yo'q: qator umuman bosiladigan yuza EMAS, kichrayish esa
    // aksincha «buni bosish mumkin» degan yolg'on va'da bo'lardi.
    !disabled && 'press',
    collapsed && 'justify-center px-0',
    // Ichki band chapdan suriladi: ierarxiya CHIZIQ bilan emas, bo'shliq
    // bilan beriladi — iOS «Settings» dagi kabi.
    nested && 'pl-9',
    disabled
      ? 'cursor-not-allowed text-[var(--fg-tertiary)]'
      : // iOS Settings naqshi: yorliq to'liq yorqin, ierarxiya ikona rangi
        // bilan beriladi — shaffoflik bilan emas. Shaffoflik qorong'i fonda
        // matnni o'qib bo'lmas holga keltiradi.
        active
        ? 'bg-[var(--accent-soft)] font-medium text-[var(--accent-on-soft)]'
        : 'text-[var(--fg)] hover:bg-[var(--bg-hover)]',
  )
}

/**
 * `soon` bandlar bu funksiyani ENDI chaqirmaydi — ular o'z ikonasi
 * rangini `LinkRow` ichida to'g'ridan-to'g'ri, o'chirilgan holat uchun
 * belgilaydi (pastga qarang). Shu sababli bu yerda faqat oddiy ikki holat:
 * faol va oddiy.
 */
function iconClass(active: boolean) {
  return cn(
    'size-[18px] shrink-0 transition-colors',
    active
      ? 'text-[var(--accent-on-soft)]'
      : 'text-[var(--fg-secondary)] group-hover:text-[var(--fg)]',
  )
}

function LinkRow({
  item,
  collapsed,
  active,
  nested = false,
}: {
  item: NavItem
  collapsed: boolean
  active: boolean
  nested?: boolean
}) {
  const { t, num } = useLocale()
  // `undefined` yoki `0` — belgisiz, oddiy qator (`0` ma'lumot emas, shovqin).
  const count = item.count !== undefined && item.count > 0 ? item.count : null
  const label = t(`nav.${item.key}`)

  /*
   * «soon» band — hali qurilmagan bo'lim, marshrut fayli yo'q.
   *
   * ATAYLAB `<Link>` emas: `to={item.to}` mavjud bo'lmagan sahifaga olib
   * borardi. Shu bilan birga band menyudan OLIB TASHLANMAYDI — yo'l
   * xaritasini ko'rsatib turadi (`nav-items.ts` boshidagi izohga qarang).
   *
   * `disabled` emas, `aria-disabled="true"`: haqiqiy `disabled` atributi
   * elementni sichqoncha hodisalaridan ham butunlay chetlashtiradi —
   * Chrome/Firefox'da `title` maslahat oynasi HAM ko'rinmay qoladi, ya'ni
   * yig'ilgan menyuda (faqat ikona) band nima ekanini bilib bo'lmay
   * qolardi. `aria-disabled` esa elementni ko'rinadigan, sichqoncha va
   * klaviatura bilan TOPILADIGAN, lekin harakatsiz qoldiradi (`onClick`
   * yo'q — bosilganda shunchaki hech narsa bo'lmaydi) — skrinriderlar buni
   * «o'chirilgan» deb o'qiydi, WAI-ARIA'ning o'zi ham shu holat uchun aynan
   * shu naqshni tavsiya qiladi.
   */
  if (item.soon) {
    const comingSoon = t('common.comingSoon')

    return (
      <button
        type="button"
        aria-disabled="true"
        // Ilgari faqat yig'ilgan holatda edi — endi HAR DOIM: pastdagi
        // yorliq endi matnsiz (izohga qarang), shu sababli «Tez orada»
        // degan ma'no faqat shu maslahat oynasi orqali qoladi.
        title={`${label} — ${comingSoon}`}
        className={rowClass(collapsed, false, nested, true)}
      >
        <item.icon
          className="size-[18px] shrink-0 text-[var(--fg-tertiary)]"
          strokeWidth={1.9}
        />

        {!collapsed && (
          <>
            <span className="truncate-1 flex-1 text-left">{label}</span>
            {/*
              Ilgari bu yerda «Tez orada»/«Coming soon» kabi TO'LIQ matnli
              yorliq turardi (~70-80px) — uzun nomlarda («Rag'bat va
              jarima») yorliqqa atigi ~100px qolib, matn «Rag'bat va…»
              bo'lib kesilib qolardi (ekranda ko'rinib, HR'dan xabar
              kelgan xato). Yorliqning o'zi BU YERDA yagona o'zgaruvchan
              uzunlikdagi element — uni qisqartirish emas, YONIDAGI
              belgini qisqartirish to'g'ri yechim: soat ikonchasi bir xil
              ma'noni (~vaqt, «hali emas») atigi ~28px da beradi, matn
              esa yuqoridagi `title`ga ko'chadi (skrinriderlar buni ham
              o'qiydi — pastdagi `sr-only`). Bo'shagan ~50px endi to'liq
              yorliqqa tegadi: to'rt tilning barchasida (eng uzuni —
              inglizcha «Rewards & penalties», 20 belgi) matn endi
              kesilmaydi yoki `flags.reason` ustunidagi kabi qabul
              qilingan darajada kesiladi (`flag-list-page.tsx`).
            */}
            <Badge tone="neutral" size="sm">
              <Clock className="size-3" strokeWidth={2.5} aria-hidden />
              <span className="sr-only">{comingSoon}</span>
            </Badge>
          </>
        )}
      </button>
    )
  }

  return (
    <Link
      to={item.to}
      // Parametrlar bandning o'zida yozilgan (`nav-items.ts`). Berilmagan
      // bo'lsa `undefined` — router avvalgi parametrlarni saqlab qolmaydi
      // va toza manzil ochiladi.
      search={item.search}
      title={collapsed ? label : undefined}
      aria-current={active ? 'page' : undefined}
      className={rowClass(collapsed, active, nested)}
    >
      {/*
        `relative` — yig'ilgan holatdagi nuqta ikonaning ustki-o'ng
        burchagiga ANIQ shu wrapper ichida joylashadi.
      */}
      <span className="relative shrink-0">
        <item.icon className={iconClass(active)} strokeWidth={active ? 2.2 : 1.9} />

        {/*
          Yig'ilgan menyuda (76px, faqat ikona) to'liq raqamga joy yo'q —
          "12" ikonaning ustiga tushib, ikkalasi ham o'qilmas bo'lardi.
          Shu sababli bu holatda faqat NUQTA: «bu yerda diqqat talab
          qiluvchi narsa bor», aniq soni esa menyu ochilganda ko'rinadi.
          Halqa (`ring`) — orqa fondan ajratish uchun, xuddi shu rangda
          bo'lgan ikonaga qo'shilib ketmasin.
        */}
        {collapsed && count !== null && (
          <span
            aria-hidden
            className="absolute -top-0.5 -right-0.5 size-2 rounded-full bg-ios-orange ring-2 ring-[var(--bg-elevated)]"
          />
        )}
      </span>

      {!collapsed && <span className="truncate-1 flex-1">{label}</span>}

      {!collapsed && count !== null && (
        <Badge tone="warning" size="sm">
          {num(count)}
        </Badge>
      )}
    </Link>
  )
}

/**
 * Ochiluvchi guruh sarlavhasi.
 *
 * ATAYLAB tugma, havola emas: bosilganda hech qayerga o'tilmaydi va
 * shuning uchun uni havola qilib ko'rsatish yolg'on va'da bo'lardi.
 * Ekran o'quvchi uchun `aria-expanded` holatni aytib turadi.
 */
function GroupRow({
  item,
  collapsed,
  open,
  onToggle,
}: {
  item: NavItem
  collapsed: boolean
  open: boolean
  onToggle: () => void
}) {
  const { t } = useLocale()
  const label = t(`nav.${item.key}`)

  return (
    <button
      type="button"
      onClick={onToggle}
      aria-expanded={open}
      title={collapsed ? label : undefined}
      className={rowClass(collapsed, false)}
    >
      <item.icon className={iconClass(false)} strokeWidth={1.9} />

      {!collapsed && (
        <>
          <span className="truncate-1 flex-1 text-left">{label}</span>
          {/*
            Burchak belgisi 90° ga aylanadi — iOS'da ochiluvchi ro'yxatning
            odatiy ishorasi. Yangi ikonka almashtirish emas: aylanish
            harakatning O'ZINI ko'rsatadi va ko'z uni kuzatib boradi.
          */}
          <ChevronRight
            className={cn(
              'size-4 shrink-0 text-[var(--fg-tertiary)]',
              'transition-transform duration-200 ease-[var(--ease-ios)]',
              open && 'rotate-90',
            )}
            strokeWidth={2.2}
          />
        </>
      )}
    </button>
  )
}

/** Ochiluvchi guruhga ega bandlar — barcha bo'limlardan yig'iladi. */
function groups(): NavItem[] {
  return NAV.flatMap((section) => section.items.filter((item) => item.children))
}

/**
 * Menyudagi HAMMA band — ochiluvchi guruhlarning ichidagilari bilan birga.
 *
 * NEGA kerak: `isActive` parametrsiz bandni tekshirayotganda «shu yo'lakda
 * parametrli qarindoshi bormi?» deb qaraydi. Faqat yuqori darajaga
 * qaralsa, guruh ICHIDAGI juftlik topilmasdi — «Nomzodlar» va «Rad
 * etilganlar» aynan shunday juftlik va ikkalasi birdaniga yonib turardi.
 */
function allItems(): NavItem[] {
  return NAV.flatMap((section) =>
    section.items.flatMap((item) => [item, ...(item.children ?? [])]),
  )
}

/** Band shu yo'lakdami (qidiruv parametrlari hisobga olinmaydi). */
function onPath(item: NavItem, pathname: string): boolean {
  return item.to === '/' ? pathname === '/' : pathname.startsWith(item.to)
}

/**
 * Menyudagi band hozir ochiqmi.
 *
 * Yo'l bilan taqqoslash yetmaydi: «Xodimlar» va «Ishdan ketganlar» BITTA
 * sahifaga olib boradi va faqat qidiruv parametri bilan farq qiladi. Faqat
 * yo'lga qarasak, ikkalasi birdaniga yonib turardi va HR qaysi ro'yxatda
 * turganini menyudan bilolmasdi.
 *
 * Qoida oddiy: parametrli band FAQAT o'sha parametrlar manzilda turganda
 * yonadi; parametrsizi esa — shu yo'lakdagi parametrli variantlardan
 * birortasi yonmagan bo'lsa.
 */
function isActive(
  item: NavItem,
  pathname: string,
  search: Record<string, unknown>,
): boolean {
  if (!onPath(item, pathname)) return false

  if (item.search) return matches(item.search, search)

  return !allItems().some(
    (other) =>
      other !== item &&
      other.to === item.to &&
      other.search !== undefined &&
      matches(other.search, search),
  )
}

function matches(
  expected: Readonly<Record<string, string>>,
  actual: Record<string, unknown>,
): boolean {
  return Object.entries(expected).every(([key, value]) => String(actual[key] ?? '') === value)
}
