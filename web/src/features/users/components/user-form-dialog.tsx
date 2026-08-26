import { zodResolver } from '@hookform/resolvers/zod'
import { LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Checkbox, Dialog, TextField } from '@/shared/ui'

import type { UserSummary } from '../api/users-api'
import { useSaveUser } from '../api/users-queries'
import { USER_ERROR_FIELD, userErrorCode } from '../lib/user-error'
import { ADMIN_ROLE_CODE, ROLE_CODES, type RoleCode } from '../lib/user-presentation'
import {
  passwordFieldSchema,
  toFullPhone,
  toTextOrNull,
  toUserFormValues,
  userFormSchema,
} from '../lib/user-schemas'

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
 * bir xil naqsh): farq faqat sarlavhada, parol maydonida va `item`
 * borligida. Parol BU YERDA faqat yaratishda ko'rinadi — tahrirlashda
 * uni o'zgartirish uchun alohida «parolni tiklash» amali bor
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

  /** Maydonga bog'lanmagan server xatosi. */
  const [failure, setFailure] = useState<string | null>(null)

  const schema = useMemo(() => (isCreate ? createSchema : editSchema), [isCreate])

  const {
    register,
    handleSubmit,
    reset,
    setError,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<CreateValues>({
    resolver: zodResolver(schema),
    defaultValues: { ...toUserFormValues(item), password: '' },
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  useEffect(() => {
    if (!open) return
    setFailure(null)
    reset({ ...toUserFormValues(item), password: '' })
    save.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item])

  const roleCodes = watch('roleCodes')

  const toggleRole = (code: RoleCode) => {
    // O'zining admin rolini o'zi olib tashlay olmaydi — backend ham shu
    // amalni 403 bilan rad etadi (`foydalanuvchi.ozini_adminlikdan_
    // chetlashtirish`). Katakcha shu yerda bosilmaydigan qilib qo'yiladi,
    // shu sababli bu tekshiruv ham amalda hech qachon ishga tushmaydi —
    // lekin himoya ikki qatlamli bo'lsin.
    if (isSelf && code === ADMIN_ROLE_CODE && roleCodes.includes(code)) return

    setValue(
      'roleCodes',
      roleCodes.includes(code) ? roleCodes.filter((c) => c !== code) : [...roleCodes, code],
      { shouldValidate: false },
    )
  }

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)

    const body = {
      login: toFullPhone(values.phone),
      lastName: values.lastName.trim(),
      firstName: values.firstName.trim(),
      middleName: toTextOrNull(values.middleName),
      roleCodes: values.roleCodes,
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

  const rolesError =
    typeof errors.roleCodes?.message === 'string' ? errorMessage(errors.roleCodes.message) : undefined

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
          <Button type="submit" form={FORM_ID} variant="primary" disabled={isSubmitting}>
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

        <TextField
          label={t('employee.phone')}
          leading="+998"
          inputMode="numeric"
          maxLength={9}
          placeholder="905088888"
          autoComplete="off"
          disabled={isSubmitting}
          error={message('phone')}
          inputClassName="tabular"
          {...register('phone')}
        />

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

        {/* ───────── Rollar ───────── */}
        <div className="space-y-2">
          <span className="block text-footnote font-medium text-[var(--fg-secondary)]">
            {t('users.roles')}
          </span>

          {/*
            NEGA `flex flex-col` va `space-y-*` emas: `Checkbox` o'zi
            `inline-flex` bilan chiziladi (matn ichida ishlatilishi mumkin
            bo'lsin deb), shu sababli `space-y-*` faqat vertikal margin
            qo'shadi — inline elementlar konteyner kengligiga sig'sa,
            baribir bir qatorda yonma-yon qolib, orasidagi bo'sh joy
            yo'qoladi. `flex-col` har birini alohida qatorga majburlaydi.
          */}
          <div className="flex flex-col items-start gap-2.5 rounded-md border border-[var(--border-strong)] bg-[var(--bg-sunken)] p-3.5">
            {ROLE_CODES.map((code) => {
              const lockedForSelf = isSelf && code === ADMIN_ROLE_CODE && roleCodes.includes(code)

              return (
                <Checkbox
                  key={code}
                  checked={roleCodes.includes(code)}
                  onChange={() => toggleRole(code)}
                  label={t(`roles.${code}`)}
                  disabled={isSubmitting || lockedForSelf}
                />
              )
            })}
          </div>

          <p className="text-footnote text-[var(--fg-tertiary)]">
            {isSelf ? errorMessage('foydalanuvchi.ozini_adminlikdan_chetlashtirish') : t('users.rolesHint')}
          </p>

          {rolesError !== undefined && (
            <p role="alert" className="text-footnote text-ios-red">
              {rolesError}
            </p>
          )}
        </div>
      </form>
    </Dialog>
  )
}
