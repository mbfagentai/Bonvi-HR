import { describe, expect, it } from 'vitest'

import type { ProcessLink, ProcessSubject } from '../api/processes-api'
import { resolveProcessLink } from './process-link'

const SUBJECT: ProcessSubject = {
  kind: 'Employment',
  name: 'Ali Valiyev',
  position: 'Operator',
  vacancyRequestId: 'vr-1',
  candidateId: 'c-1',
  employmentId: 'em-1',
  probationId: null,
  employeeId: 'e-1',
}

const EMPTY: ProcessSubject = {
  kind: 'None',
  name: null,
  position: null,
  vacancyRequestId: null,
  candidateId: null,
  employmentId: null,
  probationId: null,
  employeeId: null,
}

const link = (screen: string, args: Record<string, string> = {}): ProcessLink => ({ screen, args })

describe('resolveProcessLink', () => {
  it.each([
    ['vacancyRequests', '/vakansiya-sorovlari?id=vr-1'],
    ['candidate', '/nomzodlar/c-1'],
    ['candidateForm', '/nomzodlar/c-1'],
    ['employee', '/xodimlar/e-1'],
    ['employment', '/buyruqlar?q=Ali%20Valiyev'],
    ['probations', '/sinov'],
    ['attendanceMapping', '/verifikatsiya/moslashtirish'],
    ['attendanceSchedules', '/verifikatsiya/jadvallar'],
  ])('sends %s to its screen', (screen, href) => {
    expect(resolveProcessLink(link(screen), SUBJECT, null)).toEqual({ kind: 'internal', href })
  })

  it('opens the existing blank, otherwise asks to create one', () => {
    const blank = link('blank', { template: 'HR-F06', subject: 'employee' })

    expect(resolveProcessLink(blank, SUBJECT, 'b-1')).toEqual({
      kind: 'internal',
      href: '/blankalar/b-1',
    })
    expect(resolveProcessLink(blank, SUBJECT, null)).toEqual({
      kind: 'createBlank',
      template: 'HR-F06',
      subject: 'employee',
    })
  })

  it('finds nothing to open without the subject it needs', () => {
    expect(resolveProcessLink(link('candidate'), EMPTY, null)).toEqual({ kind: 'none' })
    expect(resolveProcessLink(link('employee'), EMPTY, null)).toEqual({ kind: 'none' })
    expect(resolveProcessLink(link('blank'), EMPTY, null)).toEqual({ kind: 'none' })
  })

  it('still opens the plain list when only the list applies', () => {
    expect(resolveProcessLink(link('vacancyRequests'), EMPTY, null)).toEqual({
      kind: 'internal',
      href: '/vakansiya-sorovlari',
    })
    expect(resolveProcessLink(link('employment'), EMPTY, null)).toEqual({
      kind: 'internal',
      href: '/buyruqlar',
    })
  })

  it('opens only http(s) addresses externally', () => {
    expect(resolveProcessLink(link('external', { url: 'https://mehnat.uz' }), EMPTY, null)).toEqual({
      kind: 'external',
      url: 'https://mehnat.uz',
    })
    expect(
      resolveProcessLink(link('external', { url: 'javascript:alert(1)' }), EMPTY, null),
    ).toEqual({ kind: 'none' })
  })

  it('treats none and unknown screens alike', () => {
    expect(resolveProcessLink(link('none'), SUBJECT, null)).toEqual({ kind: 'none' })
    expect(resolveProcessLink(link('futureScreen'), SUBJECT, null)).toEqual({ kind: 'none' })
  })
})
