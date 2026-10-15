import { Bell } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui'

import { useUnreadCount } from '../api/notifications-queries'
import { NotificationsDialog } from './notifications-dialog'

/**
 * Nishonda ko'rsatiladigan eng katta son.
 *
 * Undan yuqorisi «99+» bo'lib qoladi: uch xonali son 16px lik doiraga
 * sig'masdi va qo'ng'iroqning o'zini surib yuborardi. Aniq son bu
 * chegaradan keyin ahamiyatini ham yo'qotadi — 100 ham, 240 ham bitta
 * xabar beradi: «ro'yxatni oching».
 */
const MAX_BADGE = 99

/**
 * Yuqori paneldagi qo'ng'iroq va uning oynasi.
 *
 * <b>Ilgari bu tugma O'LIK edi</b> va ustidagi qizil nuqta QATTIQ YOZIB
 * QO'YILGAN edi (`topbar.tsx`): hech qanday bildirishnoma bo'lmasa ham
 * doim yonib turardi. Bunday nuqta eng yomon turdagi belgi — u har kuni
 * yolg'on gapiradi va bir hafta ichida foydalanuvchi uni umuman
 * ko'rmay qo'yadi. Endi u HAQIQIY sanoqqa bog'langan va nol bo'lsa
 * umuman chizilmaydi.
 *
 * <b>Nega sanoq raqam bilan, nuqta bilan emas.</b> Bir xil narxda ikki
 * barobar ma'lumot: «bittami yoki o'ntami» degan savolga javob oynani
 * ochmasdan beriladi va HR uni ochish-ochmaslikni shu yerda hal qiladi.
 * iOS ham aynan shunday qiladi.
 *
 * <b>Nega tugma va oyna BITTA komponentda.</b> Ular bitta holatni
 * («oyna ochiqmi») bo'lishadi va u ikkalasiga ham kerak: oyna uni
 * ko'rinish uchun, tugma esa sanoqning davriy so'rovini TO'XTATISH
 * uchun (`useUnreadCount` izohiga qarang). Holatni yuqoriga —
 * `topbar.tsx` ga — chiqarish panelga u umuman ishlatmaydigan holatni
 * yuklardi (`global-search.tsx`, `user-menu.tsx` bilan bir xil qoida:
 * panel faqat joylashtiradi).
 */
export function NotificationsBell() {
  const { t, num } = useLocale()
  const [open, setOpen] = useState(false)

  const count = useUnreadCount(open)
  const unread = count.data ?? 0

  /*
    ───────── Yangi bildirishnoma kelganida harakat ─────────

    Raqamning o'zi YETMAYDI: qo'ng'iroq ekranning chekkasida turadi va
    son 3 dan 4 ga o'zgargani boshqa ish bilan band odamning ko'ziga
    umuman tashlanmaydi — o'zgarish statik.

    Animatsiya FAQAT son OSHGANDA ishga tushadi. Nega shart:

      · sahifa ochilganda o'ynasa — u xabar bermaydi, bezakka aylanadi
        va bir kunda e'tibordan qoladi (bu fayldagi «har kuni yolg'on
        gapiradigan qizil nuqta» xatosining aynan o'zi);
      · son KAMAYGANDA (o'qilgach) o'ynasa — «yangi narsa keldi» degan
        yolg'on signal bo'lardi.

    `previous` boshida `null`: birinchi javob «oshish» deb
    hisoblanmaydi, aks holda har sahifa ochilishida qo'ng'iroq
    chayqalardi.
  */
  const [ringing, setRinging] = useState(false)
  const previous = useRef<number | null>(null)

  useEffect(() => {
    // So'rov hali kelmagan: `undefined` ni nol deb hisoblab, keyin
    // haqiqiy sonni «oshish» deb o'qib yuborish mumkin emas.
    if (count.data === undefined) return

    const before = previous.current
    previous.current = count.data

    if (before === null || count.data <= before) return

    setRinging(true)

    // Animatsiya tugagach sinf OLIB TASHLANADI: aks holda keyingi
    // xabarda u qaytadan ishga tushmasdi (bir xil sinf — bir xil holat).
    const timer = window.setTimeout(() => setRinging(false), 700)
    return () => window.clearTimeout(timer)
  }, [count.data])

  return (
    <>
      <Button
        variant="ghost"
        size="icon-sm"
        onClick={() => setOpen(true)}
        title={t('common.notifications')}
        /*
          Ovozli o'qishda son ham aytiladi: ekranni ko'rmaydigan
          foydalanuvchi uchun nishonning o'zi mavjud emas.
        */
        aria-label={
          unread === 0
            ? t('common.notifications')
            : t('notifications.bellLabel', { n: num(unread) })
        }
        className="relative"
      >
        <Bell className={cn(ringing && 'bell-ring')} />

        {unread > 0 && (
          /*
            `aria-hidden`: son yuqorida `aria-label` da allaqachon
            aytilgan, ikkinchi marta o'qilishi shovqin bo'lardi.

            `ring-[var(--bg)]` — panel foni bulanadigan bo'lsa ham
            (`blur-bar`), nishon atrofidagi halqa uni qo'ng'iroq
            chizig'idan ajratib turadi (avvalgi nuqtada ham shunday edi).
          */
          <span
            aria-hidden
            className={cn(
              'tabular absolute -top-1 -right-1 grid h-4 min-w-4 place-items-center rounded-full',
              'bg-ios-red px-1 text-[10px] leading-none font-semibold text-[var(--fg-on-accent)]',
              'ring-2 ring-[var(--bg)]',
              ringing && 'badge-pulse',
            )}
          >
            {unread > MAX_BADGE ? `${MAX_BADGE}+` : num(unread)}
          </span>
        )}
      </Button>

      {/*
        Oyna DOIM chizilgan holatda turadi (ichida `open` bayrog'i bilan):
        `Dialog` yopilish animatsiyasini o'zi boshqaradi va shartli
        chizishda u umuman ko'rinmasdi — oyna «yo'qolib» qolardi.
      */}
      <NotificationsDialog open={open} onClose={() => setOpen(false)} />
    </>
  )
}
