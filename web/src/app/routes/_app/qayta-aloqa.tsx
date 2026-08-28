import { createFileRoute } from '@tanstack/react-router'

import { ExitInterviewsPage } from '@/features/feedback/components/exit-interviews-page'
import { validateListSearch } from '@/shared/lib/list-search'

/**
 * Qayta aloqa (exit interview) jurnali — faqat `feedback.view` ruxsati bilan.
 *
 * `hodisalar.tsx` dagi bilan bir xil naqsh: marshrutda `beforeLoad` yo'q,
 * sahifaning o'zi ruxsat holatini ko'rsatadi.
 */
export const Route = createFileRoute('/_app/qayta-aloqa')({
  /**
   * Qidiruv matni — manzil satrida (loyihaning qat'iy konvensiyasi).
   * Sana oralig'i esa sahifaning o'zida qoladi: u kamdan-kam
   * o'zgartiriladi va bu yechim ilgari ongli ravishda qabul qilingan.
   */
  validateSearch: validateListSearch,

  component: ExitInterviewsPage,
})
