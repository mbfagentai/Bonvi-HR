import { useCallback } from 'react'

import { api } from '@/api/client'
import type {
  TeamBoardCountsDto,
  TeamBoardDto,
  TeamBoardItemDto,
  TeamBoardStatus,
  TeamDepartmentDto,
  TeamMemberDto,
  TeamScopeDto,
} from '@/api/types'
import { readStorage, removeStorage, writeStorage } from '@/lib/storage'
import { monthKey } from '@/lib/time'
import { invalidateQuery, useQuery, type QueryResult } from '@/lib/use-query'
import { toMonth, type AttendanceMonth } from './attendance'

/**
 * «Jamoa» — bo'lim boshlig'ining o'z odamlari davomati (SPEC-jamoam §F).
 *
 * ⚠️ Bu yerda HECH QANDAY ruxsat qoidasi yo'q: kim kimni ko'rishini server
 * har so'rovda bazadan hisoblaydi (§B). Ilova faqat o'qiydi va «qaysi
 * bo'limlarni ko'raman» degan ro'yxatni hech qachon yubormaydi. Holatlar
 * ham serverniki — HR paneli bilan bitta qoida (`DashboardStatusRule`).
 */

/** Bitta sahifa — server sukuti bilan bir xil (eng ko'pi 200). */
export const TEAM_PAGE_SIZE = 50

/** Bugungi panel shuncha vaqtda bir yangilanadi (ekran ko'rinib turganda). */
export const TEAM_REFRESH_MS = 60_000

/**
 * Sanoq kartalari — HR panelidagi tartib (`DASHBOARD_STATUSES`): kelganlar,
 * keyin kelmaganlar, keyin kunning shakli.
 */
export const TEAM_STATUS_ORDER: readonly TeamBoardStatus[] = [
  'OnTime',
  'Late',
  'Arrived',
  'Absent',
  'Excused',
  'NotStarted',
  'DayOff',
  'NoSchedule',
]

/**
 * DOIM ko'rinadigan kartalar (SPEC §F): Keldi, Kechikdi, Kelmadi, Sababli,
 * Hali boshlanmagan, Dam olish. Qolgan ikkitasi («Vaqtida», «Jadval yo'q»)
 * faqat odam bo'lsa chiqadi — aks holda jadvalli jamoada vaqtida kelganlar,
 * jadvalsizida esa o'tmaganlar hech bir kartaga tushmay qolardi.
 */
const ALWAYS_SHOWN: readonly TeamBoardStatus[] = ['Late', 'Arrived', 'Absent', 'Excused', 'NotStarted', 'DayOff']

export const TEAM_STATUS_COUNT: Record<TeamBoardStatus, keyof TeamBoardCountsDto> = {
  OnTime: 'onTime',
  Late: 'late',
  Arrived: 'arrived',
  Absent: 'absent',
  Excused: 'excused',
  NotStarted: 'notStarted',
  DayOff: 'dayOff',
  NoSchedule: 'noSchedule',
}

/** Ko'rsatiladigan kartalar — tanlangan filtr nol bo'lsa ham o'z kartasi yo'qolmaydi. */
export function visibleStatuses(counts: TeamBoardCountsDto | null, active: TeamBoardStatus | null): TeamBoardStatus[] {
  return TEAM_STATUS_ORDER.filter(
    (status) =>
      ALWAYS_SHOWN.indexOf(status) >= 0 ||
      status === active ||
      (counts !== null && counts[TEAM_STATUS_COUNT[status]] > 0),
  )
}

/**
 * Manzildagi holat — o'zbekcha so'z (`?holat=kelmadi`), enum nomi emas:
 * havola qo'ng'iroqda o'qib berilganda ekrandagi tilda eshitilishi kerak
 * (`router.tsx` boshidagi qoida).
 */
