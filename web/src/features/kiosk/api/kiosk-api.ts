import axios from 'axios'

import type { KioskReferralChannelDto } from '@/shared/api/generated/model'
import { api } from '@/shared/api/http-client'
import { authStorage } from '@/shared/lib/auth-storage'
import { parseDefinition, type KioskDefinition } from '../dynamic/definition'

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
  /**
   * HR's wording from HRMentor (SPEC-blankalar-hrmentor §5.1): only the texts
   * and lists HR changed. `null` — the built-in texts (integration off, no
   * revision yet, or an older server).
   */
  texts: KioskTexts | null
  /**
   * The form HR built in HRMentor's constructor (SPEC-anketa-konstruktori §5.3):
   * when present, `/anketa` renders it instead of the built-in form and sends
   * its `version` back as `definitionVersion`. `null` — the built-in form.
   */
  definition: KioskDefinition | null
}

/** One option of an HR list — HR's order, its label per language, hidden or not. */
export interface KioskTextOption {
  value: string
  /** Only on the computer list: `os`, `office`, `special`. */
  group: string | null
  label: Partial<Record<KioskLanguage, string>>
  /** Not offered on a new form; a value already chosen stays and is accepted. */
  hidden: boolean
}

/** An HR list as a whole: group titles (computer list) and the options in HR's order. */
export interface KioskTextList {
  groups: Record<string, Partial<Record<KioskLanguage, string>>>
  options: KioskTextOption[]
}

export interface KioskTexts {
  /** Sent back with the form (`textsRevision`) — the PDF keeps this revision's wording. */
  revision: number
  /** Language → `kiosk.*` key → HR's text. */
  strings: Partial<Record<KioskLanguage, Record<string, string>>>
  /** List id (`housing`, `computer`, …) → HR's whole list. */
  lists: Record<string, KioskTextList>
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
  /**
   * The link to the bot that lets it write to this person. `null`: they already
   * have a chat, the bot hasn't started, or an older server.
   */
  telegramLink: string | null
}

/** Only a Telegram link is ever rendered as a link or a QR code. */
function telegramLinkOf(value: unknown): string | null {
  return typeof value === 'string' && value.startsWith('https://t.me/') ? value : null
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
  /** Built-in keys (`msWord`) and HR-added ones (`x-…`) the session's list offers. */
  computerSkills: Partial<Record<string, number>>
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
  /** The HR texts revision the form was shown with; left out with the built-in texts. */
  textsRevision?: number | undefined
}

/** The server's answer to a missing, invalid or expired Turnstile token (SPEC §2.3 item 2). */
export const BOT_CHECK_FAILED = 'anketa.bot_tekshiruvi'

/* ──────────────────────────────────────────────────────────── calls */

export async function fetchKioskSession(signal?: AbortSignal): Promise<KioskSession> {
  const { data } = await kioskApi.get<KioskSession>('/api/kiosk/questionnaire/session', { signal })
  return parseKioskSession(data)
}

/** The session payload, read defensively (the preview page reads the same shape). */
export function parseKioskSession(data: KioskSession | undefined): KioskSession {
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
    texts: parseTexts((data as { texts?: unknown } | undefined)?.texts),
    definition: parseDefinition((data as { definition?: unknown } | undefined)?.definition),
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

/** Per-language labels; anything that is not a string of a form language is dropped. */
function parseLabels(value: unknown): Partial<Record<KioskLanguage, string>> {
  const labels: Partial<Record<KioskLanguage, string>> = {}
  if (!isRecord(value)) return labels

  for (const language of KIOSK_LANGUAGES) {
    const text = value[language]
    if (typeof text === 'string' && text.trim() !== '') labels[language] = text
  }
  return labels
}

/**
 * The server shaped these texts against the catalog already; this only keeps
 * a broken or foreign payload from reaching i18n. Strings outside `kiosk.*`
 * are dropped: the overlay touches the form's own keys and nothing else.
 */
function parseTexts(value: unknown): KioskTexts | null {
  if (!isRecord(value) || typeof value['revision'] !== 'number' || !Number.isInteger(value['revision'])) return null

  const strings: KioskTexts['strings'] = {}
  const rawStrings = value['strings']
  if (isRecord(rawStrings)) {
    for (const language of KIOSK_LANGUAGES) {
      const bag = rawStrings[language]
      if (!isRecord(bag)) continue
      strings[language] = Object.fromEntries(
        Object.entries(bag).filter(
          (entry): entry is [string, string] => entry[0].startsWith('kiosk.') && typeof entry[1] === 'string',
        ),
      )
    }
  }

  const lists: KioskTexts['lists'] = {}
  const rawLists = value['lists']
  if (isRecord(rawLists)) {
    for (const [id, list] of Object.entries(rawLists)) {
      if (!isRecord(list) || !Array.isArray(list['options'])) continue

      const groups: KioskTextList['groups'] = {}
      if (isRecord(list['groups'])) {
        for (const [group, labels] of Object.entries(list['groups'])) groups[group] = parseLabels(labels)
      }

      lists[id] = {
        groups,
        options: (list['options'] as unknown[])
          .filter((option): option is Record<string, unknown> => isRecord(option) && typeof option['value'] === 'string')
          .map((option) => ({
            value: option['value'] as string,
            group: typeof option['group'] === 'string' ? option['group'] : null,
            label: parseLabels(option['label']),
            hidden: option['hidden'] === true,
          })),
      }
    }
  }

  return { revision: value['revision'], strings, lists }
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
  if (submission.textsRevision !== undefined) form.append('textsRevision', String(submission.textsRevision))

  const { data } = await kioskApi.post<KioskSubmitResult>('/api/kiosk/questionnaire', form)

  return {
    formNumber: typeof data?.formNumber === 'number' ? data.formNumber : 0,
    submittedAt: typeof data?.submittedAt === 'string' ? data.submittedAt : '',
    telegramLink: telegramLinkOf(data?.telegramLink),
  }
}

/** The constructor's form being sent (SPEC-anketa-konstruktori §6). */
export interface DynamicSubmission {
  /** `{ fieldId: value }` (HRMentor §19.3 value shapes). */
  answers: Record<string, unknown>
  /** The version the candidate filled — the server validates against exactly that one (§5.3). */
  definitionVersion: number
  clientSubmissionId: string
  language: KioskLanguage
  photo: Blob
  signature: Blob
  website: string
  turnstileToken?: string | undefined
}

/** The server no longer knows the version this page rendered (§5.3) — the page must reload. */
export const DEFINITION_VERSION_UNKNOWN = 'anketa.versiya'

/** Same endpoint and parts as the built-in form, plus `definitionVersion` and no `textsRevision`. */
export async function submitDynamicQuestionnaire(submission: DynamicSubmission): Promise<KioskSubmitResult> {
  const form = new FormData()
  form.append('answers', JSON.stringify(submission.answers))
  form.append('definitionVersion', String(submission.definitionVersion))
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
    telegramLink: telegramLinkOf(data?.telegramLink),
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
