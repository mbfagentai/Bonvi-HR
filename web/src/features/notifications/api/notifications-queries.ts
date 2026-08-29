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
const NOTIFICATION_LIMIT = 50

/**
 * Qo'ng'iroq ustidagi sanoq qanchadan keyin yangilanadi.
 *
 * 60 000 ms tanlandi va sabab bildirishnomalarning TABIATIDA:
 * ularning hammasi KUN o'lchovida yashaydi — «sinov muddati ertaga
 * tugaydi», «bugun tug'ilgan kun», «taklifga 2 kun qoldi». Bunday
 * ro'yxat bir daqiqada o'zgarishi mumkin bo'lgan yagona yo'l — shu
 * tizimda kimningdir yozuv kiritishi, va u holda kesh `providers.tsx`
 * orqali DARHOL bekor qilinadi (sanoq ham qayta so'raladi). Ya'ni davr
 * faqat BOSHQA foydalanuvchi kiritgan o'zgarishni ushlab olish uchun.
 *
 * NEGA tezroq emas (masalan 15 s): foyda nol — HR bir daqiqa avvalgi
 * sondan hech narsa yutqazmaydi; narxi esa doimiy fon trafigi.
 * NEGA sekinroq emas (5 daqiqa): ilova kun bo'yi ochiq turadi va soat
 * 09:00 da paydo bo'ladigan «bugun tug'ilgan kun» kechikib chiqardi.
 *
 * ⚠️ Yashirin oynada TanStack so'rov yubormaydi (`refetchIntervalInBackground`
 * standart bo'yicha `false`) — ya'ni ochiq qoldirilgan, lekin
 * qaralmayotgan tab tarmoqni bandlamaydi.
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
