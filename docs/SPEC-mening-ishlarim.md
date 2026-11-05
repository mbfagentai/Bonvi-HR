# SPEC — «Mening ishlarim»: process engine over HR-F01…F19 and HR-R01…R08

> Status: authoritative for build. Written 2026-11-04. Inputs: owner decisions 2026-11-04 (§1.3),
> `data/hr-paket/namuna-shef-spec.json` (the chef's prototype — steps, guide lines, rules Q01…AKT),
> `docs/INTAKE-hr-blankalar.md` §3–§4 (who fills/approves, calendars), `docs/SPEC-blankalar.md` (blank engine,
> wave 1 in code), `data/hr-paket/HR-R03_Moslashtirish_va_sinov.txt` (probation timeline).
> Decisions taken here are logged in `docs/ASSUMPTIONS.md` (2026-11-04). Owner questions: §18.
>
> **Reference implementations** (copy their shape, do not edit them):
> versioned JSON templates + seeder → `api/src/Bonvi.Infrastructure/Blanks/BlankTemplateSeeder.cs`, `Blanks/Templates/*.json`;
> aggregate + status + errors → `api/src/Bonvi.Domain/Recruiting/VacancyRequest.cs`;
> optimistic `Version` → `api/src/Bonvi.Domain/Attendance/AbsenceRequest.cs`;
> polling job (idempotent, overlap-safe) → `api/src/Bonvi.Application/Attendance/Recompute/AttendanceScheduledJobs.cs`
> (`AttendanceEmploymentChangesJob`); guard called from a command → `EmployeeFlagGuard` usage in
> `api/src/Bonvi.Application/Employees/Employments/TerminateEmploymentCommand.cs`;
> computed-on-read list → `api/src/Bonvi.Application/Notifications/NotificationCollector.cs`;
> endpoints → `api/src/Bonvi.Api/Endpoints/BlankEndpoints.cs`; MCP tool → `api/src/Bonvi.Api/Mcp/Tools/BlankTools.cs`;
> grants migration → `api/src/Bonvi.Infrastructure/Persistence/Migrations/20261103111355_Blankalar.cs`;
> web feature → `web/src/features/blanks/{api,components,lib}`; list page → `web/src/features/blanks/components/blanks-page.tsx`.

## 1. Goal, scope, non-goals

### 1.1 Goal
«The system leads the person.» A new HR employee opens the app and sees, in order: what to do today with
step-by-step instructions, who holds every case and at which stage, and what is late. A new menu item
«Mening ishlarim» with three tabs — «Mening ishlarim», «Jarayonlar», «Kim kechikdi» — is the landing page for HR
users after login. All 19 blanks HR-F01…F19 and regulations HR-R01…R08 live inside processes.

### 1.2 Scope by phase
| Phase | Content |
|---|---|
| 0 (exists) | Blanks wave 1 (F06 F07 F08 F09 F13) — code done, prod deploy is the owner's call; F02 = `CandidateForm` (planshet anketa) |
| **1 (this SPEC, full detail)** | Engine (templates, cases, steps, reconciler, gates, overrides), three tabs + case detail + settings page, flows 1–4 (§3.2–§3.6), F01 = link to `VacancyRequest`, MCP tools, «Tartib» cards. F03 F04 F05 F11 are **made in HRMentor** (§1.5); until they are served, their steps close by the manual fallback |
| 2 | Monthly close (F18 auto-filled from attendance, F12 flow on `Incentive`, F15 auto report), flow 6 discipline, Telegram reminders + «Eslatish», `process_reminders` |
| 3 | F10 F14 F19 F16 F17, flows 5/7/8, D2 (employee one-time code / kiosk PIN) and D3 (E-IMZO), guided tour + glossary, transfer gate (§18 Q7), position profiles |

### 1.3 Owner decisions (binding, 2026-11-04)
1. Process engine: steps close themselves from facts in existing modules and blanks; a manual «Bajarildi» exists only for
   work outside the system (mehnat.uz, accountant, E-IMZO, paper personal file, outside phone/posting work).
2. **Additive.** Existing internals are not rewritten. Every existing file that is edited is listed in §13 with the exact change.
3. Gates are **active** (they block), but only through the hook points of §13; HR-1 can override with a reason.
4. HR-1…HR-4 → user mapping is done later by the owner, reusing `blank_role_assignments`.
5. F01 and F12 are not duplicated: the steps link the existing `VacancyRequest` / `Incentive`.
6. iOS HIG design; the chef's prototype layout (§11).

### 1.4 Non-goals (Phase 1)
- No domain events, no dispatcher, no edits to aggregates (§7.1 explains why).
- The engine never writes into recruiting, employees, attendance or blanks tables. It only reads them. Users act through
  the existing screens/APIs, reached by the «Boshlash» deep link. (The step panel may call the existing blanks API
  `POST /api/blanks/submissions` on the user's click — that is a user action with the user's permission, not an engine write.)
- No employee-app channel, no D2/D3 signatures, no Telegram (Phase 2/3).
- No visual process builder: templates are JSON files in the repo, versioned and seeded like blank templates.

### 1.5 HRMentor is the only source of documents (owner 2026-11-01, CEO D6 2026-11-04 — binding)
`docs/SPEC-hujjat-manbai.md`: no blank or regulation is authored in this repository; the built-in blank definitions
F06 F07 F08 F09 F13 are frozen debt D6 and `HujjatManbaiQulfiTests` fails on any new `Blanks/Templates/*.json`.
Consequences for this SPEC (owner, 2026-11-04):
- New blanks (F03 F04 F05 F11 in Phase 1, all later ones) are created in HRMentor and reach the blank engine
  through the engine owner's HRMentor work. This SPEC does **not** author them (§5.2 is the contract the HRMentor
  definitions must keep so the facts can read them).
- Until a blank is served, every step that reads it has a **manual fallback** (`manual.allowed`, inputs below) and
  the fact also accepts the served blank once it exists — no code change at switch-over. Steps: r02-8 (F03: input
  `outcome` choice `ok`/`watch`/`notRecommended`), r02-9 (F04 list: no input), r02-10 (F04 decision: input
  `decision` choice `accept`/`conditional`/`reserve`/`reject`, `councilDate` date), r02-11 (F05 offer: input
  `startDate` date when accept), r02-12 (no input), r01-2 (input `docs` checklist, as direct hire), r02-13 (F11:
  no input). Gate Q01 reads the manual r02-10 decision the same as an F04 decision.
- Process definitions (steps, roles, due rules, gates) are system logic, not documents: they stay in code
  (`Processes/Templates/*.json`), like the §18 catalog in SPEC-hujjat-manbai S6. The «Nima qilaman?» guide lines
  are regulation text: Phase 1 ships the chef's prototype lines as a temporary default; serving them from HRMentor
  is a Phase 2 item (same pattern as HR-F02 texts, `docs/SPEC-blankalar-hrmentor.md`).

## 2. Overlap with existing data (LINK, never duplicate or sync)

| Process need | Source of truth (read only) | Never |
|---|---|---|
| F01 Bo'sh o'rin so'rovi | `vacancy_requests` (1 Pending 2 Approved 3 Rejected 4 Cancelled; `decided_at`, `vacancy_id`, `department_id`, `openings`) | an F01 blank |
| F02 Nomzod anketasi | `candidate_forms` (`candidate_id`, `number`, `submitted_at`) | an F02 blank |
| Candidate pipeline | `candidates` (status 1 InProgress 2 Rejected 3 Hired 4 Withdrawn 5 Reserve 6 Blacklisted; `vacancy_id`, `created_at`, `reapplied_at`, `deleted_at`), `candidate_stage_entries` (`stage_id`, `recorded_at`), `pipeline_stages` (HR-editable) | binding a step to a stage **name** |
| Hire / registry | `employments` (1 Probation 2 Active 3 Terminated; `hire_date`, `hire_order_no`, `hire_order_date`, `termination_date`), `employment_events` (type 1 Hire … 7 Termination, 8 Rehire; `candidate_id`, `recorded_at`) | a process copy of the hire |
| Probation | `probations` (1 InProgress 2 Passed 3 Failed 4 LeftEarly 5 Cancelled; `start_date`, `end_date`, `resolved_at`) | auto-passing (removed 2026-10-30) |
| Blanks | `blank_submissions`, `blank_submission_steps` (status, step state, `acted_at`, `answers`) | hooking `IBlankApprovedHook` (single registration used by `BlankFinalPdfApprovedHook`) |
| Attendance enrolment | `attendance_identities` (`employee_id`), `device_enrollments` (status 1 Synced …), `employees.photo_key`, `schedule_assignments` | copying identities |
| Exit interview | **F08 blank** (K-code). `exit_interviews` («Qayta aloqa», free-text reason, no K-code) is shown as info only | treating `exit_interviews` as Q13 evidence |
| Notifications | `NotificationCollector.CollectAsync` (computed on read) | storing notifications |
| Role → user mapping | `blank_role_assignments` (+ new office roles `safetyEngineer`, `council`) | a second role table |

## 3. Process catalogue — flows 1–4 in full

### 3.1 Template definition (JSON, `schema: 1`)
File per process: `api/src/Bonvi.Infrastructure/Processes/Templates/<code>.json`, embedded resource, seeded by
`ProcessTemplateSeeder` exactly like `BlankTemplateSeeder` (create / add version + retire old / same version different
SHA-256 → log error, no overwrite).

Top level: `{ "schema": 1, "code", "version": "1.0", "title", "source": "HR-R02", "subjectKind": "vacancyRequest"|"candidate"|"employment", "board": [{ "key", "title" }], "steps": Step[] }`.

Step:
```
{ "id": "r02-3", "title": "So'rovni tasdiqlang yoki rad eting", "column": "approve",
  "role": "hr1", "fallbackRole": null, "blank": "HR-F01"|null, "signature": "D1"|"D2"|"D3"|null,
  "kind": "fact"|"manual"|"auto", "prereqs": ["r02-2"], "optional": false,
  "when": { "fact": "<factName>", "equals": <value> } | null,
  "due": { "from": "<anchor>", "workingDays": 2 } | { "from": "<anchor>", "days": 7 } | { "rule": "nextCouncilDay", "from": "<anchor>" } | null,
  "gate": { "code": "Q04", "mode": "block"|"warn" } | null,
  "completion": { "fact": "<factName>", "args": { … } } | null,        // fact/auto steps
  "manual": { "allowed": true, "inputs": [ManualInput] } | null,      // manual steps, or fact steps with a manual fallback
  "skippable": false, "legacySkippable": false,
  "guide": ["…", "…"], "sourceRef": "HR-R02, 3-bo'lim, 3-qadam",
  "link": { "screen": "<screen>", "args": { … } } }
```
- `kind: fact` closes when its completion fact holds; `manual` closes only by a user «Bajarildi»; `auto` = done by the
  system from a fact, never shown as a task (holder «Tizim»), no due. A `fact` step with `manual.allowed` closes on
  whichever comes first.
