import { describe, expect, it } from 'vitest'

import type { EmploymentSummary } from '@/features/employees/api/employees-api'

import { employmentOptionLabel, isBeforeHire, openEmployments } from './process-start'

function employment(partial: Partial<EmploymentSummary>): EmploymentSummary {
  return {
    id: 'e1',
    legalEntity: { id: 'l1', name: 'Bonvi' },
    department: { id: 'd1', name: 'Metan zavodi' },
    position: { id: 'p1', name: 'Payvandchi' },
    hireDate: '2024-03-01',
    status: 'active',
    rate: 1,
    isPrimary: false,
    salary: null,
    hireOrderNo: null,
    hireOrderDate: null,
    terminationDate: null,
    terminationReasonName: null,
    probationEndDate: null,
    probationId: null,
    probationEndedByLeaving: false,
    needsReview: false,
    duties: [],
    totalPay: null,
    ...partial,
  }
}

describe('openEmployments', () => {
  it('keeps active and probation, drops ended', () => {
    const open = openEmployments([
      employment({ id: 'a', status: 'active' }),
      employment({ id: 'p', status: 'probation' }),
      employment({ id: 't', status: 'terminated' }),
    ])
    expect(open.map((item) => item.id)).toEqual(['a', 'p'])
  })

  it('puts the main job first and leaves the input alone', () => {
    const input = [employment({ id: 'second' }), employment({ id: 'main', isPrimary: true })]
    expect(openEmployments(input).map((item) => item.id)).toEqual(['main', 'second'])
    expect(input.map((item) => item.id)).toEqual(['second', 'main'])
  })
})

describe('employmentOptionLabel', () => {
  it('shows position and department', () => {
    const one = employment({})
    expect(employmentOptionLabel(one, [one])).toBe('Payvandchi · Metan zavodi')
  })

  it('adds the legal entity only when two jobs read the same', () => {
    const a = employment({ id: 'a', legalEntity: { id: 'l1', name: 'Bonvi' } })
    const b = employment({ id: 'b', legalEntity: { id: 'l2', name: 'Propan' } })
    expect(employmentOptionLabel(a, [a, b])).toBe('Payvandchi · Metan zavodi · Bonvi')
    expect(employmentOptionLabel(b, [a, b])).toBe('Payvandchi · Metan zavodi · Propan')
  })

  it('falls back to the legal entity, then a dash, when no place is known', () => {
    const noPlace = employment({ position: null, department: null })
    expect(employmentOptionLabel(noPlace, [noPlace])).toBe('Bonvi')
    const nothing = employment({ position: null, department: null, legalEntity: null })
    expect(employmentOptionLabel(nothing, [nothing])).toBe('—')
  })
})

describe('isBeforeHire', () => {
  it('refuses only a day strictly before the hire date', () => {
    expect(isBeforeHire('2024-02-29', '2024-03-01')).toBe(true)
    expect(isBeforeHire('2024-03-01', '2024-03-01')).toBe(false)
    expect(isBeforeHire('2024-03-02', '2024-03-01')).toBe(false)
  })

  it('says nothing until both dates are known', () => {
    expect(isBeforeHire('', '2024-03-01')).toBe(false)
    expect(isBeforeHire('2024-02-29', undefined)).toBe(false)
    expect(isBeforeHire('2024-02-29', '')).toBe(false)
  })
})
