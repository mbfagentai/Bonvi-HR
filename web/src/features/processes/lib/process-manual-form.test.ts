import { describe, expect, it } from 'vitest'

import type { ProcessAutoItem, ProcessManualInput } from '../api/processes-api'
import {
  checklistRows,
  describeRecordedInputs,
  initialManualValues,
  isManualInputRequired,
  missingRequiredItems,
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
    autoItems: null,
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
        l: { a: 'done', b: 'na' },
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
    expect(validateManualValues([list], { l: { a: 'na', b: 'na' } })).toEqual({})
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
      { c: 'b', d: '2026-11-05', n: 4, l: { a: 'done', b: 'na' } },
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
          { value: 'b', label: 'Ikkinchi', mark: 'na' },
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

describe('data-backed checklist items', () => {
  const auto = (value: string, state: ProcessAutoItem['state']): ProcessAutoItem => ({
    value,
    state,
    autoValue: state === 'Known' ? 'done' : state === 'NotApplicable' ? 'na' : null,
    autoSource: state === 'Known' ? 'EmployeeCard' : null,
    sourceDetail: null,
    reason: state === 'NotApplicable' ? 'Female' : null,
    fill:
      state === 'Missing'
        ? {
            kind: 'EmployeeDocument',
            employeeId: 'e1',
            candidateId: null,
            documentTypeId: null,
            documentTypeName: null,
            stepCode: null,
          }
        : null,
  })

  const docs = input({
    id: 'docs',
    type: 'Checklist',
    required: true,
    options: [
      { value: 'passport', label: 'Pasport' },
      { value: 'certificates', label: 'Sertifikat' },
      { value: 'diploma', label: 'Diplom' },
      { value: 'military', label: 'Harbiy' },
      { value: 'bankCard', label: 'Bank kartasi' },
    ],
    autoItems: [
      auto('passport', 'Known'),
      auto('diploma', 'Missing'),
      auto('military', 'NotApplicable'),
    ],
  })

  it('sorts the rows by state, treating an option with no entry as a plain tick', () => {
    const rows = checklistRows(docs)
    expect(rows.missing.map((row) => row.option.value)).toEqual(['diploma'])
    expect(rows.plain.map((row) => row.option.value)).toEqual(['certificates', 'bankCard'])
    expect(rows.known.map((row) => row.option.value)).toEqual(['passport'])
    expect(rows.notApplicable.map((row) => row.option.value)).toEqual(['military'])
  })

  it('treats every option as plain when the server sent no auto items', () => {
    const rows = checklistRows(input({ id: 'x', type: 'Checklist', options }))
    expect(rows.plain).toHaveLength(2)
    expect(rows.missing).toHaveLength(0)
  })

  it('asks a person only about the plain items', () => {
    const plain = { certificates: 'done', bankCard: 'na' } as const
    expect(validateManualValues([docs], { docs: plain })).toEqual({})
    expect(validateManualValues([docs], { docs: { certificates: 'done' } })).toEqual({
      docs: 'checklistIncomplete',
    })
  })

  it('sends only plain items, even if something else is in the form state', () => {
    expect(
      toManualInputs([docs], { docs: { certificates: 'done', passport: 'done', diploma: 'na' } }),
    ).toEqual({ docs: { certificates: 'done' } })
    expect(toManualInputs([docs], { docs: { passport: 'done' } })).toEqual({})
  })

  it('lets a step with only auto items go with nothing to tick', () => {
    const settled = input({
      id: 'all',
      type: 'Checklist',
      required: true,
      options: [{ value: 'passport', label: 'Pasport' }],
      autoItems: [auto('passport', 'Known')],
    })
    expect(validateManualValues([settled], { all: {} })).toEqual({})
    expect(missingRequiredItems([settled], { all: {} })).toEqual([])
  })

  it('counts a missing item only on an input required right now', () => {
    expect(missingRequiredItems([docs], {}).map((row) => row.option.value)).toEqual(['diploma'])
    expect(missingRequiredItems([{ ...docs, required: false }], {})).toEqual([])

    const conditional = {
      ...docs,
      required: false,
      requiredWhen: { input: 'kind', equalsValue: 'a', fact: null },
    } satisfies ProcessManualInput
    expect(missingRequiredItems([conditional], { kind: 'b' })).toEqual([])
    expect(missingRequiredItems([conditional], { kind: 'a' })).toHaveLength(1)
  })
})
