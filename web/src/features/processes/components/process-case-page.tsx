import { getRouteApi, Link, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, ListChecks } from 'lucide-react'
import { useEffect, useRef, type ReactNode, type Ref } from 'react'

import { STATUS_KEY, STATUS_TONE } from '@/features/blanks/lib/blank-presentation'
import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { useReturnTo } from '@/shared/lib/return-to'
import { Alert, Badge, Card, EmptyState, Skeleton } from '@/shared/ui'

import type { ProcessCaseDetail, ProcessLinkedBlank } from '../api/processes-api'
import { useProcessCase } from '../api/processes-queries'
import { MY_WORK_PATH } from '../lib/landing'
import { PROCESSES_VIEW } from '../lib/permissions'
import { longDueDate } from '../lib/process-due'
import { defaultStepCode, stepProgress } from '../lib/process-step'
import { PROCESS_PAGE_CLASS, ProcessLegacyBadge, ProcessStatusBadge } from './process-chips'
import { ProcessLoadError, ProcessNoAccess } from './process-states'
import { ProcessStepGuide, ProcessStepPanel } from './process-step-panel'
import { ProcessStepTimeline } from './process-step-timeline'

const route = getRouteApi('/_app/mening-ishlarim/$caseId')

/** Beside the stepper from here up (`lg`, the grid below). Under it the panel is out of sight of the row just pressed. */
const SIDE_BY_SIDE = '(min-width: 64rem)'

/**
 * The stepper beside the panel and the guide. From `lg` the stepper stands beside a stack of the other two; from `xl` the
 * stack dissolves into the prototype's three columns.
 */
function CaseColumns({
  stepper,
  panel,
  guide,
  panelRef,
}: {
  stepper: ReactNode
  panel: ReactNode
  guide: ReactNode
  panelRef?: Ref<HTMLDivElement>
}) {
  return (
    <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(0,2fr)] xl:grid-cols-[minmax(260px,26fr)_minmax(0,63fr)_minmax(240px,28fr)]">
      <div className="min-w-0">{stepper}</div>
      <div className="flex min-w-0 flex-col gap-5 lg:sticky lg:top-4 xl:contents">
        <div ref={panelRef} className="min-w-0 scroll-mt-4 xl:sticky xl:top-4">
          {panel}
        </div>
        <div className="min-w-0 space-y-5 xl:sticky xl:top-4">{guide}</div>
      </div>
    </div>
  )
}

function CaseSkeleton() {
  return (
    <div className="space-y-5" aria-busy="true">
      <Card className="space-y-2.5 px-5 py-5">
        <Skeleton className="h-4 w-28 rounded-md" />
        <Skeleton className="h-8 w-64 max-w-full rounded-md" />
        <Skeleton className="h-4 w-80 max-w-full rounded-md" />
      </Card>
      <CaseColumns
        stepper={
          <Card className="space-y-4 p-5">
            {[0, 1, 2, 3, 4].map((row) => (
              <div key={row} className="flex gap-3">
                <Skeleton className="size-[26px] shrink-0 rounded-full" />
                <div className="min-w-0 flex-1 space-y-1.5">
                  <Skeleton className="h-4 w-full rounded-md" />
                  <Skeleton className="h-3 w-16 rounded-md" />
                </div>
              </div>
            ))}
          </Card>
        }
        panel={
          <Card className="space-y-4 p-5">
            <Skeleton className="h-6 w-2/3 rounded-full" />
            <Skeleton className="h-7 w-1/2 rounded-md" />
            <Skeleton className="h-24 w-full rounded-lg" />
          </Card>
        }
        guide={
          <Card className="space-y-3 p-5">
            <Skeleton className="h-5 w-32 rounded-md" />
            <Skeleton className="h-16 w-full rounded-md" />
          </Card>
        }
      />
    </div>
  )
}

