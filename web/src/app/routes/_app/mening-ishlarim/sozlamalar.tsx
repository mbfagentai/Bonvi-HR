import { createFileRoute } from '@tanstack/react-router'

import { ProcessSettingsPage } from '@/features/processes/components/process-settings-page'

/** Engine settings (docs/SPEC-mening-ishlarim.md §11.7). A static segment outranks the sibling `$caseId`. */
export const Route = createFileRoute('/_app/mening-ishlarim/sozlamalar')({
  component: ProcessSettingsPage,
})
