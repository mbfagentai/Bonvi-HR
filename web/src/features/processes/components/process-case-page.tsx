import { getRouteApi, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, ListChecks } from 'lucide-react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { useReturnTo } from '@/shared/lib/return-to'
import { Button, PageHeader } from '@/shared/ui'

import { useProcessCase } from '../api/processes-queries'
import { MY_WORK_PATH } from '../lib/landing'
import { PROCESSES_VIEW } from '../lib/permissions'
import { processSubjectLine, ProcessLegacyBadge, ProcessStatusBadge } from './process-chips'
import {
  ProcessListSkeleton,
  ProcessLoadError,
  ProcessNoAccess,
  ProcessPlaceholder,
} from './process-states'

const route = getRouteApi('/_app/mening-ishlarim/$caseId')

/** `/mening-ishlarim/{caseId}` (SPEC §11.5). The real heading and all states; the step timeline arrives in P11. */
export function ProcessCasePage() {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const { caseId } = route.useParams()
  const navigate = useNavigate()
  const canView = hasPermission(PROCESSES_VIEW)
  const query = useProcessCase(caseId, canView)
  const back = useReturnTo(() => void navigate({ to: MY_WORK_PATH }))

  const backButton = (
    <Button variant="ghost" size="sm" onClick={back.go} className="-ml-2">
      <ArrowLeft />
      {back.label}
    </Button>
  )

  if (!canView) {
    return (
      <div className="space-y-5">
        {backButton}
        <ProcessNoAccess />
      </div>
    )
  }

  if (query.isPending) {
    return (
      <div className="space-y-5">
        {backButton}
        <ProcessListSkeleton />
      </div>
    )
  }

  if (query.isError) {
    return (
      <div className="space-y-5">
        {backButton}
        <ProcessLoadError error={query.error} onRetry={() => void query.refetch()} />
      </div>
    )
  }

  const detail = query.data

  return (
    <div className="space-y-5">
      {backButton}
      <PageHeader
        title={detail.processTitle}
        subtitle={processSubjectLine(detail.subject, detail.departmentName)}
        actions={
          <>
            {detail.legacy && <ProcessLegacyBadge />}
            <ProcessStatusBadge status={detail.status} />
          </>
        }
      />
      <ProcessPlaceholder
        icon={ListChecks}
        title={t('processes.case.stepsTitle')}
        description={t('processes.placeholder')}
      />
    </div>
  )
}
