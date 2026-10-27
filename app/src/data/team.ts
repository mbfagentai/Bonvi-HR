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
  TimesheetCellDto,
  TimesheetTotalsDto,
} from '@/api/types'
import { readStorage, removeStorage, writeStorage } from '@/lib/storage'
import { monthKey } from '@/lib/time'
import { invalidateQuery, useQuery, type QueryResult } from '@/lib/use-query'
import type { AttendanceDay } from './attendance'
import { clearTeamPhotoCache } from './photo'

/**
 * «Jamoa» — bo'lim boshlig'ining o'z odamlari davomati (SPEC-jamoam §F).
 *
 * ⚠️ Bu yerda HECH QANDAY ruxsat qoidasi yo'q: kim kimni ko'rishini server
 * har so'rovda bazadan hisoblaydi (§B). Ilova faqat o'qiydi va «qaysi
 * bo'limlarni ko'raman» degan ro'yxatni hech qachon yubormaydi. Holatlar
 * ham serverniki — HR paneli bilan bitta qoida (`DashboardStatusRule`).
 */

/** «Ko'proq» qadami — server sukuti bilan bir xil. */
export const TEAM_PAGE_SIZE = 50

/** Server bitta sahifada beradigan eng ko'p qator (`pageSize` chegarasi). */
const TEAM_MAX_PAGE_SIZE = 200

/**
 * Panelning eng eski sanasi — server chegarasi (`jamoa.sana_notogri`, 400).
 * Manzilga qo'lda yozilgan undan eski sana so'rov yubormay bugunga tushadi.
 */
export const TEAM_MIN_DATE = '2000-01-01'

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
 * («+998 90 000 00 01») — terish uchun faqat raqamlar va boshidagi `+`.
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
  loadedRows.clear()
  wantedRows.clear()
  removeStorage(TEAM_HINT_KEY)
  // A'zolar rasmlari ham — boshqa xodim yoki boshliqligi olib tashlangan odam ularni ko'rmasin
  clearTeamPhotoCache()
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

/** Ekrandagi panel: ro'yxat, sanoq va lahza — bitta (kamdan-kam bir necha) javobdan. */
export interface TeamBoardView {
  date: string
  isToday: boolean
  asOf: string
  total: number
  counts: TeamBoardCountsDto
  items: TeamBoardItemDto[]
  totalItems: number
  /** Nechta qator so'ralgan (50, 100, …) — «Ko'proq» yana 50 qo'shadi. */
  loadedRows: number
  /** Serverda yana qator bor — «Ko'proq» tugmasi. */
  hasMore: boolean
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
 * Har bir panel kaliti uchun nechta qator O'QILGAN va «Ko'proq» bilan nechtasi
 * SO'RALGAN. Modul darajasida — keshdagi panelga qaytilganda (a'zo ekranidan)
 * birinchi fon yangilanishi ro'yxatni 50 qatorga qisqartirib yubormasin.
 */
const loadedRows = new Map<string, number>()
const wantedRows = new Map<string, number>()

/**
 * Jamoa paneli — «Ko'proq» bilan.
 *
 * ⭐ Har o'qish — avtomatik yangilash ham, «Ko'proq» ham — BITTA so'rov:
 * 1-sahifa, `pageSize` = ko'rinib turgan qatorlar soni (50, 100, … 200).
 * Sabab ikkita: (1) daqiqada bir yangilanadigan ro'yxatda odam holati
 * o'zgarib, saralashda o'rnini almashtiradi — bitta javob doim izchil, alohida
 * sahifalarni qo'shib borsak kimdir ikki marta chiqib, kimdir yo'qolardi;
 * (2) serverda tezlik chegarasi bor (daqiqasiga ~30) — har yangilash bir nechta
 * parallel so'rov bo'lsa, katta jamoada chegaraga tez yetilardi.
 *
 * 200 dan ortiq qator (bitta katta zavod) — 200 talik sahifalar ketma-ket;
 * odatiy jamoada bu holat yo'q.
 *
 * Kesh `useQuery` niki (modul darajasida): a'zo ekranidan qaytganda ro'yxat
 * skeletsiz, avvalgi joyida turadi — aylantirish holati ham shunga tayanadi.
 */
