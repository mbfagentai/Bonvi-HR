const DOCUMENT_PREFIX = 'doc:'

/**
 * Where on the employee card a missing thing is entered. The owner's rule: HR fills in what is missing and nothing is
 * ever ticked «yo'q», so a gap's action is the screen that takes the data, not a checkbox.
 *
 * - `edit`: the card with its edit form open (PINFL, passport, phone, address).
 * - `overview`: the card's first tab. It holds the photo editor, the salary button and the review notes.
 * - `timeline`: the labour history, whose hire record takes the order number.
 * - `documents`: the document list.
 */
export type CompletenessPlace = 'edit' | 'overview' | 'timeline' | 'documents'

export function isDocumentKind(code: string): boolean {
  return code.startsWith(DOCUMENT_PREFIX)
}

export function completenessPlace(code: string): CompletenessPlace {
  if (isDocumentKind(code)) return 'documents'

  switch (code) {
    case 'pinfl':
    case 'passport':
    case 'phone':
    case 'address':
      return 'edit'
    case 'orderNo':
      return 'timeline'
    default:
      return 'overview'
  }
}

/** The employee route's search for a place; the first tab stays out of the address, as the route itself does. */
export function completenessCardSearch(place: CompletenessPlace): {
  tab?: 'timeline' | 'documents'
  edit?: true
} {
  switch (place) {
    case 'edit':
      return { edit: true }
    case 'timeline':
    case 'documents':
      return { tab: place }
    case 'overview':
      return {}
  }
}

/**
 * The thing a card's main action goes to: the kind the list is filtered by, when this person lacks it, otherwise the
 * first thing they lack. HR working through «Buyruq raqami» must land there on every card, not wherever each person's
 * first gap happens to be.
 */
export function primaryCompletenessKind(missing: readonly string[], filter?: string): string | null {
  if (filter !== undefined && missing.includes(filter)) return filter
  return missing[0] ?? null
}
