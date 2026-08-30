import { useNavigate } from '@tanstack/react-router'
import { BellOff, CheckCheck, LoaderCircle, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Checkbox, Dialog, EmptyState, Skeleton } from '@/shared/ui'

import type { AppNotification } from '../api/notifications-api'
import {
  useDeleteNotifications,
  useMarkNotificationsRead,
  useNotifications,
} from '../api/notifications-queries'
import { notificationErrorCode } from '../lib/notification-presentation'
import { navigateToNotification } from '../lib/notification-navigation'
import { NotificationRow } from './notification-row'

/** Modul darajasida — har render'da yangi massiv keraksiz qayta hisoblash beradi. */
const EMPTY: AppNotification[] = []

/** Skelet qatorlari soni: modalning ko'rinadigan qismiga sig'adigan miqdor. */
const SKELETON_ROWS = [0, 1, 2, 3]

/**
 * Bildirishnomalar oynasi.
 *
 * <b>Nega modal, alohida sahifa emas.</b> Bildirishnoma — ishning O'ZI
 * emas, unga ishora. HR uni ochib, «shu nima ekan» deb qarab, kerakli
 * ekranga o'tadi yoki yopib, qilayotgan ishini davom ettiradi. Alohida
 * marshrut har safar kontekstni yo'qotardi va «orqaga» tugmasini
 * ro'yxatning bir qismiga aylantirardi (`settings-dialog.tsx` dagi bilan
 * bir xil sabab).
 *
 * <b>Minimalistik nimani anglatadi.</b> Ekranda faqat to'rt narsa bor:
 * belgilash, ro'yxat, ikkita amal. Filtr yo'q, saralash yo'q, sahifalash
 * yo'q — tartibni server allaqachon to'g'ri qo'yadi (muhimligi bo'yicha
 * kamayish, keyin sana), ro'yxat esa amalda o'nlab yozuvdan oshmaydi.
 *
 * <b>Ruxsat tekshirilmaydi</b> va bu ataylab: endpoint faqat
 * autentifikatsiya talab qiladi, MAZMUN esa serverda filtrlanadi
 * («recruiting.view» yo'q odamga nomzod yozuvlari umuman kelmaydi).
 * Klientda ikkinchi chegara qo'yish ikkita qoida hosil qilardi va ular
 * ertami-kechmi ajralib ketardi (`nav-items.ts` dagi bilan teskari
 * holat: u yerda sahifa bor, bu yerda faqat ro'yxat mazmuni).
 */
