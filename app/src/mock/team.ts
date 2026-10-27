import type {
  AttendanceMarkDto,
  MyAttendanceDayDto,
  TeamBoardCountsDto,
  TeamBoardDto,
  TeamBoardItemDto,
  TeamBoardQuery,
  TeamBoardStatus,
  TeamDepartmentDto,
  TeamMemberAttendanceDto,
  TeamMemberDto,
  TimesheetCellDto,
  TimesheetTotalsDto,
} from '@/api/types'
import { daysInMonth, monthKey, pad, parseIsoDate, shiftMonth, tashkentParts, todayIso } from '@/lib/time'
import { MOCK_ME, tashkentToUtcIso } from './data'

/**
 * Soxta jamoa — «Jamoa» tabini serversiz namoyish qilish uchun (SPEC-jamoam §F).
 *
 * ⚠️ Shakllar va xato kodlari serverniki bilan AYNAN bir xil (`api/types.ts`).
 * Bu yerda faqat ma'lumot: mock xodim (`MOCK_ME`) «Ishlab chiqarish bo'limi»
 * ning boshlig'i, qamrovda besh bo'lim va ellikdan ortiq odam — «Ko'proq»
 * tugmasi ham ko'rinsin. Holatlar SANA va odamdan barqaror hosil qilinadi:
 * panel va a'zo oyi bir kunni bir xil ko'rsatadi, ertaga ham bo'sh ochilmaydi.
 */

export const MOCK_TEAM_DEPARTMENTS: TeamDepartmentDto[] = [
  { id: 'dep-1', name: 'Ishlab chiqarish bo‘limi', path: 'Ishlab chiqarish bo‘limi', level: 0, parentId: null },
  { id: 'dep-2', name: '1-smena', path: 'Ishlab chiqarish bo‘limi / 1-smena', level: 1, parentId: 'dep-1' },
  { id: 'dep-3', name: '2-smena', path: 'Ishlab chiqarish bo‘limi / 2-smena', level: 1, parentId: 'dep-1' },
  { id: 'dep-4', name: 'Sifat nazorati', path: 'Ishlab chiqarish bo‘limi / Sifat nazorati', level: 1, parentId: 'dep-1' },
  {
    id: 'dep-5',
    name: 'Laboratoriya',
    path: 'Ishlab chiqarish bo‘limi / Sifat nazorati / Laboratoriya',
    level: 2,
    parentId: 'dep-4',
  },
]

interface MockMember extends TeamMemberDto {
  departmentId: string
  /** Jadvalli (08:30–18:00, dushanba–juma) yoki jadvalsiz — prod'dagi kabi ikkalasi ham bor. */
  scheduled: boolean
  /** Necha oy oldin jamoaga kelgan — undan oldingi oy 404 `jamoa.xodim_topilmadi`. */
  joinedMonthsAgo: number
  /**
   * Kelgan oyning qaysi kuni jamoaga o'tgan (1 — oy boshidan). Undan oldingi
   * kunlar — `NotEmployed`: server qamrovdan tashqari kunni shunday beradi.
   */
  joinedDay: number
  seed: number
}

const LAST_NAMES = [
  'Abdullayev', 'Ahmedov', 'Alimov', 'Azimov', 'Boboyev', 'Ergashev', 'G‘aniyev', 'Hasanov',
  'Ibragimov', 'Islomov', 'Jo‘rayev', 'Karimov', 'Mirzayev', 'Nazarov', 'Normatov', 'Odilov',
  'Qodirov', 'Rahimov', 'Rustamov', 'Saidov', 'Sobirov', 'Tursunov', 'Umarov', 'Usmonov',
  'Xolmatov', 'Yusupov', 'Zokirov',
]

const FIRST_NAMES = [
  'Akmal', 'Bekzod', 'Doniyor', 'Eldor', 'Farrux', 'Jasur', 'Javohir', 'Mirjalol', 'Nodir',
  'Otabek', 'Rustam', 'Sardor', 'Sherzod', 'Ulug‘bek', 'Zafar', 'Dilnoza', 'Gulnora', 'Madina',
  'Nilufar', 'Sevara',
]

const POSITIONS = ['Operator', 'Ustasi', 'Mexanik', 'Elektrik', 'Laborant', 'Nazoratchi', 'Yuk tashuvchi']

/** Boshliqdan tashqari a'zolar soni — bitta sahifadan (50) oshsin. */
const MEMBER_COUNT = 57

function departmentName(id: string): string {
  return MOCK_TEAM_DEPARTMENTS.filter((item) => item.id === id).map((item) => item.name)[0] ?? ''
}

