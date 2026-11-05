import { beforeEach, describe, expect, it, vi } from 'vitest'

import { api } from '@/shared/api/http-client'

import {
  completeProcessStep,
  skipProcessStep,
  startProcessCase,
  undoProcessStep,
} from './processes-api'

vi.mock('@/shared/api/http-client', () => ({ api: { get: vi.fn(), post: vi.fn() } }))

const post = vi.mocked(api.post)

beforeEach(() => {
  post.mockReset()
  post.mockResolvedValue({ data: {} })
})

/** The bodies are P7's contract word for word: a renamed field would answer 400 on every write. */
describe('process write client', () => {
  it('done sends version and inputs, with a checklist as item: done | na', async () => {
    await completeProcessStep('c1', 'r02-1', {
      version: 7,
      inputs: { payment: 'cash', docs: { passport: 'done', diploma: 'na' } },
    })

    expect(post).toHaveBeenCalledWith('/api/processes/cases/c1/steps/r02-1/done', {
      version: 7,
      inputs: { payment: 'cash', docs: { passport: 'done', diploma: 'na' } },
    })
  })

  it('skip sends version and reason', async () => {
    await skipProcessStep('c1', 'r02-1', { version: 7, reason: 'Kerak emas' })

    expect(post).toHaveBeenCalledWith('/api/processes/cases/c1/steps/r02-1/skip', {
      version: 7,
      reason: 'Kerak emas',
    })
  })

  it('undo sends only the version', async () => {
    await undoProcessStep('c1', 'r02-1', { version: 7 })

    expect(post).toHaveBeenCalledWith('/api/processes/cases/c1/steps/r02-1/undo', { version: 7 })
  })

  it('start sends the dismissal, the employment, the last work day and the basis', async () => {
    const body = {
      processCode: 'dismissal',
      employmentId: 'e1',
      lastWorkDay: '2026-11-30',
      basis: 'Ariza',
    } as const

    await startProcessCase(body)

    expect(post).toHaveBeenCalledWith('/api/processes/cases', body)
  })

  it('escapes a step code in the address', async () => {
    await undoProcessStep('c1', 'a/b', { version: 1 })

    expect(post).toHaveBeenCalledWith('/api/processes/cases/c1/steps/a%2Fb/undo', { version: 1 })
  })
})
