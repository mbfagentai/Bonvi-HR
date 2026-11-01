import type {
  FamilyRelation,
  FormEducationLevel,
  FormLanguageName,
  FormMaritalStatus,
  HousingType,
  MilitaryService,
  QuestionnaireAnswers,
  StartAfter,
} from '../api/kiosk-api'
import {
  ABOUT_QUESTIONS,
  COMPUTER_LEVELS,
  CONSENTS,
  DRIVER_LICENSE_CATEGORIES,
  FIXED_LANGUAGES,
  MIN_ANSWER_LETTERS,
  REQUIRED_ABOUT,
  SCREEN_FIELDS,
  WIZARD_SCREENS,
  isAboutAsked,
  isComputerSkillKey,
  type AboutQuestion,
  type ConsentKey,
  type DriverLicenseCategory,
  type WizardScreen,
} from './kiosk-options'

/**
 * What the wizard edits. Inputs keep the raw text the candidate typed (digits,
 * `dd.mm.yyyy`-masked dates are already ISO via `DateField`, months as
 * `mm.yyyy`); `toAnswers` converts it to the server contract only after the
 * step validators passed. Yes/no questions are `null` until answered, so an
 * untouched required question is an error rather than a silent «Yo'q».
 */
export interface DraftEducation {
  institution: string
  faculty: string
  startYear: string
  endYear: string
  diplomaNumber: string
}

export interface DraftLanguage {
  language: FormLanguageName
  otherName: string
  speaking: number | null
  reading: number | null
  writing: number | null
}

export interface DraftJob {
  organization: string
  position: string
  /** `mm.yyyy` as typed. */
  from: string
  to: string
  current: boolean
  leaveReason: string
}

export interface DraftReference {
  fullName: string
  organizationPosition: string
  /** Nine local digits. */
  phone: string
}

export interface DraftFamilyMember {
  relation: FamilyRelation | ''
  fullName: string
  birthYear: string
  workplace: string
  phone: string
  address: string
}

export interface KioskDraft {
  clientSubmissionId: string

  /** `''` — not chosen yet, `general` — «Umumiy (istalgan lavozim)», otherwise a vacancy id. */
  vacancy: string

  lastName: string
  firstName: string
  middleName: string
  birthDate: string
  registeredAddress: string
  actualAddress: string
  sameAddress: boolean
  housingType: HousingType | null
  mahalla: string
  phone: string
  phoneExtra: string
  telegram: string
  email: string
  maritalStatus: FormMaritalStatus | null
  childrenCount: string
  militaryService: MilitaryService | null
  militaryServiceMonths: string
  hasDriverLicense: boolean | null
  driverLicenseCategories: string[]
  hasCar: boolean | null
  carModel: string

  educationLevel: FormEducationLevel | null
  educations: DraftEducation[]
  schoolName: string
  strongSubjects: string
  courses: string

  languages: DraftLanguage[]
  /** Built-in keys and HR-added ones (`x-…`, SPEC-blankalar-hrmentor §5.2). */
  computerSkills: Partial<Record<string, number>>
  computerSkillsOther: string

  /**
   * «Avval biror joyda ishlaganmisiz?» (form v3.1). Rows typed under «Ha»
   * stay in the draft after «Yo'q» (the candidate may switch back), but they
   * are neither checked nor sent.
   */
  hasWorkExperience: boolean | null
  jobs: DraftJob[]
  references: DraftReference[]
  familyMembers: DraftFamilyMember[]

  /**
   * `''` — not chosen. A catalog channel id from the session (SPEC-qayerdan-bilgan
   * §5). A draft saved by an older build carried `referralSource` (the closed
   * enum) — `draftStorage.load` drops unknown keys, so it simply asks again.
   */
  referralChannelId: string
  referralDetail: string
  hasAcquaintances: boolean | null
  acquaintancesDetail: string
  lastSalary: string
  expectedSalary: string
  startAfter: StartAfter | null
  canWorkShifts: boolean | null
  canWorkOvertime: boolean | null
  canTravel: boolean | null
  hasOtherJob: boolean | null
  otherJobDetail: string
  hasLoan: boolean | null
  loanMonthlyPayment: string
  agreesToTrial: boolean | null
  agreesParentsInvite: boolean | null
  startConditions: string

  hasHealthRestriction: boolean | null
  healthRestrictionDetail: string
  hasFamilyPatient: boolean | null
  hasConviction: boolean | null
  convictionDetail: string
  relativesConviction: boolean | null
  relativesConvictionDetail: string
  abroadTrips: string

  about: Record<AboutQuestion, string>
  consents: Record<ConsentKey, boolean>
  signatureName: string
}

/* ─────────────────────────────────────────────────────────── limits (SPEC §3) */

export const LIMITS = {
  name: 60,
  short: 200,
  address: 300,
  long: 1000,
  extraNote: 2000,
  money: 1_000_000_000,
} as const

