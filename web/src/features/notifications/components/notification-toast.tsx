import { useNavigate } from '@tanstack/react-router'
import { X } from 'lucide-react'
import { useCallback } from 'react'
import { Toaster, toast } from 'sonner'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'

import type { AppNotification } from '../api/notifications-api'
import { useNotificationStream } from '../api/notifications-stream'
import { navigateToNotification } from '../lib/notification-navigation'
import {
  SEVERITY_LOOK,
  TYPE_ICON,
  notificationTextKey,
  notificationValues,
} from '../lib/notification-presentation'

/**
 * Bir vaqtda ekranda nechta xabar turadi.
 *
 * Uchtadan ortig'i ekranning yarmini yopib, HR ning ishiga xalaqit
 * berardi — holbuki butun g'oyaning maqsadi teskarisi. Qolganlari
 * yo'qolmaydi: ular qo'ng'iroq ostidagi ro'yxatda turadi va sanoq
 * ularni ham hisoblaydi.
 */
const VISIBLE_TOASTS = 3

/**
 * Xabar ekranda necha millisekund turadi.
 *
 * 3 soniya — foydalanuvchi belgilagan muddat. Taymer HAR BIR xabar
 * uchun alohida ketadi: ketma-ket tushgan xabarlar birga emas, qaysi
 * biri qachon kelgan bo'lsa, o'sha tartibda birin-ketin o'chadi.
 *
 * Sichqoncha ustiga kelsa sonner taymerni O'ZI to'xtatadi — o'qiyotgan
 * odam xabarni yo'qotmaydi. Ma'lumotning o'zi baribir yo'qolmaydi: u
 * qo'ng'iroq ostidagi ro'yxatda qoladi.
 */
const TOAST_MS = 3_000

/**
 * Jonli bildirishnomalar: oqim, xabar (toast) va uning ko'rinishi.
 *
 * ⭐ <b>Nima uchun qo'shildi.</b> Foydalanuvchi ko'rsatgan nosozlik:
 * «HR ishlab o'tirganda bot orqali ariza tushsa, u buni bilmaydi —
 * faqat sahifani yangilagandan keyin ko'radi». Endi xabar ekranning
 * o'ng tepasidan, qo'ng'iroq tugmasining tagidan tushadi va
 * qo'ng'iroqdagi son o'sha zahoti o'zgaradi.
 *
 * ⭐ <b>Uchta qat'iy qoida, ularning har biri foydalanuvchi talabidan.</b>
 *
 * <b>1. Sahifa YANGILANMAYDI.</b> Na `window.location.reload`, na
 * umumiy `invalidateQueries`. Oqim faqat ikkita bildirishnoma keshini
 * to'g'ridan-to'g'ri yozadi (`useNotificationStream` izohi), ya'ni
 * to'ldirilayotgan forma, ochilgan oyna va yozilayotgan matn joyida
 * qoladi.
 *
 * <b>2. Xabar FOKUSNI o'g'irlamaydi.</b> `Toaster` ning o'zi modal
 * emas: u ostidagi ekran bosiladigan holicha qoladi va klaviatura
 * fokusi ko'chmaydi. HR yozayotgan bo'lsa, harflar o'z joyiga
 * tushaveradi.
 *
 * <b>3. Xabar YO'QOLSA ham ma'lumot yo'qolmaydi.</b> U 3 soniyada
 * o'zi ketadi, lekin bildirishnomaning o'zi qo'ng'iroq ostidagi
 * ro'yxatda va sanoqda qoladi.
 *
 * <b>Nega `sonner`.</b> U loyihaning bog'liqliklarida allaqachon bor
 * edi (ishlatilmagan holda) va tayyor yechadigan narsalar ro'yxati
 * uzun: navbat, taymerni sichqoncha ostida to'xtatish, surib yuborish,
 * `aria-live`, `prefers-reduced-motion` va ekran o'lchamiga moslashish.
 * Ko'rinish esa BUTUNLAY bizniki — `toast.custom` bilan JSX o'zimiz
 * beramiz, ya'ni sonner'ning uslubidan bir piksel ham kirmaydi.
 */
