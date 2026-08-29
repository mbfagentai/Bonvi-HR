import { ChevronRight } from 'lucide-react'
import { Fragment } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Checkbox } from '@/shared/ui'

import type { AppNotification } from '../api/notifications-api'
import {
  SEVERITY_LOOK,
  TYPE_ICON,
  notificationMeta,
  notificationTextKey,
  notificationValues,
} from '../lib/notification-presentation'

/**
 * Ro'yxatning bitta qatori.
 *
 * Tuzilishi iOS «Bildirishnomalar markazi» dagi kabi va ATAYLAB shunday
 * yassi: katakcha → ikona → matn → sana → strelka. Ichida karta ham,
 * chegara ham yo'q — qatorlar faqat ingichka ajratgich bilan bo'linadi
 * (`settings-dialog.tsx` dagi guruh qatorlari bilan bir xil his).
 *
 * <b>O'qilmagan qanday ajraladi.</b> Uch belgi birga ishlaydi: sarlavha
 * QALIN, sana yonida rangli nuqta, o'qilgani esa xiralashadi. Faqat
 * nuqta qoldirilsa, u ro'yxatning o'ng chekkasida yo'qolib ketardi;
 * faqat qalinlik qoldirilsa — bir xil qalinlikdagi qo'shni qatorlar
 * orasida sezilmasdi.
 *
 * <b>Nuqtaning RANGI — muhimlik darajasi</b> (ko'k / to'q sariq / qizil),
 * ikona esa TURNI aytadi. Ikkalasi bir-birini takrorlamaydi: sabab
 * `notification-presentation.ts` dagi `TYPE_ICON` izohida.
 */
export function NotificationRow({
  item,
  selected,
  onSelect,
  onOpen,
}: {
  item: AppNotification
  selected: boolean
  onSelect: (checked: boolean) => void
  /** «Kirib ko'rish». Havolasi ochilmaydigan qatorda BERILMAYDI. */
  onOpen: (() => void) | null
}) {
  const { t, num, money, date } = useLocale()

  const textKey = notificationTextKey(item)
  const values = notificationValues(item, { num, money, date })
  const meta = notificationMeta(item, { num, money, date })

  const look = SEVERITY_LOOK[item.severity]
  const Icon = TYPE_ICON[item.type]

  const title = t(`notifications.type.${textKey}.title`)

  return (
    <li className="flex items-start gap-3 py-2.5">
      {/*
        Katakchaning yorlig'i EKRAN O'QUVCHI uchun: ko'z bilan qaraganda
        qaysi qatorga tegishli ekani joylashuvidan ko'rinadi, ovoz bilan
        esa «belgilash katagi» dan boshqa hech narsa eshitilmasdi.
        `Checkbox` `aria-label` ni qabul qilmaydi (umumiy komponent, unga
        tegilmaydi), lekin yorliqni qabul qiladi — shu sababli yorliq
        ko'rinmas qilib beriladi (`sidebar.tsx` dagi bilan bir xil usul).
      */}
      <Checkbox
        checked={selected}
        onChange={onSelect}
        className="mt-1"
        label={<span className="sr-only">{title}</span>}
      />

      <Icon
        aria-hidden
        strokeWidth={2}
        className={cn('mt-1 size-[18px] shrink-0', look.icon, item.isRead && 'opacity-50')}
      />

      {/*
        Butun matn maydoni — BITTA tugma.

        NEGA alohida «Kirib ko'rish» yorlig'i emas: qatorda uchta
        bosiladigan joy (katakcha, yorliq, strelka) bo'lsa, tasodifan
        noto'g'risiga tegish osonlashadi va ro'yxat shovqinli ko'rinadi.
        Katta nishon — iOS'ning asosiy qoidasi; niyat esa strelka va
        `title` bilan aytiladi.

        `disabled` — havolasi yo'q kamdan-kam holat uchun (server `id` ni
        `null` qaytarsa). Bunda tugma jim qoladi: bosilib, hech qayerga
        olib bormaslikdan ko'ra bosilmagani ma'qul.
      */}
      <button
        type="button"
        onClick={onOpen ?? undefined}
        disabled={onOpen === null}
        title={onOpen === null ? undefined : t('notifications.open')}
        className={cn(
          '-mx-2 flex min-w-0 flex-1 items-start gap-2 rounded-md px-2 py-1 text-left',
          'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
          onOpen === null ? 'cursor-default' : 'press hover:bg-[var(--bg-hover)]',
        )}
      >
        <span className="min-w-0 flex-1">
          <span className="flex items-baseline gap-2">
            <span
              className={cn(
                'truncate-1 text-subhead',
                item.isRead ? 'font-medium text-[var(--fg-secondary)]' : 'font-semibold',
              )}
            >
              {title}
            </span>

            <span className="ml-auto flex shrink-0 items-center gap-1.5">
              {!item.isRead && (
                <span
                  className={cn('size-[7px] rounded-full', look.dot)}
                  title={t('notifications.unread')}
                />
              )}
              <span className="tabular text-caption1 text-[var(--fg-tertiary)]">
                {date(item.date)}
              </span>
            </span>
          </span>

          <span className="mt-0.5 block text-footnote text-[var(--fg-secondary)]">
            {t(`notifications.type.${textKey}.text`, values)}
          </span>

          {/*
            Kontekst satri: bo'lim, lavozim, vakansiya, yosh, oklad,
            manba — qaysi biri kelgan bo'lsa. Nuqta bilan ajratiladi va
            ATAYLAB eng xira rangda: bu ma'lumot, harakat emas.
          */}
          {meta.length > 0 && (
            <span className="mt-1 block text-caption1 text-[var(--fg-tertiary)]">
              {meta.map((entry, index) => (
                <Fragment key={index}>
                  {index > 0 && ' · '}
                  {entry.kind === 'text' ? entry.value : t(entry.key, entry.values)}
                </Fragment>
              ))}
            </span>
          )}
        </span>

        {onOpen !== null && (
          <ChevronRight
            aria-hidden
            className="mt-1 size-4 shrink-0 text-[var(--fg-tertiary)]"
            strokeWidth={2.5}
          />
        )}
      </button>
    </li>
  )
}
