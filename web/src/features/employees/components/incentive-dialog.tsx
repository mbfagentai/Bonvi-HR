import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Badge,
  Button,
  DateField,
  Dialog,
  MoneyInput,
  PercentInput,
  SegmentedControl,
  Select,
  TextArea,
  TextField,
} from '@/shared/ui'
import { isValidPercent, parsePercent } from '@/shared/lib/percent-input'
import { todayIso } from '@/shared/lib/today'

import {
  useCreateIncentive,
  useIncentiveTypes,
  useUpdateIncentive,
} from '../api/employees-queries'
import type { Incentive, IncentiveKindValue } from '../api/incentives-api'
import { employeeErrorCode } from '../lib/employee-error'
import { INCENTIVE_KIND_LOOK } from '../lib/employee-presentation'

/** Pulli jarima qanday beriladi: qat'iy summa yoki foiz. */
type PenaltyUnit = 'amount' | 'percent'

function orNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * Rag'bat/jarima yozuvini yaratish yoki tahrirlash — BITTA komponent.
 *
 * <b>`kind` faqat YARATISHDA tanlanadi.</b> Tahrirlashda rag'bat/jarima
 * o'zgarmaydi — backend ham buni rad etadi (`UpdateIncentiveRequest`da
 * `kind` maydoni umuman yo'q): «rag'batni jarimaga aylantirish» tuzatish
 * emas, boshqa fakt — yozuv o'chirilib, yangisi kiritiladi. Shu sababli
 * tahrirlashda tanlov o'rniga sobiq qiymat rangli yorliq bilan ko'rsatiladi.
 *
 * <b>Forma turga qarab o'zgaradi.</b> Tanlangan turning `hasAmount`
 * belgisiga qarab summa maydoni ko'rsatiladi/yashiriladi (backend
 * qoidasi: `ragbat_jarima.summa_kerak` / `summa_kerak_emas`). Sovg'a
 * maydoni faqat rag'batda ko'rinadi — jarimada bo'lishi mumkin emas
 * (`ragbat_jarima.sovga_jarimada`). Jarimada sabab MAJBURIY
 * (`ragbat_jarima.sabab_bosh`).
 *
 * <b>Jarima — summa YOKI foiz, buyruqsiz</b> (egasi, 2026-10-16). Pulli
 * jarima turida «Summa | Foiz» tanlovi chiqadi: «300 000 so'm» yoki «4 %»,
 * ikkalasi birga emas (`ragbat_jarima.summa_va_foiz`). Jarima uchun buyruq
 * chiqarilmaydi — buyruq raqami va sanasi maydonlari faqat rag'batda
 * (`ragbat_jarima.buyruq_jarimada`).
 */
