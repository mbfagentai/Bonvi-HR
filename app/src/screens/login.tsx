import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'

import { ApiError } from '@/api/errors'
import { useSession } from '@/app/session'
import { isInAppBrowser } from '@/lib/device'
import { useErrorText } from '@/lib/use-error-message'
import { Button } from '@/ui/button'
import { InlineMessage } from '@/ui/feedback'
import { Field, TextInput } from '@/ui/form'
import { Icon } from '@/ui/icon'

/**
 * KIRISH (UX §A.3).
 *
 * ⚠️ Token yo'qolgani uchun shu ekranga tushgan odamga hech qanday
 * qo'rqinchli xabar ko'rsatilmaydi: Safari saqlangan ma'lumotni 7 kun
 * faolsizlikdan keyin o'chirib yuborishi mumkin va bu ODATIY hol.
 * Telefon raqami oldindan to'ldirilgan bo'ladi — xolos (UX §G.4).
 */
export function LoginScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const session = useSession()
  const errorText = useErrorText()
  const [params] = useSearchParams()

  const [phone, setPhone] = useState(session.lastPhone)
  const [password, setPassword] = useState('')
  const [visible, setVisible] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  const submit = async (): Promise<void> => {
    setBusy(true)
    setError(null)

    try {
      await session.signIn(phone.trim(), password)

      const next = params.get('keyin')
      navigate(next && next.indexOf('/') === 0 ? next : '/', { replace: true })
    } catch (cause: unknown) {
      setError(ApiError.from(cause))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="mx-auto flex min-h-screen w-full max-w-app flex-col px-4 pb-8 pt-safe">
      <header className="pb-8 pt-12 text-center">
        <img
          src={`${import.meta.env.BASE_URL}icons/apple-touch-icon.png`}
          alt=""
          className="mx-auto mb-4 h-20 w-20 rounded-[22px]"
        />
        <h1 className="text-title1 font-bold text-fg">{t('auth.title')}</h1>
        <p className="mt-1 text-subhead text-fg-secondary">{t('auth.subtitle')}</p>
      </header>

      {/* Telegram/Instagram ichidagi brauzerda PWA o'rnatib bo'lmaydi
          va joylashuv ham o'sha ilovaniki bo'ladi — buni yashirmasdan
          aytamiz (UX §F.1). */}
      {isInAppBrowser() ? (
        <div className="mb-4">
          <InlineMessage tone="warning">{t('auth.inAppBrowser')}</InlineMessage>
        </div>
      ) : null}

      <div className="space-y-4">
        <Field label={t('auth.phone')} required>
          <TextInput
            type="tel"
            inputMode="tel"
            value={phone}
            onChange={setPhone}
            placeholder={t('auth.phonePlaceholder')}
            autoComplete="username"
          />
        </Field>

        <Field label={t('auth.password')} required>
          <div className="relative">
            <TextInput
              type={visible ? 'text' : 'password'}
              value={password}
              onChange={setPassword}
              placeholder={t('auth.passwordPlaceholder')}
              autoComplete="current-password"
            />
            <button
              type="button"
              aria-label={visible ? t('auth.hidePassword') : t('auth.showPassword')}
              onClick={() => setVisible((value) => !value)}
              className="absolute bottom-0 right-0 top-0 flex w-12 items-center justify-center text-fg-tertiary"
            >
              <Icon name={visible ? 'x-circle' : 'information-circle'} className="h-5 w-5" />
            </button>
          </div>
        </Field>

        {error ? (
          <InlineMessage tone="danger" assertive>
            {errorText(error).body}
          </InlineMessage>
        ) : null}

        <Button block busy={busy} onClick={() => void submit()}>
          {busy ? t('auth.submitting') : t('auth.submit')}
        </Button>

        <div className="text-center">
          <Link to="/parol-yordam" className="text-footnote text-accent-text">
            {t('auth.forgot')}
          </Link>
        </div>
      </div>
    </div>
  )
}
