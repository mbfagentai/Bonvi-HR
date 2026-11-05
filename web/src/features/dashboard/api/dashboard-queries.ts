import { useQuery } from '@tanstack/react-query'

import {
  fetchDashboardBirthdays,
  fetchDashboardBreakdown,
  fetchDashboardStats,
  type DashboardPeriod,
} from './dashboard-api'

/**
 * Kesh kalitlari.
 *
 * Davr kalitning ICHIDA: almashtirgich bosilganda yangi so'rov ketadi,
 * lekin oldingi davr keshda qoladi. HR davrlar orasida u yoqdan-bu yoqqa
 * yuradi («oyda ko'p ketibdi — haftada-chi?») va qaytishda raqamlar
 * darhol chiqadi.
 */
export const dashboardKeys = {
  root: ['dashboard'] as const,
  stats: (period: DashboardPeriod) => ['dashboard', 'stats', period] as const,

  /**
   * Taqsimot kaliti — davrSIZ, ataylab.
   *
   * Davrni bu yerga qo'shish oson bo'lardi («qo'shnisi bilan bir xil
   * bo'lsin») va aynan shu narsa xato bo'lardi: endpoint oraliqni
   * umuman qabul qilmaydi, ya'ni to'rt xil kalit AYNAN bir xil javobni
   * to'rt marta so'rab, kartani har bosishda sababsiz miltillatardi.
   */
  breakdown: () => ['dashboard', 'breakdown'] as const,

  /**
   * Tug'ilgan kunlar ro'yxati — davrSIZ va SANASIZ.
   *
   * Sana kalitga qo'shilmaydi, chunki uni mijoz bilmaydi: «bugun»
   * serverda, Toshkent vaqti bo'yicha hisoblanadi. Brauzer sanasini
   * kalitga qo'yish uni ikkinchi haqiqat manbaiga aylantirardi va yarim
   * tunda ikkalasi ajralib ketardi.
   */
  birthdays: () => ['dashboard', 'birthdays'] as const,
}

/**
 * Bosh sahifa ko'rsatkichlari.
 */
export function useDashboardStats(period: DashboardPeriod, enabled = true) {
  return useQuery({
    queryKey: dashboardKeys.stats(period),
    queryFn: ({ signal }) => fetchDashboardStats(period, signal),
    enabled,

    /**
     * Davr almashtirilganda kartalar bo'shab, keyin qayta to'lmaydi —
     * eski raqamlar ko'rinib turadi va faqat ustidagi holat
     * «yangilanmoqda» bo'ladi. Skelet faqat ENG BIRINCHI yuklashda
     * chiqadi: aks holda har bosishda ekran sakrardi.
     */
    placeholderData: (previous) => previous,

    /**
     * Bir daqiqa — kadrlar harakati sekin o'zgaradigan ma'lumot.
     * Bosh sahifaga har qaytganda so'rov yuborish faqat ortiqcha yuk:
     * xodimlar soni bir daqiqada o'zgarmaydi, o'zgarsa ham ishga qabul
     * qilgan odam allaqachon buni biladi.
     */
    staleTime: 60_000,
  })
}

/**
 * Xodimlarning bo'lim va yo'nalish kesimi.
 */
export function useDashboardBreakdown() {
  return useQuery({
    queryKey: dashboardKeys.breakdown(),
    queryFn: ({ signal }) => fetchDashboardBreakdown(signal),

    /**
     * `placeholderData` bu yerda KERAK EMAS (`useDashboardStats` dan farqi).
     * U «kalit o'zgardi — eski ma'lumot ko'rinib tursin» degani, bu
     * so'rovning kaliti esa hech qachon o'zgarmaydi: birinchi yuklashdan
     * keyin ma'lumot shundoq ham joyida qoladi.
     */

    /**
     * Qo'shnisi bilan bir xil bir daqiqa — bosh sahifaning ikkala so'rovi
     * bir xil ma'lumotdan (xodimlar ro'yxatidan) hosil bo'ladi, ya'ni
     * ularning «eskirish» tezligi ham bir xil.
     */
    staleTime: 60_000,
  })
}

/**
 * Bugun tug'ilgan kuni bo'lgan xodimlar ro'yxati.
 *
 * @param enabled Modal OCHIQmi.
 *
 * ⚠️ **So'rov faqat oyna ochilganda ketadi** va bu qasddan qilingan
 * asosiy qaror. Bosh sahifa har kirishda ochiladi, bu ro'yxatni esa ko'p
 * hollarda hech kim ochmaydi: «Bugun 2 ta xodimning tug'ilgan kuni»
 * yozuvining O'ZI allaqachon javob va odam odatda shu bilan kifoyalanadi.
 * Ro'yxatni sahifa bilan birga yuklash har bir kirishda ishlatilmaydigan
 * so'rov demak bo'lardi — va u eng ko'p ochiladigan ekranda. Bu
 * `useNotifications(open)` dagi bilan AYNAN bir xil naqsh: qo'ng'iroqqa
 * faqat SANOQ kerak, ro'yxat esa bosilganda so'raladi.
 *
 * `staleTime` shu sababli MEROS bo'yicha olinadi (`providers.tsx` dagi
 * umumiy qiymat) va qo'lda `0` ga tushirilmaydi — bildirishnomalardan
 * farqi shunda: u yerda oyna «nima o'zgardi?» degan savol bilan qayta
 * ochiladi va har safar yangi ro'yxat kerak; bu yerda esa ro'yxat KUN
 * davomida o'zgarmaydi — tug'ilgan kun sanasi bugun ko'chib ketmaydi.
 * Yangi xodim qo'shilsa, kesh `providers.tsx` orqali baribir bekor
 * qilinadi.
 */
export function useDashboardBirthdays(enabled: boolean) {
  return useQuery({
    queryKey: dashboardKeys.birthdays(),
    queryFn: ({ signal }) => fetchDashboardBirthdays(signal),
    enabled,
  })
}
