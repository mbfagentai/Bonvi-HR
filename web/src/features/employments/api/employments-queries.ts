import { useQuery } from '@tanstack/react-query'

import { fetchEmploymentEvents, type EmploymentEventFilters } from './employments-api'

/*
 * Bo'lim va yuridik shaxs ro'yxatlari filtr paneli uchun kerak, lekin
 * ular BUYRUQLAR domeniga tegishli emas — xodimlar moduli ularni
 * allaqachon yig'ib beradi (`useFilterOptions`). Qayta so'ramaslik
 * uchun to'g'ridan-to'g'ri o'sha yerdan qayta eksport qilinadi: kesh
 * kaliti bir xil bo'lgani sababli, agar HR avval xodimlar ro'yxatini
 * ochgan bo'lsa, bu yerda ikkinchi marta so'rov ketmaydi.
 */
export { useFilterOptions } from '@/features/employees/api/employees-queries'

export const employmentEventKeys = {
  root: ['employmentEvents'] as const,
  list: (filters: EmploymentEventFilters) => ['employmentEvents', 'list', filters] as const,
}

/**
 * Buyruqlar ro'yxati — barcha xodimlar bo'yicha, filtrlangan va sahifalangan.
 */
export function useEmploymentEvents(filters: EmploymentEventFilters) {
  return useQuery({
    queryKey: employmentEventKeys.list(filters),
    queryFn: ({ signal }) => fetchEmploymentEvents(filters, signal),

    /**
     * Sahifadan sahifaga yoki filtrdan filtrga o'tganda jadval bo'shab
     * qolmaydi — eski ro'yxat ko'rinib turadi, faqat holat
     * «yangilanmoqda» bo'ladi. Xodimlar ro'yxatida ham xuddi shunday.
     */
    placeholderData: (previous) => previous,
  })
}
