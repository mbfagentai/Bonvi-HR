import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useAuth } from '@/features/auth'
import {
  activateUser,
  createUser,
  deactivateUser,
  fetchUsers,
  resetUserPassword,
  updateUser,
  type CreateUserInput,
  type UserFormInput,
} from './users-api'
import { USERS_MANAGE } from '../lib/user-presentation'

/** Hammasi bitta ildizdan: ro'yxat kichik va bitta amaldan keyin butunlay yangilanadi. */
export const userKeys = {
  root: ['users'] as const,
  list: ['users', 'list'] as const,
}

/**
 * Foydalanuvchilar ro'yxati.
 *
 * `enabled: canManage` — `GET /api/users` ham `users.manage` bilan
 * himoyalangan (`UserEndpoints.cs`: butun guruh bitta ruxsat ostida).
 * Ruxsatsiz odam uchun so'rovni umuman yubormaymiz: aks holda har safar
 * behuda 403 kelardi va konsolni ogohlantirish bilan to'ldirardi — sahifa
 * o'zi buni allaqachon bilib turibdi (`useAuth().hasPermission`).
 */
export function useUsers() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(USERS_MANAGE)

  return useQuery({
    queryKey: userKeys.list,
    queryFn: ({ signal }) => fetchUsers(signal),
    enabled: canManage,
  })
}

/**
 * Yaratish va tahrirlash bitta mutatsiyada — `catalog-queries.ts` dagi
 * `useSaveCatalogItem` bilan bir xil sabab: forma bir xil, farq faqat
 * `id` borligida.
 */
export function useSaveUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (
      input: { id: null; body: CreateUserInput } | { id: string; body: UserFormInput },
    ) => (input.id === null ? createUser(input.body) : updateUser(input.id, input.body)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.root })
    },
  })
}

export function useDeactivateUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => deactivateUser(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.root })
    },
  })
}

export function useActivateUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => activateUser(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.root })
    },
  })
}

/**
 * Ro'yxatdagi hech qanday maydonni o'zgartirmaydi (parol ko'rinmaydi),
 * shu sababli keshni bekor qilish shart emas.
 */
export function useResetUserPassword() {
  return useMutation({
    mutationFn: ({ id, newPassword }: { id: string; newPassword: string }) =>
      resetUserPassword(id, newPassword),
  })
}
