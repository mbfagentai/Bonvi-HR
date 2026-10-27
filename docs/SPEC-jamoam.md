# SPEC — «Jamoam»: a department head sees their own people's attendance

Status: **building on branch `jamoam`** (owner 2026-10-27: «ok qil … alohida branchda»).
Not merged to `main`, not deployed.

## A. What the owner decided (2026-10-27)

* A department head (e.g. a plant director) sees **who came, who is late, who is
  absent** among **their own** people, today and on past days — like Verifix did.
* Access goes through the **employee app (PWA)**, not the HR system. The HR system
  holds passports, PINFL, salary, candidates and vacancies. A scoped HR role would need
  a "only my department" filter on every list, search, export, report and MCP tool,
  and one missing filter leaks the whole company. The app is a separate door
  (`bonvi-employee` token audience, no permission claims, SPEC-xodim-ilovasi §D2). It gets
  a few purpose-built read-only endpoints that return attendance and nothing else.
* Phone number of each team member **is shown**, so the head can call someone who did not come.
* **Not in this wave:** the 09:30 «who did not come» notification, deputies or several
  viewers per department, a head commenting on or approving requests, a different
  password policy for heads (the 2026-09-27 rule stays: 6 digits, HR sees it, the employee
  cannot change it).

## B. Who is a head, and what is "my team"

* `Department.HeadEmployeeId` already exists (column `departments.head_employee_id`).
  On prod it is empty for all 105 departments, and the HR web has no field for it.
  The API's save command already writes it: `DepartmentDefinition.ApplySpecific` →
  `AssignHead(command.HeadEmployeeId)`.
* An employee is a **head** when at least one live (not deleted) department has
  `HeadEmployeeId = me`. Inactive departments still count; HR removes the head to revoke.
* **Scope** = the union of the subtrees (the department itself plus all descendants,
  `BranchIdsAsync` semantics) of every department the employee heads. The scope is
  computed **on the server, from the database, on every request**. The client never
  sends "which departments I may see". Changing or removing the head takes effect on
  the next request.
* A head's own row is part of the board when they work in the scope (they are in it).
* Team membership on a date uses the same rule as the HR dashboard's department filter:
  an employment covering that date whose `DepartmentId` is in the scope.
* The head being fired or deleted closes everything already: `EmployeeAccessGate` → 403
  `xodim.hisob_faol_emas` on the whole `/api/me` group.

## C. API — new endpoints under `/api/me/team` (EmployeePolicy group)

All of them sit in the existing `/api/me` group: employee scheme only, so an HR token
gets 401, and they pass the `EmployeeAccessGate` filter. They add nothing to the HR API.

Errors:
* `jamoa.boshliq_emas` (403) — the caller heads no live department.
* `jamoa.xodim_topilmadi` (404) — the employee is outside the scope. This is the same
  response for "does not exist" and "not yours", so nobody can probe for IDs.
* `jamoa.bolim_topilmadi` (404) — `departmentId` is outside the scope.

### C1. `GET /api/me/team` — my scope
```
{ departments: [ { id, name, path, level, parentId } ] }   // flattened scope, tree order
```
* Lists every department in the scope (roots and descendants), so the app can offer a
  department picker.
* A non-head gets 403 `jamoa.boshliq_emas`.

### C2. `GET /api/me/team/board?date=&departmentId=&status=&search=&page=&pageSize=`
* `date` is a Tashkent date and defaults to today.
* `departmentId` is optional. It must be in the scope, otherwise `jamoa.bolim_topilmadi`.
  If given, the board is narrowed to that department's subtree; if not, the whole scope.
* `status`, `search`, `page` and `pageSize` work as on `GET /api/attendance/dashboard`.
  Default page size is 50, the maximum 200.
* **Reuses `IAttendanceReadModel.GetDashboardAsync`** — same status rule
  (`DashboardStatusRule`) and same counts, so the head and HR never read one day two ways.
  The filter gets one additive field: a set of department roots whose subtrees are
  unioned. Existing callers pass nothing and behave exactly as before.

Response:
```
{ date, isToday, asOf, total, counts{onTime,late,absent,notStarted,dayOff,noSchedule,excused,arrived},
  items:[{ employeeId, fullName, hasPhoto, positionName, departmentName,
           arrivedAt, leftAt, status, lateMinutes, leftAuto, phone }],
  page, pageSize, totalItems, pageCount }
```
* `phone` is the employee's primary phone, formatted like elsewhere in the app; `null`
  when there is none.
* **No** `deviceHealth`, photo key, PINFL, passport, birth date, address, salary, flags,
  documents or any HR-only field.
* `hasPhoto` replaces `photoKey`, because the key is an internal storage name. The photo
  comes from C4.

### C3. `GET /api/me/team/members/{employeeId}/attendance?year=&month=`
* The employee must be in the scope: an employment in the scope that overlaps the
  requested month. Otherwise `jamoa.xodim_topilmadi`.
* `month` gets the same validation as `/api/me/attendance` (`davomat.oy_notogri`).
* Response:
```
{ employee:{ employeeId, fullName, hasPhoto, positionName, departmentName, phone },
  attendance: <exactly MyAttendanceDto — the shape /api/me/attendance returns> }
```
* Built by the same code path as `GetMyAttendanceQuery`, parameterised by employee id,
  not a copy. The member's month must look exactly like the member's own app.
* A mobile mark's site name is fine to show. Raw turnstile photos are not included,
  same as `/api/me/attendance`.

### C4. `GET /api/me/team/members/{employeeId}/photo`
* Image bytes with `ETag` and `If-None-Match` → 304, cache `private`, exactly like
  `/api/me/photo`.
