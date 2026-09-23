# PLAN — Role-based access: «Davomat mas'uli» and a narrowed employee profile

Status: **plan only, not scheduled.** Requested by the owner 2026-09-23
("hozir qilish shart emas, reja qilib ajratib qo'y"). Two decisions are
still open — see §5. Nothing here is implemented.

## 1. Problem

The person who runs Verifikatsiya (attendance, devices, schedules, sites,
the employee app) needs to open employees, but today the only way to give
them that is the full **HR menejer** role — which also exposes passport,
PINFL, address, history, flags, documents, incentives and (with
`salary.view`) salary.

## 2. What exists today (verified in code, 2026-09-23)

* **Permissions are already granular** — `api/src/Bonvi.Domain/Identity/Permissions.cs`:
  `employees.view/create/edit/delete`, `employment.view/manage`,
  `salary.view`, `lists.view/manage`, `incentives.*`, `attendance.view`,
  `attendance.manage`, `attendance.requests.manage`, `catalog.*`, …
* **Roles are fixed** — `SystemRoles`: `super_admin`, `main_boss`,
  `hr_manager`, `recruiting_bot`. There is no role editor (no `/api/roles`
  endpoints, no UI); the seeder writes the permission sets.
* **Employee profile tabs** — `web/src/app/routes/_app/xodimlar/$employeeId.tsx:264`:
  * gated: Hujjatlar, Qobiliyatlar, Rag'bat, Davomat (`attendance.view`);
  * **ungated**: Umumiy (overview), Tarix (timeline), Belgilar (flags).
* **Server side** — `api/src/Bonvi.Api/Endpoints/EmployeeEndpoints.cs`:
  * `GET /api/employees/{id}` and the list require only `employees.view`
    and return the **full** record — PINFL, passport, phone, address, …
  * `/timeline` needs `employment.view`, `/flags` needs `lists.view`
    (so the ungated tabs would error for a narrow role rather than leak —
    still a UX bug to fix).

**The gap:** hiding tabs in the UI is not enough, because
`GET /api/employees/{id}` hands every personal field to anyone with
`employees.view`. The server must stop sending them.

## 3. Proposal

### 3.1 New permission `employees.personal.view`
"Shaxsiy ma'lumotlar": PINFL, passport, birth date, address, second phone,
criminal-record note, mehnat.uz screenshot. Granted to `super_admin`,
`main_boss`, `hr_manager` by the seeder → **existing users see no change.**

Server: the employee DTO (detail + list) returns those fields as `null`
when the caller lacks the permission; the mehnat.uz screenshot endpoint
requires it. One mapping point, covered by tests — not per-screen checks.

### 3.2 New system role `attendance_officer` — «Davomat mas'uli»

| Permission | Why |
|---|---|
| `employees.view` | open the list and a profile (header + allowed tabs) |
| `attendance.view`, `attendance.manage` | journal, timesheet, schedules, devices, sites, app accounts |
| `attendance.requests.manage` | approve/reject absence requests |
| `catalog.view` | departments/positions for filters |
| `reports.view` (open) | attendance reports — confirm with owner |

Not granted: `employees.create/edit/delete`, `employees.personal.view`,
`employment.*`, `salary.view`, `lists.*`, `incentives.*`, `recruiting.*`,
`users.manage`, `settings.manage`.

### 3.3 What the officer sees on an employee profile

* Header: photo, full name, department, position, status.
* **Phone number** — owner chose this (to call someone who didn't come).
  → phone stays outside `employees.personal.view`.
* Tabs: **Davomat**, **Ilova** (app account, when built).
* Hidden: Umumiy's personal block, Tarix, Belgilar, Hujjatlar,
  Qobiliyatlar, Rag'bat, salary.

Web: gate Umumiy's personal block on `employees.personal.view`, Tarix on
`employment.view`, Belgilar on `lists.view` (same "no permission → tab not
drawn" rule the other tabs already follow). If a profile has no tab besides
Davomat, open on Davomat.

### 3.4 Assigning the role
Foydalanuvchilar page already assigns roles — the new role just appears in
the picker. Who may assign it: `super_admin` and `main_boss` (same rule as
`hr_manager`).

## 4. Work breakdown (when scheduled)

1. Domain: permission + role constants; seeder grants (idempotent, existing
   DBs get the permission added to the three HR roles). ~0.5 day
2. API: DTO masking + screenshot endpoint gate; tests — officer gets `null`
   personal fields, HR gets them, officer 403 on create/edit/flags/timeline. ~0.5 day
3. Web: tab gating, personal-block gating, default tab; role label in
   4 locales. ~0.5 day
4. `qa-security` pass focused on IDOR/field leaks (list export, search by
   PINFL, reports, document endpoints). ~0.5 day

Estimate ≈ 2 days. No migration beyond seed data.

## 5. Open decisions (owner)

1. **Fixed role vs role editor.** Plan assumes a fixed role (faster, harder to
   misconfigure). A role editor (checkbox UI) can come later if more
   custom roles are needed.
2. **Anything else on the officer's profile view** besides phone —
   flags (disciplinary notes), hire date / probation status. Owner:
   "keyin hal qilaman".
3. Should the officer see attendance **reports** (`reports.view` is wider
   than attendance today — check what else it opens first).
