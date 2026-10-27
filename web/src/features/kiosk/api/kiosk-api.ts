import axios from 'axios'

import type { KioskReferralChannelDto } from '@/shared/api/generated/model'
import { api } from '@/shared/api/http-client'
import { authStorage } from '@/shared/lib/auth-storage'
import type { ComputerSkillKey } from '../lib/kiosk-options'

/**
 * Candidate questionnaire HR-F02 — the public side of
 * `docs/SPEC-planshet-anketa.md` (§2, §7).
 *
 * Both endpoints are anonymous: anyone with the link fills the form, like a
 * Google Form. This module still has its own axios instance on purpose — the
 * HR instance attaches the HR bearer token and, on 401, refreshes it and
 * redirects to /kirish. Neither belongs on a public page: the form must work
 * the same whether or not an HR session happens to live in this browser.
 */

/** Same API origin as the HR client; only the base URL is shared, not the instance. */
const kioskApi = axios.create({
  baseURL: api.defaults.baseURL,
  timeout: 60_000,
})

/* ─────────────────────────────────────────────────────────── contract */

/** `GET /api/kiosk/questionnaire/session` (SPEC §2.1) — public, no credentials. */
export interface KioskSession {
  /** Tashkent date on the server, `yyyy-MM-dd`. */
  today: string
  vacancies: { id: string; title: string }[]
  generalPoolAvailable: boolean
  /**
   * «Ish haqida qayerdan bildingiz?» — the ACTIVE rows of HR's «Qayerdan
   * bilgan» catalog, alphabetical (docs/SPEC-qayerdan-bilgan.md §5).
   */
  referralChannels: ReferralChannelOption[]
  /**
   * Cloudflare Turnstile site key (SPEC §2.3 item 2), public. `null` — the bot
   * check is off (dev, tests, until the owner adds the keys): no widget, and
   * the form is sent without a token.
   */
  turnstileSiteKey: string | null
  /**
   * The blank version the server takes (`CandidateForm.HrF02Version`). When it
   * is not {@link KIOSK_FORM_VERSION} a newer form went live while this page
   * stayed open — the start screen reloads. Absent on an older server.
   */
  formVersion?: string
}

/** The HR-F02 version this build of the form fills in. */
export const KIOSK_FORM_VERSION = '3.1'

/**
 * `{ id, name, asksDetail }` — the generated `KioskReferralChannelDto`. Choosing
 * a channel with `asksDetail` asks «Aniqroq yozing» (who recommended / which
 * source), then required.
 */
export type ReferralChannelOption = KioskReferralChannelDto

/** `POST /api/kiosk/questionnaire` → 201 / 200 (idempotent repeat). */
export interface KioskSubmitResult {
  formNumber: number
  submittedAt: string
}

/** Languages the candidate may fill the form in — sent as `language`. */
export const KIOSK_LANGUAGES = ['uz-Latn', 'uz-Cyrl', 'ru'] as const
export type KioskLanguage = (typeof KIOSK_LANGUAGES)[number]

export type HousingType = 'Apartment' | 'House'
export type FormMaritalStatus = 'Married' | 'Unmarried' | 'Divorced' | 'Widowed'
export type MilitaryService = 'Served' | 'Reserve' | 'Deferred' | 'NotServed'
export type FormEducationLevel =
  | 'SecondarySchool'
  | 'VocationalSchool'
  | 'HigherIncomplete'
  | 'HigherComplete'
  | 'HigherMaster'
export type FormLanguageName = 'Uzbek' | 'Russian' | 'English' | 'Chinese' | 'Other'
export type FamilyRelation = 'Father' | 'Mother' | 'Spouse' | 'Brother' | 'Sister' | 'Child' | 'Other'
/** «Ishga qachondan chiqa olasiz?» — counted from the hire day (form v3.1). */
export type StartAfter = 'OnHireDay' | 'OneWeek' | 'TenDays' | 'FifteenDays'

export interface EducationRow {
  institution: string
  faculty?: string
  startYear?: number
  endYear?: number
  diplomaNumber?: string
}

export interface LanguageRow {
  language: FormLanguageName
  otherName?: string
  speaking: number
  reading: number
  writing: number
}

