import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { problemCode } from '@/shared/api/problem-details'
import { Alert, Button, Skeleton, TextArea, TextField } from '@/shared/ui'

import type { SystemSetting } from '../api/settings-api'
import { useSaveSettings, useSystemSettings } from '../api/settings-queries'

/**
 * Har bir kalitning tarjima kaliti.
 *
 * Shablon (`` `settings.system.${key}` ``) EMAS, aniq xarita — sabab
 * `notification-presentation.ts` dagi `SOURCE_KEY` bilan bir xil:
 * `check-locales.mjs` obyektda yig'ilgan satrni ko'rmaydi va tarjimasi
 * unutilgan sozlama ekranga XOM KALIT bo'lib chiqardi. Xarita bilan
 * serverdan kelgan noma'lum kalit shunchaki chizilmaydi.
 */
const LABEL_KEY: Readonly<Record<string, string>> = {
  'bot.about.cyrillic': 'settings.system.botAboutCyrillic',
  'bot.about.latin': 'settings.system.botAboutLatin',
  'bot.conversationTimeoutMinutes': 'settings.system.botConversationTimeout',
  'notifications.probationWarningDays': 'settings.system.probationWarningDays',
  'notifications.newCandidateDays': 'settings.system.newCandidateDays',
  'notifications.offerWarningDays': 'settings.system.offerWarningDays',
}

/**
 * Tizim sozlamalari — FAQAT super administrator uchun.
 *
 * <b>Bu sozlamalar SIR EMAS.</b> Bot matni va bildirishnoma chegaralari —
 * ishlash parametrlari, shu sababli ular oddiy matn bo'lib saqlanadi va
 * shifrlanmaydi. Token, parol va kalitlar bu ro'yxatda ATAYLAB yo'q:
 * ularni bazaga ko'chirish xavfsizlikni oshirmasdi (shifrlash kaliti
 * baribir `.env` da qolardi), lekin zaxira nusxalarga sir qo'shardi.
 *
 * <b>Nega alohida bo'lim, alohida sahifa emas.</b> Bu kundalik ish emas —
 * yiliga bir necha marta ochiladi. Ma'lumotnomalar yon menyuda yopiq
 * guruhda turgani bilan bir xil fikr: kamdan-kam ishlatiladigan narsa
 * har kuni ko'zga tashlanib turmasligi kerak.
 */
export function SystemSettingsSection() {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const settings = useSystemSettings(true)
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

  if (settings.isLoading) {
    return <Skeleton className="h-40 w-full" />
  }

  if (settings.isError || settings.data === undefined) {
    return <Alert tone="danger">{errorMessage(problemCode(settings.error))}</Alert>
  }

  const items = settings.data.filter((item) => LABEL_KEY[item.key] !== undefined)

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
    <div className="space-y-4 px-4 py-4">
      {save.isError && <Alert tone="danger">{errorMessage(problemCode(save.error))}</Alert>}

      {items.map((item) => (
        <SettingField
          key={item.key}
          setting={item}
          label={t(LABEL_KEY[item.key]!)}
          value={draft[item.key] ?? item.value}
          onChange={(next) => setDraft((current) => ({ ...current, [item.key]: next }))}
        />
      ))}

      {/*
        Tugma FAQAT o'zgarish bo'lganda faollashadi: «saqlash» bosilib,
        hech narsa o'zgarmaganini ko'rish foydalanuvchini «saqlandimi?»
        degan savol bilan qoldirardi.
      */}
      <div className="flex justify-end">
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
