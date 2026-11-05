import { getRouteApi, Link } from '@tanstack/react-router'
import { Kanban, UserMinus } from 'lucide-react'
import { useMemo } from 'react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'
import { Button, Card, EmptyState, SegmentedControl, Select, Skeleton } from '@/shared/ui'

import type { ProcessBoardCard, ProcessTemplate } from '../api/processes-api'
import { useProcessBoard, useProcessTemplates } from '../api/processes-queries'
import { PROCESSES_WORK } from '../lib/permissions'
import { sortProcessTemplates } from '../lib/process-board'
import {
  ProcessCountChip,
  ProcessDueChip,
  ProcessHolderLabel,
  ProcessLegacyBadge,
  ProcessStateBadge,
  ProcessWarningChips,
  processSubjectLine,
} from './process-chips'
import { ProcessCasesTable } from './process-cases-table'
import { ProcessLoadError, ProcessListSkeleton } from './process-states'

const route = getRouteApi('/_app/mening-ishlarim/')

type BoardView = 'board' | 'list'

const NO_TEMPLATES: ProcessTemplate[] = []

/**
 * Tab «Jarayonlar» (SPEC §11.3): one process as a board, or every case as a filtered list. The choice of process and view
 * is in the address, so «Orqaga» from a case returns to the same place.
 */
export function BoardTab() {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const search = route.useSearch()
  const navigate = route.useNavigate()
  const templatesQuery = useProcessTemplates()

  const templates = useMemo(
    () => sortProcessTemplates(templatesQuery.data ?? NO_TEMPLATES),
    [templatesQuery.data],
  )

  const view: BoardView = search.view ?? 'board'
  // A stale code in the address (a process since retired) falls back rather than ending on an error.
  const knownProcess = templates.find((template) => template.code === search.process)?.code

  const changeView = (next: BoardView) => {
    void navigate({
      search: (previous) => ({
        ...previous,
        view: next === 'list' ? 'list' : undefined,
        page: undefined,
      }),
      replace: true,
    })
  }

  const changeProcess = (code: string) => {
    void navigate({
      search: (previous) => ({ ...previous, process: code === '' ? undefined : code, page: undefined }),
      replace: true,
    })
  }

  if (templatesQuery.isPending) return <ProcessListSkeleton />

  if (templatesQuery.isError) {
    return (
      <ProcessLoadError
        error={templatesQuery.error}
        onRetry={() => void templatesQuery.refetch()}
      />
    )
  }

  if (templates.length === 0) {
    return (
      <Card>
        <EmptyState
          icon={Kanban}
          title={t('processes.board.noTemplatesTitle')}
          description={t('processes.board.noTemplatesText')}
        />
      </Card>
    )
  }

  const boardProcess = knownProcess ?? templates[0]?.code

  return (
    <div className="flex flex-col gap-4 frame:min-h-0 frame:flex-1">
      <Card className="flex flex-wrap items-center gap-2.5 rounded-lg px-4 py-3 sm:px-5">
        <Select
          size="sm"
          aria-label={t('processes.board.process')}
          className="w-full sm:w-[320px]"
          placeholder={view === 'list' ? t('processes.board.allProcesses') : undefined}
          options={templates.map((template) => ({ value: template.code, label: template.title }))}
          value={view === 'list' ? (knownProcess ?? '') : (boardProcess ?? '')}
          onChange={(event) => changeProcess(event.target.value)}
        />
        <SegmentedControl<BoardView>
          size="sm"
          value={view}
          onChange={changeView}
          aria-label={t('processes.board.view')}
          options={[
            { value: 'board', label: t('processes.board.viewBoard') },
            { value: 'list', label: t('processes.board.viewList') },
          ]}
        />
        {hasPermission(PROCESSES_WORK) && (
          // Stays disabled until slice P7 brings `POST /cases` and this button gets its start dialog.
          <Button
            type="button"
            variant="create"
            disabled
            title={t('processes.board.startSoon')}
            className="sm:ml-auto"
          >
            <UserMinus />
            {t('processes.board.startDismissal')}
          </Button>
        )}
      </Card>

      {view === 'list' ? (
        <ProcessCasesTable process={knownProcess} />
      ) : (
        <ProcessBoardColumns processCode={boardProcess} />
      )}
    </div>
  )
}

