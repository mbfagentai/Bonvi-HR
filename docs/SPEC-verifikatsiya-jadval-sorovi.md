# SPEC — Schedule change requests + Verifix-style absence form (Verifikatsiya, wave 6)

> Status: authoritative for this wave. Written 2026-10-17. Source: the owner,
> after a read-only walk through Verifix web (`INTAKE-verifix-web.md`). The owner
> chose three items (AskUserQuestion, 2026-10-17): schedule change requests,
> the Verifix-like absence form with filters, the «employee may request» flag.
> Cycle schedules (2/2) — not now. Mark requests («Belgilash») — not now.
> Verifix is read-only reference; nothing is written there.

---

## A. «Xodim o'zi so'ray oladi» — absence kind flag

* `absence_types.employee_can_request boolean NOT NULL DEFAULT true`
  (Verifix «Разрешить сотрудникам создавать запрос на данный вид отсутствия»).
* `GET /api/me` lists only active kinds with the flag; `POST /api/me/requests`
  with a kind without it → 400 `davomat.yoqlik_turi_xodimga_yopiq`.
* HR still may enter any active kind on the employee's behalf.
* Catalog dialog: a second switch «Xodim o'zi so'ray oladi», list badge
  «Faqat HR» when off. Seed: all three on.

## B. Absence request form and filters like Verifix

* Form mode radio, app and HR dialog: **«Qism kun» / «Butun kun» / «Bir necha kun»**
  (Verifix «Часть дня / Весь день / Несколько дней»):
  * Qism kun — one date + start* + end* (both required);
  * Butun kun — one date;
  * Bir necha kun — from date + to date (to > from).
  The server contract is unchanged (from/to date + optional times); the mode
  is derived: times → part day; from == to → whole day; else several days.
* HR request list filters gain **absence kind** (`absenceTypeId`) and **mode**
  (`mode=PartDay|WholeDay|MultiDay`) — `GET /api/attendance/requests`.

## C. Schedule change requests («Jadvalni o'zgartirish so'rovi»)

Verifix «Запрос на изменение рабочего графика»: 13 242 rows in MBF, mostly one
day with a moved/extended break («went home for lunch and Friday prayer»).

### C1. Model

`schedule_change_requests` — aggregate, mirrors `absence_requests`:
`EmployeeId`, `Kind` (`Change = 1` «Kunni o'zgartirish», `Swap = 2` «Kun
almashtirish»), `Note`, `Status` (same four values as absence requests),
`DecisionNote`, `DecidedBy`, `DecidedAt`, `SubmittedByAccountId`,
`CancelledByAccountId`, audit + soft delete.

`schedule_change_request_days` — children, one row per affected date:
`Date`, `IsWorking`, `StartTime`, `EndTime`, `BreakStart`, `BreakEnd`
(break both or none), `BreakMinutes`, `SwapWith` (the other date of a swap pair,
display only). Unique `(request_id, date)`.

**AMENDED 2026-10-17 (qa-review before the first deploy) — `BreakMinutes` is stored.**
As in `work_schedule_days`, the deducted break length and the break window are
independent (a 60-minute lunch somewhere inside 12:00–15:00). The first cut derived
the length from the window, so a swap turned that day into a 180-minute break and a
60-minute break without a window into 0. Now: a swap snapshot copies the schedule's
`BreakMinutes`; a Change day takes the client's `breakMinutes` when given (the form
sends the prefilled plan value while the user leaves the break untouched), otherwise
the window length (Verifix semantics). `0 ≤ BreakMinutes ≤ 480`, 0 on a day off
(CHECK `ck_schedule_change_request_days_break_minutes`). Migration
`JadvalSorovTanaffusi` back-fills existing rows with their window length.

* **Change**: 1–31 dates, each «ish kuni» (start*, end*, optional break) or
  «dam olish kuni». End before start = night shift (as in schedules). Break
  must lie inside the shift (same rule as `WorkScheduleDay`).
* **Swap**: 1–15 pairs (A ⇄ B, A ≠ B, no date twice). At creation the server
  snapshots the employee's **current effective plan** of both dates into rows:
  A gets B's day (times), B gets A's day. A date without a schedule → 400
  `davomat.jadval_sorovi_jadval_yoq`.
* An active (pending/approved) request may not cover a date another active
  schedule request of the same employee covers → 409
  `davomat.jadval_sorovi_takroriy`.
