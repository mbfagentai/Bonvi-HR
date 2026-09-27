# SPEC — Owner-defined roles and an immutable audit trail («Rollar va audit»)

> Status: authoritative. Written 2026-09-26; revised the same day after the
> owner's second decision (§1.2 item 5): **the owner defines every role himself
> in the UI, and roles change at runtime without a server update.** Nothing in
> any deploy creates an HR head or applies a split; Appendix A is a suggestion.
> Branch: `main`.
> Language: English (project rule). Four things stay in the repo's Uzbek style,
> each for a stated reason: code comments (project convention), error codes
> (`rol.*`, `audit.*`, `mehnat.*` — the frontend translates by code), migration
> class names (§5), and every user-facing string (it goes through i18n in four
> locales, §9.9 — Uzbek first).
>
> **Build agents cannot see the conversation that produced this file.** Every
> decision below is final unless the owner overrides one of the items in §17.
> "Rejected alternative" notes exist so nobody re-litigates them mid-work.

---

## 0. Before you start

### 0.1 The working tree is dirty — land it first

At the time of writing `main` has uncommitted work that overlaps files this SPEC
changes: `web/src/shared/i18n/locales/*.json`, `web/scripts/check-locales.mjs`,
`web/src/features/users/components/user-form-dialog.tsx`,
`web/src/features/employees/components/employee-overview-tab.tsx`,
`web/src/features/employees/components/hire-dialog.tsx`,
`web/src/features/recruiting/**`, `web/manual/*.md`, `docs/ASSUMPTIONS.md`.
That work (reserve candidates page, phone input) must be committed by its owner
**before** any unit of this SPEC starts. Never build on top of someone else's
uncommitted edits in a shared file.

### 0.2 The `verification` branch

It is **not merged and must not be merged, rebased or cherry-picked** as part of
this work. It carries the attendance module, `attendance.*` permissions and the
employee PWA accounts. §14 says exactly what the future merge must do; nothing in
this SPEC may depend on code that exists only there.

### 0.3 Conventions and reference implementations

`docs/STACK.md` and `docs/CONVENTIONS.md` do not exist; conventions are read out
of the code. Copy the shape of these files; where this SPEC deviates, the
deviation is named.

| What | Reference |
|---|---|
| Admin CRUD with reach rules, errors, DTO factory | `api/src/Bonvi.Api/Endpoints/UserEndpoints.cs`, `api/src/Bonvi.Application/Identity/Users/**` |
| SaveChanges interceptor | `api/src/Bonvi.Infrastructure/Persistence/Interceptors/AuditableInterceptor.cs`, `DataChangeInterceptor.cs` |
| Paged list with URL filters (backend) | `api/src/Bonvi.Application/Employees/Employments/ListEmploymentEventsQuery.cs` + `EmploymentEndpoints.cs` `GET /events` |
| Paged list with URL filters (frontend) | `web/src/features/employments/components/employments-page.tsx` |
| Hand-written migration with data carry-over + its test | `20260910051756_NomzodIzohVaFayllar.cs` + `api/tests/Bonvi.Api.IntegrationTests/CandidateFileMigrationTests.cs` |
| JSON field-leak test | `api/tests/Bonvi.Api.IntegrationTests/PublicApiLeakTests.cs` |
| Test token with a custom permission set | `RecruitingApiTests.KoruvchiTokenAsync()` |
| Settings-style page with grouped cards | `web/src/features/settings/components/system-settings-page.tsx` |
| Search rule without JSHSHIR/passport | `EmployeeSearchQuery.MatchingPublic` in `api/src/Bonvi.Infrastructure/Persistence/SearchQuery.cs` |

New frontend API calls use the **Orval-generated client** (`./bonvi gen`), not
hand-written `*-api.ts` files — hand-written clients are the documented source of
three silent bugs in this project (PLAN.md, "texnik qarz" 3).

### 0.4 Production

Production is live (3 HR users daily). Every phase in §11 is deployed separately,
**only after the owner's explicit go-ahead for that phase**, using the safe
procedure in §13. Never run anything against the production database while
building or testing.

---

## 1. Goal and scope

### 1.1 Goal

Today three fixed roles decide who can do what, the only employment right is the
all-or-nothing `employment.manage`, and the system cannot say who transferred,
terminated or re-salaried an employee or what a corrected record said before. The
owner wants: (1) a role editor for the whole system in which **he** defines every
role, (2) employment rights fine-grained enough to build, for example, a
«Kadr bo‘limi boshlig‘i» role that alone may change salary, terminate and
complete probation, and (3) a complete, tamper-proof history of every change with
its author.

### 1.2 Owner decisions (2026-09-26) — fixed

1. **Role editor for the whole system.** An admin creates and edits roles in the
   UI. For every section (module) a role gets one level: **Yo‘q / Ko‘rish /
   O‘zgartirish / To‘liq** (No access / View / Edit / Full). Some modules also
   have individually assignable actions.
2. **«Kadr bo‘limi boshlig‘i» (HR head).** The owner's intended split: HR
   manager may transfer, give additional duty, extend probation; **salary
   change, termination and probation completion are HR head only**. Every
   employment action is individually assignable in the editor. *(Revised by
   item 5: the system does not create this role — the owner builds it in the
   editor; Appendix A describes the suggested setup.)*
3. **Complete, immutable history**: who, when, old value → new value, for every
   change. Nobody can edit or delete it through the application, admins included.
4. **WebAuthn/Face ID step-up is deferred** — §10 is a design sketch only.
5. **"I define the roles/positions myself, and they must be changeable without
   updating the server."** Consequences, all binding:
   - no business role is seeded or reset by any deploy or restart;
   - only `super_admin` stays protected (plus the hidden technical bot role);
     today's «Bosh rahbar» and «HR menejeri» become ordinary roles — same rights
     on deploy, then renamable, editable and deletable (when unused);
   - every role change — create, rename, edit levels/actions, delete, assign to
     a user — takes effect at runtime from the UI, with no deploy and no restart;
   - role names are free text typed by the owner, in one language, shown as
     typed in all four UI languages.

Owner's scope clarification (same day): the editor is **only for system users** —
the 3–4 HR/management people on the «Foydalanuvchilar» page. The recruiting bot's
technical role stays as it is: protected, not editable, not offered as an
assignable role.

### 1.3 In scope

- Permission model: module × level + capabilities, in code (§3).
- Owner-defined roles in the database, guard rules, and the removal of every
  rule that depends on the `main_boss`/`hr_manager` codes (§4).
- Expand-only migrations that keep every current user's access on deploy (§5).
- Permission changes take effect on the user's next request (§6).
- Automatic audit of every entity change with old→new values, security events,
  database-level immutability, audit pages (§7, §9.5, §9.6).
- Server-side personal-data masking (`employees.personal.view`), absorbing
  `docs/PLAN-rollar.md` from the `verification` branch (§3.10, phase P1).
- Future WebAuthn step-up — sketch only (§10).

### 1.4 Non-goals — do not build these

- **No roles for employees.** Employee PWA accounts (verification branch,
  separate table and token audience) are out of scope. Do not model their access
  through this role system and do not plan role assignment for ~950 employees.
- **No per-user overrides.** A user's access is exactly their role's. With 3–4
  users, a second role is cheaper and more visible than hidden per-user
  exceptions, and the audit/dominance rules stay simple.
- **No bulk operations** (bulk assign, bulk edit). One user, one role, one form.
- **No multiple roles per user.** `User.SetRole` stays the only writer.
- **No department-scoped access** ("sees only his department"). Every role sees
  the whole organisation within its modules (PLAN.md §Xavflar 2).
- **No read auditing** (who viewed what). Only changes, security events and
  Excel exports are recorded.
- **No seeded business roles and no presets applied by code.** No HR head role
  is created; no "recommended setup" button exists; Appendix A is documentation
  the owner may follow by hand.
- **No translated role names.** A role name is data, not an i18n key.
- **No UI for pipeline-stage configuration** — it was removed on purpose
  (`recruiting-api.ts` comment, 2026-08-31); only the server rights change.
- **No step-up/WebAuthn code** in any scheduled phase.

---

## 2. Verified current state

Every fact below was read from the code on 2026-09-26; the design rests on them.

1. Permission codes are constants in `api/src/Bonvi.Domain/Identity/Permissions.cs`
   (24 codes). `AuthenticationExtensions.AddBonviAuthorization` registers one
   policy per code (policy name = code) requiring a `perm` claim.
2. `perm` claims are written into the access JWT at login and refresh by
   `JwtTokenService.CreateTokens`, from `UserRepository.GetPermissionsAsync`
   (reads `roles.permissions text[]`). Access token: 15 min, `ClockSkew = 0`.
   **There is no per-request check**: a role edit, deactivation or password reset
   reaches an already-issued access token only when it expires (≤ 15 min).
   `GET /api/auth/me` reads fresh data; the web app calls it only on page load,
   and `web/src/shared/api/http-client.ts` ignores the `user` in the refresh
   response.
3. `IdentitySeeder.SeedAsync` runs on every start and **reconciles** system roles
   to `SystemRoles.Defaults` (adds missing codes, removes extra ones). An edited
   system role would be reset by the next restart. `IdentitySeederPersistenceTests`
   pins this behaviour.
4. One role per user (`users.role_ids uuid[]`, `User.SetRole`). `UserRoleRules`
   hard-codes reach: super_admin → everyone; main_boss → hr_manager only.
   Single-holder roles: super_admin, main_boss.
5. There is no `/api/roles`. `web/src/features/users/lib/user-presentation.ts`
   hard-codes `ROLE_CODES` and mirrors `UserRoleRules` (`canActOnRole`).
6. All nine employment writes require `employment.manage`. Money travels inside
   several of them: `TransferRequest.SalaryAmount`,
   `AdditionalDutyRequest.ExtraSalaryAmount`, `ResolveProbationRequest.SalaryAmount`
   (plus position/department/rate), `EditEmploymentEventRequest.Amount`, and the
   hire's initial salary. `POST /api/recruiting/candidates/{id}/hire` creates an
   employee, an employment and a hire order under `recruiting.manage` only.
7. `employees.delete` and `audit.view` are granted but no endpoint requires them.
8. `audit_logs` exists (text `old_values`/`new_values`). Only five user-management
   handlers write to it (`IAuditLogger`); `AuditLogger` never fills `ip_address`
   or `user_agent`. `AuditableInterceptor` fills created/updated/deleted
   metadata only. `Employment.EditEvent` → `EmploymentEvent.Edit` **overwrites**
   `EffectiveDate`, `OrderNo`, `OrderDate`, `Note`, `ToSalary` (and
   `Employment.Salary` when that event is the salary source) — the previous
   values are lost.
9. `EmploymentEvent.RecordedBy` is **never set**: `Employment` calls
   `EmploymentEvent.Create` without `recordedBy`, and `EmploymentEvent` is not
   `IAuditable`. The author of every transfer, salary change and termination is
   unknown today; `employments.updated_by` only holds the last toucher.
10. Writes that bypass the change tracker: `RefreshTokenRepository.DeleteExpiredAsync`
    (`ExecuteDelete`) and `DepartmentRepository.RebuildHierarchyAsync` (raw
    `UPDATE` of the derived `level`/`path`). There are no others.
11. The ETL builds its own `AppDbContext` with only `AuditableInterceptor`
    (`api/tools/Bonvi.Etl/Runtime/EtlDatabase.cs`).
12. `web/src/app/layout/nav-items.ts`: many items have no `permission`
    (employees, probation, former employees, orders, the four lists, every
    catalogue item). `sidebar.tsx` filters only top-level items; group children
    are never filtered.
13. Every integration-test token helper mints tokens with
    `ITokenService.CreateTokens(<real admin user loaded from DB>, [], <custom set>)`.
14. The API connects to PostgreSQL as `POSTGRES_USER`, a superuser in the
    official image (`docker-compose.prod.yml`).
15. The bot account holds `recruiting_bot` (`recruiting.view`,
    `recruiting.manage`); `BotServicePolicy` checks the role claim. On 401 the
    bot logs in again once (`BonviClient`). It calls: vacancies list,
    application-check, rejection/interview notices and marks, `GET /api/settings/bot`,
    `/bot/logo`, `/bot/token`, `POST /api/recruiting/candidates`,
    `POST /api/recruiting/candidates/{id}/photo`.
16. `verification` adds `attendance.view`, `attendance.manage`,
    `attendance.requests.manage` and grants them to hr_manager through
    `SystemRoles.Defaults`; its `docs/PLAN-rollar.md` plans an attendance officer
    role and `employees.personal.view`.

---

## 3. Permission model

### 3.1 Vocabulary

- **Permission code** — a string constant in `Permissions.cs`, e.g.
  `employment.transfer`. Endpoints bind to codes (`RequireAuthorization(code)`).
  Codes live **only in code**, never in a table of their own.
- **Module** — a section of the system (§3.3). Defined in code.
- **Level** — `None < View < Edit < Full`. A role stores one level per module.
- **Capability** — a permission code inside a module that is individually
  assignable (§3.4). Two kinds: *visibility* (see a sensitive field) and
  *operation* (perform a specific action).
- **Role** — a named record in the database: levels per module + ticked
  capabilities. Its **effective permission set** is computed from the catalog
  (§3.6) whenever a token is issued.

### 3.2 Level semantics (identical for every module)

| Level | uz-Latn | Meaning |
|---|---|---|
| None | Yo‘q | Module hidden in the menu; every endpoint returns 403. |
| View | Ko‘rish | Reads everything in the module. Visibility capabilities may be ticked. |
| Edit | O‘zgartirish | View + create and update records. No deleting records, no deactivating reference data, no module configuration. Replacing or removing a photo, or adding or removing a check material (`EmployeeCheckFile`, which replaced the single mehnat.uz screenshot on 2026-09-27), counts as editing the card, not deleting a record. Operation capabilities are assignable only from this level and apply only when ticked. |
| Full | To‘liq | Everything the module offers: Edit + delete, deactivate, module configuration, and **all** capabilities of the module (implied, not stored). |

A module offers only the levels that differ in that module (e.g. Lists has
nothing to delete, so it offers None/View/Edit). The UI draws only offered levels.

### 3.3 Module catalog

Order = order in the editor. "Adds" lists the codes a level adds on top of the
lower levels (expansion is cumulative).

| # | Key | Section | Title (uz-Latn) | Offered levels | View adds | Edit adds | Full adds |
|---|---|---|---|---|---|---|---|
| 1 | `employees` | people | Xodimlar | None, View, Edit, Full | `employees.view` | `employees.create`, `employees.edit` | `employees.delete` |
| 2 | `employment` | people | Mehnat munosabatlari | None, View, Edit, Full | `employment.view` | — (only ticked operations) | — (all capabilities implied) |
| 3 | `lists` | people | Ro‘yxatlar | None, View, Edit | `lists.view` | `lists.manage` | — |
| 4 | `incentives` | people | Rag‘bat va jarima | None, View, Edit, Full | `incentives.view` | `incentives.manage` | `incentives.delete` |
| 5 | `safety` | people | Baxtsiz hodisalar | None, View, Edit, Full | `safety.view` | `safety.manage` | `safety.delete` |
| 6 | `feedback` | people | Qayta aloqa | None, View, Edit, Full | `feedback.view` | `feedback.manage` | `feedback.delete` |
| 7 | `recruiting` | recruiting | Rekruting | None, View, Edit, Full | `recruiting.view` | `recruiting.manage` | `recruiting.configure`, `recruiting.delete` |
| 8 | `reports` | analytics | Hisobotlar | None, View, Full | `reports.view` | — | `reports.export` |
| 9 | `catalog` | reference | Ma’lumotnomalar | None, View, Edit, Full | `catalog.view` | `catalog.manage` | `catalog.deactivate` |
| 10 | `users` | system | Foydalanuvchilar | None, Full | — | — | `users.manage` |
| 11 | `roles` | system | Rollar | None, View, Full | `roles.view` | — | `roles.manage` |
| 12 | `audit` | system | Audit jurnali | None, View | `audit.view` | — | — |
| 13 | `settings` | system | Tizim sozlamalari | None, Full | — | — | `settings.manage` |

Sections (editor group headers): `people` «Kadrlar», `recruiting` «Rekruting»,
`analytics` «Tahlil», `reference` «Ma’lumotnomalar», `system` «Tizim».

The dashboard (`/`) is not a module: it is visible to every signed-in user and
each card renders only when its endpoint's code is present.

### 3.4 Capabilities

| Code | Module | Kind | Assignable from | Implied at | Requires | Label (uz-Latn) |
|---|---|---|---|---|---|---|
| `employees.personal.view` | employees | Visibility | View | Edit, Full | — | Shaxsiy ma’lumotlar |
| `salary.view` | employment | Visibility | View | Full | — | Okladni ko‘rish |
| `salary.extra.view` | employment | Visibility | View | Full | — | Qo‘shimcha haqni ko‘rish (2026-09-27: additional-duty pay and the card total; also required, together with `employment.salary`, to send, correct, date-move or end a paid duty) |
| `employment.hire` | employment | Operation | Edit | Full | module `employees` ≥ Edit | Ishga qabul |
| `employment.transfer` | employment | Operation | Edit | Full | — | Ko‘chirish (lavozim, bo‘lim) |
| `employment.duty` | employment | Operation | Edit | Full | — | Qo‘shimcha vazifa |
| `employment.salary` | employment | Operation | Edit | Full | `salary.view` (auto-added) | Oklad o‘zgarishi |
| `employment.terminate` | employment | Operation | Edit | Full | — | Ishdan bo‘shatish |
| `employment.probation.extend` | employment | Operation | Edit | Full | — | Sinov muddatini uzaytirish |
| `employment.probation.resolve` | employment | Operation | Edit | Full | — | Sinov muddatini yakunlash |
| `employment.event.correct` | employment | Operation | Edit | Full | — | Yozuvni tuzatish |

- **Why `employees.personal.view` is implied at Edit:** editing a card means
  retyping JSHSHIR and passport; an editor who cannot see those fields would
  submit empty values and wipe them. So the only role without personal data is a
  View-level role (the future attendance officer).
- **Why `salary.view` is a capability and not its own module:** it is a field
  that appears in several screens (card, orders, hire, export), not a section.
- **Other modules need no capabilities.** Deleting is covered by Full; Excel
  export is Reports Full; personal data and salary are the only field-level
  secrets in the current schema. Adding more later follows §3.12.

### 3.5 Dependencies (validated on every role save; the UI auto-applies them)

| If | Then |
|---|---|
| `employees` ≥ View | `catalog` ≥ View |
| `employment` ≥ View | `employees` ≥ View |
| `lists` ≥ View | `employees` ≥ View |
| `incentives` ≥ View | `employees` ≥ View |
| `safety` ≥ View | `employees` ≥ View |
| `feedback` ≥ View | `employees` ≥ View |
| `recruiting` ≥ View | `catalog` ≥ View |
| `reports` ≥ View | `employees` ≥ View |
| `users` = Full | `roles` ≥ View (the user form lists roles) |
| capability `employment.hire` | `employees` ≥ Edit |

Server behaviour: a violated module dependency → **400 `rol.bogliqlik`** (the
server never raises another module silently — that would grant access nobody
chose). A capability's `Requires` capability (`employment.salary` →
`salary.view`) is **auto-added** by normalisation, because it only makes the
granted action usable. The technical bot role is exempt from dependency
validation (§4.1).

### 3.6 Expansion — levels to codes

Implemented once, in `Bonvi.Domain/Identity/AccessCatalog.cs` (pure code, no
dependencies — Domain rules):

```
Expand(levels, capabilities):
  codes = {}
  for module in Modules:
    L = levels[module.Key] or None
    if L == None: continue
    for each offered level O of module with O <= L: codes += module.Adds[O]
    for cap in module.Capabilities:
      implied = (L == Full) or (cap.ImpliedAt contains L)
      ticked  = cap.Code in capabilities and L >= cap.AssignableFrom
      if implied or ticked: codes += cap.Code
  for cap in codes: codes += RequiresOf(cap)                 // salary.view
  if codes ∩ employment operation capabilities ≠ ∅: codes += "employment.manage"   // compatibility alias, §3.7
  return codes
```

