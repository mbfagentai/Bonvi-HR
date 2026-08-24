import { Link, useRouterState } from '@tanstack/react-router'
import { ChevronRight, PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { BonviMark } from '@/shared/ui'
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
  const pathname = useRouterState({ select: (s) => s.location.pathname })
  const search = useRouterState({ select: (s) => s.location.search })

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
        {NAV.map((section, index) => (
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
              {section.items.map((item) =>
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
                      item={item}
                      collapsed={collapsed}
                      active={isActive(item, pathname, search)}
                    />
                  </li>
                ),
              )}
            </ul>
          </div>
        ))}
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
function rowClass(collapsed: boolean, active: boolean, nested = false) {
  return cn(
    'press group flex h-10 w-full items-center gap-3 rounded-md px-3 text-subhead',
    collapsed && 'justify-center px-0',
    // Ichki band chapdan suriladi: ierarxiya CHIZIQ bilan emas, bo'shliq
    // bilan beriladi — iOS «Settings» dagi kabi.
    nested && 'pl-9',
    // iOS Settings naqshi: yorliq to'liq yorqin, ierarxiya ikona rangi
    // bilan beriladi — shaffoflik bilan emas. Shaffoflik qorong'i fonda
    // matnni o'qib bo'lmas holga keltiradi.
    active
      ? 'bg-[var(--accent-soft)] font-medium text-[var(--accent-on-soft)]'
      : 'text-[var(--fg)] hover:bg-[var(--bg-hover)]',
  )
}

function iconClass(active: boolean, soon: boolean | undefined) {
  return cn(
    'size-[18px] shrink-0 transition-colors',
    active
      ? 'text-[var(--accent-on-soft)]'
      : soon
        ? 'text-[var(--fg-tertiary)] group-hover:text-[var(--fg-secondary)]'
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
  const { t } = useLocale()

  return (
    <Link
      to={item.to}
      // Parametrlar bandning o'zida yozilgan (`nav-items.ts`). Berilmagan
      // bo'lsa `undefined` — router avvalgi parametrlarni saqlab qolmaydi
      // va toza manzil ochiladi.
      search={item.search}
      title={collapsed ? t(`nav.${item.key}`) : undefined}
      aria-current={active ? 'page' : undefined}
      className={rowClass(collapsed, active, nested)}
    >
      <item.icon className={iconClass(active, item.soon)} strokeWidth={active ? 2.2 : 1.9} />
      {!collapsed && <span className="truncate-1 flex-1">{t(`nav.${item.key}`)}</span>}
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
      <item.icon className={iconClass(false, undefined)} strokeWidth={1.9} />

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

  return !NAV.some((section) =>
    section.items.some((other) =>
      other !== item && other.to === item.to && other.search
        ? matches(other.search, search)
        : false,
    ),
  )
}

function matches(
  expected: Readonly<Record<string, string>>,
  actual: Record<string, unknown>,
): boolean {
  return Object.entries(expected).every(([key, value]) => String(actual[key] ?? '') === value)
}
