import { createFileRoute } from '@tanstack/react-router'

import { CouncilMinutesPage } from '@/features/recruiting/components/council-minutes-page'

/** The hiring council's minutes, one page per council day. The chosen day stays in the address bar. */
export const Route = createFileRoute('/_app/kengash-xulosasi')({
  validateSearch: (search: Record<string, unknown>): { date?: string } =>
    typeof search.date === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(search.date) ? { date: search.date } : {},
  component: CouncilMinutesPage,
})