export const MAX_EDUCATIONS = 5
export const MAX_JOBS = 5
export const MAX_REFERENCES = 3
export const MAX_FAMILY = 8
export const MAX_OTHER_LANGUAGES = 3

/* ─────────────────────────────────────────────────────────── factories */

export function newId(): string {
  // `crypto.randomUUID` needs a secure context — the tablet opens the site over HTTPS,
  // the fallback only keeps plain-HTTP dev setups working.
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID()

  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/gu, (char) => {
    const random = Math.floor(Math.random() * 16)
    return (char === 'x' ? random : (random & 0x3) | 0x8).toString(16)
  })
}

export const emptyEducation = (): DraftEducation => ({
  institution: '',
  faculty: '',
  startYear: '',
  endYear: '',
  diplomaNumber: '',
})

export const emptyJob = (): DraftJob => ({
  organization: '',
  position: '',
  from: '',
  to: '',
  current: false,
  leaveReason: '',
})

export const emptyReference = (): DraftReference => ({ fullName: '', organizationPosition: '', phone: '' })

export const emptyFamilyMember = (): DraftFamilyMember => ({
  relation: '',
  fullName: '',
  birthYear: '',
  workplace: '',
  phone: '',
  address: '',
})

export const emptyLanguage = (language: FormLanguageName): DraftLanguage => ({
  language,
  otherName: '',
  speaking: null,
  reading: null,
  writing: null,
})

export function emptyDraft(): KioskDraft {
  return {
    clientSubmissionId: newId(),
    vacancy: '',
    lastName: '',
    firstName: '',
    middleName: '',
    birthDate: '',
    registeredAddress: '',
    actualAddress: '',
    sameAddress: false,
    housingType: null,
    mahalla: '',
    phone: '',
    phoneExtra: '',
    telegram: '',
    email: '',
    maritalStatus: null,
    childrenCount: '',
    militaryService: null,
    militaryServiceMonths: '',
    hasDriverLicense: null,
    driverLicenseCategories: [],
    hasCar: null,
    carModel: '',
    educationLevel: null,
    educations: [],
    schoolName: '',
    strongSubjects: '',
    courses: '',
    languages: FIXED_LANGUAGES.map(emptyLanguage),
    computerSkills: {},
    computerSkillsOther: '',
    hasWorkExperience: null,
    jobs: [],
    references: [],
    // Rows are added through «+ Qo'shish»; «at least one» is the `familyMin` rule.
    familyMembers: [],
    referralChannelId: '',
    referralDetail: '',
    hasAcquaintances: null,
    acquaintancesDetail: '',
    lastSalary: '',
    expectedSalary: '',
    startAfter: null,
    canWorkShifts: null,
    canWorkOvertime: null,
    canTravel: null,
    hasOtherJob: null,
    otherJobDetail: '',
    hasLoan: null,
    loanMonthlyPayment: '',
    agreesToTrial: null,
    agreesParentsInvite: null,
    startConditions: '',
    hasHealthRestriction: null,
    healthRestrictionDetail: '',
    hasFamilyPatient: null,
    hasConviction: null,
    convictionDetail: '',
    relativesConviction: null,
    relativesConvictionDetail: '',
    abroadTrips: '',
    about: Object.fromEntries(ABOUT_QUESTIONS.map((key) => [key, ''])) as Record<AboutQuestion, string>,
    consents: Object.fromEntries(CONSENTS.map((key) => [key, false])) as Record<ConsentKey, boolean>,
    signatureName: '',
  }
}

/* ─────────────────────────────────────────────────────────── draft storage */

const DRAFT_STORAGE = 'bonvi.kiosk.draft'

/**
 * Answers survive an accidental reload (SPEC §7) — sessionStorage, so the
 * draft dies with the tab. A draft older than `maxAgeMs` is thrown away on
 * load (`KioskRules.draftMaxAgeMs`: 15 minutes on the office tablet, where a
 * stale draft would show the previous candidate's health, loan and
 * conviction answers to whoever taps «davom ettirish»; 2 hours on a personal
 * device). Photo and signature are never stored. Only the keys the current
 * draft has are read back: a draft saved by an older build may still carry
 * answers the form no longer asks (gender, passport, JSHSHIR — SPEC §3.1;
 * the computer skills cut on 2026-10-26 — SPEC §3.4; the total experience and
 * the `availableFrom` date of form v3.0), and those must neither reach the
 * payload nor be written back. Questions added later (`hasWorkExperience`,
 * `startAfter`) start unanswered from the defaults, and a value a list no
 * longer offers (licence «BE», a dropped skill) is removed.
 */
