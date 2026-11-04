# SPEC — HR adds a pass by hand + day dialog (2026-11-03)

Owner (2026-11-03): in the employee card «Davomat» → calendar, HR can correct a
day, but **existing passes are never edited** — HR only **adds** a new «Kirish»
or «Chiqish». Clicking a calendar cell opens that day with **all** its passes.
Look and feel follows Verifix «Календарь / Отметки» (screenshots in the chat).

## A. Data

- A hand-added pass is an ordinary `attendance_passes` row, like a phone mark
  (`MobileCheckInCommand`): the timesheet, board, reports and exports need no
  new rule.
  - `device_id` = synthetic device **«HR (qo'lda)»**
    (`AttendanceDevice.HrManualId` = `01996f00-0000-7000-8000-00000000b072`),
    seeded by migration, inactive, dead token — same pattern as «Mobil ilova».
  - `identity_provider` = `HrManual` (6), `raw_person_id` = employee id.
  - `method` = `Manual` (8), `direction_source` = `Hr` (6), direction = In/Out
    as HR chose (never Unknown).
  - `dedupe_key` = `hr:{employeeId:N}:{yyyyMMddHHmm}:{in|out}` → the existing
    unique index rejects a second identical mark (409).
- Evidence row **`attendance_manual_passes`** (1:1 with the pass): `pass_id` (PK,
  FK cascade), `employee_id`, `note` (required, ≤ 500), `created_at`,
  `created_by` (`IAuditable`). Audited through `AuditCatalog` (subject = employee).

## B. API

| Method | Path | Permission |
|---|---|---|
| GET | `/api/attendance/employees/{employeeId}/days/{date}/passes` | `attendance.view` |
| POST | `/api/attendance/employees/{employeeId}/manual-passes` | `attendance.requests.manage` |
| DELETE | `/api/attendance/manual-passes/{passId}` | `attendance.requests.manage` |

- **GET** returns the passes the timesheet put into that cell (same attribution
  as the employee app: stored plan window, late exit of the previous day), each
  as `AttendancePassDto` with the **effective** direction plus `counted`
  (false = dropped repeat, `PassPairing`). `AttendancePassDto` gains optional
  `manual` (note, who, when).
- **POST** body `{ occurredAt (Tashkent local, minute), direction: In|Out, note }`.
  Rules: employee exists and is employed on that date; not in the future; not
  older than `AttendanceRules.MaxRecomputeDays` (92); note required. Saves, then
  recomputes `[date−1, date+1]` synchronously and marks the pass processed —
  HR sees the new numbers at once.
- **DELETE** only for `method = Manual` passes (a terminal pass → 400
  `davomat.belgi_qolda_emas`): removes the pass and its evidence, recomputes.
  Mistyped marks would otherwise stay forever.

## C. UI (employee card → «Davomat» → «Kalendar»)

- Cell, Verifix style: plan bar (solid blue), fact bar (solid green, first in –
  last out), deviations as dashed chips: late / early leave (red), overtime
  (green), shortfall on flexible days (orange). Status colours stay.
- Click a cell → dialog «6-oktabr — Ish kuni», tabs **«Kun statistikasi»**
  (plan, fact, worked, late, early, overtime, break) and **«Belgilar»**
  (filter «Hammasi / Faqat hisobga olinganlar»; columns: sana, vaqt, tur ●
  Kirish/Chiqish, joy, hisobga olingan Ha/Yo'q, amal «Ko'rish» → pass photo
  dialog; a hand-added row shows who/why and «O'chirish»).
- «Belgi qo'shish» (only with `attendance.requests.manage`): date (default the
  cell date, next day allowed for a night shift), time, Kirish/Chiqish, izoh.

## D. Out of scope

Editing or hiding terminal passes; bulk add; employee-side requests for a mark.
