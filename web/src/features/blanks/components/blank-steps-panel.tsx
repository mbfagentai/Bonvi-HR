import { CheckCircle2, PenLine, Undo2, XCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
import { Badge, Button, Card } from '@/shared/ui'

import type { BlankStep, BlankSubmission } from '../api/blanks-api'
import { useBlankStepSignature } from '../api/blanks-queries'
import { BLANKS_APPROVE } from '../lib/blank-permissions'
import { fieldsById } from '../lib/blank-presentation'
import type { BlankDefinition, BlankStepState, JsonValue, StepDefinition } from '../lib/definition-types'

export type StepAction = 'approve' | 'sign' | 'return' | 'reject'

const STEP_TONE: Record<BlankStepState, 'neutral' | 'warning' | 'success' | 'danger'> = {
  Waiting: 'neutral',
  Open: 'warning',
  Signed: 'success',
  Skipped: 'neutral',
  Rejected: 'danger',
}

const OFFICE_ROLES = new Set([
  'hr1', 'hr2', 'hr3', 'hr4', 'hr5', 'hr8', 'payrollAccountant', 'chiefAccountant', 'director', 'lawyer',
])

/** Key under which the server stores the decision extra in `answersPatch` (SPEC §3.4). */
const DECISION_EXTRA_KEY = '$decisionExtra'

export function stepDefinition(definition: BlankDefinition, stepCode: string): StepDefinition | undefined {
  return definition.steps?.find((step) => step.id === stepCode)
}

/** «HR-4», «Xodimning o'zi», «Murabbiy» (label of the `field:` ref) … */
export function useActorLabel(definition: BlankDefinition) {
  const { t } = useLocale()
  const fields = useMemo(() => fieldsById(definition), [definition])
  return (role: string): string => {
    if (OFFICE_ROLES.has(role)) return t(`blanks.roles.codes.${role}`)
    if (role.startsWith('field:')) {
      const field = fields.get(role.slice('field:'.length))
      return field?.label ?? role
    }
    return t(`blanks.steps.actor.${role}`, { defaultValue: role })
  }
}

function extraText(value: JsonValue | undefined): string | null {
  if (typeof value === 'string' && value.trim() !== '') return value
  if (typeof value === 'number') return String(value)
  if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
    const label = value['label'] ?? value['number']
    return typeof label === 'string' ? label : null
  }
  return null
}

/**
 * Approval route of one blank (SPEC §5): groups by `orderNo` (equal numbers = parallel),
 * a state chip per step, who acted / signed, when and how, the decision and comment, and
 * the in-person signature (#16). Buttons appear only for `can.actionableSteps` — the panel
 * never re-derives §6.2; the static endpoint permission of «reject» is the one extra check.
 */
export function BlankStepsPanel({
  submission,
  definition,
  disabled,
  onAction,
}: {
  submission: BlankSubmission
  definition: BlankDefinition
  disabled: boolean
  onAction: (action: StepAction, step: BlankStep) => void
}) {
  const { t, num } = useLocale()

  const groups = useMemo(() => {
    const byOrder = new Map<number, BlankStep[]>()
    for (const step of submission.steps) byOrder.set(step.orderNo, [...(byOrder.get(step.orderNo) ?? []), step])
    return [...byOrder.entries()].sort(([a], [b]) => a - b).map(([, steps]) => steps)
  }, [submission.steps])

  if (groups.length === 0) return null

  return (
    <Card className="p-5">
      <h2 className="mb-3 text-headline font-semibold">{t('blanks.fill.steps')}</h2>
      <ol className="space-y-4">
        {groups.map((steps, index) => (
          <li key={steps[0]?.orderNo ?? index}>
            <p className="mb-1 text-footnote font-medium uppercase tracking-wide text-[var(--fg-secondary)]">
              {t('blanks.steps.group', { n: num(index + 1) })}
              {steps.length > 1 && ` · ${t('blanks.steps.parallel')}`}
            </p>
            <ul className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border)]">
              {steps.map((step) => (
                <StepRow
                  key={step.id}
                  submission={submission}
                  definition={definition}
                  step={step}
                  disabled={disabled}
                  onAction={onAction}
                />
              ))}
            </ul>
          </li>
        ))}
      </ol>
    </Card>
  )
}