function ProcessBoardColumns({ processCode }: { processCode: string | undefined }) {
  const { t, num } = useLocale()
  const query = useProcessBoard(processCode)
  const today = todayIso()

  if (query.isPending) {
    return (
      <div aria-busy="true" className="flex flex-col gap-4 md:flex-row md:overflow-hidden">
        {[0, 1, 2, 3].map((column) => (
          <Skeleton key={column} className="h-72 w-full shrink-0 rounded-lg md:w-72" />
        ))}
      </div>
    )
  }

  if (query.isError) {
    return <ProcessLoadError error={query.error} onRetry={() => void query.refetch()} />
  }

  const { columns } = query.data

  if (columns.every((column) => column.cards.length === 0)) {
    return (
      <Card>
        <EmptyState
          icon={Kanban}
          title={t('processes.board.emptyTitle')}
          description={t('processes.board.emptyText')}
        />
      </Card>
    )
  }

  return (
    <div className="flex flex-col gap-4 md:-mx-1 md:flex-row md:items-start md:overflow-x-auto md:px-1 md:pb-2">
      {columns.map((column, index) => (
        // A phone stacks the columns, and an empty one there would only push the work further down.
        <Card
          key={column.key}
          className={cn(
            'w-full shrink-0 flex-col gap-3 rounded-lg p-4 md:flex md:w-72',
            column.cards.length === 0 ? 'hidden' : 'flex',
          )}
        >
          <h3 className="flex items-center justify-between gap-2 text-footnote font-semibold tracking-wide uppercase">
            <span className="min-w-0 break-words">
              {num(index + 1)}. {column.title}
            </span>
            <ProcessCountChip>{num(column.cards.length)}</ProcessCountChip>
          </h3>
          {column.cards.length === 0 ? (
            <p className="py-3 text-center text-footnote text-[var(--fg-secondary)]">
              {t('processes.board.columnEmpty')}
            </p>
          ) : (
            column.cards.map((card) => <BoardCard key={card.caseId} card={card} today={today} />)
          )}
        </Card>
      ))}
    </div>
  )
}

/** Subject, current step, holder, due (SPEC §11.3). A case standing on several steps names the first and counts the rest. */
function BoardCard({ card, today }: { card: ProcessBoardCard; today: string }) {
  const { t, num } = useLocale()
  const [step, ...otherSteps] = card.currentSteps

  return (
    <Link
      to="/mening-ishlarim/$caseId"
      params={{ caseId: card.caseId }}
      className="block space-y-2 rounded-lg border border-[var(--border)] p-3 transition-colors hover:bg-[var(--bg-hover)]"
    >
      <p className="text-subhead font-semibold break-words">
        {processSubjectLine(card.subject, card.departmentName) || t('processes.board.noSubject')}
      </p>

      {step !== undefined && (
        <div className="space-y-1 text-footnote">
          <p>{step.title}</p>
          <ProcessHolderLabel
            holder={step.holder}
            className="block text-[var(--fg-secondary)]"
          />
        </div>
      )}

      {otherSteps.length > 0 && (
        <p className="text-footnote text-[var(--fg-secondary)]">
          {t('processes.board.moreSteps', { n: num(otherSteps.length) })}
        </p>
      )}

      <div className="flex flex-wrap items-center gap-1.5">
        {step !== undefined && step.state !== 'Active' && <ProcessStateBadge state={step.state} />}
        <ProcessDueChip dueDate={card.dueDate} daysLate={card.daysLate} today={today} />
        {card.legacy && <ProcessLegacyBadge />}
        {step !== undefined && <ProcessWarningChips codes={step.warnings} />}
      </div>
    </Link>
  )
}
