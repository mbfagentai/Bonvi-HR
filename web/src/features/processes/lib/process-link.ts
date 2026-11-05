import type { ProcessLink, ProcessSubject } from '../api/processes-api'

export type ProcessLinkTarget =
  /** Open with `navigate({ href })`. */
  | { kind: 'internal'; href: string }
  /** Open in a new tab. */
  | { kind: 'external'; url: string }
  /** A blank step with no blank yet: «Boshlash» creates it (`POST /api/blanks/submissions`), then navigates. */
  | { kind: 'createBlank'; template: string; subject: string | null }
  /** Nothing to open: the screen is `none`, unknown, or its subject is missing. */
  | { kind: 'none' }

const NONE: ProcessLinkTarget = { kind: 'none' }

const internal = (href: string): ProcessLinkTarget => ({ kind: 'internal', href })

/** The templates are trusted files, but `window.open` with a `javascript:` address would still run it. */
function externalTarget(url: string | undefined): ProcessLinkTarget {
  return url !== undefined && /^https?:\/\//i.test(url) ? { kind: 'external', url } : NONE
}

/** Where a step leads (SPEC §11.6). The definition names the screen; the case supplies the ids. */
export function resolveProcessLink(
  link: ProcessLink,
  subject: ProcessSubject | null | undefined,
  linkedBlankSubmissionId: string | null,
): ProcessLinkTarget {
  const employeeId = subject?.employeeId ?? null
  const candidateId = subject?.candidateId ?? null
  const vacancyRequestId = subject?.vacancyRequestId ?? null
  const name = subject?.name ?? null

  switch (link.screen) {
    case 'vacancyRequests':
      return internal(
        vacancyRequestId === null
          ? '/vakansiya-sorovlari'
          : `/vakansiya-sorovlari?id=${encodeURIComponent(vacancyRequestId)}`,
      )
    case 'candidate':
    case 'candidateForm':
      return candidateId === null ? NONE : internal(`/nomzodlar/${candidateId}`)
    case 'blank': {
      if (linkedBlankSubmissionId !== null) return internal(`/blankalar/${linkedBlankSubmissionId}`)
      const template = link.args['template']
      return template === undefined
        ? NONE
        : { kind: 'createBlank', template, subject: link.args['subject'] ?? null }
    }
    case 'employee':
      return employeeId === null ? NONE : internal(`/xodimlar/${employeeId}`)
    case 'employment':
      // `/buyruqlar` filters by free text only, so the person's name stands in for an id.
      return internal(name === null ? '/buyruqlar' : `/buyruqlar?q=${encodeURIComponent(name)}`)
    case 'probations':
      return internal('/sinov')
    case 'attendanceMapping':
      return internal('/verifikatsiya/moslashtirish')
    case 'attendanceSchedules':
      return internal('/verifikatsiya/jadvallar')
    case 'external':
      return externalTarget(link.args['url'])
    default:
      return NONE
  }
}
