/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employees-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Farqi `employments-api.ts` dagi bilan bir xil: bu yerda ham endpoint
 * ALLAQACHON TAYYOR va testlangan (boshqa agent backend'ni yozgan), shu
 * sababli maydon nomlari «kutilayotgan shartnoma» emas — ular haqiqiy
 * manbadan olingan:
 *   api/src/Bonvi.Domain/Employees/EmployeeCompetency.cs
 *   api/src/Bonvi.Api/Endpoints/EmployeeEndpoints.cs (`MapCompetency`)
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `employees-queries.ts` dagi hook'lar generatsiya qilinganiga o'tkaziladi.
 */
import { api } from '@/shared/api/http-client'

/**
 * Til kodlari — backend'dagi `LanguageCode` sanog'ining nusxasi.
 *
 * Tartib C# enum'ining RAQAMLI qiymatlariga mos (1 dan): `Uzbek = 1` →
 * `names[0]` — `enumValue` shu qoidaga tayanadi (`employees-api.ts` dagi
 * bilan bir xil kelishuv). Yangi til faqat OXIRIGA qo'shiladi.
 *
 * Bu ma'lumotnoma EMAS, qat'iy ro'yxat: dunyodagi tillar HR to'ldiradigan
 * narsa emas, shu sababli serverdan so'ralmaydi — ro'yxat kodda yashaydi
 * va `check-locales` har bir kod uchun 4 tilda tarjima borligini qadaydi.
 */
export const LANGUAGE_CODES = [
  'uzbek',
  'russian',
  'english',
  'karakalpak',
  'kazakh',
  'kyrgyz',
  'tajik',
  'turkmen',
  'azerbaijani',
  'turkish',
  'arabic',
  'persian',
  'chinese',
  'korean',
  'japanese',
  'german',
  'french',
  'spanish',
  'italian',
  'ukrainian',
  'hindi',
] as const
export type LanguageCode = (typeof LANGUAGE_CODES)[number]

/**
 * Daraja — backend'dagi `LanguageLevel`. 5 pog'ona, tushunarli so'zlar
 * bilan (CEFR'ning A1–C2 shkalasi ATAYLAB emas — HR suhbatdan taxminiy
 * baho qo'yadi, rasmiy shkala yolg'on aniqlik taassurotini berardi).
 */
export const LANGUAGE_LEVELS = [
  'elementary',
  'intermediate',
  'upperIntermediate',
  'advanced',
  'native',
] as const
export type LanguageLevel = (typeof LANGUAGE_LEVELS)[number]

/** Profildagi bitta til: kod + daraja. */
export interface LanguageSkill {
  language: LanguageCode
  level: LanguageLevel
}

/**
 * Xodimning qobiliyatlar profili (Excel: «91.15 Қобилятлар жадвали»).
 *
 * Bitta xodimda BITTA profil bo'ladi — `EmployeeFlag` yoki `Incentive`
 * kabi tarix emas: yangi ma'lumot eskisini TO'LIQ almashtiradi.
 */
export interface EmployeeCompetency {
  id: string
  employeeId: string
  /**
   * ⚠️ ESKI erkin matn («Рус тили , Инглиз тили») — faqat Excel'dan
   * ko'chgan yozuvlarda bor. Yangi kiritishlar `languageSkills`da;
   * matn tahrirlanmaydi, saqlashda O'ZGARISHSIZ qaytarib yuboriladi
   * (aks holda har saqlash uni bo'shatib qo'yardi — `PUT` to'liq
   * almashtirish).
   */
  languages: string | null
  /** Til bilish — strukturaviy ro'yxat: til + daraja. */
  languageSkills: LanguageSkill[]
  /** Kompyuter savodxonligi, 0–100%. */
  computerLiteracyPercent: number | null
  specialization: string | null
  otherAbilities: string | null
  interest: string | null
  educationPlace: string | null
  /**
   * Nomzodlik anketasidan KO'CHGAN qo'shimcha ma'lumot — tayyor matn.
   *
   * Xodim tomonida o'rni yo'q javoblar shu yerda yashaydi: ta'lim
   * darajasi, ish tajribasi, oilaviy holat, oylik kutilmasi va ishni
   * qayerdan eshitgani. Matn ko'chirish paytida yig'iladi va
   * O'ZBEKCHA bo'lib qoladi (sababi backend'dagi
   * `EmployeeCompetency.CandidateNote` izohida).
   */
  candidateNote: string | null
}

/**
 * `PUT` tanasi — butun profil bir yo'la yuboriladi, qisman yangilash yo'q.
 * Forma har doim BARCHA maydonlarni qaytaradi.
 */
export interface SaveCompetencyRequest {
  languages: string | null
  languageSkills: LanguageSkill[]
  computerLiteracyPercent: number | null
  specialization: string | null
  otherAbilities: string | null
  interest: string | null
  educationPlace: string | null
  candidateNote: string | null
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * Serverdan kelgan sanoq qiymatini frontend birlashmasiga keltiradi —
 * `employees-api.ts` dagi `enumValue` bilan bir xil kelishuv (u eksport
 * qilinmagan, shu sababli bu yerda takrorlanadi): server nom (`"English"`)
 * ham, raqam (`3`) ham yuborishi mumkin, ikkalasi ham tushuniladi.
 * Tanilmagan qiymat `null` — qator jimgina noto'g'ri tilga aylanmasdan
 * ro'yxatdan tushib qoladi.
 */
function enumOrNull<T extends string>(raw: unknown, names: readonly T[]): T | null {
  if (typeof raw === 'number') {
    return names[raw - 1] ?? null
  }

  if (typeof raw === 'string') {
    const needle = raw.toLowerCase()
    return names.find((name) => name.toLowerCase() === needle) ?? null
  }

  return null
}

/** Tillar massivi — tanilmagan yozuv TASHLAB YUBORILADI (himoyaviy o'qish). */
function skillList(raw: unknown): LanguageSkill[] {
  if (!Array.isArray(raw)) return []

  const out: LanguageSkill[] = []
  for (const item of raw) {
    const box = (item ?? {}) as Record<string, unknown>
    const language = enumOrNull(box['language'], LANGUAGE_CODES)
    const level = enumOrNull(box['level'], LANGUAGE_LEVELS)
    if (language !== null && level !== null) out.push({ language, level })
  }
  return out
}

/**
 * Frontend birlashmasini C# enum nomiga qaytaradi: «english» → «English».
 * ASP.NET Core enum'ni NOM bo'yicha bog'laydi — `employees-api.ts` dagi
 * `enumName` bilan bir xil qoida.
 */
function enumName(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

/** `int?` — son yoki `null`. Tushunarsiz qiymat ham `null` (himoyaviy o'qish). */
function intOrNull(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return Math.trunc(value)
  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? Math.trunc(parsed) : null
  }
  return null
}

/**
 * Javob tanasi umuman bo'sh — profil hali yaratilmagan.
 *
 * Server `Results.Ok(null)` qaytaradi, bu JSON `null` matniga aylanadi va
 * axios uni to'g'ridan-to'g'ri `null` qilib o'qiydi. `''`/`undefined` ham
 * shu bilan bir qatorda tekshiriladi — qo'shimcha ehtiyot chorasi, aslida
 * shu holat kelmasligi kerak (ASP.NET Core doim `"null"` yozadi).
 */
function isEmptyBody(data: unknown): boolean {
  return data === null || data === undefined || data === ''
}

function toCompetency(raw: unknown): EmployeeCompetency {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    languages: text(box['languages']),
    languageSkills: skillList(box['languageSkills']),
    computerLiteracyPercent: intOrNull(box['computerLiteracyPercent']),
    specialization: text(box['specialization']),
    otherAbilities: text(box['otherAbilities']),
    interest: text(box['interest']),
    educationPlace: text(box['educationPlace']),
    candidateNote: text(box['candidateNote']),
  }
}

/**
 * Qobiliyatlar profilini o'qiydi.
 *
 * ⚠️ HR hali to'ldirmagan bo'lsa server `200` va BO'SH tana qaytaradi —
 * `404` EMAS (`EmployeeEndpoints.MapCompetency` izohiga qarang: «HR hali
 * to'ldirmagan bo'lsa — bo'sh javob (200, null tana), xato emas»). Bu
 * holat xato emas, `null` ga aylanadi: chaqiruvchi buni «forma bo'sh
 * boshlanadi» deb o'qiydi, `isError` emas.
 *
 * `404` bu yerda faqat XODIM topilmasa keladi (haqiqiy xato) — u
 * qayta ishlanmasdan chaqiruvchiga o'tadi.
 */
export async function fetchEmployeeCompetency(
  employeeId: string,
  signal?: AbortSignal,
): Promise<EmployeeCompetency | null> {
  const { data } = await api.get<unknown>(`/api/employees/${employeeId}/competency`, { signal })
  return isEmptyBody(data) ? null : toCompetency(data)
}

/**
 * Profilni saqlaydi — birinchi marta YARATADI yoki bor bo'lsa TO'LIQ
 * ALMASHTIRADI. Qisman yangilash yo'q: `body` har doim barcha oltita
 * maydonni o'z ichiga oladi.
 */
export async function saveEmployeeCompetency(
  employeeId: string,
  body: SaveCompetencyRequest,
): Promise<EmployeeCompetency> {
  const { data } = await api.put<unknown>(`/api/employees/${employeeId}/competency`, {
    ...body,
    // Sanoqlar serverga NOM bilan boradi («english» → «English») —
    // javob ham nom bilan keladi, shartnoma ikki tomonda bir xil.
    languageSkills: body.languageSkills.map((skill) => ({
      language: enumName(skill.language),
      level: enumName(skill.level),
    })),
  })
  return toCompetency(data)
}
