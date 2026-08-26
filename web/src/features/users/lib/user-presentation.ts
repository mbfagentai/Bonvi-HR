/** `UserEndpoints.cs` dagi guruh ruxsati — GET ham shu bilan himoyalangan. */
export const USERS_MANAGE = 'users.manage'

/**
 * Tizimdagi rollar — QO'LDA yozilgan, chunki roʻyxatni oladigan endpoint
 * (`GET /api/roles`) yo'q.
 *
 * Bu tasodifiy emas: `SystemRoles.Defaults` (`api/src/Bonvi.Domain/Identity/
 * Permissions.cs`) ga ko'ra tizimda ATAYLAB faqat ikkita rol bor — «tizimdan
 * kim foydalanadi» qismidagi izohga qarang. Uchinchisi kerak bo'lganda bu
 * ro'yxat ham, backend ham birga o'zgaradi; alohida endpoint qurish ikkita
 * doim bir xil qatorli ro'yxat uchun ortiqcha qatlam bo'lardi.
 */
export const ROLE_CODES = ['admin', 'hr_manager'] as const

export type RoleCode = (typeof ROLE_CODES)[number]

export const ADMIN_ROLE_CODE: RoleCode = 'admin'
