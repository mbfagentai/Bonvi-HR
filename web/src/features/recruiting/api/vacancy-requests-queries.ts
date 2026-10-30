import { useMutation, useQuery } from '@tanstack/react-query'

import {
  approveVacancyRequest,
  cancelVacancyRequest,
  createVacancyRequest,
  fetchVacancyRequests,
  rejectVacancyRequest,
  type VacancyRequestApproveInput,
  type VacancyRequestFilters,
  type VacancyRequestInput,
} from './vacancy-requests-api'

/**
 * Kesh kalitlari — `['recruiting', …]` ostida: vakansiya ochilganda vakansiyalar
 * ro'yxati ham shu ildiz bilan yangilanadi.
 *
 * Mutatsiyalar QO'LDA `invalidateQueries` yozmaydi — `providers.tsx` har
 * muvaffaqiyatli yozuvdan keyin keshni o'zi bekor qiladi (`recruiting-queries.ts` izohi).
 */
export const vacancyRequestKeys = {
  list: (filters: VacancyRequestFilters) => ['recruiting', 'vacancy-requests', filters] as const,
}

export function useVacancyRequests(filters: VacancyRequestFilters) {
  return useQuery({
    queryKey: vacancyRequestKeys.list(filters),
    queryFn: ({ signal }) => fetchVacancyRequests(filters, signal),
    // Sahifa yoki filtr almashganda jadval bo'shab qolmaydi (`useVacancies` bilan bir xil)
    placeholderData: (previous) => previous,
  })
}

export function useCreateVacancyRequest() {
  return useMutation({ mutationFn: (body: VacancyRequestInput) => createVacancyRequest(body) })
}

export function useApproveVacancyRequest() {
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: VacancyRequestApproveInput }) => approveVacancyRequest(id, body),
  })
}

export function useRejectVacancyRequest() {
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => rejectVacancyRequest(id, reason),
  })
}

export function useCancelVacancyRequest() {
  return useMutation({ mutationFn: (id: string) => cancelVacancyRequest(id) })
}
