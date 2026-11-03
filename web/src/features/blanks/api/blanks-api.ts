/**
 * «Blankalar» — API layer (docs/SPEC-blankalar.md §7).
 *
 * Types come from the generated client (`./bonvi gen` → `@/shared/api/generated`);
 * this file holds only thin call wrappers and the narrowing of generated types
 * the UI needs (same contract as `features/verification/api/attendance-api.ts`):
 *   · `number | string` → `number` (the `| string` describes what the server
 *     ACCEPTS, it always SENDS numbers — see `AsSent` there);
 *   · `status: string` → the `BlankSubmissionStatus` union;
 *   · `JsonElement` → the definition / answers types of `lib/definition-types.ts`.
 *
 * TODO seams (endpoints built in parallel, not in the OpenAPI document yet):
 *   · steps #12–#16 — input types only (`BlankStepApproveInput`, `BlankStepCommentInput`);
 *     the next slice (S12) adds the calls after `./bonvi gen`.
 *   · files #17–#20 — hand-written wrappers on the SPEC contract below, marked
 *     `TODO(S7)`; replace with the generated functions once they exist.
 */
import {
  cancelBlankSubmission,
  createBlankSubmission,
  getBlankSubmission,
  getBlankTemplateVersion,
  listBlankRoles,
  listBlankSubmissions,
  listBlankTemplates,
  saveBlankAnswers,
  setBlankRoleUsers,
  setBlankTemplateActive,
  submitBlankSubmission,
} from '@/shared/api/generated/blanks/blanks'
import type {
  BlankCanDto,
  BlankCurrentStepDto,
  BlankRoleDto,
  BlankStepDto,
  BlankSubjectDto,
  BlankSubmissionDetailDto,
  BlankSubmissionListItemDto,
  BlankTemplateDto,
  ListBlankSubmissionsParams,
} from '@/shared/api/generated/model'
import { api } from '@/shared/api/http-client'

import type {
  Answers,
  BlankAttachmentDto,
  BlankDefinition,
  BlankStepState,
  BlankSubmissionStatus,
  BlankTemplateVersionDto,
  ComputedValues,
} from '../lib/definition-types'

export const BLANK_STATUSES = ['Draft', 'InApproval', 'Approved', 'Rejected', 'Cancelled'] as const satisfies readonly BlankSubmissionStatus[]
const STEP_STATES = ['Waiting', 'Open', 'Signed', 'Skipped', 'Rejected'] as const satisfies readonly BlankStepState[]

/** Office roles of §6.1 — the order of the roles dialog. */
export const BLANK_OFFICE_ROLES = [
  'hr1', 'hr2', 'hr3', 'hr4', 'hr5', 'hr8',
  'payrollAccountant', 'chiefAccountant', 'director', 'lawyer',
] as const

const BASE = '/api/blanks'

/* ═══════════════════════════════════════════════════════════════ types */

export type BlankTemplate = BlankTemplateDto
export type BlankRole = BlankRoleDto
export type BlankSubject = BlankSubjectDto
export type BlankCurrentStep = BlankCurrentStepDto
export type BlankCan = BlankCanDto

export interface BlankListItem
  extends Omit<BlankSubmissionListItemDto, 'number' | 'status' | 'missingCount'> {
  number: number | null
  status: BlankSubmissionStatus
  missingCount: number
}

export interface BlankStep extends Omit<BlankStepDto, 'orderNo' | 'state'> {
  orderNo: number
  state: BlankStepState
}

export interface BlankSubmission
  extends Omit<
    BlankSubmissionDetailDto,
    'number' | 'status' | 'version' | 'answers' | 'computed' | 'missingCount' | 'steps' | 'attachments'
  > {
  number: number | null
  status: BlankSubmissionStatus
  version: number
  answers: Answers
  computed: ComputedValues
  missingCount: number
  steps: BlankStep[]
  attachments: BlankAttachmentDto[]
}

export interface BlankPage {
  items: BlankListItem[]
  total: number
  page: number
  pageSize: number
}

/** List filters without paging — the same set the future Excel export takes (SPEC Phase 2). */
export interface BlankListFilters {
  templateCode?: string
  status?: BlankSubmissionStatus
  employeeId?: string
  departmentId?: string
  from?: string
  to?: string
  search?: string
  waitingForMe?: boolean
  incomplete?: boolean
  overdue?: boolean
}

export interface BlankListQuery extends BlankListFilters {
  page: number
  pageSize: number
}

export interface BlankCreateInput {
  templateCode: string
  employeeId: string | null
  candidateId: string | null
  /** Idempotency key — one per opened create dialog (same id → same draft). */
  clientRequestId: string
}

/** TODO(S6): body of #12 `steps/{stepCode}/approve` — calls land with the steps slice. */
export interface BlankStepApproveInput {
  expectedVersion: number
  decision?: string | null
  decisionExtra?: string | null
  comment?: string | null
  answers?: Answers | null
}

/** TODO(S6): body of #14 `return` and #15 `reject` (comment required). */
export interface BlankStepCommentInput {
  expectedVersion: number
  comment: string
}

/* ═══════════════════════════════════════════════════════════════ narrowing */

function toNumber(value: number | string): number {
  const result = typeof value === 'number' ? value : Number(value)
  return Number.isFinite(result) ? result : 0
}

function toStatus(value: string): BlankSubmissionStatus {
  // Unknown status → «InApproval»: read-only everywhere, never an editable draft.
  return (BLANK_STATUSES as readonly string[]).includes(value) ? (value as BlankSubmissionStatus) : 'InApproval'
}

