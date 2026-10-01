import { Bell, Bot, ImagePlus, KeyRound, LoaderCircle, SlidersHorizontal, Trash2 } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { problemCode } from '@/shared/api/problem-details'
import { cn } from '@/shared/lib/cn'
import {
  Alert,
  Badge,
  Button,
  Card,
  Dialog,
  EmptyState,
  IconTile,
  PageHeader,
  Skeleton,
  TextArea,
  TextField,
  type IconTone,
} from '@/shared/ui'

import { SETTINGS_MANAGE, type SystemSetting } from '../api/settings-api'
import {
  useBotLogo,
  useDeleteBotLogo,
  useSaveSettings,
  useSystemSettings,
  useUploadBotLogo,
} from '../api/settings-queries'
import { ApiKeysSection } from './api-keys-section'
import { McpSection } from './mcp-section'

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
      'bot.token': 'settings.system.botToken',
      'bot.about.cyrillic': 'settings.system.botAboutCyrillic',
      'bot.about.latin': 'settings.system.botAboutLatin',
      'bot.about.russian': 'settings.system.botAboutRussian',
      'bot.contacts.cyrillic': 'settings.system.botContactsCyrillic',
      'bot.contacts.latin': 'settings.system.botContactsLatin',
      'bot.contacts.russian': 'settings.system.botContactsRussian',
      'bot.location': 'settings.system.botLocation',
      'bot.interview.phone': 'settings.system.botInterviewPhone',
      'bot.interview.address': 'settings.system.botInterviewAddress',
      'bot.logo': 'settings.system.botLogo',
      'bot.social.instagram': 'settings.system.botInstagram',
      'bot.social.telegram': 'settings.system.botTelegram',
      'bot.social.facebook': 'settings.system.botFacebook',
      'bot.social.website': 'settings.system.botWebsite',
      'bot.offerUrl': 'settings.system.botOfferUrl',
      'bot.conversationTimeoutMinutes': 'settings.system.botConversationTimeout',
      /*
        ⭐ Qayta ariza muddati — kaliti `recruiting.*`, o'rni esa BOT
        guruhida va bu ataylab.

        Guruh — KO'RINISH masalasi (yuqoridagi izoh), ya'ni savol
        «kalit qaysi domenniki?» emas, «foydalanuvchi buni qayerdan
        qidiradi?». Qoida faqat BOTDA ishlaydi: HR qo'lda kiritganda
        hech qanday to'siq yo'q. Uni alohida «Rekruting» guruhiga
        chiqarish bitta maydonli guruh hosil qilardi va HR bot
        qoidalarini ikki joydan qidirardi — «suhbat muddati» esa
        allaqachon shu yerda turibdi.
      */
      'recruiting.reapplyMonths': 'settings.system.reapplyMonths',
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
      /*
        Suhbatdan oldingi ogohlantirish — guruhdagi yagona SOATLI
        qiymat, qolganlari kun bilan o'lchanadi. Nega baribir shu
        guruhda: guruh — «qo'ng'iroqcha nimani, qachon ko'rsatadi»
        degan savol, o'lchov birligi emas. Uni alohida guruhga
        chiqarish bitta maydonli guruh hosil qilardi va HR bitta
        qo'ng'iroqchaning sozlamalarini ikki joydan qidirardi
        (`recruiting.reapplyMonths` dagi bilan bir xil fikr, teskari
        tomondan).
      */
      'notifications.interviewWarningHours': 'settings.system.interviewWarningHours',
      'notifications.readRetentionDays': 'settings.system.readRetentionDays',
    },
  },
]

/**
 * Bir qatorli matn maydonlari — kalit va uning o'z izohi.
 *
 * Matnli sozlama SUKUT BO'YICHA sakkiz qatorli maydonga tushadi, chunki
 * ularning deyarli hammasi bot xabari: sarlavha, bo'sh qator, tavsif.
 * Xarita nuqtasi esa bitta qisqa qiymat («40.505852, 70.930622») va u
 * katta maydonda adashib ketardi — HR u yerga manzil YOZIB qo'yishi
 * mumkin edi, holbuki kerak bo'lgani ikkita son.
 *
 * Xarita ro'yxatda: turini SERVER hal qilmaydi, chunki server uchun
 * ikkalasi ham matn. Bu ko'rinish masalasi va u shu yerda qoladi —
 * `GROUPS` dagi bilan bir xil fikr.
 */
