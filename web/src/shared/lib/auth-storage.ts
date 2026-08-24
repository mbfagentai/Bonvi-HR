const ACCESS_KEY = 'bonvi.access'
const REFRESH_KEY = 'bonvi.refresh'
const USER_KEY = 'bonvi.user'

/** Sessiya qayerda saqlanadi. */
type Persistence = 'local' | 'session'

/**
 * Tokenlarni brauzerda saqlash.
 *
 * IKKI OMBOR ishlatiladi va tanlovni foydalanuvchi qiladi:
 *   localStorage   — «meni eslab qol» belgilangan: brauzer yopilsa ham qoladi
 *   sessionStorage — belgilanmagan: tab yopilishi bilan sessiya tugaydi
 * Bu HR bo'limidagi umumiy kompyuter uchun muhim — kirgan odam ketgach
 * keyingi kishi uning hisobiga tushib qolmasligi kerak.
 *
 * Eslatma: hozircha ikkalasi ham JS uchun ochiq ombor. Ishlab chiqarishga
 * chiqishdan oldin refresh token'ni HttpOnly cookie'ga ko'chirish
 * rejalashtirilgan — bu XSS hujumida token o'g'irlanishining oldini oladi.
 * Shu sababli saqlash mantiqi shu yerda ajratilgan: almashtirish bitta fayl.
 */
export const authStorage = {
  getAccessToken: (): string | null => read(ACCESS_KEY),

  getRefreshToken: (): string | null => read(REFRESH_KEY),

  /**
   * @param remember `true` — brauzer yopilgandan keyin ham saqlanadi.
   *   Berilmasa, sessiya qaysi omborda bo'lsa o'sha yerda qoladi: token
   *   avtomatik yangilanganda foydalanuvchining tanlovi o'zgarmasligi kerak.
   */
  setTokens(access: string, refresh: string, remember?: boolean): void {
    const persistence: Persistence =
      remember === undefined ? currentPersistence() : remember ? 'local' : 'session'

    write(persistence, ACCESS_KEY, access)
    write(persistence, REFRESH_KEY, refresh)
  },

  /**
   * Foydalanuvchi ma'lumotining nusxasi.
   *
   * Nega kerak: sahifa yangilanganda interfeys `GET /api/auth/me` javobini
   * kutib turmasdan darhol chiziladi — ism va menyu «sakramaydi».
   *
   * Tipi ataylab `unknown`: bu qatlam ma'lumot shaklini bilmaydi. Eski nusxa
   * yangi shaklga mos kelmasligi mumkin, shu sababli uni o'qigan modul
   * (auth) sxema bo'yicha tekshirib oladi.
   */
  getUser(): unknown {
    const raw = read(USER_KEY)
    if (raw === null) return null

    try {
      return JSON.parse(raw)
    } catch {
      // Buzilgan nusxa — yo'q deb hisoblanadi, serverdan qayta so'raladi
      return null
    }
  },

  /** Tokenlardan KEYIN chaqirilishi kerak: ombor tanlovi token joyiga qarab aniqlanadi. */
  setUser(user: unknown): void {
    write(currentPersistence(), USER_KEY, JSON.stringify(user))
  },

  clear(): void {
    erase(ACCESS_KEY)
    erase(REFRESH_KEY)
    erase(USER_KEY)
  },

  get isAuthenticated(): boolean {
    return read(ACCESS_KEY) !== null
  },
}

// ============================================================
// Ichki yordamchilar
//
// Har bir murojaat `try` ichida: Safari'ning maxfiy rejimida va
// «saytlarga ma'lumot saqlashni taqiqlash» sozlamasida `localStorage`
// istisno tashlaydi. Bunday holatda sessiya faqat xotirada davom etadi —
// bu xato emas, shunchaki eslab qolinmaydi.
// ============================================================

function storeOf(persistence: Persistence): Storage {
  return persistence === 'local' ? globalThis.localStorage : globalThis.sessionStorage
}

/** Avval doimiy ombor, keyin joriy tab ombori. */
function read(key: string): string | null {
  try {
    return globalThis.localStorage.getItem(key) ?? globalThis.sessionStorage.getItem(key)
  } catch {
    return null
  }
}

function write(persistence: Persistence, key: string, value: string): void {
  try {
    storeOf(persistence).setItem(key, value)
    // Bir kalit bir vaqtda ikki omborda turib qolmasligi kerak —
    // aks holda `read` eski qiymatni qaytarishi mumkin
    storeOf(persistence === 'local' ? 'session' : 'local').removeItem(key)
  } catch {
    // yuqoridagi izohga qarang
  }
}

function erase(key: string): void {
  try {
    globalThis.localStorage.removeItem(key)
    globalThis.sessionStorage.removeItem(key)
  } catch {
    // yuqoridagi izohga qarang
  }
}

/** Joriy sessiya qaysi omborda: access token qayerda bo'lsa — o'sha yerda. */
function currentPersistence(): Persistence {
  try {
    return globalThis.localStorage.getItem(ACCESS_KEY) === null ? 'session' : 'local'
  } catch {
    return 'session'
  }
}
