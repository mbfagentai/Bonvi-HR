/**
 * TypeScript mirror of the blank template definition (SPEC-blankalar §4, `schema: 1`)
 * and of the submission DTOs the renderer consumes (§7).
 *
 * The definition is authored as JSON on the server (seeded template files) and
 * delivered by `GET /api/blanks/template-versions/{id}`. The server validates it
 * (§4.8), so the web trusts its SHAPE but never its content: every evaluator in
 * this folder treats unknown values defensively (null), never throws.
 */

// ── JSON values ──────────────────────────────────────────────────────────────

export type JsonPrimitive = string | number | boolean | null
export type JsonValue = JsonPrimitive | JsonValue[] | { [key: string]: JsonValue }

// ── Conditions (§4.4) ────────────────────────────────────────────────────────

export type Condition =
  | { field: string; eq: JsonPrimitive }
  | { field: string; in: JsonPrimitive[] }
  | { field: string; filled: boolean }
  | { all: Condition[] }
  | { any: Condition[] }
  | { not: Condition }

// ── Computed expressions (§4.5) ──────────────────────────────────────────────

export interface Band {
  /** `null` = catch-all (last band). Bands are ordered high → low. */
  from: number | null
  label: string
}

export type AggregateOp = 'avg' | 'sum' | 'count' | 'min' | 'max'
/** `round`: args = [value, digits 0–10], half away from zero. */
export type ArithmeticOp = 'sub' | 'add' | 'mul' | 'div' | 'round'
export type CompareOp = 'gte' | 'gt' | 'lte' | 'lt' | 'eq'
/** [date, integer count] → date. */
export type DateShiftOp = 'addDays' | 'addMonths' | 'addWorkingDays'
/** [from, to] → whole days; working days count (from; to], Monday–Friday. */
export type DateDiffOp = 'diffDays' | 'diffWorkingDays'

export type Expr =
  | { ref: string }
  | { const: JsonPrimitive }
  | { op: AggregateOp; args: Expr[] }
  | { op: ArithmeticOp; args: [Expr, Expr] }
  | { op: DateShiftOp; args: [Expr, Expr] }
  | { op: DateDiffOp; args: [Expr, Expr] }
  | { op: CompareOp; args: [Expr, Expr] }
  | { op: 'and' | 'or'; args: Expr[] }
  | { op: 'not'; args: [Expr] }
  | { op: 'band'; value: Expr; bands: Band[] }
  | { op: 'if'; args: [Expr, Expr, Expr] }

// ── Prefill (§4.6) ───────────────────────────────────────────────────────────

export type PrefillSource =
  | 'subject.fullName'
  | 'subject.positionName'
  | 'subject.departmentName'
  | 'subject.directionName'
  | 'subject.hireDate'
  | 'subject.terminationDate'
  | 'subject.probationEndDate'
  | 'subject.departmentHeadId'
  | 'currentUser.employeeId'
  | 'today'
  | 'subject.priorF13Count12m'

// ── Options (choice / multi / step decision) ────────────────────────────────

export type OptionExtraType = 'text' | 'longtext' | 'date' | 'int' | 'ref' | 'docRef'

export interface OptionExtra {
  type: OptionExtraType
  label: string
  required?: boolean
  /** Only for `type: "ref"`; defaults to `employee`. */
  refKind?: RefKind
  /** Only for `type: "docRef"`. */
  templateCodes?: string[]
}

export interface FieldOption {
  value: string
  label: string
  extra?: OptionExtra
}

// ── Fields (§4.3) ────────────────────────────────────────────────────────────

export type RefKind = 'employee' | 'candidate' | 'department' | 'position' | 'direction'

/** Date bound: ISO date, `today` or `today+N` / `today-N`. */
export type DateBound = string

export interface FieldBase {
  id: string
  label?: string
  hint?: string
  required?: boolean
  when?: Condition
  readOnly?: boolean
  prefill?: PrefillSource
  /** PDF column share in tables — ignored by the web renderer. */
  pdfWidth?: number
}

export interface TextField extends FieldBase {
  type: 'text'
  maxLength?: number
}
export interface LongTextField extends FieldBase {
  type: 'longtext'
  maxLength?: number
}
export interface IntField extends FieldBase {
  type: 'int'
  min?: number
  max?: number
}
export interface PercentField extends FieldBase {
  type: 'percent'
  passAt?: number
}
export interface MoneyField extends FieldBase {
  type: 'money'
  min?: number
  max?: number
}
export interface DateField extends FieldBase {
  type: 'date'
  min?: DateBound
  max?: DateBound
}
export interface DateTimeField extends FieldBase {
  type: 'datetime'
  min?: DateBound
  max?: DateBound
}
export interface MonthField extends FieldBase {
  type: 'month'
  min?: string
  max?: string
}
export interface YearField extends FieldBase {
  type: 'year'
  min?: number
  max?: number
}
export interface DateRangeFieldDef extends FieldBase {
  type: 'dateRange'
}
export interface YesNoField extends FieldBase {
  type: 'yesno'
}
export interface ChoiceField extends FieldBase {
  type: 'choice'
  options: FieldOption[]
}
export interface MultiField extends FieldBase {
  type: 'multi'
  options: FieldOption[]
  maxSelected?: number
}
export interface CheckField extends FieldBase {
  type: 'check'
}
export interface ScoreField extends FieldBase {
  type: 'score'
}
export interface RefField extends FieldBase {
  type: 'ref'
  refKind: RefKind
}
export interface DocRefField extends FieldBase {
  type: 'docRef'
  templateCodes: string[]
}
export interface OrderField extends FieldBase {
  type: 'order'
}
export interface StaticField extends FieldBase {
  type: 'static'
  /** Supports `{{fieldId}}` placeholders. */
  text: string
}

