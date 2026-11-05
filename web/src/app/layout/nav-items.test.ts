import { Users } from 'lucide-react'
import { describe, expect, it } from 'vitest'

import { NAV, navFor, visibleItem, type NavItem } from './nav-items'

/** The menu a viewer with exactly these permissions sees: what `sidebar.tsx` draws, without empty sections. */
function menuFor(permissions: readonly string[], attendanceScoped = false) {
  const hasPermission = (code: string) => permissions.includes(code)

  return navFor(NAV, attendanceScoped)
    .map((section) =>
      section.items.flatMap((item) => visibleItem(item, hasPermission)).map((item) => item.key),
    )
    .filter((keys) => keys.length > 0)
}

const HR_MANAGER = ['employees.view', 'recruiting.view', 'reports.view', 'attendance.view']

const keysOf = (attendanceScoped: boolean) =>
  navFor(NAV, attendanceScoped).flatMap((section) => section.items.map((item) => item.key))

const only =
  (...codes: string[]) =>
  (code: string) =>
    codes.includes(code)

const verification = (): NavItem => {
  const item = NAV.flatMap((section) => section.items).find((row) => row.key === 'verification')
  if (item === undefined) throw new Error('The verification menu item is gone')
  return item
}

describe('myWork menu item', () => {
  it('is the first item of the first section, alone there', () => {
    expect(NAV[0]?.items.map((item) => item.key)).toEqual(['myWork'])
  })

  it('points at the page, guarded by processes.view, with the danger badge tone', () => {
    expect(NAV[0]?.items[0]).toMatchObject({
      to: '/mening-ishlarim',
      permission: 'processes.view',
      countTone: 'danger',
    })
  })

  it('leads the menu for a viewer with processes.view', () => {
    expect(menuFor([...HR_MANAGER, 'processes.view'])[0]).toEqual(['myWork'])
  })

  it('and its whole section are absent without processes.view', () => {
    const menu = menuFor(HR_MANAGER)

    expect(menu.flat()).not.toContain('myWork')
    expect(menu[0]).toEqual(['dashboard', 'reports'])
  })

  it('changes nothing else in the menu', () => {
    const without = menuFor(HR_MANAGER)
    const withView = menuFor([...HR_MANAGER, 'processes.view'])

    expect(withView.slice(1)).toEqual(without)
  })

  it('is hidden from someone with no permissions at all', () => {
    expect(menuFor([]).flat()).not.toContain('myWork')
  })

  it('is hidden from a scoped user even with processes.view: the scope wins', () => {
    const menu = menuFor(['attendance.view', 'attendance.scoped', 'processes.view', 'processes.work'], true)

    expect(menu).toEqual([['verification']])
  })
})

describe('navFor', () => {
  it('returns the very same menu for a normal user', () => {
    expect(navFor(NAV, false)).toBe(NAV)
  })

  it('leaves a scoped user only the attendance group, with no empty sections', () => {
    expect(keysOf(true)).toEqual(['verification'])
    expect(navFor(NAV, true).every((section) => section.items.length > 0)).toBe(true)
  })

  it('hides work sites from a scoped user, whose permissions alone would show them', () => {
    const children = (attendanceScoped: boolean) =>
      navFor(NAV, attendanceScoped)
        .flatMap((section) => section.items)
        .find((row) => row.key === 'verification')
        ?.children?.map((child) => child.key)

    expect(children(true)).toBeDefined()
    expect(children(true)).not.toContain('attendanceSites')
    expect(children(false)).toContain('attendanceSites')
  })
})

describe('visibleItem', () => {
  it('shows a scoped user only the attendance pages their permissions open', () => {
    const [item] = visibleItem(
      verification(),
      only('attendance.view', 'attendance.requests.manage', 'reports.export', 'attendance.scoped'),
    )
    const children = (item?.children ?? []).map((child) => child.to)

    expect(children).toContain('/verifikatsiya/jonli')
    expect(children).toContain('/verifikatsiya/tabel')
    expect(children).toContain('/verifikatsiya/otishlar')
    expect(children).not.toContain('/verifikatsiya/qurilmalar')
  })

  it('shows a manager more of the same group than a scoped user', () => {
    const count = (hasPermission: (code: string) => boolean) =>
      visibleItem(verification(), hasPermission)[0]?.children?.length ?? 0

    expect(count(only('attendance.view', 'attendance.manage'))).toBeGreaterThan(
      count(only('attendance.view')),
    )
  })

  it('removes a group whose children are all closed', () => {
    expect(visibleItem(verification(), only())).toEqual([])
  })

  it('removes an item whose own permission is missing', () => {
    const item: NavItem = { key: 'employees', to: '/xodimlar', icon: Users, permission: 'employees.view' }

    expect(visibleItem(item, only())).toEqual([])
    expect(visibleItem(item, only('employees.view'))).toEqual([item])
  })
})
