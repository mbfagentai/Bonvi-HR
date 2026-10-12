import { useMemo } from 'react'

import type { RoleCatalog } from '@/features/roles/api/roles-api'
import { deriveLevel } from '@/features/roles/lib/role-access'
import type { AccessLevel } from '@/features/roles/lib/role-keys'
import { AuditAction, AuditActorKind } from '@/shared/api/generated/model'
import { useLocale } from '@/shared/i18n/use-locale'
import { formatPhone } from '@/shared/lib/phone'

import type { AuditActor, AuditChange, AuditEntry } from '../api/audit-queries'
import {
  AUDIT_ENTITY_NAMES,
  AUDIT_FIELD_KEYS,
  AUDIT_LOGIN_REASONS,
  AUDIT_OPERATION_NAMES,
  auditActionKey,
} from './audit-keys'

/**
 * ⭐ Jurnal qiymatlarini O'QILADIGAN matnga aylantirish (SPEC §9.5).
 *
 * Server qiymatni turi bilan yuboradi (`valueType`): sana, lahza, pul,
 * havola, sanoq… Bu fayl har birini HR o'qiydigan ko'rinishga keltiradi —
 * sanalar `formatDate`, lahzalar Toshkent vaqtida, pul ming ajratgichi va
 * valyuta bilan, havolalar NOMI bilan (identifikator emas), sanoqlar
 * mavjud tarjimalar orqali («Ko'chirish», «Sinovdan o'tdi»).
 *
 * Funksiyalar SOF: tilga bog'liq hamma narsa `AuditFormatter` orqali
 * keladi (`useAuditFormatter`). Shu sababli ular komponentdan tashqarida
 * ham chaqiriladi — ro'yxatdagi bir qatorlik xulosa ham, tafsilot oynasi ham
 * BITTA qoidadan foydalanadi.
 *
 * Qiymat `unknown` deb olinadi va turiga qarab toraytiriladi: generatsiya
 * qilingan `JsonElement` — bo'sh interfeys, ichida nima kelishini tip
 * aytmaydi. Kutilmagan shakl (masalan sanoq o'rnida son) xato bermaydi —
 * u oddiy matn bo'lib chiqadi.
 */

/** Tarjima funksiyasi — `useLocale().t` ning shu yerda kerakli qismi. */
export type Translate = (key: string, options?: Record<string, unknown>) => string

export interface AuditFormatter {
  t: Translate
  num: (value: number) => string
  money: (value: number, currency?: string) => string
  date: (value: string) => string
  dateTime: (value: string) => string
}

/** Yashirilgan qiymat — SPEC §9.5: «•••» va «Ko'rish huquqi yo'q» izohi. */
export const MASKED_TEXT = '•••'

/** Bo'sh qiymat — «yuklanmadimi?» degan savol tug'ilmasin. */
export const EMPTY_TEXT = '—'

export function useAuditFormatter(): AuditFormatter {
  const { t, num, money, date, dateTime } = useLocale()

  return useMemo<AuditFormatter>(
    () => ({ t: (key, options) => t(key, options), num, money, date, dateTime }),
    [t, num, money, date, dateTime],
  )
}

/**
 * Tarjima topilmasa — zaxira matn. i18next yo'q kalitga kalitning o'zini
 * qaytaradi va ekranda «audit.fields.Foo» paydo bo'lardi.
 */
function translateOr(fmt: AuditFormatter, key: string, fallback: string): string {
  const text = fmt.t(key)
  return text === key ? fallback : text
}

function lowerFirst(value: string): string {
  return value.charAt(0).toLowerCase() + value.slice(1)
}

