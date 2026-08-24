import { Moon, Sun } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { useTheme } from '@/shared/theme/theme-provider'
import { cn } from '@/shared/lib/cn'
import { Button } from './button'

/**
 * Yorug' ↔ tungi rejim.
 *
 * Ikki holat, uchta emas: «tizim bo'yicha» degan variant interfeysda yo'q.
 * Tizim sozlamasi faqat birinchi ochilishda hisobga olinadi (theme-provider).
 *
 * Ikonalar bir-birining ustida turadi va aylanib almashadi — iOS'dagi kabi
 * bir holatdan ikkinchisiga silliq o'tish.
 */
export function ThemeToggle() {
  const { theme, toggle } = useTheme()
  const { t } = useLocale()

  const label = theme === 'dark' ? t('common.themeLight') : t('common.themeDark')
  const dark = theme === 'dark'

  return (
    <Button
      variant="ghost"
      size="icon-sm"
      onClick={toggle}
      title={label}
      aria-label={label}
      className="relative overflow-hidden"
    >
      <Sun
        className={cn(
          'absolute transition-all duration-300 ease-[var(--ease-ios-out)]',
          dark ? 'scale-50 -rotate-90 opacity-0' : 'scale-100 rotate-0 opacity-100',
        )}
      />
      <Moon
        className={cn(
          'absolute transition-all duration-300 ease-[var(--ease-ios-out)]',
          dark ? 'scale-100 rotate-0 opacity-100' : 'scale-50 rotate-90 opacity-0',
        )}
      />
    </Button>
  )
}
