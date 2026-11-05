import { getRouteApi, useNavigate } from '@tanstack/react-router'
import { ClipboardCheck, Settings2 } from 'lucide-react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { Button, IconTile, PageHeader, SegmentedControl } from '@/shared/ui'

import { MY_WORK_PATH } from '../lib/landing'
import { PROCESSES_MANAGE, PROCESSES_VIEW } from '../lib/permissions'
import type { ProcessTab } from '../lib/processes-search'
import { BoardTab } from './board-tab'
import { LateTab } from './late-tab'
import { MyWorkTab } from './my-work-tab'
import { ProcessNoAccess } from './process-states'

const route = getRouteApi('/_app/mening-ishlarim/')

/**
 * `/mening-ishlarim` (SPEC §11.1): the header and the three tabs. Each tab owns its data, and only the open one is
 * mounted, so opening the page asks for one tab's data. The tab lives in the address: reload and «Orqaga» keep it.
 */
export function ProcessesPage() {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const { tab = 'my' } = route.useSearch()
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
      <div className="space-y-5">
        {header}
        <ProcessNoAccess />
      </div>
    )
  }

  const changeTab = (next: ProcessTab) => {
    void navigate({ to: MY_WORK_PATH, search: next === 'my' ? {} : { tab: next } })
  }

  return (
    <div className="space-y-5">
      {header}

      <SegmentedControl<ProcessTab>
        value={tab}
        onChange={changeTab}
        aria-label={t('processes.tabs.label')}
        className="w-full max-w-xl"
        options={[
          { value: 'my', label: t('processes.tabs.my') },
          { value: 'board', label: t('processes.tabs.board') },
          { value: 'late', label: t('processes.tabs.late') },
        ]}
      />

      {tab === 'my' && <MyWorkTab />}
      {tab === 'board' && <BoardTab />}
      {tab === 'late' && <LateTab />}
    </div>
  )
}
