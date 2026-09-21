import { createFileRoute } from '@tanstack/react-router'

import { AttendancePassesPage } from '@/features/verification/components/attendance-passes-page'
import { validatePassesSearch } from '@/features/verification/lib/attendance-search'

/**
 * O'tishlar jurnali — `attendance.view` ruxsati bilan.
 *
 * `hodisalar.tsx` naqshi: marshrutda `beforeLoad` YO'Q, ruxsat holatini
 * sahifaning o'zi ko'rsatadi — server `GET /api/attendance/passes` ni
 * xuddi shu ruxsat bilan himoyalagan (SPEC §7.3).
 */
export const Route = createFileRoute('/_app/verifikatsiya/otishlar')({
  /**
   * ⚠️ `validateListSearch` EMAS (SPEC §8.2 — AMENDED 2026-09-17): u faqat
   * `q` ni qoldiradi va `passId` ni JIMGINA tashlab yuborardi. Taxtadagi
   * «ishdan ketgan xodim» ogohlantirishi aynan `?passId=` bilan shu yerga
   * olib keladi — havola ochilar edi-yu, xavfsizlik hodisasining o'zi
   * ko'rinmasdi.
   */
  validateSearch: validatePassesSearch,

  /** To'liq kenglik — verifikatsiya bo'limining hamma sahifasi bir xil (`app-shell.tsx`). */
  staticData: { fullWidth: true },

  component: AttendancePassesPage,
})