- `role` / `fallbackRole`: §9.1 codes. `fallbackRole` holds the step when a person role cannot be resolved to a user.
- `when`: false → step `Skipped` (resolution `condition`); unknown (fact not yet known) → step stays `Locked`.
- `optional: true` → never locks later steps; auto-skipped (resolution `caseEnded`) when the case ends.
- `ManualInput`: `{ "id", "label", "type": "text"|"longtext"|"date"|"int"|"choice"|"checklist", "required", "options"?: [{value,label}], "min"?, "max"? }`.
  `checklist` = fixed items `options`, each must be ticked or marked «yo'q / kerak emas» when `required`.
- Anchors (`due.from`): `case.startedOn`, `case.anchorDate`, `step:<id>.doneOn`, `candidate.appliedOn`,
  `employment.hireDate`, `employment.hireOrderDate` (falls back to `hireDate`), `employment.terminationDate`,
  `probation.startDate`, `probation.endDate`. Anchor unknown → due null (shown «muddatsiz»).
- `nextCouncilDay`: the first date strictly after the anchor whose ISO weekday = setting `processes.councilWeekday`
  (default 4 Thursday) and which is a working day; if that Thursday is a holiday → the next working day.
- `days` = calendar days; `workingDays` = §6.10 calendar (Mon–Fri minus `process_holidays`, plus working Saturdays).
  Negative values allowed (`-5` = 5 working days before).
- Due floor: for non-legacy cases `due = max(rule, case.startedOn)` — a case never starts already late.
- `link.screen` values (§11.6): `vacancyRequests`, `candidate`, `candidateForm`, `blank` (create-or-open, args
  `{ template, subject }`), `employee`, `employment`, `probations`, `attendanceMapping`, `attendanceSchedules`,
  `external` (args `{ url }`), `none`.
- Validator (`ProcessDefinitionValidator`, run by the seeder; invalid file → startup error in Development, logged +
  template skipped in Production): unknown keys/kinds/roles/facts/anchors/screens; duplicate ids; prereq cycles;
  prereq/`step:` anchor pointing to an unknown id; `manual` step without `manual.allowed`; `auto` step with `due`,
  `manual` or `gate`; `gate.code` not in §8.1; blank code not `HR-F01…F19`.
- **Guide lines** (`guide`): verbatim from the prototype `processes[].steps[id].guide` for every prototype id, except the
  lines marked † in the tables below, which are replaced by the text given there. New ids carry the text given here.

### 3.2 Process codes (Phase 1)
| Code | Title (UI) | Source | Subject | Starter (fact scan, §7.3) | `subject_ref` |
|---|---|---|---|---|---|
| `hire-request` | «Bo'sh o'rin so'rovi» | HR-R02 §3 steps 1–4, 14 | vacancy request | `vacancy_requests.created_at ≥ startedOn` (any status) | `vr:{id}` |
| `hire-candidate` | «Nomzod tanlovi» | HR-R02 §3 steps 5–13 | candidate | `candidates.status = 1`, `deleted_at IS NULL`, `appliedOn ≥ startedOn` where appliedOn = Tashkent date of `coalesce(reapplied_at, created_at)` | `c:{id}:{appliedOn yyyyMMdd}` |
| `hire-formalise` | «Ishga qabulni rasmiylashtirish» | HR-R01 §2.1 | candidate → employment | (a) step r02-11 Done with branch `accept` in a `hire-candidate` case (child, `parent_case_id`); (b) `employment_events` type 1/8 with `recorded_at ≥ startedOn` whose employment has no `hire-formalise` case (direct hire) | (a) `hf:c:{candidateId}:{appliedOn}` (b) `hf:e:{employmentId}` |
| `probation` | «Moslashtirish va sinov muddati» | HR-R03 | employment (+probation) | `probations.status = 1`, `start_date ≥ startedOn` | `p:{probationId}` |
| `dismissal` | «Bo'shatish va ishlarni topshirish» | HR-R01 §2.4 + HR-R06 §4 | employment | (a) manual start (`POST /cases`); (b) a non-Cancelled F08 or F09 (`answers.kind = 'dismissal'`) created `≥ startedOn` for an employment with status 1/2; (c) `employments.status = 3` with `termination_date ≥ startedOn` and no case (late case) | `d:{employmentId}` |

`hire-candidate.parent_case_id` = the Active `hire-request` case whose request has `vacancy_id = candidates.vacancy_id`
(latest `created_at` if several); none → null. `hire-formalise` (b) gets `employment_id`; (a) gets it when the
`cand.hired` fact appears (reconciler fills `employment_id`, `employee_id`). Rule order inside one pass: (a) attaches
employments **before** (b) scans, so a candidate hire never yields two formalise cases; plus partial UNIQUE
(process_code, employment_id) WHERE employment_id IS NOT NULL.

### 3.3 Flow 1a — `hire-request` (board columns: `request` «So'rov», `approve` «Tasdiq», `search` «Qidiruv», `closed` «Yopildi»)
| Id | Title (verb-first) | Role | Blank / record | Sig | Kind | Prereqs | Due | Gate | Closes when (fact) | Link |
|---|---|---|---|---|---|---|---|---|---|---|
| r02-1 | «Bo'sh o'rin so'rovini yuboring» | departmentHead (fallback hr4) | `vacancy_requests` | D1 | fact | — | — | — | `vr.created` (always true at start) | vacancyRequests |
| r02-2 | «So'rovni jurnalga kiritish» | system | `vacancy_requests` | — | auto | r02-1 | — | — | `vr.created` | none |
| r02-3 | «So'rovni tasdiqlang yoki rad eting» | hr1 | `vacancy_requests` | D1 | fact | r02-2 | `case.startedOn` +2 wd | — | `vr.decided` = approved (rejected/cancelled → case ends §7.5) | vacancyRequests |
| r02-4 | «Nomzod qidiring va e'lon joylang» | hr4 | — | — | fact + manual | r02-3 | `step:r02-3.doneOn` +1 wd | — | `vr.firstCandidate` OR manual (input `channels` text required «Qayerga joylandi») | vacancyRequests |
| r02-14 | «So'rovni yopish» | system | `vacancy_requests` | — | auto | r02-3 | — | — | `vr.filled`; stores `daysToClose` = done − request created (calendar) | none |

Guide †: r02-1 line 1 → «Bo'lim rahbari «Vakansiya so'rovlari» orqali so'rov yuboradi: lavozim, o'rinlar soni, sabab va nomzodga talablar.» (no salary, §18 Q3); r02-1 line 2 and r02-2 line 2 (F10) are **dropped** until Phase 3 (contradiction C3); r02-3 line 2 → «Yangi lavozim ochilsa, bosh buxgalter shtat jadvaliga mosligini tasdiqlaydi (hozircha tizimdan tashqarida).»; r02-4 line 3 → «E'lon joylangan sana va manbalarni shu qadamda «Bajarildi» bilan yozing.»; r02-14 line 2 kept as text only (norm colouring is Phase 3, needs position profiles).

