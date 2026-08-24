import { Bell, Search } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Button, Input } from '@/shared/ui'
import { LanguageSwitcher } from '@/shared/ui/language-switcher'
import { ThemeToggle } from '@/shared/ui/theme-toggle'
import { UserMenu } from './user-menu'

/**
 * Yuqori panel. iOS uslubida shaffof, orqa foni bulanadi (blur-bar) —
 * kontent ostidan o'tganda ko'rinib turadi.
 *
 * Tarkibi: butun bo'sh kenglikni egallagan qidiruv → o'ngda til, ko'rinish,
 * bildirishnoma va eng chekkada foydalanuvchi menyusi.
 * Menyuni yig'ish tugmasi bu yerda emas — u yon menyuning o'z pastida.
 */
export function Topbar() {
  const { t } = useLocale()

  return (
    <header className="blur-bar sticky top-0 z-30 border-b border-[var(--border)]">
      <div className="flex h-16 items-center gap-3 px-5 lg:gap-4 lg:px-6">
        {/* Qidiruv — bo'sh kenglikning hammasini egallaydi */}
        <div className="min-w-0 flex-1">
          <Input
            icon={Search}
            shape="pill"
            size="lg"
            type="search"
            placeholder={t('common.searchPlaceholder')}
            aria-label={t('common.search')}
          />
        </div>

        <div className="flex shrink-0 items-center gap-1.5 sm:gap-2">
          <LanguageSwitcher />

          <ThemeToggle />

          <Button
            variant="ghost"
            size="icon-sm"
            title={t('common.notifications')}
            aria-label={t('common.notifications')}
            className="relative"
          >
            <Bell />
            <span className="absolute top-1.5 right-1.5 size-2 rounded-full bg-ios-red ring-2 ring-[var(--bg)]" />
          </Button>

          {/* Ajratgich — hisob qismini boshqa boshqaruvlardan ajratadi */}
          <span className="mx-0.5 hidden h-6 w-px bg-[var(--border)] sm:block" />

          <UserMenu />
        </div>
      </div>
    </header>
  )
}
