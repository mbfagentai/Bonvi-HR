import { BriefcaseBusiness, LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useFilterOptions } from '@/features/employees/api/employees-queries'
import {
  AttendanceEmployeePicker,
  type PickedEmployee,
} from '@/features/verification/components/attendance-employee-picker'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Badge, Button, DateField, Dialog, Select, TextArea, TextField } from '@/shared/ui'
import type { VacancyRequest } from '../api/vacancy-requests-api'
import {
  useApproveVacancyRequest,
  useCancelVacancyRequest,
  useCreateVacancyRequest,
  useRejectVacancyRequest,
} from '../api/vacancy-requests-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, RECRUITING_MANAGE, today } from '../lib/recruiting-presentation'

const CREATE_FORM_ID = 'vacancy-request-form'
const APPROVE_FORM_ID = 'vacancy-request-approve-form'
const TITLE_MAX_LENGTH = 200

/** Holat yorlig'ining rangi — ro'yxat va karta bir xil. */
export const REQUEST_STATUS_TONE = {
  Pending: 'warning',
  Approved: 'success',
  Rejected: 'danger',
  Cancelled: 'neutral',
} as const

/**
 * Yangi so'rov — boshliq aytgan 3–4 narsa: bo'lim, lavozim, nechta odam, qachondan, izoh
 * (`docs/SPEC-vakansiya-sorovi.md`). So'ragan xodim tanlanmasa — bo'lim boshlig'i.
 */
export function VacancyRequestCreateDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const create = useCreateVacancyRequest()
  const options = useFilterOptions()

  const [departmentId, setDepartmentId] = useState('')
  const [requester, setRequester] = useState<PickedEmployee | null>(null)
  const [title, setTitle] = useState('')
  const [openings, setOpenings] = useState('1')
  const [neededBy, setNeededBy] = useState('')
  const [note, setNote] = useState('')
  const [failure, setFailure] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return
    create.reset()
    setFailure(null)
    setDepartmentId('')
    setRequester(null)
    setTitle('')
    setOpenings('1')
    setNeededBy('')
    setNote('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  if (!open) return null

  const incomplete = departmentId === '' || title.trim() === ''

  const submit = () => {
    if (incomplete) return
    setFailure(null)

    create.mutate(
      {
        departmentId,
        requestedByEmployeeId: requester?.id ?? null,
        title: title.trim(),
        // Bo'sh maydon 1 — `vacancy-form-dialog.tsx` dagi bilan bir xil mulohaza
        openings: openings.trim() === '' ? 1 : Number(openings),
        neededBy: orNull(neededBy),
        note: orNull(note),
      },
      { onSuccess: onClose, onError: (error) => setFailure(recruitingErrorCode(error)) },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!create.isPending}
      title={t('vacancyRequests.createTitle')}
      description={t('vacancyRequests.createText')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={create.isPending}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form={CREATE_FORM_ID} variant="primary" disabled={create.isPending || incomplete}>
            {create.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <form
        id={CREATE_FORM_ID}
        noValidate
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault()
          submit()
        }}
      >
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}

        <Select
          label={t('employee.department')}
          placeholder={t('vacancyRequests.chooseDepartment')}
          options={options.departments}
          value={departmentId}
          disabled={create.isPending}
          onChange={(event) => setDepartmentId(event.target.value)}
        />

        <AttendanceEmployeePicker
          label={t('vacancyRequests.requestedBy')}
          value={requester}
          onChange={setRequester}
          employedOnly
          disabled={create.isPending}
        />
        <p className="-mt-2 text-footnote text-[var(--fg-secondary)]">{t('vacancyRequests.requestedByHint')}</p>

        <TextField
          label={t('vacancyRequests.position')}
          placeholder={t('vacancyRequests.positionPlaceholder')}
          autoComplete="off"
          maxLength={TITLE_MAX_LENGTH}
          value={title}
          disabled={create.isPending}
          onChange={(event) => setTitle(event.target.value)}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('vacancyRequests.openings')}
            type="number"
            min={1}
            max={100}
            inputMode="numeric"
            value={openings}
            disabled={create.isPending}
            onChange={(event) => setOpenings(event.target.value)}
          />
          <DateField
            label={t('vacancyRequests.neededBy')}
            value={neededBy}
            disabled={create.isPending}
            onChange={setNeededBy}
          />
        </div>

        <TextArea
          label={t('vacancyRequests.note')}
          placeholder={t('vacancyRequests.notePlaceholder')}
          rows={3}
          maxLength={2000}
          value={note}
          disabled={create.isPending}
          onChange={(event) => setNote(event.target.value)}
        />
      </form>
    </Dialog>
  )
}

