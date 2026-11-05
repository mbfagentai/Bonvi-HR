import { LoaderCircle } from 'lucide-react'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { useStages } from '@/features/recruiting/api/recruiting-queries'
import { RECRUITING_VIEW } from '@/features/recruiting/lib/recruiting-presentation'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, CardContent, CardHeader, Select, type SelectOption, Skeleton } from '@/shared/ui'

import {
  PROCESS_BINDING_CODES,
  PROCESS_SETTINGS_API_READY,
  type ProcessBindingCode,
  type ProcessStageBindingRow,
} from '../api/process-admin-api'
import { useProcessStageBindings, useSaveProcessStageBindings } from '../api/process-admin-queries'
import { ProcessSectionError } from './process-section-error'

/** The four selects, whatever feeds them: the live form, or their disabled shape while the API is not merged. */
function BindingSelects({
  values,
  options,
  disabled,
  onChange,
}: {
  values: Readonly<Record<string, string>>
  options: readonly SelectOption[]
  disabled: boolean
  onChange: (code: ProcessBindingCode, stageId: string) => void
}) {
  const { t } = useLocale()

  return (
    <div className="grid gap-4 md:grid-cols-2">
      {PROCESS_BINDING_CODES.map((code) => (
        <Select
          key={code}
          label={t(`processes.settings.bindings.${code}`)}
          placeholder={t('processes.settings.bindings.none')}
          options={options}
          value={values[code] ?? ''}
          disabled={disabled}
          onChange={(event) => onChange(code, event.target.value)}
        />
      ))}
    </div>
  )
}

/** Read from the rows alone: the server already names the stage, so this viewer needs no recruiting access. */
function BindingsReadOnly({ rows }: { rows: readonly ProcessStageBindingRow[] }) {
  const { t } = useLocale()

  return (
    <dl className="divide-y divide-[var(--border)]">
      {PROCESS_BINDING_CODES.map((code) => (
        <div key={code} className="flex items-center justify-between gap-4 py-2.5 text-subhead">
          <dt className="text-[var(--fg-secondary)]">{t(`processes.settings.bindings.${code}`)}</dt>
          <dd className="text-right">
            {rows.find((row) => row.bindingCode === code)?.pipelineStageName ??
              t('processes.settings.bindings.none')}
          </dd>
        </div>
      ))}
    </dl>
  )
}

function BindingsForm({ rows }: { rows: readonly ProcessStageBindingRow[] }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const stages = useStages(false)
  const save = useSaveProcessStageBindings()
  const [draft, setDraft] = useState<Readonly<Record<string, string>>>({})

  const saved = (code: string) => rows.find((row) => row.bindingCode === code)?.pipelineStageId ?? ''
  const values = Object.fromEntries(
    PROCESS_BINDING_CODES.map((code) => [code, draft[code] ?? saved(code)]),
  )
  const changed = PROCESS_BINDING_CODES.filter((code) => values[code] !== saved(code))

  // A stage retired after it was bound stays selectable, so opening the form does not silently unbind it.
  const options = (stages.data ?? [])
    .filter((stage) => stage.isActive || Object.values(values).includes(stage.id))
    .map((stage) => ({ value: stage.id, label: stage.name }))

  const submit = () => {
    save.mutate(
      changed.map((code) => ({ bindingCode: code, pipelineStageId: values[code] || null })),
      { onSuccess: () => setDraft({}) },
    )
  }

  if (stages.isPending) return <Skeleton className="h-28 w-full" />
  if (stages.isError) {
    return <ProcessSectionError error={stages.error} onRetry={() => void stages.refetch()} />
  }

  return (
    <div className="space-y-4">
      {save.isError && <Alert tone="danger">{errorMessage(problemCode(save.error))}</Alert>}
      <BindingSelects
        values={values}
        options={options}
        disabled={save.isPending}
        onChange={(code, stageId) => setDraft((current) => ({ ...current, [code]: stageId }))}
      />
      <div className="flex items-center justify-end gap-3">
        {changed.length > 0 && (
          <span className="text-footnote text-[var(--fg-secondary)]">{t('settings.system.unsaved')}</span>
        )}
        <Button variant="primary" onClick={submit} disabled={save.isPending || changed.length === 0}>
          {save.isPending && <LoaderCircle className="animate-spin" />}
          {t('common.save')}
        </Button>
      </div>
    </div>
  )
}

/** The stage list (`GET /api/recruiting/stages`) needs `recruiting.view`; without it the bindings can only be read. */
function LiveBindings() {
  const { hasPermission } = useAuth()
  const bindings = useProcessStageBindings()

  if (bindings.isPending) return <Skeleton className="h-28 w-full" />
  if (bindings.isError) {
    return <ProcessSectionError error={bindings.error} onRetry={() => void bindings.refetch()} />
  }

  return hasPermission(RECRUITING_VIEW) ? (
    <BindingsForm rows={bindings.data} />
  ) : (
    <BindingsReadOnly rows={bindings.data} />
  )
}

/** Which pipeline stage means «contacted», «invited», «council», «practical test» (SPEC §6.5, §10 #16). */
export function ProcessStageBindingsCard() {
  const { t } = useLocale()

  return (
    <Card>
      <CardHeader
        title={t('processes.settings.bindings.title')}
        subtitle={t('processes.settings.bindings.hint')}
      />
      <CardContent className="space-y-4">
        {PROCESS_SETTINGS_API_READY ? (
          <LiveBindings />
        ) : (
          <>
            <Alert tone="info">{t('processes.settings.notYet')}</Alert>
            <BindingSelects values={{}} options={[]} disabled onChange={() => undefined} />
          </>
        )}
      </CardContent>
    </Card>
  )
}
