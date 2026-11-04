import { createFileRoute } from '@tanstack/react-router'

import { useBlankSubmission } from '@/features/blanks/api/blanks-queries'
import { BlankFillPage } from '@/features/blanks/components/blank-fill-page'
import { BlankViewPage } from '@/features/blanks/components/blank-view-page'

/**
 * One blank: the fill form for a Draft the caller may edit (`can.edit`), the view page
 * (steps, signing, PDF) for everything else. Both pages share the detail query — this
 * choice costs no extra request; while it loads the view page draws the skeleton / error.
 */
export const Route = createFileRoute('/_app/blankalar/$submissionId')({
  component: BlankSubmissionRoute,
})

function BlankSubmissionRoute() {
  const { submissionId } = Route.useParams()
  const detail = useBlankSubmission(submissionId)
  const fill = detail.data !== undefined && detail.data.status === 'Draft' && detail.data.can.edit
  // Keyed: opening another blank starts from a clean working copy
  return fill ? (
    <BlankFillPage key={submissionId} submissionId={submissionId} />
  ) : (
    <BlankViewPage key={submissionId} submissionId={submissionId} />
  )
}
