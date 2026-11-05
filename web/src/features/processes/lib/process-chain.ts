import type {
  ProcessCaseDetail,
  ProcessCaseStep,
  ProcessLinkedBlank,
  ProcessStepState,
} from '../api/processes-api'
import { groupStepsByColumn, type ProcessColumnSteps } from './process-step'

/** A step with the case it belongs to: writes go to THAT case, with THAT case's `version`. */
export interface ProcessChainStep {
  detail: ProcessCaseDetail
  step: ProcessCaseStep
}

/**
 * The stepper of one person's cases, oldest stage first. Two templates can name a column alike, so a key is only unique
 * together with its case.
 */
export function chainColumns(details: readonly ProcessCaseDetail[]): ProcessColumnSteps[] {
  return details.flatMap((detail) =>
    groupStepsByColumn(detail.columns, detail.steps).map((column) => ({
      ...column,
      key: `${detail.id}:${column.key}`,
    })),
  )
}

/** Every step of the chain as the stepper draws it, with its own case. */
export function chainSteps(details: readonly ProcessCaseDetail[]): ProcessChainStep[] {
  return details.flatMap((detail) =>
    groupStepsByColumn(detail.columns, detail.steps).flatMap((column) =>
      column.steps.map((step) => ({ detail, step })),
    ),
  )
}

function isOpen(state: ProcessStepState): boolean {
  return state === 'Active' || state === 'Blocked'
}

function isFinished(state: ProcessStepState): boolean {
  return state === 'Done' || state === 'Skipped'
}

/**
 * Which step a chain opens on: the first the viewer can act on, else what is in motion on the case that was opened, else
 * anywhere in the chain, else the last finished one. A case that has ended has nothing left to act on, whatever its rows
 * still say.
 */
export function defaultChainStepCode(
  details: readonly ProcessCaseDetail[],
  openedCaseId: string,
): string | undefined {
  const steps = chainSteps(details)
  const running = steps.filter(({ detail, step }) => detail.status === 'Active' && isOpen(step.state))
  const finished = steps.filter(({ step }) => isFinished(step.state))

  return (
    running.find(({ step }) => step.viewerHolds) ??
    running.find(({ detail }) => detail.id === openedCaseId) ??
    running[0] ??
    finished[finished.length - 1] ??
    steps[0]
  )?.step.stepCode
}

/**
 * Whose page this is: the latest stage that names a person. A request names a position until there is someone to hire, so
 * it only answers when nothing else does.
 */
export function chainPersonCase(details: readonly ProcessCaseDetail[]): ProcessCaseDetail | undefined {
  const named = details.filter((detail) => (detail.subject?.name ?? null) !== null)
  const people = named.filter((detail) => detail.subject?.kind !== 'VacancyRequest')

  return people[people.length - 1] ?? named[named.length - 1] ?? details[details.length - 1]
}

/** The latest stage's answer, or an earlier one's when that stage does not know yet. */
export function latestInChain<T>(
  details: readonly ProcessCaseDetail[],
  read: (detail: ProcessCaseDetail) => T | null | undefined,
): T | null {
  for (let index = details.length - 1; index >= 0; index -= 1) {
    const detail = details[index]
    const value = detail === undefined ? null : read(detail)
    if (value !== null && value !== undefined) return value
  }
  return null
}

/** The blanks of all the stages, once each: a blank can be linked from two steps. */
export function chainBlanks(details: readonly ProcessCaseDetail[]): ProcessLinkedBlank[] {
  const byId = new Map(details.flatMap((detail) => detail.blanks).map((blank) => [blank.id, blank]))
  return [...byId.values()]
}
