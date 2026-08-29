import { LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import type { EmployeeFlag, EmployeeFlagType } from '../api/employees-api'
import { useCreateFlag, useEmployeeFlags } from '../api/employees-queries'
import { employeeErrorCode } from '../lib/employee-error'
import {
  FLAG_LOOK,
  isRoutedMark,
  stageChoices,
  type EmployeeStage,
  type RoutedMark,
} from '../lib/employee-presentation'

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
 * Xodimga belgi qo'yish.
 *
 * <b>Belgi aynan shu yerdan qo'yiladi</b>, ro'yxat sahifasidan emas: belgi
 * odamga tegishli va uni qo'yayotgan HR o'sha odamning kartasini ko'rib
 * turgan bo'lishi kerak.
 *
 * <b>Tanlov BOSQICHGA qarab o'zgaradi.</b> Ishlab turgan xodimga —
 * «nazoratda», «qobiliyatli» va baxtsiz hodisa; ishdan ketganiga —
 * «rezerv», «qora ro'yxat» va qayta aloqa. Sabab
 * `employee-presentation.ts` dagi `FLAG_STAGE` izohida; qoida serverda ham
 * bor (`belgi.xodim_ishlayapti` / `belgi.xodim_ketgan`) va u TAQIQ, ya'ni
 * tasdiq bilan ochilmaydi — shu sababli mumkin bo'lmagan tur ro'yxatda
 * umuman ko'rsatilmaydi. Bo'lmagan tanlovni chiqarib, keyin xato berish
 * HR ni bekorga urintirardi.
 *
 * <b>Bir vaqtda ATIGI BITTA kuchdagi belgi.</b> Xodimda kuchdagi belgi
 * bo'lsa, ogohlantirish darhol ko'rinadi va tugma matni «Almashtirish» ga
 * (qizil) o'zgaradi — ya'ni HR nima yo'qolishini bilib turib tasdiqlaydi.
 * Tasdiqdan keyin eskisi YOPILADI (o'chirilmaydi — tarix qoladi).
 *
 * ⚠️ Ikkala tekshiruv ham QULAYLIK. Asosiy himoya serverda
 * (`RaiseFlagCommandHandler`).
 */
export function RaiseFlagDialog({
  employeeId,
  employeeName,
  stage,
  open,
  onClose,
  onRoute,
}: {
  employeeId: string
  employeeName: string
  /** Xodim hozir ishlayaptimi — tanlov shunga qarab tuziladi. */
  stage: EmployeeStage
  open: boolean
  onClose: () => void
  /** Havolali yozuv tanlanganda — o'z formasini ochish. */
  onRoute: (mark: RoutedMark) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const raise = useCreateFlag(employeeId)

  /*
   * Ro'yxat RUXSATGA ham bog'liq (`stageChoices` izohi), shu sababli
   * `useMemo`: har render'da yangi massiv `useEffect` ni bekorga
   * qo'zg'atardi.
   */
  const choices = useMemo(() => stageChoices(stage, hasPermission), [stage, hasPermission])
  const firstChoice = choices[0] as EmployeeFlagType

  /*
   * Belgilar ro'yxati kartada ALLAQACHON so'ralgan (`$employeeId.tsx`),
   * ya'ni bu chaqiruv yangi so'rov emas — TanStack Query bir xil kalitni
   * bo'lishadi. Shu sababli ogohlantirish oyna ochilishi bilan darhol
   * ko'rinadi, kutish bo'lmaydi.
   */
  const flags = useEmployeeFlags(employeeId)

  const [choice, setChoice] = useState<EmployeeFlagType | RoutedMark>(firstChoice)
  const [startDate, setStartDate] = useState(today)
  const [endDate, setEndDate] = useState('')
  const [reason, setReason] = useState('')

  /** Foydalanuvchi almashtirishga rozi bo'ldimi. */
  const [confirming, setConfirming] = useState(false)

  useEffect(() => {
    if (!open) return

    setChoice(firstChoice)
    setStartDate(today())
    setEndDate('')
    setReason('')
    setConfirming(false)
    raise.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, stage])

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

  const routed = isRoutedMark(choice)

  /*
   * Qora ro'yxat uchun sabab MAJBURIY — bu domen qoidasi, mijoz uni
   * takrorlamaydi, faqat oldindan ko'rsatadi. Sababsiz yozuvlar Excel'da
   * bor edi va keyin hech kim nima uchunligini bilmasdi.
   */
  const reasonRequired = choice === 'blacklist'
  const incomplete = !routed && (startDate === '' || (reasonRequired && reason.trim() === ''))

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
   *
   * Havolali yozuvda ziddiyat UMUMAN yo'q: hodisa va suhbat belgi emas,
   * ular kuchdagi belgini almashtirmaydi.
   */
  const replacing = !routed && (conflicts.length > 0 || confirming)

  const submit = () => {
    if (routed) {
      onRoute(choice)
      return
    }

    raise.mutate(
      {
        type: choice,
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

  /*
   * Kalit `t()` chaqiruvidan TASHQARIDA tanlanadi: `check-locales.mjs`
   * chaqiruv ichidagi HAR BIR satrni kalit deb o'qiydi va ternarning
   * shart qismidagi `'employed'` ni «yo'q kalit» deb hisoblardi.
   */
  const stageHintKey =
    stage === 'employed' ? 'employees.flagStageEmployed' : 'employees.flagStageDeparted'

  const confirmLabel = routed
    ? t('employees.markRouteConfirm')
    : replacing
      ? t('employees.flagReplaceConfirm')
      : t('employees.raiseFlag')

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
            {confirmLabel}
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
          hint={t(stageHintKey)}
          options={choices.map((value) => ({
            value,
            label: isRoutedMark(value)
              ? t(`employees.marks.${value}`)
              : t(`employees.flags.${FLAG_LOOK[value].key}`),
          }))}
          value={choice}
          onChange={(event) => setChoice(event.target.value as EmployeeFlagType | RoutedMark)}
        />

        {/*
          Havolali yozuv tanlangan: sana va sabab maydonlari CHIZILMAYDI.
          Ular bu yerda to'ldirilsa, keyingi formada qaytadan so'ralardi va
          HR bir xil ma'lumotni ikki marta kiritardi.
        */}
        {routed ? (
          <Alert tone="info" title={t(`employees.marks.${choice}`)}>
            {t(`employees.markRouteText.${choice}`)}
          </Alert>
        ) : (
          <>
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
          </>
        )}
      </div>
    </Dialog>
  )
}
