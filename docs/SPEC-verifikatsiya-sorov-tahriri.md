# SPEC — Editing pending requests, break rule for schedule change requests, personal time (Verifikatsiya, wave 8)

> Status: authoritative for this wave. Written 2026-10-20. Source: the owner's three decisions of
> 2026-10-20 (§1). Every decision not taken by the owner is mine, is logged in `docs/ASSUMPTIONS.md`
> under `[2026-10-20]`, and is listed with its standing answer in §12.
>
> Base: commit `e164daa` (wave 7, «Erkin ish jadvali») — **not deployed**; production runs `c233722`.
> Migrations of this wave go **on top of `20261020051430_ErkinJadval`** and are additive only.
>
> House rules — unchanged (`SPEC-verifikatsiya.md` §0.2): English identifiers, Uzbek code comments,
> Uzbek `davomat.*` error codes, Uzbek migration class name, every UI string in 4 locales
> (`uz-Latn`, `uz-Cyrl`, `ru`, `en`; `uz-Latn` uses `o‘`/`g‘` = U+2018 and `’` = U+2019), **no
> `Guid X = default` in a request record** (OpenAPI 500 trap), work additively (extend records with
> appended defaulted parameters; rewrite nothing), DB defaults only in the migration (rule 2 at the top
> of `AttendanceConfigurations.cs`), `./bonvi` for every command.
>
> Read with: `SPEC-verifikatsiya-sorovlar.md` §B2–§B4 (absence requests, work-time credit),
> `SPEC-verifikatsiya-jadval-sorovi.md` §C (schedule change requests),
> `SPEC-verifikatsiya-erkin-jadval.md` §2 + its «Owner override» (flexible days: all presence counts),
> `SPEC-verifikatsiya.md` §5.5–§5.7 (pairing, break deduction, status), `UX-xodim-ilovasi.md` §A.3,
> §E.3–§E.6, §F.7.

## Reference implementations — copy their shape

| What | Copy from |
|---|---|
| Employee-token write over an HR handler (token → employee, foreign request = 404, delegation) | `CreateMyRequestCommandHandler`, `CancelMyRequestCommandHandler` (`Application/Me/MyAbsenceRequestUseCases.cs`); date window: `CreateMyScheduleRequestCommandHandler` (`Me/MyScheduleRequestUseCases.cs`) |
| HR edit of a pending request | `UpdateAbsenceRequestCommandHandler`, `UpdateScheduleChangeRequestCommandHandler` |
| A database race turned into a domain answer, not a 500 | `IDatabaseFault.IsConcurrentDayWrite` + `PostgresFault` |
| Attendance numbers stored as system settings, edited in a module card | `SystemSettingKeys.PhotoRetentionDays` / `PassRetentionDays`, `AttendanceRetention` (reader), `UpdateSettingsCommandHandler.CheckRetentionOrderAsync` (cross-key rule), `web/src/features/verification/components/attendance-retention-card.tsx` |
| An approved request merged into the day computation | `WorkCreditRule` (`Application/Attendance/Computation/WorkCredit.cs`) + `WorkCreditTests.cs` |
| A new stored figure on `attendance_days` shown in timesheet, app, summary, Excel | `shortfall_minutes` (migration `ErkinJadval`) and `credited_minutes` (migration `IshVaqtiSorovi`) |
| Migration test | `api/tests/Bonvi.Api.IntegrationTests/ErkinJadvalMigrationTests.cs` |
| Form in edit mode that must not overwrite stored values with the current plan | edit branch of `web/.../components/schedule-request-dialog.tsx` (`touched: true`, keeps `breakMinutes`) |

---

## 0. Goal

Employees can fix their own request while HR has not decided yet, instead of cancelling and
re-sending it. Schedule change requests follow the company break policy (12:00–15:00, 60 minutes,
Friday up to 120). An approved «Shaxsiy» (personal) request finally changes the numbers: its hours are
excused but are not work time — they are removed from worked minutes and shown separately, so the
timesheet says «worked 6:00, personal 2:00» instead of silently crediting or ignoring them.

## 1. Owner decisions (2026-10-20) and scope

| # | Decision |
|---|---|
| A1 | **Edit pending requests** (Verifix ✏️). The employee can change his own request — absence and schedule change — while it is `Pending`; afterwards it is read-only. |
| A2 | **Break rule for schedule change requests**: a break must lie inside **12:00–15:00**; length **exactly 60 min** Mon–Thu and Sat/Sun; **Friday up to 120 min**; a working day **without** a break stays allowed. Configurable, server-side, on create and edit, mirrored in app and HR forms. Existing requests are not re-validated on approval. |
| A3 | **Approved personal kinds are subtracted from work time** («uzrli, lekin haq to‘lanmaydi»). Owner's example: plan 08:30–17:30 (8:00), personal 14:00–16:00, came 08:25, left 17:35 without badging out → **worked 6:00, personal 2:00, plan 8:00, no lateness, no early leave**. Generalised **by the flag**: every kind with `counts_as_work_time = false` is a personal kind. |
| A4 | HR must be able to approve every kind (verified: nothing blocks it — §3.6). |

**Out of scope:** reopening a decided request; HR-side editing beyond what already exists (§3.6); a
new notification channel; personal time in MCP tools / public API / the director's bot; deleting
(🗑) a request — the employee's «delete» is the existing «So‘rovni bekor qilish»; cycle schedules;
the five reserved request types of wave 7.

---

## 2. Data model

### 2.1 Columns — migration `SorovTahririVaShaxsiyVaqt`

| Table | Column | Type | Null | DB default | Meaning |
|---|---|---|---|---|---|
| `absence_requests` | `version` | integer | no | `0` | optimistic-concurrency counter, EF concurrency token (§3.3) |
| | `edited_at` | timestamptz | yes | — | last edit while `Pending` (HR or employee), UTC |
| | `edited_by_account_id` | uuid | yes | — | employee account of the last edit when it came from the app; NULL when HR edited or never edited. No FK — same as `submitted_by_account_id` |
| `schedule_change_requests` | `version` | integer | no | `0` | same |
| | `edited_at` | timestamptz | yes | — | same |
| | `edited_by_account_id` | uuid | yes | — | same |
| `attendance_days` | `personal_minutes` | integer | no | `0` | approved personal time inside the day's plan, after the break correction (§5.3 step 4) |

* No new indexes, no CHECK constraints (`attendance_days` is a computed table with a single writer;
  `version` is never edited by hand).
* `./bonvi migrate SorovTahririVaShaxsiyVaqt`. Up: `SET lock_timeout = '3s';` → 7 × `AddColumn`.
  **No `UPDATE`**: existing rows get version 0, never edited, personal 0 — i.e. they behave exactly
  as before. Down drops the 7 columns. Uzbek `<summary>` like `ErkinJadval`. **Read the generated
  file by hand**: only these ADD COLUMN operations; no DROP, no default change on other columns.
* Image rollback without Down works: the old code ignores the columns, DB defaults keep its inserts
  valid; while rolled back `version` is not incremented and `personal_minutes` goes stale (old code
  never reads either). Down deletes edit markers and personal minutes (recomputable).

### 2.2 Settings — 4 new keys, **no migration** (rows appear on first save, defaults come from code)

| Key | Kind | Default | Min | Max | Meaning |
|---|---|---|---|---|---|
| `attendance.requestBreakFrom` | Number (minute of day) | `720` (12:00) | 0 | 1439 | allowed break window start |
| `attendance.requestBreakTo` | Number (minute of day) | `900` (15:00) | 1 | 1439 | allowed break window end |
| `attendance.requestBreakMinutes` | Number | `60` | 1 | 480 | required break length Mon–Thu, Sat, Sun; also the Friday minimum |
| `attendance.requestBreakFridayMaxMinutes` | Number | `120` | 1 | 480 | Friday maximum |

* Defaults live in `AttendanceRules` (`DefaultRequestBreakFromMinute = 720`,
  `DefaultRequestBreakToMinute = 900`, `DefaultRequestBreakMinutes = 60`,
  `DefaultRequestBreakFridayMaxMinutes = 120`) and `SystemSettingKeys.All` references them (the
  `Number(AttendanceRules.Default…)` pattern). Place the four definitions right after
  `PassRetentionDays`. Do **not** add them to `system-settings-page.tsx` `GROUPS` — they are edited in
  the module card (§7.2), exactly like the retention keys.
* **Cross-key rule** in `UpdateSettingsCommandHandler` (next to `CheckRetentionOrderAsync`, same
  "requested value, else current value" merge): `from < to`, `minutes ≤ fridayMax`,
  `fridayMax ≤ to − from`. Violation → 400 **`davomat.tanaffus_qoidasi_nomuvofiq`** (in
  `SettingsErrors`, like `RetentionOrder`). No DB access when none of the four keys is in the request.
* `RequestBreakRuleReader` (new, `Application/Attendance/Requests/RequestBreakRuleReader.cs`, scoped)
  reads the four numbers through `SystemSettingsReader` and returns a `RequestBreakRule`. If the stored
  combination is inconsistent (only reachable by raw SQL) it returns `RequestBreakRule.Default`.

### 2.3 Domain and Application changes (appended parameters have defaults ⇒ existing callers compile)