function upperFirst(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

/* ════════════════════════════════════════════════════════ yorliqlar */

const FIELD_KEYS: ReadonlySet<string> = new Set(AUDIT_FIELD_KEYS)
const ENTITY_NAMES: ReadonlySet<string> = new Set(AUDIT_ENTITY_NAMES)
const OPERATION_NAMES: ReadonlySet<string> = new Set(AUDIT_OPERATION_NAMES)
const LOGIN_REASONS: ReadonlySet<string> = new Set(AUDIT_LOGIN_REASONS)

/**
 * Maydon yorlig'i. `known: false` — tarjimasi yo'q, xom nom (ekranda
 * ikkinchi darajali rangda, SPEC §9.9).
 *
 * Kengaytirishdan oldingi qatorlarda nomlar kichik harf bilan
 * (`permissions`) — ular ham taniladi.
 */
export function fieldLabel(fmt: AuditFormatter, field: string): { text: string; known: boolean } {
  const key = field.replaceAll('.', '_')
  const candidates = [key, upperFirst(key)]

  for (const candidate of candidates) {
    if (FIELD_KEYS.has(candidate)) {
      return { text: fmt.t(`audit.fields.${candidate}`), known: true }
    }
  }

  return { text: field, known: false }
}

export function actionLabel(fmt: AuditFormatter, action: AuditAction): string {
  return fmt.t(`audit.actions.${auditActionKey(action)}`)
}

/**
 * Qatordagi amal yorlig'i. Yagona istisno: xodim ilovasi parolini KO'RISH
 * ham `Export` bo'lib yoziladi (ma'lumot tizimdan chiqdi), lekin unga
 * «Excelga yuklandi» deyish yolg'on bo'lardi.
 */
export function entryActionLabel(fmt: AuditFormatter, action: AuditAction, operation: string | null): string {
  return action === AuditAction.Export && operation === 'RevealEmployeePassword'
    ? fmt.t('audit.actionRevealed')
    : actionLabel(fmt, action)
}

/** Entity turining yorlig'i («Buyruq», «Xodim kartasi»); notanishi — xom nom. */
export function entityTypeLabel(fmt: AuditFormatter, entityName: string): string {
  return ENTITY_NAMES.has(entityName) ? fmt.t(`audit.entities.${entityName}`) : entityName
}

/**
 * Biznes amalining nomi («Ko'chirish», «Ishdan bo'shatish») — `null`,
 * agar tanish bo'lmasa: u holda ekran amal turini ko'rsatadi
 * («O'zgartirildi», SPEC §9.9 dagi zaxira).
 *
 * Tizim amallari (`etl:…`, `startup:…`, `job:…`) umumiy nom bilan, sabab:
 * ularning qadamlari ko'p va HR uchun «Excel'dan ko'chirish» yetarli.
 */
export function operationLabel(fmt: AuditFormatter, operation: string | null): string | null {
  if (operation === null || operation === '') return null

  if (OPERATION_NAMES.has(operation)) return fmt.t(`audit.operations.${operation}`)
  if (operation.startsWith('etl:')) return fmt.t('audit.operationEtl')
  if (operation.startsWith('startup:')) return fmt.t('audit.operationStartup')
  if (operation.startsWith('job:')) return fmt.t('audit.operationJob')
  // Ma'lumotnomalarning umumiy endpointlari: `ActivateLegalEntity`, `DeactivateDirection`…
  if (operation.startsWith('Deactivate')) return fmt.t('audit.operationDeactivate')
  if (operation.startsWith('Activate')) return fmt.t('audit.operationActivate')

  return null
}

/** Qatorning sarlavhasi: biznes amali, bo'lmasa amal turi. */
export function entryTitle(fmt: AuditFormatter, entry: AuditEntry): string {
  return operationLabel(fmt, entry.operation) ?? actionLabel(fmt, entry.action)
}

/* ══════════════════════════════════════════════════════ bajaruvchi */

export interface ActorView {
  /** Ekrandagi nom. */
  name: string
  /** Ikkinchi qator: o'sha lahzadagi rol nomi yoki bajaruvchi turi. */
  secondary: string | null
  /** Odammi (bosh harfli doira) yoki tizim/qurilma (ikonka). */
  person: boolean
}

/**
 * Bajaruvchi. Rol nomi — O'SHA LAHZADAGI nusxa (`roleName`), bugungisi
 * emas: egasi rolni qayta nomlasa tarix o'zgarmasin (SPEC §9.5). Rol nomi
 * ma'lumot, tarjima qilinmaydi.
 */
export function actorView(fmt: AuditFormatter, actor: AuditActor): ActorView {
  switch (actor.kind) {
    case AuditActorKind.System:
      return { name: fmt.t('audit.system'), secondary: null, person: false }

    case AuditActorKind.Device:
      return { name: fmt.t('audit.actorDevice'), secondary: null, person: false }

    case AuditActorKind.EmployeeAccount:
      return {
        name: actor.name ?? fmt.t('audit.actorEmployeeApp'),
        secondary: fmt.t('audit.actorEmployeeApp'),
        person: actor.name !== null,
      }

    case AuditActorKind.Anonymous: {
      // Notanish login bilan kirish urinishi — normallashgan telefon raqami
      const login = actor.login === null ? null : formatPhone(actor.login)
      return login === null
        ? { name: fmt.t('audit.actorAnonymous'), secondary: null, person: false }
        : { name: login, secondary: fmt.t('audit.actorAnonymous'), person: false }
    }

    default: {
      const login = actor.login === null ? null : formatPhone(actor.login)
      return {
        name: actor.name ?? login ?? fmt.t('audit.actorUnknown'),
        secondary: actor.roleName,
        person: true,
      }
    }
  }
}

/* ═══════════════════════════════════════════════════════════ qiymatlar */

/** Maydonning tomoni: oldingi yoki keyingi qiymat. */
export type ChangeSide = 'old' | 'new'

/** Telefon raqami saqlanadigan maydonlar — «+998 90 123 45 67» ko'rinishida. */
const PHONE_FIELDS: ReadonlySet<string> = new Set(['Phone', 'SecondPhone', 'Login'])

/**
 * Sanoq maydonlarining tarjimasi — MAVJUD kalitlar orqali (SPEC §9.5).
 * Kalit: `Entity.Maydon`, qiymat: server sanog'ining nomidan kalit yasash.
 * Tarjimasi topilmagan qiymat xom chiqadi.
 */
const ENUM_KEYS: Readonly<Record<string, (value: string) => string>> = {
  'EmploymentEvent.Type': (value) => `employees.events.${lowerFirst(value)}`,
  'EmployeeFlag.Type': (value) => `employees.flags.${lowerFirst(value)}`,
  'Probation.Status': (value) => `probation.status.${lowerFirst(value)}`,
  'Employment.Status': (value) => `employee.status${upperFirst(value)}`,
  'Employee.Gender': (value) => `employees.gender${upperFirst(value)}`,
  'Candidate.Status': (value) => `recruiting.candidateStatus.${lowerFirst(value)}`,
  'Candidate.Source': (value) => `recruiting.source.${lowerFirst(value)}`,
  'Vacancy.Status': (value) => `recruiting.vacancyStatus.${lowerFirst(value)}`,
  'CandidateNote.Kind': (value) => `recruiting.noteKind.${lowerFirst(value)}`,
}

function enumText(fmt: AuditFormatter, entityName: string, field: string, value: string): string {
  const keyOf = ENUM_KEYS[`${entityName}.${field}`]
  return keyOf === undefined ? value : translateOr(fmt, keyOf(value), value)
}

/** Havola yorlig'i topilmasa — qisqa identifikator (8 belgi), to'liq GUID emas. */
function shortId(value: unknown): string {
  const text = typeof value === 'string' ? value : JSON.stringify(value)
  return text.length > 9 ? `${text.slice(0, 8)}…` : text
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function yesNo(fmt: AuditFormatter, value: boolean): string {
  return value ? fmt.t('employees.stateYes') : fmt.t('employees.stateNo')
}

/** Pul: `{ amount, currency }`. Valyuta yo'q bo'lsa — so'm (bazadagi hamma oklad UZS). */
function moneyText(fmt: AuditFormatter, value: unknown): string | null {
  if (!isRecord(value)) return typeof value === 'number' ? fmt.num(value) : null

  const amount = value['amount']
  const currency = value['currency']
  if (typeof amount !== 'number') return null

  return fmt.money(amount, typeof currency === 'string' && currency !== '' ? currency : undefined)
}

/** Sanoqsiz, turi noma'lum qiymat — ixcham matn. */
function plainText(fmt: AuditFormatter, value: unknown): string {
  if (typeof value === 'string') return value
  if (typeof value === 'number') return fmt.num(value)
  if (typeof value === 'boolean') return yesNo(fmt, value)
  if (Array.isArray(value)) return value.map((item) => plainText(fmt, item)).join(', ')
  return compactJson(fmt, value)
}

/**
 * JSON — ixcham, o'qiladigan ko'rinishda: «status: active, rows: 12».
 * Qavs va qo'shtirnoqlar HR uchun shovqin.
 */
function compactJson(fmt: AuditFormatter, value: unknown): string {
  if (isRecord(value)) {
    const parts = Object.entries(value)
      .filter(([, item]) => item !== null && item !== undefined)
      .map(([key, item]) => `${key}: ${plainText(fmt, item)}`)
    return parts.length === 0 ? EMPTY_TEXT : parts.join(', ')
  }

  return value === null || value === undefined ? EMPTY_TEXT : JSON.stringify(value)
}

/**
 * Bitta qiymatning matni. `null` — qiymat yo'q (ekranda «—»).
 *
 * Yashirilgan (`masked`) maydon bu yerga kelmaydi — uni chaqiruvchi
 * «•••» bilan o'zi chizadi (izohi bilan).
 */
export function changeValueText(
  fmt: AuditFormatter,
  entry: AuditEntry,
  change: AuditChange,
  side: ChangeSide,
): string | null {
  const value: unknown = side === 'old' ? change.old : change.new
  const label = side === 'old' ? change.oldLabel : change.newLabel

  if (value === null || value === undefined) return null

  switch (change.valueType) {
    case 'ref':
      return label ?? shortId(value)

    case 'list': {
      if (label !== null) return label
      if (!Array.isArray(value)) return plainText(fmt, value)
      return value.length === 0 ? null : value.map((item) => plainText(fmt, item)).join(', ')
    }

    case 'money':
      return moneyText(fmt, value)

    case 'date':
      return typeof value === 'string' ? fmt.date(value) : plainText(fmt, value)

    case 'datetime':
      return typeof value === 'string' ? fmt.dateTime(value) : plainText(fmt, value)

    case 'bool':
      return typeof value === 'boolean' ? yesNo(fmt, value) : plainText(fmt, value)

    case 'number':
      if (typeof value === 'number') return fmt.num(value)
      return typeof value === 'string' && value.trim() !== '' && Number.isFinite(Number(value))
        ? fmt.num(Number(value))
        : plainText(fmt, value)

    case 'enum':
      return typeof value === 'string'
        ? enumText(fmt, entry.entityName, change.field, value)
        : plainText(fmt, value)

    case 'json':
      return compactJson(fmt, value)

    default:
      return textValue(fmt, change.field, value)
  }
}

/** Matnli maydon — kirish hodisasining sababi, eksport amali va telefonlar alohida. */
function textValue(fmt: AuditFormatter, field: string, value: unknown): string | null {
  if (typeof value !== 'string') return plainText(fmt, value)
  if (value === '') return null

  if (field === 'reason' && LOGIN_REASONS.has(value)) return fmt.t(`audit.reasons.${value}`)
  if (field === 'action' && value === 'password_viewed') return fmt.t('audit.passwordViewed')
  if (PHONE_FIELDS.has(field)) return formatPhone(value) ?? value

  return value
}

/* ═════════════════════════════════════════════════ rol ruxsatlari */

/** Rolning ruxsatlar ro'yxati — darajalar farqi sifatida chiziladi (pastda). */
export function isRolePermissions(entry: AuditEntry, change: AuditChange): boolean {
  return entry.entityName === 'Role' && change.field.toLowerCase() === 'permissions'
}

function codesOf(value: unknown): Set<string> {
  return new Set(
    Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [],
  )
}

/** Rollar muharririning yorliqlari (`useRoleLabels`) — shu yerda kerakli qismi. */
export interface RoleLabelSet {
  module: (key: string) => string
  level: (level: AccessLevel) => string
  action: (code: string) => string
  extra: (code: string) => string
}

/**
 * ⭐ Rol ruxsatlarining o'zgarishi — kodlar emas, muharrirdagi tilda:
 * «Xodimlar: To'liq → O'zgartirish», «+ Okladni ko'rish», «− Ishdan
 * bo'shatish» (SPEC §9.5).
 *
 * Daraja kodlardan chiqariladi — muharrirning o'zi qanday chiqarsa,
 * aynan shunday (`deriveLevel`): jurnal va muharrir bir rolni ikki xil
 * ko'rsatmasligi kerak. Katalog bo'lmasa (yuklanmagan) yoki kod katalogda
 * yo'q bo'lsa (eski kod) — «+ kod» ko'rinishida, tanish nomi bilan.
 */
export function rolePermissionLines(
  change: AuditChange,
  catalog: RoleCatalog | null,
  labels: RoleLabelSet,
): string[] {
  const before = codesOf(change.old)
  const after = codesOf(change.new)

  const lines: string[] = []
  const explained = new Set<string>()

  if (catalog !== null) {
    for (const module of catalog.modules) {
      const from = deriveLevel(module, before)
      const to = deriveLevel(module, after)

      for (const codes of Object.values(module.levels)) {
        for (const code of codes ?? []) explained.add(code)
      }

      if (from !== to) {
        lines.push(`${labels.module(module.key)}: ${labels.level(from)} → ${labels.level(to)}`)
      }

      for (const code of module.actions) {
        explained.add(code)
        if (after.has(code) && !before.has(code)) lines.push(`+ ${labels.action(code)}`)
        if (before.has(code) && !after.has(code)) lines.push(`− ${labels.action(code)}`)
      }
    }

    for (const code of catalog.extras) {
      explained.add(code)
      if (after.has(code) && !before.has(code)) lines.push(`+ ${labels.extra(code)}`)
      if (before.has(code) && !after.has(code)) lines.push(`− ${labels.extra(code)}`)
    }
  }

  // Katalog tushuntirmagan kodlar — yorlig'i bo'lsa yorliq, bo'lmasa kodning o'zi
  const codeLabel = (code: string) => {
    const action = labels.action(code)
    return action === code ? labels.extra(code) : action
  }

  for (const code of [...after].sort()) {
    if (!before.has(code) && !explained.has(code)) lines.push(`+ ${codeLabel(code)}`)
  }
  for (const code of [...before].sort()) {
    if (!after.has(code) && !explained.has(code)) lines.push(`− ${codeLabel(code)}`)
  }

  return lines
}

/* ══════════════════════════════════════════════ bir qatorlik xulosa */

/** Xulosada ko'rsatiladigan maydonlar soni — qolgani «yana N». */
const SUMMARY_FIELDS = 4

/** Qatordagi o'zgarishlar bitta qatorda; bo'sh satr — aytadigan narsa yo'q. */
export function changeSummary(fmt: AuditFormatter, entry: AuditEntry): string {
  const detail = eventSummary(fmt, entry)
  if (detail !== null) return detail

  const changes = entry.changes
  if (changes.length === 0) return ''

  // Bitta maydonli tahrir — qiymati bilan: «Oklad: 4 000 000 → 5 000 000»
  const [only] = changes
  if (entry.action === AuditAction.Update && changes.length === 1 && only !== undefined && !isRolePermissions(entry, only)) {
    const label = fieldLabel(fmt, only.field).text
    if (only.masked) return `${label}: ${MASKED_TEXT}`

    const before = changeValueText(fmt, entry, only, 'old') ?? EMPTY_TEXT
    const after = changeValueText(fmt, entry, only, 'new') ?? EMPTY_TEXT
    return `${label}: ${before} → ${after}`
  }

  const labels = changes.slice(0, SUMMARY_FIELDS).map((change) => fieldLabel(fmt, change.field).text)
  const rest = changes.length - SUMMARY_FIELDS

  return rest > 0
    ? `${labels.join(', ')} ${fmt.t('audit.moreFields', { n: fmt.num(rest) })}`
    : labels.join(', ')
}

/** Kirish hodisalari va eksport — tafsilotdan bitta jumla; boshqa amallarda `null`. */
function eventSummary(fmt: AuditFormatter, entry: AuditEntry): string | null {
  const valueOf = (field: string): unknown =>
    entry.changes.find((change) => change.field === field)?.new ?? null

  switch (entry.action) {
    case AuditAction.LoginFailed: {
      const reason = valueOf('reason')
      return typeof reason === 'string' && LOGIN_REASONS.has(reason)
        ? fmt.t(`audit.reasons.${reason}`)
        : ''
    }

    case AuditAction.PermissionDenied: {
      const method = valueOf('method')
      const path = valueOf('path')
      return [method, path].filter((part): part is string => typeof part === 'string').join(' ')
    }

    case AuditAction.Logout:
      return valueOf('allSessions') === true ? fmt.t('audit.allSessions') : ''

    case AuditAction.Login:
    case AuditAction.TokenReuse:
      return ''

    case AuditAction.Export: {
      if (valueOf('action') === 'password_viewed') return fmt.t('audit.passwordViewed')
      const rows = valueOf('rows')
      return typeof rows === 'number' ? fmt.t('audit.rows', { n: fmt.num(rows) }) : ''
    }

    default:
      return null
  }
}

/* ════════════════════════════════════════════════════ qurilma satri */

/**
 * Brauzer satridan qisqa nom: «Chrome · Windows», «Safari · iPhone».
 *
 * To'liq satr (300 belgigacha) HR uchun o'qib bo'lmaydigan shovqin, kerak
 * bo'lsa u `title` da turadi. Tanilmagan satr (bot, skript) — o'zi, qisqartirib.
 */
export function simplifyUserAgent(userAgent: string): string {
  const browser = BROWSERS.find(([pattern]) => pattern.test(userAgent))?.[1] ?? null
  const os = SYSTEMS.find(([pattern]) => pattern.test(userAgent))?.[1] ?? null

  if (browser === null && os === null) {
    return userAgent.length > 60 ? `${userAgent.slice(0, 60)}…` : userAgent
  }

  return [browser, os].filter((part): part is string => part !== null).join(' · ')
}

/** Tartib MUHIM: Edge va Opera satrida «Chrome» ham bor, Chrome'nikida «Safari». */
const BROWSERS: ReadonlyArray<readonly [RegExp, string]> = [
  [/Edg(e|A|iOS)?\//u, 'Edge'],
  [/OPR\/|Opera/u, 'Opera'],
  [/YaBrowser\//u, 'Yandex'],
  [/SamsungBrowser\//u, 'Samsung Internet'],
  [/Firefox\/|FxiOS\//u, 'Firefox'],
  [/Chrome\/|CriOS\//u, 'Chrome'],
  [/Version\/[\d.]+.*Safari\//u, 'Safari'],
]

const SYSTEMS: ReadonlyArray<readonly [RegExp, string]> = [
  [/iPhone/u, 'iPhone'],
  [/iPad/u, 'iPad'],
  [/Android/u, 'Android'],
  [/Windows/u, 'Windows'],
  [/CrOS/u, 'ChromeOS'],
  [/Mac OS X|Macintosh/u, 'macOS'],
  [/Linux/u, 'Linux'],
]

/* ═════════════════════════════════════════════════════ guruhlash */

/** Bitta biznes amali (bir xil `correlationId`) — ketma-ket qatorlar. */
export interface AuditGroup {
  key: string
  entries: AuditEntry[]
}

/**
 * Ketma-ket, bir xil `correlationId` li qatorlar bitta guruhga (SPEC §9.5):
 * «Ko'chirish» — mehnat munosabati tahriri + yangi buyruq, ikkalasi bitta
 * sarlavha ostida. Faqat KETMA-KET: ro'yxat vaqt bo'yicha tartiblangan va
 * guruhni yig'ish uchun qatorlarni joyidan surish tartibni buzardi.
 */
export function groupEntries(entries: readonly AuditEntry[]): AuditGroup[] {
  const groups: AuditGroup[] = []

  for (const entry of entries) {
    const last = groups.at(-1)
    const lastEntry = last?.entries.at(-1)

    if (
      last !== undefined &&
      entry.correlationId !== null &&
      lastEntry?.correlationId === entry.correlationId
    ) {
      last.entries.push(entry)
    } else {
      groups.push({ key: entry.id, entries: [entry] })
    }
  }

  return groups
}
