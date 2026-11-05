import { Users } from 'lucide-react'
import { describe, expect, it } from 'vitest'

import { NAV, navFor, visibleItem, type NavItem } from './nav-items'

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

describe('navFor', () => {
  it('returns the very same menu for a normal user', () => {
    expect(navFor(NAV, false)).toBe(NAV)
  })

  it('leaves a scoped user only the attendance group, with no empty sections', () => {
    expect(keysOf(true)).toEqual(['verification'])
    expect(navFor(NAV, true).every((section) => section.items.length > 0)).toBe(true)
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