export function IncentiveDialog({
  employeeId,
  mode,
  item,
  onClose,
}: {
  employeeId: string
  /** `null` — oyna yopiq. */
  mode: 'create' | 'edit' | null
  /** Faqat tahrirlashda to'ldirilgan. */
  item: Incentive | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const open = mode !== null
  const isEdit = mode === 'edit' && item !== null

  const [kind, setKind] = useState<IncentiveKindValue>('award')
  const [typeId, setTypeId] = useState('')
  const [date, setDate] = useState(todayIso)
  const [amount, setAmount] = useState('')
  const [penaltyUnit, setPenaltyUnit] = useState<PenaltyUnit>('amount')
  const [percent, setPercent] = useState('')
  const [giftDescription, setGiftDescription] = useState('')
  const [reason, setReason] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')

  const types = useIncentiveTypes(kind)
  const create = useCreateIncentive(employeeId)
  const update = useUpdateIncentive(employeeId)
  const mutation = isEdit ? update : create

  useEffect(() => {
    if (!open) return

    if (isEdit && item !== null) {
      setKind(item.kind)
      setTypeId(item.typeId)
      setDate(item.date)
      setAmount(item.amount === null ? '' : String(item.amount.amount))
      setPenaltyUnit(item.percent === null ? 'amount' : 'percent')
      setPercent(item.percent === null ? '' : String(item.percent))
      setGiftDescription(item.giftDescription ?? '')
      setReason(item.reason ?? '')
      setOrderNo(item.orderNo ?? '')
      setOrderDate(item.orderDate ?? '')
    } else {
      setKind('award')
      setTypeId('')
      setDate(todayIso())
      setAmount('')
      setPenaltyUnit('amount')
      setPercent('')
      setGiftDescription('')
      setReason('')
      setOrderNo('')
      setOrderDate('')
    }

    create.reset()
    update.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  if (!open) return null

  const selectedType = (types.data ?? []).find((option) => option.id === typeId) ?? null
  const hasAmount = selectedType?.hasAmount ?? false
  const isPenalty = kind === 'penalty'
  const reasonRequired = isPenalty
  // Foiz faqat pulli jarimada; rag'bat doim summa bilan.
  const usesPercent = isPenalty && hasAmount && penaltyUnit === 'percent'

  // Rag'bat/jarima YARATISHDA gina almashtiriladi — tur ro'yxati boshqa
  // ma'lumotnomadan (award-types ⇄ penalty-types), eskisi endi ma'nosiz.
  // Jarimaga o'tilganda buyruq maydonlari ham tozalanadi: ular
  // yashiriladi va eski qiymat jimgina yuborilib qolmasligi kerak.
  const handleKindChange = (next: IncentiveKindValue) => {
    setKind(next)
    setTypeId('')
    setAmount('')
    setPenaltyUnit('amount')
    setPercent('')
    setGiftDescription('')
    setOrderNo('')
    setOrderDate('')
  }

  // Tur o'zgarganda: yangi tur summasiz bo'lsa, eski summa jimgina
  // qolib ketmasin — aks holda yashiringan maydonning eski qiymati
  // yuborilib, backend `summa_kerak_emas` bilan rad etardi.
  const handleTypeChange = (nextId: string) => {
    setTypeId(nextId)
    const nextType = (types.data ?? []).find((option) => option.id === nextId) ?? null
    if (nextType?.hasAmount !== true) {
      setAmount('')
      setPercent('')
    }
  }

  // Birlik almashganda ikkinchisining qiymati tozalanadi — server ikkalasini
  // birga qabul qilmaydi, yashirin qiymat esa ko'rinmay qolib ketardi.
  const handlePenaltyUnitChange = (next: PenaltyUnit) => {
    setPenaltyUnit(next)
    if (next === 'percent') setAmount('')
    else setPercent('')
  }

  const amountValue = amount.trim() === '' ? null : Number(amount)
  const percentValue = parsePercent(percent)
  const amountInvalid = usesPercent
    ? !isValidPercent(percentValue)
    : hasAmount && (amountValue === null || Number.isNaN(amountValue))

  // «0» yoki chala «4,» — xato faqat nimadir yozilgandan keyin ko'rinadi,
  // bo'sh maydon esa shunchaki «Saqlash»ni o'chirib turadi.
  const percentError =
    usesPercent && percent !== '' && !percent.endsWith('.') && !isValidPercent(percentValue)
      ? t('incentives.percentInvalid')
      : undefined

  const incomplete =
    typeId === '' ||
    date === '' ||
    (reasonRequired && reason.trim() === '') ||
    amountInvalid

  const submit = () => {
    if (incomplete) return

    const shared = {
      typeId,
      date,
      amount: hasAmount && !usesPercent ? amountValue : null,
      percent: usesPercent ? percentValue : null,
      giftDescription: kind === 'award' ? orNull(giftDescription) : null,
      reason: orNull(reason),
      // Jarima uchun buyruq chiqarilmaydi — server ham rad etadi.
      orderNo: isPenalty ? null : orNull(orderNo),
      orderDate: isPenalty || orderDate === '' ? null : orderDate,
    }

    if (isEdit && item !== null) {
      update.mutate({ incentiveId: item.id, body: shared }, { onSuccess: onClose })
    } else {
      create.mutate({ kind, ...shared }, { onSuccess: onClose })
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      dismissible={!mutation.isPending}
      title={isEdit ? t('incentives.editTitle') : t('incentives.newTitle')}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={mutation.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={submit}
            disabled={mutation.isPending || incomplete}
          >
            {mutation.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {mutation.isError && (
          <Alert tone="danger">{errorMessage(employeeErrorCode(mutation.error))}</Alert>
        )}

        <div className="space-y-2">
          <span className="block text-footnote font-medium text-[var(--fg-secondary)]">
            {t('incentives.kind')}
          </span>

          {isEdit ? (
            // Tahrirlashda tanlov YO'Q — faqat sobiq qiymat ko'rinadi.
            <Badge tone={INCENTIVE_KIND_LOOK[kind].tone}>{t(INCENTIVE_KIND_LOOK[kind].labelKey)}</Badge>
          ) : (
            <SegmentedControl
              value={kind}
              onChange={handleKindChange}
              aria-label={t('incentives.kind')}
              options={[
                { value: 'award', label: t(INCENTIVE_KIND_LOOK.award.labelKey) },
                { value: 'penalty', label: t(INCENTIVE_KIND_LOOK.penalty.labelKey) },
              ]}
            />
          )}
        </div>

        <Select
          label={t('incentives.type')}
          options={(types.data ?? []).map((option) => ({ value: option.id, label: option.name }))}
          placeholder={types.isPending ? t('common.loading') : t('incentives.chooseType')}
          value={typeId}
          disabled={types.isPending}
          onChange={(event) => handleTypeChange(event.target.value)}
        />

        <DateField
          label={t('incentives.date')}
          value={date}
          onChange={setDate}
        />

        {/* Summa — faqat tanlangan tur pul bilan bog'liq bo'lsa
            (`hasAmount`). Boshqa turda maydon UMUMAN chizilmaydi: uni
            ko'rsatib, keyin xato qaytarish yomon tajriba bo'lardi. */}
        {hasAmount && !isPenalty && (
          <MoneyInput label={t('incentives.amount')} value={amount} onChange={setAmount} />
        )}

        {/* Pulli jarima — summa YOKI foiz. Tanlov maydon sarlavhasining
            o'ng tomonida: «Jarima miqdori · [Summa | Foiz]». */}
        {hasAmount && isPenalty && (
          <div className="space-y-1.5">
            <div className="flex items-center justify-between gap-3">
              <span
                id="incentive-penalty-size"
                className="block text-footnote font-medium text-[var(--fg-secondary)]"
              >
                {t('incentives.penaltySize')}
              </span>
              <SegmentedControl
                size="sm"
                value={penaltyUnit}
                onChange={handlePenaltyUnitChange}
                aria-label={t('incentives.penaltySize')}
                options={[
                  { value: 'amount', label: t('incentives.unitAmount') },
                  { value: 'percent', label: t('incentives.unitPercent') },
                ]}
              />
            </div>

            {penaltyUnit === 'percent' ? (
              <PercentInput
                aria-labelledby="incentive-penalty-size"
                placeholder="0"
                value={percent}
                onChange={setPercent}
                hint={percentError === undefined ? t('incentives.percentHint') : undefined}
                error={percentError}
              />
            ) : (
              <MoneyInput
                aria-labelledby="incentive-penalty-size"
                placeholder="0"
                value={amount}
                onChange={setAmount}
              />
            )}
          </div>
        )}

        {/* Sovg'a — faqat rag'batda. Jarima yozuvida bu maydon umuman
            ko'rinmaydi: backend uni taqiqlaydi (`sovga_jarimada`). */}
        {kind === 'award' && (
          <TextField
            label={t('incentives.giftDescription')}
            value={giftDescription}
            onChange={(event) => setGiftDescription(event.target.value)}
          />
        )}

        <TextArea
          label={reasonRequired ? t('employees.reasonRequired') : t('flags.reason')}
          hint={reasonRequired ? t('incentives.penaltyReasonHint') : undefined}
          rows={3}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />

        {/* Buyruq — faqat rag'batda: jarima uchun buyruq chiqarilmaydi. */}
        {!isPenalty && (
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
        )}
      </div>
    </Dialog>
  )
}