export type FileAccept = 'pdf' | 'image' | 'office' | 'any'
export type FileCapture = 'environment' | 'user'

export interface FileFieldDef extends FieldBase {
  type: 'file'
  accept?: FileAccept[]
  /** 1–20, default 5 (§8). */
  maxFiles?: number
  /** 1–20, default 20 (§8). */
  maxSizeMb?: number
  capture?: FileCapture | null
}

export interface FixedRowCell {
  readOnly?: boolean
  text?: string
}
export interface FixedRow {
  id: string
  label: string
  cells?: Record<string, FixedRowCell>
}

export interface TableField extends FieldBase {
  type: 'table'
  columns: ColumnField[]
  minRows?: number
  maxRows?: number
  fixedRows?: FixedRow[]
}

export type ComputedValueType = 'number' | 'date' | 'bool' | 'text'

export interface ComputedField extends FieldBase {
  type: 'computed'
  valueType: ComputedValueType
  expr: Expr
}

/** Every field kind that can live inside a table (no table nesting, §4.3). */
export type ColumnField = Exclude<Field, TableField | StaticField>

export type Field =
  | TextField
  | LongTextField
  | IntField
  | PercentField
  | MoneyField
  | DateField
  | DateTimeField
  | MonthField
  | YearField
  | DateRangeFieldDef
  | YesNoField
  | ChoiceField
  | MultiField
  | CheckField
  | ScoreField
  | RefField
  | DocRefField
  | OrderField
  | StaticField
  | FileFieldDef
  | TableField
  | ComputedField

export type FieldType = Field['type']

// ── Sections and steps (§4.2, §4.7) ─────────────────────────────────────────

export type SectionFill = { at: 'draft' } | { at: 'step'; step: string } | { at: 'open' }

export interface Section {
  id: string
  title: string
  hint?: string
  when?: Condition
  /** Default `{ at: "draft" }`. */
  fill?: SectionFill
  fields: Field[]
}

export type StepMode = 'electronic' | 'inPerson' | 'either'

export interface StepDefinition {
  id: string
  title: string
  order: number
  /** §6.1 role code or `field:<refEmployeeFieldId>`. */
  actor: string
  mode: StepMode
  when?: Condition
  decision?: { options: FieldOption[]; required: boolean }
  canReject?: boolean
  canReturn?: boolean
  due?: { field: string; days: number }
}

// ── Top level (§4.1) ────────────────────────────────────────────────────────

export type SubjectKind = 'employee' | 'candidate' | 'none'

export interface BlankDefinition {
  schema: 1
  code: string
  version: string
  title: string
  subject: SubjectKind
  legalStatus?: 'final' | 'draft'
  organization?: string
  retention?: string
  dueDate?: { field: string }
  sections: Section[]
  steps?: StepDefinition[]
}

// ── Stored answer shapes (§4.3 «Stored value») ───────────────────────────────

export interface ChoiceAnswer {
  value: string
  extra?: JsonValue
}
export interface RefAnswer {
  id: string
  label: string
}
export interface DocRefAnswer {
  submissionId: string
  templateCode: string
  number: string
}
export interface OrderAnswer {
  number: string
  date: string
}
export interface DateRangeAnswer {
  from: string
  to: string
}

/** Answers are keyed by field id (not by section); stored as jsonb. */
export type Answers = Record<string, JsonValue>
/** Server-evaluated computed values — keyed by computed field id. */
export type ComputedValues = Record<string, JsonValue>

// ── Submission DTOs (§7) used by the renderer ────────────────────────────────

export type BlankSubmissionStatus = 'Draft' | 'InApproval' | 'Approved' | 'Rejected' | 'Cancelled'
export type BlankStepState = 'Waiting' | 'Open' | 'Signed' | 'Skipped' | 'Rejected'

export interface BlankAttachmentDto {
  id: string
  fieldPath: string
  kind: string
  fileName: string
  sizeBytes: number
  uploadedAt: string
}

export interface BlankActionableStep {
  stepCode: string
  electronic: boolean
  inPerson: boolean
}

export interface BlankCan {
  edit: boolean
  submit: boolean
  cancel: boolean
  editableSections: string[]
  actionableSteps: BlankActionableStep[]
}

export interface BlankTemplateVersionDto {
  id: string
  templateId: string
  code: string
  version: string
  status: string
  definition: BlankDefinition
}
