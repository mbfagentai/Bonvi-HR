import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { useNavigate } from '@tanstack/react-router'
import { ChevronDown, LogOut, Settings, type LucideIcon } from 'lucide-react'
import { useRef, useState } from 'react'

import { AccountPhoto, formatFullPhone, useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { SettingsDialog } from './settings-dialog'

/**
 * Foydalanuvchi menyusi — yuqori panelning eng o'ng burchagida.
 *
 * Bosilganda ochiladigan ro'yxat: sozlamalar va chiqish.
 * Apple'ning yondashuvi: hisob bilan bog'liq hamma narsa bitta joyda,
 * interfeysning eng chekka nuqtasida — kundalik ishga xalaqit bermaydi.
 *
 * Menyuda faqat ikkita band: bu tizimda foydalanuvchi o'zidan boshqa
 * hisoblarni ko'rmaydi, shu sababli "Foydalanuvchilar" bandiga o'rin yo'q.
 *
 * Avatar tashqaridan BERILMAYDI: `AccountPhoto` uni o'zi oladi va uch
 * joyda (panel, menyu, sozlamalar) bitta kesh yozuvidan foydalanadi.
 * Havolani yuqoridan uzatish har bir oraliq komponentga rasm haqida
 * bilishni majburlardi.
 */
export function UserMenu() {
  const { t } = useLocale()
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  const [settingsOpen, setSettingsOpen] = useState(false)

  /*
   * Sozlamalar oynasi menyu YOPILIB BO'LGACH ochiladi.
   *
   * Sabab: menyu yopilish animatsiyasi tugagunicha DOM'da qoladi va
   * oxirida fokusni o'zi chaqirgan tugmaga qaytaradi. Oyna undan oldin
   * ochilsa, fokus oyna ichidan tortib olinadi va klaviatura bilan
   * ishlash buziladi. Bayroq esa Escape yoki boshqa band bilan
   * yopilgan holatni ajratib turadi.
   */
  const settingsRequested = useRef(false)

  const name = user?.fullName ?? t('auth.unknownUser')
  // Rol kodini («hr_manager») ko'rsatish foydasiz — telefon raqami esa
  // foydalanuvchiga qaysi hisob ostida ishlayotganini aniq aytadi
  const role = user ? formatFullPhone(user.login) : '—'

  const handleSignOut = () => {
    // `logout` tokenlarni birinchi navbatda — kutmasdan — o'chiradi,
    // shu sababli marshrut himoyasi darhol ishlaydi
    void logout()
    void navigate({ to: '/kirish', replace: true })
  }

  return (
    <>
      <DropdownMenu.Root>
        <DropdownMenu.Trigger asChild>
          <button
            type="button"
            aria-label={name}
            className={cn(
              'press group flex h-11 items-center gap-2.5 rounded-full border py-0 pr-2.5 pl-1',
              'border-transparent bg-transparent',
              'hover:border-[var(--border)] hover:bg-[var(--bg-elevated)]',
              'data-[state=open]:border-[var(--border)] data-[state=open]:bg-[var(--bg-elevated)]',
              'transition-colors',
            )}
          >
            <AccountPhoto name={name} size="sm" />

            {/* Tor ekranda faqat avatar qoladi */}
            <span className="hidden min-w-0 text-left leading-tight lg:block">
              <span className="block max-w-[140px] truncate text-footnote font-medium">
                {name}
              </span>
              <span className="block max-w-[140px] truncate text-caption2 text-[var(--fg-tertiary)]">
                {role}
              </span>
            </span>

            <ChevronDown
              className="hidden size-3 shrink-0 text-[var(--fg-tertiary)] transition-transform duration-200 group-data-[state=open]:rotate-180 lg:block"
              strokeWidth={2.5}
            />
          </button>
        </DropdownMenu.Trigger>

        <DropdownMenu.Portal>
          <DropdownMenu.Content
            align="end"
            sideOffset={8}
            onCloseAutoFocus={() => {
              if (!settingsRequested.current) return
              settingsRequested.current = false
              setSettingsOpen(true)
            }}
            className={cn(
              'pop-panel z-50 min-w-[240px] rounded-xl border p-1.5',
              'border-[var(--border)] bg-[var(--bg-elevated)] shadow-floating',
            )}
          >
            {/* Hisob sarlavhasi */}
            <div className="flex items-center gap-3 px-2 py-2.5">
              <AccountPhoto name={name} size="md" />
              <span className="min-w-0 leading-tight">
                <span className="block truncate text-subhead font-semibold">{name}</span>
                <span className="block truncate text-caption1 text-[var(--fg-tertiary)]">
                  {role}
                </span>
              </span>
            </div>

            <DropdownMenu.Separator className="my-1.5 h-px bg-[var(--border)]" />

            <MenuItem
              icon={Settings}
              label={t('nav.settings')}
              onSelect={() => {
                settingsRequested.current = true
              }}
            />

            <DropdownMenu.Separator className="my-1.5 h-px bg-[var(--border)]" />

            {/* Chiqish menyuda ham qoladi: bu eng tez-tez kerak bo'ladigan
                amal va uni ikki bosishga uzaytirish ma'nosiz */}
            <MenuItem
              icon={LogOut}
              label={t('common.signOut')}
              danger
              onSelect={handleSignOut}
            />
          </DropdownMenu.Content>
        </DropdownMenu.Portal>
      </DropdownMenu.Root>

      <SettingsDialog
        open={settingsOpen}
        onClose={() => setSettingsOpen(false)}
        onSignOut={() => {
          // Oyna avval yopiladi: fon varaqlanishini u tiklaydi, aks holda
          // /kirish sahifasi ochilib qolgan modal ostida qoladi
          setSettingsOpen(false)
          handleSignOut()
        }}
      />
    </>
  )
}

function MenuItem({
  icon: Icon,
  label,
  danger = false,
  onSelect,
}: {
  icon: LucideIcon
  label: string
  danger?: boolean
  onSelect?: () => void
}) {
  return (
    <DropdownMenu.Item
      onSelect={onSelect}
      className={cn(
        'flex h-9 cursor-pointer items-center gap-2.5 rounded-md px-2 text-subhead outline-none select-none',
        'transition-colors duration-100',
        danger
          // Matn `--danger-text` da: tint (#FF3B30) oq fonda 3.55:1 berardi
          // — AA dan past. Ajratilgan fon esa o'z o'rnida qoladi, ustidagi
          // matn u yerda ham AA dan o'tadi (yorug' 4.70:1 · tungi 5.47:1).
          ? 'text-[var(--danger-text)] data-[highlighted]:bg-ios-red/10'
          : 'data-[highlighted]:bg-[var(--bg-hover)]',
      )}
    >
      <Icon className="size-4 shrink-0" strokeWidth={2} />
      <span className="truncate">{label}</span>
    </DropdownMenu.Item>
  )
}
