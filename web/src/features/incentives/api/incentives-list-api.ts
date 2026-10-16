/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employments-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Endpoint ALLAQACHON TAYYOR va testlangan, shu sababli maydon nomlari
 * «kutilayotgan shartnoma» emas — ular haqiqiy manbadan olingan:
 *   api/src/Bonvi.Api/Endpoints/IncentiveEndpoints.cs (GET /api/incentives)
 *   api/src/Bonvi.Application/Employees/Incentives/ListIncentivesQuery.cs
 *   api/src/Bonvi.Application/Employees/Dtos/IncentiveDtos.cs (IncentiveListItemDto)
 *
 * NEGA fayl nomi `incentives-api.ts` EMAS (feature nomi shunday bo'lsa
 * ham): `web/src/features/employees/api/incentives-api.ts` allaqachon bor
 * — u xodim KARTASIDAGI tab bilan ishlaydi (berish, tahrirlash,
 * o'chirish). Ikkita bir xil nomli fayl kod bo'ylab qidirishda doimiy
 * chalkashlik bo'lardi, shu sababli bu yerdagisi «list» bilan ajratilgan:
 * u FAQAT umumiy ro'yxatni o'qiydi va hech narsani o'zgartirmaydi.
 *
 * Sanoq turi (`IncentiveKindValue`) va tur/pul yordamchilari qayta
 * yozilmaydi — ular xodim kartasidagi klientda ALLAQACHON bor va ikkala
 * ekran bitta domen tushunchasini ikki xil o'qimasligi kerak.
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `incentives-list-queries.ts` generatsiya qilingan hook'ga o'tkaziladi.
 */
import {
  INCENTIVE_KINDS,
  percentValue,
  type IncentiveKindValue,
  type IncentiveMoney,
} from '@/features/employees/api/incentives-api'
import { revealProblemDetails, type PagedResult } from '@/features/employees/api/employees-api'
import { api } from '@/shared/api/http-client'
import { fileNameFromDisposition } from '@/shared/lib/download'
import { todayIso } from '@/shared/lib/today'

/**
 * Umumiy ro'yxatning bitta qatori — yozuv va uning EGASI.
 *
 * Xodim kartasidagi `Incentive` dan farqi ikki tomonlama: bu yerda xodim
 * ma'lumoti QO'SHILGAN (har qator boshqa odamniki bo'lishi mumkin),
 * sabab va sovg'a esa OLIB TASHLANGAN — jadval shakli foydalanuvchi
 * bilan kelishilgan: sana · xodim · turi · summa · buyruq.
 */
export interface IncentiveListItem {
  id: string
  employeeId: string
  employeeName: string
  photoKey: string | null
  kind: IncentiveKindValue
  typeId: string
  /** Ma'lumotnomadan o'chirilgan tur uchun `null` — kutilgan holat. */
  typeName: string | null
  date: string
  /**
   * `null` — tur pul bilan bog'liq EMAS (domen qoidasi: pulsiz turda
   * summa taqiqlangan). `salary.view` ruxsatiga BOG'LIQ EMAS — sababi
   * backend'dagi `ListIncentivesQueryHandler.Amount` izohida.
   */
  amount: IncentiveMoney | null
  /** Faqat pulli jarimada, `amount` o'rniga («4 %»). */
  percent: number | null
  /** Faqat rag'batda — jarima uchun buyruq chiqarilmaydi. */
  orderNo: string | null
  orderDate: string | null
}

export interface IncentiveListFilters {
  /** Xodim F.I.Sh. bo'yicha (buyruq raqami emas — backend shunday qidiradi). */
  search?: string
  kind?: IncentiveKindValue
  /** `DateOnly` → «2026-08-26». Ikki tomondan yopiq oraliq. */
  from?: string
  to?: string
  departmentId?: string
  legalEntityId?: string
  /** `date` | `-date` | `employee` | `-employee`. Bo'sh — server sukuti (`-date`). */
  sort?: string
  page: number
  pageSize: number
}

export async function fetchIncentiveList(
  filters: IncentiveListFilters,
  signal?: AbortSignal,
): Promise<PagedResult<IncentiveListItem>> {
  const { data } = await api.get<unknown>('/api/incentives', {
    params: listParams(filters),
    signal,
  })

  return readPage(data, toItem, filters.page, filters.pageSize)
}

/**
 * Excel eksportining filtrlari — ro'yxatniki bilan AYNAN bir xil, faqat
 * sahifasiz: fayl mos yozuvlarning hammasini oladi.
 */
export type IncentiveExportFilters = Omit<IncentiveListFilters, 'page' | 'pageSize'>

/** Eksport javobi: baytlar va ular qanday nom bilan saqlanishi. */
export interface IncentiveExportFile {
  blob: Blob
  fileName: string
}

/**
 * «Rag'bat va jarima» — ekrandagi filtr va saralash bilan Excel fayl.
 *
 * Parametrlar ro'yxat so'rovi bilan BITTA funksiyadan (`filterParams`)
 * yig'iladi: yangi filtr qo'shilsa, u ikkalasiga birga tushadi va «ekranda
 * boshqa, faylda boshqa» holati kelib chiqmaydi (`exportEmployees` naqshi).
 */
export async function exportIncentives(
  filters: IncentiveExportFilters,
  signal?: AbortSignal,
): Promise<IncentiveExportFile> {
  try {
    const response = await api.get<Blob>('/api/incentives/export', {
      params: filterParams(filters),
      responseType: 'blob',
      signal,
    })

    const disposition = response.headers['content-disposition']

    return {
      blob: response.data,
      fileName:
        fileNameFromDisposition(typeof disposition === 'string' ? disposition : null) ??
        `Bonvi_ragbat_jarima_${todayIso()}.xlsx`,
    }
  } catch (error) {
    // Xato tanasi ham Blob bo'lib keladi — «eksport.juda_kop» kodi o'qilsin.
    await revealProblemDetails(error)
    throw error
  }
}

function listParams(filters: IncentiveListFilters): Record<string, unknown> {
  return { page: filters.page, pageSize: filters.pageSize, ...filterParams(filters) }
}

function filterParams(filters: IncentiveExportFilters): Record<string, unknown> {
  const params: Record<string, unknown> = {}

  if (filters.search) params['search'] = filters.search
  if (filters.from) params['from'] = filters.from
  if (filters.to) params['to'] = filters.to
  if (filters.departmentId) params['departmentId'] = filters.departmentId
  if (filters.legalEntityId) params['legalEntityId'] = filters.legalEntityId
  if (filters.sort) params['sort'] = filters.sort

  // ASP.NET Core enum'ni NOM bo'yicha bog'laydi (registrga sezgir emas):
  // «award» → «Award». Noma'lum qiymat 400 beradi, shu sababli bu yerda
  // faqat ro'yxatdagi qiymat yuboriladi (manzil satri `readKind` bilan
  // allaqachon tozalangan).
  if (filters.kind !== undefined) {
    params['kind'] = filters.kind.charAt(0).toUpperCase() + filters.kind.slice(1)
  }

  return params
}

/* ─────────────────────────────────────────────────── xaritalash (map) */

function toItem(raw: unknown): IncentiveListItem {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    employeeName: text(box['employeeName']) ?? '—',
    photoKey: text(box['photoKey']),
    kind: toKind(box['kind']),
    typeId: text(box['typeId']) ?? '',
    typeName: text(box['typeName']),
    date: text(box['date']) ?? '',
    amount: money(box['amount']),
    percent: percentValue(box['percent']),
    orderNo: text(box['orderNo']),
    orderDate: text(box['orderDate']),
  }
}

