import { createFileRoute } from '@tanstack/react-router'

import { CandidatesPage } from '@/features/recruiting/components/candidates-page'
import { validateCandidatesSearch } from '@/features/recruiting/lib/candidates-search'

/**
 * Nomzodlar — 2-bosqichning asosiy ekrani.
 *
 * Ikkita ko'rinish (jadval va voronka) ham SHU marshrutda: voronka
 * alohida endpoint emas, u ro'yxatning boshqa chizilishi. Ko'rinish
 * manzil parametrida (`?view=funnel`) — sabab `candidates-search.ts`
 * dagi `CandidatesSearch` izohida.
 */
export const Route = createFileRoute('/_app/nomzodlar/')({
  /**
   * Qidiruv, filtrlar, saralash, sahifa va ko'rinish — manzil satrida.
   *
   * NEGA: bu komponent nomzod kartasiga o'tilganda unmount bo'ladi.
   * Holat uning ichida bo'lsa, «orqaga» bosgan HR uchinchi sahifadan
   * birinchisiga, filtrsiz holatga tushib qolardi.
   *
   * Tekshiruv `validateCandidatesSearch` da: noto'g'ri qiymat ekranni
   * buzmaydi, shunchaki tashlab yuboriladi.
   */
  validateSearch: validateCandidatesSearch,

  component: CandidatesPage,
})
