/**
 * Xodim ilovasi (PWA) — HR tomoni: login, parol va «Telefondan belgilash».
 *
 * `attendance-api.ts` dan ALOHIDA kichik fayl: bu endpointlar xodim
 * kartasining ichida yashaydi (`/api/employees/{id}/app-access`) va
 * davomat jurnali, tabel yoki qurilmalar bilan umumiy hech narsasi yo'q.
 *
 * Egasining qarori (2026-09-27): hisob HAR BIR xodimga avtomatik ochiladi,
 * login — profildagi asosiy telefon, parolni faqat HR beradi va ko'radi.
 * Shu sababli «hisob ochish», «hisobni yopish» chaqiruvlari bu yerda YO'Q.
 *
 * Tiplar va chaqiruvlar GENERATSIYA QILINGAN (`./bonvi gen`) — bu yerda faqat
 * yupqa o'ramlar va kesh kalitlari (`attendance-api.ts` sarlavhasidagi qoida).
 * Mutatsiyalar keshni qo'lda bekor qilmaydi: `providers.tsx` har bir
 * muvaffaqiyatli yozuvdan keyin buni o'zi qiladi.
 */
import { useMutation, useQuery } from '@tanstack/react-query'

import {
  getEmployeeAppAccess,
  regenerateEmployeePassword,
  revealEmployeePassword,
  setEmployeePassword,
  setMobileCheckInAllowed,
} from '@/shared/api/generated/employee-app/employee-app'
import type { EmployeeAppAccessDto, EmployeeLoginIssue } from '@/shared/api/generated/model'

/** Xodimning ilovaga kirish holati: login, hisob va o'chirgich. */
export type EmployeeAppAccess = EmployeeAppAccessDto

/** Nega bu raqam bilan kirib bo'lmaydi — `None` bo'lsa kirsa bo'ladi. */
export type AppLoginIssue = EmployeeLoginIssue

export const appAccessKey = (employeeId: string) => ['attendance', 'app-access', employeeId] as const

/**
 * Parol keshining kaliti — bo'lim kalitidan ALOHIDA ildiz bilan.
 *
 * ⚠️ Ochiq parol keshda turmasligi kerak: so'rov faqat ko'z tugmasi bosilganda
 * yoqiladi va yopilishi bilan (`gcTime: 0`) keshdan o'chadi.
 */
const passwordKey = (employeeId: string) => ['attendance', 'app-password', employeeId] as const

/** «Ilova» bo'limi — hisob yo'q bo'lsa server uni SHU so'rovda ochadi. */
export function useEmployeeAppAccess(employeeId: string, enabled = true) {
  return useQuery({
    queryKey: appAccessKey(employeeId),
    queryFn: ({ signal }) => getEmployeeAppAccess(employeeId, signal),
    enabled: enabled && employeeId !== '',
  })
}

/**
 * Xodimning parolini ko'rish — faqat `enabled` bo'lganda (ko'z tugmasi).
 *
 * Qayta urinish YO'Q: `xodim.parol_korinmaydi` (409) qayta so'rash bilan
 * tuzalmaydi, yechim — yangi parol yasash.
 */
export function useEmployeePassword(employeeId: string, enabled: boolean) {
  return useQuery({
    queryKey: passwordKey(employeeId),
    queryFn: ({ signal }) => revealEmployeePassword(employeeId, signal),
    enabled: enabled && employeeId !== '',
    staleTime: 0,
    gcTime: 0,
    retry: false,
  })
}

/** Yangi tasodifiy 6 xonali parol — xodimning barcha sessiyalari yopiladi. */
export function useRegenerateEmployeePassword(employeeId: string) {
  return useMutation({
    mutationFn: () => regenerateEmployeePassword(employeeId),
  })
}

/** HR yozgan parol (6–64 belgi) — xodimning barcha sessiyalari yopiladi. */
export function useSetEmployeePassword(employeeId: string) {
  return useMutation({
    mutationFn: (password: string) => setEmployeePassword(employeeId, { password }),
  })
}

/** «Telefondan belgilash» o'chirgichi — XODIMGA beriladi, hisobga emas. */
export function useSetMobileCheckInAllowed(employeeId: string) {
  return useMutation({
    mutationFn: (allowed: boolean) => setMobileCheckInAllowed(employeeId, { allowed }),
  })
}