`Normalize(levels, capabilities)` (run before every save): drops `None` entries,
drops capabilities implied by the stored level, adds required capabilities,
sorts capabilities ordinally. `Validate(...)` returns the first failure among:
unknown module (`rol.notanish_bolim`), level not offered (`rol.daraja_yoq`),
unknown capability (`rol.notanish_ruxsat` — existing code), capability below its
`AssignableFrom` (`rol.imkoniyat_daraja`), module dependency (`rol.bogliqlik`).

### 3.7 Permission codes after this work

Existing codes keep their names and meaning unless listed as changed.

| Code | Status | Bound to |
|---|---|---|
| `employees.view/create/edit` | unchanged | as today |
| `employees.delete` | **now used** | `DELETE /api/employees/{id}/documents/{documentId}` |
| `employees.personal.view` | **new** (R1 defines, P1 enforces) | §3.10 |
| `employment.view`, `salary.view` | unchanged | as today |
| `employment.hire`, `.transfer`, `.duty`, `.salary`, `.terminate`, `.probation.extend`, `.probation.resolve`, `.event.correct` | **new** | §3.8 |
| `employment.manage` | **compatibility alias** | No endpoint requires it after R1. Emitted when a role holds ≥ 1 employment operation, only so that a code-only rollback to the previous release keeps working (§5.7). Remove in the release after P1. |
| `lists.view/manage` | unchanged | as today |
| `incentives.delete`, `feedback.delete`, `safety.delete` | **new** | the three `DELETE` endpoints |
| `recruiting.configure` | **new** | pipeline-stage writes |
| `recruiting.delete` | **new** | `DELETE` vacancy, `DELETE` candidate file |
| `catalog.deactivate` | **new** | the 18 `/activate` and `/deactivate` catalogue endpoints |
| `roles.view`, `roles.manage` | **new** | §8.1 |
| `reports.*`, `users.manage`, `audit.view`, `settings.manage` | unchanged | `audit.view` now used by §8.3 |

`Permissions.All` gets every new code (so the policy loop registers them);
`Permissions.CompatibilityAliases = { employment.manage }` (excluded from the
catalog placement test, §12.1).

### 3.8 Endpoint → permission contract

"→" marks a change. Everything not listed as changed keeps its current policy.
This table is also the source for `endpoint-permissions.txt` (§12.1).

**Auth, system, notifications, dashboard**

| Route | Policy |
|---|---|
| `POST /api/auth/login`, `/refresh`, `/logout` | anonymous |
| `GET /api/auth/me`, `GET/POST/DELETE /api/auth/me/photo`, `GET /api/system/info`, all 5 `/api/notifications/*` | authenticated |
| `GET /api/dashboard/stats`, `/breakdown` | `employees.view` |
| `GET /api/dashboard/birthdays` | `employees.view` → `employees.personal.view` (P1) |

**Employees** (`/api/employees`)

| Route | Policy |
|---|---|
| `GET /`, `GET /{id}` | `employees.view` (P1: personal fields masked, §3.10) |
| `GET /{id}/photo` | `employees.view` (the photo is not personal data) |
| `POST /` | `employees.create` |
| `PUT /{id}`, `POST/DELETE /{id}/photo`, `POST /{id}/check-files`, `DELETE /{id}/check-files/{fileId}`, `PUT /{id}/competency` | `employees.edit` |
| `GET /{id}/check-files`, `GET /{id}/check-files/{fileId}/content`, `GET /{id}/competency` | `employees.view` → `employees.personal.view` (P1) |
| `GET /{id}/timeline` | `employment.view` |
| `GET /{id}/stages` | `recruiting.view` |
| `GET /flagged`, `GET /{id}/flags` | `lists.view` |
| `POST /{id}/flags`, `POST /{id}/flags/{flagId}/close` | `lists.manage` |
| `GET /{id}/history` (**new**, A2) | `audit.view` AND `employees.view` |

**Employee documents** (`/api/employees/{employeeId}/documents`)

| Route | Policy |
|---|---|
| `GET /`, `GET /{documentId}/file` | `employees.view` → `employees.personal.view` (P1) |
| `POST /`, `PUT /{documentId}` | `employees.edit` |
| `DELETE /{documentId}` | `employees.edit` → `employees.delete` (R1) |

**Employment** (`/api/employments`, `/api/probations`) — all changes in R1

| Route | Policy |
|---|---|
| `POST /api/employments/hire` | → `employment.hire` AND `employees.create` |
| `GET /api/employments/events`, `GET /api/employments/{id}` | `employment.view` |
| `POST /api/employments/{id}/transfer` | → `employment.transfer` (+ money rule §3.9) |
| `POST /api/employments/{id}/additional-duty` | → `employment.duty` (+ money rule) |
| `POST /api/employments/{id}/salary` | → `employment.salary` |
| `PUT /api/employments/{id}/events/{eventId}` | → `employment.event.correct` (+ money rule) |
| `POST /api/employments/{id}/terminate` | → `employment.terminate` |
| `GET /api/probations`, `/ending-soon`, `/missing` | `employment.view` |
| `POST /api/probations/{id}/resolve` | → `employment.probation.resolve` (+ appointment rules) |
| `POST /api/probations/{id}/extend` | → `employment.probation.extend` |
| `POST /api/probations/missing/confirm` | → `employment.probation.resolve` |

**Incentives, feedback, safety** (R1)

| Route | Policy |
|---|---|
| `GET /api/employees/{employeeId}/incentives`, `GET /api/incentives` | `incentives.view` |
| `POST`, `PUT /{incentiveId}` | `incentives.manage` |
| `DELETE /{incentiveId}` | → `incentives.delete` |
| `GET /api/exit-interviews`, `/by-employee/{id}`, `/by-employment/{id}` | `feedback.view` |
| `POST`, `PUT /{id}` | `feedback.manage` |
| `DELETE /{id}` | → `feedback.delete` |
| `GET /api/safety/incidents`, `/by-employee/{id}`, `/{id}` | `safety.view` |
| `POST`, `PUT /{id}`, `POST /{id}/conclude` | `safety.manage` |
| `DELETE /{id}` | → `safety.delete` |

**Recruiting** (R1)

| Route | Policy |
|---|---|
| `GET /api/recruiting/vacancies`, `/{id}`; `GET /api/recruiting/candidates`, `/{id}`, `/{id}/files/{fileId}`, `/{id}/photo`; `GET /api/recruiting/stages`; `GET /api/settings/bot`, `/bot/logo` | `recruiting.view` |
| Vacancy `POST`, `PUT`, `/reopen`, `/close`; candidate `POST`, `PUT`, `/reassign`, `POST /stages`, `PUT /stages/{entryId}`, `/interview-invite`, `/reject`, `/withdraw`, `/reserve`, `/return`, `POST /files`, `POST/DELETE /photo` | `recruiting.manage` |
| `DELETE /api/recruiting/vacancies/{id}`, `DELETE /api/recruiting/candidates/{id}/files/{fileId}` | → `recruiting.delete` |
| `POST /api/recruiting/stages`, `PUT /api/recruiting/stages/{id}`, `POST /api/recruiting/stages/reorder`, `POST /api/recruiting/stages/{id}/activate`, `POST /api/recruiting/stages/{id}/deactivate` | → `recruiting.configure` |
| `POST /api/recruiting/candidates/{id}/hire` | → `recruiting.manage` AND `employment.hire` AND `employees.create` (the `/api/employments/hire` pair plus recruiting: it creates the person too) |
| The five bot routes (`application-check`, `rejection-notices`, `…-sent`, `interview-notices`, `…-sent`), `GET /api/settings/bot/token` | `BotServicePolicy` (unchanged) |

**Catalogue** (`/api/catalog`, R1) — 9 kinds: `legal-entities`, `directions`,
`termination-reasons`, `award-types`, `penalty-types`, `document-types`,
`vacancy-titles`, `departments`, `positions`

| Route | Policy |
|---|---|
| All `GET` (list, one, `departments/tree`, `positions/{id}/duties`) | `catalog.view` |
| `POST`, `PUT`, `departments/{id}/move`, duty `POST`/`DELETE`/`PUT order` | `catalog.manage` |
| `POST /{kind}/{id}/activate`, `POST /{kind}/{id}/deactivate` (18 routes) | → `catalog.deactivate` |

**Reports, users, settings, API keys, public API** — unchanged:
`GET /api/reports/turnover` `reports.view`; `GET /api/reports/employees/export`
`reports.export` (P1 masks columns); all nine `/api/users/*` `users.manage`;
`/api/settings` GET/PUT and `/bot/logo` POST/DELETE `settings.manage`;
`/api/api-keys/*` `settings.manage`; `/api/public/v1/*` API-key scheme.

**New**: `/api/access-catalog`, `/api/roles*` (§8.1), `/api/audit*` (§8.3).

`AND` means two policies on one endpoint: `.RequireAuthorization(codeA, codeB)`
— ASP.NET evaluates every listed policy and all must pass.

### 3.9 Money inside employment actions (R1)

Rule: **any money value submitted with an employment action requires
`employment.salary`**, except the initial salary of a hire (it is part of the hire
order; there is no previous salary to change). Checked in the Application handler
via `ICurrentUser.HasPermission`, **before any mutation**, returning
**403 `mehnat.oklad_huquqi_yoq`**. This closes the bypass "transfer to the same
department with a new salary".

| Handler | Condition that needs `employment.salary` |
|---|---|
| `TransferEmployeeCommandHandler` | `SalaryAmount != null` |
| `AddAdditionalDutyCommandHandler` | `ExtraSalaryAmount != null` (see §17 decision 2) |
| `ResolveProbationCommandHandler` | `SalaryAmount != null` |
| `EditEmploymentEventCommandHandler` | `Amount != null` **and** the target event has `ToSalary != null` (otherwise the amount is ignored anyway, `EmploymentEvent.Edit`) |
| `HireEmployeeCommandHandler`, `HireCandidateCommandHandler` | never (initial salary) |

Appointment rule: `ResolveProbationCommandHandler` with `DepartmentId` or
`PositionId` set also requires `employment.transfer` → **403
`mehnat.kochirish_huquqi_yoq`**. `Rate` is part of resolving and needs nothing
extra.

### 3.10 Field-level rules

**Salary** — unchanged mechanism: `SalaryAccess.Mask` (`salary.view`).
**Additional-duty pay** (2026-09-27) — `SalaryAccess.MaskExtraPay`
(`salary.extra.view`), separate from the base salary: the timeline, the orders
list, and the card's active duties and total (`EmploymentDto.Duties`/`TotalPay`).

**Personal data (`employees.personal.view`)** — implemented in phase P1, server
side, one mapping point per DTO, never per screen. Without the capability:

| Where | Effect |
|---|---|
| `EmployeeDto` (card) | `BirthDate`, `Pinfl`, `Passport`, `SecondPhone`, `Address`, `HasCriminalRecord`, `CriminalRecordNote`, `MehnatUzUrl` = `null` |
| `EmployeeListItemDto` (list, grid, topbar search) | `BirthDate`, `Pinfl`, `Passport` = `null` |
| `search` on every employee-based list (employees, events, probations, incentives, flagged, exit interviews, incidents) | matches **name and phone only** — the `EmployeeSearchQuery.MatchingPublic` rule. `EmployeeSearchQuery.Matching`/`MatchingIds` gain a `bool includeDocuments` parameter; each Application query handler sets it from `ICurrentUser.HasPermission(employees.personal.view)` and passes it through its read-model filter (Infrastructure never reads the principal). Reason: searching by JSHSHIR would turn the list into an identity oracle (same decision as the public API, 2026-09-18). |
| Excel export | the JSHSHIR, passport, birth date and address columns are omitted |
| Documents, competency, check materials (`GET /{id}/check-files*`) | 403 (policy change in §3.8) — check materials hold criminal-record and debt certificates |
| Dashboard birthdays, notification types `BirthdayToday`/`BirthdayTomorrow` | policy / `NotificationAccess.RequiredPermission` → `employees.personal.view` |

Phone number, name, photo, legal entity, department, position, status and hire
date stay visible at Employees View (owner's decision in `PLAN-rollar.md` §3.3:
the officer must be able to call a person who did not come).

### 3.11 Why existing `RequireAuthorization` keeps working

Endpoints still require **codes**; roles still produce **codes** in the token.
The only thing that changes is where the codes come from: instead of a stored
array edited by the seeder, `AccessCatalog.Expand(role.ModuleLevels,
role.Capabilities)` produces them at token issue. Every endpoint whose code is
unchanged in §3.8 needs no edit. The migration (§5.5) converts each existing
role to levels whose expansion is a **superset** of its current codes, so no
current user loses a single endpoint on deploy.

### 3.12 Adding a permission or module later

1. Add the constant to `Permissions.cs` and to `Permissions.All`.
2. Place it in `AccessCatalog`: in a level's `Adds` of its module, or as a
   capability. Unplaced codes fail `AccessCatalogTests.Every_code_is_placed`.
3. Bind the endpoint; update `endpoint-permissions.txt`.
4. Roles at **Full** get it automatically; roles below Full get it only if the
   level that adds it is reached. **No silent grants for new modules**: a new
   module starts at None for every role except super_admin (which always has
   every code, §4.1); the owner grants it in the editor after the deploy. A data
   migration may touch roles only to *preserve* access that a code change would
   otherwise remove (e.g. one code split into two), never to grant new access,
   and only with the owner's approval stated in the PR.
5. Add i18n labels (§9.9) and the `check-locales.mjs` dynamic source.

---

## 4. Roles

### 4.1 Role kinds

| Kind | Roles | Definition lives in | Grants / name / description editable | Deletable | Shown in `GET /api/roles` | Assignable by |
|---|---|---|---|---|---|---|
| Protected — owner | `super_admin` | **code**: its effective codes are always every code in `Permissions.All` except the compatibility alias, whatever is stored | no | no | yes, read-only, badge «O‘zgarmas» | super_admin only; always single-holder |
| Protected — technical | `recruiting_bot` | **code**: always exactly `recruiting.view` + `recruiting.manage` | no | no | **no** (404 by id) | super_admin only, via API; never offered in the UI |
| Ordinary | every other role: today's «Bosh rahbar» (code `main_boss`) and «HR menejeri» (code `hr_manager`) and every role the owner creates | **database**: `module_levels`, `capabilities`, `single_holder`, `name`, `description` | yes (§4.4) | yes, when unused (G6) | yes | per §4.5 |

Protection is derived from the **code** (`SystemRoles.Protected = {super_admin,
recruiting_bot}`), not from a stored flag, so a database edit cannot unlock a
role. Because both protected definitions live in code, no startup step ever
rewrites a stored role (§5.6). The codes `main_boss` and `hr_manager` survive
only as internal identifiers of two ordinary rows; no rule reads them (§4.6).
Codes of new roles are generated (`custom_` + 10 hex characters of the id) and
are never shown in the UI.

### 4.2 Grants after deploy (converted) and on an empty database (starter)

R1 converts today's roles one-to-one (§5.5): every current user keeps exactly
today's rights. A brand-new **empty** database (tests, development, a new
installation) receives the same four rows once, as a starting point (§5.6).
After that every ordinary role belongs to the owner: rename, change, delete.

| Module | super_admin (protected, computed) | «Bosh rahbar» (`main_boss`) | «HR menejeri» (`hr_manager`) | «Telegram bot» (protected, hidden) |
|---|---|---|---|---|
| employees | Full | Full | Full | — |
| employment | Full | Full | Full | — |
| lists | Edit | Edit | Edit | — |
| incentives | Full | Full | Full | — |
| safety | Full | Full | Full | — |
| feedback | Full | Full | Full | — |
| recruiting | Full | Full | Full | Edit |
| reports | Full | Full | Full | — |
| catalog | Full | Full | Full | — |
| users | Full | Full | — | — |
| roles | Full | View | — | — |
| audit | View | View | View | — |
| settings | Full | — | — | — |
| single holder | always | **yes** — the flag is set by the migration (§17 decision 1) | no | no |

- Names stay as stored today: «Super administrator», «Bosh rahbar»,
  «HR menejeri», «Telegram bot».
- The bot's expansion is exactly `recruiting.view` + `recruiting.manage` — its
  codes do not change. Stage configuration and deletes moved to new codes it
  never held and never used (fact 15).
- «Bosh rahbar» gets `roles` = View because it manages users and the user form
  lists roles. Only super_admin edits roles until the owner grants
  «Rollar: To‘liq» to someone in the editor.
- **There is no HR head row.** The owner creates that role himself, whenever he
  chooses; Appendix A describes the suggested setup.

### 4.3 Dominance

`Codes(role) = AccessCatalog.Expand(...)` without the compatibility alias.
Role A **dominates** B when `Codes(B) ⊆ Codes(A)`; **strictly** when also
`Codes(A) ≠ Codes(B)`. Implemented once in `AccessCatalog.Dominates(a, b, strict)`.

### 4.4 Guard rules

| # | Rule | Error |
|---|---|---|
| G1 | super_admin is protected: no change to levels, capabilities, name, description or single-holder flag; no delete. | 409 `rol.ozgarmas` |
| G2 | The technical bot role is hidden: `GET/PUT/DELETE /api/roles/{id}` → 404; it is never offered in a picker. | 404 `rol.topilmadi` |
| G3 | Every other role is ordinary — the two that exist today included: renamable, editable, deletable when unused. No role code gets special treatment anywhere (§4.6). | — |
| G4 | Nobody edits the role they hold. | 403 `rol.oz_roli` |
| G5 | Nobody grants more than they have: a non-super-admin may create a role only if his role strictly dominates the proposed grants, and edit a role only if his role strictly dominates **both** its current and its proposed grants. | 403 `rol.huquqdan_oshadi` |
| G6 | A role held by any non-deleted user (active or not) cannot be deleted. Inactive holders count: reactivating them would otherwise produce a role-less account. | 409 `rol.foydalanishda` |
| G7 | Somebody can always manage users. (a) The last active user whose role grants `users.manage` cannot be deactivated or moved to a role without it — counted by capability, not by role code. (b) super_admin is protected structurally: only super_admin reaches super_admin, it cannot deactivate itself or change its own role, it is always single-holder, and its rights are computed, so no role edit can take `users.manage` away from it. | 409 `foydalanuvchi.oxirgi_administrator` (existing) |
| G8 | Role create/update/delete are audited automatically (§7) with operation `CreateRole`/`UpdateRole`/`DeleteRole`. | — |
| G9 | Creating, editing and deleting roles requires `roles.manage` (default: super_admin only), bounded by G4/G5. | 403 `auth.ruxsat_yoq` |
| G10 | Single holder is a **per-role switch** (`single_holder`), not a list of codes. A role with the switch on — and super_admin, always — has at most one **active** holder. Creating, assigning or reactivating a second active holder is refused. Turning the switch on while the role already has two or more active holders is refused. | 409 `foydalanuvchi.super_admin_mavjud` (super_admin, existing) / `foydalanuvchi.rol_band` (others, new); 409 `rol.bir_nechta_egasi` |
| G11 | Validation of levels/capabilities (§3.6). | 400 codes in §8.4 |
| G12 | Name: free text typed by the owner in **one language** (no translations; shown as typed in all four UI languages), trimmed, 2–60 characters, unique case-insensitively among non-deleted roles. Description ≤ 300 characters. | 400 `rol.nom_bosh` (existing) / `rol.nom_uzun`; 409 `rol.nom_band`; 400 `rol.tavsif_uzun` |
| G13 | Deleting is soft (`deleted_at`): audit rows keep resolving the role name. | — |

### 4.4.1 Rejected alternatives

- *Keep «Bosh rahbar» and «HR menejeri» as protected system roles* — rejected by
  the owner (§1.2 item 5).
- *Seed an HR head role, or ship a "recommended setup" button* — rejected by the
  same decision; Appendix A is text the owner may follow by hand.
