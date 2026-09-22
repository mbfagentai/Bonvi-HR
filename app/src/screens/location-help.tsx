import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'

import { deviceKind, type DeviceKind } from '@/lib/device'
import { permissionState } from '@/lib/geolocation'
import { PushedScreen } from '@/ui/app-shell'
import { Button } from '@/ui/button'
import { Card } from '@/ui/card'
import { InlineMessage } from '@/ui/feedback'
import { useToast } from '@/ui/toast'

/**
 * JOYLASHUV RUXSATI — tiklash ekrani (UX §C.7).
 *
 * ⚠️ Bu toast emas, EKRAN. iOS'da rad javobi yopishqoq: brauzer boshqa
 * so'ramaydi va xodim Sozlamalarga borishi kerak, ba'zan esa
 * nishonchani o'chirib qaytadan qo'shishi. Jim yiqilish esa odamni
 * bino tashqarisida, hech qanday yo'lsiz qoldiradi.
 *
 * ⚠️ Qurilmani aniqlash TAXMINIY, shu sababli to'rtala yo'riqnoma
 * ham ekranda ochiq turadi — taxmin faqat tartibni belgilaydi.
 */
const VARIANTS: DeviceKind[] = ['ios-standalone', 'ios-safari', 'android-app', 'android-chrome']

const STEPS: Record<DeviceKind, { titleKey: string; stepKeys: string[] }> = {
  'ios-standalone': {
    titleKey: 'location.iosStandalone',
    stepKeys: ['location.iosStandalone1', 'location.iosStandalone2', 'location.iosStandalone3'],
  },
  'ios-safari': {
    titleKey: 'location.iosSafari',
    stepKeys: ['location.iosSafari1', 'location.iosSafari2'],
  },
  'android-app': {
    titleKey: 'location.androidApp',
    stepKeys: ['location.androidApp1', 'location.androidApp2'],
  },
  'android-chrome': {
    titleKey: 'location.androidChrome',
    stepKeys: ['location.androidChrome1', 'location.androidChrome2'],
  },
}

export function LocationHelpScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()

  const [showAll, setShowAll] = useState(false)
  const [stillDenied, setStillDenied] = useState(false)
  const [checking, setChecking] = useState(false)

  const guessed = deviceKind()
  const others = VARIANTS.filter((variant) => variant !== guessed)

  const check = (): void => {
    setChecking(true)
    void permissionState().then((state) => {
      setChecking(false)

      if (state === 'granted' || state === 'prompt') {
        toast.show(t('location.granted'), 'success')
        navigate('/', { replace: true })
        return
      }

      setStillDenied(true)
    })
  }

  return (
    <PushedScreen title={t('location.title')}>
      <Card>
        <h2 className="text-title3 font-semibold text-fg">{t('location.deniedTitle')}</h2>
        <p className="mt-2 text-subhead text-fg-secondary">{t('location.why')}</p>
      </Card>

      <Card title={t(STEPS[guessed].titleKey)}>
        <Steps stepKeys={STEPS[guessed].stepKeys} />
      </Card>

      {showAll ? (
        others.map((variant) => (
          <Card key={variant} title={t(STEPS[variant].titleKey)}>
            <Steps stepKeys={STEPS[variant].stepKeys} />
          </Card>
        ))
      ) : (
        <Button variant="plain" block onClick={() => setShowAll(true)}>
          {t('location.otherDevice')}
        </Button>
      )}

      <Button block busy={checking} onClick={check}>
        {t('location.check')}
      </Button>

      {stillDenied ? (
        <InlineMessage tone="warning" assertive>
          {t('location.stillDenied')}
        </InlineMessage>
      ) : null}

      {/* Hech kim bu ekranda YO'LSIZ qolmasligi kerak. Turniket —
          baribir asosiy hisoblash usuli, ya'ni bu taskin emas,
          to'g'ri ko'rsatma (SPEC §B1). */}
      <Card title={t('location.fallbackTitle')}>
        <p className="text-subhead text-fg-secondary">{t('location.fallbackText')}</p>
      </Card>
    </PushedScreen>
  )
}

function Steps({ stepKeys }: { stepKeys: readonly string[] }) {
  const { t } = useTranslation()

  return (
    <ol className="space-y-3">
      {stepKeys.map((key, index) => (
        <li key={key} className="flex">
          <span className="nums mr-3 h-6 w-6 shrink-0 rounded-pill bg-accent-soft text-center text-footnote font-semibold leading-6 text-accent-on-soft">
            {index + 1}
          </span>
          <span className="min-w-0 flex-1 text-subhead text-fg">{t(key)}</span>
        </li>
      ))}
    </ol>
  )
}
