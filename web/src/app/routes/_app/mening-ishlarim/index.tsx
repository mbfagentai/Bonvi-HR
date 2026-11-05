import { createFileRoute } from '@tanstack/react-router'

import { ProcessesPage } from '@/features/processes/components/processes-page'
import { validateProcessesSearch } from '@/features/processes/lib/processes-search'

/**
 * «Mening ishlarim» (docs/SPEC-mening-ishlarim.md §11). No `beforeLoad`, the same pattern as `rollar.tsx`: a viewer
 * without `processes.view` gets the page's own clear state, and the server guards the data anyway.
 */
export const Route = createFileRoute('/_app/mening-ishlarim/')({
  validateSearch: validateProcessesSearch,
  component: ProcessesPage,
})
