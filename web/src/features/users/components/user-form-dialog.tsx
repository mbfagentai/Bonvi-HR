import { zodResolver } from '@hookform/resolvers/zod'
import { LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextField, type SelectOption } from '@/shared/ui'

import type { UserSummary } from '../api/users-api'
import { useSaveUser } from '../api/users-queries'
import { USER_ERROR_FIELD, userErrorCode } from '../lib/user-error'
import {
  ROLE_CODES,
  SINGLE_HOLDER_ROLE_CODES,
  canActOnRole,
  currentRoleCode,
} from '../lib/user-presentation'
import {
  passwordFieldSchema,
  toFullPhone,
  toTextOrNull,
  toUserFormValues,
  userFormSchema,
} from '../lib/user-schemas'
import { UserPhotoEditor } from './user-photo-editor'

const FORM_ID = 'user-form'

/**
 * `password` maydoni bo'lgan to'liq shakl — tahrirlashda ham TIP darajasida
 * mavjud (bo'sh qatorda), faqat forma UNI chizmaydi va yubormaydi.
 *
 * NEGA ikkita sxema emas: `zodResolver(isCreate ? A : B)` bilan tanlansa,
 * ikkala sxemaning chiqish tipi BIR XIL bo'lishi kerak — aks holda
 * `useForm<T>()` ning yagona `T` si bilan mos kelmaydi. Shu sababli
 * tahrirlash sxemasi ham xuddi shu maydonni oladi, faqat tekshiruvsiz
 * (`z.string()` — har qanday qiymatni, hatto bo'sh qatorni ham qabul
 * qiladi).
 */
const createSchema = userFormSchema.extend({ password: passwordFieldSchema })
const editSchema = userFormSchema.extend({ password: z.string() })
type CreateValues = z.infer<typeof createSchema>

/**
 * Foydalanuvchi yaratish va tahrirlash — bitta oyna.
 *
 * Bitta forma ikkala holat uchun (`catalog-form-dialog.tsx` dagi bilan
 * bir xil naqsh): farq faqat sarlavhada, parol maydonida, rasm blokida va
 * `item` borligida. Parol BU YERDA faqat yaratishda ko'rinadi —
 * tahrirlashda uni o'zgartirish uchun alohida «parolni tiklash» amali bor
 * (`reset-password-dialog.tsx`), chunki backend ham shunday ajratgan.
 */
