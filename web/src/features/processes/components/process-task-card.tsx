import { Link, useNavigate } from '@tanstack/react-router'
import {
  ArrowUpRight,
  ChevronRight,
  ExternalLink,
  FileText,
  Lock,
  type LucideIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Badge, Button, Card } from '@/shared/ui'

import type { ProcessTask } from '../api/processes-api'
import { taskLinkAction, taskResolver } from '../lib/my-work'
import { taskTone, type ProcessTaskTone } from '../lib/process-due'
import {
  ProcessBlankChip,
  ProcessDueChip,
  ProcessHolderLabel,
  ProcessLegacyBadge,
  processSubjectLine,
  ProcessWarningChips,
} from './process-chips'

/** The stripe says how pressing the task is before a word is read (SPEC §11.2). */
const STRIPE: Record<ProcessTaskTone, string> = {
  late: 'border-l-ios-red',
  today: 'border-l-ios-yellow',
  blocked: 'border-l-ios-gray',
  normal: 'border-l-[var(--accent)]',
}

/** A heading over a group of task cards: small icon, uppercase title, grey count. */
export function ProcessTaskSection({
  icon: Icon,
  title,
  count,
  children,
}: {
  icon: LucideIcon
  title: ReactNode
  count?: number
  children: ReactNode
}) {
  const { num } = useLocale()

  return (
    <section className="space-y-3">
      <h2 className="flex items-center gap-2 px-1 text-footnote font-semibold tracking-wide text-[var(--fg-secondary)] uppercase">
        <Icon aria-hidden className="size-4 shrink-0" strokeWidth={2} />
        <span className="min-w-0">{title}</span>
        {count !== undefined && (
          <span className="tabular rounded-full bg-[var(--bg-sunken)] px-2 py-0.5 font-mono text-caption2 font-medium">
            {num(count)}
          </span>
        )}
      </h2>
      {children}
    </section>
  )
}

/**
 * One task, in the look of the chef's prototype: a white panel, process above, a large verb-first title, then pill chips.
 * The whole card opens the case: the title is the only link, stretched over the card, so a screen reader hears one link,
 * and any other control stays real and separate above it.
 *
 * «Kim kechikdi» draws the same card for someone else's task. `stripeTone` overrides the task's own tone, and `notice` and
 * `actions`, when given, take the place of the holder's own note under the chips and of the button to the step's screen.
 */
export function ProcessTaskCard({
  task,
  today,
  stripeTone,
  notice,
  actions,
}: {
  task: ProcessTask
  today: string
  stripeTone?: ProcessTaskTone
  notice?: ReactNode
  actions?: ReactNode
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()

  const tone = taskTone(task.state, task.dueDate, task.daysLate, today)
  const subjectLine = processSubjectLine(task.subject, task.departmentName)
  const resolver = taskResolver(task)
  const action = taskLinkAction(task)
  const gateMessage =
    task.state === 'Blocked' && task.gateErrorCode !== null ? errorMessage(task.gateErrorCode) : null

  return (
    <Card
      className={cn(
        'relative rounded-lg border-l-4',
        STRIPE[stripeTone ?? tone],
        'transition-colors duration-[120ms] ease-[var(--ease-ios)] hover:bg-[var(--bg-hover)]',
      )}
    >
      <div className="flex flex-col gap-4 px-5 py-5 sm:flex-row sm:items-center">
        <div className="min-w-0 flex-1">
          <p className="truncate-1 text-footnote text-[var(--fg-secondary)]">{task.processTitle}</p>

          <h3 className="mt-1 flex items-start gap-2 text-title3 leading-snug font-semibold">
            {task.state === 'Blocked' && (
              <Lock
                aria-hidden
                strokeWidth={2.5}
                className="mt-[5px] size-[18px] shrink-0 text-ios-gray"
              />
            )}
            <Link
              to="/mening-ishlarim/$caseId"
              params={{ caseId: task.caseId }}
              className={cn(
                'min-w-0 outline-none after:absolute after:inset-0 after:rounded-lg',
                'focus-visible:after:ring-2 focus-visible:after:ring-[var(--accent)] focus-visible:after:ring-inset',
              )}
            >
              {task.stepTitle}
            </Link>
          </h3>

          {subjectLine !== '' && (
            <p className="mt-1 text-subhead text-[var(--fg-secondary)]">{subjectLine}</p>
          )}

          <div className="mt-3 flex flex-wrap items-center gap-2 [&>*]:h-auto [&>*]:min-h-7 [&>*]:py-0.5 [&>*]:max-w-full [&>*]:px-3 [&>*]:text-footnote">
            <Badge
              tone={task.holder.unassigned ? 'warning' : 'brand'}
              title={t('processes.my.holder')}
            >
              <ProcessHolderLabel holder={task.holder} className="truncate" />
            </Badge>
            <ProcessBlankChip code={task.blankCode} title={task.blankTitle} />
            <ProcessDueChip dueDate={task.dueDate} daysLate={task.daysLate} today={today} />
            <ProcessWarningChips codes={task.warnings} />
            {task.legacy && <ProcessLegacyBadge />}
          </div>

          {notice}

          {notice === undefined && (gateMessage !== null || resolver !== null) && (
            <div className="mt-3 rounded-lg border-l-4 border-l-[var(--accent)] bg-[var(--accent-soft)] px-4 py-3 text-subhead">
              {gateMessage !== null && <p className="font-medium">{gateMessage}</p>}
              {resolver !== null && (
                <p
                  className={cn(
                    'text-footnote text-[var(--fg-secondary)]',
                    gateMessage !== null && 'mt-1',
                  )}
                >
                  {t('processes.my.resolver')}: <ProcessHolderLabel holder={resolver} />
                </p>
              )}
            </div>
          )}
        </div>

        <div className="flex shrink-0 flex-wrap items-center gap-2 sm:flex-col sm:items-end">
          {/* Only the look of the action: the stretched title above is what takes the click and the focus. */}
          <span
            aria-hidden
            className="inline-flex h-9 items-center gap-1 rounded-full bg-[var(--accent-soft)] pr-2.5 pl-4 text-subhead font-semibold text-[var(--accent-on-soft)]"
          >
            {t('processes.my.open')}
            <ChevronRight strokeWidth={2.5} className="size-4" />
          </span>

          {Boolean(actions) && (
            <div className="relative z-10 flex flex-wrap items-center gap-2 sm:justify-end">
              {actions}
            </div>
          )}

          {actions === undefined && action !== null && (
            <Button
              variant="ghost"
              size="sm"
              className="relative z-10"
              onClick={() => {
                if (action.kind === 'external') {
                  window.open(action.url, '_blank', 'noopener,noreferrer')
                } else {
                  void navigate({ href: action.href })
                }
              }}
            >
              {action.kind === 'external' ? (
                <>
                  <ExternalLink />
                  {t('processes.my.link.external')}
                </>
              ) : action.label === 'blank' ? (
                <>
                  <FileText />
                  {t('processes.my.link.blank')}
                </>
              ) : (
                <>
                  <ArrowUpRight />
                  {t('processes.my.link.screen')}
                </>
              )}
            </Button>
          )}
        </div>
      </div>
    </Card>
  )
}