/**
 * `/mening-ishlarim/{caseId}` (SPEC §11.5), in the look of the chef's prototype: the case on top, then the stepper, the
 * chosen step and its guide. The chosen step lives in the address (`?step=`), so a task card can link straight to it and a
 * reload keeps it.
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
  /** Where `main` stood when a step was pressed. `null` is every other change of the address. */
  const scrollBeforeSelect = useRef<number | null>(null)

  /*
   * `app-shell.tsx` scrolls `main` to the top on every change of the address, which is right for a new page and wrong for
   * a press in this page's own stepper. Effects run child first, so the shell's reset comes after this one: the frame
   * puts the position back before anything is painted.
   */
  useEffect(() => {
    const top = scrollBeforeSelect.current
    if (top === null) return undefined
    scrollBeforeSelect.current = null

    const frame = requestAnimationFrame(() => {
      panelRef.current?.closest('main')?.scrollTo({ top })
      if (!window.matchMedia(SIDE_BY_SIDE).matches) {
        panelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
      }
    })
    return () => cancelAnimationFrame(frame)
  }, [stepInAddress])

  const backLink = (
    <button
      type="button"
      onClick={back.go}
      className="inline-flex items-center gap-1 rounded-md text-subhead font-medium text-[var(--accent-text)] hover:underline"
    >
      <ArrowLeft aria-hidden className="size-4" />
      {back.label}
    </button>
  )

  const shell = (children: ReactNode) => (
    <div className={`${PROCESS_PAGE_CLASS} space-y-4`}>
      {backLink}
      {children}
    </div>
  )

  if (!canView) return shell(<ProcessNoAccess />)
  if (query.isPending) return shell(<CaseSkeleton />)
  if (query.isError) {
    return shell(<ProcessLoadError error={query.error} onRetry={() => void query.refetch()} />)
  }

  const detail = query.data
  const selectedCode =
    stepInAddress !== undefined && detail.steps.some((step) => step.stepCode === stepInAddress)
      ? stepInAddress
      : defaultStepCode(detail.steps)
  const selected = detail.steps.find((step) => step.stepCode === selectedCode)

  const selectStep = (stepCode: string) => {
    if (stepCode !== stepInAddress) {
      scrollBeforeSelect.current = panelRef.current?.closest('main')?.scrollTop ?? 0
    }
    void routeNavigate({ search: { step: stepCode }, replace: true })
  }

  return shell(
    <div className="space-y-5">
      <CaseHeader detail={detail} />

      {detail.steps.length === 0 ? (
        <Card>
          <EmptyState icon={ListChecks} title={t('processes.case.noSteps')} />
        </Card>
      ) : (
        <CaseColumns
          panelRef={panelRef}
          stepper={
            <Card className="px-[18px] py-5">
              <ProcessStepTimeline
                columns={detail.columns}
                steps={detail.steps}
                selectedStepCode={selectedCode}
                onSelect={selectStep}
              />
            </Card>
          }
          panel={selected !== undefined && <ProcessStepPanel detail={detail} step={selected} />}
          guide={
            <>
              {selected !== undefined && <ProcessStepGuide step={selected} />}
              <CaseBlanks blanks={detail.blanks} />
            </>
          }
        />
      )}
    </div>,
  )
}

/** «Payvandchi · Metan zavodi · ishga chiqish: 9-noyabr». A missing subject or field drops out with its separator. */
function useCaseLine(detail: ProcessCaseDetail): string {
  const { t, locale } = useLocale()

  const when =
    detail.anchorDate !== null
      ? `${t(`processes.case.anchorBy.${detail.processCode}`, {
          defaultValue: t('processes.case.anchorOther'),
        })}: ${longDueDate(detail.anchorDate, locale)}`
      : t('processes.case.startedOn', { date: longDueDate(detail.startedOn, locale) })

  return [detail.subject?.position, detail.departmentName, when].filter(Boolean).join(' · ')
}

/**
 * The case at a glance: process, person, where they are, and how far along. Closing facts appear only once they are true,
 * and the status only once it is not the normal one.
 */
