import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { closeFlag } from '@/features/employees/api/employees-api'
import type { EmployeeFlagType } from '@/features/employees/api/employees-api'
import { employeeKeys } from '@/features/employees/api/employees-queries'

import { fetchFlagged } from './flags-api'

export const flagKeys = {
  root: ['flagged'] as const,
  // Qidiruv kalitning bir qismi: server tomonda filtrlangan natija
  // boshqa natija, ya'ni uni bir xil kalitga yozib bo'lmaydi.
  list: (type: EmployeeFlagType, onlyActive: boolean, search: string) =>
    ['flagged', type, onlyActive, search] as const,
}

export function useFlagged(type: EmployeeFlagType, onlyActive: boolean, search: string) {
  return useQuery({
    queryKey: flagKeys.list(type, onlyActive, search),
    queryFn: ({ signal }) => fetchFlagged(type, onlyActive, search, signal),

    // Qidiruv o'zgarganda jadval bo'shab, «hech narsa topilmadi» bo'lib
    // ko'rinmasligi kerak — eski natija yangisi kelguncha turadi
    // (`useExitInterviews` dagi bilan bir xil qoida).
    placeholderData: (previous) => previous,
  })
}

/**
 * Belgini yopish.
 *
 * Yopilgandan keyin ikkita joy eskiradi va ikkalasi ham bekor qilinadi:
 * shu sahifadagi ro'yxat va xodimning o'z profilidagi belgilar. Ikkinchisi
 * unutilsa, HR profilga o'tganda yopilgan belgi hamon kuchdagidek
 * ko'rinardi.
 *
 * Xodimlar ro'yxati ham bekor qilinadi — u qatorda belgi yorlig'ini
 * ko'rsatadi (`activeFlags`).
 */
export function useCloseFlag() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      employeeId,
      flagId,
      closedAt,
      note,
    }: {
      employeeId: string
      flagId: string
      closedAt: string
      note: string | null
    }) => closeFlag(employeeId, flagId, closedAt, note),

    onSuccess: (_result, variables) => {
      void queryClient.invalidateQueries({ queryKey: flagKeys.root })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.flags(variables.employeeId) })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.lists })
    },
  })
}
