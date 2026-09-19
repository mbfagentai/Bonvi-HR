import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { employeeErrorCode } from '@/features/employees/lib/employee-error'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, DateField, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import {
  PROBATION_RESULTS,
  type ProbationStatus,
  type ProbationTarget,
} from '../api/probations-api'
import { useExtendProbation, useResolveProbation } from '../api/probations-queries'

/**
 * Sinov muddatini yakunlash — va u «o'tdi» bo'lsa, doimiy xodimga o'tkazish.
 *
 * <b>Natija majburiy, xulosa esa yo'q</b> — domen shunday: yakuniy holat
 * ko'rsatilmasa server rad etadi, izoh esa ixtiyoriy. Forma aynan shu
 * qoidani takrorlaydi, o'zidan qo'shimcha shart o'ylab topmaydi.
 *
 * <b>Nega tayinlash shu yerda.</b> HR talabi: «sinovdan o'tgan bo'lsa, bir
 * bosishda xodimlarga o'tkazib, unga kerakli hamma narsani qo'yib
 * yuborsak». Ilgari bu ikki qadam edi — avval sinov yopilardi, keyin xodim
 * kartasidan lavozim o'zgarishi yozilardi. Ikkinchisi muntazam unutilardi
 * va odam doimiy bo'lgani bilan «stajyor» lavozimida qolib ketardi.
 *
 * <b>Faqat O'ZGARGAN maydon yuboriladi.</b> Lavozim va bo'lim tanlovi
 * hozirgi qiymat bilan to'ldiriladi (HR nima borligini ko'rib tursin),
 * lekin tegilmagan bo'lsa so'rovga tushmaydi — aks holda hech nima
 * o'zgarmasa ham tarixda «lavozim o'zgarishi» hodisasi paydo bo'lardi.
 */
