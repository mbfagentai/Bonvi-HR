import { createFileRoute } from '@tanstack/react-router'

import { DepartmentsPage } from '@/features/catalog'

export const Route = createFileRoute('/_app/malumotnomalar/bolimlar')({
  component: DepartmentsPage,
})
