import { Bell, Bot, LoaderCircle, SlidersHorizontal } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { problemCode } from '@/shared/api/problem-details'
import {
  Alert,
  Button,
  Card,
  EmptyState,
  IconTile,
  PageHeader,
  Skeleton,
  TextArea,
  TextField,
  type IconTone,
} from '@/shared/ui'

import { SETTINGS_MANAGE, type SystemSetting } from '../api/settings-api'
import { useSaveSettings, useSystemSettings } from '../api/settings-queries'

/**
 * Ekrandagi guruhlar: sarlavha, ikona va ichidagi kalitlar.
 *
 * Guruhlash SERVERDA yo'q va ataylab yo'q: `SystemSettingKeys` uchun
 * guruh — bu KO'RINISH masalasi, sozlamaning ta'rifi emas. Ikkita bot
 * matni va suhbat muddati bitta joyda turishi kerakligini ekran biladi,
 * domen esa faqat kalit, tur va chegarani biladi.
 *
 * Kalitlar shablon (`` `settings.system.${key}` ``) EMAS, aniq xarita —
 * sabab `notification-presentation.ts` dagi `SOURCE_KEY` bilan bir xil:
 * `check-locales.mjs` obyektda yig'ilgan satrni ko'rmaydi va tarjimasi
 * unutilgan sozlama ekranga XOM KALIT bo'lib chiqardi. Xarita bilan
 * serverdan kelgan noma'lum kalit shunchaki chizilmaydi.
 */
const GROUPS: readonly {
  titleKey: string
  hintKey: string
  icon: LucideIcon
  tone: IconTone
  fields: Readonly<Record<string, string>>
}[] = [
  {
    titleKey: 'settings.system.groupBot',
    hintKey: 'settings.system.groupBotHint',
    icon: Bot,
    tone: 'blue',
    fields: {
      'bot.about.cyrillic': 'settings.system.botAboutCyrillic',
      'bot.about.latin': 'settings.system.botAboutLatin',
      'bot.conversationTimeoutMinutes': 'settings.system.botConversationTimeout',
    },
  },
  {
    titleKey: 'settings.system.groupNotifications',
    hintKey: 'settings.system.groupNotificationsHint',
    icon: Bell,
    tone: 'orange',
    fields: {
      'notifications.probationWarningDays': 'settings.system.probationWarningDays',
      'notifications.newCandidateDays': 'settings.system.newCandidateDays',
      'notifications.offerWarningDays': 'settings.system.offerWarningDays',
    },
  },
]

/**
 * Tizim sozlamalari — FAQAT super administrator uchun.
 *
 * <b>Bu sozlamalar SIR EMAS.</b> Bot matni va bildirishnoma chegaralari —
 * ishlash parametrlari, shu sababli ular oddiy matn bo'lib saqlanadi va
 * shifrlanmaydi. Token, parol va kalitlar bu ro'yxatda ATAYLAB yo'q:
 * ularni bazaga ko'chirish xavfsizlikni oshirmasdi (shifrlash kaliti
 * baribir `.env` da qolardi), lekin zaxira nusxalarga sir qo'shardi.
 *
 * <b>Nega ALOHIDA SAHIFA, shaxsiy sozlamalar oynasining bo'limi emas.</b>
 * Ilgari u shu oynaning ichida edi va bu xato edi: oyna — «MENING
 * hisobim» (rasmim, tilim, mavzuim, chiqish). Bu yerdagi qiymatlar esa
 * shaxsiy emas — ular BUTUN tizimga, ya'ni boshqa foydalanuvchilarga va
 * nomzod ko'radigan bot matniga ta'sir qiladi. Ikki xil ma'nodagi narsani
 * bitta oynaga tiqish, ustiga uni tor modalda ko'rsatish, sozlamani
 * «yashirin» qilib qo'yardi. Endi u yon menyuda, «Tizim» guruhida —
 * `/foydalanuvchilar` yonida, chunki ikkalasi ham bir xil savolga
 * tegishli: tizimning o'zi qanday ishlaydi.
 *
 * <b>Nega butun sahifa ruxsat bilan o'ralgan.</b> `GET /api/settings`
 * ning o'zi ham `settings.manage` bilan himoyalangan, ya'ni ruxsatsiz
 * odam qiymatlarni SERVERDAN OLA OLMAYDI. Menyudan band unga umuman
 * ko'rinmaydi (`nav-items.ts` dagi `permission`), lekin to'g'ridan-to'g'ri
 * manzil yozilsa ham sahifa tushunarli holat ko'rsatadi (`UsersPage`
 * dagi naqsh).
 */