| Type (file) | Change |
|---|---|
| `AbsenceRequest` (`Domain/Attendance/AbsenceRequest.cs`) | props `int Version`, `DateTimeOffset? EditedAt`, `Guid? EditedByAccountId`; `const int MaxNoteLength = 2000`. `Create`: after the period check, note > 2000 → `davomat.izoh_uzun`. `Update(…, string? note, DateTimeOffset? at = null, Guid? editedByAccountId = null)`: existing checks + note length; on success `Version++` and, when `at` is given, `EditedAt = at`, `EditedByAccountId = editedByAccountId`. `Approve`/`Reject`: decision note > 2000 → `davomat.izoh_uzun`; `Version++`. `Cancel`, `CancelBySubmitter`: `Version++` |
| `ScheduleChangeRequest` (`ScheduleChangeRequest.cs`) | same three props. `Create(…, Guid? submittedByAccountId = null, RequestBreakRule? breakRule = null)`; `Update(kind, days, note, RequestBreakRule? breakRule = null, DateTimeOffset? at = null, Guid? editedByAccountId = null)` → `Version++`, edit markers as above; `Approve`/`Reject`/`Cancel`/`CancelBySubmitter` → `Version++`. `Validate(kind, days, breakRule)` applies §4.3 after `WorkScheduleDay.Validate`, **only when `kind == Change`** |
| `RequestBreakRule` (new, `Domain/Attendance/RequestBreakRule.cs`) | `sealed record RequestBreakRule(TimeOnly From, TimeOnly To, int Minutes, int FridayMaxMinutes)`; `static Default`; `bool IsConsistent`; `internal Result Validate(ScheduleChangeDayInput day)` (§4.3) |
| `AttendanceDayFacts` / `AttendanceDay` (`AttendanceDay.cs`) | append `int PersonalMinutes = 0` after `ShortfallMinutes`; prop `PersonalMinutes`; `Recompute` copies it |
| `AttendanceRules` | the four defaults of §2.2 |
| `SystemSettingKeys` (`Domain/Settings/SystemSettingKeys.cs`) | the four keys and definitions of §2.2 |
| `AbsenceType` remark (`Domain/Catalog/SimpleCatalogs.cs`) | `CountsAsWorkTime` comment: "false — personal kind: a timed request removes its hours from work time; a whole-day request excuses an absent day" |
| `WorkCredit` (`Application/Attendance/Computation/WorkCredit.cs`) | append `bool CountsAsWorkTime = true` |
| `PersonalTime`, `PersonalTimeRule` (new, `Computation/PersonalTime.cs`) | §5.9 |
| `ComputedAttendanceDay` (`AttendanceDayBuilder.cs`) | append `PersonalTime? Personal = null` (not stored; the panel reads it) |
| `IDatabaseFault` + `PostgresFault` | `bool IsStaleRequestWrite(Exception)` — `DbUpdateConcurrencyException` whose entries are all `AbsenceRequest`, `ScheduleChangeRequest` or `ScheduleChangeDay` |
| EF configs (`AttendanceConfigurations.cs`) | `AbsenceRequestConfiguration` and `ScheduleChangeRequestConfiguration`: `Property(r => r.Version).IsRequired().IsConcurrencyToken()`; `EditedAt`, `EditedByAccountId` by convention. `AttendanceDayConfiguration`: `PersonalMinutes` `IsRequired()` (default only in the migration) |
| `AuditCatalog` | `AbsenceRequest`, `ScheduleChangeRequest`: `.Exclude(nameof(Version), nameof(EditedAt))`; `EditedByAccountId` added to the existing `AuditRefTarget.EmployeeAccount` ref list |

---

## 3. Editing pending requests (A1)

### 3.1 Rules

* **Who:** the employee, through the app, on **his own** request (`request.EmployeeId` = the token's
  employee). That includes a request HR entered on his behalf — the same rule the existing employee
  cancel already uses. Another employee's request answers **404 `davomat.sorov_topilmadi`** (never
  403 — the existing reason: it must not reveal that the id exists).
* **When:** only `Pending`. `Approved`, `Rejected` **or** `Cancelled` → 409
  `davomat.sorov_qaror_qabul_qilingan` — the code the domain `Update` already returns for every
  non-pending request (kept as is; the app never offers editing outside `Pending`).
* **What:** the whole request is replaced by the body, with **exactly the validation and limits of
  create**: absence — domain period rules, kind exists / active / `employee_can_request`, overlap with
  the employee's other active absence requests (the request itself excluded), note ≤ 2000. Schedule
  — kind may change (Change ⇄ Swap), 1–31 days / 1–15 pairs, the employee date window
  today −31 … today +92 (`davomat.jadval_sorovi_sana_oynasi`), years 2000–2100, 92-day span, the
  break rule (§4), overlap with other active schedule requests (itself excluded), Swap days
  re-snapshotted from the **current** plans at edit time (as HR edit does today), body ≤ 32 KB.
* **Effect:** a pending request never affects attendance ⇒ an edit recomputes **nothing**.
* **Status after edit:** stays `Pending`. `CreatedAt` and `SubmittedByAccountId` are unchanged.

### 3.2 Endpoints (details in §6)

