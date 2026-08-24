import { createFileRoute } from '@tanstack/react-router'

import { PositionsPage } from '@/features/catalog'

export const Route = createFileRoute('/_app/malumotnomalar/lavozimlar')({
  component: PositionsPage,
})
