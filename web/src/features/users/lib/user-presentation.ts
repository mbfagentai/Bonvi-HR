/** `UserEndpoints.cs` dagi guruh ruxsati — GET ham shu bilan himoyalangan. */
export const USERS_MANAGE = 'users.manage'

/**
 * Tizimdagi rollar — QO'LDA yozilgan, chunki roʻyxatni oladigan endpoint
 * (`GET /api/roles`) yo'q.
 *
 * Bu tasodifiy emas: `SystemRoles.Defaults` (`api/src/Bonvi.Domain/Identity/
 * Permissions.cs`) ga ko'ra tizimda ATAYLAB atigi uchta rol bor — «tizimdan
 * kim foydalanadi» qismidagi izohga qarang. To'rtinchisi kerak bo'lganda bu
 * ro'yxat ham, backend ham birga o'zgaradi; alohida endpoint qurish doim bir
 * xil uch qatorli ro'yxat uchun ortiqcha qatlam bo'lardi.
 *
 * Tartib IYERARXIK (kuchlidan kuchsizga), alifbo bo'yicha emas: forma shu
 * tartibda ko'rsatadi va u modelning o'zini tushuntiradi.
 */
export const ROLE_CODES = ['super_admin', 'main_boss', 'hr_manager'] as const

export type RoleCode = (typeof ROLE_CODES)[number]

export const SUPER_ADMIN_ROLE_CODE: RoleCode = 'super_admin'
export const MAIN_BOSS_ROLE_CODE: RoleCode = 'main_boss'
export const HR_MANAGER_ROLE_CODE: RoleCode = 'hr_manager'

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
 * «Kim kimga tega oladi» — backend'dagi `UserRoleRules` ning KO'ZGUSI.
 *
 * <b>Nega klientda ham bor.</b> Server baribir o'zi tekshiradi va 403
 * (`foydalanuvchi.rolga_haqqi_yoq`) qaytaradi — bu yerdagi nusxa faqat
 * BOSILMAYDIGAN tugmani oldindan o'chirish uchun. Tugmani ko'rsatib, keyin
 * xato qaytarish yomon tajriba: foydalanuvchi nimadir buzilgan deb o'ylaydi.
 *
 * <b>Nega qoida bitta funksiyada.</b> Ro'yxatda ham, formada ham, rasm
 * tahririda ham AYNAN shu savol beriladi. Har birida qo'lda yozilsa, bir
 * kun kelib biri unutilardi.
 *
 * Qoidaning o'zi:
 *   • super administrator — hammaga;
 *   • bosh rahbar — faqat HR menejerlarga;
 *   • qolganlar — hech kimga (ular bu ekranga baribir kira olmaydi).
 * Roli noma'lum NISHONGA faqat super administrator tegadi — noaniq holatda
 * eng ehtiyotkor javob shu (backend ham xuddi shunday qaraydi).
 */
export function canActOnRole(actingRole: string | null, targetRole: string | null): boolean {
  if (actingRole === SUPER_ADMIN_ROLE_CODE) return true
  if (actingRole === MAIN_BOSS_ROLE_CODE && targetRole === HR_MANAGER_ROLE_CODE) return true
  return false
}

/**
 * Joriy foydalanuvchining rol kodi.
 *
 * `AuthUser.roles` hamon MASSIV (token shakli o'zgarmadi), lekin unda bitta
 * element bo'ladi — hisobda aynan bitta rol bor. Ro'yxatda tanish kod
 * topilmasa `null` qaytadi va yuqoridagi qoida uni «hech kimga tegolmaydi»
 * deb qaraydi.
 */
export function currentRoleCode(roles: readonly string[] | undefined): string | null {
  return roles?.find((code) => (ROLE_CODES as readonly string[]).includes(code)) ?? null
}
