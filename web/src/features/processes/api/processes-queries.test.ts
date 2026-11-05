import { MutationObserver, QueryClient, QueryObserver } from '@tanstack/react-query'
import { describe, expect, it, vi } from 'vitest'

import { completeStepOptions, processKeys } from './processes-queries'
import { completeProcessStep, type ProcessCaseDetail } from './processes-api'

vi.mock('./processes-api', () => ({ completeProcessStep: vi.fn() }))

const target = { caseId: 'c1', stepCode: 's1', body: { version: 3, inputs: {} } }

describe('completeStepOptions', () => {
  it('stays pending after the 200 until the case has reloaded', async () => {
    vi.mocked(completeProcessStep).mockResolvedValue({} as ProcessCaseDetail)

    const client = new QueryClient()
    let finishReload: (version: number) => void = () => undefined
    const queryFn = vi
      .fn<() => Promise<number>>()
      .mockResolvedValueOnce(3)
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            finishReload = resolve
          }),
      )
    const reader = new QueryObserver(client, { queryKey: processKeys.case('c1'), queryFn })
    const stop = reader.subscribe(() => undefined)
    await vi.waitFor(() => expect(reader.getCurrentResult().data).toBe(3))

    const writer = new MutationObserver(client, completeStepOptions(client))
    const done = writer.mutate(target)

    await vi.waitFor(() => expect(queryFn).toHaveBeenCalledTimes(2))
    expect(completeProcessStep).toHaveBeenCalledTimes(1)
    expect(writer.getCurrentResult().isPending).toBe(true)

    finishReload(4)
    await done

    expect(writer.getCurrentResult().isSuccess).toBe(true)
    expect(client.getQueryData(processKeys.case('c1'))).toBe(4)
    stop()
  })

  it('still ends when the reload fails, so the form is not stuck on a spinner', async () => {
    vi.mocked(completeProcessStep).mockResolvedValue({} as ProcessCaseDetail)

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const queryFn = vi
      .fn<() => Promise<number>>()
      .mockResolvedValueOnce(3)
      .mockRejectedValueOnce(new Error('offline'))
    const reader = new QueryObserver(client, { queryKey: processKeys.case('c2'), queryFn })
    const stop = reader.subscribe(() => undefined)
    await vi.waitFor(() => expect(reader.getCurrentResult().data).toBe(3))

    const writer = new MutationObserver(client, completeStepOptions(client))
    await writer.mutate({ ...target, caseId: 'c2' })

    expect(writer.getCurrentResult().isSuccess).toBe(true)
    expect(client.getQueryData(processKeys.case('c2'))).toBe(3)
    stop()
  })
})
