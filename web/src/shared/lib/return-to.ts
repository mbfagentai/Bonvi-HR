import { useRouter } from '@tanstack/react-router'

import { useLocale } from '@/shared/i18n/use-locale'

export interface ReturnTarget {
  label: string
  go: () => void
}

/**
 * Kartadagi «Orqaga» — brauzerdagi «orqaga»ning o'zi.
 *
 * <p>Egasi (2026-10-21): tugma hamma joyda shunchaki «Orqaga» deb yozilsin va
 * QAYSI sahifadan kelingan bo'lsa o'shanga qaytarsin — ma'lum menyuga emas.
 * Ilgari manzil nomzodning JORIY holatidan tanlanardi: filtrlangan
 * «Nomzodlar»dan kirib nomzodni rad etgan HR «Rad etilganlar»ga, filtrsiz
 * tushib qolardi.</p>
 *
 * <p>Filtr, sahifa, saralash manzilda (`search`) turadi, kartaning ichki
 * tablari esa tarixga yozuv qo'shmaydi (`replace` / ichki holat) — shuning
 * uchun tarixdagi oldingi yozuv aynan HR qoldirgan sahifa, filtrlari bilan.
 * Kartadan kartaga o'tilgan bo'lsa — oldingi karta, keyin ro'yxat.</p>
 *
 * <p>Tarixda ilova ichidagi oldingi yozuv bo'lmasa (havola, yangi tab) —
 * `fallback`: kartaning asosiy ro'yxati.</p>
 */
export function useReturnTo(fallback: () => void): ReturnTarget {
  const router = useRouter()
  const { t } = useLocale()

  return {
    label: t('common.back'),
    go: () => {
      if (router.history.canGoBack()) {
        router.history.back()
        return
      }
      fallback()
    },
  }
}