### 3.4 Flow 1b — `hire-candidate` (columns: `contact` «Bog'lanish», `form` «Anketa», `council` «Kengash», `check` «Surishtiruv», `offer` «Taklif», `firstDay` «Ishga chiqish»)
`appliedOn` = Tashkent date of `coalesce(reapplied_at, created_at)`; `appliedAt` = that timestamp.
| Id | Title | Role | Blank / record | Sig | Kind | Prereqs | Due | Gate | Closes when (fact) | Link |
|---|---|---|---|---|---|---|---|---|---|---|
| r02-5 | «Nomzodga qo'ng'iroq qiling va ofisga taklif qiling» | hr4 | `candidates` | — | fact + manual | — | `candidate.appliedOn` +3 wd | — | `cand.contacted` OR manual (input `result` choice: `invited` «Ofisga taklif qilindi», `noAnswer` «Javob bermadi», `notSuitable` «Mos emas») | candidate |
| r02-6 | «Anketa oling va dastlabki suhbat o'tkazing» | hr4 | HR-F02 (`candidate_forms`) | D2→in person | fact | r02-5 | `candidate.appliedOn` +3 wd | — | `cand.f02` | candidateForm |
| r02-7 | «Amaliy sinovni tashkil qiling (ishchi kasblar)» | hr4 | HR-F02 §13 | D1 | fact + manual | r02-6 | `step:r02-6.doneOn` +2 wd | — | `cand.stage:practicalTest` OR manual (inputs `score` int 1–5 required, `comment` text) | candidate |
| r02-9 | «Nomzodni Kengashga tayyorlang (HR-F04 ro'yxatiga kiriting)» | hr4 | HR-F04 | — | fact | r02-6 | nextCouncilDay from `step:r02-6.doneOn` | — | `cand.inF04`; legacy cases also `cand.stage:invited` | blank `{HR-F04}` |
| r02-10 | «Kengash qaror qabul qiladi» | council (fallback hr1) | HR-F04 | D1 | fact | r02-9 | = r02-9 due | — | `cand.f04Decision` (value `accept`/`conditional`/`reserve`/`reject`); legacy cases also `cand.stage:council` (value `unknown` → treated as `accept` for branching, HR decides) | blank (the F04 holding the candidate) |
| r02-8 | «Surishtiruv o'tkazing (Kengashdan keyin, rozilik bilan)» | hr4 | HR-F03 | D1 | fact | r02-10 | `step:r02-10.doneOn` +3 wd | — | `cand.f03` (outcome `ok`/`watch`/`notRecommended`) | blank `{HR-F03, candidate}` |
| r02-11 | «Natijani nomzodga yetkazing va taklif bering (HR-F05)» | hr4 | HR-F05 | D2→in person | fact | r02-10, r02-8 | `step:r02-8.doneOn` +1 wd, or `step:r02-10.doneOn` +1 wd when r02-8 Skipped | — | branch **accept** (decision accept, or conditional + F03 ok/watch): `cand.f05Step:offerCandidate`; branch **decline** (reserve, reject, F03 notRecommended): `cand.status` ∈ {2 Rejected, 5 Reserve} | blank `{HR-F05, candidate}` / candidate |
| r02-12 | «Hujjatlarni HR-2 ga topshiring» | hr4 | HR-F05 | D1 | fact | r02-11 | `f05.startDate` −1 day (F05 answer `startDate`); unknown → `step:r02-11.doneOn` +3 wd | — | `cand.f05Step:docsHandover` | blank (the F05) |
| r02-13 | «Yangi xodimni kutib oling va HR-F11 da imzo oling» | hr4 | HR-F11 | D2→in person | fact | r02-12 | `employment.hireDate` (unknown → `f05.startDate`) | **Q01 block**, **Q04 block** (engine, §8.3) | `emp.f11` | blank `{HR-F11, employee}` |

Conditions (`when`): r02-7 — `optional: true` + `skippable: true` (HR clicks «Kerak emas» for non-worker positions; there
is no position on a candidate — vacancies carry only a title); r02-8 — `cand.f04Decision = conditional` (accept → Skipped; reserve/reject → case
ends); r02-12, r02-13 — branch accept only (decline → Skipped, case Completed with outcome).
Guide †: r02-6 line 1 → «Nomzod ofisda planshetda anketaning A qismini o'zi to'ldiradi va oxirida imzo qo'yadi.»
(PIN is D2, Phase 3, C7); r02-6 line 2 → «Planshet anketasidagi «Rasmga olish» bilan nomzodni rasmga oling — shu rasm
xodim kartasiga va davomat terminaliga o'tadi.»; r02-7 line 2 → «Usta bergan 1–5 bahoni va izohni shu qadamda
«Bajarildi» bilan yozing.»; r02-10 line 4 → «Bayonnomani rais va kotib (HR-1) imzolaydi. Kengashdan keyin nomzod
kartasida «Кенгаш» bosqichini ham belgilang.» (§18 Q2); r02-11 add line 4 «Rad etilgan yoki zaxiraga olingan nomzodning
holatini nomzod kartasida o'zgartiring — jarayon shundan keyin yopiladi.»; r02-13 line 3 → «Anketa, Kengash qarori,
buyruq va mehnat muhofazasi yo'riqnomasi bo'lmasa xodim ishga chiqarilmaydi.»

### 3.5 Flow 2 — `hire-formalise` (columns: `docs` «Hujjatlar», `order` «Shartnoma va buyruq», `firstDay` «Birinchi kun», `after` «Keyingi 5 kun»)
`case.anchorDate` = planned start date: F05 `startDate` for (a), `employment.hireDate` for (b).
| Id | Title | Role | Blank / record | Sig | Kind | Prereqs | Due | Gate | Closes when (fact) | Link |
|---|---|---|---|---|---|---|---|---|---|---|
| r01-2 | «Hujjatlar to'liqligini tekshiring (HR-F05, 2-qism)» | hr2 | HR-F05 | D1 | fact | — | `case.anchorDate` −1 wd | — | `cand.f05Step:docsHr2`; direct hire (b): manual (input `docs` checklist = 12 F05 rows, required) | blank (the F05) / none |
| r01-3 | «Mehnat shartnomasini tayyorlang va imzolating» | hr2 | — (paper / E-IMZO) | D3 | manual | r01-2 | `case.anchorDate` | — | manual (inputs `contractNo` text, `term` choice `permanent` «muddatsiz» / `fixed` «muddatli» required, `basis` text required when fixed) | none |
| r01-4 | «Ishga qabul buyrug'ini direktorga imzolating» | hr2 | `employments.hire_order_no` | D3 | fact + manual | r01-3 | `case.anchorDate` | — | `emp.hireOrder`; manual fallback (input `orderNo` text required) | employment |
| r01-5 | «Xodimlar ro'yxatiga (reestrga) kiritish» | system | `employments` | — | auto | — | — | — | `emp.hired` | none |
| mm | «Mehnat muhofazasi yo'riqnomasini o'tkazing» | safetyEngineer (fallback hr2) | — (replaces paper journal) | D1 | manual | r01-5 | `employment.hireDate` (unknown → anchor) | — | manual (input `topics` checklist «stanok», «gaz», «elektr», «yong'in», required) | none |
| r01-8a | «Xodimni davomat tizimiga kiriting (Face ID)» | hr3 | `attendance_identities` / `device_enrollments` | — | fact | r01-5 | `employment.hireDate` | Q02 warn | `emp.attendance` | attendanceMapping |
| r01-6 | «Xodimni mehnat.uz ga kiriting» | hr2 | — (outside) | — | manual | r01-4 | `employment.hireOrderDate` +3 wd | Q05 warn | manual (input `enteredOn` date required) | external `https://mehnat.uz` |
| r01-7 | «Shaxsiy ishni oching va to'liq yig'ing» | hr2 | — (paper) | D1 | manual | r01-5 | `employment.hireDate` +5 wd | Q06 warn | manual (input `items` checklist = Q06 mandatory list: «Mehnat shartnomasi», «Ishga qabul buyrug'i», «Anketa HR-F02», «Hujjat nusxalari (HR-F05, 2-qism)», «HR-F11 tanishuv varag'i», «Tibbiy ko'rik xulosasi (tashxissiz)», «Mehnat muhofazasi belgisi», required) | employee |
| r01-9 | «Buyruq va oylik ma'lumotini hisobchiga bering» | hr2 | HR-F18 (Phase 2) | D1 | manual | r01-4 | `employment.hireOrderDate` | **Q03 block** (engine) | manual (input `payment` choice `card` «Bank kartasi rekvizitlari berildi» / `cash` «Karta yo'q, naqd», required) | employee |

Prototype ordering kept for display (`order_no` = table order). Guide †: r01-5 line 1 → «Tizim o'zi bajaradi: ishga
qabul «Buyruqlar» bo'limida yozilgach xodim ro'yxatga qo'shiladi.»; mm line 3 → «Xodimning o'z tasdig'i (kod bilan)
keyinroq qo'shiladi.» (D2, Phase 3); r01-8a line 1 → «Xodim ishga chiqqan kuni uni davomat terminaliga qo'shing —
yuz rasmi xodim kartasidagi rasmdan olinadi.»; r01-9 line 1 → «Ishga qabul buyrug'i nusxasini ish haqi hisobchisiga
bering va shu qadamda karta yoki naqd ekanini belgilang.» (F18 is Phase 2). The F05 «3-qism» (first-day checklist) is
**this case** — the F05 template does not repeat it (§5.2).

### 3.6 Flow 3 — `probation` (columns: `start` «Boshlanish», `d7` «7–14 kun», `d30` «30–60 kun», `final` «Yakuniy baho», `decision` «Qaror»)
Offsets from `probation.startDate` (calendar days, as R03 and F06); 85/88/90 from `probation.endDate` (positions may
have a custom length). A step whose due ≥ `endDate − 5` (except p03-85…90) is Skipped (resolution `condition`).
| Id | Title | Role | Blank | Sig | Kind | Prereqs | Due | Closes when (fact) | Link |
|---|---|---|---|---|---|---|---|---|---|
| p03-0 | «Ustoz tanlang va moslashtirish rejasini (HR-F06) oching» | hr4 | HR-F06 | — | fact | — | `probation.startDate` −1 day | `prob.f06` (F06 with `answers.mentor` not null) | blank `{HR-F06, employee}` |
| p03-1 | «HR-F06 ni xodim, ustoz va rahbarga imzolating» | hr4 | HR-F06 | D1/in person | fact | p03-0 | `probation.startDate` | `prob.f06Group1` (steps signEmployee, signMentor, signHead, signHr4 all Signed) | blank (the F06) |
| p03-7 | «7-kun testini o'tkazing (kamida 70%)» | hr4 | HR-F06 `day7` | D1 | fact | p03-1 | start +7 | `prob.f06Step:day7` (value = computed `day7Pass`) | blank (the F06) |
| p03-14 | «14-kunda qayta test o'tkazing» | hr4 | — (Google Forms, outside) | D1 | manual | p03-7 | start +14 | manual (input `percent` int 0–100 required); `when`: `prob.f06Step:day7` value = false | blank (the F06) |
| p03-30 | «30-kun suhbatini o'tkazing» | departmentHead (fallback hr4) | HR-F06 `day30` | D1/in person | fact | p03-7 | start +30 | `prob.f06Step:day30` | blank (the F06) |
| p03-60 | «60-kun suhbatini o'tkazing va 1–5 baho qo'ying» | departmentHead (fallback hr4) | HR-F06 `day60` | D1/in person | fact | p03-30 | start +60 | `prob.f06Step:day60` | blank (the F06) |
| p03-85 | «85-kun testi va yakuniy baho (HR-F07)» | hr4 | HR-F06 `day85` + HR-F07 | D1 | fact | p03-60 | end −5 | `prob.f06Step:day85` AND `prob.f07Submitted` | blank `{HR-F07, employee}` |
| p03-88 | «Sinov natijasi bo'yicha qaror qiling» | hr1 | HR-F07 `hr1Decision` | D1 | fact | p03-85 | end −2 | `prob.f07Step:hr1Decision` | blank (the F07) |
| p03-90 | «Sinov yakuni bo'yicha buyruq va natijani «Sinov» bo'limida yozing» | hr2 | `probations` | D3 | fact | p03-88 | end | `prob.resolved` | probations |

`legacySkippable: true` on p03-0 … p03-60. Guide (new process; text from R03 §3/§5):
p03-0 «Ustozni bo'lim rahbari bilan birga tanlang: shu lavozimda kamida 1 yil ishlagan, jazosiz xodim.» · «Ish joyi va
jihoz tayyorligini tekshiring.» · «HR-F06 ni oching va ustozni belgilang — ustozsiz yangi xodim ishlamaydi.»
p03-1 «Birinchi kuni rejani xodim, ustoz, bo'lim rahbari va siz imzolaysiz (planshetda yoki tizimda).» · «Ustoz birinchi 30 kun har kuni kamida 15 daqiqa xodim bilan ishlaydi.»
p03-7 «7-kun testini o'tkazing; o'tish chegarasi — 70%.» · «Natijani HR-F06 ning 7-kun bo'limiga yozing.» · «7-kun testisiz xodim 8-kunga o'tmaydi.»
p03-14 «7-kun testidan o'tmagan xodim 14-kunda qayta topshiradi.» · «Ikkinchi marta ham o'tmasa — natija HR-1 ga boradi (88-kun qarorida ko'rinadi).»
p03-30 «Bo'lim rahbari 30-kun suhbatini o'tkazadi: davom / o'qitish / to'xtatish.» · «Natija HR-F06 ning 30-kun bo'limiga yoziladi.»
p03-60 «Bo'lim rahbari 60-kun suhbatini o'tkazadi va 1–5 baho qo'yadi.»
p03-85 «85-kun testini (nazariy + amaliy) o'tkazing va HR-F06 ning 85-kun bo'limini to'ldiring.» · «Bo'lim rahbari va ustoz HR-F07 yakuniy bahosini to'ldiradi; xodim natija bilan tanishadi.»
p03-88 «HR-1 HR-F07 da qaror qabul qiladi.» · «O'tmasa — yuristga (ogohlantirish muddati). Natijani xodimga tanishtirmasdan qaror qabul qilinmaydi.»
p03-90 «HR-2 sinov yakuni bo'yicha buyruq tayyorlaydi (yoki qonun tartibida shartnoma bekor qilinadi).» · «Natijani «Sinov» bo'limida yozing — sinov o'zi «o'tdi» bo'lmaydi.»
`sourceRef` = «HR-R03, 3-bo'lim, <kun>-kun».

### 3.7 Flow 4 — `dismissal` (columns: `start` «Ariza», `handover` «Topshirish akti», `order` «Buyruq», `settle` «Hisob-kitob»)
`case.anchorDate` = last working day: manual start input `lastWorkDay` > F08 `answers.leaveDate` > `employments.termination_date`.
| Id | Title | Role | Blank / record | Sig | Kind | Prereqs | Due | Gate | Closes when (fact) | Link |
|---|---|---|---|---|---|---|---|---|---|---|
| ex-1 | «Ariza yoki asos hujjatni qabul qiling» | system | — (paper) | D2→paper | auto | — | — | — | case started (manual start = the application record; starters (b)/(c) imply it) | none |
| ex-2 | «Topshirish aktini (HR-F09) oching» | hr3 | HR-F09 | — | fact | ex-1 | `case.startedOn` | — | `dis.f09Opened` | blank `{HR-F09, employee}` |
| ex-3 | «Chiqish suhbatini o'tkazing va K-kodni belgilang (HR-F08)» | hr4 | HR-F08 | D1 | fact | ex-1 | `case.anchorDate` | **Q13 block** (hook) | `dis.f08Approved` | blank `{HR-F08, employee}` |
| ex-4 | «Xodim va qabul qiluvchi aktni to'ldiradi» | subjectEmployee (fallback hr3) | HR-F09 | in person | fact | ex-2 | `case.anchorDate` −5 wd | AKT warn | `dis.f09Step:handedOver` AND `dis.f09Step:received` | blank (the F09) |
| ex-5 | «Bo'lim rahbari aktni tekshiradi va imzolaydi» | departmentHead (fallback hr3) | HR-F09 | D1 | fact | ex-4 | = ex-4 due | AKT warn | `dis.f09Step:signHead` | blank (the F09) |
| ex-6 | «Aktni tekshiring va tasdiqlang» | hr3 | HR-F09 | D1 | fact | ex-5 | = ex-4 due | AKT warn | `dis.f09Step:hr3Control` | blank (the F09) |
| ex-7 | «Akt bor» belgisi | system | HR-F09 | — | auto | ex-6 | — | — | `dis.f09Approved` (while open: holder shown = role of the F09's open step) | blank (the F09) |
| ex-8 | «Bo'shatish buyrug'ini imzolating va bo'shatishni «Buyruqlar» da yozing» | hr2 | `employments` | D3 | fact | ex-7, ex-3 | `case.anchorDate` | **Q12 block** (hook) | `dis.terminated` | employment |
| ex-8b | «Xodimni mehnat.uz dan chiqaring» | hr2 | — (outside) | — | manual | ex-8 | `employment.terminationDate` +3 wd | Q05 warn | manual (input `removedOn` date required) | external `https://mehnat.uz` |
| ex-9 | «Ketgan xodim ma'lumotini hisobchiga bering» | hr2 | HR-F18 (Phase 2) | — | manual | ex-8 | `employment.terminationDate` | — | manual (no inputs) | none |
| ex-10 | «Yakuniy hisob-kitobni qiling» | payrollAccountant (fallback hr2) | — | D1 | manual | ex-8 | `employment.terminationDate` | **Q12 block** (engine) | manual (input `paidOn` date required) | none |
| ex-11 | «Aktni aktlar ro'yxatiga kiritish» | system | HR-F09 | — | auto | ex-7, ex-8 | — | — | `dis.f09Approved` AND `dis.terminated` | none |

Guide †: ex-1 line 2 kept; add line «Arizani olgan kuni «Jarayonlar → Bo'shatishni boshlash» bilan jarayonni oching va
oxirgi ish kunini kiriting.»; ex-2 line 1 → «Arizadan keyin shu kuni HR-F09 aktini oching va muddatini (oxirgi kundan
5 ish kuni oldin) yozing.» (Phase 1 does not create blanks automatically); ex-2 line 2 dropped (ex-3 appears by itself);
ex-4 add line «Xodimning tizimga kirishi bo'lmasa, aktni HR planshetida u bilan birga imzolang.»; ex-8 split: ex-8
lines 1–2 of the prototype; ex-8b = prototype line 3; ex-9 lines → «Xodim ketganlar ro'yxatiga o'zi tushadi
(bo'shatish yozilgach).» · «Ish haqi hisobchisiga xabar bering; HR-F18 orqali topshirish keyinroq qo'shiladi.»;
ex-10 line kept. AKT warn = §8.3.

## 4. Process catalogue — later flows (catalogue level; full step tables are written in their phase's SPEC update)

| # | Code | Title | Source | Chain | Starter | Phase |
|---|---|---|---|---|---|---|
| 5 | `transfer` | «Ichki o'tkazish / lavozim o'zgartirish» | HR-R01, R06 | F19 (employee → current head → receiving head → HR-4 → chief accountant → HR-1 → director) → F09 (kind transfer) / F06 / F11 → order (`EmploymentChangeCommands` Transfer/Promotion) | F19 submitted | 3 |
| 6 | `discipline` | «Mehnat intizomi» | HR-R07 | F13 (hr3Demand → employee → witnesses → hr3Measure → hr1Decision → hr2Order) → F12 §2 penalty on the existing `Incentive` (link, F12 carries only extra fields) → F18 row | F13 created | 2 |
| 7 | `passport-reserve` | «Lavozim pasporti va zaxira» | HR-R06 | F10 (head + holder → HR-8/HR-1 check → HR-1) → F14 (reserve, training, «zaxira kuni») → F11 acknowledgement | F10 created; position without passport | 3 |
| 8 | `training` | «O'qitish va attestatsiya» | HR-R04, R05 | test/attestation plan → results → F07-style evaluation | schedule rows | 3 |
| C1 | `monthly-close` | «Oylik yopish» | HR-R07 §3, R08 §4 | working day 1 F18 (auto-filled from attendance timesheet) → days 1–3 F12 on `Incentive` → days 4–5 F15 (auto report) | calendar: one case per month, `subject_ref = m:{yyyy-MM}` | 2 |
| C2 | `council-week` | «Haftalik Kengash» | HR-R02 §2.4, HR-04 §2.5 | one F04 per council day; quorum 2/3 shown | calendar: one case per council day `w:{yyyy-MM-dd}` | 2 |
| C3 | `yearly-audit` | «Yillik HR audit» | HR-R08 / F16 | F16 (HR-1 + independent → director) | calendar: `y:{yyyy}` in December | 3 |

Calendar cases have `subject_kind = 4 None`; the engine already supports that kind in Phase 1 (enum value reserved), the
starters ship in their phase.

## 5. HR-F01…F19 → process / step / filler → approver / status

### 5.1 Table
| Blank | Process · step | Fills → approves (INTAKE §3) | Status |
|---|---|---|---|
| F01 | hire-request · r02-1…3 | Bo'lim rahbari → HR-1 (chief accountant for a new position: outside) | **link** `VacancyRequest` (no blank) |
| F02 | hire-candidate · r02-6/7 | Nomzod; HR-4 | exists (`CandidateForm`) |
| F03 | hire-candidate · r02-8 | Nomzod (I) → HR-4 (II–IV) → HR-1 | new — **HRMentor**; manual fallback until served |
| F04 | hire-candidate · r02-9/10; C2 | HR-4 / HR-1 (kotib) → Rais + Kotib | new — **HRMentor**; manual fallback until served |
| F05 | hire-candidate · r02-11/12; hire-formalise · r01-2 | HR-4 → Nomzod; Nomzod + HR-2 | new — **HRMentor** (parts 1–2); manual fallback until served |
| F06 | probation · p03-0…85 | HR-4 + rahbar; ustoz → … HR-1 (day 90) | built (wave 1) |
| F07 | probation · p03-85/88 | Rahbar + ustoz → HR-4 → Xodim → HR-1 | built (wave 1) |
| F08 | dismissal · ex-3 | HR-3 → Xodim + HR-4 | built (wave 1) |
| F09 | dismissal · ex-2…7, ex-11; transfer | HR-3 opens; topshiruvchi → qabul qiluvchi → rahbar → HR-3 → HR-1 → HR-3 | built (wave 1) |
| F10 | passport-reserve; (r02-1 check) | Rahbar + egasi → HR-8/HR-1 → HR-1 | new, Phase 3 |
| F11 | hire-candidate · r02-13; passport-reserve; transfer | HR-2/HR-4 → Xodim | new — **HRMentor**; manual fallback until served |
| F12 | discipline; monthly-close | Bo'lim rahbari → HR-3 → HR-1 → hisobchi | **link** `Incentive` (+ extra-field blank), Phase 2 |
| F13 | discipline | HR-3 → Xodim → guvohlar → HR-3 → HR-1 → HR-2 | built (wave 1) |
| F14 | passport-reserve | Rahbar → HR-1 | new, Phase 3 |
| F15 | monthly-close | HR-3 → HR-1 → direktor | new, Phase 2 |
| F16 | yearly-audit | HR-1 + mustaqil → direktor | new, Phase 3 |
| F17 | hire-formalise (manager positions) | HR-1 → yurist → direktor + xodim | new, Phase 3 |
| F18 | hire-formalise · r01-9; dismissal · ex-9; monthly-close | HR-3 → hisobchi → bosh buxgalter | new, Phase 2 |
| F19 | transfer | Qabul qiluvchi rahbar → … → direktor | new, Phase 3 |

### 5.2 Phase 1 blank contract (definitions are made in HRMentor, §1.5 — NOT in this repo)
The HRMentor definitions of HR-F03, HR-F04, HR-F05, HR-F11 must keep the ids below (facts read them); everything
else follows INTAKE §2 (`legalStatus: draft` for F03 and F05 — lawyer pending). Until served, the steps use the manual
fallback of §1.5.
| Blank | Subject | Engine-relevant fields | Steps (id · order · actor · mode · decision options) |
|---|---|---|---|
| HR-F03 | candidate | `refs` table min 2 (I); `consentEmployer`, `consentRefs` choice `agree`/`refuse` | `consent` · 1 · candidate · inPerson · — ; `hr4Check` · 2 · hr4 · electronic · `ok` «Ishga olish mumkin — e'tiroz yo'q», `watch` «… kuzatuv bilan», `notRecommended` «Ishga olish tavsiya etilmaydi» (`decisionExtra` text required for watch/notRecommended); `hr1Approve` · 3 · hr1 · electronic · — |
| HR-F04 | none | `meetingDate` date req; `members` table (`member` ref employee, `role` text, `present` check); `presentCount` computed count, `quorumMet` computed (shown, not enforced, §18 Q5); `candidates` table min 1 (`candidate` ref candidate req, `position` text, `department` ref department, `requestNo` text, `formNo` text, `decision` choice req `accept` «qabul» / `conditional` «shartli qabul — surishtiruvdan keyin» / `reserve` «zaxira» / `reject` «rad», `practical` choice `passed` «o'tdi» / `failed` «o'tmadi» / `notRequired` «talab qilinmaydi», `salary` money, `startDate` date); `conditions` table (`candidate` ref candidate, `text`); `after` section (5 fixed dates, `fill: open`) | `secretary` · 1 · hr1 · electronic; `chair` · 2 · council · either |
| HR-F05 | candidate | Part 1: `councilRef` text, `position` text, `department` ref department, `workplace`, `schedule` text, `salaryProbation`, `salaryAfter` money, `startDate` date req, `probationTo` computed `addMonths(startDate,3)`, `manager`, `mentor` ref employee, `offerValidUntil` date. Part 2 (`fill: step docsHr2`): `docs` fixed table of the 12 INTAKE rows with `brought` check + `hr2Mark` check; `docsComplete` choice yes/no req; `missingDocs` text; `missingDueDate` date. Part 3 = static text «3-qism «Mening ishlarim» → «Ishga qabulni rasmiylashtirish» jarayonida yuritiladi.» | `offerHr4` · 1 · hr4 · electronic; `offerCandidate` · 2 · candidate · inPerson; `docsHandover` · 3 · hr4 · electronic; `docsHr2` · 4 · hr2 · electronic; `docsCandidate` · 4 · candidate · inPerson |
| HR-F11 | employee | `documents` table min 1 (`title` text req, `version` text, `date` date, `introducedBy` ref employee) — the per-row signature column is replaced by the two steps | `signEmployee` · 1 · subjectEmployee · either; `signIntroducer` · 1 · hr4 · electronic |

Candidate in-person steps: `signer_employee_id` stays null; the operator types nothing (the PDF prints the submission's
candidate name). Slice P3 adds the integration test «candidate in-person sign → 200». If the existing step handler
rejects it, the minimal fix is hook point H17 (§13) — nothing else in the blank engine changes.

## 6. Data model (PostgreSQL, snake_case, `uuid` v7 ids, `timestamptz` UTC, display Tashkent +5)

All new tables. Column names avoid the MCP hidden-column pattern (`subject_ref`, `binding_code`, `board_column`,
`evidence_sha256` — never `*_key`, `*_hash`, `*token*`, `*secret*`).

### 6.1 `process_templates` / `process_template_versions`
Same shape as `blank_templates` / `blank_template_versions` (SPEC-blankalar §3.1–§3.2): template row per `code`
(`title`, `source`, `subject_kind`, `is_active`); version row per (`template_id`, `version`) with `status` (1 Draft,
2 Published, 3 Retired; partial UNIQUE (template_id) WHERE status = 2), `definition jsonb`, `definition_sha256`,
`published_at`. Seeded by `ProcessTemplateSeeder` from embedded `Processes/Templates/*.json`.

### 6.2 `process_cases` — aggregate root
| Column | Type | Null | Notes |
|---|---|---|---|
| id | uuid | no | |
| process_code | text | no | §3.2 |
| template_version_id | uuid | no | FK; a case keeps the version it started with |
| status | smallint | no | 1 Active, 2 Completed, 3 Cancelled (manual cancel or subject deleted) |
| outcome | text | yes | e.g. `hired`, `declined`, `rejected`, `passed`, `failed`, `terminated` |
| legacy | bool | no | true when the subject's start fact is older than `processes.startedOn` (§7.6) |
| subject_kind | smallint | no | 1 VacancyRequest, 2 Candidate, 3 Employment, 4 None |
| subject_ref | text | no | §3.2; UNIQUE (process_code, subject_ref) over all statuses |
| vacancy_request_id, candidate_id, employment_id, probation_id, employee_id | uuid | yes | filled as facts appear; partial UNIQUE (process_code, employment_id) WHERE employment_id IS NOT NULL |
| department_id | uuid | yes | refreshed each pass (filters, «Kim kechikdi» grouping) |
| parent_case_id | uuid | yes | FK self (hire-candidate → hire-request; hire-formalise → hire-candidate) |
| anchor_date | date | yes | §3.5, §3.7 |
| started_on | date | no | Tashkent date of the start fact |
| started_by | uuid | yes | user for manual starts |
| completed_at, cancelled_at | timestamptz | yes | |
| cancel_reason | text | yes | required on cancel |
| version | int | no | optimistic concurrency (AbsenceRequest pattern) |
| created_at, updated_at | timestamptz | no | |

### 6.3 `process_case_steps`
| Column | Type | Null | Notes |
|---|---|---|---|
| id | uuid | no | |
| case_id | uuid | no | FK cascade |
| step_code | text | no | definition id; UNIQUE (case_id, step_code) |
| order_no | int | no | definition order |
| board_column | text | no | definition `column` |
| state | smallint | no | 1 Locked, 2 Active, 3 Waiting, 4 Blocked, 5 Done, 6 Skipped |
| resolution | smallint | yes | 1 fact, 2 manual, 3 auto, 4 condition, 5 legacy, 6 userSkip, 7 caseEnded |
| holder_role | text | yes | §9.1 code; null for system steps |
| holder_employee_id | uuid | yes | resolved person (department head, subject employee) |
| due_date | date | yes | §3.1 due rules |
| done_at | timestamptz | yes | time of the FACT (not of the pass) or of the manual click |
| done_by | uuid | yes | user (manual) or null (fact/auto) |
| payload | jsonb | yes | manual inputs, fact value (e.g. F04 decision) |
| evidence_sha256 | text | yes | SHA-256 of canonical payload JSON for manual D1 steps |
| blocked_gate_code | text | yes | §8.1 code while state = Blocked |
| linked_blank_submission_id | uuid | yes | the blank the step reads |
| linked_record_id | uuid | yes | vacancy request / candidate form / employment event |
| updated_at | timestamptz | no | |

### 6.4 `process_gate_overrides`
`id`, `gate_code`, `case_id` (null for direct hire), `step_code`, `employment_id`, `candidate_id`, `for_user_id`
(direct hire: the operator allowed), `reason` (text ≥ 10 chars), `granted_by`, `granted_at`, `expires_at` (+24 h),
`consumed_at`, `consumed_operation` (`hire` / `terminate`). Single use; consumed in the same transaction as the
command (§8.4).

### 6.5 `process_stage_bindings`
`binding_code` (PK; `contacted`, `invited`, `council`, `practicalTest`) → `pipeline_stage_id` (FK, nullable). Owner
sets them in the settings page (§11.7). Unbound → the stage fact is false (step closes by its manual fallback only).

### 6.6 Settings (`SystemSettingKeys`)
| Key | Type | Default | Notes |
|---|---|---|---|
| `processes.startedOn` | Date, Hidden | written once by the seeder (Tashkent today), never in the Testing environment | absent → engine dormant: no cases, gates off (§7.6) |
| `processes.gates.mode` | Text `^(enforce\|warn\|off)$` | `warn` | kill switch; `warn` turns every block into an `HrWarning`. Owner decision: 1–2 weeks in `warn`, then `enforce` in the settings |
| `processes.councilWeekday` | Number 1–5 | 4 | `nextCouncilDay` |
| `processes.reconcile.watermark` | Text, Hidden | — | last pass time (diagnostics only; passes are full, §7.4) |

### 6.7 `process_reminders` (Phase 2, table created in Phase 2)
`case_step_id`, `sent_to_user_id`, `channel` (inApp / telegram), `sent_at`, `sent_by`. Phase 1 has no reminders.

### 6.8 Indexes
`process_cases (status, process_code)`, `(employee_id)`, `(candidate_id)`, `(department_id) WHERE status = 1`;
`process_case_steps (state, holder_role) WHERE state IN (2,4)`, `(holder_employee_id) WHERE state IN (2,4)`,
`(due_date) WHERE state IN (2,4)`.

### 6.9 Edge cases
- Subject deleted (candidate soft-deleted, vacancy request removed) → case Cancelled, `cancel_reason = 'subject_deleted'`.
- Candidate re-applies (`reapplied_at` changes) → new `appliedOn` → a NEW `hire-candidate` case; the old Active one is
  Completed with outcome `reapplied`.
- Rehire (`employment_events` type 8) → a new `hire-formalise` case (new employment id).
- Template version bump: running cases keep their version; new cases take the Published one.

### 6.10 Working-day calendar
`process_holidays`: `date` (PK), `kind` (1 Holiday, 2 WorkingDay — a working Saturday), `title`. Working day = Mon–Fri
not Holiday, plus WorkingDay rows. Seed: fixed-date Uzbek holidays 2026–2027 (1 Jan, 8 Mar, 21 Mar, 9 May, 1 Sep,
1 Oct, 8 Dec, 31 Dec); moving ones (Ramazon/Qurbon hayit) and government transfers are entered by HR-1 (§18 Q8).
`IWorkingDayCalendar` (Application) is the single implementation; the blanks engine keeps its own `addWorkingDays`
(Mon–Fri) in Phase 1 (contradiction C10).

## 7. State machine and reconciliation

### 7.1 Why polling, not domain events
`IDomainEvent` / `Entity.Raise` exist but nothing raises and there is no dispatcher (`Ignore(DomainEvents)` in every
EF config). Raising events would edit ~10 aggregates (decision 2). Facts also arrive through SQL imports/ETL that
bypass aggregates, and lateness is time-based anyway. Precedent: `AttendanceEmploymentChangesJob`. So the engine
**reconciles from facts**: each completion trigger is a named read-only fact query (§7.2) that returns a value and
the `occurred_at` of the fact itself.

### 7.2 Fact catalogue (`IProcessFactSource`, one EF query per fact family, batched per pass)
| Fact | True when | `occurred_at` / value |
|---|---|---|
| `vr.created` | request row exists | `created_at` |
| `vr.decided` | status 2/3/4 | `decided_at`; value `approved`/`rejected`/`cancelled` |
| `vr.firstCandidate` | a candidate with `vacancy_id = request.vacancy_id` created after `decided_at` | that `created_at` |
| `vr.filled` | hired candidates (status 3) for the vacancy ≥ `openings`, or vacancy closed | last hire time |
| `cand.contacted` / `cand.stage:<binding>` | a `candidate_stage_entries` row with the bound stage id, `recorded_at ≥ appliedAt` | `recorded_at` |
| `cand.f02` | `candidate_forms` row for the candidate, `submitted_at ≥ appliedAt − 1 day` | `submitted_at` |
| `cand.inF04` | a non-Cancelled HR-F04 whose `candidates` table has the candidate | submission `created_at` |
| `cand.f04Decision` | that F04 is Approved | approval time; value = row `decision` |
| `cand.f03` | HR-F03 for the candidate Approved | approval time; value = `hr4Check` decision |
| `cand.f05Step:<stepId>` | HR-F05 for the candidate has that step Signed/Approved | step `acted_at` |
| `cand.status` | candidate status | `updated_at`; value = status |
| `cand.hired` | `employment_events` type 1/8 with `candidate_id` | `recorded_at`; fills `employment_id`, `employee_id` |
| `emp.hired` | employment exists | `created_at` |
| `emp.hireOrder` | `employments.hire_order_no` not empty | `updated_at` (fallback `created_at`) |
| `emp.attendance` | a live `attendance_identities` row for the employee OR a `device_enrollments` row status 1 Synced (column names read from the EF configs in P4) | earliest such row time |
| `emp.f11` | HR-F11 for the employee with both steps Signed | last `acted_at` |
| `prob.f06` / `prob.f06Group1` / `prob.f06Step:<id>` | HR-F06 for the employee created ≥ probation start − 7 days (`answers.mentor` set / group-1 steps signed / that step Signed) | `acted_at`; value = computed field where §3.6 says so |
| `prob.f07Submitted` / `prob.f07Step:hr1Decision` | HR-F07 for the employee InApproval+ / that step Signed | `acted_at`; value = decision |
| `prob.resolved` | `probations.status` ∈ 2–5 | `resolved_at`; value = status |
| `dis.f09Opened` / `dis.f09Step:<id>` / `dis.f09Approved` | HR-F09 for the employee with `answers.kind = 'dismissal'`, non-Cancelled / step Signed / Approved | `created_at` / `acted_at` |
| `dis.f08Approved` | HR-F08 for the employee Approved (or InApproval with K-code filled — Q13 needs the code) | `acted_at` |
| `dis.terminated` | `employments.status = 3` | `termination_date` (+ `updated_at`) |

Blank facts only read `blank_submissions` / `blank_submission_steps`; they never call blank handlers.

### 7.3 Starters
Scans of §3.2, each `INSERT … ON CONFLICT (process_code, subject_ref) DO NOTHING`. Order inside a pass:
hire-request → hire-candidate → hire-formalise (a) → hire-formalise (b) → probation → dismissal (b), (c). Manual
start (`POST /cases`) exists only for `dismissal` (input `lastWorkDay` date required, `basis` text) in Phase 1.
Backfill: the first pass after `processes.startedOn` is written creates cases only for subjects whose start fact is
on/after that date — no mass backfill; older in-flight subjects get a case only through their next start fact
(e.g. a new F09, a termination) and are marked `legacy`.

### 7.4 One pass (per case, ordered by `order_no`)
1. Load the case's facts (batched). 2. For each step: `when` false → Skipped(condition); fact holds → Done(fact,
`done_at` = fact time) — **a fact wins over order**: a later step can be Done before an earlier one; a fact that
disappears reverts a fact-Done step to Active (a manual Done never reverts). 3. Remaining steps: any non-optional
prereq not Done/Skipped → Locked; prereqs met and a `block` gate fails (§8.3) → Blocked (`blocked_gate_code`);
otherwise Active. 4. Waiting = Active for a person role whose holder is someone other than the viewer — computed at
read time, never stored (state 3 is reserved for «waiting for another case», e.g. r02-13 while the formalise case's
mm is open). 5. Recompute `due_date`, holder, `department_id`. 6. Save **only if something changed** (no SSE loop;
`IDataChangeSignal` fires only then).
Runs: Hangfire `processes:reconcile` cron `1-59/5` (UTC; collides with no existing job), and inline before process
reads (60 s in-memory throttle; the reader waits 2 s at most, the pass has its own 60 s). One global `pg_try_advisory_xact_lock`; busy → the read serves current state.
Opening a case detail reconciles that one case under the same lock.

### 7.5 When a case ends
Completed when every non-optional step is Done/Skipped, or an end fact holds: hire-request — `vr.decided`
rejected/cancelled (outcome = value), `vr.filled`; hire-candidate — decline branch closed (outcome `declined`) or
`cand.status` 2/4/6 at any step (outcome = status), hire (outcome `hired`); probation — `prob.resolved` (outcome =
status) after p03-90; dismissal — ex-11 Done (outcome `terminated`), or the employment back to Active with no open
F09 (outcome `withdrawn`). Optional steps still open → Skipped(caseEnded). Cancelled: manual (`processes.manage`,
reason) or subject deleted. `POST /cases/{id}/reopen` (manage) → Active, steps recomputed.

### 7.6 Dormant mode and legacy
While `processes.startedOn` is absent (always in the Testing environment unless a test writes it) the engine creates
nothing and `IProcessGate` returns Allow — existing integration tests that hire without a candidate stay green.
`legacy = true` cases: gates warn only; steps with `legacySkippable` may be Skipped by the holder with one click.

### 7.7 Holder resolution
Office role → `blank_role_assignments` for that role (all assigned users see it); unassigned → every
`processes.manage` holder. Person roles: `departmentHead` → `Department.HeadEmployeeId` of the case department →
the user linked to that employee (`users.employee_id`); `subjectEmployee` → the case employee's user; no user →
`fallbackRole`. `system` → «Tizim», never a task. `council` → office role (members assigned in «Rollar»).

## 8. Gates

### 8.1 Catalogue
| Code | Mode | Where enforced | Rule | Error / warning code |
|---|---|---|---|---|
| Q01 | block | hook: Hire (H13) | a hire with a candidate needs `cand.f02` AND `cand.f04Decision` ∈ {accept, conditional} AND (conditional → `cand.f03` ok/watch); without a candidate → `jarayon.nomzodsiz_qabul` unless the person has an Active employment in another legal entity; hire order number missing → warning only | `jarayon.anketa_yoq`, `jarayon.kengash_qarori_yoq`, `jarayon.surishtiruv_yoq`, `jarayon.nomzodsiz_qabul`, warning `jarayon.buyruq_raqami_yoq` |
| Q02 | warn | engine | r01-8a not Done by hire date | — (card colour) |
| Q03 | block | engine (r01-9) | employee has JSHSHIR (`pinfl`) and r01-9 input `payment` given | `jarayon.jshshir_yoq` |
| Q04 | block | engine (r02-13) | step `mm` of the child `hire-formalise` case Done | `jarayon.yoriqnoma_yoq` |
| Q05 | warn | engine | r01-6 / ex-8b past due | — |
| Q06 | warn | engine | r01-7 past due or checklist incomplete | — |
| Q12 | block | hook: Terminate (H14) + engine (ex-10) | an Approved HR-F09 kind dismissal exists for the employment | `jarayon.akt_yopilmagan`; an override is honoured only when termination date ≤ today, else `jarayon.istisno_oxirgi_kunda` |
| Q13 | block | hook: Terminate (H14) | HR-F08 for the employee with the main K-code filled | `jarayon.chiqish_suhbati_yoq` |
| AKT | warn | engine (ex-4…6) | F09 §1–4 all empty → card warning; blocking would change the blank engine (C8) | — |

Q07–Q11 are absent from the prototype (C1, §18 Q6).

### 8.2 Hook gates — `IProcessGate`
New Application port: `Task<GateResult> CheckAsync(GateContext ctx, CancellationToken)`; `GateContext` = operation
(`Hire` / `Terminate`), candidate id, employee id, employment id, legal entity, termination date, acting user id.
`GateResult` = Allow | Warn(codes) | Block(codes). Called with ONE line in `HireEmployeeCommandHandler` and
`TerminateEmploymentCommandHandler`, next to `EmployeeFlagGuard` (constructor injection only; no test constructs the
handlers directly). Block → 409 problem+json `.With(codes)` (existing pattern in Terminate); Warn → `HrWarning`
entries in the existing Confirm flow. Gates evaluate facts synchronously at call time, not the reconciled state.
`HireCandidateCommandHandler` goes through Hire; `ProbationCommands` (Failed → terminate) goes through Terminate
unchanged. Mode: `off` → Allow; `warn` or legacy subject (candidate applied before `startedOn`; employment whose
dismissal case is legacy) → blocks become warnings; employment with no dismissal case → enforced.

### 8.3 Engine gates
Evaluated in the pass (§7.4 step 3): Q03, Q04, the engine part of Q12 (ex-10 Blocked while no Approved F09), AKT
warning, Q02/Q05/Q06 lateness warnings. A Blocked step shows the gate's text and the holder of the missing thing
(prototype `holderOf`: Q04 → safety engineer «yo'riqnomani tasdiqlamagan»; Q12 → role of the F09's open step).

### 8.4 Overrides
`POST /api/processes/cases/{id}/gates/{code}/override {reason}` and `POST /api/processes/gate-overrides/direct-hire
{forUserId, reason}` — permission `processes.override` AND the caller holds the hr1 assignment (or, while hr1 is
unassigned, `processes.manage`). Creates a row valid 24 h; the guard consumes it (`consumed_at`, operation) inside the
command's transaction. Command signatures do not change. Every override is audited (§14) and listed on «Kim kechikdi».

## 9. Roles and permissions

### 9.1 Role codes
Office (via `blank_role_assignments`): `hr1` `hr2` `hr3` `hr4` `hr5` `hr8` `payrollAccountant` `chiefAccountant`
`director` `lawyer` + new `safetyEngineer`, `council` (added to `BlankActorRoles.OfficeRoles`, H12). Person:
`departmentHead`, `subjectEmployee`. `system`. HR-5/HR-8 work is done by hr1 until assigned (prototype glossary).

### 9.2 Permissions (`Permissions.cs`, module key `processes`)
| Permission | Grants |
|---|---|
| `processes.view` | the three tabs (own tasks, board, late), case detail, MCP read |
| `processes.work` | manual «Bajarildi», «Kerak emas» (skippable / legacy), undo own manual step within 24 h, start a dismissal case; landing on «Mening ishlarim» |
| `processes.manage` | cancel / reopen cases, stage bindings, holidays, act on any manual step, settings |
| `processes.override` | gate overrides (with the hr1 rule) |
Default role grants (H1): Bosh rahbar and HR manager roles get view+work+manage; override only Bosh rahbar (owner may
change in «Rollar»).

### 9.3 Who may act on a step
Manual step: the resolved holder, or any `processes.manage` holder. Fact steps are never «done» by hand except their
declared `manual` fallback. A user acting for a role they do not hold is recorded (`done_by`) and audited.

## 10. API contract (`/api/processes`, all `RequireAuthorization`, JSON camelCase, problem+json errors)
| # | Method · path | Permission | Notes |
|---|---|---|---|
| 1 | GET `/my` | view | the viewer's tasks grouped `urgent` (today + late + Blocked held by me), `later`, `held` (Blocked, missing thing held by someone else), `waiting` (Locked whose blockers are Active for others, due ≤ 7 days), plus `tartib` cards (§11.8) |
| 2 | GET `/my/count` | view | badge count = urgent + later |
| 3 | GET `/cases` | view | filters `process`, `status`, `department`, `employee`, `candidate`, `holderRole`, `late`, `from`, `to`, `q`; paged (20/50/100/all) |
| 4 | GET `/board?process=` | view | columns of one process with cards (case, current step, holder, due) |
| 5 | GET `/cases/{id}` | view | case + steps (state, holder, due, guide, link, gate text, payload) + linked blanks; reconciles the case first |
| 6 | GET `/late` | view | `blocked`, `late`, `warn` lists with holder and reason (prototype `problems()`), overrides of the last 30 days |
| 7 | GET `/templates` | view | published definitions (for the step panel and the board) |
| 8 | GET `/status` | view | engine state: dormant/active, gates mode, last pass time |
| 9 | POST `/cases` | work | manual start (`dismissal` only in Phase 1) |
| 10 | POST `/cases/{id}/steps/{code}/done` | work | manual inputs validated against `manual.inputs`; 409 `jarayon.qadam_holati_notogri` if not Active |
| 11 | POST `/cases/{id}/steps/{code}/skip` | work | only `skippable` or legacy `legacySkippable`; reason required for legacy |
| 12 | POST `/cases/{id}/steps/{code}/undo` | work | own manual Done within 24 h, or manage |
| 13 | POST `/cases/{id}/cancel` · `/reopen` | manage | reason required |
| 14 | POST `/cases/{id}/gates/{code}/override` | override | §8.4 |
| 15 | POST `/gate-overrides/direct-hire` · GET `/gate-overrides` | override · view | §8.4 |
| 16 | GET/PUT `/settings/stage-bindings` | manage | §6.5 |
| 17 | GET/POST/DELETE `/holidays` | view/manage | §6.10 |
| 18 | POST `/reconcile` | manage | runs a pass now (diagnostics) |
Errors (`jarayon.*`, 4 locales): `jarayon.topilmadi` 404, `jarayon.qadam_holati_notogri` 409, `jarayon.kiritish_notogri`
400, `jarayon.versiya` 409 (concurrency), `jarayon.ruxsat_yoq` 403, plus §8.1 gate codes.

## 11. UI (iOS HIG; feature `web/src/features/processes/{api,components,lib}`)

### 11.1 Menu and landing
New first sidebar section with item `myWork` «Mening ishlarim» (`/mening-ishlarim`, permission `processes.view`,
`countTone: 'danger'`, count = #2). After login a user with `processes.work` lands on `/mening-ishlarim`
(`login-form.tsx` line ~57: `redirectTo ?? landingPath`); `redirectTo` still wins. The super admin lands there too.

### 11.2 Tab «Mening ishlarim»
Header «Bugun: {date}» + one line «{n} ta ish, {k} tasi kechikkan». Sections in the prototype order: «Bugun va
kechikkan», «Keyinroq», «To'xtatilgan — boshqasi hal qilishi kerak», «Kutilmoqda — avval boshqalar bajarishi kerak»,
then «Tartib» cards. Task card: colour stripe (red late, yellow today, grey blocked 🔒), verb-first title, «{subject}
· {position}, {department}», due chip («Bugun», «Ertaga», «3 kun kechikdi», «24.11 gacha»), blank chip (code + short
title). Empty state «Hozir sizda ochiq ish yo'q».

### 11.3 Tab «Jarayonlar»
Process selector (5 codes) → board with the definition's columns; card = subject, current step, holder, due. List
view toggle with the project's one-line table rule and filters of #3. «Bo'shatishni boshlash» button (work).

### 11.4 Tab «Kim kechikdi» (view; prominent for hr1 / manage)
Three groups «To'xtatilgan», «Kechikkan», «Ogohlantirish» — row: case, step, holder (role + name), reason, days late,
«Ochish». «Eslatish» is Phase 2 (button hidden). Overrides log below.

### 11.5 Case detail and step panel
`/mening-ishlarim/{caseId}` (also as a side sheet from a card): timeline of steps by column (state icons), the
selected step's panel: «Nima qilaman?» numbered guide, «Manba (reglament)» disclosure, gate text if Blocked with
«Kim hal qiladi», the manual form (inputs) with «Bajarildi», «Kerak emas» when allowed, and **«Boshlash»**: blank
steps call `POST /api/blanks/submissions` (template + subject; existing endpoint, user's permission) then navigate
to the blank; other steps navigate to §11.6. If the viewer is not the holder: «Bu qadamni {role} bajaradi».

### 11.6 Deep-link screens
`vacancyRequests` → `/vakansiya-sorovlari?id=`; `candidate` / `candidateForm` → candidate card (form tab);
`blank` → `/blankalar/{id}` (create-or-open); `employee` → `/xodimlar/{id}`; `employment` → `/buyruqlar` filtered;
`probations` → `/sinov`; `attendanceMapping` → `/verifikatsiya/moslashtirish`; `attendanceSchedules` →
`/verifikatsiya/jadvallar`; `external` → new tab.

### 11.7 Settings page (manage)
`/mening-ishlarim/sozlamalar`: stage bindings (4 selects of pipeline stages), holidays list, council weekday, gates
mode (with a warning text), engine status.

### 11.8 «Tartib» cards
Read on request, not stored: NotificationCollector types `CandidateContactDue`, `CandidateContactOverdue`,
`CandidateInterviewSoon`, `CandidateReapplied`, `ProbationMissing` (with each type's existing permission), plus three
count cards: unmatched terminal persons (`ListUnmatchedQuery`) → Moslashtirish, active employees without photo,
active employees without a schedule. `CandidateNew`, `CandidateFromBot`, `VacancyRequested` and the `Probation*`
timing types are left out — the processes cover them.

Strings in 4 locales (`./bonvi check` must pass).

## 12. MCP / FullRead exposure (read-only)
`api/src/Bonvi.Api/Mcp/Tools/ProcessTools.cs` (shape of `BlankTools.cs`), registered where `BlankTools` is
(`McpSetup.cs`), scope `processes.view`:
| Tool | Args | Returns |
|---|---|---|
| `process_cases` | process, status, department, employee, late, from, to, page, pageSize ≤ 200 | case rows: process, subject name, current step(s), holder, due, days late |
| `process_case` | id | case + steps (state, holder, due, done_at, resolution, gate) — no manual payload free text beyond 500 chars |
| `process_stuck` | department, minDaysLate | blocked + late + warn lists (as #6) — what the AIAgents HR bot reports |
Migration redefines `mcp_apply_ro_grants` (the Blankalar §11 pattern) to add the `process_*` tables; columns are named
outside the hidden pattern (§6).

## 13. Registration and hook points (the ONLY existing files edited)
| # | File | Change |
|---|---|---|
| H1 | `api/src/Bonvi.Domain/Identity/Permissions.cs` | 4 constants `processes.*`; default role lists (~line 637) per §9.2 |
| H2 | `api/src/Bonvi.Domain/Identity/AccessModuleKeys.cs` | key `processes` |
| H3 | `api/src/Bonvi.Domain/Identity/PermissionCatalog.cs` | `Module("processes", view, work, manage, [override])` |
| H4 | `api/src/Bonvi.Domain/PublicApi/ApiKeyScope.cs` | FullRead includes `processes.view` |
| H5 | `api/src/Bonvi.Domain/Settings/SystemSettingKeys.cs` | the four keys of §6.6 |
| H6 | `api/src/Bonvi.Infrastructure/Persistence/AppDbContext.cs` | DbSets + `ApplyConfiguration` for §6 tables |
| H7 | `api/src/Bonvi.Infrastructure/Persistence/Auditing/AuditCatalog.cs` | entity labels for cases, manual steps, overrides, bindings, holidays |
| H8 | `api/src/Bonvi.Application/Common/DependencyInjection.cs` | `AddProcessesApplication()` |
| H9 | `api/src/Bonvi.Infrastructure/DependencyInjection.cs` | `AddProcessesInfrastructure()` |
| H10 | `api/src/Bonvi.Api/Startup/DatabaseInitializer.cs` | run `ProcessTemplateSeeder` after `BlankTemplateSeeder` |
| H11 | `api/src/Bonvi.Api/Program.cs` | `app.MapProcessEndpoints()` |
| H12 | `api/src/Bonvi.Domain/Blanks/BlankActorRoles.cs` | `OfficeRoles` + `safetyEngineer`, `council` (one line) |
| H13 | `api/src/Bonvi.Application/Employees/Employments/HireEmployeeCommand.cs` | inject `IProcessGate`; ONE call next to `EmployeeFlagGuard`; Block → 409 `.With(codes)`, Warn → `HrWarning` |
| H14 | `api/src/Bonvi.Application/Employees/Employments/TerminateEmploymentCommand.cs` | same as H13 for Terminate |
| H15 | `api/src/Bonvi.Api/Startup/BackgroundJobs.cs` | recurring `processes:reconcile` cron `1-59/5` |
| H16 | `api/src/Bonvi.Infrastructure/Bonvi.Infrastructure.csproj` | `EmbeddedResource Include="Processes\Templates\*.json"` |
| H17 | `api/src/Bonvi.Application/Blanks/Steps/BlankStepHandlers.cs` | ONLY if P3's test fails: allow in-person signing of a step whose actor is `candidate` (null `signer_employee_id`) |
| H18 | `api/src/Bonvi.Api/Mcp/McpSetup.cs` | register `ProcessTools` |
| H19 | migration `…_Processes.cs` | new tables + `mcp_apply_ro_grants` redefinition (body copied from the Blankalar migration plus `process_*`) |
| W1 | `web/src/app/layout/nav-items.ts` | new first section, item `myWork` |
| W2 | `web/src/app/layout/sidebar.tsx` | count line for `myWork` (`useMyWorkCount`) |
| W3 | `web/src/features/auth/components/login-form.tsx` (line 57) | `redirectTo ?? landingPath(user)` |
| W4 | `web/src/app/routes/_app/mening-ishlarim/*` | new routes (new files; route tree regenerates) |
| W5 | `web/src/features/blanks/components/blank-create-dialog.tsx` (~line 52) | enable candidate subject |
| W6 | `web/src/features/blanks/lib/blank-lookups.ts` | candidate search for `ref:candidate` fields |
| W7 | `web/src/features/recruiting/components/candidate-card-page.tsx` (~line 303) | «Blankalar» card (reuse `SubjectBlanksTab` shape) + «Jarayon» chip linking the case |
| W8 | `web/src/shared/i18n/locales/{uz-Latn,uz-Cyrl,ru,en}.json` | `processes.*` keys + `jarayon.*` error codes |
Nothing else in recruiting, employees, attendance or the blank engine changes.

## 14. Audit
Through the existing audit interceptor: case created (system / user), cancelled, reopened; manual step done / undone
/ skipped (inputs, `evidence_sha256`); gate override granted and consumed (reason, operation); stage bindings,
holidays and settings changed. Fact-driven step changes are NOT audited (they are derived; the source change is
audited where it happened). `/audit` shows process entries under «Jarayonlar».

## 15. Contradictions and resolutions
| # | Conflict | Resolution |
|---|---|---|
| C1 | Prototype rules list Q07–Q11 is missing | ignored until the owner supplies texts (§18 Q6) |
| C2 | r02-1 F01 «ish haqi» vs SPEC-vakansiya-sorovi «no salary in the request» (owner 2026-10-30) | no salary in Phase 1 (§18 Q3); F04 `salary` carries the offered pay |
| C3 | F10 gate on r02-1/2, but F10 is Phase 3 | guide lines dropped; gate added in Phase 3 |
| C4 | F03 after council (prototype, newer) vs before (INTAKE/R02) | prototype wins (r02-8 after r02-10) |
| C5 | F04 overlaps the «Кенгаш» pipeline stage | F04 is the evidence; the stage is moved by HR (guide line); legacy cases accept the stage (§18 Q2) |
| C6 | Probation outcome options differ (F06 day90: 2, R03/F07: 3); auto-pass removed 2026-10-30 | F07 decision drives p03-88; p03-90 closes on `probations.status` |
| C7 | D2 = employee one-time code; blank engine has in-person PNG signatures only | in person in Phase 1; D2 in Phase 3 |
| C8 | AKT must block approval of an empty F09 | warning only (blocking edits the blank engine) |
| C9 | F06 day-90 section not tracked | p03-88 uses F07 instead |
| C10 | blanks `addWorkingDays` ignores holidays | processes use `IWorkingDayCalendar`; blanks unchanged in Phase 1 |
| C11 | r02-7 condition «ishchi kasblar» needs a position; candidates have only a vacancy title | optional + skippable by HR |
| C12 | F05 part 3 (first-day checklist) | is the `hire-formalise` case, not a blank section |
| C13 | This SPEC first planned F03 F04 F05 F11 as repo JSON; SPEC-hujjat-manbai S1 / D6 forbids it | made in HRMentor; manual fallback until served (§1.5) |

## 16. Build plan (slices for subagents; one slice ≈ 40–60 tool calls)
| Slice | Content | Depends on |
|---|---|---|
| **P1** Domain + persistence | entities §6 (`Bonvi.Domain/Processes/*`), enums, `ProcessErrors`, repositories, EF configs, migration `./bonvi migrate Processes` (+ grants §12; read it, drop spurious DROPs), H1–H7, H12 | — |
| **P2** Definitions + calendar + seeder | definition records, `ProcessDefinitionValidator`, `IWorkingDayCalendar` + `process_holidays` seed, the 5 template JSONs of §3 (guide lines from the prototype), `ProcessTemplateSeeder`, H10, H16; validator + calendar unit tests | P1 |
| ~~P3~~ | moved to the HRMentor work (§1.5); not built here | — |
| **P4** Facts, starters, reconciler, job | `IProcessFactSource` (§7.2), starters §7.3, pass §7.4–§7.7, Hangfire job H15, dormant mode; scenario tests per flow | P2 |
| **P5** Gates + overrides | `IProcessGate` impl, H13, H14, override endpoints #14–#15, error codes; tests: block, warn, legacy, off, dormant, override consumed once | P4 |
| **P6** Read API | #1–#8, holder resolution, «Tartib» cards, H11 | P4 |
| **P7** Write API | #9–#13, #16–#18; RBAC tests per endpoint | P6 |
| **P8** MCP | §12, H18, H19 grants check | P6 |
| **P9** Web foundation | API client (`./bonvi gen`), routes W4, menu W1–W2, landing W3, i18n W8 | P6 |
| **P10** Tab «Mening ishlarim» | §11.2 + step panel §11.5 | P9 |
| **P11** Board + case detail | §11.3, §11.5 page | P9 |
| **P12** «Kim kechikdi» + settings | §11.4, §11.7 | P9 |
| **P13** Candidate blanks in web | W5–W7 — only once a candidate-subject blank is served from HRMentor (deferred) | HRMentor |
| **P14** QA | `qa-review`, `test-migration`, browser pass of the three tabs | all |

Parallel waves: **[P1 ∥ P2a ∥ P4a] → [P2b ∥ P4b] → [P5 ∥ P6] → P7 → [P8 ∥ P9] → [P10 ∥ P11 ∥ P12] → P14**, where
P2a = definitions, validator, calendar logic, the 5 template JSONs (no DB); P2b = holidays table use + seeder;
P4a = `IProcessFactSource` (§7.2, reads existing tables only); P4b = starters, pass, job.

## 17. Tests (Definition of Done)
- `./bonvi test` green, no test deleted or weakened; existing hire/terminate integration tests pass unchanged
  (dormant mode in Testing).
- Unit: validator (every rule of §3.1), calendar (holidays, working Saturdays, negative offsets), due rules, pass
  algorithm (fact wins over order, revert, manual never reverts, Locked/Active/Blocked), case end rules §7.5.
- Scenario (integration, real PostgreSQL): one per flow — hire-request → hire-candidate (accept and decline branches)
  → hire-formalise → probation → dismissal, driven only through existing endpoints + blank endpoints; assert steps
  close by themselves.
- Gates: Q01 each missing piece → 409 code; Q12 / Q13; warn mode; legacy; override single use and 24 h expiry; Q12
  override before the last day → `jarayon.istisno_oxirgi_kunda`.
- RBAC: every endpoint 401 anonymous, 403 without the permission.
- Migration: `upgrade head` on an empty DB and on a prod copy (pre-deploy routine); downgrade drops only `process_*`.
- Web: `./bonvi pnpm build`, `./bonvi pnpm test`, `./bonvi check` (4 locales).

## 18. Owner questions (everything else is decided and logged in `docs/ASSUMPTIONS.md`)
1. **Hire without a candidate** (someone hired directly in «Buyruqlar»): block unless HR-1 gives a one-time permission?
   Recommendation: block; HR-1 permits per operator for 24 h.
2. **F04 → «Кенгаш» stage.** Should approving F04 also move the candidate to the «Кенгаш» stage automatically?
   Recommendation: not in Phase 1 (HR moves it, the guide says so); Phase 2 adds a one-click button acting as the user.
3. **Salary in F01.** The prototype asks for it, the vacancy-request decision (2026-10-30) says no salary.
   Recommendation: none in the request; the offered pay is written in F04/F05.
4. **Gate rollout.** Cases older than go-live only warn. Recommendation: yes.
5. **Safety engineer and council members** — who are they? Is the 2/3 quorum enforced? Recommendation: owner assigns
   them in «Rollar»; quorum shown, not enforced.
6. **Rules Q07–Q11** are missing from the prototype. Are there more rules?
7. **Transfers** (Phase 3): block a transfer order without an approved F19? Recommendation: yes, legacy warns.
8. **Holidays.** Recommendation: fixed dates seeded; HR-1 enters moving holidays every December.
