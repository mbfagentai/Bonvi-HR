import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { closeFlag } from '@/features/employees/api/employees-api'
import type { EmployeeFlagType } from '@/features/employees/api/employees-api'
import { employeeKeys } from '@/features/employees/api/employees-queries'
import type { DateRange } from '@/shared/lib/list-search'

import { fetchFlagged } from './flags-api'

export const flagKeys = {
  root: ['flagged'] as const,
  // Qidiruv kalitning bir qismi: server tomonda filtrlangan natija
  // boshqa natija, ya'ni uni bir xil kalitga yozib bo'lmaydi.
  // Sana oralig'i ham — chegaralari bilan (bo'sh satr «chegara yo'q»).
  list: (type: EmployeeFlagType, onlyActive: boolean, search: string, range: DateRange) =>
    ['flagged', type, onlyActive, search, range.from, range.to] as const,
}

/**
 * @param enabled `false` — so'rov yuborilmaydi (teskari sana oralig'ida:
 * server baribir 400 qaytarardi, sahifa sababni o'zi aytadi).
 */
export function useFlagged(
  type: EmployeeFlagType,
  onlyActive: boolean,
  search: string,
  range: DateRange,
  enabled = true,
) {
  return useQuery({
    queryKey: flagKeys.list(type, onlyActive, search, range),
    queryFn: ({ signal }) => fetchFlagged(type, onlyActive, search, range, signal),
    enabled,

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
