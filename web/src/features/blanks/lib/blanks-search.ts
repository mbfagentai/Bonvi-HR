import { BLANK_STATUSES, type BlankListFilters } from '../api/blanks-api'
import type { BlankSubmissionStatus } from './definition-types'

/**
 * «Blankalar» list state — in the address bar (`vacancy-requests-search.ts` pattern:
 * «Orqaga» returns to the same filter). Every key is optional and defaults are not written.
 */
export interface BlanksSearch {
  page?: number
  q?: string
  template?: string
  status?: BlankSubmissionStatus
  department?: string
  employee?: string
  from?: string
  to?: string
  waiting?: boolean
  incomplete?: boolean
  overdue?: boolean
}

export function validateBlanksSearch(search: Record<string, unknown>): BlanksSearch {
  return {
    page: readPage(search['page']),
    q: readText(search['q']),
    template: readText(search['template']),
    status: readStatus(search['status']),
    department: readText(search['department']),
    employee: readText(search['employee']),
    from: readDate(search['from']),
    to: readDate(search['to']),
    waiting: readFlag(search['waiting']),
    incomplete: readFlag(search['incomplete']),
    overdue: readFlag(search['overdue']),
  }
}

export function searchToListFilters(search: BlanksSearch): BlankListFilters {
  return {
    search: search.q,
    templateCode: search.template,
    status: search.status,
    departmentId: search.department,
    employeeId: search.employee,
    from: search.from,
    to: search.to,
    waitingForMe: search.waiting,
    incomplete: search.incomplete,
    overdue: search.overdue,
  }
}

export function isBlanksFiltered(search: BlanksSearch): boolean {
  const { page: _page, ...rest } = search
  return Object.values(rest).some((value) => value !== undefined)
}

/* ─────────────────────────────────────────────────────────── reading */

function readStatus(value: unknown): BlankSubmissionStatus | undefined {
  return typeof value === 'string' && (BLANK_STATUSES as readonly string[]).includes(value)
    ? (value as BlankSubmissionStatus)
    : undefined
}

function readPage(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isInteger(value) && value >= 2 ? value : undefined
}

function readText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined
}

function readFlag(value: unknown): boolean | undefined {
  return value === true || value === 'true' ? true : undefined
}

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/u

function readDate(value: unknown): string | undefined {
  return typeof value === 'string' && DATE_ONLY.test(value) ? value : undefined
}
