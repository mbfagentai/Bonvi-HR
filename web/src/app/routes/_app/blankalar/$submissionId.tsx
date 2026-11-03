import { createFileRoute } from '@tanstack/react-router'

import { BlankFillPage } from '@/features/blanks/components/blank-fill-page'

/**
 * One blank: the fill form for an editable draft, read-only otherwise.
 * TODO(S12): choose `<BlankViewPage>` (steps, signing, PDF) for non-Draft statuses.
 */
export const Route = createFileRoute('/_app/blankalar/$submissionId')({
  component: BlankSubmissionRoute,
})

function BlankSubmissionRoute() {
  const { submissionId } = Route.useParams()
  // Keyed: opening another blank starts from a clean working copy
  return <BlankFillPage key={submissionId} submissionId={submissionId} />
}
