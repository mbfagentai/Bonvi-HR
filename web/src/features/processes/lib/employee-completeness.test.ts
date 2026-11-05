import { describe, expect, it } from 'vitest'

import {
  completenessCardSearch,
  completenessPlace,
  isDocumentKind,
  primaryCompletenessKind,
} from './employee-completeness'

describe('completenessPlace', () => {
  it.each(['pinfl', 'passport', 'phone', 'address'])('sends %s to the edit form', (code) => {
    expect(completenessPlace(code)).toBe('edit')
  })

  it('sends the order number to the labour history, whose hire record takes it', () => {
    expect(completenessPlace('orderNo')).toBe('timeline')
  })

  it.each(['photo', 'salary', 'needsReview'])('sends %s to the card itself', (code) => {
    expect(completenessPlace(code)).toBe('overview')
  })

  it('sends a required document type to the document list', () => {
    const code = 'doc:6f1c0e3a-0000-4000-8000-000000000001'
    expect(isDocumentKind(code)).toBe(true)
    expect(completenessPlace(code)).toBe('documents')
  })
})

describe('completenessCardSearch', () => {
  it('builds the address of each place', () => {
    expect(completenessCardSearch('edit')).toEqual({ edit: true })
    expect(completenessCardSearch('timeline')).toEqual({ tab: 'timeline' })
    expect(completenessCardSearch('documents')).toEqual({ tab: 'documents' })
    expect(completenessCardSearch('overview')).toEqual({})
  })
})

describe('primaryCompletenessKind', () => {
  it('takes the first gap without a filter', () => {
    expect(primaryCompletenessKind(['phone', 'orderNo'])).toBe('phone')
  })

  it('takes the filtered kind even when it is not first', () => {
    expect(primaryCompletenessKind(['phone', 'orderNo'], 'orderNo')).toBe('orderNo')
  })

  it('ignores a filter this person does not lack', () => {
    expect(primaryCompletenessKind(['phone'], 'orderNo')).toBe('phone')
  })

  it('has nothing to offer with no gaps', () => {
    expect(primaryCompletenessKind([])).toBeNull()
  })
})