/**
 * ⚠️ Raqamlar ATAYLAB soxta: «+998 90 000 00 NN». Tasodifiy ko'rinadigan
 * raqam kimningdir haqiqiy telefoni bo'lib chiqishi mumkin — namoyishda
 * «Qo'ng'iroq qilish» bosilsa, begona odamga qo'ng'iroq ketardi.
 */
function fakePhone(n: number): string {
  const digits = String(10000 + (n % 10000)).slice(1)
  return `+998 90 000 ${digits.slice(0, 2)} ${digits.slice(2, 4)}`
}

function phoneOf(index: number): string | null {
  // Har o'n uchinchi odamda raqam yo'q — qo'ng'iroq tugmasi ham yo'q
  if (index % 13 === 7) return null
  return fakePhone(index)
}

function buildMembers(): MockMember[] {
  const head: MockMember = {
    employeeId: MOCK_ME.employeeId,
    fullName: MOCK_ME.fullName,
    hasPhoto: true,
    positionName: MOCK_ME.position,
    departmentName: departmentName('dep-1'),
    phone: fakePhone(0),
    departmentId: 'dep-1',
    scheduled: true,
    joinedMonthsAgo: 36,
    joinedDay: 1,
    seed: 3,
  }

  const members: MockMember[] = [head]
  const departments = ['dep-1', 'dep-2', 'dep-2', 'dep-3', 'dep-3', 'dep-4', 'dep-5']

  for (let index = 1; index <= MEMBER_COUNT; index += 1) {
    const first = FIRST_NAMES[(index * 7) % FIRST_NAMES.length] ?? 'Akmal'
    const last = LAST_NAMES[(index * 5) % LAST_NAMES.length] ?? 'Karimov'
    // Ayollar familiyasi «-ova»: ro'yxat haqiqiy bo'limga o'xshasin
    const female = ['Dilnoza', 'Gulnora', 'Madina', 'Nilufar', 'Sevara'].indexOf(first) >= 0
    const departmentId = departments[index % departments.length] ?? 'dep-2'

    members.push({
      employeeId: `emp-${100 + index}`,
      fullName: `${female ? `${last}a` : last} ${first}`,
      hasPhoto: index % 6 === 0,
      positionName: POSITIONS[index % POSITIONS.length] ?? null,
      departmentName: departmentName(departmentId),
      phone: phoneOf(index),
      departmentId,
      // Har to'rtinchisi jadvalsiz — prod'da jadval hali biriktirilmagan odamlar ko'p
      scheduled: index % 4 !== 1,
      joinedMonthsAgo: index % 9 === 4 ? 0 : 3 + (index % 20),
      // Bir qismi oy o'rtasida boshqa bo'limdan o'tib kelgan
      joinedDay: index % 3 === 0 ? 1 + (index % 17) : 1,
      seed: index * 37,
    })
  }

  return members.sort((a, b) => a.fullName.localeCompare(b.fullName))
}

const MEMBERS = buildMembers()

/** Bo'lim va uning butun avlodi — server `BranchIdsAsync` ma'nosi. */
function subtree(rootId: string): string[] {
  const ids = [rootId]
  for (let cursor = 0; cursor < ids.length; cursor += 1) {
    const parent = ids[cursor]
    MOCK_TEAM_DEPARTMENTS.forEach((item) => {
      if (item.parentId === parent && ids.indexOf(item.id) < 0) ids.push(item.id)
    })
  }
  return ids
}

/**
 * Kun va odamdan barqaror «tasodif» (0–99). Oxirida aralashtiriladi: usiz
 * qo'shni kunlar ketma-ket son berib, taqvimda bir haftalik «kechikish
 * seriyasi» chizilardi.
 */
function hashOf(seed: number, dateIso: string): number {
  let hash = seed
  for (let i = 0; i < dateIso.length; i += 1) hash = (hash * 31 + dateIso.charCodeAt(i)) % 100003
  hash = Math.imul(hash ^ (hash >>> 16), 0x45d9f3b)
  hash = Math.imul(hash ^ (hash >>> 16), 0x45d9f3b)
  return ((hash ^ (hash >>> 16)) >>> 0) % 100
}

/** Hozirgi lahza Toshkent kunining nechanchi daqiqasi. */
function nowMinute(): number {
  const parts = tashkentParts(new Date())
  return parts ? parts.hour * 60 + parts.minute : 12 * 60
}

const PLAN_START = 8 * 60 + 30
const PLAN_END = 18 * 60

