import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { employeeErrorCode } from '@/features/employees/lib/employee-error'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import {
  PROBATION_RESULTS,
  type ProbationItem,
  type ProbationStatus,
} from '../api/probations-api'
import { useExtendProbation, useResolveProbation } from '../api/probations-queries'

/**
 * Sinov muddatini yakunlash.
 *
 * <b>Natija majburiy, xulosa esa yo'q</b> — domen shunday: yakuniy holat
 * ko'rsatilmasa server rad etadi, izoh esa ixtiyoriy. Forma aynan shu
 * qoidani takrorlaydi, o'zidan qo'shimcha shart o'ylab topmaydi.
 */
export function ResolveDialog({
  item,
  onClose,
}: {
  item: ProbationItem | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const resolve = useResolveProbation()

  const [status, setStatus] = useState<ProbationStatus>('passed')
  const [conclusion, setConclusion] = useState('')

  // Dialog yopilib qayta ochilganda oldingi xodimning xulosasi qolib
  // ketmasligi kerak — bu boshqa odamga tegishli matn bo'lardi.
  useEffect(() => {
    if (item !== null) {
      setStatus('passed')
      setConclusion('')
      resolve.reset()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const submit = () => {
    resolve.mutate(
      {
        id: item.id,
        request: { status, conclusion: conclusion.trim() === '' ? null : conclusion.trim() },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!resolve.isPending}
      title={t('probation.resolveTitle')}
      description={item.employeeName}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={resolve.isPending}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" onClick={submit} disabled={resolve.isPending}>
            {resolve.isPending && <LoaderCircle className="animate-spin" />}
            {t('probation.resolve')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {resolve.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(resolve.error))}</Alert>}

        <Select
          label={t('probation.result')}
          options={PROBATION_RESULTS.map((value) => ({
            value,
            label: t(`probation.status.${value}`),
          }))}
          value={status}
          onChange={(event) => setStatus(event.target.value as ProbationStatus)}
        />

        <TextArea
          label={t('probation.conclusion')}
          hint={t('probation.conclusionHint')}
          rows={4}
          value={conclusion}
          onChange={(event) => setConclusion(event.target.value)}
        />
      </div>
    </Dialog>
  )
}

/**
 * Sinov muddatini uzaytirish.
 *
 * Server ikkita qoidani tekshiradi: yangi sana joriysidan keyin bo'lishi va
 * umumiy muddat 180 kundan oshmasligi (Mehnat kodeksi chegarasi). Ular bu
 * yerda TAKRORLANMAYDI — qoida ikki joyda yozilsa, biri o'zgarganda
 * ikkinchisi jimgina eskiradi. Mijoz faqat bo'sh sana yuborilmasligini
 * kuzatadi, qolganini server aytadi.
 */
export function ExtendDialog({
  item,
  onClose,
}: {
  item: ProbationItem | null
  onClose: () => void
}) {
  const { t, date } = useLocale()
  const errorMessage = useErrorMessage()
  const extend = useExtendProbation()

  const [newEndDate, setNewEndDate] = useState('')

  useEffect(() => {
    if (item !== null) {
      setNewEndDate('')
      extend.reset()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const submit = () => {
    if (newEndDate === '') return
    extend.mutate({ id: item.id, request: { newEndDate } }, { onSuccess: onClose })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!extend.isPending}
      title={t('probation.extendTitle')}
      description={item.employeeName}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={extend.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="primary"
            onClick={submit}
            disabled={extend.isPending || newEndDate === ''}
          >
            {extend.isPending && <LoaderCircle className="animate-spin" />}
            {t('probation.extend')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {extend.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(extend.error))}</Alert>}

        <TextField
          label={t('probation.newEndDate')}
          hint={
            item.endDate === null
              ? undefined
              : t('probation.currentEndDate', { date: date(item.endDate) })
          }
          type="date"
          value={newEndDate}
          onChange={(event) => setNewEndDate(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
