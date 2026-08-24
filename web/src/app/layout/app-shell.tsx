import { useCallback, useEffect, useState, type ReactNode } from 'react'

import { Sidebar } from './sidebar'
import { Topbar } from './topbar'

const STORAGE_KEY = 'bonvi.sidebar'

/**
 * Ilova qobig'i: yon menyu + yuqori panel + kontent.
 * Menyuning yig'ilgan holati saqlanadi.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === '1'
    } catch {
      return false
    }
  })

  // Tor ekranda menyu avtomatik yig'iladi
  useEffect(() => {
    const media = globalThis.matchMedia?.('(max-width: 1024px)')
    if (!media) return

    const apply = (matches: boolean) => {
      if (matches) setCollapsed(true)
    }

    apply(media.matches)
    const onChange = (event: MediaQueryListEvent) => apply(event.matches)
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [])

  const toggle = useCallback(() => {
    setCollapsed((previous) => {
      const next = !previous
      try {
        localStorage.setItem(STORAGE_KEY, next ? '1' : '0')
      } catch {
        // Maxfiy rejimda saqlash mumkin emas — bu xato emas
      }
      return next
    })
  }, [])

  return (
    <div className="flex min-h-dvh">
      <div className="sticky top-0 h-dvh">
        <Sidebar collapsed={collapsed} onToggle={toggle} />
      </div>

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar />
        <main className="flex-1 px-5 py-6 lg:px-8 lg:py-8">
          <div className="mx-auto w-full max-w-[1400px]">{children}</div>
        </main>
      </div>
    </div>
  )
}
