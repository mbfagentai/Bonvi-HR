import { describe, expect, it } from 'vitest'

import { longDueDate, describeDue, shortDueDate, taskTone } from './process-due'

const TODAY = '2026-11-05'

describe('describeDue', () => {
  it('trusts the server count over the date', () => {
    expect(describeDue('2026-11-02', 3, TODAY)).toEqual({ kind: 'late', days: 3 })
  })

  it('names today and tomorrow across a month boundary', () => {
    expect(describeDue(TODAY, 0, TODAY)).toEqual({ kind: 'today' })
    expect(describeDue('2026-12-01', 0, '2026-11-30')).toEqual({ kind: 'tomorrow' })
  })

  it('gives a later date as is and tolerates no date', () => {
    expect(describeDue('2026-11-24', 0, TODAY)).toEqual({ kind: 'until', date: '2026-11-24' })
    expect(describeDue(null, 0, TODAY)).toEqual({ kind: 'none' })
  })

  it('treats a past date the server does not count as late as today', () => {
    expect(describeDue('2026-11-04', 0, TODAY)).toEqual({ kind: 'today' })
  })
})

describe('shortDueDate', () => {
  it('follows the locale date order', () => {
    expect(shortDueDate('2026-11-24', 'uz-Latn')).toBe('24.11')
    expect(shortDueDate('2026-11-24', 'en')).toBe('11-24')
  })
})

describe('taskTone', () => {
  it('lets a block outrank lateness', () => {
    expect(taskTone('Blocked', '2026-11-02', 3, TODAY)).toBe('blocked')
  })

  it('maps the due date to the stripe colour', () => {
    expect(taskTone('Active', '2026-11-02', 3, TODAY)).toBe('late')
    expect(taskTone('Active', TODAY, 0, TODAY)).toBe('today')
    expect(taskTone('Active', '2026-11-24', 0, TODAY)).toBe('normal')
    expect(taskTone('Active', null, 0, TODAY)).toBe('normal')
  })
})

describe('longDueDate', () => {
  it('writes the date the way each language says it', () => {
    expect(longDueDate('2026-11-05', 'uz-Latn')).toBe('5-noyabr')
    expect(longDueDate('2026-11-05', 'uz-Cyrl')).toBe('5-ноябр')
    expect(longDueDate('2026-11-05', 'ru')).toBe('5 ноября')
    expect(longDueDate('2026-11-05', 'en')).toBe('5 November')
  })

  it('shows an unreadable date as it came', () => {
    expect(longDueDate('soon', 'uz-Latn')).toBe('soon')
  })
})