const SINGLE_LINE_TEXT: Readonly<Record<string, string>> = {
  'bot.location': 'settings.system.botLocationHint',
  /*
    Suhbat taklifining ikkita qatori: telefon va manzil YORLIG'I.
    Ikkalasi ham bitta qisqa qiymat — «Kontaktlar» matnidan farqi
    aynan shunda (sabab `SystemSettingKeys.BotInterviewPhone` izohida).
  */
  'bot.interview.phone': 'settings.system.botInterviewPhoneHint',
  'bot.interview.address': 'settings.system.botInterviewAddressHint',
  'bot.social.instagram': 'settings.system.botLinkHint',
  'bot.social.telegram': 'settings.system.botLinkHint',
  'bot.social.facebook': 'settings.system.botLinkHint',
  'bot.social.website': 'settings.system.botLinkHint',
  /*
    Ommaviy oferta havolasi — qolgan havolalar bilan BIR XIL chiziladi
    (bir qatorli maydon), lekin izohi boshqa: bu ijtimoiy tarmoq emas,
    anketaning oxirgi qadami. Bo'sh qoldirilsa qadam umuman
    ko'rsatilmaydi va matn ham aynan shuni aytadi.
  */
  'bot.offerUrl': 'settings.system.botOfferUrlHint',
}

/**
 * Sonli sozlamaning O'Z izohi — chegara matnidan TASHQARI.
 *
 * NEGA kerak: `Number` maydonining izohi sukut bo'yicha faqat chegarani
 * aytadi («1 dan 120 gacha»), ya'ni son NIMA qilishini tushuntirmaydi.
 * Ko'pchilik sonli sozlamada bu yetarli — yorlig'ining o'zi javob
 * beradi («Sinov muddati ogohlantirishi (kun)»). «Qayta ariza muddati»
 * esa bunday emas: uning ta'siri boshqa joyda ko'rinadi (bot yangi
 * anketani rad etadi) va yorliqdan buni bilib bo'lmaydi.
 *
 * Xarita `SINGLE_LINE_TEXT` bilan bir xil fikr: turini SERVER hal
 * qilmaydi, chunki server uchun ikkalasi ham son. Bu ko'rinish masalasi
 * va u shu yerda qoladi.
 */