type Step = 'view' | 'approve' | 'reject'

/**
 * So'rov kartasi: tafsilot va HR qarori. «Vakansiya ochish» — so'rovdan to'ldirilgan vakansiya
 * formasi (egasi: «qoralama» — HR nomni, o'rin sonini va izohni tahrirlab saqlaydi), «Rad
 * etish» — sabab majburiy.
 */
export function VacancyRequestDialog({
  item,
  onClose,
}: {
  item: VacancyRequest | null
  onClose: () => void
}) {
  const { t, date, dateTime, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canDecide = hasPermission(RECRUITING_MANAGE)

  const approve = useApproveVacancyRequest()
  const reject = useRejectVacancyRequest()
  const cancel = useCancelVacancyRequest()
  const pending = approve.isPending || reject.isPending || cancel.isPending

  const [step, setStep] = useState<Step>('view')
  const [title, setTitle] = useState('')
  const [openings, setOpenings] = useState('1')
  const [openedAt, setOpenedAt] = useState(today)
  const [vacancyNote, setVacancyNote] = useState('')
  const [reason, setReason] = useState('')
  const [failure, setFailure] = useState<string | null>(null)

  useEffect(() => {
    if (item === null) return
    approve.reset()
    reject.reset()
    cancel.reset()
    setStep('view')
    setFailure(null)
    setTitle(item.title)
    setOpenings(String(item.openings))
    setOpenedAt(today())
    setVacancyNote(item.note ?? '')
    setReason('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const onError = (error: unknown) => setFailure(recruitingErrorCode(error))
  const isPending = item.status === 'Pending'

  const submitApprove = () => {
    if (title.trim() === '') return
    setFailure(null)
    approve.mutate(
      {
        id: item.id,
        body: {
          title: title.trim(),
          openings: openings.trim() === '' ? 1 : Number(openings),
          openedAt: orNull(openedAt),
          note: orNull(vacancyNote),
        },
      },
      { onSuccess: onClose, onError },
    )
  }

  const submitReject = () => {
    if (reason.trim() === '') return
    setFailure(null)
    reject.mutate({ id: item.id, reason: reason.trim() }, { onSuccess: onClose, onError })
  }

  const footer =
    step === 'approve' ? (
      <>
        <Button type="button" onClick={() => setStep('view')} disabled={pending}>
          {t('common.back')}
        </Button>
        <Button type="submit" form={APPROVE_FORM_ID} variant="primary" disabled={pending || title.trim() === ''}>
          {approve.isPending && <LoaderCircle className="animate-spin" />}
          {t('vacancyRequests.openVacancy')}
        </Button>
      </>
    ) : step === 'reject' ? (
      <>
        <Button type="button" onClick={() => setStep('view')} disabled={pending}>
          {t('common.back')}
        </Button>
        <Button type="button" variant="danger" disabled={pending || reason.trim() === ''} onClick={submitReject}>
          {reject.isPending && <LoaderCircle className="animate-spin" />}
          {t('vacancyRequests.reject')}
        </Button>
      </>
    ) : canDecide && isPending ? (
      <>
        <Button
          type="button"
          variant="ghost"
          disabled={pending}
          onClick={() => cancel.mutate(item.id, { onSuccess: onClose, onError })}
        >
          {cancel.isPending && <LoaderCircle className="animate-spin" />}
          {t('vacancyRequests.cancelRequest')}
        </Button>
        <Button type="button" variant="danger" disabled={pending} onClick={() => setStep('reject')}>
          {t('vacancyRequests.reject')}
        </Button>
        <Button type="button" variant="primary" disabled={pending} onClick={() => setStep('approve')}>
          <BriefcaseBusiness />
          {t('vacancyRequests.openVacancy')}
        </Button>
      </>
    ) : (
      <Button type="button" onClick={onClose}>
        {t('common.close')}
      </Button>
    )

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!pending}
      size="md"
      title={step === 'approve' ? t('vacancyRequests.approveTitle') : item.title}
      description={
        step === 'approve' ? t('vacancyRequests.approveText') : (item.departmentPath ?? item.departmentName)
      }
      footer={footer}
    >
      <div className="space-y-4">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}

        {step === 'view' && (
          <dl className="grid grid-cols-[minmax(0,10rem)_1fr] gap-x-4 gap-y-2.5 text-subhead">
            <Row label={t('vacancyRequests.status')}>
              <Badge tone={REQUEST_STATUS_TONE[item.status]}>{t(`vacancyRequests.statuses.${item.status}`)}</Badge>
            </Row>
            <Row label={t('vacancyRequests.requestedBy')}>{item.requestedByName}</Row>
            <Row label={t('employee.department')}>{item.departmentPath ?? item.departmentName}</Row>
            <Row label={t('vacancyRequests.openings')}>{num(item.openings)}</Row>
            <Row label={t('vacancyRequests.neededBy')}>{item.neededBy === null ? '—' : date(item.neededBy)}</Row>
            <Row label={t('vacancyRequests.createdAt')}>{dateTime(item.createdAt)}</Row>
            <Row label={t('vacancyRequests.note')}>
              <span className="whitespace-pre-wrap">{item.note ?? '—'}</span>
            </Row>
            {item.decidedAt !== null && (
              <Row label={t('vacancyRequests.decidedAt')}>
                {dateTime(item.decidedAt)}
                {item.decidedByName !== null && ` · ${item.decidedByName}`}
              </Row>
            )}
            {item.decisionNote !== null && (
              <Row label={t('vacancyRequests.rejectReason')}>
                <span className="whitespace-pre-wrap">{item.decisionNote}</span>
              </Row>
            )}
            {item.vacancyTitle !== null && (
              <Row label={t('vacancyRequests.vacancy')}>
                {item.vacancyTitle} ·{' '}
                {t('vacancyRequests.vacancyProgress', {
                  candidates: num(item.candidateCount),
                  hired: num(item.hiredCount),
                })}
              </Row>
            )}
          </dl>
        )}

        {step === 'approve' && (
          <form
            id={APPROVE_FORM_ID}
            noValidate
            className="space-y-4"
            onSubmit={(event) => {
              event.preventDefault()
              submitApprove()
            }}
          >
            <TextField
              label={t('recruiting.vacancyTitle')}
              hint={t('recruiting.vacancyTitleHint')}
              autoComplete="off"
              autoFocus
              maxLength={TITLE_MAX_LENGTH}
              value={title}
              disabled={pending}
              onChange={(event) => setTitle(event.target.value)}
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField
                label={t('recruiting.openings')}
                type="number"
                min={1}
                inputMode="numeric"
                value={openings}
                disabled={pending}
                onChange={(event) => setOpenings(event.target.value)}
              />
              <DateField label={t('recruiting.openedAt')} value={openedAt} disabled={pending} onChange={setOpenedAt} />
            </div>
            <TextArea
              label={t('recruiting.note')}
              rows={3}
              maxLength={2000}
              value={vacancyNote}
              disabled={pending}
              onChange={(event) => setVacancyNote(event.target.value)}
            />
          </form>
        )}

        {step === 'reject' && (
          <TextArea
            label={t('vacancyRequests.rejectReason')}
            hint={t('vacancyRequests.rejectReasonHint')}
            rows={3}
            maxLength={2000}
            autoFocus
            value={reason}
            disabled={pending}
            onChange={(event) => setReason(event.target.value)}
          />
        )}
      </div>
    </Dialog>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-[var(--fg-secondary)]">{label}</dt>
      <dd className="min-w-0 text-[var(--fg-primary)]">{children}</dd>
    </>
  )
}
