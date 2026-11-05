import { describe, expect, it } from 'vitest'

import { isFiltered, validateVacancyRequestsSearch } from './vacancy-requests-search'

describe('validateVacancyRequestsSearch', () => {
  it('keeps the id of the request a process step links to (SPEC-mening-ishlarim §11.6)', () => {
    expect(validateVacancyRequestsSearch({ id: 'abc' }).id).toBe('abc')
  })

  it('drops an id it cannot read', () => {
    expect(validateVacancyRequestsSearch({ id: 5 }).id).toBeUndefined()
    expect(validateVacancyRequestsSearch({ id: '  ' }).id).toBeUndefined()
  })

  it('does not treat the id as a filter: it opens one request, the list stays as it is', () => {
    expect(isFiltered(validateVacancyRequestsSearch({ id: 'abc' }))).toBe(false)
  })
})
