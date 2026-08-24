import { LoaderCircle } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'

import { useCatalogList } from '@/features/catalog/api/catalog-queries'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import type { EmploymentSummary } from '../api/employees-api'
import {
  useAdditionalDuty,
  useChangeSalary,
  useFilterOptions,
  useTerminate,
  useTransfer,
} from '../api/employees-queries'
import { employeeErrorCode, terminationWarning } from '../lib/employee-error'

/** `YYYY-MM-DD` — server `DateOnly` kutadi. */
function today(): string {
  return new Date().toISOString().slice(0, 10)
}

/**
 * Buyruq maydonlari — to'rtala oynada bir xil.
 *
 * HR aytgan qoida: har bir o'zgarish buyruq bilan rasmiylashtiriladi va
 * qidiruv aynan buyruq raqami bo'yicha ketadi. Shu sababli ular alohida
 * komponentga chiqarilgan — bittasida unutilib qolmasin.
 */
function OrderFields({
  orderNo,
  orderDate,
  onOrderNo,
  onOrderDate,
}: {
  orderNo: string
  orderDate: string
  onOrderNo: (value: string) => void
  onOrderDate: (value: string) => void
}) {
  const { t } = useLocale()

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <TextField
        label={t('employee.orderNo')}
        value={orderNo}
        onChange={(event) => onOrderNo(event.target.value)}
      />
      <TextField
        label={t('employee.orderDate')}
        type="date"
        value={orderDate}
        onChange={(event) => onOrderDate(event.target.value)}
      />
    </div>
  )
}

/** To'rtala oynaning umumiy qobig'i — takrorlanadigan tugmalar va xato bloki. */
function ActionDialog({
  open,
  onClose,
  title,
  description,
  submitLabel,
  onSubmit,
  pending,
  disabled = false,
  error,
  danger = false,
  children,
}: {
  open: boolean
  onClose: () => void
  title: ReactNode
  description?: ReactNode
  submitLabel: string
  onSubmit: () => void
  pending: boolean
  disabled?: boolean
  error?: ReactNode
  danger?: boolean
  children: ReactNode
}) {
  const { t } = useLocale()

  return (
    <Dialog
      open={open}
      onClose={onClose}
      dismissible={!pending}
      title={title}
      description={description}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            variant={danger ? 'danger' : 'primary'}
            onClick={onSubmit}
            disabled={pending || disabled}
          >
            {pending && <LoaderCircle className="animate-spin" />}
            {submitLabel}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {error}
        {children}
      </div>
    </Dialog>
  )
}

/* ═══════════════════════════════════════════════ Lavozim / bo'lim o'zgarishi */

/**
 * Ko'chirish — lavozim yoki bo'lim o'zgarishi (Excel: «91.3»).
 *
 * <b>Avvalgi qiymatlar oldindan to'ldiriladi.</b> HR odatda faqat bittasini
 * o'zgartiradi; bo'sh forma bo'lsa, qolganini qayta terishga to'g'ri kelardi
 * va bir maydon unutilsa u jimgina tozalanib ketardi.
 */
