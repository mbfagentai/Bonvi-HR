import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { fetchSettings, saveSettings, type SettingUpdate } from './settings-api'

export const settingsKeys = {
  root: ['systemSettings'] as const,
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
