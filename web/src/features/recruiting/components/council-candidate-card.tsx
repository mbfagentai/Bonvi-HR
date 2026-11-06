import { Link } from '@tanstack/react-router'
import { Check, Printer } from 'lucide-react'
import { useState, type FormEvent } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Badge, Button, Card, CardContent, DateField, MoneyInput, Select, TextArea, TextField } from '@/shared/ui'
import {
  COUNCIL_DECISIONS,
  COUNCIL_PRACTICALS,
  type CouncilCandidate,
  type CouncilDecision,
  type CouncilPractical,
} from '../api/council-api'
import { useSaveCouncilDecision } from '../api/council-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { CouncilOpinions } from './council-opinions'

const DECISION_TONE = { Accept: 'success', Conditional: 'warning', Reserve: 'neutral', Reject: 'danger' } as const

/** One candidate's row on the minutes. The draft lives here, so a refetch never overwrites what HR is typing. */
export function CouncilCandidateCard({
  date,
  row,
  canManage,
  canSeeSalary,
  onPrint,
}: {
  date: string
  row: CouncilCandidate
  canManage: boolean
  canSeeSalary: boolean
  onPrint: (row: CouncilCandidate) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveCouncilDecision(date, row.candidateId)

  const [decision, setDecision] = useState<CouncilDecision | ''>(row.decision ?? '')
  const [practical, setPractical] = useState<CouncilPractical | ''>(row.practical ?? '')
  const [salary, setSalary] = useState(row.offeredSalary === null ? '' : String(Math.trunc(row.offeredSalary)))
  const [startDate, setStartDate] = useState(row.startDate ?? '')
  const [condition, setCondition] = useState(row.condition ?? '')
  const [conclusion, setConclusion] = useState(row.conclusion ?? '')
  const [decisionMissing, setDecisionMissing] = useState(false)
  const [dirty, setDirty] = useState(false)

  const edit = <T,>(setter: (value: T) => void) => (value: T) => {
    setter(value)
    setDirty(true)
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    if (decision === '') {
      setDecisionMissing(true)
      return
    }

    setDecisionMissing(false)
    save.mutate(
      {
        decision,
        practical: practical === '' ? null : practical,
        offeredSalary: salary === '' ? null : Number(salary),
        startDate: startDate === '' ? null : startDate,
        condition: condition.trim() === '' ? null : condition,
        conclusion: conclusion.trim() === '' ? null : conclusion,
      },
      { onSuccess: () => setDirty(false) },
    )
  }

  return (
    <Card>
      <CardContent className="p-4 sm:p-5">
        <form onSubmit={submit} className="space-y-4">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div className="min-w-0">
              <Link
                to="/nomzodlar/$candidateId"
                params={{ candidateId: row.candidateId }}
                className="text-base font-semibold text-[var(--fg)] hover:underline"
              >
                {row.fullName}
              </Link>
              <p className="mt-0.5 text-sm text-[var(--fg-secondary)]">
                {row.position}
                {row.formNumber !== null && ` · ${t('council.formNumber', { number: row.formNumber })}`}
              </p>
            </div>
            <div className="flex flex-wrap gap-1.5">
              {!row.isActive && <Badge tone="neutral">{t('council.leftFunnel')}</Badge>}
              {row.decision !== null ? (
                <Badge tone={DECISION_TONE[row.decision]}>{t(`council.decision.${row.decision}`)}</Badge>
              ) : (
                <Badge tone="warning">{t('council.noDecision')}</Badge>
              )}
            </div>
          </div>

          <CouncilOpinions row={row} />

          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <Select
              label={t('council.decisionLabel')}
              value={decision}
              disabled={!canManage}
              placeholder={t('council.choose')}
              error={decisionMissing ? t('council.decisionRequired') : undefined}
              options={COUNCIL_DECISIONS.map((value) => ({ value, label: t(`council.decision.${value}`) }))}
              onChange={(event) => edit(setDecision)(event.target.value as CouncilDecision | '')}
            />
            <Select
              label={t('council.practicalLabel')}
              value={practical}
              disabled={!canManage}
              placeholder={t('council.choose')}
              options={COUNCIL_PRACTICALS.map((value) => ({ value, label: t(`council.practical.${value}`) }))}
              onChange={(event) => edit(setPractical)(event.target.value as CouncilPractical | '')}
            />
            {canSeeSalary && (
              <MoneyInput label={t('council.salary')} value={salary} disabled={!canManage} onChange={edit(setSalary)} />
            )}
            <DateField label={t('council.startDate')} value={startDate} disabled={!canManage} onChange={edit(setStartDate)} />
          </div>

          <TextField
            label={t('council.condition')}
            hint={t('council.conditionHint')}
            value={condition}
            maxLength={1000}
            disabled={!canManage}
            onChange={(event) => edit(setCondition)(event.target.value)}
          />
          <TextArea
            label={t('council.finalConclusion')}
            rows={2}
            value={conclusion}
            maxLength={4000}
            disabled={!canManage}
            onChange={(event) => edit(setConclusion)(event.target.value)}
          />

          {save.isError && <Alert tone="danger">{errorMessage(recruitingErrorCode(save.error))}</Alert>}

          {canManage && (
            <div className="flex items-center justify-end gap-3">
              {save.isSuccess && !dirty && (
                <span className="inline-flex items-center gap-1 text-sm text-ios-green">
                  <Check className="size-4" />
                  {t('council.saved')}
                </span>
              )}
              {row.decided && !dirty && (
                <Button type="button" variant="secondary" size="sm" onClick={() => onPrint(row)}>
                  <Printer />
                  {t('council.printOne')}
                </Button>
              )}
              <Button type="submit" size="sm" disabled={save.isPending}>
                {t('council.save')}
              </Button>
            </div>
          )}
        </form>
      </CardContent>
    </Card>
  )
}
