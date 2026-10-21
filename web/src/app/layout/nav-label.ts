import { NAV } from './nav-items'

/**
 * Manzil menyudagi qaysi sahifa — uning nomi kaliti (`nav.*`), yoki `null`.
 *
 * Kartadagi «orqaga» tugmasi shu bilan yoziladi (`shared/lib/return-to.ts`):
 * HR «Ishdan ketganlar» dan kelgan bo'lsa — «← Ishdan ketganlar», «Rad
 * etilganlar» dan — «← Rad etilganlar». Nom menyudan olinadi, ya'ni tugma
 * va yon menyu hech qachon bir-biridan farq qilmaydi.
 *
 * Yo'l ANIQ solishtiriladi (`sidebar.tsx` dagi `onPath` kabi boshi bilan
 * emas): `/xodimlar/…` — karta, u menyudagi sahifa emas va `null` qaytadi.
 * Kirish sahifasi va 404 ham menyuda yo'q.
 *
 * Bitta yo'lda bir nechta band bo'lsa («Xodimlar» va «Ishdan ketganlar»,
 * «Nomzodlar» va «Rad etilganlar»), `sidebar.tsx` dagi `isActive` qoidasi
 * takrorlanadi: parametrli band — faqat o'sha parametrlar manzilda
 * turganda, aks holda parametrsizi.
 */
export function navLabelKey(pathname: string, search: Record<string, unknown>): string | null {
  const path = pathname.length > 1 ? pathname.replace(/\/+$/, '') : pathname

  // Ochiluvchi guruhning o'zi (`/rekruting`, `/verifikatsiya`) sahifa emas
  const items = NAV.flatMap((section) =>
    section.items.flatMap((item) => [item, ...(item.children ?? [])]),
  ).filter((item) => item.children === undefined && item.to === path)

  const found =
    items.find((item) => item.search !== undefined && matches(item.search, search)) ??
    items.find((item) => item.search === undefined)

  return found === undefined ? null : `nav.${found.key}`
}

/** `sidebar.tsx` dagi `matches` bilan bir xil: router `true` ni `'true'` deb solishtiradi. */
function matches(
  expected: Readonly<Record<string, string>>,
  actual: Record<string, unknown>,
): boolean {
  return Object.entries(expected).every(([key, value]) => String(actual[key] ?? '') === value)
}
