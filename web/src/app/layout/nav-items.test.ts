import { describe, expect, it } from 'vitest'

import { NAV, visibleItem } from './nav-items'

/** The menu a viewer with exactly these permissions sees: what `sidebar.tsx` draws, without empty sections. */
function menuFor(permissions: readonly string[]) {
  const hasPermission = (code: string) => permissions.includes(code)

  return NAV.map((section) =>
    section.items.flatMap((item) => visibleItem(item, hasPermission)).map((item) => item.key),
  ).filter((keys) => keys.length > 0)
}

const HR_MANAGER = ['employees.view', 'recruiting.view', 'reports.view', 'attendance.view']

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
})
