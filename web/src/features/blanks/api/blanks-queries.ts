import { useMutation, useQuery } from '@tanstack/react-query'

import type { Answers } from '../lib/definition-types'
import {
  fetchBlankAttachment,
  fetchBlankPdf,
  fetchBlankRoles,
  fetchBlankStepSignature,
  fetchBlankSubmission,
  fetchBlankSubmissions,
  fetchBlankTemplates,
  fetchBlankTemplateVersion,
  fetchBlankWaitingCount,
  postBlankCancel,
  postBlankStepApprove,
  postBlankStepReject,
  postBlankStepReturn,
  postBlankStepSignInPerson,
  postBlankSubmission,
  postBlankSubmit,
  putBlankAnswers,
  putBlankRoleUsers,
  type BlankCreateInput,
  type BlankListQuery,
  type BlankStepApproveInput,
  type BlankStepCommentInput,
  type BlankStepSignInput,
} from './blanks-api'

/**
 * Cache keys live under `['blanks', …]`.
 *
 * Mutations do NOT call `invalidateQueries` themselves — `providers.tsx` invalidates
 * the cache after every successful write (same rule as `vacancy-requests-queries.ts`).
 */
export const blankKeys = {
  templates: () => ['blanks', 'templates'] as const,
  version: (id: string) => ['blanks', 'template-version', id] as const,
  roles: () => ['blanks', 'roles'] as const,
  list: (query: BlankListQuery) => ['blanks', 'submissions', query] as const,
  detail: (id: string) => ['blanks', 'submission', id] as const,
  signature: (id: string, stepCode: string) => ['blanks', 'signature', id, stepCode] as const,
  attachment: (id: string, attachmentId: string) => ['blanks', 'attachment', id, attachmentId] as const,
  pdf: (id: string, version: number) => ['blanks', 'pdf', id, version] as const,
  waitingCount: () => ['blanks', 'waiting-count'] as const,
}

export function useBlankTemplates() {
  return useQuery({
    queryKey: blankKeys.templates(),
    queryFn: ({ signal }) => fetchBlankTemplates(signal),
    staleTime: 5 * 60_000,
  })
}

/** A Published version never changes — fetched once per session. */
export function useBlankTemplateVersion(id: string | undefined) {
  return useQuery({
    queryKey: blankKeys.version(id ?? ''),
    queryFn: ({ signal }) => fetchBlankTemplateVersion(id ?? '', signal),
    enabled: id !== undefined && id !== '',
    staleTime: Infinity,
    gcTime: Infinity,
  })
}

export function useBlankRoles(enabled = true) {
  return useQuery({ queryKey: blankKeys.roles(), queryFn: ({ signal }) => fetchBlankRoles(signal), enabled })
}

export function useBlankSubmissions(query: BlankListQuery) {
  return useQuery({
    queryKey: blankKeys.list(query),
    queryFn: ({ signal }) => fetchBlankSubmissions(query, signal),
    // The table does not blank out while the page or a filter changes
    placeholderData: (previous) => previous,
  })
}

export function useBlankSubmission(id: string) {
  return useQuery({
    queryKey: blankKeys.detail(id),
    queryFn: ({ signal }) => fetchBlankSubmission(id, signal),
    // Background refetches must not race the autosave — the fill page reloads explicitly
    refetchOnWindowFocus: false,
  })
}

export function useCreateBlank() {
  return useMutation({ mutationFn: (body: BlankCreateInput) => postBlankSubmission(body) })
}

export function useSaveBlankAnswers() {
  return useMutation({
    mutationFn: ({ id, expectedVersion, answers }: { id: string; expectedVersion: number; answers: Answers }) =>
      putBlankAnswers(id, expectedVersion, answers),
  })
}

export function useSubmitBlank() {
  return useMutation({
    mutationFn: ({ id, expectedVersion }: { id: string; expectedVersion: number }) => postBlankSubmit(id, expectedVersion),
  })
}

export function useCancelBlank() {
  return useMutation({
    mutationFn: ({ id, expectedVersion, reason }: { id: string; expectedVersion: number; reason: string }) =>
      postBlankCancel(id, expectedVersion, reason),
  })
}

export function useSetBlankRoleUsers() {
  return useMutation({
    mutationFn: ({ roleCode, userIds }: { roleCode: string; userIds: string[] }) => putBlankRoleUsers(roleCode, userIds),
  })
}

/* ── steps #12–#16 ──────────────────────────────────────────────────────── */

export function useApproveBlankStep() {
  return useMutation({
    mutationFn: ({ id, stepCode, body }: { id: string; stepCode: string; body: BlankStepApproveInput }) =>
      postBlankStepApprove(id, stepCode, body),
  })
}

export function useSignBlankStepInPerson() {
  return useMutation({
    mutationFn: ({ id, stepCode, body, signature }: { id: string; stepCode: string; body: BlankStepSignInput; signature: Blob }) =>
      postBlankStepSignInPerson(id, stepCode, body, signature),
  })
}

export function useReturnBlankStep() {
  return useMutation({
    mutationFn: ({ id, stepCode, body }: { id: string; stepCode: string; body: BlankStepCommentInput }) =>
      postBlankStepReturn(id, stepCode, body),
  })
}

export function useRejectBlankStep() {
  return useMutation({
    mutationFn: ({ id, stepCode, body }: { id: string; stepCode: string; body: BlankStepCommentInput }) =>
      postBlankStepReject(id, stepCode, body),
  })
}

/** A stored signature never changes — fetched once; no retry (the row shows a dash instead). */
export function useBlankStepSignature(id: string, stepCode: string, enabled: boolean) {
  return useQuery({
    queryKey: blankKeys.signature(id, stepCode),
    queryFn: ({ signal }) => fetchBlankStepSignature(id, stepCode, signal),
    enabled,
    staleTime: Infinity,
    retry: false,
  })
}

/** One file — one content (a replacement gets a new id), same rule as `useCandidateFileBlob`. */
export function useBlankAttachmentBlob(id: string, attachmentId: string, enabled: boolean) {
  return useQuery({
    queryKey: blankKeys.attachment(id, attachmentId),
    queryFn: ({ signal }) => fetchBlankAttachment(id, attachmentId, signal),
    enabled: enabled && attachmentId !== '',
    staleTime: Infinity,
    retry: false,
  })
}

/** The PDF is rendered fresh by the server — keyed by version, dropped as soon as the viewer closes. */
export function useBlankPdf(id: string, version: number, enabled: boolean) {
  return useQuery({
    queryKey: blankKeys.pdf(id, version),
    queryFn: ({ signal }) => fetchBlankPdf(id, signal),
    enabled,
    staleTime: Infinity,
    gcTime: 0,
    retry: false,
  })
}

/** Nav counter «Menga kutilmoqda». */
export function useBlankWaitingCount(enabled: boolean) {
  return useQuery({
    queryKey: blankKeys.waitingCount(),
    queryFn: ({ signal }) => fetchBlankWaitingCount(signal),
    enabled,
    staleTime: 60_000,
  })
}
