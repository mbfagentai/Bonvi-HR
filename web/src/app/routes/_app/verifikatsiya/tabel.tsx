import { createFileRoute } from '@tanstack/react-router'

import { AttendanceTimesheetPage } from '@/features/verification/components/attendance-timesheet-page'
import { validateTimesheetSearch } from '@/features/verification/lib/attendance-search'

/**
 * Tabel — xodim × kun, `attendance.view` ruxsati bilan.
 *
 * `hodisalar.tsx` naqshi: `beforeLoad` yo'q, ruxsat holatini sahifa
 * ko'rsatadi (SPEC §8.2).
 */
export const Route = createFileRoute('/_app/verifikatsiya/tabel')({
  /**
   * Oy, sahifa va qidiruv — manzilda (SPEC §8.2: «month + list search»).
   * Oy manzilga YOZILADI, sukut bo'lsa ham (`TimesheetSearch.year` izohi):
   * sentabrda yuborilgan havola oktabrda ochilganda boshqa tabelni
   * ko'rsatmasligi kerak.
   */
  validateSearch: validateTimesheetSearch,

  component: AttendanceTimesheetPage,
})
