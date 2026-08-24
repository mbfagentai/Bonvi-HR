import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  extendProbation,
  fetchEndingSoon,
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
}

export function useProbations(filters: ProbationFilters) {
  return useQuery({
    queryKey: probationKeys.list(filters),
    queryFn: ({ signal }) => fetchProbations(filters, signal),
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
    },
  })
}

export function useExtendProbation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ExtendProbationRequest }) =>
      extendProbation(id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: probationKeys.root })
    },
  })
}