function StepRow({
  submission,
  definition,
  step,
  disabled,
  onAction,
}: {
  submission: BlankSubmission
  definition: BlankDefinition
  step: BlankStep
  disabled: boolean
  onAction: (action: StepAction, step: BlankStep) => void
}) {
  const { t, date, dateTime } = useLocale()
  const { hasPermission } = useAuth()
  const actorLabel = useActorLabel(definition)
  const stepDef = stepDefinition(definition, step.stepCode)

  const open = step.state === 'Open' && submission.status === 'InApproval'
  const actionable = open ? submission.can.actionableSteps.find((item) => item.stepCode === step.stepCode) : undefined
  const canReturn = actionable?.electronic === true && stepDef?.canReturn !== false
  const canReject = actionable?.electronic === true && stepDef?.canReject === true && hasPermission(BLANKS_APPROVE)

  const decisionLabel =
    step.decision === null
      ? null
      : (stepDef?.decision?.options.find((option) => option.value === step.decision)?.label ?? step.decision)
  const patch = step.answersPatch
  const decisionExtra =
    patch !== null && typeof patch === 'object' && !Array.isArray(patch)
      ? extraText((patch as Record<string, JsonValue>)[DECISION_EXTRA_KEY])
      : null
  const signer = step.signerName ?? step.actedByName
  const today = todayIso()
  const late = open && step.dueDate !== null && step.dueDate < today

  return (
    <li className="space-y-2 px-4 py-3">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <span className="min-w-0 flex-1 truncate font-medium" title={step.title}>
          {step.title}
        </span>
        <span className="text-footnote text-[var(--fg-secondary)]">{actorLabel(step.actorRole)}</span>
        {step.dueDate !== null && step.state !== 'Signed' && step.state !== 'Skipped' && (
          <span className={late ? 'tabular text-footnote font-medium text-ios-red' : 'tabular text-footnote text-[var(--fg-secondary)]'}>
            {t('blanks.steps.due', { date: date(step.dueDate) })}
          </span>
        )}
        <Badge tone={STEP_TONE[step.state]}>{t(`blanks.fill.stepState.${step.state}`)}</Badge>
      </div>

      {step.actedAt !== null && (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-footnote text-[var(--fg-secondary)]">
          {signer !== null && <span className="font-medium text-[var(--fg)]">{signer}</span>}
          <span className="tabular">{dateTime(step.actedAt)}</span>
          {step.method !== null && <span>{t(step.method === 'InPerson' ? 'blanks.steps.methodInPerson' : 'blanks.steps.methodElectronic')}</span>}
          {step.method === 'InPerson' && step.actedByName !== null && step.actedByName !== signer && (
            <span>{t('blanks.steps.operator', { name: step.actedByName })}</span>
          )}
        </div>
      )}

      {(decisionLabel !== null || step.comment !== null) && (
        <div className="space-y-0.5 text-subhead">
          {decisionLabel !== null && (
            <p>
              <span className="text-[var(--fg-secondary)]">{t('blanks.steps.decision')}: </span>
              <span className="font-medium">{decisionLabel}</span>
              {decisionExtra !== null && <span> — {decisionExtra}</span>}
            </p>
          )}
          {step.comment !== null && (
            <p className="whitespace-pre-line">
              <span className="text-[var(--fg-secondary)]">{t('blanks.steps.comment')}: </span>
              {step.comment}
            </p>
          )}
        </div>
      )}

      {step.hasSignature && <SignatureThumb submissionId={submission.id} stepCode={step.stepCode} />}

      {actionable !== undefined && (
        <div className="flex flex-wrap gap-2 pt-1">
          {actionable.electronic && (
            <Button size="sm" variant="primary" disabled={disabled} onClick={() => onAction('approve', step)}>
              <CheckCircle2 />
              {t('blanks.steps.approve')}
            </Button>
          )}
          {actionable.inPerson && (
            <Button size="sm" variant={actionable.electronic ? undefined : 'primary'} disabled={disabled} onClick={() => onAction('sign', step)}>
              <PenLine />
              {t('blanks.steps.signInPerson')}
            </Button>
          )}
          {canReturn && (
            <Button size="sm" disabled={disabled} onClick={() => onAction('return', step)}>
              <Undo2 />
              {t('blanks.steps.return')}
            </Button>
          )}
          {canReject && (
            <Button size="sm" variant="danger" disabled={disabled} onClick={() => onAction('reject', step)}>
              <XCircle />
              {t('blanks.steps.reject')}
            </Button>
          )}
        </div>
      )}
    </li>
  )
}

/** The PNG of #16 on a white tile (it is drawn for a white PDF page in any theme). */
function SignatureThumb({ submissionId, stepCode }: { submissionId: string; stepCode: string }) {
  const { t } = useLocale()
  const signature = useBlankStepSignature(submissionId, stepCode, true)
  const [url, setUrl] = useState<string | null>(null)

  useEffect(() => {
    if (!signature.data) return
    const objectUrl = URL.createObjectURL(signature.data)
    setUrl(objectUrl)
    return () => URL.revokeObjectURL(objectUrl)
  }, [signature.data])

  if (signature.isError) return <p className="text-footnote text-[var(--fg-tertiary)]">{t('blanks.steps.signatureFailed')}</p>
  if (url === null) return <div className="h-14 w-36 animate-pulse rounded-lg bg-[var(--bg-sunken)]" aria-hidden="true" />
  return (
    <img
      src={url}
      alt={t('blanks.steps.signature')}
      className="h-14 w-auto max-w-[220px] rounded-lg border border-[var(--border)] bg-white object-contain p-1"
    />
  )
}
