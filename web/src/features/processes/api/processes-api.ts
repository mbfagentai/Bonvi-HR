/**
 * «Mening ishlarim» read API (docs/SPEC-mening-ishlarim.md §10 #1–#8).
 *
 * WHY handwritten: the module was built while the generated client could not be regenerated (`./bonvi gen` touches the
 * shared dev stack). The types mirror `Bonvi.Application/Processes/Queries/ProcessReadDtos.cs` field by field, so a
 * renamed server field is a change in this file too. Enums go out as their C# names.
 */
import { api } from '@/shared/api/http-client'

import type { AppNotification } from '@/features/notifications/api/notifications-api'

export type ProcessSubjectKind = 'VacancyRequest' | 'Candidate' | 'Employment' | 'None'
export type ProcessCaseStatus = 'Active' | 'Completed' | 'Cancelled'
export type ProcessStepState = 'Locked' | 'Active' | 'Waiting' | 'Blocked' | 'Done' | 'Skipped'
export type ProcessStepResolution = 'Fact' | 'Manual' | 'Auto' | 'Condition' | 'Legacy'
export type ProcessStepKind = 'Fact' | 'Manual' | 'Auto'
export type ProcessGateMode = 'Block' | 'Warn'
export type ProcessInputType = 'Text' | 'LongText' | 'Date' | 'WholeNumber' | 'Choice' | 'Checklist'

export const PROCESS_CASE_STATUSES = ['Active', 'Completed', 'Cancelled'] as const

/** Which link screens exist (`ProcessScreens.cs`, SPEC §11.6). */
export const PROCESS_SCREENS = [
  'vacancyRequests',
  'candidate',
  'candidateForm',
  'blank',
  'employee',
  'employment',
  'probations',
  'attendanceMapping',
  'attendanceSchedules',
  'external',
  'none',
] as const
export type ProcessScreen = (typeof PROCESS_SCREENS)[number]

export interface ProcessSubject {
  kind: ProcessSubjectKind
  name: string | null
  position: string | null
  vacancyRequestId: string | null
  candidateId: string | null
  employmentId: string | null
  probationId: string | null
  employeeId: string | null
}

export interface ProcessUserName {
  userId: string
  name: string
}

export interface ProcessHolder {
  /** `null` is the system itself («Tizim»). */
  role: string | null
  employeeId: string | null
  employeeName: string | null
  users: ProcessUserName[]
  unassigned: boolean
}

export interface ProcessLink {
  /** A `ProcessScreen`; typed wide because the server may add one before this file knows it. */
  screen: string
  args: Readonly<Record<string, string>>
}

export interface ProcessTask {
  caseId: string
  processCode: string
  processTitle: string
  stepCode: string
  stepTitle: string
  state: ProcessStepState
  /** `DateOnly`: «2026-11-24». */
  dueDate: string | null
  daysLate: number
  subject: ProcessSubject
  departmentId: string | null
  departmentName: string | null
  blankCode: string | null
  blankTitle: string | null
  linkedBlankSubmissionId: string | null
  legacy: boolean
  gateCode: string | null
  gateErrorCode: string | null
  holder: ProcessHolder
  resolver: ProcessHolder | null
  /** Warn-mode gate codes that currently fire (`Q02`, `Q05`, `Q06`, `AKT`). */
  warnings: string[]
  link: ProcessLink
}

export interface ProcessTartibCount {
  /** `unmatchedTerminalPersons`, `employeesWithoutPhoto`, `employeesWithoutSchedule`; zero is omitted. */
  key: string
  count: number
}

export interface ProcessTartib {
  notifications: AppNotification[]
  counts: ProcessTartibCount[]
}

export interface ProcessMyWork {
  today: string
  count: number
  lateCount: number
  urgent: ProcessTask[]
  later: ProcessTask[]
  held: ProcessTask[]
  waiting: ProcessTask[]
  tartib: ProcessTartib
}

export interface ProcessMyCount {
  count: number
}