export const draftStorage = {
  load(maxAgeMs: number): KioskDraft | null {
    try {
      const raw = window.sessionStorage.getItem(DRAFT_STORAGE)
      if (raw === null) return null

      const { savedAt, ...parsed } = JSON.parse(raw) as Partial<KioskDraft> & { savedAt?: unknown }

      if (typeof savedAt !== 'number' || Date.now() - savedAt > maxAgeMs) {
        window.sessionStorage.removeItem(DRAFT_STORAGE)
        return null
      }

      if (typeof parsed.clientSubmissionId !== 'string') return null

      // A draft from an older build is merged over the defaults instead of trusted blindly.
      const defaults = emptyDraft()
      const known = Object.fromEntries(Object.entries(parsed).filter(([key]) => Object.hasOwn(defaults, key)))
      return {
        ...defaults,
        ...known,
        driverLicenseCategories: knownLicenseCategories(parsed.driverLicenseCategories),
        computerSkills: knownComputerSkills(parsed.computerSkills),
        about: { ...defaults.about, ...knownTexts(parsed.about) },
      }
    } catch {
      return null
    }
  },
  save(draft: KioskDraft): void {
    try {
      window.sessionStorage.setItem(DRAFT_STORAGE, JSON.stringify({ ...draft, savedAt: Date.now() }))
    } catch {
      // Quota or blocked storage — the wizard keeps working from memory.
    }
  },
  clear(): void {
    try {
      window.sessionStorage.removeItem(DRAFT_STORAGE)
    } catch {
      // ignore
    }
  },
}

/** An HR-added computer skill key — the shape the server accepts on an open list. */
const ADDED_SKILL = /^x-[A-Za-z0-9_.-]{1,78}$/

/**
 * Only the skills the form still asks, each with a level of the scale. The
 * server rejects any other key (SPEC §3.4), so a removed skill from an older
 * draft (`python`, `linux`, …) is dropped here instead of failing the submit.
 *
 * HR-added skills (`x-…`): with `offered` — only those HR's list still has
 * (sending); without it — any of that shape (restoring a draft before the
 * session is known; the send filters again).
 */
export function knownComputerSkills(value: unknown, offered?: ReadonlySet<string>): Partial<Record<string, number>> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return {}

  const levels: readonly number[] = COMPUTER_LEVELS
  const skills: Partial<Record<string, number>> = {}
  for (const [key, level] of Object.entries(value)) {
    const known = isComputerSkillKey(key) || (offered === undefined ? ADDED_SKILL.test(key) : offered.has(key))
    if (known && typeof level === 'number' && levels.includes(level)) skills[key] = level
  }
  return skills
}

/** Licence categories the form still offers, in its order (form v3.1 replaced «BE» with «BC»). */
export function knownLicenseCategories(value: unknown): DriverLicenseCategory[] {
  if (!Array.isArray(value)) return []

  const stored: readonly unknown[] = value
  return DRIVER_LICENSE_CATEGORIES.filter((category) => stored.includes(category))
}

/** The open answers of a stored draft that are still strings — the rest fall back to empty. */
function knownTexts(value: unknown): Partial<Record<AboutQuestion, string>> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return {}

  const texts: Partial<Record<AboutQuestion, string>> = {}
  for (const key of ABOUT_QUESTIONS) {
    const text: unknown = (value as Record<string, unknown>)[key]
    if (typeof text === 'string') texts[key] = text
  }
  return texts
}

/* ─────────────────────────────────────────────────────────── validation */

/**
 * Error codes — each is `kiosk.error.<code>` in the locale files
 * (`check-locales.mjs` reads this list).
 */
export const KIOSK_ERROR_CODES = [
  'required',
  'tooLong',
  'invalidDate',
  'age',
  'phone',
  'telegram',
  'email',
  'range',
  'yearOrder',
  'month',
  'monthOrder',
  'categories',
  'languageLevels',
  'otherLanguageName',
  'familyMin',
  'jobsMin',
  'referencesMin',
  'consent',
  'signatureName',
  'signature',
  'photo',
  'vacancy',
  'answer',
  // Constructor form only (SPEC-anketa-konstruktori §7) — limits come from the definition
  'ageRange',
  'notBeforeToday',
  'phoneAny',
  'monthFuture',
  'selectedMin',
  'selectedMax',
  'levelsAll',
  'otherName',
  'rowsMin',
  'rowsMax',
  'characters',
  'invalid',
] as const
export type KioskErrorCode = (typeof KIOSK_ERROR_CODES)[number]

export interface FieldError {
  code: KioskErrorCode
  values?: Record<string, string | number>
}

/** Field path (`lastName`, `jobs.0.from`, `photo`) → error. */
export type FieldErrors = Record<string, FieldError>

export interface ValidationContext {
  /** Tashkent today, `yyyy-MM-dd` (server's value when known). */
  today: string
  vacancyIds: ReadonlySet<string>
  generalPoolAvailable: boolean
  hasPhoto: boolean
  hasSignature: boolean
  /** Active referral channels of the session: id → `asksDetail`. */
  referralChannels: ReadonlyMap<string, boolean>
  /**
   * Every computer key HR's list offers (hidden ones too). Absent — the
   * built-in texts: only the built-in keys go to the server.
   */
  computerKeys?: ReadonlySet<string>
}

