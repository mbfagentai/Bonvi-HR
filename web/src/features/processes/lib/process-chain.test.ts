import { describe, expect, it } from 'vitest'

import type { ProcessCaseDetail, ProcessCaseStep, ProcessSubject } from '../api/processes-api'
import {
  chainBlanks,
  chainColumns,
  chainPersonCase,
  chainSteps,
  defaultChainStepCode,
  latestInChain,
} from './process-chain'

const holder = { role: 'hr1', employeeId: null, employeeName: null, users: [], unassigned: false }

function step(stepCode: string, column: string, partial: Partial<ProcessCaseStep> = {}): ProcessCaseStep {
  return {
    stepCode,
    orderNo: 1,
    title: stepCode,
    column,
    kind: 'Manual',
    state: 'Locked',
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
    viewerHolds: false,
    canComplete: false,
    canSkip: false,
    canUndo: false,
    ...partial,
  }
}

function subject(kind: ProcessSubject['kind'], name: string | null): ProcessSubject {
  return {
    kind,
    name,
    position: null,
    vacancyRequestId: null,
    candidateId: null,
    employmentId: null,
    probationId: null,
    employeeId: null,
  }
}

function caseOf(
  id: string,
  steps: ProcessCaseStep[],
  { subject: subjectOf, ...partial }: Partial<ProcessCaseDetail> = {},
): ProcessCaseDetail {
  const columns = [...new Set(steps.map((row) => row.column))].map((key) => ({ key, title: key }))

  const detail: ProcessCaseDetail = {
    id,
    processCode: 'hire-candidate',
    processTitle: id,
    source: null,
    status: 'Active',
    outcome: null,
    legacy: false,
    departmentId: null,
    departmentName: null,
    parentCaseId: null,
    anchorDate: null,
    startedOn: '2026-11-01',
    completedAt: null,
    cancelledAt: null,
    cancelReason: null,
    version: 1,
    columns,
    steps,
    blanks: [],
    ...partial,
  }
  if (subjectOf !== undefined) detail.subject = subjectOf
  return detail
}

const finished = { state: 'Done' } as const
const running = { state: 'Active' } as const

describe('chainColumns', () => {
  it('keeps the chain order and tells apart columns that two templates name alike', () => {
    const columns = chainColumns([
      caseOf('request', [step('r1', 'a')]),
      caseOf('candidate', [step('c1', 'a'), step('c2', 'b')]),
    ])

    expect(columns.map((column) => column.key)).toEqual(['request:a', 'candidate:a', 'candidate:b'])
    expect(columns.map((column) => column.title)).toEqual(['a', 'a', 'b'])
  })
})

describe('chainSteps', () => {
  it('hands every step to the case it belongs to', () => {
    const request = caseOf('request', [step('r1', 'a')], { version: 4 })
    const candidate = caseOf('candidate', [step('c1', 'a')], { version: 9 })

    expect(chainSteps([request, candidate]).map(({ detail, step: row }) => [row.stepCode, detail.id, detail.version])).toEqual([
      ['r1', 'request', 4],
      ['c1', 'candidate', 9],
    ])
  })

  it('reads as the stepper draws it, column by column', () => {
    const detail = caseOf('one', [
      step('later', 'b', { orderNo: 1 }),
      step('sooner', 'a', { orderNo: 2 }),
    ], { columns: [{ key: 'a', title: 'a' }, { key: 'b', title: 'b' }] })

    expect(chainSteps([detail]).map(({ step: row }) => row.stepCode)).toEqual(['sooner', 'later'])
  })
})

describe('defaultChainStepCode', () => {
  it('opens on the first step the viewer can act on, across the cases', () => {
    const details = [
      caseOf('request', [step('r1', 'a', finished), step('r2', 'a', running)]),
      caseOf('candidate', [step('c1', 'a', { ...running, viewerHolds: true })]),
      caseOf('formalise', [step('f1', 'a', { ...running, viewerHolds: true })]),
    ]

    expect(defaultChainStepCode(details, 'formalise')).toBe('c1')
  })

  it('without anything to act on, prefers what moves on the case that was opened', () => {
    const details = [
      caseOf('request', [step('r1', 'a', running)]),
      caseOf('candidate', [step('c1', 'a', running)]),
    ]

    expect(defaultChainStepCode(details, 'candidate')).toBe('c1')
    expect(defaultChainStepCode(details, 'other')).toBe('r1')
  })

  it('offers no open step of a case that has ended', () => {
    const details = [
      caseOf('request', [step('r1', 'a', { ...running, viewerHolds: true })], { status: 'Cancelled' }),
      caseOf('candidate', [step('c1', 'a', running)]),
    ]

    expect(defaultChainStepCode(details, 'candidate')).toBe('c1')
  })

  it('counts a blocked step as open', () => {
    const details = [caseOf('one', [step('s1', 'a', { state: 'Blocked', viewerHolds: true })])]

    expect(defaultChainStepCode(details, 'one')).toBe('s1')
  })

  it('opens a finished chain on its last finished step, and a chain still ahead on its first', () => {
    expect(
      defaultChainStepCode(
        [caseOf('one', [step('s1', 'a', finished)]), caseOf('two', [step('s2', 'a', finished)])],
        'one',
      ),
    ).toBe('s2')
    expect(defaultChainStepCode([caseOf('one', [step('s1', 'a'), step('s2', 'a')])], 'one')).toBe('s1')
  })

  it('has nothing to open on without steps', () => {
    expect(defaultChainStepCode([caseOf('one', [])], 'one')).toBeUndefined()
  })
})

describe('chainPersonCase', () => {
  const request = caseOf('request', [], { subject: subject('VacancyRequest', 'Payvandchi') })
  const candidate = caseOf('candidate', [], { subject: subject('Candidate', 'Karimov Aziz') })
  const formalise = caseOf('formalise', [], { subject: subject('Employment', 'Karimov Aziz Olim') })

  it('prefers the latest stage that names a person over the request', () => {
    expect(chainPersonCase([request, candidate])?.id).toBe('candidate')
    expect(chainPersonCase([request, candidate, formalise])?.id).toBe('formalise')
  })

  it('skips a stage that does not know the name yet', () => {
    const nameless = caseOf('formalise', [], { subject: subject('Employment', null) })

    expect(chainPersonCase([request, candidate, nameless])?.id).toBe('candidate')
  })

  it('falls back to the request, then to the last case', () => {
    expect(chainPersonCase([request])?.id).toBe('request')
    expect(chainPersonCase([caseOf('a', []), caseOf('b', [])])?.id).toBe('b')
  })
})

describe('latestInChain', () => {
  const details = [
    caseOf('first', [], { departmentName: 'Metan' }),
    caseOf('second', [], { departmentName: null }),
  ]

  it('takes the latest stage that knows, and null when none does', () => {
    expect(latestInChain(details, (detail) => detail.departmentName)).toBe('Metan')
    expect(latestInChain(details, (detail) => detail.anchorDate)).toBeNull()
  })
})

describe('chainBlanks', () => {
  const blank = {
    id: 'b1',
    templateCode: 'HR-F01',
    templateTitle: null,
    number: null,
    status: 'Draft',
    dueDate: null,
    completedAt: null,
  }

  it('lists a blank linked from two stages once', () => {
    const details = [
      caseOf('a', [], { blanks: [blank] }),
      caseOf('b', [], { blanks: [blank, { ...blank, id: 'b2' }] }),
    ]

    expect(chainBlanks(details).map((row) => row.id)).toEqual(['b1', 'b2'])
  })
})
