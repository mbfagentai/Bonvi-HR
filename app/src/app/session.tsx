import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react'

import { api } from '@/api/client'
import { sessionStore } from '@/api/session-store'
import { clearPhotoCache } from '@/data/photo'
import { clearQueryCache } from '@/lib/use-query'
import { readStorage, writeStorage } from '@/lib/storage'

/**
 * Sessiya: token.
 *
 * Holatning O'ZI `api/session-store.ts` da — HTTP qatlami ham uni o'qiydi
 * va yozadi (yangilash, sessiya tugashi). Bu yerda faqat React'ga ulash va
 * ikkita amal: kirish va chiqish. Parolni HR beradi va xodim uni
 * almashtirmaydi (2026-09-27) — «parol o'rnatish» amali ham, ekrani ham yo'q.
 *
 * ⚠️ Token yo'qolishi ODATIY hol: Safari yozuvlarni 7 kun
 * faolsizlikdan keyin o'chirib yuborishi mumkin (UX §G.4). Shu
 * sababli tokensiz holat xato emas — kirish ekrani hech qanday
 * qo'rqinchli xabarsiz, telefon raqami oldindan to'ldirilgan holda
 * ochiladi.
 */
const PHONE_KEY = 'bonvi.ilova.telefon'

interface SessionApi {
  token: string | null
  /** Sessiyani server yopdi — kirish ekrani buni bir qator bilan aytadi. */
  expired: boolean
  /** Oxirgi kirgan raqam — kirish formasi shundan to'ldiriladi. */
  lastPhone: string
  signIn: (login: string, password: string) => Promise<void>
  signOut: () => void
}

const SessionContext = createContext<SessionApi | null>(null)

export function SessionProvider({ children }: { children: ReactNode }) {
  const state = useSyncExternalStore(sessionStore.subscribe, sessionStore.get, sessionStore.get)
  const [lastPhone, setLastPhone] = useState<string>(() => readStorage(PHONE_KEY) ?? '')

  const signIn = useCallback(
    async (login: string, password: string) => {
      const result = await api.login({ login, password })

      // Kesh oldingi odamdan qolmasligi kerak — bitta telefonda ikki
      // xodim navbat bilan kirishi mumkin.
      clearQueryCache()
      clearPhotoCache()

      sessionStore.setTokens(result)

      writeStorage(PHONE_KEY, login)
      setLastPhone(login)
    },
    [],
  )

  const signOut = useCallback(() => {
    const refreshToken = sessionStore.get().refreshToken

    sessionStore.clear()
    clearQueryCache()
    clearPhotoCache()

    // Serverdagi sessiya ham yopiladi — javob kutilmaydi: internet yo'q
    // bo'lsa ham odam darhol chiqadi (token 7 kundan keyin o'zi o'ladi).
    if (refreshToken) void api.logout(refreshToken).catch(() => undefined)
  }, [])

  const value = useMemo<SessionApi>(
    () => ({
      token: state.accessToken,
      expired: state.expired,
      lastPhone,
      signIn,
      signOut,
    }),
    [state, lastPhone, signIn, signOut],
  )

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

export function useSession(): SessionApi {
  const context = useContext(SessionContext)
  if (!context) throw new Error('useSession: SessionProvider ichida chaqirilishi kerak')
  return context
}