class Collector {
  readonly errors: FieldErrors = {}

  add(path: string, code: KioskErrorCode, values?: Record<string, string | number>): void {
    if (!(path in this.errors)) this.errors[path] = values === undefined ? { code } : { code, values }
  }

  required(path: string, value: string, limit: number): void {
    const text = value.trim()
    if (text === '') this.add(path, 'required')
    else this.max(path, text, limit)
  }

  /** An open question: required, and «.» or «-» is not an answer. */
  answer(path: string, value: string, limit: number): void {
    this.required(path, value, limit)
    if (value.trim() !== '' && (value.match(/\p{L}/gu)?.length ?? 0) < MIN_ANSWER_LETTERS) this.add(path, 'answer')
  }

  max(path: string, value: string, limit: number): void {
    if (value.trim().length > limit) this.add(path, 'tooLong', { max: limit })
  }

  chosen(path: string, value: unknown): void {
    if (value === null || value === '') this.add(path, 'required')
  }

  whole(path: string, value: string, min: number, max: number): void {
    if (value.trim() === '') return
    const number = Number(value)
    if (!Number.isInteger(number) || number < min || number > max) this.add(path, 'range', { min, max })
  }

  phone(path: string, digits: string, required: boolean): void {
    if (digits === '') {
      if (required) this.add(path, 'required')
      return
    }
    if (!/^\d{9}$/u.test(digits)) this.add(path, 'phone')
  }

