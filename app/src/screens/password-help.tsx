import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'

import { Button } from '@/ui/button'
import { Card } from '@/ui/card'

/**
 * PAROLNI UNUTDIM — statik ekran (UX §A.3).
 *
 * Bu yerda forma YO'Q va bo'lmaydi ham: parolni xodimning o'zi tiklaydigan
 * yoki almashtiradigan yo'l yo'q — parolni HR beradi va istalgan payt
 * ko'rib aytib bera oladi (SPEC §D2, 2026-09-27). Ishlamaydigan «tiklash»
 * tugmasi qo'yish — xodimni behuda kutishga majburlash.
 */
export function PasswordHelpScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const steps = [t('auth.helpStep1'), t('auth.helpStep2'), t('auth.helpStep3')]

  return (
    <div className="mx-auto flex min-h-screen w-full max-w-app flex-col px-4 pb-8 pt-safe">
      <header className="pb-6 pt-10">
        <h1 className="text-title1 font-bold text-fg">{t('auth.helpTitle')}</h1>
        <p className="mt-2 text-subhead text-fg-secondary">{t('auth.helpText')}</p>
      </header>

      <Card>
        <ol className="space-y-3">
          {steps.map((step, index) => (
            <li key={step} className="flex">
              <span className="nums mr-3 h-6 w-6 shrink-0 rounded-pill bg-accent-soft text-center text-footnote font-semibold leading-6 text-accent-on-soft">
                {index + 1}
              </span>
              <span className="min-w-0 flex-1 text-subhead text-fg">{step}</span>
            </li>
          ))}
        </ol>
      </Card>

      <div className="mt-6">
        <Button block variant="secondary" onClick={() => navigate('/kirish')}>
          {t('auth.backToSignIn')}
        </Button>
      </div>
    </div>
  )
}