export interface JobRow {
  organization: string
  position?: string
  /** `yyyy-MM`. */
  from?: string
  /** `yyyy-MM`; absent when `current`. */
  to?: string
  current: boolean
  leaveReason?: string
}

export interface ReferenceRow {
  fullName: string
  organizationPosition?: string
  phone?: string
}

export interface FamilyMemberRow {
  relation: FamilyRelation
  fullName: string
  birthYear?: number
  workplace?: string
  phone?: string
  address?: string
}

/**
 * The `answers` JSON (SPEC §3, form HR-F02 v3.0). Optional keys are omitted
 * when the candidate left them empty — the server treats "absent" and "empty"
 * the same, and omitting keeps the stored document small.
 */
export interface QuestionnaireAnswers {
  vacancyId: string | null

  lastName: string
  firstName: string
  middleName?: string
  birthDate: string
  registeredAddress: string
  actualAddress: string
  housingType?: HousingType
  mahalla?: string
  phone: string
  phoneExtra?: string
  telegram?: string
  email?: string
  maritalStatus: FormMaritalStatus
  childrenCount?: number
  /** Optional for everyone — the form no longer asks for gender (SPEC §3.1, 2026-10-26). */
  militaryService?: MilitaryService
  /** Only with `Served`. */
  militaryServiceMonths?: number
  hasDriverLicense: boolean
  driverLicenseCategories: string[]
  hasCar?: boolean
  carModel?: string

  educationLevel: FormEducationLevel
  educations: EducationRow[]
  schoolName?: string
  strongSubjects?: string
  courses?: string

  languages: LanguageRow[]
  /** Level 1–4 per known skill; an absent key = does not know (SPEC §3.4). */
  computerSkills: Partial<Record<ComputerSkillKey, number>>
  computerSkillsOther?: string

  /**
   * «Avval biror joyda ishlaganmisiz?» (form v3.1). `true` — at least one job
   * and one referee; `false` — `jobs`, `references`, `lastJobAchievement` and
   * `bestIdea` are not sent (the server drops them anyway).
   */
  hasWorkExperience: boolean
  jobs: JobRow[]

  references: ReferenceRow[]
  familyMembers: FamilyMemberRow[]

  /** An active channel from the session's `referralChannels`. */
  referralChannelId: string
  /** Only when that channel `asksDetail` (then required, ≤ 200). */
  referralDetail?: string
  hasAcquaintances: boolean
  acquaintancesDetail?: string
  lastSalary?: number
  expectedSalary: number
  startAfter: StartAfter
  canWorkShifts: boolean
  canWorkOvertime: boolean
  canTravel: boolean
  hasOtherJob: boolean
  otherJobDetail?: string
  hasLoan: boolean
  /** Required with `hasLoan`. */
  loanMonthlyPayment?: number
  agreesToTrial: boolean
  agreesParentsInvite?: boolean
  startConditions?: string

  hasHealthRestriction: boolean
  healthRestrictionDetail?: string
  hasFamilyPatient?: boolean
  hasConviction: boolean
  convictionDetail?: string
  relativesConviction?: boolean
  relativesConvictionDetail?: string
  abroadTrips?: string

  motivation: string
  whyUs: string
  strengths: string
  weaknesses: string
  motivators: string
  successDefinition: string
  /** Required with `hasWorkExperience`, absent without. */
  lastJobAchievement?: string
  bestIdea?: string
  biggestChallenge?: string
  stressHandling?: string
  unfairnessToYou?: string
  unfairnessToCompany?: string
  teamwork?: string
  previousTeamOpinion?: string
  studentWorkExperience?: string
  fiveYearPlan?: string
  extraNote?: string

  consentAccuracy: boolean
  consentPersonalData: boolean
  consentReferenceCheck: boolean
  consentNotifyChanges: boolean
  consentConfidentiality: boolean
  signatureName: string
}

export interface KioskSubmission {
  answers: QuestionnaireAnswers
  clientSubmissionId: string
  language: KioskLanguage
  photo: Blob
  signature: Blob
  /**
   * Honeypot (SPEC §2): the hidden `website` input's value. People never see
   * the field, so it is empty; a bot that fills every input gets a 400.
   */
  website: string
  /** Turnstile token — only when the bot check is on; the part is left out otherwise. */
  turnstileToken?: string | undefined
}

