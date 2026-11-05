import type { ProcessGateOverrideRow, ProcessTask } from '../api/processes-api'

/** `ProcessGateOverride.MinReasonLength` / `MaxReasonLength`; the server has the final word. */
export const OVERRIDE_REASON_MIN = 10
export const OVERRIDE_REASON_MAX = 1000

export type ProcessOverrideStanding = 'used' | 'open' | 'expired'

/** Where a granted exception stands: spent, still waiting to be used, or lapsed unused after its 24 hours. */
export function overrideStanding(
  row: Pick<ProcessGateOverrideRow, 'consumedAt' | 'expiresAt'>,
  nowMs: number,
): ProcessOverrideStanding {
  if (row.consumedAt !== null) return 'used'
  return Date.parse(row.expiresAt) > nowMs ? 'open' : 'expired'
}

/** The exception already granted for this blocked step and not yet used; a second one would achieve nothing. */
export function openOverrideFor(
  task: Pick<ProcessTask, 'caseId' | 'gateCode'>,
  overrides: readonly ProcessGateOverrideRow[],
  nowMs: number,
): ProcessGateOverrideRow | undefined {
  return overrides.find(
    (row) =>
      row.caseId === task.caseId &&
      row.gateCode === task.gateCode &&
      overrideStanding(row, nowMs) === 'open',
  )
}

/** Whether a row can take an exception at all: only a step a gate is holding has something to excuse. */
export function canTakeOverride(task: Pick<ProcessTask, 'state' | 'gateCode'>): boolean {
  return task.state === 'Blocked' && task.gateCode !== null
}

/** Length as the server counts it: surrounding spaces do not make a reason. */
export function isOverrideReasonValid(reason: string): boolean {
  const { length } = reason.trim()
  return length >= OVERRIDE_REASON_MIN && length <= OVERRIDE_REASON_MAX
}
