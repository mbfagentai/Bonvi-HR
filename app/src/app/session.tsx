import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react'

import { api } from '@/api/client'
import { sessionStore } from '@/api/session-store'
import type { LoginResult } from '@/api/types'
import { clearPhotoCache } from '@/data/photo'
import { clearQueryCache } from '@/lib/use-query'
import { readStorage, writeStorage } from '@/lib/storage'

/**
 * Sessiya: token va «parolni almashtirish shart» bayrog'i.
 *
 * Holatning O'ZI `api/session-store.ts` da — HTTP qatlami ham uni o'qiydi
 * va yozadi (yangilash, sessiya tugashi, «avval parolni almashtiring»).
 * Bu yerda faqat React'ga ulash va uchta amal: kirish, parol o'rnatish,
 * chiqish.
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
  mustChangePassword: boolean
  /** Sessiyani server yopdi — kirish ekrani buni bir qator bilan aytadi. */
  expired: boolean
  /** Oxirgi kirgan raqam — kirish formasi shundan to'ldiriladi. */
  lastPhone: string
  /**
   * HR bergan BIR MARTALIK parol — faqat kirishdan parol o'rnatishgacha,
   * faqat XOTIRADA. Sahifa qayta yuklansa yo'qoladi va parol ekrani uni
   * so'raydi. `localStorage` ga hech qachon yozilmaydi.
   */
  oneTimePassword: string | null
  signIn: (login: string, password: string) => Promise<void>
  /** Parol almashtirildi — server bergan YANGI juftlik saqlanadi (eskisi o'lgan). */
  passwordChanged: (result: LoginResult) => void
  signOut: () => void
}

const SessionContext = createContext<SessionApi | null>(null)

export function SessionProvider({ children }: { children: ReactNode }) {
  const state = useSyncExternalStore(sessionStore.subscribe, sessionStore.get, sessionStore.get)
  const [lastPhone, setLastPhone] = useState<string>(() => readStorage(PHONE_KEY) ?? '')

  // Ref + holat: ref — qiymatning o'zi (render'lar orasida), holat —
  // ekranni qayta chizish uchun.
  const oneTimeRef = useRef<string | null>(null)
  const [oneTimePassword, setOneTimePassword] = useState<string | null>(null)

  const rememberOneTime = useCallback((value: string | null) => {
    oneTimeRef.current = value
    setOneTimePassword(value)
  }, [])

  const signIn = useCallback(
    async (login: string, password: string) => {
      const result = await api.login({ login, password })

      // Kesh oldingi odamdan qolmasligi kerak — bitta telefonda ikki
      // xodim navbat bilan kirishi mumkin.
      clearQueryCache()
      clearPhotoCache()

      sessionStore.setTokens(result)
      rememberOneTime(result.mustChangePassword ? password : null)

      writeStorage(PHONE_KEY, login)
      setLastPhone(login)
    },
    [rememberOneTime],
  )

  const passwordChanged = useCallback(
    (result: LoginResult) => {
      sessionStore.setTokens(result)
      rememberOneTime(null)
    },
    [rememberOneTime],
  )

  const signOut = useCallback(() => {
    const refreshToken = sessionStore.get().refreshToken

    sessionStore.clear()
    rememberOneTime(null)
    clearQueryCache()
    clearPhotoCache()

    // Serverdagi sessiya ham yopiladi — javob kutilmaydi: internet yo'q
    // bo'lsa ham odam darhol chiqadi (token 7 kundan keyin o'zi o'ladi).
    if (refreshToken) void api.logout(refreshToken).catch(() => undefined)
  }, [rememberOneTime])

  const value = useMemo<SessionApi>(
    () => ({
      token: state.accessToken,
      mustChangePassword: state.mustChangePassword,
      expired: state.expired,
      lastPhone,
      oneTimePassword,
      signIn,
      passwordChanged,
      signOut,
    }),
    [state, lastPhone, oneTimePassword, signIn, passwordChanged, signOut],
  )

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

export function useSession(): SessionApi {
  const context = useContext(SessionContext)
  if (!context) throw new Error('useSession: SessionProvider ichida chaqirilishi kerak')
  return context
}
