import { Languages, LogOut, Phone, ShieldCheck, SunMoon, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

import { AccountPhotoEditor, formatFullPhone, useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { useTheme } from '@/shared/theme/theme-provider'
import {
  Badge,
  Card,
  CardDivider,
  DangerRow,
  Dialog,
  IconTile,
  type IconTone,
} from '@/shared/ui'
import { LanguageSwitcher } from '@/shared/ui/language-switcher'
import { ThemeToggle } from '@/shared/ui/theme-toggle'

/**
 * Shaxsiy sozlamalar oynasi — faqat o'zining hisobi.
 *
 * Nega alohida sahifa emas: bu yerda ko'rsatiladigan narsa oz va u
 * boshqa ish oqimiga bog'lanmagan. Foydalanuvchi sozlamani ochib,
 * o'zgartirib, ayni turgan sahifasiga qaytishi kerak — modal aynan
 * shuni beradi, marshrut esa kontekstni yo'qotadi.
 *
 * Nega faqat o'z ma'lumoti: tizimga faqat HR bo'limi va rahbar kiradi,
 * xodimlar umuman kirmaydi. Ya'ni "profil" bu yerda kadrlar kartasi
 * emas — u shunchaki "men kim bo'lib turibman" degan savolga javob.
 *
 * Nega `md` (560px), `lg` emas: oynada bitta ustunli qisqa ro'yxat bor.
 * Kengroq varaq qatorlarni cho'zib yuboradi — yorliq chapda, qiymat esa
 * o'ng chekkada qolib, ko'z ular orasida bo'sh joyni kesib o'tishga
 * majbur bo'ladi. iOS sozlamalar varag'i ham ataylab tor.
 *
 * ⚠️ Parol o'zgartirish YO'Q: backend'da endpoint yo'q. Ishlamaydigan
 * tugma qo'yilmaydi.
 */
export function SettingsDialog({
  open,
  onClose,
  onSignOut,
}: {
  open: boolean
  onClose: () => void
  /** Chiqish menyudagi bilan bir xil bo'lishi uchun tashqaridan beriladi. */
  onSignOut: () => void
}) {
  const { t } = useLocale()
  const { user } = useAuth()
  const { theme } = useTheme()

  const name = user?.fullName ?? t('auth.unknownUser')
  const phone = user === null ? null : formatFullPhone(user.login)

  /*
   * Rol kodi («hr_manager») foydalanuvchiga hech narsa aytmaydi, shu
   * sababli faqat tarjimasi bor rollar qoladi. Tarjimasi yo'q kod —
   * bu tizimga endi qo'shilgan rol: uni kod ko'rinishida ko'rsatgandan
   * ko'ra ko'rsatmagan afzal.
   */
  const roles = (user?.roles ?? [])
    .map((code) => ({ code, label: t(`roles.${code}`) }))
    .filter((role) => role.label !== `roles.${role.code}`)

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="md"
      title={t('nav.settings')}
      description={t('settings.subtitle')}
    >
      <div className="space-y-6">
        {/* ───────── Hisob ───────── */}
        <Section title={t('settings.account')}>
          {/*
            Rasm, ism va «rasmni o'chirish» qatori — bitta komponent:
            uchalasi ham bitta holatga (yuklandimi, o'chirilyaptimi)
            bog'langan va uni ikki joyga bo'lish tasdiqlash oynasini
            boshqarishni chalkashtirardi.
          */}
          <AccountPhotoEditor name={name} />

          {phone !== null && (
            <>
              <CardDivider />
              <Row
                icon={Phone}
                tone="green"
                label={t('employee.phone')}
                hint={t('settings.phoneHint')}
              >
                <span className="tabular text-subhead">{phone}</span>
              </Row>
            </>
          )}

          {roles.length > 0 && (
            <>
              <CardDivider />
              <Row icon={ShieldCheck} tone="brand" label={t('settings.role')}>
                {roles.map((role) => (
                  <Badge key={role.code} tone="brand">
                    {role.label}
                  </Badge>
                ))}
              </Row>
            </>
          )}
        </Section>

        {/*
          ───────── Interfeys ─────────
          Til va ko'rinish yuqori panelda ham bor, lekin ularning kanonik
          joyi shu yer: foydalanuvchi sozlamani birinchi navbatda
          "Sozlamalar" dan qidiradi. Almashtirgichlar qayta ishlatiladi —
          nusxasi bo'lsa, biri o'zgarganda ikkinchisi eskirib qolardi.
        */}
        <Section title={t('settings.interface')}>
          <Row
            icon={Languages}
            tone="blue"
            label={t('common.language')}
            hint={t('settings.languageHint')}
          >
            <LanguageSwitcher />
          </Row>

          <CardDivider />

          <Row
            icon={SunMoon}
            tone="purple"
            label={t('common.theme')}
            hint={t('settings.themeHint')}
          >
            {/* Ikonaning o'zi joriy holatni aytadi, lekin matn bilan
                birga u ikki xil o'qilmaydi */}
            <span className="text-footnote text-[var(--fg-secondary)]">
              {theme === 'dark' ? t('common.themeDark') : t('common.themeLight')}
            </span>
            <ThemeToggle />
          </Row>
        </Section>

        {/*
          ───────── Sessiya ─────────
          Chiqish — to'ldirilgan qizil tugma EMAS, qolgan qatorlar bilan
          bir xil qator (sababi `DangerRow` da). Tushuntirish matni tepada
          qoladi: qaytish uchun nima kerakligini oldindan aytadi.
        */}
        <Section title={t('settings.session')}>
          <p className="px-4 pt-4 pb-3 text-footnote text-[var(--fg-secondary)]">
            {t('settings.signOutHint')}
          </p>

          <CardDivider />

          <DangerRow icon={LogOut} label={t('common.signOut')} onClick={onSignOut} />
        </Section>
      </div>
    </Dialog>
  )
}

/**
 * iOS "Settings" dagi guruh: karta ustida kichik sarlavha, ichida qatorlar.
 * Sarlavha kartaning ichida emas — shunda guruhlar bir-biridan aniq ajraladi.
 */
function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section>
      <h3 className="px-1 pb-2 text-caption2 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase">
        {title}
      </h3>
      <Card className="overflow-hidden">{children}</Card>
    </section>
  )
}

/** Guruh ichidagi bitta qator: rangli ikona, yorliq va o'ngda qiymat yoki boshqaruv. */
function Row({
  icon,
  tone,
  label,
  hint,
  children,
}: {
  icon: LucideIcon
  tone: IconTone
  label: string
  /** Qator nima uchun kerakligini bir jumlada aytadi. Ixtiyoriy. */
  hint?: string
  children: ReactNode
}) {
  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-2.5 px-4 py-3">
      <IconTile icon={icon} tone={tone} />

      <div className="min-w-0 flex-1">
        <p className="text-subhead font-medium">{label}</p>
        {hint !== undefined && (
          <p className="mt-0.5 text-caption1 text-[var(--fg-tertiary)]">{hint}</p>
        )}
      </div>

      <div className="flex shrink-0 flex-wrap items-center justify-end gap-2">{children}</div>
    </div>
  )
}