- *Translate role names* — rejected: names are the owner's data; a translation
  key per role would need a deploy for every new role.
- *Drop the single-holder rule entirely* — rejected as the default (§17
  decision 1): it would silently loosen today's protection of the «Bosh rahbar»
  seat. A per-role switch keeps today's behaviour and makes it the owner's
  runtime choice.
- *A stored "locked" flag for super_admin* — rejected: a flag can be edited in
  the database; a code-derived rule cannot.

### 4.5 User management rules (`UserRoleRules` rewrite, R1)

`UserRoleRules.Allowed(actingRole, targetRole)` (the "who reaches whom" matrix)
becomes:

- acting role is super_admin → allowed;
- target role is null, super_admin, or the technical bot role → only super_admin;
- otherwise → allowed iff `Dominates(acting, target, strict: true)`.

`EnsureCanAssign(acting, newRole)`: the same function with the new role as the
target. `EnsureSingleHolder(role, …)`: reads the role's switch (`IsSingleHolder`
= `single_holder` or super_admin) instead of a code list. `EnsureManagerRemains`:
"is the target the last active user whose role grants `users.manage`?" (G7a).

On today's data this reproduces today's behaviour exactly: «Bosh rahbar»
strictly dominates «HR menejeri»; nobody but super_admin dominates «Bosh
rahbar»; «Bosh rahbar» stays single-holder through its switch; and the existing
tests `Yagona_rahbar_ozini_faolsizlantira_olmaydi` (409) and
`Super_administrator_boshqa_rahbar_bolsa_ham_ozini_faolsizlantira_olmaydi` (403)
keep passing, because the rule counts `users.manage` holders — exactly what the
current `EnsureManagerRemains` comment says it means.

`UserSummaryDto` gains `RoleId` and `CanManage` (acting user may act on this
row per the rule above) so the frontend stops mirroring the rule (§9.4).

### 4.6 Every dependency on the `main_boss` / `hr_manager` codes, and its replacement

| Place | Today | After R1 |
|---|---|---|
| `SystemRoles.Defaults` (`Permissions.cs`) | defines and, via the seeder, **resets** four roles on every start | split into `SystemRoles.Protected` (super_admin, bot — definitions in code) and `SystemRoles.StarterRoles` (the four rows of §4.2), used **only** when the `roles` table is empty (§5.6) |
| `SystemRoles.FullAccess` = {super_admin, main_boss} | `Role.Revoke` refuses; `EnsureManagerRemains`; `UpdateUserCommand` line 164 skips the last-manager check when the new role is in the set | removed. super_admin is protected by code (§4.1); the last-manager check counts `users.manage` holders (G7a); `UpdateUserCommand` runs it when the new role lacks `users.manage` |
| `SystemRoles.SingleHolder` = {super_admin, main_boss} | `CreateUserCommand` line 143, `ActivateUserCommand` line 104, `UpdateUserCommand` via `EnsureSingleHolder` | `role.IsSingleHolder` (per-role switch; super_admin always); the migration sets the switch for today's «Bosh rahbar» (§5.5) |
| `UserRoleRules.Allowed` | code matrix: super_admin → all, main_boss → hr_manager | strict dominance (§4.5) |
| `UserErrors.MainBossExists` (`foydalanuvchi.bosh_rahbar_mavjud`) | second active main_boss | generic `foydalanuvchi.rol_band` for any single-holder role; the old code has no producer (keep its translation for old clients) |
| `Role.Revoke` / `Role.Grant` | leadership roles cannot lose codes | removed with individual grants (§5.1) |
| `IdentitySeeder` | reconciles the four roles on every start | empty-database seeding only; never touches an existing role (§5.6) |
| `DatabaseInitializer.CreateFirstAdminAsync`, `CreateBotUserAsync` | find `super_admin` / `recruiting_bot` by code | unchanged — protected roles |
| `DatabaseInitializer.CreateDevUsersAsync` (development only) | creates the dev HR account with role code `hr_manager` | unchanged; renaming keeps the code; if the owner deleted that role in a dev database, the account is skipped (the method already skips a missing role) |
| `UserEndpoints` OpenAPI descriptions | describe the main_boss/hr_manager matrix | rewritten to describe dominance and the single-holder switch |
| `AuthenticationExtensions.BotServicePolicy` | `RequireRole(recruiting_bot)` | unchanged |
| `web/src/features/users/lib/user-presentation.ts` | `ROLE_CODES`, `SUPER_ADMIN_ROLE_CODE`, `MAIN_BOSS_ROLE_CODE`, `HR_MANAGER_ROLE_CODE`, `LEADER_ROLE_CODES`, `SINGLE_HOLDER_ROLE_CODES`, `canActOnRole`, `currentRoleCode` | deleted; the server's `canManage`, `canAssign`, `isSingleHolder` replace them |
| `users-page.tsx` (`roleLabel`, leader counting at line 128) | `roles.<code>` translation; client-side "last leader" guess | stored `roleName`; the server decides (`canManage`, G7) |
| `app/layout/settings-dialog.tsx` role badge | `t('roles.<code>')`, hidden when untranslated | `/me` `roleName` as stored (§8.2) |
| `features/users/lib/user-error.ts` | maps `super_admin_mavjud` to the role field | also maps `foydalanuvchi.rol_band` |
| `features/auth/lib/dev-accounts.ts` (development only) | plain labels + codes for the dev login panel | unchanged |
| `features/auth/api/auth-api.ts` doc comment | lists obsolete codes (`admin`, `hr_operator`, …) | comment rewritten: "role codes are internal identifiers; show `roleName`" |
| Locale files `roles.*`, `check-locales.mjs` `SKIPPED_DYNAMIC_PREFIXES` `'roles.'` | translated names of three codes | keys deleted from all four files; `'roles.'` removed from the skip list (no `roles.${…}` call remains) |
| Tests | see §12.1 | fixtures build roles from `SystemRoles.StarterRoles`; assertions about who reaches whom are unchanged; the two `bosh_rahbar_mavjud` assertions become `rol_band` |

End-of-R1 check (DoD): `grep -rn "SystemRoles.MainBoss\|SystemRoles.HrManager" api/src`
finds only `SystemRoles.cs` (starter definitions) and
`DatabaseInitializer.CreateDevUsersAsync`.

---

## 5. Data model and migrations

Naming: `UseSnakeCaseNamingConvention()` produces the column names below; do not
set `HasColumnName` except where the existing code already does.

### 5.1 `roles` — changed (R1)

