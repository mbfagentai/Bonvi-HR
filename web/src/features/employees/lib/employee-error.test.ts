import { AxiosError, type AxiosResponse } from 'axios'
import { describe, expect, it } from 'vitest'

import { processGateBlock, terminationWarning } from './employee-error'

function failure(status: number, data: unknown) {
  return new AxiosError('failed', undefined, undefined, undefined, {
    status,
    data,
  } as AxiosResponse)
}

const flag = failure(409, { title: 'xodim.qora_royxatda', warnings: ['xodim.qora_royxatda', 'xodim.nazoratda'] })
const gate = failure(409, {
  title: 'jarayon.akt_yopilmagan',
  warnings: ['jarayon.akt_yopilmagan', 'jarayon.chiqish_suhbati_yoq'],
})

describe('terminationWarning', () => {
  it('offers a confirmation for a flag, a code it does not know included', () => {
    expect(terminationWarning(flag)).toMatchObject({
      code: 'xodim.qora_royxatda',
      extra: ['xodim.nazoratda'],
    })
    expect(terminationWarning(failure(409, { title: 'sinov.yangi_qoida' }))?.code).toBe('sinov.yangi_qoida')
  })

  it('offers none for a process gate, because confirm cannot pass it', () => {
    expect(terminationWarning(gate)).toBeNull()
  })
})

describe('processGateBlock', () => {
  it('reads the reason and everything else that is missing', () => {
    expect(processGateBlock(gate)).toEqual({
      code: 'jarayon.akt_yopilmagan',
      extra: ['jarayon.chiqish_suhbati_yoq'],
    })
  })

  it('is nothing for a flag, another status or no error', () => {
    expect(processGateBlock(flag)).toBeNull()
    expect(processGateBlock(failure(400, { title: 'jarayon.kiritish_notogri' }))).toBeNull()
    expect(processGateBlock(null)).toBeNull()
  })
})