const STATUS_SLUG: Record<TeamBoardStatus, string> = {
  OnTime: 'vaqtida',
  Late: 'kechikdi',
  Arrived: 'keldi',
  Absent: 'kelmadi',
  Excused: 'sababli',
  NotStarted: 'boshlanmagan',
  DayOff: 'dam',
  NoSchedule: 'jadvalsiz',
}

export function statusSlug(status: TeamBoardStatus): string {
  return STATUS_SLUG[status]
}

export function statusFromSlug(value: string | null): TeamBoardStatus | null {
  if (!value) return null
  return TEAM_STATUS_ORDER.filter((status) => STATUS_SLUG[status] === value)[0] ?? null
}

/**
 * `tel:` havolasi. Server raqamni ko'rsatish uchun formatlaydi
 * («+998 97 666 26 75») — terish uchun faqat raqamlar va boshidagi `+`.
 */
export function phoneHref(phone: string | null): string | null {
  if (!phone) return null
  const digits = phone.replace(/[^\d+]/g, '')
  const clean = digits.charAt(0) === '+' ? `+${digits.slice(1).replace(/\+/g, '')}` : digits.replace(/\+/g, '')
  return clean.replace(/\D/g, '').length >= 7 ? `tel:${clean}` : null
}

/**
 * Bo'lim tanlagichidagi qatorlar: qamrov ildizlari — chekinishsiz, qolgani
 * qamrov ICHIDAGI ota-bobolari soniga qarab ichkariga (daraxt tartibi
 * serverniki). `level` butun daraxtdagi chuqurlik — ikki xil chuqurlikdagi
 * bo'limni boshqaradigan boshliqda u ildizlarni ham chekintirib yuborardi.
 */
export interface TeamDepartmentOption {
  id: string
  name: string
  depth: number
}

export function departmentOptions(departments: readonly TeamDepartmentDto[]): TeamDepartmentOption[] {
  const parentOf: Record<string, string | null> = {}
  departments.forEach((item) => {
    parentOf[item.id] = item.parentId
  })

  return departments.map((item) => {
    let depth = 0
    let parent = item.parentId
    // Halqa bo'lmaydi (server tekshiradi), lekin chegara baribir — cheksiz aylanish qimmat
    while (parent !== null && parentOf[parent] !== undefined && depth < departments.length) {
      depth += 1
      parent = parentOf[parent] ?? null
    }
    return { id: item.id, name: item.name, depth }
  })
}

/* ─────────────────────────────────────────────── ma'lum a'zolar */

/**
 * Paneldan va oy javobidan ko'rilgan a'zolar — `employeeId` bo'yicha.
 *
 * NEGA: a'zo ekrani ochilganda sarlavha (ism, rasm, telefon) oy yuklanishini
 * kutmasdan darhol chiziladi; oy 404 bilan qaytsa ham (xodim o'sha oyda
 * jamoada bo'lmagan) kim haqida gap ketayotgani ekranda qoladi. Hisobdan
 * chiqishda tozalanadi.
 */
const knownMembers = new Map<string, TeamMemberDto>()

export function clearTeamCache(): void {
  knownMembers.clear()
  loadedPages.clear()
  wantedPages.clear()
  removeStorage(TEAM_HINT_KEY)
}

/**
 * «Bu telefondagi xodim boshliq edi» — faqat tab paneli uchun ISHORA.
 *
 * NEGA: `me` har ochilishda serverdan keladi va shu paytgacha tab paneli
 * to'rt tab bilan chizilib, keyin beshinchisi «sakrab» chiqardi — boshliq
 * buni har safar ko'rardi. Ishora hech narsani ochmaydi: ruxsatni server har
 * so'rovda o'zi tekshiradi, `me` kelgach esa ishora uning qiymatiga almashadi.
 * Kirish/chiqishda va boshliqlik olib tashlanganda o'chiriladi.
 */
const TEAM_HINT_KEY = 'bonvi.ilova.boshliq-ishora'

export function readTeamHint(): boolean {
  return readStorage(TEAM_HINT_KEY) === '1'
}

