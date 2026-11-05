import { Link } from '@tanstack/react-router'
import { ArrowRight } from 'lucide-react'

import { useAuth } from '@/features/auth'
import type { ProcessGateBlock } from '@/features/employees/lib/employee-error'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert } from '@/shared/ui'

import { useProcessCases } from '../api/processes-queries'
import { PROCESSES_VIEW } from '../lib/permissions'
import type { ProcessCaseFilters } from '../api/processes-api'

const LINK = 'mt-2 inline-flex items-center gap-1 text-footnote font-medium text-[var(--accent-text)] hover:underline'

/**
 * A process gate refused a hire or a termination (SPEC §8). `?confirm=true` cannot pass it, so there is no «do it anyway»
 * here: the reasons, and the way to the case where they get closed. `cases` says whose case; without it (a direct hire has
 * no subject yet), or without `processes.view`, only the reasons show.
 */
export function ProcessGateBlockAlert({
  block,
  cases,
}: {
  block: ProcessGateBlock
  cases?: Pick<ProcessCaseFilters, 'employee' | 'candidate' | 'process'>
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const canOpen = cases !== undefined && hasPermission(PROCESSES_VIEW)
  const found = useProcessCases({ ...cases, status: 'Active', page: 1, pageSize: 1 }, canOpen)
  const caseId = found.data?.items[0]?.id

  return (
    <Alert tone="danger" title={errorMessage(block.code)}>
      {block.extra.length > 0 && (
        <ul className="space-y-1">
          {block.extra.map((code) => (
            <li key={code}>{errorMessage(code)}</li>
          ))}
        </ul>
      )}

      {canOpen && caseId !== undefined && (
        <Link to="/mening-ishlarim/$caseId" params={{ caseId }} className={LINK}>
          {t('processes.case.gate.openCase')}
          <ArrowRight className="size-3.5" />
        </Link>
      )}

      {canOpen && found.isSuccess && caseId === undefined && (
        <Link to="/mening-ishlarim" className={LINK}>
          {t('processes.case.gate.openWork')}
          <ArrowRight className="size-3.5" />
        </Link>
      )}
    </Alert>
  )
}
