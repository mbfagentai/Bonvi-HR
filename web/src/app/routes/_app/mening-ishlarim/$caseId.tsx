import { createFileRoute } from '@tanstack/react-router'

import { ProcessCasePage } from '@/features/processes/components/process-case-page'
import { validateProcessCaseSearch } from '@/features/processes/lib/processes-search'

/** One case (docs/SPEC-mening-ishlarim.md §11.5). `?step=` names the step the panel shows. */
export const Route = createFileRoute('/_app/mening-ishlarim/$caseId')({
  validateSearch: validateProcessCaseSearch,
  component: ProcessCasePage,
})
