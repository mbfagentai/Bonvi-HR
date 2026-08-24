import { cn } from '@/shared/lib/cn'
import { formatUzPhone } from '../lib/phone'
import { devAccounts, type DevAccount } from '../lib/dev-accounts'

/**
 * Dev-rejimda sinov hisobi bilan bir bosishda kirish.
 *
 * ⚠️ FAQAT ISHLAB CHIQISH UCHUN — production build'da bu komponent
 * umuman chaqirilmaydi (`devAccounts()` bo'sh ro'yxat qaytaradi va
 * Vite butun shoxni bundle'dan chiqarib tashlaydi).
 *
 * Kartalar formadan TASHQARIDA, ostida turadi: ular kirishning bir qismi
 * emas, faqat ishlab chiqishdagi qulaylik. Shu sababli ularning ko'rinishi
 * ham asosiy kartadan yengilroq — fon shaffof, chegara nozik.
 *
 * Matn ataylab tarjima qilinmagan: buni faqat dasturchi ko'radi, va
 * 4 tilga kalit qo'shish tarjima fayllarini keraksiz shishiradi.
 */
export function DevAccountsPanel({
  onPick,
  disabled = false,
}: {
  onPick: (account: DevAccount) => void
  disabled?: boolean
}) {
  const accounts = devAccounts()

  if (accounts.length === 0) {
    return null
  }

  return (
    <section className="mt-8">
      <p className="mb-3 text-center text-caption2 font-semibold tracking-[0.12em] text-[var(--fg-tertiary)] uppercase">
        Dev hisoblar
      </p>

      <ul className="grid gap-2.5 sm:grid-cols-2">
        {accounts.map((account) => (
          <li key={account.login}>
            <button
              type="button"
              disabled={disabled}
              onClick={() => onPick(account)}
              title={account.hint}
              className={cn(
                'press flex h-full w-full flex-col items-start gap-0.5 rounded-lg border px-4 py-3 text-left',
                'border-[var(--border)] bg-[var(--bg-elevated)]/70 backdrop-blur-sm',
                'hover:border-[var(--accent)] hover:bg-[var(--bg-elevated)]',
                'disabled:pointer-events-none disabled:opacity-50',
                'transition-colors',
              )}
            >
              <span className="w-full truncate text-subhead font-medium">{account.label}</span>
              <span className="tabular w-full truncate text-footnote text-[var(--fg-tertiary)]">
                +998 {formatUzPhone(account.login)}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}
