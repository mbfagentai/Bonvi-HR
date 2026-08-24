import { Link, useRouterState } from '@tanstack/react-router'
import { PanelLeftClose, PanelLeftOpen } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { BonviMark } from '@/shared/ui'
import { NAV } from './nav-items'

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
          {/*
            Logotip — brend navy'sida, tint'da emas. Bu tizimdagi yagona
            joy: iOS HIG «Color» tint rangini harakat signali qilib
            saqlaydi, brend rangi esa faqat identifikatsiya uchun qoladi.
            Tungi rejimda navy fonda ko'rinmay qoladi, shu sababli
            shkalaning yorug' pog'onasi (kartaga nisbatan 7.3:1).
          */}
          <BonviMark className="size-10 shrink-0 text-brand-700 dark:text-brand-300" />
          {!collapsed && (
            <span className="min-w-0">
              <span className="block truncate text-callout leading-tight font-semibold">
                {t('app.name')}
              </span>
              <span className="block truncate text-caption1 text-[var(--fg-secondary)]">
                {t('app.tagline')}
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
              {section.items.map((item) => {
                const active =
                  item.to === '/' ? pathname === '/' : pathname.startsWith(item.to)

                return (
                  <li key={item.key}>
                    <Link
                      to={item.to}
                      title={collapsed ? t(`nav.${item.key}`) : undefined}
                      aria-current={active ? 'page' : undefined}
                      className={cn(
                        'press group flex h-10 items-center gap-3 rounded-md px-3 text-subhead',
                        collapsed && 'justify-center px-0',
                        // iOS Settings naqshi: yorliq to'liq yorqin,
                        // ierarxiya ikona rangi bilan beriladi — shaffoflik bilan emas.
                        // Shaffoflik qorong'i fonda matnni o'qib bo'lmas holga keltiradi.
                        active
                          ? 'bg-[var(--accent-soft)] font-medium text-[var(--accent-on-soft)]'
                          : 'text-[var(--fg)] hover:bg-[var(--bg-hover)]',
                      )}
                    >
                      <item.icon
                        className={cn(
                          'size-[18px] shrink-0 transition-colors',
                          active
                            ? 'text-[var(--accent-on-soft)]'
                            : item.soon
                              ? 'text-[var(--fg-tertiary)] group-hover:text-[var(--fg-secondary)]'
                              : 'text-[var(--fg-secondary)] group-hover:text-[var(--fg)]',
                        )}
                        strokeWidth={active ? 2.2 : 1.9}
                      />
                      {!collapsed && (
                        <span className="truncate-1 flex-1">{t(`nav.${item.key}`)}</span>
                      )}
                    </Link>
                  </li>
                )
              })}
            </ul>
          </div>
        ))}
      </nav>

      {/* ---------- Menyuni yig'ish ---------- */}
      <div className="border-t border-[var(--border)] p-3">
        <button
          type="button"
          onClick={onToggle}
          title={collapsed ? t('common.expandSidebar') : t('common.collapseSidebar')}
          aria-label={collapsed ? t('common.expandSidebar') : t('common.collapseSidebar')}
          aria-expanded={!collapsed}
          className={cn(
            'press flex h-12 w-full items-center gap-3 rounded-md px-3 text-subhead font-medium',
            'text-[var(--fg)] hover:bg-[var(--bg-hover)]',
            collapsed && 'justify-center px-0',
          )}
        >
          {collapsed ? (
            <PanelLeftOpen className="size-6 shrink-0" strokeWidth={1.9} />
          ) : (
            <>
              <PanelLeftClose className="size-6 shrink-0" strokeWidth={1.9} />
              <span className="truncate-1 flex-1 text-left">
                {t('common.collapseSidebar')}
              </span>
            </>
          )}
        </button>
      </div>
    </aside>
  )
}
