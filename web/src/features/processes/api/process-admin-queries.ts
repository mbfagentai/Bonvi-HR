import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  deleteProcessHoliday,
  fetchProcessHolidays,
  fetchProcessStageBindings,
  grantCaseGateOverride,
  grantDirectHireOverride,
  runProcessReconcile,
  saveProcessHoliday,
  saveProcessStageBindings,
} from './process-admin-api'
import { processKeys } from './processes-queries'

const adminKeys = {
  stageBindings: () => [...processKeys.all, 'stageBindings'] as const,
  holidays: () => [...processKeys.all, 'holidays'] as const,
}

/** Any of these can move a due date or a step's state, so the whole module refreshes (see `processKeys`). */
function useRefreshProcesses() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: processKeys.all })
}

export function useGrantCaseGateOverride() {
  const refresh = useRefreshProcesses()
  return useMutation({ mutationFn: grantCaseGateOverride, onSuccess: refresh })
}

export function useGrantDirectHireOverride() {
  const refresh = useRefreshProcesses()
  return useMutation({ mutationFn: grantDirectHireOverride, onSuccess: refresh })
}

export function useProcessStageBindings() {
  return useQuery({
    queryKey: adminKeys.stageBindings(),
    queryFn: ({ signal }) => fetchProcessStageBindings(signal),
  })
}

export function useSaveProcessStageBindings() {
  const refresh = useRefreshProcesses()
  return useMutation({ mutationFn: saveProcessStageBindings, onSuccess: refresh })
}

export function useProcessHolidays() {
  return useQuery({
    queryKey: adminKeys.holidays(),
    queryFn: ({ signal }) => fetchProcessHolidays(signal),
  })
}

export function useSaveProcessHoliday() {
  const refresh = useRefreshProcesses()
  return useMutation({ mutationFn: saveProcessHoliday, onSuccess: refresh })
}

export function useDeleteProcessHoliday() {
  const refresh = useRefreshProcesses()
  return useMutation({ mutationFn: deleteProcessHoliday, onSuccess: refresh })
}

export function useRunProcessReconcile() {
  const refresh = useRefreshProcesses()
  return useMutation({ mutationFn: runProcessReconcile, onSuccess: refresh })
}
