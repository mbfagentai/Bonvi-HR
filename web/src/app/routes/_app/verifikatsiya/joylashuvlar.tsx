import { createFileRoute } from '@tanstack/react-router'

import { WorkSitesPage } from '@/features/verification/work-sites/components/work-sites-page'

/**
 * Work sites — where a phone check-in is accepted (SPEC-joylashuvlar §D).
 *
 * Read — `attendance.view`, write — `attendance.manage`. No `beforeLoad`
 * guard (the `hodisalar.tsx` pattern used by every verification page): the
 * page renders its own «no access» state and the server guards each endpoint.
 */
export const Route = createFileRoute('/_app/verifikatsiya/joylashuvlar')({
  /** Full width — like every other verification page (`app-shell.tsx`). */
  staticData: { fullWidth: true },

  component: WorkSitesPage,
})
