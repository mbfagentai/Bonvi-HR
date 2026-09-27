import { zodResolver } from '@hookform/resolvers/zod'
import {
  Briefcase,
  Check,
  ListChecks,
  LoaderCircle,
  ReceiptText,
  UserRound,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { Controller, useForm, type FieldPath } from 'react-hook-form'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  Alert,
  Badge,
  Button,
  Checkbox,
  DateField,
  Dialog,
  MoneyInput,
  PhoneInput,
  Select,
  Skeleton,
  TextArea,
  TextField,
} from '@/shared/ui'
import type { FilterOptions } from '../api/employees-queries'
import { useHire, usePositionDefaults, usePositionDuties } from '../api/employees-queries'
import { HIRE_ERROR_FIELD, HIRE_FIELD_STEP, employeeErrorCode } from '../lib/employee-error'
import { SALARY_VIEW } from '../lib/employee-presentation'
import {
  emptyHireValues,
  hireSchema,
  orNull,
  toAmount,
  toFullPhone,
  toPassport,
  toRate,
  type HireValues,
} from '../lib/employee-schemas'

const FORM_ID = 'hire-form'

/**
 * Har bir bosqichga tegishli maydonlar.
 *
 * NEGA kerak: «Davom etish» bosilganda FAQAT shu bosqich tekshiriladi.
 * Butun forma tekshirilsa, birinchi bosqichda turgan foydalanuvchi hali
 * ko'rmagan maydonlari uchun xato oladi va nima qilishni tushunmaydi.
 */
const STEP_FIELDS: ReadonlyArray<ReadonlyArray<FieldPath<HireValues>>> = [
  [
    'lastName',
    'firstName',
    'middleName',
    'birthDate',
    'pinfl',
    'passport',
    'phone',
    'secondPhone',
    'address',
    'speciality',
    'telegram',
  ],
  ['legalEntityId', 'departmentId', 'positionId', 'hireDate', 'rate'],
  ['salaryAmount', 'orderNo', 'orderDate'],
]

/**
 * Ishga qabul — HR ning asosiy talabi.
 *
 * «шу битта бланка тўлдирилиши керак, тўлдирганда мана бу маълумот
 * ҳаммаси ўзига кириб кетиши керак» — ya'ni HR bitta forma to'ldiradi,
 * tizim esa shaxsni, mehnat munosabatini va birinchi hodisani (buyruqni)
 * bir vaqtda yaratadi. Excel'da bu sakkizta varaqqa qo'lda ko'chirilardi.
 *
 * Forma UCH BOSQICHGA bo'lingan, lekin bitta so'rov yuboradi. Bosqich —
 * vizual yengillik uchun: yigirmata maydon bitta ekranda turganda odam
 * qayerdan boshlashni bilmaydi. Ma'lumot esa bo'linmaydi — yarim
 * saqlangan xodim degan holat yo'q.
 */
