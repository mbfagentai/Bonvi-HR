import { Lock, TriangleAlert } from 'lucide-react'
import type { ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Badge, type BadgeTone } from '@/shared/ui'

import type {
  ProcessCaseStatus,
  ProcessHolder,
  ProcessStepState,
  ProcessSubject,
} from '../api/processes-api'
import { describeDue, shortDueDate } from '../lib/process-due'

/*
 * The small pieces every tab of «Mening ishlarim» draws (SPEC §11.2–§11.5). They live here so the task card, the board
 * card and the late row cannot drift apart in wording or colour.
 */

const STATE_TONE: Record<ProcessStepState, BadgeTone> = {
  Locked: 'neutral',
  Active: 'brand',
  Waiting: 'neutral',
  Blocked: 'danger',
  Done: 'success',
  Skipped: 'neutral',
}

const STATUS_TONE: Record<ProcessCaseStatus, BadgeTone> = {
  Active: 'brand',
  Completed: 'success',
  Cancelled: 'neutral',
}

/** «Bugun», «Ertaga», «3 kun kechikdi», «24.11 gacha». Draws nothing when the step has no due date. */
export function ProcessDueChip({
  dueDate,
  daysLate,
  today,
}: {
  dueDate: string | null
  daysLate: number
  /** `ProcessMyWork.today`, or `todayIso()` on the screens whose response has none. */
  today: string
}) {
  const { t, num, locale } = useLocale()
  const due = describeDue(dueDate, daysLate, today)

  switch (due.kind) {
    case 'late':
      return <Badge tone="danger">{t('processes.due.late', { n: num(due.days) })}</Badge>
    case 'today':
      return <Badge tone="warning">{t('processes.due.today')}</Badge>
    case 'tomorrow':
      return <Badge tone="neutral">{t('processes.due.tomorrow')}</Badge>
    case 'until':
      return (
        <Badge tone="neutral">
          {t('processes.due.until', { date: shortDueDate(due.date, locale) })}
        </Badge>
      )
    case 'none':
      return null
  }
}

export function ProcessStateBadge({ state }: { state: ProcessStepState }) {
  const { t } = useLocale()

  return (
    <Badge tone={STATE_TONE[state]}>
      {state === 'Blocked' && <Lock aria-hidden className="size-3" />}
      {t(`processes.state.${state}`)}
    </Badge>
  )
}

export function ProcessStatusBadge({ status }: { status: ProcessCaseStatus }) {
  const { t } = useLocale()

  return <Badge tone={STATUS_TONE[status]}>{t(`processes.status.${status}`)}</Badge>
}

/** A case or step older than the engine's start date: it may be closed without the usual checks. */
export function ProcessLegacyBadge() {
  const { t } = useLocale()

  return <Badge tone="neutral">{t('processes.legacy')}</Badge>
}

/** The width every screen of the section shares, taken from the chef's prototype. */
export const PROCESS_PAGE_CLASS = 'mx-auto w-full max-w-[1256px]'

/** The grey pill beside a heading: «3/3», or a plain count. `label` says it in words for a screen reader. */
export function ProcessCountChip({ children, label }: { children: ReactNode; label?: string }) {
  return (
    <span
      aria-label={label}
      role={label === undefined ? undefined : 'img'}
      className="tabular shrink-0 rounded-full bg-[var(--bg-sunken)] px-2 py-0.5 font-mono text-caption2 font-medium tracking-normal text-[var(--fg-secondary)]"
    >
      {children}
    </span>
  )
}

/** Short title plus the code in small type («Ishga qabul HR-F06»); nothing for a step without a blank. */
export function ProcessBlankChip({ code, title }: { code: string | null; title: string | null }) {
  if (code === null) return null

  return (
    <Badge
      tone="neutral"
      className="h-auto min-h-6 max-w-full py-0.5 whitespace-normal text-[var(--fg)]"
    >
      {title}
      <span className="font-mono text-caption2 text-[var(--fg-secondary)]">{code}</span>
    </Badge>
  )
}

/** Warn-mode gates that currently fire. They never stop the work; they say why the card looks urgent. */
export function ProcessWarningChips({ codes }: { codes: readonly string[] }) {
  const { t } = useLocale()

  return (
    <>
      {codes.map((code) => (
        <Badge key={code} tone="warning">
          <TriangleAlert aria-hidden className="size-3" />
          {t(`processes.warnings.${code}`, { defaultValue: code })}
        </Badge>
      ))}
    </>
  )
}

/** The holder's role in words; a code this build does not know is shown as is rather than as a translation key. */
export function useProcessRoleLabel(): (role: string | null) => string {
  const { t } = useLocale()

  return (role) => {
    const code = role ?? 'system'
    return t(`processes.roles.${code}`, { defaultValue: code })
  }
}

/** The person on the case, else the first user holding the role, plus how many more hold it. */
function holderPerson(holder: ProcessHolder): { name: string; others: number } | null {
  if (holder.employeeName !== null) return { name: holder.employeeName, others: 0 }

  const [first, ...rest] = holder.users
  return first === undefined ? null : { name: first.name, others: rest.length }
}

/** «HR-1 · Aliyev Vali», «Tizim», or an orange «HR-3 · Tayinlanmagan» when nobody holds the role. */
export function ProcessHolderLabel({
  holder,
  className,
}: {
  holder: ProcessHolder
  className?: string
}) {
  const { t, num } = useLocale()
  const roleLabel = useProcessRoleLabel()
  const person = holderPerson(holder)

  if (holder.role === null) {
    return <span className={className}>{roleLabel(null)}</span>
  }

  return (
    <span className={className}>
      {roleLabel(holder.role)}
      {' · '}
      {holder.unassigned || person === null ? (
        <span className="text-ios-orange">{t('processes.holder.unassigned')}</span>
      ) : (
        <>
          {person.name}
          {person.others > 0 && ` ${t('processes.holder.others', { n: num(person.others) })}`}
        </>
      )}
    </span>
  )
}

/**
 * «{subject} · {position}, {department}»; the parts that are absent drop out together with their separator, and so does a
 * whole subject the case does not have.
 */
export function processSubjectLine(
  subject: Pick<ProcessSubject, 'name' | 'position'> | null | undefined,
  departmentName: string | null | undefined,
): string {
  const where = [subject?.position, departmentName].filter(Boolean).join(', ')
  return [subject?.name, where].filter(Boolean).join(' · ')
}
