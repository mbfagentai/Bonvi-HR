import type {
  Answers,
  BlankDefinition,
  ComputedValues,
  Expr,
  JsonValue,
  TableField,
} from './definition-types'

/**
 * Computed expressions (SPEC §4.5) — the web twin of the server's
 * `BlankComputedEvaluator`. The server value is authoritative (it is evaluated
 * on every save); this one only makes the form feel live between autosaves.
 * Both MUST pass `api/tests/Bonvi.Application.Tests/Blanks/computed-vectors.json`.
 *
 * Rules:
 * - aggregates (`avg|sum|count|min|max`) flatten arrays and ignore empty/null
 *   inputs; `avg|min|max` of nothing = null, `sum|count` of nothing = 0;
 * - every other operator returns null when any input is null (or of the wrong
 *   type); `div` by zero = null;
 * - dates are `yyyy-MM-dd` strings (a longer ISO string is cut to its date);
 * - `{"ref":"t[*].c"}` reads column `c` of every row of table `t` (an array);
 * - choice answers read as their `.value`, ref answers as their `.id`;
 * - a computed COLUMN of a table resolves bare refs against its own row first,
 *   then against the top level; its result is keyed `table[i].col`.
 * The evaluator never throws: malformed input yields null.
 */

const TABLE_REF = /^([a-z][A-Za-z0-9]*)\[\*\]\.([a-z][A-Za-z0-9]*)$/
const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})/
const DAY_MS = 86_400_000

export type Lookup = (ref: string) => JsonValue | undefined

function scalar(value: JsonValue | undefined): JsonValue {
  if (value === undefined) return null
  if (typeof value === 'string' && value.trim() === '') return null
  if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
    if ('value' in value) return scalar(value['value'])
    if ('id' in value) return scalar(value['id'])
  }
  return value
}

function asNumber(value: JsonValue): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null
}

/** Days since epoch of an ISO date, or null. */
function dayNumber(value: JsonValue): number | null {
  if (typeof value !== 'string') return null
  const match = ISO_DATE.exec(value)
  if (!match) return null
  const [, y, m, d] = match
  const ms = Date.UTC(Number(y), Number(m) - 1, Number(d))
  return Number.isNaN(ms) ? null : Math.round(ms / DAY_MS)
}

function isoFromDayNumber(day: number): string {
  return new Date(day * DAY_MS).toISOString().slice(0, 10)
}

function flatten(values: JsonValue[]): JsonValue[] {
  return values.flatMap((v) => (Array.isArray(v) ? flatten(v) : [scalar(v)]))
}

function compare(a: JsonValue, b: JsonValue): number | null {
  if (typeof a === 'number' && typeof b === 'number') return a - b
  if (typeof a === 'string' && typeof b === 'string') return a < b ? -1 : a > b ? 1 : 0
  return null
}

