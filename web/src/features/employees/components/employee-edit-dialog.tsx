import { zodResolver } from '@hookform/resolvers/zod'
import { LoaderCircle } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useForm, type FieldPath } from 'react-hook-form'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Button,
  Checkbox,
  Dialog,
  Select,
  Skeleton,
  TextArea,
  TextField,
  type SelectOption,
} from '@/shared/ui'

import type { EmployeeDetails } from '../api/employees-api'
import { useFilterOptions, useUpdateEmployee } from '../api/employees-queries'
import { HIRE_ERROR_FIELD, employeeErrorCode } from '../lib/employee-error'
import {
  employeeSchema,
  fromTristate,
  orNull,
  pinflBirthDate,
  toEmployeeValues,
  toFullPhone,
  toPassport,
  type EmployeeValues,
} from '../lib/employee-schemas'

const FORM_ID = 'employee-edit-form'

/**
 * Sana va JSHSHIR nomuvofiqligi — JUFTLIKNING xatosi.
 *
 * Server qaysi biri noto'g'ri ekanini aytmaydi va ayta olmaydi ham: u
 * faqat ikkalasi bir-biriga mos emasligini biladi. Shu sababli xato
 * bitta maydonga emas, IKKALASIGA qo'yiladi — aks holda HR JSHSHIR
 * ostidagi qizil yozuvni ko'rib, aslida sana xato bo'lgan yozuvda
 * to'g'ri JSHSHIRni «tuzatishga» kirishardi.
 */
const PINFL_DATE_MISMATCH = 'xodim.jshshir_sana_mos_emas'

/** Massiv maydoni xato ko'rsatishda qatnashmaydi — u tanlov, matn emas. */
type ScalarField = Exclude<keyof EmployeeValues, 'directionIds'>

/**
 * Xodimning shaxsiy ma'lumotini tahrirlash.
 *
 * <b>Nega bu forma birinchi darajali.</b> Excel'dan ko'chirishda 3872
 * yozuv «tekshirish kerak» deb belgilangan: JSHSHIR yo'q yoki nazorat
 * raqami xato, pasport ustuni siljigan, telefon o'qilmagan. Ko'chirish
 * ularni to'xtatmadi, lekin tuzatish vositasi bo'lmasa, o'sha belgi
 * abadiy qolib ketardi. Saqlangach server belgini o'zi olib tashlaydi —
 * ya'ni bu forma HR uchun ro'yxatni tozalash quroli.
 *
 * Ishga qabuldan farqli o'laroq bosqichga bo'linmagan: bu yerga odam
 * bitta noto'g'ri maydonni tuzatgani keladi, uni bosqichlar ostida
 * yashirish ortiqcha ish bo'lardi.
 */
