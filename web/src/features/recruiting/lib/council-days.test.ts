import { describe, expect, it } from 'vitest'

import { defaultCouncilDay, tashkentToday } from './council-days'

const day = (date: string, invited = 1) => ({ date, invited, decided: 0 })

describe('defaultCouncilDay', () => {
  it('opens today when today has invitations', () => {
    expect(defaultCouncilDay([day('2026-11-01'), day('2026-11-05'), day('2026-11-07')], '2026-11-05')).toBe('2026-11-05')
  })

  it('opens the nearest coming day, skipping one with only a saved sitting', () => {
    expect(defaultCouncilDay([day('2026-11-06', 0), day('2026-11-07', 31)], '2026-11-05')).toBe('2026-11-07')
  })

  it('falls back to the latest past day, then to today', () => {
    expect(defaultCouncilDay([day('2026-10-01'), day('2026-10-20')], '2026-11-05')).toBe('2026-10-20')
    expect(defaultCouncilDay([], '2026-11-05')).toBe('2026-11-05')
  })
})

describe('tashkentToday', () => {
  it('turns over at midnight Tashkent time', () => {
    expect(tashkentToday(new Date('2026-11-05T18:59:00Z'))).toBe('2026-11-05')
    expect(tashkentToday(new Date('2026-11-05T19:00:00Z'))).toBe('2026-11-06')
  })
})