export interface ProcessCurrentStep {
  stepCode: string
  title: string
  column: string
  state: ProcessStepState
  holder: ProcessHolder
  dueDate: string | null
  daysLate: number
  gateCode: string | null
  warnings: string[]
}

export interface ProcessCaseListItem {
  id: string
  processCode: string
  processTitle: string
  status: ProcessCaseStatus
  outcome: string | null
  legacy: boolean
  subject: ProcessSubject
  departmentId: string | null
  departmentName: string | null
  startedOn: string
  completedAt: string | null
  cancelledAt: string | null
  currentSteps: ProcessCurrentStep[]
  daysLate: number
}

export interface ProcessCasePage {
  items: ProcessCaseListItem[]
  page: number
  pageSize: number
  total: number
  pageCount: number
}

export interface ProcessBoardCard {
  caseId: string
  subject: ProcessSubject
  departmentId: string | null
  departmentName: string | null
  legacy: boolean
  currentSteps: ProcessCurrentStep[]
  dueDate: string | null
  daysLate: number
}

export interface ProcessBoardColumn {
  key: string
  title: string
  cards: ProcessBoardCard[]
}

export interface ProcessBoard {
  processCode: string
  processTitle: string
  columns: ProcessBoardColumn[]
}

export interface ProcessOption {
  value: string
  label: string
}

export interface ProcessRequiredWhen {
  input: string | null
  fact: string | null
  equalsValue: string
}

export interface ProcessManualInput {
  id: string
  label: string
  type: ProcessInputType
  required: boolean
  requiredWhen: ProcessRequiredWhen | null
  options: ProcessOption[]
  min: number | null
  max: number | null
}

export interface ProcessManual {
  allowed: boolean
  inputs: ProcessManualInput[]
}

/** The recorded inputs of a manual step; their shape belongs to the step's `manual.inputs`. */
export type ProcessStepPayload = Readonly<Record<string, unknown>>

export interface ProcessCaseStep {
  stepCode: string
  orderNo: number
  title: string
  column: string
  kind: ProcessStepKind | null
  state: ProcessStepState
  resolution: ProcessStepResolution | null
  holder: ProcessHolder
  dueDate: string | null
  daysLate: number
  doneAt: string | null
  doneBy: string | null
  doneByName: string | null
  payload: ProcessStepPayload | null
  blankCode: string | null
  blankTitle: string | null
  signature: string | null
  linkedBlankSubmissionId: string | null
  linkedRecordId: string | null
  gateCode: string | null
  gateErrorCode: string | null
  resolver: ProcessHolder | null
  warnings: string[]
  prereqs: string[]
  optional: boolean
  guide: string[]
  sourceRef: string | null
  link: ProcessLink
  manual: ProcessManual | null
  viewerHolds: boolean
  canComplete: boolean
  canSkip: boolean
  canUndo: boolean
}

export interface ProcessLinkedBlank {
  id: string
  templateCode: string
  templateTitle: string | null
  number: number | null
  status: string
  dueDate: string | null
  completedAt: string | null
}

export interface ProcessColumn {
  key: string
  title: string
}

export interface ProcessCaseDetail {
  id: string
  processCode: string
  processTitle: string
  source: string | null
  status: ProcessCaseStatus
  outcome: string | null
  legacy: boolean
  subject: ProcessSubject
  departmentId: string | null
  departmentName: string | null
  parentCaseId: string | null
  anchorDate: string | null
  startedOn: string
  completedAt: string | null
  cancelledAt: string | null
  cancelReason: string | null
  version: number
  columns: ProcessColumn[]
  steps: ProcessCaseStep[]
  blanks: ProcessLinkedBlank[]
}

export interface ProcessGateOverrideRow {
  id: string
  gateCode: string
  caseId: string | null
  stepCode: string | null
  employmentId: string | null
  candidateId: string | null
  forUserId: string | null
  forUserName: string | null
  reason: string
  grantedBy: string
  grantedByName: string | null
  grantedAt: string
  expiresAt: string
  consumedAt: string | null
  consumedOperation: string | null
}

