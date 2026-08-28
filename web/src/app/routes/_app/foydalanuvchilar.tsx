import { createFileRoute } from '@tanstack/react-router'

import { UsersPage } from '@/features/users/components/users-page'
import { validateListSearch } from '@/shared/lib/list-search'

/**
 * Foydalanuvchilarni boshqarish — faqat administrator.
 *
 * Sahifaning o'zi ruxsatsiz odamni ham qabul qiladi (marshrutda
 * `beforeLoad` yo'q): `UsersPage` buni ichkarida ko'rsatadi — chunki
 * server `GET /api/users` ni ham `users.manage` bilan himoyalagan,
 * ruxsatsiz so'rov baribir 403 qaytaradi. Menyudan bu band HR menejeriga
 * umuman ko'rinmaydi (`nav-items.ts` dagi `permission` maydoni), lekin
 * to'g'ridan-to'g'ri manzil yozilsa ham sahifa tushunarli holat ko'rsatadi.
 */
export const Route = createFileRoute('/_app/foydalanuvchilar')({
  /** Qidiruv matni — manzil satrida (loyihaning qat'iy konvensiyasi). */
  validateSearch: validateListSearch,

  component: UsersPage,
})
