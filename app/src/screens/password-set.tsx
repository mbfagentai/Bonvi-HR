import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'

import { api } from '@/api/client'
import { ApiError } from '@/api/errors'
import { useSession } from '@/app/session'
import { useErrorText } from '@/lib/use-error-message'
import { Button } from '@/ui/button'
import { InlineMessage } from '@/ui/feedback'
import { Field, TextInput } from '@/ui/form'
import { useToast } from '@/ui/toast'

/**
 * PAROLNI O'RNATISH — majburiy birinchi qadam (SPEC §D2).
 *
 * HR bergan parol bir martalik. Bu ekrandan chiqishning ikki yo'li
 * bor: parolni o'rnatish yoki hisobdan chiqish — boshqa marshrutlar
 * shu yerga qaytariladi (UX §B.3).
 *
 * ⭐ Joriy parol — HR bergan bir martalik parol — server tomonidan
 * MAJBURIY tekshiriladi. Xodim uni hozirgina kirish ekranida kiritgan:
 * sessiya uni XOTIRADA ushlab turadi va bu yerda qayta so'ramaydi.
 * Sahifa qayta yuklangan (xotira tozalangan) yoki HR parolni ochiq
 * sessiya davomida tiklagan bo'lsa — bir martalik parol maydoni chiqadi.
 *
 * ⭐ Javobda YANGI token juftligi keladi: server barcha eski sessiyalarni
 * yopadi, ya'ni eski yangilash tokeni endi o'lgan va keyingi yangilash
 * uni ishlatsa, o'g'irlik deb baholanib hamma sessiya yopilardi.
 *
 * ⚠️ Ilovada YAGONA avtomatik fokus shu yerda: ekran ochilgan
 * zahoti klaviatura kerak, chunki boshqa qiladigan ish yo'q.
 */
export function PasswordSetScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const session = useSession()
  const errorText = useErrorText()

  const [current, setCurrent] = useState('')
  const [askCurrent, setAskCurrent] = useState(session.oneTimePassword === null)
  const [next, setNext] = useState('')
  const [repeat, setRepeat] = useState('')
  const [touched, setTouched] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  const tooShort = touched && next.length > 0 && next.length < 8
  const mismatch = touched && repeat.length > 0 && repeat !== next
  const currentMissing = touched && askCurrent && current.length === 0

  const save = async (): Promise<void> => {
    setTouched(true)
    const currentPassword = askCurrent ? current : session.oneTimePassword ?? ''
    if (next.length < 8 || next !== repeat || currentPassword.length === 0) return

    setBusy(true)
    setError(null)

    try {
      const result = await api.changePassword({ currentPassword, newPassword: next })
      session.passwordChanged(result)
      toast.show(t('password.saved'), 'success')
      navigate('/', { replace: true })
    } catch (cause: unknown) {
      const failure = ApiError.from(cause)
      setError(failure)

      // Xotiradagi parol mos kelmadi (HR uni shu orada tiklagan) — endi so'raymiz.
      if (failure.code === 'xodim.parol_notogri') setAskCurrent(true)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="mx-auto flex min-h-screen w-full max-w-app flex-col px-4 pb-8 pt-safe">
      <header className="pb-6 pt-10">
        <h1 className="text-title1 font-bold text-fg">{t('password.setTitle')}</h1>
        <p className="mt-2 text-subhead text-fg-secondary">{t('password.setText')}</p>
      </header>

      <div className="space-y-4">
        {askCurrent ? (
          <Field
            label={t('password.oneTime')}
            required
            helper={t('password.oneTimeHint')}
            error={currentMissing ? t('password.empty') : null}
          >
            <TextInput
              type="password"
              value={current}
              onChange={setCurrent}
              invalid={currentMissing}
              autoFocus
              autoComplete="current-password"
            />
          </Field>
        ) : null}

        <Field
          label={t('password.new')}
          required
          helper={t('password.hint')}
          error={tooShort ? t('password.tooShort') : null}
        >
          <TextInput
            type="password"
            value={next}
            onChange={setNext}
            onBlur={() => setTouched(true)}
            invalid={tooShort}
            autoFocus={!askCurrent}
            autoComplete="new-password"
          />
        </Field>

        <Field
          label={t('password.repeat')}
          required
          error={mismatch ? t('password.mismatch') : null}
        >
          <TextInput
            type="password"
            value={repeat}
            onChange={setRepeat}
            onBlur={() => setTouched(true)}
            invalid={mismatch}
            autoComplete="new-password"
          />
        </Field>

        {error ? (
          <InlineMessage tone="danger" assertive>
            {errorText(error).body}
          </InlineMessage>
        ) : null}

        <Button block busy={busy} onClick={() => void save()}>
          {busy ? t('password.submitting') : t('password.submit')}
        </Button>

        <Button
          variant="plain"
          block
          onClick={() => {
            session.signOut()
            navigate('/kirish', { replace: true })
          }}
        >
          {t('common.signOut')}
        </Button>
      </div>
    </div>
  )
}
