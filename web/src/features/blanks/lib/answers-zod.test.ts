import { describe, expect, it } from 'vitest'

import { HR_F06_EXAMPLE } from '../components/renderer/renderer-demo-definition'
import { resolveDateBound, validateAnswers } from './answers-zod'
import { computeVisibility, dropHidden, evalCondition, isEmptyValue } from './conditions'
import type { Answers, BlankDefinition, Condition, JsonValue } from './definition-types'

const TODAY = '2026-11-03'

describe('conditions — SPEC §4.4', () => {
  const answers: Answers = {
    initiative: { value: 'employee' },
    reasons: [{ value: 'k01' }, { value: 'k03', extra: 'x' }],
    mentor: { id: 'e1', label: 'Ali' },
    note: '  ',
    hasBonus: false,
  }
  const lookup = (id: string) => answers[id]
  const check = (c: Condition) => evalCondition(c, lookup)

  it('eq compares choice .value, ref .id, multi any-of', () => {
    expect(check({ field: 'initiative', eq: 'employee' })).toBe(true)
    expect(check({ field: 'initiative', eq: 'employer' })).toBe(false)
    expect(check({ field: 'mentor', eq: 'e1' })).toBe(true)
    expect(check({ field: 'reasons', eq: 'k03' })).toBe(true)
    expect(check({ field: 'hasBonus', eq: false })).toBe(true)
  })

  it('in / filled / all / any / not', () => {
    expect(check({ field: 'initiative', in: ['a', 'employee'] })).toBe(true)
    expect(check({ field: 'note', filled: true })).toBe(false)
    expect(check({ field: 'note', filled: false })).toBe(true)
    expect(check({ field: 'missing', filled: false })).toBe(true)
    expect(check({ all: [{ field: 'mentor', filled: true }, { field: 'hasBonus', eq: false }] })).toBe(true)
    expect(check({ any: [{ field: 'note', filled: true }, { field: 'hasBonus', eq: true }] })).toBe(false)
    expect(check({ not: { field: 'note', filled: true } })).toBe(true)
  })

  it('isEmptyValue: null, "", [] are empty; false and 0 are not', () => {
    expect([null, undefined, '', ' ', []].every((v) => isEmptyValue(v))).toBe(true)
    expect([false, 0, {}].some((v) => isEmptyValue(v))).toBe(false)
  })

  it('a hidden field counts as empty for later conditions; values are dropped', () => {
    const definition: BlankDefinition = {
      ...HR_F06_EXAMPLE,
      sections: [
        {
          id: 'a',
          title: 'a',
          fields: [
            { id: 'kind', type: 'yesno', label: 'k' },
            { id: 'detail', type: 'text', label: 'd', when: { field: 'kind', eq: true } },
            { id: 'more', type: 'text', label: 'm', when: { field: 'detail', filled: true } },
          ],
        },
        { id: 'b', title: 'b', when: { field: 'kind', eq: true }, fields: [{ id: 'x', type: 'text', label: 'x' }] },
      ],
    }
    const v = computeVisibility(definition, { kind: false, detail: 'stale', more: 'stale', x: 'y' })
    expect([...v.hiddenFields].sort()).toEqual(['detail', 'more', 'x'])
    expect([...v.hiddenSections]).toEqual(['b'])
    expect(dropHidden({ kind: false, detail: 'stale', x: 'y' }, v)).toEqual({ kind: false })
  })
})

