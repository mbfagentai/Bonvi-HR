import { describe, expect, it } from 'vitest'

import type { ProcessTemplate, ProcessTemplateStep } from '../api/processes-api'
import { officeRolesOf, sortHolidays } from './process-settings'

function step(role: string, fallbackRole: string | null = null): ProcessTemplateStep {
  return {
    id: role,
    title: role,
    column: 'c',
    role,
    fallbackRole,
    blank: null,
    signature: null,
    kind: 'Manual',
    prereqs: [],
    optional: false,
    skippable: false,
    legacySkippable: false,
    gates: [],
    manual: null,
    guide: [],
    sourceRef: null,
    link: { screen: 'none', args: {} },
  }
}

function template(steps: ProcessTemplateStep[]): ProcessTemplate {
  return { code: 't', version: '1', title: 'T', source: 's', subjectKind: 'None', board: [], steps }
}

describe('officeRolesOf', () => {
  it('collects each role once, fallbacks included, across templates', () => {
    const roles = officeRolesOf([
      template([step('hr1', 'hr3'), step('hr1')]),
      template([step('safetyEngineer', 'hr1')]),
    ])
    expect(roles).toEqual(['hr1', 'hr3', 'safetyEngineer'])
  })

  it('leaves out the roles nobody assigns by hand', () => {
    expect(officeRolesOf([template([step('system'), step('departmentHead'), step('subjectEmployee')])])).toEqual([])
  })
})

describe('sortHolidays', () => {
  it('sorts by date without touching the input', () => {
    const input = [
      { date: '2027-01-01', kind: 'Holiday' as const, title: 'b' },
      { date: '2026-03-21', kind: 'WorkingDay' as const, title: 'a' },
    ]
    expect(sortHolidays(input).map((row) => row.date)).toEqual(['2026-03-21', '2027-01-01'])
    expect(input[0]?.date).toBe('2027-01-01')
  })
})
