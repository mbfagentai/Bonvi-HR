# INTAKE — Verifix web (app.verifix.com): requests and work schedules

> Read-only walk-through on 2026-10-17 in the owner's browser (company «MBF»,
> Verifix 6.20.1), at the owner's request: «so'rovlar, jadval va grafiklar —
> funksionallik jihatdan». Nothing was saved, approved or changed in Verifix.
> No personal data is copied here — only structure, fields and usage counts.
> Companion: `INTAKE-verifix-ilova.md` (the mobile app, 2026-09-22).

---

## 1. Menu «Посещения» (attendance) — what exists and what is used

| Section | Rows in MBF | Verdict |
|---|---|---|
| Графики работы (work schedules) | 28 | used |
| Запросы на отсутствие (absence requests) | **21 693** | heavily used |
| Запросы на изменение графика (schedule change requests) | **13 242** | heavily used |
| Запросы на отметки (mark requests) | 16 | barely used |
| Запросы на сверхурочные (overtime requests) | 0 | unused |
| Внутренняя командировка (internal business trip) | 0 | unused |
| Индивидуальные графики (+ для позиций) | 0 | unused |
| Расписания / запросы на изменение расписания | 0 | unused |
| Запросы на локацию, Локации, Устройства, Отметки | — | not reviewed (out of scope) |

## 2. Absence requests («Запросы на отсутствие»)

**List.** Tabs «Мои» / «Доступные». Columns: employee, request time, absence
kind, period («6 окт. 2026, 09:15-15:16» · «15 окт. – 16 окт. (2 дн.)» ·
«16 окт. (Полный день)»), note, state. Row actions: view, edit, approve
(«Подтвердить»), reject («Отклонить»), delete, printable report.

**Filter.** Employee, request date, kind, **request type** (part of day / whole
day / several days), state, start, end; saved filter templates.

**States.** «В ожидании» → «Подтвержден» → «Завершен», or «Отклонен». The view
shows both «Подтвердил» (approved by) and «Завершил» (completed by) and a
manager note. A completed request can be «Восстановить» (reopened).

**Create form.** Employee · absence kind · radio **«Часть дня / Весь день /
Несколько дней»** · date + start + end (part of day; start defaults to now) ·
note.

**Usage pattern.** Almost all requests are hourly «Ишхона иши билан» (went out on
company business: customs, bank, market, another plant), a few «Шахсий».

## 3. Absence kinds («Виды отсутствий») and time kinds

| Field | MBF: «Ишхона иши билан» | MBF: «Шахсий» |
|---|---|---|
| Вид времени (time kind) | **Рабочая Встреча** (child of **Явка** = presence) | **Почасовой Отгул** (hourly time off) |
| Разрешить сотрудникам создавать запрос | yes | yes |
| Учитывать неиспользованное время | yes | no |
| Является ежегодным / Количество дней в год | no / — | no / — |
| Количество дней ограничения, Предоставляется (рабочие/календарные дни), Политика переноса | — | — |

Row action «Сотрудники» — which employees may use the kind.

**Time kinds («Виды рабочего времени», 26).** A tree with letter codes and a
«load on plan» (полная / частичная / внеплановая): Явка (Я) → Рабочая Встреча
(РВ), Явка из корректировки, Явка из опоздания или раннего ухода; Свободное
время → обеденное, дорабочее, послерабочее, вне смены; Выходной → праздник,
нерабочий день, доп. выходной; Сверхурочные → по запросу, из корректировки;
Командировка, Отпуск, Отгул, Почасовой отгул, Опоздание, Ранний уход,
Отсутствие, Неоплачиваемый отпуск.

**Mapping to BonviHR.** «Рабочая Встреча под Явкой» is exactly our
`absence_types.counts_as_work_time` (2026-10-17). The rest of the tree is
Verifix's payroll vocabulary; we do not compute pay (SPEC §1.2 #2).

## 4. Schedule change requests («Запросы на изменение графика»)

**Create form.** Employee · request type:

* **«Обмен дней»** (swap days) — date ⇄ date pairs, «+» adds more pairs.
* **«Изменение графика»** (change the schedule) — for one or more dates
  («+»): day type **working / day off**; for a working day: **Явка**,
  **Начало\***, **Конец\***, optional **Перерыв** (start\*, end\*).

Note field; same states and approve/reject as absence requests.

**Usage pattern (13 242 rows).** One-day changes of the **break** — e.g.
«16.10, working day, 08:30–18:00, break 12:23–14:23» with the note «went home for
lunch and Friday prayer». The employee asks to move/extend the break so the
time out is not an absence; the manager approves; that one day is computed by
the new plan.

**BonviHR today:** no equivalent. Schedules are per schedule (weekly pattern +
year grid of dates), assigned by tier; there is no per-employee per-date
override and no request that creates one.

## 5. Mark requests («Запросы на отметки»)

Columns: employee, photo, department, position, location, time, note, mark
type (Приход / Уход), status. 16 rows, mostly old. Owner (2026-10-17): not now.

## 6. Work schedules («Графики работы»)

**Types (Создать):** Обычный · Почасовой · Продвинутый · Многосменный ·
Продвинутый многосменный. All 28 MBF schedules seen are **Обычный** (even
«24/7» is Обычный with 11 h every day).

**Обычный form.** Name · year · code · active · year grid 12 × 31 (cell = hours,
«В» = day off; month totals of days and hours) — already built as our year
grid (`SPEC-verifikatsiya-jadval-tori.md`). Settings: interval calculation type
(«Первый приход и первый уход»), checkboxes Свободное время / Поздний приход /
Ранний уход / Отсутствие (what is counted), «Не считать опоздание, если есть
отметка до начала работы», «Считать факты по локациям», «Часовая ставка»,
«Расписание отметок», auto-update by production calendar.

**«Заполнить» dialog.** Fill method **by weekdays** (5/6-day week) or **by
cycles of arbitrary length** («Периодичность» N, days 1…N ticked = working;
the start date anchors the cycle) · date range · same settings for all days or
per day · start, end, break (start, end) · **Явка** (e.g. 08:00 for a 09:00
shift — the earliest presence counted for the day).

**BonviHR today:** weekly pattern + year grid ✓, break window ✓, grace ✓.
Missing: cycle fill (`WorkScheduleKind.Cycle` exists but is rejected), a per-day
«Явка», and the per-day settings toggles.

## 7. Reports («Отчетность → Посещения»)

Employee attendance report (period, department, position, location, employees;
HTML / Excel / CSV / XML; saved templates), discipline, department regime, T-13,
detailed marks, distance, hourly, shifts, time kinds, lateness, timetable.
BonviHR already has timesheet, exceptions, summary and Excel export — not
compared in detail this round.
