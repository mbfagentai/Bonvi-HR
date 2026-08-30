import { useMutation, useQuery } from '@tanstack/react-query'

import {
  deleteNotifications,
  fetchNotifications,
  fetchUnreadCount,
  markNotificationsRead,
  type MarkReadInput,
} from './notifications-api'

/**
 * Bir so'rovda nechta bildirishnoma olinadi.
 *
 * Server 1..200 oralig'ini qabul qiladi, sukut — 50. Sahifalash ATAYLAB
 * yo'q (backend qarori), shu sababli bu son ro'yxatning amaldagi
 * chegarasi. 50 tanlandi: HR uchun bu bir necha kunlik oqim (tekshirilgan
 * bazada jami 3 ta yozuv bor edi, eng band kunda ham o'nlab), ya'ni
 * amalda kesish umuman sodir bo'lmaydi. 200 ga ko'tarish esa modalni
 * hech kim oxirigacha aylantirmaydigan varaqqa aylantirardi — chegaraga
 * yetilgan holat oynada alohida qator bilan ochiq aytiladi
 * (`notifications.limitNote`).
 */
export const NOTIFICATION_LIMIT = 50

/**
 * Qo'ng'iroq ustidagi sanoq qanchadan keyin yangilanadi.
 *
 * ⭐ <b>Bu endi ASOSIY yo'l EMAS — ZAXIRA.</b> Boshqa foydalanuvchi (yoki
 * Telegram bot) kiritgan o'zgarish jonli oqim orqali DARHOL keladi
 * (`notifications-stream.ts`). Davriy so'rov esa oqim ishlamay
 * qolgan holatlar uchun qoladi: ulanish uzilgan va hali qayta
 * tiklanmagan, proksi oqimni yopib qo'ygan, yoki brauzer uni
 * qo'llab-quvvatlamaydi. Ikkalasi AYNAN bir xil kesh kalitiga yozadi,
 * ya'ni to'qnashuv yo'q — qaysi biri oldin kelsa, o'sha ko'rinadi.
 *
 * <b>Nega 60 soniya (oqim qo'shilgach ham o'zgarmadi).</b> Zaxira
 * yo'lning vazifasi «jonli» bo'lish emas, YO'QOLIB QOLMASLIK. Tezroq
 * qilish oqim ishlayotgan 99% vaqtda bekor trafik, sekinroq qilish esa
 * uzilish paytida sanoqni ishonchsiz qilardi.
 *
 * ⚠️ Yashirin oynada TanStack so'rov yubormaydi (`refetchIntervalInBackground`
 * standart bo'yicha `false`) — ya'ni ochiq qoldirilgan, lekin
 * qaralmayotgan tab tarmoqni bandlamaydi. Oqim esa bu holatda ham
 * ochiq qoladi: u so'rov yubormaydi, faqat eshitadi.
 */
const UNREAD_POLL_MS = 60_000

/**
 * Kesh kalitlari.
 *
 * ⚠️ Mutatsiyalar QO'LDA `invalidateQueries` YOZMAYDI: `providers.tsx`
 * har qanday muvaffaqiyatli yozuv amalidan keyin butun keshni (rasm
 * kalitlaridan tashqari) o'zi bekor qiladi — u yerdagi izohda sabab
 * batafsil. Ya'ni «o'qildi» yoki «o'chirish» dan keyin ham ro'yxat, ham
 * sanoq o'zi yangilanadi.
 */
export const notificationKeys = {
  root: ['notifications'] as const,
  list: (limit: number) => ['notifications', 'list', limit] as const,
  unreadCount: ['notifications', 'unread-count'] as const,
}

