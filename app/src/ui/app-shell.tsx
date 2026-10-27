import clsx from 'clsx'
import { useEffect, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router-dom'

import { useMe } from '@/data/queries'
import { onTeamRevoked, readTeamHint, writeTeamHint } from '@/data/team'
import { useFormat } from '@/lib/format'
import { useOnline } from '@/lib/use-online'
import { Icon, type IconName } from './icon'

/**
 * Ilova ramkasi (UX §E.1).
 *
 * ⚠️ Balandligi `100vh` bo'lgan, ichida aylanadigan qobiq ATAYLAB
 * yo'q. Hujjatning o'zi aylanadi, tab paneli esa `fixed` — shunda
 * iOS'dagi mashhur «100vh noto'g'ri» muammosi umuman tug'ilmaydi
 * (UX §F.2). Buning evaziga har bir ekran pastdan bo'sh joy
 * qoldiradi: `pb-tabbar`.
 */

interface Tab {
  to: string
  labelKey: string
  icon: IconName
}

const TABS: Tab[] = [
  { to: '/', labelKey: 'tab.home', icon: 'home' },
  { to: '/taqvim', labelKey: 'tab.calendar', icon: 'calendar' },
  { to: '/sorovlar', labelKey: 'tab.requests', icon: 'document-text' },
  { to: '/profil', labelKey: 'tab.profile', icon: 'user' },
]

/**
 * «Jamoa» — beshinchi tab, «So'rovlar» va «Profil» orasida, FAQAT bo'lim
 * boshlig'iga (SPEC-jamoam §F). Boshliq bo'lmagan xodim uchun bu ekran
 * mavjud emas — «yopiq, sababi …» holati ham emas: unga hech qanday ish
 * qilib bo'lmaydi va u xodimga tegishli savol ham emas.
 */
const TEAM_TAB: Tab = { to: '/jamoa', labelKey: 'tab.team', icon: 'users' }

function tabsFor(isTeamLead: boolean): Tab[] {
  if (!isTeamLead) return TABS
  return TABS.slice(0, 3).concat([TEAM_TAB], TABS.slice(3))
}

export function TabBar() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  // `me` keshdan darhol o'qiladi; tab paneli qayta ulanganda fonda bitta
  // yangilash ketadi — boshliqlik olib tashlansa tab ham shu yo'l bilan yo'qoladi
  const me = useMe()
  const { refetch: refetchMe } = me
  useEffect(() => onTeamRevoked(refetchMe), [refetchMe])

  // `me` kelguncha — oldingi ochilishdagi qiymat (tab paneli sakramasin), keyin — serverniki
  const isTeamLead = me.data ? me.data.isTeamLead === true : readTeamHint()
  useEffect(() => {
    if (me.data) writeTeamHint(me.data.isTeamLead === true)
  }, [me.data])

  const tabs = tabsFor(isTeamLead)
  const activeTab = tabs.filter((tab) => isTabActive(tab.to, location.pathname))[0] ?? tabs[0]

  const go = (to: string): void => {
    if (activeTab && activeTab.to === to) {
      // Faol tabni qayta bosish — tepaga chiqish. Oy yoki filtr
      // ATAYLAB tiklanmaydi: noto'g'ri tegish tanlangan oyni yo'qotsa,
      // uzun aylantirishdan ham yomonroq bo'lardi (UX §A.2).
      window.scrollTo(0, 0)
      return
    }

    /*
      Tarix qoidasi (UX §B.2): tablar orasida yurish tarixga iz
      QOLDIRMAYDI. «Asosiy» dan boshqa tabga o'tishda bitta yozuv
      qo'shiladi — shunda «orqaga» tugmasi doim «Asosiy» ga olib
      keladi; qolgan barcha o'tishlar joriy yozuvni almashtiradi.
    */
    const fromHome = location.pathname === '/' || location.pathname === ''
    navigate(to, { replace: !fromHome })
  }

  return (
    <nav
      role="tablist"
      aria-label={t('app.name')}
      className="fixed bottom-0 left-0 right-0 z-20 border-t border-border bg-bg-elevated pb-safe"
    >
      <div className="mx-auto flex w-full max-w-app">
        {tabs.map((tab) => {
          const active = activeTab === tab

          return (
            <button
              key={tab.to}
              type="button"
              role="tab"
              aria-selected={active}
              aria-current={active ? 'page' : undefined}
              onClick={() => go(tab.to)}
              className={clsx(
                'flex h-12 min-w-0 flex-1 flex-col items-center justify-center px-1 pt-1 active:bg-bg-pressed',
                active ? 'text-accent-text' : 'text-fg-tertiary',
              )}
            >
              <Icon name={tab.icon} className="h-6 w-6" />
              <span className="mt-0.5 w-full truncate text-center text-caption2">
                {t(tab.labelKey)}
              </span>
            </button>
          )
        })}
      </div>
    </nav>
  )
}

