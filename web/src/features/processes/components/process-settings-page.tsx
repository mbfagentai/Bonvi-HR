import { useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Settings2 } from 'lucide-react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { useReturnTo } from '@/shared/lib/return-to'
import { Button, IconTile, PageHeader } from '@/shared/ui'

import { MY_WORK_PATH } from '../lib/landing'
import { PROCESSES_MANAGE } from '../lib/permissions'
import { ProcessNoAccess, ProcessPlaceholder } from './process-states'

/** `/mening-ishlarim/sozlamalar` (SPEC §11.7, `processes.manage`). Placeholder until P12. */
export function ProcessSettingsPage() {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const navigate = useNavigate()
  const back = useReturnTo(() => void navigate({ to: MY_WORK_PATH }))

  return (
    <div className="space-y-5">
      <Button variant="ghost" size="sm" onClick={back.go} className="-ml-2">
        <ArrowLeft />
        {back.label}
      </Button>

      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={Settings2} tone="brand" size="lg" />
            {t('processes.settings.title')}
          </span>
        }
      />

      {hasPermission(PROCESSES_MANAGE) ? (
        <ProcessPlaceholder
          icon={Settings2}
          title={t('processes.settings.title')}
          description={t('processes.placeholder')}
        />
      ) : (
        <ProcessNoAccess />
      )}
    </div>
  )
}
