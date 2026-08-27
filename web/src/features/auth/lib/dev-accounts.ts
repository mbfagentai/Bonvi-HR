/**
 * Dev-rejimdagi sinov hisoblari.
 *
 * ⚠️ FAQAT ISHLAB CHIQISH UCHUN.
 *
 * <b>Uch qatlamli himoya</b> — production build'ga parol tushmasligi uchun:
 *
 * 1. `import.meta.env.DEV` — kompilyatsiya paytidagi qiymat. Production'da
 *    `false` ga almashadi va shart bloki o'lik kodga aylanadi.
 * 2. `VITE_DEV_*` o'zgaruvchilariga **statik** murojaat (`import.meta.env[key]`
 *    emas). Faqat shunda Vite ularni qiymat bilan almashtira oladi —
 *    dinamik kalit bilan u butun `env` obyektini bundle'ga solib yuboradi.
 * 3. `vite.config.ts` da production rejimi uchun `define` — bu o'zgaruvchilarni
 *    majburan `undefined` qiladi. Ya'ni build paytida ular muhitda mavjud
 *    bo'lsa ham, bundle'ga tushmaydi.
 *
 * Tekshirilgan: `VITE_DEV_PASSWORD` yoqilgan holda production build qilinganda
 * ham parol yakuniy faylda topilmadi.
 */
export interface DevAccount {
  /** Ekranda ko'rinadigan nom. */
  label: string
  /** Rol kodi — qaysi huquqlar bilan kirilayotgani ko'rinib tursin. */
  role: string
  /** Rol nima bera olishini bir jumlada tushuntiradi. */
  hint: string
  /** Telefon raqami: 9 xona, +998 siz. */
  login: string
  password: string
}

/**
 * Sinov hisoblarining telefon raqamlari.
 *
 * Bular backend'dagi `DatabaseInitializer.CreateDevUsersAsync` bilan
 * bir xil bo'lishi SHART — u shu raqamlar bilan foydalanuvchi yaratadi.
 * Administrator raqami esa sozlanadigan, shu sababli muhitdan olinadi.
 */
const HR_MANAGER_LOGIN = '905088877'

function trimmed(value: string | undefined): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined
}

/**
 * Mavjud sinov hisoblari ro'yxati.
 * Production'da <b>doim bo'sh</b>.
 */
export function devAccounts(): DevAccount[] {
  if (!import.meta.env.DEV) {
    return []
  }

  // Statik murojaat — Vite buni qiymat bilan almashtiradi
  const adminLogin = trimmed(import.meta.env.VITE_DEV_LOGIN)
  const password = trimmed(import.meta.env.VITE_DEV_PASSWORD)

  if (!adminLogin || !password) {
    return []
  }

  return [
    {
      label: trimmed(import.meta.env.VITE_DEV_LABEL) ?? 'Super administrator',
      role: 'super_admin',
      hint: 'Barcha ruxsatlar, jumladan foydalanuvchi boshqaruvi',
      login: adminLogin,
      password,
    },
    {
      label: 'HR menejeri',
      role: 'hr_manager',
      hint: 'Butun kadrlar ishi; foydalanuvchi boshqaruvi yopiq',
      login: HR_MANAGER_LOGIN,
      password,
    },
  ]
}
