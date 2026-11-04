import { revealProblemDetails } from '@/features/employees/api/employees-api'
import { api } from '@/shared/api/http-client'
import { fileNameFromDisposition } from '@/shared/lib/download'

/** Ruxsat kodlari — backend'dagi `Permissions` bilan bir xil. */
export const EMPLOYEES_VIEW = 'employees.view'
export { SETTINGS_MANAGE } from '@/features/settings/api/settings-api'

/**
 * HRMentor dan olingan bitta blanka (SPEC-blankalar-hrmentor §6).
 *
 * Fayl Bonvi HR ning o'z omborida turadi: HRMentor o'chiq bo'lsa ham
 * yuklab olinadi.
 */
export interface Blank {
  id: string
  code: string
  title: string
  versionNo: number
  publishedAt: string | null
  fileName: string
  contentType: string
  sizeBytes: number
}

/**
 * «Blankalar» sahifasining hammasi — ro'yxat va HRMentor bilan oxirgi
 * aloqa holati.
 *
 * `lastError` — oxirgi urinish KODI (`hrmentor.unreachable`), matn emas:
 * ekran uni `errors.*` dan o'z tilida o'qiydi.
 */
export interface BlanksState {
  connected: boolean
  lastAttemptAt: string | null
  lastSuccessAt: string | null
  lastError: string | null
  textsRevision: number | null
  items: Blank[]
  /**
   * Blankalar qayerdan olingani (SPEC-hujjat-manbai §2): `platform` — HRMentor'da shu
   * saytga biriktirilganlar, `legacy` — kalit hali platformaga bog'lanmagan, eski ro'yxat.
   */
  source?: 'platform' | 'legacy' | null
}

export async function fetchBlanks(signal?: AbortSignal): Promise<BlanksState> {
  const { data } = await api.get<BlanksState>('/api/hrmentor/blanks', { signal })
  return { ...data, items: Array.isArray(data.items) ? data.items : [] }
}

/** «Hozir yangilash» — server HRMentor dan oladi (≤ 120 s) va yangi holatni qaytaradi. */
export async function syncBlanks(): Promise<BlanksState> {
  const { data } = await api.post<BlanksState>('/api/hrmentor/blanks/sync', null, { timeout: 150_000 })
  return { ...data, items: Array.isArray(data.items) ? data.items : [] }
}

/** Blanka fayli — nomi serverning `Content-Disposition` idan, bo'lmasa ro'yxatdagisi. */
export async function downloadBlank(blank: Blank): Promise<{ blob: Blob; fileName: string }> {
  try {
    const response = await api.get<Blob>(`/api/hrmentor/blanks/${blank.id}/file`, { responseType: 'blob' })
    const disposition = response.headers['content-disposition']

    return {
      blob: response.data,
      fileName: fileNameFromDisposition(typeof disposition === 'string' ? disposition : null) ?? blank.fileName,
    }
  } catch (error) {
    // Xato tanasi ham Blob bo'lib keladi — «hrmentor.blank_not_found» kodi o'qilsin
    await revealProblemDetails(error)
    throw error
  }
}
