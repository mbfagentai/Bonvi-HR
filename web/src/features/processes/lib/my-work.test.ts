import { describe, expect, it } from 'vitest'

import type {
  ProcessHolder,
  ProcessMyWork,
  ProcessSubject,
  ProcessTask,
} from '../api/processes-api'
import { hasNoTasks, isMyWorkEmpty, tartibCountHref, taskLinkAction, taskResolver } from './my-work'

const holder = (role: string | null, employeeId: string | null = null): ProcessHolder => ({
  role,
  employeeId,
  employeeName: null,
  users: [],
  unassigned: false,
})

const subject: ProcessSubject = {
  kind: 'Candidate',
  name: 'Aliyev Vali',
  position: null,
  vacancyRequestId: null,
  candidateId: 'c1',
  employmentId: null,
  probationId: null,
  employeeId: 'e1',
}

const emptyWork: ProcessMyWork = {
  today: '2026-11-05',
  count: 0,
  lateCount: 0,
  urgent: [],
  later: [],
  held: [],
  waiting: [],
  tartib: { notifications: [], counts: [] },
}

const someTask = {} as ProcessTask

describe('isMyWorkEmpty', () => {
  it('is empty only with no tasks and no tartib card', () => {
    expect(isMyWorkEmpty(emptyWork)).toBe(true)
    expect(hasNoTasks(emptyWork)).toBe(true)
  })

  it.each(['urgent', 'later', 'held', 'waiting'] as const)('counts a task in %s', (section) => {
    const work = { ...emptyWork, [section]: [someTask] }
    expect(hasNoTasks(work)).toBe(false)
    expect(isMyWorkEmpty(work)).toBe(false)
  })

  it('keeps the tab alive for a tartib card alone, though there are no tasks', () => {
    const work = {
      ...emptyWork,
      tartib: { notifications: [], counts: [{ key: 'employeesWithoutPhoto', count: 3 }] },
    }
    expect(hasNoTasks(work)).toBe(true)
    expect(isMyWorkEmpty(work)).toBe(false)
  })
})

describe('tartibCountHref', () => {
  it('leads each known count to its screen', () => {
    expect(tartibCountHref('unmatchedTerminalPersons')).toBe('/verifikatsiya/moslashtirish')
    expect(tartibCountHref('employeesWithoutPhoto')).toBe('/xodimlar')
    expect(tartibCountHref('employeesWithoutSchedule')).toBe('/verifikatsiya/jadvallar')
    expect(tartibCountHref('employeesWithIncompleteFiles')).toBe('/mening-ishlarim?tab=docs')
  })

  it('has no destination for a key this build does not know', () => {
    expect(tartibCountHref('somethingNew')).toBeNull()
  })
})

describe('taskLinkAction', () => {
  const action = (screen: string, args: Record<string, string>, linked: string | null = null) =>
    taskLinkAction({ link: { screen, args }, subject, linkedBlankSubmissionId: linked })

  it('names an opened blank as a blank', () => {
    expect(action('blank', { template: 'HR-F06' }, 'b1')).toEqual({
      kind: 'internal',
      href: '/blankalar/b1',
      label: 'blank',
    })
  })

  it('leaves creating a blank to the step panel', () => {
    expect(action('blank', { template: 'HR-F06' })).toBeNull()
  })

  it('names any other screen generically', () => {
    expect(action('employee', {})).toEqual({
      kind: 'internal',
      href: '/xodimlar/e1',
      label: 'screen',
    })
  })

  it('passes an external address through and refuses a script address', () => {
    expect(action('external', { url: 'https://example.uz/a' })).toEqual({
      kind: 'external',
      url: 'https://example.uz/a',
    })
    expect(action('external', { url: 'javascript:alert(1)' })).toBeNull()
  })

  it('offers nothing for a step with no screen', () => {
    expect(action('none', {})).toBeNull()
  })
})

describe('taskResolver', () => {
  it('names a different resolver on a blocked step', () => {
    const resolver = holder('hr2')
    expect(taskResolver({ state: 'Blocked', holder: holder('hr1'), resolver })).toBe(resolver)
  })

  it('stays quiet when the holder clears the block themself', () => {
    expect(
      taskResolver({ state: 'Blocked', holder: holder('hr1'), resolver: holder('hr1') }),
    ).toBeNull()
  })

  it('tells apart two people holding the same role', () => {
    const resolver = holder('departmentHead', 'e2')
    expect(
      taskResolver({ state: 'Blocked', holder: holder('departmentHead', 'e1'), resolver }),
    ).toBe(resolver)
  })

  it('ignores a resolver on a step that is not blocked', () => {
    expect(
      taskResolver({ state: 'Active', holder: holder('hr1'), resolver: holder('hr2') }),
    ).toBeNull()
  })
})
