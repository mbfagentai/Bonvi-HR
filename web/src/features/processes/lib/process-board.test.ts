import { describe, expect, it } from 'vitest'

import type { ProcessTemplate } from '../api/processes-api'
import {
  boardSearchToCaseFilters,
  clearBoardListFilters,
  hiddenBoardFilterCount,
  sortProcessTemplates,
} from './process-board'

function template(code: string): ProcessTemplate {
  return {
    code,
    version: '1',
    title: code,
    source: '',
    subjectKind: 'None',
    board: [],
    steps: [],
  }
}

describe('sortProcessTemplates', () => {
  it('follows the hiring order and puts an unknown process last', () => {
    const sorted = sortProcessTemplates([
      template('dismissal'),
      template('future'),
      template('hire-request'),
      template('probation'),
    ])
    expect(sorted.map((item) => item.code)).toEqual([
      'hire-request',
      'probation',
      'dismissal',
      'future',
    ])
  })
})

describe('boardSearchToCaseFilters', () => {
  it('takes the filters from the address and the process from the caller', () => {
    expect(
      boardSearchToCaseFilters(
        { process: 'stale', view: 'list', page: 3, status: 'Active', late: true, q: 'Vali' },
        'dismissal',
      ),
    ).toEqual({ process: 'dismissal', status: 'Active', late: true, q: 'Vali' })
  })
})

describe('clearBoardListFilters', () => {
  it('keeps the view and the process', () => {
    expect(
      clearBoardListFilters({
        view: 'list',
        process: 'dismissal',
        status: 'Active',
        employee: 'e1',
        late: true,
        q: 'Vali',
        page: 2,
      }),
    ).toEqual({ view: 'list', process: 'dismissal' })
  })
})

describe('hiddenBoardFilterCount', () => {
  it('leaves out what is always in view', () => {
    expect(hiddenBoardFilterCount({ q: 'Vali', status: 'Active', process: 'dismissal' })).toBe(0)
  })

  it('counts a date range once', () => {
    expect(
      hiddenBoardFilterCount({
        department: 'd1',
        holderRole: 'hr1',
        late: true,
        from: '2026-11-01',
        to: '2026-11-30',
      }),
    ).toBe(4)
  })
})
