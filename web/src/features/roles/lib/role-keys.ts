/**
 * Ruxsat katalogining TARJIMA qilinadigan kalitlari.
 *
 * <b>Nega frontendda ro'yxat bor, katalog serverdan kelsa ham.</b> Server
 * yorliq yubormaydi — faqat kalit (`employees`, `employment.hire`) va
 * matnni har tilda frontend tanlaydi (rollar shartnomasi §2). Tarjima esa
 * statik JSON'da turadi, ya'ni «qaysi kalitlar bo'lishi mumkin» degan
 * ro'yxat kodda ham bo'lishi SHART: `scripts/check-locales.mjs` aynan shu
 * massivlarni o'qib, har biriga to'rt tilda matn borligini tekshiradi.
 * Usiz yangi bo'lim qo'shilganda HR ekranda xom kalitni ko'rardi va buni
 * hech kim sezmasdi.
 *
 * <b>Katalogda bor, bu yerda yo'q kalit</b> (backend yangi bo'lim qo'shgan,
 * frontend hali yangilanmagan) ekranni BUZMAYDI: `useRoleLabels` uni
 * xom holida ko'rsatadi — yashirmaydi, chunki yashirilgan bo'lim «bunday
 * ruxsat yo'q» degan yolg'on taassurot berardi.
 *
 * ⚠️ Massivlar `export const NOM = [...] as const` shaklida qolishi
 * kerak — tekshiruv skripti ularni oddiy muntazam ifoda bilan o'qiydi
 * (tip izohi qo'shilsa, u massivni topolmay qoladi).
 */

/**
 * Daraja kalitlari — «Yo'q» dan «To'liq» gacha, O'SISH tartibida.
 *
 * `none` serverda YO'Q (u «bo'limning birorta ham kodi yo'q» degani), lekin
 * ekranda u ham tanlov — shu sababli tarjimasi ham kerak.
 */
export const ACCESS_LEVELS = ['none', 'view', 'edit', 'full'] as const

export type AccessLevel = (typeof ACCESS_LEVELS)[number]

/** Serverdagi haqiqiy darajalar — ular kodlar ro'yxatiga ochiladi. */
export type GrantLevel = Exclude<AccessLevel, 'none'>

/** Katalogdagi bo'limlar — shartnoma §2 dagi tartibda. */
export const ROLE_MODULE_KEYS = [
  'employees',
  'employment',
  'lists',
  'incentives',
  'feedback',
  'safety',
  'recruiting',
  'blanks',
  'processes',
  'attendance',
  'catalog',
  'reports',
  'users',
  'roles',
  'audit',
  'settings',
] as const

/**
 * Mehnat munosabatlari amallari — ilgari bitta `employment.manage` edi.
 *
 * Har biri serverdagi bitta endpoint'ga mos keladi va shu sababli ekranda
 * ham ALOHIDA katakcha: «bo'shatadi, lekin oklad o'zgartirmaydi» degan rol
 * endi mumkin.
 */
export const ROLE_ACTION_CODES = [
  'employment.hire',
  'employment.transfer',
  'employment.duty',
  'employment.salary',
  'employment.terminate',
  'employment.probation.extend',
  'employment.probation.complete',
  'employment.correct',
  'blanks.approve',
  'processes.override',
] as const

/**
 * Hech qaysi bo'limning darajasiga kirmaydigan alohida ruxsatlar.
 *
 * `mcp.connect` — AI orqali o'qish (2026-10-01, `docs/SPEC-mcp.md` §1):
 * Claude/ChatGPT faqat o'qiydi va faqat egasining o'z ruxsatlari doirasida.
 *
 * `employees.personal.view` — shaxsiy ma'lumotlar (JSHSHIR, pasport, tug'ilgan
 * sana…, 2026-10-12): server uni «xodimlar» darajasiga emas, qo'shimchalarga
 * qo'ygan (`PermissionCatalog.Extras`) — «Ko'rish» darajali rolga alohida
 * beriladi, «O'zgartirish» da esa o'z-o'zidan bor. Ro'yxatda bo'lmasa,
 * muharrir uni xom kod bilan ko'rsatardi.
 */
export const ROLE_EXTRA_CODES = [
  'salary.view',
  'salary.extra.view',
  'mcp.connect',
  'employees.personal.view',
] as const
