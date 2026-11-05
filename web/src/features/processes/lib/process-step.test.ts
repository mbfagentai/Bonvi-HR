import { describe, expect, it } from 'vitest'

import type { ProcessCaseStep, ProcessSubject } from '../api/processes-api'
import {
  blankCreateBody,
  defaultStepCode,
  groupStepsByColumn,
  pendingPrereqTitles,
  stepEntry,
} from './process-step'

const holder = { role: 'hr1', employeeId: null, employeeName: null, users: [], unassigned: false }

function step(partial: Partial<ProcessCaseStep> & Pick<ProcessCaseStep, 'stepCode'>) {
  return {
    orderNo: 1,
    title: partial.stepCode,
    column: 'a',
    kind: 'Manual',
    state: 'Active',
    resolution: null,
    holder,
    dueDate: null,
    daysLate: 0,
    doneAt: null,
    doneBy: null,
    doneByName: null,
    payload: null,
    blankCode: null,
    blankTitle: null,
    signature: null,
    linkedBlankSubmissionId: null,
    linkedRecordId: null,
    gateCode: null,
    gateErrorCode: null,
    resolver: null,
    warnings: [],
    prereqs: [],
    optional: false,
    guide: [],
    sourceRef: null,
    link: { screen: 'none', args: {} },
    manual: null,
    viewerHolds: true,
    canComplete: false,
    canSkip: false,
    canUndo: false,
    ...partial,
  } satisfies ProcessCaseStep
}

const subject: ProcessSubject = {
  kind: 'Employment',
  name: 'Aliyev Vali',
  position: null,
  vacancyRequestId: null,
  candidateId: 'cand-1',
  employmentId: 'emp-1',
  probationId: null,
  employeeId: 'person-1',
}

const blankLink = (extra: Record<string, string> = {}) => ({
  screen: 'blank',
  args: { template: 'HR-F08', ...extra },
})

describe('groupStepsByColumn', () => {
  it('keeps the definition order and sorts steps inside a column', () => {
    const groups = groupStepsByColumn(
      [
        { key: 'a', title: 'A' },
        { key: 'b', title: 'B' },
      ],
      [
        step({ stepCode: 's3', orderNo: 3, column: 'b' }),
        step({ stepCode: 's2', orderNo: 2, column: 'a' }),
        step({ stepCode: 's1', orderNo: 1, column: 'a' }),
      ],
    )
    expect(groups.map((group) => [group.key, group.steps.map((s) => s.stepCode)])).toEqual([
      ['a', ['s1', 's2']],
      ['b', ['s3']],
    ])
  })

  it('drops an empty column and still shows a step in an unlisted one', () => {
    const groups = groupStepsByColumn(
      [{ key: 'a', title: 'A' }],
      [step({ stepCode: 's1', column: 'z' })],
    )
    expect(groups.map((group) => [group.key, group.title])).toEqual([['z', 'z']])
  })
})

describe('defaultStepCode', () => {
  it('prefers an open step the viewer holds', () => {
    expect(
      defaultStepCode([
        step({ stepCode: 'other', orderNo: 1, viewerHolds: false }),
        step({ stepCode: 'mine', orderNo: 2 }),
      ]),
    ).toBe('mine')
  })

  it('falls back to any open step, then the last finished one, then the first', () => {
    expect(
      defaultStepCode([
        step({ stepCode: 'done', orderNo: 1, state: 'Done' }),
        step({ stepCode: 'other', orderNo: 2, viewerHolds: false, state: 'Blocked' }),
      ]),
    ).toBe('other')
    expect(
      defaultStepCode([
        step({ stepCode: 'd1', orderNo: 1, state: 'Done' }),
        step({ stepCode: 'd2', orderNo: 2, state: 'Skipped' }),
        step({ stepCode: 'locked', orderNo: 3, state: 'Locked' }),
      ]),
    ).toBe('d2')
    expect(
      defaultStepCode([
        step({ stepCode: 'l2', orderNo: 2, state: 'Locked' }),
        step({ stepCode: 'l1', orderNo: 1, state: 'Locked' }),
      ]),
    ).toBe('l1')
    expect(defaultStepCode([])).toBeUndefined()
  })
})

