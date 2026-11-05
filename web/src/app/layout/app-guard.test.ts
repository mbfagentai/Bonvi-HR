import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  isRedirect,
} from '@tanstack/react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { appBeforeLoad } from './app-guard'

function fakeStorage(): Storage {
  const values = new Map<string, string>()
  return {
    get length() {
      return values.size
    },
    clear: () => values.clear(),
    getItem: (key) => values.get(key) ?? null,
    key: (index) => [...values.keys()][index] ?? null,
    removeItem: (key) => void values.delete(key),
    setItem: (key, value) => void values.set(key, value),
  }
}

const SCOPED_USER = {
  id: 'u1',
  login: '+998901234567',
  fullName: 'Davomat Mas’uli',
  roles: ['attendance_responsible'],
  permissions: ['attendance.view', 'attendance.requests.manage', 'attendance.scoped'],
}

const NORMAL_USER = {
  ...SCOPED_USER,
  roles: ['hr_manager'],
  permissions: ['attendance.view', 'attendance.manage', 'employees.view'],
}

function signIn(user: unknown): void {
  globalThis.localStorage.setItem('bonvi.access', 'token')
  globalThis.localStorage.setItem('bonvi.user', JSON.stringify(user))
}

/** What the guard threw for this location; `undefined` — it let the user through. */
function guardOutcome(pathname: string): unknown {
  try {
    appBeforeLoad({ location: { href: pathname, pathname } })
    return undefined
  } catch (thrown) {
    return thrown
  }
}

/** The real router around the guard: `redirect({ href, replace })` has to survive a real load. */
function routerAt(initialPath: string) {
  const rootRoute = createRootRoute()
  const appRoute = createRoute({
    getParentRoute: () => rootRoute,
    id: '_app',
    beforeLoad: appBeforeLoad,
  })
  const page = (path: string) => createRoute({ getParentRoute: () => appRoute, path })
  const loginRoute = createRoute({ getParentRoute: () => rootRoute, path: '/kirish' })

  return createRouter({
    routeTree: rootRoute.addChildren([
      loginRoute,
      appRoute.addChildren([
        page('/'),
        page('/xodimlar'),
        page('/xodimlar/$employeeId'),
        page('/verifikatsiya/jonli'),
        page('/verifikatsiya/tabel'),
      ]),
    ]),
    history: createMemoryHistory({ initialEntries: [initialPath] }),
    // The test runs without a DOM, and a «server» router records a redirect instead of following it
    isServer: false,
    origin: 'http://localhost',
  })
}

beforeEach(() => {
  vi.stubGlobal('localStorage', fakeStorage())
  vi.stubGlobal('sessionStorage', fakeStorage())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('appBeforeLoad', () => {
  it('sends a signed-out visitor to the login page, remembering the target', () => {
    const outcome = guardOutcome('/xodimlar')

    expect(isRedirect(outcome)).toBe(true)
    expect(outcome).toMatchObject({
      options: { to: '/kirish', search: { redirect: '/xodimlar' } },
    })
  })

  it('leaves a normal user alone everywhere', () => {
    signIn(NORMAL_USER)

    expect(guardOutcome('/')).toBeUndefined()
    expect(guardOutcome('/xodimlar')).toBeUndefined()
    expect(guardOutcome('/verifikatsiya/jonli')).toBeUndefined()
  })

  it('throws a same-app replace redirect for a scoped user outside attendance', () => {
    signIn(SCOPED_USER)

    for (const pathname of ['/', '/xodimlar', '/xodimlar/42', '/verifikatsiya']) {
      const outcome = guardOutcome(pathname)

      expect(isRedirect(outcome)).toBe(true)
      expect(outcome).toMatchObject({
        options: { href: '/verifikatsiya/jonli', replace: true },
      })
      // A full page reload here would throw away the session's in-memory state
      expect(outcome).not.toMatchObject({ options: { reloadDocument: true } })
    }
  })

  it('leaves a scoped user alone inside attendance', () => {
    signIn(SCOPED_USER)

    expect(guardOutcome('/verifikatsiya/jonli')).toBeUndefined()
    expect(guardOutcome('/verifikatsiya/tabel')).toBeUndefined()
  })

  it('does not act on an unreadable stored user: the shell re-checks the fresh one', () => {
    signIn({ id: 'from-an-older-version' })

    expect(guardOutcome('/xodimlar')).toBeUndefined()
  })
})

describe('appBeforeLoad in a real router', () => {
  it('lands a scoped user on the attendance home after a load', async () => {
    signIn(SCOPED_USER)
    const router = routerAt('/')

    await router.load()

    expect(router.state.location.pathname).toBe('/verifikatsiya/jonli')
  })

  it('replaces the history entry, so «back» does not bounce through the redirect', async () => {
    signIn(SCOPED_USER)
    const router = routerAt('/verifikatsiya/tabel')
    await router.load()
    const lengthBefore = router.history.length

    await router.navigate({ to: '/xodimlar' })

    expect(router.state.location.pathname).toBe('/verifikatsiya/jonli')
    // One new entry: the redirect replaced the blocked page instead of stacking on top of it
    expect(router.history.length).toBe(lengthBefore + 1)
  })

  it('catches a notification link to an employee card without rejecting', async () => {
    signIn(SCOPED_USER)
    const router = routerAt('/verifikatsiya/tabel')
    await router.load()

    await expect(
      router.navigate({ to: '/xodimlar/$employeeId', params: { employeeId: '42' } }),
    ).resolves.toBeUndefined()

    expect(router.state.location.pathname).toBe('/verifikatsiya/jonli')
  })

  it('keeps a normal user on the page they asked for', async () => {
    signIn(NORMAL_USER)
    const router = routerAt('/xodimlar')

    await router.load()

    expect(router.state.location.pathname).toBe('/xodimlar')
  })
})
