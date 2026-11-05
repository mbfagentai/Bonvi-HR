import { describe, expect, it } from 'vitest'

import type { ProcessManualInput } from '../api/processes-api'
import {
  describeRecordedInputs,
  initialManualValues,
  isManualInputRequired,
  toManualInputs,
  validateManualValues,
} from './process-manual-form'

function input(partial: Partial<ProcessManualInput> & Pick<ProcessManualInput, 'id' | 'type'>) {
  return {
    label: partial.id,
    required: false,
    requiredWhen: null,
    options: [],
    min: null,
    max: null,
    ...partial,
  } satisfies ProcessManualInput
}

const options = [
  { value: 'a', label: 'Birinchi' },
  { value: 'b', label: 'Ikkinchi' },
]

describe('initialManualValues', () => {
  it('starts a checklist with no marks and everything else empty', () => {
    expect(
      initialManualValues([input({ id: 'x', type: 'Text' }), input({ id: 'y', type: 'Checklist' })]),
    ).toEqual({ x: '', y: {} })
  })
})

describe('isManualInputRequired', () => {
  const dependent = input({
    id: 'endDate',
    type: 'Date',
    requiredWhen: { input: 'term', fact: null, equalsValue: 'fixed' },
  })

  it('follows the input it depends on', () => {
    expect(isManualInputRequired(dependent, { term: 'fixed' })).toBe(true)
    expect(isManualInputRequired(dependent, { term: 'open' })).toBe(false)
  })

  it('leaves a condition on a server fact to the server', () => {
    const onFact = input({
      id: 'startDate',
      type: 'Date',
      requiredWhen: { input: null, fact: 'cand.offerBranch', equalsValue: 'accept' },
    })
    expect(isManualInputRequired(onFact, {})).toBe(false)
  })
})

describe('validateManualValues', () => {
  it('passes a filled form', () => {
    const inputs = [
      input({ id: 'x', type: 'Text', required: true }),
      input({ id: 'n', type: 'WholeNumber', required: true, min: 1, max: 5 }),
      input({ id: 'c', type: 'Choice', required: true, options }),
      input({ id: 'l', type: 'Checklist', required: true, options }),
    ]
    expect(
      validateManualValues(inputs, {
        x: 'matn',
        n: '3',
        c: 'b',
        l: { a: 'done', b: 'notNeeded' },
      }),
    ).toEqual({})
  })

  it('names a blank required field, treating spaces as blank', () => {
    expect(
      validateManualValues([input({ id: 'x', type: 'Text', required: true })], { x: '   ' }),
    ).toEqual({ x: 'required' })
  })

  it('lets an optional field stay empty', () => {
    expect(
      validateManualValues(
        [
          input({ id: 'n', type: 'WholeNumber', min: 1 }),
          input({ id: 'l', type: 'Checklist', options }),
        ],
        { n: '', l: {} },
      ),
    ).toEqual({})
  })

  it('wants every required checklist item marked, either way', () => {
    const list = input({ id: 'l', type: 'Checklist', required: true, options })
    expect(validateManualValues([list], { l: { a: 'done' } })).toEqual({ l: 'checklistIncomplete' })
    expect(validateManualValues([list], { l: { a: 'notNeeded', b: 'notNeeded' } })).toEqual({})
  })

  it('checks a number for shape and range', () => {
    const score = input({ id: 'n', type: 'WholeNumber', min: 1, max: 5 })
    expect(validateManualValues([score], { n: '2.5' })).toEqual({ n: 'notWholeNumber' })
    expect(validateManualValues([score], { n: '0' })).toEqual({ n: 'belowMin' })
    expect(validateManualValues([score], { n: '6' })).toEqual({ n: 'aboveMax' })
    expect(validateManualValues([score], { n: '5' })).toEqual({})
  })

  it('refuses a choice the definition does not offer', () => {
    expect(
      validateManualValues([input({ id: 'c', type: 'Choice', options })], { c: 'z' }),
    ).toEqual({ c: 'invalidChoice' })
  })

  it('makes a dependent field required once its condition holds', () => {
    const inputs = [
      input({
        id: 'term',
        type: 'Choice',
        required: true,
        options: [{ value: 'fixed', label: 'F' }],
      }),
      input({
        id: 'endDate',
        type: 'Date',
        requiredWhen: { input: 'term', fact: null, equalsValue: 'fixed' },
      }),
    ]
    expect(validateManualValues(inputs, { term: 'fixed', endDate: '' })).toEqual({
      endDate: 'required',
    })
  })
})

describe('toManualInputs', () => {
  it('sends a number as a number, trims text and leaves empty fields out', () => {
    expect(
      toManualInputs(
        [
          input({ id: 'n', type: 'WholeNumber' }),
          input({ id: 'x', type: 'Text' }),
          input({ id: 'd', type: 'Date' }),
          input({ id: 'l', type: 'Checklist', options }),
          input({ id: 'm', type: 'Checklist', options }),
        ],
        { n: '4', x: '  salom ', d: '', l: { a: 'done' }, m: {} },
      ),
    ).toEqual({ n: 4, x: 'salom', l: { a: 'done' } })
  })
})

describe('describeRecordedInputs', () => {
  it('has nothing to say about a step without a payload', () => {
    expect(describeRecordedInputs([input({ id: 'x', type: 'Text' })], null)).toEqual([])
  })

  it('shows answers in the words of the definition', () => {
    const rows = describeRecordedInputs(
      [
        input({ id: 'c', type: 'Choice', label: 'Tanlov', options }),
        input({ id: 'd', type: 'Date', label: 'Sana' }),
        input({ id: 'n', type: 'WholeNumber', label: 'Baho' }),
        input({ id: 'l', type: 'Checklist', label: 'Ro‘yxat', options }),
      ],
      { c: 'b', d: '2026-11-05', n: 4, l: { a: 'done', b: 'notNeeded' } },
    )
    expect(rows).toEqual([
      { kind: 'text', id: 'c', label: 'Tanlov', text: 'Ikkinchi' },
      { kind: 'date', id: 'd', label: 'Sana', date: '2026-11-05' },
      { kind: 'text', id: 'n', label: 'Baho', text: '4' },
      {
        kind: 'checklist',
        id: 'l',
        label: 'Ro‘yxat',
        items: [
          { value: 'a', label: 'Birinchi', mark: 'done' },
          { value: 'b', label: 'Ikkinchi', mark: 'notNeeded' },
        ],
      },
    ])
  })

  it('ignores what a fact wrote and values of an unexpected shape', () => {
    expect(
      describeRecordedInputs(
        [input({ id: 'x', type: 'Text' }), input({ id: 'l', type: 'Checklist', options })],
        { value: 'accept', x: { nested: true }, l: 'text' },
      ),
    ).toEqual([])
  })
})
