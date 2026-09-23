/**
 * Work sites (Verifikatsiya → Joylashuvlar) — API layer.
 *
 * Every type is derived from the generated contract (`./bonvi gen`); this
 * file only narrows `number | string` to `number` (`AsSent`, same rule as
 * `attendance-api.ts`) and wraps the generated calls. No hand-written fetch.
 *
 * Why the generated React Query hooks are not used directly: `orval.config.ts`
 * turns every operation into `useQuery` (including POST/PUT), so writes are
 * wrapped in `useMutation` in `work-sites-queries.ts`, exactly like the rest
 * of the verification module.
 */
import {
  activateWorkSite,
  createWorkSite,
  deactivateWorkSite,
  listWorkSites,
  updateWorkSite,
} from '@/shared/api/generated/attendance/attendance'
import {
  WorkSiteShape,
  type WorkSiteDto,
  type WorkSiteRequest,
  type WorkSiteVertexDto,
} from '@/shared/api/generated/model'

import type { AsSent } from '../../api/attendance-api'

export { WorkSiteShape }

/** A work site as the server sends it. For polygons `latitude/longitude` is the centroid. */
export type WorkSite = AsSent<WorkSiteDto>

/** One polygon vertex. */
export type WorkSiteVertex = AsSent<WorkSiteVertexDto>

/** Create and update share one body (server contract). */
export type WorkSiteBody = WorkSiteRequest

/* Limits — mirror of the domain rules (`WorkSite.cs`, `WorkSitePolygon.cs`). */
export const SITE_NAME_MAX = 255
export const SITE_ADDRESS_MAX = 300
export const SITE_RADIUS_MIN = 50
export const SITE_RADIUS_MAX = 5000
export const SITE_RADIUS_DEFAULT = 200
export const SITE_BUFFER_MIN = 0
export const SITE_BUFFER_MAX = 500
export const SITE_BUFFER_DEFAULT = 50
export const SITE_VERTICES_MIN = 3
export const SITE_VERTICES_MAX = 50

function asSent<T>(value: T): AsSent<T> {
  return value as unknown as AsSent<T>
}

/**
 * All sites, inactive ones included.
 *
 * The list is small (a handful of warehouses), so the page filters
 * active/inactive on the client: that way it can warn "no active site"
 * even while inactive rows are hidden.
 */
export async function fetchWorkSites(signal?: AbortSignal): Promise<WorkSite[]> {
  return asSent(await listWorkSites({ onlyActive: false }, signal))
}

/** `id === null` — create. */
export async function saveWorkSite(id: string | null, body: WorkSiteBody): Promise<WorkSite> {
  return asSent(id === null ? await createWorkSite(body) : await updateWorkSite(id, body))
}

/**
 * Deactivation never deletes: accepted check-ins keep pointing at the site.
 * `reason` is appended to the note by the server.
 */
export async function setWorkSiteActive(
  id: string,
  active: boolean,
  reason: string | null,
): Promise<void> {
  if (active) {
    await activateWorkSite(id)
  } else {
    await deactivateWorkSite(id, reason === null ? undefined : { reason })
  }
}
