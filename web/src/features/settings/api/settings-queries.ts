import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  deleteBotLogo,
  fetchBotLogo,
  fetchSettings,
  saveSettings,
  uploadBotLogo,
  type SettingUpdate,
} from './settings-api'

export const settingsKeys = {
  root: ['systemSettings'] as const,

  /*
    Kalit rasm KALITINI o'z ichiga oladi: logotip almashganda kalit ham
    almashadi va React Query eski nusxani keshdan olmaydi. Rasm
    o'zgargani, lekin manzil o'sha bo'lgani — brauzer keshidagi eng
    ko'p uchraydigan tuzoq (`accountPhotoKeys` bilan bir xil naqsh).
  */
  logo: (key: string) => ['systemSettings', 'logo', key] as const,
}

/**
 * Tizim sozlamalari.
 *
 * `enabled` — ruxsat tekshiruvi CHAQIRUVCHIDA: `settings.manage` yo'q
 * foydalanuvchida so'rov umuman yuborilmasligi kerak, aks holda
 * sozlamalar oynasi har ochilganda 403 xatosi konsolga tushardi.
 */
export function useSystemSettings(enabled: boolean) {
  return useQuery({
    queryKey: settingsKeys.root,
    queryFn: ({ signal }) => fetchSettings(signal),
    enabled,
  })
}

/**
 * Bot logotipi — rasm baytlari.
 *
 * `key` bo'sh bo'lsa so'rov umuman yuborilmaydi: sozlamalar javobida
 * logotip yo'qligi allaqachon ma'lum va 404 ni bekorga chaqirishning
 * hojati yo'q.
 */
export function useBotLogo(key: string) {
  return useQuery({
    queryKey: settingsKeys.logo(key),
    queryFn: ({ signal }) => fetchBotLogo(signal),
    enabled: key !== '',
    staleTime: Infinity,
  })
}

export function useUploadBotLogo() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (file: File) => uploadBotLogo(file),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: settingsKeys.root }),
  })
}

export function useDeleteBotLogo() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => deleteBotLogo(),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: settingsKeys.root }),
  })
}

export function useSaveSettings() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (items: SettingUpdate[]) => saveSettings(items),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: settingsKeys.root })

      /*
        Bildirishnoma chegaralari o'zgargan bo'lishi mumkin — ro'yxat
        eski chegaralar bilan hisoblangan holicha qolmasin.
      */
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
  })
}
