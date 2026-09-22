import { useTranslation } from 'react-i18next'

import { useInstall } from '@/app/install'
import { Button } from './button'
import { Icon } from './icon'

/**
 * O'rnatish kartasi (UX §G.4) — bitta komponent, ikki ko'rinish.
 *
 * Nega o'rnatish muhim (xodimga BEVOSITA aytilmaydi, lekin sabab shu):
 * bosh ekranga qo'shilgan iOS ilovasi joylashuv ruxsatini O'ZI uchun
 * oladi — Safari'da rad etgan odam shu yo'l bilan tiklaydi; bundan
 * tashqari o'rnatilgan ilovada saqlangan token 7 kunlik tozalashga
 * tushmaydi.
 */
export function InstallCard() {
  const { t } = useTranslation()
  const { variant, prompt, dismiss } = useInstall()

  if (!variant) return null

  return (
    <section className="rounded-card bg-bg-elevated p-4">
      <div className="flex">
        <div className="min-w-0 flex-1">
          <h2 className="text-body font-semibold text-fg">
            {variant === 'android' ? t('install.androidTitle') : t('install.iosTitle')}
          </h2>
          <p className="mt-1 text-footnote text-fg-secondary">
            {variant === 'android' ? t('install.androidText') : t('install.iosText')}
          </p>
        </div>

        <button
          type="button"
          aria-label={t('install.dismiss')}
          onClick={dismiss}
          className="ml-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-control text-fg-tertiary active:bg-bg-pressed"
        >
          <Icon name="x-circle" className="h-5 w-5" />
        </button>
      </div>

      {variant === 'android' ? (
        <div className="mt-3 flex">
          <Button variant="secondary" size="compact" onClick={prompt}>
            {t('install.androidAction')}
          </Button>
          <div className="w-2" />
          <Button variant="plain" size="compact" onClick={dismiss}>
            {t('install.later')}
          </Button>
        </div>
      ) : null}
    </section>
  )
}