export function LiveNotifications() {
  const navigate = useNavigate()
  const { isAuthenticated } = useAuth()
  const { t, num, date } = useLocale()

  const show = useCallback(
    (items: AppNotification[]) => {
      /*
        Uchtadan ortig'i BITTA yig'ma xabar bo'lib chiqadi. Nega:
        o'nta ariza birdan tushsa (masalan bot bir necha soat
        ishlamay turib, keyin ulangan bo'lsa), o'nta xabar butun
        ekranni yopardi.
      */
      const detailed = items.slice(0, VISIBLE_TOASTS)
      const rest = items.length - detailed.length

      for (const item of detailed) {
        const key = notificationTextKey(item)

        toast.custom(
          (id) => (
            <ToastCard
              item={item}
              /*
                Ikki qatorli, xuddi telefondagi bildirishnomadek:
                tepada TUR («Botdan ariza tushdi»), ostida TAFSILOT
                («Ism — bugun ariza topshirdi»). Faqat turi
                ko'rsatilsa, HR kartani ochmasdan kim haqidaligini
                bilmasdi; faqat tafsilot ko'rsatilsa, bir qarashda
                nima bo'lganini tushunish qiyinlashardi.
              */
              title={t(`notifications.type.${key}.title`)}
              text={t(`notifications.type.${key}.text`, notificationValues(item, { num, date }))}
              onOpen={() => {
                toast.dismiss(id)
                navigateToNotification(navigate, item)
              }}
              onClose={() => toast.dismiss(id)}
            />
          ),
          { duration: TOAST_MS },
        )
      }

      if (rest > 0) {
        /*
          Yig'ma karta. Ikona uchun ro'yxatning KEYINGI yozuvi olinadi —
          u ko'rsatilmagan xabarlarning birinchisi, ya'ni ikona hech
          bo'lmasa nima haqidaligiga ishora qiladi. Bosilganda hech
          qayerga o'tmaydi (qaysi biriga?), shunchaki yopiladi —
          ro'yxatning o'zi qo'ng'iroq ostida.
        */
        const next = items[detailed.length]!

        toast.custom(
          (id) => (
            <ToastCard
              item={next}
              title={t('notifications.moreArrived', { n: num(rest) })}
              onOpen={() => toast.dismiss(id)}
              onClose={() => toast.dismiss(id)}
            />
          ),
          { duration: TOAST_MS },
        )
      }
    },
    [navigate, t, num, date],
  )

  useNotificationStream(isAuthenticated, show)

  return (
    <Toaster
      /*
        O'NG TEPA burchak — qo'ng'iroq tugmasining tagi.

        Nega o'rtada emas: xabar sahifaning o'rtasidan tushganda u
        ishlanayotgan mazmunning ustiga tushardi va «bu qayerdan
        keldi» degan savolni tug'dirardi. Qo'ng'iroqning tagida esa
        javob ko'rinib turibdi: xabar o'sha ro'yxatga tushdi, sanoq
        ham o'sha yerda o'zgardi. Ko'z xabardan tugmagacha bir
        harakat bilan boradi.

        O'rin `globals.css` dagi `--toast-top` / `--toast-inset` bilan
        beriladi — ikkinchisi yuqori panelning yon to'ldirmasiga
        tekislangan, ya'ni xabarning o'ng qirrasi qo'ng'iroq va
        foydalanuvchi menyusining qirrasi bilan bir chiziqda.
      */
      position="top-right"
      offset={{ top: 'var(--toast-top)', right: 'var(--toast-inset)' }}
      mobileOffset={{
        top: 'var(--toast-top)',
        right: 'var(--toast-inset)',
        left: 'var(--toast-inset)',
      }}
      /*
        TAGMA-TAG. `expand` bo'lmasa sonner xabarlarni bir-birining
        USTIGA yig'ib, faqat eng oxirgisini ko'rsatadi — qolganlari
        sichqoncha ustiga kelgandagina ochiladi. Ketma-ket tushgan
        arizalar shunda ko'rinmay qolardi: HR ekranda bitta xabar
        turganini ko'rib, aslida uchtasi kelganini bilmasdi.
      */
      expand
      /*
        Uchta tafsilotli karta USTIGA yig'ma karta ham chiqishi
        mumkin (pastdagi `rest`), shuning uchun +1. Aks holda sonner
        yig'ma kartani chiqarish uchun tafsilotli kartalardan birini
        siqib chiqarardi.
      */
      visibleToasts={VISIBLE_TOASTS + 1}
      /*
        `unstyled` — sonner'ning O'Z ko'rinishi butunlay o'chiriladi.
        Usiz uning oq karta va soyasi bizning kartamiz ostidan
        ko'rinib turardi va tungi rejimda ayniqsa ko'zga tashlanardi.
      */
      toastOptions={{ unstyled: true, classNames: { toast: 'w-full' } }}
      gap={10}
    />
  )
}

