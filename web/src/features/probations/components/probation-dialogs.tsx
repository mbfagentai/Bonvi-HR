import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useCatalogList } from '@/features/catalog/api/catalog-queries'
import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { HrOutcomeDialog } from '@/features/employees/components/hr-outcome-dialog'
import {
  employeeErrorCode,
  processGateBlock,
  terminationWarning,
} from '@/features/employees/lib/employee-error'
import { warningsToShow, type HrWarning } from '@/features/employees/lib/hr-warnings'
import { ProcessGateBlockAlert } from '@/features/processes/components/process-gate-block'
import {
  EMPLOYMENT_SALARY,
  EMPLOYMENT_TERMINATE,
  EMPLOYMENT_TRANSFER,
} from '@/features/employees/lib/employee-presentation'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
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

import {
  LEAVING_RESULTS,
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
 * <b>«O'tmadi» / «o'zi ketdi» — shu oynaning o'zida ishdan bo'shatish</b>
 * (2026-10-14, egasining qarori). Ilgari natija yozilardi, xodim esa
 * «sinovda» holatida qolib ketardi va bo'shatishni HR alohida eslashi kerak
 * edi. Endi ketish sanasi, sababi va buyruq shu yerda so'raladi va server
 * ikkalasini bitta tranzaksiyada bajaradi. Qora ro'yxat / nazorat
 * ogohlantirishi oddiy bo'shatishdagi kabi: avval o'qiladi, keyin tasdiqlanadi.
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
  const { hasPermission } = useAuth()

  /*
    Oklad maydoni — PUL QOIDASI bo'yicha (`EMPLOYMENT_SALARY` izohi):
    sinovni yakunlash huquqi bor, oklad huquqi yo'q odamga maydon umuman
    chizilmaydi va so'rovga summa tushmaydi. Aks holda server butun
    yakunlashni 403 bilan rad etardi.
  */
  const canSetSalary = hasPermission(EMPLOYMENT_SALARY)

  /*
    Lavozim va bo'lim — KO'CHIRISH qoidasi bo'yicha, okladdagi bilan aynan
    bir xil naqsh: sinov yakunidagi yangi lavozim yoki bo'lim — bu
    ko'chirish va server uni `employment.transfer` siz butun yakunlash
    bilan birga 403 (`auth.ruxsat_yoq`) bilan rad etadi. Ruxsatsiz odamga
    ikkala maydon umuman chizilmaydi va so'rovga tushmaydi — xodim hozirgi
    joyida qoladi.

    Stavka bu qoidaga KIRMAYDI: u shartnoma sharti, alohida amali yo'q va
    sinovni yakunlash huquqi bilan keladi (ishga qabuldagi kabi).
  */
  const canTransfer = hasPermission(EMPLOYMENT_TRANSFER)

  /*
    «O'tmadi» / «o'zi ketdi» — bu ishdan bo'shatish va server uni
    `employment.terminate` siz rad etadi. Ruxsatsiz odamga bu natijalar
    taklif qilinmaydi, sababi esa aytiladi.
  */
  const canTerminate = hasPermission(EMPLOYMENT_TERMINATE)
  const reasons = useCatalogList('termination-reasons', true)

  const [status, setStatus] = useState<ProbationStatus>('passed')
  const [conclusion, setConclusion] = useState('')
  const [positionId, setPositionId] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [salary, setSalary] = useState('')
  const [rate, setRate] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')
  const [terminationDate, setTerminationDate] = useState(todayIso)
  /** The flags HR has confirmed in this dialog; `null` until then. A retry keeps the confirmation. */
  const [confirmed, setConfirmed] = useState<readonly string[] | null>(null)
  const [outcome, setOutcome] = useState<HrWarning[] | null>(null)
  const [reasonId, setReasonId] = useState('')

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
      setOrderDate('')
      setTerminationDate(todayIso())
      setReasonId('')
      setConfirmed(null)
      setOutcome(null)
      resolve.reset()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  if (outcome !== null) {
    return (
      <HrOutcomeDialog
        title={t('probation.resolveTitle')}
        description={item.employeeName}
        done={t('probation.terminatedDone')}
        warnings={outcome}
        onClose={onClose}
      />
    )
  }

  const promoting = status === 'passed'
  const leaving = LEAVING_RESULTS.includes(status)
  const warning = leaving ? terminationWarning(resolve.error) : null
  const block = leaving ? processGateBlock(resolve.error) : null

  const results = PROBATION_RESULTS.filter((value) => canTerminate || !LEAVING_RESULTS.includes(value))

  const submit = () => {
    const acknowledged = warning === null ? confirmed : [warning.code, ...warning.extra]
    setConfirmed(acknowledged)

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
                positionId: canTransfer ? changed(positionId, item.positionId) : null,
                departmentId: canTransfer ? changed(departmentId, item.departmentId) : null,
                salaryAmount:
                  !canSetSalary || salary.trim() === '' || !Number.isFinite(parsedSalary)
                    ? null
                    : parsedSalary,
                rate: rate.trim() === '' || !Number.isFinite(parsedRate) ? null : parsedRate,
                orderNo: trimmedOrder === '' ? null : trimmedOrder,
              }
            : {}),

          // Bo'shatish — faqat ketish natijasi bilan. Izoh ATAYLAB yo'q:
          // server bo'shatish izohiga sinov xulosasini qo'yadi, ikkinchi
          // matn maydoni HR ni bir narsani ikki marta yozishga majbur qilardi.
          ...(leaving
            ? {
                termination: {
                  terminationDate,
                  reasonId: reasonId === '' ? null : reasonId,
                  orderNo: trimmedOrder === '' ? null : trimmedOrder,
                  orderDate: orderDate === '' ? null : orderDate,
                  note: null,
                },
                confirm: acknowledged !== null,
              }
            : {}),
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
          <Button
            variant={leaving ? 'danger' : 'primary'}
            onClick={submit}
            disabled={resolve.isPending || (leaving && terminationDate === '')}
          >
            {resolve.isPending && <LoaderCircle className="animate-spin" />}
            {!leaving
              ? t('probation.resolve')
              : warning === null
                ? t('probation.resolveAndTerminate')
                : t('employees.terminateAnyway')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {resolve.isError &&
          (block !== null ? (
            <ProcessGateBlockAlert
              block={block}
              cases={{ employee: item.employeeId, process: 'dismissal' }}
            />
          ) : warning === null ? (
            <Alert tone="danger">{errorMessage(employeeErrorCode(resolve.error))}</Alert>
          ) : (
            <Alert tone="warning" title={errorMessage(warning.code)}>
              <ul className="space-y-1">
                {warning.extra.map((code) => (
                  <li key={code}>{errorMessage(code)}</li>
                ))}
                {warning.detail !== null && <li>{warning.detail}</li>}
              </ul>
            </Alert>
          ))}

        <Select
          label={t('probation.result')}
          hint={canTerminate ? undefined : t('probation.terminateLockedHint')}
          options={results.map((value) => ({
            value,
            label: t(`probation.status.${value}`),
          }))}
          value={status}
          onChange={(event) => {
            // Boshqa natijaga o'tilganda eski ogohlantirish (qora ro'yxat)
            // qolib, yangi natijaga tegishlidek ko'rinmasligi kerak.
            resolve.reset()
            setStatus(event.target.value as ProbationStatus)
          }}
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

            {/*
              Ruxsatsiz foydalanuvchida maydonlar yashiriladi — lekin SABABI
              aytiladi: aks holda HR «lavozimni qayerdan tanlayman?» deb
              qidirib qolardi (tekshiruv, 2026-09-27).
            */}
            {!canTransfer && (
              <p className="text-caption1 text-[var(--fg-tertiary)]">{t('probation.transferLockedHint')}</p>
            )}

            {canTransfer && (
              <>
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
              </>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              {canSetSalary && (
                <MoneyInput
                  label={t('employee.salary')}
                  placeholder={t('probation.keepCurrent')}
                  value={salary}
                  onChange={setSalary}
                />
              )}

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

        {/*
          Bo'shatish bloki — faqat «o'tmadi» / «o'zi ketdi». Maydonlar oddiy
          bo'shatish oynasidagining o'zi: sana, sabab, buyruq.
        */}
        {leaving && (
          <div className="space-y-4 rounded-md border border-[var(--border)] bg-[var(--bg-sunken)] p-4">
            <div>
              <p className="text-footnote font-medium">{t('probation.terminateTitle')}</p>
              <p className="mt-0.5 text-caption1 text-[var(--fg-secondary)]">
                {t('probation.terminateHint')}
              </p>
            </div>

            <DateField
              label={t('employees.terminationDate')}
              value={terminationDate}
              onChange={setTerminationDate}
            />

            <Select
              label={t('employees.terminationReason')}
              options={(reasons.data ?? []).map((reason) => ({ value: reason.id, label: reason.name }))}
              placeholder={t('employees.chooseReason')}
              value={reasonId}
              onChange={(event) => setReasonId(event.target.value)}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <TextField
                label={t('employee.orderNo')}
                value={orderNo}
                onChange={(event) => setOrderNo(event.target.value)}
              />
              <DateField
                label={t('employee.orderDate')}
                value={orderDate}
                onChange={setOrderDate}
              />
            </div>
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
