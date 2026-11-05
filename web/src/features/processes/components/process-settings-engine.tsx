import { useQueryClient } from '@tanstack/react-query'
import { LoaderCircle, RefreshCw } from 'lucide-react'
import { useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { SETTINGS_MANAGE } from '@/features/settings/api/settings-api'
import { useSaveSettings } from '@/features/settings/api/settings-queries'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Badge, Button, Card, CardContent, CardHeader, Select, Skeleton } from '@/shared/ui'

import { PROCESS_SETTINGS_API_READY } from '../api/process-admin-api'
import { useRunProcessReconcile } from '../api/process-admin-queries'
import type { ProcessEngineStatus } from '../api/processes-api'
import { processKeys, useProcessEngineStatus } from '../api/processes-queries'
import {
  COUNCIL_WEEKDAY_KEY,
  COUNCIL_WEEKDAYS,
  GATES_MODE_KEY,
  PROCESS_GATES_MODES,
} from '../lib/process-settings'
import { ProcessLoadError } from './process-states'

function StatusRow({ label, children }: { label: ReactNode; children: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 py-2.5 text-subhead">
      <dt className="text-[var(--fg-secondary)]">{label}</dt>
      <dd className="text-right">{children}</dd>
    </div>
  )
}

/** What the engine is doing right now (`GET /status`) and the diagnostic pass (§10 #18). */
function EngineStatusCard({ status }: { status: ProcessEngineStatus }) {
  const { t, date, dateTime, num } = useLocale()
  const errorMessage = useErrorMessage()
  const reconcile = useRunProcessReconcile()
  const result = reconcile.data

  return (
    <Card>
      <CardHeader
        title={t('processes.settings.status.title')}
        action={
          <Button
            size="sm"
            disabled={!PROCESS_SETTINGS_API_READY || reconcile.isPending}
            onClick={() => reconcile.mutate()}
          >
            {reconcile.isPending ? <LoaderCircle className="animate-spin" /> : <RefreshCw />}
            {t('processes.settings.status.reconcile')}
          </Button>
        }
      />
      <CardContent className="space-y-3">
        {status.engine === 'dormant' && (
          <Alert tone="info">{t('processes.settings.status.dormantText')}</Alert>
        )}
        {reconcile.isError && (
          <Alert tone="danger">{errorMessage(problemCode(reconcile.error))}</Alert>
        )}
        {result !== undefined && (
          <Alert tone={result.status === 'Finished' ? 'success' : 'info'}>
            {t(`processes.settings.status.result${result.status}`, {
              started: num(result.started),
              changed: num(result.changedRows),
            })}
          </Alert>
        )}
        <dl className="divide-y divide-[var(--border)]">
          <StatusRow label={t('processes.settings.status.engine')}>
            <Badge tone={status.engine === 'active' ? 'success' : 'neutral'}>
              {t(`processes.settings.status.${status.engine}`)}
            </Badge>
          </StatusRow>
          <StatusRow label={t('processes.settings.status.startedOn')}>
            {status.startedOn === null ? t('processes.settings.status.never') : date(status.startedOn)}
          </StatusRow>
          <StatusRow label={t('processes.settings.status.lastPass')}>
            {status.lastPassAt === null ? t('processes.settings.status.never') : dateTime(status.lastPassAt)}
          </StatusRow>
          <StatusRow label={t('processes.settings.gatesMode.label')}>
            {t(`processes.settings.gatesMode.${status.gatesMode}`)}
          </StatusRow>
          <StatusRow label={t('processes.settings.council.label')}>
            {t(`processes.settings.council.day${status.councilWeekday}`, {
              defaultValue: num(status.councilWeekday),
            })}
          </StatusRow>
        </dl>
      </CardContent>
    </Card>
  )
}

/**
 * Council day and the gates' kill switch (SPEC §6.6). They are ordinary system settings, so they are written through
 * the existing `PUT /api/settings`, which needs `settings.manage`: the caller draws this card only for that holder.
 */
function EngineRulesCard({ status }: { status: ProcessEngineStatus }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveSettings()
  const queryClient = useQueryClient()

  const [gatesDraft, setGatesDraft] = useState<string | null>(null)
  const [councilDraft, setCouncilDraft] = useState<string | null>(null)

  const gatesMode = gatesDraft ?? status.gatesMode
  const council = councilDraft ?? String(status.councilWeekday)

  const changes = [
    ...(gatesMode === status.gatesMode ? [] : [{ key: GATES_MODE_KEY, value: gatesMode }]),
    ...(council === String(status.councilWeekday) ? [] : [{ key: COUNCIL_WEEKDAY_KEY, value: council }]),
  ]

  const submit = () => {
    save.mutate(changes, {
      onSuccess: async () => {
        await queryClient.invalidateQueries({ queryKey: processKeys.all })
        setGatesDraft(null)
        setCouncilDraft(null)
      },
    })
  }

  return (
    <Card>
      <CardHeader title={t('processes.settings.rulesTitle')} />
      <CardContent className="space-y-4">
        {save.isError && <Alert tone="danger">{errorMessage(problemCode(save.error))}</Alert>}

        <div className="grid gap-4 md:grid-cols-2">
          <Select
            label={t('processes.settings.council.label')}
            hint={t('processes.settings.council.hint')}
            options={COUNCIL_WEEKDAYS.map((day) => ({
              value: String(day),
              label: t(`processes.settings.council.day${day}`),
            }))}
            value={council}
            disabled={save.isPending}
            onChange={(event) => setCouncilDraft(event.target.value)}
          />
          <Select
            label={t('processes.settings.gatesMode.label')}
            hint={t('processes.settings.gatesMode.hint')}
            options={PROCESS_GATES_MODES.map((mode) => ({
              value: mode,
              label: t(`processes.settings.gatesMode.${mode}`),
            }))}
            value={gatesMode}
            disabled={save.isPending}
            onChange={(event) => setGatesDraft(event.target.value)}
          />
        </div>

        {gatesMode === 'warn' && <Alert tone="warning">{t('processes.settings.gatesMode.warnText')}</Alert>}
        {gatesMode === 'off' && <Alert tone="danger">{t('processes.settings.gatesMode.offText')}</Alert>}

        <div className="flex items-center justify-end gap-3">
          {changes.length > 0 && (
            <span className="text-footnote text-[var(--fg-secondary)]">{t('settings.system.unsaved')}</span>
          )}
          <Button variant="primary" onClick={submit} disabled={save.isPending || changes.length === 0}>
            {save.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}

/** The engine's state and, for whoever may change them, its two switches (SPEC §11.7). */
export function ProcessEngineSection() {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const status = useProcessEngineStatus()

  if (status.isPending) return <Skeleton className="h-64 w-full rounded-lg" />
  if (status.isError) {
    return <ProcessLoadError error={status.error} onRetry={() => void status.refetch()} />
  }

  return (
    <>
      <EngineStatusCard status={status.data} />
      {hasPermission(SETTINGS_MANAGE) ? (
        <EngineRulesCard status={status.data} />
      ) : (
        <Alert tone="info">{t('processes.settings.rulesNoAccess')}</Alert>
      )}
    </>
  )
}
