import { describe, expect, it } from 'vitest'

import {
  ATTENDANCE_SCOPED,
  ATTENDANCE_SCOPED_HOME,
  attendanceScopeRedirect,
  isAttendanceScoped,
} from './attendance-scope'

const scoped = { roles: ['attendance_responsible'], permissions: [ATTENDANCE_SCOPED] }

const stored = (user: { roles: string[]; permissions: string[] }) => ({
  id: 'u1',
  login: '+998901234567',
  fullName: 'Test',
  ...user,
})

describe('isAttendanceScoped', () => {
  it('needs the marker', () => {
    expect(isAttendanceScoped(scoped)).toBe(true)
    expect(isAttendanceScoped({ roles: ['hr_manager'], permissions: ['attendance.view'] })).toBe(false)
  })

  it('is false for a full account that happens to hold the marker', () => {
    expect(
      isAttendanceScoped({ roles: ['main_boss'], permissions: [ATTENDANCE_SCOPED, 'attendance.manage'] }),
    ).toBe(false)
    expect(isAttendanceScoped({ roles: ['super_admin'], permissions: [ATTENDANCE_SCOPED] })).toBe(false)
  })

  it('is false without a user', () => {
    expect(isAttendanceScoped(null)).toBe(false)
    expect(isAttendanceScoped(undefined)).toBe(false)
  })
})

describe('attendanceScopeRedirect', () => {
  it('keeps a scoped user inside the attendance section', () => {
    expect(attendanceScopeRedirect(stored(scoped), '/verifikatsiya/tabel')).toBeNull()
  })

  it.each([
    '/',
    '/xodimlar',
    '/xodimlar/42',
    '/verifikatsiya',
    '/verifikatsiya-boshqa',
    '/verifikatsiya/joylashuvlar',
    '/verifikatsiya/joylashuvlar/',
  ])(
    'sends a scoped user home from %s',
    (pathname) => {
      expect(attendanceScopeRedirect(stored(scoped), pathname)).toBe(ATTENDANCE_SCOPED_HOME)
    },
  )

  it('never moves anyone else', () => {
    const normal = stored({ roles: ['hr_manager'], permissions: ['employees.view'] })

    expect(attendanceScopeRedirect(normal, '/xodimlar')).toBeNull()
    expect(attendanceScopeRedirect(normal, '/verifikatsiya/joylashuvlar')).toBeNull()
  })

  it('ignores a stored copy it cannot read', () => {
    expect(attendanceScopeRedirect(null, '/xodimlar')).toBeNull()
    expect(attendanceScopeRedirect({ roles: 'not-a-list' }, '/xodimlar')).toBeNull()
  })
})
