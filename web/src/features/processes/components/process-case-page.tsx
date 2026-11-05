import { getRouteApi, Link, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, FileText, ListChecks } from 'lucide-react'
import { useRef } from 'react'

import { STATUS_KEY, STATUS_TONE } from '@/features/blanks/lib/blank-presentation'
import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { useReturnTo } from '@/shared/lib/return-to'
import { Alert, Badge, Button, Card, CardContent, EmptyState, PageHeader } from '@/shared/ui'

import type { ProcessCaseDetail, ProcessLinkedBlank } from '../api/processes-api'
import { useProcessCase } from '../api/processes-queries'
import { MY_WORK_PATH } from '../lib/landing'
import { PROCESSES_VIEW } from '../lib/permissions'
import { defaultStepCode } from '../lib/process-step'
import { processSubjectLine, ProcessLegacyBadge, ProcessStatusBadge } from './process-chips'
import { ProcessListSkeleton, ProcessLoadError, ProcessNoAccess } from './process-states'
import { ProcessStepPanel } from './process-step-panel'
import { ProcessStepTimeline } from './process-step-timeline'

const route = getRouteApi('/_app/mening-ishlarim/$caseId')

/** Beside each other from here up; the same breakpoint as the grid below. */
const SIDE_BY_SIDE = '(min-width: 64rem)'

/**
 * `/mening-ishlarim/{caseId}` (SPEC §11.5): the steps by column on one side, the chosen step's panel on the other. The
 * chosen step lives in the address (`?step=`), so a task card can link straight to it and a reload keeps it.
 */
export function ProcessCasePage() {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const { caseId } = route.useParams()
  const { step: stepInAddress } = route.useSearch()
  const navigate = useNavigate()
  const routeNavigate = route.useNavigate()
  const canView = hasPermission(PROCESSES_VIEW)
  const query = useProcessCase(caseId, canView)
  const back = useReturnTo(() => void navigate({ to: MY_WORK_PATH }))
  const panelRef = useRef<HTMLDivElement>(null)

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
  const selectedCode =
    stepInAddress !== undefined && detail.steps.some((step) => step.stepCode === stepInAddress)
      ? stepInAddress
      : defaultStepCode(detail.steps)
  const selected = detail.steps.find((step) => step.stepCode === selectedCode)

  const selectStep = (stepCode: string) => {
    void routeNavigate({ search: { step: stepCode }, replace: true })
    // On a narrow screen the panel is below the list, out of sight of the row just pressed.
    if (!window.matchMedia(SIDE_BY_SIDE).matches) {
      panelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }

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

      <CaseFacts detail={detail} />

      {detail.steps.length === 0 ? (
        <Card>
          <EmptyState icon={ListChecks} title={t('processes.case.noSteps')} />
        </Card>
      ) : (
        <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.15fr)]">
          <div className="space-y-5">
            <Card>
              <CardContent>
                <h2 className="mb-4 px-2.5 text-headline font-semibold">
                  {t('processes.case.stepsTitle')}
                </h2>
                <ProcessStepTimeline
                  columns={detail.columns}
                  steps={detail.steps}
                  selectedStepCode={selectedCode}
                  onSelect={selectStep}
                />
              </CardContent>
            </Card>
            <CaseBlanks blanks={detail.blanks} />
          </div>

          <div ref={panelRef} className="scroll-mt-4 lg:sticky lg:top-4">
            {selected !== undefined && (
              <Card>
                <CardContent>
                  <ProcessStepPanel detail={detail} step={selected} />
                </CardContent>
              </Card>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

/** The case's dates in one line, and why it was cancelled if it was. */
function CaseFacts({ detail }: { detail: ProcessCaseDetail }) {
  const { t, date } = useLocale()

  const facts = [
    t('processes.case.startedOn', { date: date(detail.startedOn) }),
    detail.anchorDate !== null && t('processes.case.anchorDate', { date: date(detail.anchorDate) }),
    detail.completedAt !== null &&
      t('processes.case.completedAt', { date: date(detail.completedAt) }),
    detail.cancelledAt !== null &&
      t('processes.case.cancelledAt', { date: date(detail.cancelledAt) }),
  ].filter((fact) => fact !== false)

  return (
    <div className="space-y-3">
      <p className="tabular flex flex-wrap items-center gap-x-4 gap-y-1 text-footnote text-[var(--fg-secondary)]">
        {facts.map((fact) => (
          <span key={fact}>{fact}</span>
        ))}
        {detail.parentCaseId !== null && (
          <Link
            to="/mening-ishlarim/$caseId"
            params={{ caseId: detail.parentCaseId }}
            className="text-[var(--accent)] hover:underline"
          >
            {t('processes.case.parentCase')}
          </Link>
        )}
      </p>
      {detail.cancelReason !== null && (
        <Alert tone="info" title={t('processes.case.cancelReason')}>
          {detail.cancelReason}
        </Alert>
      )}
    </div>
  )
}

/** The blanks linked to this case (SPEC §10 #5), each opening on its own page. */
function CaseBlanks({ blanks }: { blanks: readonly ProcessLinkedBlank[] }) {
  const { t, num, date } = useLocale()

  return (
    <Card>
      <CardContent>
        <h2 className="mb-3 px-2.5 text-headline font-semibold">{t('processes.case.blanks')}</h2>
        {blanks.length === 0 ? (
          <p className="px-2.5 text-subhead text-[var(--fg-secondary)]">
            {t('processes.case.blanksEmpty')}
          </p>
        ) : (
          <ul className="space-y-0.5">
            {blanks.map((blank) => (
              <li key={blank.id}>
                <Link
                  to="/blankalar/$submissionId"
                  params={{ submissionId: blank.id }}
                  className="flex items-center gap-2.5 rounded-md px-2.5 py-2 transition-colors hover:bg-[var(--bg-hover)]"
                >
                  <FileText aria-hidden className="size-4 shrink-0 text-[var(--fg-secondary)]" />
                  <span className="min-w-0 flex-1 truncate text-subhead">
                    {blank.templateCode}
                    {blank.templateTitle !== null && ` · ${blank.templateTitle}`}
                    {blank.number !== null && ` · № ${num(blank.number)}`}
                  </span>
                  {blank.completedAt === null && blank.dueDate !== null && (
                    <span className="tabular text-footnote text-[var(--fg-secondary)]">
                      {date(blank.dueDate)}
                    </span>
                  )}
                  <BlankStatusBadge status={blank.status} />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

function isBlankStatus(status: string): status is keyof typeof STATUS_KEY {
  return status in STATUS_KEY
}

/** The API sends the status as free text, so one this build does not know is shown as is. */
function BlankStatusBadge({ status }: { status: string }) {
  const { t } = useLocale()

  return isBlankStatus(status) ? (
    <Badge tone={STATUS_TONE[status]}>{t(STATUS_KEY[status])}</Badge>
  ) : (
    <Badge tone="neutral">{status}</Badge>
  )
}
