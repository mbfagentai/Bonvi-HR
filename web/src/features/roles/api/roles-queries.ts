import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { userKeys } from '@/features/users/api/users-queries'

import {
  createRole,
  deleteRole,
  fetchRoleCatalog,
  fetchRoles,
  updateRole,
  type RoleInput,
} from './roles-api'

/**
 * Ro'yxat va katalog bitta ildiz ostida — `userKeys` dagi bilan bir xil
 * sabab: ro'yxat kichik (bir necha rol) va har qanday amaldan keyin
 * butunlay qayta o'qiladi.
 */
export const roleKeys = {
  root: ['roles'] as const,
  list: ['roles', 'list'] as const,
  catalog: ['roles', 'catalog'] as const,
}

/**
 * Rollar ro'yxati — ikki sahifada kerak: «Rollar» ning o'zi va
 * «Foydalanuvchilar» dagi rol tanlovi.
 *
 * `enabled` — ruxsat tekshiruvi CHAQIRUVCHIDA: server `GET /api/roles` ni
 * `users.manage` YOKI `roles.manage` bilan himoyalagan va qaysi biri
 * yetarli ekanini faqat sahifa biladi. Ruxsatsiz odamda so'rov umuman
 * ketmaydi — aks holda konsolga behuda 403 tushardi (`useApiKeys` dagi
 * bilan bir xil fikr).
 */
export function useRoles(enabled: boolean) {
  return useQuery({
    queryKey: roleKeys.list,
    queryFn: ({ signal }) => fetchRoles(signal),
    enabled,
  })
}

/**
 * Ruxsat katalogi.
 *
 * `staleTime: Infinity` — katalog KODDA aniqlanadi va faqat yangi versiya
 * chiqqanda o'zgaradi. Bir sessiya ichida uni qayta so'rashning ma'nosi
 * yo'q; yangi versiyadan keyin sahifa baribir qayta yuklanadi.
 */
export function useRoleCatalog(enabled: boolean) {
  return useQuery({
    queryKey: roleKeys.catalog,
    queryFn: ({ signal }) => fetchRoleCatalog(signal),
    enabled,
    staleTime: Infinity,
  })
}

/**
 * Yaratish va tahrirlash — bitta mutatsiya (`useSaveUser` dagi bilan bir
 * xil sabab: forma bir xil, farq faqat `id` borligida).
 *
 * Foydalanuvchilar ro'yxati ham bekor qilinadi: u yerda har bir qatorda
 * rol NOMI turadi va nom o'zgargach eski nom ko'rinib qolmasligi kerak.
 */
export function useSaveRole() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: { id: string | null; body: RoleInput }) =>
      input.id === null ? createRole(input.body) : updateRole(input.id, input.body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.list })
      void queryClient.invalidateQueries({ queryKey: userKeys.root })
    },
  })
}

export function useDeleteRole() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => deleteRole(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.list })
    },
  })
}
