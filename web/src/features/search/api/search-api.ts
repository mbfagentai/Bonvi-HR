/**
 * Global qidiruv klienti — `GET /api/search`.
 *
 * ⚠️ QO'LDA YOZILGAN (`dashboard-api.ts` dagi bilan bir xil sabab): tiplar
 * `Bonvi.Application/Search/Dtos/GlobalSearchDtos.cs` dan bir-bir ko'chirilgan.
 * `./bonvi gen` ishga tushirilganda generatsiya qilingan klientga almashadi.
 */
import {
  EMPLOYEE_FLAG_TYPES,
  EMPLOYEE_STATUSES,
  type EmployeeFlagType,
  type EmployeeStatus,
} from '@/features/employees/api/employees-api'
import {
  CANDIDATE_STATUSES,
  oneOf,
  VACANCY_STATUSES,
  type CandidateStatus,
  type VacancyStatus,
} from '@/features/recruiting/api/recruiting-api'
import { api } from '@/shared/api/http-client'

/** Server qidirmaydigan eng qisqa matn (`GlobalSearchQueryHandler.MinLength`). */
export const SEARCH_MIN_LENGTH = 2

export interface SearchEmployee {
  id: string
  fullName: string
  photoKey: string | null
  /** Munosabati yo'q xodimda `null`. */
  status: EmployeeStatus | null
  position: string | null
  department: string | null
  flags: EmployeeFlagType[]
}

export interface SearchCandidate {
  id: string
  fullName: string
  hasPhoto: boolean
  status: CandidateStatus
  vacancy: string | null
  stage: string | null
  createdAt: string
  hiredEmployeeId: string | null
}

export interface SearchVacancy {
  id: string
  title: string
  status: VacancyStatus
  openings: number
  candidateCount: number
}

/**
 * Guruh — `null` bo'lsa foydalanuvchida bu bo'limga ruxsat yo'q (guruh
 * umuman chizilmaydi); bo'sh `items` esa «qidirildi, topilmadi».
 */
export interface SearchGroup<T> {
  /** Topilganlarning HAMMASI — `items` dan ko'p bo'lishi mumkin. */
  total: number
  items: T[]
}

export interface GlobalSearchResult {
  query: string
  employees: SearchGroup<SearchEmployee> | null
  candidates: SearchGroup<SearchCandidate> | null
  vacancies: SearchGroup<SearchVacancy> | null
}

/** Server sanoqlarni «Active», «Blacklist», «Rejected» ko'rinishida yuboradi. */
interface RawEmployee extends Omit<SearchEmployee, 'status' | 'flags'> {
  status: string | null
  flags: string[]
}

interface RawCandidate extends Omit<SearchCandidate, 'status'> {
  status: string
}

interface RawVacancy extends Omit<SearchVacancy, 'status'> {
  status: string
}

interface RawResult {
  query: string
  employees: SearchGroup<RawEmployee> | null
  candidates: SearchGroup<RawCandidate> | null
  vacancies: SearchGroup<RawVacancy> | null
}

const isFlag = (value: string): value is EmployeeFlagType =>
  (EMPLOYEE_FLAG_TYPES as readonly string[]).includes(value)

export async function fetchGlobalSearch(
  query: string,
  signal?: AbortSignal,
): Promise<GlobalSearchResult> {
  const { data } = await api.get<RawResult>('/api/search', { params: { q: query }, signal })

  /*
    Sanoqlar TEKSHIRILADI (`oneOf` — `recruiting-api.ts` dagi bilan bir xil
    sabab): backend yangi holat qo'shsa, deploy paytida ochiq qolgan eski
    tab yuqori paneldagi qidiruv tufayli butun qobig'ini yiqitmasin —
    noma'lum qiymat eng zararsiz ko'rinishga tushadi. Xodimlar moduli
    holat va belgilarni kichik harf bilan biladi (`EMPLOYEE_STATUSES`).
  */
  return {
    query: data.query,
    employees: data.employees && {
      total: data.employees.total,
      items: data.employees.items.map((item) => ({
        ...item,
        status:
          item.status === null
            ? null
            : oneOf(item.status.toLowerCase(), EMPLOYEE_STATUSES, 'active'),
        flags: item.flags.map((flag) => flag.toLowerCase()).filter(isFlag),
      })),
    },
    candidates: data.candidates && {
      total: data.candidates.total,
      items: data.candidates.items.map((item) => ({
        ...item,
        status: oneOf(item.status, CANDIDATE_STATUSES, 'InProgress'),
      })),
    },
    vacancies: data.vacancies && {
      total: data.vacancies.total,
      items: data.vacancies.items.map((item) => ({
        ...item,
        status: oneOf(item.status, VACANCY_STATUSES, 'Open'),
      })),
    },
  }
}
