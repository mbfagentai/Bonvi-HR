import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react'

import { api } from '@/api/client'
import { clearQueryCache } from '@/lib/use-query'
import { readStorage, removeStorage, writeStorage } from '@/lib/storage'

/**
 * Sessiya: token va «parolni almashtirish shart» bayrog'i.
 *
 * ⚠️ Token yo'qolishi ODATIY hol: Safari yozuvlarni 7 kun
 * faolsizlikdan keyin o'chirib yuborishi mumkin (UX §G.4). Shu
 * sababli tokensiz holat xato emas — kirish ekrani hech qanday
 * qo'rqinchli xabarsiz, telefon raqami oldindan to'ldirilgan holda
 * ochiladi.
 */
const TOKEN_KEY = 'bonvi.ilova.token'
const REFRESH_KEY = 'bonvi.ilova.refresh'
const MUST_CHANGE_KEY = 'bonvi.ilova.parol-almashsin'
const PHONE_KEY = 'bonvi.ilova.telefon'

interface SessionApi {
  token: string | null
  mustChangePassword: boolean
  /** Oxirgi kirgan raqam — kirish formasi shundan to'ldiriladi. */
  lastPhone: string
  signIn: (login: string, password: string) => Promise<void>
  passwordChanged: () => void
  signOut: () => void
}

const SessionContext = createContext<SessionApi | null>(null)

export function SessionProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() => readStorage(TOKEN_KEY))
  const [mustChangePassword, setMustChange] = useState<boolean>(
    () => readStorage(MUST_CHANGE_KEY) === '1',
  )
  const [lastPhone, setLastPhone] = useState<string>(() => readStorage(PHONE_KEY) ?? '')

  const signIn = useCallback(async (login: string, password: string) => {
    const result = await api.login({ login, password })

    writeStorage(TOKEN_KEY, result.accessToken)
    writeStorage(REFRESH_KEY, result.refreshToken)
    writeStorage(PHONE_KEY, login)
    writeStorage(MUST_CHANGE_KEY, result.mustChangePassword ? '1' : '0')

    // Kesh oldingi odamdan qolmasligi kerak — bitta telefonda ikki
    // xodim navbat bilan kirishi mumkin.
    clearQueryCache()

    setToken(result.accessToken)
    setMustChange(result.mustChangePassword)
    setLastPhone(login)
  }, [])

  const passwordChanged = useCallback(() => {
    writeStorage(MUST_CHANGE_KEY, '0')
    setMustChange(false)
  }, [])

  const signOut = useCallback(() => {
    removeStorage(TOKEN_KEY)
    removeStorage(REFRESH_KEY)
    removeStorage(MUST_CHANGE_KEY)
    clearQueryCache()
    setToken(null)
    setMustChange(false)
  }, [])

  const value = useMemo<SessionApi>(
    () => ({ token, mustChangePassword, lastPhone, signIn, passwordChanged, signOut }),
    [token, mustChangePassword, lastPhone, signIn, passwordChanged, signOut],
  )

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

export function useSession(): SessionApi {
  const context = useContext(SessionContext)
  if (!context) throw new Error('useSession: SessionProvider ichida chaqirilishi kerak')
  return context
}