const NUMBER_HINT: Readonly<Record<string, string>> = {
  'recruiting.reapplyMonths': 'settings.system.reapplyMonthsHint',
  /*
    Suhbat ogohlantirishi — xuddi shu holat. Yorlig'i («Suhbatdan
    oldin ogohlantirish (soat)») son NIMAGA ta'sir qilishini
    aytmaydi: ogohlantirish qo'ng'iroqchada chiqadi va u nomzod
    suhbatga kelishidan oldin BIR MARTA ko'rinadi. Qo'shni
    maydonlarda («Sinov muddati ogohlantirishi (kun)») izoh yo'q va
    kerak ham emas — u yerda yorliqning o'zi to'liq javob beradi.
  */
  'notifications.interviewWarningHours': 'settings.system.interviewWarningHoursHint',
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

  /**
   * O'chirish tasdig'i kutilayotgan SIR.
   *
   * Nega alohida holat va nega umuman tasdiq: sirni o'chirish
   * QAYTARIB BO'LMAYDI — qiymat serverdan hech qachon o'qilmagan, ya'ni
   * uni tiklashning yagona yo'li @BotFather ga qaytadan borish.
   * Tasodifan bosilgan tugma botni to'xtatib qo'yardi.
   */
  const [clearing, setClearing] = useState<SystemSetting | null>(null)

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

  /*
    O'zgargan sozlamalar — faqat shular yuboriladi.

    ⚠️ SIR uchun qoida BOSHQACHA va bu ataylab. Uning `value` si serverdan
    har doim bo'sh keladi, ya'ni oddiy solishtiruv («yozilgani kelganidan
    farq qiladimi?») bo'sh maydonni ham «o'zgargan» deb hisoblab qolardi —
    server esa bo'sh sirni O'CHIRISH deb tushunadi. Natijada bot matnini
    tahrirlab «Saqlash» bosgan odam tokenni ham birga o'chirib yuborardi.

    Shu sababli sir uchun yagona mezon: maydonga HAQIQATAN nimadir
    yozilganmi. Bo'sh yoki faqat bo'shliqdan iborat qiymat — «tegmadim».
    O'chirish alohida tugma orqali ketadi (`confirmClear`).
  */
  const changed = items.filter((item) =>
    item.kind === 'Photo'
      /*
        RASM bu ro'yxatga HECH QACHON tushmaydi. U «Saqlash» tugmasidan
        mustaqil: fayl tanlangan zahoti yuklanadi va server uni umumiy
        so'rov orqali o'zgartirishga umuman ruxsat bermaydi
        («sozlama.qolda_ozgarmaydi»). Bu yerda tekshirilmasa, rasmi bor
        tizimda «Saqlash» har safar shu xato bilan yiqilardi.
      */
      ? false
      : item.kind === 'Secret'
        ? (draft[item.key] ?? '').trim().length > 0
        : (draft[item.key] ?? item.value) !== item.value,
  )

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

  /*
    Sirni o'chirish — FORMADAN TASHQARI amal.

    Nega «Saqlash» bilan emas: sir maydoni har doim bo'sh boshlanadi
    (qiymat serverdan kelmaydi), ya'ni «bo'sh qoldirib saqlash» =
    «tegmadim» degani. Agar bo'shlik «o'chirish» deb tushunilsa, bot
    matnini tahrirlab «Saqlash» bosgan odam tokenni ham birga
    o'chirib yuborardi. Shu sababli o'chirish ALOHIDA tugma va u
    darhol, o'z so'rovi bilan bajariladi.
  */
  const confirmClear = () => {
    if (clearing === null) return

    save.mutate([{ key: clearing.key, value: '' }], {
      onSuccess: () => {
        setClearing(null)
        setDraft({})
      },
    })
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
                    onClear={() => setClearing(item)}
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

          {/*
            Tashqi API kalitlari — FORMADAN TASHQARI bo'lim, shu sababli
            «Saqlash» tugmasidan KEYIN turadi.

            Joyi ataylab shunday: tugma yuqoridagi guruhlarni saqlaydi va
            o'sha yo'lning oxirida qolishi kerak (tugma yonidagi izoh).
            Kalit esa sozlama emas, YOZUV: u o'z oynasi bilan yaratiladi
            va bekor qilinadi, «Saqlash» ga hech qanday aloqasi yo'q —
            bo'limni tugmadan yuqoriga qo'ysak, tugma unga tegishli bo'lib
            ko'rinardi (logotip maydonidagi bilan bir xil fikr).
          */}
          <ApiKeysSection />

          {/*
            AI ulanish (MCP) — «Tashqi API» ostida (SPEC-mcp §8). U ham
            formadan TASHQARI: o'chirgich darhol saqlanadi, ulanishlar esa
            o'z oynasi bilan uziladi — «Saqlash» ga aloqasi yo'q.
          */}
          <McpSection />
        </div>
      )}

      {/* ───────── Sirni o'chirishni tasdiqlash ───────── */}
      <Dialog
        open={clearing !== null}
        onClose={() => setClearing(null)}
        size="sm"
        dismissible={!save.isPending}
        title={t('settings.system.clearTitle')}
        footer={
          <>
            <Button type="button" onClick={() => setClearing(null)} disabled={save.isPending}>
              {t('common.cancel')}
            </Button>

            <Button
              type="button"
              variant="danger"
              onClick={confirmClear}
              disabled={save.isPending}
            >
              {save.isPending ? <LoaderCircle className="animate-spin" /> : <Trash2 />}
              {t('common.delete')}
            </Button>
          </>
        }
      >
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('settings.system.clearText')}
        </p>
      </Dialog>
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
  onClear,
}: {
  setting: SystemSetting
  label: string
  value: string
  onChange: (next: string) => void
  /** Sirni o'chirish so'raldi. Sir bo'lmagan maydonda chaqirilmaydi. */
  onClear: () => void
}) {
  const { t, num } = useLocale()

  if (setting.kind === 'Secret') {
    return <SecretField setting={setting} label={label} value={value} onChange={onChange} onClear={onClear} />
  }

  if (setting.kind === 'Photo') {
    return <LogoField label={label} storedKey={setting.value} />
  }

  if (setting.kind === 'Number') {
    /*
      Izoh ikki bo'lakdan: sozlamaning MA'NOSI (bo'lsa) va chegara.
      Tartib shunday, chunki chegara faqat ma'no tushunilgandan keyin
      foyda beradi. Bo'lagi yo'q bo'lsa maydon izohsiz qoladi — bo'sh
      satr chizish yorliq bilan maydon orasini bekorga uzaytirardi.
    */
    const explanation = NUMBER_HINT[setting.key]
    const parts = [
      explanation === undefined ? null : t(explanation),
      setting.min !== null && setting.max !== null
        ? t('settings.system.range', { min: num(setting.min), max: num(setting.max) })
        : null,
    ].filter((part): part is string => part !== null)

    return (
      <TextField
        label={label}
        type="number"
        min={setting.min ?? undefined}
        max={setting.max ?? undefined}
        hint={parts.length === 0 ? undefined : parts.join(' · ')}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    )
  }

  if (SINGLE_LINE_TEXT[setting.key] !== undefined) {
    return (
      <TextField
        label={label}
        hint={t(SINGLE_LINE_TEXT[setting.key]!)}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    )
  }

  return (
    <TextArea
      label={label}
      /*
        Sakkiz qator — matnning HAQIQIY shakliga qarab olingan. Bot
        matni bir bo'lak emas: sarlavha, bo'sh qator, tavsif, yana bo'sh
        qator va manzil — ya'ni beshtadan kam bo'lmaydi. To'rt qatorda
        u har doim aylanib turardi va HR o'zi yozgan matnni butunicha
        ko'rolmasdi.
      */
      rows={8}
      hint={t('settings.system.textHint')}
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  )
}

