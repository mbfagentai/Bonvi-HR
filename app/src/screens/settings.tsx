import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { useInstall } from '@/app/install'
import { useTheme, type ThemeChoice } from '@/app/theme'
import { useUpdate } from '@/app/update'
import { APP_VERSION } from '@/app/version'
import { LOCALES, type LocaleCode } from '@/i18n/locales'
import { permissionState, type PermissionState } from '@/lib/geolocation'
import { PushedScreen } from '@/ui/app-shell'
import { ListGroup, ListRow } from '@/ui/list'
import { Sheet } from '@/ui/sheet'

/**
 * SOZLAMALAR (UX §A.5).
 *
 * Uchta guruh, boshqa hech narsa: interfeys, joylashuv, ilova haqida.
 * Bildirishnomalar bo'limi YO'Q — bu to'lqinda push yo'q, o'chirilgan
 * holda ko'rsatilgan sozlama esa yo'q sozlamadan yomonroq.
 */
export function SettingsScreen() {
  const { t, i18n } = useTranslation()
  const theme = useTheme()
  const update = useUpdate()
  const install = useInstall()

  const [themeOpen, setThemeOpen] = useState(false)
  const [langOpen, setLangOpen] = useState(false)
  const [permission, setPermission] = useState<PermissionState>('unknown')

  useEffect(() => {
    let alive = true
    void permissionState().then((state) => {
      if (alive) setPermission(state)
    })
    return () => {
      alive = false
    }
  }, [])

  const themeLabel: Record<ThemeChoice, string> = {
    system: t('settings.themeSystem'),
    light: t('settings.themeLight'),
    dark: t('settings.themeDark'),
  }

  const permissionLabel: Record<PermissionState, string> = {
    granted: t('settings.locationGranted'),
    denied: t('settings.locationDenied'),
    prompt: t('settings.locationPrompt'),
    unknown: t('settings.locationUnknown'),
  }

  const currentLocale = LOCALES.filter((locale) => locale.code === i18n.language)[0] ?? LOCALES[0]

  return (
    <PushedScreen title={t('settings.title')}>
      <ListGroup title={t('settings.interface')}>
        <ListRow
          label={t('settings.theme')}
          value={themeLabel[theme.choice]}
          chevron
          onClick={() => setThemeOpen(true)}
        />
        <ListRow
          label={t('settings.language')}
          value={currentLocale.label}
          chevron
          onClick={() => setLangOpen(true)}
        />
      </ListGroup>

      <ListGroup title={t('settings.location')}>
        <ListRow
          label={t('settings.locationState')}
          value={permissionLabel[permission]}
          chevron={false}
        />
        <ListRow label={t('settings.locationHelp')} to="/joylashuv" />
      </ListGroup>

      <ListGroup title={t('settings.about')}>
        <ListRow label={t('settings.version')} value={APP_VERSION} chevron={false} />
        {/* Doimiy yo'l: yangilanish kutayotgan bo'lsa shu qator
            ishlaydi. Qo'llab-quvvatlash qo'ng'irog'ida odamni aynan
            shu qator orqali olib o'tish mumkin (UX §G.5). */}
        <ListRow
          label={t('settings.update')}
          secondary={update.needRefresh ? t('settings.updateReady') : t('settings.updateNone')}
          disabled={!update.needRefresh}
          chevron={update.needRefresh}
          onClick={update.needRefresh ? update.update : undefined}
        />
        {install.canPrompt ? (
          <ListRow label={t('settings.install')} chevron onClick={install.prompt} />
        ) : null}
      </ListGroup>

      <Sheet open={themeOpen} title={t('settings.theme')} onClose={() => setThemeOpen(false)}>
        <div className="overflow-hidden rounded-card bg-bg-elevated">
          {(['system', 'light', 'dark'] as ThemeChoice[]).map((choice) => (
            <ListRow
              key={choice}
              label={themeLabel[choice]}
              value={theme.choice === choice ? '✓' : undefined}
              chevron={false}
              onClick={() => {
                theme.setChoice(choice)
                setThemeOpen(false)
              }}
            />
          ))}
        </div>
      </Sheet>

      <Sheet open={langOpen} title={t('settings.language')} onClose={() => setLangOpen(false)}>
        <div className="overflow-hidden rounded-card bg-bg-elevated">
          {LOCALES.map((locale) => (
            <ListRow
              key={locale.code}
              label={locale.label}
              secondary={locale.script}
              value={i18n.language === locale.code ? '✓' : undefined}
              chevron={false}
              onClick={() => {
                void i18n.changeLanguage(locale.code as LocaleCode)
                setLangOpen(false)
              }}
            />
          ))}
        </div>
      </Sheet>
    </PushedScreen>
  )
}
