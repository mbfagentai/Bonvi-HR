# SPEC — «Blankalar»: HR paper blanks HR-F01…F19 filled online

> Status: authoritative for build. Written 2026-11-03. Inputs: `docs/INTAKE-hr-blankalar.md` (field inventory,
> roles, 22 questions), `docs/SPEC-planshet-anketa.md` (HR-F02, hard-coded). Decisions taken here are logged
> in `docs/ASSUMPTIONS.md` (2026-11-03). Owner questions: §15.
>
> **Reference implementations** (copy their shape, do not edit them):
> aggregate + status + errors → `api/src/Bonvi.Domain/Recruiting/VacancyRequest.cs`;
> optimistic `Version` → `api/src/Bonvi.Domain/Attendance/AbsenceRequest.cs`;
> endpoints → `api/src/Bonvi.Api/Endpoints/VacancyRequestEndpoints.cs`;
> upload/download → `api/src/Bonvi.Api/Endpoints/EmployeeDocumentEndpoints.cs`;
> PDF → `api/src/Bonvi.Infrastructure/Reports/Pdf/QuestionnairePdfRenderer.cs`;
> MCP tool → `api/src/Bonvi.Api/Mcp/Tools/HrRecordTools.cs`;
> grants migration → `api/src/Bonvi.Infrastructure/Persistence/Migrations/20261026101017_QayerdanBilgan.cs`;
> web list page → `web/src/features/recruiting/components/vacancy-requests-page.tsx`.

## 1. Goal, scope, non-goals

HR fills the company's paper blanks in the platform. One **generic engine** (template definition JSON + submission
with answers, steps, attachments, PDF) serves every blank; a new blank is a reviewed JSON file, not code.
Filled blanks are structured data that AI agents (project «AIAgents») read through the existing read-only MCP
tools / public FullRead API, including «what is missing».

| In scope — Phase 1 (wave 1) | Not in Phase 1 |
|---|---|
| Engine: templates (seeded), submissions, steps, attachments, PDF, MCP read | Template builder UI (Phase 3) |
| Blanks **HR-F06, F07, F08, F09, F13** | F01 (owner Q1), F02 (stays hard-coded, untouched) |
| «Blankalar» menu, fill/view page, employee card tab | Employee-app (PWA) channel for heads/mentors/employees (Phase 3, seam §6.4) |
| Per-field configurable file uploads incl. Office | AI «template from uploaded Word/PDF» (Phase 3, seam §9) |
| Role-assignment settings (HR-1…HR-4 etc. → users) | Writing into existing modules (Probation, ExitInterview, Incentive, Employment) |

**Additive rule.** No existing behaviour changes. Existing files are touched ONLY at registration points (§12).
`CandidateForm`, `ExitInterview`, `Probation`, `Incentive`, `IFileContentInspector`, `DocumentLimits` are not modified.

## 2. Overlap with existing data (decided: LINK, never duplicate or sync)

| Blank | Existing data | Decision |
|---|---|---|
| F06/F07 | `Probation` (status, end date, conclusion) | Blank prefills from the subject's probation. F07 approval does **not** resolve the probation (needs `employment.probation.complete` + order). MCP `blank_gaps` reports «F07 approved, probation still InProgress after end date». |
| F08 | `ExitInterview` (free-text reason, recommend) | Separate record; no auto-create. Both linked by `employment_id`. «Qayta aloqa» page untouched. |
| F09 | `Employment.Terminate` | No gate on termination in Phase 1; `blank_gaps` reports «terminated without approved F09». |
| F13 | `Incentive` (penalty) | No auto penalty; the measure is data in the blank. |

## 3. Data model (PostgreSQL, snake_case, `uuid` v7 ids, `timestamptz` UTC, display in Tashkent +5)

Column-name rule: the MCP grant function hides every column whose name matches
`(key|file|photo|passport|pasport|pinfl|jshshir|token|secret|hash|screenshot|selfie|attachment|password|login)`.
Columns meant for AI must NOT match it (hence `step_code`, `actor_role`, `role_code`); storage keys and file
names MUST match it (`*_storage_key`, `file_name`).

### 3.1 `blank_templates` — one row per blank code
| Column | Type | Null | Default | Note |
|---|---|---|---|---|
| id | uuid | no | | PK |
| code | varchar(16) | no | | `HR-F06`; UNIQUE |
| title | varchar(200) | no | | «Moslashtirish rejasi …» |
| subject_kind | smallint | no | | 1 Employee, 2 Candidate, 3 None |
| is_active | bool | no | true | false → no new submissions; existing ones keep working |
| sort_order | int | no | 0 | list order (code order by default) |
| created_at/by, updated_at/by | | | | `IAuditable` |

### 3.2 `blank_template_versions` — immutable once Published
| Column | Type | Null | Note |
|---|---|---|---|
| id | uuid | no | PK |
| template_id | uuid | no | FK → blank_templates RESTRICT; index |
| version | varchar(8) | no | `1.0`; UNIQUE (template_id, version) |
| status | smallint | no | 1 Draft, 2 Published, 3 Retired. Partial UNIQUE (template_id) WHERE status = 2 |
| definition | jsonb | no | §4; ≤ 256 KB |
| definition_sha256 | char(64) | no | hex of canonical JSON; seeder change detection |
| source | smallint | no | 1 Seed, 2 Builder, 3 Imported |
| published_at / published_by | timestamptz / uuid | yes | |
| created_at/by, updated_at/by | | | |

Publishing a version sets the previous Published one to Retired in the same transaction. Retired versions remain
readable (old submissions render with their own version). A Published definition is never edited.

### 3.3 `blank_submissions` — aggregate root (children: steps, attachments)
| Column | Type | Null | Default | Note |
|---|---|---|---|---|
| id | uuid | no | | PK |
| template_id | uuid | no | | FK RESTRICT |
| template_version_id | uuid | no | | FK RESTRICT — definition used for validation, UI and PDF forever |
| template_code | varchar(16) | no | | denormalized for filters/MCP |
| number | int | yes | | assigned at first submit from `blank_number_counters`; partial UNIQUE (template_code, number) WHERE number IS NOT NULL |
| status | smallint | no | 1 | 1 Draft, 2 InApproval, 3 Approved, 4 Rejected, 5 Cancelled |
| subject_kind | smallint | no | | copy of template's |
| employee_id | uuid | yes | | FK employees RESTRICT; index |
| employment_id | uuid | yes | | FK employments RESTRICT; the active employment at creation, else the latest |
| candidate_id | uuid | yes | | FK candidates RESTRICT; index |
| department_id | uuid | yes | | FK departments SET NULL; snapshot at creation (employment's dept; candidate → vacancy's dept); index |
| answers | jsonb | no | `'{}'` | §4.4; ≤ 512 KB serialized |
| computed | jsonb | no | `'{}'` | server-evaluated computed fields `{fieldPath: value}` |
| missing | jsonb | no | `'[]'` | required-but-empty field paths of sections already due (§5.3) |
| missing_count | int | no | 0 | = length of `missing`; index (template_code, missing_count) |
| due_date | date | yes | | from definition `dueDate` (F09 «Tugallanishi kerak»); else the earliest pending step due |
| version | int | no | 0 | concurrency token; +1 on every change; conditional UPDATE (AbsenceRequest pattern) |
| client_request_id | uuid | yes | | UNIQUE; create idempotency |
| submitted_at, completed_at, cancelled_at | timestamptz | yes | | |
| cancel_reason | varchar(1000) | yes | | |
| return_comment | varchar(2000) | yes | | last «return» comment (§5); shown on the fill page, cleared by the next submit |
| final_pdf_storage_key | varchar(500) | yes | | PDF snapshot written at Approved |
| created_at/by, updated_at/by | | | | |

