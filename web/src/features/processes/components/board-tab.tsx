import { getRouteApi, Link } from '@tanstack/react-router'
import { Kanban, UserMinus } from 'lucide-react'
import { useMemo } from 'react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
import { Button, Card, EmptyState, SegmentedControl, Select, Skeleton } from '@/shared/ui'

import type { ProcessBoardCard, ProcessTemplate } from '../api/processes-api'
import { useProcessBoard, useProcessTemplates } from '../api/processes-queries'
import { PROCESSES_WORK } from '../lib/permissions'
import { sortProcessTemplates } from '../lib/process-board'
import {
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
      <div className="flex flex-wrap items-center gap-2.5">
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
      </div>

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
      <div aria-busy="true" className="flex gap-3 overflow-hidden">
        {[0, 1, 2, 3].map((column) => (
          <Skeleton key={column} className="h-72 w-72 shrink-0 rounded-lg" />
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
    <div className="-mx-1 flex items-start gap-3 overflow-x-auto px-1 pb-2">
      {columns.map((column) => (
        <section
          key={column.key}
          className="flex w-72 shrink-0 flex-col gap-2 rounded-lg bg-[var(--bg-sunken)] p-2.5"
        >
          <h3 className="flex items-center justify-between gap-2 px-1 text-footnote font-semibold text-[var(--fg-secondary)]">
            <span className="truncate">{column.title}</span>
            <span className="tabular">{num(column.cards.length)}</span>
          </h3>
          {column.cards.length === 0 ? (
            <p className="px-1 py-3 text-center text-footnote text-[var(--fg-tertiary)]">
              {t('processes.board.columnEmpty')}
            </p>
          ) : (
            column.cards.map((card) => <BoardCard key={card.caseId} card={card} today={today} />)
          )}
        </section>
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
      className="block space-y-2 rounded-md bg-[var(--bg-elevated)] p-3 shadow-sm transition-colors hover:bg-[var(--bg-hover)]"
    >
      <p className="text-subhead font-medium">
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
