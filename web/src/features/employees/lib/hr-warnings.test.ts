import { describe, expect, it } from 'vitest'

import { readHrWarnings, warningsToShow } from './hr-warnings'

describe('readHrWarnings', () => {
  it('reads the code and the message of each warning', () => {
    expect(readHrWarnings([{ code: 'jarayon.akt_yopilmagan', message: 'Akt yopilmagan' }])).toEqual([
      { code: 'jarayon.akt_yopilmagan', message: 'Akt yopilmagan' },
    ])
  })

  it('falls back to the code when the message is missing', () => {
    expect(readHrWarnings([{ code: 'jarayon.akt_yopilmagan' }])).toEqual([
      { code: 'jarayon.akt_yopilmagan', message: 'jarayon.akt_yopilmagan' },
    ])
  })

  it('takes nothing from a reply without warnings or with broken items', () => {
    expect(readHrWarnings(undefined)).toEqual([])
    expect(readHrWarnings('x')).toEqual([])
    expect(readHrWarnings([null, { code: '' }, { code: 1 }, 'x'])).toEqual([])
  })
})

describe('warningsToShow', () => {
  const gate = { code: 'jarayon.akt_yopilmagan', message: 'a' }
  const flag = { code: 'xodim.nazoratda', message: 'b' }

  it('keeps everything when nothing was confirmed', () => {
    expect(warningsToShow([flag, gate], null)).toEqual([flag, gate])
  })

  it('drops the flags HR has just confirmed and keeps the gate warnings', () => {
    expect(warningsToShow([flag, gate], ['xodim.nazoratda'])).toEqual([gate])
  })
})
