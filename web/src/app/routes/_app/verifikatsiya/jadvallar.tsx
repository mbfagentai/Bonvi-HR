import { createFileRoute } from '@tanstack/react-router'

import { AttendanceSchedulesPage } from '@/features/verification/components/attendance-schedules-page'

/**
 * Ish jadvallari, biriktirmalar va «kim uchun amal qiladi».
 *
 * O'qish — `attendance.view`, yozish — `attendance.manage` (SPEC §7.3).
 * Marshrutda to'siq yo'q (`hodisalar.tsx` naqshi, §8.2): sahifaning o'zi
 * ruxsat holatini ko'rsatadi, server esa har bir endpoint'ni o'zi yopgan.
 * Manzil holati yo'q — sahifada qidiruv ham, sahifalash ham yo'q.
 */
export const Route = createFileRoute('/_app/verifikatsiya/jadvallar')({
  /** To'liq kenglik — verifikatsiya bo'limining hamma sahifasi bir xil (`app-shell.tsx`). */
  staticData: { fullWidth: true },

  component: AttendanceSchedulesPage,
})
