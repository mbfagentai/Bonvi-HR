import { createFileRoute } from '@tanstack/react-router'

import { EmploymentEventsPage } from '@/features/employments/components/employments-page'
import { validateEmploymentEventsSearch } from '@/features/employments/lib/employments-search'

/**
 * Buyruqlar — barcha xodimlar bo'yicha mehnat hodisalari, bitta ro'yxatda.
 *
 * Hozirgacha HR hodisalarni faqat xodim kartasidan (mehnat tarixi
 * lentasi) ko'ra olardi. Bu sahifa — «shu oyda qanday buyruqlar chiqdi?»
 * degan savolga javob beradigan umumiy ro'yxat.
 */
export const Route = createFileRoute('/_app/buyruqlar')({
  /**
   * Qidiruv, filtrlar va sahifa — manzil satrida (`employees-search.ts`
   * naqshi, sabab ham bir xil: «orqaga» bosgan HR filtrsiz holatga
   * tushib qolmasin).
   */
  validateSearch: validateEmploymentEventsSearch,

  component: EmploymentEventsPage,
})
