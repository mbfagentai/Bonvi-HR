/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employees-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Endpoint ALLAQACHON TAYYOR va testlangan (boshqa agent backend'ni
 * yozgan, 647 test), shu sababli maydon nomlari «kutilayotgan
 * shartnoma» emas — ular VAZIFA brifidagi `EmployeeDocumentDto` dan
 * to'g'ridan-to'g'ri ko'chirilgan.
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `employees-queries.ts` dagi hook'lar generatsiya qilinganiga o'tkaziladi.
 */
import { api } from '@/shared/api/http-client'

/** `EmployeeDocumentDto` — xodim kartasiga biriktirilgan bitta hujjat. */
export interface EmployeeDocument {
  id: string
  employeeId: string
  documentTypeId: string
  /** Ro'yxatda guruhlash va ko'rsatish uchun — tur ma'lumotnomasidan. */
  documentTypeName: string
  fileName: string
  contentType: string
  sizeBytes: number
  note: string | null
  uploadedAt: string
}

/** `PUT` tanasi — `file` ATAYLAB YO'Q: tahrirlashda faylning o'zi almashtirilmaydi. */
export interface UpdateDocumentRequest {
  documentTypeId: string
  note: string | null
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

function number(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

function toDocument(raw: unknown): EmployeeDocument {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    documentTypeId: text(box['documentTypeId']) ?? '',
    documentTypeName: text(box['documentTypeName']) ?? '',
    fileName: text(box['fileName']) ?? '',
    contentType: text(box['contentType']) ?? '',
    sizeBytes: number(box['sizeBytes'], 0),
    note: text(box['note']),
    uploadedAt: text(box['uploadedAt']) ?? '',
  }
}

/** `documentTypeId` berilmasa — hammasi. Guruhlash klient tomonda bajariladi. */
export async function fetchDocuments(
  employeeId: string,
  documentTypeId: string | undefined,
  signal?: AbortSignal,
): Promise<EmployeeDocument[]> {
  const { data } = await api.get<unknown>(`/api/employees/${employeeId}/documents`, {
    params: documentTypeId === undefined ? undefined : { documentTypeId },
    signal,
  })
  return Array.isArray(data) ? data.map(toDocument) : []
}

/**
 * Hujjat yuklash — **multipart**.
 *
 * `Content-Type` bu yerda QO'YILMAYDI: axios `FormData` ni ko'rib o'zi
 * yozadi, chegara (boundary) satri bilan birga — sabab `http-client.ts`
 * da batafsil (xodim rasmi bilan bir xil muammo).
 */
export async function uploadDocument(
  employeeId: string,
  file: File,
  documentTypeId: string,
  note: string | null,
): Promise<EmployeeDocument> {
  const form = new FormData()
  form.append('file', file)
  form.append('documentTypeId', documentTypeId)
  if (note !== null) form.append('note', note)

  const { data } = await api.post<unknown>(`/api/employees/${employeeId}/documents`, form)
  return toDocument(data)
}

/**
 * Hujjat baytlari — BLOB, havola emas.
 *
 * NEGA `<a href="/api/…">` ishlamaydi: tizim `Authorization: Bearer`
 * sarlavhasi bilan ishlaydi, cookie yo'q. Brauzer bu so'rovga sarlavha
 * qo'sha olmaydi va server 401 qaytaradi — xodim rasmidagi bilan aynan
 * bir xil sabab (`employees-api.ts` dagi `fetchEmployeePhoto`ga qarang).
 */
export async function fetchDocumentFile(
  employeeId: string,
  documentId: string,
  signal?: AbortSignal,
): Promise<Blob> {
  const { data } = await api.get<Blob>(
    `/api/employees/${employeeId}/documents/${documentId}/file`,
    { responseType: 'blob', signal },
  )
  return data
}

/**
 * Tahrirlash — FAQAT tur va izoh. Faylning o'zi almashtirilmaydi:
 * noto'g'ri fayl yuklangan bo'lsa, bu tuzatish emas, boshqa fakt —
 * yozuv o'chirilib, yangisi yuklanadi (`UpdateDocumentRequest`da
 * `file` maydoni umuman yo'q, VAZIFA brifi shuni talab qiladi).
 */
export async function updateDocument(
  employeeId: string,
  documentId: string,
  body: UpdateDocumentRequest,
): Promise<EmployeeDocument> {
  const { data } = await api.put<unknown>(
    `/api/employees/${employeeId}/documents/${documentId}`,
    body,
  )
  return toDocument(data)
}

/** Server javobi `204`. */
export async function deleteDocument(employeeId: string, documentId: string): Promise<void> {
  await api.delete(`/api/employees/${employeeId}/documents/${documentId}`)
}
