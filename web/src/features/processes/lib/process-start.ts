import type { EmploymentSummary } from '@/features/employees/api/employees-api'

/** `ProcessCase.MaxCancelReasonLength`, which also bounds `basis`; the server has the final word. */
export const PROCESS_START_BASIS_MAX = 1000

/** What `POST /cases` accepts: an ended employment answers `jarayon.holat_notogri`. The main job comes first. */
export function openEmployments(employments: readonly EmploymentSummary[]): EmploymentSummary[] {
  return employments
    .filter((employment) => employment.status !== 'terminated')
    .sort((a, b) => Number(b.isPrimary) - Number(a.isPrimary))
}

function placeOf(employment: EmploymentSummary): string {
  return [employment.position?.name, employment.department?.name].filter(Boolean).join(' · ')
}

/**
 * Position and department tell two jobs apart; the legal entity is added only for two jobs they cannot separate,
 * because the choice decides whose dismissal opens.
 */
export function employmentOptionLabel(
  employment: EmploymentSummary,
  all: readonly EmploymentSummary[],
): string {
  const place = placeOf(employment)
  const ambiguous = all.some((other) => other.id !== employment.id && placeOf(other) === place)
  const legalEntity = employment.legalEntity?.name
  if (place === '') return legalEntity ?? '—'
  return ambiguous && legalEntity !== undefined ? `${place} · ${legalEntity}` : place
}

/** The server refuses a day before the hire date with a bare 400, so the form says why first. Both are `yyyy-MM-dd`. */
export function isBeforeHire(lastWorkDay: string, hireDate: string | undefined): boolean {
  return lastWorkDay !== '' && hireDate !== undefined && hireDate !== '' && lastWorkDay < hireDate
}
