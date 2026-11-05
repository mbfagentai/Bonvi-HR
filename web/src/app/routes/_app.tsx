import { createFileRoute, Outlet, useNavigate, useRouterState } from '@tanstack/react-router'
import { useEffect } from 'react'

import { appBeforeLoad } from '@/app/layout/app-guard'
import { AppShell } from '@/app/layout/app-shell'
import { attendanceScopeRedirect, useAuth } from '@/features/auth'

/**
 * Manzilsiz (pathless) qatlam: bu marshrutning URL'da izi yo'q,
 * lekin barcha ichki sahifalar ilova qobig'i ichida ochiladi.
 * Kirish sahifasi shu qobiqdan tashqarida.
 */
export const Route = createFileRoute('/_app')({
  beforeLoad: appBeforeLoad,

  component: AppLayout,
})

function AppLayout() {
  useAttendanceScopeRedirect()

  return (
    <AppShell>
      <Outlet />
    </AppShell>
  )
}

/**
 * The guard's rule again, for what the guard cannot see: it only runs on navigation and reads the
 * stored copy, so a role changed while the app is open (the provider re-reads the user every
 * minute) or a stale copy after a reload would leave the user on a page that only returns 403.
 */
function useAttendanceScopeRedirect() {
  const { user } = useAuth()
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const navigate = useNavigate()

  useEffect(() => {
    const target = attendanceScopeRedirect(user, pathname)

    if (target !== null) {
      void navigate({ href: target, replace: true })
    }
  }, [user, pathname, navigate])
}
