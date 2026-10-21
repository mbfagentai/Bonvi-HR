import { createFileRoute } from '@tanstack/react-router'

import { ProbationsPage } from '@/features/probations/components/probations-page'
import {
  validateDateRangeSearch,
  validateListSearch,
  type DateRangeSearch,
  type ListSearch,
} from '@/shared/lib/list-search'

/**
 * Sinov muddatidagi xodimlar.
 *
 * HR eng ko'p so'ragan imkoniyat shu sahifada: muddat tugashiga 7 kun
 * qolganlar yuqorida ogohlantirish bo'lib chiqadi. Excel'da bu qo'lda
 * kuzatilardi va tez-tez o'tkazib yuborilardi.
 */
export const Route = createFileRoute('/_app/sinov')({
  /**
   * Qidiruv matni va sinov tugash sanasi oralig'i — manzil satrida
   * (`employees-search.ts` naqshi). Sabab ham bir xil: qatorni bosib
   * xodim kartasiga o'tgan va «orqaga» bosgan HR qidiruvni ham, oraliqni
   * ham qayta terishi kerak emas.
   *
   * Holat (`status`) esa ATAYLAB manzilda emas — u sahifaning o'zida
   * qoladi: yuqoridagi ogohlantirish tugmasi uni bir bosishda
   * o'zgartiradi va bu «filtr qo'yish» emas, ko'rinishni almashtirish.
   */
  validateSearch: (search: Record<string, unknown>): ListSearch & DateRangeSearch => ({
    ...validateListSearch(search),
    ...validateDateRangeSearch(search),
  }),

  component: ProbationsPage,
})
