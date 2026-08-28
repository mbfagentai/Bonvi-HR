/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employees-api.ts` boshidagi
 * izohga qarang). Tiplar jonli API javobidan va domen qatlamidan olingan:
 *   api/src/Bonvi.Domain/Employees/EmployeeFlag.cs
 *   api/src/Bonvi.Application/Employees/Abstractions/IEmployeeReadModel.cs
 */
import { api } from '@/shared/api/http-client'
import {
  EMPLOYEE_FLAG_TYPES,
  type EmployeeFlagType,
} from '@/features/employees/api/employees-api'

/**
 * Ro'yxatdagi bitta belgi — xodim nomi bilan birga.
 *
 * `employees-api.ts` dagi `EmployeeFlag` dan farqi: u bitta xodim
 * profilida ko'rsatiladi va ismni bilishi shart emas. Bu esa ism bo'yicha
 * ro'yxat — xodim nomi va mas'ul shaxs ham keladi.
 */
export interface FlaggedEmployee {
  id: string
  employeeId: string
  employeeName: string
  /** Rasm kaliti — `EmployeePhoto` shu bo'yicha rasmni oladi. */
  photoKey: string | null
  type: EmployeeFlagType
  startDate: string | null
  /** `null` — belgi hali kuchda. */
  endDate: string | null
  reason: string | null
  responsibleName: string | null
}

/**
 * Tur bo'yicha ro'yxat.
 *
 * To'rt ro'yxat (rezerv, qora ro'yxat, nazorat, qobiliyatli) bitta
 * endpoint'dan keladi — server tomonda ham ular bitta jadval. Excel'da
 * bular to'rt xil varaq edi va bir odam ikki varaqda turganda ikkinchisi
 * yangilanmay qolardi.
 *
 * `onlyActive` sukut bo'yicha rost: HR ko'pincha «hozir kimlar qora
 * ro'yxatda» degan savolga javob qidiradi, tarix esa alohida so'raladi.
 *
 * `search` SERVERGA yuboriladi, klientda `filter()` qilinmaydi: server
 * ism yig'ish (қ/ғ/ҳ/ў/ё), pasport gomogliflari va telefon
 * ajratgichlarini tashlash qoidalarini biladi, klient esa yo'q. Bu
 * ro'yxatda bu ayniqsa muhim: «bu odam qora ro'yxatdami?» degan savolga
 * ismning yozilishiga qarab emas, JSHSHIR bilan javob berish kerak.
 */
export async function fetchFlagged(
  type: EmployeeFlagType,
  onlyActive: boolean,
  search: string,
  signal?: AbortSignal,
): Promise<FlaggedEmployee[]> {
  const params: Record<string, unknown> = { type: typeName(type), onlyActive }

  // Bo'sh qidiruv YUBORILMAYDI: «?search=» manzilni ham, so'rov keshini
  // ham keraksiz shovqin bilan to'ldirardi.
  if (search.trim() !== '') params['search'] = search

  const { data } = await api.get<unknown>('/api/employees/flagged', { params, signal })

  return Array.isArray(data) ? data.map(toFlagged) : []
}

/* ─────────────────────────────────────────────────── xaritalash (map) */

/**
 * Enum ikki xil ko'rinishda yuradi: javobda SON (`"type": 2`), so'rovda NOM
 * (`?type=Blacklist`). Nom yuborilgani ma'qul — so'rov logida `Blacklist`
 * ko'rinadi, `2` emas.
 */
function typeName(type: EmployeeFlagType): string {
  return type.charAt(0).toUpperCase() + type.slice(1)
}

function toType(value: unknown): EmployeeFlagType {
  // Server tartibi: 1 Reserve · 2 Blacklist · 3 Monitoring · 4 Capable.
  // Ro'yxatdagi tartib ham AYNAN shunday — indeks bo'yicha topiladi.
  if (typeof value === 'number') {
    return EMPLOYEE_FLAG_TYPES[value - 1] ?? 'monitoring'
  }

  if (typeof value === 'string') {
    const lowered = value.toLowerCase()
    return EMPLOYEE_FLAG_TYPES.find((name) => name === lowered) ?? 'monitoring'
  }

  return 'monitoring'
}

function toFlagged(raw: unknown): FlaggedEmployee {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    employeeName: text(box['employeeName']) ?? '—',
    photoKey: text(box['photoKey']),
    type: toType(box['type']),
    startDate: text(box['startDate']),
    endDate: text(box['endDate']),
    reason: text(box['reason']),
    responsibleName: text(box['responsibleName']),
  }
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}
