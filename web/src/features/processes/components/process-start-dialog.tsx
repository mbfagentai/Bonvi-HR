import { useNavigate } from '@tanstack/react-router'
import { LoaderCircle } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useEmployee } from '@/features/employees/api/employees-queries'
import {
  AttendanceEmployeePicker,
  type PickedEmployee,
} from '@/features/verification/components/attendance-employee-picker'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, DateField, Dialog, Select, Skeleton, TextArea } from '@/shared/ui'

import { useStartProcessCase } from '../api/processes-queries'
import {
  employmentOptionLabel,
  isBeforeHire,
  openEmployments,
  PROCESS_START_BASIS_MAX,
} from '../lib/process-start'
import { ProcessSectionError } from './process-section-error'

const FORM_ID = 'process-start-form'

/**
 * «Bo'shatishni boshlash» (SPEC §7.6, §10 #9): one current employment, its last working day and a free-text basis open
 * the dismissal case, and the new case opens. An employee with one current job needs no second choice; with several,
 * nothing is preselected, because the wrong job would open a dismissal nobody asked for.
 */
export function ProcessStartDialog({ onClose }: { onClose: () => void }) {
  const { t, date } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()
  const start = useStartProcessCase()

  const [employee, setEmployee] = useState<PickedEmployee | null>(null)
  const [employmentId, setEmploymentId] = useState('')
  const [lastWorkDay, setLastWorkDay] = useState('')
  const [basis, setBasis] = useState('')

  const card = useEmployee(employee?.id ?? '')
  const employments = useMemo(() => openEmployments(card.data?.employments ?? []), [card.data])

  const [onlyEmployment] = employments
  const employment =
    employments.length === 1 ? onlyEmployment : employments.find((item) => item.id === employmentId)

  const beforeHire = isBeforeHire(lastWorkDay, employment?.hireDate)
  const incomplete = employment === undefined || lastWorkDay === '' || beforeHire

  /** These three carry more here than their general text does (see `processes.board.start.error*`). */
  const failureText = (error: unknown): string => {
    const code = problemCode(error)
    switch (code) {
      case 'jarayon.allaqachon_ochilgan':
        return t('processes.board.start.errorAlreadyStarted')
      case 'jarayon.holat_notogri':
        return t('processes.board.start.errorWrongState')
      case 'jarayon.band':
        return t('processes.board.start.errorBusy')
      default:
        return errorMessage(code)
    }
  }

  const changeEmployee = (next: PickedEmployee | null) => {
    setEmployee(next)
    setEmploymentId('')
    start.reset()
  }

  const submit = () => {
    if (employment === undefined || incomplete || start.isPending) return
    const trimmedBasis = basis.trim()
    start.mutate(
      {
        processCode: 'dismissal',
        employmentId: employment.id,
        lastWorkDay,
        basis: trimmedBasis === '' ? null : trimmedBasis,
      },
      {
        onSuccess: (created) => {
          onClose()
          void navigate({ to: '/mening-ishlarim/$caseId', params: { caseId: created.id } })
        },
      },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!start.isPending}
      title={t('processes.board.startDismissal')}
      description={t('processes.board.start.text')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={start.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            variant="primary"
            disabled={start.isPending || incomplete}
          >
            {start.isPending && <LoaderCircle className="animate-spin" />}
            {t('processes.board.start.submit')}
          </Button>
        </>
      }
    >
      <form
        id={FORM_ID}
        noValidate
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault()
          submit()
        }}
      >
        {start.isError && <Alert tone="danger">{failureText(start.error)}</Alert>}

        <AttendanceEmployeePicker
          label={t('processes.board.start.employee')}
          value={employee}
          onChange={changeEmployee}
          employedOnly
          disabled={start.isPending}
        />

        {employee !== null &&
          (card.isPending ? (
            <Skeleton className="h-12 w-full" />
          ) : card.isError ? (
            <ProcessSectionError error={card.error} onRetry={() => void card.refetch()} />
          ) : employments.length === 0 ? (
            <Alert tone="warning">{t('processes.board.start.noEmployment')}</Alert>
          ) : (
            employments.length > 1 && (
              <Select
                label={t('processes.board.start.employment')}
                hint={t('processes.board.start.employmentHint')}
                placeholder={t('processes.board.start.chooseEmployment')}
                options={employments.map((item) => ({
                  value: item.id,
                  label: employmentOptionLabel(item, employments),
                }))}
                value={employmentId}
                disabled={start.isPending}
                onChange={(event) => {
                  setEmploymentId(event.target.value)
                  start.reset()
                }}
              />
            )
          ))}

        <DateField
          label={t('processes.board.start.lastWorkDay')}
          hint={
            employment !== undefined && !beforeHire && employment.hireDate !== ''
              ? t('processes.board.start.hiredOn', { date: date(employment.hireDate) })
              : undefined
          }
          error={
            employment !== undefined && beforeHire
              ? t('processes.board.start.beforeHire', { date: date(employment.hireDate) })
              : undefined
          }
          value={lastWorkDay}
          min={employment?.hireDate}
          required
          disabled={start.isPending}
          onChange={(next) => {
            setLastWorkDay(next)
            start.reset()
          }}
        />

        <TextArea
          label={t('processes.board.start.basis')}
          hint={t('processes.board.start.basisHint')}
          value={basis}
          maxLength={PROCESS_START_BASIS_MAX}
          disabled={start.isPending}
          onChange={(event) => setBasis(event.target.value)}
        />
      </form>
    </Dialog>
  )
}
