import { problemCode } from '@/shared/api/problem-details'

/**
 * Xatodan tarjima qilinadigan kodni ajratib oladi.
 *
 * `catalog-error.ts` / `safety-error.ts` dagi bilan bir xil naqsh: mantiq
 * `shared/api/problem-details.ts` da, bo'sh satr — «kod noma'lum»
 * (`useErrorMessage` uni umumiy xabarga aylantiradi).
 */
export function feedbackErrorCode(error: unknown): string {
  return problemCode(error)
}

/**
 * Qaysi xato kodi yaratish/tahrirlash formasining qaysi maydoniga tegishli.
 *
 * `qayta_aloqa.mehnat_tugamagan» / «qayta_aloqa.allaqachon_bor» bu yerda
 * ATAYLAB yo'q: ular bitta maydonga emas, butun tanlovga (qaysi mehnat
 * munosabati tanlangani) tegishli — forma ularni ALOHIDA, tanlov ustida
 * ko'rsatadi (`exit-interview-form-dialog.tsx` dagi izohga qarang).
 */
export const FEEDBACK_ERROR_FIELD: Readonly<Record<string, string>> = {
  'qayta_aloqa.mehnat_yoq': 'employmentId',
  'qayta_aloqa.sabab_bosh': 'reason',
}
