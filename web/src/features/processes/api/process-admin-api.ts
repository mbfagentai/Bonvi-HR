/**
 * «Mening ishlarim» administration: gate overrides (SPEC §8.4, §10 #14–#15, merged) and the settings page's calendar,
 * stage bindings and diagnostics (§10 #16–#18, part of the P7 write API).
 *
 * Handwritten for the same reason as `processes-api.ts`. The types mirror `ProcessGateOverrideUseCases.cs` and
 * `ProcessSettingsUseCases.cs`.
 */
import { api } from '@/shared/api/http-client'

/**
 * Whether the server has #16–#18. While `false` the settings page sends none of those requests and draws their controls
 * disabled. Turn it on in the commit that merges P7; nothing else needs to change.
 */
export const PROCESS_SETTINGS_API_READY: boolean = false

export type ProcessHolidayKind = 'Holiday' | 'WorkingDay'

export const PROCESS_HOLIDAY_KINDS = ['Holiday', 'WorkingDay'] as const satisfies readonly ProcessHolidayKind[]

export interface ProcessHoliday {
  /** `DateOnly`: «2026-03-21». The date is the row's identity. */
  date: string
  kind: ProcessHolidayKind
  title: string
}

/** `ProcessStageBinding.Codes`, in the order the settings page shows them (SPEC §6.5). */
export const PROCESS_BINDING_CODES = ['contacted', 'invited', 'council', 'practicalTest'] as const
export type ProcessBindingCode = (typeof PROCESS_BINDING_CODES)[number]

export interface ProcessStageBindingRow {
  bindingCode: string
  /** `null` is unbound: the stage fact stays false and the step closes by its manual fallback only. */
  pipelineStageId: string | null
  pipelineStageName: string | null
}

export interface ProcessStageBindingUpdate {
  bindingCode: string
  pipelineStageId: string | null
}

export interface ProcessReconcileResult {
  status: 'Finished' | 'Busy' | 'Dormant'
  started: number
  changedRows: number
}

/** #14 — a 24-hour, single-use exception to a blocking gate of a case. */
export async function grantCaseGateOverride(input: {
  caseId: string
  gateCode: string
  reason: string
}): Promise<void> {
  await api.post(
    `/api/processes/cases/${input.caseId}/gates/${encodeURIComponent(input.gateCode)}/override`,
    { reason: input.reason },
  )
}

/** #15 — lets one named operator make one hire without a candidate (gate Q01). */
export async function grantDirectHireOverride(input: {
  forUserId: string
  reason: string
}): Promise<void> {
  await api.post('/api/processes/gate-overrides/direct-hire', input)
}

/** #16 */
export async function fetchProcessStageBindings(signal?: AbortSignal): Promise<ProcessStageBindingRow[]> {
  const { data } = await api.get<ProcessStageBindingRow[]>('/api/processes/settings/stage-bindings', {
    signal,
  })
  return data
}

/** #16 — only the rows sent change. */
export async function saveProcessStageBindings(bindings: ProcessStageBindingUpdate[]): Promise<void> {
  await api.put('/api/processes/settings/stage-bindings', { bindings })
}

/** #17 — every calendar exception. */
export async function fetchProcessHolidays(signal?: AbortSignal): Promise<ProcessHoliday[]> {
  const { data } = await api.get<ProcessHoliday[]>('/api/processes/holidays', { signal })
  return data
}

/** #17 — adds the date, or replaces the kind and title of a date that already has a row. */
export async function saveProcessHoliday(holiday: ProcessHoliday): Promise<void> {
  await api.post('/api/processes/holidays', holiday)
}

/** #17 */
export async function deleteProcessHoliday(date: string): Promise<void> {
  await api.delete(`/api/processes/holidays/${date}`)
}

/** #18 — runs an engine pass now (diagnostics). */
export async function runProcessReconcile(): Promise<ProcessReconcileResult> {
  const { data } = await api.post<ProcessReconcileResult>('/api/processes/reconcile')
  return data
}