export function UserFormDialog({
  open,
  onClose,
  item,
}: {
  open: boolean
  onClose: () => void
  /** `null` — yangi hisob. */
  item: UserSummary | null
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const { user: currentUser } = useAuth()
  const save = useSaveUser()

  const isCreate = item === null
  const isSelf = item !== null && currentUser !== null && currentUser.id === item.id

  /** So'rovchining o'z roli — «kim kimga tega oladi» qoidasining birinchi yarmi. */
  const myRole = currentRoleCode(currentUser?.roles)

  /**
   * Nishonga umuman tega oladimi.
   *
   * Yangi hisob uchun har doim `true`: nishon hali yo'q, chegara esa
   * beriladigan ROL bo'yicha qo'yiladi (pastdagi `roleOptions`).
   */
  const canTouch = isCreate || canActOnRole(myRole, item.roleCode)

  /** Maydonga bog'lanmagan server xatosi. */
  const [failure, setFailure] = useState<string | null>(null)

  const schema = useMemo(() => (isCreate ? createSchema : editSchema), [isCreate])

  const {
    register,
    handleSubmit,
    reset,
    setError,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<CreateValues>({
    resolver: zodResolver(schema),
    defaultValues: { ...toUserFormValues(item), password: '' },
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  /*
    Bog'liqlikda `item` emas, `item?.id`.

    NEGA: `item` — ro'yxatdagi qatorning JORIY nusxasi va u ro'yxat har
    yangilanganda (masalan rasm yuklangach) yangi OBYEKT bo'lib keladi.
    Butun obyektga bog'lansa, forma o'sha lahzada qayta o'rnatilardi va
    kiritilayotgan ism yo'qolardi. Identifikator esa faqat BOSHQA hisob
    ochilganda o'zgaradi — aynan shunda forma qayta to'lishi kerak.
  */
  useEffect(() => {
    if (!open) return
    setFailure(null)
    reset({ ...toUserFormValues(item), password: '' })
    save.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item?.id])

  /**
   * Rol ro'yxati.
   *
   * Bera OLMAYDIGAN rollar ro'yxatdan olib tashlanmaydi, `disabled`
   * qilinadi: modelda uchta rol borligi va bu hisob qaysi darajada turgani
   * ko'rinib tursin. Ro'yxatdan yo'qolgan qator «bunday rol yo'q» degan
   * noto'g'ri taassurot qoldirardi.
   *
   * Hisobning O'Z joriy roli har doim yoqiq qoladi: aks holda `<select>`
   * o'zi turgan qiymatni ko'rsata olmay, birinchi qatorga sakrab ketardi
   * va forma yuborilganda rol jimgina o'zgarib qolardi.
   */
  const roleOptions = useMemo<SelectOption[]>(
    () =>
      ROLE_CODES.map((code) => ({
        value: code,
        label: t(`roles.${code}`),
        disabled: !canActOnRole(myRole, code) && code !== item?.roleCode,
      })),
    [t, myRole, item],
  )

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)

    const body = {
      login: toFullPhone(values.phone),
      lastName: values.lastName.trim(),
      firstName: values.firstName.trim(),
      middleName: toTextOrNull(values.middleName),
      roleCode: values.roleCode,
    }

    try {
      if (item === null) {
        await save.mutateAsync({ id: null, body: { ...body, password: values.password } })
      } else {
        await save.mutateAsync({ id: item.id, body })
      }
      onClose()
    } catch (error) {
      const code = userErrorCode(error)
      const field = USER_ERROR_FIELD[code]

      if (field !== undefined && field in values) {
        setError(field as keyof CreateValues, { message: code })
      } else {
        setFailure(code)
      }
    }
  })

  const message = (name: keyof CreateValues): string | undefined => {
    const raw = errors[name]?.message
    return typeof raw === 'string' ? errorMessage(raw) : undefined
  }

  /**
   * Rol maydonining izohi — nima cheklanganini AYNAN aytadi.
   *
   * Uch xil holat, uch xil sabab:
   *   • o'z hisobi — o'z rolini hech kim o'zgartira olmaydi;
   *   • nishonga huquqi yo'q — bosh rahbar super administratorga tegolmaydi;
   *   • qolgan hamma holat — «atigi bitta» qoidasi haqida ogohlantirish.
   */
  const roleHint = isSelf
    ? errorMessage('foydalanuvchi.oz_rolini_ozgartirish')
    : !canTouch
      ? errorMessage('foydalanuvchi.rolga_haqqi_yoq')
      : t('users.roleHint')

  return (
    <Dialog
      open={open}
      onClose={onClose}
      dismissible={!isSubmitting}
      title={isCreate ? t('users.createTitle') : t('users.editTitle')}
      description={item === null ? undefined : item.fullName}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            variant="primary"
            disabled={isSubmitting || !canTouch}
          >
            {isSubmitting && <LoaderCircle className="animate-spin" />}
            {isSubmitting ? t('users.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id={FORM_ID} onSubmit={onSubmit} noValidate className="space-y-4">
        {failure !== null && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(failure)}
          </Alert>
        )}

        {/*
          Rasm faqat TAHRIRLASHDA: yangi hisobda `id` hali yo'q, rasm esa
          unga biriktiriladi (`POST /api/users/{id}/photo`). Bu blok formadan
          MUSTAQIL — u «Saqlash» ni kutmaydi va o'zi darhol yuklaydi, chunki
          rasm alohida endpoint bilan boshqariladi.
        */}
        {item !== null && (
          <UserPhotoEditor
            userId={item.id}
            photoKey={item.photoKey}
            name={item.fullName}
            canEdit={canTouch}
            disabled={isSubmitting}
          />
        )}

        <TextField
          label={t('employee.phone')}
          leading="+998"
          inputMode="numeric"
          maxLength={9}
          placeholder="905088888"
          autoComplete="off"
          disabled={isSubmitting || !canTouch}
          error={message('phone')}
          inputClassName="tabular"
          {...register('phone')}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('employees.lastName')}
            autoComplete="off"
            disabled={isSubmitting || !canTouch}
            error={message('lastName')}
            {...register('lastName')}
          />
          <TextField
            label={t('employees.firstName')}
            autoComplete="off"
            disabled={isSubmitting || !canTouch}
            error={message('firstName')}
            {...register('firstName')}
          />
        </div>

        <TextField
          label={t('employees.middleName')}
          hint={t('employees.middleNameHint')}
          autoComplete="off"
          disabled={isSubmitting || !canTouch}
          error={message('middleName')}
          {...register('middleName')}
        />

        {isCreate && (
          <TextField
            label={t('users.password')}
            hint={t('users.passwordHint')}
            type="password"
            autoComplete="new-password"
            disabled={isSubmitting}
            error={message('password')}
            {...register('password')}
          />
        )}

        {/*
          ───────── Rol ─────────
          Katakchalar emas, YAGONA tanlov: hisobda aynan bitta rol bo'ladi
          (`User.SetRole`). Uchta katakchada esa «ikkitasini ham belgilash»
          texnik jihatdan mumkin ko'rinardi va foydalanuvchi faqat saqlagach
          bunday holat yo'qligini bilardi.

          O'z hisobida maydon o'chirilgan: `User.ChangeRole` buni 403 bilan
          baribir to'xtatadi, lekin tanlatib turib keyin rad etish yomon
          tajriba.
        */}
        <Select
          label={t('users.role')}
          hint={roleHint}
          options={roleOptions}
          /*
            Bo'sh qator DOIM bor: rol berilmagan (eski) yozuvda `<select>`
            hech qaysi qatorga mos kelmay, JIMGINA birinchisini ko'rsatib
            qo'yardi — va saqlaganda rol o'zi o'zgarib ketardi.
          */
          placeholder={t('users.rolePlaceholder')}
          disabled={isSubmitting || isSelf || !canTouch}
          error={message('roleCode')}
          {...register('roleCode')}
        />

        {/*
          «Atigi bitta» ogohlantirishi faqat KERAK bo'lganda chiqadi: HR
          menejer tanlanganda u shunchaki shovqin bo'lardi. Rad etish qarori
          baribir serverda qabul qilinadi — bu yerdagi matn kutilmagan 409
          ni oldindan tushuntiradi.
        */}
        {SINGLE_HOLDER_ROLE_CODES.includes(watch('roleCode')) && (
          <p className="text-footnote text-[var(--fg-tertiary)]">{t('users.singleHolderHint')}</p>
        )}
      </form>
    </Dialog>
  )
}
