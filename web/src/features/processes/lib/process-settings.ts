import type { ProcessHoliday } from '../api/process-admin-api'
import type { ProcessTemplate } from '../api/processes-api'

/** `SystemSettingKeys.ProcessesGatesMode` / `ProcessesCouncilWeekday`: written through the existing `/api/settings`. */
export const GATES_MODE_KEY = 'processes.gates.mode'
export const COUNCIL_WEEKDAY_KEY = 'processes.councilWeekday'

export const PROCESS_GATES_MODES = ['enforce', 'warn', 'off'] as const

/** The setting's range (1–5, SPEC §6.6): the council meets on a working day. */
export const COUNCIL_WEEKDAYS = [1, 2, 3, 4, 5] as const

/** Roles the engine fills in itself or per case; only the rest are assigned on the blanks' roles dialog. */
const NOT_ASSIGNED_ROLES: ReadonlySet<string> = new Set(['system', 'departmentHead', 'subjectEmployee'])

/** The office roles the published processes actually ask for, in the order they first appear. */
export function officeRolesOf(templates: readonly ProcessTemplate[]): string[] {
  const roles = new Set<string>()

  for (const template of templates) {
    for (const step of template.steps) {
      for (const role of [step.role, step.fallbackRole]) {
        if (role !== null && !NOT_ASSIGNED_ROLES.has(role)) roles.add(role)
      }
    }
  }

  return [...roles]
}

/** Calendar order: ISO dates sort as text. */
export function sortHolidays(holidays: readonly ProcessHoliday[]): ProcessHoliday[] {
  return [...holidays].sort((left, right) => left.date.localeCompare(right.date))
}
