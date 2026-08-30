import type { useNavigate } from '@tanstack/react-router'

import type { AppNotification } from '../api/notifications-api'

/** `useNavigate()` qaytaradigan funksiya turi. */
export type Navigate = ReturnType<typeof useNavigate>

/**
 * Bildirishnomadan uning ekraniga o'tadi.
 *
 * <b>Nega alohida funksiya.</b> Bu o'tish IKKI joydan chaqiriladi:
 * qo'ng'iroq oynasidagi qatordan va jonli xabar (toast) ustidan.
 * Nusxa ko'chirilsa, yangi tur qo'shilganda biri yangilanib,
 * ikkinchisi jimgina eski holicha qolardi — bosilgan xabar hech
 * qayerga olib bormasdi.
 *
 * <b>Nega manzilni QAYTARMAYDI, o'zi o'tadi.</b> TanStack Router'ning
 * `navigate` i qat'iy tiplashgan: har bir marshrutning o'z
 * parametrlari bor. «Manzil tavsifi» qaytarilsa, u birlashma (union)
 * turiga aylanardi va chaqiruv joyida tiplar yo'qolardi. Bu yerda esa
 * har bir tarmoq harfma-harf yozilgan va tekshiruv joyida qoladi.
 *
 * <b>Nega ba'zi ekranlarga QIDIRUV bilan o'tiladi.</b> Sinov muddati
 * ro'yxatida aniq qatorga «sakrash» yo'q — u yerda xodim ismi qidiruvga
 * yoziladi va jadval o'sha bitta qatorga qisqaradi. `MissingProbations`
 * da esa qidiruv YUBORILMAYDI: «sinov yozuvi yo'qlar» bloki sahifaning
 * eng tepasida turadi va qidiruvga umuman bo'ysunmaydi.
 */
export function navigateToNotification(navigate: Navigate, item: AppNotification): void {
  const { route, id } = item.link

  switch (route) {
    case 'Employee':
      if (id !== null) {
        void navigate({ to: '/xodimlar/$employeeId', params: { employeeId: id } })
      }
      return

    case 'Candidate':
      if (id !== null) {
        void navigate({ to: '/nomzodlar/$candidateId', params: { candidateId: id } })
      }
      return

    case 'Probations':
      void navigate({ to: '/sinov', search: { q: item.params['employeeName'] } })
      return

    case 'MissingProbations':
      void navigate({ to: '/sinov', search: {} })
      return
  }
}
