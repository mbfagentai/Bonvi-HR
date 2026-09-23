import { useMutation, useQuery } from '@tanstack/react-query'

import { fetchWorkSites, saveWorkSite, setWorkSiteActive, type WorkSiteBody } from './work-sites-api'

/**
 * Cache keys live under `['attendance', …]` like the rest of the module.
 *
 * Mutations do not invalidate by hand: `providers.tsx` invalidates the whole
 * cache after every successful write.
 */
export const workSiteKeys = {
  list: ['attendance', 'work-sites'] as const,
}

/** @param enabled `false` — the request is not sent at all (no permission). */
export function useWorkSites(enabled = true) {
  return useQuery({
    queryKey: workSiteKeys.list,
    queryFn: ({ signal }) => fetchWorkSites(signal),
    enabled,
  })
}

/** Create and update in one mutation: `id === null` creates. */
export function useSaveWorkSite() {
  return useMutation({
    mutationFn: ({ id, body }: { id: string | null; body: WorkSiteBody }) => saveWorkSite(id, body),
  })
}

export function useSetWorkSiteActive() {
  return useMutation({
    mutationFn: ({ id, active, reason }: { id: string; active: boolean; reason: string | null }) =>
      setWorkSiteActive(id, active, reason),
  })
}
