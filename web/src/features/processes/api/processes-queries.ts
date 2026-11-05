import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  completeProcessStep,
  fetchMyWork,
  fetchMyWorkCount,
  fetchProcessBoard,
  fetchProcessCase,
  fetchProcessCases,
  fetchProcessEngineStatus,
  fetchProcessLate,
  fetchProcessTemplates,
  skipProcessStep,
  startProcessCase,
  undoProcessStep,
  type ProcessCaseFilters,
  type ProcessCaseStartInput,
  type ProcessStepCompleteInput,
  type ProcessStepSkipInput,
  type ProcessStepUndoInput,
} from './processes-api'

/**
 * Every key starts with `processes`, so a later mutation (P7 endpoints) refreshes the whole module with
 * `invalidateQueries({ queryKey: processKeys.all })`: one finished step moves the badge, the board and the late list.
 */
export const processKeys = {
  all: ['processes'] as const,
  my: () => [...processKeys.all, 'my'] as const,
  myCount: () => [...processKeys.all, 'my', 'count'] as const,
  cases: (filters: ProcessCaseFilters) => [...processKeys.all, 'cases', filters] as const,
  board: (processCode: string) => [...processKeys.all, 'board', processCode] as const,
  case: (caseId: string) => [...processKeys.all, 'case', caseId] as const,
  late: () => [...processKeys.all, 'late'] as const,
  templates: () => [...processKeys.all, 'templates'] as const,
  status: () => [...processKeys.all, 'status'] as const,
}

/** The sidebar sits on every screen, so the badge polls slowly, and only for someone with `processes.view`. */
const MY_COUNT_REFRESH_MS = 60_000

/** The definitions ship with a release, so they do not go stale during a session. */
const TEMPLATES_STALE_MS = 10 * 60_000

export function useMyWork(enabled = true) {
  return useQuery({
    queryKey: processKeys.my(),
    queryFn: ({ signal }) => fetchMyWork(signal),
    enabled,
  })
}

/** Sidebar badge. `enabled` is the viewer's `processes.view`: without it the server answers 403 on every poll. */
export function useMyWorkCount(enabled: boolean) {
  return useQuery({
    queryKey: processKeys.myCount(),
    queryFn: ({ signal }) => fetchMyWorkCount(signal),
    enabled,
    staleTime: MY_COUNT_REFRESH_MS,
    refetchInterval: MY_COUNT_REFRESH_MS,
  })
}

export function useProcessCases(filters: ProcessCaseFilters, enabled = true) {
  return useQuery({
    queryKey: processKeys.cases(filters),
    queryFn: ({ signal }) => fetchProcessCases(filters, signal),
    enabled,
    placeholderData: keepPreviousData,
  })
}

export function useProcessBoard(processCode: string | undefined) {
  return useQuery({
    queryKey: processKeys.board(processCode ?? ''),
    queryFn: ({ signal }) => fetchProcessBoard(processCode ?? '', signal),
    enabled: processCode !== undefined && processCode !== '',
  })
}

export function useProcessCase(caseId: string, enabled = true) {
  return useQuery({
    queryKey: processKeys.case(caseId),
    queryFn: ({ signal }) => fetchProcessCase(caseId, signal),
    enabled,
  })
}

export function useProcessLate(enabled = true) {
  return useQuery({
    queryKey: processKeys.late(),
    queryFn: ({ signal }) => fetchProcessLate(signal),
    enabled,
  })
}

export function useProcessTemplates(enabled = true) {
  return useQuery({
    queryKey: processKeys.templates(),
    queryFn: ({ signal }) => fetchProcessTemplates(signal),
    enabled,
    staleTime: TEMPLATES_STALE_MS,
  })
}

export function useProcessEngineStatus(enabled = true) {
  return useQuery({
    queryKey: processKeys.status(),
    queryFn: ({ signal }) => fetchProcessEngineStatus(signal),
    enabled,
  })
}

/*
 * Writes. `providers.tsx` refreshes the query cache after every successful mutation, so these name no keys: one finished
 * step moves the badge, the board and the late list by itself.
 */

interface ProcessStepTarget {
  caseId: string
  stepCode: string
}

export function useCompleteProcessStep() {
  return useMutation({
    mutationFn: ({ caseId, stepCode, body }: ProcessStepTarget & { body: ProcessStepCompleteInput }) =>
      completeProcessStep(caseId, stepCode, body),
  })
}

export function useSkipProcessStep() {
  return useMutation({
    mutationFn: ({ caseId, stepCode, body }: ProcessStepTarget & { body: ProcessStepSkipInput }) =>
      skipProcessStep(caseId, stepCode, body),
  })
}

export function useUndoProcessStep() {
  return useMutation({
    mutationFn: ({ caseId, stepCode, body }: ProcessStepTarget & { body: ProcessStepUndoInput }) =>
      undoProcessStep(caseId, stepCode, body),
  })
}

export function useStartProcessCase() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: ProcessCaseStartInput) => startProcessCase(body),
    // Not awaited: the new case opens straight away instead of waiting for the board it is leaving.
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: processKeys.all })
    },
  })
}
