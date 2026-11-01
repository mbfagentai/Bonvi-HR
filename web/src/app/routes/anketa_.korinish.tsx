import { createFileRoute, redirect } from '@tanstack/react-router'

import { KioskPreviewPage } from '@/features/kiosk'
import { authStorage } from '@/shared/lib/auth-storage'

/**
 * HR's preview of the constructor's draft (`docs/SPEC-anketa-konstruktori.md` §2.6, §7).
 *
 * Outside `_app` like `/anketa` — HR sees the form in the candidate's own
 * frame, without the HR shell — but signed-in only: the server asks for
 * `recruiting.view` on every call, this guard only sends a stranger to the
 * sign-in page first instead of an error screen.
 */
export const Route = createFileRoute('/anketa_/korinish')({
  beforeLoad: ({ location }) => {
    if (!authStorage.isAuthenticated) {
      throw redirect({ to: '/kirish', search: { redirect: location.href } })
    }
  },

  component: KioskPreviewPage,
})