/**
 * Enum ikki xil ko'rinishda yuradi: javobda SON (`"kind": 2`), so'rovda
 * NOM. Server tartibi C# qiymatlariga mos (1 dan boshlanadi):
 * `IncentiveKind.Award = 1` → `INCENTIVE_KINDS[0]`.
 */
function toKind(value: unknown): IncentiveKindValue {
  if (typeof value === 'number') {
    return INCENTIVE_KINDS[value - 1] ?? 'award'
  }

  if (typeof value === 'string') {
    const lowered = value.toLowerCase()
    return INCENTIVE_KINDS.find((name) => name.toLowerCase() === lowered) ?? 'award'
  }

  return 'award'
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/** Summa — `MoneyDto` obyekti (`{amount, currency, formatted}`) yoki `null`. */
function money(value: unknown): IncentiveMoney | null {
  if (value === null || typeof value !== 'object') return null

  const box = value as Record<string, unknown>
  const raw = box['amount']

  // `decimal` JSON'da son bo'lib chiqadi, lekin ba'zi serializator
  // sozlamalarida satr bo'lishi ham mumkin — xodim kartasidagi klient
  // ham ikkalasini o'qiydi va ikkisi bir xil qoidada qolishi kerak.
  const amount = typeof raw === 'number' ? raw : typeof raw === 'string' ? Number(raw) : null
  if (amount === null || !Number.isFinite(amount)) return null

  return { amount, currency: text(box['currency']) ?? 'UZS' }
}

/**
 * Sahifalangan javobni bir ko'rinishga keltiradi — `employments-api.ts`
 * dagi `readPage` bilan bir xil sabab: `PagedResult` uchun .NET tomonida
 * yagona kelishuv yo'q edi, endpoint yozilgan paytda tekshirilmagan.
 */
function readPage<TRaw, TItem>(
  data: unknown,
  map: (raw: TRaw) => TItem,
  fallbackPage: number,
  fallbackPageSize: number,
): PagedResult<TItem> {
  if (Array.isArray(data)) {
    const items = (data as TRaw[]).map(map)
    return { items, total: items.length, page: 1, pageSize: items.length }
  }

  const box = (data ?? {}) as Record<string, unknown>
  const rawItems = (Array.isArray(box['items']) ? box['items'] : box['data']) as
    | TRaw[]
    | undefined

  const items = (rawItems ?? []).map(map)

  const total = firstNumber(box['total'], box['totalCount'], box['totalItems'])
  const page = firstNumber(box['page'], box['pageNumber'], box['currentPage'])
  const pageSize = firstNumber(box['pageSize'], box['perPage'], box['size'])

  return {
    items,
    total: total ?? items.length,
    page: page ?? fallbackPage,
    pageSize: pageSize ?? fallbackPageSize,
  }
}

function firstNumber(...candidates: unknown[]): number | null {
  for (const candidate of candidates) {
    if (typeof candidate === 'number' && Number.isFinite(candidate)) return candidate
  }
  return null
}
