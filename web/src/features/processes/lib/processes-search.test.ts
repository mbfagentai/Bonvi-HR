import { describe, expect, it } from 'vitest'

import { landingPath } from './landing'
import { validateProcessesSearch } from './processes-search'

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
