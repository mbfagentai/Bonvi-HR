import { Hourglass, Inbox, ListChecks, Lock, Sparkles, Sun, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Card, EmptyState, Skeleton } from '@/shared/ui'

import type { ProcessMyWork, ProcessTask } from '../api/processes-api'
import { useMyWork } from '../api/processes-queries'
import { isMyWorkEmpty, hasNoTasks, type MyWorkSection } from '../lib/my-work'
import { ProcessListSkeleton, ProcessLoadError } from './process-states'
import { ProcessTartibCards } from './process-tartib-cards'
import { ProcessTaskCard } from './process-task-card'

const SECTION_ICON: Record<MyWorkSection, LucideIcon> = {
  urgent: Sun,
  later: ListChecks,
  held: Lock,
  waiting: Hourglass,
}

function Section({
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
    <section className="space-y-2.5">
      <h2 className="flex items-center gap-2 px-1 text-subhead font-semibold text-[var(--fg-secondary)]">
        <Icon aria-hidden className="size-4 shrink-0" strokeWidth={2} />
        <span className="min-w-0">{title}</span>
        {count !== undefined && (
          <span className="tabular font-medium text-[var(--fg-tertiary)]">{num(count)}</span>
        )}
      </h2>
      {children}
    </section>
  )
}

function TaskList({ tasks, today }: { tasks: readonly ProcessTask[]; today: string }) {
  return (
    <ul className="grid gap-2.5 xl:grid-cols-2">
      {tasks.map((task) => (
        <li key={`${task.caseId}:${task.stepCode}`}>
          <ProcessTaskCard task={task} today={today} />
        </li>
      ))}
    </ul>
  )
}

function Summary({ work }: { work: ProcessMyWork }) {
  const { t, num, date } = useLocale()

  return (
    <div className="px-1">
      <p className="text-title3 leading-tight font-semibold">
        {t('processes.my.today', { date: date(work.today) })}
      </p>
      {work.count > 0 && (
        <p className="mt-1 text-subhead text-[var(--fg-secondary)]">
          {work.lateCount > 0
            ? t('processes.my.summary', { n: num(work.count), k: num(work.lateCount) })
            : t('processes.my.summaryNoLate', { n: num(work.count) })}
        </p>
      )}
    </div>
  )
}

function MyWorkLoading() {
  return (
    <div className="space-y-6" aria-busy="true">
      <div className="space-y-2 px-1">
        <Skeleton className="h-6 w-56 rounded-md" />
        <Skeleton className="h-4 w-40 rounded-md" />
      </div>
      <ProcessListSkeleton rows={3} />
    </div>
  )
}

function MyWorkContent({ work }: { work: ProcessMyWork }) {
  const { t } = useLocale()

  if (isMyWorkEmpty(work)) {
    return (
      <>
        <Summary work={work} />
        <Card>
          <EmptyState
            icon={Inbox}
            title={t('processes.my.emptyTitle')}
            description={t('processes.my.emptyText')}
          />
        </Card>
      </>
    )
  }

  const noTasks = hasNoTasks(work)
  const showTartib = work.tartib.notifications.length > 0 || work.tartib.counts.length > 0

  return (
    <>
      <Summary work={work} />

      {noTasks ? (
        <Card>
          <EmptyState
            icon={Inbox}
            title={t('processes.my.emptyTitle')}
            className="py-8"
          />
        </Card>
      ) : (
        <>
          {/* The first section stays even when empty: «nothing for today» is the news someone opens this page for. */}
          <Section
            icon={SECTION_ICON.urgent}
            title={t('processes.my.sections.urgent')}
            count={work.urgent.length}
          >
            {work.urgent.length > 0 ? (
              <TaskList tasks={work.urgent} today={work.today} />
            ) : (
              <Card className="flex items-center gap-2.5 px-4 py-3 text-subhead text-[var(--fg-secondary)]">
                <Sparkles aria-hidden className="size-4 shrink-0 text-ios-green" strokeWidth={2} />
                {t('processes.my.urgentEmpty')}
              </Card>
            )}
          </Section>

          {work.later.length > 0 && (
            <Section
              icon={SECTION_ICON.later}
              title={t('processes.my.sections.later')}
              count={work.later.length}
            >
              <TaskList tasks={work.later} today={work.today} />
            </Section>
          )}

          {work.held.length > 0 && (
            <Section
              icon={SECTION_ICON.held}
              title={t('processes.my.sections.held')}
              count={work.held.length}
            >
              <TaskList tasks={work.held} today={work.today} />
            </Section>
          )}

          {work.waiting.length > 0 && (
            <Section
              icon={SECTION_ICON.waiting}
              title={t('processes.my.sections.waiting')}
              count={work.waiting.length}
            >
              <TaskList tasks={work.waiting} today={work.today} />
            </Section>
          )}
        </>
      )}

      {showTartib && (
        <Section icon={ListChecks} title={t('processes.my.sections.tartib')}>
          <ProcessTartibCards tartib={work.tartib} />
        </Section>
      )}
    </>
  )
}

/** Tab «Mening ishlarim» (SPEC §11.2). `ProcessesPage` has already checked `processes.view`. */
export function MyWorkTab() {
  const query = useMyWork()

  // A failed refresh keeps what is on screen; only a first load with nothing to show falls to the error card.
  if (query.data === undefined) {
    return query.isError ? (
      <ProcessLoadError error={query.error} onRetry={() => void query.refetch()} />
    ) : (
      <MyWorkLoading />
    )
  }

  return (
    <div className="space-y-6">
      <MyWorkContent work={query.data} />
    </div>
  )
}
