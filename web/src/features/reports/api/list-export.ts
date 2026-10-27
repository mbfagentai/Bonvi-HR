import { useMutation } from '@tanstack/react-query'

import { revealProblemDetails } from '@/features/employees/api/employees-api'
import { api } from '@/shared/api/http-client'
import { fileNameFromDisposition, saveBlobAs } from '@/shared/lib/download'
import { todayIso } from '@/shared/lib/today'

/**
 * Ro'yxatning Excel eksporti — HAMMA ro'yxat uchun bitta yo'l
 * (`docs/SPEC-excel-eksport.md`, egasi 2026-10-22: «har bir qismda tanlangan
 * filtr ta'sir qilgan holatda Excelga yuklash»).
 *
 * Server shartnomasi har joyda bir xil: `GET <ro'yxat>/export` ro'yxat bilan
 * AYNAN bir xil filtr parametrlarini oladi (sahifasiz) va `.xlsx` qaytaradi.
 * Shu sababli chaqiruvchi o'z ro'yxat parametrlarini (`page`/`pageSize` siz)
 * beradi — ekranda nima bo'lsa, faylda ham shu.
 */
export interface ListExportRequest {
  /** Masalan `/api/recruiting/candidates/export`. */
  path: string
  /** Ro'yxat so'rovining filtr va saralash parametrlari. */
  params: Record<string, unknown>
  /** Server fayl nomini bermasa: `Bonvi_<slug>_<bugun>.xlsx`. */
  slug: string
}

export async function downloadListExport(
  request: ListExportRequest,
  signal?: AbortSignal,
): Promise<{ blob: Blob; fileName: string }> {
  try {
    const response = await api.get<Blob>(request.path, {
      params: request.params,
      responseType: 'blob',
      signal,
    })

    const disposition = response.headers['content-disposition']

    return {
      blob: response.data,
      fileName:
        fileNameFromDisposition(typeof disposition === 'string' ? disposition : null) ??
        `Bonvi_${request.slug}_${todayIso()}.xlsx`,
    }
  } catch (error) {
    // Xato tanasi ham Blob bo'lib keladi — «eksport.juda_kop» kodi o'qilsin
    await revealProblemDetails(error)
    throw error
  }
}

/**
 * Tugma bosilganda yuklab olish — `useExportEmployees` bilan bir xil:
 * mutatsiya (avtomatik takrorlanmaydi), `readOnly` — fayl yuklangani uchun
 * ekran keshini bekor qilishning ma'nosi yo'q.
 */
export function useListExport() {
  return useMutation({
    mutationFn: (request: ListExportRequest) => downloadListExport(request),
    onSuccess: ({ blob, fileName }) => saveBlobAs(blob, fileName),
    meta: { readOnly: true },
  })
}
