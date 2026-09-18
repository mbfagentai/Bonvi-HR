/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`settings-api.ts` va
 * `employees-api.ts` boshidagi izohlar bilan bir xil naqsh).
 *
 * Tiplar TAXMIN bilan emas, haqiqiy shartnomadan olingan:
 *   api/src/Bonvi.Api/Endpoints/ApiKeyEndpoints.cs
 *   api/src/Bonvi.Application/PublicApi/Dtos/  (`ApiKeyDto`, `ApiKeyCreatedDto`)
 * `./bonvi gen` dan keyin ular `@/shared/api/generated/model` da ham bor —
 * lekin generatsiya qilingan kod git'ga tushmaydi, shu sababli bu bo'lim
 * ham qolgan bo'limlar kabi o'z tipini saqlaydi.
 *
 * <b>Marshrutlar `/api/settings` ICHIDA emas</b> va bu ataylab: sozlama —
 * kalit-qiymat, kalitlar esa YOZUVLAR ro'yxati (sababi backend'dagi
 * `ApiKeyEndpoints` izohida). Ekranda ikkalasi bir sahifada ko'rinadi,
 * API'da esa alohida — shu sababli bu fayl `settings-api.ts` ga
 * qo'shilmadi, yonida turadi.
 */
import { api } from '@/shared/api/http-client'

/**
 * Ro'yxatdagi bitta kalit.
 *
 * ⚠️ Kalitning O'ZI bu yerda YO'Q va bo'lishi ham mumkin emas: bazada
 * faqat SHA-256 xeshi saqlanadi. To'liq kalit faqat yaratilgan paytda,
 * faqat bitta javobda ko'rinadi (`CreatedApiKey`).
 */
export interface ApiKeySummary {
  id: string
  /** Qaysi loyihaga berilgan — «BonviCall». */
  name: string
  /** Kalitning OCHIQ qismi (8 belgi). Ro'yxatda va loglarda faqat shu ko'rinadi. */
  prefix: string
  createdAt: string
  /**
   * Oxirgi so'rov vaqti. Aniqligi — DAQIQA: server yozuvni har so'rovda
   * emas, daqiqada bir marta yangilaydi (sababi backend'da).
   * `null` — kalit hali ishlatilmagan.
   */
  lastUsedAt: string | null
  /** Oxirgi so'rov kelgan IP. `null` — hali so'rov bo'lmagan. */
  lastUsedIp: string | null
  /** `null` — kalit kuchda. */
  revokedAt: string | null
  /** Serverning xulosasi: kalit hozir ishlaydimi. */
  isActive: boolean
}

/**
 * Endigina yaratilgan kalit — <b>to'liq kalit ko'rinadigan YAGONA joy</b>.
 *
 * ⚠️ Bu obyektni keshga, `localStorage` ga yoki global holatga
 * SAQLAMANG: oyna yopilganda u xotiradan ham yo'qolishi kerak
 * (`api-key-create-dialog.tsx` dagi izoh).
 */
export interface CreatedApiKey {
  id: string
  name: string
  prefix: string
  /** «bonvi_<prefiks8>_<sir32>». Server bu qiymatni boshqa hech qachon bermaydi. */
  key: string
}

/** Server chegarasi (`CreateApiKeyCommandHandler`) — forma ham shu bilan tekshiradi. */
export const API_KEY_NAME_MAX_LENGTH = 120

export async function fetchApiKeys(signal?: AbortSignal): Promise<ApiKeySummary[]> {
  const { data } = await api.get<unknown>('/api/api-keys', { signal })
  return Array.isArray(data) ? (data as ApiKeySummary[]) : []
}

export async function createApiKey(name: string): Promise<CreatedApiKey> {
  const { data } = await api.post<CreatedApiKey>('/api/api-keys', { name })
  return data
}

/**
 * Bekor qilish. Amal IDEMPOTENT — allaqachon bekor qilingan kalitda ham
 * 200 va YANGILANGAN qator qaytadi, xato emas.
 *
 * Tanasi yo'q: server marshrutdan faqat `id` ni oladi.
 */
export async function revokeApiKey(id: string): Promise<ApiKeySummary> {
  const { data } = await api.post<ApiKeySummary>(`/api/api-keys/${id}/revoke`)
  return data
}