export function writeTeamHint(isTeamLead: boolean): void {
  if (isTeamLead) writeStorage(TEAM_HINT_KEY, '1')
  else removeStorage(TEAM_HINT_KEY)
}

function remember(member: TeamMemberDto): void {
  knownMembers.set(member.employeeId, member)
}

function memberOf(item: TeamBoardItemDto): TeamMemberDto {
  return {
    employeeId: item.employeeId,
    fullName: item.fullName,
    hasPhoto: item.hasPhoto,
    positionName: item.positionName,
    departmentName: item.departmentName,
    phone: item.phone,
  }
}

export function knownMember(employeeId: string): TeamMemberDto | null {
  return knownMembers.get(employeeId) ?? null
}

/* ─────────────────────────────────────────────── so'rovlar */

/** Boshliqning qamrovi. `enabled: false` — so'rov ketmaydi (boshliq emas). */
export function useTeamScope(enabled: boolean): QueryResult<TeamScopeDto> {
  return useQuery(enabled ? 'jamoa' : null, () => api.getTeam())
}

export interface TeamBoardFilter {
  /** `null` — bugun. */
  date: string | null
  departmentId: string | null
  status: TeamBoardStatus | null
  search: string
}

/** Ekrandagi panel: o'qilgan sahifalar BIRLASHTIRILGAN, sanoq va lahza — birinchi sahifadan. */
export interface TeamBoardView {
  date: string
  isToday: boolean
  asOf: string
  total: number
  counts: TeamBoardCountsDto
  items: TeamBoardItemDto[]
  totalItems: number
  /** Nechta sahifa o'qilgan — «Ko'proq» keyingisini so'raydi. */
  loadedPages: number
  pageCount: number
}

export interface TeamBoardResult extends QueryResult<TeamBoardView> {
  /** Keyingi sahifa — mavjudi joyida qoladi, ro'yxat pastdan o'sadi. */
  loadMore: () => void
}

function boardKey(filter: TeamBoardFilter): string {
  return [
    'jamoa-panel',
    filter.date ?? 'bugun',
    filter.departmentId ?? 'hammasi',
    filter.status ?? 'hammasi',
    filter.search.trim().toLowerCase(),
  ].join(':')
}

/**
 * Har bir panel kaliti uchun nechta sahifa O'QILGAN va «Ko'proq» bilan nechtasi
 * SO'RALGAN. Modul darajasida — keshdagi panelga qaytilganda (a'zo ekranidan)
 * birinchi fon yangilanishi ro'yxatni bitta sahifaga qisqartirib yubormasin.
 */
const loadedPages = new Map<string, number>()
const wantedPages = new Map<string, number>()

/**
 * Jamoa paneli — «Ko'proq» bilan sahifalangan.
 *
 * ⭐ Har yangilashda O'QILGAN BARCHA sahifalar qaytadan olinadi (parallel),
 * faqat oxirgisi emas. Sabab: daqiqada bir yangilanadigan ro'yxatda odam
 * holati o'zgarib, saralashda o'rnini almashtiradi — faqat yangi sahifani
 * qo'shib borsak, kimdir ikki marta chiqib, kimdir umuman yo'qolardi. Odatiy
 * jamoa bir-ikki sahifa; besh sahifali bo'lim ham besh qisqa so'rov.
 *
 * Kesh `useQuery` niki (modul darajasida): a'zo ekranidan qaytganda ro'yxat
 * skeletsiz, avvalgi joyida turadi — aylantirish holati ham shunga tayanadi.
 */
