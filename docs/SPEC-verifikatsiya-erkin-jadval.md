# SPEC — Flexible work schedule («Erkin ish jadvali»), Verifix absence kinds, full request-type sheet (Verifikatsiya, wave 7)

> **Owner override (2026-10-20, supersedes every «clip» statement below — §2.2 row 3, §2.3 examples, §7 web footnote, §14 Q1):**
> on a flexible day presence is **NOT clipped** to the start–end window. All presence counts
> (break rules as today); the window is informational. Owner's example: 08:30–18:00, required 7:30,
> came 08:00, left 17:30 → no shortfall. The examples in §2.3 are re-pinned in the tests accordingly.


> Status: authoritative for this wave. Written 2026-10-20. Source: the owner
> compared the employee app (`app/`) request screens with the Verifix mobile app
> and decided «everything in the screenshots must exist» (§1). Every decision not
> taken by the owner is mine and is logged in `docs/ASSUMPTIONS.md` under
> `[2026-10-20]`; the owner may overturn any of them (§14).
>
> House rules — `SPEC-verifikatsiya.md` §0.2, unchanged: English identifiers,
> Uzbek code comments, Uzbek `davomat.*` error codes, Uzbek migration class name,
> all UI strings in 4 locales (`uz-Latn`, `uz-Cyrl`, `ru`, `en`) with typographic
> apostrophes in `uz-Latn` (`o‘`, `g‘` = U+2018, `’` = U+2019), **no `Guid = default`
> in a request record** (OpenAPI 500 trap). Work additively: extend the existing
> records, classes and screens; rewrite nothing.
>
> Read with: `SPEC-verifikatsiya.md` §5 (the day computation this wave amends),
> `SPEC-verifikatsiya-jadval-sorovi.md` §C (schedule change requests),
> `SPEC-verifikatsiya-jadval-tori.md` (year grid), `SPEC-verifikatsiya-sorovlar.md`
> §B1/§B3 (absence kinds, work-time credit), `INTAKE-verifix-ilova.md` §3.6/§8
> (the 8-row sheet), `INTAKE-verifix-web.md` §2–§6.

## Reference implementations — copy their shape

| What | Copy from |
|---|---|
| A per-day plan attribute flowing pattern → year cell → request day → `DayPlan` → `AttendanceDay` | the break window end to end: `WorkScheduleDay` / `WorkScheduleDate` / `ScheduleChangeDay`, `DayPlan.Working`, `PassPairing.Pair`, migration `20261017163149_JadvalSorovTanaffusi` |
| A new stored figure on `attendance_days` shown in timesheet, app and totals | `credited_minutes`: migration `20261017072546_IshVaqtiSorovi`, `AttendanceDayFacts.CreditedMinutes`, `TimesheetCellDto.CreditedMinutes` |
| Migration test | `api/tests/Bonvi.Api.IntegrationTests/IshVaqtiSorovMigrationTests.cs` |
| Computation tests | `api/tests/Bonvi.Application.Tests/Attendance/Computation/AttendanceScenario.cs` + `ScheduleOverrideTests.cs` |
| HR web form row with a checkbox that reveals fields | `ChangeRowEditor` in `web/src/features/verification/components/schedule-request-dialog.tsx` («Tanaffus») |
| App form block with a switch that reveals fields | `DayBlock` in `app/src/screens/schedule-request-new.tsx` («Tanaffus» `SwitchRow`) |

---

## 0. Goal

Some MBF employees work flexibly: they must put in N hours inside a window, not
arrive at a fixed minute. Today the engine can only judge a day by start/end
times (lateness, early leave), so these people would show false «Kechikdi» every
day. This wave adds the Verifix «Erkin ish jadvali» to schedules and to schedule
change requests, aligns the absence kinds with Verifix's two, and makes the
employee app's request-type sheet list Verifix's eight types in Verifix's order.

## 1. Owner decisions (2026-10-20) and scope

| # | Decision |
|---|---|
| O1 | **Flexible schedule** exists (a) on the work schedule itself — HR marks a day flexible, assigned permanently like any schedule — and (b) per requested date in the schedule change request, exactly like Verifix's form: start, end, optional break, checkbox «Erkin ish jadvali» with one value «Ish vaqti» (e.g. `7:30`). |
| O2 | **Absence kinds = exactly Verifix's two**: «Ishxona ishi bilan» (counts as work time) and «Shaxsiy». «Shaxsiy sabab» is renamed «Shaxsiy»; «Kasallik» is deactivated, not deleted; existing requests keep their kind. |
| O3 | **Request-type sheet lists all 8 Verifix types in Verifix's order.** This wave: the sheet, «Kun almashtirish» opens the existing schedule form with *Swap* preselected, «Jadval o‘zgartirish» with *Change* preselected. The five new types are only reserved (§9). The labels of rows 2–3 come from the owner (2026-10-20); `INTAKE-verifix-ilova.md` §8 had forbidden guessing them. |

**Out of scope:** forms/logic of Belgilash, Joylashuv so‘rovi, Qo‘shimcha ish
vaqti, Ishdan bo‘shatish, Ta’til (§9); cycle schedules; shortfall in MCP tools /
public API (§14 Q6); any change to fixed (non-flexible) days.

---

## 2. Flexible day — normative semantics

### 2.1 Definitions

* **Flexible day** — a *working* day whose effective plan (`DayPlan`, after the
  request → year cell → pattern precedence, §4) has `IsFlexible = true`.
* **Window W** = `[PlanStartAt, PlanEndAt]` — the day's start/end, built exactly as
  today (`DayPlan.Working`; crossing midnight allowed).
* **R** — required minutes («Ish vaqti»), stored per day (§3). On a flexible day
  `DayPlan.PlannedMinutes = R`.
* **LatestStartAt** = `PlanEndAt − R` (instant). Never earlier than `PlanStartAt`,
  because `R ≤ shift − break ≤ shift` (§2.5).

### 2.2 What changes, step by step

Columns: the step of `SPEC-verifikatsiya.md` §5; a fixed day (unchanged, today's
code path byte for byte); a flexible day.

| # | Step | Fixed day | Flexible day |
|---|---|---|---|
| 1 | §5.3 which day a pass belongs to (±6 h windows, late-exit rule) | as today | **same** (W is the plan window) |
| 2 | §5.5 steps 1–4: stamp, collapse repeats, infer `Unknown`, build intervals, `MissingEntry` / `MissingExit` | as today | **same**, from the real passes |
| 3 | **NEW step 4a — clip** | — | every closed interval `[a, b]` becomes `[max(a, PlanStartAt), min(b, PlanEndAt)]`; intervals with no positive length are dropped. All later steps read the clipped list |
| 4 | step 5 `worked_minutes` | Σ closed − `breakDeduction` | Σ **clipped** − `breakDeduction` (same truncation: ticks summed, truncated once) |
| 5 | §5.5.1 break (`outsideSpans`, `breakTaken`, `presentInWindow`, `breakDeduction`) | as today | **same formulas on the clipped list**. Outside spans = gaps between consecutive *clipped* intervals; the head before the first and the tail after the last are not outside spans (as today) |
| 6 | step 6 `planned_minutes` | shift − break | **R** |
| 7 | step 7 `first_in_at`, `last_out_at` | real first `In`, last `Out` | **same — real, unclipped** (a 07:00 arrival shows 07:00) |
| 8 | step 8 `late_minutes` | formula | **0** |
| 9 | step 9 `early_leave_minutes` | formula | **0** |
| 10 | **NEW `shortfall_minutes`** | 0 | `max(0, R − worked_minutes)` **only when** the final status (after every adjustment in `AttendanceDayBuilder.BuildDay`: credit `Absent→Present`, `Excused`, `Planned`) is `Present` **and** `now ≥ PlanEndAt`; otherwise 0. `worked_minutes` is the final value (credit included) |
| 11 | step 10 `overtime_minutes` | `max(0, worked − planned)` | **same formula** (planned = R, worked is clipped) |
| 12 | §5.6 status precedence | as today | **same** |
| 13 | not-started rule (`Planned`, no row; panel `NotStarted`) | `plan.Date > today ∨ (plan.Date = today ∧ now < PlanStartAt)` — unchanged | `plan.Date > today ∨ now < LatestStartAt` |
| 14 | work-time credit (`SPEC-verifikatsiya-sorovlar.md` §B3, `WorkCreditRule`) | as today | **same rule, no code change in `WorkCreditRule`**: it merges the credit with the *clipped* `PairingResult.ClosedIntervals` and its `room = PlannedMinutes − pairing.WorkedMinutes` becomes `R − worked` by itself |
| 15 | `Excused` (approved absence on an `Absent` day) | as today | **same** |
| 16 | daily panel + trend (`DashboardStatusRule.Classify`) | `OnTime` / `Late` by arrival vs `PlanStartAt + grace` | arrived ⇒ **`OnTime`**, `LateMinutes = null`, never `Late`; not arrived ⇒ step 13, then `Absent` / `Excused` as today |
| 17 | stored flags | `is_flexible = false`, `shortfall_minutes = 0` | `is_flexible = true` (snapshot of the plan used) |