function toStepState(value: string): BlankStepState {
  return (STEP_STATES as readonly string[]).includes(value) ? (value as BlankStepState) : 'Waiting'
}

function toRecord(value: unknown): Answers {
  return value !== null && typeof value === 'object' && !Array.isArray(value) ? (value as Answers) : {}
}

function toListItem(dto: BlankSubmissionListItemDto): BlankListItem {
  return {
    ...dto,
    number: dto.number === null ? null : toNumber(dto.number),
    status: toStatus(dto.status),
    missingCount: toNumber(dto.missingCount),
  }
}

function toSubmission(dto: BlankSubmissionDetailDto): BlankSubmission {
  return {
    ...dto,
    number: dto.number === null ? null : toNumber(dto.number),
    status: toStatus(dto.status),
    version: toNumber(dto.version),
    answers: toRecord(dto.answers),
    computed: toRecord(dto.computed),
    missingCount: toNumber(dto.missingCount),
    steps: dto.steps.map((step) => ({ ...step, orderNo: toNumber(step.orderNo), state: toStepState(step.state) })),
    attachments: dto.attachments.map((file) => ({ ...file, sizeBytes: toNumber(file.sizeBytes) })),
  }
}

/** Blank values are not sent — `?search=` would only pollute the cache key. */
export function blankListParams(filters: BlankListFilters): ListBlankSubmissionsParams {
  const result: ListBlankSubmissionsParams = {}
  if (filters.templateCode) result.templateCode = filters.templateCode
  if (filters.status) result.status = filters.status
  if (filters.employeeId) result.employeeId = filters.employeeId
  if (filters.departmentId) result.departmentId = filters.departmentId
  if (filters.from) result.from = filters.from
  if (filters.to) result.to = filters.to
  if (filters.search && filters.search.trim() !== '') result.search = filters.search.trim()
  if (filters.waitingForMe) result.waitingForMe = true
  if (filters.incomplete) result.incomplete = true
  if (filters.overdue) result.overdue = true
  return result
}

/* ═══════════════════════════════════════════════════════════════ calls */

export async function fetchBlankTemplates(signal?: AbortSignal): Promise<BlankTemplate[]> {
  return listBlankTemplates(signal)
}

/** Published versions are immutable — callers cache this forever. */
export async function fetchBlankTemplateVersion(id: string, signal?: AbortSignal): Promise<BlankTemplateVersionDto> {
  const dto = await getBlankTemplateVersion(id, signal)
  return { ...dto, definition: dto.definition as unknown as BlankDefinition }
}

export async function setBlankTemplateActiveFlag(id: string, isActive: boolean): Promise<BlankTemplate> {
  return setBlankTemplateActive(id, { isActive })
}

export async function fetchBlankRoles(signal?: AbortSignal): Promise<BlankRole[]> {
  return listBlankRoles(signal)
}

export async function putBlankRoleUsers(roleCode: string, userIds: string[]): Promise<BlankRole> {
  return setBlankRoleUsers(roleCode, { userIds })
}

export async function fetchBlankSubmissions(query: BlankListQuery, signal?: AbortSignal): Promise<BlankPage> {
  const page = await listBlankSubmissions(
    { ...blankListParams(query), page: query.page, pageSize: query.pageSize },
    signal,
  )
  return {
    items: page.items.map(toListItem),
    total: toNumber(page.total),
    page: toNumber(page.page),
    pageSize: toNumber(page.pageSize),
  }
}

export async function postBlankSubmission(body: BlankCreateInput): Promise<BlankSubmission> {
  return toSubmission(await createBlankSubmission(body))
}

export async function fetchBlankSubmission(id: string, signal?: AbortSignal): Promise<BlankSubmission> {
  return toSubmission(await getBlankSubmission(id, signal))
}

export async function putBlankAnswers(id: string, expectedVersion: number, answers: Answers): Promise<BlankSubmission> {
  return toSubmission(await saveBlankAnswers(id, { expectedVersion, answers }))
}

export async function postBlankSubmit(id: string, expectedVersion: number): Promise<BlankSubmission> {
  return toSubmission(await submitBlankSubmission(id, { expectedVersion }))
}

export async function postBlankCancel(id: string, expectedVersion: number, reason: string): Promise<BlankSubmission> {
  return toSubmission(await cancelBlankSubmission(id, { expectedVersion, reason }))
}

/* ═════════════════════════════════════ TODO(S7): files #17–#19, SPEC contract */

/** TODO(S7): replace with the generated #17 call after `./bonvi gen`. */
export async function uploadBlankAttachment(
  submissionId: string,
  fieldPath: string,
  file: File,
): Promise<BlankAttachmentDto> {
  const form = new FormData()
  form.append('fieldPath', fieldPath)
  form.append('file', file)
  const { data } = await api.post<BlankAttachmentDto & { sizeBytes: number | string }>(
    `${BASE}/submissions/${submissionId}/attachments`,
    form,
  )
  return { ...data, sizeBytes: toNumber(data.sizeBytes) }
}

/** TODO(S7): replace with the generated #18 call after `./bonvi gen`. */
export async function deleteBlankAttachment(
  submissionId: string,
  attachmentId: string,
  expectedVersion: number,
): Promise<void> {
  await api.delete(`${BASE}/submissions/${submissionId}/attachments/${attachmentId}`, {
    params: { expectedVersion },
  })
}

/** TODO(S7): replace with the generated #19 call after `./bonvi gen`. */
export async function fetchBlankAttachment(submissionId: string, attachmentId: string): Promise<Blob> {
  const { data } = await api.get<Blob>(`${BASE}/submissions/${submissionId}/attachments/${attachmentId}`, {
    responseType: 'blob',
  })
  return data
}