Constraints: CHECK subject columns match `subject_kind` (Employee → employee_id NOT NULL, candidate_id NULL;
Candidate → candidate_id NOT NULL, employee_id NULL; None → both NULL). Indexes also on (status, template_code),
(created_at DESC). No soft delete — Cancelled is the end state; rows are never deleted.

### 3.4 `blank_submission_steps` — materialized at submit from the definition
| Column | Type | Null | Note |
|---|---|---|---|
| id | uuid | no | PK |
| submission_id | uuid | no | FK CASCADE; index |
| step_code | varchar(64) | no | definition step id; UNIQUE (submission_id, step_code) |
| order_no | int | no | equal numbers = parallel group |
| title | varchar(200) | no | snapshot |
| actor_role | varchar(32) | no | §6.1 |
| mode | smallint | no | 1 Electronic, 2 InPerson, 3 Either |
| state | smallint | no | 1 Waiting (earlier group open), 2 Open, 3 Signed, 4 Skipped (condition false), 5 Rejected |
| decision | varchar(64) | yes | chosen option value of the step (e.g. `agree`, `object`) |
| comment | varchar(2000) | yes | |
| due_date | date | yes | §4.5 `due` |
| method | smallint | yes | 1 Electronic, 2 InPerson — how it was actually signed |
| acted_by_user_id | uuid | yes | FK users; the panel user who clicked (electronic) or operated the tablet (in-person) |
| signer_employee_id | uuid | yes | FK employees; the person who signed in person / resolved person role |
| signer_name | varchar(200) | yes | snapshot of the signer's full name (witness: typed) |
| signature_storage_key | varchar(500) | yes | PNG, re-drawn on white by the server (F02 rule), ≤ 300 KB, ≤ 1200×600 |
| answers_patch | jsonb | yes | the section values this actor submitted with the act (legal trail) |
| acted_at | timestamptz | yes | |

### 3.5 `blank_attachments`
| Column | Type | Null | Note |
|---|---|---|---|
| id | uuid | no | PK |
| submission_id | uuid | no | FK CASCADE; index |
| field_path | varchar(200) | no | `evidence` or `handover[2].doc` |
| kind | smallint | no | 1 Pdf, 2 Jpeg, 3 Png, 4 Webp, 5 Docx, 6 Xlsx, 7 Pptx |
| file_name | varchar(255) | no | original name, sanitized (hidden from MCP by pattern) |
| content_type | varchar(100) | no | from detected kind, never from the client |
| size_bytes | bigint | no | |
| content_sha256 | char(64) | no | |
| storage_key | varchar(500) | no | `IFileStorage` key |
| uploaded_by | uuid | no | users.id |
| uploaded_at | timestamptz | no | |
| removed_at / removed_by | timestamptz / uuid | yes | removal only while the field is editable; the file is deleted from disk after commit |

### 3.6 `blank_role_assignments`
id uuid PK · role_code varchar(32) · user_id uuid FK users CASCADE · created_at/by. UNIQUE (role_code, user_id).

### 3.7 `blank_number_counters`
template_code varchar(16) PK · last_number int not null. Next number: `INSERT … ON CONFLICT (template_code) DO UPDATE
SET last_number = blank_number_counters.last_number + 1 RETURNING last_number` inside the submit transaction
(gapless, row-locked). Numbers never reset per year.