interface MockMemberDay {
  status: TeamBoardStatus
  /** Toshkent kunining daqiqasi; `null` — o'tish yo'q. */
  arrived: number | null
  left: number | null
  lateMinutes: number
}

/**
 * Odamning kuni — HR panelidagi holatlar to'plami bilan. Bugun uchun hozirgi
 * soat hisobga olinadi: hali kelmagan odam smena boshlanmagan bo'lsa «Hali
 * boshlanmagan», boshlangan bo'lsa «Kelmadi».
 */
function memberDay(member: MockMember, dateIso: string): MockMemberDay {
  const parts = parseIsoDate(dateIso)
  const weekend = parts ? parts.weekday >= 6 : false
  const h = hashOf(member.seed, dateIso)
  const today = todayIso()

  let day: MockMemberDay

  if (weekend) {
    day = member.scheduled
      ? h < 6
        ? { status: 'DayOff', arrived: 9 * 60 + h, left: 15 * 60 + h, lateMinutes: 0 }
        : { status: 'DayOff', arrived: null, left: null, lateMinutes: 0 }
      : h < 20
        ? { status: 'Arrived', arrived: 9 * 60 + h, left: 16 * 60 + h, lateMinutes: 0 }
        : { status: 'NoSchedule', arrived: null, left: null, lateMinutes: 0 }
  } else if (member.seed % 11 === 3 && Math.abs(Date.parse(dateIso) - Date.parse(today)) <= 2 * 86_400_000) {
    // Ta'tildagi odam — tasdiqlangan so'rov, «Sababli»
    day = { status: 'Excused', arrived: null, left: null, lateMinutes: 0 }
  } else if (member.scheduled) {
    if (h < 7) {
      day = { status: 'Absent', arrived: null, left: null, lateMinutes: 0 }
    } else if (h < 22) {
      const arrived = PLAN_START + 1 + ((h * 3) % 40)
      day = { status: 'Late', arrived, left: PLAN_END + (h % 25), lateMinutes: arrived - PLAN_START }
    } else {
      day = { status: 'OnTime', arrived: 8 * 60 + 5 + (h % 25), left: PLAN_END + (h % 25), lateMinutes: 0 }
    }
  } else {
    day =
      h < 25
        ? { status: 'NoSchedule', arrived: null, left: null, lateMinutes: 0 }
        : { status: 'Arrived', arrived: 7 * 60 + 40 + (h % 60), left: 17 * 60 + (h % 50), lateMinutes: 0 }
  }

  if (dateIso !== today) return day

  // Bugun: kelajakdagi o'tish hali yo'q
  const now = nowMinute()
  const left = day.left !== null && day.left <= now ? day.left : null

  if (day.arrived !== null && day.arrived <= now) return { ...day, left }
  if (day.status === 'Excused' || day.status === 'DayOff' || day.status === 'NoSchedule') {
    return { ...day, arrived: null, left: null }
  }
  if (!member.scheduled) return { status: 'NoSchedule', arrived: null, left: null, lateMinutes: 0 }

  return { status: now < PLAN_START ? 'NotStarted' : 'Absent', arrived: null, left: null, lateMinutes: 0 }
}

function atMinute(dateIso: string, minute: number): string {
  return tashkentToUtcIso(dateIso, Math.floor(minute / 60), minute % 60)
}

function emptyCounts(): TeamBoardCountsDto {
  return { onTime: 0, late: 0, absent: 0, notStarted: 0, dayOff: 0, noSchedule: 0, excused: 0, arrived: 0 }
}

const COUNT_FIELD: Record<TeamBoardStatus, keyof TeamBoardCountsDto> = {
  OnTime: 'onTime',
  Late: 'late',
  Absent: 'absent',
  NotStarted: 'notStarted',
  DayOff: 'dayOff',
  NoSchedule: 'noSchedule',
  Excused: 'excused',
  Arrived: 'arrived',
}

export type MockTeamResult<T> = { ok: true; value: T } | { ok: false; status: number; code: string }

export function mockTeamScope(): { departments: TeamDepartmentDto[] } {
  return { departments: MOCK_TEAM_DEPARTMENTS.slice() }
}

/** Jamoaga o'tgan sana — panel ham, a'zo oyi ham qamrovni bir xil o'qisin. */
function joinedOn(member: MockMember): string {
  const parts = parseIsoDate(todayIso())
  if (!parts) return '2000-01-01'
  const start = shiftMonth(parts.year, parts.month, -member.joinedMonthsAgo)
  const day = Math.min(member.joinedDay, daysInMonth(start.year, start.month))
  return `${monthKey(start.year, start.month)}-${pad(day)}`
}