describe('blankCreateBody', () => {
  const target = (subjectArg: string | null) =>
    ({ kind: 'createBlank', template: 'HR-F08', subject: subjectArg }) as const

  it('sends exactly the subject the template asks for', () => {
    expect(blankCreateBody(target('employee'), subject)).toEqual({
      templateCode: 'HR-F08',
      employeeId: 'person-1',
      candidateId: null,
    })
    expect(blankCreateBody(target('candidate'), subject)).toEqual({
      templateCode: 'HR-F08',
      employeeId: null,
      candidateId: 'cand-1',
    })
  })

  it('sends no subject for a template without one', () => {
    expect(blankCreateBody(target(null), subject)).toEqual({
      templateCode: 'HR-F08',
      employeeId: null,
      candidateId: null,
    })
  })

  it('cannot create while the case does not know the subject', () => {
    expect(blankCreateBody(target('employee'), { ...subject, employeeId: null })).toBeNull()
    expect(blankCreateBody(target('candidate'), { ...subject, candidateId: null })).toBeNull()
  })
})

describe('stepEntry', () => {
  it('has no button for a step that leads nowhere', () => {
    expect(stepEntry(step({ stepCode: 's' }), subject, true)).toBeNull()
  })

  it('lets the holder of an open step start it', () => {
    const link = { screen: 'employee', args: {} }
    expect(stepEntry(step({ stepCode: 's', link }), subject, true)).toEqual({
      kind: 'start',
      target: { kind: 'internal', href: '/xodimlar/person-1' },
    })
    expect(stepEntry(step({ stepCode: 's', link, state: 'Blocked' }), subject, true)?.kind).toBe('start')
  })

  it('offers someone else only a look, and nothing before the step’s turn', () => {
    const link = { screen: 'employee', args: {} }
    expect(stepEntry(step({ stepCode: 's', link, viewerHolds: false }), subject, true)?.kind).toBe('open')
    expect(stepEntry(step({ stepCode: 's', link, state: 'Done' }), subject, true)?.kind).toBe('open')
    expect(stepEntry(step({ stepCode: 's', link, state: 'Locked' }), subject, true)).toBeNull()
    expect(stepEntry(step({ stepCode: 's', link, state: 'Waiting' }), subject, true)).toBeNull()
  })

  it('creates a blank only for the holder, only when the subject is known', () => {
    const link = blankLink({ subject: 'employee' })
    expect(stepEntry(step({ stepCode: 's', link }), subject, true)?.kind).toBe('start')
    expect(stepEntry(step({ stepCode: 's', link, viewerHolds: false }), subject, true)).toBeNull()
    expect(stepEntry(step({ stepCode: 's', link, state: 'Done' }), subject, true)).toBeNull()
    expect(stepEntry(step({ stepCode: 's', link }), { ...subject, employeeId: null }, true)).toBeNull()
  })

  it('starts nothing without `mayStart`: a read-only user or a closed case', () => {
    const link = { screen: 'employee', args: {} }
    expect(stepEntry(step({ stepCode: 's', link }), subject, false)?.kind).toBe('open')
    expect(
      stepEntry(step({ stepCode: 's', link: blankLink({ subject: 'employee' }) }), subject, false),
    ).toBeNull()
  })

  it('opens a blank that already exists instead of starting it', () => {
    expect(
      stepEntry(
        step({ stepCode: 's', link: blankLink(), linkedBlankSubmissionId: 'sub-1' }),
        subject,
        true,
      ),
    ).toEqual({ kind: 'open', target: { kind: 'internal', href: '/blankalar/sub-1' } })
  })
})

describe('pendingPrereqTitles', () => {
  it('names only the prerequisites not yet finished', () => {
    const steps = [
      step({ stepCode: 'a', title: 'Birinchi', state: 'Done' }),
      step({ stepCode: 'b', title: 'Ikkinchi', state: 'Skipped' }),
      step({ stepCode: 'c', title: 'Uchinchi', state: 'Active' }),
    ]
    expect(
      pendingPrereqTitles(step({ stepCode: 'd', prereqs: ['a', 'b', 'c', 'gone'] }), steps),
    ).toEqual(['Uchinchi'])
  })
})
