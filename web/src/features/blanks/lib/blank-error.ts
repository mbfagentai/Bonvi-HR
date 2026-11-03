import axios from 'axios'

import { problemCode, type ProblemDetails } from '@/shared/api/problem-details'

/**
 * `blanka.*` problem codes (Domain `BlankErrors`, SPEC §7). Translations live in
 * `errors["blanka.*"]` of every locale file; `useErrorMessage()` resolves them.
 */
export const BLANK_ERROR = {
  notFound: 'blanka.topilmadi',
  conflict: 'blanka.ozgargan',
  wrongStatus: 'blanka.holat_notogri',
  required: 'blanka.majburiy',
  invalid: 'blanka.notogri',
  notEditable: 'blanka.maydon_tahrirlanmaydi',
} as const

/** Codes whose `detail` is a field path (`fieldId`, `table[2].col`) — shown inline at that field. */
const FIELD_PATH_CODES: ReadonlySet<string> = new Set([BLANK_ERROR.required, BLANK_ERROR.invalid, BLANK_ERROR.notEditable])

export function blankErrorCode(error: unknown): string {
  return problemCode(error)
}

/** `detail` of the problem body, or `null`. */
export function blankErrorDetail(error: unknown): string | null {
  if (!axios.isAxiosError(error)) return null
  const detail = (error.response?.data as ProblemDetails | undefined)?.detail
  return typeof detail === 'string' && detail.trim() !== '' ? detail : null
}

/** Server error for `<BlankForm serverError>`: the field path is kept only for path-carrying codes. */
export function blankServerError(error: unknown): { code: string; detail: string | null } {
  const code = blankErrorCode(error)
  return { code, detail: FIELD_PATH_CODES.has(code) ? blankErrorDetail(error) : null }
}

/** 409 `blanka.ozgargan` — someone else saved first; the page must reload. */
export function isBlankConflict(error: unknown): boolean {
  return blankErrorCode(error) === BLANK_ERROR.conflict
}