Grace minutes (`grace_in/out_minutes`) are not used on a flexible day.

**Why the clip invariant still holds.** The break window must lie inside the
shift (`WorkScheduleDay.ValidateBreakWindow`, §2.4.0.1), i.e. inside W, so
`breakDeduction ≤ presentInWindow ≤ clipped sum` and `worked_minutes` can never
be negative.

**Where in code (normative).** `PassPairing.Pair`: when an `Out` closes
`[enteredAt, pass.At]` and `plan.IsFlexible`, compute `from = max(enteredAt,
PlanStartAt)`, `to = min(pass.At, PlanEndAt)`; if `to <= from` skip the interval
completely (no sum, no overlap, `leftAt` unchanged); otherwise use `from`/`to` in
place of `enteredAt`/`pass.At` for the outside-span overlap (`leftAt → from`),
`presentInWindow`, `closedTicks` and `closedIntervals`, then `leftAt = to`. On a
fixed day `from = enteredAt`, `to = pass.At` — today's behaviour exactly. The
`closeOpenAt` (22:00 auto-exit) path only runs without a plan and is untouched.
`AttendanceDayBuilder.BuildDay`: skip the late/early blocks when
`plan.IsFlexible`; compute shortfall last, after the status is final; fill
`AttendanceDayFacts.IsFlexible` / `ShortfallMinutes`.
`DashboardStatusRule.HasNotStarted` and `.Classify`: rows 13 and 16 — one rule
serves the timesheet (`Planned`), `attendance:started-shifts`, the panel and the
trend.

A row written before `PlanEndAt` keeps `shortfall_minutes = 0` until the next
recompute — the dirty queue on the next pass, or the nightly job, which covers
D, D+1, D+2 (the same contract as early leave, §5.5 step 9 AMENDED 2026-09-23).

### 2.3 Worked examples — normative, each pinned by a test (§10.2)

Base: flexible, W 08:30–18:00, break window 12:00–14:00, `break_minutes = 120`,
R = 450 (7:30, the default). `now` after 18:00 unless stated.

| # | Passes | clipped sum | breakTaken | presentInWindow | deduction | worked | shortfall | status / other |
|---|---|---|---|---|---|---|---|---|
| F1 | 08:30 in, 18:00 out | 570 | 0 | 120 | 120 | **450** | 0 | Present, late 0 |
| F2 | 10:00 in, 18:00 out | 480 | 0 | 120 | 120 | 360 | **90** | late 0 (a fixed day: late 80) |
| F3 | 08:30 in, 12:00 out, 14:00 in, 18:00 out | 450 | 120 | 0 | 0 | **450** | 0 | — |
| F4 | 07:00 in, 16:00 out | 450 ([08:30,16:00]) | 0 | 120 | 120 | 330 | **120** | `first_in_at` 07:00; early 0 (a fixed day: worked 420, early 110) |
| F5 | 09:00 in, 19:30 out | 540 ([09:00,18:00]) | 0 | 120 | 120 | 420 | 30 | overtime 0 — after 18:00 does not count |
| F6 | 08:30 in, 13:00 out, never back | 270 | 0 | 60 | 60 | 210 | 240 | tail is not an outside span |
| F7 | 18:30 in, 22:00 out (inside the +6 h attribution window) | 0 (dropped) | 0 | 0 | 0 | 0 | 450 | Present — presence outside W does not count |
| F8 | 09:00 in, no exit | 0 | 0 | 0 | 0 | 0 | **0** | MissingExit |
| F9 | none; today, now 10:00 | — | — | — | — | — | 0 | **Planned** (no row), panel NotStarted: 10:00 < LatestStartAt 10:30 |
| F10 | none; today, now 10:31 | — | — | — | — | — | 0 | **Absent** (row inserted by `attendance:started-shifts`), panel Absent |
| F11 | R = 240; 14:00 in, 18:00 out | 240 | 0 | 0 | 0 | 240 | 0 | LatestStartAt 14:00 |
| F12 | 14:00 in, 18:00 out + approved «Ishxona ishi bilan» 10:00–12:00 | merged 360 | 120 | 0 | 0 | 360 (credited 120) | 90 | Present |
| F13 | none + approved non-work-time absence for the day | — | — | — | — | 0 | 0 | Excused |
| F14 | night: W 20:00–08:00, break 0, R = 600; Mon 21:00 in, Tue 07:00 out | 600 | 0 | 0 | 0 | 600 | 0 | one row on Monday; LatestStartAt Mon 22:00 |
| F15 | night: W 20:00–08:00, R = 240; no passes; now Tue 01:00 | — | — | — | — | — | 0 | Planned / NotStarted (LatestStartAt Tue 04:00, although `plan.Date < today`) |
| F16 | same passes as F2 on a **fixed** day | 480 | 0 | 120 | 120 | 360 | 0 | late 80, early 0 — regression guard: fixed days unchanged |

Panel (row 16): F2 at 10:00 → `OnTime`, `LateMinutes = null`.

### 2.4 Edge cases

| Situation | Result |
|---|---|
| Flexible flag on a non-working day | silently cleared on write (`IsFlexible = false`, `RequiredMinutes = null`) — like a break window on a day off (§2.4.0) |
| `requiredMinutes` sent for a non-flexible day | ignored, stored NULL |
| Flexible day without a break window, `break_minutes = 60` | no deduction (as today); R default = shift − 60 |
| `DayOffWorked`, `NoSchedule`, `NotEmployed` | never flexible; unchanged |
| Approved request day over a flexible pattern | the request day replaces the whole plan: a non-flexible request day makes that date fixed, a flexible one makes it flexible (§4) |
| Swap involving a flexible date | the other date receives the flexible day with its R (server snapshot) |
| Pattern edited to flexible (`PUT /schedules/{id}`) | past days are **not** recomputed automatically (existing behaviour, endpoint description says so); HR runs `POST /recompute`. Year-grid edits and request decisions recompute as today |
| Row computed before `PlanEndAt` | `shortfall_minutes = 0` until the next recompute (§2.2) |
| Stored R larger than shift − break (only reachable by raw SQL or after an image rollback edited times) | used as stored; the editor shows the validation error on the next save. No clamp |

### 2.5 Required minutes («Ish vaqti») — default and limits

* `MaxRequired = shiftLength − breakMinutes`, where `shiftLength = (end − start)`
  as `TimeOnly` difference in whole minutes (1…1439, wraps midnight) and
  `breakMinutes` is the day's deducted break — for a request day the effective
  value of `ScheduleChangeRequest.BreakMinutes` (given `breakMinutes`, else the
  window length, else 0).