  money(path: string, digits: string, required: boolean): void {
    if (digits.trim() === '') {
      if (required) this.add(path, 'required')
      return
    }
    const amount = Number(digits)
    if (!Number.isInteger(amount) || amount < 0 || amount > LIMITS.money) {
      this.add(path, 'range', { min: 0, max: '1 000 000 000' })
    }
  }
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/u

function yearsBetween(birth: string, today: string): number {
  const [by, bm, bd] = birth.split('-').map(Number)
  const [ty, tm, td] = today.split('-').map(Number)
  if (!by || !bm || !bd || !ty || !tm || !td) return Number.NaN

  let age = ty - by
  if (tm < bm || (tm === bm && td < bd)) age -= 1
  return age
}

/** `mm.yyyy` → `yyyy-MM`, or `null` when the text is not a real month. */
export function monthToIso(value: string): string | null {
  const match = /^(\d{2})\.(\d{4})$/u.exec(value.trim())
  if (match === null) return null

  const month = Number(match[1])
  const year = Number(match[2])
  if (month < 1 || month > 12 || year < 1950 || year > 2100) return null

  return `${match[2]}-${match[1]}`
}

export function normalizeTelegram(value: string): string {
  return value.trim().replace(/^@/u, '')
}

/* Signature name ↔ the typed name (SPEC §3.11). The server compares through its
   own transliteration; the tablet only checks when both strings are in the same
   script, so it never blocks an answer the server would accept. */

const APOSTROPHES = /['ʻʼ‘’`]/gu

const CYRILLIC_TO_LATIN: Readonly<Record<string, string>> = {
  а: 'a', б: 'b', в: 'v', г: 'g', д: 'd', е: 'e', ё: 'yo', ж: 'j', з: 'z', и: 'i', й: 'y',
  к: 'k', л: 'l', м: 'm', н: 'n', о: 'o', п: 'p', р: 'r', с: 's', т: 't', у: 'u', ф: 'f',
  х: 'x', ц: 's', ч: 'ch', ш: 'sh', щ: 'sh', ъ: '', ы: 'i', ь: '', э: 'e', ю: 'yu', я: 'ya',
  ў: 'o', қ: 'q', ғ: 'g', ҳ: 'h',
}

function scriptOf(value: string): 'cyrillic' | 'latin' | 'mixed' {
  const cyrillic = /[Ѐ-ӿ]/u.test(value)
  const latin = /[A-Za-z]/u.test(value)
  return cyrillic && latin ? 'mixed' : cyrillic ? 'cyrillic' : 'latin'
}

function foldName(value: string): string {
  let out = ''
  for (const char of value.toLowerCase().replaceAll(APOSTROPHES, '')) out += CYRILLIC_TO_LATIN[char] ?? char
  return out.replaceAll(/\s+/gu, ' ').trim()
}

export function signatureMatches(signature: string, lastName: string, firstName: string): boolean {
  const scripts = new Set([scriptOf(signature), scriptOf(lastName), scriptOf(firstName)])
  if (scripts.size > 1 || scripts.has('mixed')) return true

  const folded = foldName(signature)
  return folded.includes(foldName(lastName)) && folded.includes(foldName(firstName))
}

/**
 * Validates the whole form. Paths match the `data-field` attributes on the
 * screens; each screen shows only the errors it owns (`screenErrors`), so the
 * rules live here once whatever the screen split is.
 */
export function validateAll(draft: KioskDraft, context: ValidationContext): FieldErrors {
  const check = new Collector()
  const currentYear = Number(context.today.slice(0, 4)) || new Date().getFullYear()

  /* ── 0–1. Vacancy, photo, personal data ── */
  if (draft.vacancy === '') check.add('vacancy', 'vacancy')
  else if (draft.vacancy === 'general' ? !context.generalPoolAvailable : !context.vacancyIds.has(draft.vacancy)) {
    check.add('vacancy', 'vacancy')
  }

  if (!context.hasPhoto) check.add('photo', 'photo')

  check.required('lastName', draft.lastName, LIMITS.name)
  check.required('firstName', draft.firstName, LIMITS.name)
  check.max('middleName', draft.middleName, LIMITS.name)

  if (draft.birthDate === '') check.add('birthDate', 'required')
  else if (!ISO_DATE.test(draft.birthDate)) check.add('birthDate', 'invalidDate')
  else {
    const age = yearsBetween(draft.birthDate, context.today)
    if (!(age >= 16 && age <= 70)) check.add('birthDate', 'age')
  }

  check.required('registeredAddress', draft.registeredAddress, LIMITS.address)
  check.required(
    'actualAddress',
    draft.sameAddress ? draft.registeredAddress : draft.actualAddress,
    LIMITS.address,
  )
  check.max('mahalla', draft.mahalla, LIMITS.short)

  check.phone('phone', draft.phone, true)
  check.phone('phoneExtra', draft.phoneExtra, false)

  const telegram = normalizeTelegram(draft.telegram)
  if (telegram !== '' && !/^[A-Za-z0-9_]{5,32}$/u.test(telegram)) check.add('telegram', 'telegram')

  const email = draft.email.trim()
  if (email !== '' && (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/u.test(email) || email.length > LIMITS.short)) {
    check.add('email', 'email')
  }

  check.chosen('maritalStatus', draft.maritalStatus)
  check.whole('childrenCount', draft.childrenCount, 0, 20)

  // Optional for everyone — gender is not asked any more (SPEC §3.1).
  if (draft.militaryService === 'Served') check.whole('militaryServiceMonths', draft.militaryServiceMonths, 1, 60)

  check.chosen('hasDriverLicense', draft.hasDriverLicense)
  if (draft.hasDriverLicense === true && draft.driverLicenseCategories.length === 0) {
    check.add('driverLicenseCategories', 'categories')
  }

  if (draft.hasCar === true) check.max('carModel', draft.carModel, LIMITS.short)

  /* ── 2–4. Education, languages, computer ── */
  check.chosen('educationLevel', draft.educationLevel)

  draft.educations.forEach((row, index) => {
    const base = `educations.${index}`
    check.required(`${base}.institution`, row.institution, LIMITS.short)
    check.max(`${base}.faculty`, row.faculty, LIMITS.short)
    check.whole(`${base}.startYear`, row.startYear, 1950, currentYear)
    check.whole(`${base}.endYear`, row.endYear, 1950, currentYear + 10)
    check.max(`${base}.diplomaNumber`, row.diplomaNumber, LIMITS.short)

    if (row.startYear !== '' && row.endYear !== '' && Number(row.startYear) > Number(row.endYear)) {
      check.add(`${base}.endYear`, 'yearOrder')
    }
  })

  check.max('schoolName', draft.schoolName, LIMITS.short)
  check.max('strongSubjects', draft.strongSubjects, LIMITS.short)
  check.max('courses', draft.courses, LIMITS.long)

  draft.languages.forEach((row, index) => {
    const base = `languages.${index}`
    const levels = [row.speaking, row.reading, row.writing]
    const touched = levels.some((level) => level !== null) || (row.language === 'Other' && row.otherName.trim() !== '')

    if ((row.language === 'Russian' || touched) && levels.some((level) => level === null)) {
      check.add(base, 'languageLevels')
    }

    if (row.language === 'Other' && touched) {
      if (row.otherName.trim() === '') check.add(`${base}.otherName`, 'otherLanguageName')
      else check.max(`${base}.otherName`, row.otherName, LIMITS.name)
    }
  })

  check.max('computerSkillsOther', draft.computerSkillsOther, LIMITS.short)

  /* ── 5–7. Work, references, family ── */
  // Jobs and referees are asked only after «Ha»; rows left from an earlier «Ha» are not checked.
  check.chosen('hasWorkExperience', draft.hasWorkExperience)
  const worked = draft.hasWorkExperience === true
  const jobs = worked ? draft.jobs : []
  const references = worked ? draft.references : []

  if (worked && jobs.length === 0) check.add('jobs', 'jobsMin')

  jobs.forEach((row, index) => {
    const base = `jobs.${index}`
    check.required(`${base}.organization`, row.organization, LIMITS.short)
    check.max(`${base}.position`, row.position, LIMITS.short)
    check.max(`${base}.leaveReason`, row.leaveReason, LIMITS.short)

    const from = row.from.trim() === '' ? '' : monthToIso(row.from)
    const to = row.current || row.to.trim() === '' ? '' : monthToIso(row.to)

    if (from === null) check.add(`${base}.from`, 'month')
    if (to === null) check.add(`${base}.to`, 'month')
    if (from && to && from > to) check.add(`${base}.to`, 'monthOrder')
  })

  if (worked && references.length === 0) check.add('references', 'referencesMin')

  references.forEach((row, index) => {
    const base = `references.${index}`
    check.required(`${base}.fullName`, row.fullName, LIMITS.short)
    check.max(`${base}.organizationPosition`, row.organizationPosition, LIMITS.short)
    check.phone(`${base}.phone`, row.phone, false)
  })

  if (draft.familyMembers.length === 0) check.add('familyMembers', 'familyMin')

  draft.familyMembers.forEach((row, index) => {
    const base = `familyMembers.${index}`
    check.chosen(`${base}.relation`, row.relation)
    check.required(`${base}.fullName`, row.fullName, LIMITS.short)
    check.whole(`${base}.birthYear`, row.birthYear, 1920, currentYear)
    check.max(`${base}.workplace`, row.workplace, LIMITS.short)
    check.phone(`${base}.phone`, row.phone, false)
    check.max(`${base}.address`, row.address, LIMITS.address)
  })

  /* ── 8–9. Conditions, health and extra ── */
  // A restored draft may name a channel HR has since switched off — then it is asked again
  if (!context.referralChannels.has(draft.referralChannelId)) check.add('referralChannelId', 'required')
  else if (context.referralChannels.get(draft.referralChannelId) === true) {
    check.required('referralDetail', draft.referralDetail, LIMITS.short)
  }

  check.chosen('hasAcquaintances', draft.hasAcquaintances)
  if (draft.hasAcquaintances === true) check.required('acquaintancesDetail', draft.acquaintancesDetail, LIMITS.short)

  check.money('lastSalary', draft.lastSalary, false)
  check.money('expectedSalary', draft.expectedSalary, true)

  check.chosen('startAfter', draft.startAfter)

  check.chosen('canWorkShifts', draft.canWorkShifts)
  check.chosen('canWorkOvertime', draft.canWorkOvertime)
  check.chosen('canTravel', draft.canTravel)

  check.chosen('hasOtherJob', draft.hasOtherJob)
  if (draft.hasOtherJob === true) check.required('otherJobDetail', draft.otherJobDetail, LIMITS.short)

  check.chosen('hasLoan', draft.hasLoan)
  if (draft.hasLoan === true) {
    check.money('loanMonthlyPayment', draft.loanMonthlyPayment, true)
    // «Ha» with a zero payment is not an answer — the server asks for more than nothing (form v3.1)
    if (draft.loanMonthlyPayment.trim() !== '' && Number(draft.loanMonthlyPayment) === 0) {
      check.add('loanMonthlyPayment', 'range', { min: 1, max: '1 000 000 000' })
    }
  }

  check.chosen('agreesToTrial', draft.agreesToTrial)
  check.max('startConditions', draft.startConditions, LIMITS.long)

  check.chosen('hasHealthRestriction', draft.hasHealthRestriction)
  if (draft.hasHealthRestriction === true) {
    check.required('healthRestrictionDetail', draft.healthRestrictionDetail, LIMITS.short)
  }

  check.chosen('hasConviction', draft.hasConviction)
  if (draft.hasConviction === true) check.required('convictionDetail', draft.convictionDetail, LIMITS.short)

  if (draft.relativesConviction === true) {
    check.max('relativesConvictionDetail', draft.relativesConvictionDetail, LIMITS.short)
  }

  check.max('abroadTrips', draft.abroadTrips, LIMITS.long)

  /* ── 10. About you ── */
  for (const key of ABOUT_QUESTIONS) {
    // A question about the previous job is not asked of someone who has not worked.
    if (!isAboutAsked(key, draft.hasWorkExperience)) continue

    const limit = key === 'extraNote' ? LIMITS.extraNote : LIMITS.long
    if (REQUIRED_ABOUT.includes(key)) check.answer(`about.${key}`, draft.about[key], limit)
    else check.max(`about.${key}`, draft.about[key], limit)
  }

  /* ── 11. Consent and signature ── */
  for (const key of CONSENTS) {
    if (!draft.consents[key]) check.add(`consents.${key}`, 'consent')
  }

  if (draft.signatureName.trim() === '') check.add('signatureName', 'required')
  else if (draft.signatureName.trim().length > LIMITS.short) check.add('signatureName', 'tooLong', { max: LIMITS.short })
  else if (!signatureMatches(draft.signatureName, draft.lastName, draft.firstName)) {
    check.add('signatureName', 'signatureName')
  }

  if (!context.hasSignature) check.add('signature', 'signature')

  return check.errors
}

/**
 * Which screen owns a field path — the wizard's (`about.motivation`,
 * `jobs.0.from`) or the server's (`familyMembers[0].fullName`, `vacancyId`).
 */
export function screenOfPath(path: string): WizardScreen | null {
  const parts = path.replace(/^answers\./u, '').replaceAll(/\[(\d+)\]/gu, '.$1').split('.')
  const root = parts[0] ?? ''
  const key = root === 'about' || root === 'consents' ? (parts[1] ?? '') : root

  return WIZARD_SCREENS.find((screen) => SCREEN_FIELDS[screen].includes(key)) ?? null
}

function pick(errors: FieldErrors, keep: (path: string) => boolean): FieldErrors {
  return Object.fromEntries(Object.entries(errors).filter(([path]) => keep(path)))
}

/** The errors one screen shows. */
export function screenErrors(screen: WizardScreen, errors: FieldErrors): FieldErrors {
  return pick(errors, (path) => screenOfPath(path) === screen)
}

/** The errors of one entry of a list (`jobs.2`, `jobs.2.from`, `languages.1.otherName`). */
export function entryErrors(errors: FieldErrors, base: string): FieldErrors {
  return pick(errors, (path) => path === base || path.startsWith(`${base}.`))
}

/** The repeating tables edited in a bottom sheet. */
export type RowLists = Pick<KioskDraft, 'educations' | 'jobs' | 'references' | 'familyMembers'>
export type RowListKey = keyof RowLists

/**
 * Errors of one row being edited in its sheet, before it is saved into the
 * draft: the whole-form rules applied to the draft with the row in place.
 */
export function rowErrors<K extends RowListKey>(
  draft: KioskDraft,
  context: ValidationContext,
  list: K,
  index: number,
  row: RowLists[K][number],
): FieldErrors {
  const rows: RowLists[K][number][] = [...draft[list]]
  rows[index] = row

  return entryErrors(validateAll({ ...draft, [list]: rows } as KioskDraft, context), `${list}.${index}`)
}

/* ─────────────────────────────────────────────────────────── payload */

function opt(value: string): string | undefined {
  const text = value.trim()
  return text === '' ? undefined : text
}

function optNumber(value: string): number | undefined {
  return value.trim() === '' ? undefined : Number(value)
}

function fullPhone(digits: string): string {
  return `+998${digits}`
}

function optPhone(digits: string): string | undefined {
  return digits === '' ? undefined : fullPhone(digits)
}

function optBool(value: boolean | null): boolean | undefined {
  return value === null ? undefined : value
}

/**
 * Draft → `answers` (SPEC §3). Call only after every screen validated: required
 * `null`s are asserted here, conditional fields are dropped when their
 * condition is off (a «Yo'q» answer never carries a stale detail).
 */
export function toAnswers(
  draft: KioskDraft,
  context: Pick<ValidationContext, 'referralChannels' | 'computerKeys'>,
): QuestionnaireAnswers {
  const must = <T>(value: T | null): T => {
    if (value === null) throw new Error('Draft is not validated')
    return value
  }

  const about = Object.fromEntries(
    ABOUT_QUESTIONS.map((key) => [key, opt(draft.about[key])]),
  ) as Record<AboutQuestion, string | undefined>

  const served = draft.militaryService === 'Served'
  const worked = must(draft.hasWorkExperience)

  return {
    vacancyId: draft.vacancy === 'general' ? null : draft.vacancy,

    lastName: draft.lastName.trim(),
    firstName: draft.firstName.trim(),
    middleName: opt(draft.middleName),
    birthDate: draft.birthDate,
    registeredAddress: draft.registeredAddress.trim(),
    actualAddress: (draft.sameAddress ? draft.registeredAddress : draft.actualAddress).trim(),
    housingType: draft.housingType ?? undefined,
    mahalla: opt(draft.mahalla),
    phone: fullPhone(draft.phone),
    phoneExtra: optPhone(draft.phoneExtra),
    telegram: opt(normalizeTelegram(draft.telegram)),
    email: opt(draft.email),
    maritalStatus: must(draft.maritalStatus),
    childrenCount: optNumber(draft.childrenCount),
    militaryService: draft.militaryService ?? undefined,
    militaryServiceMonths: served ? optNumber(draft.militaryServiceMonths) : undefined,
    hasDriverLicense: must(draft.hasDriverLicense),
    driverLicenseCategories: draft.hasDriverLicense === true ? draft.driverLicenseCategories : [],
    hasCar: optBool(draft.hasCar),
    carModel: draft.hasCar === true ? opt(draft.carModel) : undefined,

    educationLevel: must(draft.educationLevel),
    educations: draft.educations.map((row) => ({
      institution: row.institution.trim(),
      faculty: opt(row.faculty),
      startYear: optNumber(row.startYear),
      endYear: optNumber(row.endYear),
      diplomaNumber: opt(row.diplomaNumber),
    })),
    schoolName: opt(draft.schoolName),
    strongSubjects: opt(draft.strongSubjects),
    courses: opt(draft.courses),

    // Rows the candidate never touched are not sent (SPEC §3.3).
    languages: draft.languages
      .filter((row) => row.speaking !== null && row.reading !== null && row.writing !== null)
      .map((row) => ({
        language: row.language,
        otherName: row.language === 'Other' ? opt(row.otherName) : undefined,
        speaking: row.speaking ?? 0,
        reading: row.reading ?? 0,
        writing: row.writing ?? 0,
      })),
    computerSkills: knownComputerSkills(draft.computerSkills, context.computerKeys ?? new Set()),
    computerSkillsOther: opt(draft.computerSkillsOther),

    // «Yo'q» sends no jobs and no referees, whatever an earlier «Ha» left in the draft.
    hasWorkExperience: worked,
    jobs: worked
      ? draft.jobs.map((row) => ({
          organization: row.organization.trim(),
          position: opt(row.position),
          from: row.from.trim() === '' ? undefined : (monthToIso(row.from) ?? undefined),
          to: row.current || row.to.trim() === '' ? undefined : (monthToIso(row.to) ?? undefined),
          current: row.current,
          leaveReason: opt(row.leaveReason),
        }))
      : [],

    references: worked
      ? draft.references.map((row) => ({
          fullName: row.fullName.trim(),
          organizationPosition: opt(row.organizationPosition),
          phone: optPhone(row.phone),
        }))
      : [],
    familyMembers: draft.familyMembers.map((row) => ({
      relation: must(row.relation === '' ? null : row.relation),
      fullName: row.fullName.trim(),
      birthYear: optNumber(row.birthYear),
      workplace: opt(row.workplace),
      phone: optPhone(row.phone),
      address: opt(row.address),
    })),

    referralChannelId: must(draft.referralChannelId === '' ? null : draft.referralChannelId),
    // A detail typed for another channel is not sent once the chosen one does not ask for it
    referralDetail:
      context.referralChannels.get(draft.referralChannelId) === true ? opt(draft.referralDetail) : undefined,
    hasAcquaintances: must(draft.hasAcquaintances),
    acquaintancesDetail: draft.hasAcquaintances === true ? opt(draft.acquaintancesDetail) : undefined,
    lastSalary: optNumber(draft.lastSalary),
    expectedSalary: Number(draft.expectedSalary),
    startAfter: must(draft.startAfter),
    canWorkShifts: must(draft.canWorkShifts),
    canWorkOvertime: must(draft.canWorkOvertime),
    canTravel: must(draft.canTravel),
    hasOtherJob: must(draft.hasOtherJob),
    otherJobDetail: draft.hasOtherJob === true ? opt(draft.otherJobDetail) : undefined,
    hasLoan: must(draft.hasLoan),
    loanMonthlyPayment: draft.hasLoan === true ? optNumber(draft.loanMonthlyPayment) : undefined,
    agreesToTrial: must(draft.agreesToTrial),
    agreesParentsInvite: optBool(draft.agreesParentsInvite),
    startConditions: opt(draft.startConditions),

    hasHealthRestriction: must(draft.hasHealthRestriction),
    healthRestrictionDetail: draft.hasHealthRestriction === true ? opt(draft.healthRestrictionDetail) : undefined,
    hasFamilyPatient: optBool(draft.hasFamilyPatient),
    hasConviction: must(draft.hasConviction),
    convictionDetail: draft.hasConviction === true ? opt(draft.convictionDetail) : undefined,
    relativesConviction: optBool(draft.relativesConviction),
    relativesConvictionDetail:
      draft.relativesConviction === true ? opt(draft.relativesConvictionDetail) : undefined,
    abroadTrips: opt(draft.abroadTrips),

    motivation: about.motivation ?? '',
    whyUs: about.whyUs ?? '',
    strengths: about.strengths ?? '',
    weaknesses: about.weaknesses ?? '',
    motivators: about.motivators ?? '',
    successDefinition: about.successDefinition ?? '',
    lastJobAchievement: worked ? about.lastJobAchievement : undefined,
    bestIdea: worked ? about.bestIdea : undefined,
    biggestChallenge: about.biggestChallenge,
    stressHandling: about.stressHandling,
    unfairnessToYou: about.unfairnessToYou,
    unfairnessToCompany: about.unfairnessToCompany,
    teamwork: about.teamwork,
    previousTeamOpinion: worked ? about.previousTeamOpinion : undefined,
    studentWorkExperience: about.studentWorkExperience,
    fiveYearPlan: about.fiveYearPlan,
    extraNote: about.extraNote,

    consentAccuracy: draft.consents.consentAccuracy,
    consentPersonalData: draft.consents.consentPersonalData,
    consentReferenceCheck: draft.consents.consentReferenceCheck,
    consentNotifyChanges: draft.consents.consentNotifyChanges,
    consentConfidentiality: draft.consents.consentConfidentiality,
    signatureName: draft.signatureName.trim(),
  }
}
