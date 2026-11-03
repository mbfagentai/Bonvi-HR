import { useMutation, useQuery } from '@tanstack/react-query'

import type { Answers } from '../lib/definition-types'
import {
  fetchBlankRoles,
  fetchBlankSubmission,
  fetchBlankSubmissions,
  fetchBlankTemplates,
  fetchBlankTemplateVersion,
  postBlankCancel,
  postBlankSubmission,
  postBlankSubmit,
  putBlankAnswers,
  putBlankRoleUsers,
  type BlankCreateInput,
  type BlankListQuery,
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
