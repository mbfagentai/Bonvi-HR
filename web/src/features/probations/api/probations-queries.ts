import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { employeeKeys } from '@/features/employees/api/employees-queries'

import {
  confirmMissingProbations,
  extendProbation,
  fetchEndingSoon,
  fetchMissingProbations,
  fetchProbations,
  resolveProbation,
  type ExtendProbationRequest,
  type ProbationFilters,
  type ResolveProbationRequest,
} from './probations-api'

/**
 * Muddat tugashiga necha kun qolganda ogohlantiriladi.
 *
 * 7 — HR aynan shu raqamni so'ragan (`PLAN.md`, S6). Server ham shu bilan
 * hisoblaydi (`Probation.WarningDays`), lekin so'rovda ochiq yuboriladi:
 * qoida ikki joyda turgani ma'qul emas, ammo mijoz nimani so'raganini
 * ko'rsatib turishi kerak — aks holda ro'yxat va sanoq nega bunday
 * chiqqani kodni o'qimasdan tushunarsiz bo'lardi.
 */
export const WARNING_DAYS = 7

export const probationKeys = {
  root: ['probations'] as const,
  lists: ['probations', 'list'] as const,
  list: (filters: ProbationFilters) => ['probations', 'list', filters] as const,
  endingSoon: (withinDays: number) => ['probations', 'ending-soon', withinDays] as const,
  missing: ['probations', 'missing'] as const,
}

/**
 * @param enabled `false` — so'rov yuborilmaydi (teskari sana oralig'ida:
 * server baribir 400 qaytarardi, sahifa sababni o'zi aytadi).
 */
export function useProbations(filters: ProbationFilters, enabled = true) {
  return useQuery({
    queryKey: probationKeys.list(filters),
    queryFn: ({ signal }) => fetchProbations(filters, signal),
    enabled,
  })
}

/**
 * Muddati yaqinlashganlar.
 *
 * Alohida so'rov, chunki u ro'yxat filtridan MUSTAQIL: HR «o'tganlar» ni
 * ko'rayotganda ham yuqoridagi ogohlantirish o'z kuchida qolishi kerak.
 */
export function useEndingSoon(withinDays: number = WARNING_DAYS) {
  return useQuery({
    queryKey: probationKeys.endingSoon(withinDays),
    queryFn: ({ signal }) => fetchEndingSoon(withinDays, signal),
  })
}

export function useResolveProbation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ResolveProbationRequest }) =>
      resolveProbation(id, request),
    onSuccess: () => {
      // Butun ildiz: yakunlangan muddat ham ro'yxatdan, ham ogohlantirish
      // sanog'idan chiqib ketishi kerak.
      void queryClient.invalidateQueries({ queryKey: probationKeys.root })

      // Xodimlar ro'yxati ham: sinovdan o'tgan odam endi «doimiy» va
      // aynan o'sha ro'yxatda paydo bo'ladi. Busiz HR «o'tkazdim, lekin
      // xodimlarda yo'q» degan holatni ko'rardi.
      void queryClient.invalidateQueries({ queryKey: employeeKeys.root })

      // «O'tmadi» / «o'zi ketdi» — bu bo'shatish ham, u esa ishlab turganlar
      // belgilarini yopadi (`useTerminate` dagi bilan bir xil sabab).
      void queryClient.invalidateQueries({ queryKey: ['flagged'] })
    },
  })
}

/**
 * Sinov yozuvisiz «sinovda» qolganlar.
 *
 * Bu ro'yxat bir martalik tozalash ishi va u tugagach bo'shab qoladi —
 * shu sababli sahifalash ham, filtr ham yo'q. Xodimlar ro'yxati bilan
 * bir vaqtda so'ralmaydi: u faqat sinov sahifasida kerak.
 */
export function useMissingProbations() {
  return useQuery({
    queryKey: probationKeys.missing,
    queryFn: ({ signal }) => fetchMissingProbations(signal),
  })
}

/**
 * Belgilanganlarni doimiy holatga o'tkazadi.
 *
 * Xodimlar ro'yxati ham bekor qilinadi: tasdiqlangan odam endi u yerda
 * paydo bo'ladi va HR natijani darhol ko'rishi kerak.
 */
export function useConfirmMissingProbations() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (employmentIds: readonly string[]) => confirmMissingProbations(employmentIds),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: probationKeys.root })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.root })
    },
  })
}

/**
 * Muddatni uzaytirish.
 *
 * Xodimlar ildizi ham bekor qilinadi. NEGA: uzaytirish endi xodim
 * KARTASIDAN ham chaqiriladi va kartadagi «Sinov muddati tugashi» qatori
 * aynan shu so'rovdan keladi. Busiz HR sanani o'zgartirib, ekranda eski
 * sanani ko'rib turardi — va amal bajarilmagan deb o'ylardi.
 */
export function useExtendProbation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ExtendProbationRequest }) =>
      extendProbation(id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: probationKeys.root })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.root })
    },
  })
}