export function EmployeeEditDialog({
  open,
  employee,
  onClose,
}: {
  open: boolean
  employee: EmployeeDetails
  onClose: () => void
}) {
  const { t, date } = useLocale()
  const errorMessage = useErrorMessage()
  const options = useFilterOptions()
  const update = useUpdateEmployee(employee.id)

  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    getValues,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<EmployeeValues>({
    resolver: zodResolver(employeeSchema),
    defaultValues: toEmployeeValues(employee),
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  /*
   * Qiymatlar oyna OCHILGAN paytda kartadan qayta olinadi.
   *
   * Ikki nozik joy bor. Birinchisi: faqat `defaultValues` ga tayanib
   * bo'lmaydi — komponent sahifa bilan birga tirik qoladi va rasm
   * yuklangach yoki boshqa amaldan keyin forma eski ma'lumot bilan
   * ochilardi. Ikkinchisi: kartaning O'ZI yangilanganda (fon so'rovi)
   * forma qayta to'ldirilmasligi kerak — HR yozib turgan matn ko'z
   * oldida yo'qolib ketardi. Shu sababli faqat «yopiq → ochiq» o'tishi.
   */
  const wasOpen = useRef(false)

  useEffect(() => {
    if (open && !wasOpen.current) {
      setFailure(null)
      reset(toEmployeeValues(employee))
    }

    wasOpen.current = open
  }, [open, employee, reset])

  const directionIds = watch('directionIds')

  /*
   * JSHSHIR yozilayotgan paytda kuzatiladi: sana maydonining izohi
   * undan hosil bo'ladi. Faqat shu bitta maydonga obuna bo'linadi —
   * argumentsiz `watch()` butun formaga obuna bo'lib, har bir harf
   * uchun yigirmata maydonni qayta chizardi.
   */
  const encodedBirthDate = pinflBirthDate(watch('pinfl'))

  // `<input type="date">` ning o'z chegarasi: sanani tanlash oynasida
  // kelajak umuman ochilmasin — xatoni ko'rsatgandan ko'ra oldini olgan yaxshi
  const today = todayIso()

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)

    try {
      await update.mutateAsync({
        lastName: values.lastName.trim(),
        firstName: values.firstName.trim(),
        middleName: orNull(values.middleName),
        /*
         * Bo'sh maydon serverga `null` bo'lib ketadi va bu «sanani
         * o'chir» EMAS, «tegilmasin» degani (`SetBirthDateAndPinfl`).
         * Farqi shundaki: sanasi yo'q kartani saqlash mumkin, lekin
         * mavjud sanani forma orqali yo'qotib bo'lmaydi — tug'ilgan
         * sanani bo'shatish HR uchun ma'noli amal emas.
         */
        birthDate: orNull(values.birthDate),
        gender: values.gender === '' ? null : values.gender,
        pinfl: orNull(values.pinfl),
        passport: toPassport(values.passport),
        phone: toFullPhone(values.phone),
        secondPhone: toFullPhone(values.secondPhone),
        address: orNull(values.address),
        speciality: orNull(values.speciality),
        telegramUrl: orNull(values.telegram),
        // Rasm shu formada o'zgarmaydi. Server bo'sh kalitni e'tiborsiz
        // qoldiradi, ya'ni uni yubormaslik ham xavfsiz — baribir kartadagi
        // JORIY kalit qaytariladi, shunda so'rov kartaning to'liq holatini
        // ko'rsatadi va shartnoma o'zgarsa bu joyda tuzatadigan narsa qolmaydi
        photoKey: employee.photoKey,
        criminalRecordNote: orNull(values.criminalRecordNote),
        hasCriminalRecord: fromTristate(values.hasCriminalRecord),
        /*
         * Havola oddiy qoidada: bo'sh yuborish uni O'CHIRADI. Skrinshotdan
         * farqli — u alohida endpoint bilan boshqariladi va bu formada
         * umuman yo'q (rasm bilan bir xil bo'linish).
         */
        mehnatUzUrl: orNull(values.mehnatUzUrl),
        directionIds: values.directionIds,
      })

      onClose()
    } catch (error) {
      const code = employeeErrorCode(error)

      if (code === PINFL_DATE_MISMATCH) {
        setError('pinfl', { message: code })
        setError('birthDate', { message: code })
        return
      }

      const field = HIRE_ERROR_FIELD[code]

      /*
       * Xato kodlari jadvali ishga qabul formasi bilan UMUMIY: server
       * ikkala amalda ham bir xil kod qaytaradi. Lekin unda bu formada
       * yo'q maydonlar ham bor (oklad, stavka) — shu sababli maydon
       * haqiqatan mavjudligi tekshiriladi, aks holda xato ko'rinmaydigan
       * joyga tushib, foydalanuvchi «hech narsa bo'lmadi» deb o'ylardi.
       */
      if (field !== undefined && field in getValues()) {
        setError(field as FieldPath<EmployeeValues>, { message: code })
      } else {
        setFailure(code)
      }
    }
  })

  /** Xato KODI tarjimaga aylantiriladi (sxema kod saqlaydi, matn emas). */
  const message = (name: ScalarField): string | undefined => {
    const raw = errors[name]?.message
    return typeof raw === 'string' ? errorMessage(raw) : undefined
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!isSubmitting}
      title={t('employees.editTitle')}
      description={employee.fullName}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>

          <Button type="submit" form={FORM_ID} variant="primary" disabled={isSubmitting}>
            {isSubmitting && <LoaderCircle className="animate-spin" />}
            {isSubmitting ? t('employees.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id={FORM_ID} onSubmit={onSubmit} noValidate className="space-y-5">
        {failure !== null && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(failure)}
          </Alert>
        )}

        {/*
          Belgilangan yozuv uchun ogohlantirish EMAS, va'da: saqlash
          «tekshirish kerak» ni olib tashlaydi. HR shu sababli formani
          oxirigacha to'ldiradi — u nima uchun harakat qilayotganini biladi.
        */}
        {employee.needsReview && (
          <Alert tone="info" title={t('employees.reviewClears')}>
            {employee.reviewNote ?? undefined}
          </Alert>
        )}

        {/* ───────── Shaxs ───────── */}
        <Section title={t('employees.stepPerson')}>
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
            {/*
              Tug'ilgan sana TAHRIRLANADI — bu ataylab shunday.

              «Tekshirish kerak» deb belgilangan yozuvlarning katta
              qismida nomuvofiqlik aynan shu juftlikda: JSHSHIR ichidagi
              sana kartadagi sana bilan mos emas. Maydon qulf bo'lsa,
              HR to'g'ri JSHSHIRni kiritganda server rad javob berardi,
              sanani esa tuzata olmasdi — belgini olib tashlashning yo'li
              butunlay berk bo'lardi.

              Ikkalasi BIR so'rovda ketadi va server ularni faqat
              bir-biriga qarab baholaydi (`SetBirthDateAndPinfl`): HR
              ikkalasini birga tuzatsa o'tadi. Bittasi eski qolsa —
              `xodim.jshshir_sana_mos_emas` va xato IKKALA maydonda ham
              ko'rinadi.

              Izoh esa xatoni kutmasdan aytadi: JSHSHIR ichida qanday
              sana turganini HR 14 xonalik raqamdan o'qiy olmaydi.
            */}
            <TextField
              label={t('employee.birthDate')}
              type="date"
              max={today}
              hint={
                encodedBirthDate === null
                  ? t('employees.birthDateHint')
                  : t('employees.birthDateFromPinfl', { date: date(encodedBirthDate) })
              }
              disabled={isSubmitting}
              error={message('birthDate')}
              {...register('birthDate')}
            />
            <Select
              label={t('employees.gender')}
              placeholder={t('employees.genderUnknown')}
              hint={t('employees.genderHint')}
              options={[
                { value: 'male', label: t('employees.genderMale') },
                { value: 'female', label: t('employees.genderFemale') },
              ]}
              disabled={isSubmitting}
              error={message('gender')}
              {...register('gender')}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
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
            <TextField
              label={t('employee.passport')}
              maxLength={9}
              placeholder="AB1234567"
              disabled={isSubmitting}
              error={message('passport')}
              inputClassName="uppercase tabular"
              {...register('passport')}
            />
          </div>
        </Section>

        {/* ───────── Aloqa ───────── */}
        <Section title={t('employees.contacts')}>
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={t('employee.phone')}
              leading="+998"
              inputMode="numeric"
              maxLength={9}
              placeholder="905088888"
              disabled={isSubmitting}
              error={message('phone')}
              inputClassName="tabular"
              {...register('phone')}
            />
            <TextField
              label={t('employees.secondPhone')}
              leading="+998"
              inputMode="numeric"
              maxLength={9}
              placeholder="905088888"
              hint={t('employees.secondPhoneHint')}
              disabled={isSubmitting}
              error={message('secondPhone')}
              inputClassName="tabular"
              {...register('secondPhone')}
            />
          </div>

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
              hint={t('employees.telegramHint')}
              disabled={isSubmitting}
              error={message('telegram')}
              {...register('telegram')}
            />
          </div>
        </Section>

        {/* ───────── Tekshiruvlar va yo'nalishlar ───────── */}
        <Section title={t('employees.checks')}>
          {/*
            Sudlanganlik JAVOBI — endi tanlanadi, matndan taxmin
            qilinmaydi. «Noma'lum» va «Sudlanmagan» bir xil emas: birida
            hech kim tekshirmagan, ikkinchisida tekshirilgan va topilmagan.
            Ilgari ikkalasi ham bo'sh matn bo'lib ko'rinardi.

            Havola bilan yonma-yon: ikkalasi ham «bu odam haqida nima
            ma'lum?» degan savolning javobi va bir xil kenglikda o'qiladi.
          */}
          <div className="grid gap-4 sm:grid-cols-2">
            <Select
              label={t('employees.criminalRecord')}
              options={[
                { value: 'unknown', label: t('employees.unknown') },
                { value: 'no', label: t('employees.criminalRecordNo') },
                { value: 'yes', label: t('employees.criminalRecordYes') },
              ]}
              disabled={isSubmitting}
              error={message('hasCriminalRecord')}
              {...register('hasCriminalRecord')}
            />
            <TextField
              label={t('employees.mehnatUzLink')}
              placeholder="https://mehnat.uz/..."
              hint={t('employees.mehnatUzLinkHint')}
              disabled={isSubmitting}
              error={message('mehnatUzUrl')}
              {...register('mehnatUzUrl')}
            />
          </div>

          {/*
            Tafsilot — javobning izohi: qaysi modda, qachon.
            Skrinshot bu formada YO'Q, u rasm bilan bir xil yo'ldan
            boradi (kartadan yuklanadi).
          */}
          <TextArea
            label={t('employees.criminalRecordNote')}
            hint={t('employees.criminalRecordHint')}
            rows={2}
            disabled={isSubmitting}
            error={message('criminalRecordNote')}
            {...register('criminalRecordNote')}
          />

          <DirectionPicker
            options={options.directions}
            loading={options.isPending}
            selected={directionIds}
            disabled={isSubmitting}
            onToggle={(id) =>
              setValue(
                'directionIds',
                directionIds.includes(id)
                  ? directionIds.filter((item) => item !== id)
                  : [...directionIds, id],
                { shouldValidate: false },
              )
            }
          />
        </Section>
      </form>
    </Dialog>
  )
}

