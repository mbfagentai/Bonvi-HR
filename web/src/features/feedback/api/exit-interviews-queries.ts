import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'

import { useEmployee, useEmployees } from '@/features/employees/api/employees-queries'
import type { EmploymentSummary } from '@/features/employees/api/employees-api'
import type { SelectOption } from '@/shared/ui'

import {
  createExitInterview,
  deleteExitInterview,
  fetchExitInterviewByEmployment,
  fetchExitInterviews,
  updateExitInterview,
  type ExitInterviewFilters,
  type ExitInterviewFormRequest,
  type RecordExitInterviewRequest,
} from './exit-interviews-api'

export const exitInterviewKeys = {
  root: ['exitInterviews'] as const,
  list: (filters: ExitInterviewFilters) => ['exitInterviews', 'list', filters] as const,
  byEmployment: (employmentId: string) => ['exitInterviews', 'byEmployment', employmentId] as const,
}

/** Jurnal — filtrlangan, sahifalanmagan (server ham massiv qaytaradi). */
export function useExitInterviews(filters: ExitInterviewFilters) {
  return useQuery({
    queryKey: exitInterviewKeys.list(filters),
    queryFn: ({ signal }) => fetchExitInterviews(filters, signal),
    placeholderData: (previous) => previous,
  })
}

/**
 * Bitta mehnat munosabati bo'yicha suhbat — mavjud emasligi (`null`) XATO
 * EMAS (`exit-interviews-api.ts` dagi izohga qarang).
 */
export function useExitInterviewByEmployment(employmentId: string | null) {
  return useQuery({
    queryKey: exitInterviewKeys.byEmployment(employmentId ?? ''),
    queryFn: ({ signal }) => fetchExitInterviewByEmployment(employmentId ?? '', signal),
    enabled: employmentId !== null && employmentId !== '',
  })
}

function useInvalidateExitInterviews() {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: exitInterviewKeys.root })
  }
}

export function useCreateExitInterview() {
  const invalidate = useInvalidateExitInterviews()

  return useMutation({
    mutationFn: (body: RecordExitInterviewRequest) => createExitInterview(body),
    onSuccess: invalidate,
  })
}

export function useUpdateExitInterview() {
  const invalidate = useInvalidateExitInterviews()

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: ExitInterviewFormRequest }) =>
      updateExitInterview(id, body),
    onSuccess: invalidate,
  })
}

export function useDeleteExitInterview() {
  const invalidate = useInvalidateExitInterviews()

  return useMutation({
    mutationFn: (id: string) => deleteExitInterview(id),
    onSuccess: invalidate,
  })
}

/**
 * Ishdan ketgan xodimlarni tanlash uchun — «faqat yakunlangan (Terminated)
 * mehnat munosabati uchun yozish mumkin» qoidasi shu yerdan boshlanadi
 * (server ham baribir tekshiradi: `qayta_aloqa.mehnat_tugamagan»`).
 *
 * ⚠️ TO'LIQ RO'YXAT EMAS — `incidents-queries.ts` dagi `useEmployeeOptions`
 * bilan bir xil soddalashtirish (server sahifasi eng ko'pi 200 ta beradi).
 */
export function useTerminatedEmployeeOptions(): { options: SelectOption[]; isPending: boolean } {
  const list = useEmployees({ page: 1, pageSize: 200, sort: 'fullName', status: 'terminated' })

  const options = useMemo(
    () => (list.data?.items ?? []).map((item) => ({ value: item.id, label: item.fullName })),
    [list.data],
  )

  return { options, isPending: list.isPending }
}

/**
 * Tanlangan xodimning YAKUNLANGAN mehnat munosabatlari.
 *
 * NEGA kerak: qayta aloqa `employmentId`ga bog'lanadi, `employeeId`ga
 * emas (`exit-interviews-api.ts` izohiga qarang) — bitta shaxsning bir
 * nechta (hatto bir nechta tugagan) mehnat munosabati bo'lishi mumkin
 * (masalan qayta ishga olib, yana bo'shatilgan bo'lsa). Shu sababli xodim
 * tanlangach, aynan QAYSI ketishga suhbat tegishli ekani alohida so'raladi.
 */
export function useTerminatedEmployments(employeeId: string): {
  employments: EmploymentSummary[]
  isPending: boolean
} {
  const employee = useEmployee(employeeId)

  const employments = useMemo(
    () => (employee.data?.employments ?? []).filter((row) => row.status === 'terminated'),
    [employee.data],
  )

  return { employments, isPending: employee.isPending }
}
