import {
  createContext,
  use,
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'

import { authStorage } from '@/shared/lib/auth-storage'
import {
  getMe,
  login as loginRequest,
  logout as logoutRequest,
  type AuthUser,
} from './api/auth-api'
import { parseAuthUser } from './schemas/auth-user-schema'

export interface LoginInput {
  /** To'liq telefon raqami: «+998905088888». */
  login: string
  password: string
  /** `true` — brauzer yopilgandan keyin ham sessiya saqlanadi. */
  remember: boolean
}

export interface AuthContextValue {
  user: AuthUser | null
  isAuthenticated: boolean
  /**
   * Saqlangan token bo'yicha foydalanuvchi hali serverdan tasdiqlanmoqda.
   * Keshdagi nusxa bo'lsa, `user` shu paytda ham to'ldirilgan bo'ladi.
   */
  isRestoring: boolean
  login: (input: LoginInput) => Promise<void>
  logout: () => Promise<void>
  /**
   * Foydalanuvchi ma'lumotini serverdan qayta o'qiydi.
   *
   * Nega kerak: hisob rasmi almashtirilganda `photoKey` o'zgaradi va u
   * ham holatda, ham brauzerdagi nusxada yangilanishi kerak — aks holda
   * yuqori paneldagi avatar eski suratni ko'rsatib turaverardi.
   */
  refreshUser: () => Promise<void>
  /** Ruxsat kodi bo'yicha tekshiruv: `hasPermission('employees.edit')`. */
  hasPermission: (code: string) => boolean
  hasRole: (code: string) => boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  // Boshlang'ich qiymat keshdan: sahifa yangilanganda ism va menyu
  // «bo'sh → to'ldi» bo'lib sakramaydi
  const [user, setUser] = useState<AuthUser | null>(() =>
    parseAuthUser(authStorage.getUser()),
  )
  const [isRestoring, setRestoring] = useState(() => authStorage.isAuthenticated)

  /*
   * Tiklash: keshdagi nusxa darhol ko'rsatiladi, keyin server bilan
   * solishtiriladi. Ruxsatlar administrator tomonidan o'zgartirilgan
   * bo'lishi mumkin — ular eskirib qolmasligi kerak.
   */
  useEffect(() => {
    if (!authStorage.isAuthenticated) {
      setRestoring(false)
      return
    }

    let cancelled = false

    void getMe()
      .then((fresh) => {
        if (cancelled) return
        setUser(fresh)
        authStorage.setUser(fresh)
      })
      .catch(() => {
        // Token yaroqsiz bo'lsa (401) http-client o'zi yangilaydi yoki
        // /kirish ga qaytaradi. Boshqa xato — masalan server o'chiq —
        // ishni to'xtatmaydi: keshdagi nusxa bilan davom etiladi.
      })
      .finally(() => {
        if (!cancelled) setRestoring(false)
      })

    return () => {
      cancelled = true
    }
  }, [])

  const refreshUser = useCallback(async () => {
    const fresh = await getMe()
    setUser(fresh)
    authStorage.setUser(fresh)
  }, [])

  const login = useCallback(async ({ login: phone, password, remember }: LoginInput) => {
    const session = await loginRequest({ login: phone, password })

    // Tartib muhim: `setUser` qaysi omborga yozishni access token joyiga
    // qarab aniqlaydi, shu sababli tokenlar birinchi saqlanadi
    authStorage.setTokens(session.accessToken, session.refreshToken, remember)
    authStorage.setUser(session.user)

    setUser(session.user)
    setRestoring(false)
  }, [])

  const logout = useCallback(async () => {
    const refreshToken = authStorage.getRefreshToken()

    // Mahalliy holat darhol tozalanadi — server javobini kutib turish
    // foydalanuvchini ushlab qolishga arzimaydi
    authStorage.clear()
    setUser(null)

    if (refreshToken === null) return

    try {
      await logoutRequest(refreshToken)
    } catch {
      // Server javob bermasa ham chiqish bajarilgan: tokenlar o'chirildi
    }
  }, [])

  const hasPermission = useCallback(
    (code: string) => user?.permissions.includes(code) ?? false,
    [user],
  )

  const hasRole = useCallback((code: string) => user?.roles.includes(code) ?? false, [user])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null || authStorage.isAuthenticated,
      isRestoring,
      login,
      logout,
      refreshUser,
      hasPermission,
      hasRole,
    }),
    [user, isRestoring, login, logout, refreshUser, hasPermission, hasRole],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}

export function useAuth(): AuthContextValue {
  const context = use(AuthContext)
  if (!context) throw new Error('useAuth faqat AuthProvider ichida ishlaydi')
  return context
}
