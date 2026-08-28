import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useAuth } from '@/features/auth'
import {
  activateUser,
  createUser,
  deactivateUser,
  deleteUserPhoto,
  fetchUserPhoto,
  fetchUsers,
  resetUserPassword,
  updateUser,
  uploadUserPhoto,
  type CreateUserInput,
  type UserFormInput,
} from './users-api'
import { USERS_MANAGE } from '../lib/user-presentation'

/**
 * Ro'yxat bitta ildizdan: u kichik va bitta amaldan keyin butunlay yangilanadi.
 *
 * Rasm esa ALOHIDA ildizda (`user-photo`): u foydalanuvchi yozuvidan
 * mustaqil va faqat `photoKey` o'zgarganda o'zgaradi — xodimlar bo'limidagi
 * bilan aynan bir xil sabab.
 */
export const userKeys = {
  root: ['users'] as const,
  // Qidiruv kalitning bir qismi: server tomonda filtrlangan natija
  // boshqa natija, ya'ni uni bir xil kalitga yozib bo'lmaydi.
  list: (search: string) => ['users', 'list', search] as const,
  photos: (id: string) => ['user-photo', id] as const,
  photo: (id: string, photoKey: string | null) => ['user-photo', id, photoKey] as const,
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
export function useUsers(search: string) {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(USERS_MANAGE)

  return useQuery({
    queryKey: userKeys.list(search),
    queryFn: ({ signal }) => fetchUsers(search, signal),
    enabled: canManage,

    // Qidiruv o'zgarganda jadval bo'shab, «hech kim topilmadi» bo'lib
    // ko'rinmasligi kerak — eski natija yangisi kelguncha turadi.
    placeholderData: (previous) => previous,
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

/* ─────────────────────────────────────────────────────────── rasm */

/**
 * Foydalanuvchi rasmi.
 *
 * Kalitda `photoKey` turadi — bu keshni o'z-o'zidan to'g'ri saqlaydi:
 * rasm almashtirilsa kalit ham boshqa bo'ladi va eski blob'ga hech kim
 * murojaat qilmaydi. Ya'ni keshni qo'lda tozalash kerak emas.
 */
export function useUserPhoto(userId: string, photoKey: string | null) {
  return useQuery({
    queryKey: userKeys.photo(userId, photoKey),
    queryFn: ({ signal }) => fetchUserPhoto(userId, signal),
    /**
     * Kaliti yo'q — rasmi ham yo'q, so'rov umuman yuborilmaydi.
     * Busiz ro'yxat har ochilganda har bir qator uchun keraksiz 404 ketardi.
     */
    enabled: userId !== '' && photoKey !== null,
    /** Bir kalit — bir rasm. U o'zgarmaydi, demak eskirmaydi ham. */
    staleTime: Infinity,
    /**
     * Qayta urinish yo'q: rasm chiqmasa bosh harflar qoladi va ish
     * to'xtamaydi. Muvaffaqiyatsiz so'rovni takrorlash esa ro'yxatni
     * yuklashni sekinlashtiradi.
     */
    retry: false,
  })
}

/**
 * Rasmni yuklash.
 *
 * Ro'yxat bekor qilinadi — undan kelgan yangi `photoKey` rasm so'rovini
 * o'zi qayta ishga tushiradi. Qo'shimcha ehtiyot chorasi sifatida shu
 * hisobning barcha rasm yozuvlari ham bekor qilinadi: server kalitni
 * O'ZGARTIRMASDAN faylni almashtirsa (masalan kalit hisob `id` sidan hosil
 * qilinsa), keshdagi eski rasm ko'rinib qolardi.
 */
export function useUploadUserPhoto(userId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (file: File) => uploadUserPhoto(userId, file),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.root })
      void queryClient.invalidateQueries({ queryKey: userKeys.photos(userId) })
    },
  })
}

/**
 * Rasmni o'chirish.
 *
 * Keshdagi blob `invalidate` emas, `remove` qilinadi: `photoKey` `null`
 * bo'lgach unga hech kim murojaat qilmaydi, lekin qayta so'rov yuborishning
 * ham ma'nosi yo'q — u faqat 404 olib keladi.
 */
export function useDeleteUserPhoto(userId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => deleteUserPhoto(userId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.root })
      queryClient.removeQueries({ queryKey: userKeys.photos(userId) })
    },
  })
}