| Column | Type | Rule |
|---|---|---|
| `module_levels` | `jsonb NOT NULL DEFAULT '{}'` | `{"employees":"Full","employment":"Edit",…}`. Keys = module keys, values = `View`/`Edit`/`Full`; a missing key = None. EF: `Dictionary<string, AccessLevel>` via a value converter (JSON with enum names) plus a `ValueComparer` comparing content, order-insensitive. |
| `capabilities` | `text[] NOT NULL DEFAULT '{}'` | Ticked capability codes after `Normalize` (sorted, no implied ones). `PrimitiveCollection` like `permissions`. |
| `description` | `text NULL` | ≤ 300. |
| `single_holder` | `boolean NOT NULL DEFAULT false` | The per-role switch of G10. Ignored for super_admin (always single-holder by code). |
| `deleted_at`, `deleted_by` | `timestamptz NULL`, `uuid NULL` | `Role` becomes `ISoftDeletable` (global query filter applies). |
| `is_system` | `boolean` (existing) | **Meaning narrows**: `true` only for the two protected roles; the migration sets it to `false` for `main_boss` and `hr_manager` (§5.5). New code never reads it (protection is derived from the code, §4.1). It is kept consistent because the previous release's seeder skips roles with `is_system = false` — so a code-only rollback leaves the owner's edits alone (§5.7). |
| `permissions` | `text[]` (existing) | **Meaning changes: derived mirror.** Written only when a role is saved through the API, as `Expand(...)` **including** the compatibility alias. **Never** re-derived at startup — no startup step writes to an existing role (§5.6). Read by nothing in the new code; kept only so the previous release works after a code-only rollback. Never edit it by hand. |
| index `ux_roles_name` | unique on `lower(name)` `WHERE deleted_at IS NULL` | Created with raw SQL (the catalogue tables' `ux_<table>_name` convention). |

Domain (`Bonvi.Domain/Identity/Role.cs`): add `ModuleLevels`, `Capabilities`,
`Description`, `SingleHolder`; derived `IsProtected` (super_admin or the bot) and
`IsTechnical` (the bot), both from `Code`; `EffectiveCodes()` — protected roles
return their code definition (§4.1), ordinary roles `AccessCatalog.Expand(...)`;
`IsSingleHolder` = `SingleHolder || Code == super_admin`. Mutators, all refusing
protected roles with `rol.ozgarmas`: `SetAccess(levels, capabilities)` (validate
+ normalise + write the mirror), `Rename(name)` (existing method, now available
for every ordinary role), `Describe(description)`, `SetSingleHolder(bool)`.
`CreateCustom(name, description)` generates the code (`custom_` + last 10 hex
characters of the id). `Grant` and `Revoke` are removed — individually granted
codes no longer exist; their tests are rewritten (§12.1). `Recode` stays
(legacy `admin` → `super_admin`, §5.6).

`SystemRoles` moves to its own file `Bonvi.Domain/Identity/SystemRoles.cs` and
holds: `Protected` (super_admin: every code; recruiting_bot: its two codes) and
`StarterRoles` — `SystemRoleDefinition(Code, Name, SingleHolder, Levels,
Capabilities)` for the four rows of §4.2, used **only** on an empty database.
The constants `MainBoss` and `HrManager` remain only as the codes of those
starter rows. `Defaults`, `FullAccess` and the `SingleHolder` code set are
removed (§4.6).

### 5.2 `users` — changed (R1)

| Column | Type | Rule |
|---|---|---|
| `access_stamp` | `integer NOT NULL DEFAULT 1` | Incremented whenever the user's effective access or credentials change (§6). |

Domain: `User.AccessStamp` + `BumpAccessStamp()`. Bumped inside `SetRole` and
`ChangeRole` (only when the role actually changes), both `Deactivate` overloads,
`Activate`, and a new `ResetPassword(hash)` (= `ChangePassword` + bump, used by
`ResetUserPasswordCommandHandler`). `ChangePassword` itself does **not** bump —
login calls it for silent re-hashing and must not log the user out elsewhere.

### 5.3 `audit_logs` — changed (A1)

Existing columns stay with their types (`old_values`/`new_values` stay `text`
holding JSON — changing them to `jsonb` would break a code-only rollback because
the previous release binds them as text).

| Column | Type | Rule |
|---|---|---|
| `actor_kind` | `integer NOT NULL DEFAULT 1` | `AuditActorKind`: `User = 1`, `System = 2`, `Anonymous = 3`. Numbers 4 (`EmployeeAccount`) and 5 (`Device`) are reserved for the verification merge — write that in a code comment, do not add the members now. Existing rows become `User` (they all are). |
| `actor_name` | `text NULL` | Full name snapshot at the time of the change. |
| `actor_role` | `text NULL` | Role **name** snapshot, exactly as it read at that moment. Names are owner-editable free text now, so resolving them at read time would rewrite history after a rename. |
| `operation` | `text NULL` | Endpoint name (`TransferEmployee`) or system operation (`startup:identity-seed`, `etl:employees`). |
| `correlation_id` | `uuid NULL` | Same value for every row written during one HTTP request / one system scope. |
| `subject_employee_id` | `uuid NULL` | The employee this change concerns (§7.3). |
| `entity_label` | `text NULL` | Human label snapshot (employee/candidate full name, role name, catalogue name…), ≤ 200 chars. |
| `changed_fields` | `text[] NULL` | Field names of the change (Create: all non-null fields written; Update: changed ones; Delete: `NULL`). |
| index `ix_audit_logs_subject_employee_id_occurred_at` | `(subject_employee_id, occurred_at DESC) WHERE subject_employee_id IS NOT NULL` | per-employee history |
| index `ix_audit_logs_correlation_id` | `(correlation_id) WHERE correlation_id IS NOT NULL` | "same action" grouping |

`AuditAction` gains `TokenReuse = 9` (appended; stored as int). Values 10/11 are
reserved for the WebAuthn phase (`StepUp`, `StepUpFailed`) — comment only.

Domain (`Bonvi.Domain/Auditing/AuditLog.cs`): add the properties above and a
second factory `RecordChange(...)` carrying all of them. The type stays without
setters or mutators.

### 5.4 Migration A1 — `AuditJurnaliKengaytirildi`

`Up`, exact order:

1. `ALTER TABLE audit_logs ADD COLUMN …` for the eight columns of §5.3.
2. The two indexes (raw SQL, partial).
3. The immutability function and triggers (§7.7) — **last**, so nothing above is
   blocked.

`Down`: drop triggers, function, indexes, columns. `Down` exists for development
only; production rollback never runs it (§13).

After `--autogenerate`, read the migration: it must contain no `DropColumn`,
`DropTable` or `AlterColumn` of existing columns.

### 5.5 Migration R1 — `RollarMuharriri`

`Up`, exact order:

1. `roles`: add `module_levels`, `capabilities`, `description`, `single_holder`,
   `deleted_at`, `deleted_by`. `users`: add `access_stamp`.
2. Convert every existing role (raw SQL, verbatim; the last two statements make
   today's «Bosh rahbar» and «HR menejeri» ordinary and keep «Bosh rahbar»
   single-holder):

```sql
UPDATE roles SET module_levels = jsonb_strip_nulls(jsonb_build_object(
  'employees', CASE WHEN permissions @> ARRAY['employees.delete'] THEN 'Full'
                    WHEN permissions && ARRAY['employees.create','employees.edit'] THEN 'Edit'
                    WHEN permissions @> ARRAY['employees.view'] THEN 'View' END,
  'employment', CASE WHEN permissions @> ARRAY['employment.manage'] THEN 'Full'
                     WHEN permissions @> ARRAY['employment.view'] THEN 'View' END,
  'lists', CASE WHEN permissions @> ARRAY['lists.manage'] THEN 'Edit'
                WHEN permissions @> ARRAY['lists.view'] THEN 'View' END,
  'incentives', CASE WHEN permissions @> ARRAY['incentives.manage'] THEN 'Full'
                     WHEN permissions @> ARRAY['incentives.view'] THEN 'View' END,
  'safety', CASE WHEN permissions @> ARRAY['safety.manage'] THEN 'Full'
                 WHEN permissions @> ARRAY['safety.view'] THEN 'View' END,
  'feedback', CASE WHEN permissions @> ARRAY['feedback.manage'] THEN 'Full'
                   WHEN permissions @> ARRAY['feedback.view'] THEN 'View' END,
  'recruiting', CASE WHEN code = 'recruiting_bot' THEN 'Edit'
                     WHEN permissions @> ARRAY['recruiting.manage'] THEN 'Full'
                     WHEN permissions @> ARRAY['recruiting.view'] THEN 'View' END,
  'reports', CASE WHEN permissions @> ARRAY['reports.export'] THEN 'Full'
                  WHEN permissions @> ARRAY['reports.view'] THEN 'View' END,
  'catalog', CASE WHEN permissions @> ARRAY['catalog.manage'] THEN 'Full'
                  WHEN permissions @> ARRAY['catalog.view'] THEN 'View' END,
  'users', CASE WHEN permissions @> ARRAY['users.manage'] THEN 'Full' END,
  'roles', CASE WHEN code = 'super_admin' THEN 'Full'
                WHEN permissions @> ARRAY['users.manage'] THEN 'View' END,
  'audit', CASE WHEN permissions @> ARRAY['audit.view'] THEN 'View' END,
  'settings', CASE WHEN permissions @> ARRAY['settings.manage'] THEN 'Full' END));

UPDATE roles SET capabilities = ARRAY(
  SELECT c FROM unnest(ARRAY[
    CASE WHEN module_levels->>'employees' = 'View' THEN 'employees.personal.view' END,
    CASE WHEN permissions @> ARRAY['salary.view']
          AND module_levels->>'employment' IN ('View','Edit') THEN 'salary.view' END
  ]) AS c WHERE c IS NOT NULL ORDER BY c);

UPDATE roles SET single_holder = (code IN ('super_admin', 'main_boss'));
UPDATE roles SET is_system     = (code IN ('super_admin', 'recruiting_bot'));
```

   Every role that could see personal data keeps seeing it; `salary.view` is kept
   where it is not already implied. Production today has four roles
   (super_admin, main_boss, hr_manager, recruiting_bot); the pre-deploy SQL
   (§13.1) prints them so nothing unexpected is converted.
3. `CREATE UNIQUE INDEX ux_roles_name ON roles (lower(name)) WHERE deleted_at IS NULL;`

`permissions` is **not** touched: the new code never reads it, the previous
release keeps reading it after a rollback, and it is rewritten only when the
owner saves that role in the editor. No startup step writes to a role (§5.6).

`Down`: drop the index and the added columns.

### 5.6 `IdentitySeeder` — new semantics (R1)

The owner's rule: **startup never creates, resets or rewrites a role that
already exists.** The seeder does exactly this, audited as `System` /
`startup:identity-seed` (this reverses fact 3):

1. `Kochir` (legacy `admin` → `super_admin`) — kept. It fires only on a database
   that still has the pre-2026-08 `admin` system role and no `super_admin`; no
   such database exists any more, and it can never touch a role the owner made.
2. If the `roles` table is **empty** (counted with `IgnoreQueryFilters`, so
   soft-deleted rows make it non-empty): create the four
   `SystemRoles.StarterRoles` rows of §4.2 in one `SaveChanges`. This happens on
   test databases, fresh development databases and new installations — never in
   production.
3. Otherwise: nothing. If `super_admin` or `recruiting_bot` is missing (possible
   only by editing the database by hand), log an error
   (`LogProtectedRoleMissing`, next free `EventId` in `DatabaseInitializer`) and
   continue — do not recreate it.

There is no mirror re-derivation and no stamp bump at startup. The protected
roles need no enforcement step: their rights are computed from code on every
token issue (§4.1), whatever their stored columns say. Deleting a starter role
is therefore permanent — a restart does not bring it back.

### 5.7 Expand-only rule and rollback compatibility

Both migrations only **add** nullable or defaulted columns, indexes, a trigger and
a function; they drop, rename and retype nothing. Consequences, each verified in
§12.3:

- The previous release runs unchanged against the migrated schema (new columns
  have defaults; it never updates or deletes audit rows).
- A production rollback is **code-only** (§13.3): re-tag and restart the previous
  images. After a rollback from R1+, the previous code reads the `permissions`
  mirror: the untouched pre-R1 codes for roles the owner has not saved, the
  latest derived codes (plus `employment.manage` where the role has an
  employment action) for roles he has saved — so holders of roles he created
  keep working. The previous seeder skips rows with `is_system = false`, so the
  owner's roles and names survive the rollback; it re-applies its old
  definitions only to super_admin and the bot, which are unchanged anyway.

### 5.8 Deploy changes nothing; the split is the owner's runtime action

After R1 every current user has exactly today's rights (§4.2) and nothing
creates an HR head. Once R2 is live the owner can, at any time and without a
deploy or restart: create «Kadr bo‘limi boshlig‘i» (e.g. by duplicating
«HR menejeri» and setting «Mehnat munosabatlari» to «To‘liq»), assign it, then
reduce «HR menejeri» — Appendix A lists the steps. Each step reaches the
affected users on their next request (§6).

Order matters only for convenience. If «HR menejeri» is reduced before anyone
holds the HR head role, no action is stranded — super_admin always holds every
code and «Bosh rahbar» keeps Employment «To‘liq» — but the HR managers must
wait for those two until an HR head is assigned.

---

## 6. Token and permission freshness (R1)

**Mechanism: access stamp checked on every authenticated request.**

1. `JwtTokenService.CreateTokens` adds claim `ast` = `user.AccessStamp`
   (decimal string). Test helpers keep working because they pass a real user
   loaded from the database (fact 13).
2. `AddBonviAuthentication` sets `JwtBearerEvents.OnTokenValidated`: resolve
   `IAccessStampValidator` (Application port, Infrastructure implementation) from
   `context.HttpContext.RequestServices` and run one query:
   `SELECT is_active, access_stamp FROM users WHERE id = @sub AND deleted_at IS NULL`.
   Invalid when: no row, inactive, no `ast` claim, or `ast` ≠ `access_stamp` →
   `context.Fail("auth.token_eskirgan")` → the existing `OnChallenge` returns
   **401 `auth.token_yaroqsiz`**. No cache: one primary-key lookup per request is
   well under a millisecond at this size, and a cache is the classic place for an
   invalidation bug.
3. What bumps the stamp: role assignment change, deactivate, activate, password
   reset (§5.2), and **every** role update — levels, actions, single-holder
   switch, name, description — for every non-deleted holder, in the same
   transaction (`UpdateRoleCommand`). Nothing at startup bumps stamps (§5.6).
4. Client (`web/src/shared/api/http-client.ts`): on 401 it already refreshes and
   retries. Add: after a successful refresh, store `data.user` with
   `authStorage.setUser` and dispatch `window` event `bonvi:user-refreshed`
   (`detail` = the user). `AuthProvider` (`features/auth/auth-context.tsx`)
   listens and calls `setUser(parseAuthUser(detail))`, so menus and buttons
   re-render with the new permissions. Refresh failure (e.g. 403
   `auth.hisob_faol_emas` after deactivation) keeps today's behaviour: clear
   session, go to `/kirish`.
5. SSE (`NotificationEndpoints.StreamNotificationsAsync`): at the top of every
   loop iteration (≤ 20 s), re-run the validator for `http.User` and check the
   `exp` claim; if either fails, end the response. The client already reconnects,
   hits 401, refreshes and opens a new stream.
6. Deploy effect: tokens issued before R1 have no `ast` → every open session
   silently refreshes once. The bot re-logs in on 401 (fact 15).

Result: a role edit, deactivation or password reset takes effect on the affected
user's **next request**; the UI updates on that same request.

### 6.1 Every role change is a runtime change — no deploy, no restart

| Change made in the UI | What the server does | When the affected people see it |
|---|---|---|
| Create a role | inserts the row | at once: the role list and the user form read the database on every request |
| Rename it / change its description | updates the row; bumps every holder's stamp | holders: on their next request (the silent refresh brings the new `roleName` into `/me`); everyone else: on their next list load |
| Change levels, actions or the single-holder switch | updates the row; bumps every holder's stamp | holders: on their next request (401 → silent refresh → new codes → menus and buttons re-render) |
| Delete an unused role | soft-deletes the row | at once: it leaves every list; it has no holders to notify |
| Assign a role to a user, or change it (create/edit user) | updates the user; bumps that user's stamp | that user: on the next request |
| Deactivate, activate, reset a password | as in §5.2 | that user: on the next request |

Nothing caches roles: they are read from the database whenever a token is
issued or a list is requested. The permission **catalog** (§3.3–3.5) is the
only static part; it changes only with a release that adds a feature. One
latent trap is recorded in §16: the defined-but-unused output-cache policy keys
by role codes.

Rejected alternative: shortening the access token to 1–2 minutes — costs a
refresh storm, still leaves a window, and does not update the UI.

---

## 7. Audit trail

### 7.1 What is recorded

1. **Every entity change** made through `AppDbContext.SaveChanges` — insert,
   update, soft delete, hard delete — automatically, by `AuditTrailInterceptor`
   (A1). Includes domain actions: a transfer is one `Employment` update + one
   `EmploymentEvent` insert with the same `correlation_id` and
   `operation = TransferEmployee`; an event correction is an `EmploymentEvent`
   update whose `old_values` keep what the overwrite destroyed (closes fact 8).
2. **Security events** (A1): `Login`, `LoginFailed`, `Logout`, `TokenReuse`,
   `PermissionDenied` (policy 403s), `Export` (Excel export: filters + row count,
   no personal data).
3. **ETL and startup writes** (A1): same interceptor, actor `System`, operation
   `etl:<step>` / `startup:<name>`.

Not recorded: reads; the two tracker-bypassing writes of fact 10 (expired
refresh tokens; derived department `level`/`path`).

### 7.2 Interceptor algorithm

`Bonvi.Infrastructure/Persistence/Interceptors/AuditTrailInterceptor.cs`,
registered **after** `AuditableInterceptor` (it must see soft deletes already
converted and `UpdatedAt` set) in both `AddPersistence` and `EtlDatabase`.
Implement `SavingChanges` and `SavingChangesAsync` (the sync path uses sync
queries), `SaveChangesFailed(Async)` and `SavedChanges(Async)`.

```
SavingChanges:
  DetectChanges()
  if any AuditLog entry is Modified or Deleted → throw InvalidOperationException (append-only)
  for each entry in Added | Modified | Deleted whose type is audited (§7.3):
     Added    → Create; new = snapshot(current values, non-null, non-excluded)
     Deleted  → Delete; old = snapshot(original values, non-excluded)
     Modified → if ISoftDeletable and DeletedAt: null → value  ⇒ Delete, old = snapshot(original)
                else changed = properties with IsModified && !comparer.Equals(original, current),
                     minus excluded; if empty → skip
                     old/new = only the changed fields
     masked fields: value written as "***", field name kept in changed_fields
  resolve subject_employee_id and entity_label (tracked entities first, then one
     batched AsNoTracking query per target table)
  actor = IAuditActorSource.Get()   (once per scope)
  add one AuditLog per change; remember them for this SaveChanges call
SaveChangesFailed → detach the remembered AuditLog entries (a failed save must
  leave nothing behind for a later successful save)
SavedChanges → forget them
```

Complex properties (`FullName`, `Money`) are flattened with dotted names
(`Salary.Amount`, `Salary.Currency`, `FullName.LastName`). Primitive collections
(`DirectionIds`, `RoleIds`, `Capabilities`, `Permissions`) are compared with their
value comparer and written as JSON arrays.

### 7.3 Classification catalog

`Bonvi.Infrastructure/Persistence/Auditing/AuditCatalog.cs`. Global exclusions
for every entity: `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`,
`DeletedAt`, `DeletedBy`, `RecordedAt` (the audit row itself carries who/when).
Module keys are the §3.3 keys; A1 introduces them as string constants in
`Bonvi.Domain/Identity/AccessModuleKeys.cs`, and R1's catalog reuses them.

| Entity | Audited | Module | Subject employee | Label | Field rules |
|---|---|---|---|---|---|
| `User` | yes | users | — | full name | `PasswordHash` masked (never stored); `LastLoginAt`, `FailedLoginAttempts`, `LockedUntil`, `AccessStamp` excluded; `RoleIds` ref→roles |
| `Role` | yes | roles | — | name | all kept (`Permissions` shown as technical) |
| `RefreshToken`, `NotificationState`, `AuditLog` | **no** | | | | |
| `LegalEntity`, `Direction`, `TerminationReason`, `AwardType`, `PenaltyType`, `DocumentType`, `VacancyTitle` | yes | catalog | — | name | — |
| `PipelineStage` | yes | recruiting | — | name | — |
| `Department` | yes | catalog | — | name | `Level`, `Path` excluded (derived); `ParentId`, `LegalEntityId` ref; `HeadEmployeeId` ref employee |
| `Position`, `PositionDuty` | yes | catalog | — | name / position name | `DepartmentId`, `PositionId` ref |
| `Employee` | yes | employees | own `Id` | full name | **personal**: `BirthDate`, `Pinfl`, `Passport`, `SecondPhone`, `Address`, `HasCriminalRecord`, `CriminalRecordNote`, `MehnatUzUrl`; `DirectionIds` ref |
| `Employment` | yes | employment | `EmployeeId` | employee name | **salary**: `Salary.*`; refs: `LegalEntityId`, `DepartmentId`, `PositionId`, `TerminationReasonId`, `ResponsibleEmployeeId` |
| `EmploymentEvent` | yes | employment | via `EmploymentId` | employee name | **salary**: `FromSalary.*`, `ToSalary.*`; refs: department/position ids, `RecordedBy`→user, `CandidateId`→candidate |
| `Probation` | yes | employment | via `EmploymentId` | employee name | `ReviewerEmployeeId` ref |
| `EmployeeFlag` | yes | lists | `EmployeeId` | employee name | — |
| `EmployeeCompetency`, `EmployeeCompetencyLanguage` | yes | employees (read needs `employees.personal.view`) | `EmployeeId` / via competency | employee name | — |
| `EmployeeDocument` | yes | employees (read needs `employees.personal.view`) | `EmployeeId` | employee name | `DocumentTypeId` ref |
| `EmployeeCheckFile` | yes | employees (read needs `employees.personal.view`) | `EmployeeId` | employee name | — (soft delete; evidence for the «Tekshiruvlar» card) |
| `Incentive` | yes | incentives | `EmployeeId` | employee name | `TypeId` ref (award or penalty type by `Kind`) |
| `ExitInterview` | yes | feedback | via `EmploymentId` | employee name | — |
| `Incident` | yes | safety | `EmployeeId` | employee name | `ResponsibleEmployeeId` ref |
| `Vacancy` | yes | recruiting | — | vacancy title name | `VacancyTitleId` ref, `ResponsibleEmployeeId` ref |
| `Candidate`, `CandidateStageEntry`, `CandidateNote`, `CandidateFile`, `CandidateQuestionnaire` | yes | recruiting | — | candidate full name | `StageId` ref, `VacancyId` ref |
| `SystemSetting` | yes | settings | — | key | `Value` masked when the key's kind is `Secret` |
| `ApiKey` | yes | settings | — | name | `KeyHash` masked; `LastUsedAt`, `LastUsedIp` excluded (updated on every use) |

Security events belong to module `users`; `Export` to `reports`.

`AuditCatalogCoverageTests` (§12.1) fails when an entity type in the EF model is
neither audited nor explicitly excluded, or when a property whose name contains
`Hash`, `Token`, `Secret` or `Password` is neither masked nor excluded. This is
what forces the `verification` merge to classify its entities (§14).

### 7.4 Actor, operation, correlation, request metadata

New Application ports, implemented per host:

| Port | HTTP implementation (Api) | Non-HTTP |
|---|---|---|
| `IRequestContext` { `Operation`, `CorrelationId`, `IpAddress`, `UserAgent` } | Operation = `IEndpointNameMetadata.EndpointName` of the matched endpoint; CorrelationId = a Guid v7 created once per request (kept in `HttpContext.Items`); IP = `Connection.RemoteIpAddress` (forwarded headers already applied); UserAgent = header, first 300 chars | `IAuditScope.Use(operation)` sets Operation and a new CorrelationId for the scope |
| `IAuditActorSource` → `{ Kind, UserId, Login, FullName, RoleName }` | `ICurrentUser.UserId` set → `User` + one `AsNoTracking` lookup of full name and current role name, cached for the scope; no user on an HTTP request → `Anonymous` | `System` |

Call sites that must set `IAuditScope.Use(...)`: `DatabaseInitializer`
(`startup:identity-seed`, `startup:catalog-seed`, `startup:general-pool`,
`startup:candidate-handover`, `startup:first-admin`, `startup:bot-user`,
`startup:dev-users`) and the ETL (`etl:<step name>`).

`AuditLogger` (manual path) fills the same metadata from the same ports.

### 7.5 Value serialization

`AuditValueSerializer`, System.Text.Json, invariant culture, keys in EF property
order:

- enum → member **name**; `DateOnly` → `yyyy-MM-dd`; `DateTimeOffset` → ISO-8601
  UTC; `decimal`/ints → JSON number; `Guid` → string; `bool` → JSON bool;
- property with a value converter (value objects: `Pinfl`, `PhoneNumber`,
  `PassportSeries`) → the converter's **provider** value;
- `jsonb` columns (`Role.ModuleLevels`) → embedded as a JSON object, not a string;
- primitive collections → JSON arrays; `byte[]` → never (masked);
- `null` values are omitted from Create snapshots and kept as `null` in Update
  diffs; strings are stored in full (no truncation — truncation loses history).

### 7.6 Security events

`IAuditLogger` gains
`RecordSecurityEvent(AuditAction action, Guid? userId, string? login, string? detailJson)`
for callers without an authenticated principal. Each call site saves in its own
flow:

| Event | Where | Actor | Detail |
|---|---|---|---|
| `Login` | `LoginCommandHandler`, success | the user | — |
| `LoginFailed` | every early return of the handler: unknown login (actor `Anonymous`, `user_login` = the typed login, normalised when parseable, else first 32 chars), wrong password, locked, inactive | the user when known | `{"reason":"credentials"|"locked"|"inactive"}` |
| `Logout` | `LogoutCommandHandler` | token's user | `{"allSessions":true/false}` |
| `TokenReuse` | `RefreshSessionCommandHandler`, reuse branch | token's user | — |
| `PermissionDenied` | `JwtBearerEvents.OnForbidden` (policy 403s only): resolve `IAuditLogger` and `IUnitOfWork` from `RequestServices`, record, save, then write the existing 403 body; a failure to save is logged and never changes the 403 | principal | `{"method":"POST","path":"/api/employments/{id:guid}/salary"}` — the route **pattern**, never the query string |
| `Export` | `ExportEmployeesQueryHandler` | current user | filters + row count |

The five manual `_auditLogger.Record(...)` calls in
`Identity/Users/Commands/*` are **removed**: the interceptor records the same
changes automatically (with `PasswordHash` masked and `operation =
ResetUserPassword`), and keeping both would write every user change twice.

### 7.7 Immutability

Application level:
- `AuditLog` has no setters and no mutators (unchanged).
- The interceptor throws if an `AuditLog` entry is `Modified` or `Deleted`.
- No endpoint maps `PUT`/`PATCH`/`DELETE` under `/api/audit` (pinned by the
  inventory test).

Database level (migration A1):

```sql
CREATE FUNCTION audit_logs_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'audit_logs: yozuvlar o''zgartirilmaydi va o''chirilmaydi (%)', TG_OP;
END $$;
CREATE TRIGGER trg_audit_logs_append_only BEFORE UPDATE OR DELETE ON audit_logs
  FOR EACH ROW EXECUTE FUNCTION audit_logs_append_only();
CREATE TRIGGER trg_audit_logs_no_truncate BEFORE TRUNCATE ON audit_logs
  FOR EACH STATEMENT EXECUTE FUNCTION audit_logs_append_only();
```

Triggers fire for every role, superusers included, unless someone deliberately
runs DDL (`ALTER TABLE … DISABLE TRIGGER`, `DROP TRIGGER`). A future migration
that really must rewrite audit rows has to disable the trigger in plain sight of
code review.

**Honest limit, to tell the owner:** a person with root on the server can still
disable the trigger and edit rows. That cannot be prevented inside one machine;
it is made *detectable* by the daily `pg_dump` backups (30 days, off the live
table) — a tampered row differs from yesterday's backup. Stronger options are in
§16 (deferred).

### 7.8 Retention, volume, performance

- **Retention: forever.** No purge job. Any future retention rule needs the
  owner's decision and a migration that visibly disables the trigger.
- **Volume** (≈2,400 employees, 3 HR users, the bot): a busy day ≈ 20 hires × 5
  rows + 50 transfers/edits × 2 + 100 candidate actions × 3 + 50 bot candidates
  × 4 + 30 other × 2 ≈ **750 rows/day**, ≈1 KB each with indexes → **≈0.2–0.3 GB
  per year** at the high end, typically ~0.1 GB. Daily backups grow by the
  compressed share of that. The VPS has 40 GB. No partitioning; revisit at 5 GB
  or 10 M rows.
- **Write cost:** per request that saves, one actor lookup (cached per scope)
  and at most one batched lookup per referenced table for subjects/labels.
  Per-row JSON serialisation of a few fields.
- **Read cost:** every audit query is bounded by a date range (default: last 30
  days, §8.3) and uses `ix_audit_logs_occurred_at` or the subject index; pages of
  50.
- **ETL:** a full ETL run writes ~20k audit rows (~20 MB). Acceptable for a tool
  run a few times a year.

### 7.9 Employment events: author and corrections

- **A1** — `AuditableInterceptor`: for every `Added` `EmploymentEvent` whose
  `RecordedBy` is `null` and a current user exists, set it through EF
  (`entry.Property(nameof(EmploymentEvent.RecordedBy)).CurrentValue = userId`).
  ETL and startup rows stay `null` (their domain meaning: "imported"). Past events
  stay `null` — there is no source to backfill from.
- **A2** — `EmploymentEventDto` gains `RecordedByName` (`string?`, users joined
  with `IgnoreQueryFilters`, so deactivated authors still resolve); the timeline
  shows it (§9.6). A1 does not change any DTO.
- `EmploymentEvent.Edit` keeps overwriting — that is the owner's 2026-09-16
  decision for typo fixes — and the overwritten values now live in the audit row
  of that correction.

### 7.10 Reading the trail — visibility and masking (A2)

A row is returned to a viewer only if the viewer has `audit.view` **and** the
*entry code* of the row's module (§7.3) — the code added by the module's lowest
offered level: `employees.view`, `employment.view`, `lists.view`,
`incentives.view`, `safety.view`, `feedback.view`, `recruiting.view`,
`reports.view`, `catalog.view`, `users.manage`, `roles.view`, `audit.view`,
`settings.manage`. Two refinements: rows of `EmployeeCompetency*`,
`EmployeeDocument` and `EmployeeCheckFile` need `employees.personal.view`; security events need
`users.manage`. Within a visible row, `salary` fields need
`salary.view` and `personal` fields need `employees.personal.view`; otherwise the
change is returned with `masked: true` and no values. Field **names** stay
visible ("the salary changed on this date" is acceptable; the amount is not).

---

## 8. API contract (new and changed)

All new endpoints: `.WithName`, `.WithSummary`, `.WithDescription` in the
project's Uzbek style, `Produces`/`ProducesProblem` declared. Request records
must not use `Guid = default` (the OpenAPI 500 trap, memory 2026-08-29); query
parameters are nullable.

### 8.1 Roles and access catalog (R1)

| Route | Policy | Body / query | Response |
|---|---|---|---|
| `GET /api/access-catalog` | `roles.view` | — | `AccessCatalogDto` |
| `GET /api/roles` | `roles.view` | — | `IReadOnlyList<RoleSummaryDto>` — excludes the technical role; super_admin first, then every other role by name (`StringComparer.OrdinalIgnoreCase`) |
| `GET /api/roles/{id}` | `roles.view` | — | `RoleDetailsDto`; 404 `rol.topilmadi` (also for the technical role) |
| `POST /api/roles` | `roles.manage` | `SaveRoleRequest` | `RoleDetailsDto` (200) |
| `PUT /api/roles/{id}` | `roles.manage` | `SaveRoleRequest` | `RoleDetailsDto` |
| `DELETE /api/roles/{id}` | `roles.manage` | — | 204 |

```csharp
public sealed record SaveRoleRequest(
    string? Name,                                 // free text, one language (G12)
    string? Description,
    bool? SingleHolder,                           // create: null = false; update: null = unchanged
    IReadOnlyList<ModuleLevelInput>? Modules,     // omitted module = None; duplicates → 400 rol.bolim_takror
    IReadOnlyList<string>? Capabilities);
public sealed record ModuleLevelInput(string? Module, AccessLevel Level);   // enum as string

public sealed record AccessCatalogDto(IReadOnlyList<AccessModuleDto> Modules);
public sealed record AccessModuleDto(
    string Key, string Section, IReadOnlyList<AccessLevel> OfferedLevels,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Adds,   // "View" → codes
    IReadOnlyList<AccessCapabilityDto> Capabilities,
    IReadOnlyList<AccessDependencyDto> Dependencies);          // this module's own requirements
public sealed record AccessCapabilityDto(
    string Code, string Kind /* Visibility|Operation */, AccessLevel AssignableFrom,
    IReadOnlyList<AccessLevel> ImpliedAt, IReadOnlyList<string> RequiresCapabilities,
    string? RequiresModule, AccessLevel? RequiresModuleLevel);
public sealed record AccessDependencyDto(AccessLevel WhenAtLeast, string RequiresModule, AccessLevel RequiresLevel);

public sealed record RoleSummaryDto(
    Guid Id, string Code, string Name, string? Description,
    string Kind /* Protected|Ordinary */, bool IsSingleHolder,
    int HolderCount, int ActiveHolderCount,
    IReadOnlyList<ModuleLevelDto> Modules, IReadOnlyList<string> Capabilities,
    bool CanAssign, bool CanEdit, bool CanDelete);            // computed for the caller, §4.4/§4.5
public sealed record RoleDetailsDto(
    RoleSummaryDto Summary,                                   // nested, not flattened
    IReadOnlyList<RoleHolderDto> Holders,
    DateTimeOffset? UpdatedAt, string? UpdatedByName);
public sealed record ModuleLevelDto(string Module, AccessLevel Level);
public sealed record RoleHolderDto(Guid UserId, string FullName, string Login, bool IsActive);
```

`Capabilities` in the DTOs are the **stored** (ticked) ones; implied ones are
derived by the client from the catalog. For super_admin, `Modules` is the
computed "highest offered level everywhere" (read-only display); its stored
columns are never shown.

Handlers live in `Bonvi.Application/Identity/Roles/`: `ListRolesQuery`,
`GetRoleQuery`, `GetAccessCatalogQuery`, `CreateRoleCommand`,
`UpdateRoleCommand` (bumps holders' stamps on any change, §6.1),
`DeleteRoleCommand`, `RoleErrors`, `RoleAccessRules` (G4/G5/G10). Endpoints in
`Bonvi.Api/Endpoints/RoleEndpoints.cs`, mapped in `Program.cs` after users.

### 8.2 Users (changed, R1)

- `UserSummaryDto` + `RoleId` (`Guid?`) + `CanManage` (`bool`). `RoleName`
  (existing) is the stored free-text name.
- `CurrentUserDto` (`/api/auth/me`, login and refresh responses) + `RoleName`
  (`string?`) — the settings dialog shows it (§4.6).
- Request contracts unchanged (`roleCode` stays the identifier; renaming never
  changes a code; new roles have generated codes).
- Reach and assignment follow §4.5; a second active holder of a single-holder
  role → 409 `foydalanuvchi.rol_band` (super_admin keeps
  `foydalanuvchi.super_admin_mavjud`).

### 8.3 Audit (A2)

| Route | Policy | Query | Response |
|---|---|---|---|
| `GET /api/audit` | `audit.view` | `from`, `to` (Tashkent `DateOnly`, inclusive; neither → last 30 days), `actor` (**string**: a user id, or the literal `system`), `module`, `action`, `employeeId`, `entity` (entity type name, §7.3), `entityId`, `kind` (`changes` default \| `security` \| `all`), `page` (from 1), `pageSize` (default 50, max 200) | `PagedResult<AuditEntryDto>`, newest first (`occurred_at DESC, id DESC`) |
| `GET /api/audit/{id}` | `audit.view` | — | `AuditEntryDetailsDto`; 404 `audit.topilmadi` when missing **or not visible** to the caller |
| `GET /api/audit/actors` | `audit.view` | — | `[{ userId, fullName, login, isActive }]` — users that appear in the log |
| `GET /api/employees/{id}/history` | `audit.view` AND `employees.view` | `module`, `page`, `pageSize` | `PagedResult<AuditEntryDto>` where `subject_employee_id = id`, `kind = changes` |

`from > to` → 400 `audit.davr_notogri`. Unknown `module`/`action`/`entity`, or an
`actor` that is neither a GUID nor `system` → 400 (same style as
`ListEmploymentEventsQuery`'s `type`). A `module` or `entity` the caller cannot see
is not an error: visibility is applied first, so the page is simply empty.
`kind=security` or `all` without `users.manage` behaves as `changes`. Dates convert
with `BonviTime` (`[from 00:00 +05, to+1 00:00 +05)`).

```csharp
public sealed record AuditEntryDto(
    Guid Id, DateTimeOffset OccurredAt, AuditAction Action, string? Operation,
    string? Module, string EntityName, string? EntityId, string? EntityLabel,
    Guid? SubjectEmployeeId, AuditActorDto Actor,
    IReadOnlyList<AuditChangeDto> Changes, Guid? CorrelationId);
public sealed record AuditActorDto(
    AuditActorKind Kind, Guid? UserId, string? Login, string? Name,
    string? RoleName);                   // the snapshot in actor_role — the name at that moment
public sealed record AuditChangeDto(
    string Field,
    string ValueType,            // text|number|money|date|datetime|bool|enum|ref|list|json
    JsonElement? Old, JsonElement? New,
    string? OldLabel, string? NewLabel,   // ref fields: names resolved at read time
    bool Masked);
public sealed record AuditEntryDetailsDto(
    AuditEntryDto Entry, string? IpAddress, string? UserAgent,
    IReadOnlyList<AuditEntryDto> Related);   // same correlation_id, visible ones only
```

`money` changes merge `X.Amount` + `X.Currency` into one change whose values are
`{ "amount": 4000000, "currency": "UZS" }`. Ref labels are loaded in one batch
per target table with `IgnoreQueryFilters` (deleted names still resolve).

Implementation: `Bonvi.Application/Audit/` (queries, DTOs, `IAuditReadModel`),
`Bonvi.Infrastructure/Persistence/ReadModels/AuditReadModel.cs`,
`Bonvi.Api/Endpoints/AuditEndpoints.cs` (owns all four routes, including the
`/api/employees/{id}/history` one, via its own `MapGroup`).

### 8.4 Error codes (Uzbek, project style; all four locales, §9.9)

| Code | HTTP | When |
|---|---|---|
| `rol.topilmadi` | 404 | unknown, deleted or technical role id |
| `rol.nom_band` | 409 | name taken |
| `rol.nom_uzun` | 400 | name over 60 characters |
| `rol.tavsif_uzun` | 400 | description over 300 characters |
| `rol.ozgarmas` | 409 | change to super_admin (G1) |
| `rol.bir_nechta_egasi` | 409 | single-holder switch turned on while the role has 2+ active holders (G10) |
| `foydalanuvchi.rol_band` | 409 | a second active holder for a single-holder role (G10) |
| `rol.oz_roli` | 403 | edit own role (G4) |
| `rol.huquqdan_oshadi` | 403 | grants beyond the editor's own (G5) |
| `rol.foydalanishda` | 409 | delete a role in use (G6); `detail` contains the holder count |
| `rol.notanish_bolim` | 400 | unknown module key |
| `rol.bolim_takror` | 400 | same module twice in the request |
| `rol.daraja_yoq` | 400 | level not offered by that module |
| `rol.imkoniyat_daraja` | 400 | capability below its `AssignableFrom` |
| `rol.bogliqlik` | 400 | module dependency not met (`detail` names both modules) |
| `mehnat.oklad_huquqi_yoq` | 403 | money without `employment.salary` (§3.9) |
| `mehnat.kochirish_huquqi_yoq` | 403 | probation appointment without `employment.transfer` |
| `audit.topilmadi` | 404 | audit entry missing or not visible |
| `audit.davr_notogri` | 400 | `from` after `to` |

Existing codes reused: `rol.nom_bosh`, `rol.notanish_ruxsat`,
`foydalanuvchi.rolga_haqqi_yoq`, `foydalanuvchi.oxirgi_administrator`,
`foydalanuvchi.super_admin_mavjud`, `auth.token_yaroqsiz`, `auth.ruxsat_yoq`.
`rol.admin_ozgartirilmaydi` and `foydalanuvchi.bosh_rahbar_mavjud` lose their
last producers; keep their translations (old clients).

---

## 9. UI (iOS HIG — the project's standing design rule)

Use existing primitives only (`web/src/shared/ui`): `PageHeader`, `Card`,
`SegmentedControl`, `Switch`, `Badge`, `Button`, `Dialog`, `Select`,
`TextField`, `TextArea`, `IconTile`, `DataTable`, `EmptyState`, `Alert`,
`Skeleton`, `DateField`, `Avatar`; icons from `lucide-react`. No change to
`shared/ui` is needed (`SegmentedControl` has no disabled state — read-only mode
renders `Badge`s instead). Semantic colour tokens only; no new colours or radii.
Light and dark mode; usable at phone width.

### 9.1 Navigation (R2) — `nav-items.ts`, `sidebar.tsx`

Every item gets a `permission`; group children are filtered; a group with no
visible child is not drawn.

| Item | Permission |
|---|---|
| dashboard | none |
| reports | `reports.view` |
| vacancies, candidates, rejectedCandidates | `recruiting.view` |
| employees, formerEmployees | `employees.view` |
| probation, employment (Buyruqlar) | `employment.view` |
| incentives | `incentives.view` |
| reserve, blacklist, monitoring, capable | `lists.view` |
| incidents | `safety.view` |
| feedback | `feedback.view` |
| every catalogue child | `catalog.view` |
| users | `users.manage` |
| **roles** (new, `/rollar`, icon `ShieldCheck`) | `roles.view` |
| **audit** (new, `/audit`, icon `History`) | `audit.view` |
| systemSettings | `settings.manage` |

`sectionSystem` order: users, roles, audit, systemSettings. The sidebar probation
count and every dashboard query run only when their code is present
(`employees.view` for `useDashboardStats`), so a narrow role never fires 403s on
page load.

### 9.2 Roles list — `/rollar` (R2)

`PageHeader` «Rollar», subtitle «{{n}} ta rol»; button «Yangi rol» only with
`roles.manage`. One `Card` with a divided list; each row: `IconTile` (lock icon
for super_admin, shield for others), the role **name exactly as stored** (never
translated), description (one line, secondary), right side: holder count
«{{n}} kishi», a `Badge` «O‘zgarmas» on the super_admin row only, «Faqat bitta»
badge when `isSingleHolder`, chevron. Row → `/rollar/$roleId`. The list is
never empty (super_admin is always there), so no empty state is needed.

### 9.3 Role editor — `/rollar/$roleId` and `/rollar/yangi` (R2)

Layout, top to bottom:

1. **Header card**: name (`TextField` «Rol nomi» with the hint «Bir tilda
   yoziladi va barcha tillarda shunday ko‘rinadi» for every ordinary role —
   today's «Bosh rahbar» and «HR menejeri» included; plain title for
   super_admin), description (`TextArea`), the single-holder `Switch` «Faqat
   bitta faol hisob» (hint: «Yoqilsa, bu rolni bir vaqtda faqat bitta faol hisob
   oladi.»), badges, holders list (`Avatar` + name, muted when inactive; link to
   `/foydalanuvchilar?search=<login>`).
2. **Read-only banner** (`Alert`, info) with the reason when `canEdit` is false:
   protected role / own role / beyond your rights / no `roles.manage`. In
   read-only mode levels render as `Badge`s, not controls.
3. **Matrix**: one `Card` per section (§3.3 order), each module a row:
   - left: module title + one-line hint for the selected level (§9.9 tables);
   - right: `SegmentedControl` with **only the offered levels** (segment count
     varies by module; that is intended); on narrow screens it moves under the
     title, full width;
   - under `employees` and `employment`: capability rows, each a `Switch` with
     label and a secondary reason line when disabled — «O‘zgartirish darajasi
     kerak» (below `AssignableFrom`), «To‘liq darajaga kiradi» (implied, shown
     on), «Oklad o‘zgarishi uchun kerak» (`salary.view` while `employment.salary`
     is on), «Xodimlar kamida O‘zgartirish bo‘lishi kerak» (`employment.hire`).
   - Employment at Edit with no operation ticked shows an inline note «Hech
     qanday amal tanlanmagan — bu «Ko‘rish» bilan bir xil».
4. **Dependency handling** (client, `features/roles/lib/access-form.ts`, driven
   only by `GET /api/access-catalog` — no hard-coded module lists): raising a
   module raises its requirements (recursively) and shows a toast «{{module}}
   ham «{{level}}» darajasiga ko‘tarildi»; lowering a module that others need
   first asks in a `Dialog` «Bu o‘zgarish quyidagilarni ham yopadi: …» and, on
   confirm, lowers the dependents to None and unticks dependent capabilities.
5. **Save bar** (sticky bottom, `blur-bar`): «{{n}} ta o‘zgarish»,
   «Bekor qilish», «Saqlash». Saving an existing role with holders opens a
   confirmation listing reduced modules/capabilities: «Bu rolda {{n}} kishi.
   O‘zgarish ularning keyingi amalida kuchga kiradi.»
6. **Overflow actions**: «Nusxa olish» (→ `/rollar/yangi?from=<id>`, prefilled
   with that role's levels, capabilities and switch; name = source name + space
   + `t('roleEditor.copySuffix')`); «O‘chirish» (any ordinary role with
   `canDelete`, i.e. unused; danger `Dialog`). There is **no** preset or
   "recommended setup" action — Appendix A is documentation.

`/rollar/yangi` without `roles.manage` shows the standard no-access state.
Server errors show through `useErrorMessage` (translated codes).

### 9.4 Users page (R2) — `features/users/**`

- Role column: `roleName` exactly as stored (no translation). The settings
  dialog's role badge (`app/layout/settings-dialog.tsx`) shows `/me` `roleName`
  the same way.
- User form: the role `Select` lists `GET /api/roles` entries with
  `canAssign === true` (plus the row's current role, disabled, when not
  assignable); below it the selected role's description and, with `roles.view`,
  a link «Ruxsatlarini ko‘rish» → `/rollar/$roleId`. The single-holder hint uses
  `isSingleHolder`.
- A user whose role is the technical role: the select is replaced by a read-only
  field `t('users.technicalRole', { name: roleName })`; the form re-sends the
  same `roleCode`.
- Row actions depend on the server's `canManage`. Remove `ROLE_CODES`,
  `canActOnRole`, `currentRoleCode`, `LEADER_ROLE_CODES`,
  `SINGLE_HOLDER_ROLE_CODES` from `user-presentation.ts` once unused.

### 9.5 Audit journal — `/audit` (A2)

`PageHeader` «Audit jurnali», subtitle «Kim, qachon, nimani o‘zgartirdi».
Toolbar (every value in the URL search params, like `/buyruqlar`) — the owner's
"who, what, when, entity":

| Filter | Control | Values |
|---|---|---|
| «Davr» (when) | two `DateField`s | default: last 30 days |
| «Kim» (who) | `Select` | `/api/audit/actors` + «Tizim» (`actor=system`) |
| «Bo‘lim» | `Select` | modules the viewer can see |
| «Yozuv turi» (entity) | `Select`, enabled once a module is chosen | entity types of that module (§7.3), labels `audit.entities.*` |
| «Amal» (what) | `Select` | `audit.actions.*` |
| «O‘zgarishlar \| Kirish va xavfsizlik» | `SegmentedControl` | second segment only with `users.manage` |

There is **no employee filter** on this page: 2,400 people do not fit a
`Select`, and the per-employee history lives on the employee card (§9.6). An
`entityId` in the URL (set by the timeline link, §9.6) shows as a removable chip
«Bitta yozuv».

List (cards on narrow screens, `DataTable` on wide): time (Tashkent,
`formatDateTime`), actor (avatar/initials + name + the role name as it read at
that moment — the snapshot, not today's name; `System` →
«Tizim»), action/operation label, «nima» (`entityLabel` + entity type), and a
one-line summary of changed fields. Consecutive rows with the same
`correlationId` are grouped under one header (one business action). Row →
detail `Dialog`: a three-column change list «Maydon | Oldin | Keyin», masked
values as «•••» with tooltip «Ko‘rish huquqi yo‘q», then IP and a simplified
device string, then «Shu amal ichida» (related rows).

Value rendering (`features/audit/lib/audit-format.ts`): dates via `formatDate`,
instants via Tashkent time, booleans «Ha/Yo‘q», money with thousands separators
+ currency, refs by label, known enums via existing keys
(`employees.events.*`, `probation.status.*`, `employees.flags.*`), `Role`
`ModuleLevels` as per-module «Xodimlar: To‘liq → O‘zgartirish» and
`Capabilities` as «+ Oklad o‘zgarishi / − Ishdan bo‘shatish».

### 9.6 Employee card (A2)

- New tab «O‘zgarishlar tarixi» (last position) when `audit.view`: the same list
  as §9.5 limited to `/api/employees/{id}/history`, with module chips as filter.
- Timeline (`employee-timeline-tab.tsx`): each event shows «Kiritdi: {{name}}»
  when `recordedByName` is present; corrected events show «Tuzatilgan:
  {{date}}» and, with `audit.view`, a link «Tuzatishlarni ko‘rish» opening the
  audit detail filtered to that event (`entity=EmploymentEvent&entityId=…`).

### 9.7 Action gating (R2) — replace every `EMPLOYMENT_MANAGE` / manage check

| Component | Control | Required code(s) |
|---|---|---|
| `employee-overview-tab.tsx` | transfer / duty / salary / terminate / probationResolve / probationExtend actions | `employment.transfer` / `.duty` / `.salary` / `.terminate` / `.probation.resolve` / `.probation.extend` (each individually; `onAction` is no longer all-or-nothing) |
| `employment-dialogs.tsx` | salary input in transfer; extra pay in duty | `employment.salary` (hidden otherwise) |
| `probation-dialogs.tsx` | resolve: position/department fields; salary field | `employment.transfer`; `employment.salary` |
| `employee-timeline-tab.tsx` | «Yozuvni tuzatish»; its amount field | `employment.event.correct`; `employment.salary` |
| `employees-page.tsx`, `probations-page.tsx` | hire button | `employment.hire` + `employees.create` |
| `probations-page.tsx` | «Yakunlash» / «Uzaytirish» row actions | `employment.probation.resolve` / `employment.probation.extend` |
| `missing-probations.tsx` | confirm | `employment.probation.resolve` |
| `candidate-card-page.tsx` / `candidate-hire-dialog.tsx` | «Ishga olish» | `recruiting.manage` + `employment.hire` + `employees.create` |
| `employee-documents-tab.tsx` | delete | `employees.delete` |
| `employee-incentives-tab.tsx`, `incentives-page.tsx` | delete | `incentives.delete` |
| `exit-interviews-page.tsx` | delete | `feedback.delete` |
| `incidents-page.tsx` | delete | `safety.delete` |
| `vacancies-page.tsx` | delete | `recruiting.delete` |
| `candidate-files-card.tsx` | delete | `recruiting.delete` |
| `catalog-active-field.tsx` (all catalogue dialogs) | activate/deactivate | `catalog.deactivate` |

Constants live next to today's ones (`employee-presentation.ts`,
`recruiting-presentation.ts`, `catalog-kinds.ts`, `feedback-presentation.ts`,
`safety-presentation.ts`).

P1 additions: overview tab personal block, «Hujjatlar» and «Qobiliyatlar» tabs,
list/grid JSHSHIR/passport/birth-date columns, the mehnat.uz block and the
dashboard birthdays card require `employees.personal.view`; the search
placeholder becomes «F.I.Sh. yoki telefon…» without it (new key
`common.searchPlaceholderNoDocuments`: «F.I.Sh. yoki telefon…» | «Ф.И.Ш. ёки
телефон…» | «Ф.И.О. или телефон…» | «Name or phone…»). If the only visible tab
left is «Umumiy», open on it.

### 9.8 Session refresh wiring (R1) — §6 item 4.

### 9.9 i18n — exact strings (uz-Latn | uz-Cyrl | ru | en)

**Role names are data, not i18n keys.** The owner types a name in one language
(Uzbek Latin in practice); it is stored as typed and shown as typed in all four
UI languages — including «Super administrator» and the bot role's name, which
keep their stored text. Consequently: the `roles.*` keys (`super_admin`,
`main_boss`, `hr_manager`) are **deleted** from all four locale files, `'roles.'`
is removed from `SKIPPED_DYNAMIC_PREFIXES` in `check-locales.mjs`, and no
`t('roles.…')` call may remain (`users-page.tsx` `roleLabel`,
`settings-dialog.tsx`). Module, level and capability labels *are* translated —
they are code, not data.

Keys: `nav.roles`, `nav.audit`; `access.sections.<key>`;
`access.modules.<key>`; `access.levels.<none|view|edit|full>`;
`access.levelHints.<level>`; `access.moduleHints.<key>.<level>` (only where
listed); `access.capabilities.<code with "." → "_">`; `access.kind.locked`;
`roleEditor.*`; `audit.*`; `employees.tabHistory`, `employees.recordedBy`,
`employees.correctedAt`, `employees.showCorrections`; `errors.<code>`.
Add `check-locales.mjs` dynamic sources for the module, capability, level and
audit-action key families (read from the const arrays in
`features/roles/lib/access-keys.ts` and `features/audit/lib/audit-keys.ts`).

| Key | uz-Latn | uz-Cyrl | ru | en |
|---|---|---|---|---|
| nav.roles | Rollar | Роллар | Роли | Roles |
| nav.audit | Audit jurnali | Аудит журнали | Журнал аудита | Audit log |
| access.sections.people | Kadrlar | Кадрлар | Персонал | People |
| access.sections.recruiting | Rekruting | Рекрутинг | Рекрутинг | Recruiting |
| access.sections.analytics | Tahlil | Таҳлил | Аналитика | Analytics |
| access.sections.reference | Ma’lumotnomalar | Маълумотномалар | Справочники | Catalogue |
| access.sections.system | Tizim | Тизим | Система | System |
| access.modules.employees | Xodimlar | Ходимлар | Сотрудники | Employees |
| access.modules.employment | Mehnat munosabatlari | Меҳнат муносабатлари | Трудовые отношения | Employment |
| access.modules.lists | Ro‘yxatlar | Рўйхатлар | Списки | Lists |
| access.modules.incentives | Rag‘bat va jarima | Рағбат ва жарима | Поощрения и взыскания | Rewards & penalties |
| access.modules.safety | Baxtsiz hodisalar | Бахтсиз ҳодисалар | Несчастные случаи | Incidents |
| access.modules.feedback | Qayta aloqa | Қайта алоқа | Обратная связь | Exit interviews |
| access.modules.recruiting | Rekruting | Рекрутинг | Рекрутинг | Recruiting |
| access.modules.reports | Hisobotlar | Ҳисоботлар | Отчёты | Reports |
| access.modules.catalog | Ma’lumotnomalar | Маълумотномалар | Справочники | Catalogue |
| access.modules.users | Foydalanuvchilar | Фойдаланувчилар | Пользователи | Users |
| access.modules.roles | Rollar | Роллар | Роли | Roles |
| access.modules.audit | Audit jurnali | Аудит журнали | Журнал аудита | Audit log |
| access.modules.settings | Tizim sozlamalari | Тизим созламалари | Настройки системы | System settings |
| access.levels.none | Yo‘q | Йўқ | Нет доступа | No access |
| access.levels.view | Ko‘rish | Кўриш | Просмотр | View |
| access.levels.edit | O‘zgartirish | Ўзгартириш | Изменение | Edit |
| access.levels.full | To‘liq | Тўлиқ | Полный | Full |
| access.levelHints.none | Bo‘lim ko‘rinmaydi va ochilmaydi | Бўлим кўринмайди ва очилмайди | Раздел скрыт и недоступен | The section is hidden and closed |
| access.levelHints.view | Faqat ko‘radi, hech narsani o‘zgartirmaydi | Фақат кўради, ҳеч нарсани ўзгартирмайди | Только просмотр, без изменений | Can look, cannot change anything |
| access.levelHints.edit | Qo‘shadi va tahrirlaydi, o‘chira olmaydi | Қўшади ва таҳрирлайди, ўчира олмайди | Добавляет и редактирует, но не удаляет | Adds and edits, cannot delete |
| access.levelHints.full | Hammasi: o‘chirish va bo‘lim sozlamalari ham | Ҳаммаси: ўчириш ва бўлим созламалари ҳам | Всё, включая удаление и настройки раздела | Everything, including deleting and section settings |
| access.moduleHints.employment.edit | Faqat quyida belgilangan amallar | Фақат қуйида белгиланган амаллар | Только отмеченные ниже действия | Only the actions ticked below |
| access.moduleHints.employment.full | Barcha amallar, oklad va bo‘shatish ham | Барча амаллар, оклад ва бўшатиш ҳам | Все действия, включая оклад и увольнение | Every action, salary and termination included |
| access.moduleHints.lists.edit | Belgi qo‘yish va yopish | Белги қўйиш ва ёпиш | Ставить и снимать отметки | Add and close marks |
| access.moduleHints.recruiting.full | O‘chirish va voronka bosqichlari ham | Ўчириш ва воронка босқичлари ҳам | Включая удаление и этапы воронки | Including deleting and funnel stages |
| access.moduleHints.reports.full | Excelga yuklash ham | Excelга юклаш ҳам | Включая выгрузку в Excel | Including Excel export |
| access.moduleHints.catalog.full | Faolsizlantirish ham | Фаолсизлантириш ҳам | Включая деактивацию | Including deactivating entries |
| access.moduleHints.users.full | Hisob ochish, tahrirlash, parol tiklash — o‘zidan past rollar uchun | Ҳисоб очиш, таҳрирлаш, парол тиклаш — ўзидан паст роллар учун | Создание, изменение, сброс пароля — для ролей ниже своей | Create, edit, reset passwords — for roles below one’s own |
| access.moduleHints.roles.full | Rol yaratish va tahrirlash — o‘z huquqidan oshmagan holda | Рол яратиш ва таҳрирлаш — ўз ҳуқуқидан ошмаган ҳолда | Создание и изменение ролей в пределах своих прав | Create and edit roles within one’s own rights |
| access.moduleHints.audit.view | Faqat o‘zi ko‘ra oladigan bo‘limlar tarixi | Фақат ўзи кўра оладиган бўлимлар тарихи | История только доступных разделов | History of the sections one can see |
| access.capabilities.employees_personal_view | Shaxsiy ma’lumotlar | Шахсий маълумотлар | Личные данные | Personal details |
| access.capabilities.salary_view | Okladni ko‘rish | Окладни кўриш | Просмотр оклада | See salary |
| access.capabilities.employment_hire | Ishga qabul | Ишга қабул | Приём на работу | Hire |
| access.capabilities.employment_transfer | Ko‘chirish (lavozim, bo‘lim) | Кўчириш (лавозим, бўлим) | Перевод (должность, отдел) | Transfer (position, department) |
| access.capabilities.employment_duty | Qo‘shimcha vazifa | Қўшимча вазифа | Дополнительная обязанность | Additional duty |
| access.capabilities.employment_salary | Oklad o‘zgarishi | Оклад ўзгариши | Изменение оклада | Salary change |
| access.capabilities.employment_terminate | Ishdan bo‘shatish | Ишдан бўшатиш | Увольнение | Termination |
| access.capabilities.employment_probation_extend | Sinov muddatini uzaytirish | Синов муддатини узайтириш | Продление испытательного срока | Extend probation |
| access.capabilities.employment_probation_resolve | Sinov muddatini yakunlash | Синов муддатини якунлаш | Завершение испытательного срока | Complete probation |
| access.capabilities.employment_event_correct | Yozuvni tuzatish | Ёзувни тузатиш | Исправление записи | Correct a record |
| access.kind.locked | O‘zgarmas | Ўзгармас | Неизменяемая | Locked |
| roleEditor.newRole | Yangi rol | Янги рол | Новая роль | New role |
| roleEditor.subtitle | {{n}} ta rol | {{n}} та рол | Ролей: {{n}} | {{n}} roles |
| roleEditor.holders | {{n}} kishi | {{n}} киши | Человек: {{n}} | {{n}} people |
| roleEditor.name | Rol nomi | Рол номи | Название роли | Role name |
| roleEditor.nameHint | Bir tilda yoziladi va barcha tillarda shunday ko‘rinadi | Бир тилда ёзилади ва барча тилларда шундай кўринади | Пишется на одном языке и так же показывается во всех языках | Written in one language and shown as written in every language |
| roleEditor.singleHolder | Faqat bitta faol hisob | Фақат битта фаол ҳисоб | Только один активный пользователь | Only one active holder |
| roleEditor.singleHolderHint | Yoqilsa, bu rolni bir vaqtda faqat bitta faol hisob oladi. | Ёқилса, бу ролни бир вақтда фақат битта фаол ҳисоб олади. | Если включено, роль может быть только у одного активного пользователя. | When on, only one active account can hold this role at a time. |
| roleEditor.singleHolderBadge | Faqat bitta | Фақат битта | Только один | Single |
| roleEditor.duplicate | Nusxa olish | Нусха олиш | Копировать | Duplicate |
| roleEditor.readOnlyLocked | Bu rol o‘zgarmaydi: u tizim egasiga tegishli va har doim barcha huquqlarga ega. | Бу рол ўзгармайди: у тизим эгасига тегишли ва ҳар доим барча ҳуқуқларга эга. | Эта роль не меняется: она принадлежит владельцу системы и всегда имеет все права. | This role cannot change: it belongs to the system owner and always has every right. |
| roleEditor.readOnlyOwn | O‘z rolingizni tahrirlay olmaysiz. | Ўз ролингизни таҳрирлай олмайсиз. | Нельзя изменять собственную роль. | You cannot edit your own role. |
| roleEditor.readOnlyAbove | Bu rol sizning huquqingizdan keng yoki unga teng. | Бу рол сизнинг ҳуқуқингиздан кенг ёки унга тенг. | Эта роль шире ваших прав или равна им. | This role is equal to or wider than your own rights. |
| roleEditor.saveConfirm | Bu rolda {{n}} kishi. O‘zgarish ularning keyingi amalida kuchga kiradi. | Бу ролда {{n}} киши. Ўзгариш уларнинг кейинги амалида кучга киради. | У роли {{n}} чел. Изменение вступит в силу при их следующем действии. | {{n}} people hold this role. The change applies on their next action. |
| roleEditor.readOnlyNoRight | Rollarni tahrirlash huquqingiz yo‘q. | Ролларни таҳрирлаш ҳуқуқингиз йўқ. | У вас нет права изменять роли. | You are not allowed to edit roles. |
| roleEditor.changes | {{n}} ta o‘zgarish | {{n}} та ўзгариш | Изменений: {{n}} | {{n}} changes |
| roleEditor.raisedToast | {{module}} ham «{{level}}» darajasiga ko‘tarildi | {{module}} ҳам «{{level}}» даражасига кўтарилди | Раздел «{{module}}» тоже повышен до «{{level}}» | {{module}} was raised to “{{level}}” too |
| roleEditor.lowerConfirm | Bu o‘zgarish quyidagilarni ham yopadi: {{list}} | Бу ўзгариш қуйидагиларни ҳам ёпади: {{list}} | Это изменение закроет также: {{list}} | This also closes: {{list}} |
| roleEditor.noActions | Hech qanday amal tanlanmagan — bu «Ko‘rish» bilan bir xil | Ҳеч қандай амал танланмаган — бу «Кўриш» билан бир хил | Не выбрано ни одного действия — это то же, что «Просмотр» | No action ticked — this equals “View” |
| roleEditor.needsEdit | O‘zgartirish darajasi kerak | Ўзгартириш даражаси керак | Нужен уровень «Изменение» | Needs the “Edit” level |
| roleEditor.impliedByFull | To‘liq darajaga kiradi | Тўлиқ даражага киради | Входит в уровень «Полный» | Included in “Full” |
| roleEditor.requiredBySalary | Oklad o‘zgarishi uchun kerak | Оклад ўзгариши учун керак | Нужно для изменения оклада | Needed for salary change |
| roleEditor.hireNeedsEmployeesEdit | Xodimlar kamida «O‘zgartirish» bo‘lishi kerak | Ходимлар камида «Ўзгартириш» бўлиши керак | «Сотрудники» — не ниже уровня «Изменение» | Employees must be at least “Edit” |
| roleEditor.copySuffix | (nusxa) | (нусха) | (копия) | (copy) |
| roleEditor.deleteTitle | Rolni o‘chirish | Ролни ўчириш | Удалить роль | Delete role |
| users.technicalRole | {{name}} (texnik rol) | {{name}} (техник рол) | {{name}} (техническая роль) | {{name}} (technical role) |
| users.viewRolePermissions | Ruxsatlarini ko‘rish | Рухсатларини кўриш | Посмотреть права | View permissions |
| audit.title | Audit jurnali | Аудит журнали | Журнал аудита | Audit log |
| audit.subtitle | Kim, qachon, nimani o‘zgartirdi | Ким, қачон, нимани ўзгартирди | Кто, когда и что изменил | Who changed what, and when |
| audit.system | Tizim | Тизим | Система | System |
| audit.masked | Ko‘rish huquqi yo‘q | Кўриш ҳуқуқи йўқ | Нет права просмотра | No permission to view |
| audit.before | Oldin | Олдин | Было | Before |
| audit.after | Keyin | Кейин | Стало | After |
| audit.related | Shu amal ichida | Шу амал ичида | В рамках того же действия | Part of the same action |
| audit.kindChanges | O‘zgarishlar | Ўзгаришлар | Изменения | Changes |
| audit.kindSecurity | Kirish va xavfsizlik | Кириш ва хавфсизлик | Входы и безопасность | Sign-ins & security |
| audit.actions.create | Yaratildi | Яратилди | Создано | Created |
| audit.actions.update | O‘zgartirildi | Ўзгартирилди | Изменено | Changed |
| audit.actions.delete | O‘chirildi | Ўчирилди | Удалено | Deleted |
| audit.actions.login | Tizimga kirdi | Тизимга кирди | Вход в систему | Signed in |
| audit.actions.loginFailed | Kirish muvaffaqiyatsiz | Кириш муваффақиятсиз | Неудачный вход | Failed sign-in |
| audit.actions.logout | Tizimdan chiqdi | Тизимдан чиқди | Выход из системы | Signed out |
| audit.actions.export | Excelga yuklandi | Excelга юкланди | Выгрузка в Excel | Exported to Excel |
| audit.actions.permissionDenied | Ruxsatsiz urinish | Рухсатсиз уриниш | Попытка без права | Denied attempt |
| audit.actions.tokenReuse | Sessiya o‘g‘irlanishi shubhasi | Сессия ўғирланиши шубҳаси | Подозрение на кражу сессии | Suspected session theft |
| audit.filterPeriod | Davr | Давр | Период | Period |
| audit.filterActor | Kim | Ким | Кто | Who |
| audit.filterModule | Bo‘lim | Бўлим | Раздел | Section |
| audit.filterEntity | Yozuv turi | Ёзув тури | Тип записи | Record type |
| audit.filterAction | Amal | Амал | Действие | Action |
| audit.singleRecord | Bitta yozuv | Битта ёзув | Одна запись | One record |
| audit.field | Maydon | Майдон | Поле | Field |
| audit.ip | IP manzil | IP манзил | IP-адрес | IP address |
| audit.device | Qurilma | Қурилма | Устройство | Device |
| audit.emptyTitle | Bu davrda o‘zgarish yo‘q | Бу даврда ўзгариш йўқ | За этот период изменений нет | No changes in this period |
| employees.tabHistory | O‘zgarishlar tarixi | Ўзгаришлар тарихи | История изменений | Change history |
| employees.recordedBy | Kiritdi: {{name}} | Киритди: {{name}} | Внёс: {{name}} | Recorded by {{name}} |
| employees.correctedAt | Tuzatilgan: {{date}} | Тузатилган: {{date}} | Исправлено: {{date}} | Corrected: {{date}} |
| employees.showCorrections | Tuzatishlarni ko‘rish | Тузатишларни кўриш | Показать исправления | Show corrections |
| errors.rol.topilmadi | Rol topilmadi. | Рол топилмади. | Роль не найдена. | Role not found. |
| errors.rol.nom_band | Bu nomdagi rol allaqachon bor. | Бу номдаги рол аллақачон бор. | Роль с таким названием уже есть. | A role with this name already exists. |
| errors.rol.nom_uzun | Rol nomi 60 belgidan oshmasin. | Рол номи 60 белгидан ошмасин. | Название роли — не длиннее 60 символов. | The role name must be at most 60 characters. |
| errors.rol.tavsif_uzun | Tavsif 300 belgidan oshmasin. | Тавсиф 300 белгидан ошмасин. | Описание — не длиннее 300 символов. | The description must be at most 300 characters. |
| errors.rol.ozgarmas | Super administrator roli o‘zgartirilmaydi. | Супер администратор роли ўзгартирилмайди. | Роль суперадминистратора не изменяется. | The super administrator role cannot be changed. |
| errors.rol.bir_nechta_egasi | Bu rolda allaqachon bir nechta faol hisob bor — avval ortiqchalarini boshqa rolga o‘tkazing. | Бу ролда аллақачон бир нечта фаол ҳисоб бор — аввал ортиқчаларини бошқа ролга ўтказинг. | У роли уже несколько активных пользователей — сначала смените роль лишним. | This role already has several active holders — move the extra ones to another role first. |
| errors.foydalanuvchi.rol_band | Bu rolni faqat bitta faol hisob oladi va u band. | Бу ролни фақат битта фаол ҳисоб олади ва у банд. | Эту роль может иметь только один активный пользователь, и она уже занята. | Only one active account can hold this role, and it is taken. |
| errors.rol.oz_roli | O‘z rolingizni tahrirlay olmaysiz. | Ўз ролингизни таҳрирлай олмайсиз. | Нельзя изменять собственную роль. | You cannot edit your own role. |
| errors.rol.huquqdan_oshadi | O‘zingizda yo‘q yoki o‘zingiznikiga teng huquqni bera olmaysiz. | Ўзингизда йўқ ёки ўзингизникига тенг ҳуқуқни бера олмайсиз. | Нельзя выдать права шире своих или равные им. | You cannot grant rights equal to or beyond your own. |
| errors.rol.foydalanishda | Bu rol foydalanuvchilarga berilgan — avval ularga boshqa rol tanlang. | Бу рол фойдаланувчиларга берилган — аввал уларга бошқа рол танланг. | Роль назначена пользователям — сначала смените им роль. | Users hold this role — give them another role first. |
| errors.rol.notanish_bolim | Bunday bo‘lim yo‘q. | Бундай бўлим йўқ. | Такого раздела нет. | Unknown section. |
| errors.rol.bolim_takror | Bitta bo‘lim ikki marta ko‘rsatilgan. | Битта бўлим икки марта кўрсатилган. | Раздел указан дважды. | A section is listed twice. |
| errors.rol.daraja_yoq | Bu bo‘limda bunday daraja yo‘q. | Бу бўлимда бундай даража йўқ. | В этом разделе нет такого уровня. | This section does not offer that level. |
| errors.rol.imkoniyat_daraja | Bu amal uchun bo‘lim darajasi yetarli emas. | Бу амал учун бўлим даражаси етарли эмас. | Уровень раздела недостаточен для этого действия. | The section level is too low for this action. |
| errors.rol.bogliqlik | Bu bo‘lim boshqa bo‘limsiz ishlamaydi — uning darajasini ham ko‘taring. | Бу бўлим бошқа бўлимсиз ишламайди — унинг даражасини ҳам кўтаринг. | Раздел не работает без другого — повысьте и его уровень. | This section needs another one — raise its level too. |
| errors.mehnat.oklad_huquqi_yoq | Oklad bilan bog‘liq qiymatni kiritish huquqingiz yo‘q. | Оклад билан боғлиқ қийматни киритиш ҳуқуқингиз йўқ. | У вас нет права указывать оклад. | You are not allowed to set salary amounts. |
| errors.mehnat.kochirish_huquqi_yoq | Lavozim yoki bo‘limni o‘zgartirish huquqingiz yo‘q. | Лавозим ёки бўлимни ўзгартириш ҳуқуқингиз йўқ. | У вас нет права менять должность или отдел. | You are not allowed to change position or department. |
| errors.audit.topilmadi | Yozuv topilmadi. | Ёзув топилмади. | Запись не найдена. | Entry not found. |
| errors.audit.davr_notogri | Boshlanish sanasi tugash sanasidan keyin bo‘lmasin. | Бошланиш санаси тугаш санасидан кейин бўлмасин. | Начало периода позже конца. | The start date is after the end date. |

Entity type labels (`audit.entities.<EntityName>`; used by «Yozuv turi» and the
list's «nima» column):

| Entity | uz-Latn | uz-Cyrl | ru | en |
|---|---|---|---|---|
| User | Foydalanuvchi | Фойдаланувчи | Пользователь | User |
| Role | Rol | Рол | Роль | Role |
| LegalEntity | Yuridik shaxs | Юридик шахс | Юридическое лицо | Legal entity |
| Direction | Yo‘nalish | Йўналиш | Направление | Direction |
| TerminationReason | Ishdan ketish sababi | Ишдан кетиш сабаби | Причина увольнения | Termination reason |
| AwardType | Rag‘bat turi | Рағбат тури | Вид поощрения | Reward type |
| PenaltyType | Jarima turi | Жарима тури | Вид взыскания | Penalty type |
| DocumentType | Hujjat turi | Ҳужжат тури | Тип документа | Document type |
| VacancyTitle | Vakansiya nomi | Вакансия номи | Название вакансии | Vacancy title |
| PipelineStage | Voronka bosqichi | Воронка босқичи | Этап воронки | Funnel stage |
| Department | Bo‘lim | Бўлим | Отдел | Department |
| Position | Lavozim | Лавозим | Должность | Position |
| PositionDuty | Lavozim vazifasi | Лавозим вазифаси | Обязанность должности | Position duty |
| Employee | Xodim kartasi | Ходим картаси | Карточка сотрудника | Employee card |
| Employment | Mehnat munosabati | Меҳнат муносабати | Трудовое отношение | Employment |
| EmploymentEvent | Buyruq | Буйруқ | Приказ | Order |
| Probation | Sinov muddati | Синов муддати | Испытательный срок | Probation |
| EmployeeFlag | Belgi | Белги | Отметка | Mark |
| EmployeeCompetency | Qobiliyatlar | Қобилиятлар | Навыки | Skills |
| EmployeeCompetencyLanguage | Til bilish | Тил билиш | Знание языка | Language skill |
| Incentive | Rag‘bat yoki jarima | Рағбат ёки жарима | Поощрение или взыскание | Reward or penalty |
| EmployeeDocument | Hujjat | Ҳужжат | Документ | Document |
| EmployeeCheckFile | Tekshiruv materiali | Текширув материали | Материал проверки | Check material |
| ExitInterview | Qayta aloqa suhbati | Қайта алоқа суҳбати | Выходное интервью | Exit interview |
| Incident | Baxtsiz hodisa | Бахтсиз ҳодиса | Несчастный случай | Incident |
| Vacancy | Vakansiya | Вакансия | Вакансия | Vacancy |
| Candidate | Nomzod | Номзод | Кандидат | Candidate |
| CandidateStageEntry | Bosqich yozuvi | Босқич ёзуви | Запись этапа | Stage entry |
| CandidateNote | Nomzod izohi | Номзод изоҳи | Заметка о кандидате | Candidate note |
| CandidateFile | Nomzod fayli | Номзод файли | Файл кандидата | Candidate file |
| CandidateQuestionnaire | Anketa | Анкета | Анкета | Questionnaire |
| SystemSetting | Tizim sozlamasi | Тизим созламаси | Системная настройка | System setting |
| ApiKey | Tashqi API kaliti | Ташқи API калити | Ключ внешнего API | External API key |

Audit field and operation labels **reuse existing keys** — no new translations:
field → key map in `features/audit/lib/audit-keys.ts` (e.g. `DepartmentId` →
`employee.department`, `PositionId` → `employee.position`, `Salary` →
`employee.salary`, `OrderNo` → `employee.orderNo`, `OrderDate` →
`employee.orderDate`, `Pinfl` → `employee.pinfl`, `Passport` →
`employee.passport`, `Phone` → `employee.phone`, `Address` → `employee.address`,
`BirthDate` → `employee.birthDate`, `LegalEntityId` → `employee.legalEntity`,
`Status` → `employee.status`); operation → key map (`HireEmployee` →
`employees.hire`, `TransferEmployee` → `employees.transfer`,
`AddAdditionalDuty` → `employees.dutyTitle`, `ChangeSalary` →
`employees.salaryTitle`, `EditEmploymentEvent` → `employees.eventEditTitle`,
`TerminateEmployment` → `employees.terminateTitle`, `ResolveProbation` →
`probation.resolveTitle`, `ExtendProbation` → `probation.extendTitle`,
`UpdateEmployee` → `employees.editTitle`, `CreateUser` → `users.createTitle`,
`UpdateUser` → `users.editTitle`, `DeactivateUser` → `users.deactivate`,
`ActivateUser` → `users.activate`, `ResetUserPassword` →
`users.resetPasswordTitle`, `HireCandidate` → `recruiting.hireTitle`).
Unmapped fields show the raw property name in secondary style; unmapped
operations fall back to `audit.actions.<action>`.

---

## 10. FUTURE — WebAuthn step-up («Face ID») — design sketch only

**Not scheduled. No code in any phase of §11.** Recorded so the scheduled phases
leave room for it.

- **Goal**: a stolen password alone cannot perform critical actions: salary
  change, termination, transfer, probation completion, role create/edit/delete,
  role assignment (`CreateUser`, `UpdateUser` with a changed role), password
  reset.
- **Library**: `Fido2` + `Fido2.AspNet` 4.0.1 (MIT, passwordless-lib), backend.
  Browser: native `navigator.credentials` (no extra dependency needed). RP ID
  `hr.bonvi.uz`; `localhost` in development (WebAuthn needs a secure context —
  production already serves HTTPS). The API image is Ubuntu noble; Fido2's native
  dependency (NSec/libsodium) must be smoke-tested in that image first.
- **Data**: `webauthn_credentials` (id, user_id FK, credential_id bytea unique,
  public_key bytea, sign_count bigint, aaguid uuid, transports text[],
  device_name text, created_at, last_used_at, revoked_at, revoked_by) and
  `step_up_grants` (id, user_id, action_code, target_id uuid null, token_hash
  bytea, expires_at, used_at). Both audited; hashes masked.
- **Registration** (`/api/me/passkeys`): options → `navigator.credentials.create`
  (user verification required, resident key preferred) → verify → store.
  Registering requires re-entering the password in the same dialog, so a stolen
  session alone cannot enrol a new device. Users list their devices in the
  personal settings dialog and can revoke them.
- **Step-up flow**: endpoints get `.RequireStepUp(<action code>)` (an endpoint
  filter). Without a valid grant → **428** `{ code: "stepup.talab", action }`.
  Client: `POST /api/me/step-up/options {action, targetId}` →
  `navigator.credentials.get` (Face ID / Touch ID / Windows Hello / phone) →
  `POST /api/me/step-up` → server verifies the assertion and issues a random
  256-bit token (stored hashed) bound to **user + action + target**, valid **5
  minutes**, **single use** → client retries the original request with header
  `X-Step-Up`. Challenges live in `IDistributedCache` (Redis) for 5 minutes.
- **Cross-device**: the browser's hybrid transport (QR on the desktop, phone
  confirms over Bluetooth proximity) needs no server code; allow `hybrid` in
  transports.
- **Recovery**: super_admin revokes a user's passkeys from the Users page; the
  user enrols again (password re-entry). super_admin's own recovery: a
  `./bonvi` command that revokes their credentials on the server (needs shell
  access, deliberately). Ten one-time recovery codes at super_admin enrolment.
- **Rollout**: a system setting per action (`stepup.<action>` off/on), piloted
  on salary change first; audit actions `StepUp = 10`, `StepUpFailed = 11`.
- **Estimate when scheduled**: 5–6 days including device testing (iPhone Safari,
  Android Chrome, Windows Hello, desktop + phone QR).

---

## 11. Phases, units, parallelism, estimates

Each phase is independently deployable and leaves production working. Order:
**A1 → R1 → R2 → A2 → P1**, with **R3** (the owner's own role work) possible at
any moment after R2 and blocking nothing. Rationale: capture history before
anything else changes (so every role the owner creates or edits is recorded from
its first minute); rights next; the audit UI reads data that is already
accumulating; P1 last because nobody lacks personal data until a narrow role is
built.

| Phase | Delivers | Migration | Visible to users | Estimate |
|---|---|---|---|---|
| **A1 — Audit capture** | §5.3, §5.4, §7.1–7.9: interceptor, catalog, serializer, ports, security events, immutability trigger, `RecordedBy`, removal of manual user-command records, ETL/startup scopes | `AuditJurnaliKengaytirildi` | no | 3 days |
| **R1 — Access model** | §3 (codes, catalog, expansion, endpoint rebinding, money rules), §4 (protected vs ordinary roles, dominance, single-holder switch, removal of every `main_boss`/`hr_manager` rule — §4.6), §5.1–5.2, §5.5–5.7, §6 (incl. frontend refresh wiring), roles API §8.1, users/me DTOs §8.2 | `RollarMuharriri` | no (sessions refresh once; everyone keeps today's rights) | 4.5 days |
| **R2 — Role editor UI** | §9.1–9.4, §9.7, i18n for them (incl. deleting `roles.*`), manual section «Rollar va ruxsatlar» with Appendix A as a worked example | none | yes | 3 days |
| **R3 — Owner defines roles in the UI** | the owner creates, renames, edits and assigns roles himself whenever he chooses — e.g. the HR head split of Appendix A. No deploy, no restart, no developer work | none | yes, per change (§6.1) | owner's time only |
| **A2 — Audit UI** | §7.10, §8.3, §9.5, §9.6, i18n, manual section «Audit jurnali» | none | yes | 3 days |
| **P1 — Personal data** | §3.10, §9.7 P1 part | none | no (all current roles keep personal data) | 1.5 days |
| **F — WebAuthn** | §10 | yes | — | 5–6 days, not scheduled |

Deploy overhead (backup, prod-copy rehearsal, deploy, watch): ≈0.5 day per
deployed phase (A1, R1, R2, A2, P1). **Total scheduled ≈ 17.5 working days**
(unchanged by the owner-defined-roles revision: removing the seeded HR head and
the startup reconciliation roughly offsets the single-holder switch and the
generic replacement rules; R3 no longer costs developer time).

### 11.1 Sequential shared files — one author at a time, never in parallel

| File | Why |
|---|---|
| `api/src/Bonvi.Infrastructure/Persistence/Migrations/*` + `AppDbContextModelSnapshot.cs` | one snapshot; parallel migrations conflict irreconcilably |
| `api/src/Bonvi.Domain/Identity/Permissions.cs`, `SystemRoles.cs`, `AccessCatalog.cs` | every backend unit reads them |
| `api/src/Bonvi.Infrastructure/Persistence/AppDbContext.cs`, `Infrastructure/DependencyInjection.cs`, `Application/Common/DependencyInjection.cs` | one DI list each |
| `api/src/Bonvi.Api/Program.cs`, `Api/Extensions/AuthenticationExtensions.cs` | pipeline and schemes |
| `api/tests/Bonvi.Api.IntegrationTests/Rbac/endpoint-permissions.txt` | one snapshot |
| `web/src/shared/i18n/locales/*.json` (4) + `web/scripts/check-locales.mjs` | one author, or keys collide |
| `web/src/app/layout/nav-items.ts`, `sidebar.tsx` | shared |
| `web/src/shared/api/generated/**` | regenerated by `./bonvi gen`, never hand-edited |
| `web/src/app/routeTree.gen.ts` | generated by the router plugin |
| `web/manual/*.md`, `PLAN.md`, `docs/ASSUMPTIONS.md` | shared docs |

### 11.2 Units

| Unit | Touches | Depends on | Parallel? |
|---|---|---|---|
| **A1-S** schema | `AuditLog.cs`, `AuditAction`, `AuditActorKind`, `AccessModuleKeys.cs`, `AuditLogConfiguration.cs`, migration A1 | — | No — first, alone |
| **A1-C** capture | `AuditTrailInterceptor.cs`, `Auditing/AuditCatalog.cs`, `AuditValueSerializer.cs`, `IRequestContext`/`IAuditScope`/`IAuditActorSource` (+ Api and system implementations), `AuditableInterceptor.cs` (RecordedBy), `DatabaseInitializer.cs`, `EtlDatabase.cs`, DI files | A1-S | Yes, with A1-E |
| **A1-E** security events | `IAuditLogger.cs`, `AuditLogger.cs`, Login/Logout/Refresh handlers, `ExportEmployeesQueryHandler`, `OnForbidden` in `AuthenticationExtensions.cs`, removal of the five manual calls + their unit-test updates | A1-S | Yes, with A1-C (DI edits belong to A1-C) |
| **A1-T** tests | §12.1 audit tests | A1-C, A1-E | No |
| **R1-D** domain | `Permissions.cs`, `SystemRoles.cs`, `AccessLevel.cs`, `AccessCatalog.cs`, `Role.cs`, `User.cs` | A1 merged | No — first, alone |
| **R1-M** persistence | `RoleConfiguration.cs`, `UserConfiguration.cs`, migration R1, `IdentitySeeder.cs`, `UserRepository.GetPermissionsAsync` (expand), `JwtTokenService.cs` (`ast`), `AccessStampValidator.cs` | R1-D | Yes, with R1-E |
| **R1-E** endpoints and money rules | the endpoint files in §3.8, employment/probation handlers, `EmployeeErrors`/`EmploymentErrors` | R1-D | Yes, with R1-M |
| **R1-R** roles API + user rules | `Application/Identity/Roles/**`, `RoleEndpoints.cs`, `Program.cs`, `UserRoleRules.cs`, `UserErrors.cs`, `Create/Update/Activate/DeactivateUserCommand.cs` (single-holder switch, `users.manage` rule), `UserSummaryDtoFactory.cs`, `UserDtos.cs`, `AuthDtos.cs` + `GetCurrentUserQuery` + login/refresh handlers (`RoleName`), `UserEndpoints.cs` descriptions | R1-M | No |
| **R1-A** freshness | `AuthenticationExtensions.cs` (`OnTokenValidated`), `NotificationEndpoints.cs` (SSE), `http-client.ts`, `auth-context.tsx` | R1-M | Yes, with R1-R (different files) |
| **R1-W** error translations | `errors.rol.*`, `errors.mehnat.*` and `errors.foydalanuvchi.rol_band` rows of §9.9 in the four locale files (`./bonvi check` fails on any untranslated backend code, so R1 cannot wait for R2) | R1-E, R1-R | No — single author |
| **R1-T** tests | §12.1–12.3 R1 tests, `endpoint-permissions.txt` | R1-E, R1-R, R1-A | No |
| **R2-F1** nav | `nav-items.ts`, `sidebar.tsx`, dashboard query gating | R1 merged, `./bonvi gen` | Yes |
| **R2-F2** roles feature | `features/roles/**`, routes `rollar/index.tsx`, `rollar/$roleId.tsx`, `rollar/yangi.tsx` | same | Yes |
| **R2-F3** users page and role names | `features/users/**`, `app/layout/settings-dialog.tsx`, `features/auth/api/auth-api.ts` (comment) | same | Yes |
| **R2-F4** action gating | files in §9.7 | same | Yes |
| **R2-W** wiring | locales ×4 (new keys; delete `roles.*`), `check-locales.mjs` (drop `'roles.'` from the skip list, add dynamic sources), manual | F1–F4 | No — single author, last |
| **A2-B** audit read side | `Application/Audit/**`, `AuditReadModel.cs`, `AuditEndpoints.cs`, `Program.cs`, DTO additions for `RecordedByName` | R2 merged | Yes, with A2-F after `./bonvi gen` |
| **A2-F** audit UI | `features/audit/**`, route `audit.tsx`, employee card tab, timeline markers | A2-B contract (§8.3) | Yes |
| **A2-W / A2-T** | locales, manual / tests | A2-B, A2-F | No |
| **P1-B / P1-F / P1-T** | mappers, read models, export, search, notifications, endpoint policies / frontend gating / leak tests | A2 merged | B and F in parallel; T last |

Frontend units may start from this SPEC before the backend is running (the
contracts in §8 are exact), but `./bonvi gen` must run against the finished
backend before merging.

---

## 12. Test plan

### 12.1 `test-backend`

**Domain / unit**

- `AccessCatalogTests`: every code in `Permissions.All` (minus aliases) is placed
  exactly once; expansion per module and level (table-driven from §3.3);
  capability implied/assignable rules; `employment.salary` auto-adds
  `salary.view`; alias emitted iff an employment operation is present; every
  dependency of §3.5 rejected when violated; normalisation drops implied
  capabilities and sorts; dominance strict/non-strict; the starter definitions
  of §4.2 pass validation (bot exempt); super_admin's computed codes equal
  `Permissions.All` minus the alias.
- `RoleTests` rewritten: every mutator on a protected role fails with
  `rol.ozgarmas`; ordinary roles (starter ones included) can be renamed,
  re-described, re-leveled and have the switch toggled; `CreateCustom` code
  format; mirror written on `SetAccess`; `Recode` unchanged. Old tests are
  **replaced**, each with a named successor: `Grant/Revoke` tests →
  `SetAccess` tests; `Rahbariyat_rolidan_ruxsat_olib_tashlanmaydi` →
  `Super_admin_roli_ozgarmas`; the tests that read `SystemRoles.Defaults`
  (`Uchta_odam_va_bitta_xizmat_roli_mavjud`,
  `Bosh_rahbarda_faqat_tizim_sozlamalari_yoq`,
  `Super_administrator_barcha_ruxsatlarga_ega`,
  `Atigi_bitta_qoidasi_rahbariyat_rollariga_tegishli`,
  `HR_menejeri_foydalanuvchilarni_boshqara_olmaydi`,
  `Bosh_rahbar_foydalanuvchilarni_boshqara_oladi`) are re-pointed at
  `SystemRoles.StarterRoles` / `Protected` and keep asserting the same facts.
  Do not delete a test without its successor.
- `UserTests`: stamp bumps on role change / deactivate / activate /
  `ResetPassword`; no bump on `ChangePassword` or on setting the same role.
- `UserRoleRules` tests for §4.5 (starter «Bosh rahbar» ↔ «HR menejeri», custom
  roles, a renamed role — identical behaviour, the bot, super_admin, null role;
  the single-holder switch; the `users.manage` last-manager rule). Application
  handler fixtures build roles from `SystemRoles.StarterRoles` instead of
  `Role.Create(code, …, isSystem: true)`; their assertions do not change.
- Handler tests for §3.9 money and appointment rules: denied without the code
  (and nothing mutated), allowed with it; hire with salary allowed without
  `employment.salary`.

**Integration — RBAC (the ones that matter)**

- **Endpoint inventory** (`Rbac/EndpointInventoryTests.cs`): enumerate
  `EndpointDataSource`, render `METHOD pattern -> policies` sorted, compare with
  `Rbac/endpoint-permissions.txt`. Any new or changed endpoint fails until the
  file is updated in the same PR. No `PUT/PATCH/DELETE` under `/api/audit`.
- **Module × level matrix** (`Rbac/ModuleLevelAccessTests.cs`): for every
  module, every offered level, and for `employment` every capability alone,
  mint a token (`ITokenService.CreateTokens(admin, [], Expand(...))`). Call every
  inventory endpoint **whose policies include at least one code of that module**
  (route ids = `Guid.Empty`, body = `{}`), plus one pass of **all**
  permission-bound endpoints with an empty-permission token. Assert:
  - required codes ⊆ token codes → the response is **not** a policy denial;
  - otherwise → **403 whose problem `title` is `auth.ruxsat_yoq`** (the
    `OnForbidden` body).
  A policy denial is identified by that title, not by the status alone:
  handlers legitimately return 403 with their own codes
  (`foydalanuvchi.rolga_haqqi_yoq`, `rol.*`, `mehnat.*`) and those count as
  "authorized by policy". The fixture overrides `RateLimiting:Api:PermitLimit`
  and `RateLimiting:Export:PermitLimit` to 1000000 (per-endpoint rate policies
  stay active in `Testing`). Bot routes and §3.9 rules are tested separately.
- Candidate hire needs all three codes; the bot token cannot reach stage
  configuration, deletes or candidate hire, and can still register a candidate
  and upload its photo.
- Roles API: CRUD happy paths; every guard rule G1–G13 with its exact code;
  technical role hidden (list + 404); the starter «HR menejeri» can be renamed
  and, once unused, deleted (204); «Bosh rahbar» can be renamed and re-leveled
  by super_admin only; switch on with two active holders → 409
  `rol.bir_nechta_egasi`; `canAssign/canEdit/canDelete` correct for super_admin,
  «Bosh rahbar», «HR menejeri».
- Users: «Bosh rahbar» can assign «HR menejeri» and a custom role it strictly
  dominates (the test builds an HR-head-like role per Appendix A through the
  API), not «Bosh rahbar», super_admin or the bot role; a second active holder of
  «Bosh rahbar» → 409 `foydalanuvchi.rol_band` (this replaces the two
  `bosh_rahbar_mavjud` assertions in `UserManagementApiTests`); the two
  self-deactivation tests keep their codes (§4.5); `canManage` per row.
- **Freshness** (§6.1, one test per row): role created → it appears in
  `GET /api/roles` on the next call; renamed → the holder's next request 401 →
  refresh → `/me` shows the new `roleName`; levels/actions/switch edited → the
  holder's next request 401 → refresh returns the new permissions → retried
  request succeeds or 403 as expected; deleted → gone from the list; user
  re-assigned → that user's next request 401; deactivate → next request 401 and
  refresh 403; password reset → 401; token without `ast` → 401; SSE stream ends
  within one heartbeat after a stamp bump. None of these needs a restart.
- Seeder (replaces the two reconciliation tests of fact 3): empty database →
  exactly the four starter rows; non-empty database → **no** write to `roles`
  across a restart — a renamed, re-leveled or deleted starter role stays exactly
  as left (no startup audit row for `Role`); tampered stored columns of
  super_admin do not change its computed rights; a missing protected role is
  logged, not recreated.

**Integration — audit**

- Insert, update, soft delete, hard delete each produce exactly one row with the
  right action, old/new values and `changed_fields`; an update touching only
  excluded fields (a login) produces none.
- A transfer produces two rows sharing `correlation_id` and
  `operation = TransferEmployee`; an event correction keeps the pre-correction
  `OrderNo`/`EffectiveDate`/`ToSalary` in `old_values`.
- `subject_employee_id` correct for every employee-related entity of §7.3;
  `entity_label` = the employee's name at that moment.
- Masking: `PasswordHash` never appears (search the raw row text for the hash);
  a secret `SystemSetting` value never appears; `ApiKey.KeyHash` never appears.
- A failed `SaveChanges` (unique violation) leaves no audit row and no tracked
  `AuditLog` entity; the next successful save writes only its own rows.
- `EmploymentEvent.RecordedBy` = the acting user.
- Security events: login success/failure (unknown login → `Anonymous`), logout,
  token reuse, policy 403, export — one row each, no password, no query string.
- **Immutability**: modifying or removing a loaded `AuditLog` through EF throws;
  raw `UPDATE`, `DELETE` and `TRUNCATE` on `audit_logs` fail with the trigger
  message (`NpgsqlCommand` in the test).
- `AuditCatalogCoverageTests` (§7.3).
- A2: visibility per module; salary/personal masking; `security` kind only with
  `users.manage`; filters, date defaults, paging (`total` equals rows across
  pages); employee history; `audit.topilmadi` for an invisible id.
- P1: a View-only token without `employees.personal.view` sees no JSHSHIR,
  passport, birth date or address in any employee endpoint, export or search —
  assert on raw JSON like `PublicApiLeakTests`; searching by a known JSHSHIR
  returns nothing; HR tokens unchanged.

### 12.2 `test-migration` — mandatory (models change in A1 and R1)

1. `upgrade head` on an empty database — covered by `BonviApiFactory`.
2. **R1 conversion**, in the style of `CandidateFileMigrationTests`: migrate a
   scratch DB to the migration before `RollarMuharriri`; insert raw rows for the
   four production roles with today's exact permission arrays (copy from
   `SystemRoles.Defaults` at the commit before R1) plus one synthetic view-only
   role with `catalog.view`, `employees.view`, `employment.view`, `salary.view`
   (it exercises the capability conversion: it must come out as Catalog View,
   Employees View + `employees.personal.view`, Employment View + `salary.view`);
   migrate to head; assert the `module_levels`/`capabilities` of §4.2, that
   `Expand(converted) ⊇ old permissions` for every role, that `single_holder` is
   true exactly for `super_admin` and `main_boss`, that `is_system` is true
   exactly for `super_admin` and `recruiting_bot`, and that `permissions` is
   byte-for-byte unchanged.
3. **A1**: old rows keep their values, `actor_kind = 1`; the trigger rejects
   `UPDATE`/`DELETE`/`TRUNCATE`.
4. `Down` then `Up` again succeeds for both migrations (development safety).
5. Read both generated migrations line by line: no `DropColumn`, `DropTable`,
   `AlterColumn` on existing columns.
6. **Previous release against the new schema** (manual, part of §12.3): run the
   image of the commit before the phase against the migrated copy, log in,
   create a user, transfer an employee. It must work — this is the proof of §5.7
   and the rehearsal of §13.3.

### 12.3 Prod-like rehearsal (before every deploy)

On a local scratch copy of the latest production backup (never production
itself): run the phase's image, check counts before/after, run the §13.1 SQL,
log in as a test copy of an HR manager (set a known password hash **in the copy
only**), exercise the phase. For R1 additionally: terminate and change salary as
that HR manager (must still work); restart the API twice and confirm that the
`roles` rows did not change (no startup write). For R2 additionally: follow
Appendix A in the copy, confirm the split, rename «HR menejeri», restart, and
confirm the name and levels survive. Then roll back code-only in the copy,
restart, confirm the renamed role is still renamed (the previous seeder skips
`is_system = false`), and repeat one action. Destroy the copy afterwards — it
contains real JSHSHIR and passports.

### 12.4 `test-frontend`

`./bonvi pnpm build`, `./bonvi check` (four locales, used keys, backend error
codes), `./bonvi gen` succeeds (`/openapi/v1.json` not 500). Manual pass: as
super_admin build a custom role and assign it; as a user holding it, the menu
shows exactly the permitted sections and no page fires a 403 on load.

---

## 13. Deploy and rollback

### 13.1 Before each deploy

Follow the standing procedure (memory «Xavfsiz deploy tartibi»):
tag current images `bonvi-hr-prod-{api,bot,web}:rollback-<commit>`; take a fresh
backup `docker compose -f docker-compose.prod.yml exec backup /scripts/run-all.sh`;
rehearse on a copy (§12.3); deploy with
`nohup ./infra/deploy.sh > /tmp/deploy-<commit>.log &` outside working hours
(the bot restarts; half-filled questionnaires are lost).

Pre-deploy SQL (read-only):

```sql
SELECT migration_id FROM "__EFMigrationsHistory" ORDER BY 1 DESC LIMIT 1;
SELECT code, name, is_system, permissions FROM roles ORDER BY code;          -- R1: expect exactly 4 rows
SELECT lower(name), count(*) FROM roles GROUP BY 1 HAVING count(*) > 1;       -- R1: must be empty
SELECT r.code, count(*) FROM users u JOIN roles r ON u.role_ids @> ARRAY[r.id]
 WHERE u.deleted_at IS NULL AND u.is_active GROUP BY r.code;                  -- who holds what
SELECT count(*) FROM audit_logs;                                              -- A1: rows before
```

### 13.2 After each deploy

- A1: perform one harmless edit (e.g. a catalogue note) and see its audit row;
  `SELECT count(*) FROM audit_logs` grows; the bot still answers `/start`.
- R1: every user is still able to open their usual pages (they get one silent
  refresh); `SELECT code, name, module_levels, single_holder, is_system FROM roles`
  matches §4.2 and §5.5; still exactly four roles; an HR manager can still
  terminate (nothing split).
- R2: the owner opens «Rollar»; the matrix of «HR menejeri» shows Employment
  «To‘liq»; renaming a test copy of a role in the UI and restarting the API
  leaves the rename in place.
- R3 (owner, whenever he chooses, no deploy): he builds his roles — e.g.
  Appendix A. An HR manager reloads nothing and, on the next click, sees the
  salary/terminate/probation-complete actions disappear; the audit log shows
  every role change.
- A2/P1: pages open; spot-check masking with a temporary narrow custom role,
  then delete it.

### 13.3 Rollback

**Code-only, always**: re-tag the `rollback-<commit>` images and `up -d`. The
schema is expand-only (§5.7), so the previous release runs on it. **Never run a
migration `Down` in production** — it would drop audit columns (history) and role
edits. If data itself is damaged, restore from the backup taken before the
deploy (`./bonvi restore` procedure) — a decision for the owner, because it
discards everything written after the backup.

---

## 14. Merging the `verification` branch later

When the owner decides to merge (not part of this work):

1. **Permissions.cs conflict**: take `main`'s structure; add the three constants
   `AttendanceView`, `AttendanceManage`, `AttendanceRequestsManage` and add them to
   `Permissions.All`. Do **not** reintroduce `SystemRoles.Defaults` or any
   per-role permission array — verification's edit of the hr_manager defaults is
   dropped.
2. **AccessCatalog**: add module `attendance` (section `people`, «Davomat»):
   View adds `attendance.view`; Edit adds `attendance.requests.manage`; Full adds
   `attendance.manage`. Dependencies: `attendance` ≥ View ⇒ `employees` ≥ View and
   `catalog` ≥ View. Work-site read endpoints keep their own read policy.
3. **No data migration for roles.** Attendance has never been in production, so
   nobody loses anything: the module starts at None for every ordinary role
   (super_admin has it automatically, computed), and the owner grants it in the
   editor after the deploy — to whichever roles he has by then (they may be
   renamed or gone; no code may assume `hr_manager` or `main_boss` exists).
   Add the attendance empty-database defaults to `SystemRoles.StarterRoles`
   (Full for both starter ordinary roles) so fresh test and development
   databases match what verification grants today.
4. **Attendance officer** = a custom role built in the editor, not seeded:
   Employees View **without** `employees.personal.view`, Catalog View,
   Attendance Edit or Full. Attendance reports live in the attendance module, so
   the officer does not need Reports (resolves `PLAN-rollar.md` §5 question 3);
   flags visibility = Lists View if the owner wants it (question 2).
5. **Audit classification** (the coverage test will demand it): audited —
   `AbsenceRequest`, `WorkSchedule`, `WorkScheduleDay`, `WorkScheduleDate`,
   `ScheduleAssignment`, `AttendanceDevice` (token hash masked),
   `AttendanceIdentity`, `WorkSite`, `EmployeeAccount` (password hash masked);
   excluded — `AttendancePass`, `AttendanceDay`, `DeviceEnrollment`,
   `MobileCheckIn` (machine-generated, high volume, already an immutable event
   log), `EmployeeRefreshToken`.
6. **Actors**: add `AuditActorKind.EmployeeAccount = 4` (PWA requests via
   `ICurrentEmployee`) and `Device = 5` (agent/ingest). This is attribution only —
   employee accounts get **no roles**.
7. **Freshness**: the `EmployeeBearer` scheme is untouched by §6; the
   `OnTokenValidated` handler is registered on the default HR scheme only.
8. **Migrations**: verification's migrations have earlier timestamps; EF applies
   pending migrations by id, so on a database that already has A1/R1 they run
   after them — they are independent tables. The model snapshot must be merged by
   hand (known since `TashqiApiKalitlari`).
9. New attendance endpoints go into `endpoint-permissions.txt`; the matrix test
   covers them automatically.

---

## 15. Definition of Done

For **every** phase:

- [ ] `./bonvi test` green; no test deleted or weakened without a named successor.
- [ ] `./bonvi check` green; `./bonvi pnpm build` green; `./bonvi gen` succeeds.
- [ ] Migration (A1, R1): exists, `upgrade head` works, the generated file was
      read and contains no drops/alters of existing columns; §12.2 passes.
- [ ] Every new endpoint is in `endpoint-permissions.txt` and covered by an RBAC
      test; every changed endpoint's matrix row passes.
- [ ] §12.3 rehearsal done on a production copy; the copy destroyed.
- [ ] Deployed only after the owner's go-ahead; §13.2 checks done.
- [ ] One line per unanswered §17 decision appended to `docs/ASSUMPTIONS.md`.

Phase-specific:

- [ ] A1: an HR action in the running system produces audit rows with actor
      name, role, operation, IP; the trigger rejects `UPDATE` in `./bonvi psql`.
- [ ] R1: all current users keep every endpoint they had (the §12.2 conversion
      test proves `Expand(converted) ⊇ old permissions`); still exactly four
      roles, no new one; the seeder test proves no startup write to an existing
      role; the §4.6 grep finds `SystemRoles.MainBoss`/`HrManager` only in
      `SystemRoles.cs` and `CreateDevUsersAsync`; freshness tests (§6.1) pass;
      `PLAN.md` §5.1 rewritten for owner-defined roles.
- [ ] R2: a role built, renamed, re-leveled, assigned and deleted by hand in the
      browser behaves as the matrix says with no restart; no `t('roles.…')`
      call remains; manual (uz-Latn + uz-Cyrl) has «Rollar va ruxsatlar» with
      Appendix A as a worked example; PDFs rebuilt with
      `./bonvi pnpm build:manual`.
- [ ] R3 is the owner's own work, not a delivery — nothing to tick.
- [ ] A2: the owner can answer "who changed this employee's salary and what was
      it before" from the employee's «O‘zgarishlar tarixi» tab; manual has
      «Audit jurnali».
- [ ] P1: leak tests pass; no current user sees any difference.

---

## 16. Risks and deferred items

1. **Root on the server can still tamper with `audit_logs`** (§7.7). Deferred
   hardening, recommended only if database credentials are ever shared or the
   server hosts other tenants: (a) run the API as a non-superuser `bonvi_app`
   role with `INSERT, SELECT` only on `audit_logs` (migrations keep the owner
   login — needs a second connection string); (b) a hash-chained seal table
   exported off-box daily. Recommendation: **not now** — the application and
   every HR user already cannot touch the trail, and daily backups make tampering
   detectable.
2. **Deleted candidate/employee files**: the audit keeps name, type, size and
   storage key, not the bytes (unchanged file-deletion behaviour).
   Recommendation: unchanged.
3. **Hard-coded dashboard/notification assumptions**: any screen that assumes
   "every user has employees.view" breaks for a narrow role. R2 gates the
   dashboard and sidebar counts; the §12.4 manual pass with a narrow role is the
   guard.
4. **`employment.manage` alias** outlives its purpose if forgotten. Remove it in
   the release after P1 (a one-line change plus the inventory update).
5. **ETL writes audit rows** (~20k per full run). Accepted (§7.8).
6. **Event correction still overwrites** the event row by design; history lives
   only in the audit. Accepted: the owner decided typos are corrected in place
   (2026-09-16).
7. **One DB round-trip per request** for the stamp check. Accepted at this scale;
   a cache can be added behind `IAccessStampValidator` if ever measured as a
   problem.
8. **Latent output-cache trap.** `OutputCachePolicies.Malumotnoma`
   (`PerformanceExtensions.cs`) varies its cache key by **role codes** and is
   attached to no endpoint today. Now that a role's rights change at runtime
   under an unchanged code, attaching it would serve stale lists. R1 adds a code
   comment on the policy: key by the caller's access stamp or permission set if
   it is ever used. Recommendation: nothing else now.
9. **A deleted starter role never comes back.** Deleting «HR menejeri» (once
   unused) is permanent — restarts do not recreate it (§5.6). Accepted: that is
   the owner's rule; «Nusxa olish» from another role rebuilds one in a minute.

---

## 17. Open decisions for the owner

If the owner says nothing, each default stands; log it in `docs/ASSUMPTIONS.md`.

Settled by the owner's decision that he defines roles himself (no longer open):
who becomes HR head and when the split starts; whether HR managers keep hire and
record correction; who may edit roles; what HR managers see in the audit log.
Each of these is now a setting he changes in the editor at runtime (Appendix A
is only a suggestion).

Still open:

1. **The "only one «Bosh rahbar»" rule, now that «Bosh rahbar» is an ordinary
   role.** *Default:* it becomes a per-role switch «Faqat bitta faol hisob»; the
   migration turns it on for «Bosh rahbar», so behaviour on deploy is unchanged,
   and the owner can switch it off or put it on any other role in the editor
   (G10). super_admin stays single-holder by code. *(Alternative: drop the rule
   entirely — simpler, but it silently allows a second «Bosh rahbar».)*
2. **Money inside other employment actions** — the one part of the split the
   editor cannot change, because it closes a bypass instead of granting a right
   (§3.9). *Default:* a transfer with a new salary, a paid additional duty, the
   salary set when completing probation, and a corrected salary amount all need
   «Oklad o‘zgarishi»; the starting salary of a hire does not. Paid additional
   duty is the debatable case; relaxing it is a one-line change if the owner
   prefers.

---

## Appendix A — Suggested setup (documentation only; nothing applies it)

This is the owner's intended split of 2026-09-26, written as clicks. No
migration, seeder or button performs it; the owner follows it himself, any time
after R2, as super_admin. Each step reaches the affected users on their next
request (§6.1) and leaves an audit row.

**A.1 «Kadr bo‘limi boshlig‘i» and a narrower «HR menejeri»**

1. «Rollar» → «HR menejeri» → «Nusxa olish». Name «Kadr bo‘limi boshlig‘i».
   «Mehnat munosabatlari» → «To‘liq». Saqlash.
2. «Foydalanuvchilar» → the chosen person → Rol «Kadr bo‘limi boshlig‘i» →
   Saqlash. (super_admin or «Bosh rahbar» can do this step.)
3. «Rollar» → «HR menejeri» → «Mehnat munosabatlari» → «O‘zgartirish», then tick
   «Ishga qabul», «Ko‘chirish (lavozim, bo‘lim)», «Qo‘shimcha vazifa»,
   «Sinov muddatini uzaytirish», «Yozuvni tuzatish», «Okladni ko‘rish»; leave
   «Oklad o‘zgarishi», «Ishdan bo‘shatish», «Sinov muddatini yakunlash»
   unticked. Saqlash → confirm.

Resulting grants (the other modules stay as converted, §4.2):

| Module / action | «Kadr bo‘limi boshlig‘i» | «HR menejeri» |
|---|---|---|
| Mehnat munosabatlari | To‘liq (every action) | O‘zgartirish |
| Ishga qabul · Ko‘chirish · Qo‘shimcha vazifa · Sinovni uzaytirish · Yozuvni tuzatish · Okladni ko‘rish | yes | yes |
| Oklad o‘zgarishi · Ishdan bo‘shatish · Sinovni yakunlash | yes | no |

Notes: «Ishga qabul» and «Yozuvni tuzatish» are not in the owner's original
wording — suggested because HR managers do both daily. Because of §3.9, an HR
manager without «Oklad o‘zgarishi» cannot put money into a transfer, a paid
duty, a probation completion or a salary correction, but can still enter the
starting salary of a hire. Do steps 1–2 before step 3; if step 3 comes first,
nothing is lost (super_admin and «Bosh rahbar» keep every action), but HR
managers wait until the HR head is assigned.

**A.2 «Davomat mas’uli» (after the `verification` merge, §14)**

A new role: «Xodimlar» → «Ko‘rish» without «Shaxsiy ma’lumotlar»;
«Ma’lumotnomalar» → «Ko‘rish»; «Davomat» → «O‘zgartirish» (absence-request
decisions) or «To‘liq» (also devices and schedules); optionally «Ro‘yxatlar» →
«Ko‘rish» if the owner wants flags visible (`PLAN-rollar.md` §5 question 2).