/** The server's answer to a missing, invalid or expired Turnstile token (SPEC §2.3 item 2). */
export const BOT_CHECK_FAILED = 'anketa.bot_tekshiruvi'

/* ──────────────────────────────────────────────────────────── calls */

export async function fetchKioskSession(signal?: AbortSignal): Promise<KioskSession> {
  const { data } = await kioskApi.get<KioskSession>('/api/kiosk/questionnaire/session', { signal })

  return {
    today: typeof data?.today === 'string' ? data.today : '',
    vacancies: Array.isArray(data?.vacancies)
      ? data.vacancies.filter(
          (row): row is { id: string; title: string } =>
            typeof row?.id === 'string' && typeof row?.title === 'string',
        )
      : [],
    generalPoolAvailable: data?.generalPoolAvailable === true,
    referralChannels: Array.isArray(data?.referralChannels)
      ? data.referralChannels
          .filter(
            (row): row is ReferralChannelOption =>
              typeof row?.id === 'string' && typeof row?.name === 'string',
          )
          .map((row) => ({ id: row.id, name: row.name, asksDetail: row.asksDetail === true }))
      : [],
    turnstileSiteKey:
      typeof data?.turnstileSiteKey === 'string' && data.turnstileSiteKey.trim() !== ''
        ? data.turnstileSiteKey.trim()
        : null,
    ...(typeof data?.formVersion === 'string' && data.formVersion !== '' ? { formVersion: data.formVersion } : {}),
  }
}

/**
 * Multipart, field names per SPEC §2.1. `Content-Type` is left to axios so the
 * multipart boundary is written (same reason as `http-client.ts`).
 */
export async function submitQuestionnaire(submission: KioskSubmission): Promise<KioskSubmitResult> {
  const form = new FormData()
  form.append('answers', JSON.stringify(submission.answers))
  form.append('clientSubmissionId', submission.clientSubmissionId)
  form.append('language', submission.language)
  form.append('photo', submission.photo, 'photo.jpg')
  form.append('signature', submission.signature, 'signature.png')
  form.append('website', submission.website)
  if (submission.turnstileToken) form.append('turnstileToken', submission.turnstileToken)

  const { data } = await kioskApi.post<KioskSubmitResult>('/api/kiosk/questionnaire', form)

  return {
    formNumber: typeof data?.formNumber === 'number' ? data.formNumber : 0,
    submittedAt: typeof data?.submittedAt === 'string' ? data.submittedAt : '',
  }
}

/* ──────────────────────────────────────────────────── HR session (tablet mode) */

/** True when this browser still holds an HR session (normal HR app login). */
export function hasHrSession(): boolean {
  return authStorage.getRefreshToken() !== null || authStorage.getAccessToken() !== null
}

/**
 * Ends any HR session this browser still holds. Only the office tablet asks
 * for it (SPEC §7, tablet mode): the tablet is handed from candidate to
 * candidate, and an HR session left on it would open the HR app to the next
 * person. Best effort — the refresh token is revoked on the server when
 * reachable, and the local copy is dropped either way.
 */
export function dropHrSession(): void {
  const refreshToken = authStorage.getRefreshToken()
  if (refreshToken === null && authStorage.getAccessToken() === null) return

  authStorage.clear()

  if (refreshToken !== null) {
    void axios.post(`${api.defaults.baseURL}/api/auth/logout`, { refreshToken }).catch(() => undefined)
  }
}

/* ──────────────────────────────────────────────────────────── errors */

export type KioskFailure =
  | { kind: 'offline' }
  | { kind: 'rejected'; code: string; detail: string | null }

/**
 * Turns an axios error into what the form screens need to show. A rate limit
 * (429 `rate_limit.oshib_ketdi`) is an ordinary `rejected` with its code.
 */
export function kioskFailure(error: unknown): KioskFailure {
  if (!axios.isAxiosError(error)) return { kind: 'rejected', code: '', detail: null }

  const response = error.response
  if (!response) return { kind: 'offline' }

  const problem = response.data as { title?: unknown; detail?: unknown } | undefined

  return {
    kind: 'rejected',
    code: typeof problem?.title === 'string' ? problem.title : '',
    detail: typeof problem?.detail === 'string' ? problem.detail : null,
  }
}
