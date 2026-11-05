import { Hourglass, Inbox, ListChecks, Lock, Sparkles, Sun } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Card, EmptyState, Skeleton } from '@/shared/ui'

import type { ProcessMyWork } from '../api/processes-api'
import { useMyWork } from '../api/processes-queries'
import { hasNoTasks, isMyWorkEmpty, type MyWorkSection, sectionTotal } from '../lib/my-work'
import { ProcessLoadError } from './process-states'
import { ProcessTartibCards } from './process-tartib-cards'
import { ProcessTaskCard, ProcessTaskSection } from './process-task-card'

function TaskList({ work, section }: { work: ProcessMyWork; section: MyWorkSection }) {
  const { t, num } = useLocale()
  const tasks = work[section]
  const total = sectionTotal(work, section)

  return (
    <>
      <ul className="space-y-3">
        {tasks.map((task) => (
          <li key={`${task.caseId}:${task.stepCode}`}>
            <ProcessTaskCard task={task} today={work.today} />
          </li>
        ))}
      </ul>
      {/* The API sends the soonest 200; the rest exist, so the list must not read as complete. */}
      {total > tasks.length && (
        <p className="px-1 text-footnote text-[var(--fg-secondary)]">
          {t('processes.my.sectionCut', { n: num(tasks.length), total: num(total) })}
        </p>
      )}
    </>
  )
}

function Summary({ work }: { work: ProcessMyWork }) {
  const { t, num, date } = useLocale()

  return (
    <Card className="rounded-lg px-5 py-5">
      <p className="text-title1 leading-tight font-bold">
        {t('processes.my.today', { date: date(work.today) })}
      </p>
      {work.count > 0 && (
        <p className="mt-1.5 text-subhead text-[var(--fg-secondary)]">
          {work.lateCount > 0
            ? t('processes.my.summary', { n: num(work.count), k: num(work.lateCount) })
            : t('processes.my.summaryNoLate', { n: num(work.count) })}
        </p>
      )}
    </Card>
  )
}

function MyWorkLoading() {
  return (
    <div className="space-y-6" aria-busy="true">
      <Card className="space-y-2.5 rounded-lg px-5 py-5">
        <Skeleton className="h-8 w-64 rounded-md" />
        <Skeleton className="h-4 w-40 rounded-md" />
      </Card>
      <div className="space-y-3">
        <Skeleton className="h-36 w-full rounded-lg" />
        <Skeleton className="h-36 w-full rounded-lg" />
        <Skeleton className="h-36 w-full rounded-lg" />
      </div>
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

  return (
    <>
      <Summary work={work} />

      {hasNoTasks(work) ? (
        <Card>
          <EmptyState icon={Inbox} title={t('processes.my.emptyTitle')} className="py-8" />
        </Card>
      ) : (
        <>
          {/* The first section stays even when empty: «nothing for today» is the news someone opens this page for. */}
          <ProcessTaskSection
            icon={Sun}
            title={t('processes.my.sections.urgent')}
            count={sectionTotal(work, 'urgent')}
          >
            {work.urgent.length > 0 ? (
              <TaskList work={work} section="urgent" />
            ) : (
              <Card className="flex items-center gap-2.5 rounded-lg px-5 py-4 text-subhead text-[var(--fg-secondary)]">
                <Sparkles aria-hidden className="size-4 shrink-0 text-ios-green" strokeWidth={2} />
                {t('processes.my.urgentEmpty')}
              </Card>
            )}
          </ProcessTaskSection>

          {work.later.length > 0 && (
            <ProcessTaskSection
              icon={ListChecks}
              title={t('processes.my.sections.later')}
              count={sectionTotal(work, 'later')}
            >
              <TaskList work={work} section="later" />
            </ProcessTaskSection>
          )}

          {work.held.length > 0 && (
            <ProcessTaskSection
              icon={Lock}
              title={t('processes.my.sections.held')}
              count={sectionTotal(work, 'held')}
            >
              <TaskList work={work} section="held" />
            </ProcessTaskSection>
          )}

          {work.waiting.length > 0 && (
            <ProcessTaskSection
              icon={Hourglass}
              title={t('processes.my.sections.waiting')}
              count={sectionTotal(work, 'waiting')}
            >
              <TaskList work={work} section="waiting" />
            </ProcessTaskSection>
          )}
        </>
      )}

      {(work.tartib.notifications.length > 0 || work.tartib.counts.length > 0) && (
        <ProcessTaskSection icon={ListChecks} title={t('processes.my.sections.tartib')}>
          <ProcessTartibCards tartib={work.tartib} />
        </ProcessTaskSection>
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
