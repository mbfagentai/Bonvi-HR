import { LoaderCircle } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { useCatalogList } from '@/features/catalog/api/catalog-queries'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Button,
  DateField,
  Dialog,
  MoneyInput,
  Select,
  TextArea,
  TextField,
} from '@/shared/ui'
import { todayIso } from '@/shared/lib/today'

import type { EmploymentSummary, TimelineEvent } from '../api/employees-api'
import {
  useAdditionalDuty,
  useChangeSalary,
  useEditEmploymentEvent,
  useEndAdditionalDuty,
  useFilterOptions,
  useTerminate,
  useTransfer,
} from '../api/employees-queries'
import { employeeErrorCode, terminationWarning } from '../lib/employee-error'
import { EMPLOYMENT_SALARY, SALARY_EXTRA_VIEW, SALARY_VIEW } from '../lib/employee-presentation'
import { warningsToShow, type HrWarning } from '../lib/hr-warnings'
import { HrOutcomeDialog } from './hr-outcome-dialog'

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
      <DateField
        label={t('employee.orderDate')}
        value={orderDate}
        onChange={onOrderDate}
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

  const [effectiveDate, setEffectiveDate] = useState(todayIso)
  const [departmentId, setDepartmentId] = useState('')
  const [positionId, setPositionId] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')
  const [note, setNote] = useState('')

  useEffect(() => {
    if (employment === null) return

    setEffectiveDate(todayIso())
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
          // Ko'chirish oynasida oklad maydoni yo'q — oklad alohida buyruq
          // (`SalaryDialog`). Summa yuborilmagani uchun bu amal
          // `employment.salary` siz ham o'tadi (`EMPLOYMENT_SALARY` izohi).
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
      <DateField
        label={t('employees.effectiveDate')}
        value={effectiveDate}
        onChange={setEffectiveDate}
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
  const { hasPermission } = useAuth()

  /*
    Ustama — PUL QOIDASI bo'yicha (`EMPLOYMENT_SALARY` izohi) VA qo'shimcha
    haqning alohida ruxsati bilan (`SALARY_EXTRA_VIEW` izohi, 2026-09-27):
    ruxsatsiz odamga maydon chizilmaydi va summa yuborilmaydi. Aks holda
    server vazifaning o'zini ham 403 bilan rad etardi.
  */
  const canSetSalary =
    hasPermission(EMPLOYMENT_SALARY) && hasPermission(SALARY_VIEW) && hasPermission(SALARY_EXTRA_VIEW)

  const [text, setText] = useState('')
  const [effectiveDate, setEffectiveDate] = useState(todayIso)
  const [extra, setExtra] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')

  useEffect(() => {
    if (employment === null) return

    setText('')
    setEffectiveDate(todayIso())
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
          extraSalaryAmount: !canSetSalary || extra.trim() === '' ? null : Number(extra),
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
        <DateField
          label={t('employees.effectiveDate')}
          value={effectiveDate}
          onChange={setEffectiveDate}
        />
        {canSetSalary && (
          <MoneyInput label={t('employees.extraSalary')} value={extra} onChange={setExtra} />
        )}
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
  const [effectiveDate, setEffectiveDate] = useState(todayIso)
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')

  useEffect(() => {
    if (employment === null) return

    setAmount(employment.salary === null ? '' : String(employment.salary.amount))
    setEffectiveDate(todayIso())
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
      <MoneyInput
        label={t('employee.salary')}
        hint={t('employees.salaryHint')}
        value={amount}
        onChange={setAmount}
      />

      <DateField
        label={t('employees.effectiveDate')}
        value={effectiveDate}
        onChange={setEffectiveDate}
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

/* ═══════════════════════════════════════ Mehnat yo'li — xatoni tuzatish */

/**
 * ⭐ Mehnat yo'lidagi BITTA yozuvning terish xatosini tuzatish.
 *
 * <b>Bu «qarorni o'zgartirish» EMAS</b> va shuning uchun oynada hodisa
 * TURI ham, lavozim/bo'lim ham yo'q: ular «nima bo'lgan» degan faktning
 * o'zi. «Oklad 5 mln bo'lishi kerak edi» — bu yangi qaror, yangi buyruq
 * (`SalaryDialog`); «buyruq sanasi 06.09 emas, 16.09 edi» esa AYNI
 * hujjatning noto'g'ri yozib olingani va shu yerda tuzatiladi
 * (foydalanuvchi talabi, 2026-09-16).
 *
 * <b>Oklad maydoni FAQAT summasi bor hodisada chiziladi</b> — qabul va
 * oklad o'zgarishida. Bo'lim ko'chishida summa yo'q va uni so'rash
 * hodisani boshqa turga aylantirib yuborardi (server ham qabul
 * qilmaydi).
 *
 * <b>Maydonlar MAVJUD qiymat bilan to'ldirilib ochiladi.</b> Bo'sh
 * ochilsa, HR faqat sanani tuzatib saqlaganda buyruq raqami va izoh
 * o'chib ketardi: server yuborilgan qiymatni ustidan yozadi.
 */
export function EmploymentEventEditDialog({
  employeeId,
  event,
  canEditSalary,
  onClose,
}: {
  employeeId: string
  event: TimelineEvent | null
  /**
   * Summani ko'rish (`salary.view`) VA o'zgartirish (`employment.salary`)
   * huquqi birga — pul qoidasi (`EMPLOYMENT_SALARY` izohi).
   */
  canEditSalary: boolean
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const edit = useEditEmploymentEvent(employeeId, event?.id ?? '')

  const [effectiveDate, setEffectiveDate] = useState(todayIso)
  const [amount, setAmount] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')
  const [note, setNote] = useState('')

  useEffect(() => {
    if (event === null) return

    setEffectiveDate(event.effectiveDate)
    setAmount(event.toSalary === null ? '' : String(event.toSalary.amount))
    setOrderNo(event.orderNo ?? '')
    setOrderDate(event.orderDate ?? '')
    setNote(event.note ?? '')
    edit.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [event])

  if (event === null) return null

  /*
    Summa maydoni yozuvning O'ZIDA summa bo'lgandagina chiziladi va
    faqat uni ko'rish HAM o'zgartirish huquqi bo'lsa: ko'ra olmaydigan
    odamga bo'sh maydon ko'rsatish «oklad kiritilmagan» degan yolg'on
    taassurot berardi, o'zgartira olmaydiganga esa server butun tuzatishni
    rad etadigan forma taklif qilinardi. Maydonsiz so'rovda summa `null` —
    server uni «o'zgarmadi» deb tushunadi va yozuvdagi oklad saqlanadi.
  */
  const salaryEditable = event.toSalary !== null && canEditSalary

  const submit = () => {
    edit.mutate(
      {
        employmentId: event.employmentId,
        body: {
          effectiveDate,
          amount: salaryEditable && amount.trim() !== '' ? Number(amount) : null,
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
      title={t('employees.eventEditTitle')}
      submitLabel={t('common.save')}
      onSubmit={submit}
      pending={edit.isPending}
      disabled={effectiveDate === ''}
      error={
        edit.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(edit.error))}</Alert>
      }
    >
      <p className="text-footnote text-[var(--fg-secondary)]">
        {t('employees.eventEditHint')}
      </p>

      {/*
        Pul qarorining (oklad, vazifa haqi) sanasi ham pul amali — usiz
        server tuzatishni 403 bilan rad etardi. Maydon ko'rinadi, lekin
        qulflanadi: HR sanani ko'radi va nega o'zgartira olmasligini biladi.
      */}
      <DateField
        label={t('employees.effectiveDate')}
        hint={event.dateLocked ? t('employees.eventDateLockedHint') : undefined}
        value={effectiveDate}
        onChange={setEffectiveDate}
        disabled={event.dateLocked}
      />

      {salaryEditable && (
        <MoneyInput
          // Qo'shimcha vazifaning summasi — oklad emas, qo'shimcha haq
          label={event.type === 'additionalDuty' ? t('employees.extraPay') : t('employee.salary')}
          currency={event.toSalary?.currency}
          value={amount}
          onChange={setAmount}
        />
      )}

      <OrderFields
        orderNo={orderNo}
        orderDate={orderDate}
        onOrderNo={setOrderNo}
        onOrderDate={setOrderDate}
      />

      <TextArea
        label={t('employees.note')}
        maxLength={2000}
        value={note}
        onChange={(event_) => setNote(event_.target.value)}
      />
    </ActionDialog>
  )
}

/* ═══════════════════════════════════════ Qo'shimcha vazifani tugatish */

/**
 * Qo'shimcha vazifani tugatish — yoki tugatilganini bekor qilish.
 *
 * Yozuv O'CHIRILMAYDI: mehnat yo'lida «falon sanadan falon sanagacha»
 * bo'lib qoladi, tugagan vazifa haqi esa kartadagi jamiga kirmaydi
 * (egasining qarori, 2026-09-27). Sana — vazifa BO'LMAGAN birinchi kun
 * (buyruqdagi «falon sanadan boshlab vazifa olinsin»).
 */
export function EndDutyDialog({
  employeeId,
  event,
  onClose,
}: {
  employeeId: string
  event: TimelineEvent | null
  onClose: () => void
}) {
  const { t, date } = useLocale()
  const errorMessage = useErrorMessage()
  const end = useEndAdditionalDuty(employeeId, event?.id ?? '')

  const [endDate, setEndDate] = useState(todayIso)

  useEffect(() => {
    if (event === null) return

    setEndDate(event.dutyEndDate ?? todayIso())
    end.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [event])

  if (event === null) return null

  const save = (value: string | null) =>
    end.mutate({ employmentId: event.employmentId, body: { endDate: value } }, { onSuccess: onClose })

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!end.isPending}
      title={t('employees.dutyEndTitle')}
      description={event.note ?? undefined}
      footer={
        <>
          {/* Tugatilgan vazifani qayta ochish — xato bosilgan tugatishni qaytarish */}
          {event.dutyEndDate !== null && (
            <Button
              type="button"
              variant="ghost"
              onClick={() => save(null)}
              disabled={end.isPending}
              className="mr-auto"
            >
              {t('employees.dutyReopen')}
            </Button>
          )}

          <Button type="button" variant="ghost" onClick={onClose} disabled={end.isPending}>
            {t('common.cancel')}
          </Button>

          <Button
            type="button"
            onClick={() => save(endDate)}
            disabled={end.isPending || endDate === ''}
          >
            {end.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {end.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(end.error))}</Alert>}

        <p className="text-footnote text-[var(--fg-secondary)]">
          {t('employees.dutyEndHint', { since: date(event.effectiveDate) })}
        </p>

        <DateField
          label={t('employees.dutyEndDate')}
          value={endDate}
          min={event.effectiveDate}
          onChange={setEndDate}
        />
      </div>
    </Dialog>
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

  const [terminationDate, setTerminationDate] = useState(todayIso)
  const [reasonId, setReasonId] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')
  const [note, setNote] = useState('')
  /** The flags HR has confirmed in this dialog; `null` until then. A retry keeps the confirmation. */
  const [confirmed, setConfirmed] = useState<readonly string[] | null>(null)
  const [outcome, setOutcome] = useState<HrWarning[] | null>(null)

  useEffect(() => {
    if (employment === null) return

    setTerminationDate(todayIso())
    setReasonId('')
    setOrderNo('')
    setOrderDate('')
    setNote('')
    setConfirmed(null)
    setOutcome(null)
    leave.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [employment])

  if (employment === null) return null

  const warning = terminationWarning(leave.error)

  if (outcome !== null) {
    return (
      <HrOutcomeDialog
        title={t('employees.terminateTitle')}
        description={employment.legalEntity?.name ?? undefined}
        done={t('employees.terminatedDone')}
        warnings={outcome}
        onClose={onClose}
      />
    )
  }

  const submit = () => {
    const acknowledged = warning === null ? confirmed : [warning.code, ...warning.extra]
    setConfirmed(acknowledged)

    leave.mutate(
      {
        employmentId: employment.id,
        confirm: acknowledged !== null,
        body: {
          terminationDate,
          reasonId: reasonId === '' ? null : reasonId,
          orderNo: orderNo.trim() === '' ? null : orderNo.trim(),
          orderDate: orderDate === '' ? null : orderDate,
          note: note.trim() === '' ? null : note.trim(),
        },
      },
      {
        onSuccess: ({ warnings }) => {
          const unread = warningsToShow(warnings, acknowledged)
          if (unread.length > 0) setOutcome(unread)
          else onClose()
        },
      },
    )
  }

  return (
    <ActionDialog
      open
      onClose={onClose}
      title={t('employees.terminateTitle')}
      description={employment.legalEntity?.name ?? undefined}
      submitLabel={warning === null ? t('employees.terminate') : t('employees.terminateAnyway')}
      onSubmit={submit}
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
      <DateField
        label={t('employees.terminationDate')}
        value={terminationDate}
        onChange={setTerminationDate}
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