/**
 * Logotip maydoni — rasmni ko'rsatadi, almashtiradi va o'chiradi.
 *
 * <b>Nega «Saqlash» tugmasiga bog'lanmagan.</b> Qolgan sozlamalar
 * matn: ular formada yig'iladi va birga yuboriladi. Rasm esa FAYL —
 * uni formada «yig'ib turish» degani baytlarni brauzer xotirasida
 * ushlab turish va foydalanuvchini «saqladimmi?» degan savol oldida
 * qoldirish bo'lardi. Shu sababli fayl tanlangan zahoti ketadi va
 * natija darhol ko'rinadi.
 *
 * Server ham shu qoidani QO'LLAB TURADI: `Photo` turidagi sozlamani
 * umumiy so'rov bilan o'zgartirib bo'lmaydi
 * (`sozlama.qolda_ozgarmaydi`).
 */
function LogoField({ label, storedKey }: { label: string; storedKey: string }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const input = useRef<HTMLInputElement>(null)

  const logo = useBotLogo(storedKey)
  const upload = useUploadBotLogo()
  const remove = useDeleteBotLogo()

  /*
    Blob'dan manzil YASALADI va komponent yo'qolganda BO'SHATILADI.
    Bo'shatilmasa, har bir yangi rasm brauzer xotirasida qolib
    ketardi — sozlamalar sahifasi ochiq turadigan joy va rasm bir
    necha marta almashtirilishi mumkin.
  */
  const [preview, setPreview] = useState<string | null>(null)

  useEffect(() => {
    if (!(logo.data instanceof Blob)) {
      setPreview(null)
      return
    }

    const url = URL.createObjectURL(logo.data)
    setPreview(url)

    return () => URL.revokeObjectURL(url)
  }, [logo.data])

  const busy = upload.isPending || remove.isPending
  const failure = upload.error ?? remove.error

  return (
    <div className="space-y-2">
      <p className="text-footnote font-medium text-[var(--fg-secondary)]">{label}</p>

      <div className="flex items-center gap-4">
        <span
          className={cn(
            'grid size-20 shrink-0 place-items-center overflow-hidden rounded-xl',
            'border border-[var(--border)] bg-[var(--bg-sunken)]',
          )}
        >
          {preview !== null ? (
            <img src={preview} alt="" className="size-full object-cover" />
          ) : (
            <ImagePlus className="size-6 text-[var(--fg-tertiary)]" aria-hidden />
          )}
        </span>

        <div className="flex flex-wrap items-center gap-2">
          <input
            ref={input}
            type="file"
            /*
              Serverdagi ro'yxat bilan bir xil (`PhotoLimits`). Bu
              tanlash oynasini toraytiradi, LEKIN tekshiruv emas:
              haqiqiy tekshiruv serverda, fayl MAZMUNI bo'yicha.
            */
            accept="image/jpeg,image/png,image/webp"
            className="hidden"
            onChange={(event) => {
              const file = event.target.files?.[0]

              // Maydon TOZALANADI: aks holda o'sha faylni ikkinchi
              // marta tanlash `change` hodisasini umuman bermasdi.
              event.target.value = ''

              if (file !== undefined) upload.mutate(file)
            }}
          />

          <Button
            variant="secondary"
            disabled={busy}
            onClick={() => input.current?.click()}
          >
            {preview !== null ? t('settings.system.botLogoReplace') : t('settings.system.botLogoUpload')}
          </Button>

          {preview !== null && (
            <Button variant="ghost" disabled={busy} onClick={() => remove.mutate()}>
              <Trash2 className="size-4" aria-hidden />
              {t('common.delete')}
            </Button>
          )}

          {busy && <LoaderCircle className="size-4 animate-spin text-[var(--fg-tertiary)]" aria-hidden />}
        </div>
      </div>

      <p className="text-caption-1 text-[var(--fg-tertiary)]">{t('settings.system.botLogoHint')}</p>

      {failure !== null && (
        <Alert tone="danger">{errorMessage(problemCode(failure))}</Alert>
      )}
    </div>
  )
}

