import { useQuery } from '@tanstack/react-query'

import { fetchDashboardStats, type DashboardPeriod } from './dashboard-api'

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
}

/**
 * Bosh sahifa ko'rsatkichlari.
 */
export function useDashboardStats(period: DashboardPeriod) {
  return useQuery({
    queryKey: dashboardKeys.stats(period),
    queryFn: ({ signal }) => fetchDashboardStats(period, signal),

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