/** `GET /api/me/team/board` — server tartibi: sana → bo'lim → sanoq → qidiruv va holat → sahifa. */
export function mockTeamBoard(query: TeamBoardQuery): MockTeamResult<TeamBoardDto> {
  const today = todayIso()
  const date = query.date ?? today
  if (date < '2000-01-01' || date > today) return { ok: false, status: 400, code: 'jamoa.sana_notogri' }

  let scope = subtree('dep-1')
  if (query.departmentId !== null) {
    if (scope.indexOf(query.departmentId) < 0) return { ok: false, status: 404, code: 'jamoa.bolim_topilmadi' }
    scope = subtree(query.departmentId)
  }

  const rows: TeamBoardItemDto[] = MEMBERS.filter(
    (member) => scope.indexOf(member.departmentId) >= 0 && joinedOn(member) <= date,
  ).map(
    (member) => {
      const day = memberDay(member, date)
      return {
        employeeId: member.employeeId,
        fullName: member.fullName,
        hasPhoto: member.hasPhoto,
        positionName: member.positionName,
        departmentName: member.departmentName,
        arrivedAt: day.arrived !== null ? atMinute(date, day.arrived) : null,
        leftAt: day.left !== null ? atMinute(date, day.left) : null,
        status: day.status,
        lateMinutes: day.status === 'Late' ? day.lateMinutes : null,
        leftAuto: false,
        phone: member.phone,
      }
    },
  )

  const counts = emptyCounts()
  rows.forEach((row) => {
    counts[COUNT_FIELD[row.status]] += 1
  })

  const needle = query.search.trim().toLowerCase()
  const matching = rows.filter(
    (row) =>
      (query.status === null || row.status === query.status) &&
      (needle === '' || row.fullName.toLowerCase().indexOf(needle) >= 0),
  )

  const pageSize = Math.min(200, Math.max(1, query.pageSize))
  const page = Math.max(1, query.page)
  const start = (page - 1) * pageSize

  return {
    ok: true,
    value: {
      date,
      isToday: date === today,
      asOf: new Date().toISOString(),
      total: rows.length,
      counts,
      items: matching.slice(start, start + pageSize),
      page,
      pageSize,
      totalItems: matching.length,
      pageCount: Math.max(1, Math.ceil(matching.length / pageSize)),
    },
  }
}

function memberCell(member: MockMember, dateIso: string): MyAttendanceDayDto {
  const parts = parseIsoDate(dateIso)
  const weekend = parts ? parts.weekday >= 6 : false
  const today = todayIso()
  const planned = member.scheduled && !weekend

  const cell: TimesheetCellDto = {
    date: dateIso,
    status: 'NoSchedule',
    workedMinutes: 0,
    lateMinutes: 0,
    earlyLeaveMinutes: 0,
    firstInAt: null,
    lastOutAt: null,
    breakTakenMinutes: 0,
    breakDeductionMinutes: 0,
    planStartAt: planned ? atMinute(dateIso, PLAN_START) : null,
    planEndAt: planned ? atMinute(dateIso, PLAN_END) : null,
    mobileMarks: 0,
    isFlexible: false,
    plannedMinutes: planned ? PLAN_END - PLAN_START - 60 : 0,
    shortfallMinutes: 0,
    personalMinutes: 0,
  }

  if (dateIso > today) {
    const status = planned ? 'Planned' : weekend && member.scheduled ? 'DayOff' : 'NoSchedule'
    return { day: { ...cell, status }, marks: [] }
  }

  const day = memberDay(member, dateIso)
  const marks: AttendanceMarkDto[] = []

  if (day.arrived === null) {
    const status =
      day.status === 'Absent'
        ? 'Absent'
        : day.status === 'Excused'
          ? 'Excused'
          : day.status === 'DayOff'
            ? 'DayOff'
            : day.status === 'NotStarted'
              ? 'Planned'
              : 'NoSchedule'
    return { day: { ...cell, status }, marks }
  }

  marks.push({ at: atMinute(dateIso, day.arrived), direction: 'In', source: 'Terminal', siteName: null })
  // Tushlikka chiqib-kirish — jadvalli kunlarning bir qismida
  const lunch = planned && day.left !== null && hashOf(member.seed + 1, dateIso) % 3 === 0
  if (lunch) {
    marks.push({ at: atMinute(dateIso, 13 * 60 + 2), direction: 'Out', source: 'Terminal', siteName: null })
    marks.push({ at: atMinute(dateIso, 13 * 60 + 54), direction: 'In', source: 'Terminal', siteName: null })
  }
  if (day.left !== null) {
    marks.push({ at: atMinute(dateIso, day.left), direction: 'Out', source: 'Terminal', siteName: null })
  }

  // Tanaffus — server qoidasi kabi bir soat ayiriladi (tashqarida o'tgan qismi ikki marta emas)
  const breakTaken = lunch ? 52 : 0
  const breakDeduction = planned ? 60 - breakTaken : 0
  const worked = day.left !== null ? Math.max(0, day.left - day.arrived - breakTaken - breakDeduction) : 0

  return {
    day: {
      ...cell,
      status: day.left === null ? 'MissingExit' : weekend && member.scheduled ? 'DayOffWorked' : 'Present',
      workedMinutes: worked,
      lateMinutes: day.lateMinutes,
      firstInAt: atMinute(dateIso, day.arrived),
      lastOutAt: day.left !== null ? atMinute(dateIso, day.left) : null,
      breakTakenMinutes: breakTaken,
      breakDeductionMinutes: breakDeduction,
    },
    marks,
  }
}

