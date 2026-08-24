import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import { EMPLOYEE_FLAG_TYPES, type EmployeeFlagType } from '../api/employees-api'
import { useCreateFlag } from '../api/employees-queries'
import { employeeErrorCode } from '../lib/employee-error'
import { FLAG_LOOK } from '../lib/employee-presentation'

/** `YYYY-MM-DD` — server `DateOnly` kutadi. */
function today(): string {
  return new Date().toISOString().slice(0, 10)
}

/**
 * Xodimga belgi qo'yish: rezerv, qora ro'yxat, nazorat, qobiliyatli.
 *
 * <b>Belgi aynan shu yerdan qo'yiladi</b>, ro'yxat sahifasidan emas: belgi
 * odamga tegishli va uni qo'yayotgan HR o'sha odamning kartasini ko'rib
 * turgan bo'lishi kerak.
 */
export function RaiseFlagDialog({
  employeeId,
  employeeName,
  open,
  onClose,
}: {
  employeeId: string
  employeeName: string
  open: boolean
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const raise = useCreateFlag(employeeId)

  const [type, setType] = useState<EmployeeFlagType>('reserve')
  const [startDate, setStartDate] = useState(today)
  const [endDate, setEndDate] = useState('')
  const [reason, setReason] = useState('')

  useEffect(() => {
    if (!open) return

    setType('reserve')
    setStartDate(today())
    setEndDate('')
    setReason('')
    raise.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  if (!open) return null

  /*
   * Qora ro'yxat uchun sabab MAJBURIY — bu domen qoidasi, mijoz uni
   * takrorlamaydi, faqat oldindan ko'rsatadi. Sababsiz yozuvlar Excel'da
   * bor edi va keyin hech kim nima uchunligini bilmasdi.
   */
  const reasonRequired = type === 'blacklist'
  const incomplete = startDate === '' || (reasonRequired && reason.trim() === '')

  const submit = () => {
    raise.mutate(
      {
        type,
        startDate,
        endDate: endDate === '' ? null : endDate,
        reason: reason.trim() === '' ? null : reason.trim(),
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!raise.isPending}
      title={t('employees.raiseFlagTitle')}
      description={employeeName}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={raise.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="primary"
            onClick={submit}
            disabled={raise.isPending || incomplete}
          >
            {raise.isPending && <LoaderCircle className="animate-spin" />}
            {t('employees.raiseFlag')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {raise.isError && (
          <Alert tone="danger">{errorMessage(employeeErrorCode(raise.error))}</Alert>
        )}

        <Select
          label={t('employees.flagType')}
          options={EMPLOYEE_FLAG_TYPES.map((value) => ({
            value,
            label: t(`employees.flags.${FLAG_LOOK[value].key}`),
          }))}
          value={type}
          onChange={(event) => setType(event.target.value as EmployeeFlagType)}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('flags.startDate')}
            type="date"
            value={startDate}
            onChange={(event) => setStartDate(event.target.value)}
          />
          <TextField
            label={t('flags.endDate')}
            hint={t('employees.flagEndHint')}
            type="date"
            value={endDate}
            onChange={(event) => setEndDate(event.target.value)}
          />
        </div>

        <TextArea
          label={reasonRequired ? t('employees.reasonRequired') : t('flags.reason')}
          hint={reasonRequired ? t('employees.blacklistReasonHint') : undefined}
          rows={3}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
