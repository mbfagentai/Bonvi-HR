import { createFileRoute } from '@tanstack/react-router'

import { IncentivesPage } from '@/features/incentives/components/incentives-page'
import { validateIncentivesSearch } from '@/features/incentives/lib/incentives-search'

/**
 * Rag'bat va jarima — barcha xodimlar bo'yicha umumiy ro'yxat.
 *
 * Hozirgacha HR bu yozuvlarni faqat xodim kartasidan («Rag'bat/Jarima»
 * tabi) ko'ra olardi, ya'ni «shu oyda kimlarga jarima yozildi?» degan
 * savolga javob berish uchun 540 ta kartani birma-bir ochish kerak edi.
 *
 * Marshrutda `beforeLoad` YO'Q, garchi sahifa `incentives.view` ruxsatini
 * talab qilsa ham — `hodisalar.tsx` va `qayta-aloqa.tsx` dagi bilan bir
 * xil naqsh: ruxsat holatini sahifaning o'zi ko'rsatadi, chunki server
 * `GET /api/incentives` ni ham xuddi shu ruxsat bilan himoyalagan.
 */
export const Route = createFileRoute('/_app/ragbat')({
  /**
   * Qidiruv, filtrlar, saralash va sahifa — manzil satrida
   * (`employments-search.ts` naqshi). Sabab ham bir xil: qatorni bosib
   * xodim kartasiga o'tgan va «orqaga» bosgan HR filtrsiz holatga tushib
   * qolmasin — bu ekranda kartaga o'tish eng ko'p takrorlanadigan
   * harakat.
   */
  validateSearch: validateIncentivesSearch,

  component: IncentivesPage,
})
