import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { saveBlobAs } from '@/shared/lib/download'

import { downloadBlank, fetchBlanks, syncBlanks, type Blank } from './blanks-api'

export const blanksKeys = {
  root: ['hrmentor-blanks'] as const,
}

/** Blankalar ro'yxati; `enabled` — ruxsat tekshiruvi chaqiruvchida (ruxsatsiz so'rov yuborilmaydi). */
export function useBlanks(enabled: boolean) {
  return useQuery({
    queryKey: blanksKeys.root,
    queryFn: ({ signal }) => fetchBlanks(signal),
    enabled,
  })
}

/** «Hozir yangilash» — javob ro'yxatning o'zi, keshga to'g'ridan-to'g'ri yoziladi. */
export function useSyncBlanks() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: syncBlanks,
    onSuccess: (state) => client.setQueryData(blanksKeys.root, state),
  })
}

/** Bitta blankani diskka saqlash. */
export function useDownloadBlank() {
  return useMutation({
    mutationFn: (blank: Blank) => downloadBlank(blank),
    onSuccess: ({ blob, fileName }) => saveBlobAs(blob, fileName),
  })
}
