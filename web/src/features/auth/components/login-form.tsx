import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from '@tanstack/react-router'
import { ArrowRight, Eye, EyeOff, LoaderCircle } from 'lucide-react'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'

import { landingPath } from '@/features/processes/lib/landing'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, Checkbox, TextField } from '@/shared/ui'
import { useAuth } from '../auth-context'
import { authErrorCode } from '../lib/auth-error'
import { toFullPhone } from '../lib/phone'
import { loginSchema, type LoginValues } from '../schemas/login-schema'
import { PhoneField } from './phone-field'
import type { DevAccount } from '../lib/dev-accounts'
import { DevAccountsPanel } from './dev-accounts-panel'

export function LoginForm({ redirectTo }: { redirectTo?: string | undefined }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()
  const { login } = useAuth()

  const [showPassword, setShowPassword] = useState(false)
  // Serverdan kelgan xato. `null` — xato yo'q; ichidagi kod bo'sh bo'lishi
  // mumkin (noma'lum xato), shu sababli oddiy satr yetarli emas
  const [failure, setFailure] = useState<{ code: string } | null>(null)

  const {
    control,
    register,
    setValue,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { phone: '', password: '', remember: true },
    // Xato chiqqach har bosishda emas, faqat qiymat o'zgarganda qayta
    // tekshiriladi — foydalanuvchi yozayotganda qizil matn «titramaydi»
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  /**
   * Kirish mantig'i. Formadan ham, dev tugmasidan ham chaqiriladi —
   * shu sababli `handleSubmit` dan ajratilgan.
   */
  const performLogin = async (values: LoginValues) => {
    setFailure(null)

    try {
      const user = await login({
        login: toFullPhone(values.phone),
        password: values.password,
        remember: values.remember,
      })

      await navigate({ href: redirectTo ?? landingPath(user), replace: true })
    } catch (error) {
      setFailure({ code: authErrorCode(error) })
    }
  }

  const onSubmit = handleSubmit(performLogin)

  /**
   * Dev-rejimda namunaviy hisob bilan kirish.
   * Maydonlar ham to'ldiriladi — foydalanuvchi qaysi ma'lumot bilan
   * kirganini ko'rib tursin va keyin qo'lda o'zgartira olsin.
   */
  const onPickDevAccount = (account: DevAccount) => {
    setValue('phone', account.login, { shouldValidate: false })
    setValue('password', account.password, { shouldValidate: false })

    void performLogin({
      phone: account.login,
      password: account.password,
      remember: true,
    })
  }

  return (
    <>
      <Card className="rounded-2xl p-6 shadow-raised sm:p-8">
        <form onSubmit={onSubmit} noValidate className="space-y-5">
      {failure && (
        // `pop-in` — globals.css dagi ochiluvchi panel animatsiyasi:
        // xato «paydo bo'ladi», sakrab chiqmaydi
        <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
          {errorMessage(failure.code)}
        </Alert>
      )}

      <Controller
        control={control}
        name="phone"
        render={({ field }) => (
          <PhoneField
            name={field.name}
            value={field.value}
            onChange={field.onChange}
            onBlur={field.onBlur}
            disabled={isSubmitting}
            autoFocus
            label={t('auth.phone')}
            error={errors.phone ? errorMessage(errors.phone.message) : undefined}
          />
        )}
      />

      <TextField
        variant="filled"
        size="lg"
        label={t('auth.password')}
        type={showPassword ? 'text' : 'password'}
        autoComplete="current-password"
        placeholder={t('auth.passwordPlaceholder')}
        disabled={isSubmitting}
        error={errors.password ? errorMessage(errors.password.message) : undefined}
        trailing={
          <button
            type="button"
            // Tab bilan yurganda parol maydonidan to'g'ri «Kirish» tugmasiga
            // o'tilsin — bu tugma klaviatura yo'lida ortiqcha to'siq
            tabIndex={-1}
            disabled={isSubmitting}
            onClick={() => setShowPassword((previous) => !previous)}
            aria-label={showPassword ? t('auth.hidePassword') : t('auth.showPassword')}
            title={showPassword ? t('auth.hidePassword') : t('auth.showPassword')}
            className="press -mr-1.5 grid size-8 shrink-0 place-items-center rounded-sm text-[var(--fg-tertiary)] transition-colors hover:bg-[var(--bg-hover)] hover:text-[var(--fg-secondary)]"
          >
            {showPassword ? <EyeOff className="size-[18px]" /> : <Eye className="size-[18px]" />}
          </button>
        }
        {...register('password')}
      />

      <Controller
        control={control}
        name="remember"
        render={({ field }) => (
          <Checkbox
            checked={field.value}
            onChange={field.onChange}
            disabled={isSubmitting}
            label={t('auth.remember')}
          />
        )}
      />

      <Button
        type="submit"
        variant="primary"
        size="lg"
        disabled={isSubmitting}
        className="group h-[52px] w-full text-callout"
      >
        {isSubmitting ? (
          <>
            <LoaderCircle className="animate-spin" />
            {t('auth.submitting')}
          </>
        ) : (
          <>
            {t('auth.submit')}
            <ArrowRight className="transition-transform group-hover:translate-x-0.5" />
          </>
        )}
      </Button>
        </form>
      </Card>

      <p className="mt-5 text-center text-footnote text-[var(--fg-tertiary)]">
        {t('auth.help')}
      </p>

      {/* Production build'da `import.meta.env.DEV` — `false`, shu sababli
          bu shox butunlay olib tashlanadi */}
      {import.meta.env.DEV && (
        <DevAccountsPanel onPick={onPickDevAccount} disabled={isSubmitting} />
      )}
    </>
  )
}