### 3.8 Edge cases
Money = whole so'm (`long`), no decimals, no currency field (UZS only). Percent = 0–100 with ≤ 1 decimal.
Dates = Tashkent calendar dates (`DateOnly`); datetimes stored UTC, sent as ISO with offset, shown +5.
Score = int 1–5. Averages rounded half-away-from-zero to 2 decimals (F07 «O'rtacha ball»).

## 4. Template definition (JSON, `schema: 1`)

### 4.1 Top level
| Key | Type | Req | Meaning |
|---|---|---|---|
| schema | int | yes | always 1 |
| code, version, title | string | yes | must equal the template row / file name |
| subject | `employee`\|`candidate`\|`none` | yes | |
| legalStatus | `final`\|`draft` | no (final) | `draft` → PDF prints «Loyiha — yurist tasdig'idan o'tmagan» under the title |
| organization | string | no | header left text; default «MBF Company — HR hujjatlar paketi» |
| retention | string | no | printed last line («Saqlash: …») |
| dueDate | `{ "field": "<date field id>" }` | no | submission `due_date` |
| sections | Section[] | yes | ≥ 1 |
| steps | Step[] | no | empty → submit = Approved |

### 4.2 Section
`{ "id", "title", "hint"?, "when"?, "fill"?, "fields": Field[] }`.
`fill` = who edits it and when: `{"at":"draft"}` (default — creator, before submit) ·
`{"at":"step","step":"<stepId>"}` (filled by that step's actor at signing; required-check at that step) ·
`{"at":"open"}` (editable by `blanks.fill` any time until Approved; required-check only at the final step).

### 4.3 Field (common keys)
`id` (`^[a-z][A-Za-z0-9]{0,47}$`, unique across the whole definition; answers are keyed by field id, not by
section; table columns are unique within their table and addressed as `tableId[i].colId`), `type`, `label`, `hint`?, `required`? (false), `when`?,
`readOnly`? (false), `prefill`? (§4.6), `pdfWidth`? (column share in tables).

| type | Extra keys | Stored value (answers) | Validation |
|---|---|---|---|
| text | maxLength (≤ 500, default 200) | string | trimmed; control/bidi/zero-width chars rejected (F02 rule) |
| longtext | maxLength (≤ 4000, default 2000) | string | newlines allowed |
| int | min, max | number (integer) | |
| percent | passAt? (e.g. 70) | number 0–100, ≤ 1 decimal | |
| money | min?, max? (≤ 1e12) | integer so'm | |
| date / datetime / month / year | min?, max? (`today`, `today+N`) | `yyyy-MM-dd` / ISO+offset / `yyyy-MM` / int | |
| dateRange | | `{ "from", "to" }` | from ≤ to |
| yesno | | bool | |
| choice | options[] | `{ "value", "extra"? }` | value ∈ options; extra required if the option's `extra.required` |
| multi | options[], maxSelected? | `[{ "value", "extra"? }]` | distinct; ≤ maxSelected |
| check | | bool | |
| score | min 1, max 5 (fixed) | int | |
| ref | refKind: `employee`\|`candidate`\|`department`\|`position`\|`direction` | `{ "id", "label" }` (label = name snapshot) | id exists (soft-deleted allowed for history) |
| docRef | templateCodes[] | `{ "submissionId", "templateCode", "number" }` | target exists, not Cancelled |
| order | | `{ "number": string ≤ 50, "date" }` | buyruq № + date |
| static | text (supports `{{fieldId}}` placeholders) | — | not stored |
| file | §8 keys | `["<attachmentId>", …]` | ids belong to this submission and this field_path |
| table | columns: Field[] (no table/static/file nesting beyond file), minRows?, maxRows? (≤ 200), fixedRows? | `[ { "_row"?: "<fixedRowId>", "<colId>": value } ]` | with fixedRows: exactly those rows, in order |
| computed | valueType `number`\|`date`\|`bool`\|`text`, expr | not in answers; in `computed` | evaluated server-side on every save |

Option: `{ "value": "^[a-z0-9_]{1,32}$", "label", "extra"?: { "type": "text"|"longtext"|"date"|"int"|"ref"|"docRef", "label", "required" } }`.
fixedRow: `{ "id", "label", "cells"?: { "<colId>": { "readOnly": true, "text": "printed value" } } }`.

### 4.4 Conditions (`when`) — JSON, no string eval
`{"field":"f","eq":v}` · `{"field":"f","in":[…]}` · `{"field":"f","filled":true}` · `{"all":[…]}` · `{"any":[…]}` · `{"not":{…}}`.
For choice fields `eq` compares `.value`. A field/section/step whose `when` is false is not required, not
validated, its stored value is dropped on save, and it prints as «—» (section: not printed).

### 4.5 Computed expressions and step dues
`expr` nodes: `{"ref":"fieldId"}`, `{"ref":"table[*].col"}` (array), `{"const":x}`, `{"op":"avg"|"sum"|"count"|"min"|"max","args":[…]}`
(empty/null inputs ignored; avg of nothing = null), `{"op":"sub"|"add"|"mul"|"div","args":[a,b]}`,
`{"op":"addDays","args":[date,n]}`, `{"op":"diffDays","args":[from,to]}` (to − from), `{"op":"gte"|"lt"|"eq","args":[a,b]}`,
`{"op":"band","value":x,"bands":[{"from":3.5,"label":"o'tdi"},{"from":3.0,"label":"shartli"},{"from":null,"label":"o'tmadi"}]}`
(first band with value ≥ from, ordered high → low), `{"op":"if","args":[cond,a,b]}`. Any null input → null.
Server (C#) and web (TS) evaluators MUST pass the same vector file `api/tests/Bonvi.Application.Tests/Blanks/computed-vectors.json`
(the web test reads it via relative path). Step `due`: `{ "field": "hireDate", "days": 7 }` → due_date = field + days.

### 4.6 Prefill sources (resolved once at draft creation; editable unless `readOnly`)
`subject.fullName`, `subject.positionName`, `subject.departmentName`, `subject.directionName`, `subject.hireDate`,
`subject.terminationDate`, `subject.probationEndDate`, `subject.departmentHeadId` (ref employee),
`currentUser.employeeId` (ref employee), `today`, `subject.priorF13Count12m` (int — the subject's Approved HR-F13 submissions in the last 12 months, server-side). Unknown key → definition invalid. Missing data → empty.

### 4.7 Step
`{ "id", "title", "order", "actor": "<role>", "mode": "electronic"|"inPerson"|"either", "when"?, "decision"?: { "options": Option[], "required": true }, "canReject"?: false, "canReturn"?: true, "due"? }`.
`actor` for person roles referencing a field: `"actor": "field:mentor"` (a ref-employee field).

### 4.8 Definition validator (Application, run by seeder and by future builder)
Rejects: unknown keys/types; duplicate ids; `when`/`expr`/`due`/`dueDate`/`fill.step` pointing to unknown or later-defined
items; cycles in computed refs; options without value; table nesting; `actor` not in §6.1 or `field:` pointing to a
non-employee ref; size limits (≤ 500 fields, ≤ 30 steps). Errors are returned as a list `(path, message)`.

### 4.9 Example — HR-F06 v1.0 (abridged: every construct used in wave 1 appears once)
```json
{ "schema": 1, "code": "HR-F06", "version": "1.0", "subject": "employee",
  "title": "Moslashtirish rejasi (1–30–60–90 kun) va ustoz biriktirish",
  "retention": "Nusxalar: xodim, ustoz, bo'lim rahbari, shaxsiy ish",
  "sections": [
    { "id": "hdr", "title": "Xodim", "fields": [
      { "id": "employee", "type": "ref", "refKind": "employee", "label": "Xodim F.I.Sh.", "prefill": "subject.fullName", "readOnly": true },
      { "id": "position", "type": "text", "label": "Lavozim / bo'lim", "prefill": "subject.positionName" },
      { "id": "hireDate", "type": "date", "label": "Ishga chiqqan sana", "required": true, "prefill": "subject.hireDate" },
      { "id": "probationEnd", "type": "date", "label": "Sinov muddati tugash sanasi", "prefill": "subject.probationEndDate" },
      { "id": "manager", "type": "ref", "refKind": "employee", "label": "Bevosita rahbar", "prefill": "subject.departmentHeadId" },
      { "id": "mentor", "type": "ref", "refKind": "employee", "label": "Ustoz-murabbiy", "required": true } ] },
    { "id": "s1", "title": "Ustoz-murabbiy majburiyatlari", "fields": [
      { "id": "duties", "type": "static", "text": "Kamida 1 yil shu lavozimda … birinchi 30 kunda har kuni ≥ 15 daqiqa …" } ] },
    { "id": "d7", "title": "7-kun testi", "fill": { "at": "step", "step": "day7" }, "fields": [
      { "id": "day7Percent", "type": "percent", "label": "Natija (%)", "passAt": 70, "required": true },
      { "id": "day7Pass", "type": "computed", "label": "O'tdi (≥ 70%)", "valueType": "bool",
        "expr": { "op": "gte", "args": [ { "ref": "day7Percent" }, { "const": 70 } ] } } ] },
    { "id": "d30", "title": "30-kun", "fill": { "at": "step", "step": "day30" }, "fields": [
      { "id": "day30Decision", "type": "choice", "label": "Qaror", "required": true, "options": [
        { "value": "continue", "label": "davom" }, { "value": "training", "label": "o'qitish" },
        { "value": "stop", "label": "to'xtatish", "extra": { "type": "longtext", "label": "Sabab", "required": true } } ] } ] },
    { "id": "s3", "title": "Birinchi 30 kundagi savollar", "fill": { "at": "open" }, "fields": [
      { "id": "questions", "type": "table", "minRows": 0, "maxRows": 50, "columns": [
        { "id": "date", "type": "date", "label": "Sana" },
        { "id": "question", "type": "longtext", "label": "Savol / muammo" },
        { "id": "answer", "type": "longtext", "label": "Javob / yechim (ustoz)" } ] } ] }
  ],
  "steps": [
    { "id": "signEmployee", "title": "Xodim tanishdi", "order": 1, "actor": "subjectEmployee", "mode": "either" },
    { "id": "signMentor", "title": "Ustoz", "order": 1, "actor": "field:mentor", "mode": "either" },
    { "id": "signHead", "title": "Bo'lim rahbari", "order": 1, "actor": "departmentHead", "mode": "either" },
    { "id": "signHr4", "title": "HR-4", "order": 1, "actor": "hr4", "mode": "electronic" },
    { "id": "day7", "title": "7-kun testi", "order": 2, "actor": "hr4", "mode": "electronic", "due": { "field": "hireDate", "days": 7 } },
    { "id": "day30", "title": "30-kun qarori", "order": 3, "actor": "departmentHead", "mode": "either", "due": { "field": "hireDate", "days": 30 } },
    { "id": "day90", "title": "90-kun: HR-1 qarori", "order": 6, "actor": "hr1", "mode": "electronic",
      "decision": { "required": true, "options": [ { "value": "passed", "label": "sinovdan o'tdi" }, { "value": "failed", "label": "o'tmadi" } ] },
      "due": { "field": "hireDate", "days": 90 } } ] }
```
The full F06 file also carries day1 (order 2, check), day60 (order 4, departmentHead, score), day85 (order 5, hr4,
percent) and the «open» mentor-notes section (rows 1–7, 8–30, 31–60 as longtext fields).

### 4.10 Wave-1 template content (author from INTAKE §2; labels verbatim; JSON files reviewed in code review)
| Code | Subject | Draft sections (creator) | Steps (order: actor, mode) | Notes |
|---|---|---|---|---|
| F06 | employee | hdr, s1 | §4.9 | day 60 = score 1–5 (INTAKE Q22: stored) |
| F07 | employee | hdr (prefill incl. probation end, mentor from latest approved F06 via docRef `f06`) | 1: departmentHead fills §1 (7 criteria fixedRows: score + note) and §3 conclusion, either · 1: field:mentor, either · 2: hr4 fills §2 tests (fixedRows d7, d85: date, percent, computed pass), electronic · 3: subjectEmployee, either, decision agree/object (+text) · 4: hr1, electronic, decision approve / extraInterview / lawyer, canReject | computed `avgScore` = avg of 7 scores (2 dec), `band` 3.5/3.0 bands — **suggestion only**, never blocks (INTAKE Q11) |
| F08 | employee | hdr (hire/leave dates, computed `stajDays` = diffDays), initiative choice, §1 K01–K12: `mainReason` choice (required) + `extraReasons` multi maxSelected 2, §2 Q1–Q7 (Q1 `when` initiative = employee) | 1: hr3 fills §3 (F09 status choice prefilled from linked F09 docRef), electronic · 2: subjectEmployee, either · 2: hr4, electronic | K-codes are fixed options, not the TerminationReason dictionary |
| F09 | employee | hdr (type choice, receiver ref field `receiver`, `mustCompleteBy` date = `dueDate`), §1–§4 tables (maxRows 50), §5 choices with extra, §6 | 1: subjectEmployee «Topshirdim», either · 2: field:receiver, either · 3: departmentHead, either · 4: hr3, electronic · 5: hr1, electronic · 6: hr3 «akt tugallangan — bo'shatish buyrug'iga ruxsat», electronic | §2–§4 prefill from F10 is Phase 3 (no F10 yet) |
| F13 | employee | part 1 (datetime, violation multi + lateness minutes extra, longtext, `evidence` file accept image+pdf, capture environment, maxFiles 10) | 1: hr3 «talabni topshirdi», electronic · 2: subjectEmployee fills part 2 explanation (longtext or `explanationFile`), inPerson, decision received/refused · 3 (`when` explanation empty): witness ×2 (`witness1`, `witness2`, inPerson, typed names) — part 3 · 4: hr3 fills part 4 (measure choice), electronic · 5: hr1, electronic, decision approve/changed/none (+text), canReject · 6: hr2 order (`order` field), electronic | `legalStatus: draft` (owner Q2); computed `prior12m` comes from the server-side prefill `subject.priorF13Count12m` (§4.6); witnesses = 2 (INTAKE Q14) |

## 5. Submission state machine

```
          create                submit (draft sections valid)          all non-skipped steps Signed
 (none) ───────▶ Draft ─────────────────────────────▶ InApproval ─────────────────────────────▶ Approved
                  ▲  │ cancel                           │  │ reject (step.canReject)                 
                  │  ▼                                  │  ▼                                         
                  │ Cancelled ◀── cancel (blanks.manage)┘ Rejected                                   
                  └──────── return (step.canReturn, comment required) ─┘
```
| Action | From | Who | Effect |
|---|---|---|---|
| create | — | `blanks.fill` | Draft, prefill, `client_request_id` idempotent (same id → 200 same body) |
| save answers | Draft: draft sections · InApproval: sections of an Open step the user may act on, and `open` sections | `blanks.fill` (or step actor, §6.2) | type/limit validation only; recompute `computed`, `missing` |
| submit | Draft | `blanks.fill` | required-check of draft sections → number (first submit only) → steps materialized (`when` false → Skipped) → lowest order group Open → InApproval, or Approved when no steps |
| approve (electronic) / sign in person | InApproval, step Open | §6.2 | step section required-check + `answers_patch` + decision → Signed; group done → next group Open; none left → Approved |
| return | InApproval, step Open, `canReturn` | step actor, comment required | Draft; all step rows deleted (their acts stay in the audit journal); number kept; the comment is stored in `blank_submissions.return_comment` varchar(2000) null (§3.3) and shown on the fill page until the next submit clears it |
| reject | InApproval, step Open, `canReject` | step actor, comment required | Rejected (end) |
| cancel | Draft (own: `blanks.fill`; anyone's: `blanks.manage`), InApproval (`blanks.manage`) | | Cancelled (end), reason required |

On Approved: render and store the final PDF (`final_pdf_storage_key`); failure is logged and never blocks
(on-demand rendering still works). Every mutation requires `expectedVersion` = current `version`, else 409 `blanka.ozgargan`.

### 5.3 Completeness (`missing`)
After every change: required, visible, empty fields of (a) draft sections, (b) step sections of Signed and Open
steps, (c) `open` sections when the last group is Open. Empty = null, "", [], or a table below `minRows`.
This is what AI agents read as «incomplete».

## 6. Actors and permissions

### 6.1 Actor roles
| Office roles (assignable to users, §3.6) | Person roles (resolved per submission) |
|---|---|
| `hr1` `hr2` `hr3` `hr4` `hr5` `hr8` `payrollAccountant` `chiefAccountant` `director` `lawyer` | `subjectEmployee` · `departmentHead` (Department.HeadEmployeeId of `department_id`) · `field:<id>` (employee ref in answers) · `witness` (typed name, in person only) · `candidate` (Phase 2) |

### 6.2 Who may act on a step
- **Electronic, office role**: user has `blanks.approve` AND (role has assignments ? user is assigned : any `blanks.approve` holder).
- **Electronic, person role**: the user's `users.employee_id` equals the resolved employee (permission `blanks.view` suffices for that user — signing one's own step needs no approve right). Unresolvable person (no head, empty field) → step can only be signed in person.
- **In person** (`inPerson`/`either`): operator has `blanks.fill`; signature PNG required; `signer_employee_id` = resolved person (witness: typed `signerName` required); `acted_by_user_id` = operator.
- Same user may not sign two steps of one parallel group as two different office roles? Allowed (small HR team) — decided, logged.

### 6.3 Permissions (`Permissions.cs`, module key `blanks`)
| Code | Level in PermissionCatalog | Grants |
|---|---|---|
| `blanks.view` | view | menu, lists, detail, PDF, attachment download, card tab, MCP tools |
| `blanks.fill` | edit | create, save, submit, attachments, in-person signing, cancel own draft |
| `blanks.manage` | full | role assignments, cancel any, template activate/deactivate (Phase 3: builder) |
| `blanks.approve` | action | electronic office-role steps, return, reject |

`Module(AccessModuleKeys.Blanks, view:[BlanksView], edit:[BlanksFill], full:[BlanksManage], actions:[BlanksApprove])`.
Add `blanks.view` to `ApiKeyScopes.FullReadPermissions` (existing FullRead keys gain blank reads; creating a new full key
now also requires `blanks.view` — super admin has it). Existing roles get the codes by hand in «Rollar» (no migration);
`IdentitySeeder` new-DB default for `hr_manager`: view + fill + approve.

### 6.4 Employee-app seam (Phase 3, not built)
Person-role resolution lives in one Application service `BlankActorResolver`. A later `/api/me/blanks/steps` lists Open
steps whose resolved employee = the app account's employee, and signs them (method Electronic, `acted_by_user_id`
null, `signer_employee_id` set). No schema change needed.

## 7. API contract (`/api/blanks`, all `RequireAuthorization`, JSON camelCase, problem+json errors)

| # | Method · path | Permission | Request | Response |
|---|---|---|---|---|
| 1 | GET `/templates` | view | — | `[{ id, code, title, subjectKind, isActive, version, versionId }]` (current Published) |
| 2 | GET `/template-versions/{id}` | view | — | `{ id, templateId, code, version, status, definition }` |
| 3 | PATCH `/templates/{id}` | manage | `{ isActive }` | template DTO |
| 4 | GET `/roles` | view | — | `[{ roleCode, users: [{ id, fullName }] }]` all office roles |
| 5 | PUT `/roles/{roleCode}` | manage | `{ userIds: uuid[] ≤ 20 }` | role DTO (replace set) |
| 6 | GET `/submissions` | view | query below | `PagedResult<BlankSubmissionListItemDto>` |
| 7 | POST `/submissions` | fill | `{ templateCode, employeeId?, candidateId?, clientRequestId }` | 201 detail DTO |
| 8 | GET `/submissions/{id}` | view | — | detail DTO (below) |
| 9 | PUT `/submissions/{id}/answers` | fill, or step actor (§6.2) | `{ expectedVersion, answers }` (full replacement of editable sections only; other sections ignored) | detail DTO |
| 10 | POST `/submissions/{id}/submit` | fill | `{ expectedVersion }` | detail DTO |
| 11 | POST `/submissions/{id}/cancel` | fill (+manage rule §5) | `{ expectedVersion, reason }` | detail DTO |
| 12 | POST `/submissions/{id}/steps/{stepCode}/approve` | approve, or own person step | `{ expectedVersion, decision?, decisionExtra?, comment?, answers? }` | detail DTO |
| 13 | POST `/submissions/{id}/steps/{stepCode}/sign-in-person` | fill | multipart: `payload` (JSON as #12 + `signerName?`), `signature` (PNG) | detail DTO |
| 14 | POST `/submissions/{id}/steps/{stepCode}/return` | approve, or own person step | `{ expectedVersion, comment }` | detail DTO |
| 15 | POST `/submissions/{id}/steps/{stepCode}/reject` | approve | `{ expectedVersion, comment }` | detail DTO |
| 16 | GET `/submissions/{id}/steps/{stepCode}/signature` | view | — | image/png |
| 17 | POST `/submissions/{id}/attachments` | fill, or step actor | multipart: `fieldPath`, `file` | `BlankAttachmentDto` |
| 18 | DELETE `/submissions/{id}/attachments/{attachmentId}` | fill, or step actor | `?expectedVersion=` | 204 |
| 19 | GET `/submissions/{id}/attachments/{attachmentId}` | view | — | file; PDF/images `inline`, Office `attachment`; `X-Content-Type-Options: nosniff` |
| 20 | GET `/submissions/{id}/pdf` | view | — | application/pdf, rendered fresh |

List query (#6): `templateCode`, `status` (name), `employeeId`, `candidateId`, `departmentId` (with sub-departments),
`from`/`to` (created date, Tashkent, inclusive), `search` (number, subject name — `search_key` rule), `waitingForMe` (bool:
Open steps the caller may act on electronically), `incomplete` (missing_count > 0), `overdue` (due_date < today and not
ended), `page`, `pageSize` (default 50, max 10 000 — project rule). Sort: `created_at` desc.
List item: `{ id, templateCode, templateTitle, number, status, subject: { kind, id, fullName }, departmentName,
currentSteps: [{ stepCode, title, actorRole, dueDate }], missingCount, dueDate, createdAt, submittedAt, completedAt }`.
Detail adds: `templateVersionId, version, answers, computed, missing, steps[] (all columns of §3.4 except storage key;
`hasSignature`, `actedByName`), attachments[] { id, fieldPath, kind, fileName, sizeBytes, uploadedAt }, can: { edit,
submit, cancel, editableSections[], actionableSteps: [{ stepCode, electronic, inPerson }] }`.
Upload limits: `DisableAntiforgery`, request size = field max + 1 MB (cap 21 MB); #13 cap 1 MB.

**Error codes** (Domain `BlankErrors`; translated in `web/src/shared/i18n/locales/*.json` → `errors.blanka.*`):
| Code | HTTP | When |
|---|---|---|
| blanka.topilmadi / shablon_topilmadi / qadam_topilmadi / fayl_topilmadi | 404 | |
| blanka.shablon_faol_emas | 409 | create on inactive template |
| blanka.subyekt_notogri | 400 | subject ids do not match subject_kind |
| blanka.xodim_topilmadi / nomzod_topilmadi / rol_notogri / foydalanuvchi_topilmadi | 404/400 | |
| blanka.holat_notogri | 409 | action not allowed in current status |
| blanka.ozgargan | 409 | expectedVersion mismatch |
| blanka.majburiy | 400 | required empty; `detail` = field path |
| blanka.notogri | 400 | wrong type/range/option; `detail` = field path |
| blanka.json / blanka.juda_katta | 400 | unreadable answers / > 512 KB |
| blanka.maydon_tahrirlanmaydi | 409 | field/section not editable now |
| blanka.qadam_ochiq_emas | 409 | step not Open |
| blanka.qadam_ruxsat_yoq | 403 | caller may not act on this step |
| blanka.ruxsat_yoq | 403 | cancel someone else's draft without manage |
| blanka.qaror_kerak / sabab_kerak / imzo_kerak / imzo_yaroqsiz / imzochi_kerak | 400 | decision, comment, PNG, witness name |
| blanka.rad_mumkin_emas / qaytarish_mumkin_emas | 409 | step flags |
| blanka.fayl_turi / fayl_eski_format / fayl_makro / fayl_katta / fayl_soni / fayl_bosh | 400 | §8 |

## 8. Files (per field, «maximally flexible»)
Field keys: `accept`: subset of `pdf`, `image`, `office`, `any` (`any` = all three; arbitrary binaries are never accepted);
`maxFiles` 1–20 (default 5); `maxSizeMb` 1–20 (default 20; `DocumentLimits.MaxBytes` is the hard cap); `required`;
`capture`: `environment`\|`user`\|null (web `<input capture>`; images downscaled in browser to ≤ 2400 px JPEG).
New Application port `IBlankFileInspector` (`Application/Blanks/Files`) → `BlankFileKind?`; Infrastructure
`BlankFileInspector` delegates PDF/JPEG/PNG/WebP to the existing `IFileContentInspector` (unchanged) and adds OOXML:
| Check | Rule |
|---|---|
| Container | ZIP local header `PK\x03\x04`; read central directory only; ≤ 2 000 entries; Σ uncompressed ≤ 100 MB; no entry > 50 MB (zip-bomb) |
| Type | `[Content_Types].xml` main part: `…wordprocessingml.document.main+xml` → Docx, `…spreadsheetml.sheet.main+xml` → Xlsx, `…presentationml.presentation.main+xml` → Pptx |
| Macros | any `macroEnabled` content type, `vbaProject.bin`, `.docm/.xlsm/.pptm` main type → `blanka.fayl_makro` |
| Encrypted / legacy | OLE CFB header `D0 CF 11 E0` (legacy .doc/.xls or password-protected OOXML) → `blanka.fayl_eski_format` («.docx/.xlsx sifatida saqlab yuklang») |
No antivirus exists in the stack (stance: no macros, no legacy formats, size caps, downloads as attachment, nosniff).
Office files are download-only in the web (FileViewer previews PDF/images only). Stored via `IFileStorage`; file name
sanitized; extension from the detected kind.

## 9. Template import seam (Phase 3, not built)
`IBlankTemplateDraftExtractor.ExtractAsync(Stream, fileName, ct) → (string definitionJson, string[] warnings)`.
Output always passes the §4.8 validator and lands as a **Draft** version (`source = Imported`), never published
automatically. Future endpoint `POST /api/blanks/templates/import` (`blanks.manage`).

## 10. PDF (generic renderer)
`IBlankPdfRenderer.Render(BlankPdfModel) → byte[]` (Application) · `Infrastructure/Blanks/Pdf/BlankPdfRenderer.cs`.
PDFsharp-MigraDoc **6.2.4 pinned (≥ 6.5 is AGPL)**; reuse `BundledFontResolver` and the embedded logo as-is.
Model = definition + answers + computed + steps (with signature PNG bytes) + attachment names + number + dates.
Layout = F02 visual language: A4, 1.5 cm margins, header «{organization}» / «{code} · {title} · v{version}», footer
«Sahifa X / Y»; title block (logo, title navy `#1F3A5F` 16 pt bold, «Blanka {code} · Versiya {version}», `legalStatus`
draft line); info row «№ · Sana · Xodim/Nomzod · Bo'lim»; each section = navy title + two-column label/value table
(labels `#F2F5F9` bold); choice/multi print every option ☑/☐ with extra inline; yesno/check ☑/☐; tables navy header,
fixed rows printed, ≥ 3 empty-looking rows only when the table is empty; computed values printed with «(hisoblangan)»;
files as «Ilova: name (PDF, 1,2 MB)»; final «Imzolar» table: step title · actor · decision · F.I.Sh. · date-time (Tashkent)
· signature image (in person) or «Elektron tasdiqlandi» (electronic); last line provenance «Bonvi HR · {code} №{n} ·
holat · chop etildi dd.MM.yyyy HH:mm» and `retention`. Money «3 000 000 so'm», dates «24.10.2026».

## 11. MCP / FullRead exposure (read-only)
New `api/src/Bonvi.Api/Mcp/Tools/BlankTools.cs` (`[McpServerToolType]`, auto-exposed at `/api/public/v1/full/{tool}`);
every tool `t.Require(Permissions.BlanksView, "Blankalar")`; reads via `t.Db` (read-only PG role).
| Tool | Params | Returns |
|---|---|---|
| `blank_templates` | — | code, title, subject, version, fields `[{id,label,type,required,section}]`, steps `[{id,title,actor}]` |
| `blanks` | templateCode, status, employeeId, departmentId, from, to, incomplete, overdue, page, pageSize ≤ 200 | list items (§7) without file data |
| `blank` | id | header, answers **labelled** (`[{section, field, label, value}]`; refs as names; file fields as `{ attachments: n, kinds: [...] }`), computed, missing (labels), steps (role, state, decision, comment, signer name, actedAt, method) |
| `blank_gaps` | from, to, departmentId | rule hits: hired in range without F06 · probation end ≤ today+5 without F07 · terminated in range without F08 / without Approved F09 · F07 Approved but probation InProgress past end · submissions overdue · incomplete Approved |
Migration redefines `mcp_apply_ro_grants` = the QayerdanBilgan body + `allowed` += `blank_templates`,
`blank_template_versions`, `blank_submissions`, `blank_submission_steps`, `blank_attachments`, `blank_role_assignments`
(sensitive-pattern columns stay hidden automatically); `Down` restores the QayerdanBilgan body verbatim.
Never returned: storage keys, file names, signature images, file bytes (scrubber + grants).

## 12. Registration points (the ONLY existing files edited)
| File | Change | Slice |
|---|---|---|
| `Bonvi.Domain/Identity/Permissions.cs` | 4 constants + `All` | S1 |
| `Bonvi.Domain/Identity/AccessModuleKeys.cs` | `Blanks = "blanks"` + `All` (after Recruiting) | S1 |
| `Bonvi.Domain/Identity/PermissionCatalog.cs` | module line §6.3 | S1 |
| `Bonvi.Domain/PublicApi/ApiKeyScope.cs` | `FullReadPermissions` += BlanksView | S1 |
| `Bonvi.Infrastructure/Persistence/AppDbContext.cs` | DbSets | S1 |
| `Bonvi.Infrastructure/Persistence/Auditing/AuditCatalog.cs` | §13 | S1 |
| `Bonvi.Infrastructure/Persistence/Seed/IdentitySeeder.cs` | hr_manager defaults (new DB only) | S1 |
| `Bonvi.Infrastructure/Bonvi.Infrastructure.csproj` | `<EmbeddedResource Include="Blanks/Templates/*.json" />` | S1 |
| `Bonvi.Infrastructure/DependencyInjection.cs` | `services.AddBlanksInfrastructure()` | S1 |
| `Bonvi.Application/Common/DependencyInjection.cs` | `services.AddBlanks()` | S5 |
| `Bonvi.Api/Startup/DatabaseInitializer.cs` | run `BlankTemplateSeeder` after migrations | S3 |
| `Bonvi.Api/Program.cs` | `app.MapBlankEndpoints();` | S5 |
| `web/src/app/layout/nav-items.ts` | own section, item `blanks` → `/blankalar`, `FileText`, `permission: 'blanks.view'`, warning count = `waitingForMe` total | S9 |
| `web/src/features/roles/lib/role-keys.ts` | `'blanks'` module, `'blanks.approve'` action | S9 |
| `web/src/shared/i18n/locales/*.json` (4) | `nav.blanks`, `roles.module.blanks`, `blanks.*`, `errors.blanka.*` | S9 (+ Edit-only additions §14) |
| `web/src/app/routes/_app/xodimlar/$employeeId.tsx` | tab `blanks` (if `blanks.view`) → `<SubjectBlanksTab employeeId>` | S12 |

## 13. Audit
`Audit<BlankTemplate>`, `Audit<BlankTemplateVersion>` (exclude Definition, DefinitionSha256), `Audit<BlankSubmission>`
(module `blanks`; `.Subject(EmployeeId)`; Ref Employee/Candidate/Department; exclude Answers, Computed, Missing,
MissingCount, Version, ClientRequestId, FinalPdfStorageKey), `Audit<BlankSubmissionStep>` (exclude SignatureStorageKey,
AnswersPatch), `Audit<BlankAttachment>` (exclude StorageKey, ContentSha256), `Audit<BlankRoleAssignment>`
(Ref User). `NotAudited`: `BlankNumberCounter` («technical counter»). Answers are kept out of the journal (sensitive:
discipline, exit reasons); the per-step `answers_patch` is the legal trail. `AuditCatalogCoverageTests` must pass.

## 14. Build plan (slices for subagents)

Rules for every slice: mirror the reference files in the header; 40–60 tool calls; report files changed + test result +
what remains. Locale files: only S9 adds the key set; later slices add missing keys with **Edit** (never Write) under
their own sub-namespace (`blanks.list.*`, `blanks.form.*`, `blanks.view.*`). Shared files (§12, the migration, `Program.cs`,
`DatabaseInitializer.cs`, `nav-items.ts`) are edited only by the slice named there — **sequential, never parallel**.

| Slice | Content | Files (new unless marked) | Depends | Parallel |
|---|---|---|---|---|
| **S1** Domain + persistence | entities §3 (`Bonvi.Domain/Blanks/*.cs`: BlankTemplate, BlankTemplateVersion, BlankSubmission (+Step, +Attachment children), BlankRoleAssignment, BlankNumberCounter, enums, `BlankErrors`), repositories interfaces `Bonvi.Domain/Abstractions/IBlankRepositories.cs`, EF configs `Persistence/Configurations/BlankConfigurations.cs`, repos `Repositories/BlankRepositories.cs`, `Bonvi.Infrastructure/Blanks/BlanksInfrastructureRegistration.cs`, migration `./bonvi migrate Blankalar` (+ grants function §11; read it, drop spurious DROPs), §12 rows marked S1; Domain unit tests (status transitions, version bump) | §3, §12 | — | with S2, S4 |
| **S2** Definition engine | `Bonvi.Application/Blanks/Definitions/`: model + parser, `BlankDefinitionValidator` §4.8, `BlankConditions`, `BlankComputedEvaluator`, `BlankAnswersValidator` (types, limits, required per section set, drops hidden), `BlankCompleteness`, `IBlankPrefillSource` port; vectors JSON + unit tests `api/tests/Bonvi.Application.Tests/Blanks/*` | pure Application | — | with S1, S4 |
| **S4** File inspector | `Application/Blanks/Files/IBlankFileInspector.cs`, `BlankFileKind`, `Infrastructure/Blanks/BlankFileInspector.cs` (registered in `BlanksInfrastructureRegistration` — create it if S1 has not yet; coordinate: S1 owns the DI line in `DependencyInjection.cs`), tests with generated docx/xlsx/docm/encrypted/zip-bomb samples | §8 | — | with S1, S2 |
| **S3** Templates + seeder | `Infrastructure/Blanks/Templates/HR-F06.json, HR-F07.json, HR-F08.json, HR-F09.json, HR-F13.json` (§4.10, labels verbatim from INTAKE), `Infrastructure/Blanks/BlankTemplateSeeder.cs` (create / add version + retire old / same version different hash → log error, no overwrite), `DatabaseInitializer.cs` line; test: every file passes the validator | §4 | S1, S2 | — |
| **S5** Submissions API | `Application/Blanks/BlanksRegistration.cs` (calls `AddBlankSteps()`/`AddBlankFiles()` stubs in `BlankStepsRegistration.cs`, `BlankFilesRegistration.cs`), `Application/Blanks/Submissions/*` (create, save, submit, cancel, list, get), `Application/Blanks/BlankActorResolver.cs`, prefill impl `Infrastructure/Blanks/BlankPrefillSource.cs`, read model `Persistence/ReadModels/BlankReadModel.cs`, `Api/Endpoints/BlankEndpoints.cs` (aggregator: templates/roles/submissions + calls `MapBlankStepEndpoints`, `MapBlankFileEndpoints` from stub files `BlankStepEndpoints.cs`, `BlankFileEndpoints.cs`), endpoints #1–#11, `Program.cs` line; integration tests `BlankSubmissionApiTests.cs` | §5, §7 | S1, S2, S3 | — |
| **S6** Steps API | `Application/Blanks/Steps/*` (approve, sign-in-person incl. PNG re-draw via existing image processor, return, reject, signature read), fill `BlankStepEndpoints.cs` (#12–#16), `BlankStepsRegistration.cs`; tests `BlankStepApiTests.cs` (F07 full chain, parallel group, Skipped `when`, person-role electronic via `users.employee_id`, assignment rule) | §5, §6 | S5 | with S7, S8 |
| **S7** Files + PDF API | `Application/Blanks/Files/*` (upload/remove/download), `Application/Blanks/Pdf/IBlankPdfRenderer.cs`, `Infrastructure/Blanks/Pdf/BlankPdfRenderer.cs`, final-PDF hook on Approved (event handler in `Application/Blanks/Pdf`), fill `BlankFileEndpoints.cs` (#17–#20), `BlankFilesRegistration.cs`; tests `BlankFileApiTests.cs`, `BlankPdfRendererTests.cs` (each wave-1 template renders a valid multi-page PDF, full + minimal) | §8, §10 | S5, S4 | with S6, S8 |
| **S8** MCP tools | `Api/Mcp/Tools/BlankTools.cs` §11; tests `BlankMcpToolsTests.cs` (+ leak test: no storage key / file name / signature in output; FullRead key reads `blanks`; without `blanks.view` → denied) | §11 | S5 (data), S1 (grants) | with S6, S7 |
| **S9** Web foundation | `./bonvi gen`; `web/src/features/blanks/api/{blanks-api.ts,blanks-queries.ts}`, `lib/{definition-types.ts,conditions.ts,computed.ts,answers-zod.ts,blank-error.ts,blank-presentation.ts}` + vitest using the shared vectors; routes `app/routes/_app/blankalar/index.tsx`, `app/routes/_app/blankalar/$submissionId.tsx` (shells); §12 web rows except the employee tab; all i18n keys | §4, §7, §12 | S6, S7 | — |
| **S10** List page | `components/blanks-page.tsx` (filters: template, status, department, employee, from/to, «Menga kutilmoqda», «To'liq emas», «Muddati o'tgan»; one-line rows per project table rule; pagination 20/50/100/Barchasi), `blank-create-dialog.tsx` (template + subject picker → POST → navigate), `blank-roles-dialog.tsx` (`blanks.manage`) | §7 | S9 | with S11 |
| **S11** Fill renderer | `components/renderer/{blank-form.tsx, section-view.tsx, field-input.tsx, choice-field.tsx, table-field.tsx, file-field.tsx, computed-field.tsx, ref-field.tsx}`, `blank-fill-page.tsx`: renders editable sections, autosave (PUT, 2 s debounce, `expectedVersion`, 409 → reload banner), inline errors from zod + server `detail` path, submit | §4, §5, §8 | S9 | with S10 |
| **S12** View + steps + card tab | `components/{blank-view-page.tsx, blank-steps-panel.tsx, step-approve-dialog.tsx, step-sign-dialog.tsx (imports `@/features/kiosk/components/signature-pad`), subject-blanks-tab.tsx}`; route `$submissionId` chooses fill vs view; PDF button; attachments via `FileViewer`; employee card tab (§12) | §5, §6 | S11 | — |
| **S13** Wiring/QA | `./bonvi check`, web build, full `./bonvi test`, `upgrade head` on a prod-copy DB, fix gaps, update `docs/PENDING_WIRING.md` if anything is deferred | — | all | — |

Parallel waves: **[S1 ∥ S2 ∥ S4] → S3 → S5 → [S6 ∥ S7 ∥ S8] → S9 → [S10 ∥ S11] → S12 → S13**.
After S1: `test-migration` (model changed). After S8: `test-backend` RBAC sweep. Before deploy: `qa-security`.

**Phase 2** (new briefs, same engine): F03 + F05 (candidate subject; «Blankalar» card on the candidate card via
`candidate-card-page.tsx` one-line hook, CandidateFormsCard pattern), F11, F12, F14, F19; Excel export
(`/submissions/export`, `blanks.view` + `reports.export`, VacancyRequest export pattern).
**Phase 3**: builder UI + publish/retire endpoints, import seam §9, employee-app channel §6.4, F10, F04, F15–F18; F01 per Q1.

## 15. Tests (Definition of Done)
- [ ] Unit (Application): definition validator (each rejection in §4.8), conditions, computed vectors (C# and TS both green), answers validator per type incl. option extra, table min/max/fixedRows, hidden-field drop, completeness.
- [ ] Unit (Domain): every transition in §5 incl. forbidden ones → correct error; version increments.
- [ ] Unit (Infra): file inspector matrix (pdf, jpeg, png, webp, docx, xlsx, pptx accepted; docm, xlsm, encrypted, .doc, zip-bomb, renamed exe rejected); PDF renders for all 5 templates.
- [ ] Integration: **every endpoint #1–#20 → 401 without token, 403 without its permission, 2xx with it**; create idempotency; save rejects non-editable section (409); submit numbers are sequential per template and unique under two concurrent submits; F07 chain to Approved incl. employee in-person signature and HR-1 decision; return → Draft keeps number; reject; cancel rules; stale `expectedVersion` → 409; attachment upload limits/count/kind per field; download headers; MCP tools + FullRead + leak test; `AuditCatalogCoverageTests`; migration up/down on a DB with data; grants: `bonvi_mcp` can select `blank_submissions.answers` and cannot select `storage_key`/`file_name`/`signature_storage_key`.
- [ ] `./bonvi check` (4 locales + every `blanka.*` code translated), web build, full test suite green; existing tests untouched.

## 16. Owner questions (everything else is decided and logged)
1. **F01 vs «Vakansiya so'rovlari».** Recommendation: F01 is NOT a blank; extend the existing vacancy request later
   (it already has the approval flow). Until answered, F01 stays deferred.
2. **Lawyer-pending blanks** (F13 now; F03, F12, F17, F19 later). Recommendation: publish with `legalStatus: draft` (PDF says
   «Loyiha»), switch to `final` by a new version once the lawyer signs off.
3. **Legal weight of online signatures.** Recommendation: the online blank is the working record; for F09 and F13 HR also
   prints the PDF and collects wet signatures for the personal file until the lawyer says the online record suffices.
4. **Heads, mentors, employees signing.** Phase 1: they sign in person on the HR device, or electronically if they have an
   HR-panel login linked to their employee card. The employee-app (PWA) channel is Phase 3. Confirm this order.
5. **Who holds HR-1…HR-4, payroll accountant, chief accountant, director** — not blocking: the owner fills «Blankalar →
   Rollar»; until then any `blanks.approve` holder may sign those steps.