export function NotificationsDialog({
  open,
  onClose,
}: {
  open: boolean
  onClose: () => void
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()

  const list = useNotifications(open)
  const markRead = useMarkNotificationsRead()
  const remove = useDeleteNotifications()

  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set())
  const [confirming, setConfirming] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const items = list.data?.items ?? EMPTY
  const total = list.data?.total ?? 0
  const unread = list.data?.unreadCount ?? 0

  /*
   * Oyna yopilganda holat noldan boshlanadi.
   *
   * NEGA kerak: tanlov «shu safar nimani o'chirmoqchi edim» degan
   * qisqa muddatli niyat. U saqlanib qolsa, ertaga oynani ochgan HR
   * allaqachon belgilangan beshta qatorni ko'rardi va «o'chirish»
   * tugmasi faol turardi — bu qaytarib bo'lmaydigan amal uchun eng
   * xavfli boshlang'ich holat.
   */
  useEffect(() => {
    if (open) return

    setSelected(new Set())
    setConfirming(false)
    setFailure(null)
  }, [open])

  /*
   * Tanlanganlarning RO'YXATDA TURGANLARI.
   *
   * NEGA `selected` to'g'ridan-to'g'ri ishlatilmaydi: ro'yxat oyna ochiq
   * turganda ham yangilanadi (boshqa foydalanuvchi o'chirgan yoki shart
   * yo'qolgan bo'lishi mumkin). U holda `selected` da endi mavjud
   * bo'lmagan kalit qolib ketardi va «3 ta tanlandi» yozuvi ekranda
   * ikkita qator bilan turardi. Ro'yxat bo'yicha filtrlash bu holatni
   * o'z-o'zidan to'g'rilaydi — alohida tozalash effekti kerak emas.
   */
  const selectedKeys = useMemo(
    () => items.filter((item) => selected.has(item.key)).map((item) => item.key),
    [items, selected],
  )

  const busy = markRead.isPending || remove.isPending
  const allSelected = items.length > 0 && selectedKeys.length === items.length

  const toggleAll = (checked: boolean) => {
    setSelected(checked ? new Set(items.map((item) => item.key)) : new Set())
  }

  const toggleOne = (key: string, checked: boolean) => {
    setSelected((previous) => {
      const next = new Set(previous)
      if (checked) next.add(key)
      else next.delete(key)
      return next
    })
  }

  /*
   * ⭐ Kesh QO'LDA bekor qilinmaydi: `providers.tsx` har qanday
   * muvaffaqiyatli mutatsiyadan keyin buni o'zi qiladi — ham ro'yxat,
   * ham qo'ng'iroqdagi sanoq yangilanadi.
   */
  const markAllRead = () => {
    setFailure(null)
    markRead.mutate(
      { all: true },
      { onError: (error) => setFailure(notificationErrorCode(error)) },
    )
  }

  const confirmDelete = () => {
    setFailure(null)
    remove.mutate(selectedKeys, {
      onSuccess: () => {
        setSelected(new Set())
        setConfirming(false)
      },
      onError: (error) => {
        // Tasdiqlash oynasi yopiladi: xato asosiy oynada ko'rsatiladi,
        // aks holda xabar tasdiqlash ostida ko'rinmay qolardi.
        setFailure(notificationErrorCode(error))
        setConfirming(false)
      },
    })
  }

  /**
   * Havolasi haqiqiy ekranga olib boradimi.
   *
   * `Probations` va `MissingProbations` uchun `id` shart emas — ular
   * RO'YXAT sahifasiga olib boradi. `Employee` va `Candidate` esa aniq
   * kartani ochadi va `id` siz ma'nosiz.
   */
  const canOpen = (item: AppNotification): boolean =>
    item.link.id !== null ||
    item.link.route === 'Probations' ||
    item.link.route === 'MissingProbations'

  /**
   * «Kirib ko'rish»: o'qildi deb belgilanadi, oyna yopiladi, ekran ochiladi.
   *
   * Belgilash javobi KUTILMAYDI (`mutate`, `mutateAsync` emas): o'tish
   * so'rovga bog'lansa, HR sekin tarmoqda bosgan qatorining ochilishini
   * kutib turardi — holbuki «o'qildi» belgisi uning uchun ikkinchi
   * darajali natija.
   *
   * Marshrutlar:
   *   · `Employee`           → `/xodimlar/$employeeId`
   *   · `Candidate`          → `/nomzodlar/$candidateId`
   *   · `Probations`         → `/sinov?q=<xodim ismi>`
   *   · `MissingProbations`  → `/sinov`
   *
   * ⚠️ `/sinov` da AJRATIB KO'RSATISH imkoniyati yo'q — sahifa manzildan
   * faqat qidiruv matnini (`q`) o'qiydi va unga yangi parametr qo'shish
   * `features/probations` ni o'zgartirishni talab qilardi (bu ish
   * doirasidan tashqarida). Shu sababli mavjud vosita ishlatiladi:
   * xodim ismi qidiruvga yoziladi va jadval o'sha bitta qatorga
   * qisqaradi — natija «ajratib ko'rsatish» bilan deyarli bir xil.
   * `MissingProbations` da esa qidiruv YUBORILMAYDI: «sinov yozuvi
   * yo'qlar» bloki sahifaning eng tepasida turadi va qidiruvga umuman
   * bo'ysunmaydi — matn faqat pastdagi jadvalni behuda toraytirardi.
   */
  const openNotification = (item: AppNotification) => {
    if (!item.isRead) {
      markRead.mutate(
        { keys: [item.key] },
        { onError: (error) => setFailure(notificationErrorCode(error)) },
      )
    }

    onClose()

    // O'tishning O'ZI umumiy funksiyada: aynan shu yo'l jonli xabar
    // (toast) ustidan ham bosiladi (izohi `navigateToNotification` da).
    navigateToNotification(navigate, item)
  }

  /*
   * Sarlavha ostidagi izoh bir jumlada holatni aytadi. Bo'sh ro'yxatda
   * u umuman chizilmaydi — `EmptyState` ning o'zi allaqachon gapiradi.
   */
  const description =
    total === 0
      ? undefined
      : unread > 0
        ? t('notifications.unreadSummary', { n: num(unread) })
        : t('notifications.allRead')

  return (
    <>
      <Dialog
        open={open}
        onClose={onClose}
        size="lg"
        title={t('common.notifications')}
        description={description}
        /*
          Amallar pastda: yuqorida ular sarlavha bilan bellashardi, bu
          yerda esa doim ko'rinib turadi (`footer` surilmaydi) — uzun
          ro'yxatda «hammasini o'qildi» uchun tepaga qaytish kerak emas.
          Bo'sh ro'yxatda ikkita o'chirilgan tugma ko'rsatishning ma'nosi
          yo'q, shu sababli butun qavat olib tashlanadi.
        */
        footer={
          items.length === 0 ? undefined : (
            <>
              <Button
                type="button"
                variant="ghost"
                className="mr-auto"
                onClick={markAllRead}
                disabled={unread === 0 || busy}
              >
                {markRead.isPending ? <LoaderCircle className="animate-spin" /> : <CheckCheck />}
                {t('notifications.markAllRead')}
              </Button>

              {/*
                O'chirish — qaytarib bo'lmaydigan amal, shu sababli qizil
                va TASDIQLASH orqali (sabab tasdiqlash oynasi izohida).
              */}
              <Button
                type="button"
                variant="danger"
                onClick={() => setConfirming(true)}
                disabled={selectedKeys.length === 0 || busy}
              >
                <Trash2 />
                {t('notifications.deleteSelected')}
              </Button>
            </>
          )
        }
      >
        <div className="space-y-3">
          {failure !== null && (
            <Alert tone="danger" title={t('notifications.actionFailed')}>
              {errorMessage(failure)}
            </Alert>
          )}

          {list.isError && (
            <Alert tone="danger" title={t('notifications.loadFailed')}>
              {errorMessage(notificationErrorCode(list.error))}
            </Alert>
          )}

          {list.isPending ? (
            /* Spinner emas, skelet: ro'yxat kelganda ekran sakramaydi */
            <div aria-hidden className="space-y-5 py-2">
              {SKELETON_ROWS.map((row) => (
                <div key={row} className="flex items-start gap-3">
                  <Skeleton className="mt-1 size-5" />
                  <div className="min-w-0 flex-1 space-y-2">
                    <Skeleton className="h-4 w-48" />
                    <Skeleton className="h-3 w-full" />
                  </div>
                </div>
              ))}
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={BellOff}
              title={t('notifications.emptyTitle')}
              description={t('notifications.emptyText')}
            />
          ) : (
            <>
              {/*
                «Barchasini belgilash» — YAGONA katakcha, ro'yxat ustida.

                `sticky`: uzun ro'yxatni aylantirgan HR belgilashni
                boshlash uchun tepaga qaytmaydi. `-mx-6 px-6` — oynaning
                ichki bo'shlig'ini bosib o'tib, ajratgich chekkadan
                chekkagacha chizilsin uchun.

                ⚠️ Qisman tanlangan holat («aniqlanmagan» katakcha) YO'Q:
                umumiy `Checkbox` uni qo'llab-quvvatlamaydi va unga
                tegilmaydi. O'rniga yorliq SONNI aytadi — «3 ta tanlandi»
                belgisi katakchaning yarim holatidan aniqroq.
              */}
              <div className="sticky top-0 z-10 -mx-6 border-b border-[var(--border)] bg-[var(--bg-elevated)] px-6 pb-2.5">
                <Checkbox
                  checked={allSelected}
                  onChange={toggleAll}
                  disabled={busy}
                  label={
                    selectedKeys.length === 0
                      ? t('notifications.selectAll')
                      : t('notifications.selectedCount', { n: num(selectedKeys.length) })
                  }
                />
              </div>

              <ul className="divide-y divide-[var(--border)]">
                {items.map((item) => (
                  <NotificationRow
                    key={item.key}
                    item={item}
                    selected={selected.has(item.key)}
                    onSelect={(checked) => toggleOne(item.key, checked)}
                    onOpen={canOpen(item) ? () => openNotification(item) : null}
                  />
                ))}
              </ul>

              {/*
                Chegaraga yetilgani OCHIQ aytiladi. Sahifalash yo'q
                (server qarori), ya'ni bu yerda «yana ko'rsatish» tugmasi
                ham bo'lolmaydi — jim turish esa HR ni «qolganlari
                qayerda?» degan savol bilan qoldirardi.
              */}
              {total > items.length && (
                <p className="pt-1 text-center text-caption1 text-[var(--fg-tertiary)]">
                  {t('notifications.limitNote', {
                    shown: num(items.length),
                    total: num(total),
                  })}
                </p>
              )}
            </>
          )}
        </div>
      </Dialog>

      {/*
        ───────── Tasdiqlash ─────────

        HA, o'chirishda tasdiqlash BOR. Sabab: amal qaytarib bo'lmaydi —
        server o'chirilgan bildirishnomani o'sha foydalanuvchiga BOSHQA
        HECH QACHON ko'rsatmaydi, hatto sharti hamon rost bo'lsa ham
        (sinov muddati o'tib ketgan xodim ro'yxatdan butunlay yo'qoladi).
        «Barchasini belgilash» esa bir tegishda 50 ta qatorni tanlaydi va
        undan keyingi tugma darhol yonida turadi.

        NEGA «bekor qilish» (undo) emas: uni bajarib bo'lmaydi — backend'da
        o'chirishni qaytaradigan endpoint yo'q va bu ish doirasida
        backend'ga tegilmaydi. Bajarib bo'lmaydigan va'da bermaslik uchun
        savol OLDINDAN beriladi.

        NEGA «hammasini o'qildi» da tasdiqlash YO'Q: u hech narsani
        yo'qotmaydi — yozuvlar ro'yxatda qoladi, faqat xiralashadi.
        Har bir tugmani savol bilan o'rash tasdiqlashning o'zini
        avtomatik bosiladigan bosqichga aylantirardi.
      */}
      <Dialog
        open={confirming}
        onClose={() => setConfirming(false)}
        size="sm"
        dismissible={!remove.isPending}
        title={t('notifications.confirmDeleteTitle')}
        footer={
          <>
            <Button type="button" onClick={() => setConfirming(false)} disabled={remove.isPending}>
              {t('common.cancel')}
            </Button>

            <Button
              type="button"
              variant="danger"
              onClick={confirmDelete}
              disabled={remove.isPending}
            >
              {remove.isPending && <LoaderCircle className="animate-spin" />}
              {remove.isPending ? t('notifications.deleting') : t('common.delete')}
            </Button>
          </>
        }
      >
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('notifications.confirmDeleteText', { n: num(selectedKeys.length) })}
        </p>
      </Dialog>
    </>
  )
}
