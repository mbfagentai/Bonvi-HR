import type { ProcessCaseFilters, ProcessTemplate } from '../api/processes-api'
import { hasBoardListFilters, type ProcessBoardSearch } from './processes-search'

/** The order HR meets the processes in (SPEC §3.2), not the alphabet. */
const PROCESS_ORDER = ['hire-request', 'hire-candidate', 'hire-formalise', 'probation', 'dismissal']

/** Who can hold a step: the keys of `processes.roles`, for the holder filter (SPEC §9.1). «Tizim» holds no one's work. */
export const PROCESS_HOLDER_ROLES = [
  'hr1',
  'hr2',
  'hr3',
  'hr4',
  'hr5',
  'hr8',
  'payrollAccountant',
  'chiefAccountant',
  'director',
  'lawyer',
  'safetyEngineer',
  'council',
  'departmentHead',
  'subjectEmployee',
] as const

function rank(code: string): number {
  const index = PROCESS_ORDER.indexOf(code)
  return index === -1 ? PROCESS_ORDER.length : index
}

/** A process this build does not list yet goes last, in the order the server gave. */
export function sortProcessTemplates(templates: readonly ProcessTemplate[]): ProcessTemplate[] {
  return [...templates].sort((a, b) => rank(a.code) - rank(b.code))
}

/** The address as the `/cases` query. `process` is passed apart: only a code the definitions know may be sent. */
export function boardSearchToCaseFilters(
  search: ProcessBoardSearch,
  process: string | undefined,
): ProcessCaseFilters {
  return {
    process,
    status: search.status,
    department: search.department,
    employee: search.employee,
    candidate: search.candidate,
    holderRole: search.holderRole,
    late: search.late,
    from: search.from,
    to: search.to,
    q: search.q,
  }
}

/** The address with every filter of the list removed; the process and the view stay. */
export function clearBoardListFilters<T extends ProcessBoardSearch>(search: T): T {
  return {
    ...search,
    status: undefined,
    department: undefined,
    employee: undefined,
    candidate: undefined,
    holderRole: undefined,
    late: undefined,
    from: undefined,
    to: undefined,
    q: undefined,
    page: undefined,
  }
}

/** How many filters sit behind the «Filtrlar» button: the search box and the status are always in view. */
export function hiddenBoardFilterCount(search: ProcessBoardSearch): number {
  if (!hasBoardListFilters(search)) return 0

  return [
    search.department,
    search.employee,
    search.candidate,
    search.holderRole,
    search.late,
    search.from ?? search.to,
  ].filter((value) => value !== undefined).length
}
