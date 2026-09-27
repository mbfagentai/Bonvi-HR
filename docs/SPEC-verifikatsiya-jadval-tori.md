# SPEC — Work schedule year grid (Verifikatsiya, wave 5)

> Status: authoritative for this wave. Written 2026-09-20. Branch: `verification`.
> Source: the owner, 2026-09-20, against four Verifix `Графики работы` screenshots.

---

## A. What the owner decided

| # | Decision |
|---|---|
| A1 | The schedule gets a **year grid like Verifix**: 12 months × 31 days, a value in every cell, editable. "Whatever it takes, same as Verifix." |
| A2 | **No holiday calendar.** In this company holidays are normally working days; a day off on a holiday is entered by hand, in the schedule that needs it. |

**Technical constraint stated up front and accepted:** a Verifix "normal" cell holds
only the hours norm (`8.5`). Ours cannot: lateness and early leave — the two
numbers this module exists for — are computed from the day's **start and end
time**. So a cell stores a time window and *displays* hours, exactly like Verifix
looks, without breaking `Kechikkanlar`.

---

## B. Model

### B1. `work_schedule_dates` — one row per explicit date

| Field | Meaning |
|---|---|
| `WorkScheduleId` | Owner schedule. |
| `Date` | Tashkent date. Unique with the schedule. |
| `IsWorking` | Working day or a day off. |
| `StartTime` / `EndTime` | The window; required when `IsWorking`. |
| `BreakMinutes`, `BreakStart`, `BreakEnd` | Same meaning as on the weekday template. |

### B2. The weekday template stays as the fallback

`DayPlan.For(schedule, date)` resolves in this order:

1. a row in `work_schedule_dates` for that exact date → use it;
2. otherwise the weekday template (`work_schedule_days`), as today.

**Why not "the grid is the only source" (Verifix's literal model).** A schedule
there is tied to a year, and the day after 31 December every employee on it would
silently fall to "no schedule" until someone builds next year's grid. The fallback
costs nothing, keeps every existing schedule and test working, and is invisible on
screen: a cell with no row shows the template's hours, so the grid still reads as
full — the same picture as Verifix, minus the cliff.

### B3. Editing rewrites days, never the timesheet

Any change to the grid **enqueues a recompute** for the touched dates, for the
employees the schedule reaches (employee / position / department assignment), via
the existing dirty queue. Stored `attendance_days` are never edited by hand — the
module's standing rule.

**AMENDED 2026-09-27 — as written, not as first built.** The first build enqueued
`attendance:recompute-range` from the earliest to the latest past date for
**every** employee: "fill the year" on 27 September recomputed ~270 days × ~540
people, the neighbours of a changed day were not recomputed, and future dates were
skipped. Now the edit enqueues `attendance:schedule-dates` (scheduleId + the exact
dates), which:

1. resolves, date by date, who the schedule actually reaches (the same
   `ScheduleResolver` the timesheet uses — tier, department walk, transfer date);
2. recomputes only those people, only the changed dates **and their ±1
   neighbours** — a day turned into a 20:00–08:00 shift claims the next morning's
   exit, and without the neighbour that exit was counted on both days;
3. includes **future** dates — an approved request's «Sababli» row can live there
   and must disappear when the day becomes a day off;
4. goes through the single writer, which works in ≤ 92-day windows and 100-employee
   batches and forgets each saved batch.

---

## C. API

* `GET /api/attendance/schedules/{id}/year?year=2026` — every date of that year
  with its effective plan: `date`, `isWorking`, `startTime`, `endTime`,
  `breakMinutes`, `plannedMinutes`, and `source: "pattern" | "date"`.
  Read under `attendance.view`.
* `PUT /api/attendance/schedules/{id}/dates` — bulk write under
  `attendance.manage`: a list of dates plus the value to set, or
  `reset: true` to drop the rows and fall back to the template.
  One call per edit gesture (a cell, a range, a whole month, a whole year).

Validation reuses the weekday rules: a working day needs both times, the break
window must sit inside the shift, and a shift may cross midnight.

## D. Screen

Inside the schedule form, under the weekly pattern: a **year grid** —
`‹ 2026 ›`, rows = months, columns = 1…31, each cell showing the planned hours
(`8,5`) or `D` for a day off, greyed when it comes from the template and solid
when it is an explicit date. Click a cell to edit it, drag/shift-click for a
range, buttons for "fill the year from the pattern" and "clear the year".

i18n in four locales; iOS/HIG look; the grid scrolls horizontally on a phone
like the timesheet does.

## E. Definition of done

* `./bonvi test` green except the one pre-existing red test.
* Migration read by hand; `upgrade head` works.
* A test proving: an explicit date beats the template, a cleared date falls back
  to it, and both are reflected in the timesheet after the recompute.
* `./bonvi check`, typecheck and frontend build green.