* **Default** — `isFlexible = true` with `requiredMinutes` null/omitted ⇒ the
  server stores `R = MaxRequired`. Examples: 08:30–18:00, 120 → **450 (7:30)**;
  09:00–18:00, 60 → 480; 20:00–08:00, 60 → 660; 08:00–20:00, 0 → 720.
* **Valid**: integer `1 ≤ R ≤ MaxRequired`. Otherwise 400
  **`davomat.jadval_erkin_vaqti`** — also when `MaxRequired < 1` (break ≥ shift:
  flexible impossible).
* **Validation order** (domain and both client mirrors, first failure wins):
  times empty → times equal → break length → half window → window outside
  shift → **flexible required**.

---

## 3. Data model

### 3.1 Columns

| Table | Column | Type | Null | DB default | Meaning |
|---|---|---|---|---|---|
| `work_schedule_days` | `is_flexible` | boolean | no | `false` | weekly pattern day is flexible |
| | `required_minutes` | integer | yes | — | R; non-null iff `is_flexible` |
| `work_schedule_dates` | `is_flexible` | boolean | no | `false` | year-grid cell |
| | `required_minutes` | integer | yes | — | |
| `schedule_change_request_days` | `is_flexible` | boolean | no | `false` | requested day (Change) or snapshot (Swap) |
| | `required_minutes` | integer | yes | — | |
| `attendance_days` | `is_flexible` | boolean | no | `false` | snapshot: the plan used was flexible |
| | `shortfall_minutes` | integer | no | `0` | §2.2 row 10 |

CHECK constraints (in the EF configurations, `HasCheckConstraint`, and in the
migration):

```sql
-- ck_work_schedule_days_flexible, ck_work_schedule_dates_flexible,
-- ck_schedule_change_request_days_flexible — same body
(NOT is_flexible AND required_minutes IS NULL)
OR (is_flexible AND required_minutes IS NOT NULL AND required_minutes BETWEEN 1 AND 1440)
```

* `is_working` is deliberately **not** in the CHECK: after an image rollback the
  old code may turn a flexible day into a day off and must not fail to save; the
  new code ignores flexibility on non-working days and clears it on the next
  write. The exact upper bound (shift − break) needs the midnight projection and
  stays in the domain — the same split as `ck_work_schedule_days_break_window`.
* `attendance_days` gets **no** CHECK (computed table, single writer
  `AttendanceDayRecomputer`; a validating scan of the big table is avoided).