/**
 * Bitta xabar kartasi — iOS bildirishnomasining shakli.
 *
 * Butun karta bosiladi (o'sha ekranga o'tadi), o'ng chetdagi kichik
 * tugma esa faqat yopadi. Nega ikkitasi: xabarni «o'qidim, kerak
 * emas» deb yubormoqchi bo'lgan odam uni yopish uchun o'sha ekranga
 * o'tishga majbur bo'lmasligi kerak.
 */
function ToastCard({
  item,
  title,
  text,
  onOpen,
  onClose,
}: {
  item: AppNotification
  /** Turning nomi — qalin birinchi qator. */
  title: string
  /** Tafsilot. Yig'ma kartada berilmaydi. */
  text?: string
  onOpen: () => void
  onClose: () => void
}) {
  const { t } = useLocale()

  const Icon = TYPE_ICON[item.type]
  const look = SEVERITY_LOOK[item.severity]

  return (
    <div
      /*
        `role="status"` + `aria-live="polite"`: ekran o'quvchi matnni
        o'qiydi, LEKIN foydalanuvchining joriy ishini bo'lmaydi —
        u navbatdagi tanaffusda aytiladi. `assertive` bu yerda
        noto'g'ri bo'lardi: bildirishnoma muhim, lekin shoshilinch
        emas va yozayotgan odamni to'xtatishga arzimaydi.
      */
      role="status"
      aria-live="polite"
      className={cn(
        'flex w-full items-start gap-3 rounded-lg border p-3.5',
        'border-[var(--border)] bg-[var(--bg-elevated)] shadow-floating',
        // Tepadan tushish — `globals.css` dagi `toast-drop`.
        'toast-drop',
      )}
    >
      <span
        aria-hidden
        className="grid size-9 shrink-0 place-items-center rounded-md bg-[var(--bg-sunken)]"
      >
        <Icon className={cn('size-[18px]', look.icon)} />
      </span>

      {/*
        Matn tugma ICHIDA: butun karta bosiladigan bo'lsa ham, bosish
        maydonini `<div onClick>` bilan yasash klaviatura va ekran
        o'quvchi uchun ishlamasdi.
      */}
      <button
        type="button"
        onClick={onOpen}
        className="press min-w-0 flex-1 text-left"
      >
        <p className="truncate-1 text-subhead font-semibold">{title}</p>
        {text !== undefined && (
          <p className="truncate-2 mt-0.5 text-footnote text-[var(--fg-secondary)]">{text}</p>
        )}
      </button>

      <button
        type="button"
        onClick={onClose}
        aria-label={t('common.close')}
        className={cn(
          'press -m-1 grid size-7 shrink-0 place-items-center rounded-md',
          'text-[var(--fg-tertiary)] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)]',
        )}
      >
        <X className="size-4" />
      </button>
    </div>
  )
}
