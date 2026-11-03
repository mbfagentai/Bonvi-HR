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
 * Steps #12–#15 and files #17–#18 use the generated calls; the blob downloads
 * #16 (signature PNG), #19 (attachment) and #20 (PDF) are hand-written GETs because
 * the generated functions are typed `void` (no `responseType: 'blob'`).
 */
import {
  approveBlankStep,
  cancelBlankSubmission,
  createBlankSubmission,
  getBlankSubmission,
  getBlankTemplateVersion,
  listBlankRoles,
  listBlankSubmissions,
  listBlankTemplates,
  rejectBlankStep,
  removeBlankAttachment,
  returnBlankStep,
  saveBlankAnswers,
  setBlankRoleUsers,
  setBlankTemplateActive,
  signBlankStepInPerson,
  submitBlankSubmission,
  uploadBlankAttachment as generatedUploadBlankAttachment,
} from '@/shared/api/generated/blanks/blanks'
import type {
  ApproveBlankStepBody,
  BlankCanDto,
  BlankCurrentStepDto,
  BlankRoleDto,
  BlankStepCommentBody,
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
  JsonValue,
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

/** Body of #12 `steps/{stepCode}/approve` — the generated body with the UI types. */
export interface BlankStepApproveInput
  extends Omit<ApproveBlankStepBody, 'expectedVersion' | 'decisionExtra' | 'answers'> {
  expectedVersion: number
  decisionExtra: JsonValue | null
  /** The step's own sections only (server: full replacement of those sections). */
  answers: Answers | null
}

/** JSON `payload` of #13 `sign-in-person` = #12 body + the typed signer name (witness: required). */
export interface BlankStepSignInput extends BlankStepApproveInput {
  signerName: string | null
}

/** Body of #14 `return` and #15 `reject` (comment required). */
export interface BlankStepCommentInput extends Omit<BlankStepCommentBody, 'expectedVersion' | 'comment'> {
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

/* ═══════════════════════════════════════════════════════════════ steps #12–#16 */

export async function postBlankStepApprove(id: string, stepCode: string, body: BlankStepApproveInput): Promise<BlankSubmission> {
  return toSubmission(await approveBlankStep(id, stepCode, body))
}

/** #13: `payload` = JSON of the act, `signature` = the PNG from the pad. */
export async function postBlankStepSignInPerson(
  id: string,
  stepCode: string,
  body: BlankStepSignInput,
  signature: Blob,
): Promise<BlankSubmission> {
  return toSubmission(await signBlankStepInPerson(id, stepCode, { payload: JSON.stringify(body), signature }))
}

export async function postBlankStepReturn(id: string, stepCode: string, body: BlankStepCommentInput): Promise<BlankSubmission> {
  return toSubmission(await returnBlankStep(id, stepCode, body))
}

export async function postBlankStepReject(id: string, stepCode: string, body: BlankStepCommentInput): Promise<BlankSubmission> {
  return toSubmission(await rejectBlankStep(id, stepCode, body))
}

/** #16 — hand-written: the generated `getBlankStepSignature` is typed `void` and reads text. */
export async function fetchBlankStepSignature(id: string, stepCode: string, signal?: AbortSignal): Promise<Blob> {
  const { data } = await api.get<Blob>(`${BASE}/submissions/${id}/steps/${encodeURIComponent(stepCode)}/signature`, {
    responseType: 'blob',
    signal,
  })
  return data
}

/** `waitingForMe` total for the nav counter — one row is enough, only `total` is read. */
export async function fetchBlankWaitingCount(signal?: AbortSignal): Promise<number> {
  const page = await listBlankSubmissions({ waitingForMe: true, page: 1, pageSize: 1 }, signal)
  return toNumber(page.total)
}

/* ═════════════════════════════════════════════════════════════ files #17–#20 */

/** #17 — `POST /api/blanks/submissions/{id}/attachments` (multipart: `fieldPath`, `file`). */
export async function uploadBlankAttachment(
  submissionId: string,
  fieldPath: string,
  file: File,
): Promise<BlankAttachmentDto> {
  const data = await generatedUploadBlankAttachment(submissionId, { fieldPath, file })
  return { ...data, sizeBytes: toNumber(data.sizeBytes) }
}

/** #18 — `DELETE /api/blanks/submissions/{id}/attachments/{attachmentId}?expectedVersion=`. */
export async function deleteBlankAttachment(
  submissionId: string,
  attachmentId: string,
  expectedVersion: number,
): Promise<void> {
  await removeBlankAttachment(submissionId, attachmentId, { expectedVersion })
}

/**
 * #19 — `GET /api/blanks/submissions/{id}/attachments/{attachmentId}`. Hand-written blob GET:
 * the generated `downloadBlankAttachment` is typed `void` (no `responseType: 'blob'`).
 */
export async function fetchBlankAttachment(submissionId: string, attachmentId: string, signal?: AbortSignal): Promise<Blob> {
  const { data } = await api.get<Blob>(`${BASE}/submissions/${submissionId}/attachments/${attachmentId}`, {
    responseType: 'blob',
    signal,
  })
  return data
}

/**
 * #20 — `GET /api/blanks/submissions/{id}/pdf`, rendered fresh in any status. Hand-written blob GET:
 * the generated `getBlankPdf` is typed `void`.
 */
export async function fetchBlankPdf(submissionId: string, signal?: AbortSignal): Promise<Blob> {
  const { data } = await api.get<Blob>(`${BASE}/submissions/${submissionId}/pdf`, { responseType: 'blob', signal })
  return data
}
