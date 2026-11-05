import { LoaderCircle } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'

import { useUsers } from '@/features/users/api/users-queries'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, Skeleton, TextArea } from '@/shared/ui'

import { useGrantCaseGateOverride, useGrantDirectHireOverride } from '../api/process-admin-queries'
import type { ProcessTask } from '../api/processes-api'
import { isOverrideReasonValid, OVERRIDE_REASON_MAX, OVERRIDE_REASON_MIN } from '../lib/process-override'
import { processSubjectLine } from './process-chips'

/** The frame both exceptions share: the terms, the reason, and the server's refusal (§8.4's hr1 rule lives there). */
function OverrideDialog({
  title,
  children,
  reason,
  onReasonChange,
  pending,
  error,
  canSubmit,
  onSubmit,
  onClose,
}: {
  title: ReactNode
  children: ReactNode
  reason: string
  onReasonChange: (next: string) => void
  pending: boolean
  error: unknown
  canSubmit: boolean
  onSubmit: () => void
  onClose: () => void
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!pending}
      title={title}
      description={t('processes.late.override.terms')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="primary"
            onClick={onSubmit}
            disabled={pending || !canSubmit || !isOverrideReasonValid(reason)}
          >
            {pending && <LoaderCircle className="animate-spin" />}
            {t('processes.late.override.submit')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {error !== null && <Alert tone="danger">{errorMessage(problemCode(error))}</Alert>}
        {children}
        <TextArea
          label={t('processes.late.override.reason')}
          hint={t('processes.late.override.reasonHint', { n: num(OVERRIDE_REASON_MIN) })}
          value={reason}
          maxLength={OVERRIDE_REASON_MAX}
          disabled={pending}
          onChange={(event) => onReasonChange(event.target.value)}
        />
      </div>
    </Dialog>
  )
}

/** #14: an exception to the gate holding this step. */
export function CaseOverrideDialog({ task, onClose }: { task: ProcessTask; onClose: () => void }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const grant = useGrantCaseGateOverride()
  const [reason, setReason] = useState('')
  const subject = processSubjectLine(task.subject, task.departmentName)

  return (
    <OverrideDialog
      title={t('processes.late.override.title')}
      reason={reason}
      onReasonChange={setReason}
      pending={grant.isPending}
      error={grant.error}
      canSubmit={task.gateCode !== null}
      onSubmit={() => {
        if (task.gateCode === null) return
        grant.mutate(
          { caseId: task.caseId, gateCode: task.gateCode, reason: reason.trim() },
          { onSuccess: onClose },
        )
      }}
      onClose={onClose}
    >
      <div className="text-subhead">
        {subject !== '' && <p className="font-semibold">{subject}</p>}
        <p className="text-[var(--fg-secondary)]">{task.stepTitle}</p>
        {task.gateErrorCode !== null && (
          <p className="mt-1 text-footnote text-[var(--fg-secondary)]">{errorMessage(task.gateErrorCode)}</p>
        )}
      </div>
    </OverrideDialog>
  )
}

/** #15: one named operator may make one hire without a candidate. The caller draws this only with `users.manage`. */
export function DirectHireOverrideDialog({ onClose }: { onClose: () => void }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const grant = useGrantDirectHireOverride()
  const users = useUsers('')
  const [reason, setReason] = useState('')
  const [forUserId, setForUserId] = useState('')

  const options = useMemo(
    () =>
      (users.data ?? [])
        .filter((user) => user.isActive)
        .map((user) => ({ value: user.id, label: user.fullName })),
    [users.data],
  )

  return (
    <OverrideDialog
      title={t('processes.late.directHire.title')}
      reason={reason}
      onReasonChange={setReason}
      pending={grant.isPending}
      error={grant.error}
      canSubmit={forUserId !== ''}
      onSubmit={() => grant.mutate({ forUserId, reason: reason.trim() }, { onSuccess: onClose })}
      onClose={onClose}
    >
      {users.isPending ? (
        <Skeleton className="h-12 w-full" />
      ) : users.isError ? (
        <Alert tone="danger">{errorMessage(problemCode(users.error))}</Alert>
      ) : (
        <Select
          label={t('processes.late.directHire.forUser')}
          hint={t('processes.late.directHire.forUserHint')}
          placeholder={t('processes.late.directHire.choose')}
          options={options}
          value={forUserId}
          disabled={grant.isPending}
          onChange={(event) => setForUserId(event.target.value)}
        />
      )}
    </OverrideDialog>
  )
}
