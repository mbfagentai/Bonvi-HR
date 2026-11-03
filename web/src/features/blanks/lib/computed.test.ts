import { describe, expect, it } from 'vitest'

import { HR_F06_EXAMPLE } from '../components/renderer/renderer-demo-definition'
import { answersLookup, evaluateComputed, evaluateExpr } from './computed'
import type { Answers, Expr, JsonValue } from './definition-types'

const run = (expr: Expr, answers: Answers = {}) => evaluateExpr(expr, answersLookup(answers, {}))

const BANDS = [
  { from: 3.5, label: "o'tdi" },
  { from: 3.0, label: 'shartli' },
  { from: null, label: "o'tmadi" },
]

describe('computed — SPEC §4.5 examples', () => {
  it('F06 day7Pass: gte(percent, 70)', () => {
    const expr: Expr = { op: 'gte', args: [{ ref: 'day7Percent' }, { const: 70 }] }
    expect(run(expr, { day7Percent: 70 })).toBe(true)
    expect(run(expr, { day7Percent: 69.9 })).toBe(false)
    expect(run(expr, {})).toBeNull()
  })

  it('avg over a table column ignores empty cells; avg of nothing = null', () => {
    const expr: Expr = { op: 'avg', args: [{ ref: 'crit[*].score' }] }
    expect(run(expr, { crit: [{ score: 4 }, { score: 3 }, { score: null }, {}] })).toBe(3.5)
    expect(run(expr, { crit: [] })).toBeNull()
    expect(run(expr, {})).toBeNull()
  })

  it('sum / count / min / max', () => {
    const args: Expr[] = [{ ref: 't[*].n' }, { const: 10 }]
    const answers = { t: [{ n: 1 }, { n: 5 }, { n: null }] }
    expect(run({ op: 'sum', args }, answers)).toBe(16)
    expect(run({ op: 'count', args }, answers)).toBe(3)
    expect(run({ op: 'min', args }, answers)).toBe(1)
    expect(run({ op: 'max', args }, answers)).toBe(10)
    expect(run({ op: 'sum', args: [{ ref: 'none' }] })).toBe(0)
    expect(run({ op: 'count', args: [{ ref: 'none' }] })).toBe(0)
    expect(run({ op: 'max', args: [{ ref: 'none' }] })).toBeNull()
  })

  it('band: first band with value ≥ from, high → low', () => {
    const band = (v: JsonValue) => run({ op: 'band', value: { const: v as number }, bands: BANDS })
    expect(band(3.5)).toBe("o'tdi")
    expect(band(3.49)).toBe('shartli')
    expect(band(3.0)).toBe('shartli')
    expect(band(1)).toBe("o'tmadi")
    expect(band(null)).toBeNull()
  })

  it('diffDays = to − from (F08 stajDays)', () => {
    const expr: Expr = { op: 'diffDays', args: [{ ref: 'hire' }, { ref: 'leave' }] }
    expect(run(expr, { hire: '2026-01-01', leave: '2026-03-01' })).toBe(59)
    expect(run(expr, { hire: '2024-02-28', leave: '2024-03-01' })).toBe(2)
    expect(run(expr, { hire: '2026-01-01' })).toBeNull()
  })

  it('addDays (step due: hireDate + 7 / 30 / 90)', () => {
    const due = (n: number) => run({ op: 'addDays', args: [{ ref: 'hireDate' }, { const: n }] }, { hireDate: '2026-12-28' })
    expect(due(7)).toBe('2027-01-04')
    expect(due(30)).toBe('2027-01-27')
    expect(due(-28)).toBe('2026-11-30')
  })

  it('arithmetic: any null → null, div by zero → null', () => {
    expect(run({ op: 'add', args: [{ const: 2 }, { const: 3 }] })).toBe(5)
    expect(run({ op: 'sub', args: [{ const: 2 }, { const: 3 }] })).toBe(-1)
    expect(run({ op: 'mul', args: [{ const: 2 }, { const: 3 }] })).toBe(6)
    expect(run({ op: 'div', args: [{ const: 3 }, { const: 2 }] })).toBe(1.5)
    expect(run({ op: 'div', args: [{ const: 3 }, { const: 0 }] })).toBeNull()
    expect(run({ op: 'add', args: [{ const: 2 }, { ref: 'x' }] })).toBeNull()
  })

  it('compare + if; choice answers read as .value', () => {
    const expr: Expr = {
      op: 'if',
      args: [{ op: 'eq', args: [{ ref: 'decision' }, { const: 'stop' }] }, { const: 'yes' }, { const: 'no' }],
    }
    expect(run(expr, { decision: { value: 'stop', extra: 'x' } })).toBe('yes')
    expect(run(expr, { decision: { value: 'continue' } })).toBe('no')
    expect(run(expr, {})).toBeNull()
    expect(run({ op: 'lt', args: [{ const: '2026-01-01' }, { const: '2026-01-02' }] })).toBe(true)
  })

  it('evaluateComputed: chains computed fields in definition order', () => {
    const answers: Answers = {
      day7Percent: 82,
      criteria: [
        { _row: 'c1', score: 4 },
        { _row: 'c2', score: 3 },
      ],
    }
    const computed = evaluateComputed(HR_F06_EXAMPLE, answers)
    expect(computed['day7Pass']).toBe(true)
    expect(computed['avgScore']).toBe(3.5)
    expect(computed['verdict']).toBe("o'tdi")
  })

  it('computed table column resolves refs against its own row', () => {
    const definition = {
      ...HR_F06_EXAMPLE,
      sections: [
        {
          id: 'x',
          title: 'x',
          fields: [
            {
              id: 'tests',
              type: 'table' as const,
              columns: [
                { id: 'percent', type: 'percent' as const, label: '%' },
                {
                  id: 'pass',
                  type: 'computed' as const,
                  label: 'pass',
                  valueType: 'bool' as const,
                  expr: { op: 'gte' as const, args: [{ ref: 'percent' }, { const: 70 }] as [Expr, Expr] },
                },
              ],
            },
          ],
        },
      ],
    }
    const computed = evaluateComputed(definition, { tests: [{ percent: 90 }, { percent: 50 }, {}] })
    expect(computed['tests[0].pass']).toBe(true)
    expect(computed['tests[1].pass']).toBe(false)
    expect(computed['tests[2].pass']).toBeNull()
  })
})