export function HireDialog({
  open,
  onClose,
  options,
}: {
  open: boolean
  onClose: () => void
  options: FilterOptions
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canSeeSalary = hasPermission(SALARY_VIEW)

  const hire = useHire()

  const [step, setStep] = useState(0)
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    control,
    handleSubmit,
    reset,
    setError,
    trigger,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<HireValues>({
    resolver: zodResolver(hireSchema),
    defaultValues: emptyHireValues(),
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  // Oyna har ochilganda forma toza boshlanadi: oldingi xodimning
  // ma'lumoti yangisiga «yopishib» qolmasligi kerak
  useEffect(() => {
    if (!open) return
    setStep(0)
    setFailure(null)
    reset(emptyHireValues())
  }, [open, reset])

  const positionId = watch('positionId')
  const departmentId = watch('departmentId')

  const duties = usePositionDuties(positionId === '' ? null : positionId)
  const position = usePositionDefaults(positionId === '' ? null : positionId)

  /*
   * Lavozim tanlanganda bo'lim O'ZI to'ladi.
   *
   * HR: «лавозимни танлаганда автоматически вазифаси ҳам юкланади» —
   * bo'lim ham xuddi shu mantiqdan: ma'lumotnomada lavozim allaqachon
   * bo'limga biriktirilgan, uni qaytadan qo'lda tanlash ortiqcha ish va
   * xato manbai. Foydalanuvchi ataylab boshqa bo'lim tanlagan bo'lsa —
   * tegilmaydi (shu sababli faqat BO'SH bo'lganda ishlaydi).
   */
  useEffect(() => {
    if (positionId === '' || departmentId !== '') return

    const suggested = options.positionDepartment.get(positionId)
    if (suggested !== undefined) {
      setValue('departmentId', suggested, { shouldValidate: false })
    }
  }, [positionId, departmentId, options.positionDepartment, setValue])

  const rateValue = toRate(watch('rate'))

  const goNext = async () => {
    const fields = STEP_FIELDS[step]
    if (fields === undefined) return

    const valid = await trigger([...fields])
    if (valid) setStep((previous) => Math.min(previous + 1, STEP_FIELDS.length - 1))
  }

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)

    try {
      await hire.mutateAsync({
        employeeId: null,
        lastName: values.lastName.trim(),
        firstName: values.firstName.trim(),
        middleName: orNull(values.middleName),
        birthDate: orNull(values.birthDate),
        pinfl: orNull(values.pinfl),
        passport: toPassport(values.passport),
        phone: toFullPhone(values.phone),
        secondPhone: toFullPhone(values.secondPhone),
        address: orNull(values.address),
        speciality: orNull(values.speciality),
        telegram: orNull(values.telegram),
        directionIds: [],
        legalEntityId: values.legalEntityId,
        departmentId: orNull(values.departmentId),
        positionId: orNull(values.positionId),
        hireDate: values.hireDate,
        rate: toRate(values.rate) ?? 1,
        isPrimary: values.isPrimary,
        // ⚠️ Ruxsat yo'q bo'lsa oklad maydoni umuman ko'rsatilmagan —
        // shu sababli yuborilmaydi ham (bo'sh qiymat bilan eskisini
        // o'chirib yuborish xavfi bo'lmasin)
        salaryAmount: canSeeSalary ? toAmount(values.salaryAmount) : null,
        salaryCurrency: 'UZS',
        orderNo: orNull(values.orderNo),
        orderDate: orNull(values.orderDate),
      })

      onClose()
    } catch (error) {
      const code = employeeErrorCode(error)
      const field = HIRE_ERROR_FIELD[code]

      if (field !== undefined) {
        setError(field as FieldPath<HireValues>, { message: code })
        // Xato qaysi bosqichda — o'sha bosqichga qaytariladi, aks holda
        // foydalanuvchi qizil maydonni ko'rmaydi
        const target = HIRE_FIELD_STEP[field]
        if (target !== undefined) setStep(target)
      } else {
        setFailure(code)
      }
    }
  })

  /** Xato KODI tarjimaga aylantiriladi (sxema kod saqlaydi, matn emas). */
  const message = (name: keyof HireValues): string | undefined => {
    const raw = errors[name]?.message
    return typeof raw === 'string' ? errorMessage(raw) : undefined
  }

  const last = step === STEP_FIELDS.length - 1

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!isSubmitting}
      title={t('employees.hireTitle')}
      description={t('employees.hireSubtitle')}
      footer={
        <>
          {step > 0 && (
            <Button
              type="button"
              variant="ghost"
              disabled={isSubmitting}
              onClick={() => setStep((previous) => Math.max(0, previous - 1))}
            >
              {t('common.back')}
            </Button>
          )}

          <Button type="button" onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>

          {last ? (
            <Button
              type="submit"
              form={FORM_ID}
              variant="primary"
              disabled={isSubmitting}
            >
              {isSubmitting && <LoaderCircle className="animate-spin" />}
              {isSubmitting ? t('employees.hiring') : t('employees.hireConfirm')}
            </Button>
          ) : (
            <Button type="button" variant="primary" onClick={() => void goNext()}>
              {t('employees.nextStep')}
            </Button>
          )}
        </>
      }
    >
      <Steps current={step} onSelect={setStep} />

      <form id={FORM_ID} onSubmit={onSubmit} noValidate className="mt-5 space-y-4">
        {failure !== null && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(failure)}
          </Alert>
        )}

        {/* ───────── 1-bosqich: shaxs ───────── */}
        <fieldset className={cn('space-y-4', step === 0 ? 'block' : 'hidden')}>
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={t('employees.lastName')}
              autoComplete="off"
              disabled={isSubmitting}
              error={message('lastName')}
              {...register('lastName')}
            />
            <TextField
              label={t('employees.firstName')}
              autoComplete="off"
              disabled={isSubmitting}
              error={message('firstName')}
              {...register('firstName')}
            />
          </div>

          <TextField
            label={t('employees.middleName')}
            hint={t('employees.middleNameHint')}
            autoComplete="off"
            disabled={isSubmitting}
            error={message('middleName')}
            {...register('middleName')}
          />

          <div className="grid gap-4 sm:grid-cols-2">
            <Controller
              control={control}
              name="birthDate"
              render={({ field }) => (
                <DateField
                  label={t('employee.birthDate')}
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  name={field.name}
                  disabled={isSubmitting}
                  error={message('birthDate')}
                />
              )}
            />
            <TextField
              label={t('employee.pinfl')}
              inputMode="numeric"
              maxLength={14}
              placeholder="12345678901234"
              hint={t('employees.pinflHint')}
              disabled={isSubmitting}
              error={message('pinfl')}
              inputClassName="tabular"
              {...register('pinfl')}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={t('employee.passport')}
              maxLength={9}
              placeholder="AB1234567"
              disabled={isSubmitting}
              error={message('passport')}
              inputClassName="uppercase tabular"
              {...register('passport')}
            />
            <Controller
              control={control}
              name="phone"
              render={({ field }) => (
                <PhoneInput
                  label={t('employee.phone')}
                  disabled={isSubmitting}
                  error={message('phone')}
                  name={field.name}
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                />
              )}
            />
          </div>

          <Controller
            control={control}
            name="secondPhone"
            render={({ field }) => (
              <PhoneInput
                label={t('employees.secondPhone')}
                hint={t('employees.secondPhoneHint')}
                disabled={isSubmitting}
                error={message('secondPhone')}
                name={field.name}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
              />
            )}
          />

          <TextArea
            label={t('employee.address')}
            rows={2}
            disabled={isSubmitting}
            error={message('address')}
            {...register('address')}
          />

          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={t('employee.speciality')}
              disabled={isSubmitting}
              error={message('speciality')}
              {...register('speciality')}
            />
            <TextField
              label={t('employee.telegram')}
              placeholder="@username"
              disabled={isSubmitting}
              error={message('telegram')}
              {...register('telegram')}
            />
          </div>
        </fieldset>

        {/* ───────── 2-bosqich: ish joyi ───────── */}
        <fieldset className={cn('space-y-4', step === 1 ? 'block' : 'hidden')}>
          <Select
            label={t('employee.legalEntity')}
            placeholder={t('employees.chooseLegalEntity')}
            options={options.legalEntities}
            disabled={isSubmitting}
            error={message('legalEntityId')}
            {...register('legalEntityId')}
          />

          <Select
            label={t('employee.position')}
            placeholder={t('employees.choosePosition')}
            options={options.positions}
            disabled={isSubmitting}
            error={message('positionId')}
            {...register('positionId')}
          />

          <Select
            label={t('employee.department')}
            placeholder={t('employees.chooseDepartment')}
            hint={t('employees.departmentAutoHint')}
            options={options.departments}
            disabled={isSubmitting}
            error={message('departmentId')}
            {...register('departmentId')}
          />

          {positionId !== '' && (
            <DutiesPreview
              loading={duties.isPending}
              duties={duties.data ?? []}
              probationDays={position?.probationDays ?? null}
            />
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <Controller
              control={control}
              name="hireDate"
              render={({ field }) => (
                <DateField
                  label={t('employee.hireDate')}
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  name={field.name}
                  disabled={isSubmitting}
                  error={message('hireDate')}
                />
              )}
            />
            <TextField
              label={t('employees.rate')}
              inputMode="decimal"
              placeholder="1"
              hint={t('employees.rateHint')}
              disabled={isSubmitting}
              error={message('rate')}
              inputClassName="tabular"
              {...register('rate')}
            />
          </div>

          {rateValue !== null && rateValue < 1 && (
            <Alert tone="info">{t('employees.partTimeHint')}</Alert>
          )}

          <Checkbox
            checked={watch('isPrimary')}
            onChange={(checked) => setValue('isPrimary', checked)}
            label={t('employees.isPrimary')}
            disabled={isSubmitting}
          />
        </fieldset>

        {/* ───────── 3-bosqich: oklad va buyruq ───────── */}
        <fieldset className={cn('space-y-4', step === 2 ? 'block' : 'hidden')}>
          {/*
            ⚠️ Oklad `salary.view` ruxsatiga bog'liq: ruxsat bo'lmasa maydon
            BO'SH emas, umuman YO'Q. Bo'sh maydon «oklad kiritilmagan»
            degan yolg'on taassurot qoldiradi va HR uni to'ldirmoqchi bo'ladi.
          */}
          {canSeeSalary ? (
            /*
              Summa YOZISH paytidayoq uch xonadan ajratiladi («4 500 000»):
              nolni ortiqcha yozib yuborish HR formalarida eng ko'p uchraydigan
              xato va uni faqat ajratilgan raqamda ko'rish mumkin (`MoneyInput`).
            */
            <Controller
              control={control}
              name="salaryAmount"
              render={({ field }) => (
                <MoneyInput
                  label={t('employee.salary')}
                  placeholder="4 500 000"
                  hint={t('employees.salaryHint')}
                  disabled={isSubmitting}
                  error={message('salaryAmount')}
                  value={field.value ?? ''}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  name={field.name}
                  ref={field.ref}
                />
              )}
            />
          ) : (
            <Alert tone="info">{t('employees.salaryHidden')}</Alert>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={t('employee.orderNo')}
              placeholder="№ 91"
              disabled={isSubmitting}
              error={message('orderNo')}
              {...register('orderNo')}
            />
            <Controller
              control={control}
              name="orderDate"
              render={({ field }) => (
                <DateField
                  label={t('employee.orderDate')}
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  name={field.name}
                  disabled={isSubmitting}
                  error={message('orderDate')}
                />
              )}
            />
          </div>

          <Alert tone="info">{t('employees.probationHint')}</Alert>
        </fieldset>
      </form>
    </Dialog>
  )
}

/**
 * Bosqich ko'rsatkichi.
 *
 * O'tib bo'lingan bosqichga qaytish mumkin (bosiladi), oldinga esa faqat
 * «Davom etish» orqali — shunda tekshiruv o'tkazib yuborilmaydi.
 */
function Steps({
  current,
  onSelect,
}: {
  current: number
  onSelect: (step: number) => void
}) {
  const { t } = useLocale()

  const steps = [
    { icon: UserRound, label: t('employees.stepPerson') },
    { icon: Briefcase, label: t('employees.stepWorkplace') },
    { icon: ReceiptText, label: t('employees.stepOrder') },
  ]

  return (
    <ol className="flex items-center gap-1.5">
      {steps.map((item, index) => {
        const done = index < current
        const active = index === current

        return (
          <li key={item.label} className="flex min-w-0 flex-1 items-center gap-1.5">
            <button
              type="button"
              disabled={index > current}
              onClick={() => onSelect(index)}
              aria-current={active ? 'step' : undefined}
              className={cn(
                'press flex min-w-0 flex-1 items-center gap-2 rounded-md px-2 py-2',
                'transition-colors disabled:cursor-default',
                active && 'bg-[var(--accent-soft)]',
                !active && done && 'hover:bg-[var(--bg-hover)]',
              )}
            >
              <span
                className={cn(
                  'grid size-7 shrink-0 place-items-center rounded-full text-caption1 font-semibold',
                  done && 'bg-ios-green/14 text-ios-green',
                  active && 'bg-[var(--accent)] text-[var(--fg-on-accent)]',
                  !done && !active && 'bg-[var(--bg-sunken)] text-[var(--fg-tertiary)]',
                )}
              >
                {done ? <Check className="size-4" strokeWidth={3} /> : index + 1}
              </span>

              <span
                className={cn(
                  'truncate-1 hidden text-footnote font-medium sm:block',
                  active ? 'text-[var(--accent-on-soft)]' : 'text-[var(--fg-secondary)]',
                )}
              >
                {item.label}
              </span>
            </button>

            {index < steps.length - 1 && (
              <span aria-hidden className="h-px w-3 shrink-0 bg-[var(--border-strong)]" />
            )}
          </li>
        )
      })}
    </ol>
  )
}

/**
 * Tanlangan lavozimning vazifalari.
 *
 * HR ning aniq talabi: «лавозимни танлаганда автоматически вазифаси ҳам
 * юкланади». Bu shunchaki qulaylik emas — HR shu ro'yxatga qarab
 * shartnomaga nima tushishini ko'radi va lavozim to'g'ri tanlanganini
 * tekshiradi. Vazifasi yo'q lavozim ham ma'lumot: uni ma'lumotnomada
 * to'ldirish kerak.
 */
function DutiesPreview({
  loading,
  duties,
  probationDays,
}: {
  loading: boolean
  duties: Array<{ id: string; text: string }>
  probationDays: number | null
}) {
  const { t, num } = useLocale()

  return (
    <section className="space-y-3 rounded-lg border border-[var(--border)] bg-[var(--bg-sunken)] p-4">
      <header className="flex flex-wrap items-center gap-2.5">
        <ListChecks className="size-[18px] text-[var(--fg-secondary)]" strokeWidth={2} />
        <h3 className="text-subhead font-semibold">{t('catalog.positions.duties')}</h3>
        {duties.length > 0 && <Badge size="sm">{num(duties.length)}</Badge>}

        {probationDays !== null && (
          <Badge tone="warning" size="sm" className="ml-auto">
            {t('employees.probationDays', { n: num(probationDays) })}
          </Badge>
        )}
      </header>

      {loading && (
        <div className="space-y-2">
          {[0, 1, 2].map((index) => (
            <Skeleton key={index} className="h-9 w-full" />
          ))}
        </div>
      )}

      {!loading && duties.length === 0 && (
        <p className="py-2 text-footnote text-[var(--fg-secondary)]">
          {t('employees.noDuties')}
        </p>
      )}

      {!loading && duties.length > 0 && (
        <ol className="space-y-1.5">
          {duties.map((duty, index) => (
            <li
              key={duty.id}
              className="flex gap-2.5 rounded-md border border-[var(--border)] bg-[var(--bg-elevated)] px-3 py-2"
            >
              <span className="tabular w-5 shrink-0 text-caption1 text-[var(--fg-tertiary)]">
                {index + 1}.
              </span>
              <span className="min-w-0 flex-1 text-subhead">{duty.text}</span>
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}
