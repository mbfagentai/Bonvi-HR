import type { RoleCatalog, RoleCatalogModule, RoleSummary } from '../api/roles-api'
import type { AccessLevel, GrantLevel } from './role-keys'

/**
 * Rol muharririning holati — ruxsat KODLARI emas, ekrandagi tanlovlar.
 *
 * Kodlar ro'yxati bilan to'g'ridan-to'g'ri ishlash mumkin edi, lekin u
 * holda har bir segmentni bosish «qaysi kodlarni olib tashlay, qaysilari
 * boshqa daraja uchun ham kerak» degan hisobni talab qilardi. Bu yerda
 * esa har bir bo'limda BITTA qiymat turadi va kodlar faqat saqlashda
 * yig'iladi (`permissionsOf`).
 */
export interface RoleForm {
  name: string
  /** Bo'lim kaliti → tanlangan daraja. Katalogdagi har bir bo'lim uchun bor. */
  levels: Readonly<Record<string, AccessLevel>>
  /** Belgilangan amallar (`employment.hire`, …). */
  actions: ReadonlySet<string>
  /** Yoqilgan qo'shimcha ruxsatlar (`salary.view`). */
  extras: ReadonlySet<string>
}

/** Serverdagi darajalar o'sish tartibida — «eng yuqori» shu tartibdan topiladi. */
const GRANT_ORDER: readonly GrantLevel[] = ['view', 'edit', 'full']

/** Bo'lim taklif qiladigan darajalar, o'sish tartibida («Yo'q» siz). */
export function offeredLevels(module: RoleCatalogModule): GrantLevel[] {
  return GRANT_ORDER.filter((level) => (module.levels[level]?.length ?? 0) > 0)
}

/**
 * ⭐ Rolning kodlaridan bo'lim darajasini chiqarish.
 *
 * Qoida (shartnoma §7): kodlari rolda TO'LIQ bor bo'lgan ENG YUQORI
 * daraja. Tekshiruv yuqoridan pastga ketadi va birinchi mos kelgani
 * olinadi — darajalar o'suvchi bo'lgani uchun u eng yuqorisi bo'ladi.
 *
 * Misol: rolda `employees.view` va `employees.edit` bor, `employees.create`
 * yo'q. «O'zgartirish» (`view + create + edit`) to'liq emas, demak daraja
 * «Ko'rish». Ortiqcha `employees.edit` hech bir darajaga to'liq kirmaydi —
 * u `droppedPermissions` da ko'rinadi va muharrir buni ochiq aytadi.
 *
 * Hech bir daraja to'liq bo'lmasa — «Yo'q».
 */
export function deriveLevel(module: RoleCatalogModule, granted: ReadonlySet<string>): AccessLevel {
  for (const level of [...offeredLevels(module)].reverse()) {
    const codes = module.levels[level] ?? []
    if (codes.every((code) => granted.has(code))) return level
  }
  return 'none'
}

/**
 * Formaning boshlang'ich holati. `null` — yangi rol: hamma bo'lim «Yo'q».
 *
 * Amal faqat bo'lim «Yo'q» dan yuqori bo'lsa belgilangan hisoblanadi:
 * ko'rish huquqisiz amal ma'nosiz (shartnoma §2) va muharrir uni
 * baribir o'chiq ko'rsatadi.
 */
export function formFromRole(catalog: RoleCatalog, role: RoleSummary | null): RoleForm {
  const granted = new Set(role?.permissions ?? [])

  const levels: Record<string, AccessLevel> = {}
  const actions = new Set<string>()

  for (const module of catalog.modules) {
    const level = deriveLevel(module, granted)
    levels[module.key] = level

    if (level === 'none') continue
    for (const code of module.actions) {
      if (granted.has(code)) actions.add(code)
    }
  }

  const extras = new Set(catalog.extras.filter((code) => granted.has(code)))

  return { name: role?.name ?? '', levels, actions, extras }
}

/**
 * Saqlanadigan kodlar: tanlangan darajalarning kodlari + belgilangan
 * amallar + yoqilgan qo'shimchalar. Takrorsiz va tartiblangan —
 * server ro'yxatni to'liq ALMASHTIRADI va tartib audit yozuvida
 * «eski → yangi» ni solishtirishni osonlashtiradi.
 */
export function permissionsOf(catalog: RoleCatalog, form: RoleForm): string[] {
  const codes = new Set<string>()

  for (const module of catalog.modules) {
    const level = form.levels[module.key] ?? 'none'
    if (level === 'none') continue

    for (const code of module.levels[level] ?? []) codes.add(code)
    for (const code of module.actions) {
      if (form.actions.has(code)) codes.add(code)
    }
  }

  for (const code of catalog.extras) {
    if (form.extras.has(code)) codes.add(code)
  }

  return [...codes].sort()
}

/**
 * Rolda bor, lekin saqlashda tushib qoladigan kodlar.
 *
 * NEGA kerak: darajalar butun bo'lak bo'lib ishlaydi, rolda esa (qo'lda
 * yoki eski versiyada yozilgan) bo'lakning bir qismi bo'lishi mumkin.
 * Muharrir uni bitta darajaga yaxlitlaydi va nomini o'zgartirish kabi
 * begunoh saqlash ham shu qoldiqni JIMGINA o'chirib yuborardi. Ro'yxat
 * bo'sh bo'lmasa, muharrir buni saqlashdan OLDIN aytadi.
 */
export function droppedPermissions(catalog: RoleCatalog, role: RoleSummary): string[] {
  const kept = new Set(permissionsOf(catalog, formFromRole(catalog, role)))
  return role.permissions.filter((code) => !kept.has(code))
}

/** `codes` ning hammasi `held` ichidami — «bu darajani bera olamanmi?» savoli. */
export function allHeld(codes: readonly string[], held: ReadonlySet<string>): boolean {
  return codes.every((code) => held.has(code))
}
