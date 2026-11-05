import { PROCESSES_WORK } from './permissions'

export const MY_WORK_PATH = '/mening-ishlarim'

/**
 * Where a login goes when no `?redirect=` was asked for (SPEC §11.1): someone with `processes.work` starts on their
 * tasks, everybody else on the dashboard as before. Takes only the permissions so `features/auth` can call it without
 * this feature depending back on auth.
 */
export function landingPath(user: { readonly permissions: readonly string[] }): string {
  return user.permissions.includes(PROCESSES_WORK) ? MY_WORK_PATH : '/'
}