describe('answers validation (zod) — §4.3 / §5', () => {
  const save = (answers: Answers, sections?: string[]) =>
    validateAnswers(HR_F06_EXAMPLE, answers, { requireFilled: false, today: TODAY, ...(sections ? { sections } : {}) })
  const submit = (answers: Answers, sections?: string[]) =>
    validateAnswers(HR_F06_EXAMPLE, answers, { requireFilled: true, today: TODAY, ...(sections ? { sections } : {}) })

  it('save mode checks types only; submit adds required', () => {
    expect(save({}, ['hdr'])).toEqual({})
    const errors = submit({}, ['hdr'])
    expect(Object.keys(errors).sort()).toEqual(['hireDate', 'mentor'])
    expect(errors['hireDate']?.key).toBe('required')
  })

  it('readOnly fields are never required (prefill may be missing)', () => {
    expect(submit({}, ['hdr'])['employee']).toBeUndefined()
  })

  it('only the given sections are validated', () => {
    expect(submit({}, ['d7'])).toEqual({ day7Percent: { key: 'required', params: {} } })
  })

  it('percent: 0–100, one decimal', () => {
    expect(save({ day7Percent: 101 })['day7Percent']?.key).toBe('percentRange')
    expect(save({ day7Percent: 70.25 })['day7Percent']?.key).toBe('percentDecimals')
    expect(save({ day7Percent: 70.5 })).toEqual({})
  })

  it('choice: unknown option rejected; required extra checked on submit', () => {
    expect(save({ day30Decision: { value: 'nope' } })['day30Decision.value']?.key).toBe('invalidOption')
    expect(save({ day30Decision: { value: 'stop' } })).toEqual({})
    expect(submit({ day30Decision: { value: 'stop' } }, ['d30'])['day30Decision.extra']?.key).toBe('extraRequired')
    expect(submit({ day30Decision: { value: 'stop', extra: 'Sabab' } }, ['d30'])).toEqual({})
  })

  it('multi: maxSelected, distinct, per-item extra', () => {
    const tooMany: JsonValue = [{ value: 'k01' }, { value: 'k02' }, { value: 'k03', extra: 'x' }]
    expect(save({ reasons: tooMany })['reasons']).toEqual({ key: 'tooManySelected', params: { max: 2 } })
    expect(save({ reasons: [{ value: 'k01' }, { value: 'k01' }] })['reasons']?.key).toBe('duplicate')
    expect(submit({ reasons: [{ value: 'k01' }, { value: 'k03' }] }, ['demoExtras'])['reasons[1].extra']?.key).toBe(
      'extraRequired',
    )
  })

  it('text: length after trim, control and bidi characters rejected', () => {
    expect(save({ position: 'x'.repeat(201) })['position']).toEqual({ key: 'tooLong', params: { max: 200 } })
    expect(save({ position: `ab‮cd` })['position']?.key).toBe('invalidChars')
    expect(save({ position: `ab\ncd` })['position']?.key).toBe('invalidChars')
  })

  it('table: cell paths, maxRows, fixed rows in order', () => {
    const errors = save({ questions: [{ date: '2026-13-01' }, { date: '2026-11-01', question: 'ok\nmulti' }] })
    expect(errors).toEqual({ 'questions[0].date': { key: 'invalidDate', params: {} } })
    expect(save({ questions: Array.from({ length: 51 }, () => ({})) })['questions']?.key).toBe('tooManyRows')
    expect(save({ criteria: [{ _row: 'c2' }, { _row: 'c1' }] })['criteria']?.key).toBe('fixedRows')
    expect(submit({ criteria: [{ _row: 'c1', score: 6 }, { _row: 'c2' }] }, ['demoExtras'])).toMatchObject({
      'criteria[0].score': { key: 'score' },
      'criteria[1].score': { key: 'required' },
    })
  })

  it('hidden fields (when false) are neither required nor validated', () => {
    expect(submit({ hasBonus: false, criteria: [{ _row: 'c1', score: 3 }, { _row: 'c2', score: 3 }] }, ['demoExtras'])).toEqual({})
    expect(submit({ hasBonus: true, criteria: [{ _row: 'c1', score: 3 }, { _row: 'c2', score: 3 }] }, ['demoExtras'])['bonus']?.key).toBe(
      'required',
    )
  })

  it('file: maxFiles', () => {
    expect(save({ evidence: Array.from({ length: 11 }, (_, i) => `a${i}`) })['evidence']).toEqual({
      key: 'tooManyFiles',
      params: { max: 10 },
    })
  })

  it('date bounds: today, today+N, today-N, ISO', () => {
    expect(resolveDateBound('today', TODAY)).toBe(TODAY)
    expect(resolveDateBound('today+30', TODAY)).toBe('2026-12-03')
    expect(resolveDateBound('today-3', TODAY)).toBe('2026-10-31')
    expect(resolveDateBound('2026-01-01', TODAY)).toBe('2026-01-01')
    expect(resolveDateBound('garbage', TODAY)).toBeUndefined()
  })
})
