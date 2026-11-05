import type { ProcessHolder, ProcessMyWork, ProcessTask } from '../api/processes-api'
import { resolveProcessLink } from './process-link'

/** The four task lists of `/my`, in the order the tab draws them (SPEC §11.2). */
export const MY_WORK_SECTIONS = ['urgent', 'later', 'held', 'waiting'] as const
export type MyWorkSection = (typeof MY_WORK_SECTIONS)[number]

/** True when the four task lists hold nothing, whatever the «Tartib» cards say. */
export function hasNoTasks(work: ProcessMyWork): boolean {
  return MY_WORK_SECTIONS.every((section) => work[section].length === 0)
}

/** A section's size before the API's cut, never less than the cards actually delivered. */
export function sectionTotal(work: ProcessMyWork, section: MyWorkSection): number {
  return Math.max(work[`${section}Total`], work[section].length)
}

/** True when the whole tab would be blank: no tasks and no «Tartib» card. */
export function isMyWorkEmpty(work: ProcessMyWork): boolean {
  return (
    hasNoTasks(work) && work.tartib.notifications.length === 0 && work.tartib.counts.length === 0
  )
}

/** The screen behind a «Tartib» count card (SPEC §11.8); `null` for a key this build does not know. */
export function tartibCountHref(key: string): string | null {
  switch (key) {
    case 'unmatchedTerminalPersons':
      return '/verifikatsiya/moslashtirish'
    case 'employeesWithoutPhoto':
      return '/xodimlar'
    case 'employeesWithoutSchedule':
      return '/verifikatsiya/jadvallar'
    case 'employeesWithIncompleteFiles':
      return '/mening-ishlarim?tab=docs'
    default:
      return null
  }
}

export type TaskLinkAction =
  | { kind: 'internal'; href: string; label: 'blank' | 'screen' }
  | { kind: 'external'; url: string }

/**
 * The card's second button, beside opening the case. `createBlank` gets none on purpose: creating a blank is a write
 * with its own checks, and it belongs to the step panel on the case page (SPEC §11.5).
 */
export function taskLinkAction(
  task: Pick<ProcessTask, 'link' | 'subject' | 'linkedBlankSubmissionId'>,
): TaskLinkAction | null {
  const target = resolveProcessLink(task.link, task.subject, task.linkedBlankSubmissionId)

  switch (target.kind) {
    case 'internal':
      return {
        kind: 'internal',
        href: target.href,
        label: task.link.screen === 'blank' ? 'blank' : 'screen',
      }
    case 'external':
      return { kind: 'external', url: target.url }
    case 'createBlank':
    case 'none':
      return null
  }
}

function sameHolder(a: ProcessHolder, b: ProcessHolder): boolean {
  return a.role === b.role && a.employeeId === b.employeeId
}

/** Who must clear a block, when that is somebody other than the step's own holder. */
export function taskResolver(
  task: Pick<ProcessTask, 'state' | 'holder' | 'resolver'>,
): ProcessHolder | null {
  if (task.state !== 'Blocked' || task.resolver === null) return null
  return sameHolder(task.holder, task.resolver) ? null : task.resolver
}