export interface ProcessLate {
  blocked: ProcessTask[]
  late: ProcessTask[]
  warn: ProcessTask[]
  overrides: ProcessGateOverrideRow[]
}

export interface ProcessTemplateGate {
  code: string
  mode: ProcessGateMode
}

export interface ProcessTemplateStep {
  id: string
  title: string
  column: string
  role: string
  fallbackRole: string | null
  blank: string | null
  signature: string | null
  kind: ProcessStepKind
  prereqs: string[]
  optional: boolean
  skippable: boolean
  legacySkippable: boolean
  gates: ProcessTemplateGate[]
  manual: ProcessManual | null
  guide: string[]
  sourceRef: string | null
  link: ProcessLink
}

export interface ProcessTemplate {
  code: string
  version: string
  title: string
  source: string
  subjectKind: ProcessSubjectKind
  board: ProcessColumn[]
  steps: ProcessTemplateStep[]
}

export interface ProcessEngineStatus {
  engine: 'dormant' | 'active'
  startedOn: string | null
  gatesMode: 'enforce' | 'warn' | 'off'
  /** `DayOfWeek` as a number: 0 is Sunday. */
  councilWeekday: number
  lastPassAt: string | null
}

/** `GET /api/processes/cases` filters. An unset key is not sent at all. */
export interface ProcessCaseFilters {
  process?: string
  status?: ProcessCaseStatus
  department?: string
  employee?: string
  candidate?: string
  holderRole?: string
  late?: boolean
  from?: string
  to?: string
  q?: string
  page?: number
  pageSize?: number
}

/** #1 — the viewer's work; the server runs a throttled reconcile pass first. */
export async function fetchMyWork(signal?: AbortSignal): Promise<ProcessMyWork> {
  const { data } = await api.get<ProcessMyWork>('/api/processes/my', { signal })
  return data
}

/** #2 — the sidebar badge; runs no pass, so it is safe on every page. */
export async function fetchMyWorkCount(signal?: AbortSignal): Promise<number> {
  const { data } = await api.get<ProcessMyCount>('/api/processes/my/count', { signal })
  return data.count
}

/** #3 — the list view of the board. */
export async function fetchProcessCases(
  filters: ProcessCaseFilters,
  signal?: AbortSignal,
): Promise<ProcessCasePage> {
  const { data } = await api.get<ProcessCasePage>('/api/processes/cases', {
    params: filters,
    signal,
  })
  return data
}

/** #4 — one process's board (Active cases only). */
export async function fetchProcessBoard(
  processCode: string,
  signal?: AbortSignal,
): Promise<ProcessBoard> {
  const { data } = await api.get<ProcessBoard>('/api/processes/board', {
    params: { process: processCode },
    signal,
  })
  return data
}

/** #5 — one case with its steps, reconciled by the server first. */
export async function fetchProcessCase(
  caseId: string,
  signal?: AbortSignal,
): Promise<ProcessCaseDetail> {
  const { data } = await api.get<ProcessCaseDetail>(`/api/processes/cases/${caseId}`, { signal })
  return data
}

/** #6 — «Kim kechikdi». */
export async function fetchProcessLate(signal?: AbortSignal): Promise<ProcessLate> {
  const { data } = await api.get<ProcessLate>('/api/processes/late', { signal })
  return data
}

/** #7 — the definitions: process selector titles, board columns, guides. */
export async function fetchProcessTemplates(signal?: AbortSignal): Promise<ProcessTemplate[]> {
  const { data } = await api.get<ProcessTemplate[]>('/api/processes/templates', { signal })
  return data
}

/** #8 — engine state for the settings page. */
export async function fetchProcessEngineStatus(signal?: AbortSignal): Promise<ProcessEngineStatus> {
  const { data } = await api.get<ProcessEngineStatus>('/api/processes/status', { signal })
  return data
}
