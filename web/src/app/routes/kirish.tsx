import { createFileRoute, redirect } from '@tanstack/react-router'

import { LoginForm } from '@/features/auth'
import { BonviLogo } from '@/shared/ui'
import { useLocale } from '@/shared/i18n/use-locale'
import { authStorage } from '@/shared/lib/auth-storage'
import { LanguageSwitcher } from '@/shared/ui/language-switcher'
import { ThemeToggle } from '@/shared/ui/theme-toggle'

/**
 * «Ochiq qayta yo'naltirish» hujumidan himoya.
 *
 * `?redirect=` qiymati manzil satridan keladi, ya'ni uni istalgan odam
 * yozishi mumkin. Faqat ilova ichidagi yo'l qabul qilinadi: `//evil.uz`
 * va `https://…` — begona sayt, ular tashlab yuboriladi.
 */
function safeRedirect(value: unknown): string | undefined {
  if (typeof value !== 'string') return undefined
  return value.startsWith('/') && !value.startsWith('//') ? value : undefined
}

export const Route = createFileRoute('/kirish')({
  validateSearch: (search: Record<string, unknown>): { redirect?: string } => ({
    redirect: safeRedirect(search['redirect']),
  }),

  beforeLoad: ({ search }) => {
    // Allaqachon kirgan bo'lsa, kirish sahifasi ko'rsatilmaydi
    if (authStorage.isAuthenticated) {
      throw redirect({ href: search.redirect ?? '/' })
    }
  },

  component: LoginPage,
})

function LoginPage() {
  const { t } = useLocale()
  const { redirect: redirectTo } = Route.useSearch()

  return (
    <div className="relative grid min-h-dvh place-items-center overflow-hidden px-5 py-12">
      <Backdrop />

      {/* Til va ko'rinish kirishdan OLDIN ham o'zgartirilishi kerak:
          rus tilida o'qiydigan xodim maydon nomlarini tushunmasligi mumkin */}
      <div className="absolute top-5 right-5 z-10 flex items-center gap-2">
        <LanguageSwitcher />
        <ThemeToggle />
      </div>

      <main className="relative z-10 w-full max-w-[440px]">
        <header className="mb-9 flex flex-col items-center text-center">
          {/*
            Logotip qutisiz — brend belgisi sifatida turadi, ikonka bo'lib emas.
            Sahifada u eng yuqoridagi va eng katta element: birinchi qarashda
            «bu qaysi tizim» degan savolga javob beradi.

            Rasm emas, kod: `currentColor` bilan chizilgani uchun tungi
            rejimda o'zi moslashadi va har qanday o'lchamda aniq qoladi.
          */}
          {/* Brend navy — tint emas: logotip identifikatsiya, harakat emas */}
          <BonviLogo height={46} className="text-brand-700 dark:text-brand-300" />

          <h1 className="mt-3 text-title1 leading-tight font-semibold tracking-tight sm:text-[32px]">
            {t('auth.title')}
          </h1>

          <p className="mt-2.5 text-callout text-[var(--fg-secondary)]">
            {t('auth.subtitle')}
          </p>
        </header>

        <LoginForm redirectTo={redirectTo} />

        <p className="mt-8 text-center text-caption1 text-[var(--fg-tertiary)]">
          {t('auth.footerNote', { company: t('app.company') })}
        </p>
      </main>
    </div>
  )
}

/**
 * Orqa fon: brend rangidagi ikkita juda suyuq nur.
 * iOS'da fon hech qachon «tekis kulrang» emas — unda nozik chuqurlik bo'ladi,
 * lekin u kontentga xalaqit bermaydi (shu sababli shaffoflik 6–8%).
 */
function Backdrop() {
  return (
    <div aria-hidden className="pointer-events-none absolute inset-0 overflow-hidden">
      {/*
        Yuqoridagi nur — brend navy'sida. Ilgari u `--accent` edi; tint
        systemBlue'ga o'tgach ikkala nur ham bir xil ko'k bo'lib qolar va
        chuqurlik yo'qolar edi. Navy + ko'k juftligi logotip bilan bir
        gapda turadi.
      */}
      <div className="absolute -top-48 left-1/2 size-[620px] -translate-x-1/2 rounded-full bg-brand-700 opacity-[0.08] blur-[130px]" />
      <div className="absolute -right-40 -bottom-56 size-[520px] rounded-full bg-ios-blue opacity-[0.06] blur-[130px]" />
    </div>
  )
}
