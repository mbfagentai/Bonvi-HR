import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'

import { api } from '@/api/client'
import { ApiError } from '@/api/errors'
import type { TeamVacancyRequestDto } from '@/api/types'
import { useMe } from '@/data/queries'
import { REQUEST_STATUS_LOOK } from '@/data/status-look'
import {
  departmentOptions,
  invalidateTeamVacancyRequests,
  useTeamScope,
  useTeamVacancyRequests,
} from '@/data/team'
import { useFormat } from '@/lib/format'
import { addDays, todayIso } from '@/lib/time'
import { useErrorText } from '@/lib/use-error-message'
import { PushedScreen } from '@/ui/app-shell'
import { BottomAction, Button } from '@/ui/button'
import { Card } from '@/ui/card'
import { EmptyState, ErrorState, InlineMessage, SkeletonLines } from '@/ui/feedback'
import { DateRow, Field, SelectRow, SwitchRow, TextArea, TextInput } from '@/ui/form'
import { ConfirmSheet } from '@/ui/sheet'
import { StatusChip } from '@/ui/status-chip'
import { useToast } from '@/ui/toast'

/**
 * VAKANSIYA SO'ROVI — boshliq HR'dan yangi odam so'raydi (egasi, 2026-10-31).
 *
 * So'rov HR panelidagi «Vakansiya so'rovlari» menyusiga «Yangi» holatida
 * tushadi; HR vakansiya ochadi yoki sababini yozib rad etadi. Boshliq
 * natijani shu kartada ko'radi. Maosh so'ralmaydi (egasi) — uni HR hal qiladi.
 */

/** Server chegaralari — `VacancyRequest.MaxTitleLength`, `Vacancy.MaxOpenings`. */
const MAX_TITLE = 200
const MAX_OPENINGS = 100

/** «Jamoa» ekranidagi karta — so'nggi so'rovlar va «Vakansiya so'rash». */
export function TeamVacancyCard() {
  const { t } = useTranslation()
  const format = useFormat()
  const toast = useToast()
  const errorText = useErrorText()
  const requests = useTeamVacancyRequests(true)

  const [cancelling, setCancelling] = useState<TeamVacancyRequestDto | null>(null)
  const [busy, setBusy] = useState(false)

  const cancel = async (): Promise<void> => {
    if (!cancelling) return
    setBusy(true)
    try {
      await api.cancelTeamVacancyRequest(cancelling.id)
      toast.show(t('teamVacancy.cancelled'), 'success')
    } catch (cause: unknown) {
      toast.show(errorText(ApiError.from(cause)).body, 'error')
    } finally {
      setBusy(false)
      setCancelling(null)
      invalidateTeamVacancyRequests()
      requests.refetch()
    }
  }

  const items = requests.data ?? []

  return (
    <Card title={t('teamVacancy.title')} action={{ label: t('teamVacancy.new'), to: '/jamoa/vakansiya/yangi' }}>
      {requests.isLoading && !requests.data ? (
        <SkeletonLines rows={2} />
      ) : requests.error && !requests.data ? (
        <ErrorState onRetry={requests.refetch} />
      ) : items.length === 0 ? (
        <EmptyState icon="users" title={t('teamVacancy.emptyTitle')} text={t('teamVacancy.emptyText')} />
      ) : (
        <div>
          {items.map((item) => (
            <button
              key={item.id}
              type="button"
              disabled={item.status !== 'Pending'}
              onClick={() => setCancelling(item)}
              className="block w-full border-t border-border py-3 text-left first:border-t-0 enabled:active:bg-bg-pressed"
            >
              <div className="flex items-start">
                <div className="min-w-0 flex-1">
                  <p className="text-body text-fg server-text">
                    {t('teamVacancy.rowTitle', { title: item.title, n: format.number(item.openings) })}
                  </p>
                  <p className="mt-0.5 text-footnote text-fg-secondary server-text">
                    {item.departmentName} · {format.date(item.createdAt)}
                    {item.neededBy ? ` · ${t('teamVacancy.neededByShort', { date: format.date(item.neededBy) })}` : ''}
                  </p>
                </div>
                <span className="ml-3 shrink-0">
                  <StatusChip look={REQUEST_STATUS_LOOK[item.status]} size="sm" />
                </span>
              </div>
              {item.status === 'Rejected' && item.decisionNote ? (
                <p className="mt-1.5 text-footnote text-danger-text server-text">
                  {t('teamVacancy.rejectReason', { reason: item.decisionNote })}
                </p>
              ) : null}
              {item.status === 'Approved' ? (
                <p className="mt-1.5 text-footnote text-fg-secondary">
                  {t('teamVacancy.approvedInfo', {
                    candidates: format.number(item.candidateCount),
                    hired: format.number(item.hiredCount),
                  })}
                </p>
              ) : null}
            </button>
          ))}
        </div>
      )}

      <ConfirmSheet
        open={cancelling !== null}
        title={t('teamVacancy.cancelTitle')}
        text={cancelling ? t('teamVacancy.rowTitle', { title: cancelling.title, n: format.number(cancelling.openings) }) : undefined}
        confirmLabel={t('teamVacancy.cancelConfirm')}
        cancelLabel={t('common.stay')}
        danger
        busy={busy}
        onConfirm={() => void cancel()}
        onClose={() => setCancelling(null)}
      />
    </Card>
  )
}

