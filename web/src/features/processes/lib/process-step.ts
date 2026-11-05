import type {
  ProcessCaseStep,
  ProcessColumn,
  ProcessStepState,
  ProcessSubject,
} from '../api/processes-api'
import { resolveProcessLink, type ProcessLinkTarget } from './process-link'

/**
 * Slice P7 brings `POST /cases`, `/done`, `/skip`, `/undo`. Until it merges, the buttons are drawn from the server's
 * `canComplete`/`canSkip`/`canUndo` but stay disabled. Turning this on is the whole wiring.
 */
export const PROCESS_WRITE_API_READY: boolean = true

export interface ProcessColumnSteps extends ProcessColumn {
  steps: ProcessCaseStep[]
}

function byOrder(a: ProcessCaseStep, b: ProcessCaseStep): number {
  return a.orderNo - b.orderNo
}

/**
 * The timeline's groups, in the definition's column order. An empty column is dropped; a step naming a column the
 * definition does not list still shows, at the end, under its raw key.
 */
export function groupStepsByColumn(
  columns: readonly ProcessColumn[],
  steps: readonly ProcessCaseStep[],
): ProcessColumnSteps[] {
  const known = new Set(columns.map((column) => column.key))
  const strays = [
    ...new Set(steps.map((step) => step.column).filter((column) => !known.has(column))),
  ]

  return [...columns, ...strays.map((key) => ({ key, title: key }))].flatMap((column) => {
    const own = steps.filter((step) => step.column === column.key).sort(byOrder)
    return own.length === 0 ? [] : [{ ...column, steps: own }]
  })
}

export function isStepFinished(state: ProcessStepState): boolean {
  return state === 'Done' || state === 'Skipped'
}

/** «7 / 23 qadam bajarildi»: a skipped step is out of the way too, so it counts as finished. */
export function stepProgress(steps: readonly Pick<ProcessCaseStep, 'state'>[]): {
  done: number
  total: number
} {
  return { done: steps.filter((step) => isStepFinished(step.state)).length, total: steps.length }
}

function isStepOpen(state: ProcessStepState): boolean {
  return state === 'Active' || state === 'Blocked'
}

/**
 * Which step the page opens on: what the viewer can act on, else what is in motion for someone else, else the last
 * finished step, so a closed case does not open on its very first line.
 */
export function defaultStepCode(steps: readonly ProcessCaseStep[]): string | undefined {
  const ordered = [...steps].sort(byOrder)
  const finished = ordered.filter((step) => step.state === 'Done' || step.state === 'Skipped')

  return (
    ordered.find((step) => step.viewerHolds && isStepOpen(step.state)) ??
    ordered.find((step) => isStepOpen(step.state)) ??
    finished[finished.length - 1] ??
    ordered[0]
  )?.stepCode
}

/** The step's own screen: «Boshlash» starts the work, «Ochish» only shows what is already there. */
export interface ProcessStepEntry {
  kind: 'start' | 'open'
  target: Exclude<ProcessLinkTarget, { kind: 'none' }>
}

export interface ProcessBlankCreate {
  templateCode: string
  employeeId: string | null
  candidateId: string | null
}

/**
 * The body of `POST /api/blanks/submissions`. The server takes exactly one subject, the one the template asks for. `null`
 * means this case does not know that subject yet (a hire before the employee exists), so nothing can be created.
 */
export function blankCreateBody(
  target: Extract<ProcessLinkTarget, { kind: 'createBlank' }>,
  subject: ProcessSubject | null | undefined,
): ProcessBlankCreate | null {
  const base = { templateCode: target.template, employeeId: null, candidateId: null }
  const employeeId = subject?.employeeId ?? null
  const candidateId = subject?.candidateId ?? null

  switch (target.subject) {
    case 'employee':
      return employeeId === null ? null : { ...base, employeeId }
    case 'candidate':
      return candidateId === null ? null : { ...base, candidateId }
    default:
      return base
  }
}

/**
 * What the panel's main button does (SPEC §11.5, §11.6), or `null` for no button. Only the holder of an open step starts
 * anything, and only with `mayStart`: `processes.work`, on a case still running. Others may still look at what the step
 * links to, unless it is not this step's turn yet.
 */
export function stepEntry(
  step: ProcessCaseStep,
  subject: ProcessSubject | null | undefined,
  mayStart: boolean,
): ProcessStepEntry | null {
  const target = resolveProcessLink(step.link, subject, step.linkedBlankSubmissionId)
  if (target.kind === 'none') return null

  const startable = mayStart && step.viewerHolds && isStepOpen(step.state)

  if (target.kind === 'createBlank') {
    return startable && blankCreateBody(target, subject) !== null
      ? { kind: 'start', target }
      : null
  }

  if (startable && step.linkedBlankSubmissionId === null) return { kind: 'start', target }
  return step.state === 'Locked' || step.state === 'Waiting' ? null : { kind: 'open', target }
}

/**
 * Whether «you only watch, nobody can do this for them» is true for this viewer. A processes.manage
 * user is not the holder and still has a working «Bajarildi»: the text would contradict that button.
 */
export function isViewOnlyForViewer(
  step: Pick<ProcessCaseStep, 'viewerHolds' | 'canComplete'>,
): boolean {
  return !step.viewerHolds && !step.canComplete
}

/** The titles of the steps still in the way of a locked one. */
export function pendingPrereqTitles(
  step: ProcessCaseStep,
  steps: readonly ProcessCaseStep[],
): string[] {
  return step.prereqs.flatMap((code) => {
    const prereq = steps.find((candidate) => candidate.stepCode === code)
    return prereq === undefined || prereq.state === 'Done' || prereq.state === 'Skipped'
      ? []
      : [prereq.title]
  })
}
