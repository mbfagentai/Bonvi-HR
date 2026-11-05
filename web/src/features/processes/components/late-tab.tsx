import { Link } from '@tanstack/react-router'
import { CircleCheck, ShieldAlert } from 'lucide-react'
import { useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { USERS_MANAGE } from '@/features/users/lib/user-presentation'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
import { Badge, type BadgeTone, Button, buttonVariants, Card, CardHeader, EmptyState } from '@/shared/ui'

import type { ProcessGateOverrideRow, ProcessTask } from '../api/processes-api'
import { useProcessLate } from '../api/processes-queries'
import { PROCESSES_OVERRIDE } from '../lib/permissions'
import {
  canTakeOverride,
  openOverrideFor,
  overrideStanding,
  type ProcessOverrideStanding,
} from '../lib/process-override'
import {
  ProcessBlankChip,
  ProcessCountChip,
  ProcessDueChip,
  ProcessHolderLabel,
  ProcessLegacyBadge,
  ProcessWarningChips,
  processSubjectLine,
} from './process-chips'
import { CaseOverrideDialog, DirectHireOverrideDialog } from './process-override-dialogs'
import { ProcessListSkeleton, ProcessLoadError } from './process-states'

const STANDING_TONE: Record<ProcessOverrideStanding, BadgeTone> = {
  used: 'success',
  open: 'brand',
  expired: 'neutral',
}

const ROW_CLASS = 'flex flex-col gap-3 px-5 py-4 sm:px-6 md:flex-row md:items-start md:justify-between'
const LIST_CLASS = 'divide-y divide-[var(--border)] border-t border-[var(--border)]'
const OPEN_LINK_CLASS = buttonVariants({ variant: 'secondary', size: 'sm' })

/** One step someone is behind on (SPEC §11.4): case, step, holder, why, how late, and the way in. */
function LateRow({
  task,
  today,
  children,
}: {
  task: ProcessTask
  today: string
  children?: ReactNode
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const subject = processSubjectLine(task.subject, task.departmentName)
  const blocked = task.state === 'Blocked'

  return (
    <li className={ROW_CLASS}>
      <div className="min-w-0 space-y-1">
        <p className="flex flex-wrap items-center gap-2 text-subhead font-semibold">
          {subject === '' ? task.processTitle : subject}
          {task.legacy && <ProcessLegacyBadge />}
        </p>
        <p className="text-subhead">
          {task.processTitle}
          {' · '}
          {task.stepTitle}
        </p>
        <p className="text-footnote text-[var(--fg-secondary)]">
          <ProcessHolderLabel holder={task.holder} />
        </p>
        {blocked && (
          <p className="text-footnote text-ios-red">
            {task.gateErrorCode === null
              ? t('processes.late.blockedNoReason')
              : errorMessage(task.gateErrorCode)}
          </p>
        )}
        {blocked && task.resolver !== null && (
          <p className="text-footnote text-[var(--fg-secondary)]">
            {t('processes.late.resolver')}: <ProcessHolderLabel holder={task.resolver} />
          </p>
        )}
        <div className="flex flex-wrap items-center gap-1.5 pt-1">
          <ProcessDueChip dueDate={task.dueDate} daysLate={task.daysLate} today={today} />
          <ProcessBlankChip code={task.blankCode} title={task.blankTitle} />
          <ProcessWarningChips codes={task.warnings} />
        </div>
      </div>

      <div className="flex shrink-0 flex-wrap items-center gap-2">
        {children}
        <Link to="/mening-ishlarim/$caseId" params={{ caseId: task.caseId }} className={OPEN_LINK_CLASS}>
          {t('processes.late.open')}
        </Link>
      </div>
    </li>
  )
}

/** A group with nothing in it drops out: an empty heading would only be noise above the ones that matter. */
function LateGroup({
  title,
  tone,
  count,
  children,
}: {
  title: string
  tone: BadgeTone
  count: number
  children: ReactNode
}) {
  const { num } = useLocale()
  if (count === 0) return null

  return (
    <Card className="overflow-hidden rounded-xl">
      <h2 className="flex items-center gap-2 px-5 py-3.5 text-footnote font-semibold tracking-wide uppercase sm:px-6">
        <span className="min-w-0 break-words">{title}</span>
        {tone === 'danger' ? (
          <Badge tone="danger" size="sm" className="tabular font-mono">
            {num(count)}
          </Badge>
        ) : (
          <ProcessCountChip>{num(count)}</ProcessCountChip>
        )}
      </h2>
      <ul className={LIST_CLASS}>{children}</ul>
    </Card>
  )
}

function OverrideLogRow({ row, nowMs }: { row: ProcessGateOverrideRow; nowMs: number }) {
  const { t, dateTime } = useLocale()
  const standing = overrideStanding(row, nowMs)

  return (
    <li className={ROW_CLASS}>
      <div className="min-w-0 space-y-1">
        <p className="flex flex-wrap items-center gap-2 text-subhead font-semibold">
          {t(`processes.late.gates.${row.gateCode}`, { defaultValue: row.gateCode })}
          <Badge tone={STANDING_TONE[standing]}>{t(`processes.late.standing.${standing}`)}</Badge>
        </p>
        {row.forUserName !== null && (
          <p className="text-footnote text-[var(--fg-secondary)]">
            {t('processes.late.overrideFor', { name: row.forUserName })}
          </p>
        )}
        <p className="text-subhead break-words">{row.reason}</p>
        <p className="text-footnote text-[var(--fg-secondary)]">
          {t('processes.late.overrideGranted', {
            name: row.grantedByName ?? t('processes.late.unknownUser'),
            date: dateTime(row.grantedAt),
          })}
        </p>
        <p className="text-footnote text-[var(--fg-secondary)]">
          {row.consumedAt === null
            ? t('processes.late.overrideExpires', { date: dateTime(row.expiresAt) })
            : t('processes.late.overrideConsumed', { date: dateTime(row.consumedAt) })}
        </p>
      </div>

      {row.caseId !== null && (
        <Link to="/mening-ishlarim/$caseId" params={{ caseId: row.caseId }} className={OPEN_LINK_CLASS}>
          {t('processes.late.open')}
        </Link>
      )}
    </li>
  )
}

/** Tab «Kim kechikdi» (SPEC §11.4). «Eslatish» is Phase 2, so that button does not exist yet. */
export function LateTab() {
  const { t, dateTime } = useLocale()
  const { hasPermission } = useAuth()
  const query = useProcessLate()
  const [overriding, setOverriding] = useState<ProcessTask | null>(null)
  const [grantingDirectHire, setGrantingDirectHire] = useState(false)

  if (query.isPending) return <ProcessListSkeleton />
  if (query.isError) {
    return <ProcessLoadError error={query.error} onRetry={() => void query.refetch()} />
  }

  const { blocked, late, warn, overrides } = query.data
  const today = todayIso()
  // The moment the rows arrived, so a re-render cannot disagree with the list beside it.
  const nowMs = query.dataUpdatedAt

  const canOverride = hasPermission(PROCESSES_OVERRIDE)
  // Choosing the operator reads `/api/users`, which is closed behind `users.manage`.
  const canGrantDirectHire = canOverride && hasPermission(USERS_MANAGE)
  const nobodyBehind = blocked.length === 0 && late.length === 0 && warn.length === 0

  return (
    <div className="space-y-5">
      {nobodyBehind ? (
        <Card className="rounded-xl">
          <EmptyState
            icon={CircleCheck}
            title={t('processes.late.emptyTitle')}
            description={t('processes.late.emptyText')}
          />
        </Card>
      ) : (
        <>
          <LateGroup title={t('processes.late.blocked')} tone="danger" count={blocked.length}>
            {blocked.map((task) => {
              const open = openOverrideFor(task, overrides, nowMs)

              return (
                <LateRow key={`${task.caseId}:${task.stepCode}`} task={task} today={today}>
                  {open !== undefined ? (
                    <Badge tone="brand">
                      {t('processes.late.overrideOpen', { date: dateTime(open.expiresAt) })}
                    </Badge>
                  ) : (
                    canOverride &&
                    canTakeOverride(task) && (
                      <Button size="sm" onClick={() => setOverriding(task)}>
                        <ShieldAlert />
                        {t('processes.late.override.action')}
                      </Button>
                    )
                  )}
                </LateRow>
              )
            })}
          </LateGroup>

          <LateGroup title={t('processes.late.late')} tone="danger" count={late.length}>
            {late.map((task) => (
              <LateRow key={`${task.caseId}:${task.stepCode}`} task={task} today={today} />
            ))}
          </LateGroup>

          <LateGroup title={t('processes.late.warn')} tone="warning" count={warn.length}>
            {warn.map((task) => (
              <LateRow key={`${task.caseId}:${task.stepCode}`} task={task} today={today} />
            ))}
          </LateGroup>
        </>
      )}

      <Card className="overflow-hidden rounded-xl">
        <CardHeader
          className="px-5 pb-3 sm:px-6"
          title={t('processes.late.overridesTitle')}
          subtitle={t('processes.late.overridesHint')}
          action={
            canGrantDirectHire ? (
              <Button size="sm" onClick={() => setGrantingDirectHire(true)}>
                <ShieldAlert />
                {t('processes.late.directHire.action')}
              </Button>
            ) : undefined
          }
        />
        {overrides.length === 0 ? (
          <p className="border-t border-[var(--border)] px-5 py-6 text-center text-footnote text-[var(--fg-secondary)]">
            {t('processes.late.overridesEmpty')}
          </p>
        ) : (
          <ul className={LIST_CLASS}>
            {overrides.map((row) => (
              <OverrideLogRow key={row.id} row={row} nowMs={nowMs} />
            ))}
          </ul>
        )}
      </Card>

      {overriding !== null && (
        <CaseOverrideDialog task={overriding} onClose={() => setOverriding(null)} />
      )}
      {grantingDirectHire && (
        <DirectHireOverrideDialog onClose={() => setGrantingDirectHire(false)} />
      )}
    </div>
  )
}
