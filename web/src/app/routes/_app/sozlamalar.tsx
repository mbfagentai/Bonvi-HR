import { createFileRoute } from '@tanstack/react-router'

import { SystemSettingsPage } from '@/features/settings/components/system-settings-page'

/**
 * Tizim sozlamalari — faqat super administrator.
 *
 * Manzil qisqa (`/sozlamalar`, `/tizim-sozlamalari` emas): boshqa
 * «sozlamalar» sahifasi yo'q — shaxsiy sozlamalar oyna bo'lib ochiladi
 * va uning manzili umuman yo'q.
 *
 * Sahifaning o'zi ruxsatsiz odamni ham qabul qiladi (marshrutda
 * `beforeLoad` yo'q) — `foydalanuvchilar.tsx` dagi bilan bir xil naqsh:
 * server `GET /api/settings` ni ham `settings.manage` bilan himoyalagan,
 * ya'ni ruxsatsiz so'rov baribir 403 qaytaradi. Menyudan bu band
 * ko'rinmaydi (`nav-items.ts` dagi `permission`), lekin to'g'ridan-to'g'ri
 * manzil yozilsa sahifa tushunarli holat ko'rsatadi.
 */
export const Route = createFileRoute('/_app/sozlamalar')({
  component: SystemSettingsPage,
})
