import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { Check, ChevronDown } from 'lucide-react'

import { LOCALES, type LocaleCode, type LocaleMeta } from '@/shared/i18n/locales'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'

/**
 * Til almashtirgich — Apple uslubidagi ochiluvchi ro'yxat.
 *
 * Nega segmented control emas: to'rt til uchun qisqartmalar (UZ · ЎЗ · RU · EN)
 * kichik o'lchamda o'qilmaydi — «ЎЗ» ko'zga «Ў3» bo'lib ko'rinadi.
 * Bayroq + tilning o'z nomi bir qarashda tanib olinadi.
 *
 * O'zbek tilining ikki yozuvi bir xil bayroq bilan ko'rsatiladi, lekin
 * ostidagi «Lotin» / «Кирилл» qatori ularni ajratib turadi.
 */
export function LanguageSwitcher({ className }: { className?: string }) {
  const { locale, setLocale, meta, t } = useLocale()

  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <button
          type="button"
          aria-label={t('common.language')}
          className={cn(
            'press group inline-flex h-8 items-center gap-1.5 rounded-full border py-0 pr-2 pl-1',
            'border-[var(--border-strong)] bg-[var(--bg-elevated)]',
            'hover:bg-[var(--bg-hover)]',
            'data-[state=open]:border-[var(--accent)] data-[state=open]:bg-[var(--bg-hover)]',
            'transition-colors',
            className,
          )}
        >
          <Flag meta={meta} />
          <span className="text-caption1 font-semibold tracking-wide">{meta.short}</span>
          <ChevronDown
            className="size-3 text-[var(--fg-tertiary)] transition-transform duration-200 group-data-[state=open]:rotate-180"
            strokeWidth={2.5}
          />
        </button>
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content
          align="end"
          sideOffset={8}
          className={cn(
            // `pop-panel` — ochilish/yopilish animatsiyasi globals.css da
            'pop-panel z-50 min-w-[236px] rounded-xl border p-1.5',
            'border-[var(--border)] bg-[var(--bg-elevated)] shadow-floating',
          )}
        >
          <DropdownMenu.Label
            className="px-2.5 pt-1.5 pb-2 text-caption2 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase"
          >
            {t('common.language')}
          </DropdownMenu.Label>

          {LOCALES.map((item) => {
            const active = item.code === locale

            return (
              <DropdownMenu.Item
                key={item.code}
                onSelect={() => setLocale(item.code as LocaleCode)}
                className={cn(
                  'flex h-11 cursor-pointer items-center gap-2.5 rounded-md px-2 outline-none select-none',
                  'transition-colors duration-100',
                  active
                    ? 'bg-[var(--accent-soft)]'
                    : 'data-[highlighted]:bg-[var(--bg-hover)]',
                )}
              >
                <Flag meta={item} size="md" />

                <span className="min-w-0 flex-1 leading-tight">
                  <span
                    className={cn(
                      'block truncate text-subhead',
                      active
                        ? 'font-semibold text-[var(--accent-on-soft)]'
                        : 'font-medium',
                    )}
                  >
                    {item.label}
                  </span>
                  <span className="block truncate text-caption2 text-[var(--fg-tertiary)]">
                    {item.script}
                  </span>
                </span>

                {active && (
                  <Check
                    className="size-4 shrink-0 text-[var(--accent-on-soft)]"
                    strokeWidth={3}
                  />
                )}
              </DropdownMenu.Item>
            )
          })}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}

/**
 * Bayroq — yumaloq oynacha ichida.
 *
 * Emoji to'g'ridan-to'g'ri qo'yilsa tizimga qarab har xil o'lchamda chiqadi.
 * Doira ichiga solinganda barcha bayroqlar bir xil ko'rinadi va
 * interfeysga "tugma" hissini beradi — Apple'ning yondashuvi.
 */
function Flag({ meta, size = 'sm' }: { meta: LocaleMeta; size?: 'sm' | 'md' }) {
  return (
    <span
      aria-hidden
      className={cn(
        'grid shrink-0 place-items-center overflow-hidden rounded-full',
        'bg-[var(--bg-sunken)] ring-1 ring-black/5 dark:ring-white/10',
        size === 'sm' ? 'size-6 text-[13px]' : 'size-8 text-[17px]',
      )}
    >
      <span className="translate-y-px leading-none">{meta.flag}</span>
    </span>
  )
}