function CaseHeader({ detail }: { detail: ProcessCaseDetail }) {
  const { t, num, date } = useLocale()
  const { done, total } = stepProgress(detail.steps)
  const line = useCaseLine(detail)
  const name = detail.subject?.name ?? null
  const progressText = t('processes.case.progress', { done: num(done), total: num(total) })

  return (
    <Card className="p-5">
      <div className="flex flex-col gap-4 md:flex-row md:flex-wrap md:items-end md:gap-x-7">
        <div className="min-w-0">
          {name !== null && (
            <p className="text-subhead text-[var(--fg-secondary)]">{detail.processTitle}</p>
          )}
          <h1 className="mt-1 text-title1 leading-tight font-bold break-words">
            {name ?? detail.processTitle}
          </h1>
          {line !== '' && (
            <p className="mt-1 text-subhead break-words text-[var(--fg-secondary)]">{line}</p>
          )}
        </div>

        <div className="w-full md:w-auto md:max-w-[332px] md:min-w-[220px] md:flex-1 md:pb-1">
          <p className="tabular text-subhead text-[var(--fg-secondary)]">{progressText}</p>
          <progress
            value={done}
            max={Math.max(total, 1)}
            aria-label={progressText}
            className="mt-2 block h-2 w-full appearance-none overflow-hidden rounded-full border-0 bg-[var(--bg-sunken)] [&::-moz-progress-bar]:rounded-full [&::-moz-progress-bar]:bg-[var(--accent)] [&::-webkit-progress-bar]:bg-[var(--bg-sunken)] [&::-webkit-progress-value]:rounded-full [&::-webkit-progress-value]:bg-[var(--accent)]"
          />
        </div>

        <div className="flex flex-wrap items-center gap-1.5 md:ml-auto md:self-start">
          {detail.legacy && <ProcessLegacyBadge />}
          {detail.status !== 'Active' && <ProcessStatusBadge status={detail.status} />}
        </div>
      </div>

      {(detail.completedAt !== null || detail.cancelledAt !== null || detail.parentCaseId !== null) && (
        <p className="tabular mt-4 flex flex-wrap items-center gap-x-4 gap-y-1 text-footnote text-[var(--fg-secondary)]">
          {detail.completedAt !== null && (
            <span>{t('processes.case.completedAt', { date: date(detail.completedAt) })}</span>
          )}
          {detail.cancelledAt !== null && (
            <span>{t('processes.case.cancelledAt', { date: date(detail.cancelledAt) })}</span>
          )}
          {detail.parentCaseId !== null && (
            <Link
              to="/mening-ishlarim/$caseId"
              params={{ caseId: detail.parentCaseId }}
              className="text-[var(--accent-text)] hover:underline"
            >
              {t('processes.case.parentCase')}
            </Link>
          )}
        </p>
      )}

      {detail.cancelReason !== null && (
        <Alert tone="info" title={t('processes.case.cancelReason')} className="mt-4">
          {detail.cancelReason}
        </Alert>
      )}
    </Card>
  )
}

/** The blanks linked to this case (SPEC §10 #5), each opening on its own page. A case without any draws nothing here. */
function CaseBlanks({ blanks }: { blanks: readonly ProcessLinkedBlank[] }) {
  const { t, num, date } = useLocale()

  if (blanks.length === 0) return null

  return (
    <Card className="p-5">
      <h2 className="text-body font-semibold">{t('processes.case.blanks')}</h2>
      <ul className="mt-3 space-y-1">
        {blanks.map((blank) => (
          <li key={blank.id}>
            <Link
              to="/blankalar/$submissionId"
              params={{ submissionId: blank.id }}
              className="-mx-2 block space-y-1 rounded-md px-2 py-2 transition-colors hover:bg-[var(--bg-hover)]"
            >
              <span className="block text-subhead break-words">
                {blank.templateTitle ?? blank.templateCode}
                {blank.number !== null && ` · № ${num(blank.number)}`}
              </span>
              <span className="flex flex-wrap items-center gap-1.5">
                <span className="font-mono text-caption2 text-[var(--fg-secondary)]">
                  {blank.templateCode}
                </span>
                <BlankStatusBadge status={blank.status} />
                {blank.completedAt === null && blank.dueDate !== null && (
                  <span className="tabular text-footnote text-[var(--fg-secondary)]">
                    {date(blank.dueDate)}
                  </span>
                )}
              </span>
            </Link>
          </li>
        ))}
      </ul>
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
