import { createFileRoute } from '@tanstack/react-router'

import { AttendanceDevicesPage } from '@/features/verification/components/attendance-devices-page'

/**
 * Turniket qurilmalari — faqat `attendance.manage` (SPEC §7.3).
 *
 * Marshrutda to'siq yo'q (`hodisalar.tsx` naqshi, §8.2): sahifaning o'zi
 * «ruxsat yo'q» holatini ko'rsatadi, server esa `GET /devices` ni shu
 * ruxsat bilan yopgan. Manzil holati yo'q — «faol / hammasi» tanlovi
 * kamdan-kam almashtiriladi va havolada ma'nosiz.
 */
export const Route = createFileRoute('/_app/verifikatsiya/qurilmalar')({
  component: AttendanceDevicesPage,
})
