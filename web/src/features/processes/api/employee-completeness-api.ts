/**
 * «Hujjatlari chala» read API. Handwritten for the reason `processes-api.ts` gives; the types mirror
 * `Bonvi.Application/Employees/Completeness/EmployeeCompleteness.cs` field by field.
 */
import { api } from '@/shared/api/http-client'

export interface EmployeeCompletenessItem {
  employeeId: string
  fullName: string
  department: string | null
  position: string | null
  hireDate: string
  hasPhoto: boolean
  /** Kind codes: a fixed one, or `doc:{document type id}`. */
  missing: string[]
  done: number
  total: number
}

export interface EmployeeCompletenessPage {
  items: EmployeeCompletenessItem[]
  page: number
  pageSize: number
  total: number
  pageCount: number
}

export interface EmployeeCompletenessKind {
  code: string
  /** The catalogue's name of a document kind; the client words the fixed kinds itself. */
  name: string | null
  count: number
}

export interface EmployeeCompletenessSummary {
  employees: number
  withGaps: number
  kinds: EmployeeCompletenessKind[]
}

export interface EmployeeCompletenessFilters {
  search?: string
  departmentId?: string
}

export interface EmployeeCompletenessListFilters extends EmployeeCompletenessFilters {
  missing?: string
  page: number
  pageSize: number
}

export async function fetchEmployeeCompleteness(
  filters: EmployeeCompletenessListFilters,
  signal?: AbortSignal,
): Promise<EmployeeCompletenessPage> {
  const { data } = await api.get<EmployeeCompletenessPage>('/api/employees/completeness', {
    params: filters,
    signal,
  })
  return data
}

export async function fetchEmployeeCompletenessSummary(
  filters: EmployeeCompletenessFilters,
  signal?: AbortSignal,
): Promise<EmployeeCompletenessSummary> {
  const { data } = await api.get<EmployeeCompletenessSummary>(
    '/api/employees/completeness/summary',
    { params: filters, signal },
  )
  return data
}
