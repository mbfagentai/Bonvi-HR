import { zodResolver } from '@hookform/resolvers/zod'
import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextField } from '@/shared/ui'

import type { UserSummary } from '../api/users-api'
import { useResetUserPassword } from '../api/users-queries'
import { userErrorCode } from '../lib/user-error'
import { createPasswordSchema, type CreatePasswordValues } from '../lib/user-schemas'

const FORM_ID = 'reset-password-form'

/**
 * Parolni tiklash — xavfli amal, bexosdan bosilmasin.
 *
 * <b>Nega ikki bosqich (matn + forma), oddiy tasdiqlash emas.</b>
 * `DeactivateUserDialog` dan farqi: bu yerda YANGI QIYMAT kerak (parolning
 * o'zi), shunchaki «ha/yo'q» yetarli emas. Ogohlantirish matni baribir
 * yuqorida turadi: bu amal foydalanuvchining BARCHA joriy sessiyalarini
 * bekor qiladi — backend hujjatida ham shu ta'kidlangan
 * (`ResetUserPasswordCommandHandler`).
 */
export function ResetPasswordDialog({
  item,
  onClose,
}: {
  item: UserSummary | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const resetPassword = useResetUserPassword()

  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreatePasswordValues>({
    resolver: zodResolver(createPasswordSchema),
    defaultValues: { password: '' },
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  useEffect(() => {
    if (item === null) return
    setFailure(null)
    reset({ password: '' })
    resetPassword.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)

    try {
      await resetPassword.mutateAsync({ id: item.id, newPassword: values.password })
      onClose()
    } catch (error) {
      setFailure(userErrorCode(error))
    }
  })

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!isSubmitting}
      title={t('users.resetPasswordTitle')}
      description={item.fullName}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form={FORM_ID} variant="danger" disabled={isSubmitting}>
            {isSubmitting && <LoaderCircle className="animate-spin" />}
            {isSubmitting ? t('users.resettingPassword') : t('users.resetPassword')}
          </Button>
        </>
      }
    >
      <form id={FORM_ID} onSubmit={onSubmit} noValidate className="space-y-4">
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('users.resetPasswordText', { name: item.fullName })}
        </p>

        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}

        <TextField
          label={t('users.newPassword')}
          hint={t('users.newPasswordHint')}
          type="password"
          autoComplete="new-password"
          autoFocus
          disabled={isSubmitting}
          error={errors.password ? errorMessage(errors.password.message) : undefined}
          {...register('password')}
        />
      </form>
    </Dialog>
  )
}