* No new indexes.
* DB defaults exist **only in the migration** (EF's `AddColumn` default), never as
  `HasDefaultValue` in the model — rule 2 at the top of
  `AttendanceConfigurations.cs`. They stay in the database so a rolled-back
  image can still insert rows.

### 3.2 Domain and Application records (all appended parameters have defaults ⇒ existing callers compile)

| Type | Change |
|---|---|
| `WorkScheduleDayInput` (`Bonvi.Domain/Attendance/WorkSchedule.cs`) | append `bool IsFlexible = false, int? RequiredMinutes = null` |
| `WorkScheduleDay` | props `IsFlexible`, `RequiredMinutes`; `public const int MinRequiredMinutes = 1`; `public static int MaxRequiredMinutes(TimeOnly start, TimeOnly end, int breakMinutes)` (= `max(0, (end − start) in minutes − breakMinutes)`); `Validate` adds the §2.5 rule last; `Apply` sets `IsFlexible = IsWorking && input.IsFlexible`, `RequiredMinutes = IsFlexible ? input.RequiredMinutes ?? Max : null`, clears both on a day off |
| `WorkScheduleDateInput`, `WorkScheduleDate` | same two fields; `Validate` passes them to `WorkScheduleDay.Validate`; `Apply` as above |
| `ScheduleChangeDayInput` (`ScheduleChangeRequest.cs`) | append `bool IsFlexible = false, int? RequiredMinutes = null` after `BreakMinutes` |
| `ScheduleChangeRequest.Validate` | passes `IsFlexible` / `RequiredMinutes` into the `WorkScheduleDayInput` it already builds (with the effective `BreakMinutes(day)`) |
| `ScheduleChangeDay` | props; `Create` stores `IsFlexible` and R (explicit or `MaxRequiredMinutes(start, end, effective break)`) for working days only |
| `AttendanceDayFacts` | append `bool IsFlexible = false, int ShortfallMinutes = 0` after `CreditedMinutes` |
| `AttendanceDay` | props `IsFlexible`, `ShortfallMinutes`; `Recompute` copies them |
| `EmployeeDayOverride` (`Application/Attendance/Scheduling`) | append `bool IsFlexible = false, int? RequiredMinutes = null` after `DecidedAt` |
| Error | `davomat.jadval_erkin_vaqti`, `Error.Validation` (400), message «Erkin jadvalda ish vaqti 1 daqiqadan smena − tanaffusgacha bo'lishi kerak.» |

### 3.3 Migration `ErkinJadval`

`./bonvi migrate ErkinJadval`. Up: `SET lock_timeout = '3s';` → 8 × `AddColumn`
(booleans `NOT NULL DEFAULT false`, `shortfall_minutes NOT NULL DEFAULT 0`,
`required_minutes` nullable) → 3 × `AddCheckConstraint`. **No `UPDATE`**: every
existing row becomes non-flexible with shortfall 0, i.e. behaves exactly as
before. Constant defaults are metadata-only in PostgreSQL 11+ (prod runs 18).
Down: drop the 3 CHECKs and 8 columns. Uzbek `<summary>` like the reference
migration. **Read the generated file by hand:** only these ADD COLUMN / ADD
CONSTRAINT operations; no DROP, no default changes on other columns.

---

## 4. Plan resolution and the request override layer

* `DayPlan` (`Application/Attendance/Scheduling/DayPlan.cs`): new
  `bool IsFlexible`, `DateTimeOffset? LatestStartAt` (= `PlanEndAt − PlannedMinutes`
  when flexible, else `PlanStartAt`). `Working(...)` gains `bool isFlexible,
  int? requiredMinutes`; `PlannedMinutes = isFlexible ? (requiredMinutes ??
  max(0, shift − break)) : max(0, shift − break)`. `For` passes the fields from
  `exact` (year cell) or the pattern `day`; `ForOverride` from the override.
* `ScheduleOverrideQuery.ApprovedAsync` projects `day.IsFlexible`,
  `day.RequiredMinutes` into `EmployeeDayOverride`.
* `ScheduleResolver` — **no logic change.** Precedence stays: approved request
  day > year-grid cell > weekly pattern; no schedule ⇒ no override (NoSchedule
  stays). The override **replaces** the day; flexibility is part of the day and is
  never merged from the layer below.
* Swap snapshot (`ScheduleChangeDays.BuildAsync` → local `Take`): copies the
  other date's `IsFlexible` / `RequiredMinutes` (`DayPlanDto`).
* `EmployeePlanReader.ReadAsync` → `DayPlanDto.IsFlexible = plan.IsFlexible`,
  `RequiredMinutes = plan.IsFlexible ? plan.PlannedMinutes : null`.
* `ResolveScheduleQuery.ResolvedDay` (request override branch) and
  `WorkScheduleMapper.ToDto` (pattern day, year cell) fill the new
  `WorkScheduleDayDto` fields.
* `GetScheduleYearQueryHandler`: flags/R of an explicit date come only from that
  date, otherwise from the pattern (the existing «times from the cell only» rule);
  `plannedMinutes` = `plan.PlannedMinutes` (R on flexible cells).
* `FillScheduleYearCommandHandler`: copies the pattern day's `IsFlexible` /
  `RequiredMinutes` into every new cell (the plan stays identical ⇒ still no
  recompute).
* Recompute triggers: unchanged (year grid → `attendance:schedule-dates` ±1;
  decide/cancel → inline ±1; weekly pattern edit → manual `POST /recompute`).

---

## 5. API contract

No new endpoints, no new permissions; every endpoint below keeps its policy.
Enums travel by name. New request fields are optional with defaults, so an old
client keeps working unchanged (non-flexible).

### 5.1 HR — schedules (`/api/attendance`)

| Method · path | Permission | Change |
|---|---|---|
| `POST /schedules`, `PUT /schedules/{id}` | `attendance.manage` | `days[]` (`WorkScheduleDayRequest`) gain `isFlexible: bool = false`, `requiredMinutes: int? = null`. New 400 `davomat.jadval_erkin_vaqti` (add to both `WithDescription` error lists) |
| `GET /schedules`, `GET /schedules/{id}` | `attendance.view` | `WorkScheduleDayDto` gains `isFlexible: bool`, `requiredMinutes: int?` (non-null iff `isFlexible`) |
| `GET /schedules/{id}/year` | `attendance.view` | `ScheduleYearDayDto` gains `isFlexible`, `requiredMinutes`; `plannedMinutes` = R on flexible cells |
| `PUT /schedules/{id}/dates` | `attendance.manage` | `ScheduleDatesBody` and `SetScheduleDatesCommand` gain `isFlexible = false`, `requiredMinutes = null`; ignored with `reset: true` |
| `POST /schedules/{id}/year/fill` | `attendance.manage` | no body change; copies flexibility (§4) |
| `GET /schedules/resolve` | `attendance.view` | `day` carries the flexible fields of the effective day (incl. `daySource = Request`) |

```json
{ "dayIndex": 1, "isWorking": true, "startTime": "08:30", "endTime": "18:00",
  "breakMinutes": 120, "breakStart": "12:00", "breakEnd": "14:00",
  "isFlexible": true, "requiredMinutes": 450 }
```

### 5.2 Schedule change requests

| Method · path | Auth | Change |
|---|---|---|
| `POST /api/attendance/schedule-requests`, `PUT /api/attendance/schedule-requests/{id}` | `attendance.requests.manage` | `days[]` (`ScheduleChangeDayBody`) gain `isFlexible = false`, `requiredMinutes = null`; `ScheduleChangeRequestBody.DayInputs()` passes them. `swaps` unchanged (server snapshots flexibility). Add `davomat.jadval_erkin_vaqti` to the `ErrorCodes` description constant |
| `GET /api/attendance/schedule-requests`, `/{id}` | `attendance.view` | `ScheduleChangeDayDto` gains `isFlexible: bool`, `requiredMinutes: int?` |
| `POST /api/me/schedule-requests` | employee token | same day fields (mapping in `MeEndpoints.MapScheduleRequests`); date window and limits unchanged |
| `GET /api/me/schedule-requests` | employee token | DTO as above |
| `GET /api/me/plan?date=` | employee token | `DayPlanDto` gains `isFlexible: bool = false`, `requiredMinutes: int? = null` |

Body limit 32 KB unchanged.

### 5.3 Attendance read endpoints

| Method · path | Permission | Change |
|---|---|---|
| `GET /timesheet`, `GET /export/timesheet` | `attendance.view` (+ `reports.export`) | `TimesheetCellDto` appends `isFlexible: bool = false`, `plannedMinutes: int = 0`, `shortfallMinutes: int = 0` (from the stored row; synthesized cells false/0/0). `TimesheetTotalsDto` appends `shortfallMinutes` (Σ cells) |
| `GET /api/me/attendance` | employee token | same cell/totals DTOs |
| `GET /reports/exceptions`, `GET /export/exceptions` | `attendance.view` (+ `reports.export`) | `kind=Shortfall` — new `ExceptionKind.Shortfall = 5`: stored days with `shortfall_minutes >= minMinutes` (default 1); `sort=Minutes` sorts by shortfall. `AttendanceExceptionDto` appends `shortfallMinutes: int = 0` |
| `GET /reports/summary`, `GET /export/summary` | same | `AttendanceSummaryDto` appends `shortfallMinutes` (Σ) |
| Board, dashboard, trend | `attendance.view` | DTOs unchanged — flexible arrivals count as `OnTime` |

**Excel** (`AttendanceExcelWriter`) — new columns **appended at the end** of each
sheet, existing positions unchanged: «Kunlar» + «Erkin jadval» («Ha» / empty),
«Kam ishlangan (daq.)», «Kam ishlangan (soat:daq)»; «Tabel» totals, exceptions and
summary sheets + «Kam ishlangan (daq.)», «Kam ishlangan (soat:daq)». Exceptions
sheet name for `Shortfall`: «Kam ishlaganlar».

### 5.4 Errors

One new code, `davomat.jadval_erkin_vaqti` (400). Translations: web 4 locales
(`./bonvi check` fails without them) and app 4 locales (employee wording, §7.3).

---

## 6. Absence kinds = Verifix's two

### 6.1 Target state

| Name | `counts_as_work_time` | `employee_can_request` | `is_active` |
|---|---|---|---|
| Ishxona ishi bilan | true | true | true |
| Shaxsiy | false | true | true |
| Kasallik (existing DBs only) | unchanged | unchanged | **false** |

Verifix maps «Ишхона иши билан» to «Рабочая встреча» under «Явка» (= our flag) and
«Шахсий» to «Почасовой отгул» (not work time) — `INTAKE-verifix-web.md` §3.
«Exactly two» is a data state, not a code rule: HR may add kinds later.

### 6.2 Seed

`api/src/Bonvi.Infrastructure/Persistence/Seed/data/absence-types.json` becomes
exactly:

```json
[
  { "name": "Ishxona ishi bilan", "countsAsWorkTime": true },
  { "name": "Shaxsiy" }
]
```

`CatalogSeeder` fills a catalog **only when it is empty**, so this changes fresh
dev/test databases only — never production.

### 6.3 Production (and any existing dev DB): in-app catalog edit — no migration, no SQL

After the release deploy, the owner (or an HR user with `catalog.manage`) in HR web
«Ma’lumotnomalar → Yo‘qlik turlari»:

1. «Shaxsiy sabab» → edit → name «Shaxsiy» → save (flags untouched). Its existing
   requests point at the same row and now read «Shaxsiy».
2. «Kasallik» → «Faolsizlantirish», reason «Verifix bilan moslash (egasi,
   2026-10-20)». Not deleted.

If step 1 returns 409 `malumotnoma.nom_band` (a «Shaxsiy» already exists): stop,
merge nothing, report to the owner. Why the UI and not a migration: catalog rows
are data (`CatalogSeeder` doc comment), the UI path is audited (`/audit`) and is
reversible from the same screen. The release is not signed off until this
read-only query shows §6.1:

```sql
SELECT name, is_active, counts_as_work_time, employee_can_request
FROM absence_types WHERE deleted_at IS NULL ORDER BY name;
-- and: SELECT count(*) FROM absence_types WHERE is_active AND deleted_at IS NULL;  -- = 2
```

### 6.4 Behaviour afterwards (existing rules, no code)

* The app (`GET /api/me`) and the HR create/edit dialog list active kinds only.
* «Kasallik» requests keep kind and name; they stay listed and filterable (the HR
  filter lists inactive kinds too); decide/cancel work; editing a pending one
  requires switching the kind (`davomat.yoqlik_turi_faolsiz`).
* Nothing is recomputed (no flag changed).

### 6.5 Files that name the old kinds

| File | Action |
|---|---|
| `api/src/Bonvi.Infrastructure/Persistence/Seed/data/absence-types.json` | §6.2 |
| `api/tests/Bonvi.Api.IntegrationTests/CatalogPersistenceTests.cs` → `Yoqlik_turlari_urugdan_toldiriladi` | assert contains «Ishxona ishi bilan» (with `CountsAsWorkTime = true`) and «Shaxsiy»; assert does **not** contain «Kasallik» nor «Shaxsiy sabab» (no exact-count assertion — other tests may add kinds) |
| `api/src/Bonvi.Domain/Catalog/SimpleCatalogs.cs` (`AbsenceType` summary, `CountsAsWorkTime` remark) | comment text: «Ishxona ishi bilan», «Shaxsiy» |
| `app/src/mock/data.ts` (`MOCK_ABSENCE_TYPES`, `buildRequests`) | exactly `at-1` «Ishxona ishi bilan», `at-2` «Shaxsiy»; requests on `at-2`/`at-4`/`at-5` → `at-2` «Shaxsiy» |
| `api/tests/Bonvi.Api.IntegrationTests/IshVaqtiSorovMigrationTests.cs` | **leave** — the names are raw SQL fixtures of an old migration test |
| `docs/SPEC-verifikatsiya-sorovlar.md` §B1, §B3 | one-line «AMENDED 2026-10-20 → SPEC-verifikatsiya-erkin-jadval.md §6» |
| `docs/UX-xodim-ilovasi.md` §A.1 sheet row, §E.8 icon list | amendment note: sheet has 8 rows (§7.2), kinds are two |
| `docs/DEPLOY-CHECKLIST.md` | history stays; the new release entry carries §6.3 and §11 |

---

## 7. UI

All screens keep the project's iOS-HIG look: only existing primitives —
`web/src/shared/ui` (`Checkbox`, `TextField`, `Badge`, …) and `app/src/ui`
(`Card`, `Field`, `SwitchRow`, `TimeRow`, `Sheet`, …) plus the one new app
primitive `DurationRow` (§7.2). No new colours, radii or type styles.

### 7.1 HR web (`web/src/features/verification`) — types from `./bonvi gen`

**Rules mirror** — `lib/attendance-schedule-rules.ts`: `MIN_REQUIRED_MINUTES = 1`;
`maxRequiredMinutes(day) = max(0, shiftSpanMinutes(day) − day.breakMinutes)`;
`validateScheduleDay` gets the §2.5 rule last (`'davomat.jadval_erkin_vaqti'`);
`plannedDayMinutes` returns R on a flexible day (`requiredMinutes ?? max`);
`parseDuration('7:30') → 450` accepting `^\d{1,2}:[0-5]\d$` with hours ≤ 23
(else client message `attendance.durationFormat`). `lib/attendance-presentation.ts`
`weekSummary.plannedMinutes = max(0, Σshift(fixed) − Σbreak(fixed)) + ΣR(flexible)`
(fixed-only schedules give exactly today's number).

**Duration field behaviour (all three HR forms).** Draft holds `isFlexible`,
`required` (text `H:MM`) and `requiredAuto`. While `requiredAuto`, the field shows
`formatDuration(max)` and follows every change of times/break; typing sets
`requiredAuto = false`; typing exactly the current max sets it back to `true`.
Loading a stored day: `requiredAuto = (requiredMinutes === max(day))`. Sent:
`isFlexible = isWorking && isFlexible`, `requiredMinutes = isFlexible &&
!requiredAuto ? parseDuration(required) : null` (null ⇒ server default).

| Screen / file | Change |
|---|---|
| Weekly editor — `components/attendance-schedule-form-dialog.tsx` `DayEditor` (working day), after the break-window text line, before the error line | (1) `Checkbox` «Erkin ish jadvali»; (2) when checked: `TextField` «Ish vaqti» (`inputMode="numeric"`, placeholder `7:30`, `hint` «soat:daqiqa · ko‘pi bilan {{max}}», `error` = the client/server code message) and a footnote «Kechikish va erta ketish hisoblanmaydi. Kun davomidagi butun vaqt hisoblanadi; normadan kami «Kam ishlangan» bo‘lib ko‘rinadi.» Day caption: flexible → «Erkin · {{hours}}», else the existing «Reja: …». `DayDraft`, `draftsOf`, `toDayInput`, `toggleDay` template fill and «Dushanbani … nusxalash» carry the three fields |
| Year grid — `components/attendance-schedule-year-dialog.tsx` | edit panel (`CellDraft` + the three fields): same two controls after the break fields; `patternDraft`, `cellDraft`, `toDayInput` and the `PUT /dates` body carry them. Cell label unchanged (hours of `plannedMinutes` = R); tooltip adds «Erkin jadval · reja {{hours}}». Read-only mode unchanged |
| Schedules list — `components/attendance-schedules-page.tsx` `ScheduleCard` | `Badge size="sm"` «Erkin jadval» next to the name when any working day is flexible; summary via the new `weekSummary` |
| HR request dialog — `components/schedule-request-dialog.tsx` `ChangeRowEditor` | after the break block: `Checkbox` «Erkin ish jadvali» + the «Ish vaqti» field. Prefill from `/schedules/resolve` `day.isFlexible` / `day.requiredMinutes` under the existing `touched`/`filledFor` rules; edit mode loads the request's day. Swap rows unchanged |
| Request text — `lib/schedule-request-text.ts` | `DayPlanLike` + `isFlexible`, `requiredMinutes`; `plan()` appends `, erkin {{hours}}` on flexible days ⇒ the queue list, the decision dialog and the employee-card list show «16.10 · 08:30–18:00, tanaffus 12:00–14:00, erkin 7:30» |
| Timesheet — `components/attendance-timesheet-grid.tsx` | `DayCell`: orange bar (`LATE_BAR_CLASS`) when `lateMinutes > 0 \|\| shortfallMinutes > 0`. `CellDetails` on a flexible cell: facts «Kechikish»/«Erta ketish» replaced by «Reja» (`formatDuration(plannedMinutes)`) and «Kam ishlangan» (orange when > 0), plus a line «Erkin jadval: {{range}} oralig‘idagi vaqt hisoblanadi» (range = plan start–end). `TotalsDetails`: + «Kam ishlangan». Legend: bar label → «Kechikish yoki kam ishlangan» |
| Employee card → Davomat — `components/employee-attendance-tab.tsx` | same cell rules: bar, plan line «Erkin · 7:30 (08:30–18:00)», «Kam ishlangan N daq.» instead of late/early lines on flexible days; totals stat «Kam ishlangan» |
| Reports — `components/attendance-reports-page.tsx`, `lib/attendance-presentation.ts` (`REPORT_KINDS`, `REPORT_KIND_LOOK`), `lib/attendance-search.ts` | new tab «Kam ishlaganlar» (`Shortfall`, tone `warning`, lucide `Hourglass`) between «Erta ketganlar» and «Kelmaganlar»; its table shows a «Kam ishlangan» minutes column in place of late/early; min-minutes filter and minutes sort as for Late/EarlyLeave. Summary table: «Kam ishlangan» duration column after «Erta ketish» |

Permission gating unchanged: editors, the year panel and the request dialog
render only for holders of `attendance.manage` / `attendance.requests.manage`
exactly as today; read views under `attendance.view`.

### 7.2 Employee app (`app/src`) — hand-written contract in `api/types.ts`

**Types** (`api/types.ts`): `TimesheetCellDto` + `isFlexible?: boolean`,
`plannedMinutes?: number`, `shortfallMinutes?: number` (optional, like
`creditedMinutes` — an older server omits them); `TimesheetTotalsDto` +
`shortfallMinutes?: number`; `ScheduleChangeDayDto` and `DayPlanDto` +
`isFlexible?: boolean`, `requiredMinutes?: number | null` (read side, optional for
the same reason; absent ⇒ not flexible); `ScheduleChangeDayInput` + `isFlexible:
boolean`, `requiredMinutes: number | null` (always sent).

**Request-type sheet** — `screens/requests.tsx` `RequestTypeSheet`: exactly these
8 rows, this order (`INTAKE-verifix-ilova.md` §8):

| # | Label (uz-Latn) | Icon (`ui/icon.tsx`) | Tap |
|---|---|---|---|
| 1 | Ish joyida yo‘qlik | `person-running` (new) | `/sorovlar/yangi` |
| 2 | Kun almashtirish | `arrows-right-left` (new) | `/sorovlar/jadval/yangi?tur=almashtirish` |
| 3 | Jadval o‘zgartirish | `calendar-pencil` (new) | `/sorovlar/jadval/yangi?tur=ozgartirish` |
| 4 | Belgilash | `person-walking-arrows` (new) | disabled |
| 5 | Joylashuv so‘rovi | `map-pin` (existing) | disabled |
| 6 | Qo‘shimcha ish vaqti | `briefcase-clock` (new) | disabled |
| 7 | Ishdan bo‘shatish | `user-minus` (new) | disabled |
| 8 | Ta’til | `beach-umbrella` (new) | disabled |

Row: min height 52 px, icon 24 px `text-fg-secondary`, label `text-body`, chevron
on rows 1–3. Rows 4–8: `<button disabled aria-disabled="true">`, icon and label
`text-fg-tertiary`, trailing «Tez orada» (`text-footnote text-fg-tertiary`), no
chevron, no pressed state. **No description lines** — delete
`requests.typeAbsenceText` / `requests.typeScheduleText` in all 4 locales. Title
«So‘rov turini tanlang» and the «Yopish» button unchanged. New icons: outline,
24×24 viewBox, stroke 1.5, round caps (UX §E.8 family); update the icon-count
comment in `icon.tsx`.

**Schedule form** — `screens/schedule-request-new.tsx`:

* initial `kind` from `?tur=`: `almashtirish` → `Swap`, anything else / absent →
  `Change`; the segmented control stays; `?kun=` prefill unchanged;
* title follows `kind`: Swap «Kun almashtirish» (`schedule.newTitleSwap`), Change
  «Jadval o‘zgartirish» (`schedule.newTitle`, value changed);
* `DayBlock`, working day, after the break rows and the `breakKept` note:
  `SwitchRow` «Erkin ish jadvali»; when on: `Field` «Ish vaqti» (required) with
  `DurationRow`, helper «{{range}} oralig‘ida · ko‘pi bilan {{max}}»;
* `DayDraft` + `isFlexible`, `requiredMinutes: number | null` (null = auto).
  `applyPlan`: first fill times/break from the plan as today, then
  `isFlexible = plan.isFlexible` and `requiredMinutes = plan.isFlexible &&
  plan.requiredMinutes !== defaultRequired(filledDraft) ? plan.requiredMinutes :
  null`;
* `defaultRequired(draft) = forward(start, end) − (draft.breakMinutes ?? (hasBreak
  ? breakWindowMinutes(breakStart, breakEnd) : 0))` — the server's formula
  (§2.5); the row shows `requiredMinutes ?? defaultRequired`; picking a value
  equal to the default stores `null`;
* `dayErrors` + `required`: flexible and value outside `1…default` →
  «Ish vaqti 0:01 dan {{max}} gacha bo‘lishi kerak.» (`data-invalid`, so
  `scrollToFirstError` finds it);
* `toInput`: `isFlexible: isWorking && isFlexible`, `requiredMinutes: isWorking &&
  isFlexible ? requiredMinutes : null`.

**`DurationRow`** (new, `ui/form.tsx`, next to `TimeRow`): the `TimeRow` row look
(h-12 button, `nums` value «7:30», trailing `clock` icon); opens a `Sheet` with two
native `<select>` — hours 0–23 and minutes 00–59 — and the same «Tasdiqlash» /
«Bekor qilish» buttons; value in minutes. Not `input type="time"` (12-hour locales
would render «7:30 AM»).

**Other app screens**

| File | Change |
|---|---|
| `data/schedule-requests.ts` | `hoursMinutes(450) → '7:30'`; `dayPlanText` appends `, erkin 7:30` (`schedule.withFlexible`) on flexible days — used by list lines and the form's «Hozirgi jadval» helper |
| `screens/schedule-request-detail.tsx` | day row: extra footnote «Erkin ish jadvali · 7:30» (`schedule.flexibleLine`) |
| `ui/day-panel.tsx` | row «Kam ishlangan» (`format.duration`) when `shortfallMinutes > 0` |
| `screens/calendar.tsx` «Ish jadvali» card | flexible: «Erkin ish jadvali · reja {{hours}}» + «{{range}} oralig‘ida» instead of «Ish kuni {{range}}»; «Oylik natija» row «Kam ishlangan vaqt» when totals `shortfallMinutes > 0` |
| `screens/home.tsx` subtitle | flexible: «Erkin jadval {{hours}} · {{range}}» (`home.workdayFlexible`) |
| `screens/checkin-card.tsx` hint | flexible: «Erkin jadval: {{range}} oralig‘ida {{hours}}» (`checkin.hintFlexible`) |
| `data/attendance.ts` | `MonthTotals.shortfallMinutes` from server totals |
| `screens/request-new.tsx` | title value `requests.newTitle` → «Ish joyida yo‘qlik so‘rovi» |
| `mock/data.ts`, `mock/index.ts` | §6.5 kinds; plan/request mocks carry `isFlexible: false, requiredMinutes: null`; create echoes the fields; one past mock day flexible, `Present`, `plannedMinutes 450`, `shortfallMinutes 25` |

### 7.3 i18n — `uz-Latn` values (uz-Cyrl = transliteration, ru/en = translation)

**Web** (`web/src/shared/i18n/locales/*.json`, `attendance.*` unless stated):
`flexible` «Erkin ish jadvali» · `flexibleRequired` «Ish vaqti» ·
`flexibleRequiredHelper` «soat:daqiqa · ko‘pi bilan {{max}}» · `flexibleHint` (text
above) · `flexibleBadge` «Erkin jadval» · `dayPlannedFlexible` «Erkin · {{hours}}» ·
`durationFormat` «Vaqtni soat:daqiqa ko‘rinishida yozing, masalan 7:30.» ·
`scheduleRequestFlexible` «erkin {{hours}}» · `planned` «Reja» ·
`shortfallMinutes` «Kam ishlangan» · `flexibleCellLine` «Erkin jadval: {{range}}
oralig‘idagi vaqt hisoblanadi» · `lateOrShortfall` «Kechikish yoki kam ishlangan» ·
`kindShortfall` «Kam ishlaganlar» · `yearCellFlexible` «Erkin jadval · reja
{{hours}}» · `planFlexibleLine` «Erkin · {{hours}} ({{range}})» (employee card) · `errors` → `davomat.jadval_erkin_vaqti` «Erkin jadvalda ish vaqti
noldan katta va smena uzunligidan tanaffus ayirilgandan oshmasligi kerak.» Reuse a
key if it already exists with the same text.

**App** (`app/src/i18n/locales/*.json`): `requests.typeAbsence` «Ish joyida yo‘qlik»
(changed) · `requests.typeSwap` «Kun almashtirish» · `requests.typeSchedule` «Jadval
o‘zgartirish» (changed) · `requests.typeMark` «Belgilash» · `requests.typeLocation`
«Joylashuv so‘rovi» · `requests.typeOvertime` «Qo‘shimcha ish vaqti» ·
`requests.typeResignation` «Ishdan bo‘shatish» · `requests.typeVacation` «Ta’til» ·
`requests.soon` «Tez orada» · `requests.newTitle` «Ish joyida yo‘qlik so‘rovi»
(changed) · `schedule.newTitle` «Jadval o‘zgartirish» (changed) ·
`schedule.newTitleSwap` «Kun almashtirish» · `schedule.flexible` «Erkin ish
jadvali» · `schedule.required` «Ish vaqti» · `schedule.requiredHint` «{{range}}
oralig‘ida · ko‘pi bilan {{max}}» · `schedule.requiredRange` «Ish vaqti 0:01 dan
{{max}} gacha bo‘lishi kerak.» · `schedule.withFlexible` «{{plan}}, erkin {{hours}}» ·
`schedule.flexibleLine` «Erkin ish jadvali · {{hours}}» · `schedule.durationHours`
«Soat» · `schedule.durationMinutes` «Daqiqa» · `calendar.shortfall` «Kam ishlangan» ·
`calendar.scheduleFlexible` «Erkin ish jadvali · reja {{hours}}» ·
`calendar.scheduleFlexibleRange` «{{range}} oralig‘ida» · `calendar.totalShortfall`
«Kam ishlangan vaqt» · `home.workdayFlexible` «Erkin jadval {{hours}} · {{range}}» ·
`checkin.hintFlexible` «Erkin jadval: {{range}} oralig‘ida {{hours}}» · error
`davomat.jadval_erkin_vaqti` «Ish vaqti noldan katta va tanaffussiz ish vaqti
oralig‘idan oshmasligi kerak.» Removed: `requests.typeAbsenceText`,
`requests.typeScheduleText`.

---

## 8. Request-type sheet — the full Verifix order (summary of O3)

Rows 1–3 are live (§7.2); rows 4–8 render disabled with «Tez orada» until their
forms are specified (§9). The schedule form is one screen for rows 2 and 3; the
row only preselects the kind and the title.

## 9. Pending: new request types (no design in this wave)

The owner will send screenshots of these forms. Until then: a disabled sheet row
and nothing else — no route, no endpoint, no table.

| # | Type | What is known | Source |
|---|---|---|---|
| 4 | **Belgilash** (mark request — «forgot to mark») | Verifix web «Запросы на отметки»: columns employee, photo, department, position, location, time, note, **mark type Приход / Уход**, status; 16 rows in MBF, mostly old. Approval presumably adds an In/Out at the given time — unconfirmed | `INTAKE-verifix-web.md` §1, §5; owner «not now» 2026-10-17 |
| 5 | **Joylashuv so‘rovi** (location request) | listed as «Запросы на локацию» in Verifix web; not reviewed | `INTAKE-verifix-web.md` §1 |
| 6 | **Qo‘shimcha ish vaqti** (overtime request) | «Запросы на сверхурочные»: **0 rows** in MBF; Verifix time kind «Сверхурочные → по запросу». We compute overtime but never price it (SPEC-verifikatsiya §1.2 #2) | `INTAKE-verifix-web.md` §1, §3 |
| 7 | **Ishdan bo‘shatish** (resignation request) | not seen in Verifix web. In BonviHR, termination is an HR employment action; an employee request would only start it | — |
| 8 | **Ta’til** (vacation) | Verifix time kinds «Отпуск», «Неоплачиваемый отпуск»; absence kinds carry «Является ежегодным / Количество дней в год» (unused in MBF). No vacation module in BonviHR | `INTAKE-verifix-web.md` §3 |

Naming note: «Belgilash» already means the phone check-in in the app vocabulary
(UX §F.7) — the future form title will be «Belgilash so‘rovi» (§14 Q7).

---

## 10. Tests

All backend tests run with `./bonvi test`. Test names and comments follow the
project style (Uzbek names, e.g. `Erkin_kunda_kechikish_hisoblanmaydi`).

### 10.1 Domain — `api/tests/Bonvi.Domain.Tests/Attendance/WorkScheduleFlexibleTests.cs` (new)

* Default R stored when null: 08:30–18:00/120 → 450; 20:00–08:00/60 → 660.
* Explicit R kept; R = 0, R = max + 1, break ≥ shift ⇒ `davomat.jadval_erkin_vaqti`.
* Day off with `IsFlexible = true` ⇒ saved as non-flexible, R null; non-flexible
  with R ⇒ R null.
* Order: a bad break window reports `davomat.jadval_tanaffus_oynasi`, not the flexible code.
* `WorkScheduleDate.Create/Update` — same rules.
* `ScheduleChangeRequestTests.cs` (+cases): Change day flexible, window 12:00–14:00,
  `breakMinutes` null ⇒ R 450; window 12:00–15:00 with `breakMinutes` 60 on
  09:00–18:00 ⇒ R 480.

### 10.2 Computation — `api/tests/Bonvi.Application.Tests/Attendance/Computation/FlexibleDayTests.cs` (new)

Extend `AttendanceScenario` with `FlexibleOffice(...)` (schedule whose days are
flexible) and an `Override(..., isFlexible, requiredMinutes)` overload. One test
per row F1–F16 of §2.3 asserting every listed figure, plus:
`DashboardStatusRuleTests.cs` (flexible arrival 11:00 → `OnTime`/null; before and
after `LatestStartAt`; excused; F15 night case), `DayPlanTests.cs`
(`PlannedMinutes = R`, `LatestStartAt` for pattern, year cell and override),
`ScheduleResolverTests.cs` (flexible override over fixed pattern, fixed override
over flexible pattern, flexible year cell over fixed pattern). Existing tests stay
untouched and green — they are the fixed-day regression suite.

### 10.3 API / RBAC — `api/tests/Bonvi.Api.IntegrationTests/FlexibleScheduleApiTests.cs` (new)

* `POST /schedules` flexible day with `requiredMinutes` null → 200, DTO echoes
  `isFlexible = true`, `requiredMinutes = 450`; out-of-range → 400 with the code.
* `PUT /schedules/{id}/dates` flexible → `GET …/year` shows it; `year/fill` copies it;
  `GET /schedules/resolve` returns it.
* HR `POST /schedule-requests` Change flexible → DTO fields; Swap where one date is
  flexible → the swapped day carries `isFlexible`/R; approve → the stored day has
  `is_flexible = true`, `late_minutes = 0`, shortfall per §2.2.
* App: `GET /api/me/plan` on a flexible date → fields; `POST /api/me/schedule-requests`
  flexible → 200.
* Timesheet / `GET /api/me/attendance` cell + totals fields; `reports/exceptions?kind=Shortfall`;
  summary `shortfallMinutes`; Excel headers appended at the end (positions of the
  existing columns asserted unchanged).
* RBAC regression on the changed bodies: `attendance.view`-only → 403 on
  `POST/PUT /schedules`, `PUT /schedules/{id}/dates`, `POST /schedule-requests`;
  anonymous → 401 on `reports/exceptions?kind=Shortfall`; HR token on
  `POST /api/me/schedule-requests` → 401 (employee policy).
* `CatalogPersistenceTests` per §6.5. `OpenApiDocumentTests` stays green.

### 10.4 Migration — `api/tests/Bonvi.Api.IntegrationTests/ErkinJadvalMigrationTests.cs` (new)

Mirror `IshVaqtiSorovMigrationTests`: migrate to `20261017163149_JadvalSorovTanaffusi`,
insert a schedule + 7 days, a year cell, a request + day and an attendance day by
SQL; Up ⇒ new columns false/NULL/0 and every old column unchanged; an «old image»
insert without the new columns succeeds in all four tables; CHECKs reject
(`true`, NULL), (`false`, 450), (`true`, 0) and **accept** (`true`, 450,
`is_working = false`); Down ⇒ columns and CHECKs gone, row counts unchanged.

### 10.5 Frontend

No frontend test runner exists. Gates: web `./bonvi pnpm typecheck`,
`./bonvi pnpm build`, `./bonvi check`; app `docker compose exec app sh -c "pnpm
typecheck && pnpm check:locales && pnpm build"`. Manual pass in the browser (dev):
a flexible weekly day, a flexible year cell, an HR flexible request, the app
sheet (8 rows, 5 disabled), the app flexible request, a flexible timesheet cell.

---

## 11. Rollout

* **Impact today: none.** As of 2026-10-17 production had no schedule assigned;
  new columns default to non-flexible. Re-check before deploy:
  `SELECT count(*) FROM schedule_assignments WHERE deleted_at IS NULL;` and
  `SELECT count(*) FROM work_schedule_days WHERE is_flexible;` (after migration: 0).
* Usual safe-deploy procedure as recorded in `docs/DEPLOY-CHECKLIST.md`: rollback
  image tag, backup, migration tried on a prod copy (Up, second Up, Down, Up),
  then deploy while HR is idle.
* After deploy: §6.3 catalog edit + verification query; app sheet shows 8 rows.
* **Image rollback without Down works**: old code ignores the new columns, DB
  defaults keep its inserts valid, the CHECK has no `is_working` clause. While
  rolled back, flexible days compute as fixed days; `attendance_days.is_flexible` /
  `shortfall_minutes` may be stale (old code never reads them) until the next
  forward recompute. **Down** deletes every flexible setting — use only if none
  exist or the owner accepts the loss. The catalog rename is reverted from the same UI.

---

## 12. Work units

**Shared files — one author at a time, never parallel:**
`api/src/Bonvi.Application/Attendance/Dtos/AttendanceDtos.cs` and
`ScheduleChangeDtos.cs` (B4 only) · `AttendanceConfigurations.cs` + the migration +
`AppDbContextModelSnapshot.cs` (B2 only) · `Api/Endpoints/AttendanceEndpoints.cs`,
`MeEndpoints.cs` (B4 only) · web locale files ×4 (B1's error line first, then the
web stream) · app locale files ×4 (app stream only) · `web/src/shared/api/generated/**`
(regenerated once by G) · `docs/ASSUMPTIONS.md` (append-only).

| Unit | Files | Depends on | Parallel? |
|---|---|---|---|
| **B1** Model | `Bonvi.Domain/Attendance/WorkSchedule.cs` (input record), `WorkScheduleDay.cs`, `WorkScheduleDate.cs`, `ScheduleChangeRequest.cs`, `AttendanceDay.cs`; `Application/Attendance/Scheduling/EmployeeDayOverride.cs`; web locales ×4: error `davomat.jadval_erkin_vaqti`; §10.1 tests | — | yes with B6 and A; everything else waits |
| **B2** Persistence | `Infrastructure/Persistence/Configurations/AttendanceConfigurations.cs` (3 configs: properties + CHECKs; `AttendanceDay` properties), migration `ErkinJadval` + snapshot, `Persistence/ScheduleOverrideQuery.cs`; §10.4 | B1 | yes with B3 (disjoint files) |
| **B3** Computation | `Scheduling/DayPlan.cs`, `Computation/PassPairing.cs`, `Computation/AttendanceDayBuilder.cs`, `Computation/DashboardStatusRule.cs`; §10.2 (`AttendanceScenario.cs` and the test files named there) | B1 | yes with B2 |
| **B4** Use cases, DTOs, endpoints | `Dtos/AttendanceDtos.cs` (`WorkScheduleDayDto`, `ScheduleYearDayDto`, `TimesheetCellDto`, `TimesheetTotalsDto`, `AttendanceExceptionDto`, `AttendanceSummaryDto`), `Dtos/ScheduleChangeDtos.cs` (`ScheduleChangeDayDto`, `DayPlanDto`), `Schedules/WorkScheduleMapper.cs`, `ScheduleYearUseCases.cs`, `FillScheduleYearUseCase.cs`, `ResolveScheduleQuery.cs`, `Requests/ScheduleChangeRequestUseCases.cs`, `Abstractions/IAttendanceReadModel.cs` (`ExceptionKind.Shortfall`), `Api/Endpoints/AttendanceEndpoints.cs`, `MeEndpoints.cs`, `ReadModels/AttendanceReadModel.ScheduleRequests.cs` | B1, B3 | no — sole author of the DTO/endpoint files |
| **B5** Read models, reports, Excel | `ReadModels/AttendanceReadModel.cs` (timesheet `DayRow`/cells/totals, summary, exceptions filter + sort + DTO), `Infrastructure/Reports/AttendanceExcelWriter.cs`, `Queries/AttendanceReportQueries.cs` (`MinMinutes` doc) | B2, B4 | no |
| **B6** Absence kinds | §6.5 backend rows (seed JSON, `SimpleCatalogs.cs`, `CatalogPersistenceTests.cs`) | — | yes, any time |
| **T** API/RBAC tests | §10.3 | B4, B5 (B2 migrated) | no |
| **G** `./bonvi gen` | `web/src/shared/api/generated/**` | B4, B5 merged, API running | no |
| **W** Web stream, in this order: W1 rules + schedule editor + year grid + schedules page; W2 HR request dialog + request text; W3 timesheet + employee tab + reports | §7.1 files, web locales ×4 | G | sequential inside W (shared locale and rules files); parallel with A |
| **A** App stream | §7.2 files, `app/src/ui/icon.tsx`, `ui/form.tsx`, app locales ×4, mock | this SPEC (contract §5) — integrate against the real API after B4/B5 | yes with all backend units and W |
| **D** Docs | amendment notes (§6.5), `DEPLOY-CHECKLIST.md` release entry (§6.3, §11) | all | last |

Order: B1 → (B2 ∥ B3) → B4 → B5 → T → G → W1 → W2 → W3; B6 and A run alongside;
D at the end.

## 13. Definition of Done

- [ ] `./bonvi test` fully green (latest baseline: all tests passed on 2026-10-17); §10.1–§10.4 present.
- [ ] Every F1–F16 example of §2.3 has its own asserting test; the existing computation tests are unchanged and green.
- [ ] Migration `ErkinJadval` read by hand (only ADD COLUMN / ADD CONSTRAINT); `upgrade head` works on a fresh DB and on a prod copy; Down tested.
- [ ] `./bonvi gen` succeeds (OpenAPI document builds — no `Guid = default`); `OpenApiDocumentTests` green.
- [ ] Web: `./bonvi pnpm typecheck`, `./bonvi pnpm build`, `./bonvi check` (4 locales + every backend error code translated) green.
- [ ] App: `pnpm typecheck`, `pnpm check:locales`, `pnpm build` green.
- [ ] No new endpoint; every changed endpoint keeps its permission; §10.3 RBAC cases pass.
- [ ] A fixed day computes exactly as before (F16 + untouched suite); Excel column positions of existing columns unchanged.
- [ ] App sheet: 8 rows in Verifix order, rows 1–3 navigate (Swap / Change preselected), rows 4–8 disabled «Tez orada».
- [ ] Seed has exactly the two kinds; after deploy the §6.3 query shows §6.1.
- [ ] `docs/ASSUMPTIONS.md` has the 2026-10-20 lines (done with this SPEC); amendment notes of §6.5 written.

## 14. Open questions — each has a standing decision

| # | Question | Decision (stands unless the owner says otherwise) |
|---|---|---|
| Q1 | On a flexible day, does presence outside the start–end window count? | ~~No — clipped~~ → **Yes — owner 2026-10-20: all presence counts, no clip** (see the override at the top). Original default: — the literal reading of the proposed default «the start–end window still bounds which presence counts». HR widens the window to allow early/late work. Flipping it is one branch in `PassPairing` |
| Q2 | Panel status for a flexible arrival | **`OnTime`**, never `Late`; no new panel status |
| Q3 | Should «Hali boshlanmagan» consider the break when computing the latest start? | **No** — `LatestStartAt = PlanEndAt − R`, optimistic, never accuses early |
| Q4 | Shortfall on `Absent` / `MissingExit` / `MissingEntry` / `Excused` days | **0** — the status carries it; nothing is counted twice |
| Q5 | Align absence kinds by migration or in the UI? | **UI catalog edit** (§6.3), audited and reversible |
| Q6 | Expose shortfall to MCP tools / the director's bot | **Not this wave**; add on request |
| Q7 | «Belgilash» clashes with the check-in word | sheet keeps the owner's label; the future form is titled «Belgilash so‘rovi» |
| Q8 | Show or hide the five pending types | **Show, disabled, «Tez orada»** — the owner asked for all eight |
| Q9 | Minimum «Ish vaqti» | **1 minute** (positive); the maximum is shift − break |