export function useTeamBoard(filter: TeamBoardFilter, enabled: boolean): TeamBoardResult {
  const key = enabled ? boardKey(filter) : null

  const query = useQuery<TeamBoardView>(key, () => {
    const id = boardKey(filter)
    const wanted = wantedRows.get(id)
    const rows = wanted ?? loadedRows.get(id) ?? TEAM_PAGE_SIZE

    return loadBoard(filter, rows).then(
      (view) => {
        loadedRows.set(id, view.loadedRows)
        if (wantedRows.get(id) === wanted) wantedRows.delete(id)
        return view
      },
      (cause: unknown) => {
        // Yiqilgan «Ko'proq» (masalan 429) keyingi avtomatik yangilashni ham yiqitmasin
        if (wantedRows.get(id) === wanted) wantedRows.delete(id)
        throw cause
      },
    )
  })

  const { refetch } = query
  const rows = query.data ? query.data.loadedRows : TEAM_PAGE_SIZE
  const hasMore = query.data ? query.data.hasMore : false

  const loadMore = useCallback(() => {
    if (key === null || !hasMore) return
    wantedRows.set(key, rows + TEAM_PAGE_SIZE)
    refetch()
  }, [key, rows, hasMore, refetch])

  return { ...query, loadMore }
}

/**
 * `rows` gacha qator: 200 gacha — bitta so'rov (`page=1&pageSize=rows`),
 * undan ko'pi — 200 talik sahifalar KETMA-KET (parallel emas: tezlik chegarasi).
 */
function loadBoard(filter: TeamBoardFilter, rows: number): Promise<TeamBoardView> {
  const wanted = Math.max(TEAM_PAGE_SIZE, rows)
  const pageSize = Math.min(TEAM_MAX_PAGE_SIZE, wanted)
  const pages = Math.ceil(wanted / pageSize)

  const read = (page: number): Promise<TeamBoardDto> =>
    api.getTeamBoard({
      date: filter.date,
      departmentId: filter.departmentId,
      status: filter.status,
      search: filter.search,
      page,
      pageSize,
    })

  const results: TeamBoardDto[] = []
  const next = (page: number): Promise<TeamBoardDto[]> =>
    read(page).then((result) => {
      results.push(result)
      // Server sahifalari tugadi — qolganini so'rash befoyda
      return page < pages && page < result.pageCount ? next(page + 1) : results
    })

  return next(1).then((all) => merge(all, pageSize * pages))
}

function merge(results: TeamBoardDto[], requestedRows: number): TeamBoardView {
  const first = results[0]
  if (!first) throw new Error('team board: no pages')

  // Sahifalar orasida kimdir siljigan bo'lsa (faqat 200 dan ortiqda) — bir odam bir marta
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
    loadedRows: requestedRows,
    hasMore: items.length < first.totalItems,
  }
}

/**
 * A'zoning oyi — ekranga qulay shaklda (kataklar taqvimdagidek tekis).
 *
 * ⚠️ `current` YO'Q (tugma holati faqat xodimning o'ziga) — `toMonth`
 * ataylab ishlatilmaydi, u `current` ni ko'chiradi.
 */
export interface TeamMemberMonth {
  employee: TeamMemberDto
  year: number
  month: number
  days: AttendanceDay[]
  totals: TimesheetTotalsDto
}

export function useTeamMemberMonth(employeeId: string, year: number, month: number): QueryResult<TeamMemberMonth> {
  return useQuery(employeeId ? `jamoa-xodim:${employeeId}:${monthKey(year, month)}` : null, () =>
    api.getTeamMemberMonth(employeeId, year, month).then((dto) => {
      remember(dto.employee)
      return {
        employee: dto.employee,
        year: dto.attendance.year,
        month: dto.attendance.month,
        days: dto.attendance.days.map((item) =>
          item.day.status === 'NotEmployed' ? outsideDay(item.day) : { ...item.day, marks: item.marks },
        ),
        totals: dto.attendance.totals,
      }
    }),
  )
}

/**
 * Qamrovdan tashqari (yoki ishlamagan) kun — faqat sana va holat qoladi.
 *
 * Server bunday kunni allaqachon bo'sh yuboradi; bu yerda yana bir bor
 * tozalanadi: biror sabab bilan vaqt yoki belgi kelib qolsa ham, boshliq
 * o'z jamoasida bo'lmagan kunni ko'rmasligi kerak.
 */
function outsideDay(day: TimesheetCellDto): AttendanceDay {
  return {
    date: day.date,
    status: 'NotEmployed',
    workedMinutes: 0,
    lateMinutes: 0,
    earlyLeaveMinutes: 0,
    breakTakenMinutes: 0,
    breakDeductionMinutes: 0,
    firstInAt: null,
    lastOutAt: null,
    planStartAt: null,
    planEndAt: null,
    mobileMarks: 0,
    marks: [],
  }
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