export function SystemSettingsPage() {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  /*
    Tizim sozlamalari FAQAT super administratorda. Bosh rahbarda ham
    yo'q — bu loyihadagi «rahbariyat ruxsatlar bo'yicha teng» qoidasidan
    yagona chekinish (sabab `Permissions.SettingsManage` izohida).
  */
  const canManage = hasPermission(SETTINGS_MANAGE)

  const settings = useSystemSettings(canManage)
  const save = useSaveSettings()

  /** Tahrirlanayotgan qiymatlar: kalit → matn. */
  const [draft, setDraft] = useState<Record<string, string>>({})

  /*
    Serverdan kelgan qiymatlar nusxasi. Foydalanuvchi yozayotganda
    so'rov fon rejimida yangilansa (oyna fokusga qaytganda), yozilgan
    matn o'chib ketmasligi kerak — shu sababli nusxa FAQAT ro'yxat
    birinchi marta kelganda olinadi.
  */
  useEffect(() => {
    if (settings.data === undefined) return

    setDraft((current) =>
      Object.keys(current).length > 0
        ? current
        : Object.fromEntries(settings.data.map((item) => [item.key, item.value])),
    )
  }, [settings.data])

  const items = settings.data ?? []
  const changed = items.filter((item) => (draft[item.key] ?? item.value) !== item.value)

  const submit = () => {
    save.mutate(
      changed.map((item) => ({ key: item.key, value: draft[item.key] ?? item.value })),
      {
        // Saqlangach nusxa TOZALANADI: keyingi `useEffect` serverdan
        // kelgan yangi qiymatlarni oladi va forma haqiqat bilan
        // moslashadi (masalan server matnni qirqqan bo'lsa).
        onSuccess: () => setDraft({}),
      },
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={SlidersHorizontal} tone="brand" size="lg" />
            {t('settings.system.title')}
          </span>
        }
        subtitle={t('settings.system.subtitle')}
      />

      {!canManage ? (
        <Card>
          <EmptyState
            icon={SlidersHorizontal}
            title={t('settings.system.noAccessTitle')}
            description={t('settings.system.noAccessText')}
          />
        </Card>
      ) : settings.isLoading ? (
        <Skeleton className="h-96 w-full" />
      ) : settings.isError || settings.data === undefined ? (
        <Alert tone="danger">{errorMessage(problemCode(settings.error))}</Alert>
      ) : (
        <div className="space-y-6">
          {save.isError && <Alert tone="danger">{errorMessage(problemCode(save.error))}</Alert>}

          {GROUPS.map((group) => {
            /*
              Guruhda ko'rsatiladigan sozlamalar SERVER tartibida emas,
              XARITA tartibida olinadi: ekrandagi ketma-ketlik ko'rinish
              masalasi va u API javobining tasodifiy tartibiga bog'liq
              bo'lmasligi kerak. Serverda yo'q kalit shunchaki tushib
              qoladi.
            */
            const fields = Object.keys(group.fields)
              .map((key) => items.find((item) => item.key === key))
              .filter((item) => item !== undefined)

            if (fields.length === 0) return null

            return (
              <Group
                key={group.titleKey}
                icon={group.icon}
                tone={group.tone}
                title={t(group.titleKey)}
                hint={t(group.hintKey)}
              >
                {fields.map((item) => (
                  <SettingField
                    key={item.key}
                    setting={item}
                    label={t(group.fields[item.key]!)}
                    value={draft[item.key] ?? item.value}
                    onChange={(next) =>
                      setDraft((current) => ({ ...current, [item.key]: next }))
                    }
                  />
                ))}
              </Group>
            )
          })}

          {/*
            Tugma FAQAT o'zgarish bo'lganda faollashadi: «saqlash» bosilib,
            hech narsa o'zgarmaganini ko'rish foydalanuvchini «saqlandimi?»
            degan savol bilan qoldirardi.

            Joyi — oxirgi guruhdan KEYIN, sahifa boshidagi harakatlar
            qatorida emas: forma yuqoridan pastga to'ldiriladi va tugma
            o'sha yo'lning oxirida turishi kerak.
          */}
          <div className="flex items-center justify-end gap-3">
            {changed.length > 0 && (
              <span className="text-footnote text-[var(--fg-secondary)]">
                {t('settings.system.unsaved')}
              </span>
            )}
            <Button
              variant="primary"
              onClick={submit}
              disabled={save.isPending || changed.length === 0}
            >
              {save.isPending && <LoaderCircle className="animate-spin" />}
              {t('common.save')}
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}

/**
 * Bitta guruh: rangli ikona bilan sarlavha, ostida bir jumlalik izoh va
 * maydonlar. `settings-dialog.tsx` dagi `Section` bilan bir xil fikr,
 * lekin bu yerda sarlavha kartaning ICHIDA — sahifada guruhlar kam va
 * har biri o'zining nima uchun kerakligini aytib turishi kerak.
 */
function Group({
  icon,
  tone,
  title,
  hint,
  children,
}: {
  icon: LucideIcon
  tone: IconTone
  title: string
  hint: string
  children: ReactNode
}) {
  return (
    <Card className="overflow-hidden">
      <div className="flex items-start gap-3 border-b border-[var(--border)] px-4 py-4 sm:px-5">
        <IconTile icon={icon} tone={tone} />
        <div className="min-w-0">
          <h2 className="text-subhead font-semibold">{title}</h2>
          <p className="mt-0.5 text-caption1 text-[var(--fg-tertiary)]">{hint}</p>
        </div>
      </div>

      <div className="space-y-4 px-4 py-4 sm:px-5">{children}</div>
    </Card>
  )
}

/**
 * Bitta sozlama maydoni.
 *
 * Tur SERVERDAN keladi (`kind`), mijoz uni taxmin qilmaydi: uzun matn
 * ko'p qatorli maydonga, son esa raqamli maydonga tushadi va chegaralar
 * o'sha yerda ko'rsatiladi.
 */
function SettingField({
  setting,
  label,
  value,
  onChange,
}: {
  setting: SystemSetting
  label: string
  value: string
  onChange: (next: string) => void
}) {
  const { t, num } = useLocale()

  if (setting.kind === 'Number') {
    return (
      <TextField
        label={label}
        type="number"
        min={setting.min ?? undefined}
        max={setting.max ?? undefined}
        hint={
          setting.min !== null && setting.max !== null
            ? t('settings.system.range', { min: num(setting.min), max: num(setting.max) })
            : undefined
        }
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    )
  }

  return (
    <TextArea
      label={label}
      rows={4}
      hint={t('settings.system.textHint')}
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  )
}