export function evaluateExpr(expr: Expr, lookup: Lookup): JsonValue {
  if ('ref' in expr) {
    const value = lookup(expr.ref)
    return Array.isArray(value) ? value.map((v) => scalar(v)) : scalar(value)
  }
  if ('const' in expr) return expr.const

  switch (expr.op) {
    case 'avg':
    case 'sum':
    case 'count':
    case 'min':
    case 'max': {
      const items = flatten(expr.args.map((a) => evaluateExpr(a, lookup))).filter(
        (v) => v !== null,
      )
      if (expr.op === 'count') return items.length
      const numbers = items.map(asNumber).filter((n): n is number => n !== null)
      if (expr.op === 'sum') return numbers.reduce((s, n) => s + n, 0)
      if (numbers.length === 0) return null
      if (expr.op === 'avg') return numbers.reduce((s, n) => s + n, 0) / numbers.length
      return expr.op === 'min' ? Math.min(...numbers) : Math.max(...numbers)
    }
    case 'add':
    case 'sub':
    case 'mul':
    case 'div': {
      const a = asNumber(evaluateExpr(expr.args[0], lookup))
      const b = asNumber(evaluateExpr(expr.args[1], lookup))
      if (a === null || b === null) return null
      if (expr.op === 'add') return a + b
      if (expr.op === 'sub') return a - b
      if (expr.op === 'mul') return a * b
      return b === 0 ? null : a / b
    }
    case 'addDays': {
      const day = dayNumber(evaluateExpr(expr.args[0], lookup))
      const n = asNumber(evaluateExpr(expr.args[1], lookup))
      if (day === null || n === null || !Number.isInteger(n)) return null
      return isoFromDayNumber(day + n)
    }
    case 'diffDays': {
      const from = dayNumber(evaluateExpr(expr.args[0], lookup))
      const to = dayNumber(evaluateExpr(expr.args[1], lookup))
      return from === null || to === null ? null : to - from
    }
    case 'gte':
    case 'lt':
    case 'eq': {
      const a = evaluateExpr(expr.args[0], lookup)
      const b = evaluateExpr(expr.args[1], lookup)
      if (a === null || b === null || Array.isArray(a) || Array.isArray(b)) return null
      if (expr.op === 'eq') return a === b
      const diff = compare(a, b)
      if (diff === null) return null
      return expr.op === 'gte' ? diff >= 0 : diff < 0
    }
    case 'band': {
      const value = asNumber(evaluateExpr(expr.value, lookup))
      if (value === null) return null
      const band = expr.bands.find((b) => b.from === null || value >= b.from)
      return band ? band.label : null
    }
    case 'if': {
      const condition = evaluateExpr(expr.args[0], lookup)
      if (typeof condition !== 'boolean') return null
      return evaluateExpr(condition ? expr.args[1] : expr.args[2], lookup)
    }
    default:
      return null
  }
}

function rowsOf(value: JsonValue | undefined): { [key: string]: JsonValue }[] {
  if (!Array.isArray(value)) return []
  return value.filter(
    (row): row is { [key: string]: JsonValue } =>
      typeof row === 'object' && row !== null && !Array.isArray(row),
  )
}

/** Top-level lookup: answers, then already-computed values; `t[*].c` → array. */
export function answersLookup(answers: Answers, computed: ComputedValues): Lookup {
  return (ref) => {
    const table = TABLE_REF.exec(ref)
    if (table) {
      const [, tableId = '', col = ''] = table
      return rowsOf(answers[tableId]).map((row, i) => {
        const key = `${tableId}[${i}].${col}`
        return col in row ? (row[col] ?? null) : (computed[key] ?? null)
      })
    }
    return ref in answers ? answers[ref] : computed[ref]
  }
}

function evaluateTableColumns(
  table: TableField,
  answers: Answers,
  computed: ComputedValues,
): void {
  const columns = table.columns.filter((c) => c.type === 'computed')
  if (columns.length === 0) return
  const top = answersLookup(answers, computed)
  rowsOf(answers[table.id]).forEach((row, i) => {
    for (const column of columns) {
      const lookup: Lookup = (ref) => {
        if (ref in row) return row[ref]
        const rowKey = `${table.id}[${i}].${ref}`
        if (rowKey in computed) return computed[rowKey]
        return top(ref)
      }
      computed[`${table.id}[${i}].${column.id}`] = evaluateExpr(column.expr, lookup)
    }
  })
}

/**
 * Every computed field of the definition, in definition order (a computed field
 * may read earlier computed fields — §4.8 forbids forward refs and cycles).
 */
export function evaluateComputed(definition: BlankDefinition, answers: Answers): ComputedValues {
  const computed: ComputedValues = {}
  const lookup = answersLookup(answers, computed)
  for (const section of definition.sections) {
    for (const field of section.fields) {
      if (field.type === 'computed') computed[field.id] = evaluateExpr(field.expr, lookup)
      else if (field.type === 'table') evaluateTableColumns(field, answers, computed)
    }
  }
  return computed
}
