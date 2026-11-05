import { Link, useNavigate } from '@tanstack/react-router'
import { ArrowUpRight, ChevronRight, ExternalLink, FileText, Lock, User } from 'lucide-react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button, Card } from '@/shared/ui'

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
  late: 'bg-ios-red',
  today: 'bg-ios-yellow',
  blocked: 'bg-ios-gray',
  normal: 'bg-[var(--border)]',
}

/**
 * One task on «Mening ishlarim». The whole card opens the case: the title is the only link, stretched over the card, so
 * a screen reader hears one link and the second button stays a real, separate control above it.
 */
export function ProcessTaskCard({ task, today }: { task: ProcessTask; today: string }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()

  const tone = taskTone(task.state, task.dueDate, task.daysLate, today)
  const subjectLine = processSubjectLine(task.subject, task.departmentName)
  const resolver = taskResolver(task)
  const action = taskLinkAction(task)
  const hasChips =
    task.daysLate > 0 ||
    task.dueDate !== null ||
    task.blankCode !== null ||
    task.warnings.length > 0 ||
    task.legacy

  return (
    <Card
      className={cn(
        'group relative overflow-hidden',
        'transition-colors duration-[120ms] ease-[var(--ease-ios)] hover:bg-[var(--bg-hover)]',
      )}
    >
      <span aria-hidden className={cn('absolute inset-y-0 left-0 w-1', STRIPE[tone])} />

      <div className="flex items-start gap-3 py-3.5 pr-3 pl-5">
        <div className="min-w-0 flex-1">
          <p className="truncate-1 text-caption1 text-[var(--fg-tertiary)]">{task.processTitle}</p>

          <h3 className="mt-0.5 flex items-start gap-1.5 text-callout leading-snug font-semibold">
            {task.state === 'Blocked' && (
              <Lock
                aria-hidden
                strokeWidth={2.5}
                className="mt-[3px] size-4 shrink-0 text-ios-gray"
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
            <p className="mt-0.5 text-footnote text-[var(--fg-secondary)]">{subjectLine}</p>
          )}

          {hasChips && (
            <div className="mt-2 flex flex-wrap items-center gap-1.5">
              <ProcessDueChip dueDate={task.dueDate} daysLate={task.daysLate} today={today} />
              <ProcessBlankChip code={task.blankCode} title={task.blankTitle} />
              <ProcessWarningChips codes={task.warnings} />
              {task.legacy && <ProcessLegacyBadge />}
            </div>
          )}

          {task.state === 'Blocked' && task.gateErrorCode !== null && (
            <p className="mt-2 text-footnote text-[var(--fg-secondary)]">
              {errorMessage(task.gateErrorCode)}
            </p>
          )}

          <div className="mt-2 flex flex-wrap items-center justify-between gap-x-3 gap-y-1.5">
            <div className="min-w-0 space-y-0.5 text-caption1 text-[var(--fg-tertiary)]">
              <p className="flex items-center gap-1.5">
                <User aria-hidden className="size-3.5 shrink-0" />
                <span className="min-w-0">
                  {t('processes.my.holder')}: <ProcessHolderLabel holder={task.holder} />
                </span>
              </p>

              {resolver !== null && (
                <p className="flex items-center gap-1.5">
                  <Lock aria-hidden className="size-3.5 shrink-0" />
                  <span className="min-w-0">
                    {t('processes.my.resolver')}: <ProcessHolderLabel holder={resolver} />
                  </span>
                </p>
              )}
            </div>

            {action !== null && (
              <Button
                variant="ghost"
                size="sm"
                className="relative z-10 -my-1"
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

        <ChevronRight
          aria-hidden
          strokeWidth={2.5}
          className="mt-5 size-4 shrink-0 text-[var(--fg-tertiary)]"
        />
      </div>
    </Card>
  )
}
