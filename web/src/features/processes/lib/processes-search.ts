import { PROCESS_CASE_STATUSES, type ProcessCaseStatus } from '../api/processes-api'

/** The three tabs of `/mening-ishlarim` (SPEC §11.2–§11.4). */
export const PROCESS_TABS = ['my', 'board', 'late'] as const
export type ProcessTab = (typeof PROCESS_TABS)[number]

/** The «Jarayonlar» tab's address (SPEC §11.3): which process, which view, and the list's filters (§10 #3). */
export interface ProcessBoardSearch {
  /** Absent means the board, so the plain tab address stays clean. */
  view?: 'list'
  process?: string
  status?: ProcessCaseStatus
  department?: string
  /** Set only by a link from elsewhere: these two ids have no picker on this screen, `q` finds people by name. */
  employee?: string
  candidate?: string
  holderRole?: string
  late?: true
  from?: string
  to?: string
  q?: string
  page?: number
}

export interface ProcessesSearch extends ProcessBoardSearch {
  /** Absent means the first tab, so the plain menu address stays clean. */
  tab?: Exclude<ProcessTab, 'my'>
}

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/u

function readText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined
}

function readDate(value: unknown): string | undefined {
  return typeof value === 'string' && DATE_ONLY.test(value) ? value : undefined
}

function readStatus(value: unknown): ProcessCaseStatus | undefined {
  return PROCESS_CASE_STATUSES.find((candidate) => candidate === value)
}

function readPage(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isInteger(value) && value >= 2 ? value : undefined
}

/** A value that is absent stays absent as a key, so an address never fills with empty parameters. */
function validateBoardSearch(search: Record<string, unknown>): ProcessBoardSearch {
  const entries: ProcessBoardSearch = {
    view: search['view'] === 'list' ? 'list' : undefined,
    process: readText(search['process']),
    status: readStatus(search['status']),
    department: readText(search['department']),
    employee: readText(search['employee']),
    candidate: readText(search['candidate']),
    holderRole: readText(search['holderRole']),
    late: search['late'] === true ? true : undefined,
    from: readDate(search['from']),
    to: readDate(search['to']),
    q: readText(search['q']),
    page: readPage(search['page']),
  }

  return Object.fromEntries(
    Object.entries(entries).filter(([, value]) => value !== undefined),
  ) as ProcessBoardSearch
}

/** Never throws: a stale or mistyped `tab` opens the first tab, and only the board tab keeps its own parameters. */
export function validateProcessesSearch(search: Record<string, unknown>): ProcessesSearch {
  const tab = search['tab']
  if (tab === 'board') return { tab, ...validateBoardSearch(search) }
  return tab === 'late' ? { tab } : {}
}

/** Whether a filter of the list is in effect, for the empty state's wording and the «Tozalash» button. */
export function hasBoardListFilters(search: ProcessBoardSearch): boolean {
  return (
    search.status !== undefined ||
    search.department !== undefined ||
    search.employee !== undefined ||
    search.candidate !== undefined ||
    search.holderRole !== undefined ||
    search.late !== undefined ||
    search.from !== undefined ||
    search.to !== undefined ||
    search.q !== undefined
  )
}

/** The case page's address: which step the panel shows. Absent means the page's own choice. */
export interface ProcessCaseSearch {
  step?: string
}

export function validateProcessCaseSearch(search: Record<string, unknown>): ProcessCaseSearch {
  const step = readText(search['step'])
  return step === undefined ? {} : { step }
}
