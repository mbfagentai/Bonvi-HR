import { createFileRoute } from '@tanstack/react-router'

import { ProbationsPage } from '@/features/probations/components/probations-page'
import { validateListSearch } from '@/shared/lib/list-search'

/**
 * Sinov muddatidagi xodimlar.
 *
 * HR eng ko'p so'ragan imkoniyat shu sahifada: muddat tugashiga 7 kun
 * qolganlar yuqorida ogohlantirish bo'lib chiqadi. Excel'da bu qo'lda
 * kuzatilardi va tez-tez o'tkazib yuborilardi.
 */
export const Route = createFileRoute('/_app/sinov')({
  /**
   * Qidiruv matni — manzil satrida (`employees-search.ts` naqshi).
   * Sabab ham bir xil: qatorni bosib xodim kartasiga o'tgan va «orqaga»
   * bosgan HR qidiruvni qayta terishi kerak emas.
   *
   * Holat (`status`) esa ATAYLAB manzilda emas — u sahifaning o'zida
   * qoladi: yuqoridagi ogohlantirish tugmasi uni bir bosishda
   * o'zgartiradi va bu «filtr qo'yish» emas, ko'rinishni almashtirish.
   */
  validateSearch: validateListSearch,

  component: ProbationsPage,
})
