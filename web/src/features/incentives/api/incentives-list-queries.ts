import { useMutation, useQuery } from '@tanstack/react-query'

import { saveBlobAs } from '@/shared/lib/download'

import {
  exportIncentives,
  fetchIncentiveList,
  type IncentiveExportFilters,
  type IncentiveListFilters,
} from './incentives-list-api'

/*
 * Bo'lim va yuridik shaxs ro'yxatlari filtr paneli uchun kerak, lekin
 * ular RAG'BAT domeniga tegishli emas — xodimlar moduli ularni
 * allaqachon yig'ib beradi (`useFilterOptions`). Qayta so'ramaslik
 * uchun to'g'ridan-to'g'ri o'sha yerdan qayta eksport qilinadi: kesh
 * kaliti bir xil bo'lgani sababli, agar HR avval xodimlar yoki
 * buyruqlar ro'yxatini ochgan bo'lsa, bu yerda ikkinchi marta so'rov
 * ketmaydi (`employments-queries.ts` dagi bilan bir xil qaror).
 */
export { useFilterOptions } from '@/features/employees/api/employees-queries'

/*
 * Kesh ildizi xodim kartasidagi tabdan ATAYLAB ALOHIDA
 * (`employeeKeys.incentives(id)` — `employees-queries.ts`). Ikkalasi
 * bitta jadvalni o'qisa ham, ular boshqa-boshqa so'rovlar: biri bitta
 * odamniki, ikkinchisi butun tizim bo'yicha filtrlangan sahifa. Bitta
 * ildizga yig'ilsa, yozuv tahrirlanganda bekor qilish qoidalari
 * chalkashardi — kartadagi tab o'zining prefiksini tozalab, umumiy
 * ro'yxatni ham tasodifan qayta so'ratardi (yoki aksincha, unutardi).
 */
export const incentiveListKeys = {
  root: ['incentiveList'] as const,
  list: (filters: IncentiveListFilters) => ['incentiveList', 'list', filters] as const,
}

/**
 * «Rag'bat va jarima» ro'yxati — barcha xodimlar bo'yicha, filtrlangan,
 * saralangan va sahifalangan.
 */
export function useIncentiveList(filters: IncentiveListFilters) {
  return useQuery({
    queryKey: incentiveListKeys.list(filters),
    queryFn: ({ signal }) => fetchIncentiveList(filters, signal),

    /**
     * Sahifadan sahifaga yoki filtrdan filtrga o'tganda jadval bo'shab
     * qolmaydi — eski ro'yxat ko'rinib turadi, faqat holat
     * «yangilanmoqda» bo'ladi. Xodimlar va buyruqlar ro'yxatida ham
     * xuddi shunday.
     */
    placeholderData: (previous) => previous,
  })
}

/**
 * «Excelga yuklash» — ekrandagi filtr va saralash bilan.
 *
 * `useExportEmployees` bilan bir xil: mutatsiya shaklida (tugma bosilganda,
 * avtomatik takrorlanmaydi), `readOnly` — fayl yuklangani uchun ekran
 * keshini bekor qilishning ma'nosi yo'q.
 */
export function useExportIncentives() {
  return useMutation({
    mutationFn: (filters: IncentiveExportFilters) => exportIncentives(filters),
    onSuccess: ({ blob, fileName }) => saveBlobAs(blob, fileName),
    meta: { readOnly: true },
  })
}
