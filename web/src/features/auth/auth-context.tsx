import {
  createContext,
  use,
  useCallback,
  useEffect,
  useMemo,
  useRef,
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
  /** Resolves with the signed-in user: the state update has not reached the caller's render yet. */
  login: (input: LoginInput) => Promise<AuthUser>
  logout: () => Promise<void>
  /**
   * Foydalanuvchi ma'lumotini serverdan qayta o'qiydi.
   *
   * Nega kerak: hisob rasmi almashtirilganda `photoKey` o'zgaradi va u
   * ham holatda, ham brauzerdagi nusxada yangilanishi kerak — aks holda
   * yuqori paneldagi avatar eski suratni ko'rsatib turaverardi.
   *
   * Tizimga kirgan odamda u O'ZI ham ishlaydi — oyna fokusga qaytganda va
   * har daqiqada (pastdagi `REFRESH_INTERVAL_MS` izohi).
   */
  refreshUser: () => Promise<void>
  /** Ruxsat kodi bo'yicha tekshiruv: `hasPermission('employees.edit')`. */
  hasPermission: (code: string) => boolean
  hasRole: (code: string) => boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

/**
 * Ruxsatlarni serverdan qayta o'qish oralig'i.
 *
 * <b>Nega kerak.</b> Rollarni egasi ish vaqtida o'zgartiradi va o'zgarish
 * deploy'siz kuchga kiradi (rollar shartnomasi §6). Server buni keyingi
 * so'rovdayoq biladi, brauzer esa ruxsatlarni faqat kirishda va sahifa
 * yangilanganda o'qirdi — ya'ni HR soatlab yopilgan bo'limning menyusini
 * ko'rib, har bosishda 403 olardi yoki yangi berilgan tugmani umuman
 * ko'rmasdi. Endi menyu va tugmalar bir daqiqa ichida ergashadi.
 *
 * <b>Nega bir daqiqa.</b> Server ruxsatlar keshini ≤30 soniya saqlaydi —
 * tezroq so'rash yangi natija bermaydi. Tizimdan 3–4 kishi foydalanadi,
 * ya'ni daqiqada bitta yengil so'rov serverga sezilmaydi.
 */
const REFRESH_INTERVAL_MS = 60_000

/**
 * Serverdan kelgan nusxa ekrandagisidan farq qiladimi.
 *
 * NEGA solishtiriladi: `/api/auth/me` har daqiqada keladi va deyarli doim
 * O'SHA ma'lumotni qaytaradi. Har safar yangi obyekt qo'yilsa, `useAuth`
 * ga bog'langan butun ilova (menyu, jadvallar, ochiq formalar) sababsiz
 * qayta chizilardi. Ruxsatlar TO'PLAM sifatida solishtiriladi — server
 * tartibi kafolatlanmagan.
 */
function sameUser(a: AuthUser | null, b: AuthUser): boolean {
  if (a === null) return false

  return (
    a.id === b.id &&
    a.login === b.login &&
    a.fullName === b.fullName &&
    a.photoKey === b.photoKey &&
    sameCodes(a.roles, b.roles) &&
    // Rol nomi o'zgarsa (egasi uni «Rollar» da qayta nomladi) — yangilanadi
    sameCodes(a.roleNames ?? [], b.roleNames ?? []) &&
    sameCodes(a.permissions, b.permissions)
  )
}

function sameCodes(a: readonly string[], b: readonly string[]): boolean {
  if (a.length !== b.length) return false
  const set = new Set(a)
  return b.every((code) => set.has(code))
}

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

  /**
   * Ekrandagi joriy nusxa — davriy yangilash uni effektni qayta
   * o'rnatmasdan o'qishi uchun (`useSearchDraft` dagi naqsh).
   */
  const latestUser = useRef(user)
  latestUser.current = user

  const signedIn = user !== null

  /*
   * ⭐ Davriy yangilash: oyna fokusga qaytganda va har daqiqada
   * (`REFRESH_INTERVAL_MS` izohi).
   *
   * «Sakrash» bo'lmasligi uchun uchta qoida:
   *   • so'rov FONDA ketadi — `isRestoring` o'zgarmaydi, skelet chiqmaydi;
   *   • ma'lumot o'zgarmagan bo'lsa holat ham o'zgarmaydi (`sameUser`),
   *     ya'ni odatdagi holatda hech narsa qayta chizilmaydi;
   *   • xato JIM yutiladi — keshdagi nusxa bilan ish davom etadi.
   *     Token yaroqsiz bo'lsa (hisob faolsizlantirilgan) 401 ni
   *     `http-client` o'zi ushlaydi va /kirish ga qaytaradi.
   *
   * Chiqilgan holatda effekt umuman o'rnatilmaydi (`signedIn`). Yashirin
   * varaqda ham so'rov ketmaydi: menyuni hech kim ko'rmayapti, qaytib
   * kelganda esa `focus` o'zi yangilaydi.
   */
  useEffect(() => {
    if (!signedIn) return

    let active = true
    let busy = false

    const pull = () => {
      if (busy || !authStorage.isAuthenticated || document.visibilityState === 'hidden') return
      busy = true

      getMe()
        .then((fresh) => {
          // Javob kelguncha odam chiqib ketgan bo'lishi mumkin — u holda
          // eski hisobni qaytarib yozish uni «tiriltirib» qo'yardi.
          if (!active || !authStorage.isAuthenticated) return
          if (sameUser(latestUser.current, fresh)) return

          authStorage.setUser(fresh)
          setUser(fresh)
        })
        .catch(() => {
          // Server vaqtincha javob bermasa — keyingi urinishgacha kutiladi.
        })
        .finally(() => {
          busy = false
        })
    }

    const timer = window.setInterval(pull, REFRESH_INTERVAL_MS)
    window.addEventListener('focus', pull)

    return () => {
      active = false
      window.clearInterval(timer)
      window.removeEventListener('focus', pull)
    }
  }, [signedIn])

  const login = useCallback(async ({ login: phone, password, remember }: LoginInput) => {
    const session = await loginRequest({ login: phone, password })

    // Tartib muhim: `setUser` qaysi omborga yozishni access token joyiga
    // qarab aniqlaydi, shu sababli tokenlar birinchi saqlanadi
    authStorage.setTokens(session.accessToken, session.refreshToken, remember)
    authStorage.setUser(session.user)

    setUser(session.user)
    setRestoring(false)

    return session.user
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