/**
 * Modal oynadagi ro'yxat.
 *
 * `enabled` — oyna ochiqmi. Yopiq turganda so'rov umuman ketmaydi:
 * qo'ng'iroqqa faqat SANOQ kerak, u esa yengilroq endpoint'dan keladi.
 *
 * `staleTime: 0` — ATAYLAB umumiy sozlamani (60 s) bekor qiladi. Sabab:
 * «modal ochilganda ro'yxat yangilansin» degan talab aynan shu bilan
 * bajariladi. Kesh 60 s yashasa, oynani yopib darhol qayta ochgan HR
 * eski ro'yxatni ko'rardi — holbuki u oynani aynan «nima o'zgardi?»
 * degan savol bilan qayta ochadi.
 *
 * NEGA `placeholderData` yo'q (ro'yxatlarda odatiy naqsh): bu yerda
 * filtr yo'q, ya'ni «so'rov o'zgardi, natija bo'shab qoldi» holati
 * umuman yuzaga kelmaydi. Qayta ochilganda esa kesh o'zi joyida turadi
 * va skelet ko'rinmaydi.
 */
export function useNotifications(enabled: boolean) {
  return useQuery({
    queryKey: notificationKeys.list(NOTIFICATION_LIMIT),
    queryFn: ({ signal }) => fetchNotifications(NOTIFICATION_LIMIT, signal),
    enabled,
    staleTime: 0,
  })
}

/**
 * Qo'ng'iroq ustidagi o'qilmaganlar soni.
 *
 * @param paused Modal OCHIQmi. Ochiq turganda davriy so'rov to'xtaydi:
 *   ro'yxat javobining o'zida ham sanoq bor va ikkalasi bir vaqtda
 *   so'ralsa, bir-biriga mos kelmagan ikkita raqam ekranda yonma-yon
 *   turishi mumkin edi. Yozuv amallaridan keyin sanoq baribir
 *   yangilanadi (kesh bekor qilinadi), ya'ni pauza hech narsani
 *   eskirtirmaydi.
 */
export function useUnreadCount(paused: boolean) {
  return useQuery({
    queryKey: notificationKeys.unreadCount,
    queryFn: ({ signal }) => fetchUnreadCount(signal),
    refetchInterval: paused ? false : UNREAD_POLL_MS,

    /*
     * Oynaga qaytilganda DARHOL yangilanadi.
     *
     * Ilova bo'ylab standart `refetchOnWindowFocus: false`
     * (`providers.tsx`) — va u qolgan hamma joyda to'g'ri: HR ro'yxatni
     * ochib, boshqa dasturga o'tib qaytganda jadval sababsiz
     * miltillamasligi kerak.
     *
     * LEKIN bu sanoq uchun aynan teskarisi kerak. Davriy so'rov tab
     * YASHIRIN bo'lganda umuman ketmaydi (TanStack Query ning standart
     * xatti-harakati — bu to'g'ri, fonda turgan tab uchun so'rov
     * yuborish behuda). Natijada brauzer bilan boshqa dastur orasida
     * yurgan foydalanuvchi qaytganda ESKI sonni ko'radi va u haqiqatga
     * mos kelmaydi — nishon esa aynan «hozir nima bor» degan savolga
     * javob berishi kerak. Noto'g'ri son nishonni butunlay ishonchsiz
     * qiladi (qattiq yozib qo'yilgan qizil nuqta bilan bir xil kasallik).
     *
     * Narxi — tabga qaytganda bitta yengil so'rov (`{ unreadCount }`).
     */
    refetchOnWindowFocus: true,
  })
}

/**
 * «O'qildi» belgisi — bitta yozuv uchun ham, hammasi uchun ham.
 *
 * Bitta chaqiruvda ikkala holat ham qoplanadi (`{ keys }` va
 * `{ all: true }`), chunki server ham bitta endpoint: ikkita hook
 * yozilsa, ular orasidagi yagona farq tananing shakli bo'lardi.
 */
export function useMarkNotificationsRead() {
  return useMutation({
    mutationFn: (body: MarkReadInput) => markNotificationsRead(body),
  })
}

/** Belgilanganlarni ro'yxatdan olib tashlash. ⚠️ Qaytarib bo'lmaydi. */
export function useDeleteNotifications() {
  return useMutation({
    mutationFn: (keys: string[]) => deleteNotifications(keys),
  })
}
