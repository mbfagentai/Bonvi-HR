import { createFileRoute } from '@tanstack/react-router'

import { IncidentsPage } from '@/features/safety/components/incidents-page'
import { validateListSearch } from '@/shared/lib/list-search'

/**
 * Baxtsiz hodisalar jurnali — faqat `safety.view` ruxsati bilan.
 *
 * `foydalanuvchilar.tsx` dagi bilan bir xil naqsh: marshrutda `beforeLoad`
 * yo'q, sahifaning o'zi ruxsat holatini ko'rsatadi — chunki server
 * `GET /api/safety/incidents` ni ham xuddi shu ruxsat bilan himoyalagan.
 */
export const Route = createFileRoute('/_app/hodisalar')({
  /**
   * Qidiruv matni — manzil satrida (loyihaning qat'iy konvensiyasi).
   * Sana oralig'i va daraja sahifaning o'zida qoladi: ular kamdan-kam
   * o'zgartiriladi va bu yechim ilgari ongli ravishda qabul qilingan.
   */
  validateSearch: validateListSearch,

  component: IncidentsPage,
})
