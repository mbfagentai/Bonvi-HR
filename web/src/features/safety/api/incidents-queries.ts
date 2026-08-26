import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'

import { useEmployees } from '@/features/employees/api/employees-queries'
import type { SelectOption } from '@/shared/ui'

import {
  concludeIncident,
  createIncident,
  deleteIncident,
  fetchIncident,
  fetchIncidents,
  updateIncident,
  type ConcludeIncidentRequest,
  type IncidentFilters,
  type IncidentFormRequest,
  type RecordIncidentRequest,
} from './incidents-api'

export const incidentKeys = {
  root: ['incidents'] as const,
  list: (filters: IncidentFilters) => ['incidents', 'list', filters] as const,
  detail: (id: string) => ['incidents', 'detail', id] as const,
}

/** Jurnal — filtrlangan, sahifalanmagan (server ham massiv qaytaradi). */
export function useIncidents(filters: IncidentFilters) {
  return useQuery({
    queryKey: incidentKeys.list(filters),
    queryFn: ({ signal }) => fetchIncidents(filters, signal),
    placeholderData: (previous) => previous,
  })
}

export function useIncident(id: string) {
  return useQuery({
    queryKey: incidentKeys.detail(id),
    queryFn: ({ signal }) => fetchIncident(id, signal),
    enabled: id !== '',
  })
}

function useInvalidateIncidents() {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: incidentKeys.root })
  }
}

export function useCreateIncident() {
  const invalidate = useInvalidateIncidents()

  return useMutation({
    mutationFn: (body: RecordIncidentRequest) => createIncident(body),
    onSuccess: invalidate,
  })
}

export function useUpdateIncident() {
  const invalidate = useInvalidateIncidents()

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: IncidentFormRequest }) =>
      updateIncident(id, body),
    onSuccess: invalidate,
  })
}

export function useConcludeIncident() {
  const invalidate = useInvalidateIncidents()

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: ConcludeIncidentRequest }) =>
      concludeIncident(id, body),
    onSuccess: invalidate,
  })
}

export function useDeleteIncident() {
  const invalidate = useInvalidateIncidents()

  return useMutation({
    mutationFn: (id: string) => deleteIncident(id),
    onSuccess: invalidate,
  })
}

/**
 * Xodimlarni tanlash uchun (jabrlangan / mas'ul).
 *
 * ⚠️ TO'LIQ RO'YXAT EMAS: server sahifasi eng ko'pi 200 ta beradi
 * (`EmployeeEndpoints.cs`), bu yerda ham shu chegara ishlatiladi.
 * Kompaniyada 200 dan ortiq xodim bo'lsa, ro'yxatning oxiri bu yerda
 * ko'rinmaydi — qidiruv-kombobox o'rniga oddiy `<select>` tanlangani
 * uchun bu ataylab qilingan soddalashtirish (vaqt cheklovi bilan bog'liq,
 * hisobotda alohida ko'rsatilgan).
 */
export function useEmployeeOptions(): { options: SelectOption[]; isPending: boolean } {
  const list = useEmployees({ page: 1, pageSize: 200, sort: 'fullName' })

  const options = useMemo(
    () => (list.data?.items ?? []).map((item) => ({ value: item.id, label: item.fullName })),
    [list.data],
  )

  return { options, isPending: list.isPending }
}