function isTabActive(to: string, pathname: string): boolean {
  if (to === '/') return pathname === '/' || pathname === ''
  return pathname === to || pathname.indexOf(`${to}/`) === 0
}

/** Tab ildizi: katta sarlavha, tab paneli ko'rinadi, orqaga yo'q. */
export function TabScreen({
  title,
  subtitle,
  trailing,
  staleSince,
  bottomSpace = false,
  children,
}: {
  title: string
  subtitle?: ReactNode
  trailing?: ReactNode
  /** Ekrandagi ma'lumot qachon olingan — oflayn chizig'i uchun. */
  staleSince?: Date | null
  /** A BottomAction sits over the tab bar — keep content clear of it too. */
  bottomSpace?: boolean
  children: ReactNode
}) {
  return (
    <div
      className={clsx(
        'mx-auto w-full max-w-app px-4 pt-safe',
        bottomSpace ? 'pb-tabbar-action' : 'pb-tabbar',
      )}
    >
      <header className="mb-4 flex items-start justify-between">
        <div className="min-w-0">
          <h1 className="text-title1 font-bold text-fg">{title}</h1>
          {subtitle ? <div className="mt-1 text-subhead text-fg-secondary">{subtitle}</div> : null}
        </div>
        {trailing ? <div className="ml-3 shrink-0">{trailing}</div> : null}
      </header>

      <StaleBar since={staleSince ?? null} />

      <div className="space-y-3">{children}</div>
    </div>
  )
}

/**
 * Ichkariga ochilgan ekran: orqaga chevron, markazda sarlavha, tab
 * paneli YO'Q. Bu farq mutlaq (UX §B.1): tab paneli ko'rinsa orqaga
 * yo'q, orqaga ko'rinsa tab paneli yo'q.
 */
export function PushedScreen({
  title,
  action,
  children,
  onBack,
  bottomSpace = false,
}: {
  title: string
  action?: ReactNode
  children: ReactNode
  onBack?: () => void
  /** Pastda qadalgan tugma bor — kontent uning ostiga tushmasin. */
  bottomSpace?: boolean
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  return (
    <div className="mx-auto w-full max-w-app">
      <header className="sticky top-0 z-10 flex h-12 items-center border-b border-border bg-bg-elevated px-1 pt-safe">
        <button
          type="button"
          aria-label={t('common.back')}
          onClick={() => (onBack ? onBack() : navigate(-1))}
          className="flex h-11 w-11 items-center justify-center rounded-control text-accent-text active:bg-bg-pressed"
        >
          <Icon name="chevron-left" />
        </button>

        <h1 className="min-w-0 flex-1 truncate text-center text-body font-semibold text-fg">
          {title}
        </h1>

        <div className="flex h-11 min-w-[44px] items-center justify-end pr-1">{action}</div>
      </header>

      <div className={clsx('px-4 pt-4', bottomSpace ? 'pb-[120px]' : 'pb-8')}>
        <StaleBar since={null} />
        <div className="space-y-3">{children}</div>
      </div>
    </div>
  )
}

/**
 * Oflayn chizig'i (UX §E.9 dagi beshinchi holat).
 *
 * Butun ekranni egallamaydi: keshdagi davomat haqiqatan foydali va
 * uni «internet yo'q» rasmi ostiga yashirish keshlamaslikdan ham
 * yomonroq bo'lardi.
 */
function StaleBar({ since }: { since: Date | null }) {
  const { t } = useTranslation()
  const format = useFormat()
  const online = useOnline()

  if (online) return null

  return (
    <div className="mb-3 rounded-control bg-tint-late px-3 py-2 text-footnote text-fg">
      {/* Vaqt — ma'lumot KESHGA tushgan lahza, hozirgi soat emas:
          «hozir» deb ko'rsatilgan eski ma'lumot yolg'on bo'lardi. */}
      {since ? t('state.offlineBar', { time: format.time(since) }) : t('state.offlineTitle')}
    </div>
  )
}
