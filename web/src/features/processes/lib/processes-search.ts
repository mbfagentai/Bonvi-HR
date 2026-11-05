/** The three tabs of `/mening-ishlarim` (SPEC §11.2–§11.4). */
export const PROCESS_TABS = ['my', 'board', 'late'] as const
export type ProcessTab = (typeof PROCESS_TABS)[number]

export interface ProcessesSearch {
  /** Absent means the first tab, so the plain menu address stays clean. */
  tab?: Exclude<ProcessTab, 'my'>
}

/** Never throws: a stale or mistyped `tab` opens the first tab. */
export function validateProcessesSearch(search: Record<string, unknown>): ProcessesSearch {
  const tab = search['tab']
  return tab === 'board' || tab === 'late' ? { tab } : {}
}
