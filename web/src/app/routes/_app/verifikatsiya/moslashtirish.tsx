import { createFileRoute } from '@tanstack/react-router'

import { AttendanceMappingPage } from '@/features/verification/components/attendance-mapping-page'
import { validateListSearch } from '@/shared/lib/list-search'

/**
 * Moslashtirish — turniket raqamlari ↔ xodimlar, faqat `attendance.manage`.
 *
 * `hodisalar.tsx` naqshi (SPEC §8.2): marshrutda `beforeLoad` to'sig'i
 * yo'q, sahifaning o'zi «ruxsat yo'q» holatini ko'rsatadi — server
 * `GET /api/attendance/identities` ni baribir shu ruxsat bilan yopgan.
 */
export const Route = createFileRoute('/_app/verifikatsiya/moslashtirish')({
  /**
   * Mavjud mosliklar qidiruvi — manzil satrida (loyihaning qat'iy
   * konvensiyasi): «orqaga» bosilganda va havola ulashilganda saqlanadi.
   * Sahifa raqamlari esa sahifaning o'zida — ular qidiruv bilan birga
   * noldan boshlanadi va havolada ma'nosiz.
   */
  validateSearch: validateListSearch,

  /** To'liq kenglik — verifikatsiya bo'limining hamma sahifasi bir xil (`app-shell.tsx`). */
  staticData: { fullWidth: true },

  component: AttendanceMappingPage,
})
