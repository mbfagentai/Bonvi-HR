import { LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import {
  EMPLOYEE_FLAG_TYPES,
  type EmployeeFlag,
  type EmployeeFlagType,
} from '../api/employees-api'
import { useCreateFlag, useEmployeeFlags } from '../api/employees-queries'
import { employeeErrorCode } from '../lib/employee-error'
import { FLAG_LOOK } from '../lib/employee-presentation'

/** `YYYY-MM-DD` — server `DateOnly` kutadi. */
function today(): string {
  return new Date().toISOString().slice(0, 10)
}

/** Server «bir vaqtda atigi bitta belgi» qoidasini buzganini shu kod bilan aytadi. */
const ALREADY_IN_FORCE = 'belgi.boshqa_belgi_bor'

/**
 * Belgi yangi belgi kuchga kiradigan kundan KEYIN ham kuchda qoladimi.
 *
 * Shart backend'dagi `EmployeeFlag.IsInForceAfter` bilan AYNAN bir xil
 * (`endDate > startDate`, `>=` emas): o'sha kuni tugaydigan belgi
 * yangisiga xalaqit bermaydi. Ikkalasi bir xil yozilishi shart, aks
 * holda interfeys tasdiq so'ramagan joyda server 409 qaytarardi —
 * yoki teskarisi, keraksiz ogohlantirish chiqarardi.
 */
function inForceAfter(flag: EmployeeFlag, date: string): boolean {
  return flag.startDate <= date && (flag.endDate === null || flag.endDate > date)
}

/**
 * Xodimga belgi qo'yish: rezerv, qora ro'yxat, nazorat, qobiliyatli.
 *
 * <b>Belgi aynan shu yerdan qo'yiladi</b>, ro'yxat sahifasidan emas: belgi
 * odamga tegishli va uni qo'yayotgan HR o'sha odamning kartasini ko'rib
 * turgan bo'lishi kerak.
 *
 * <b>Bir vaqtda ATIGI BITTA kuchdagi belgi.</b> Foydalanuvchining aniq
 * qarori, istisnosiz: qora ro'yxatdagi odam ayni paytda rezervda ham
 * bo'la olmaydi. Xodimda kuchdagi belgi bo'lsa, birinchi bosishda amal
 * BAJARILMAYDI — ogohlantirish chiqadi va tugma matni o'zgaradi, ya'ni
 * HR nima yo'qolishini bilib turib tasdiqlaydi. Tasdiqdan keyin eskisi
 * YOPILADI (o'chirilmaydi — tarix qoladi) va yangisi ochiladi.
 *
 * ⚠️ Bu tekshiruv — QULAYLIK. Asosiy himoya serverda
 * (`RaiseFlagCommandHandler`): agar keshdagi belgilar ro'yxati eskirgan
 * bo'lsa, server `belgi.boshqa_belgi_bor` qaytaradi va o'sha kod ham
 * shu yerda tasdiqlash holatiga aylanadi — foydalanuvchi tushunarsiz
 * xato o'rniga ma'noli savol ko'radi.
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

  /*
   * Belgilar ro'yxati kartada ALLAQACHON so'ralgan (`$employeeId.tsx`),
   * ya'ni bu chaqiruv yangi so'rov emas — TanStack Query bir xil kalitni
   * bo'lishadi. Shu sababli ogohlantirish oyna ochilishi bilan darhol
   * ko'rinadi, kutish bo'lmaydi.
   */
  const flags = useEmployeeFlags(employeeId)

  const [type, setType] = useState<EmployeeFlagType>('reserve')
  const [startDate, setStartDate] = useState(today)
  const [endDate, setEndDate] = useState('')
  const [reason, setReason] = useState('')

  /** Foydalanuvchi almashtirishga rozi bo'ldimi. */
  const [confirming, setConfirming] = useState(false)

  useEffect(() => {
    if (!open) return

    setType('reserve')
    setStartDate(today())
    setEndDate('')
    setReason('')
    setConfirming(false)
    raise.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  /*
   * Ziddiyat YANGI belgi boshlanadigan kunga qarab hisoblanadi, bugunga
   * emas: sana orqaga qo'yilganda ham savol o'sha kunga tegishli
   * (serverdagi bilan bir xil qoida).
   */
  const conflicts = useMemo(
    () => (flags.data ?? []).filter((flag) => inForceAfter(flag, startDate)),
    [flags.data, startDate],
  )

  if (!open) return null

  /*
   * Qora ro'yxat uchun sabab MAJBURIY — bu domen qoidasi, mijoz uni
   * takrorlamaydi, faqat oldindan ko'rsatadi. Sababsiz yozuvlar Excel'da
   * bor edi va keyin hech kim nima uchunligini bilmasdi.
   */
  const reasonRequired = type === 'blacklist'
  const incomplete = startDate === '' || (reasonRequired && reason.trim() === '')

  /*
   * Ziddiyat OLDINDAN ma'lum bo'lsa, tasdiqlash alohida bosqich EMAS —
   * ogohlantirish darhol ko'rinadi va tugma «Belgi qo'yish» dan
   * «Almashtirish» ga (qizil) o'zgaradi. Ya'ni HR nima bo'lishini o'qib
   * turib, boshqa nomli tugmani ataylab bosadi.
   *
   * NEGA ikki bosqichli «avval ogohlantir, keyin bajar» EMAS: ogohlantirish
   * allaqachon ekranda turganda birinchi bosish hech narsa qilmagandek
   * ko'rinardi va foydalanuvchi uni ikki marta bosaverardi.
   *
   * `confirming` esa BOSHQA holat uchun: kesh eskirib, ziddiyat faqat
   * serverdan ma'lum bo'lganda. O'shanda ham natija bir xil — tugma
   * qizarib, matni o'zgaradi.
   */
  const replacing = conflicts.length > 0 || confirming

  const submit = () => {
    raise.mutate(
      {
        type,
        startDate,
        endDate: endDate === '' ? null : endDate,
        reason: reason.trim() === '' ? null : reason.trim(),
        confirm: replacing,
      },
      {
        onSuccess: onClose,
        onError: (error) => {
          // Kesh eskirgan bo'lsa ziddiyat faqat shu yerda ma'lum bo'ladi:
          // xatoni ko'rsatish o'rniga tasdiqlash holatiga o'tamiz.
          if (employeeErrorCode(error) === ALREADY_IN_FORCE) setConfirming(true)
        },
      },
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
            variant={replacing ? 'danger' : 'primary'}
            onClick={submit}
            disabled={raise.isPending || incomplete}
          >
            {raise.isPending && <LoaderCircle className="animate-spin" />}
            {replacing ? t('employees.flagReplaceConfirm') : t('employees.raiseFlag')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {/*
          Xato ogohlantirishdan PASTDA emas, tepada — lekin ziddiyat
          kodining o'zi bu yerda ko'rsatilmaydi: u xato emas, savol.
          Uni ogohlantirish bloki aytadi.
        */}
        {raise.isError && employeeErrorCode(raise.error) !== ALREADY_IN_FORCE && (
          <Alert tone="danger">{errorMessage(employeeErrorCode(raise.error))}</Alert>
        )}

        {/*
          Kuchdagi belgi haqidagi ogohlantirish. Qaysi belgi yopilishi
          ATAYLAB nomi bilan yoziladi: «boshqa belgi bor» degan umumiy
          matn HR ga nima yo'qolayotganini aytmasdi va u tasdiqlashdan
          oldin kartani yopib, belgilar tabini ochib ko'rishi kerak
          bo'lardi.
        */}
        {replacing && (
          <Alert tone="warning" title={t('employees.flagReplaceTitle')}>
            {conflicts.length === 0
              ? t('employees.flagReplaceUnknown')
              : t('employees.flagReplaceText', {
                  flag: conflicts
                    .map((flag) => t(`employees.flags.${FLAG_LOOK[flag.type].key}`))
                    .join(', '),
                })}
          </Alert>
        )}

        <Select
          label={t('employees.flagType')}
          hint={t('employees.flagSingleHint')}
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
