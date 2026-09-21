import { createFileRoute } from '@tanstack/react-router'

import { AttendanceReportsPage } from '@/features/verification/components/attendance-reports-page'
import { validateReportsSearch } from '@/features/verification/lib/attendance-search'

/**
 * Davomat hisobotlari — `attendance.view` ruxsati bilan; Excel esa
 * qo'shimcha `reports.export` talab qiladi (SPEC §7.3).
 *
 * `hodisalar.tsx` naqshi: `beforeLoad` yo'q, ruxsat holatini sahifa
 * ko'rsatadi (SPEC §8.2).
 */
export const Route = createFileRoute('/_app/verifikatsiya/hisobotlar')({
  /**
   * Ochiq tab (`?kind=`) va sahifa — manzilda (SPEC §8.4): «kechikkanlar
   * ro'yxati» havolasini yuborish mumkin bo'lsin.
   */
  validateSearch: validateReportsSearch,

  /** To'liq kenglik — verifikatsiya bo'limining hamma sahifasi bir xil (`app-shell.tsx`). */
  staticData: { fullWidth: true },

  component: AttendanceReportsPage,
})
