import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { deleteMyPhoto, fetchMyPhoto, uploadMyPhoto } from './auth-api'

/**
 * Kesh kalitlari.
 *
 * Kalitda `photoKey` turadi — bu keshni o'z-o'zidan to'g'ri saqlaydi: rasm
 * almashtirilsa kalit ham boshqa bo'ladi va eski blob'ga hech kim murojaat
 * qilmaydi. `userId` ham kalitda: bitta brauzerda hisob almashtirilsa,
 * oldingi foydalanuvchining surati yangisiga ko'chib o'tmaydi.
 */
export const accountPhotoKeys = {
  root: ['account-photo'] as const,
  photo: (userId: string, photoKey: string | null) =>
    ['account-photo', userId, photoKey] as const,
}

/**
 * Joriy foydalanuvchining rasmi.
 *
 * Yuqori panel, menyu va sozlamalar oynasi — uchalasi shu hook'ni chaqiradi
 * va bitta kesh yozuvini bo'lishadi, ya'ni so'rov bir marta ketadi.
 */
export function useMyPhoto(userId: string | null, photoKey: string | null) {
  return useQuery({
    queryKey: accountPhotoKeys.photo(userId ?? '', photoKey),
    queryFn: ({ signal }) => fetchMyPhoto(signal),

    // Kaliti yo'q — rasmi ham yo'q, so'rov umuman yuborilmaydi
    enabled: userId !== null && photoKey !== null,

    /** Bir kalit — bir rasm. U o'zgarmaydi, demak eskirmaydi ham. */
    staleTime: Infinity,

    /**
     * Qayta urinish yo'q: rasm chiqmasa bosh harflar qoladi va ishda hech
     * narsa to'xtamaydi. Muvaffaqiyatsiz so'rovni takrorlash esa har bir
     * sahifa ochilishini sekinlashtiradi.
     */
    retry: false,
  })
}

/**
 * Rasmni yuklash.
 *
 * `onSuccess` da faqat blob keshi tozalanadi. `photoKey` ni yangilash —
 * `AuthProvider` ishi (`refreshUser`), chunki u brauzerdagi nusxada ham
 * saqlanadi; komponent ikkalasini birga chaqiradi.
 */
export function useUploadMyPhoto() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (file: File) => uploadMyPhoto(file),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: accountPhotoKeys.root })
    },
  })
}

/**
 * Rasmni o'chirish.
 *
 * Keshdagi blob `invalidate` emas, `remove` qilinadi: `photoKey` `null`
 * bo'lgach unga hech kim murojaat qilmaydi, qayta so'rov esa faqat 404
 * olib kelardi.
 */
export function useDeleteMyPhoto() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => deleteMyPhoto(),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: accountPhotoKeys.root })
    },
  })
}
