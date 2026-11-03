import type {
  Answers,
  BlankDefinition,
  ComputedValues,
  Condition,
  Field,
  JsonPrimitive,
  JsonValue,
  Section,
} from './definition-types'

/**
 * `when` conditions (SPEC §4.4) — JSON, never string eval.
 *
 * Semantics shared with the C# twin (`BlankConditions`):
 * - choice answers compare their `.value`; ref answers compare their `.id`;
 * - multi answers: `eq` / `in` match when ANY selected value matches;
 * - a field hidden by its own `when` (or by its section's) counts as empty —
 *   the server drops hidden values on save, so a later condition that reads it
 *   sees nothing. Conditions may only point at earlier items (§4.8), so one
 *   pass in definition order is enough.
 */

/** Empty = null, undefined, "" (after trim), [] (§5.3). */
export function isEmptyValue(value: JsonValue | undefined): boolean {
  if (value === null || value === undefined) return true
  if (typeof value === 'string') return value.trim() === ''
  if (Array.isArray(value)) return value.length === 0
  return false
}

function isRecord(value: JsonValue | undefined): value is { [key: string]: JsonValue } {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

/** The comparable scalar(s) of a stored answer. */
export function comparableValues(value: JsonValue | undefined): JsonPrimitive[] {
  if (value === undefined || value === null) return []
  if (Array.isArray(value)) return value.flatMap((item) => comparableValues(item))
  if (isRecord(value)) {
    if ('value' in value) return comparableValues(value['value'])
    if ('id' in value) return comparableValues(value['id'])
    return []
  }
  return [value]
}

export type ValueLookup = (fieldId: string) => JsonValue | undefined

export function evalCondition(condition: Condition | undefined, lookup: ValueLookup): boolean {
  if (!condition) return true
  if ('all' in condition) return condition.all.every((c) => evalCondition(c, lookup))
  if ('any' in condition) return condition.any.some((c) => evalCondition(c, lookup))
  if ('not' in condition) return !evalCondition(condition.not, lookup)

  const value = lookup(condition.field)
  if ('filled' in condition) return isEmptyValue(value) !== condition.filled
  const values = comparableValues(value)
  if ('eq' in condition) return values.some((v) => v === condition.eq)
  return values.some((v) => condition.in.includes(v))
}

/** Every field of the definition with its section, in definition order. */
export function* walkFields(definition: BlankDefinition): Generator<{ section: Section; field: Field }> {
  for (const section of definition.sections) {
    for (const field of section.fields) yield { section, field }
  }
}

export interface Visibility {
  hiddenSections: ReadonlySet<string>
  hiddenFields: ReadonlySet<string>
}

/**
 * Which sections and fields are hidden for these answers.
 * Hidden items are not rendered, not required, not validated (§4.4).
 */
export function computeVisibility(
  definition: BlankDefinition,
  answers: Answers,
  computed: ComputedValues = {},
): Visibility {
  const hiddenSections = new Set<string>()
  const hiddenFields = new Set<string>()
  const lookup: ValueLookup = (id) =>
    hiddenFields.has(id) ? undefined : id in answers ? answers[id] : computed[id]

  for (const section of definition.sections) {
    const sectionVisible = evalCondition(section.when, lookup)
    if (!sectionVisible) hiddenSections.add(section.id)
    for (const field of section.fields) {
      if (!sectionVisible || !evalCondition(field.when, lookup)) hiddenFields.add(field.id)
    }
  }
  return { hiddenSections, hiddenFields }
}

/** Answers with hidden values removed — what the server would store (§4.4). */
export function dropHidden(answers: Answers, visibility: Visibility): Answers {
  const result: Answers = {}
  for (const [key, value] of Object.entries(answers)) {
    if (!visibility.hiddenFields.has(key)) result[key] = value
  }
  return result
}
