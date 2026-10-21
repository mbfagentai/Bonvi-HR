import { createFileRoute } from '@tanstack/react-router'

import { AbsenceRequestsPage } from '@/features/verification/components/absence-requests-page'
import { validateDateRangeSearch } from '@/shared/lib/list-search'

/**
 * Yo'qlik so'rovlari — butun tashkilot bo'yicha navbat, `attendance.view`
 * ruxsati bilan (SPEC-so'rovlar §B5).
 *
 * `hodisalar.tsx` naqshi: `beforeLoad` yo'q, ruxsat holatini sahifa
 * ko'rsatadi, chegara esa serverda.
 */
export const Route = createFileRoute('/_app/verifikatsiya/sorovlar')({
  /**
   * To'liq kenglik — verifikatsiya bo'limining qolgan ekranlari kabi.
   * Jadvalda yettita ustun bor (xodim, tur, davr, ikkita izoh, holat,
   * amallar) va 1400 px chegarasi ularni katta monitorda sababsiz
   * siqib qo'yardi.
   */
  staticData: { fullWidth: true },

  /**
   * So'rov davri oralig'i — manzil satrida (loyihaning qat'iy
   * konvensiyasi): «orqaga» bosgan yoki sahifani yangilagan HR oraliqni
   * qayta terishi kerak emas. Ikkala navbatga (yo'qlik, jadval) umumiy.
   */
  validateSearch: validateDateRangeSearch,

  component: AbsenceRequestsPage,
})