export function useTeamBoard(filter: TeamBoardFilter, enabled: boolean): TeamBoardResult {
  const key = enabled ? boardKey(filter) : null

  const query = useQuery<TeamBoardView>(key, () => {
    const id = boardKey(filter)
    const wanted = wantedPages.get(id)
    const pages = wanted ?? loadedPages.get(id) ?? 1

    return loadBoard(filter, pages).then(
      (view) => {
        loadedPages.set(id, view.loadedPages)
        if (wantedPages.get(id) === wanted) wantedPages.delete(id)
        return view
      },
      (cause: unknown) => {
        // Yiqilgan «Ko'proq» keyingi avtomatik yangilashni ham yiqitmasin
        if (wantedPages.get(id) === wanted) wantedPages.delete(id)
        throw cause
      },
    )
  })

  const { refetch } = query
  const loaded = query.data ? query.data.loadedPages : 1
  const pageCount = query.data ? query.data.pageCount : 1

  const loadMore = useCallback(() => {
    if (key === null || loaded >= pageCount) return
    wantedPages.set(key, loaded + 1)
    refetch()
  }, [key, loaded, pageCount, refetch])

  return { ...query, loadMore }
}

function loadBoard(filter: TeamBoardFilter, pages: number): Promise<TeamBoardView> {
  const numbers: number[] = []
  for (let page = 1; page <= Math.max(1, pages); page += 1) numbers.push(page)

  return Promise.all(
    numbers.map((page) =>
      api.getTeamBoard({
        date: filter.date,
        departmentId: filter.departmentId,
        status: filter.status,
        search: filter.search,
        page,
        pageSize: TEAM_PAGE_SIZE,
      }),
    ),
  ).then((results) => merge(results))
}

function merge(results: TeamBoardDto[]): TeamBoardView {
  const first = results[0]
  if (!first) throw new Error('team board: no pages')

  // Sahifalar orasida kimdir siljigan bo'lsa — bir odam bir marta
  const seen: Record<string, true> = {}
  const items: TeamBoardItemDto[] = []
  results.forEach((result) =>
    result.items.forEach((item) => {
      if (seen[item.employeeId]) return
      seen[item.employeeId] = true
      items.push(item)
      remember(memberOf(item))
    }),
  )

  return {
    date: first.date,
    isToday: first.isToday,
    asOf: first.asOf,
    total: first.total,
    counts: first.counts,
    items,
    totalItems: first.totalItems,
    loadedPages: results.length,
    pageCount: Math.max(1, first.pageCount),
  }
}

/** A'zoning oyi — ekranga qulay shaklda (`toMonth`, taqvimdagining o'zi). */
export interface TeamMemberMonth {
  employee: TeamMemberDto
  month: AttendanceMonth
}

export function useTeamMemberMonth(employeeId: string, year: number, month: number): QueryResult<TeamMemberMonth> {
  return useQuery(employeeId ? `jamoa-xodim:${employeeId}:${monthKey(year, month)}` : null, () =>
    api.getTeamMemberMonth(employeeId, year, month).then((dto) => {
      remember(dto.employee)
      return { employee: dto.employee, month: toMonth(dto.attendance) }
    }),
  )
}

/**
 * Boshliqlik olib tashlandi (403 `jamoa.boshliq_emas`) — `me` qayta o'qiladi,
 * shunda «Jamoa» tabi ham yo'qoladi. Jamoa keshi ham tozalanadi: endi u
 * ko'rsatilmasligi kerak bo'lgan ma'lumot.
 *
 * Tab paneli `me` ni O'Z nusxasida ushlaydi (`useQuery` holati ekranlar orasida
 * bo'lishilmaydi) — shu sababli unga alohida xabar beriladi (`onTeamRevoked`).
 */
const revokedListeners: Array<() => void> = []

export function onTeamRevoked(listener: () => void): () => void {
  revokedListeners.push(listener)
  return () => {
    const index = revokedListeners.indexOf(listener)
    if (index >= 0) revokedListeners.splice(index, 1)
  }
}

export function invalidateTeam(): void {
  invalidateQuery('jamoa')
  invalidateQuery('jamoa-panel')
  invalidateQuery('jamoa-xodim')
  invalidateQuery('me')
  clearTeamCache()
  revokedListeners.slice().forEach((listener) => listener())
}
