import { createFileRoute } from '@tanstack/react-router'

import { FlagListPage } from '@/features/flags/components/flag-list-page'

/** Nazoratdagilar — bir muddat kuzatiladigan xodimlar. */
export const Route = createFileRoute('/_app/nazorat')({
  component: () => <FlagListPage type="monitoring" />,
})
