import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'

import { readStorage, writeStorage } from '@/lib/storage'

/**
 * Mavzu (UX §D.1, 4-nuqson).
 *
 * Verifix faqat tungi rejimda ishlaydi. Belgilash esa ko'cha-ko'yda,
 * yozda, soat 08:30 da bo'ladi — quyoshda qora ekran eng yomon tanlov.
 * Shu sababli bu yerda YORUG' sukut bo'yicha, tungisi to'liq qiymatli,
 * tanlov esa `Sozlamalar` da.
 *
 * ⚠️ Tanlov CSS da emas, JS da hal qilinadi: «tizimdagi kabi» ni ham,
 * qo'lda tanlovni ham bitta `data-theme` atributi ifodalaydi. CSS
 * media so'rovi bilan aralashsa, qo'lda «Yorug'» tanlagan telefon
 * kechqurun o'zicha qorayib ketardi.
 */
export type ThemeChoice = 'system' | 'light' | 'dark'

const STORAGE_KEY = 'bonvi.ilova.mavzu'

interface ThemeApi {
  choice: ThemeChoice
  resolved: 'light' | 'dark'
  setChoice: (choice: ThemeChoice) => void
}

const ThemeContext = createContext<ThemeApi>({
  choice: 'system',
  resolved: 'light',
  setChoice: () => {},
})

function readChoice(): ThemeChoice {
  const stored = readStorage(STORAGE_KEY)
  return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system'
}

function systemPrefersDark(): boolean {
  try {
    return window.matchMedia('(prefers-color-scheme: dark)').matches
  } catch {
    return false
  }
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [choice, setChoiceState] = useState<ThemeChoice>(readChoice)
  const [systemDark, setSystemDark] = useState<boolean>(systemPrefersDark)

  useEffect(() => {
    let query: MediaQueryList
    try {
      query = window.matchMedia('(prefers-color-scheme: dark)')
    } catch {
      return undefined
    }

    const onChange = (): void => setSystemDark(query.matches)

    // `addEventListener` MediaQueryList uchun Safari 14 dan; bizning
    // pol 12.2 — shu sababli eski `addListener` zaxirada turadi.
    if (typeof query.addEventListener === 'function') {
      query.addEventListener('change', onChange)
      return () => query.removeEventListener('change', onChange)
    }

    query.addListener(onChange)
    return () => query.removeListener(onChange)
  }, [])

  const resolved: 'light' | 'dark' =
    choice === 'system' ? (systemDark ? 'dark' : 'light') : choice

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', resolved)

    // Brauzer paneli rangi ham mavzuga ergashadi — ochiq ilovada
    // ekranning yuqori qismi boshqa rangda qolib ketmasin.
    const meta = document.querySelector('meta[name="theme-color"]')
    if (meta) meta.setAttribute('content', resolved === 'dark' ? '#1A1D22' : '#FFFFFF')
  }, [resolved])

  const setChoice = useCallback((next: ThemeChoice) => {
    setChoiceState(next)
    writeStorage(STORAGE_KEY, next)
  }, [])

  const api = useMemo<ThemeApi>(
    () => ({ choice, resolved, setChoice }),
    [choice, resolved, setChoice],
  )

  return <ThemeContext.Provider value={api}>{children}</ThemeContext.Provider>
}

export function useTheme(): ThemeApi {
  return useContext(ThemeContext)
}