export function TransferDialog({
  employeeId,
  employment,
  onClose,
}: {
  employeeId: string
  employment: EmploymentSummary | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const options = useFilterOptions()
  const move = useTransfer(employeeId)

  const [effectiveDate, setEffectiveDate] = useState(today)
  const [departmentId, setDepartmentId] = useState('')
  const [positionId, setPositionId] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')
  const [note, setNote] = useState('')

  useEffect(() => {
    if (employment === null) return

    setEffectiveDate(today())
    setDepartmentId(employment.department?.id ?? '')
    setPositionId(employment.position?.id ?? '')
    setOrderNo('')
    setOrderDate('')
    setNote('')
    move.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [employment])

  if (employment === null) return null

  const submit = () => {
    move.mutate(
      {
        employmentId: employment.id,
        body: {
          effectiveDate,
          departmentId: departmentId === '' ? null : departmentId,
          positionId: positionId === '' ? null : positionId,
          salaryAmount: null,
          orderNo: orderNo.trim() === '' ? null : orderNo.trim(),
          orderDate: orderDate === '' ? null : orderDate,
          note: note.trim() === '' ? null : note.trim(),
        },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <ActionDialog
      open
      onClose={onClose}
      title={t('employees.transferTitle')}
      description={employment.legalEntity?.name ?? undefined}
      submitLabel={t('employees.transfer')}
      onSubmit={submit}
      pending={move.isPending}
      disabled={effectiveDate === ''}
      error={move.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(move.error))}</Alert>}
    >
      <TextField
        label={t('employees.effectiveDate')}
        type="date"
        value={effectiveDate}
        onChange={(event) => setEffectiveDate(event.target.value)}
      />

      <Select
        label={t('employee.position')}
        options={options.positions}
        placeholder={t('employees.noChange')}
        value={positionId}
        onChange={(event) => setPositionId(event.target.value)}
      />

      <Select
        label={t('employee.department')}
        options={options.departments}
        placeholder={t('employees.noChange')}
        value={departmentId}
        onChange={(event) => setDepartmentId(event.target.value)}
      />

      <OrderFields
        orderNo={orderNo}
        orderDate={orderDate}
        onOrderNo={setOrderNo}
        onOrderDate={setOrderDate}
      />

      <TextArea
        label={t('employees.note')}
        rows={2}
        value={note}
        onChange={(event) => setNote(event.target.value)}
      />
    </ActionDialog>
  )
}

/* ═════════════════════════════════════════════════════ Qo'shimcha vazifa */

/** Qo'shimcha vazifa berish (Excel: «91.4»). */
export function AdditionalDutyDialog({
  employeeId,
  employment,
  onClose,
}: {
  employeeId: string
  employment: EmploymentSummary | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const duty = useAdditionalDuty(employeeId)

  const [text, setText] = useState('')
  const [effectiveDate, setEffectiveDate] = useState(today)
  const [extra, setExtra] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')

  useEffect(() => {
    if (employment === null) return

    setText('')
    setEffectiveDate(today())
    setExtra('')
    setOrderNo('')
    setOrderDate('')
    duty.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [employment])

  if (employment === null) return null

  const submit = () => {
    duty.mutate(
      {
        employmentId: employment.id,
        body: {
          duty: text.trim(),
          effectiveDate,
          extraSalaryAmount: extra.trim() === '' ? null : Number(extra),
          orderNo: orderNo.trim() === '' ? null : orderNo.trim(),
          orderDate: orderDate === '' ? null : orderDate,
        },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <ActionDialog
      open
      onClose={onClose}
      title={t('employees.dutyTitle')}
      submitLabel={t('employees.duty')}
      onSubmit={submit}
      pending={duty.isPending}
      // Vazifa matni majburiy — domen ham nomsiz vazifani rad etadi.
      disabled={text.trim() === '' || effectiveDate === ''}
      error={duty.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(duty.error))}</Alert>}
    >
      <TextArea
        label={t('employees.dutyText')}
        hint={t('employees.dutyHint')}
        rows={3}
        value={text}
        onChange={(event) => setText(event.target.value)}
      />

      <div className="grid gap-4 sm:grid-cols-2">
        <TextField
          label={t('employees.effectiveDate')}
          type="date"
          value={effectiveDate}
          onChange={(event) => setEffectiveDate(event.target.value)}
        />
        <TextField
          label={t('employees.extraSalary')}
          type="number"
          inputMode="numeric"
          value={extra}
          onChange={(event) => setExtra(event.target.value)}
        />
      </div>

      <OrderFields
        orderNo={orderNo}
        orderDate={orderDate}
        onOrderNo={setOrderNo}
        onOrderDate={setOrderDate}
      />
    </ActionDialog>
  )
}

/* ═════════════════════════════════════════════════════════════════ Oklad */

/** Oklad o'zgarishi. Bo'sh qiymat — oklad olib tashlanadi (ishbay to'lov). */
export function SalaryDialog({
  employeeId,
  employment,
  onClose,
}: {
  employeeId: string
  employment: EmploymentSummary | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const salary = useChangeSalary(employeeId)

  const [amount, setAmount] = useState('')
  const [effectiveDate, setEffectiveDate] = useState(today)
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')

  useEffect(() => {
    if (employment === null) return

    setAmount(employment.salary === null ? '' : String(employment.salary.amount))
    setEffectiveDate(today())
    setOrderNo('')
    setOrderDate('')
    salary.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [employment])

  if (employment === null) return null

  const submit = () => {
    salary.mutate(
      {
        employmentId: employment.id,
        body: {
          amount: amount.trim() === '' ? null : Number(amount),
          effectiveDate,
          orderNo: orderNo.trim() === '' ? null : orderNo.trim(),
          orderDate: orderDate === '' ? null : orderDate,
        },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <ActionDialog
      open
      onClose={onClose}
      title={t('employees.salaryTitle')}
      submitLabel={t('common.save')}
      onSubmit={submit}
      pending={salary.isPending}
      disabled={effectiveDate === ''}
      error={
        salary.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(salary.error))}</Alert>
      }
    >
      <TextField
        label={t('employee.salary')}
        hint={t('employees.salaryHint')}
        type="number"
        inputMode="numeric"
        value={amount}
        onChange={(event) => setAmount(event.target.value)}
      />

      <TextField
        label={t('employees.effectiveDate')}
        type="date"
        value={effectiveDate}
        onChange={(event) => setEffectiveDate(event.target.value)}
      />

      <OrderFields
        orderNo={orderNo}
        orderDate={orderDate}
        onOrderNo={setOrderNo}
        onOrderDate={setOrderDate}
      />
    </ActionDialog>
  )
}

/* ══════════════════════════════════════════════════════ Ishdan bo'shatish */

/**
 * Ishdan bo'shatish.
 *
 * <b>Ikki bosqichli va bu HR so'ragan qoida.</b> Xodim qora ro'yxatda yoki
 * nazoratda bo'lsa, server <c>409</c> va sababni qaytaradi. Foydalanuvchi
 * uni O'QIYDI va shundan keyingina tasdiqlaydi — bo'shatish jimgina o'tib
 * ketmasligi kerak.
 */
export function TerminateDialog({
  employeeId,
  employment,
  onClose,
}: {
  employeeId: string
  employment: EmploymentSummary | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const reasons = useCatalogList('termination-reasons', true)
  const leave = useTerminate(employeeId)

  const [terminationDate, setTerminationDate] = useState(today)
  const [reasonId, setReasonId] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')
  const [note, setNote] = useState('')

  useEffect(() => {
    if (employment === null) return

    setTerminationDate(today())
    setReasonId('')
    setOrderNo('')
    setOrderDate('')
    setNote('')
    leave.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [employment])

  if (employment === null) return null

  const warning = terminationWarning(leave.error)

  const submit = (confirm: boolean) => {
    leave.mutate(
      {
        employmentId: employment.id,
        confirm,
        body: {
          terminationDate,
          reasonId: reasonId === '' ? null : reasonId,
          orderNo: orderNo.trim() === '' ? null : orderNo.trim(),
          orderDate: orderDate === '' ? null : orderDate,
          note: note.trim() === '' ? null : note.trim(),
        },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <ActionDialog
      open
      onClose={onClose}
      title={t('employees.terminateTitle')}
      description={employment.legalEntity?.name ?? undefined}
      submitLabel={warning === null ? t('employees.terminate') : t('employees.terminateAnyway')}
      onSubmit={() => submit(warning !== null)}
      pending={leave.isPending}
      disabled={terminationDate === ''}
      danger
      error={
        leave.isError &&
        (warning === null ? (
          <Alert tone="danger">{errorMessage(employeeErrorCode(leave.error))}</Alert>
        ) : (
          <Alert tone="warning" title={errorMessage(warning.code)}>
            {/*
              Qo'shimcha kodlar ham ko'rsatiladi: xodim bir vaqtda ham qora
              ro'yxatda, ham nazoratda bo'lishi mumkin va ikkalasi ham
              qarorga ta'sir qiladi.
            */}
            <ul className="space-y-1">
              {warning.extra.map((code) => (
                <li key={code}>{errorMessage(code)}</li>
              ))}
              {warning.detail !== null && <li>{warning.detail}</li>}
            </ul>
          </Alert>
        ))
      }
    >
      <TextField
        label={t('employees.terminationDate')}
        type="date"
        value={terminationDate}
        onChange={(event) => setTerminationDate(event.target.value)}
      />

      <Select
        label={t('employees.terminationReason')}
        options={(reasons.data ?? []).map((item) => ({ value: item.id, label: item.name }))}
        placeholder={t('employees.chooseReason')}
        value={reasonId}
        onChange={(event) => setReasonId(event.target.value)}
      />

      <OrderFields
        orderNo={orderNo}
        orderDate={orderDate}
        onOrderNo={setOrderNo}
        onOrderDate={setOrderDate}
      />

      <TextArea
        label={t('employees.note')}
        rows={2}
        value={note}
        onChange={(event) => setNote(event.target.value)}
      />
    </ActionDialog>
  )
}
