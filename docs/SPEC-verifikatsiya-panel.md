# SPEC delta — Verifikatsiya dashboard, wave 2 (filter column + period trend)

> Status: authoritative for this wave. Written 2026-09-18. Branch: `verification`.
> Language: English, same rule as `SPEC-verifikatsiya.md` (§0.2): **code comments
> stay Uzbek**, error codes stay Uzbek, UI text goes through i18n in 4 languages.
>
> This is a **delta**. `docs/SPEC-verifikatsiya.md` still governs everything it
> already covers — the six statuses (§8.4, `DashboardStatusRule`), the set rule
> ("counts ignore search and status filter"), RBAC (§7), error-code style (§6.9).
> Nothing below replaces a rule there; it only adds screens and one endpoint.
>
> Source of every decision: the owner, 2026-09-18, against two Verifix
> screenshots. Do not re-litigate them.

---

## A. What the owner decided

| # | Decision |
|---|---|
| A1 | The dashboard gets a **permanent filter column on the right** (Verifix layout): period, legal entity, department, position, and a "Yangilash" button. |
| A2 | The chart-type picker gains a third value — **`list`**: no chart at all, the table takes the full width. |
| A3 | A **status chip row** sits above the table (`Hammasi · 81 Vaqtida · 41 Kechikdi · …`) — in **all three** view types, not only in `list`. |
| A4 | Selecting a **multi-day period** switches the page to a **day-by-day line chart** with per-status toggles. **No table in that mode** (the owner chose "faqat kunlik grafik"). |
| A5 | "Absent with reason" (Verifix's `Отсутствие по причине`) is **wave 3** — a manual HR marking with its own table and migration. Not in this wave; do not add a seventh status now. |

**Out of scope, explicitly:** grades (`Разряды`), locations (`Локации` — no such
reference exists in the data model; devices only carry a free-text
`LocationNote`), a schedule filter, the birthdays panel (**lifted in §E**,
owner 2026-09-19), and A5.

---

## B. Backend — one new endpoint

### B1. `GET /api/attendance/dashboard/trend`

* **Permission:** `Permissions.AttendanceView` — same as `/dashboard`.
* **Query:** `from` (`DateOnly`, required), `to` (`DateOnly`, required),
  `legalEntityId?`, `departmentId?` (with its sub-departments, same as
  `/dashboard`), `positionId?`. **No** `search`, `status`, `sort` or paging —
  the trend is a chart, not a list.
* **Dates are Tashkent dates**, like everywhere else in the module.

**Validation** (new error codes, appended to `SPEC-verifikatsiya.md` §6.9
numbering, Uzbek keys, 400):

| Code | When |
|---|---|
| `davomat.davr_notogri` | `from > to` |
| `davomat.davr_uzun` | more than **92 days** inclusive |

Both need translations in all four locale files.

**Response:**

```csharp
public sealed record AttendanceTrendDayDto(
    DateOnly Date,
    int Total,
    AttendanceDashboardCountsDto Counts);

public sealed record AttendanceTrendDto(
    DateOnly From,
    DateOnly To,
    DateTimeOffset AsOf,
    IReadOnlyList<AttendanceTrendDayDto> Days);
```

* `Days` contains **every** date in the range, ascending, **no gaps** — a day
  where nobody is employed is `Total = 0` with six zeros, not a missing entry.
  The chart must not invent an x-axis.
* `Counts` is the **existing** `AttendanceDashboardCountsDto` — six numbers that
  always sum to that day's `Total`.
* `AsOf` — the server instant the range was computed (one value for the whole
  response), same meaning as on `/dashboard`.

**Rules — reuse, do not restate:**

1. Classification goes through **`DashboardStatusRule.Classify`**. The rule stays
   the only place that turns (plan, arrival, today, now) into a status. If a
   shape mismatch makes that awkward, change the call site, not the rule.
2. The per-day set is the same as `/dashboard`'s: people with an employment on
   **that** date matching the org filter. Department/position come from that
   date's primary employment, as in §5.1.
3. `NotStarted` vs `Absent` follows the existing rule unchanged: only *today*
   can be `NotStarted` before the shift starts; past days never are.
4. Computed from raw passes, like `/dashboard` — **unless** you can prove that
   reading stored `attendance_days` for closed days gives an identical result;
   if you do take that route, write the proof into the file's remarks and cover
   it with a test that compares both sources for the same past day.

**Performance budget — this is a real constraint, the server has 2 GB:**

* 31 days × ~1000 employees must come back **under 1.5 s** warm.
* 92 days must not time out and must not hold the whole pass table in memory.
* A per-day loop that issues one query per day (92 × ~150 ms ≈ 14 s) **fails
  this budget** — compute the range set-based, in one pass.
* Measure it and put the measured numbers in the hand-back.

**Tests (xUnit, project's existing style):**

* RBAC: without `AttendanceView` → 403.
* `from > to` → 400 `davomat.davr_notogri`; 92 days → 200; 93 days → 400
  `davomat.davr_uzun`.
* Every date present, ascending, no gaps; `Counts` sums to `Total` on each day.
* A night shift whose exit lands on the next date is counted on the **shift's**
  date, matching the timesheet.
* Org filter narrows the same way `/dashboard` does (one shared case is enough).
* At least one test proves it catches a real error: break the day grouping and
  watch it go red.

### B2. `GET /api/attendance/dashboard` does not change

No new parameters, no changed semantics. The single-day page keeps using it.

---

## C. Frontend — `attendance-dashboard-page.tsx` and friends

### C1. Layout

* `xl` and wider: page becomes two columns — the existing card on the left,
  a **permanent filter column** (~320 px) on the right. The column scrolls with
  the page and is sticky at the top like the chart column already is.
* Below `xl`: the column is **not** rendered; the current `FilterPopover` button
  stays exactly as it is today. The phone layout must not regress.

### C2. Period control (in the filter column)

Presets, in this order, resolved in Tashkent time:

`Bugun` · `Kecha` · `Oxirgi 7 kun` · `O'tgan hafta` · `Oyning birinchi yarmi` ·
`Oyning ikkinchi yarmi` · `Oxirgi 30 kun` · `Joriy oy` · `O'tgan oy` ·
`Ixtiyoriy oraliq`

* `Ixtiyoriy oraliq` reveals two date inputs; `Qo'llash` / `Bekor qilish` close
  the dropdown. Other presets apply immediately.
* The header's day stepper (`‹ 18.09.2026 ›`) stays for **single-day** periods
  and is hidden in range mode.
* A preset is stored as the preset, not as two frozen dates: `Bugun` pinned
  today must still mean "today" tomorrow.

### C3. View types

`donut` | `bars` | **`list`** (new). In `list` the chart column is not rendered
at all and the table spans the card's full width — not a hidden chart, not a
zero-height one.

### C4. Status chips (all three view types)

`Hammasi` + the six statuses, each with its count, above the table. Clicking one
filters the table through the **existing** `toggleStatus`; `Hammasi` clears it;
the active chip is visibly selected. Counts come from `counts` — the set that
ignores search and the status filter (§8.4 rule), so a chip never re-labels
itself because someone typed a name into the search box.

### C5. Range mode

* Line chart (recharts, already a dependency), one series per status, x axis =
  days `dd.MM`, tooltip per day showing every enabled series.
* Toggles below the chart, one per status. **Default on:** `Vaqtida`,
  `Kechikdi`, `Kelmadi`. **Default off:** `Boshlanmagan`, `Dam olish`,
  `Grafik yo'q`.
* No table, no chips, no paging in this mode; the search box and the status
  filter are hidden, not merely disabled.
* The chart-type picker is hidden in range mode (there is one chart).
* Colors are the six status colors already used by the donut — one palette, one
  meaning, in both themes.

### C6. Pin ("qadash")

Extends the existing prefs: view type, period **preset**, org filters, sort,
page size. Reading a stale or corrupt value must keep the page working, as the
current file already documents.

### C7. Auto-refresh

Unchanged rule: 30 s while the selected period contains today. It applies to the
trend request too.

### C8. i18n

Every new string in `uz-Latn`, `uz-Cyrl`, `ru`, `en` under `attendance.*`.
`./bonvi check` must stay green.

### C9. Design

iOS/HIG feel like the rest of the system, light and dark, no horizontal page
scroll at phone width. The right column must not squeeze the table into an
unreadable width at 1280 px — check it.

---

## D. Definition of done for this wave

* `./bonvi test` green (the one intentionally red test from the previous wave
  stays red — do not touch it, it is a real bug on the fix list).
* `./bonvi check`, typecheck and frontend build green.
* No migration in this wave. No change to `main`; everything stays on
  `verification`.
* The pages are opened in a browser — single day in all three view types, and a
  7-day range — in light and dark, at desktop and phone width.

---

## E. Wave 3 delta — the birthdays panel

> Written 2026-09-19. Source: the owner, against the same Verifix screenshot
> (`Дни рождения` under the filter column). Three decisions, asked and answered.

### E0. What the owner decided

| # | Decision |
|---|---|
| E1 | The **nearest 10 people**, no day window. The panel always has the same height and is never empty while anybody has a birth date on file. |
| E2 | The panel **obeys the org filter** of the column above it (legal entity / department / position). One screen, one population. |
| E3 | It lives **only** on Verifikatsiya → Statistika. The home page's "birthdays today" line is untouched. |

Sizing note that drove E1: of 947 working employees, ~26 have a birthday within
7 days and ~87 within 30. An unbounded window would be a scrolling column, not a
glance.

### E4. Backend — `GET /api/attendance/dashboard/birthdays`

* **Permission:** `Permissions.AttendanceView` — same as `/dashboard`.
* **Query:** `legalEntityId?`, `departmentId?` (with its sub-departments),
  `positionId?`, `limit?` (default **10**, max **50**). No `date`: "today" is a
  Tashkent date taken from `IClock`, never from the browser — the same rule as
  `/api/dashboard/birthdays`.
* **Response:** `IReadOnlyList<AttendanceBirthdayDto>`, already ordered.

```csharp
public sealed record AttendanceBirthdayDto(
    Guid EmployeeId,
    string FullName,
    string? PhotoKey,
    string? PositionName,
    string? DepartmentName,
    DateOnly BirthDate,
    DateOnly NextBirthday,
    int DaysUntil,
    int? Age);
```

**Rules — reuse, do not restate:**

1. **The population is `/dashboard`'s, for today**: an employment covering today
   that matches the org filter (`ApplyEmploymentOrgFilterAsync`). Department and
   position come from today's primary employment (§5.1), like every other row in
   the module. This is deliberately *not* `BirthdayQuery` (the home page's
   today-only condition): that one has no org filter and answers a different
   question. The two may show different people when a filter is set — that is
   the point of E2.
2. **Order:** `DaysUntil` ascending, then full name, then id — stable, so a
   refresh does not shuffle two people who share a date.
3. **Year is ignored, day and month are not.** `NextBirthday` is the next
   occurrence on or after today; today counts (`DaysUntil = 0`).
4. **29 February keeps the module's existing rule:** in a non-leap year it does
   not become 28 Feb or 1 Mar. The next occurrence is the next leap year, which
   pushes that person out of the nearest ten — the same silence as
   `BirthdayQuery`, not a new invention.
5. `Age` is the age they turn on `NextBirthday`, and is `null` when the date is
   not trustworthy (`> 0 and < 120`) — the same guard and the same threshold as
   `GetBirthdaysTodayQueryHandler`, so one person is never 136 on one screen and
   blank on another.
6. Employees with no `birth_date` are absent, not shown as "unknown" (78 of 2207
   records have none).

**Tests:** RBAC 403 without `AttendanceView`; ordering nearest-first including a
date that wraps the year end; `limit` respected and clamped; the org filter
narrows the list the way `/dashboard` does; a 29 Feb record does not surface in a
non-leap year.

### E5. Frontend

* A second `Card` in the existing right-hand `aside`, below the filter card,
  desktop only (`@min-[1100px]/page:`) — the phone layout gains nothing and the
  popover is not touched.
* Row: photo (`EmployeePhoto`, real photo — initials are not acceptable),
  full name, position, and the date on the right (`20 sentabr`, or `Bugun` /
  `Ertaga`). Clicking a row opens the employee card, when the reader may open it.
* The panel follows the org filter but **not** the period: it is "who is next",
  not "who had a birthday in the selected range".
* i18n in all four locales under `attendance.*`; iOS/HIG feel, light and dark.

---

## F. Wave 3 delta — attendance on the employee card

> Written 2026-09-19. Source: the owner, against a Verifix employee profile
> screenshot (`Сотрудник` → `Календарь / Отметки`). The question asked was
> "a tab inside our HR card, or a separate verification profile?" — answered
> below.

### F0. What the owner decided

| # | Decision |
|---|---|
| F1 | **A tab inside the existing employee card**, not a second profile. One person, one card. |
| F2 | Inside it, **two views** — `Kalendar` (month grid) and `O'tishlar` (that month's turnstile journal), plus a **month summary** strip above them. |
| F3 | The "technical" card (which schedule is assigned, device person id) is **not** in this wave. |

Why a tab and not a separate screen, recorded so it is not re-litigated:
the card's header already carries photo, name, position, department and
status — a second profile would duplicate the "primary employment" rule
(§5.1), the exact class of duplication that has already produced silent bugs
in this project. Verifix has a standalone profile because Verifix has no HR
card; we do. And there is no second audience: only HR and the top manager use
the system.

### F4. Backend — one field pair, no new endpoint

`TimesheetCellDto` gains `PlanStartAt` and `PlanEndAt` (UTC, nullable). They
come from the **stored day snapshot** (`attendance_days.plan_start_at/_end_at`,
already written by the nightly computation), never recomputed from today's
schedule — otherwise last month's calendar would change whenever a schedule is
edited. A synthesized cell (§5.7.1) has no plan, like it has no times.

Everything else the tab needs already exists: `/timesheet?employeeId=`,
`/passes?employeeId=`, and `TimesheetTotalsDto` for the summary.

### F5. Frontend

* New top-level tab **«Davomat»**, last in the row, rendered only with
  `attendance.view` — the same dynamic-tab rule the card already uses for
  documents, competency and incentives.
* The card's **tab and calendar month now live in the URL**
  (`?tab=attendance&year=2026&month=9`). The default tab and the current month
  are not written (the timesheet's rule: a link sent in September must not
  show October).
* Verification screens deep-link into it: the statistics table (row click and
  name), the timesheet grid (**carrying the month being viewed**), the reports
  table and the pass photo dialog. The birthdays card and the former-employee
  alert keep opening the overview — they ask an HR question, not an
  attendance one.
* **Calendar:** seven columns from Monday. Cell background is the day status,
  from the **same** `DAY_STATUS_LOOK` map as the timesheet grid; lateness is the
  existing bottom orange bar, not a second colour. Inside a cell: the plan as a
  blue chip, the actual first-in/last-out as bold text, then late and early-leave
  minutes, listed separately (they are two different conversations). The status
  name is printed only on days with no times. **Every day carries a border**,
  whatever its status — without one, `Present` days (whose fill is the card
  background) did not read as cells at all and the grid looked like it was made
  of red and grey days only. Deviations are written in **words** (`22 daq`,
  `1 soat 5 daq`), never with a prime mark: `22′` was read as an apostrophe.
* **Passes:** the month's journal for that employee as a **table**, 50 per page,
  opening the **same** photo dialog the journal page uses. Columns follow the
  Verifix `Отметки` screen (owner, 2026-09-19): time, device (with its location
  note underneath), device type, mark type, identification method — plus the
  turnstile **snapshot** as the first column, which Verifix does not have and
  which is the only column that answers "is this really that person".
  This needed two more fields on `AttendancePassDto`: `DeviceLocation`
  (the device's free-text `LocationNote` — **not** a reference; there is still no
  location entity, §A) and `DeviceProvider` (Hikvision / Generic, `null` when the
  device was deleted). Both come from the device row the journal already joins
  for `DeviceName`; `DeviceNamesAsync` became `DeviceLabelsAsync` so the four
  screens that show a device name keep reading it from one place.
* i18n in all four locales; `monthLabel` moved into `attendance-presentation.ts`
  so the timesheet and the calendar cannot render a month differently.
