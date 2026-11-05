import { getRouteApi, useNavigate } from '@tanstack/react-router'
import { ClipboardCheck, Settings2 } from 'lucide-react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button, IconTile, PageHeader } from '@/shared/ui'

import { MY_WORK_PATH } from '../lib/landing'
import { PROCESSES_MANAGE, PROCESSES_VIEW } from '../lib/permissions'
import type { ProcessTab } from '../lib/processes-search'
import { BoardTab } from './board-tab'
import { LateTab } from './late-tab'
import { MyWorkTab } from './my-work-tab'
import { PROCESS_PAGE_CLASS } from './process-chips'
import { ProcessNoAccess } from './process-states'

const route = getRouteApi('/_app/mening-ishlarim/')

/**
 * The section's tabs, as a white card with pale-blue active pills. Each tab takes the width its label needs and a long
 * label wraps inside its own pill, so at 390 px, in any language, two labels never touch and nothing scrolls sideways.
 */
function ProcessTabs({ value, onChange }: { value: ProcessTab; onChange: (next: ProcessTab) => void }) {
  const { t } = useLocale()
  const tabs: ReadonlyArray<{ value: ProcessTab; label: string }> = [
    { value: 'my', label: t('processes.tabs.my') },
    { value: 'board', label: t('processes.tabs.board') },
    { value: 'late', label: t('processes.tabs.late') },
  ]

  return (
    <div
      role="tablist"
      aria-label={t('processes.tabs.label')}
      className="flex w-full gap-1 rounded-lg border border-[var(--border)] bg-[var(--bg-elevated)] p-1 shadow-card sm:w-fit"
    >
      {tabs.map((tab) => {
        const selected = tab.value === value

        return (
          <button
            key={tab.value}
            type="button"
            role="tab"
            aria-selected={selected}
            onClick={() => onChange(tab.value)}
            className={cn(
              'min-h-9 flex-auto rounded-md px-2.5 py-1.5 text-center text-footnote leading-tight font-medium sm:px-4',
              'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
              selected
                ? 'bg-[var(--accent-soft)] font-semibold text-[var(--accent-on-soft)]'
                : 'text-[var(--fg-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)]',
            )}
          >
            {tab.label}
          </button>
        )
      })}
    </div>
  )
}

/**
 * `/mening-ishlarim` (SPEC §11.1): the header and the three tabs. Each tab owns its data, and only the open one is
 * mounted, so opening the page asks for one tab's data. The tab lives in the address: reload and «Orqaga» keep it.
 */
export function ProcessesPage() {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const { tab = 'my', view } = route.useSearch()
  const navigate = useNavigate()

  const header = (
    <PageHeader
      title={
        <span className="flex items-center gap-3">
          <IconTile icon={ClipboardCheck} tone="brand" size="lg" />
          {t('processes.title')}
        </span>
      }
      actions={
        hasPermission(PROCESSES_MANAGE) ? (
          <Button
            variant="secondary"
            onClick={() => void navigate({ to: '/mening-ishlarim/sozlamalar' })}
          >
            <Settings2 />
            {t('processes.settings.title')}
          </Button>
        ) : undefined
      }
    />
  )

  if (!hasPermission(PROCESSES_VIEW)) {
    return (
      <div className={cn(PROCESS_PAGE_CLASS, 'space-y-5')}>
        {header}
        <ProcessNoAccess />
      </div>
    )
  }

  const changeTab = (next: ProcessTab) => {
    void navigate({ to: MY_WORK_PATH, search: next === 'my' ? {} : { tab: next } })
  }

  // Only the list view of «Jarayonlar» is a framed list (`app-shell.tsx`): its rows scroll inside the card.
  const framed = tab === 'board' && view === 'list'

  return (
    <div
      data-list-frame={framed ? '' : undefined}
      className={cn('space-y-5', framed ? 'frame:flex frame:h-full frame:flex-col' : PROCESS_PAGE_CLASS)}
    >
      {header}

      <ProcessTabs value={tab} onChange={changeTab} />

      {tab === 'my' && <MyWorkTab />}
      {tab === 'board' && <BoardTab />}
      {tab === 'late' && <LateTab />}
    </div>
  )
}
