import type { AuthUser } from '../api/auth-api'
import { parseAuthUser } from '../schemas/auth-user-schema'

/**
 * «Davomat mas'uli» — factory staff responsible for attendance only.
 *
 * The role carries the marker permission below. The server is deny-by-default for such a user
 * outside `/api/attendance/**`, `/api/auth/**` and the current-user routes, so the client must
 * not lead them anywhere else: every 403 would be a dead end on screen.
 *
 * This file is the ONLY definition of «scoped user». Pages read `useAuth().isAttendanceScoped`;
 * the route guard, which runs outside React, calls the functions here.
 */
export const ATTENDANCE_SCOPED = 'attendance.scoped'

/** Where a scoped user lands: `/` is the HR dashboard, which they cannot load. */
export const ATTENDANCE_SCOPED_HOME = '/verifikatsiya/jonli'

/** Not imported from `features/verification`: the auth layer must not depend on a feature. */
const ATTENDANCE_MANAGE = 'attendance.manage'
const SUPER_ADMIN_ROLE = 'super_admin'

/**
 * The marker alone is not enough: `attendance.manage` or super admin means a full account that
 * happens to have the marker (the permission catalog can hand a super admin every code).
 */
export function isAttendanceScoped(
  user: Pick<AuthUser, 'roles' | 'permissions'> | null | undefined,
): boolean {
  if (user === null || user === undefined) return false

  return (
    user.permissions.includes(ATTENDANCE_SCOPED) &&
    !user.permissions.includes(ATTENDANCE_MANAGE) &&
    !user.roles.includes(SUPER_ADMIN_ROLE)
  )
}

/** `/verifikatsiya` itself has no route (SPEC §8.1), so only what is under it counts. */
function isInsideAttendance(pathname: string): boolean {
  return pathname.startsWith('/verifikatsiya/')
}

/**
 * Where to send this user instead of `pathname`; `null` — they may stay.
 *
 * Takes `unknown` because the guard reads the browser's stored copy, which may come from an older
 * version. An unreadable copy returns `null`: the provider re-reads the user from the server and
 * the shell applies the same rule to the fresh answer.
 */
export function attendanceScopeRedirect(storedUser: unknown, pathname: string): string | null {
  return isAttendanceScoped(parseAuthUser(storedUser)) && !isInsideAttendance(pathname)
    ? ATTENDANCE_SCOPED_HOME
    : null
}
