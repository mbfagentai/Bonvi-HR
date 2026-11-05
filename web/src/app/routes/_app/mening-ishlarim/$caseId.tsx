import { createFileRoute } from '@tanstack/react-router'

import { ProcessCasePage } from '@/features/processes/components/process-case-page'

/** One case (docs/SPEC-mening-ishlarim.md §11.5). */
export const Route = createFileRoute('/_app/mening-ishlarim/$caseId')({
  component: ProcessCasePage,
})