/**
 * Formaning bir bo'limi.
 *
 * Bosqich emas — bir vaqtda hammasi ko'rinadi. Sarlavha faqat ko'zni
 * yo'naltiradi: yigirmata maydon uzluksiz oqim bo'lib turganda, HR
 * qidirayotgan maydonini ko'z bilan topa olmaydi.
 */
function Section({ title, children }: { title: ReactNode; children: ReactNode }) {
  return (
    <fieldset className="space-y-4">
      <legend className="w-full border-b border-[var(--border)] pb-2 text-footnote font-semibold text-[var(--fg-secondary)]">
        {title}
      </legend>
      {children}
    </fieldset>
  )
}

/**
 * Yo'nalishlar — ko'p tanlovli.
 *
 * Bitta xodim bir vaqtda «Вело, метан, шина» bo'lishi mumkin, shu
 * sababli oddiy ro'yxat (`<select>`) to'g'ri kelmaydi. Brauzerning
 * `multiple` rejimi esa Ctrl bosib tanlashni talab qiladi va sensorli
 * ekranda umuman ishlamaydi — shu sababli oddiy belgilash kataklari.
 */
function DirectionPicker({
  options,
  loading,
  selected,
  disabled,
  onToggle,
}: {
  options: SelectOption[]
  loading: boolean
  selected: string[]
  disabled: boolean
  onToggle: (id: string) => void
}) {
  const { t } = useLocale()

  return (
    <div className="space-y-2">
      <span className="block text-footnote font-medium text-[var(--fg-secondary)]">
        {t('employee.direction')}
      </span>

      <div
        className={cn(
          'max-h-44 overflow-y-auto rounded-md border p-3.5',
          'border-[var(--border-strong)] bg-[var(--bg-sunken)]',
        )}
      >
        {loading ? (
          <div className="grid gap-2.5 sm:grid-cols-2">
            {[0, 1, 2, 3].map((index) => (
              <Skeleton key={index} className="h-5 w-full" />
            ))}
          </div>
        ) : options.length === 0 ? (
          <p className="text-footnote text-[var(--fg-tertiary)]">{t('common.empty')}</p>
        ) : (
          <div className="grid gap-2.5 sm:grid-cols-2">
            {options.map((option) => (
              <Checkbox
                key={option.value}
                checked={selected.includes(option.value)}
                onChange={() => onToggle(option.value)}
                label={option.label}
                disabled={disabled}
              />
            ))}
          </div>
        )}
      </div>

      <p className="text-footnote text-[var(--fg-tertiary)]">
        {t('employees.directionsHint')}
      </p>
    </div>
  )
}