/*
 * TODO(blanks-merge): shared C#/TS vectors — SPEC §4.5. The file is written by
 * slice S2 (`api/tests/Bonvi.Application.Tests/Blanks/computed-vectors.json`).
 * Until it exists (or when the web runs in Docker with only `web/` mounted) the
 * glob is empty and the test is skipped. Expected shape — adjust here at merge
 * if S2 chose another: `[{ name, expr, answers, expected }]` or `{ vectors: [...] }`.
 */
const vectorFiles = import.meta.glob<{ default: unknown }>(
  '../../../../../api/tests/Bonvi.Application.Tests/Blanks/computed-vectors.json',
  { eager: true },
)
interface Vector {
  name: string
  expr: Expr
  answers?: Answers
  expected: JsonValue
}
const vectorFile = Object.values(vectorFiles)[0]?.default
const vectors: Vector[] = Array.isArray(vectorFile)
  ? (vectorFile as Vector[])
  : ((vectorFile as { vectors?: Vector[] } | undefined)?.vectors ?? [])

describe.skipIf(vectors.length === 0)('computed — shared vectors (computed-vectors.json)', () => {
  it.each(vectors.map((v) => [v.name, v] as const))('%s', (_, vector) => {
    const actual = run(vector.expr, vector.answers ?? {})
    if (typeof vector.expected === 'number' && typeof actual === 'number')
      expect(actual).toBeCloseTo(vector.expected, 9)
    else expect(actual).toEqual(vector.expected)
  })
})
