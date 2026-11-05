import { describe, expect, it } from 'vitest'

import { landingPath } from './landing'
import {
  hasBoardListFilters,
  validateProcessCaseSearch,
  validateProcessesSearch,
} from './processes-search'

describe('validateProcessesSearch on the board tab', () => {
  it('keeps valid parameters', () => {
    const search = {
      tab: 'board',
      view: 'list',
      process: 'dismissal',
      status: 'Completed',
      department: 'd1',
      employee: 'e1',
      candidate: 'c1',
      holderRole: 'hr2',
      late: true,
      from: '2026-11-01',
      to: '2026-11-30',
      q: 'Aliyev',
      page: 3,
    }
    expect(validateProcessesSearch(search)).toEqual(search)
  })

  it('drops what it cannot read, leaving no empty keys behind', () => {
    const search = validateProcessesSearch({
      tab: 'board',
      view: 'board',
      process: '  ',
      status: 'Nonsense',
      late: 'yes',
      from: '01.11.2026',
      page: 1,
    })
    expect(Object.keys(search)).toEqual(['tab'])
  })

  it('keeps the board parameters out of the other tabs', () => {
    expect(validateProcessesSearch({ tab: 'late', view: 'list', q: 'x' })).toEqual({ tab: 'late' })
    expect(validateProcessesSearch({ view: 'list', process: 'dismissal' })).toEqual({})
  })
})

describe('hasBoardListFilters', () => {
  it('does not count the process, the view or the page', () => {
    expect(hasBoardListFilters({ view: 'list', process: 'dismissal', page: 2 })).toBe(false)
  })

  it('counts a filter, including an id with no picker on the screen', () => {
    expect(hasBoardListFilters({ late: true })).toBe(true)
    expect(hasBoardListFilters({ employee: 'e1' })).toBe(true)
  })
})

describe('validateProcessCaseSearch', () => {
  it('keeps a step and drops anything else', () => {
    expect(validateProcessCaseSearch({ step: 'ex-8', other: 1 })).toEqual({ step: 'ex-8' })
    expect(validateProcessCaseSearch({ step: '' })).toEqual({})
    expect(validateProcessCaseSearch({})).toEqual({})
  })
})

describe('landingPath', () => {
  it('sends processes.work to the tasks', () => {
    expect(landingPath({ permissions: ['processes.view', 'processes.work'] })).toBe(
      '/mening-ishlarim',
    )
  })

  it('leaves everybody else on the dashboard, even with processes.view alone', () => {
    expect(landingPath({ permissions: ['employees.view'] })).toBe('/')
    expect(landingPath({ permissions: ['processes.view'] })).toBe('/')
    expect(landingPath({ permissions: [] })).toBe('/')
  })
})

describe('validateProcessesSearch', () => {
  it('keeps the two named tabs', () => {
    expect(validateProcessesSearch({ tab: 'board' })).toEqual({ tab: 'board' })
    expect(validateProcessesSearch({ tab: 'late' })).toEqual({ tab: 'late' })
  })

  it('opens the first tab for anything else', () => {
    expect(validateProcessesSearch({ tab: 'my' })).toEqual({})
    expect(validateProcessesSearch({ tab: 'nonsense' })).toEqual({})
    expect(validateProcessesSearch({ tab: 3 })).toEqual({})
    expect(validateProcessesSearch({})).toEqual({})
  })
})
