import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { type ApiKeyScope, createApiKey, fetchApiKeys, revokeApiKey } from './api-keys-api'

/**
 * Kalitlar ro'yxati BITTA ildizdan: ro'yxat kichik (bir necha yozuv) va
 * har qanday amaldan keyin butunlay yangilanadi — `userKeys` dagi bilan
 * bir xil sabab.
 */
export const apiKeyKeys = {
  root: ['apiKeys'] as const,
}

/**
 * Tashqi API kalitlari.
 *
 * `enabled` — ruxsat tekshiruvi CHAQIRUVCHIDA: `GET /api/api-keys` ham
 * `settings.manage` bilan himoyalangan, ya'ni ruxsatsiz odamda so'rov
 * umuman yuborilmasligi kerak (`useSystemSettings` dagi bilan bir xil
 * fikr) — aks holda sahifa ochilganda konsolga behuda 403 tushardi.
 */
export function useApiKeys(enabled: boolean) {
  return useQuery({
    queryKey: apiKeyKeys.root,
    queryFn: ({ signal }) => fetchApiKeys(signal),
    enabled,
  })
}

/**
 * Yangi kalit.
 *
 * ⚠️ Javobdagi to'liq kalit React Query'ning MUTATSIYA KESHIDA ham
 * qoladi (`gcTime` tugaguncha). Shu sababli chaqiruvchi oyna yopilganda
 * `reset()` ni chaqirishi SHART — `api-key-create-dialog.tsx` aynan
 * shunday qiladi va sababi ham o'sha yerda yozilgan.
 */
export function useCreateApiKey() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ name, scope, departmentId }: { name: string; scope: ApiKeyScope; departmentId?: string }) =>
      createApiKey(name, scope, departmentId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: apiKeyKeys.root })
    },
  })
}

/**
 * Bekor qilish.
 *
 * Yozuv O'CHIRILMAYDI — u ro'yxatda kulrang bo'lib qoladi, shu sababli
 * ro'yxat baribir qayta o'qiladi: server `revokedAt` va `isActive` ni
 * yangilagan qatorni qaytaradi.
 */
export function useRevokeApiKey() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => revokeApiKey(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: apiKeyKeys.root })
    },
  })
}
