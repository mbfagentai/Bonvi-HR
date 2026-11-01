import { createContext, useContext } from 'react'

import i18n from '@/shared/i18n'
import { KIOSK_LANGUAGES, type KioskLanguage, type KioskTextList, type KioskTexts } from '../api/kiosk-api'
import { COMPUTER_GROUPS, isComputerSkillKey, lowerFirst, type ComputerGroup } from './kiosk-options'

/**
 * HR's wording from HRMentor on the form (SPEC-blankalar-hrmentor §5.1).
 *
 * Two halves:
 *  - Texts and option LABELS are laid over the bundled `kiosk.*` resources, so
 *    every existing translation call under `kiosk.` shows HR's word without a change at the
 *    call site. The bundled value is kept and put back when the texts change
 *    or the form is left (the HR app shares this i18n instance).
 *  - ORDER and HIDDEN are read through {@link KioskListsContext}: a screen asks
 *    `visibleValues(list, defaults, chosen)` instead of mapping its constant.
 */

const NS = 'translation'

/**
 * HR list id → the i18n prefix of its option labels (`kiosk.housing.apartment`).
 * Driver licence categories print as their codes and have no label key; the
 * computer list is handled on its own (HR-added skills have no bundled key).
 */
const LABEL_PREFIX: Readonly<Record<string, string>> = {
  housing: 'kiosk.housing',
  marital: 'kiosk.marital',
  military: 'kiosk.military',
  education: 'kiosk.education',
  startAfter: 'kiosk.startAfter',
  relation: 'kiosk.relation',
  language: 'kiosk.language',
}

export const COMPUTER_LIST = 'computer'

/** `language\u0000key` → the bundled text the overlay replaced. */
const originals = new Map<string, string>()
let applied: number | null = null

function overrides(texts: KioskTexts): Array<[KioskLanguage, string, string]> {
  const result: Array<[KioskLanguage, string, string]> = []

  for (const language of KIOSK_LANGUAGES) {
    for (const [key, text] of Object.entries(texts.strings[language] ?? {})) result.push([language, key, text])
  }

  for (const [id, list] of Object.entries(texts.lists)) {
    const prefix = LABEL_PREFIX[id]
    for (const option of list.options) {
      const key =
        prefix !== undefined
          ? `${prefix}.${lowerFirst(option.value)}`
          : id === COMPUTER_LIST && isComputerSkillKey(option.value)
            ? `kiosk.computer.skill.${option.value}`
            : null
      if (key === null) continue

      for (const language of KIOSK_LANGUAGES) {
        const label = option.label[language]
        if (label !== undefined) result.push([language, key, label])
      }
    }

    if (id === COMPUTER_LIST) {
      for (const [group, labels] of Object.entries(list.groups)) {
        for (const language of KIOSK_LANGUAGES) {
          const label = labels[language]
          if (label !== undefined) result.push([language, `kiosk.computer.group.${group}`, label])
        }
      }
    }
  }

  return result
}

/** Puts every bundled text back. Returns whether anything changed. */
export function restoreKioskTexts(): boolean {
  if (applied === null && originals.size === 0) return false

  for (const [id, text] of originals) {
    const [language, key] = id.split('\u0000') as [string, string]
    i18n.addResource(language, NS, key, text)
  }
  originals.clear()
  applied = null
  return true
}

/**
 * Lays HR's texts over the bundle; idempotent per revision, so it is safe to
 * call while rendering. Only keys the bundle has are replaced — a key the
 * bundle lacks could never be put back, and the server sends none.
 * Returns whether the resources changed.
 */
export function applyKioskTexts(texts: KioskTexts | null): boolean {
  if ((texts?.revision ?? null) === applied) return false

  restoreKioskTexts()
  if (texts === null) return true

  for (const [language, key, text] of overrides(texts)) {
    const bundled: unknown = i18n.getResource(language, NS, key)
    if (typeof bundled !== 'string') continue

    const id = `${language}\u0000${key}`
    if (!originals.has(id)) originals.set(id, bundled)
    i18n.addResource(language, NS, key, text)
  }

  applied = texts.revision
  return true
}

/* ───────────────────────────────────────────────────── order and hidden */

export const KioskListsContext = createContext<Readonly<Record<string, KioskTextList>>>({})

/** HR's list by id; `undefined` — HR did not change it (built-in order, nothing hidden). */
export function useKioskList(id: string): KioskTextList | undefined {
  return useContext(KioskListsContext)[id]
}

/** Every computer key the session's list offers, hidden ones too (a draft may hold them). */
export function offeredComputerKeys(lists: Readonly<Record<string, KioskTextList>>): ReadonlySet<string> {
  return new Set((lists[COMPUTER_LIST]?.options ?? []).map((option) => option.value))
}

/**
 * A closed list's values in HR's order. A hidden value stays only while it is
 * the chosen one; values the form does not know are dropped and any it knows
 * but HR's list lacks keep their place at the end (the server appends them
 * too, this is only a guard).
 */
export function visibleValues<T extends string>(
  list: KioskTextList | undefined,
  defaults: readonly T[],
  chosen: (value: T) => boolean = () => false,
): T[] {
  if (list === undefined) return [...defaults]

  const known = new Set<string>(defaults)
  const seen = new Set<string>()
  const result: T[] = []

  for (const option of list.options) {
    if (!known.has(option.value) || seen.has(option.value)) continue
    seen.add(option.value)
    if (!option.hidden || chosen(option.value as T)) result.push(option.value as T)
  }

  for (const value of defaults) if (!seen.has(value)) result.push(value)

  return result
}

export interface ComputerSkillRow {
  key: string
  label: string
}

export interface ComputerGroupRow {
  id: ComputerGroup
  skills: ComputerSkillRow[]
}

/**
 * The computer table: groups in the form's order, skills in HR's order with
 * HR-added ones in their group. A hidden skill stays only while it has a level.
 * Built-in labels come through `t` (the overlay already holds HR's word);
 * an HR-added skill has no bundled key, so its label is read from the list.
 */
export function computerRows(
  list: KioskTextList | undefined,
  defaults: Readonly<Record<ComputerGroup, readonly string[]>>,
  t: (key: string) => string,
  language: string,
  chosen: (key: string) => boolean,
): ComputerGroupRow[] {
  const labelOf = (key: string, labels?: Partial<Record<KioskLanguage, string>>) =>
    isComputerSkillKey(key)
      ? t(`kiosk.computer.skill.${key}`)
      : (labels?.[language as KioskLanguage] ?? labels?.['uz-Latn'] ?? key)

  if (list === undefined) {
    return COMPUTER_GROUPS.map((id) => ({ id, skills: defaults[id].map((key) => ({ key, label: labelOf(key) })) }))
  }

  return COMPUTER_GROUPS.map((id) => ({
    id,
    skills: list.options
      .filter((option) => option.group === id && (!option.hidden || chosen(option.value)))
      .map((option) => ({ key: option.value, label: labelOf(option.value, option.label) })),
  }))
}