function totalsOf(days: readonly MyAttendanceDayDto[]): TimesheetTotalsDto {
  return {
    workedMinutes: days.reduce((sum, item) => sum + item.day.workedMinutes, 0),
    plannedMinutes: days.reduce((sum, item) => sum + (item.day.plannedMinutes ?? 0), 0),
    presentDays: days.filter((item) => item.day.status === 'Present').length,
    absentDays: days.filter((item) => item.day.status === 'Absent').length,
    lateMinutes: days.reduce((sum, item) => sum + item.day.lateMinutes, 0),
    earlyLeaveMinutes: 0,
    shortfallMinutes: 0,
    personalMinutes: 0,
  }
}

/** Oylar farqi: `year-month` bugungi oydan necha oy oldin. */
function monthsAgo(year: number, month: number): number {
  const parts = parseIsoDate(todayIso())
  if (!parts) return 0
  return (parts.year - year) * 12 + (parts.month - month)
}

/** Qamrovdan tashqari kun — server shakli: ishlamagan kundek, belgisiz va vaqtsiz. */
function outsideCell(dateIso: string): MyAttendanceDayDto {
  return {
    day: {
      date: dateIso,
      status: 'NotEmployed',
      workedMinutes: 0,
      lateMinutes: 0,
      earlyLeaveMinutes: 0,
      firstInAt: null,
      lastOutAt: null,
      breakTakenMinutes: 0,
      breakDeductionMinutes: 0,
      planStartAt: null,
      planEndAt: null,
      mobileMarks: 0,
    },
    marks: [],
  }
}

/**
 * `GET /api/me/team/members/{id}/attendance` — qamrovdan tashqari yoki
 * kelmasidan oldingi oy — 404; kelgan oyning o'tishdan oldingi kunlari —
 * `NotEmployed`. `current` YO'Q: server uni boshliqqa bermaydi.
 */
export function mockTeamMemberMonth(
  employeeId: string,
  year: number,
  month: number,
): MockTeamResult<TeamMemberAttendanceDto> {
  const member = MEMBERS.filter((item) => item.employeeId === employeeId)[0]
  if (!member) return { ok: false, status: 404, code: 'jamoa.xodim_topilmadi' }

  const ago = monthsAgo(year, month)
  if (month < 1 || month > 12 || ago < 0) return { ok: false, status: 400, code: 'davomat.oy_notogri' }
  if (ago > member.joinedMonthsAgo) return { ok: false, status: 404, code: 'jamoa.xodim_topilmadi' }

  const firstDay = ago === member.joinedMonthsAgo ? member.joinedDay : 1
  const days: MyAttendanceDayDto[] = []
  for (let day = 1; day <= daysInMonth(year, month); day += 1) {
    const dateIso = `${monthKey(year, month)}-${pad(day)}`
    days.push(day < firstDay ? outsideCell(dateIso) : memberCell(member, dateIso))
  }

  return {
    ok: true,
    value: {
      employee: {
        employeeId: member.employeeId,
        fullName: member.fullName,
        hasPhoto: member.hasPhoto,
        positionName: member.positionName,
        departmentName: member.departmentName,
        phone: member.phone,
      },
      attendance: {
        year,
        month,
        days,
        totals: totalsOf(days),
      },
    },
  }
}

/** Rasm bormi — `GET /api/me/team/members/{id}/photo` 404 yoki bayt. */
export function mockTeamHasPhoto(employeeId: string): boolean {
  return MEMBERS.some((item) => item.employeeId === employeeId && item.hasPhoto)
}
