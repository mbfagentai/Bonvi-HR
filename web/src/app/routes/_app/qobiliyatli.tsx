import { createFileRoute } from '@tanstack/react-router'

import { FlagListPage } from '@/features/flags/components/flag-list-page'

/**
 * Qobiliyatli xodimlar — qo'shimcha vazifa berish mumkin bo'lganlar.
 *
 * `qora-royxat.tsx` ning aynan nusxasi: to'rtta belgi ro'yxati bitta
 * `FlagListPage` komponentidan yasaladi, farqi faqat `type` bilan.
 */
export const Route = createFileRoute('/_app/qobiliyatli')({
  component: () => <FlagListPage type="capable" />,
})
