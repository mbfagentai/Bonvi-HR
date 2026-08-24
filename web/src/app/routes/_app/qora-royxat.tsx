import { createFileRoute } from '@tanstack/react-router'

import { FlagListPage } from '@/features/flags/components/flag-list-page'

/**
 * Qora ro'yxat — qayta ishga olinmaydiganlar.
 *
 * HR aniq so'ragan qoida shu ro'yxatga tayanadi: bo'shatishda va qayta
 * ishga olishda tizim ogohlantiradi.
 */
export const Route = createFileRoute('/_app/qora-royxat')({
  component: () => <FlagListPage type="blacklist" />,
})