/**
 * Sir maydoni — token va kalitlar uchun.
 *
 * <b>Nega oddiy matn maydoni emas.</b> Uchta farq bor va uchalasi ham
 * serverning qoidasidan kelib chiqadi:
 *
 * <b>1. Maydon HAR DOIM bo'sh boshlanadi.</b> Qiymat serverdan
 * kelmaydi (`value` bo'sh) — ya'ni bu «tahrirlash» emas, «yangisini
 * qo'yish». Shu sababli joriy holat maydonning ICHIDA emas, ustida
 * ko'rsatiladi: niqoblangan ko'rinish yoki «kiritilmagan» belgisi.
 *
 * <b>2. Bo'sh qoldirish — «tegmadim».</b> Boshqa maydonlarda bo'sh
 * qiymat saqlanadi, bu yerda esa e'tiborga olinmaydi. Aks holda bot
 * matnini tahrirlab «Saqlash» bosgan odam tokenni ham o'chirib
 * yuborardi. O'chirish — alohida tugma.
 *
 * <b>3. `type="password"`.</b> Ekran ulashilayotgan yoki orqada odam
 * turgan bo'lishi mumkin; token esa bir marta ko'rinsa bas.
 * «Ko'rsatish» tugmasi ATAYLAB yo'q — u faqat endigina yozilgan matnni
 * ko'rsatardi, saqlangan qiymatni esa baribir hech kim ochib
 * ko'rolmaydi.
 */
function SecretField({
  setting,
  label,
  value,
  onChange,
  onClear,
}: {
  setting: SystemSetting
  label: string
  value: string
  onChange: (next: string) => void
  onClear: () => void
}) {
  const { t } = useLocale()

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
        <span className="flex items-center gap-2 text-footnote font-medium text-[var(--fg-secondary)]">
          <KeyRound className="size-4 text-[var(--fg-tertiary)]" />
          {label}
        </span>

        {setting.isSet ? (
          <>
            {/*
              Niqoblangan ko'rinish — «qaysi token turibdi?» degan
              savolning yagona javobi. `tabular` sinfi: raqamlar bir xil
              kenglikda bo'lsa, ikki tokenni ko'z bilan solishtirish
              osonlashadi.
            */}
            <Badge tone="success">{t('settings.system.secretSet')}</Badge>
            <code className="tabular text-caption1 text-[var(--fg-tertiary)]">
              {setting.preview}
            </code>

            <button
              type="button"
              onClick={onClear}
              className="press ml-auto text-caption1 font-medium text-ios-red hover:underline"
            >
              {t('common.delete')}
            </button>
          </>
        ) : (
          <Badge tone="neutral">{t('settings.system.secretEmpty')}</Badge>
        )}
      </div>

      <TextField
        type="password"
        /*
          Brauzerning parol menejeri bu maydonga aralashmasligi kerak:
          u tizim sozlamasi, foydalanuvchining hisob ma'lumoti emas.
          Saqlab qo'yilsa, keyingi safar kirish formasiga taklif
          qilinib chiqardi.
        */
        autoComplete="off"
        maxLength={setting.maxLength ?? undefined}
        placeholder={setting.isSet
          ? t('settings.system.secretReplace')
          : t('settings.system.secretEnter')}
        hint={t('settings.system.botTokenHint')}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  )
}
