import { describe, expect, it } from 'vitest'

import type { ProcessGateOverrideRow } from '../api/processes-api'
import {
  canTakeOverride,
  isOverrideReasonValid,
  openOverrideFor,
  overrideStanding,
} from './process-override'

const NOW = Date.parse('2026-11-05T12:00:00Z')

function override(partial: Partial<ProcessGateOverrideRow>): ProcessGateOverrideRow {
  return {
    id: 'o1',
    gateCode: 'Q03',
    caseId: 'c1',
    stepCode: 'r01-9',
    employmentId: null,
    candidateId: null,
    forUserId: null,
    forUserName: null,
    reason: 'because of the reason',
    grantedBy: 'u1',
    grantedByName: 'Ali',
    grantedAt: '2026-11-05T10:00:00Z',
    expiresAt: '2026-11-06T10:00:00Z',
    consumedAt: null,
    consumedOperation: null,
    ...partial,
  }
}

describe('overrideStanding', () => {
  it('calls a consumed grant used even after it would have lapsed', () => {
    const row = override({ consumedAt: '2026-11-05T11:00:00Z', expiresAt: '2026-11-05T11:30:00Z' })
    expect(overrideStanding(row, NOW)).toBe('used')
  })

  it('tells an open grant from a lapsed one', () => {
    expect(overrideStanding(override({}), NOW)).toBe('open')
    expect(overrideStanding(override({ expiresAt: '2026-11-05T11:59:59Z' }), NOW)).toBe('expired')
  })
})

describe('openOverrideFor', () => {
  const task = { caseId: 'c1', gateCode: 'Q03' }

  it('finds the open grant of the same case and gate', () => {
    const row = override({})
    expect(openOverrideFor(task, [row], NOW)).toBe(row)
  })

  it('ignores another gate, another case, a spent grant and a lapsed one', () => {
    expect(
      openOverrideFor(
        task,
        [
          override({ gateCode: 'Q04' }),
          override({ caseId: 'c2' }),
          override({ consumedAt: '2026-11-05T11:00:00Z' }),
          override({ expiresAt: '2026-11-05T11:00:00Z' }),
        ],
        NOW,
      ),
    ).toBeUndefined()
  })

  it('never matches the direct-hire grant, which has no case', () => {
    expect(openOverrideFor(task, [override({ caseId: null, gateCode: 'Q01' })], NOW)).toBeUndefined()
  })
})

describe('canTakeOverride', () => {
  it('needs a blocked step with a gate', () => {
    expect(canTakeOverride({ state: 'Blocked', gateCode: 'Q04' })).toBe(true)
    expect(canTakeOverride({ state: 'Blocked', gateCode: null })).toBe(false)
    expect(canTakeOverride({ state: 'Active', gateCode: 'Q04' })).toBe(false)
  })
})

describe('isOverrideReasonValid', () => {
  it('counts the trimmed text', () => {
    expect(isOverrideReasonValid('1234567890')).toBe(true)
    expect(isOverrideReasonValid('  123456789  ')).toBe(false)
    expect(isOverrideReasonValid('x'.repeat(1001))).toBe(false)
  })
})