`PUT /api/me/requests/{id}` and `PUT /api/me/schedule-requests/{id}` — employee token, same
`/api/me` group (EmployeePolicy, `EmployeeAccessGate`, the group's rate limit).

### 3.3 Optimistic concurrency — normative

Two layers, both mandatory:

1. **Database layer.** `version` is an EF concurrency token on both request tables. **Every**
   state-changing domain method increments it: `Update`, `Approve`, `Reject`, `Cancel`,
   `CancelBySubmitter`. EF therefore issues `UPDATE … WHERE id = @id AND version = @loaded`; when a
   concurrent writer committed first, 0 rows match → `DbUpdateConcurrencyException`. Every handler
   that calls `SaveChangesAsync` after mutating a request — HR update, HR decide, HR cancel and
   employee cancel, for both request kinds (8 handlers; the two new employee update handlers
   delegate to the HR update handlers) — wraps that call in
   `catch (Exception e) when (_fault.IsStaleRequestWrite(e))` and returns
   **409 `davomat.sorov_ozgargan`**. Never a 500. This also closes the decide/cancel race noted as
   qa-security L4 on 2026-10-17.
2. **API layer ("the client saw version N").** The client sends the `version` it displayed:
   * employee `PUT` bodies — **required** `version`;
   * HR `PUT /requests/{id}`, `PUT /schedule-requests/{id}` and both `…/decide` bodies — optional
     `version` (an old client keeps working; the new web always sends it).
   When given and the request is still `Pending` but `version ≠ request.Version` → 409
   `davomat.sorov_ozgargan`. Cancel endpoints take no version (cancel does not depend on content);
   they are protected by layer 1 only.

**Precedence of answers** (first match wins). The status check moves to the top of the HR update
and decide handlers (an explicit `request.Status != Pending` check with the domain's code, so it
always precedes the version check):

* employee absence edit: 404 not found/foreign → 409 `…_qaror_qabul_qilingan` → 409
  `davomat.sorov_ozgargan` (version) → 400 `davomat.yoqlik_turi_xodimga_yopiq` (employee handler) →
  404 kind not found / 400 kind inactive (HR handler) → 409 `davomat.sorov_takroriy` → domain update
  (400 period / type empty / note) → save (409 stale);
* employee schedule edit: 404 → 409 status → 409 version → 400 `davomat.jadval_sorovi_sana_oynasi` →
  `ScheduleChangeDays.BuildAsync` (400 shape / swap / no schedule) → 409
  `davomat.jadval_sorovi_takroriy` → domain update (400 day rules incl. §4, note) → save (409 stale);
* HR update: the same chains without the employee-only steps; HR decide: 404 → 409 status → 409
  version → 400 decision note → save (409 stale).

### 3.4 Implementation shape

* Extend the HR commands, then delegate from the employee handlers (the create pattern):
  * `UpdateAbsenceRequestCommand(…, string? Note = null, int? ExpectedVersion = null, Guid? EditedByAccountId = null)`;
  * `UpdateScheduleChangeRequestCommand(…, string? Note, int? ExpectedVersion = null, Guid? EditedByAccountId = null)`;
  * `DecideAbsenceRequestCommand(…, string? DecisionNote = null, int? ExpectedVersion = null)`,
    `DecideScheduleChangeRequestCommand(…, string? DecisionNote, int? ExpectedVersion = null)`.
  * HR handlers get `IClock` (pass `at = now` to `Update`) and `IDatabaseFault`; the schedule
    create/update handlers get `RequestBreakRuleReader` and pass the rule to the domain.
* New `UpdateMyRequestCommand(Guid RequestId, int Version, Guid AbsenceTypeId, DateOnly FromDate,
  DateOnly ToDate, TimeOnly? FromTime = null, TimeOnly? ToTime = null, string? Note = null)` +
  handler in `Me/MyAbsenceRequestUseCases.cs`: resolve ids → load → ownership 404 → status 409 →
  version 409 → kind `EmployeeCanRequest == false` → `davomat.yoqlik_turi_xodimga_yopiq` → delegate
  to `UpdateAbsenceRequestCommandHandler` with `ExpectedVersion = Version`,
  `EditedByAccountId = account`.
* New `UpdateMyScheduleRequestCommand(Guid RequestId, int Version, ScheduleChangeKind Kind,
  IReadOnlyList<ScheduleChangeDayInput>? Days, IReadOnlyList<ScheduleSwapInput>? Swaps, string? Note)`
  + handler in `Me/MyScheduleRequestUseCases.cs`: ids → load → ownership → status → version → window
  (same `MyScheduleRequestWindow` code as create) → delegate to
  `UpdateScheduleChangeRequestCommandHandler`.
* Register the two handlers in `Application/Common/DependencyInjection.cs` (where the other `My…`
  handlers are) and `RequestBreakRuleReader` in `Application/Attendance/AttendanceRegistration.cs`.

### 3.5 Audit and notifications

* **Audit is automatic** (`AuditTrailInterceptor`): an app edit is written with actor kind
  `EmployeeAccount` and old → new values of kind, dates, times, note; schedule day rows appear as
  deleted + created under the request's employee (existing `ScheduleChangeDay` subject). `Version` and
  `EditedAt` are excluded (§2.3). No hand-written audit call.
* **Notifications:** request creation notifies nobody today — HR sees the live pending-count badge
  (`GET /requests/pending-count`). An edit does not change that count. **No new notification**; the
  queue row and the decision dialog show the edit marker (§7.2).

### 3.6 HR side

* HR could already edit pending requests of both kinds (`PUT /api/attendance/requests/{id}`,
  `PUT /api/attendance/schedule-requests/{id}`, pencil in both queues). **No new HR editing** in this
  wave; HR edit and decide become version-aware (§3.3) and HR edits also set `edited_at`
  (`edited_by_account_id = NULL`).
* **Approving every kind — verified, no fix needed:** `RowActions` in `absence-requests-list.tsx`
  shows approve/reject for every `Pending` row regardless of kind; `DecideAbsenceRequestCommandHandler`
  has no kind restriction; role `hr_manager` holds `attendance.requests.manage`. What changes is only
  the *effect* of approving a personal kind (§5).
* «HR sees the latest version before deciding» is guaranteed by §3.3 layer 2: a decision on a row
  that changed after it was loaded fails with 409 and the queues refetch (§7.2).

---

## 4. Break rule for schedule change requests (A2)

### 4.1 Effective break length (normative)

`L = ScheduleChangeRequest.BreakMinutes(day)` — the existing internal helper, unchanged: the sent
`breakMinutes` when given, otherwise the window length (wrapping midnight), otherwise 0; 0 on a day off.
This is the value stored in `schedule_change_request_days.break_minutes` and deducted later, so the
rule judges exactly what will be computed. An untouched day from a normal schedule (window
12:00–15:00, `breakMinutes = 60` sent back by the form) has `L = 60` and passes.

### 4.2 Scope

* Every **working** day of a request with `kind = Change`, on **create and edit**, from **both** the
  app (`/api/me/schedule-requests`) and HR (`/api/attendance/schedule-requests`).
* **Not** applied to: days off; `kind = Swap` (the server copies the schedule's own day — the employee
  does not choose a break); approving or cancelling (existing requests are never re-validated;
  pending requests that violate the rule can still be decided — editing them requires compliance).
* The rule numbers are read **at the moment of the write** (§2.2 reader).

### 4.3 Algorithm (domain `RequestBreakRule.Validate`, runs after all existing day rules)

Existing order stays first (domain and both client mirrors): times empty → times equal → break length
0…480 → half window → window outside shift → flexible «Ish vaqti». Then:

```
if !day.IsWorking                       → OK
L = BreakMinutes(day)
if L == 0                               → OK            // a day without a break (even if a window with 0 deducted minutes is present)
hasWindow = BreakStart != null && BreakEnd != null
if hasWindow:
    if !(From <= BreakStart && BreakStart < BreakEnd && BreakEnd <= To)   // clock values; a window crossing midnight never fits
                                        → davomat.jadval_sorovi_tanaffus_oraligi
    if L > (BreakEnd − BreakStart) in minutes
                                        → davomat.jadval_sorovi_tanaffus_davomiyligi
isFriday = day.Date.DayOfWeek == Friday
if isFriday ? (L < Minutes || L > FridayMaxMinutes) : (L != Minutes)
                                        → davomat.jadval_sorovi_tanaffus_davomiyligi
OK
```

A day without a window but with `L > 0` (a schedule that defines minutes but no window, copied
untouched) is judged by length only — there is no position to check, and nothing is deducted without
a window anyway.

### 4.4 Examples (defaults 12:00–15:00 / 60 / 120) — each pinned by a domain test (§8.1)

| # | Day | Shift | Window | `breakMinutes` sent | L | Result |
|---|---|---|---|---|---|---|
| B1 | Mon | 08:30–17:30 | — | null | 0 | OK — no break |
| B2 | Mon | 13:00–18:00 | — | null | 0 | OK (owner's example of a day without a break) |
| B3 | Mon | 08:30–17:30 | 12:00–15:00 | 60 (untouched schedule day) | 60 | OK |
| B4 | Mon | 08:30–17:30 | 12:23–13:23 | null | 60 | OK |
| B5 | Mon | 08:30–17:30 | 12:00–14:00 | null | 120 | `…_davomiyligi` |
| B6 | Fri | 08:30–17:30 | 12:23–14:23 | null | 120 | OK (Verifix «Friday prayer» example) |
| B7 | Fri | 08:30–17:30 | 12:00–14:30 | null | 150 | `…_davomiyligi` |
| B8 | Fri | 08:30–17:30 | 12:30–13:00 | null | 30 | `…_davomiyligi` (Friday minimum is 60) |
| B9 | Tue | 08:30–17:30 | 11:30–12:30 | null | 60 | `…_oraligi` |
| B10 | Wed | 08:30–17:30 | 14:30–15:30 | null | 60 | `…_oraligi` |
| B11 | Thu | 08:30–17:30 | — | 60 (schedule without window) | 60 | OK |
| B12 | Thu | 08:30–17:30 | — | 90 | 90 | `…_davomiyligi` |
| B13 | Sat | 09:00–14:00 | 12:00–12:30 | 60 (kept from schedule) | 60 | `…_davomiyligi` (longer than its window) |
| B14 | Sun | 08:30–17:30 | 12:00–13:00 | null | 60 | OK |
| B15 | Mon | 20:00–08:00 | 00:30–01:30 | 60 | 60 | `…_oraligi` — a night shift can only be requested without a break (§12 Q2) |
| B16 | Mon | 08:30–17:30 | 12:00–13:00 | 0 (explicit) | 0 | OK — nothing is deducted |
| B17 | Mon | 08:30–17:30 | 07:00–08:00 | null | 60 | `davomat.jadval_tanaffus_oynasi` — the existing rule answers first |
| B18 | Mon | Swap with a date whose schedule break is 12:00–14:00 / 120 | | | | OK — Swap exempt |
| B19 | Mon | rule set to 13:00–14:00 / 30 / 45; window 13:00–13:45 | | null | 45 | `…_davomiyligi`; the same on Fri → OK |

---

## 5. Personal time (A3)

### 5.1 Definitions

* **Personal kind** — an absence kind with `counts_as_work_time = false` (today «Shaxsiy»; also an
  inactive «Kasallik» and any kind HR adds without the flag). The label shown everywhere is «Shaxsiy
  vaqt», regardless of the kind's name.
* **Personal interval** — an **approved, timed** (`from_time`/`to_time` set, single day) request of a
  personal kind: `[FromDate FromTime, FromDate ToTime]` Tashkent → UTC. Whole-day and multi-day
  personal requests are **not** intervals: they keep today's behaviour (an `Absent` day becomes
  `Excused`, nothing else).
* For a working day with a plan: `S = PlanStartAt`, `E = PlanEndAt`, `B` = break window
  `[BreakWindowStartAt, BreakWindowEndAt]` (may be absent), `L = plan.BreakMinutes`,
  `N = plan.PlannedMinutes` (R on a flexible day), `gi`/`go` = grace in/out.
* **P** = every personal interval in the loaded set (the loader covers `D … D + 1`, so a night
  shift sees the next morning's request — `WorkCreditRule.RequestDatesNeeded`), each clipped to
  `[S, E]`, empty ones dropped, sorted, merged (touching intervals merge). **P is not clipped to
  "now"** — it is approved plan information, not presence.
* **C** = `PairingResult.ClosedIntervals` (real closed presence, in order); **K** =
  `WorkCreditRule.IntervalsFor(…)` (approved work-time credit, clipped to `[S, min(E, now)]`).
* `G(X)` = the gaps between consecutive intervals of X (exactly the existing "outside spans").
  `|X|` = total length; every figure is summed in ticks and truncated **once** to whole minutes
  (`WholeMinutes.AtLeastZero`), as in `PassPairing`/`WorkCreditRule`.
* **When P is empty the day is computed exactly as today**, byte for byte (§8.2 regression tests).

### 5.2 Loading — one loader for credit and personal time (`Infrastructure/Persistence/AbsenceQuery.cs`)

* `WorkCreditsAsync` additionally returns approved **timed** requests of personal kinds, projected
  with `CountsAsWorkTime = workTypes.Contains(request.AbsenceTypeId)` (the existing `workTypes`
  subquery, `IgnoreQueryFilters`, `request.DeletedAt == null` written explicitly — the existing
  comment explains why). Condition: `workTypes.Contains(type) || request.FromTime != null`.
  Every caller (recompute, nightly, dirty queue, `attendance:started-shifts`, timesheet synthesis,
  panel, trend, board, pass-list late badges) therefore receives personal intervals without new code.
* **`WorkCreditRule.IntervalsFor` must ignore entries with `CountsAsWorkTime == false`** — otherwise a
  personal request would be credited as work. Pinned by a test (§8.2).
* `ExcusedDatesAsync` **excludes timed personal requests**: condition gains
  `request.FromTime == null || workTypes.Contains(request.AbsenceTypeId)` (same subquery, explicit
  `DeletedAt == null`). A 2-hour personal request no longer excuses a whole absent day (§5.5).
* `AttendanceStartedShiftsJob` already rewrites yesterday/today for every employee returned by the
  loader — now also for employees with timed personal requests (update its comment only).

### 5.3 Fixed working day with P ≠ ∅ — normative steps

1. **Credit never covers personal time:** `K′ = K \ P`.
2. **Presence without personal time:**
   `A = merge(C ∪ K′)`, `R_all = A \ P`, `O_all = G(A) \ P`;
   `R_pass = C \ P`, `O_pass = G(C) \ P`.
   Presence inside a personal interval is removed whether or not the person badged out (owner's
   example). A gap that falls inside P is neither presence nor a «tanaffus olingan» span.
3. **Break (§5.5.1 formulas on the reduced sets),** for X ∈ {all, pass}:
   `taken_X = |O_X ∩ B|`, `inWin_X = |R_X ∩ B|`,
   `ded_X = B ? max(0, min(L − taken_X, inWin_X)) : 0`, `worked_X = |R_X| − ded_X`.
4. **Personal minutes:** `absorbed = B ? min(|P ∩ B|, max(0, L − |B \ P|)) : 0`;
   `personal = |P| − absorbed`. Meaning: when the personal interval leaves less than the break length
   free inside the break window, the missing part of the break is taken to lie inside the personal
   time; it is unpaid anyway, so it is not counted twice (E7, E11).
5. **Credit split:** `K′ = ∅ ⇒ credited = 0, worked = worked_pass`; otherwise
   `credited = clamp(worked_all − worked_pass, 0, max(0, N − personal − worked_pass))`,
   `worked = worked_pass + credited`.
6. **Cap (fixed days, `personal > 0` only):** `worked = min(worked, max(0, N − personal))`.
   Presence outside the plan does not buy back unpaid personal time — this is what makes the owner's
   example 6:00 (§12 Q1). `credited` needs no adjustment (step 5 already bounds it).
7. **Stored break figures:** `break_taken_minutes = taken_all`, `break_deduction_minutes = ded_all`.
8. **Presence edges:** `firstPresence = min(pairing.FirstInAt, first(K′).From)`;
   `lastPresence = pairing.MissingExit ? null : max(pairing.LastOutAt, last(K′).To)` (the existing
   `CreditedDay` rule, with K′). `first_in_at`/`last_out_at` stay the real terminal times.
9. **Lateness:** `ES = To` of the P interval with `From ≤ S < To`, else `S`.
   `late = firstPresence == null ? 0 : |[ES + gi, firstPresence] \ P|` (0 when
   `firstPresence ≤ ES + gi`).
10. **Early leave** (only when `now ≥ E`, `!MissingExit`, `lastPresence != null` — existing gate):
    `EE = From` of the P interval with `From < E ≤ To`, else `E`;
    `early = |[lastPresence, EE − go] \ P|`.
11. **Overtime** — unchanged formula `max(0, worked − N)` (so 0 whenever the cap acted).
12. **`personal_minutes = personal`**; `planned_minutes` stays N.

### 5.4 Flexible working day with P ≠ ∅ (`SPEC-verifikatsiya-erkin-jadval` + its owner override)

Steps 1–5, 7, 8 and 12 as above; presence is still **not** clipped to the window. Differences:
* **no cap** (step 6 is skipped) — the owner's override «all presence counts»; working longer
  legitimately makes up for the personal time;
* `late = early = 0` (as every flexible day);
* **shortfall** = `status == Present && now ≥ E ? max(0, R − personal − worked) : 0` — approved
  personal time is never shortfall (A3);
* the latest arrival for «Hali boshlanmagan» moves to `E − max(0, R − personal)` (§5.5).

### 5.5 Status (after the existing `StatusOf` and the credit rule `Absent + credited > 0 → Present`)

```
ExcusesDay   = fixed:    some P interval has From ≤ S and To ≥ E      (the whole plan is personal)
               flexible: personal ≥ R
NotStartedUntil = fixed:    ES when ES > S, else null (existing rule)
                  flexible: personal > 0 ? E − max(0, R − personal) : null (existing LatestStartAt)

if status == Absent && (excused || ExcusesDay)                          → Excused
else if status == Absent && HasNotStarted(plan, today, now, NotStartedUntil) → Planned (no row)
```

* `excused` comes from `ExcusedDatesAsync` (whole/multi-day approved requests of any kind and timed
  work-time requests — §5.2).
* `DashboardStatusRule.HasNotStarted(DayPlan plan, DateOnly today, DateTimeOffset now,
  DateTimeOffset? notStartedUntil = null)`: `plan.Date > today ⇒ true`; when `notStartedUntil` is
  given ⇒ `now < notStartedUntil`; otherwise exactly today's code.
* So: absent with a partial personal interval → **`Absent`** («Kelmadi», counted in absent days;
  until now it read «Sababli»); absent with the whole plan personal → `Excused`; morning personal and
  not yet arrived → `Planned` / panel `NotStarted` until `ES`, then `Absent` (the 15-minute
  `attendance:started-shifts` writes the row).
* Passes on a day whose whole plan is personal → `Present` with `worked = 0` (the presence lies
  inside P) and `personal_minutes` = the whole plan.
* A personal interval on a day off, on a day without schedule, or entirely outside the plan → P is
  empty → no effect.

### 5.6 Panel, trend, board

* `DashboardStatusRule.Classify(…, bool excused = false, PersonalTime? personal = null)`:
  * arrived, fixed day: `expected = personal?.ExpectedStartAt ?? planStart`; `OnTime` when
    `arrived ≤ expected + grace`, else `Late` with `LateMinutes = |[expected, arrived] \ P|`
    (panel minutes stay "from the start, no grace" — today's rule with `expected` as the start);
  * arrived, flexible day: `OnTime` (unchanged);
  * not arrived: `HasNotStarted(plan, today, now, personal?.NotStartedUntil)` → `NotStarted`;
    else `excused || personal?.ExcusesDay == true` → `Excused`; else `Absent`.
  * `personal == null` ⇒ identical to today.
* `GetDashboardAsync` passes `day?.Personal`; `GetDashboardTrendAsync` keeps `day.Personal` per date
  next to the arrival and passes it; `GetBoardAsync` uses
  `lateAfter = (computed.Personal?.ExpectedStartAt ?? planStart) + grace` for not-yet-arrived people.
* The pass list's late badge (`LateMinutesByPassAsync`) uses `facts.LateMinutes` — no change needed.

### 5.7 Recompute triggers — all existing, none new

Decision and cancellation of an approved request recompute its affected days inline
(`AffectedDays`: timed → `FromDate − 1 … ToDate`); flipping `counts_as_work_time` on a kind recomputes
its approved requests (`AbsenceTypeWorkTimeHook`, both directions are now meaningful); nightly
(D−2…D), dirty queue and `attendance:started-shifts` read the same loader. Editing a **pending**
request recomputes nothing. Old approved timed personal requests are re-evaluated only when their
days are recomputed (nightly window or `POST /recompute`); production has no schedule assigned
(2026-10-20: 0 assignments), so no stored row changes at deploy.

### 5.8 Worked examples — normative, each pinned by a test (§8.2)

Base unless stated: fixed day Monday, shift **08:30–17:30**, `break_minutes = 60`, window
**12:00–15:00**, grace 10/10, N = 480, `now` = next day 03:00, requests approved. Columns: personal
P; worked; personal_minutes; late; early; break taken / deducted; status; note.

| # | Passes / situation | P | worked | personal | late | early | taken/ded | status | note |
|---|---|---|---|---|---|---|---|---|---|
| E1 | **owner**: 08:25 In, 17:35 Out | 14:00–16:00 | **360** | **120** | 0 | 0 | 0 / 60 | Present | `R` 430 − 60 = 370 → cap 480 − 120 = 360; overtime 0 |
| E2 | 08:25 In, 14:00 Out, 16:00 In, 17:35 Out | 14:00–16:00 | 360 | 120 | 0 | 0 | 0 / 60 | Present | badging out changes nothing |
| E3 | 08:30 In, 13:55 Out, 15:10 In, 17:30 Out (came back early) | 14:00–16:00 | 360 | 120 | 0 | 0 | 5 / 55 | Present | 15:10–16:00 lies in P → removed; gap 13:55–14:00 is a 5-min break |
| E4 | 10:05 In, 17:30 Out | 08:30–10:00 | 385 | 90 | 0 | 0 | 0 / 60 | Present | ES 10:00, grace to 10:10 |
| E5 | 10:30 In, 17:30 Out | 08:30–10:00 | 360 | 90 | **20** | 0 | 0 / 60 | Present | late from ES + grace |
| E6 | 08:30 In, 16:00 Out | 16:00–17:30 | 390 | 90 | 0 | 0 | 0 / 60 | Present | EE 16:00 |
| E7 | 08:30 In, 12:00 Out, 15:00 In, 17:30 Out | 12:00–15:00 | 360 | **120** | 0 | 0 | 0 / 0 | Present | window fully personal: absorbed 60 → 180 − 60 |
| E8 | 11:00 In, 17:30 Out | 10:00–12:00 | 270 | 120 | **80** | 0 | 0 / 60 | Present | late = 08:40–10:00; 11:00–12:00 removed |
| E9 | no passes, past day | 14:00–16:00 | 0 | 120 | 0 | 0 | 0 / 0 | **Absent** | was `Excused` before this wave |
| E10 | no passes | 08:00–18:00 (clipped 08:30–17:30) | 0 | 480 | 0 | 0 | 0 / 0 | **Excused** | whole plan personal: 540 − absorbed 60 |
| E11 | shift 09:00–18:00, window 13:00–14:00, L 60; 09:00 In, 13:00 Out, 14:00 In, 18:00 Out | 13:00–14:00 | 480 | **0** | 0 | 0 | 0 / 0 | Present | personal = lunch hour → costs nothing; `personal = 0` ⇒ no cap |
| E12 | today, now 09:00, no passes | 08:30–10:00 | — | — | — | — | — | **Planned** (no row) | panel NotStarted; at 10:01 → Absent; arrival 10:05 → panel OnTime, 10:20 → Late 20 |
| E13 | flexible W 08:30–18:00, R 450, window 12:00–14:00, L 120; 08:30 In, 14:00 Out, 16:00 In, 19:00 Out | 14:00–16:00 | **390** | 120 | 0 | 0 | 0 / 120 | Present | no cap; shortfall 450 − 120 − 390 → **0**; latest start 12:30 |
| E14 | as E13 but last Out 17:00 | 14:00–16:00 | 270 | 120 | 0 | 0 | 0 / 120 | Present | shortfall **60** (180 without the personal adjustment) |
| E15 | night Mon 20:00–Tue 08:00, window 00:30–01:30, L 60, N 660; whole-day «Ishxona ishi bilan» on Mon; personal on **Tue** 02:00–03:00; no passes | Tue 02:00–03:00 | 600 (credited 600) | 60 | 0 | 0 | 0 / 60 | Present | K′ = K \ P; room 660 − 60; one row on Monday |
| E16 | E1 after HR cancels the approved request | — | 490 | 0 | 0 | 0 | 0 / 60 | Present | recomputed inline; overtime 10 — today's numbers |
| E17 | E1's passes, no request (regression) | — | 490 | 0 | 0 | 0 | 0 / 60 | Present | unchanged path |
| E18 | Sunday (day off), any passes | 10:00–11:00 | as today | 0 | 0 | 0 | as today | DayOff / DayOffWorked | no plan ⇒ no P |
| E19 | 08:30 In, 17:00 Out | 17:00–19:00 (clipped 17:00–17:30) | 450 | 30 | 0 | 0 | 0 / 60 | Present | EE 17:00 |
| E20 | shift 09:00–18:00, L 60, **no window**; 09:00 In, 18:00 Out | 13:00–14:00 | **420** | 60 | 0 | 0 | 0 / 0 | Present | no window ⇒ no deduction (480 after removal), cap 480 − 60; without the request: 540, overtime 60 |
| E21 | flexible W 08:30–18:00, break 0, R 240; no passes | 08:30–12:30 | 0 | 240 | 0 | 0 | 0 / 0 | **Excused** | flexible ExcusesDay: personal ≥ R |

Panel checks (`DashboardStatusRuleTests`): E12 timings as written; E1 arrival 08:25 → OnTime; E5
arrival 10:30 → Late, `LateMinutes = 30` (from ES 10:00, no grace); E8 arrival 11:00 → Late,
`LateMinutes = 90` (08:30–10:00); E10 → Excused; E9 → Absent.

### 5.9 Where in code (normative surface)

New `api/src/Bonvi.Application/Attendance/Computation/PersonalTime.cs`:

```csharp
/// Non-stored: what the panel and the builder need about a day's personal time.
public sealed record PersonalTime(
    IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> Intervals, // P
    int Minutes,                         // personal_minutes
    DateTimeOffset? ExpectedStartAt,     // fixed: ES; flexible: null
    DateTimeOffset? ExpectedEndAt,       // fixed: EE; flexible: null
    DateTimeOffset? NotStartedUntil,     // §5.5
    bool ExcusesDay);                    // §5.5

public static class PersonalTimeRule
{
    public static IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> IntervalsFor(
        DateOnly date, DayPlan? plan, IReadOnlyCollection<WorkCredit>? requests);   // §5.1 P
    public static PersonalDay Apply(
        PairingResult pairing, DayPlan plan,
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> credit,              // K
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> personal);           // P — steps 1–8, 12
    public static int MinutesOutside(DateTimeOffset from, DateTimeOffset to,
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> personal);           // |[from,to] \ P|
}

public sealed record PersonalDay(int WorkedMinutes, int CreditedMinutes, int BreakTakenMinutes,
    int BreakDeductionMinutes, DateTimeOffset? FirstPresenceAt, DateTimeOffset? LastPresenceAt,
    DateTimeOffset? CreditedFromAt, PersonalTime Personal);
```

`AttendanceDayBuilder.BuildDay`: compute `P = PersonalTimeRule.IntervalsFor(date, plan, workCredits)`
first. **`P` empty ⇒ the existing code runs unchanged.** Otherwise use `PersonalTimeRule.Apply` for
worked / credited / break / presence edges, steps 6, 9, 10 for fixed days (§5.4 for flexible), §5.5
for status and shortfall, fill `AttendanceDayFacts.PersonalMinutes` and
`ComputedAttendanceDay.Personal` (also `CreditedFromAt` = first K′ start).

---

## 6. API contract

Enums travel by name. Every new request field is optional with a default unless marked **required**.

### 6.1 New endpoints

| Method · path | Auth | Body → Response | Errors |
|---|---|---|---|
| `PUT /api/me/requests/{id}` | employee token (`/api/me` group) | `{ version*, absenceTypeId*, fromDate*, toDate*, fromTime?, toTime?, note? }` → 200 `AbsenceRequestDto` | 400 `davomat.sorov_davri_notogri`, `davomat.sorov_uzun`, `davomat.sorov_vaqti_notogri`, `davomat.sorov_turi_yoq`, `davomat.yoqlik_turi_faolsiz`, `davomat.yoqlik_turi_xodimga_yopiq`, `davomat.izoh_uzun`; 404 `davomat.sorov_topilmadi`, `davomat.yoqlik_turi_topilmadi`; 409 `davomat.sorov_qaror_qabul_qilingan`, `davomat.sorov_ozgargan`, `davomat.sorov_takroriy`; 401; 403 `xodim.hisob_faol_emas` |
| `PUT /api/me/schedule-requests/{id}` | employee token; body ≤ 32 KB (`MaxScheduleRequestBytes`) | `{ version*, kind*, days?, swaps?, note? }` (`days[]` = `ScheduleChangeDayBody`) → 200 `ScheduleChangeRequestDto` | 400 every create code (`davomat.jadval_*`, `davomat.jadval_sorovi_*` incl. `…_sana_oynasi`, `…_tanaffus_oraligi`, `…_tanaffus_davomiyligi`, `davomat.izoh_uzun`); 404 `davomat.sorov_topilmadi`; 409 `davomat.sorov_qaror_qabul_qilingan`, `davomat.sorov_ozgargan`, `davomat.jadval_sorovi_takroriy`; 413; 401; 403 |
| `GET /api/attendance/schedule-requests/break-rule` | `attendance.view` | → 200 `RequestBreakRuleDto` | 401, 403 |

Request records (in `MeEndpoints.cs`, no `Guid = default`):
`MyAbsenceRequestUpdateBody(int Version, Guid AbsenceTypeId, DateOnly FromDate, DateOnly ToDate,
TimeOnly? FromTime = null, TimeOnly? ToTime = null, string? Note = null)`;
`MyScheduleChangeRequestUpdateBody(int Version, ScheduleChangeKind Kind,
IReadOnlyList<ScheduleChangeDayBody>? Days = null, IReadOnlyList<ScheduleSwapBody>? Swaps = null,
string? Note = null)`.

```json
PUT /api/me/requests/7f0c…   { "version": 0, "absenceTypeId": "…", "fromDate": "2026-10-21",
  "toDate": "2026-10-21", "fromTime": "14:00:00", "toTime": "16:00:00", "note": "Shifokorga" }
GET /api/attendance/schedule-requests/break-rule
  → { "from": "12:00:00", "to": "15:00:00", "minutes": 60, "fridayMaxMinutes": 120 }
```

### 6.2 Changed endpoints

| Method · path | Permission | Change |
|---|---|---|
| `PUT /api/attendance/requests/{id}` | `attendance.requests.manage` | `AbsenceRequestBody` appends `int? Version = null` (ignored by `POST /requests`); sets `edited_at`; + 409 `davomat.sorov_ozgargan`, + 400 `davomat.izoh_uzun` |
| `POST /api/attendance/requests`, `POST /api/me/requests` | unchanged | + 400 `davomat.izoh_uzun` (domain `Create`) |
| `POST /api/attendance/requests/{id}/decide`, `POST /api/attendance/schedule-requests/{id}/decide` | same | `AbsenceDecisionBody(bool Approve, string? DecisionNote = null, int? Version = null)`; + 409 `davomat.sorov_ozgargan`; absence: + 400 `davomat.izoh_uzun`. Update the decide description: approving a personal kind's timed request now removes its hours from worked time (§5) |
| `POST /api/attendance/requests/{id}/cancel`, `…/schedule-requests/{id}/cancel`, `POST /api/me/requests/{id}/cancel`, `POST /api/me/schedule-requests/{id}/cancel` | unchanged | + 409 `davomat.sorov_ozgargan` (concurrent write, §3.3 layer 1) |
| `PUT /api/attendance/schedule-requests/{id}` | `attendance.requests.manage` | `ScheduleChangeRequestBody` appends `int? Version = null`; + 409 `davomat.sorov_ozgargan`; + 400 break-rule codes |
| `POST /api/attendance/schedule-requests`, `POST /api/me/schedule-requests` | unchanged | + 400 `davomat.jadval_sorovi_tanaffus_oraligi`, `davomat.jadval_sorovi_tanaffus_davomiyligi` (add both to the `ErrorCodes` description constant) |
| `GET /api/me` | employee token | `MeDto` appends `RequestBreakRuleDto? RequestBreakRule = null` (always filled by the new server); `MyAbsenceTypeDto` appends `bool CountsAsWorkTime = false` |
| `GET /api/attendance/requests`, `GET /api/me/requests` | unchanged | `AbsenceRequestDto` appends `int Version = 0, DateTimeOffset? EditedAt = null, bool EditedByEmployee = false` |
| `GET /api/attendance/schedule-requests`, `/{id}`, `GET /api/me/schedule-requests` | unchanged | `ScheduleChangeRequestDto` appends the same three fields |
| `GET /timesheet`, `GET /export/timesheet`, `GET /api/me/attendance` | unchanged | `TimesheetCellDto` appends `int PersonalMinutes = 0` (stored row; synthesized cell 0); `TimesheetTotalsDto` appends `int PersonalMinutes = 0` (Σ cells) |
| `GET /reports/summary`, `GET /export/summary` | unchanged | `AttendanceSummaryDto` appends `int PersonalMinutes = 0` (Σ stored days) |
| `GET /api/settings`, `PUT /api/settings` | `settings.manage` | the 4 keys of §2.2; PUT + 400 `davomat.tanaffus_qoidasi_nomuvofiq` |
| Board, dashboard, trend | `attendance.view` | DTOs unchanged; numbers follow §5.6 |

`EditedByEmployee = edited_by_account_id != null`. `RequestBreakRuleDto(TimeOnly From, TimeOnly To,
int Minutes, int FridayMaxMinutes)` lives in `ScheduleChangeDtos.cs`.

**Excel** (`AttendanceExcelWriter`) — appended at the **end**, existing positions unchanged:
«Kunlar» and the «Tabel» totals block and «Xulosa»: «Shaxsiy vaqt (daq.)», «Shaxsiy vaqt (soat:daq)».
Exceptions sheets: no change.

### 6.3 New error codes

| Code | HTTP | Server message (uz) | Where defined |
|---|---|---|---|
| `davomat.sorov_ozgargan` | 409 | «So'rov boshqa joyda o'zgartirilgan — yangilangan holatini ko'rib, qaytadan urinib ko'ring.» | `AbsenceRequestErrors` + `ScheduleChangeRequestErrors` (same code) |
| `davomat.jadval_sorovi_tanaffus_oraligi` | 400 | `$"Tanaffus {From:HH:mm}–{To:HH:mm} oralig'ida bo'lishi kerak."` | `RequestBreakRule` |
| `davomat.jadval_sorovi_tanaffus_davomiyligi` | 400 | `$"Tanaffus {Minutes} daqiqa (jumada {Minutes}–{FridayMaxMinutes}) bo'lishi va oynasiga sig'ishi kerak."` | `RequestBreakRule` |
| `davomat.tanaffus_qoidasi_nomuvofiq` | 400 | «Tanaffus qoidasi noto'g'ri: oraliq boshi oxiridan oldin, tanaffus jumadagi chegaradan oshmasligi va oraliqqa sig'ishi kerak.» | `SettingsErrors` |

`davomat.izoh_uzun` already exists (translated in web and app). Web translations are mandatory
(`./bonvi check` scans backend codes); the app adds the three request codes to `APP_CODES` in
`app/scripts/check-locales.mjs`.

---

## 7. UI — Apple iOS HIG look, existing primitives only

### 7.1 Employee app (`app/src`) — hand-written contract

**Contract** (`api/types.ts`): `AbsenceRequestDto` and `ScheduleChangeRequestDto` + `version?: number`,
`editedAt?: string | null`, `editedByEmployee?: boolean`; `TimesheetCellDto` and `TimesheetTotalsDto`
+ `personalMinutes?: number`; `AbsenceTypeDto` + `countsAsWorkTime?: boolean`; `MeDto` +
`requestBreakRule?: { from: string; to: string; minutes: number; fridayMaxMinutes: number } | null`;
`UpdateAbsenceRequest = CreateAbsenceRequest & { version: number }`,
`UpdateScheduleChangeRequest = CreateScheduleChangeRequest & { version: number }`.
`api/http.ts`: `method` accepts `'PUT'`. `api/client.ts` + `real.ts` + `mock/index.ts`:
`updateRequest(id, body)` → `PUT /api/me/requests/{id}`, `updateScheduleRequest(id, body)` →
`PUT /api/me/schedule-requests/{id}`. Mock: bumps `version`, sets `editedAt`, 409 on a non-pending
request; one past mock day gets `personalMinutes: 120`.

**Routes** (`app/router.tsx`, pushed, no tab bar): `/sorovlar/:id/ozgartirish` → `RequestNewScreen` in
edit mode; `/sorovlar/jadval/:id/ozgartirish` → `ScheduleRequestNewScreen` in edit mode.

| Screen / file | Change |
|---|---|
| `screens/request-detail.tsx` | When `status === 'Pending'` **and** the request's kind is in `me.absenceTypes` (active and employee-requestable — otherwise the edit could not be saved without changing HR's kind): `Button variant="secondary" block` «O‘zgartirish» → `/sorovlar/:id/ozgartirish`, placed **above** the existing danger «So‘rovni bekor qilish». Card row «O‘zgartirilgan» with `format.date(editedAt)` when `editedAt` is set. No confirm sheet (UX §E.6 policy) |
| `screens/schedule-request-detail.tsx` | Same button for every `Pending` request → `/sorovlar/jadval/:id/ozgartirish`; same «O‘zgartirilgan» row |
| `screens/request-new.tsx` (edit mode = `useParams().id` present) | Title `requests.editTitle`. Prefill from the `useRequests()` cache: mode (`fromTime` set → part; `fromDate == toDate` → whole; else multi), kind, dates (`toDate` for multi, else `addDays(fromDate, 1)` as today's default), times `HH:mm`, note. `dirty` compares with the prefilled values. Primary button «Saqlash» (`common.save`). Submit → `api.updateRequest(id, { …body, version: request.version ?? 0 })` → `invalidateRequests()`, toast `requests.saved`, `navigate(-1)` back to the detail. Not found → the detail's EmptyState; not `Pending` → `InlineMessage` (warning) `requests.notEditable` + button back, form hidden. 409/400 → `InlineMessage` (danger) titled `requests.editFailed` with `errorText(error).body`; on 409 also `invalidateRequests()`; the form keeps the input. **New in both modes:** under «Yo‘qlik turi» a `helper`: `requests.kindWorkTime` when the chosen kind has `countsAsWorkTime`, else `requests.kindPersonalTime` |
| `screens/schedule-request-new.tsx` (edit mode) | Title `requests.editTitle`. Prefill from `useScheduleRequests()` cache: `kind`; Change → one `DayDraft` per day with `settledFor = day.date` (the plan never overwrites stored values), `isWorking`, times, `hasBreak` (window present), break times, `breakMinutes = day.isWorking ? day.breakMinutes : null` (keeps the stored effective length — same reason as the HR edit dialog), `isFlexible`, `requiredMinutes` (null when equal to `defaultRequired`); Swap → pairs from `swapPairs(day list)`. Submit → `api.updateScheduleRequest(id, { …body, version })` → `invalidateScheduleRequests()`, toast, back. Errors and not-pending exactly as the absence form. **Break rule (both modes):** when `me.requestBreakRule` exists, `kind === 'Change'`, the day is working and `L > 0` (`L = draft.breakMinutes ?? (draft.hasBreak ? breakWindowMinutes(draft.breakStart, draft.breakEnd) : 0)` — the formula `defaultRequired` already uses): `DayErrors` gains `breakRule: string \| null`, computed **after** every existing rule with §4.3 (Friday = `parseIsoDate(date)?.weekday === 5`), messages `schedule.breakRuleWindow` / `schedule.breakRuleLength` / `schedule.breakRuleLengthFriday` / `schedule.breakRuleWindowShort`; rendered as `<p className="text-footnote text-danger-text" data-invalid>` directly after the break block (after the `breakKept` note), so `scrollToFirstError` finds it. While `hasBreak` is on, a footnote `text-fg-tertiary` under the break fields: `schedule.breakRuleHint` (or `…HintFriday` on a Friday) |
| `ui/day-panel.tsx` | Row «Shaxsiy vaqt» (`calendar.personal`, `format.duration`) when `personalMinutes > 0`, after the credited row |
| `screens/home.tsx` «Bugungi holat» | `CardRow` «Shaxsiy vaqt» after «Ishlangan» when `todayCell.personalMinutes > 0` |
| `screens/calendar.tsx` «Oylik natija» | `StatRow` `calendar.totalPersonal` when `totals.personalMinutes > 0`; `data/attendance.ts` `MonthTotals.personalMinutes` from server totals (absent ⇒ 0) |

### 7.2 HR web (`web/src/features/verification`) — types from `./bonvi gen`

| Screen / file | Change |
|---|---|
| `api/attendance-queries.ts` | `useScheduleRequestBreakRule()` (`GET …/schedule-requests/break-rule`, `staleTime` 5 min, key `['attendance','schedule-requests','break-rule']`); invalidate it when the settings card saves. Request update/decide mutations: on error `davomat.sorov_ozgargan` or `davomat.sorov_qaror_qabul_qilingan` invalidate both request lists and `pendingRequests` |
| `components/absence-decision-dialog.tsx`, `schedule-decision-dialog.tsx` | Send `version: request.version`. On `davomat.sorov_ozgargan` / `…_qaror_qabul_qilingan`: show the translated `Alert`, **disable the confirm button**, keep only «Yopish» — HR re-opens the refreshed row. Description gains the edit marker (below) when `editedAt` is set |
| `components/absence-request-dialog.tsx`, `schedule-request-dialog.tsx` | Edit mode sends `version: request.version`; stale → same Alert, save disabled. Absence dialog: `hint` under the kind `Select` — `attendance.requestKindWorkTime` / `attendance.requestKindPersonalTime` from the catalog item's `countsAsWorkTime`. Schedule dialog: `changeProblem` runs §4.3 **after** `validateScheduleDay` (new `validateRequestBreak(day, date, rule)` in `lib/attendance-schedule-rules.ts`, returns the server code; Friday = `isoWeekday(date) === 5`; `L` = `toDayInput(row).breakMinutes`, which already equals the server's effective length); messages `attendance.requestBreakWindowRule` / `…LengthRule` / `…LengthRuleFriday` / `…WindowShort` with the rule's numbers; footnote `attendance.requestBreakRuleHint` under the «Tanaffus» fields. Rule not loaded yet ⇒ no client check (server still enforces) |
| `components/absence-requests-list.tsx`, `schedule-requests-list.tsx` | In the note column, under «Ilovadan»: `attendance.requestEditedInApp` (`editedByEmployee`) or `attendance.requestEditedAt` (HR edit), date-time via `dateTime(editedAt)`, `text-caption2 text-[var(--fg-tertiary)]` |
| new `components/request-break-rule-card.tsx` on `attendance-schedules-page.tsx` (bottom, only for `settings.manage` — like `AttendanceRetentionCard` on the devices page) | Card `IconTile` (lucide `Coffee`, tone `orange`) + title `attendance.breakRuleTitle` + hint `attendance.breakRuleHint`; four `TextField`s: «Oraliq boshi» and «Oraliq oxiri» `type="time"` (minutes ↔ `HH:mm`), «Tanaffus, daqiqa» and «Jumada ko‘pi bilan, daqiqa» `type="number"`; reads/writes via `useSystemSettings(true)` / `useSaveSettings` (existing `/api/settings`), sends only changed keys, server error via `errorMessage(problemCode(…))` |
| `components/attendance-timesheet-grid.tsx` `CellDetails` | Line with lucide `CalendarMinus`: `attendance.personalLine` when `personalMinutes > 0`, after the credited line. `TotalsDetails`: fact «Shaxsiy vaqt» |
| `components/employee-attendance-tab.tsx` | Day cell: caption `attendance.personalShort` + duration (like the credited caption, colour `--fg-secondary`); totals: `Total` «Shaxsiy vaqt» when > 0 |
| `components/attendance-reports-page.tsx` + `lib/attendance-presentation.ts` | Summary table: «Shaxsiy vaqt» duration column after «Kam ishlangan» |
| catalog (`features/catalog`) | Locale value `catalog.workTimeHintOff` replaced (the old text is now false) |

Permission gating unchanged: editors and dialogs render for `attendance.requests.manage`, read views
for `attendance.view`, the rule card for `settings.manage`.

### 7.3 i18n — `uz-Latn` values (`uz-Cyrl` = transliteration, `ru`/`en` = translation; reuse an existing key with the same text)

**App** (`app/src/i18n/locales/*.json`): `requests.edit` «O‘zgartirish» · `requests.editTitle`
«So‘rovni o‘zgartirish» · `requests.saved` «So‘rov o‘zgartirildi» · `requests.editFailed` «So‘rovni
o‘zgartirib bo‘lmadi» · `requests.editedAt` «O‘zgartirilgan» · `requests.notEditable` «Bu so‘rovni
endi o‘zgartirib bo‘lmaydi — u bo‘yicha qaror qabul qilingan yoki u bekor qilingan.» ·
`requests.kindWorkTime` «Bu vaqt ish vaqti hisoblanadi.» · `requests.kindPersonalTime` «Sababli,
lekin ish vaqtiga kirmaydi: soatlar ishlangan vaqtdan ayriladi.» · `calendar.personal` «Shaxsiy
vaqt» · `calendar.totalPersonal` «Shaxsiy vaqt» · `schedule.breakRuleHint` «Tanaffus {{from}}–{{to}}
oralig‘ida, {{n}} daqiqa.» · `schedule.breakRuleHintFriday` «Jumada tanaffus {{from}}–{{to}}
oralig‘ida, {{min}}–{{max}} daqiqa.» · `schedule.breakRuleWindow` «Tanaffus {{from}}–{{to}}
oralig‘ida bo‘lishi kerak.» · `schedule.breakRuleLength` «Tanaffus aniq {{n}} daqiqa bo‘lishi kerak.»
· `schedule.breakRuleLengthFriday` «Jumada tanaffus {{min}} dan {{max}} daqiqagacha bo‘lishi kerak.»
· `schedule.breakRuleWindowShort` «Tanaffus ({{n}} daqiqa) oynasiga sig‘maydi.» · errors
`davomat.sorov_ozgargan` «So‘rov boshqa joyda o‘zgartirilgan. Ro‘yxatni yangilab, qaytadan urinib
ko‘ring.», `davomat.jadval_sorovi_tanaffus_oraligi` «Tanaffus ruxsat etilgan oraliqdan tashqarida.»,
`davomat.jadval_sorovi_tanaffus_davomiyligi` «Tanaffus uzunligi qoidaga mos emas.»

**Web** (`web/src/shared/i18n/locales/*.json`, `attendance.*` unless stated): `personalMinutes`
«Shaxsiy vaqt» · `personalLine` «Shaxsiy vaqt: {{duration}} — ish vaqtiga kirmaydi» ·
`personalShort` «Shaxsiy» · `requestEditedAt` «O‘zgartirilgan · {{date}}» · `requestEditedInApp`
«Ilovada o‘zgartirilgan · {{date}}» · `requestKindWorkTime` «Tasdiqlansa, bu vaqt ish vaqti
hisoblanadi.» · `requestKindPersonalTime` «Tasdiqlansa, so‘ralgan soatlar ishlangan vaqtdan
ayriladi (sababli, lekin ish vaqtiga kirmaydi); butun kunlik so‘rov kunni «Sababli» qiladi.» ·
`breakRuleTitle` «Jadval so‘rovidagi tanaffus» · `breakRuleHint` «Xodim va HR jadval so‘rovida
tanaffusni shu qoida bo‘yicha belgilaydi. Tanaffussiz kun ham mumkin. Avval yuborilgan so‘rovlar
qayta tekshirilmaydi.» · `breakRuleFrom` «Oraliq boshi» · `breakRuleTo` «Oraliq oxiri» ·
`breakRuleMinutes` «Tanaffus, daqiqa» · `breakRuleFridayMax` «Jumada ko‘pi bilan, daqiqa» ·
`requestBreakRuleHint` «Qoida: {{from}}–{{to}} oralig‘ida, {{n}} daqiqa; jumada {{n}}–{{max}}
daqiqa.» · `requestBreakWindowRule` «Tanaffus {{from}}–{{to}} oralig‘ida bo‘lishi kerak.» ·
`requestBreakLengthRule` «Tanaffus aniq {{n}} daqiqa bo‘lishi kerak.» · `requestBreakLengthRuleFriday`
«Jumada tanaffus {{min}}–{{max}} daqiqa bo‘lishi kerak.» · `requestBreakWindowShort` «Tanaffus
({{n}} daqiqa) oynasiga sig‘maydi.» · `catalog.workTimeHintOff` (changed) «Vaqtli so‘rov — shaxsiy
vaqt: tasdiqlansa, soatlar ishlangan vaqtdan ayriladi, kechikish va erta ketish hisoblanmaydi. Butun
kunlik so‘rov «Kelmadi» ni «Sababli» qiladi.» · `errors` → the four codes of §6.3 (the app texts for
the three request codes; `davomat.tanaffus_qoidasi_nomuvofiq` «Tanaffus qoidasi noto‘g‘ri: oraliq
boshi oxiridan oldin, tanaffus jumadagi chegaradan oshmasligi va oraliqqa sig‘ishi kerak.»).

---

## 8. Tests

`./bonvi test`. Uzbek test names in the project style.

### 8.1 Domain — `api/tests/Bonvi.Domain.Tests/Attendance/`

* `RequestBreakRuleTests.cs` (new): one test per B1–B19 of §4.4, via `ScheduleChangeRequest.Create`
  with `RequestBreakRule.Default` (B19 with the custom rule); `Create` without a rule accepts B5
  (backward compatibility of existing callers); `IsConsistent` false for from ≥ to, minutes >
  fridayMax, fridayMax > to − from.
* `AbsenceRequestTests.cs` (new): `Version` +1 on `Update`, `Approve`, `Reject`, `Cancel`,
  `CancelBySubmitter`; `Update(at, account)` sets `EditedAt`/`EditedByAccountId`, HR form
  (`account = null`) sets only `EditedAt`; note and decision note of 2001 chars →
  `davomat.izoh_uzun`.
* `ScheduleChangeRequestTests.cs` (+): the same version and edit-marker cases.

### 8.2 Computation — `api/tests/Bonvi.Application.Tests/Attendance/Computation/`

* `AttendanceScenario.cs`: helper `PersonalTime(DateOnly date, string fromTime, string toTime)` adding
  `new WorkCredit(date, date, from, to, CountsAsWorkTime: false)` to the same `_credits` list.
* `PersonalTimeTests.cs` (new): **one test per E1–E21** asserting every listed figure (worked,
  personal, late, early, taken, deducted, credited, status, overtime, shortfall where flexible), plus:
  `Shaxsiy_sorov_ish_vaqtiga_qoshilmaydi` (a personal request alone never produces
  `CreditedMinutes`); `Sorovsiz_kun_aynan_avvalgidek` (E17 compared field by field with a build
  without the credits list); personal interval entirely outside the plan → facts equal the no-request
  facts.
* `DashboardStatusRuleTests.cs` (+): the panel checks under §5.8; `HasNotStarted` with and without
  `notStartedUntil`; `Classify` with `personal = null` unchanged for the existing cases.
* `WorkCreditTests.cs`: existing tests untouched and green (regression for credit).

### 8.3 Settings — `api/tests/Bonvi.Application.Tests/Settings/UpdateSettingsCommandHandlerTests.cs` (+)

Cross-key rule: from ≥ to → `davomat.tanaffus_qoidasi_nomuvofiq`; minutes > fridayMax → same;
fridayMax > to − from → same; a partial update validated against current values; a request without
the four keys reads nothing.

### 8.4 API / RBAC — `api/tests/Bonvi.Api.IntegrationTests/`

* `RequestEditApiTests.cs` (new): employee edits own pending absence request → 200, `version` +1,
  `editedByEmployee`, `editedAt`, audit row with actor kind `EmployeeAccount`; foreign request → 404;
  HR token on both employee `PUT`s → 401; anonymous → 401; after approve, after reject and after
  cancel → 409 `davomat.sorov_qaror_qabul_qilingan`; stale `version` → 409
  `davomat.sorov_ozgargan`; parity with create (period 400, HR-only kind 400, inactive kind 400,
  unknown kind 404, overlap with another own request 409, same dates as itself 200, note 2001 → 400);
  schedule edit Change → Change, Change → Swap (re-snapshot), window −32 → 400, break rule → 400 codes,
  stale → 409; HR `PUT` and `decide` with stale `version` → 409, with current → 200, without →
  200; **race**: an employee edit and an HR decision sent in parallel (`Task.WhenAll`) → exactly one
  2xx, the other 409, never 500 (both kinds); employee cancel vs HR decide in parallel → same.
* `RequestBreakRuleApiTests.cs` (new): `GET …/break-rule` — `attendance.view` 200, a user without it
  403, anonymous 401; `PUT /api/settings` of the four keys (`settings.manage`) incl. the cross-key 400
  and a non-`settings.manage` user 403; after changing the rule, create enforces the new numbers;
  `GET /api/me` returns `requestBreakRule` and `absenceTypes[].countsAsWorkTime`.
* `PersonalTimeApiTests.cs` (new): with a schedule assigned — approve a personal request (E1 passes
  ingested through the existing test helpers) → stored day `personal_minutes = 120`,
  `worked_minutes = 360`; timesheet cell + totals `personalMinutes`; `/api/me/attendance` cell;
  summary `personalMinutes`; Excel: the two new headers are the last columns of «Kunlar», «Tabel»
  totals and «Xulosa», every existing header keeps its position; HR cancel → day recomputed back
  (E16); flipping the kind's `counts_as_work_time` → days recomputed; E9 day reads `Absent` in the
  timesheet and in `GET /dashboard`.
* RBAC matrix (`AttendanceManagementApiTests` endpoint tables ~line 2936/2968): add `GET
  /schedule-requests/break-rule` to the `attendance.view` list.
* `OpenApiDocumentTests` stays green.

### 8.5 Migration — `SorovTahririVaShaxsiyVaqtMigrationTests.cs` (new)

Mirror `ErkinJadvalMigrationTests`: migrate to `20261020051430_ErkinJadval`; insert an absence
request, a schedule request + day and an attendance day by SQL; Up ⇒ `version = 0`, `edited_at` /
`edited_by_account_id` NULL, `personal_minutes = 0`, every old column unchanged; an «old image» insert
without the new columns succeeds in all three tables; Down ⇒ columns gone, row counts unchanged.

### 8.6 Existing tests to adjust (fixtures only — assertions keep their meaning; never disable the rule)

They send schedule-change **Change** days whose break violates the default rule on non-Fridays, and
their dates are relative to today (they would fail on some weekdays):

| Test | Fixture now | Fix |
|---|---|---|
| `FlexibleScheduleApiTests.Tasdiqlangan_erkin_sorov_kunni_kechikishsiz_va_kam_ishlangan_bilan_hisoblaydi` | 12:00–14:00 (120) on today −3 | move `sana` to the most recent **Friday** at least 1 day before today (the 120-minute break is legal on Friday, every expected figure stays; the test schedule works all 7 days) |
| `FlexibleScheduleApiTests.Xodim_ilovasida_erkin_reja_sorov_va_oylik_davomat` | app request 12:00–14:00 (120) on today +3 | move `keyin` to the first **Friday** ≥ today +1 (inside the +92 window) |
| `AttendanceManagementApiTests.Kutayotgan_jadval_sorovi_tahrirlanadi` | PUT 13:00–16:00 + 60 and 13:00–14:30 (90) | 12:00–15:00 + 60 and 13:00–14:00 (assert the window length 60 is stored when `breakMinutes` is omitted) |
| `EmployeeAppApiTests.Xodim_jadval_sorovi_yuboradi_va_bekor_qiladi` | 12:30–14:30 (120) | 12:30–13:30; assert `breakEnd` 13:30 |

If moving a date breaks another assumption of a test, use a compliant break instead (60 minutes inside
12:00–15:00) and recompute that test's dependent expectations by the existing formulas. Swap requests
and Change days without a break are unaffected.

### 8.7 Frontend gates (no frontend test runner exists)

Web: `./bonvi pnpm typecheck`, `./bonvi pnpm build`, `./bonvi check`. App:
`docker compose exec app sh -c "pnpm typecheck && pnpm check:locales && pnpm build"`. Manual pass in
dev: edit both request kinds in the app (pending, then after an HR decision); a stale decision in
the web (edit in the app while the dialog is open); the rule card; a break-rule error in both forms;
a personal day in the timesheet popover, employee card, app day panel and summary.

---

## 9. Work units

**Shared files — one author at a time, never parallel:**
`Application/Attendance/Dtos/AttendanceDtos.cs`, `Dtos/ScheduleChangeDtos.cs`, `Application/Me/MeDtos.cs`,
`Application/Common/DependencyInjection.cs`, `Attendance/AttendanceRegistration.cs`,
`Api/Endpoints/AttendanceEndpoints.cs`, `Api/Endpoints/MeEndpoints.cs` (all B4) ·
`Domain/Settings/SystemSettingKeys.cs` (B1) · `AttendanceConfigurations.cs` + the migration +
`AppDbContextModelSnapshot.cs` (B2) · `Infrastructure/Persistence/ReadModels/AttendanceReadModel.cs`
and `AttendanceReadModel.ScheduleRequests.cs` (B5 only) · web locales ×4 (W) · app locales ×4 and
`app/scripts/check-locales.mjs` (A) · `web/src/shared/api/generated/**` (G, regenerated once) ·
`docs/ASSUMPTIONS.md` (append-only).

| Unit | Files | Depends on | Parallel? |
|---|---|---|---|
| **B1** Domain | `Domain/Attendance/AbsenceRequest.cs`, `ScheduleChangeRequest.cs`, new `RequestBreakRule.cs`, `AttendanceDay.cs`, `AttendanceRules.cs`; `Domain/Settings/SystemSettingKeys.cs`; `Domain/Catalog/SimpleCatalogs.cs` (comment); §8.1 tests | — | yes, with A |
| **B2** Persistence | `Persistence/Configurations/AttendanceConfigurations.cs`, migration `SorovTahririVaShaxsiyVaqt` + snapshot, `Persistence/Auditing/AuditCatalog.cs`, `Persistence/PostgresFault.cs`, `Application/Abstractions/IDatabaseFault.cs`; §8.5 | B1 | yes, with B3 (disjoint files) |
| **B3** Computation | `Computation/WorkCredit.cs`, new `Computation/PersonalTime.cs`, `Computation/AttendanceDayBuilder.cs`, `Computation/DashboardStatusRule.cs`, `Infrastructure/Persistence/AbsenceQuery.cs`, `Recompute/AttendanceScheduledJobs.cs` (comment); §8.2 (`AttendanceScenario.cs`, `PersonalTimeTests.cs`, `DashboardStatusRuleTests.cs`) | B1 | yes, with B2 |
| **B4** Use cases, DTOs, endpoints | `Requests/AbsenceRequestCommands.cs`, `Requests/ScheduleChangeRequestUseCases.cs`, new `Requests/RequestBreakRuleReader.cs`, `Me/MyAbsenceRequestUseCases.cs`, `Me/MyScheduleRequestUseCases.cs`, `Me/GetMeQuery.cs`, `Me/MeDtos.cs`, `Dtos/AttendanceDtos.cs`, `Dtos/ScheduleChangeDtos.cs`, `Settings/UpdateSettingsCommand.cs`, `Settings/Common/SettingsErrors` (its file), `Common/DependencyInjection.cs`, `AttendanceRegistration.cs`, `Api/Endpoints/AttendanceEndpoints.cs`, `Api/Endpoints/MeEndpoints.cs` (bodies, routes, `WithDescription` error lists); §8.3 | B1, B2 | no — sole author of the shared DTO/endpoint files |
| **B5** Read models, reports, Excel | `ReadModels/AttendanceReadModel.cs` (request DTO mapping of version/edit fields, timesheet cells + totals, summary, dashboard/trend `Classify(…, personal)`, board `lateAfter`), `ReadModels/AttendanceReadModel.ScheduleRequests.cs`, `Reports/AttendanceExcelWriter.cs`, `Abstractions/IAttendanceReadModel.cs` (only if a row record changes) | B3, B4 | no |
| **T** API tests | §8.4, §8.6 | B2, B4, B5 | no |
| **G** `./bonvi gen` | `web/src/shared/api/generated/**` | B4, B5 merged, API running | no |
| **W** Web, in order: W1 queries + rules mirror + both request dialogs + both decision dialogs + both lists; W2 rule card + timesheet + employee tab + reports + catalog hint | §7.2 files, web locales ×4 | G | sequential inside W; parallel with A |
| **A** App | §7.1 files: `api/types.ts`, `api/http.ts`, `api/client.ts`, `api/real.ts`, `mock/index.ts`, `mock/data.ts`, `app/router.tsx`, `screens/request-detail.tsx`, `screens/request-new.tsx`, `screens/schedule-request-detail.tsx`, `screens/schedule-request-new.tsx`, `screens/home.tsx`, `screens/calendar.tsx`, `ui/day-panel.tsx`, `data/attendance.ts`, `data/schedule-requests.ts` (rule helper), app locales ×4, `scripts/check-locales.mjs` | this SPEC (§6 contract); integrate against the real API after B4/B5 | yes, with every backend unit and W |
| **D** Docs | amendment notes: `SPEC-verifikatsiya-sorovlar.md` §B3 («AMENDED 2026-10-20 → SPEC-verifikatsiya-sorov-tahriri.md §5»), §B4 (employee edit); `SPEC-verifikatsiya-jadval-sorovi.md` §C1 (break rule) and §C3 (employee PUT); `SPEC-verifikatsiya-erkin-jadval.md` §2.2 row 10 (shortfall − personal); `UX-xodim-ilovasi.md` §A.3 (two edit routes, «O‘zgartirish»); `DEPLOY-CHECKLIST.md` release entry (§10) | all | last |

Order: B1 → (B2 ∥ B3) → B4 → B5 → T → G → W1 → W2; A alongside from the start; D at the end.

---

## 10. Rollout

* Impact at deploy: **computation none** — production has no schedule assignment (re-check:
  `select count(*) from schedule_assignments where deleted_at is null;`). Editing and the break rule
  are live immediately.
* Before deploy (read-only): `select status, count(*) from schedule_change_requests where deleted_at is
  null group by status;` — pending requests that violate the rule can still be decided; tell HR that
  editing them requires a compliant break.
* Usual safe-deploy procedure (`DEPLOY-CHECKLIST.md`): rollback image tag, backup, migration tried on a
  prod copy (Up, second Up, Down, Up), deploy while HR is idle. This release carries wave 7 too (both
  migrations, in order `ErkinJadval` → `SorovTahririVaShaxsiyVaqt`).
* After deploy: the owner (settings.manage) sees the rule card with 12:00 / 15:00 / 60 / 120 on
  «Ish jadvallari»; nothing to configure unless the policy differs.

## 11. Definition of Done

- [ ] `./bonvi test` fully green; §8.1–§8.6 present; every B1–B19 and E1–E21 example has its own asserting test; the existing computation and request suites are unchanged except the four fixtures of §8.6.
- [ ] A day without personal requests computes exactly as before (E17 + untouched `WorkCreditTests`, `FlexibleDayTests`, `BreakDeductionTests`, `AttendanceDayBuilderTests`).
- [ ] Migration `SorovTahririVaShaxsiyVaqt` read by hand (7 ADD COLUMN only); `upgrade head` works on a fresh DB and on a prod copy; Down tested.
- [ ] `./bonvi gen` succeeds (OpenAPI builds — no `Guid = default`); `OpenApiDocumentTests` green.
- [ ] Web: `./bonvi pnpm typecheck`, `./bonvi pnpm build`, `./bonvi check` green (4 locales + every backend code translated).
- [ ] App: `pnpm typecheck`, `pnpm check:locales`, `pnpm build` green.
- [ ] Every new endpoint is protected (employee policy / `attendance.view`) and has an RBAC test; changed endpoints keep their policies.
- [ ] The parallel edit-vs-decide and cancel-vs-decide tests never produce a 500.
- [ ] Excel: existing column positions unchanged; the two «Shaxsiy vaqt» columns are last.
- [ ] `docs/ASSUMPTIONS.md` has this wave's `[2026-10-20]` lines (written with this SPEC); D amendment notes written.

## 12. Open questions — each has a standing decision

| # | Question | Decision (stands unless the owner says otherwise) |
|---|---|---|
| Q1 | The owner's example has 5 minutes before 08:30 and after 17:30: worked 6:00 or 6:10? | **6:00** — on a fixed day with personal time `worked ≤ plan − personal` (§5.3 step 6); presence outside the plan does not buy back unpaid time and overtime is 0 that day. Side effect E20 (schedule without break window). Removing the cap is one line; the owner's example would then read 6:10 |
| Q2 | Night/evening shifts and the 12:00–15:00 rule | **Rule applies literally** — such a day can be requested only without a break (B15). Alternative: exempt shifts that do not overlap the window |
| Q3 | Panel/timesheet while a morning personal interval runs and the person is not in yet | **«Hali boshlanmagan» / `Planned`** until the interval ends (the existing not-started mechanism), then «Kelmadi». Alternative: «Sababli» |
| Q4 | Absent all day with a 2-hour personal request | **«Kelmadi»** (was «Sababli»); only a personal interval covering the whole plan excuses the day |
| Q5 | Presence inside an approved personal interval (came back early, never badged out) | **Not counted** (owner's example requires it); a wrong interval is fixed by HR cancelling and the employee re-sending |
| Q6 | Does the break rule bind HR-entered requests | **Yes**, same server rule, mirrored in the HR dialog; no HR override |
| Q7 | Personal time in MCP tools / public API | **Not this wave** |
| Q8 | May the employee edit a request HR entered for him | **Yes** (the cancel rule); the edit button is hidden when the kind is HR-only or inactive |
| Q9 | Friday lower bound | **60** (the normal length), upper 120 |
| Q10 | Lunch inside the personal interval | absorbed by §5.3 step 4 (a 12:00–15:00 personal request on a 12:00–15:00 / 60 window = 2:00 personal) |
