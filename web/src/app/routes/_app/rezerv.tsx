import { createFileRoute } from '@tanstack/react-router'

import { FlagListPage } from '@/features/flags/components/flag-list-page'

/**
 * Rezerv — hozir olinmagan, lekin kerak bo'lsa chaqirsa bo'ladigan odamlar.
 *
 * Ro'yxatning o'zi `FlagListPage` da: uchala teg sahifasi bir xil ishlaydi
 * va faqat turi bilan farq qiladi.
 */
export const Route = createFileRoute('/_app/rezerv')({
  component: () => <FlagListPage type="reserve" />,
})