export function ResolveDialog({
  item,
  onClose,
}: {
  /*
    `ProbationTarget` — ATAYLAB tor shartnoma: oyna sinov ro'yxatidan ham,
    xodim kartasidan ham ochiladi va kartada to'liq ro'yxat qatori yo'q.
    Ikkinchi nusxa yozish o'rniga oyna faqat o'zi ishlatadigan maydonlarni
    so'raydi.
  */
  item: ProbationTarget | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const resolve = useResolveProbation()
  const options = useFilterOptions()

  const [status, setStatus] = useState<ProbationStatus>('passed')
  const [conclusion, setConclusion] = useState('')
  const [positionId, setPositionId] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [salary, setSalary] = useState('')
  const [rate, setRate] = useState('')
  const [orderNo, setOrderNo] = useState('')

  // Dialog yopilib qayta ochilganda oldingi xodimning xulosasi qolib
  // ketmasligi kerak — bu boshqa odamga tegishli matn bo'lardi.
  //
  // Lavozim va bo'lim HOZIRGI qiymat bilan to'ldiriladi: HR nima
  // turganini ko'rib, faqat o'zgartirmoqchi bo'lganini tegadi. Oklad esa
  // ataylab bo'sh — sinovdan keyin u DOIM yangi son bo'ladi va eski
  // summani ko'rsatib turish faqat adashtirardi.
  useEffect(() => {
    if (item !== null) {
      setStatus('passed')
      setConclusion('')
      setPositionId(item.positionId ?? '')
      setDepartmentId(item.departmentId ?? '')
      setSalary('')
      setRate('')
      setOrderNo('')
      resolve.reset()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const promoting = status === 'passed'

  const submit = () => {
    const trimmedConclusion = conclusion.trim()
    const trimmedOrder = orderNo.trim()
    const parsedSalary = Number(salary)
    const parsedRate = Number(rate)

    resolve.mutate(
      {
        id: item.id,
        request: {
          status,
          conclusion: trimmedConclusion === '' ? null : trimmedConclusion,

          // Tayinlash maydonlari faqat «o'tdi» bilan yuboriladi va faqat
          // HAQIQATAN o'zgargani. Server ham shu qoidani tekshiradi
          // («sinov.tayinlash_otmagan»), lekin so'rovni bu yerda toza
          // yuborish HR ga keraksiz xato ko'rsatmaslik demak.
          ...(promoting
            ? {
                positionId: changed(positionId, item.positionId),
                departmentId: changed(departmentId, item.departmentId),
                salaryAmount:
                  salary.trim() === '' || !Number.isFinite(parsedSalary) ? null : parsedSalary,
                rate: rate.trim() === '' || !Number.isFinite(parsedRate) ? null : parsedRate,
                orderNo: trimmedOrder === '' ? null : trimmedOrder,
              }
            : {}),
        },
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
          rows={3}
          value={conclusion}
          onChange={(event) => setConclusion(event.target.value)}
        />

        {/*
          Tayinlash bloki faqat «o'tdi» tanlanganda ko'rinadi. Doim
          ko'rsatib turish HR ni «ishdan ketyapti, lekin oklad qo'yaymi?»
          degan savolga majbur qilardi — server esa bunday so'rovni
          baribir rad etadi.
        */}
        {promoting && (
          <div className="space-y-4 rounded-md border border-[var(--border)] bg-[var(--bg-sunken)] p-4">
            <div>
              <p className="text-footnote font-medium">{t('probation.promoteTitle')}</p>
              <p className="mt-0.5 text-caption1 text-[var(--fg-secondary)]">
                {t('probation.promoteHint')}
              </p>
            </div>

            <Select
              label={t('employee.position')}
              options={options.positions}
              placeholder={t('probation.keepCurrent')}
              value={positionId}
              onChange={(event) => setPositionId(event.target.value)}
            />

            <Select
              label={t('employee.department')}
              options={options.departments}
              placeholder={t('probation.keepCurrent')}
              value={departmentId}
              onChange={(event) => setDepartmentId(event.target.value)}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <TextField
                label={t('employee.salary')}
                placeholder={t('probation.keepCurrent')}
                type="number"
                inputMode="numeric"
                value={salary}
                onChange={(event) => setSalary(event.target.value)}
              />

              <TextField
                label={t('probation.rate')}
                hint={t('probation.rateHint')}
                placeholder={t('probation.keepCurrent')}
                type="number"
                inputMode="decimal"
                step="0.25"
                value={rate}
                onChange={(event) => setRate(event.target.value)}
              />
            </div>

            <TextField
              label={t('employee.orderNo')}
              value={orderNo}
              onChange={(event) => setOrderNo(event.target.value)}
            />
          </div>
        )}
      </div>
    </Dialog>
  )
}

/**
 * Tanlov o'zgargan bo'lsa qiymat, aks holda `null` («o'zgarmadi»).
 *
 * Busiz forma har safar hozirgi lavozimni qaytadan yuborardi va tarixda
 * «lavozim o'zgardi» degan bo'sh hodisa paydo bo'lardi — mehnat yo'li
 * lentasi hech narsa aytmaydigan qatorlar bilan to'lardi.
 */
function changed(selected: string, current: string | null): string | null {
  if (selected === '') return null
  return selected === current ? null : selected
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
  /*
    `ProbationTarget` — ATAYLAB tor shartnoma: oyna sinov ro'yxatidan ham,
    xodim kartasidan ham ochiladi va kartada to'liq ro'yxat qatori yo'q.
    Ikkinchi nusxa yozish o'rniga oyna faqat o'zi ishlatadigan maydonlarni
    so'raydi.
  */
  item: ProbationTarget | null
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

        <DateField
          label={t('probation.newEndDate')}
          hint={
            item.endDate === null
              ? undefined
              : t('probation.currentEndDate', { date: date(item.endDate) })
          }
          value={newEndDate}
          onChange={setNewEndDate}
        />
      </div>
    </Dialog>
  )
}
