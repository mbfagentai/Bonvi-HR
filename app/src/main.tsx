import { StrictMode, useEffect } from 'react'
import { createRoot } from 'react-dom/client'
import { useTranslation } from 'react-i18next'

import { GateScreen } from '@/app/gate'
import { countSession } from '@/app/install'
import { AppRouter } from '@/app/router'
import { SessionProvider } from '@/app/session'
import { ThemeProvider } from '@/app/theme'
import { UpdateProvider } from '@/app/update'
import { isLocale, DEFAULT_LOCALE } from '@/i18n/locales'
import { isSupportedBrowser } from '@/lib/device'
import { ToastProvider } from '@/ui/toast'

import '@/i18n'
import '@/styles/index.css'

/**
 * Ilovaning kirish nuqtasi.
 *
 * Tartib ahamiyatli: avval brauzer tekshiriladi (UX §F.5), keyin
 * mavzu (ekran yorug' bo'lib chaqnab ketmasligi uchun), keyin
 * sessiya va marshrutlar.
 */
function App() {
  if (!isSupportedBrowser()) return <GateScreen />

  return (
    <ThemeProvider>
      <UpdateProvider>
        <SessionProvider>
          <ToastProvider>
            <HtmlLang />
            <AppRouter />
          </ToastProvider>
        </SessionProvider>
      </UpdateProvider>
    </ThemeProvider>
  )
}

/** `<html lang>` til bilan birga o'zgaradi — ekran o'quvchi uchun. */
function HtmlLang() {
  const { i18n } = useTranslation()

  useEffect(() => {
    const code = isLocale(i18n.language) ? i18n.language : DEFAULT_LOCALE
    document.documentElement.setAttribute('lang', code)
  }, [i18n.language])

  return null
}

countSession()

const container = document.getElementById('root')

if (container) {
  createRoot(container).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}