/** `/jamoa/vakansiya/yangi` — so'rov formasi. */
export function TeamVacancyNewScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const errorText = useErrorText()
  const me = useMe()

  const isLead = me.data?.isTeamLead === true
  const scope = useTeamScope(isLead)
  const departments = scope.data ? departmentOptions(scope.data.departments) : []
  // Bitta bo'limli boshliq bo'limni tanlamaydi — server o'zi qo'yadi
  const roots = departments.filter((item) => item.depth === 0)
  const needsDepartment = roots.length !== 1

  const [departmentId, setDepartmentId] = useState<string | null>(null)
  const [title, setTitle] = useState('')
  const [openings, setOpenings] = useState('1')
  const [hasDeadline, setHasDeadline] = useState(false)
  const [neededBy, setNeededBy] = useState(() => addDays(todayIso(), 14))
  const [note, setNote] = useState('')

  const [touched, setTouched] = useState(false)
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState<ApiError | null>(null)
  const [leaveOpen, setLeaveOpen] = useState(false)

  const count = Number.parseInt(openings, 10)
  const titleError = touched && title.trim() === '' ? t('teamVacancy.titleRequired') : null
  const countError =
    touched && !(Number.isInteger(count) && count >= 1 && count <= MAX_OPENINGS)
      ? t('teamVacancy.openingsRange', { max: MAX_OPENINGS })
      : null
  const departmentError = touched && needsDepartment && !departmentId ? t('teamVacancy.departmentRequired') : null

  const dirty = title !== '' || note !== '' || openings !== '1' || hasDeadline || departmentId !== null

  const submit = async (): Promise<void> => {
    setTouched(true)
    if (title.trim() === '' || !(Number.isInteger(count) && count >= 1 && count <= MAX_OPENINGS)) return
    if (needsDepartment && !departmentId) return

    setSending(true)
    setSendError(null)
    try {
      await api.createTeamVacancyRequest({
        departmentId,
        title: title.trim().slice(0, MAX_TITLE),
        openings: count,
        neededBy: hasDeadline ? neededBy : null,
        note: note.trim() === '' ? null : note.trim(),
      })
      invalidateTeamVacancyRequests()
      toast.show(t('teamVacancy.sent'), 'success')
      navigate(-1)
    } catch (cause: unknown) {
      setSendError(ApiError.from(cause))
    } finally {
      setSending(false)
    }
  }

  const goBack = (): void => {
    if (dirty) {
      setLeaveOpen(true)
      return
    }
    navigate(-1)
  }

  if (me.data && !isLead) {
    return (
      <PushedScreen title={t('teamVacancy.newTitle')}>
        <Card>
          <EmptyState icon="users" title={t('team.notLeadTitle')} text={t('team.notLeadText')} />
        </Card>
      </PushedScreen>
    )
  }

  return (
    <>
      <PushedScreen title={t('teamVacancy.newTitle')} onBack={goBack} bottomSpace>
        <Card>
          {!scope.data ? (
            scope.error ? <ErrorState onRetry={scope.refetch} /> : <SkeletonLines rows={4} />
          ) : (
            <div className="space-y-4">
              {departments.length > 1 ? (
                <Field
                  label={t('team.department')}
                  required={needsDepartment}
                  error={departmentError}
                  helper={needsDepartment ? undefined : t('teamVacancy.departmentHelper', { name: roots[0]?.name ?? '' })}
                >
                  <SelectRow
                    sheetTitle={t('team.department')}
                    value={departmentId}
                    options={departments}
                    placeholder={needsDepartment ? t('common.select') : (roots[0]?.name ?? t('common.select'))}
                    invalid={departmentError !== null}
                    onChange={(id) => setDepartmentId(id)}
                  />
                </Field>
              ) : null}

              <Field label={t('teamVacancy.position')} required error={titleError}>
                <TextInput
                  value={title}
                  onChange={(value) => setTitle(value.slice(0, MAX_TITLE))}
                  placeholder={t('teamVacancy.positionPlaceholder')}
                  invalid={titleError !== null}
                  autoComplete="off"
                />
              </Field>

              <Field label={t('teamVacancy.openings')} required error={countError}>
                <TextInput
                  value={openings}
                  onChange={(value) => setOpenings(value.replace(/[^0-9]/g, '').slice(0, 3))}
                  inputMode="numeric"
                  invalid={countError !== null}
                  autoComplete="off"
                />
              </Field>

              <div>
                <SwitchRow label={t('teamVacancy.hasDeadline')} checked={hasDeadline} onChange={setHasDeadline} />
                {hasDeadline ? (
                  <div className="mt-2">
                    <DateRow
                      sheetTitle={t('teamVacancy.neededBy')}
                      value={neededBy}
                      min={todayIso()}
                      onChange={setNeededBy}
                    />
                  </div>
                ) : null}
              </div>

              <Field label={t('teamVacancy.note')} helper={t('teamVacancy.noteHelper')}>
                <TextArea value={note} onChange={setNote} placeholder={t('teamVacancy.notePlaceholder')} />
              </Field>
            </div>
          )}
        </Card>

        {sendError ? (
          <InlineMessage tone="danger" title={t('teamVacancy.sendFailed')} assertive>
            {errorText(sendError).body}
          </InlineMessage>
        ) : null}

        <p className="px-1 text-footnote text-fg-tertiary">{t('teamVacancy.hint')}</p>
      </PushedScreen>

      <BottomAction>
        <Button block busy={sending} disabled={!scope.data} onClick={() => void submit()}>
          {t('common.submit')}
        </Button>
      </BottomAction>

      <ConfirmSheet
        open={leaveOpen}
        title={t('requests.dirtyTitle')}
        confirmLabel={t('requests.dirtyLeave')}
        cancelLabel={t('common.stay')}
        danger
        onConfirm={() => {
          setLeaveOpen(false)
          navigate(-1)
        }}
        onClose={() => setLeaveOpen(false)}
      />
    </>
  )
}
