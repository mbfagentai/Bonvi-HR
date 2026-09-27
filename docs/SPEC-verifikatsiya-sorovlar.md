# SPEC — Absence requests (Verifikatsiya, wave 4)

> Status: authoritative for this wave. Written 2026-09-19. Branch: `verification`.
> Language rule as everywhere: this file and all code comments/identifiers in
> English/Uzbek per `SPEC-verifikatsiya.md` §0.2; UI text through i18n in four
> languages; error codes Uzbek.
>
> This replaces `SPEC-verifikatsiya-panel.md` §A5 ("absent with reason is a
> manual HR marking"). The owner chose a stronger model on 2026-09-19: the
> **employee submits the request**, HR decides, and an approved request changes
> the attendance day.

---

## A. What the owner decided (2026-09-19)

| # | Decision |
|---|---|
| A1 | Employees get **their own app** — a PWA they install on the phone. This overturns the founding assumption that employees never log in. |
| A2 | Employees sign in with **a login and password issued by HR**. No SMS (costs money), no Telegram dependency. |
| A3 | A request covers **either one day with a time range** (`19.09, 08:30–11:57`) **or several whole days** (`12–16.09`). |
| A4 | An approved request **changes attendance**: the day stops being `Absent` and stops counting in "absent days". |
| A5 | **Only HR decides.** New permission, no new role for department heads. |
| A6 | Absence kinds live in an **editable catalog** (`Yo'qlik turlari`), not in code. |
| A7 | The PWA must be able to **ask for camera and geolocation permission**; what exactly it does with them is decided later and is out of scope here. |
| A8 | **Order of work: the HR side first**, the app second. HR must be able to run the whole flow (entering a request on the employee's behalf) before the app exists. |

**Consequence of A1/A2 that must not be forgotten:** the system grows from ~5
accounts to ~950. Password distribution, reset, session lifetime and "what a
logged-in employee may see about themselves" become real problems. They are
solved in phase 2, not hand-waved — see §E.

---

## B. Phase 1 — the HR side (this wave)

### B1. Catalog: `absence_types`

A `ReferenceEntity` like `termination_reasons` — name, `IsActive`, note. Managed
under `catalog.manage`, read under `catalog.view`, listed on the existing
catalogs screen. Seeded with the kinds Verifix shows (`Ishxona ishi bilan`,
`Kasallik`, `Shaxsiy sabab`), which HR can extend.

No `Code` column (the project's catalog rule) and no "counts as worked time"
flag in this phase — A4 only removes the absence, it does not add worked hours.

### B2. Entity: `absence_requests`

| Field | Meaning |
|---|---|
| `EmployeeId` | Who is absent. |
| `AbsenceTypeId` | From the catalog. |
| `FromDate` / `ToDate` | Tashkent dates, both inclusive. Equal for a one-day request. |
| `FromTime` / `ToTime` | Optional, **only allowed when `FromDate == ToDate`** (a partial day). Both null = whole day(s). |
| `Note` | The employee's own explanation (Verifix's `Примечание`). |
| `Status` | `Pending` → `Approved` \| `Rejected`, or `Cancelled`. |
| `DecisionNote` | HR's note (Verifix's `Примечание руководителя`). |
| `DecidedBy` / `DecidedAt` | Who closed it and when. |
| `CreatedBy` | The HR user, or (phase 2) the employee themselves. |

Validation (Uzbek error codes, 400): `davomat.sorov_davri_notogri`
(`FromDate > ToDate`), `davomat.sorov_vaqti_notogri` (times on a multi-day
request, or `FromTime >= ToTime`), `davomat.sorov_uzun` (more than 92 days —
the module's existing period ceiling), `davomat.sorov_takroriy` (an active
request already covers that day for that employee).

### B3. Effect on attendance (A4)

**The rule is deliberately narrow: an approved request turns `Absent` into
`Excused`, and nothing else.** It does not add worked minutes, does not forgive
lateness and does not touch a day the person actually attended. The reason is
that any wider rule invents numbers HR did not enter, and this module's numbers
are already the thing people trust it for.

* New `AttendanceDayStatus.Excused = 9` — timesheet, calendar, reports and the
  Excel export all learn it through the existing maps, and `absentDays` stops
  counting it.
* The daily panel (`DashboardStatusRule`, its own six-value enum) gets the same
  treatment in the same wave. A day that reads `Sababli` on the calendar and
  `Kelmadi` on the panel would be the exact inconsistency this module keeps
  being punished for.
* Approving or rejecting a request **enqueues a recompute** for the days it
  covers, through the existing dirty queue — stored days are not edited by hand.
  **AMENDED 2026-09-27:** the decision (and the cancellation of an approved
  request) recomputes that one employee's days **in the request itself, before
  the response** (≤ `MaxRecomputeDays`, the request's own limit), through the
  single writer `AttendanceDayRecomputer`; only a longer range goes to the
  background queue. The web refetches the employee card as soon as the response
  arrives — with the Hangfire hop the card kept showing «Kelmadi» for up to a
  minute.

### B4. API

* `GET /api/attendance/requests` — filters: `employeeId`, `status`, `from`,
  `to`, paging. Read under `attendance.view`.
* `POST /api/attendance/requests` — HR creates on the employee's behalf.
* `POST /api/attendance/requests/{id}/approve` · `/reject` — body: decision note.
* `POST /api/attendance/requests/{id}/cancel` — for a request entered by mistake.
* Write/decide under a **new permission `attendance.requests.manage`** (A5),
  kept separate from `attendance.manage`: deciding absences is an HR judgement,
  not device administration, and the two are not always the same person.

### B5. Screens

* **Employee card → Davomat → «So'rovlar»** (third inner view): two tables, as
  in Verifix — pending on top with the decide buttons, decided below.
* **Verifikatsiya → «So'rovlar»**: the queue across all employees, defaulting to
  `Pending`, with the count in the nav badge.
* **Calendar:** an `Excused` day reads «Sababli» in the cell, in the blue tone
  the timesheet grid uses. The kind itself (`Ishxona ishi bilan`) is **not** in
  the cell yet — that would need the type name on every timesheet cell; it is
  visible in the requests table.
* i18n in four locales; iOS/HIG look; `./bonvi check` green.

---

## C. Phase 2 — the employee PWA (next wave, not now)

Recorded so phase 1 does not paint it into a corner:

* Employee accounts are **not** rows in `users`: a separate table and a separate
  token audience, so that 950 employee logins never appear in the HR user list
  and can never carry an HR permission.
* HR issues a one-time password from the employee card; the employee sets their
  own on first sign-in.
* The PWA's only endpoints are "my requests" and "my attendance"; it asks for
  camera and geolocation permission (A7).
* Push notifications are out of scope for now (iOS PWA support is partial).

---

## D. Definition of done for phase 1

* `./bonvi test` green except the one pre-existing red test.
* Migration exists, `upgrade head` works, and the migration was read by hand.
* RBAC test for every new endpoint.
* A test proving an approved request flips a day from `Absent` to `Excused` on
  **both** the timesheet and the daily panel, and that `absentDays` drops.
* `./bonvi check`, typecheck and frontend build green.
