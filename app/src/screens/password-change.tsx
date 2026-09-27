import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'

import { api } from '@/api/client'
import { ApiError } from '@/api/errors'
import { useSession } from '@/app/session'
import { useErrorText } from '@/lib/use-error-message'
import { PushedScreen } from '@/ui/app-shell'
import { BottomAction, Button } from '@/ui/button'
import { Card } from '@/ui/card'
import { InlineMessage } from '@/ui/feedback'
import { Field, TextInput } from '@/ui/form'
import { useToast } from '@/ui/toast'

/**
 * PAROLNI O'ZGARTIRISH (UX §A.3).
 *
 * ⚠️ Javobdagi YANGI token juftligi saqlanadi: server almashtirishdan
 * keyin barcha eski sessiyalarni (shu telefonnikini ham) yopadi. Eski
 * yangilash tokeni bilan keyingi yangilash o'g'irlik deb baholanib, hamma
 * sessiyani yopib yuborardi.
 *
 * Tekshiruv fokus chiqqanda va yuborishda bo'ladi, HAR BOSISHDA emas:
 * parol yozayotgan odamga to'rtinchi harfdayoq «juda qisqa» deyish —
 * bezovta qilish.
 */
export function PasswordChangeScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const errorText = useErrorText()
  const session = useSession()

  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [repeat, setRepeat] = useState('')
  const [touched, setTouched] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  const tooShort = touched && next.length > 0 && next.length < 8
  const mismatch = touched && repeat.length > 0 && repeat !== next

  const save = async (): Promise<void> => {
    setTouched(true)
    if (next.length < 8 || next !== repeat) return

    setSaving(true)
    setError(null)

    try {
      const result = await api.changePassword({ currentPassword: current, newPassword: next })
      session.passwordChanged(result)
      toast.show(t('password.saved'), 'success')
      navigate(-1)
    } catch (cause: unknown) {
      setError(ApiError.from(cause))
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <PushedScreen title={t('password.changeTitle')} bottomSpace>
        <Card>
          <div className="space-y-4">
            <Field label={t('password.current')} required>
              <TextInput
                type="password"
                value={current}
                onChange={setCurrent}
                autoComplete="current-password"
              />
            </Field>

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
          </div>
        </Card>

        {error ? (
          <InlineMessage tone="danger" assertive>
            {errorText(error).body}
          </InlineMessage>
        ) : null}
      </PushedScreen>

      <BottomAction>
        <Button block busy={saving} onClick={() => void save()}>
          {t('password.submit')}
        </Button>
      </BottomAction>
    </>
  )
}
