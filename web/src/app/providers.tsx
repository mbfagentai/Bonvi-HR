import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { type ReactNode, useState } from 'react'

import { AuthProvider } from '@/features/auth'
import { ThemeProvider } from '@/shared/theme/theme-provider'

// i18n yon ta'sir sifatida ishga tushadi (til aniqlanadi va yuklanadi)
import '@/shared/i18n'

/**
 * Ilova bo'ylab umumiy kontekstlar.
 * QueryClient useState ichida yaratiladi — qayta render'da yangisi hosil bo'lmasligi uchun.
 */
export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            // HR ma'lumoti tez-tez o'zgarmaydi — 1 daqiqa kesh maqbul
            staleTime: 60_000,
            retry: 1,
            refetchOnWindowFocus: false,
          },
          mutations: {
            retry: 0,
          },
        },
      }),
  )

  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        {/* Auth so'rovlari ham QueryClient orqali o'tishi mumkin,
            shu sababli u ichkarida turadi */}
        <AuthProvider>
          {children}
          {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
        </AuthProvider>
      </QueryClientProvider>
    </ThemeProvider>
  )
}
