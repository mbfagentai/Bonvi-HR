import {
  createContext,
  use,
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'

export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'bonvi.theme'

interface ThemeContextValue {
  theme: Theme
  setTheme: (theme: Theme) => void
  toggle: () => void
}

const ThemeContext = createContext<ThemeContextValue | null>(null)

/**
 * Boshlang'ich ko'rinish.
 *
 * Foydalanuvchi bir marta tanlagan bo'lsa — o'sha saqlanadi.
 * Tanlamagan bo'lsa — birinchi ochilishda tizim sozlamasidan olinadi
 * (kechasi ochsa tungi rejimda ochiladi).
 *
 * Interfeysda «tizim bo'yicha» degan uchinchi holat YO'Q: foydalanuvchiga
 * ikkita aniq tanlov beriladi, uchta emas.
 */
function initialTheme(): Theme {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored === 'light' || stored === 'dark') return stored
  } catch {
    // Maxfiy rejimda localStorage yopiq bo'lishi mumkin
  }

  return globalThis.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(initialTheme)

  useEffect(() => {
    document.documentElement.classList.toggle('dark', theme === 'dark')
  }, [theme])

  const setTheme = useCallback((next: Theme) => {
    setThemeState(next)
    try {
      localStorage.setItem(STORAGE_KEY, next)
    } catch {
      // Saqlab bo'lmasa ham ko'rinish o'zgaradi — bu xato emas
    }
  }, [])

  const toggle = useCallback(() => {
    setThemeState((previous) => {
      const next: Theme = previous === 'dark' ? 'light' : 'dark'
      try {
        localStorage.setItem(STORAGE_KEY, next)
      } catch {
        // yuqoridagi kabi
      }
      return next
    })
  }, [])

  const value = useMemo(() => ({ theme, setTheme, toggle }), [theme, setTheme, toggle])

  return <ThemeContext value={value}>{children}</ThemeContext>
}

export function useTheme(): ThemeContextValue {
  const context = use(ThemeContext)
  if (!context) throw new Error('useTheme faqat ThemeProvider ichida ishlaydi')
  return context
}
