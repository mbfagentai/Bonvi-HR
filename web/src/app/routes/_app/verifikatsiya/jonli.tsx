import { createFileRoute } from '@tanstack/react-router'

import { AttendanceDashboardPage } from '@/features/verification/components/attendance-dashboard-page'
import { validateDashboardSearch } from '@/features/verification/lib/attendance-search'

/**
 * Davomat statistikasi — verifikatsiya bo'limining asosiy sahifasi,
 * `attendance.view` ruxsati bilan.
 *
 * Yo'l ATAYLAB `/jonli` bo'lib qoldi (ilgari bu yerda «Hozir ishda» taxtasi
 * turardi): menyu, qurilmalar sahifasidagi qizil qator va HR ning
 * saqlangan xatcho'plari shu manzilga olib keladi.
 *
 * `hodisalar.tsx` naqshi: `beforeLoad` yo'q, ruxsat holatini sahifa
 * ko'rsatadi, chegara esa serverda (SPEC §7.3, §8.2).
 */
export const Route = createFileRoute('/_app/verifikatsiya/jonli')({
  /**
   * Kun, qidiruv, holat, filtrlar, saralash va sahifa — manzilda
   * (`DashboardSearch` izohi): «orqaga» oldingi kunga qaytaradi, havola
   * aynan shu ko'rinishni ochadi.
   */
  validateSearch: validateDashboardSearch,

  component: AttendanceDashboardPage,
})
