import { afterEach, describe, expect, it, vi } from 'vitest'

import { councilAppClient } from './council-app-api'

describe('councilAppClient.candidate', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('gives the questionnaire card the values it translates, as the panel does', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        Response.json({
          id: 'a',
          questionnaire: {
            education: 'HigherComplete',
            maritalStatus: 'Married',
            russianLevel: 'UpperIntermediate',
            expectedSalary: 'From5To7Million',
            hasDriverLicense: false,
          },
        }),
      ),
    )

    const candidate = await councilAppClient('x').candidate('a')

    expect(candidate.questionnaire?.russianLevel).toBe('upperIntermediate')
    expect(candidate.questionnaire?.education).toBe('HigherComplete')
    expect(candidate.questionnaire?.expectedSalary).toBe('From5To7Million')
  })

  it('keeps a candidate with no questionnaire', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ id: 'a', questionnaire: null })))

    expect((await councilAppClient('x').candidate('a')).questionnaire).toBeNull()
  })
})
