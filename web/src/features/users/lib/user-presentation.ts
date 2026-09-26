import type { RoleSummary } from '@/features/roles/api/roles-api'
import { SUPER_ADMIN_ROLE_CODE } from '@/features/roles/lib/role-presentation'

/** `UserEndpoints.cs` dagi guruh ruxsati — GET ham shu bilan himoyalangan. */
export const USERS_MANAGE = 'users.manage'

/**
 * Bosh rahbar — tizimning o'zi yaratgan ikkinchi rol. Kod bilan faqat
 * ikki qoida uchun kerak (pastdagi ro'yxatlar): nomini egasi o'zgartirishi
 * mumkin, kodi esa o'zgarmaydi.
 */
export const MAIN_BOSS_ROLE_CODE = 'main_boss'

/**
 * Rahbariyat rollari — foydalanuvchi boshqaruviga qodir hisoblar.
 *
 * `SystemRoles.FullAccess` ning ko'zgusi. Kerak bo'ladigan yagona joy —
 * «oxirgi rahbarni faolsizlantirib bo'lmaydi» tugmasini oldindan o'chirish.
 */
export const LEADER_ROLE_CODES: readonly string[] = [SUPER_ADMIN_ROLE_CODE, MAIN_BOSS_ROLE_CODE]

/**
 * Tizimda ATIGI BITTA FAOL egasi bo'ladigan rollar
 * (`SystemRoles.SingleHolder` ning ko'zgusi).
 *
 * Interfeys bu ro'yxatni faqat IZOH ko'rsatish uchun ishlatadi — rad etish
 * qarori baribir serverda qabul qilinadi («atigi bitta» butun tizim
 * holatiga bog'liq va uni klient ishonchli hisoblay olmaydi: ro'yxatda
 * faqat shu lahzadagi nusxa bor).
 */
export const SINGLE_HOLDER_ROLE_CODES: readonly string[] = [
  SUPER_ADMIN_ROLE_CODE,
  MAIN_BOSS_ROLE_CODE,
]

/**
 * So'rovchining o'z roli — `GET /api/roles` ro'yxatidan.
 *
 * `AuthUser.roles` hamon MASSIV (token shakli o'zgarmadi), lekin unda bitta
 * element bo'ladi — hisobda aynan bitta rol bor. Ro'yxatda topilmasa
 * `null` qaytadi va pastdagi qoida uni «hech kimga tegolmaydi» deb qaraydi.
 */
export function actingRoleOf(
  roles: readonly RoleSummary[],
  codes: readonly string[] | undefined,
): RoleSummary | null {
  if (codes === undefined) return null
  return roles.find((role) => codes.includes(role.code)) ?? null
}

/**
 * ⭐ «Kim kimga tega oladi» — backend'dagi `UserRoleRules.Allowed` ning
 * KO'ZGUSI (rollar shartnomasi §5).
 *
 * <b>Nega klientda ham bor.</b> Server baribir o'zi tekshiradi va 403
 * (`foydalanuvchi.rolga_haqqi_yoq`) qaytaradi — bu yerdagi nusxa faqat
 * BOSILMAYDIGAN tugmani oldindan o'chirish uchun. Tugmani ko'rsatib, keyin
 * xato qaytarish yomon tajriba: foydalanuvchi nimadir buzilgan deb o'ylaydi.
 *
 * <b>Qoida endi IYERARXIYA emas, TO'PLAM:</b> rollarni egasi o'zi yaratadi,
 * ya'ni «kim kimdan yuqori» degan qat'iy ro'yxat yo'q. Shu sababli
 * ruxsatlar solishtiriladi:
 *   1. so'rovchida rol yo'q — hech kimga;
 *   2. so'rovchi super administrator — hammaga;
 *   3. nishon super administrator — hech kim (undan boshqa);
 *   4. nishon so'rovchining O'Z roli — yo'q (teng odam tengini boshqarmaydi);
 *   5. nishonning har bir ruxsati so'rovchida ham bor — ha, aks holda yo'q.
 * Rolsiz nishon — bo'sh to'plam, ya'ni `users.manage` li har kim tegadi.
 *
 * Bu avvalgi xatti-harakatni o'zgarishsiz saqlaydi (bosh rahbar HR
 * menejerlarni boshqaradi, boshqa bosh rahbarni emas) va uni egasi
 * yaratgan rollarga ham kengaytiradi.
 */
export function canActOnRole(acting: RoleSummary | null, target: RoleSummary | null): boolean {
  if (acting === null) return false
  if (acting.code === SUPER_ADMIN_ROLE_CODE) return true
  if (target === null) return true
  if (target.code === SUPER_ADMIN_ROLE_CODE) return false
  if (target.id === acting.id) return false

  const held = new Set(acting.permissions)
  if (!target.permissions.every((code) => held.has(code))) return false

  // Foydalanuvchilarni boshqaradigan TENG rol — «hamkasb»: serverdagi
  // `UserRoleRules` bilan bir xil, u QAT'IY pastda bo'lishi shart
  const peer =
    target.permissions.includes('users.manage') &&
    new Set(target.permissions).size === held.size
  return !peer
}

/**
 * Hisobga (uning JORIY roli bo'yicha) tega oladimi.
 *
 * `GET /api/roles` botning texnik rolini QAYTARMAYDI, ya'ni bunday
 * hisobning rolini klient solishtira olmaydi. Noaniq holatda eng
 * ehtiyotkor javob — unga faqat super administrator tegadi. Bu faqat
 * tugmani ko'rsatish haqida: qarorni baribir server o'zi chiqaradi.
 */
export function canActOnUser(
  acting: RoleSummary | null,
  roles: readonly RoleSummary[],
  targetCode: string | null,
): boolean {
  if (targetCode === null) return canActOnRole(acting, null)

  const target = roles.find((role) => role.code === targetCode)
  if (target === undefined) return acting?.code === SUPER_ADMIN_ROLE_CODE

  return canActOnRole(acting, target)
}