* Scope check: the employee is in the scope **today**. Otherwise `jamoa.xodim_topilmadi`.
  A missing photo is `xodim.rasm_yoq` (404).

### C5. `GET /api/me` — additive fields
* `isTeamLead: bool` — the app shows the «Jamoa» tab only when true.
* `manager: { fullName } | null` — the employee's «Rahbar», defined in D.

### C6. Audit (read audit, like `password_viewed`)
* C3 writes an `audit_logs` row: actor is the head's employee account, the entity is the
  member, `{"action":"team_member_viewed","year":…,"month":…}`.
* At most one row per (head, member) per 10 minutes, the same dedup idea as repeated 403s.
* C2 writes `{"action":"team_board_viewed"}` at most once per head per 30 minutes.
* `AuditEntryText` gets the Uzbek labels («Jamoa: xodim davomati ko'rildi», «Jamoa
  paneli ko'rildi»), so the rows read well on HR's `/audit` page.
* An audit write failure must not break the read. Log it and continue.

### C7. Rate limit
The `/api/me/team/*` endpoints use the same limiter policy as the rest of `/api/me`.

## D. «Rahbar» (manager) of an employee — Verifix's «Rahbar» line

* The manager is resolved from the employee's **primary employment's department today**:
  1. Walk up from that department.
  2. The first department whose `HeadEmployeeId` is set **and is not the employee
     themself** gives the head.
  3. If no such department exists, the manager is `null`.
* The head must be a live employee. A deleted or fired head is skipped and the walk continues.
* It is shown in two places:
  * the HR employee card header as «Rahbar: <name>», from an additive field on the
    employee detail DTO (`manager: { employeeId, fullName } | null`);
  * the app's profile screen («Rahbar» row), from C5.

## E. HR web — assigning the head

* **Bo'limlar → department dialog:** a new field «Bo'lim boshlig'i», which is a search
  picker over employees (reuse the existing employee picker, e.g. the
  responsible-employee one in `candidate-hire-dialog.tsx`), plus a clear button.
* The dialog must **always** send `headEmployeeId` (the current value) on save. Today's
  save body omits it, and the server then writes `null`, which would silently drop a
  head on any rename.
* **Department tree:** show the head's name next to the department (muted text).
* Server-side validation on save: `headEmployeeId` must be an existing, non-deleted
  employee. Otherwise 400 `bolim.boshliq_topilmadi`.
* The permission is the one that already guards department editing (`catalog.manage`).
  The change is in the immutable audit already (`AuditCatalog` lists `HeadEmployeeId`),
  so «kim kimni boshliq qildi» is traceable.

## F. Employee app — the «Jamoa» tab

* **Tab bar:** a 5th tab «Jamoa» (people icon) between «So'rovlar» and «Profil», only
  when `me.isTeamLead`. Route `/jamoa`; it is a tab root.
* **`/jamoa`** (team board):
  * top: date control (‹ today ›; a date sheet for older days, never a future date) and a
    department picker (only when the scope has more than one department; «Hammasi» first);
  * summary cards: Keldi (arrived), Kechikdi, Kelmadi, Sababli, Hali boshlanmagan,
    Dam olish. Tapping one filters the list; tapping it again clears the filter;
  * list rows: avatar (C4), name, position · department, arrival time or «—», late
    minutes chip when late, and a call button (`tel:`) when there is a phone;
  * search field; pagination as «Ko'proq» (load more);
  * auto-refresh every 60 s while `isToday` and the screen is visible; «yangilandi HH:mm».
* **`/jamoa/:employeeId`** (member):
  * header: photo, name, position, department, call button;
  * month switcher and the same month grid and day panel components as `/taqvim`, fed
    by C3;
  * `?oy=YYYY-MM` keeps the month in the URL like `/taqvim`.
* **Profile:** a «Rahbar» row when `me.manager` is set.
* **i18n:** every new string in uz-Cyrl (default), uz-Latn, ru and en.
  `check:locales` must pass.
* **Mock mode** (`VITE_API_MODE=mock`) gets a small team fixture, so the app builds and
  demos without a server.
* Design: the existing iOS-style components (`Card`, `ListGroup`, `StatusChip`,
  `MonthGrid`, `DayPanel`). Status colours come from `status-look.ts`, so a status looks
  the same as in the employee's own calendar.

## G. Definition of done

* [ ] Integration tests:
  * non-head → 403 `jamoa.boshliq_emas` on C1–C4;
  * HR token → 401;
  * the head sees exactly the subtree (nested departments), never a sibling department;
  * `departmentId` outside the scope → 404;
  * a member outside the scope → 404 for C3 and C4;
  * the head is changed → the old head gets 403 on the next request;
  * fired head → 403 `xodim.hisob_faol_emas`;
  * board counts equal the HR dashboard's for the same department and date;
  * the response has no `photoKey`, `pinfl`, `passport`, `salary` or `deviceHealth` keys;
  * audit rows are written and deduped;
  * `manager` resolution: self-head skips to the parent, a fired head is skipped.
* [ ] Unit tests for the scope and manager resolution.
* [ ] Existing suites pass (dashboard, me, catalog, OpenAPI snapshot updated
  intentionally).
* [ ] App: `pnpm build`, `check:locales`. Web: `tsc -b`, `pnpm build`, `./bonvi check`.
* [ ] `qa-security` pass focused on IDOR and field leaks across C1–C5.
* [ ] No migration expected (the column exists). If one is needed (e.g. an index on
  `head_employee_id`), it must be additive only.
