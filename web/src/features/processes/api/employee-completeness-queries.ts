import { keepPreviousData, useQuery } from '@tanstack/react-query'

import {
  fetchEmployeeCompleteness,
  fetchEmployeeCompletenessSummary,
  type EmployeeCompletenessFilters,
  type EmployeeCompletenessListFilters,
} from './employee-completeness-api'

const completenessKeys = {
  all: ['employee-completeness'] as const,
  list: (filters: EmployeeCompletenessListFilters) =>
    [...completenessKeys.all, 'list', filters] as const,
  summary: (filters: EmployeeCompletenessFilters) =>
    [...completenessKeys.all, 'summary', filters] as const,
}

/*
 * Always refetched on mount: HR leaves for the employee card, fills something in and comes back, and the card's
 * mutations know nothing of this list. A person who is now complete has to be gone from it.
 */

export function useEmployeeCompleteness(filters: EmployeeCompletenessListFilters) {
  return useQuery({
    queryKey: completenessKeys.list(filters),
    queryFn: ({ signal }) => fetchEmployeeCompleteness(filters, signal),
    placeholderData: keepPreviousData,
    refetchOnMount: 'always',
  })
}

export function useEmployeeCompletenessSummary(filters: EmployeeCompletenessFilters) {
  return useQuery({
    queryKey: completenessKeys.summary(filters),
    queryFn: ({ signal }) => fetchEmployeeCompletenessSummary(filters, signal),
    placeholderData: keepPreviousData,
    refetchOnMount: 'always',
  })
}
