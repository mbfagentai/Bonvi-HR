/**
 * Xodim ilovasi (PWA) — HR tomoni: hisob va «Telefondan belgilash».
 *
 * `attendance-api.ts` dan ALOHIDA kichik fayl: bu endpointlar xodim
 * kartasining ichida yashaydi (`/api/employees/{id}/app-access`) va
 * davomat jurnali, tabel yoki qurilmalar bilan umumiy hech narsasi yo'q.
 *
 * Tiplar va chaqiruvlar GENERATSIYA QILINGAN (`./bonvi gen`) — bu yerda faqat
 * yupqa o'ramlar va kesh kaliti (`attendance-api.ts` sarlavhasidagi qoida).
 * Mutatsiyalar keshni qo'lda bekor qilmaydi: `providers.tsx` har bir
 * muvaffaqiyatli yozuvdan keyin buni o'zi qiladi.
 */
import { useMutation, useQuery } from '@tanstack/react-query'

import {
  activateEmployeeAccount,
  deactivateEmployeeAccount,
  getEmployeeAppAccess,
  issueEmployeeAccount,
  resetEmployeeAccountPassword,
  setMobileCheckInAllowed,
} from '@/shared/api/generated/employee-app/employee-app'
import type {
  EmployeeAccountCreatedDto,
  EmployeeAccountDto,
  EmployeeAppAccessDto,
} from '@/shared/api/generated/model'

/** Xodimning ilovaga kirish holati: hisob (bo'lmasligi mumkin) va o'chirgich. */
export type EmployeeAppAccess = EmployeeAppAccessDto

/** Ilova hisobi — ochiq parolsiz. */
export type EmployeeAccount = EmployeeAccountDto

/**
 * Hisob va BIR MARTALIK parol — faqat ochish/tiklash javobida.
 *
 * ⚠️ Parol keshga yozilmaydi (mutatsiya natijasi, so'rov emas) va faqat
 * ko'rsatish oynasining xotirasida yashaydi.
 */
export type EmployeeAccountSecret = EmployeeAccountCreatedDto

export const appAccessKey = (employeeId: string) => ['attendance', 'app-access', employeeId] as const

export function useEmployeeAppAccess(employeeId: string, enabled = true) {
  return useQuery({
    queryKey: appAccessKey(employeeId),
    queryFn: ({ signal }) => getEmployeeAppAccess(employeeId, signal),
    enabled: enabled && employeeId !== '',
  })
}

/** Hisob ochish — `login` bo'sh bo'lsa server xodim kartasidagi raqamni oladi. */
export function useIssueEmployeeAccount(employeeId: string) {
  return useMutation({
    mutationFn: (login: string | null) =>
      issueEmployeeAccount(employeeId, { login: login === null || login.trim() === '' ? null : login.trim() }),
  })
}

/** Yangi bir martalik parol — xodimning barcha sessiyalari yopiladi. */
export function useResetEmployeeAccountPassword(employeeId: string) {
  return useMutation({
    mutationFn: () => resetEmployeeAccountPassword(employeeId),
  })
}

/** Hisobni yopish/qaytarish — yopishda ochiq sessiyalar ham bekor bo'ladi. */
export function useSetEmployeeAccountActive(employeeId: string) {
  return useMutation({
    mutationFn: (active: boolean) =>
      active ? activateEmployeeAccount(employeeId) : deactivateEmployeeAccount(employeeId),
  })
}

/** «Telefondan belgilash» o'chirgichi — XODIMGA beriladi, hisobga emas. */
export function useSetMobileCheckInAllowed(employeeId: string) {
  return useMutation({
    mutationFn: (allowed: boolean) => setMobileCheckInAllowed(employeeId, { allowed }),
  })
}