* **Limits (AMENDED 2026-10-17, qa-security H1)** — checked BEFORE any plan read
  or overlap query: dates within years 2000–2100 (`davomat.jadval_sorovi_sana`), all
  dates of one request within 92 days (`davomat.jadval_sorovi_davri`), note and
  decision note ≤ 2000 chars (`davomat.izoh_uzun`). Employee app only: every date
  within today −31 … today +92 (`davomat.jadval_sorovi_sana_oynasi`; `GET /api/me/plan`
  too). Request body ≤ 32 KB on the create/update endpoints.

### C2. Effect on attendance

An **approved** request's day **replaces the employee's plan for that date** —
a per-employee layer above the schedule (above the year-grid cell and the
weekly pattern). It is applied inside `ScheduleResolver.Resolve`, so the
timesheet, panel, trend, live board, app and `GET /schedules/resolve` all read
the same plan. Grace minutes come from the resolved schedule.

* The override needs a resolved schedule: an employee **without any schedule**
  is not changed (there is nothing to override; NoSchedule stays). Logged in
  ASSUMPTIONS.
* Planned minutes = shift − break; break window = the requested break.
* Approval / cancellation of an approved request recomputes each affected
  date ±1 day (pass attribution of night shifts) — inline when ≤ 92 days,
  otherwise queued, like absence requests.
* `GET /schedules/resolve` shows `daySource = Request` for such a date.

### C3. API

HR (`/api/attendance`, read `attendance.view`, write/decide
`attendance.requests.manage` — the same «O'zgartirish» level that decides absence
requests; both kinds share one HR queue. Confirmed 2026-10-17 after qa-security M1,
documented in `Permissions.AttendanceRequestsManage` and `PermissionCatalog`):

| Method | Path | Body / query |
|---|---|---|
| GET | `/schedule-requests` | `employeeId, status, decided, from, to, search, page, pageSize` |
| GET | `/schedule-requests/{id}` | — |
| POST | `/schedule-requests` | `{ employeeId, kind, days: [{ date, isWorking, startTime, endTime, breakStart, breakEnd, breakMinutes? }], swaps: [{ date, withDate }], note }` |
| PUT | `/schedule-requests/{id}` | same, pending only (409 `davomat.sorov_qaror_qabul_qilingan`) |
| POST | `/schedule-requests/{id}/decide` | `{ approve, decisionNote }` |
| POST | `/schedule-requests/{id}/cancel` | — |
| GET | `/requests/pending-count` | now counts both request kinds |

Employee app (`/api/me`, employee token):

| Method | Path |
|---|---|
| GET | `/schedule-requests` (own, paged) |
| POST | `/schedule-requests` (same body without `employeeId`) |
| POST | `/schedule-requests/{id}/cancel` (pending only) |
| GET | `/plan?date=YYYY-MM-DD` — own effective plan of a date (prefills the form) |

`ScheduleChangeRequestDto`: id, employee (id, name, photoKey, position,
department), kind, status, note, decisionNote, decidedByName, decidedAt,
createdAt, submittedByEmployee, cancelledByEmployee, days[] (date, isWorking,
startTime, endTime, breakStart, breakEnd, swapWith, breakMinutes). `DayPlanDto` (`/api/me/plan`)
also carries `breakMinutes`.

### C4. Screens

* **HR — Verifikatsiya → So'rovlar**: segmented «Yo'qlik» | «Jadval o'zgarishi»;
  the second is a queue like the first (status filter, decide dialog).
* **HR — employee card → Davomat → So'rovlar**: schedule requests listed below
  absence requests; «Jadval so'rovi qo'shish» button.
* **HR dialog**: type radio «Kunni o'zgartirish» / «Kun almashtirish»; rows of
  date + «Ish kuni / Dam olish kuni» + start/end + «Tanaffus» checkbox with
  start/end, «+» adds a date; swap rows date ⇄ date.
* **App**: «So'rov yuborish» opens a type sheet «Yo'qlik» / «Jadvalni
  o'zgartirish»; the schedule form prefills times from `/api/me/plan`; the
  requests list shows both kinds; detail + cancel while pending.

### C5. Not in this wave

Verifix «Явка» per day, manager approval chain («Подтвердил» → «Завершил»),
reopening a decided request, mark/overtime/business-trip requests, cycle
schedules.
