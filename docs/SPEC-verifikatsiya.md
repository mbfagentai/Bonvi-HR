# SPEC — Verifikatsiya: davomat (attendance), phase 3, Verifix replacement

> Status: authoritative. Written 2026-09-17. Branch: `verification`.
> Language: English (project rule — internal docs are English; only
> client-facing text is Uzbek). Three things stay in the repo's existing
> Uzbek style, each for a stated reason: **code comments** (§0.2), **error
> codes** (§6.9) and **the migration class name** (§2.12).
>
> **Read this before writing code.** Build agents cannot see the conversation
> that produced this file. Every decision below is final unless the user says
> otherwise; the "rejected alternative" notes exist so nobody re-litigates
> them halfway through the work.
>
> This is **phase 3** of `PLAN.md` §0 — the module that replaces the paid
> Verifix service. `PLAN.md` §8 records the two facts that shape it: HR uses
> only **10–15 %** of Verifix, and an employee passes the turnstile **up to
> 10 times a day**.

---

## AMENDMENT — 2026-09-17, after the owner answered §13

⚠️ **If you started from an earlier copy of this file, or from a summary of
it, several rules you have are wrong** — rows 4–7 below were added by a second
follow-up on the same day. Every amended passage is marked
**AMENDED 2026-09-17** inline; this is the index. Section numbers did not
move — nothing was renumbered, only rewritten in place.

| # | What changed | Was | Is now | Read |
|---|---|---|---|---|
| 1 | **The break IS deducted from worked minutes** — against a defined window | `break_minutes` reduced `planned_minutes` only | `work_schedule_days` gains `break_start`/`break_end`; `worked_minutes −= max(0, min(break_minutes − breakTaken, presentInWindow))` (the `presentInWindow` cap is row 4); **no window ⇒ no deduction** | §2.4.0, §2.4.0.1, §5.5 step 5, **§5.5.1 (rewritten whole)**, §5.7, §6.4 |
| 2 | **Passes expire on a policy** | kept forever | `attendance.passRetentionDays`, default 365, `0` = never; nightly `attendance:purge-passes`; **`attendance_days` never deleted** | §1.2 #5, §1.2 #10, §2.9, §9.2, **§9.2.1 (new)**, §8.9 |
| 3 | **The terminated-employee pass becomes an alert** | a `NotEmployed` status value in a table | red, non-dismissible alert at the **top of the live board**, a devices-page warning and a danger nav badge; the §2.13 seam is now **committed future work** | §2.13, §6.5, §8.4, §8.7 |
| 4 | **§13 #14 decided — the deduction is capped by presence in the window** | `max(0, break_minutes − breakTaken)`; leaving at 12:55 still cost a full break | `max(0, min(break_minutes − breakTaken, presentInWindow))`; the deduction can **never exceed the minutes worked** | §5.5.1, §5.7, §11.1 #58–60, #58a–c, §13 #1, §13 #14 |
| 5 | **`attendance_days` stores the break** | not stored | `break_taken_minutes`, `break_deduction_minutes`; they arrive in a **second** migration, `DavomatTanaffusNatijasi`, consolidated before merge — **do not squash it early**; the popover reads only these two | §2.6, §2.12, §6.6, §8.6, §11.1 #64a, §11.2 #7b, §12 |
| 6 | **Empty-id error codes renamed in code** | `davomat.xodim_topilmadi` / `davomat.jadval_topilmadi` emitted as 400 | `davomat.xodim_yoq` / `davomat.jadval_yoq` (400); `*_topilmadi` is 404 only | §6.9, §6.9.1 |
| 7 | **Client contract gaps closed (F1 review of §6)** | undefined | `GET /passes/{id}`, `GET /former-employee-alerts`, full DTO field sets, alerts **one entry per person**, `validatePassesSearch` on the log route, weekday label keys, `davomat.otish_topilmadi` (codes 40 → **41**) | §6.2–§6.6, §6.9, §7.3, §8.2, §8.4, §8.6–§8.8, §11.1 #74a–b |

Knock-on edits: §6.9 was **reconciled against the 18 codes B1 actually shipped** (33 → **40**; six invented break codes replaced by B1's three; **§6.9.1 names two codes where the code, not the SPEC, is wrong**), §5.5.1 gained the **window-to-instant formula** an earlier revision wrongly claimed §2.4.0.1 provided, §8.8 gained the new
i18n keys, §10.4 C7 gained two proofs, §11.1 gained cases **#51–74** (RBAC and
regression guards renumbered to **#75–83**), §11.2 gained case 7a, §12 gained
four boxes, and §13 #1/#3/#4 now record the **owner's answer** rather than my
standing decision. §13 #13 and #14 are new; **#14 was decided on 2026-09-17** (row 4).

⚠️ **The single most dangerous mistake this amendment introduces** is a pass
purge that marks its day dirty and lets the recompute rewrite a year-old tabel
to zero. §9.2.1 forbids it in capitals and §11.1 #67 / §10.4 C7 pin it. Read
that before writing the job.

---

## 0. Reference implementation and house conventions

### 0.1 Copy the shape of these files

Where this SPEC deviates from them, the deviation is named and justified.

| What | Reference |
|---|---|
| Whole module shape: Domain → Application (`Commands` / `Queries` / `Dtos` / `Mapper` / `Registration` / `Common/Errors`) → Api endpoints | `api/src/Bonvi.Domain/Safety/Incident.cs`, `api/src/Bonvi.Application/Safety/**`, `api/src/Bonvi.Api/Endpoints/IncidentEndpoints.cs` |
| Permission catalogue + doc-comment style + role defaults | `api/src/Bonvi.Domain/Identity/Permissions.cs` |
| Aggregate with a child collection | `Employment` + `EmploymentEvent` (`api/src/Bonvi.Domain/Employees/`) |
| EF configuration: table name, max lengths, FK delete behaviour, partial indexes | `api/src/Bonvi.Infrastructure/Persistence/Configurations/IncidentConfiguration.cs`, `EmploymentConfiguration.cs` |
| Paged list query + filter record + read model | `SearchEmployeesQuery` (`api/src/Bonvi.Application/Employees/Queries/`) + `IEmployeeReadModel` + `api/src/Bonvi.Infrastructure/Persistence/ReadModels/EmployeeReadModel.cs` |
| Excel export endpoint (`reports.export` + `RateLimitPolicies.Export`) | `api/src/Bonvi.Api/Endpoints/ReportsEndpoints.cs` + `api/src/Bonvi.Infrastructure/Reports/EmployeeExcelWriter.cs` |
| Excel/CSV **reading** (header aliases, date cells, dirty values) | `api/tools/Bonvi.Etl/Excel/**` + `api/tools/Bonvi.Etl/Mapping/AliasTable.cs` |
| File upload: `multipart/form-data`, `RequestSizeLimitAttribute`, `DisableAntiforgery` | `api/src/Bonvi.Api/Endpoints/EmployeeEndpoints.cs` (`POST /{id}/photo`) |
| Image bytes served with ETag + private cache | `api/src/Bonvi.Api/Common/PhotoHttp.cs` |
| File storage | `api/src/Bonvi.Infrastructure/Storage/LocalFileStorage.cs` (volume `uploads-data` → `/data/uploads`) |
| Encrypted secret in a settings row | `api/src/Bonvi.Domain/Settings/SystemSettingKeys.cs` (`BotToken`) + `ISecretProtector` |
| Number setting with min/max + definition list | `SystemSettingKeys.ReadRetentionDays` |
| Rate-limit policy declaration | `api/src/Bonvi.Api/Extensions/RateLimitingExtensions.cs` |
| List page: filters, `DataTable`, permission-gated empty state | `web/src/features/safety/components/incidents-page.tsx` |
| Route file + `validateListSearch` | `web/src/app/routes/_app/hodisalar.tsx` |
| Collapsible nav section | `nav-items.ts` → the `sectionCatalog` and `recruiting` entries |

`docs/STACK.md` and `docs/CONVENTIONS.md` do not exist in this repo. This is
not a new project, so the conventions were read out of the code; every
convention this SPEC relies on is named at the point of use.

### 0.2 Non-negotiable house rules for this module

1. **Identifiers are English, code comments are Uzbek.** Every file in
   `api/src/**` uses Uzbek `<summary>`/`<remarks>` doc comments and Uzbek
   inline comments. Same in `web/src/**` (JSDoc in Uzbek). This document is
   English; the code is not. Do not "translate the SPEC into the code".
2. **Comments explain *why*, not *what*.** Follow `Permissions.cs` and
   `Incident.cs`: every non-obvious decision carries the reason and, where
   relevant, the rejected alternative.
3. **Layering** is enforced by `api/tests/Bonvi.Architecture.Tests/LayerDependencyTests.cs`:
   Domain references nothing; Application must not reference EF Core, ASP.NET
   Core, Npgsql or Infrastructure; Infrastructure must not reference Api.
   Read models are interfaces in `Bonvi.Application/Attendance/Abstractions/`
   implemented in `Bonvi.Infrastructure/Persistence/ReadModels/`.
4. **Enums travel by name**, not number (`JsonStringEnumConverter` is already
   global in `Program.cs`). Still assign explicit numeric values — the column
   stores the number.
5. **All user-visible strings go through i18n in 4 locales**
   (`uz-Latn`, `uz-Cyrl`, `ru`, `en`). `./bonvi check` fails the build
   otherwise, and it also fails if a backend error code has no translation.
6. **Uzbek Latin uses typographic apostrophes** in locale files: `o‘`, `g‘`,
   `’` (U+2018 / U+2019), never `'`.
7. `Guid` ids come from the domain (`Entity.NewId()` → `Guid.CreateVersion7()`).
8. ⚠️ **`Guid = default` in a request record breaks the whole OpenAPI
   document (500).** Known trap, fixed once already on 2026-08-29. Never write
   `Guid Id = default` in a `sealed record` request. `./bonvi gen` is the
   canary.

---

## 1. Goal and scope

### 1.1 Goal

Bonvi pays Verifix every month for a turnstile/face-terminal product of which
HR uses 10–15 %: *who came, when, who was late, who is in the building now,
and the monthly tabel*. The terminals stay; the software around them moves
into BonviHR, where the employee record already lives.

Concretely, v1 delivers six things:

1. Hikvision face/turnstile terminals push every pass **directly to our API**
   over the internet; we store it with its face snapshot.
2. The device's person id (the one Verifix provisioned) is mapped to our
   `Employee` through an importable, first-class mapping table.
3. Work schedules are defined per department / position, with a per-employee
   override and a system default, and they survive a shift that crosses
   midnight.
4. A live "who is in now" board.
5. A pass log with photo thumbnails, a timesheet (tabel) grid, and
   late / early-leave / absence reporting.
6. Excel export for every one of those reports.

Users: the same ~3 HR managers and the head of the company. **Employees have
no logins in this system at all** (`PLAN.md` §9, answered 2026-08-22) — there
is no self-service, no mobile app and no employee-facing screen anywhere in
this module.

### 1.2 Non-goals — what v1 deliberately does NOT do, and why

Numbered so they can be cited. Each one was considered and rejected **for this
release**, not forever.

1. **No turnstile control.** We only record; we never tell a door to open or
   stay shut. Reason: an access *decision* is a safety-critical, real-time
   path with a completely different failure mode (if our API is down, people
   must still get to work). The terminal's own allow-list keeps doing that
   job. §2.13 describes the seam a later release would use.
2. **No payroll, no money.** Worked minutes are counted; they are never
   multiplied by anything. Reason: `Employment.Salary` exists but pay rules
   (overtime coefficients, night bonus, holidays) were never specified and
   guessing them produces numbers that look authoritative and are wrong.
   `OvertimeMinutes` is computed and stored, and stops there.
3. **No guest / visitor QR passes.** Reason: a guest is not an `Employee`, so
   every screen in this module would need a second, parallel "person" concept.
   Nobody asked for it.
4. **No employee self-service, no mobile app, no Telegram "my attendance".**
   Reason: employees have no accounts (§1.1). Adding one channel means adding
   authentication for 540 people, which is a larger project than this module.
5. **No manual editing of a computed attendance day, and no hand-deleting of
   a pass** — **AMENDED 2026-09-17.** HR cannot type "he actually worked
   8 hours", cannot delete an inconvenient pass, and there is no endpoint for
   either. Reason: the value of this archive is that **no person chose what is
   in it**. The moment one row can be removed by hand, every remaining row
   becomes an assertion rather than a record, and every dispute turns into
   "who changed it". Wrong data is fixed at its source — fix the mapping, fix
   the schedule, fix the device clock, then recompute. §5.8 lists the four
   legitimate repairs.

   What the amendment changed is **not** this: passes now expire on a
   configurable policy (§2.9, `attendance.passRetentionDays`, default 365
   days, `0` = never). That is a different thing and it does not weaken the
   argument. A retention policy is set once, applies uniformly to everything
   past a date, and is visible to anyone who opens the settings screen — it
   cannot be aimed at a particular Tuesday that somebody would rather did not
   exist. Deletion-by-hand is selective, silent and after the fact; ageing-out
   is uniform, declared in advance and the same for everyone. The first
   destroys the record's authority; the second is ordinary records management.
   And the derived record — the tabel, the hours, the lateness figures — is
   never deleted at all.
6. **No absence *reasons*** (leave, sick note, business trip). Reason: there
   is no vacation/leave module in the system yet; inventing a half one here
   would be the third place employee state is tracked. `Absent` in v1 means
   exactly "the schedule expected this person and no pass was recorded".
7. **No 2/2 or rotating cycle schedules.** Reason: a weekly pattern cannot
   express them and the real rotation rules are not known yet. The *design*
   already carries the seam (`WorkScheduleKind.Cycle`, §2.7) and night shifts
   across midnight **are** supported today, so this is a feature gap, not a
   rewrite.
8. **No local bridge / on-premise forwarding agent.** The terminals post
   straight to `https://hr.bonvi.uz`. Reason: owner's decision. The ingest
   layer is parser-per-provider (§3.5), so an agent or a pull-mode adapter is
   a new `IAttendanceEventParser` plus one enum value, not a rewrite.
9. **No WebSocket / SSE live push.** The board polls every 30 s. Reason: the
   system has no realtime transport today, three people watch the board, and
   30 s is indistinguishable from live for "who is in the building".
10. **No pass partitioning** — **AMENDED 2026-09-17.** The original wording
    was "no pass archival / partitioning … rows are kept forever". The first
    half is no longer true: passes expire on `attendance.passRetentionDays`
    (§2.9, default 365 days, `0` = never), deleted by the nightly job (§9.2),
    while `attendance_days` survives untouched. **Partitioning is still a
    non-goal**, and the retention is part of why: with a one-year default the
    table stabilises around 2 M rows (§1.4) instead of growing without bound,
    which is comfortably inside what plain PostgreSQL handles with the indexes
    of §2.3. The trigger to revisit is written into the table's doc comment:
    if someone sets the retention to `0` **and** the row count passes ~10 M,
    partition by `local_date` before the indexes stop fitting in memory.
11. **No `Employee` / `Employment` change of any kind.** See §2.11 — including
    the `Employment.ExternalRef` column that `PLAN.md` §8 proposed and that
    this SPEC deliberately does not create.
12. **No Hangfire dashboard route.** Reason: it is an admin surface that ships
    unauthenticated by default and there is no reason to expose job internals
    to anyone. Job health is visible where it matters: §8.7.

### 1.3 Vocabulary (used consistently below)

| Term | Meaning |
|---|---|
| **pass** (`o‘tish`) | one turnstile/terminal event for one person at one instant |
| **device** | one physical terminal, registered in `attendance_devices` |
| **raw person id** | the person identifier inside the device event — the id Verifix provisioned |
| **identity** | a row mapping (id-space, raw person id) → `EmployeeId` |
| **unmatched pass** | a stored pass whose raw person id has no identity yet |
| **day** | the computed daily result for one employee (`attendance_days`) |
| **tabel** | the timesheet grid: employees × days of a month |

### 1.4 Capacity — the numbers this design is sized for

540 active employees × up to 10 passes/day ≈ **5 400 passes/day**, ≈ 2.0 M
rows/year. Peak burst at shift change ≈ 4–6 requests/second across all
devices.

* `attendance_passes`: ~2 M rows/year. Six indexes (§2.4). PostgreSQL 18
  handles this without partitioning; row width is small because the payload
  is purged (§3.10).
* Photos: ~40 KB JPEG each. At the default 60-day retention ≈ 324 000 files ≈
  **13 GB** on the `uploads-data` volume. **The purge job is not optional** —
  without it this grows ~80 GB/year and fills the VPS.
* `attendance_days`: 540 × 365 ≈ **200 000 rows/year**.

These numbers belong in the doc comments of `AttendancePass` and the purge
job, so the next person sees why the retention exists.

---

## 2. Data model

Namespace `Bonvi.Domain.Attendance`, one file per aggregate, plus
`AttendanceEnums.cs` and `AttendanceRules.cs`.

### 2.0 Inherited conventions

* Table/column names are **snake_case** (global `EFCore.NamingConventions`).
* `ISoftDeletable` entities get the global query filter automatically.
* Unique indexes on soft-deletable tables carry `HasFilter("deleted_at IS NULL")`
  (precedent: `UserConfiguration.cs:120`, `ExitInterviewConfiguration.cs:55`).
* `IAuditable` supplies `CreatedAt/By`, `UpdatedAt/By`, and `ISoftDeletable`
  supplies `DeletedAt/By`. **Do not add parallel "linkedAt/linkedBy" columns**
  — see §2.3.
* Timestamps are `timestamptz` and hold **UTC**. `BonviTime.TashkentOffset`
  (+5) is for display and for "which day is this", never for storage
  (`BonviTime` doc comment).

### 2.1 `attendance_devices` — the terminal registry

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| `id` | uuid | no | — | PK, Guid v7 |
| `name` | varchar(200) | no | — | "Markaziy ofis — kirish turniketi" |
| `serial_number` | varchar(100) | yes | — | device serial, as printed |
| `model` | varchar(100) | yes | — | e.g. `DS-K1T671M`; documentation only |
| `provider` | int | no | `1` | `AttendanceProvider` — which **parser** to use |
| `identity_provider` | int | no | `1` | `AttendanceIdentityProvider` — which **id space** its events live in |
| `direction_role` | int | no | `1` | `DeviceDirectionRole` |
| `legal_entity_id` | uuid | yes | — | FK → `legal_entities`, `Restrict` |
| `department_id` | uuid | yes | — | FK → `departments`, `Restrict` |
| `location_note` | varchar(200) | yes | — | free text, e.g. "1-qavat, chap turniket" |
| `auth_mode` | int | no | `1` | `DeviceAuthMode` |
| `token_hash` | bytea | no | — | SHA-256 of the ingest token, exactly 32 bytes. **UNIQUE**. (`bytea` takes no length in PostgreSQL; the fixed width is a domain invariant, not a column constraint) |
| `token_prefix` | varchar(8) | no | — | first 8 chars of the token, shown in the UI |
| `token_issued_at` | timestamptz | no | — | |
| `token_issued_by` | uuid | yes | — | user id, no FK (same as audit columns) |
| `hmac_secret` | varchar(500) | yes | — | `ISecretProtector`-encrypted; required when `auth_mode = TokenAndHmac` |
| `allowed_ips` | varchar(500) | yes | — | comma-separated IPv4/IPv6 CIDRs; empty ⇒ no IP restriction |
| `is_active` | boolean | no | `true` | |
| `last_seen_at` | timestamptz | yes | — | any ingest request that reached authentication |
| `last_event_at` | timestamptz | yes | — | last **accepted** pass |
| `last_seen_ip` | varchar(45) | yes | — | machine-written |
| `last_skew_seconds` | int | yes | — | clock skew of the last accepted pass |
| audit + soft delete | | | | `IAuditable`, `ISoftDeletable` |

Indexes:
* `ux_attendance_devices_token_hash` UNIQUE (`token_hash`) — no soft-delete
  filter: a deleted device's token must stay dead.
* `ux_attendance_devices_name` UNIQUE on `lower(name)` WHERE `deleted_at IS NULL`
  (raw SQL in the migration, same technique as the reference catalogues).
* `ux_attendance_devices_serial` UNIQUE on `lower(serial_number)`
  WHERE `serial_number IS NOT NULL AND deleted_at IS NULL`.

Domain API: `AttendanceDevice.Register(...)`, `.Rename(...)`,
`.Reconfigure(...)`, `.RotateToken(hash, prefix, at, by)`,
`.Activate()` / `.Deactivate()`, `.NoteSeen(at, ip)`,
`.NoteEvent(at, ip, skewSeconds)`.

### 2.2 `attendance_identities` — (id space, raw person id) → employee

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| `id` | uuid | no | — | PK |
| `provider` | int | no | `1` | `AttendanceIdentityProvider` |
| `external_person_id` | varchar(64) | no | — | trimmed; stored exactly as the device sends it |
| `employee_id` | uuid | no | — | FK → `employees`, `Restrict` |
| `display_name_in_device` | varchar(200) | yes | — | FIO as it stands in the Verifix/device list |
| `source` | int | no | `2` | `IdentitySource`: `Import = 1`, `Manual = 2` |
| `note` | varchar(500) | yes | — | |
| audit + soft delete | | | | |

Indexes:
* `ux_attendance_identities_person` UNIQUE (`provider`, `external_person_id`)
  WHERE `deleted_at IS NULL`.
* `ix_attendance_identities_employee` (`employee_id`).

**One employee may hold several identities** (two terminals provisioned him
twice, or a second id space is added later) — that is why the unique index is
on the *person id*, not on the employee.

**History is kept by soft delete, not by extra columns.** Unlinking sets
`deleted_at`/`deleted_by`; relinking inserts a new row. `CreatedAt`/`CreatedBy`
*are* "linked at / linked by" and `DeletedAt`/`DeletedBy` *are* "unlinked at /
unlinked by". Rejected alternative: separate `linked_at`, `linked_by`,
`unlinked_at`, `unlinked_by` columns — four columns duplicating four that the
interceptor already fills, which would drift apart the first time someone
forgets one.

### 2.3 `attendance_passes` — one raw pass

Not `IAuditable`, not `ISoftDeletable`. Reason: nobody edits or deletes a
pass; it is a machine record and there is no delete endpoint (§1.2 #5).
`received_at` replaces `created_at`.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| `id` | uuid | no | — | PK, Guid v7 |
| `device_id` | uuid | no | — | FK → `attendance_devices`, `Restrict` |
| `identity_provider` | int | no | — | **copied from the device at ingest**, not joined |
| `raw_person_id` | varchar(64) | no | — | |
| `resolved_employee_id` | uuid | yes | — | FK → `employees`, `Restrict`. NULL ⇒ unmatched |
| `resolved_at` | timestamptz | yes | — | when the mapping was applied (ingest or backfill) |
| `occurred_at` | timestamptz | no | — | UTC instant of the pass |
| `device_local_time` | timestamp | no | — | exactly what the device wrote, no offset |
| `device_offset_minutes` | smallint | no | — | `+300` for Tashkent |
| `offset_assumed` | boolean | no | `false` | true ⇒ the device sent no offset and +5 was assumed |
| `local_date` | date | no | — | Tashkent calendar date of `occurred_at` |
| `received_at` | timestamptz | no | — | server clock at ingest |
| `clock_skew_seconds` | int | no | `0` | `received_at − occurred_at`, signed |
| `direction` | int | no | `3` | `PassDirection` |
| `direction_source` | int | no | `3` | `DirectionSource` |
| `method` | int | no | `6` | `PassMethod` |
| `photo_key` | varchar(200) | yes | — | `IFileStorage` key |
| `photo_purged_at` | timestamptz | yes | — | set by the purge job |
| `raw_event` | jsonb | yes | — | the source event JSON; purged with the photo |
| `dedupe_key` | varchar(200) | no | — | §3.6 |
| `processed_at` | timestamptz | yes | — | NULL ⇒ waiting for recompute |

Indexes:
* `ux_attendance_passes_device_dedupe` UNIQUE (`device_id`, `dedupe_key`)
  — the replay guard (§3.6).
* `ix_attendance_passes_employee_time` (`resolved_employee_id`, `occurred_at`)
  — board + per-employee log.
* `ix_attendance_passes_local_date` (`local_date`).
* `ix_attendance_passes_device_time` (`device_id`, `occurred_at`).
* `ix_attendance_passes_unmatched` (`identity_provider`, `raw_person_id`)
  WHERE `resolved_employee_id IS NULL` — the unmatched screen and the backfill.
* `ix_attendance_passes_pending` (`processed_at`) WHERE `processed_at IS NULL`
  — the dirty queue; stays tiny.
* `ix_attendance_passes_photo` (`received_at`) WHERE `photo_key IS NOT NULL`
  — the purge scan.

The pass stores **what the device said**, never an interpretation. The
timesheet's direction inference (§5.5) is written into `attendance_days`, not
back into the pass — otherwise re-running the computation with a fixed
schedule would rewrite history.

### 2.4 `work_schedules` + `work_schedule_days`

`WorkSchedule` is the aggregate root; `WorkScheduleDay` is its child (loaded
through the parent, no repository of its own — same shape as
`Employment`/`EmploymentEvent`).

`work_schedules`:

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| `id` | uuid | no | — | PK |
| `name` | varchar(200) | no | — | "Ofis 9–18", "Tungi smena 20–08" |
| `kind` | int | no | `1` | `WorkScheduleKind` |
| `cycle_length_days` | int | yes | — | NULL for `Weekly`; reserved for `Cycle` |
| `grace_in_minutes` | int | no | `10` | 0…120 |
| `grace_out_minutes` | int | no | `10` | 0…120 |
| `is_active` | boolean | no | `true` | |
| `note` | varchar(500) | yes | — | |
| audit + soft delete | | | | |

* `ux_work_schedules_name` UNIQUE on `lower(name)` WHERE `deleted_at IS NULL`.

`work_schedule_days`:

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| `id` | uuid | no | — | PK |
| `work_schedule_id` | uuid | no | — | FK, `Cascade` (child of the aggregate) |
| `day_index` | int | no | — | ISO weekday 1 = Monday … 7 = Sunday for `Weekly` |
| `is_working` | boolean | no | — | |
| `start_time` | time | yes | — | required when `is_working` |
| `end_time` | time | yes | — | required when `is_working` |
| `break_minutes` | int | no | `0` | 0…480 |
| `break_start` | time | yes | — | **AMENDED 2026-09-17** — start of the break window |
| `break_end` | time | yes | — | **AMENDED 2026-09-17** — end of the break window |

* `ux_work_schedule_days_day` UNIQUE (`work_schedule_id`, `day_index`).
* A `Weekly` schedule always has **exactly 7 rows**, created by
  `WorkSchedule.Create` (non-working days are rows with `is_working = false`).
  Reason: a missing row and a day off are then the same thing in the UI and
  in the computation, and the grid never has to ask "is this day absent or
  undefined".

#### 2.4.0 The break window — AMENDED 2026-09-17 (owner's answer to §13 #1)

> ⚠️ **This subsection reverses the original SPEC.** The first version had no
> break window and did not subtract the break from worked minutes. The owner
> decided otherwise: the break **is** subtracted, but only when it can be
> checked against a defined window. Read §5.5.1 with it — the two were
> rewritten together.

`break_start` / `break_end` describe **when** the unpaid break is expected,
not how long it is: `break_minutes` keeps doing that. Both columns are
nullable and a schedule day without them behaves exactly as v1 originally
specified (no deduction at all — §5.5.1).

Validation, enforced in `WorkScheduleDay` and mirrored by a check constraint:

> ⚠️ **Codes reconciled 2026-09-17 to what B1 actually shipped.** The draft
> of this section invented six codes; B1 had already coined **three** in
> `WorkScheduleDay.cs` and collapsed two pairs. The three below are the
> contract — use these exact strings. The invented names
> (`…_yarim`, `…_nol`, `…_tashqarida`, `…_dam_kuni`, `…_davomiyligi_yoq`,
> `…_uzun`) **do not exist**; do not translate them and do not add them.

| Rule | Violation |
|---|---|
| **Both or neither.** `break_start` and `break_end` are set together | `davomat.jadval_tanaffus_toliq_emas` |
| The window is **non-empty** *and* lies **inside the shift**, measured on the shift's own timeline (§2.4.0.1). One code covers both — B1 merged them, and from the operator's side they are the same mistake: "the window you drew is not a real part of this shift" | `davomat.jadval_tanaffus_oynasi` |
| `break_minutes` is within `0 … MaxBreakMinutes` | `davomat.jadval_tanaffus_uzunligi` |

Two rules from the draft are **not** errors, by B1's design, and this SPEC
adopts that:

* **A window on a non-working day is silently nulled**, not rejected
  (`WorkScheduleDay` sets `BreakStart = null` when `IsWorking` is false). A
  day off has no shift to place a window inside, so there is nothing to
  report and nothing a user could fix — the value simply has no meaning.
* **A window with `break_minutes = 0` is accepted** and deducts nothing
  (§5.5.1). Harmless, and rejecting it would block the natural intermediate
  state of someone filling the form top-down.

`break_minutes` is **not** checked against the window's own length. A 60-minute
break inside a 45-minute window is accepted; `breakTaken` is capped by the
overlap anyway (§5.5.1), so the arithmetic stays correct and the worst case
is a deduction the schedule author did not intend — visible in the timesheet
popover, not silent.

Check constraint `ck_work_schedule_days_break_window` (name as shipped by B1):

```sql
(break_start IS NULL AND break_end IS NULL)
OR (is_working
    AND break_start IS NOT NULL
    AND break_end IS NOT NULL
    AND break_start <> break_end)
```

⚠️ **This is the constraint B1 already generated** (migration
`20260917063405_DavomatModuli`) and this SPEC has been aligned **to it**, not
the other way round. The draft text asked for `AND break_minutes > 0` as well;
that clause is **not** in the shipped constraint and must **not** be added —
doing so needs a second migration for a rule the domain can enforce for free.
A window with `break_minutes = 0` is therefore accepted by the database and
simply deducts nothing, which is harmless (§5.5.1).

The "inside the shift" and "not longer than the window" rules are **not** in
the constraint: they need the crossing-midnight normalisation of §2.4.0.1,
which SQL would have to repeat and then keep in sync with the domain. The
constraint catches the shapes that are wrong under any reading; the domain
catches the rest. That split is deliberate — state it in the configuration's
comment.

##### 2.4.0.1 "Inside the shift" on a crossing-midnight shift

Do not compare the raw `time` values. Project all three onto the shift's own
timeline, in minutes from `start_time`:

```
offset(t) = (t − start_time + 1440) mod 1440
shiftLength = (end_time − start_time + 1440) mod 1440      // never 0, §2.4.1
```

The window is inside the shift when

```
0 <= offset(break_start) < offset(break_end) <= shiftLength
```

For a 20:00→08:00 shift with a 00:30→01:30 break: `offset(20:00) = 0`,
`offset(00:30) = 270`, `offset(01:30) = 330`, `shiftLength = 720` — inside.
Compared as raw clock times, `00:30 < 20:00` would have looked like a window
*before* the shift, which is exactly the bug this normalisation exists to
prevent.

`offset(break_end) == 0` means the window ends at the shift's start and is
rejected by the strict `<` on the left; a window that ends exactly at
`end_time` is allowed (`<= shiftLength`) — a break at the very end of a shift
is odd but not impossible, and the §5.5.1 tail rule already handles the case
where the employee simply goes home into it.

#### 2.4.1 `CrossesMidnight` is computed, not stored — deviation from the proposed shape

The proposed shape listed a `crosses-midnight flag`. It is **not** a column.

```
public bool CrossesMidnight => IsWorking && EndTime <= StartTime;
```

Reason: a stored flag can contradict the two times it describes. The first
time someone edits `end_time` from `18:00` to `02:00` without touching the
flag, the timesheet computes a negative span and reports −16 hours as if it
were data. Derived, the two can never disagree. Everything the owner asked
for (an overnight shift works, and adding 2/2 later does not need a rewrite)
is unaffected.

`start_time == end_time` is rejected — `davomat.jadval_vaqt_teng`. Reason: it
is ambiguous between "zero-length" and "24 hours" and neither is a real
shift.

#### 2.4.2 The cycle seam

`WorkScheduleKind` = `Weekly = 1`, `Cycle = 2`. v1 implements `Weekly` only
and `WorkSchedule.Create` returns `davomat.jadval_turi_qollanmaydi` for
`Cycle`. `day_index` is documented as "ISO weekday for `Weekly`, 1-based
cycle-day index for `Cycle`", so 2/2 needs a resolver change and an anchor
date on the assignment — no new table and no data migration.
`ScheduleAssignment.CycleAnchorDate` is **not** created now: it cannot be
filled with anything meaningful until cycles exist, and a nullable column is
cheap to add.

### 2.5 `schedule_assignments`

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| `id` | uuid | no | — | PK |
| `work_schedule_id` | uuid | no | — | FK → `work_schedules`, `Restrict` |
| `scope` | int | no | — | `ScheduleScope` |
| `employee_id` | uuid | yes | — | FK → `employees`, `Restrict` |
| `position_id` | uuid | yes | — | FK → `positions`, `Restrict` |
| `department_id` | uuid | yes | — | FK → `departments`, `Restrict` |
| `effective_from` | date | no | — | |
| `effective_to` | date | yes | — | inclusive; NULL ⇒ open-ended |
| audit + soft delete | | | | |

Check constraint `ck_schedule_assignments_scope` — exactly the matching column
is non-null:

```sql
(scope = 1 AND employee_id IS NOT NULL AND position_id IS NULL AND department_id IS NULL) OR
(scope = 2 AND position_id IS NOT NULL AND employee_id IS NULL AND department_id IS NULL) OR
(scope = 3 AND department_id IS NOT NULL AND employee_id IS NULL AND position_id IS NULL) OR
(scope = 4 AND employee_id IS NULL AND position_id IS NULL AND department_id IS NULL)
```

Indexes:
* `ux_schedule_assignments_scope` UNIQUE **NULLS NOT DISTINCT**
  (`scope`, `employee_id`, `position_id`, `department_id`, `effective_from`)
  WHERE `deleted_at IS NULL`. PostgreSQL 15+ syntax; we run 18.6. Without
  `NULLS NOT DISTINCT`, two identical "default from 2026-01-01" rows would
  both be allowed and the winner would depend on row order.
* `ix_schedule_assignments_lookup` (`scope`, `effective_from`).

**Why three FK columns and not one polymorphic `scope_id`.** A single id
column cannot have a foreign key, so a deleted position would leave an
assignment pointing at nothing and it would silently stop matching. Three
nullable FKs plus the check constraint give real referential integrity at the
cost of two extra columns.

**Why the position+department *pair* tier was dropped from the proposed
shape.** The owner described "per department / position, with a per-employee
override and a system default" — four tiers. A fifth "this position **in**
that department" tier doubles the precedence table and has no requested use
case. If it is ever needed it becomes `scope = 5` with both ids non-null and
one more branch in the resolver.

Precedence is in §5.2.

### 2.6 `attendance_days` — the computed daily result

Not `IAuditable`, not `ISoftDeletable`: nobody writes it by hand, the job
rewrites it, and audit columns answer "who changed this" — a question with no
meaning here. `computed_at` replaces them.

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | uuid | no | PK |
| `employee_id` | uuid | no | FK → `employees`, `Restrict` |
| `local_date` | date | no | Tashkent calendar date the shift is attributed to |
| `employment_id` | uuid | yes | the primary employment used (FK, `Restrict`) |
| `legal_entity_id` | uuid | yes | snapshot — reports stay stable after a transfer |
| `department_id` | uuid | yes | snapshot |
| `position_id` | uuid | yes | snapshot |
| `work_schedule_id` | uuid | yes | resolved schedule (FK, `Restrict`); NULL ⇒ none |
| `plan_start_at` | timestamptz | yes | the actual planned start instant for this date |
| `plan_end_at` | timestamptz | yes | may fall on the next calendar day |
| `first_in_at` | timestamptz | yes | |
| `last_out_at` | timestamptz | yes | |
| `planned_minutes` | int | no | `(plan_end − plan_start) − break`; 0 when no schedule |
| `worked_minutes` | int | no | |
| `late_minutes` | int | no | |
| `early_leave_minutes` | int | no | |
| `overtime_minutes` | int | no | computed, never priced (§1.2 #2) |
| `pass_count` | int | no | passes attributed to this day |
| `ignored_pass_count` | int | no | bounces dropped by §5.5 step 2 |
| `break_taken_minutes` | int | no | **AMENDED 2026-09-17** — `breakTaken` of §5.5.1; default `0` |
| `break_deduction_minutes` | int | no | **AMENDED 2026-09-17** — `breakDeduction` of §5.5.1, already subtracted from `worked_minutes`; default `0` |
| `status` | int | no | `AttendanceDayStatus` |
| `computed_at` | timestamptz | no | |

**The two break columns — AMENDED 2026-09-17.** Mirrored on
`AttendanceDayFacts` as `BreakTakenMinutes` / `BreakDeductionMinutes`. They are
**stored**, not recomputed when read, because the timesheet popover (§8.6) must
explain the number that was actually computed — the schedule and its break
window can change after the day was computed. `presentInWindow` (§5.5.1) is
**not** stored: it is an intermediate of the deduction and nothing displays it.
Both columns arrive in the second module migration (§2.12).

Indexes:
* `ux_attendance_days_employee_date` UNIQUE (`employee_id`, `local_date`).
* `ix_attendance_days_date_status` (`local_date`, `status`).
* `ix_attendance_days_department_date` (`department_id`, `local_date`).
* `ix_attendance_days_legal_entity_date` (`legal_entity_id`, `local_date`).

**A day belongs to an employee, not to an employment.** The turnstile records
a person; one physical presence cannot be split between two legal entities,
and in this holding one person genuinely holds two employments
(`Employee` doc comment: *"VIKOR + Вело унион 0,5 ставка ўриндош"*). The
employment used for the schedule, the legal entity, the department and the
position is the **primary** one (§5.1).

### 2.7 Enums — every value enumerated

```
AttendanceProvider          : Hikvision = 1, Generic = 2
AttendanceIdentityProvider  : Verifix = 1, Hikvision = 2, ZkTeco = 3, Manual = 4
DeviceDirectionRole         : ReportsDirection = 1, EntryOnly = 2, ExitOnly = 3
DeviceAuthMode              : Token = 1, TokenAndHmac = 2
IdentitySource              : Import = 1, Manual = 2
PassDirection               : In = 1, Out = 2, Unknown = 3
DirectionSource             : Device = 1, DeviceRole = 2, Unknown = 3
PassMethod                  : Face = 1, Card = 2, Fingerprint = 3, Password = 4,
                              Other = 5, Unknown = 6
WorkScheduleKind            : Weekly = 1, Cycle = 2
ScheduleScope               : Employee = 1, Position = 2, Department = 3, Default = 4
AttendanceDayStatus         : Present = 1, Absent = 2, DayOff = 3, DayOffWorked = 4,
                              MissingExit = 5, MissingEntry = 6, NoSchedule = 7,
                              NotEmployed = 8
IdentityMatchKind (DTO only): ExactPinfl = 1, ExactName = 2, Ambiguous = 3,
                              None = 4, AlreadyMapped = 5
ExceptionKind (query only)  : Late = 1, EarlyLeave = 2, Absent = 3, Missing = 4
```

`AttendanceProvider` (which **parser**) and `AttendanceIdentityProvider`
(whose **ids**) are deliberately two enums. A ZKTeco terminal provisioned from
the same Verifix list needs a new parser but must resolve against the existing
identities; collapsing them into one enum would force a duplicate mapping of
every employee.

`AttendanceDayStatus.DayOff` and `.NotEmployed` are rarely stored — they are
mostly **synthesized by the timesheet query** for dates that have no row
(§5.7.1). They are in the enum because the grid renders them.

### 2.8 `AttendanceRules` — domain constants

`api/src/Bonvi.Domain/Attendance/AttendanceRules.cs`, modelled on
`NotificationRules`.

| Constant | Value | Why |
|---|---|---|
| `BounceSeconds` | 60 | two reads of the same direction within a minute are one person, one door. **AMENDED 2026-09-27:** now used only by the phone check-in (a second mark within a minute is refused); the day computation drops every known-direction repeat regardless of time (§5.5 step 2) |
| `ClockSkewToleranceSeconds` | 300 | 5 min; below this a device clock is "fine" |
| `MaxClockSkewDays` | 30 | beyond this the timestamp is garbage, not skew — reject (§3.8) |
| `DayWindowPreHours` | 6 | how early before `plan_start` a pass still counts for that day |
| `DayWindowPostHours` | 6 | how late after `plan_end` |
| `BoardLookbackHours` | 18 | a pass older than this no longer means "inside" |
| `DefaultGraceInMinutes` | 10 | |
| `DefaultGraceOutMinutes` | 10 | |
| `DefaultPhotoRetentionDays` | 60 | owner's default; also the settings default |
| `MaxImportRows` | 5000 | |
| `MaxRecomputeDays` | 92 | one quarter per manual recompute request |
| `MaxIngestBodyBytes` | 2 097 152 | 2 MB; a face snapshot is 20–80 KB |
| `DefaultPassRetentionDays` | 365 | **AMENDED 2026-09-17** — owner's default for `attendance.passRetentionDays`; also the fallback when the settings row is absent |
| `FormerEmployeeAlertHours` | 24 | **AMENDED 2026-09-17** — how far back the board looks for a terminated-employee pass (§6.5) |

**Only the two retention values become `SystemSetting`s** (§2.9) —
**AMENDED 2026-09-17**: the original SPEC had one, the owner added
`attendance.passRetentionDays`. The rest stay constants: each setting costs a
row on the settings screen, a definition, a validation range and four
translations, and none of the others has been asked for. Promoting a constant
later is a one-line change; that note goes in `AttendanceRules`' doc comment.

⚠️ Both `Default…RetentionDays` constants must stay numerically identical to
the defaults in `SystemSettingKeys` — the jobs fall back to the constant when
the settings row was never written, so a divergence means the retention
silently changes the first time someone opens and saves the settings screen.
`SystemSettingKeys` should reference the constants directly once
`AttendanceRules` exists (B2's comment already flags this as a temporary
duplication).

### 2.9 New system settings — AMENDED 2026-09-17 (owner's answer to §13 #3)

**Two** keys, both added to `SystemSettingKeys.All` (shared file —
sequential, §10.1), both in one `DAVOMAT` group placed after the notification
group:

```
attendance.photoRetentionDays  Number  default 60   Min 7   Max 365
attendance.passRetentionDays   Number  default 365  Min 30  Max 3650   (0 = never delete)
```

> ⚠️ The second key is **new in this amendment**. The original SPEC kept every
> pass row forever (§1.2 #10) and expired only photos. The owner decided the
> raw archive ages out on a policy too.

| | `photoRetentionDays` | `passRetentionDays` |
|---|---|---|
| deletes | the JPEG file + `raw_event`, keeping the pass row | the **whole pass row**, and its photo file with it |
| default | 60 days | 365 days |
| range | 7…365 | 30…3650, plus the sentinel `0` |
| `0` allowed? | **no** — `Min 7`, so a typo cannot wipe today's photos | **yes**, and it means *never delete* |
| touches `attendance_days`? | no | **no** — see below |

**`0` means "never", and only on the pass key.** The two keys need opposite
protection. A photo retention of `0` would read naturally as "keep nothing"
and delete this morning's snapshots — exactly when they are needed, because
"bu men emasman" is argued the same week. A pass retention of `0` is the
opposite request: "do not ever throw the archive away", which is a legitimate
policy an auditor may impose. The asymmetry is deliberate and must be in both
doc comments, because a reader who sees `0` in one place will assume it works
in the other.

**Ordering invariant.** `passRetentionDays` must be ≥ `photoRetentionDays`
whenever it is not `0` — deleting the row before the photo would strand the
file (the purge finds photos *through* pass rows). This cannot be a
`SystemSettingDefinition` range because it spans two keys, so it is enforced
in the settings command handler: saving a smaller value returns
`davomat.saqlash_muddati_nomuvofiq` (400). The nightly job additionally
treats the effective pass retention as `max(pass, photo)` so a database edited
by hand cannot strand files either — belt and braces, because this one leaks
disk silently.

**`attendance_days` rows are never deleted by either key.** The tabel, the
worked hours, the lateness and the absence counts survive the raw evidence.
That is the whole shape of the decision: the *derived record* is permanent,
the *raw evidence* ages out. A month whose passes have expired still reports
correctly; it just can no longer be re-derived from scratch or illustrated
with a photograph. Say this on the settings screen in one sentence
(`attendance.passRetentionHint`), because an HR manager choosing `90` needs to
know it does not erase the tabel.

Both are visible on `/sozlamalar` (`settings.manage`, i.e. super
administrator only), and both must be added to the `GROUPS` list in
`system-settings-page.tsx` — a settings key absent from that list is silently
not drawn.

### 2.10 Repositories and read models

Write side (`Bonvi.Domain.Abstractions`, implemented in
`Bonvi.Infrastructure/Repositories/AttendanceRepositories.cs`, one file, same
shape as `RecruitingRepositories.cs`):

```
IAttendanceDeviceRepository   : IRepository<AttendanceDevice>
    Task<AttendanceDevice?> FindByTokenHashAsync(byte[] hash, CancellationToken)
    Task<IReadOnlyList<AttendanceDevice>> ListAsync(bool includeInactive, CancellationToken)
IAttendanceIdentityRepository : IRepository<AttendanceIdentity>
    Task<AttendanceIdentity?> FindAsync(AttendanceIdentityProvider p, string externalPersonId, CancellationToken)
    Task<IReadOnlyList<AttendanceIdentity>> ListByEmployeeAsync(Guid employeeId, CancellationToken)
IAttendancePassRepository     : IRepository<AttendancePass>
IWorkScheduleRepository       : IRepository<WorkSchedule>   // Include(Days)
IScheduleAssignmentRepository : IRepository<ScheduleAssignment>
IAttendanceDayRepository      : IRepository<AttendanceDay>
```

Read side (`Bonvi.Application/Attendance/Abstractions/IAttendanceReadModel.cs`
+ `IAttendanceImportReadModel.cs`, implemented in
`Bonvi.Infrastructure/Persistence/ReadModels/AttendanceReadModel.cs`):

* `SearchPassesAsync(PassSearchFilter, CancellationToken)` → page of rows
* `GetBoardAsync(BoardFilter, CancellationToken)`
* `GetTimesheetAsync(TimesheetFilter, CancellationToken)`
* `GetExceptionsAsync(ExceptionFilter, CancellationToken)`
* `GetSummaryAsync(SummaryFilter, CancellationToken)`
* `ListUnmatchedAsync(UnmatchedFilter, CancellationToken)`
* `FindEmployeeCandidatesAsync(string fio, string? pinfl, int take, CancellationToken)`
  — lives here because the name-folding helper (`SearchFolding`) is
  `internal` to Infrastructure.

### 2.11 `Employee` / `Employment` changes: **none**

This module reads `Employee` and `Employment` and writes nothing to them. No
new column, no new method, no new index.

`PLAN.md` §8 proposed `Employment.ExternalRef` (Verifix ID) as the phase-3
hook. **It was never built, and this SPEC deliberately does not build it.**
Three reasons:

1. One column holds one id. The owner's requirement is explicit: one employee
   may hold **several** device ids.
2. A column holds no history. Unlinking and relinking must leave a trace.
3. The id belongs to the **person**, not to the contract. A person who is
   rehired gets a new `Employment` and the same face; an `Employment` column
   would lose the mapping at exactly the moment HR needs it.

`attendance_identities` is that hook, done properly. If a builder feels the
urge to add `ExternalRef` "for compatibility" — do not; nothing reads it.

### 2.12 Migration

**One** EF migration, additive only, Uzbek class name per house style:

```
dotnet ef migrations add DavomatModuli
```

`Up` creates, in this order (FK order matters):

1. `work_schedules`
2. `work_schedule_days`
3. `attendance_devices`
4. `attendance_identities`
5. `schedule_assignments`
6. `attendance_passes`
7. `attendance_days`
8. raw SQL for the three `lower(...)` unique indexes and for
   `ux_schedule_assignments_scope` (`NULLS NOT DISTINCT` is not expressible
   through the EF fluent API on EF 10 — write it as `migrationBuilder.Sql`).
9. the check constraints `ck_schedule_assignments_scope` and
   **`ck_work_schedule_days_break_window`** (§2.4.0, **AMENDED 2026-09-17**).

**AMENDED 2026-09-17 — one migration at merge; two on disk until then.**
`break_start` and `break_end` are part of the `work_schedule_days`
`CreateTable` call in `20260917063405_DavomatModuli`, not a follow-up
`AddColumn`: nothing has shipped yet, so there is no deployed table to alter.

A **second** module migration exists on purpose:
`20260917072458_DavomatTanaffusNatijasi` adds
`attendance_days.break_taken_minutes` and `break_deduction_minutes` (§2.6).
It was added while builders were running, because regenerating the first
migration underneath them would have invalidated every unit's database at once.

⚠️ **Do not squash, delete, regenerate or "fix" either file while any builder
is running.** An earlier revision of this paragraph said to squash a second
migration on sight — **that instruction is withdrawn.** The two are
consolidated into one **once, before merge, by the coordinator**, when no unit
is running; `test-migration` (§11.2) and the restored-backup check of §12 are
re-run on the consolidated file. Until then both apply in order and §11.2 #7b
covers the second. The Definition of Done's "exactly one migration" is judged
**after** consolidation, and it stays true. The two retention settings need
**no** migration at all: `system_settings` is key-value by design (§2.9).

`Down` drops them in reverse. No `Down` data loss beyond the module's own
tables.

⚠️ **Read the generated migration before committing** (global rule). It must
contain **zero** `DropColumn`, `DropTable`, `AlterColumn` or `RenameColumn`
touching any pre-existing table. If autogenerate emits one, it is a spurious
diff — delete that statement and record why in the migration's comment.

Hangfire's own tables are **not** part of this migration: `Hangfire.PostgreSql`
creates the `hangfire` schema itself on startup (`PrepareSchemaIfNecessary`).
`test-migration` must expect them to appear outside EF's history table.

### 2.13 The door-control seam (described, not built)

> ⚠️ **AMENDED 2026-09-17 — the owner intends to switch this on later.** This
> is no longer a hypothetical "if it is ever wanted". The owner's position on
> §13 #4 is explicit: *a terminated employee must not get through the
> turnstile.* v1 still only records and flags — the write-back is **not** built
> now, and HR removes the leaver from the device by hand — but the seam below
> is **committed future work, not speculation**. Do not prune it as dead
> design, do not collapse it into `AttendancePass`, and do not let a later
> refactor quietly close it. The flagging that stands in for it in v1 is
> specified in §8.4 and §8.7 and is load-bearing, not decorative.

When "push allow/deny to the device" is turned on, this is where it goes — and
this is what v1 must **not** do so that path stays clean:

* **Do not** put `IsAllowed`, `Decision` or `DoorOpened` on `AttendancePass`.
  A pass records what happened. A decision is a different fact with a
  different lifetime (it is made *before* the pass and may not produce one).
* The outbound direction is a **new port**, mirroring the inbound one:
  `Bonvi.Application/Attendance/Abstractions/IAccessControlPublisher.cs`
  with `Task<Result> PublishAsync(AccessCommand command, CancellationToken)`,
  implemented per provider in
  `Bonvi.Infrastructure/Attendance/Hikvision/HikvisionAccessControlPublisher.cs`.
* The device registry already carries what such an adapter needs to find the
  box (`ip` via `last_seen_ip`, `model`, `serial_number`, `legal_entity_id`)
  and what it needs to talk to it (`hmac_secret` slot, `auth_mode`).
* The rules ("who may pass which door when") would be their own aggregate,
  `AccessRule`, not a column on `WorkSchedule` — a schedule says when someone
  is *expected*, which is not the same as when they are *permitted*.
* **The first rule that will be written against this seam is already known:**
  revoke on termination. That makes `Employment.Status → Terminated` the
  trigger and the device registry the target, which is precisely the pair the
  port above connects. When it is built, the v1 alert of §8.7 does not go
  away — it becomes the check that the revocation actually worked, because a
  device that was offline when the revoke was published will still let the
  person in and nothing else would notice.

---

## 3. Ingest — the public endpoint

### 3.1 Two routes, one handler

```
POST /api/attendance/ingest/{token}     anonymous   — for terminals that cannot set headers
POST /api/attendance/ingest             anonymous   — token in the X-Bonvi-Device-Token header
```

Both map to the same `IngestAttendanceEventCommandHandler`. Hikvision's "HTTP
Listening" lets you configure a URL and nothing else — no custom header, no
body signature — so the path form exists for it. Any adapter or forwarding
agent added later uses the header form, which does not leak into logs.

Both are `.AllowAnonymous()`. This is the **only** anonymous write endpoint in
the system, and it is marked as such in its doc comment, because the global
fallback policy requires authentication (`AuthenticationExtensions.cs:135`)
and "ochiqlikni ataylab e'lon qilish xavfsizroq".

### 3.2 Device authentication

**Token.** 32 random bytes from `RandomNumberGenerator`, base64url-encoded
(43 chars). Shown **once**, at registration or rotation, in the response body
of that call — never again, never in any list. Stored as `SHA-256(token)` in
`token_hash` with a unique index; the first 8 characters are stored in
`token_prefix` so HR can tell two devices apart on screen.

Lookup is `FindByTokenHashAsync(SHA256(presented))` — an index equality probe
on a 32-byte digest. No prefix scan, no per-row comparison loop, no timing
channel worth the name.

*Why SHA-256 and not a password KDF:* the token is 256 bits of machine-chosen
entropy. Argon2/bcrypt exist to slow down guessing of low-entropy human
secrets; here there is nothing to guess, and a KDF would put ~100 ms on the
hot path of every pass.

**HMAC (`auth_mode = TokenAndHmac`).** The request must carry

```
X-Bonvi-Signature: v1=<lowercase hex HMAC-SHA256(raw request body, hmac_secret)>
```

verified with `CryptographicOperations.FixedTimeEquals` over the **raw,
unparsed** body bytes. The secret is stored `ISecretProtector`-encrypted
(`bot.token` precedent). Hikvision firmware cannot produce this, so
`auth_mode` stays `Token` for terminals; the mode exists so that the day a
forwarding agent or a ZKTeco adapter is added, turning signatures on is a
setting, not a redesign. **Creating a device with `TokenAndHmac` and no
secret is rejected** (`davomat.imzo_siri_yoq`).

**IP allow-list (optional).** If `allowed_ips` is non-empty, the source
address must fall inside one of the listed CIDRs or the request is refused.
The source address is taken from `HttpContext.Connection.RemoteIpAddress`
**after** `UseForwardedHeaders` (already first in the pipeline — see the
`Manba(...)` comment in `RateLimitingExtensions.cs`), so behind Caddy it is
the real client, not the proxy. Empty ⇒ no restriction, because the terminals
sit on a dynamic consumer IP today; the field is there for the day a static
one exists.

**Failure responses — deliberately indistinguishable:**

| Situation | Status | Code |
|---|---|---|
| no token / unknown token / bad HMAC / IP not allowed | **401** | `davomat.ingest_ruxsat_yoq` |
| token valid, device `is_active = false` | **403** | `davomat.qurilma_faolsiz` |
| body unparseable for that provider | **400** | `davomat.hodisa_oqilmadi` |
| timestamp absurd (skew > 30 days) | **400** | `davomat.vaqt_yaroqsiz` |
| accepted (new or duplicate) | **200** | — |

One code for all four authentication failures so a prober cannot learn
*which* check failed. The real reason is logged (`LoggerMessage` source
generator, `EventId 5200+`) with method, path **without the token segment**,
source IP, and the presented token's first 8 chars. The inactive-device case
is deliberately distinguishable — the caller already holds a valid token, and
an installer needs to know the device is switched off rather than broken.

### 3.3 Keeping the token out of the logs

The path form puts a secret in the URL. Three things stop it reaching disk:

1. `SecurityHeadersMiddleware` / the Serilog request logger already run with
   `IncludeQueryInRequestPath = false`; the **path** still gets logged, so
2. `ErrorLogFormatter`'s existing path scrubber gains one rule: a path
   matching `^/api/attendance/ingest/` is rewritten to
   `/api/attendance/ingest/***` before it is written anywhere. This is a
   shared file (§10.1).
3. The endpoint declares `.ExcludeFromDescription()`? **No** — it stays in
   OpenAPI so the contract is discoverable, but the `{token}` route parameter
   is documented as "device ingest token; see the device card". Rejected
   alternative: hiding the endpoint. Security by obscurity buys nothing here
   and breaks `./bonvi gen`.

### 3.4 Request shapes

#### 3.4.1 Hikvision — `multipart/form-data`

The terminal posts a multipart body with two (sometimes three) parts:

| Part | Content-Type | Contents |
|---|---|---|
| `event_log` | `application/json` (sometimes `text/plain`) | the event JSON |
| `Picture` (name varies: `Picture`, the snapshot file name) | `image/jpeg` | the face snapshot |
| `Picture`/`subPicture` (optional) | `image/jpeg` | additional captures — **ignored** |

The JSON part, reduced to what we read:

```json
{
  "ipAddress": "10.0.0.42",
  "macAddress": "xx:xx:xx:xx:xx:xx",
  "dateTime": "2026-09-17T08:03:12+05:00",
  "eventType": "AccessControllerEvent",
  "eventState": "active",
  "AccessControllerEvent": {
    "deviceName": "Kirish-1",
    "majorEventType": 5,
    "subEventType": 75,
    "employeeNoString": "10427",
    "name": "Karimov Aziz",
    "cardNo": "0012345678",
    "serialNo": 88213,
    "currentVerifyMode": "face",
    "attendanceStatus": "checkIn",
    "doorNo": 1,
    "readerNo": 1,
    "picturesNumber": 1
  }
}
```

Field mapping (`HikvisionEventParser`):

| Ours | Theirs | Missing ⇒ |
|---|---|---|
| `RawPersonId` | `AccessControllerEvent.employeeNoString`, else `.cardNo` | reject `davomat.shaxs_raqami_bosh` |
| instant | `dateTime` (ISO 8601, offset optional) | reject `davomat.hodisa_oqilmadi` |
| `Direction` | `attendanceStatus`: `checkIn`/`breakIn`/`overtimeIn` → `In`; `checkOut`/`breakOut`/`overtimeOut` → `Out`; anything else → fall through to the device role | see §3.7 |
| `Method` | `currentVerifyMode` contains `face` → `Face`; `card` → `Card`; `fp`/`finger` → `Fingerprint`; `pw`/`password` → `Password`; empty → `Unknown`; else `Other` | `Unknown` |
| `ExternalEventId` | `serialNo` | fall back to the time-based dedupe key (§3.6) |
| `PhotoKey` | the JPEG part | `null` — the pass is still stored |
| `RawEvent` | the whole JSON part | — |

`deviceName`, `ipAddress`, `macAddress` are **not** used to identify the
device — only the token is. Reason: they are attacker-controlled strings in
the body.

**Events we drop before storing:** `eventType != "AccessControllerEvent"`, or
`majorEventType = 5` with a `subEventType` in the known "failed / denied"
family. Dropped events return **200** with `{"ok":true,"stored":0}` and
increment nothing. Reason: the terminal also posts door alarms, tamper events
and heartbeat pings; storing them as "passes" would put phantom people on the
board. ⚠️ The exact `subEventType` numbers differ per firmware — the parser
keeps a **named, commented allow-list** of the pass sub-types it accepts
(`75` face verified, `38` fingerprint verified, `1` card by default — *AMENDED 2026-09-17:* `76` is **face verification failed** in Hikvision's minor-event table, not fingerprint, and is dropped as a failed attempt; an earlier revision had it wrong) and an
`AcceptUnknownSubTypes` constant set to `true` for v1, so an unfamiliar
firmware records passes rather than silently dropping everybody's attendance.
Logged at `Information` the first time each unknown sub-type is seen.

#### 3.4.2 Plain JSON — `application/json`

For the header-authenticated route, an agent, a ZKTeco adapter, and every
test. One event or an array of events, max 200 per request:

```json
{
  "events": [
    {
      "personId": "10427",
      "occurredAt": "2026-09-17T08:03:12+05:00",
      "direction": "In",
      "method": "Face",
      "eventId": "88213",
      "photoBase64": null,
      "deviceName": "Kirish-1"
    }
  ]
}
```

`occurredAt` must be ISO 8601. `direction` / `method` are enum **names**
(global `JsonStringEnumConverter`); absent ⇒ `Unknown`. `photoBase64` is
optional and capped by the 2 MB body limit. A bare object (no `events`
wrapper) is also accepted and treated as a one-element array — Postel's law
applies to devices, not to us, and this costs three lines.

**AMENDED 2026-09-27 — optional `minor`.** The device agent forwards the
Hikvision minor event type it read from the terminal log (`"minor": 75`).
An event whose `minor` is in the denied family of §3.4.1 (6 no right, 7
invalid period, 10 anti-sneak, 76 face verify failed, …) is **dropped**, not
stored — the same list the Hikvision parser uses, compiled from one file
(`HikvisionEventTypes`) into both the server and the agent. The agent already
drops them; the server check is the second line for an older agent. Absent or
non-numeric `minor` ⇒ the event is read as before. The request header
`X-Bonvi-Terminal-Serial` (agent only) binds the token to a physical terminal —
see SPEC-agent §L.

#### 3.4.3 Which parser runs

Chosen by the **device's** `provider` column, never by sniffing the body.
Sniffing would let the caller pick the parser, and a parser is an attack
surface. A `Generic` device that posts multipart gets
`davomat.hodisa_oqilmadi`.

### 3.5 The device-agnostic seam

```
Bonvi.Application/Attendance/Ingest/
  IAttendanceEventParser.cs      // one per provider
  HikvisionEventParser.cs
  JsonEventParser.cs
  RawAttendanceEvent.cs          // the normalized record every parser produces
  IngestAttendanceEventCommand.cs
```

```csharp
public interface IAttendanceEventParser
{
    AttendanceProvider Provider { get; }

    Task<Result<IReadOnlyList<RawAttendanceEvent>>> ParseAsync(
        AttendanceIngestPayload payload, CancellationToken cancellationToken);
}

public sealed record RawAttendanceEvent(
    string RawPersonId,
    DateTimeOffset OccurredAt,
    bool OffsetAssumed,
    PassDirection Direction,
    DirectionSource DirectionSource,
    PassMethod Method,
    string? ExternalEventId,
    byte[]? PhotoBytes,
    string? PhotoContentType,
    string? RawEventJson);
```

`AttendanceIngestPayload` is a **transport-neutral** record built in the Api
layer (`byte[] Body`, `string ContentType`, `IReadOnlyList<IngestFilePart> Parts`)
so the Application layer never sees `HttpRequest` — the architecture test
enforces that. Parsers are registered as `IAttendanceEventParser` and selected
with `parsers.Single(p => p.Provider == device.Provider)`.

Adding ZKTeco later = one enum value + one parser class + one DI line. No
change to the endpoint, the command handler, the schema or the UI.

### 3.6 Idempotency and dedupe

Devices retry. A replay must be **harmless**, not merely tolerated.

**Natural key:** `(device_id, dedupe_key)`, enforced by
`ux_attendance_passes_device_dedupe`.

`dedupe_key` is built by the command handler, never by the parser:

```
if ExternalEventId is present:   "s:" + ExternalEventId
otherwise:                       "t:" + RawPersonId + "|"
                                      + OccurredAt.ToUniversalTime().ToString("yyyyMMddTHHmmss")
                                      + "|" + (int)Direction
```

*Why two forms.* Hikvision's `serialNo` is a per-device monotonic counter and
is the ideal key — a retransmission carries the same one. Firmware that omits
it leaves only the tuple that physically identifies the event: same person,
same second, same direction, same device. Truncating to whole seconds is
deliberate: some firmware re-sends with millisecond jitter.

**Write path:** insert, and on `PostgresErrorCodes.UniqueViolation` for that
index treat it as a duplicate. The check goes through a new method on the
existing `IDatabaseFault` port —
`bool IsDuplicatePass(Exception exception)` — so Npgsql knowledge stays in
Infrastructure (`PostgresFault.cs` precedent). Rejected alternative: a
`SELECT` before the `INSERT`. It does not actually prevent the race, it just
usually hides it, and it adds a round trip to the hottest path in the module.

**Response** is 200 either way:

```json
{ "ok": true, "stored": 1, "duplicates": 0 }
```

A duplicate does **not** touch `last_event_at`, does **not** re-save the
photo (the second photo is discarded before the DB write is attempted — see
the ordering note below), and does **not** mark the day dirty.

⚠️ **Ordering:** the photo is written to `IFileStorage` *before* the row
insert, because the row needs the key. On a duplicate, the just-written file
is deleted again in the same request. A crash between the two leaves an
orphan file; the purge job (§3.10) also sweeps files under
`attendance/**` that no pass references and that are older than 24 h.

### 3.7 Direction

Priority, first one that yields a value wins:

1. **Device says so** — the parser produced `In`/`Out` →
   `direction_source = Device`.
2. **Device role** — `direction_role = EntryOnly` ⇒ `In`;
   `ExitOnly` ⇒ `Out` → `direction_source = DeviceRole`.
3. Otherwise `Unknown` / `direction_source = Unknown`. The timesheet
   alternates them (§5.5 step 3) **without writing back**.

A device configured `ReportsDirection` that keeps sending `Unknown` shows a
warning on its card: "qurilma yo‘nalishni bermayapti" with the count over the
last 7 days. That is the signal to switch its role to `EntryOnly`/`ExitOnly`.

**AMENDED 2026-09-27 — this warning is WITHDRAWN and not built.** Every
terminal now reaches Bonvi through the device agent, and the agent sends
`Unknown` *by design* (SPEC-agent H8: the terminal gives no `attendanceStatus`;
the server alternates In/Out). On the device form the role is labelled
«Navbat bilan» (alternating), not «terminal aytadi». The rule above would be a
permanent false warning on every agent device. It is recorded in code on
`DeviceDirectionRole.ReportsDirection`. If a push terminal that really reports
direction is ever added, the warning must be limited to devices with no agent
(`agent_seen_at IS NULL`).

### 3.8 Time — where the conversion happens, and how a wrong clock is caught

* The device sends **local** time, usually with an offset
  (`2026-09-17T08:03:12+05:00`).
* Conversion happens in **exactly one place**: the parser produces a
  `DateTimeOffset`; the command handler stores
  `occurred_at = value.ToUniversalTime()`,
  `device_local_time = value.DateTime`,
  `device_offset_minutes = value.Offset.TotalMinutes`,
  `local_date = value.TashkentDate()` (the existing `BonviTime` extension).
* **No offset in the payload** ⇒ Tashkent (`BonviTime.TashkentOffset`) is
  assumed and `offset_assumed = true`. Rejected alternative: assuming UTC.
  The terminals are in Qo‘qon; assuming UTC would shift every pass by five
  hours and the error would look like "everyone arrives at 3 a.m."
* Nothing downstream converts again. The API returns UTC instants; the
  frontend renders them through the existing `TASHKENT_TIME_ZONE` helper.
  Three conversion sites is how a system ends up five hours out
  (`BonviTime` doc comment).

**Skew detection.** `clock_skew_seconds = received_at − occurred_at`, stored
on every pass and copied to the device's `last_skew_seconds`.

| \|skew\| | Behaviour |
|---|---|
| ≤ 300 s (`ClockSkewToleranceSeconds`) | normal |
| > 300 s, ≤ 30 days | **stored and used**, flagged in the pass log, device card shows a red "soat noto‘g‘ri" badge, live board shows a banner if any active device is in this state within the last 24 h |
| > 30 days (`MaxClockSkewDays`) | **rejected**, 400 `davomat.vaqt_yaroqsiz`, logged with the claimed value |

**AMENDED 2026-09-27 — one measure everywhere.** For an agent device
`received − occurred` also contains the delivery delay: after an internet
outage every buffered pass looked «soat adashgan» and the board showed the
clock banner. The verdict now follows `AttendanceDevice.HasClockDrift`
everywhere — the agent's direct measurement (`clock_offset_seconds`) when it
exists, `received − occurred` only for push terminals that are never measured:

* each pass stores the device's measured offset at ingest
  (`attendance_passes.clock_offset_seconds`); the pass log flag, the photo
  dialog and the Excel «Soat adashgan» column read
  `AttendancePass.EffectiveClockSkewSeconds` (the DTO's `clockSkewSeconds` is
  that value);
* the board's `clockWarningDeviceCount`, the devices-page banner and the
  Statistika banner all count `AttendanceDevice.ClockWarningAt(now)` (active,
  seen in the last 24 h, `HasClockDrift`); the devices-page badge shows the
  same number (offset, or `last_skew_seconds` when there is no offset), so the
  banner never counts a device whose row shows «—».

*Why flagged data is still used.* A terminal five minutes fast is still
telling the truth about who walked through; discarding it would put people on
the "Absent" report for a clock problem. A terminal claiming 1970 or 2106 is
not skewed, it is broken, and letting it write would scatter
`attendance_days` rows across meaningless dates.

### 3.9 Rate limiting and body size

* New named policy `RateLimitPolicies.Ingest`, partitioned by **source IP**
  (the token is not yet known at the point the partition is chosen), fixed
  window **300 requests / 60 s**. Sizing: 5 400 passes/day peaks near 4–6
  per second at shift change; 300/min is ~50× the sustained rate and still
  bounds an abuser.
* `/api/attendance/ingest` is added to `RateLimitingOptions.ExcludedPaths` so
  the **global** 120/min-per-IP sliding window does not also apply. Without
  this, a site whose terminals share one NAT address would throttle itself at
  shift change. Both changes are in one shared file (§10.1).
* `.WithMetadata(new RequestSizeLimitAttribute(AttendanceRules.MaxIngestBodyBytes))`
  — 2 MB. Kestrel refuses a larger body before the handler runs.
* `.DisableAntiforgery()` — required for `multipart`, and there is no session
  cookie to forge (the `EmployeeEndpoints` photo upload carries the same
  comment).
* Max 200 events per request (`davomat.hodisa_kop`, 400).

### 3.10 Photos and retention

Stored through the existing `IFileStorage` (`LocalFileStorage`, volume
`uploads-data`), under the date-sharded key scheme it already uses. **Not**
through `SquarePhotoStore` — a face snapshot is evidence, not an avatar, and
cropping it to a centred square would cut off exactly the part that proves who
walked through. **Not** through `DocumentFileStore` either: that pipeline is
built around a user-supplied filename and content-type sniffing for arbitrary
document types. The ingest path validates narrowly instead:

* content type must be `image/jpeg` or `image/png`, verified by
  `IFileContentInspector` on the bytes, not by the part's declared header;
* ≤ 1 MB per image (inside the 2 MB body cap);
* anything else ⇒ the photo is dropped, the **pass is still stored**, and
  `photo_key` stays null. A pass without a picture is still attendance.

**Purge job** `attendance:purge-photos`, daily 03:30 Tashkent (`30 22 * * *`
UTC). For passes with `received_at < today − attendance.photoRetentionDays`
and `photo_key IS NOT NULL`: delete the file, set `photo_key = NULL`,
`raw_event = NULL`, `photo_purged_at = now()`. Batches of 2 000, loops until
done or 5 minutes elapse. Also deletes orphan files older than 24 h (§3.6).

`raw_event` is purged with the photo because it contains the person's name as
provisioned in the device — the same personal data, with the same expiry.

**AMENDED 2026-09-17 — this is now the *first* of two expiries.** This job
expires the photo and keeps the pass row; a second job,
`attendance:purge-passes` (§9.2.1), later deletes the row itself on
`attendance.passRetentionDays`. Read them together: the row-level job also
deletes any photo still attached, so a pass can never outlive its file and a
file can never outlive its pass. Neither job ever touches `attendance_days`.

---

## 4. Identity resolution, unmatched passes, and the Verifix import

### 4.1 Resolution at ingest

For each parsed event the handler calls
`IAttendanceIdentityRepository.FindAsync(device.IdentityProvider, rawPersonId)`.

* Found ⇒ `resolved_employee_id`, `resolved_at = now`.
* Not found ⇒ **the pass is stored anyway** with `resolved_employee_id = NULL`.
  It is never dropped, never rejected, and never merged into an "unknown
  person" bucket. The raw id and the photo are exactly what HR needs to work
  out who it was.

Identity lookups are cached in-process for 60 s (`IMemoryCache`, key
`att:id:{provider}:{rawPersonId}`) — at 5 400 passes/day across ~540 ids the
same handful of rows is read constantly. The cache entry is evicted when an
identity is created or removed.

### 4.2 Unmatched passes are counted and visible

`GET /api/attendance/identities/unmatched` groups
`attendance_passes WHERE resolved_employee_id IS NULL` by
(`identity_provider`, `raw_person_id`) and returns, per group:

```
provider, rawPersonId, passCount, firstSeenAt, lastSeenAt,
lastDeviceId, lastDeviceName, samplePassId (newest pass that has a photo)
```

The screen shows the sample photo, so HR maps a face, not a number. A red
count badge sits on the nav item whenever any group exists — the same badge
mechanism `NavItem.count` already provides.

### 4.3 Retroactive attach — the rule that makes "never dropped" mean something

When an identity is created (manually or by import confirm), the handler
enqueues `AttendanceBackfillJob.Run(provider, rawPersonId, employeeId)`:

1. `UPDATE attendance_passes SET resolved_employee_id = @employee,
    resolved_at = now(), processed_at = NULL
    WHERE identity_provider = @provider AND raw_person_id = @rawId
      AND resolved_employee_id IS NULL`
2. Setting `processed_at = NULL` puts every affected day back into the dirty
   queue, so the recompute job rebuilds the tabel for those dates
   automatically. No separate "recompute after mapping" step, and no way to
   forget it.
3. The job is idempotent and safe to run twice.

**Deleting** an identity does the mirror image: it does **not** un-resolve
past passes. Reason: the passes really were that person; erasing the link
would silently rewrite the tabel for months already reported. If the mapping
was genuinely wrong, HR deletes the identity, creates the right one, and runs
a manual recompute over the affected range (§6.7) — an explicit, dated,
auditable act.

### 4.4 The Verifix import

Two calls, no server-side session.

**`POST /api/attendance/identities/import`** — `multipart/form-data`, field
`file`, `.xlsx` or `.csv`, ≤ 10 MB, ≤ `MaxImportRows` (5 000) data rows.
Parses and matches. **Writes nothing.** Returns a preview.

*Header detection* follows the ETL's `AliasTable` technique: scan the first 10
rows for a cell matching a known alias (case-insensitive, folded).

| Field | Aliases (extend freely; these are the seed list) |
|---|---|
| person id | `employeeNo`, `Employee No`, `Employee ID`, `Person ID`, `ID`, `Табельный номер`, `Персональный номер`, `Ходим рақами`, `Xodim raqami`, `Kod` |
| FIO | `ФИО`, `Ф.И.О.`, `F.I.Sh.`, `FIO`, `Name`, `Ходим`, `Xodim`, `Сотрудник` |
| PINFL (optional) | `ЖШШИР`, `JSHSHIR`, `ПИНФЛ`, `PINFL` |

No id column ⇒ 400 `davomat.import_ustun_topilmadi`. No FIO column is
tolerated (matching falls back to PINFL only, or produces `None`).

*Matching*, per row, first rule that fires:

1. `(provider, personId)` already mapped ⇒ `AlreadyMapped` (with the current
   employee's name).
2. PINFL present and equal to exactly one `Employee.Pinfl` ⇒ `ExactPinfl`.
3. Folded FIO matches exactly one employee ⇒ `ExactName`. Folding uses the
   existing `SearchFolding` rules (Cyrillic `қ/ғ/ҳ/ў` homoglyphs, Latin/Cyrillic
   look-alikes) — which is why this runs in the read model (§2.10).
4. Folded FIO matches 2…5 employees ⇒ `Ambiguous`, with the candidates.
5. Otherwise ⇒ `None`.

Preview row DTO:

```
rowNumber, rawPersonId, fullNameInFile, pinflInFile,
matchKind, suggestedEmployeeId, suggestedEmployeeName,
candidates: [ { employeeId, fullName, pinfl, department, position, photoKey } ]
```

**`POST /api/attendance/identities/import/confirm`** — JSON body, the rows the
operator approved:

```json
{ "provider": "Verifix",
  "rows": [ { "rawPersonId": "10427", "employeeId": "…", "displayNameInDevice": "Karimov Aziz" } ] }
```

Creates identities with `source = Import`, skips rows whose person id is
already mapped (reported back, not an error), enqueues one backfill job per
created identity, and returns `{ created, skipped, failed: [ { rawPersonId, code } ] }`.

*Why the preview is not held server-side.* There is no session store for it,
the file is small, and a server-side draft would need its own table, its own
expiry and its own cleanup job. The client holds the preview and posts back
only the decisions — at 5 000 rows that is roughly 300 KB.

---

## 5. Schedules and the timesheet computation

This section is **normative**. The algorithm must be implemented exactly as
written, because the tabel is the number HR argues with people about.

### 5.1 Which employment a day belongs to

For employee `E` on date `D`, the **primary employment** is:

1. the employment with `is_primary = true` whose `[hire_date, termination_date]`
   covers `D`;
2. if none is flagged primary, the covering employment with the earliest
   `hire_date`;
3. if two tie, the one with the smaller `id` (deterministic, never random).

Its `legal_entity_id`, `department_id`, `position_id` are snapshotted into the
`attendance_days` row. If **no** employment covers `D`, the employment fields
are NULL and the status is `NotEmployed`.

**AMENDED 2026-09-27 — department and position AS OF `D`.** `Employment.Transfer`
overwrites `department_id` / `position_id` in place; the effective date lives
only on the `EmploymentEvent`. The computation therefore derives both values
for `D` from the employment's `Transfer` / `Promotion` events
(`EmploymentPlacement`), per field and only from events that actually changed
that field: no such event ⇒ the current value; `D` on or after the last change
⇒ the current value; otherwise the "to" value of the latest change effective on
or before `D`, or — before the first change — its "from" value (current value
when an imported event left it blank). The effective day itself already
belongs to the new placement. That **one** value feeds both the schedule tiers
(§5.2) and the snapshot, so a pre-transfer day keeps the old schedule and the
old department in reports even when recomputed after the transfer, and a
forward-dated transfer does not apply early. Every loader that hands
employments to the computation includes their events.

### 5.2 Schedule resolution — precedence

Evaluated for `E` on `D` using the primary employment. Only assignments with
`effective_from <= D` and (`effective_to IS NULL` or `effective_to >= D`) are
considered. First tier that yields a match wins; within a tier, the latest
`effective_from` wins, ties broken by the latest `created_at`.

| # | Tier | Match |
|---|---|---|
| 1 | Employee | `scope = Employee AND employee_id = E` |
| 2 | Position | `scope = Position AND position_id = ` the employment's position **as of `D`** (§5.1, AMENDED 2026-09-27) |
| 3 | Department | `scope = Department AND department_id ∈ {`the employment's department **as of `D`**`, its ancestors}`, **nearest ancestor first** |
| 4 | Default | `scope = Default` |
| 5 | — | none ⇒ `AttendanceDayStatus.NoSchedule` |

*Why Position outranks Department.* A guard works the guard shift whichever
department he is attached to; a department is a reporting line, a position is
what you actually do. Reversing the two would give every guard the office
9–18.

*The department walk* climbs `Department.ParentId`. The tree is ~73 nodes, so
the recompute job loads all departments once per batch into a dictionary and
walks in memory. `Department.Path` is a **name** path and must not be used for
matching.

### 5.3 Which day a pass belongs to

For each candidate date `D ∈ {P.local_date − 1, P.local_date, P.local_date + 1}`
with a resolved schedule, the day window is

```
[ plan_start(D) − DayWindowPreHours , plan_end(D) + DayWindowPostHours ]
```

where `plan_end(D)` is on `D + 1` when the schedule day crosses midnight
(§2.4.1). A pass is attributed to the date whose window contains it; if two
windows contain it, the one whose **centre** is nearest wins; if still tied,
the earlier date wins.

With **no** schedule, the pass is attributed to its own `local_date`.

**AMENDED 2026-09-27 (owner's decision) — the late exit of yesterday's open
shift.** A pass that falls in **no** window normally stays on its own
`local_date`. Exception: it becomes the exit of the previous day `P =
local_date − 1` when **all** hold: (1) its direction is not `In`; (2) `P` is a
working day whose window ended before the pass; (3) today's window
(`plan_start − DayWindowPreHours`) has not started yet — if today is not a
working day, the bound is `P`'s window start + 24 h ("as if today started at
yesterday's time"), so an evening pass on a day off never glues to yesterday;
(4) `P` ends with an **open** `In` (after steps 2–3 of §5.5). Example:
09:00–18:00, in Monday 09:00, out Tuesday 00:20 — Monday is 09:00–00:20,
Tuesday is clean (it used to be Monday "Chiqish yo'q" and Tuesday 00:20 / 08:55
/ 18:05 = in, out, in). Late passes are examined in time order, so a second
one does not re-open a day the first one closed. For determinism under the ±1
day loading contract, "open" is judged only from `P`'s window passes whose
`local_date ≥ P` plus late exits already attached to `P` — a result must not
depend on which range is being recomputed, or one pass could be counted on
two stored days. Every recompute path already covers ±1 day (the dirty queue,
the nightly range).

*Why ±1 day and ±6 hours.* A night shift starting 20:00 on Monday ends 08:00
Tuesday; both its passes must land on Monday. Six hours of slack covers early
arrivals and long overtime without letting a 14:00 pass be claimed by
yesterday's night shift.

### 5.4 Which (employee, date) rows exist at all

An `attendance_days` row is created when **either**:

* (a) at least one pass is attributed to that (employee, date), **or**
* (b) the employee has an employment covering that date **and** a resolved
  schedule marks that weekday as working.

Everything else is synthesized by the timesheet query (§5.7.1), so the table
stays proportional to reality rather than to the calendar.

**AMENDED 2026-09-21 / 2026-09-27 — a shift that has not started is not a
row.** A working day with no passes whose plan has **not started yet** — a
future date, or today before `plan_start` (the very rule of the daily panel's
"Hali boshlanmagan", `DashboardStatusRule.HasNotStarted`, no grace) — gets the
status `Planned` and **no row**; an approved absence request still makes it
`Excused` and stored. Before this, the 03:10 nightly run stored today's 09:00
shift as `Absent` for everyone, and a 20:00 night-shift worker was "Kelmadi" all
day in the timesheet, the employee app and the reports while the panel said
"not started". Once the plan starts, the `attendance:started-shifts` job (§9.2,
every 15 min) inserts the missing `Absent` rows for yesterday and today — it
only inserts, never rewrites.

### 5.5 The pairing algorithm — normative

Input: the passes attributed to (`E`, `D`), ordered by `occurred_at`
ascending, ties broken by `id`.

1. **Stamp directions.** `Unknown` stays `Unknown` for now.
2. **Collapse repeats — AMENDED 2026-09-27 (owner's decision).** Walk the
   list; a pass with a **known** direction that equals the previous *kept*
   pass's direction is a repeat, **whatever the time between them** (the old
   60-second window is gone): with separate entry and exit terminals two
   entries can never be back-to-back. **First-In / Last-Out:** a repeated `In`
   is dropped (the earliest entry stays); a repeated `Out` replaces the
   previous kept `Out` (the latest exit stays). Count the drops in
   `ignored_pass_count`; the passes stay in the log.
   `Unknown` never collapses anything and is never collapsed — on a single
   shared terminal two reads 20 s apart stay "in, out" (owner, same day,
   deliberately). A mix of known and `Unknown` directions (phone "Kirish",
   then the shared terminal) keeps the pre-amendment behaviour — an open
   question logged in `docs/ASSUMPTIONS.md` (2026-09-27).
3. **Infer unknown directions by alternation.** If any direction is
   `Unknown`, assign directions left to right: the first event of the day is
   `In`, then alternate `Out`, `In`, `Out`… **Known directions are never
   overwritten**; alternation only fills gaps, restarting its parity from the
   last known value. Nothing is written back to `attendance_passes` (§2.3).
4. **Build intervals.** Walk the list:
   * an `In` opens an interval;
   * an `Out` closes the open interval;
   * an `Out` with nothing open is an **orphan exit** ⇒ `MissingEntry = true`;
     it does not contribute minutes;
   * an interval still open at the end is an **open interval** ⇒
     `MissingExit = true`; it does not contribute minutes.
5. **`worked_minutes`** — **AMENDED 2026-09-17** =
   `max(0, sum(closed intervals) − breakDeduction)`, in whole minutes,
   truncated (not rounded) — a 7 h 59 m 30 s day is 479 minutes, and rounding
   up would invent half a minute nobody worked. `breakDeduction` is defined in
   §5.5.1 and is **0** whenever the schedule day has no break window.
   Truncate the interval sum **first**, then subtract, so the deduction is a
   whole number of minutes against a whole number of minutes. **AMENDED
   2026-09-17:** the `max(0, …)` stays as a guard but **cannot fire** —
   `breakDeduction` never exceeds the closed-interval sum (§5.5.1).
6. **`planned_minutes`** = `(plan_end − plan_start) − break_minutes`; `0` when
   there is no schedule or the day is non-working. **Unchanged by the
   amendment** — and that is the point: plan and fact now exclude the break on
   the same terms, so `worked_minutes` and `planned_minutes` stay directly
   comparable and `overtime_minutes` (step 10) keeps meaning what it says.
7. **`first_in_at`** = the first `In`; **`last_out_at`** = the last `Out`.
8. **`late_minutes`** = `max(0, first_in_at − (plan_start + grace_in_minutes))`.
   `0` when there is no schedule, the day is non-working, or `first_in_at` is
   null.
9. **`early_leave_minutes`** = `max(0, (plan_end − grace_out_minutes) − last_out_at)`.
   `0` when `MissingExit` — if we do not know when he left we do not accuse
   him of leaving early.
   *AMENDED 2026-09-23 (owner):* also `0` while `now < plan_end` (UTC instant;
   a night shift's plan ends the next day). Mid-day, the last `Out` is often
   just lunch — someone who stepped out at 11:30 was shown "380 min early
   leave". Once `plan_end` has passed the formula applies unchanged. A row
   written before `plan_end` keeps `0` until the next recompute; with no new
   pass that is the nightly job (§9.2), which covers every date `D` on `D`,
   `D + 1` and `D + 2` — and every plan of `D` ends before `D + 2` 00:00.
10. **`overtime_minutes`** = `max(0, worked_minutes − planned_minutes)` **when a
    schedule was resolved** (a working day *or* a day off); **`0` when no
    schedule was resolved** (`NoSchedule`, `NotEmployed`).
    *AMENDED 2026-09-17 (coordinator):* with `planned_minutes = 0` the bare
    formula made every worked minute of an unscheduled day "overtime", and the
    summary report's overtime column was inflated by exactly the days whose
    norm is unknown. Work on a scheduled day off still counts in full — that
    one has a plan, and the plan is zero.

#### 5.5.1 The break deduction — AMENDED 2026-09-17 (owner's answer to §13 #1)

> ⚠️ **This subsection replaces the original rule and reverses it.** The first
> version said "`break_minutes` reduces `planned_minutes` and nothing else".
> That is no longer true. The break **is** deducted from `worked_minutes` —
> but only the part the employee did **not** actually take, and only when the
> schedule day defines a window (§2.4.0) against which "actually took it" can
> be measured.
>
> The spirit of the old rule survives intact: **nobody is punished twice for
> the same lunch.** What changed is that this is now *checked* instead of
> *assumed*. The old version protected the honest case by giving up on the
> dishonest one; the window lets us handle both.

**Definitions.** All times are instants on the day's own timeline — the same
timeline `plan_start_at` / `plan_end_at` live on.

**Resolving the window into instants — normative, AMENDED 2026-09-17
(correction).** An earlier revision of this paragraph said "§2.4.0.1 already
resolved the window into `breakWindowStart` / `breakWindowEnd` as real
instants". **That was false**: §2.4.0.1 defines *validation* offsets and never
produces an instant, so the engine was left without a formula. The formula is:

```
plan_start_at    = D + start_time, in Tashkent (+05:00), stored as UTC   // §5.3
breakWindowStart = plan_start_at + (break_start − start_time)
breakWindowEnd   = plan_start_at + (break_end   − start_time)
```

`break_start − start_time` is **`TimeOnly` subtraction**, which wraps modulo
24 h and always lies in `[0, 24 h)` — the same projection
`WorkScheduleDay.ValidateBreakWindow` already uses, so validation and
computation cannot disagree about where a window is.

⚠️ **Do not compose the calendar date with the raw clock value**
(`D.ToDateTime(break_start)`, `D + break_start`, or anything equivalent). For a
20:00→08:00 shift on Monday with a 00:30→01:30 window that yields **Monday
00:30** — nineteen and a half hours *before* the shift starts — instead of
**Tuesday 00:30**. No outside span can ever overlap it, so `breakTaken = 0` and
**every night-shift worker is silently charged the full break every night**.
Day shifts are unaffected by the mistake, which is exactly why it would pass
casual testing. §11.1 #61 pins it.

* **`outsideSpans`** — the periods the employee was demonstrably outside the
  building: the gaps **between consecutive closed intervals**. An `Out` at
  12:58 followed by an `In` at 13:44 is one 46-minute outside span.
* **`breakTaken`** = the total overlap, in whole minutes, between
  `outsideSpans` and `[breakWindowStart, breakWindowEnd]`.
* **`presentInWindow`** — **AMENDED 2026-09-17 (§13 #14 decided)** = the total
  overlap, in whole minutes, between the **closed intervals** and
  `[breakWindowStart, breakWindowEnd]`: how much of the window the employee
  actually spent inside the building. An open interval (`MissingExit`)
  contributes nothing here, exactly as it contributes nothing to the sum.
* **`breakDeduction`** = `max(0, min(break_minutes − breakTaken, presentInWindow))`.
  **AMENDED 2026-09-17** — this supersedes `max(0, break_minutes − breakTaken)`.
  The cap is the point: nobody can be charged a break for window time he was
  not in the building.

**Truncation — exact, AMENDED 2026-09-17.** Compute the closed-interval sum,
`breakTaken` and `presentInWindow` from the exact instants, in seconds, and
truncate each **once** at the end (`floor(totalSeconds / 60)`), never interval
by interval. Truncating per interval on one side and the total on the other
can make `presentInWindow` a minute larger than the closed sum, and the
invariant below would then fail by one minute on some days.

**The deduction can never exceed the minutes worked — AMENDED 2026-09-17.**
`breakDeduction ≤ presentInWindow ≤ closed sum`: the first by the `min`, the
second because an interval's overlap with the window can never be longer than
the interval itself. So `worked_minutes = closed sum − breakDeduction` is never
negative, and the floor in §5.5 step 5 is a guard that cannot fire. This is not
an arithmetic nicety; it is the rule. A deduction larger than the time worked
would be a charge for a lunch during hours the employee was not in the
building — the one outcome "nobody is punished twice" must rule out.

**The tail after `last_out_at` does not count.** If the day's last interval
closes before the window ends, the remaining time is **not** an outside span
and contributes nothing to `breakTaken`.

**Neither does the head before `first_in_at`** — **AMENDED 2026-09-17**, stated
explicitly because rows 7–9 below depend on it. Someone who arrives at 13:30
was not on a break from 13:00 to 13:30; he had not arrived yet. Both ends of the
day follow the same rule: only the gaps **between** closed intervals are
outside spans.

*Which reading this adopts, and why.* Two readings were available: treat the
time after the final exit as "outside the building, therefore break", or treat
it as "the working day ended". **This SPEC adopts the second.** A person whose
last pass of the day is inside or before the window went home; he did not take
a long lunch. Under the first reading, going home at 13:45 on a 13:00–14:00
window with a 30-minute break would credit 15 minutes of "break" and cut the
deduction from 30 to 15 — going home early would make the number better. That
is not a rounding difference, it is a perverse incentive, and it is the one
thing the deduction must not do. The early departure is already accounted for:
`early_leave_minutes` is where it belongs, and the closed-interval sum is short
by the same hours.

*Since the §13 #14 cap* (**AMENDED 2026-09-17**) the two readings give the same
deduction whenever `break_minutes` equals the window's length — including the
old 12:55 example, which now deducts 0 under either reading, because he was
present for none of the window. They still differ when the break is shorter
than the window, as in the 13:45 example, so the rule and its reason stay.

**When the window is NULL: no deduction at all.** `breakDeduction = 0`,
regardless of `break_minutes`. A duration without a window cannot be
attributed to any part of the day — there is no way to tell whether a 13:10
exit was lunch or an errand, and no way to tell whether someone who never left
was working or eating at his desk. Guessing is precisely what the owner
rejected; the honest answer is to deduct nothing and to make the window the
thing HR fills in when it wants the deduction. Existing schedules created
before this amendment therefore keep behaving exactly as the original SPEC
described, with no data migration and no silent change to anyone's hours.

**Worked examples**, on a 09:00–18:00 shift with `break_minutes = 60` and a
window of 13:00–14:00:

| # | Passes | `breakTaken` | `presentInWindow` | `breakDeduction` | closed sum | `worked_minutes` |
|---|---|---|---|---|---|---|
| 1 | 09:00 in, 18:00 out (never left) | 0 | 60 | 60 | 540 | **480** |
| 2 | 09:00 in, 13:00 out, 14:00 in, 18:00 out | 60 | 0 | 0 | 480 | **480** |
| 3 | 09:00 in, 13:00 out, 13:30 in, 18:00 out | 30 | 30 | 30 | 510 | **480** |
| 4 | 09:00 in, 12:30 out, 14:00 in, 18:00 out | 60 | 0 | 0 | 450 | **450** |
| 5 | 09:00 in, 10:00 out, 10:30 in, 18:00 out | 0 | 60 | 60 | 510 | **450** |
| 6 | 09:00 in, 12:55 out (went home) — **AMENDED** | 0 | 0 | **0** | 235 | **235** |
| 7 | 13:30 in, 18:00 out (arrived mid-window) — **AMENDED** | 0 | 30 | **30** | 270 | **240** |
| 8 | 09:00 in, 13:15 out, never returned — **AMENDED** | 0 | 15 | **15** | 255 | **240** |
| 9 | 09:00 in, 18:00 out, **`break_minutes = 30`**, same window — **AMENDED** | 0 | 60 | **30** | 540 | **510** |

Row 4 is the case the "no negative deduction" floor exists for: he was out for
90 minutes, 60 of them inside the window, and `max(0, min(60 − 60, 0))` stops there —
the extra 30 minutes are already missing from the closed-interval sum and are
not charged a second time. Row 5 is the smoke-break case: an hour outside the
window earns no break credit, so the full 60 is still deducted **and** the
hour is already gone from the sum. Both are the same principle from opposite
ends: time is counted exactly once.

**Rows 6–9 — AMENDED 2026-09-17 (§13 #14 decided).** Row 6 used to deduct 60
and leave 175 minutes, and an earlier revision of this SPEC defended that as
"harsh on purpose — do not fix it". **That note is withdrawn; row 6 now deducts
0.** He left at 12:55 and was in the building for none of the 13:00–14:00
window, so charging him a lunch there contradicts the rule this whole
subsection rests on. Rows 7 and 8 are the same principle at the two partial
edges: present for 30 and for 15 minutes of the window, charged 30 and 15.
Row 9 shows the cap is a ceiling, not a replacement: present for the whole
window, he is charged the 30-minute break, not the 60-minute window.

**`MissingExit` interacts with this** — see §5.7. The open final interval
contributes no minutes, so it also opens no outside span and adds nothing to
`presentInWindow`; the deduction is computed from whatever closed intervals
exist.

**Stored — AMENDED 2026-09-17.** `breakTaken` and `breakDeduction` are
persisted as `attendance_days.break_taken_minutes` and
`break_deduction_minutes` (§2.6); `presentInWindow` is not.

### 5.6 Status — precedence, first match wins

| # | Status | Condition |
|---|---|---|
| 1 | `NotEmployed` | no employment covers `D` (row exists only because passes arrived) |
| 2 | `NoSchedule` | no schedule resolved |
| 3 | `DayOffWorked` | schedule day is non-working **and** `pass_count > 0` |
| 4 | `Absent` | working day, employment active, `pass_count = 0` |
| 5 | `MissingEntry` | an orphan exit was found |
| 6 | `MissingExit` | an interval was left open |
| 7 | `Present` | everything else |

`late_minutes` and `early_leave_minutes` stay **separate numeric fields**, not
statuses. A person can be late *and* leave early *and* be `Present`;
collapsing that into one enum would lose a number HR reports on.

`DayOff` never appears on a stored row (rule 5.4 means such a row is not
created); it is produced by the timesheet query (§5.7.1).

### 5.7 Edge cases — the normative table

| Situation | Result |
|---|---|
| odd number of passes, last is `In` | `MissingExit`, `worked_minutes` counts only the closed intervals |
| odd number of passes, first is `Out` | `MissingEntry`, same |
| even number, correctly alternating | `Present` (plus late/early figures) |
| 10 passes in a day (lunch, smoke breaks) | 5 closed intervals, summed; this is the normal case, not an anomaly |
| two passes 20 s apart, same **known** direction | **AMENDED 2026-09-27:** one event, `ignored_pass_count = 1` — for `In` the first is kept, for `Out` the **last** (§5.5 step 2) |
| two known `In` (or two `Out`) in a row, any time apart | **AMENDED 2026-09-27:** same rule — First-In / Last-Out; no longer `MissingExit` / `MissingEntry` |
| two `Unknown` reads 20 s apart (single shared terminal) | **not** merged — alternation makes them in, out (owner's decision 2026-09-27); `09:00:00, 09:00:20, 18:00` ⇒ `worked = 0`, `MissingExit` |
| pass in no day window after yesterday's open `In`, before today's window | **AMENDED 2026-09-27:** yesterday's exit (§5.3) |
| today, working day, no passes, before `plan_start` | **AMENDED 2026-09-27:** `Planned`, no row; `Absent` from `plan_start` (§5.4) |
| department / position transfer effective on `T` | days before `T` keep the old schedule tier and snapshot, `T` onward the new (§5.1) |
| overnight shift 20:00→08:00 | one `attendance_days` row on the **start** date; `plan_end_at` is on `D+1`; both passes attributed to `D` (§5.3) |
| overnight shift, employee leaves at 08:05 | `Present`, `overtime_minutes = 5` |
| no schedule resolved, passes exist | `NoSchedule`; `worked_minutes` still computed from the pairs; `planned/late/early = 0` |
| no schedule, no passes | no row at all; the tabel cell renders "—" |
| terminated employee, no passes | no row (rule b fails); tabel shows `NotEmployed` for dates after `termination_date` |
| terminated employee, badge still works and he passes | row created by rule (a), status `NotEmployed` — **exactly the anomaly HR must see** |
| employee hired mid-month | dates before `hire_date` render `NotEmployed`; no rows |
| day off per schedule, no passes | no row; the tabel cell renders `DayOff` |
| day off per schedule, passes exist | `DayOffWorked`, `worked_minutes` counted, `late/early = 0` |
| employee with two employments | one row; schedule and org snapshot from the **primary** employment (§5.1) |
| pass with `clock_skew > 5 min` | included normally; the log row and the device card carry the warning flag |
| unmatched pass | no `attendance_days` row at all (no employee to attribute it to); it appears only in the pass log and the unmatched screen — until the mapping lands and the backfill recomputes (§4.3) |

**Break window — AMENDED 2026-09-17.** All rows below assume a 09:00–18:00
shift, `break_minutes = 60`, window 13:00–14:00 unless stated.

| Situation | Result |
|---|---|
| schedule day has **no window** (`break_start`/`break_end` NULL) | `breakDeduction = 0` — no deduction at all, whatever `break_minutes` says (§5.5.1). This is the pre-amendment behaviour and it is the default for every schedule that existed before this change |
| window set, employee **never left** (09:00 in, 18:00 out) | `breakTaken = 0`, `presentInWindow = 60`, `breakDeduction = 60`, `worked_minutes = 480` — the full break is charged: there is no evidence he took it, and he was in the building for the whole window |
| window set, out 13:00 → in 14:00 (took exactly the break) | `breakTaken = 60`, `breakDeduction = 0`, `worked_minutes = 480` — **identical to the previous row.** Pin this pair in a test: it is the whole point of the amendment |
| out 12:30 → in 14:00 (left longer than the break) | `breakTaken = 60` (only the in-window overlap), `presentInWindow = 0`, `breakDeduction = max(0, min(60 − 60, 0)) = 0`. **Never negative** — the extra 30 minutes are already absent from the closed-interval sum and are not credited back |
| out 10:00 → in 10:30 (left outside the window) | `breakTaken = 0`, `breakDeduction = 60`. The half hour is gone from the sum *and* the break is still charged — the two are separate facts |
| two short exits inside the window (13:05→13:20, 13:40→13:55) | overlaps add up: `breakTaken = 30`, `breakDeduction = 30` |
| exit straddling the window edge (12:45 → 13:25) | only the in-window part counts: `breakTaken = 25`, `breakDeduction = 35` |
| `MissingExit` with a window (09:00 in, 13:00 out, 14:00 in, no exit) | **AMENDED 2026-09-17 — corrected.** Only 09:00–13:00 is a *closed* interval, so there is no gap **between closed intervals**: `breakTaken = 0`. An earlier revision said 60, contradicting its own definition in §5.5.1. The closed interval only touches the window at 13:00, so `presentInWindow = 0` and `breakDeduction = 0`; `worked_minutes = 240`, status `MissingExit` |
| `MissingExit` before the window (09:00 in, no exit) | no closed interval, `worked_minutes = 0`, `breakDeduction` is irrelevant because the floor at 0 holds; status `MissingExit` |
| went home before the window (09:00 in, 12:55 out) | **AMENDED 2026-09-17 (§13 #14).** `breakTaken = 0`, `presentInWindow = 0`, **`breakDeduction = 0`**, `worked_minutes = 235`. Was 60 / 175 — withdrawn: he was in the building for none of the window |
| arrived mid-window (13:30 in, 18:00 out) | **AMENDED.** `breakTaken = 0` (the head before `first_in_at` is not an outside span), `presentInWindow = 30`, **`breakDeduction = min(60, 30) = 30`**, `worked_minutes = 240` |
| left inside the window, never returned (09:00 in, 13:15 out) | **AMENDED.** `breakTaken = 0` (the tail is not an outside span), `presentInWindow = 15`, **`breakDeduction = min(60, 15) = 15`**, `worked_minutes = 240` |
| 60-minute window, **`break_minutes = 30`**, never left (09:00 in, 18:00 out) | **AMENDED.** `breakTaken = 0`, `presentInWindow = 60`, **`breakDeduction = min(30, 60) = 30`**, `worked_minutes = 510` — the cap limits the charge to the time present; it never raises it above the break |
| overnight shift 20:00→08:00, window 00:30→01:30, out 00:35 → in 01:10 | window resolved to **Tue 00:30 → Tue 01:30** by the §5.5.1 formula (never Mon 00:30); `breakTaken = 35`, `breakDeduction = 25`; still **one** row, on the start date |
| `DayOffWorked` (non-working day, passes exist) | the day has no window and no `break_minutes` (rule: both NULL when `is_working = false`), so `breakDeduction = 0`; `worked_minutes` is the raw closed-interval sum |
| `NoSchedule` with passes | no schedule ⇒ no window ⇒ `breakDeduction = 0`; unchanged from the original SPEC |

### 5.7.1 Synthesizing the missing cells — normative

The tabel must show **every** day of the month for every listed employee, but
§5.4 deliberately stores rows only where something happened. The timesheet
query therefore fills the gaps. For each (employee `E`, date `D`) with **no**
stored row, produce a cell with `workedMinutes = 0`, all minute fields `0`,
`firstInAt`/`lastOutAt` null, and this status:

1. no employment covers `D` → `NotEmployed`
2. an employment covers `D` but no schedule resolves → `NoSchedule`
3. the schedule's weekday for `D` is non-working → `DayOff`
4. otherwise → `Absent`

Case 4 can only happen if the recompute job has not run yet for that date
(rule (b) of §5.4 would have stored an `Absent` row). Producing `Absent`
rather than a blank is the safe direction: a gap that silently renders as "—"
hides a person who did not come in.

⚠️ This synthesis happens **in the query, once, in memory** — after the stored
rows and the resolved schedules for the range are loaded. Do not issue a
per-cell query: 540 employees × 31 days is 16 740 cells.

### 5.8 The four legitimate repairs

Because a day cannot be hand-edited (§1.2 #5), these are the only ways to fix
a wrong tabel, and the UI says so on the timesheet screen:

1. create or correct the **identity mapping** (auto-recomputes),
2. correct the **schedule or assignment** (then recompute the range) —
   **AMENDED 2026-09-17**: this now includes setting or clearing the **break
   window** (§2.4.0). It is the first thing to check when someone says "my
   hours dropped by an hour": a window was added, so the break is now deducted
   on days he does not leave the building (§5.5.1). The fix is a schedule
   edit plus `POST /recompute` for the affected range, never a hand edit,
3. fix the **device clock** and re-send (or accept the flagged data),
4. register the **missing device** so its passes stop being refused.

⚠️ **AMENDED 2026-09-17 — a fifth thing that looks like a repair and is not:**
lowering `attendance.passRetentionDays` does **not** fix or change a tabel.
Deleting raw passes leaves every `attendance_days` row exactly as it was
(§9.2.1). Anyone reaching for the retention setting to make a number go away
has misunderstood what it does, and the settings hint (§8.9) says so.

---

## 6. API contract

Base path `/api/attendance`, tag `Attendance`. Paged endpoints use the
existing convention: query params `page` (1-based), `pageSize`, `sort`, and a
`PagedResult<T>` body — `{ items, page, pageSize, total, pageCount }`.
`DefaultPageSize = 25`, `MaxPageSize = 200` (mirrors `SearchEmployeesQuery`).

Date/period filters are `from`/`to` as `DateOnly`, **inclusive at both ends**
(the `ListIncidents` convention). A period longer than
`AttendanceRules.MaxRecomputeDays` (92) on any report ⇒ 400 `davomat.davr_uzun`.
`from > to` ⇒ 400 `davomat.davr_notogri`.

### 6.1 Ingest

| Method | Path | Auth | Body | 200 |
|---|---|---|---|---|
| POST | `/ingest/{token}` | anonymous, device token in path | multipart or JSON | `{ ok, stored, duplicates }` |
| POST | `/ingest` | anonymous, `X-Bonvi-Device-Token` header | multipart or JSON | same |

Errors: §3.2 table. The response **never** contains an employee name, an id,
or anything about who the person was. A leaked token must not become a
building-occupancy oracle.

### 6.2 Devices — `attendance.manage`

| Method | Path | Notes |
|---|---|---|
| GET | `/devices` | `?includeInactive=false`. Returns `AttendanceDeviceDto[]` (no token, ever — only `tokenPrefix`) with two computed fields: `passCount24h` = accepted passes with `received_at >= now − 24 h`, and `skewWarning` = `abs(last_skew_seconds) > AttendanceRules.ClockSkewToleranceSeconds` |
| GET | `/devices/{id}` | |
| POST | `/devices` | Returns `AttendanceDeviceCreatedDto` = the device **plus `token`** — the only time the token is ever returned |
| PUT | `/devices/{id}` | name, model, serial, provider, identityProvider, directionRole, legalEntityId, departmentId, locationNote, authMode, allowedIps, hmacSecret (write-only; `null` = leave, `""` = clear) |
| POST | `/devices/{id}/rotate-token` | Returns the new token once. Old token dies immediately |
| POST | `/devices/{id}/activate` \| `/deactivate` | |
| DELETE | `/devices/{id}` | Soft delete. Refused with `davomat.qurilma_otishlari_bor` if the device has passes — deactivate it instead, so the log keeps its origin |

**Response shapes — AMENDED 2026-09-17** (F1 review; names follow the client,
which already typed against them).

`AttendanceDeviceDto`:

| Field | Type | Notes |
|---|---|---|
| `id` | uuid | |
| `name` | string | |
| `serialNumber`, `model` | string? | |
| `provider` | `AttendanceProvider` | |
| `identityProvider` | `AttendanceIdentityProvider` | |
| `directionRole` | `DeviceDirectionRole` | |
| `legalEntityId`, `legalEntityName` | uuid?, string? | name joined from the catalogue when read, not snapshotted |
| `departmentId`, `departmentName` | uuid?, string? | same |
| `locationNote` | string? | |
| `authMode` | `DeviceAuthMode` | |
| `hasHmacSecret` | bool | `hmac_secret IS NOT NULL`. The secret is write-only and is never returned in any form, not even masked |
| `allowedIps` | string? | |
| `tokenPrefix` | string | first 8 characters — never the token |
| `tokenIssuedAt` | instant | |
| `isActive` | bool | |
| `lastSeenAt`, `lastEventAt` | instant? | |
| `lastSeenIp` | string? | |
| `lastSkewSeconds` | int? | |
| `passCount24h`, `skewWarning` | int, bool | computed — definitions in the table above |

`AttendanceDeviceCreatedDto` = **`{ device: AttendanceDeviceDto, token: string }`**,
**nested**, returned by `POST /devices` **and** `POST /devices/{id}/rotate-token`.
The table above says "the device plus `token`", which could be read as one flat
object; it is nested, so the device stays one reusable type and the one-time
secret is visibly separate from it. `PUT /devices/{id}`, `/activate` and
`/deactivate` return `AttendanceDeviceDto`.

### 6.3 Identities — `attendance.manage`

| Method | Path | Notes |
|---|---|---|
| GET | `/identities` | paged; `?search=` (person id or employee), `?employeeId=`, `?provider=` |
| POST | `/identities` | `{ provider, externalPersonId, employeeId, displayNameInDevice?, note? }` → enqueues backfill |
| DELETE | `/identities/{id}` | soft delete; past passes keep their resolution (§4.3) |
| GET | `/identities/unmatched` | §4.2; paged, sorted by `passCount` desc |
| POST | `/identities/import` | multipart `file`; preview only |
| POST | `/identities/import/confirm` | §4.4 |

**Response shapes — AMENDED 2026-09-17.**

`AttendanceIdentityDto` = `{ id, provider, externalPersonId, employeeId,
employeeName, photoKey, displayNameInDevice, source, note, createdAt }` —
`photoKey` is the **employee's** profile photo (module-wide convention, §6.5).
`UnmatchedGroupDto` is exactly the field list of §4.2.

`POST /identities/import` takes multipart `file` plus an **optional** form
field `provider` (`AttendanceIdentityProvider` name, default `Verifix`). A
client that sends only the file gets `Verifix`, the only list that exists today.

`IdentityImportPreviewDto` = `{ provider, totalRows, rows }`:

* `provider` — the id space the preview was matched against, echoed back; the
  confirm call must send the same value;
* `totalRows` — data rows read from the file, header and fully blank rows
  excluded. The preview returns **every** row, so `rows.length === totalRows`;
  the field lets the dialog say «312 qator o‘qildi» before it renders the table;
* `rows` — the per-row shape of §4.4, unchanged.

### 6.4 Schedules

| Method | Path | Permission |
|---|---|---|
| GET | `/schedules` | `attendance.view` |
| GET | `/schedules/{id}` | `attendance.view` |
| POST | `/schedules` | `attendance.manage` |
| PUT | `/schedules/{id}` | `attendance.manage` |
| DELETE | `/schedules/{id}` | `attendance.manage` — refused (`davomat.jadval_biriktirilgan`) while any live assignment points at it |
| GET | `/schedule-assignments` | `attendance.view`; `?scope=`, `?employeeId=`, `?departmentId=`, `?positionId=`, `?onDate=` |
| POST | `/schedule-assignments` | `attendance.manage` |
| DELETE | `/schedule-assignments/{id}` | `attendance.manage` |
| GET | `/schedules/resolve` | `attendance.view`; `?employeeId=&date=` → the winning schedule **and the tier that won**. This is the "why does he have this schedule" answer, and it is why support tickets will not need a developer |

**Response shapes — AMENDED 2026-09-17.**

`ScheduleAssignmentDto` = `{ id, workScheduleId, workScheduleName, scope,
employeeId, employeeName, positionId, positionName, departmentId,
departmentName, effectiveFrom, effectiveTo }`. Exactly the id/name pair that
matches `scope` is non-null — all three are null for `Default` — mirroring the
check constraint of §2.5. Names are joined when read.

`ScheduleResolutionDto` — the **full** field set of `GET /schedules/resolve`:

| Field | Type | Meaning |
|---|---|---|
| `employeeId`, `employeeName` | uuid, string | |
| `date` | date | the date asked about |
| `employmentId` | uuid? | the primary employment covering `date` (§5.1). **NULL ⇒ not employed on that date**; every field below is then NULL too |
| `legalEntityId`, `legalEntityName` | uuid?, string? | of that employment |
| `departmentId`, `departmentName` | uuid?, string? | of that employment **as of `date`** (§5.1, AMENDED 2026-09-27) — the input to tier 3 |
| `positionId`, `positionName` | uuid?, string? | of that employment **as of `date`** — the input to tier 2 |
| `scope` | `ScheduleScope`? | the tier that won. **NULL with a non-null `employmentId` ⇒ no schedule resolves** |
| `assignmentId` | uuid? | the winning assignment |
| `effectiveFrom`, `effectiveTo` | date?, date? | of the winning assignment |
| `workScheduleId`, `workScheduleName` | uuid?, string? | the resolved schedule |
| `matchedDepartmentId`, `matchedDepartmentName` | uuid?, string? | non-null **only** when `scope = Department`: the department whose assignment won, which may be an **ancestor** of `departmentId` (§5.2). This pair is the whole answer to "why does he have the factory's schedule" |
| `day` | `WorkScheduleDayDto`? | **AMENDED 2026-09-27:** the plan the timesheet actually used for `date`, break window included — the year-grid cell for that date when one exists (`dayIndex` = the date's ISO weekday), otherwise the weekday pattern; NULL when no schedule |
| `daySource` | `ScheduleDaySource`? | **new 2026-09-27:** `Date` — `day` came from the year grid, `Pattern` — from the weekly pattern; NULL with `day` |

The client decides the outcome from these two nulls only, in this order:
`employmentId == null` ⇒ not employed; else `scope == null` ⇒ no schedule;
else resolved.

`POST /schedules` body:

```json
{ "name": "Ofis 9–18", "graceInMinutes": 10, "graceOutMinutes": 10,
  "note": null,
  "days": [ { "dayIndex": 1, "isWorking": true,
              "startTime": "09:00", "endTime": "18:00",
              "breakMinutes": 60,
              "breakStart": "13:00", "breakEnd": "14:00" } ] }
```

All 7 `dayIndex` values must be present exactly once
(`davomat.jadval_kun_takror` / `davomat.jadval_kun_yetishmaydi`).

**`breakStart` / `breakEnd` — AMENDED 2026-09-17.** Both optional, both
`TimeOnly?`, both nullable together (§2.4.0). Omitting them or sending `null`
means "no window" and therefore no break deduction (§5.5.1) — which is the
behaviour every schedule had before this amendment, so an existing client that
does not send the fields keeps working unchanged. A window sent for a day with
`isWorking = false` is **accepted and discarded** (the domain nulls it —
§2.4.0), not rejected. `WorkScheduleDayDto` (response) carries the same two
fields plus a computed `breakCrossesMidnight` convenience flag for the editor,
defined as exactly **`breakEnd < breakStart` compared as raw clock values**
(true for 23:30→00:30, false for 13:00→14:00, false when there is no window).
It is derived, not stored — same reasoning as §2.4.1.

`PUT /schedules/{id}` takes the identical body. Clearing a window is sending
both fields as `null`; sending only one of them is
`davomat.jadval_tanaffus_toliq_emas` (400), never a silent half-update.

### 6.5 Passes and board — `attendance.view`

| Method | Path | Notes |
|---|---|---|
| GET | `/passes` | paged. Filters: `from`, `to` (dates), `employeeId`, `deviceId`, `direction`, `method`, `matched` (`true`/`false`/absent), `legalEntityId`, `departmentId`, `search` (employee FIO / raw person id). Default sort `-occurredAt` |
| GET | `/passes/{id}/photo` | image bytes via `PhotoHttp.Write` (ETag, `private, max-age=86400`). 404 when purged or absent |
| GET | `/board` | who is inside now. Filters: `legalEntityId`, `departmentId`, `search`. Returns `{ insideCount, expectedCount, absentCount, lateCount, clockWarningDeviceCount, items[] }` where each item is `{ employeeId, fullName, photoKey, department, position, sinceAt, lastPassId, lastDeviceName }` |
| GET | `/passes/{id}` | **AMENDED 2026-09-17.** One `AttendancePassDto` (below). 404 `davomat.otish_topilmadi`, including for a pass already removed by the retention job (§9.2.1). The deep-link target of the former-employee alert (§8.4) |
| GET | `/former-employee-alerts` | **AMENDED 2026-09-17.** `{ count, items }` — **only** the former-employee alerts, for the nav badge (§8.7). No parameters, no filters. Same rows as the board's `formerEmployeeAlerts`, from the same read-model method — see below |

**`AttendancePassDto` — AMENDED 2026-09-17**, the item of `GET /passes` and the
body of `GET /passes/{id}`:

```
{ id, deviceId, deviceName, identityProvider, rawPersonId,
  employeeId, employeeName, employeePhotoKey, department, position,
  occurredAt, localDate, receivedAt, clockSkewSeconds, skewWarning,
  offsetAssumed, direction, directionSource, method, photoKey, photoPurgedAt }
```

* `employeeId` is the pass's `resolved_employee_id`. It and `employeeName`,
  `employeePhotoKey`, `department`, `position` are NULL for an unmatched pass.
* `department` / `position` are the names from the primary employment covering
  `localDate` (§5.1); NULL when there is none — which is exactly the leaver case.
* `skewWarning` = `abs(clockSkewSeconds) > AttendanceRules.ClockSkewToleranceSeconds`,
  computed on the server like the device's. **The client must not re-derive it
  from `clockSkewSeconds`**: that would be a second copy of the 300-second
  tolerance, in TypeScript.
* **Photo keys — one convention for the whole module.** `photoKey` means the
  **employee's profile photo** in every DTO of this module (board items,
  alerts, timesheet rows, identities, import candidates) — **except this one**,
  because a pass has a photo of its own: here `photoKey` is the **face
  snapshot** (NULL when the device sent none or the purge removed it) and
  `employeePhotoKey` is the profile photo. The snapshot is always fetched
  through `GET /passes/{id}/photo`, never from a storage path built out of the
  key.

**Board definitions — normative.** "Today" is the Tashkent calendar date of
`IClock.UtcNow`.

| Field | Definition |
|---|---|
| `items` / `insideCount` | employees whose **most recent** pass within `BoardLookbackHours` (18 h) has `direction = In`. `sinceAt` is that pass's `occurred_at` |
| `expectedCount` | employees with an employment covering today **and** a resolved schedule whose weekday is working (i.e. the population of rule (b), §5.4), after the filters |
| `absentCount` | of those expected, the ones with **zero** passes today so far |
| `lateCount` | of those expected, the ones whose `first_in_at` today is later than `plan_start + grace_in_minutes`, **or** who have no pass yet and `now > plan_start + grace_in_minutes` |
| `clockWarningDeviceCount` | active devices whose `last_seen_at` is within 24 h and whose `abs(last_skew_seconds)` exceeds the tolerance |
| `formerEmployeeAlerts` | **AMENDED 2026-09-17** — see below |

**`formerEmployeeAlerts` — AMENDED 2026-09-17 (owner's answer to §13 #4).**
An array, newest first, with **one entry per person** who made a pass in the
last 24 h (`AttendanceRules.FormerEmployeeAlertHours`) with **no employment
covering the pass date** — the same condition that produces
`AttendanceDayStatus.NotEmployed` (§5.6 rule 1), evaluated on the pass rather
than waiting for the nightly job. **AMENDED 2026-09-17 — corrected:** an
earlier revision said "an array of passes", which contradicted §8.4 ("one entry
per person") and the devices-page line «{{n}} xodim». It is per person: the
entry describes that person's **most recent** such pass, and `passCount` says
how many there were. Each entry:

```
{ passId, employeeId, fullName, photoKey, occurredAt, deviceId, deviceName,
  direction, terminationDate, legalEntityName, passCount }
```

`photoKey` is the profile photo; the pass's own snapshot is one click away
through `passId`. `terminationDate` and `legalEntityName` belong to the
employee's most recently terminated employment.

The filters (`legalEntityId`, `departmentId`, `search`) **do not apply** to
this array: a leaver has no current department to filter on, and a security
alert that a departmental filter can hide is not an alert. Say so in the
endpoint's `WithDescription`.

Computed from `attendance_passes` joined to `employments`, not from
`attendance_days` — the point is that it appears **the moment it happens**,
not up to 15 minutes later when the recompute job gets to it. That is the
whole requirement: someone who no longer works here just walked into the
building.

**`GET /former-employee-alerts` — AMENDED 2026-09-17.** Returns
`{ count, items }`: `items` is **exactly** the array above and
`count = items.length` — people, not passes. It exists because the red nav
badge (§8.7) sits on every screen and polls every 30 s; polling the board for
it would run the inside / expected / absent / late aggregation for every HR
user on every screen, twice a minute. `GET /board` and this endpoint call
**one** read-model method (`GetFormerEmployeeAlertsAsync`), so the badge and
the board can never disagree about who walked in.

`lateCount` deliberately counts the not-yet-arrived: at 09:30 "5 kechikdi"
must include the four people who are still not here, or the board answers the
wrong question.

Passes from devices whose direction is `Unknown` are **excluded from the
board** (they still appear in the log and still feed the tabel), and the
screen says so. Alternation (§5.5 step 3) needs a finished day; applying it
at 11:00 would state confidently that someone is inside when he left through
another door.

### 6.6 Reports — `attendance.view`

| Method | Path | Notes |
|---|---|---|
| GET | `/timesheet` | `?year=&month=` (**required**), `legalEntityId`, `departmentId`, `positionId`, `employeeId`, `search`, paged **by employee** (25 rows/page). Returns `{ year, month, days: DateOnly[], page, pageSize, total, pageCount, lastComputedAt, items: [ { employeeId, fullName, photoKey, department, position, cells: [ { date, status, workedMinutes, lateMinutes, earlyLeaveMinutes, breakTakenMinutes, breakDeductionMinutes, firstInAt, lastOutAt } ], totals: { workedMinutes, plannedMinutes, presentDays, absentDays, lateMinutes, earlyLeaveMinutes } } ] }`. **AMENDED 2026-09-17:** `page`/`pageSize`/`total`/`pageCount` are the `PagedResult` fields, counted in employees; `lastComputedAt` = `max(computed_at)` over the stored rows behind the returned page, NULL when there are none; `breakTakenMinutes` / `breakDeductionMinutes` are the §2.6 columns, `0` on synthesized cells. Cells are returned for **every** day of the month, synthesized where no row exists (§5.4) |
| GET | `/reports/exceptions` | `?kind=Late\|EarlyLeave\|Absent\|Missing` (required), `from`, `to`, `legalEntityId`, `departmentId`, `positionId`, `employeeId`, `minMinutes` (Late/EarlyLeave only), paged. Columns: date, employee, department, position, planStartAt, planEndAt, firstInAt, lastOutAt, lateMinutes, earlyLeaveMinutes, workedMinutes, status |
| GET | `/reports/summary` | `from`, `to`, `legalEntityId`, `departmentId`, `positionId`, paged by employee. Columns: employee, department, position, plannedDays, presentDays, absentDays, dayOffWorkedDays, workedMinutes, plannedMinutes, lateMinutes, earlyLeaveMinutes, overtimeMinutes, problemDays (`MissingExit + MissingEntry + NoSchedule`) |

`kind = Missing` selects `MissingExit`, `MissingEntry` **and** `NoSchedule` —
the three "we cannot compute this day" states, which is the list HR actually
chases.

### 6.7 Export and recompute

| Method | Path | Permission | Rate limit | File name |
|---|---|---|---|---|
| GET | `/export/timesheet` | `reports.export` | `Export` | `Bonvi_davomat_tabel_2026-09.xlsx` |
| GET | `/export/passes` | `reports.export` | `Export` | `Bonvi_davomat_otishlar_2026-09-01_2026-09-17.xlsx` |
| GET | `/export/exceptions` | `reports.export` | `Export` | `Bonvi_davomat_chetlanishlar_2026-09-01_2026-09-17.xlsx` |
| GET | `/export/summary` | `reports.export` | `Export` | `Bonvi_davomat_xulosa_2026-09-01_2026-09-17.xlsx` |
| POST | `/recompute` | `attendance.manage` | `Export` | — |

Each export takes the **same query parameters** as the report it mirrors and
ignores `page`/`pageSize`. Excel headers are Uzbek-Latin, hard-coded in the
writer (`EmployeeExcelWriter` precedent: an .xlsx cannot be translated by the
frontend). Dates are **real Excel dates** formatted `dd.mm.yyyy`; durations
are **two columns** — minutes as a number *and* `h:mm` as a text label —
because HR sums the column and a text-only cell makes that impossible, while
a minutes-only cell makes it unreadable.

`POST /recompute` body: `{ "from": "2026-09-01", "to": "2026-09-17",
"employeeId": null }`. Range ≤ 92 days. Enqueues a Hangfire job and returns
`202 Accepted` with `{ jobId, employeeCount, dayCount }`. It is **not**
synchronous: recomputing a month for 540 people is tens of thousands of rows
and must not sit on an HTTP connection.

### 6.8 DTO placement

`Bonvi.Application/Attendance/Dtos/AttendanceDtos.cs` — one file, mirroring
`RecruitingDtos.cs`. It is shared by every backend unit, so it is a
**sequential** file (§10.1).

### 6.9 Error codes — complete list

Uzbek snake_case with the `davomat.` prefix, matching the house convention
(`baxtsiz_hodisa.*`, `mehnat.*`). **Every one needs a translation in all four
locale files** or `./bonvi check` fails.

**Reconciled with the code on 2026-09-17.** The *Source* column says where
each code comes from. **B1 ✓** = already emitted by `Bonvi.Domain/Attendance`
exactly as listed (18 codes, verified by grep); a unit name = not coined yet,
that unit must use this exact string. **B1 ✗** = the code exists but B1 (none remain since 2026-09-17 — §6.9.1)
emits it wrongly — see §6.9.1. The six break-window codes an earlier draft of
this table invented (`…_yarim`, `…_dam_kuni`, `…_nol`, `…_tashqarida`,
`…_davomiyligi_yoq`, `…_uzun`) **do not exist and must not be translated**;
B1's three replace them (§2.4.0).

| Code | HTTP | Meaning | Source |
|---|---|---|---|
| `davomat.ingest_ruxsat_yoq` | 401 | any ingest authentication failure | B3 |
| `davomat.qurilma_faolsiz` | 403 | valid token, device switched off | B3 |
| `davomat.hodisa_oqilmadi` | 400 | payload unparseable for this provider | B3 |
| `davomat.hodisa_kop` | 400 | more than 200 events in one request | B3 |
| `davomat.vaqt_yaroqsiz` | 400 | timestamp more than 30 days from server time | B1 ✓ |
| `davomat.shaxs_raqami_bosh` | 400 | no person id — on a pass (ingest) **or** on an identity being created | B1 ✓ |
| `davomat.otish_topilmadi` | 404 | **AMENDED** — pass id unknown or already removed by the retention job (`GET /passes/{id}`; `GET /passes/{id}/photo` when the **pass** is gone) | B5 |
| `davomat.qurilma_topilmadi` | 404 | device id unknown (management endpoints) | B6 |
| `davomat.qurilma_nom_bosh` | 400 | device name empty | B1 ✓ |
| `davomat.qurilma_nom_takror` | 409 | device name already used | B6 |
| `davomat.qurilma_seriya_takror` | 409 | serial already used | B6 |
| `davomat.qurilma_otishlari_bor` | 409 | cannot delete a device that has passes | B6 |
| `davomat.imzo_siri_yoq` | 400 | `TokenAndHmac` without a secret | B1 ✓ |
| `davomat.moslik_topilmadi` | 404 | identity not found | B6 |
| `davomat.moslik_takror` | 409 | this person id is already mapped | B6 |
| `davomat.xodim_yoq` | 400 | **AMENDED** — employee id not supplied (empty `Guid`) | B1 ✓ — renamed in code 2026-09-17, §6.9.1 |
| `davomat.xodim_topilmadi` | 404 | employee id supplied but no such employee | B6 (`Error.NotFound`) |
| `davomat.import_ustun_topilmadi` | 400 | no person-id column in the file | B6 |
| `davomat.import_fayl_turi` | 400 | not .xlsx / .csv | B6 |
| `davomat.import_fayl_katta` | 400 | file over 10 MB | B6 |
| `davomat.import_qator_kop` | 400 | over 5 000 rows | B6 |
| `davomat.jadval_yoq` | 400 | **AMENDED** — schedule id not supplied (empty `Guid`) | B1 ✓ — renamed in code 2026-09-17, §6.9.1 |
| `davomat.jadval_topilmadi` | 404 | schedule id supplied but no such schedule | B6 (`Error.NotFound`) |
| `davomat.jadval_nom_bosh` | 400 | schedule name empty | B1 ✓ |
| `davomat.jadval_nom_takror` | 409 | schedule name already used | B6 |
| `davomat.jadval_vaqt_bosh` | 400 | working day without start/end | B1 ✓ |
| `davomat.jadval_vaqt_teng` | 400 | start equals end | B1 ✓ |
| `davomat.jadval_yon_berish` | 400 | **AMENDED** — `graceIn`/`graceOut` outside `0 … 120` (`WorkSchedule.MaxGraceMinutes`); coined by B1, missing from the draft | B1 ✓ |
| `davomat.jadval_tanaffus_toliq_emas` | 400 | **AMENDED** — only one of `breakStart`/`breakEnd` sent | B1 ✓ |
| `davomat.jadval_tanaffus_oynasi` | 400 | **AMENDED** — window empty, or not inside the shift (§2.4.0.1) | B1 ✓ |
| `davomat.jadval_tanaffus_uzunligi` | 400 | **AMENDED** — `breakMinutes` outside `0 … 480` (`WorkScheduleDay.MaxBreakMinutes`) | B1 ✓ |
| `davomat.saqlash_muddati_nomuvofiq` | 400 | **AMENDED** — `passRetentionDays` < `photoRetentionDays` (§2.9) | B6 |
| `davomat.jadval_kun_takror` | 400 | duplicate `dayIndex` | B1 ✓ |
| `davomat.jadval_kun_yetishmaydi` | 400 | fewer than 7 days supplied | B1 ✓ |
| `davomat.jadval_turi_qollanmaydi` | 400 | `Cycle` schedules not implemented | B1 ✓ |
| `davomat.jadval_biriktirilgan` | 409 | schedule still assigned | B6 |
| `davomat.biriktirma_topilmadi` | 404 | assignment not found | B6 |
| `davomat.biriktirma_takror` | 409 | same scope + `effectiveFrom` already exists | B6 |
| `davomat.biriktirma_obyekt_yoq` | 400 | scope and target do not match | B1 ✓ |
| `davomat.davr_uzun` | 400 | period longer than 92 days | B5 (reports), B6 (recompute) |
| `davomat.davr_notogri` | 400 | an end date before its start date — `to < from` on a report **or** `effectiveTo < effectiveFrom` on an assignment. One meaning, correctly shared | B1 ✓ (assignments), B5 (reports) |

41 codes: the 33 of the first revision, minus none, plus eight —
`jadval_yon_berish` and the three break-window codes (all coined by B1),
`saqlash_muddati_nomuvofiq`, the two `_yoq` codes of §6.9.1, and
`otish_topilmadi` (**AMENDED 2026-09-17**, for `GET /passes/{id}`).

#### 6.9.1 `xodim_yoq` / `jadval_yoq` — resolved 2026-09-17

**Fixed in code.** `AttendanceIdentity` now emits `davomat.xodim_yoq` for an
empty employee id and `ScheduleAssignment` emits `davomat.jadval_yoq` for an
empty schedule id, both as `Error.Validation` (400). Before the fix both guards
emitted the `_topilmadi` strings, which would have given one code two HTTP
statuses and two meanings once the application layer started emitting the
same strings as 404.

The rule that stays, because it is how the rest of `api/src` already works
(`mehnat.xodim_yoq`, `baxtsiz_hodisa.xodim_yoq` /
`baxtsiz_hodisa.xodim_topilmadi`):

* `*_yoq` — `Error.Validation` (400): the id was **not supplied**;
* `*_topilmadi` — `Error.NotFound` (404): the id was supplied and **does not
  exist**. `davomat.xodim_topilmadi`, `davomat.jadval_topilmadi` and
  `davomat.otish_topilmadi` are emitted by the application layer only, never
  by the domain.

`./bonvi check` cannot enforce this — it only verifies that each emitted code
has a translation — so review has to. W translates all of them.

---

## 7. RBAC

### 7.1 Two new permissions

Added to `Permissions.cs` with a doc comment in the house style, and to
`Permissions.All`:

```csharp
// ---------- Davomat (verifikatsiya) ----------
public const string AttendanceView   = "attendance.view";
public const string AttendanceManage = "attendance.manage";
```

The `AttendanceView` doc comment must state, in Uzbek, why this is a separate
group rather than a slice of `employees.view`: attendance answers *where a
person physically was, minute by minute, with a photograph* — surveillance-grade
data with a different sensitivity from a personnel card, and the same argument
`IncentivesView` and `SafetyView` already make.

`AttendanceManage` covers devices, schedules, assignments, identity mapping,
import and recompute — i.e. everything that changes what the numbers mean.
`AttendanceView` covers boards, log, timesheet, reports.

### 7.2 Role defaults

`SystemRoles.Defaults`:

* `SuperAdmin` — `Permissions.All`, so it picks both up automatically.
* `MainBoss` — `Permissions.All except SettingsManage`, likewise automatic.
* `HrManager` — **both are added explicitly** to the hand-written list, next
  to the safety pair. Attendance is daily HR work: the person who reads the
  tabel is the person who fixes the mapping that produced it, and splitting
  them would mean every unmatched pass waits for the boss.
* `RecruitingBot` — **neither**. The bot's blast radius stays two permissions
  (its doc comment explains why).

### 7.3 Per-endpoint table

| Endpoint | Permission |
|---|---|
| `POST /api/attendance/ingest`, `/ingest/{token}` | **anonymous** — device token (§3.2) |
| `GET /api/attendance/devices`, `/devices/{id}` | `attendance.manage` |
| `POST /api/attendance/devices` | `attendance.manage` |
| `PUT /api/attendance/devices/{id}` | `attendance.manage` |
| `POST /api/attendance/devices/{id}/rotate-token` | `attendance.manage` |
| `POST /api/attendance/devices/{id}/activate` \| `/deactivate` | `attendance.manage` |
| `DELETE /api/attendance/devices/{id}` | `attendance.manage` |
| `GET /api/attendance/identities` | `attendance.manage` |
| `POST /api/attendance/identities` | `attendance.manage` |
| `DELETE /api/attendance/identities/{id}` | `attendance.manage` |
| `GET /api/attendance/identities/unmatched` | `attendance.manage` |
| `POST /api/attendance/identities/import` | `attendance.manage` |
| `POST /api/attendance/identities/import/confirm` | `attendance.manage` |
| `GET /api/attendance/schedules`, `/schedules/{id}`, `/schedules/resolve`, `/schedules/{id}/year` | `attendance.view` |
| `POST`/`PUT`/`DELETE /api/attendance/schedules…` (incl. `PUT …/dates`, `POST …/year/fill`) | `attendance.manage` |
| `GET /api/attendance/schedule-assignments` | `attendance.view` |
| `POST`/`DELETE /api/attendance/schedule-assignments…` | `attendance.manage` |
| `GET /api/attendance/passes` | `attendance.view` |
| `GET /api/attendance/passes/{id}/photo` | `attendance.view` |
| `GET /api/attendance/passes/{id}` | `attendance.view` — **AMENDED 2026-09-17** |
| `GET /api/attendance/former-employee-alerts` | `attendance.view` — **AMENDED 2026-09-17** |
| `GET /api/attendance/board` | `attendance.view` |
| `GET /api/attendance/timesheet` | `attendance.view` |
| `GET /api/attendance/reports/exceptions` | `attendance.view` |
| `GET /api/attendance/reports/summary` | `attendance.view` |
| `GET /api/attendance/export/*` | `reports.export` + `RateLimitPolicies.Export` |
| `POST /api/attendance/recompute` | `attendance.manage` + `RateLimitPolicies.Export` |

**Why devices and identities are `manage`-only, even for reading.** The device
list shows token prefixes, IP allow-lists and the physical layout of the
building's entrances; the identity list is the map from a face-terminal id to
a named person. Neither is something a read-only viewer needs, and both are
exactly what an attacker would want first.

**Exports sit under `reports.export`, not `attendance.view`.** Same reasoning
as `ReportsEndpoints`: an export is the heavy operation that leaves the
building as a file, and the system already has a permission and a rate-limit
policy that mean precisely that.

---

## 8. Frontend

Feature folder `web/src/features/verification/`, routes under
`/verifikatsiya/…`, one new collapsible nav section.

### 8.1 Nav — one new independent section

Inserted in `web/src/app/layout/nav-items.ts` as its **own section**, placed
immediately after the `recruiting` section and before `sectionPeople`
(daily-use ordering, the rule stated in the file's own comment). Same shape as
`sectionCatalog`/`recruiting`: an unlabelled section whose single item is the
group header.

```
{ key: 'verification', to: '/verifikatsiya', icon: ScanFace, children: [
    { key: 'attendanceBoard',     to: '/verifikatsiya/jonli',         icon: Radio,        permission: 'attendance.view' },
    { key: 'attendancePasses',    to: '/verifikatsiya/otishlar',      icon: DoorOpen,     permission: 'attendance.view' },
    { key: 'attendanceTimesheet', to: '/verifikatsiya/tabel',         icon: CalendarDays, permission: 'attendance.view' },
    { key: 'attendanceReports',   to: '/verifikatsiya/hisobotlar',    icon: ChartColumn,  permission: 'attendance.view' },
    { key: 'attendanceMapping',   to: '/verifikatsiya/moslashtirish', icon: Link2,        permission: 'attendance.manage' },
    { key: 'attendanceSchedules', to: '/verifikatsiya/jadvallar',     icon: Clock,        permission: 'attendance.view' },   // AMENDED 2026-09-27
    { key: 'attendanceDevices',   to: '/verifikatsiya/qurilmalar',    icon: Cpu,          permission: 'attendance.manage' },
] }
```

Icons come from `lucide-react` and must be added to the import list at the top
of the file in alphabetical order (the file's existing convention).

The group header `to: '/verifikatsiya'` is **never navigated to** — it only
drives the "are we in this corridor" check, exactly like `sectionCatalog`. No
route file is created for it.

`attendanceMapping` carries the unmatched count via `NavItem.count`, wired in
`sidebar.tsx` from `GET /identities/unmatched` (`total`), rendered only when
> 0 — the same rule the probation badge already follows.

### 8.2 Routes

TanStack file routes, each a thin wrapper around a feature component
(`hodisalar.tsx` pattern: no `beforeLoad` permission gate; the page renders
the "no access" state because the server enforces it anyway).

```
web/src/app/routes/_app/verifikatsiya/jonli.tsx
web/src/app/routes/_app/verifikatsiya/otishlar.tsx        validateSearch: validatePassesSearch   (AMENDED)
web/src/app/routes/_app/verifikatsiya/tabel.tsx           validateSearch: month + list search
web/src/app/routes/_app/verifikatsiya/hisobotlar.tsx      validateSearch: kind + list search
web/src/app/routes/_app/verifikatsiya/moslashtirish.tsx
web/src/app/routes/_app/verifikatsiya/jadvallar.tsx
web/src/app/routes/_app/verifikatsiya/qurilmalar.tsx
```

**AMENDED 2026-09-17 — the pass log does not use `validateListSearch`.** That
validator keeps only the list keys, so it silently drops `passId`, and the
former-employee alert's deep link (`/verifikatsiya/otishlar?passId=…`, §8.4)
would open an ordinary, unfiltered log with no error. The route uses
`validatePassesSearch` from `lib/attendance-search.ts`, which keeps `passId`
alongside the list keys.

### 8.3 Feature folder

```
web/src/features/verification/
  api/attendance-api.ts        // types + axios calls
  api/attendance-queries.ts    // React Query hooks
  components/attendance-board-page.tsx
  components/attendance-passes-page.tsx
  components/attendance-pass-photo-dialog.tsx
  components/attendance-timesheet-page.tsx
  components/attendance-timesheet-grid.tsx
  components/attendance-reports-page.tsx
  components/attendance-mapping-page.tsx
  components/attendance-import-dialog.tsx
  components/attendance-identity-dialog.tsx
  components/attendance-schedules-page.tsx
  components/attendance-schedule-form-dialog.tsx
  components/attendance-assignment-dialog.tsx
  components/attendance-devices-page.tsx
  components/attendance-device-form-dialog.tsx
  components/attendance-device-token-dialog.tsx
  lib/attendance-presentation.ts   // status → colour/label, minutes → h:mm
  lib/attendance-error.ts
  lib/attendance-search.ts
```

**Use the generated client** (`./bonvi gen` → `web/src/shared/api/generated`)
for the types. `PLAN.md`'s technical-debt list item 3 records that 14 of 18
existing API clients are hand-written and that this is exactly how the
`flags`/`activeFlags` bug shipped. A new module has no excuse: hand-write only
the thin call wrappers and take **every DTO type from the generated model**.

### 8.4 Pages

**Jonli (live board)** — `attendance.view`. Top row of four `StatCard`s:
inside / expected / absent / late. Below, a card grid of people inside:
`EmployeePhoto` (real photo — the existing component, initials are not
acceptable), full name, position, "since 08:03" and elapsed time. Filters:
legal entity, department, search. `refetchInterval: 30_000`, and a
"yangilandi HH:mm" line so a stale tab is obvious. A red `Alert` banner when
`clockWarningDeviceCount > 0`.

**The former-employee alert — AMENDED 2026-09-17 (owner's answer to §13 #4).**
When `formerEmployeeAlerts` is non-empty the board renders, **above the four
`StatCard`s and above the clock banner**, a `danger`-toned `Alert` — one entry
per person, newest first:

* title: `attendance.formerEmployeeAlertTitle` — «Ishdan ketgan xodim
  binoga kirdi» (one person) / with `{{n}}` for several;
* per row: the 40 px `EmployeePhoto`, full name, «ishdan ketgan:
  {{date}}», the device name and the pass time in Tashkent, direction, and —
  **AMENDED 2026-09-17** — «24 soatda {{n}} marta» (`formerEmployeePassCount`)
  when `passCount > 1`;
* each row links to that pass in the log (`/verifikatsiya/otishlar?passId=…`)
  and to the employee card;
* a one-line instruction, because the alert is useless without the fix:
  `attendance.formerEmployeeAlertHint` — «Bu odamni turniket qurilmasidan
  o‘chiring — tizim hozircha qurilmaga buyruq yubormaydi» (§2.13).

⚠️ **It is not dismissible and it does not auto-hide.** It clears only when
24 hours pass without such an event, i.e. when the underlying fact stops being
true. A "×" button would let the one alert in this module that means *someone
is in the building who should not be* be silenced in one click by whoever
happens to see it first.

⚠️ **It is the top element of the page, above the counters.** Placement is the
requirement, not a preference: the owner's demand was that this be
load-bearing rather than a status value buried in a table. Under it — visually
below the counters, or only on the pass log — it is exactly the buried status
value the amendment exists to prevent.

This is the only place in the module where `--color-ios-red` is used for a
full-width surface; §8.5's "colour only for meaning" rule is what makes that
legible. Everything else that is merely wrong (clock skew, unmatched passes)
stays orange or a count badge.

**O‘tishlar (pass log)** — `attendance.view`. `DataTable`: 48×48 rounded photo
thumbnail, time (Tashkent), employee (or the raw person id in a muted
monospace badge when unmatched), device, direction chip, method, flags
(skew / assumed offset). Filters: date range, device, direction, method,
matched/unmatched, department, search. Clicking the thumbnail opens the photo
large — consistent with the candidate-files rule already in the codebase.
Export button (`reports.export` only).
**Deep link — AMENDED 2026-09-17:** when `?passId=` is present the page calls
`GET /passes/{id}` and opens that pass's photo dialog directly, whatever page
and filters the list is on; it never pages through the list looking for it. A
404 (`davomat.otish_topilmadi` — the pass aged out) shows the translated error
in the dialog instead of an empty picture.

**Tabel (timesheet)** — `attendance.view`. Month picker + filters; rows are
employees, columns are the days of the month, plus a frozen totals column.
See §8.6 for the density rules.

**Hisobotlar** — `attendance.view`. A `SegmentedControl` with five tabs:
Kechikkanlar / Erta ketganlar / Kelmaganlar / Muammoli kunlar / Xulosa. The
first four hit `/reports/exceptions` with a different `kind`; the fifth hits
`/reports/summary`. Tab lives in the URL (`?kind=`) so a link is shareable.

**Moslashtirish (mapping)** — `attendance.manage`. Two sections: *Moslanmagan
o‘tishlar* (grouped, with the sample photo, a "Xodimga bog‘lash" button
opening an employee picker) and *Mavjud mosliklar* (searchable table with
unlink). A "Verifix ro‘yxatini yuklash" button opens the import dialog:
drop file → preview table with a per-row decision (`Tasdiqlash` /
`Boshqasini tanlash` / `O‘tkazib yuborish`) → "Tasdiqlash (N ta)".
`AlreadyMapped` rows are pre-set to skip; `ExactPinfl`/`ExactName` pre-set to
confirm; `Ambiguous`/`None` require a choice and are sorted to the top.

**Jadvallar (schedules)** — read under `attendance.view`, write under
`attendance.manage` (**AMENDED 2026-09-27**: the page and every `GET` behind it
were already `view`; only the menu item was `manage`, so view-only users could
not reach it — now they open it read-only, year grid included, and every
write control stays hidden without `manage`). Schedule list + a 7-row
day editor (weekday, working toggle, start, end, break) with a live summary
line ("Haftada 5 kun · 40 soat"). An overnight day shows a small "→ ertasi
kuni" marker next to the end time so the operator sees the consequence
immediately. Second card: assignments, grouped by scope, with the effective
range and a "Kim uchun amal qiladi" preview using `/schedules/resolve`.

**Qurilmalar (devices)** — `attendance.manage`. Table: name, location,
direction role, active switch, last seen (relative), passes in 24 h, clock
badge, token prefix. Create/rotate opens a dialog that shows the token **once**
with a copy button and the explicit sentence "Bu token boshqa ko‘rsatilmaydi",
plus the ready-made ingest URL to paste into the terminal's HTTP Listening
form.

### 8.5 Apple HIG applied to the live board

The house design system is iOS (`web/src/styles/globals.css`: soft grey
background, white cards, near-invisible shadows, generous whitespace, colour
only for meaning, large radii). For the board specifically:

* People are **cards, not table rows** — a board is glanced at from across a
  desk, and a card with a 56 px round avatar reads at that distance.
  `--radius-lg` (16 px), `--shadow-card`, white on the grey canvas.
* **One tint colour.** `--color-ios-blue` is the only "this is interactive"
  signal. Presence is not blue: inside = `--color-ios-green` dot, late =
  `--color-ios-orange`, absent = neutral grey. Colour carries meaning, never
  decoration.
* **No spinner churn.** The 30 s refresh must not blank the list.
  `placeholderData: keepPreviousData` + a quiet "yangilandi" timestamp; a
  board that flashes every half-minute is a board nobody leaves open.
* **Numbers in SF-style tabular figures** (`font-variant-numeric: tabular-nums`)
  so the counters do not jitter as they change.
* Empty state is an `EmptyState` with a sentence, not a shrug: "Hozir hech kim
  ichkarida emas" / "Qurilma hali ulanmagan — Qurilmalar bo‘limiga qarang".

### 8.6 Apple HIG applied to a dense timesheet grid

A 31-column grid is the opposite of what iOS wants, so the rules are explicit:

* **The grid is the only dense thing on the page.** Everything around it keeps
  the module's normal breathing room. Do not compress the header, the filters
  or the page padding to make the grid fit.
* **Cells are 36 × 36 px, with `--radius-xs` (6 px)** — small, but still
  rounded and still separated by a 2 px gutter. No hairline table borders
  anywhere; separation comes from the gutter and from the tinted fill, which
  is the same trick the rest of the system uses.
* **The cell shows one number: hours, one decimal** (`7.9`). Everything else
  — exact minutes, first in, last out, late minutes, and (**AMENDED
  2026-09-17**) the break line — is in the hover/tap popover. iOS's rule is
  progressive disclosure, and 540 × 31 cells cannot each carry four figures.
* **The popover always states what happened to the break**, in one line, and
  never leaves it to be inferred from a smaller-than-expected total. This is
  the first question anyone will ask about a changed number, and answering it
  in the cell is cheaper than answering it in support. **AMENDED 2026-09-17 —
  derived from the two stored fields only** (`breakDeductionMinutes`,
  `breakTakenMinutes`, §6.6); first match wins:
  1. `breakDeductionMinutes > 0` → «Tanaffus: {{deducted}} daq. ayirildi»
     (`breakDeducted`), followed by «({{taken}} daq. olingan)»
     (`breakTakenPart`) when `breakTakenMinutes > 0`;
  2. `breakTakenMinutes > 0` → «Tanaffus olingan — ayirilmadi» (`breakTaken`);
  3. otherwise → «Tanaffus ayirilmadi» (`breakNotDeducted`).

  The previous revision had a fourth form, «Tanaffus oynasi belgilanmagan»
  (`breakNoWindow`), and a `breakNotTaken` key. **Both are withdrawn.** After
  the §13 #14 cap, "0 taken, 0 deducted" means *either* no window *or* not in
  the building during the window, and the stored row cannot tell them apart.
  Reading today's schedule to decide would describe a schedule the number may
  not have been computed with, so the popover says only what the row proves.
* **Status is a tinted fill at 12 % opacity, not a border and not an icon:**
  `Present` white, `Absent` red tint, `DayOff` grey tint, `DayOffWorked`
  purple tint, `MissingExit`/`MissingEntry` orange tint, `NoSchedule` diagonal
  hatch, `NotEmployed` empty. Late is a 3 px orange bar along the cell's
  bottom edge, so "late but present" reads without a second colour.
* **The employee column and the totals column are frozen** (`position: sticky`
  on the cells, inside the scroll container — ⚠️ not on an ancestor; the app
  shell is `h-dvh overflow-hidden` and a sticky element under a parent with
  `overflow`/`transform` silently breaks, which this project has already been
  bitten by once).
* **Weekend columns get the grey canvas colour** as their header background,
  so the week rhythm is visible without gridlines.
* **The legend is always on screen**, below the grid, not in a tooltip.
* **Mobile: the grid does not attempt to be a grid.** Under `md`, the tabel
  page renders one employee per card with a horizontally scrollable day strip.
  Nobody will use this on a phone, but a broken layout on a boss's iPad is a
  bad look and this costs one breakpoint.

### 8.7 Operational visibility

No Hangfire dashboard (§1.2 #12). Instead:

* the **devices page** shows last seen, 24 h pass count and clock badge — the
  three facts that say "ingest is alive";
* the **board** shows the clock-skew banner;

  **AMENDED 2026-09-27.** The live board page was replaced by Statistika
  (`/verifikatsiya/jonli`, `attendance-dashboard-page.tsx`) and the banner and
  the «Qurilma hali ulanmagan» empty state (§8.4, §8.5) were lost with it — no
  decision dropped them. They are back on Statistika, from
  `deviceHealth` in `GET /dashboard` and `GET /dashboard/trend`
  (`attendance.view`): the orange clock banner, an info notice when no
  terminal has ever connected, and — owner's request of the same day — a
  **red** outage panel naming each device whose agent has been silent, or
  whose terminal has not answered, for more than 10 minutes, with «since».
  The same red panel is on the devices page. The threshold is schedule-free:
  the agent polls every minute whether or not anyone passes (SPEC-agent §L).
* a small "Oxirgi hisoblash: HH:mm" line on the timesheet page, from the
  response's `lastComputedAt` (§6.6, **AMENDED 2026-09-17**), so "the tabel
  looks stale" has a one-glance answer.

**The former-employee alert is part of this panel, not only of the board —
AMENDED 2026-09-17.** It surfaces in three places, and all three are required:

| Where | What |
|---|---|
| **Live board**, top of page | the full `danger` alert of §8.4 — non-dismissible, one row per person |
| **Devices page**, above the table | a compact red line: «So‘nggi 24 soatda ishdan ketgan {{n}} xodim o‘tdi» linking to the board; `{{n}}` is `count` from `GET /former-employee-alerts` (**AMENDED 2026-09-17**). This page is where the fix happens (the operator is already looking at the terminal that let them in), so the warning has to be visible from it |
| **Nav**, `attendanceBoard` item | `NavItem.count` carries `count` from **`GET /former-employee-alerts`** (§6.5) and renders in the **danger** tone, not the usual neutral badge — so the alert is visible from any screen without opening the board. **AMENDED 2026-09-17:** the badge must **not** poll `GET /board`; the query uses `refetchInterval: 30_000` and is `enabled` only for users with `attendance.view` — otherwise every screen of a user without it would fire a 403 every 30 s |

The nav badge shares its mechanism with the unmatched-passes count on
`attendanceMapping` but **not** its colour: unmatched passes are work to do,
this is a security event. If `sidebar.tsx` cannot express two badge tones
today, add the tone as a `NavItem` field rather than reusing the neutral one —
a red number and a grey number must not mean the same thing.

⚠️ This alert is what stands in for the unbuilt write-back seam (§2.13). It is
the module's only compensating control for "a terminated employee can still
open the door", so treat a bug in it as a security bug, not a UI bug.

### 8.8 i18n — required keys

Four files: `web/src/shared/i18n/locales/{uz-Latn,uz-Cyrl,ru,en}.json`. One
author for all four (§10.1).

`nav.*` (8 new keys):

| key | uz-Latn | uz-Cyrl | ru | en |
|---|---|---|---|---|
| `verification` | Verifikatsiya | Верификация | Верификация | Verification |
| `attendanceBoard` | Hozir ishda | Ҳозир ишда | Сейчас на работе | Who is in |
| `attendancePasses` | O‘tishlar | Ўтишлар | Проходы | Passes |
| `attendanceTimesheet` | Tabel | Табел | Табель | Timesheet |
| `attendanceReports` | Davomat hisobotlari | Давомат ҳисоботлари | Отчёты посещаемости | Attendance reports |
| `attendanceMapping` | Moslashtirish | Мослаштириш | Сопоставление | Mapping |
| `attendanceSchedules` | Ish jadvallari | Иш жадваллари | Графики работы | Work schedules |
| `attendanceDevices` | Qurilmalar | Қурилмалар | Устройства | Devices |

New top-level `attendance` block. Required key groups (exact strings are the
author's to write, but **every key below must exist in all four files**):

* board: `boardTitle`, `inside`, `expected`, `absent`, `late`, `since`,
  `refreshedAt`, `clockWarning`, `emptyTitle`, `emptyText`, `noDeviceTitle`,
  `noDeviceText`, **AMENDED** `formerEmployeeAlertTitle`,
  `formerEmployeeAlertTitlePlural`, `formerEmployeeAlertHint`,
  `formerEmployeeTerminatedOn`, `formerEmployeeOpenPass`,
  `formerEmployeeShort` (the compact devices-page line), and — added by the
  second 2026-09-17 follow-up — `formerEmployeePassCount`
* passes: `passesTitle`, `time`, `employee`, `unknownPerson`, `device`,
  `direction`, `directionIn`, `directionOut`, `directionUnknown`, `method`,
  `methodFace`, `methodCard`, `methodFingerprint`, `methodPassword`,
  `methodOther`, `methodUnknown`, `matched`, `unmatched`, `photoMissing`,
  `photoPurged`, `skewWarning`, `offsetAssumed`, `emptyTitle`, `emptyText`
* timesheet: `timesheetTitle`, `month`, `hoursShort`, `totalWorked`,
  `totalPlanned`, `presentDays`, `absentDays`, `legend`, `lastComputedAt`,
  `cannotEditTitle`, `cannotEditText` (the §5.8 explanation), `emptyTitle`,
  `emptyText`, **AMENDED** `breakDeducted`, `breakTakenPart`, `breakTaken`,
  `breakNotDeducted` (the §8.6 popover). `breakNotTaken` and `breakNoWindow`
  from the previous revision are **withdrawn** — do not add them
* statuses: `statusPresent`, `statusAbsent`, `statusDayOff`,
  `statusDayOffWorked`, `statusMissingExit`, `statusMissingEntry`,
  `statusNoSchedule`, `statusNotEmployed`
* reports: `reportsTitle`, `kindLate`, `kindEarlyLeave`, `kindAbsent`,
  `kindMissing`, `kindSummary`, `minMinutes`, `planStart`, `planEnd`,
  `firstIn`, `lastOut`, `lateMinutes`, `earlyLeaveMinutes`, `overtimeMinutes`,
  `problemDays`, `export`
* mapping: `mappingTitle`, `unmatchedTitle`, `unmatchedSubtitle`, `personId`,
  `passCount`, `firstSeen`, `lastSeen`, `linkToEmployee`, `unlink`,
  `unlinkConfirm`, `identitiesTitle`, `importTitle`, `importDrop`,
  `importPreview`, `importConfirm`, `matchExactPinfl`, `matchExactName`,
  `matchAmbiguous`, `matchNone`, `matchAlreadyMapped`, `skipRow`,
  `importDone`
* schedules: `schedulesTitle`, `scheduleName`, `graceIn`, `graceOut`,
  `working`, `dayOff`, `startTime`, `endTime`, `breakMinutes`, `nextDay`,
  `weekSummary`, `assignmentsTitle`, `scopeEmployee`, `scopePosition`,
  `scopeDepartment`, `scopeDefault`, `effectiveFrom`, `effectiveTo`,
  `resolvePreview`, `resolvedBy`, **AMENDED** `breakStart`, `breakEnd`,
  `breakWindow`, `breakWindowHint`, `breakWindowNone`, `breakWindowNoneHint`
* devices: `devicesTitle`, `deviceName`, `serialNumber`, `model`, `location`,
  `directionRole`, `roleReports`, `roleEntryOnly`, `roleExitOnly`, `provider`,
  `identityProvider`, `authMode`, `allowedIps`, `allowedIpsHint`, `active`,
  `lastSeen`, `passes24h`, `tokenPrefix`, `tokenTitle`, `tokenOnceWarning`,
  `tokenCopy`, `ingestUrl`, `rotateToken`, `rotateConfirm`, `deleteConfirm`
* settings (**AMENDED**, rendered on `/sozlamalar`): `photoRetentionLabel`,
  `photoRetentionHint`, `passRetentionLabel`, `passRetentionHint`,
  `passRetentionNever`
* shared: `noAccessTitle`, `noAccessText`, `recompute`, `recomputeStarted`,
  `recomputeRange`

`errors.*`: all 41 codes from §6.9 — the 33 original plus the 8 listed under its table, including `davomat.xodim_yoq`, `davomat.jadval_yoq` (§6.9.1) and `davomat.otish_topilmadi`. Do **not** translate the six invented break codes named above that table; they do not exist.

**Weekday labels — AMENDED 2026-09-17.** Seven keys, full names, used by the
schedule-day editor rows. The client already references them
(`WEEKDAY_LABEL_KEY` in `lib/attendance-presentation.ts`) and deliberately does
not use `Intl` for them, because browsers do not fully support the `uz-Latn` /
`uz-Cyrl` tags:

| key | uz-Latn | uz-Cyrl | ru | en |
|---|---|---|---|---|
| `attendance.weekdayMon` | Dushanba | Душанба | Понедельник | Monday |
| `attendance.weekdayTue` | Seshanba | Сешанба | Вторник | Tuesday |
| `attendance.weekdayWed` | Chorshanba | Чоршанба | Среда | Wednesday |
| `attendance.weekdayThu` | Payshanba | Пайшанба | Четверг | Thursday |
| `attendance.weekdayFri` | Juma | Жума | Пятница | Friday |
| `attendance.weekdaySat` | Shanba | Шанба | Суббота | Saturday |
| `attendance.weekdaySun` | Yakshanba | Якшанба | Воскресенье | Sunday |

### 8.9 The two retention fields on `/sozlamalar` — AMENDED 2026-09-17

`/sozlamalar` is an **existing** page owned by the settings module; this
module adds two rows to it, and nothing else. Unit **W** does it, because it
touches the same locale files.

* Add a `DAVOMAT` group to `GROUPS` in
  `web/src/features/settings/components/system-settings-page.tsx` holding
  `attendance.photoRetentionDays` and `attendance.passRetentionDays`, in that
  order. ⚠️ A key missing from `GROUPS` is **silently not drawn** — the server
  returns it, the screen just never renders it, and nobody notices until
  someone asks where the setting went.
* Both render with the existing `Number` field; no new control type.
* `passRetentionDays` shows `attendance.passRetentionNever` («0 — hech qachon
  o‘chirilmaydi») **under** the input, permanently, not only when the value is
  `0`. A sentinel that is only explained after you have already typed it is
  not documentation.
* `passRetentionHint` must contain the sentence that the tabel survives:
  «O‘tish yozuvlari o‘chiriladi, davomat jadvali (tabel) va soatlar
  QOLADI» — §2.9. An HR manager choosing 90 days needs to know this before
  saving, not after.
* The cross-field rule (§2.9) is server-enforced; the client just surfaces
  `davomat.saqlash_muddati_nomuvofiq` through the normal `useErrorMessage`
  path. Do not duplicate the comparison in the form — two copies of one rule
  drift, and the server's is the one that matters.

---

## 9. Background jobs

### 9.1 Hangfire is a package reference, not wiring — this module wires it

`Hangfire.Core`, `Hangfire.AspNetCore` and `Hangfire.PostgreSql` are pinned in
`api/Directory.Packages.props` under "Fon vazifalar" and referenced by
`Bonvi.Infrastructure`, but **nothing in the codebase uses them** — a grep for
`Hangfire` finds only two comments explaining why notifications deliberately
do *not* use it. This module is the first real consumer, so it carries the
wiring cost:

* `api/src/Bonvi.Api/Startup/BackgroundJobs.cs` (new):
  `AddHangfire(...UsePostgreSqlStorage(connectionString, new PostgreSqlStorageOptions { SchemaName = "hangfire" }))`,
  `AddHangfireServer(o => { o.WorkerCount = 2; o.Queues = ["default"]; })`,
  and `UseBonviRecurringJobs()` which registers the three recurring jobs.
* `Bonvi.Api.csproj` gains `Hangfire.AspNetCore`.
* `Program.cs` gains two lines (shared file, §10.1).
* **No dashboard route** (§1.2 #12).
* `WorkerCount = 2`: the VPS has 2 GB RAM (see the server note in the project
  memory) and the default worker count scales with CPU count, which would
  start far more workers than this workload needs.
* **Testing environment:** the Hangfire server is **not** started when
  `IHostEnvironment.IsEnvironment("Testing")` — integration tests must not get
  a background thread mutating their database mid-assertion. Job *methods* are
  public and are tested by calling them directly.

### 9.1.1 Application must not reference Hangfire — the `IBackgroundJobQueue` port

Two use cases enqueue work: creating an identity (§4.3) and `POST /recompute`
(§6.7). Neither may `using Hangfire;` — Application owns no technology. The
port:

```csharp
// Bonvi.Application/Abstractions/IBackgroundJobQueue.cs
public interface IBackgroundJobQueue
{
    void Enqueue<TJob>(Expression<Action<TJob>> methodCall);
}
```

Two implementations:

| Implementation | Where | Used when |
|---|---|---|
| `HangfireBackgroundJobQueue` | `Bonvi.Infrastructure/Services/` | every environment except `Testing` |
| `InlineBackgroundJobQueue` | `Bonvi.Infrastructure/Services/` | `Testing` — runs the job **synchronously**, on the calling scope |

The inline variant is not a stub for convenience: it is what makes "map an
identity, then assert the tabel cell exists" a deterministic integration test
instead of a sleep-and-hope. It is registered by the same `if` that decides
whether to start the Hangfire server, so the two can never disagree.

**Ownership:** the interface belongs to **B6** (its first caller), both
implementations to **B7**. Until B7 lands, B6 registers `InlineBackgroundJobQueue`
everywhere — which is also exactly what checkpoint C6 relies on.

### 9.2 The jobs

| Id | Schedule (UTC cron) | What |
|---|---|---|
| `attendance:recompute-dirty` | `*/15 * * * *` | §9.3 |
| `attendance:nightly-recompute` | `10 22 * * *` (03:10 Tashkent) | full recompute of the last 3 days for all employees — catches schedules, assignments and employments edited after the fact |
| `attendance:purge-photos` | `30 22 * * *` (03:30 Tashkent) | §3.10 — expires photos, keeps the pass row |
| `attendance:purge-passes` | `45 22 * * *` (03:45 Tashkent) | **AMENDED 2026-09-17** — §9.2.1, expires whole pass rows |
| `attendance:started-shifts` | `5-59/15 * * * *` | **new 2026-09-27** — inserts the missing `Absent` rows of yesterday and today whose plan has started (§5.4); only inserts |
| `attendance:employment-changes` | `12-59/15 * * * *` | **new 2026-09-27** — finds employments / employment events created, edited or deleted since the last run (hidden setting `attendance.employmentChangesCheckedAt`, re-read with a 10-minute overlap) and recomputes each such employee from the earliest affected date (the new event's effective date; the hire date when the employment row itself or an existing event changed) to today, capped at `MaxRecomputeDays`. A backdated termination, hire or transfer reaches the timesheet without touching the employment commands. The first run looks back one quarter |
| `attendance:backfill-identity` | fire-and-forget | §4.3 |
| `attendance:recompute-range` | fire-and-forget | `POST /recompute`; also an approved / cancelled absence request longer than `MaxRecomputeDays` (shorter ones are recomputed in the request itself, 2026-09-27) |
| `attendance:schedule-dates` | fire-and-forget | **new 2026-09-27** — year-grid edit (SPEC-jadval-to'ri §B3): only the employees the schedule reaches, only the changed dates ±1 |

**AMENDED 2026-09-27 — the single writer chunks.** `AttendanceDayRecomputer`
splits the requested dates into windows of at most `MaxRecomputeDays` and the
employees into batches of 100, and detaches each saved batch from the change
tracker, so memory holds one batch × one window whatever the caller asks for
(a whole-year grid fill used to keep ~146 000 tracked rows on a 2 GB VPS).

`RecurringJobOptions { TimeZone = TimeZoneInfo.Utc }` and a comment giving the
Tashkent time next to each cron string — containers run UTC, and a cron read
as local time is a silent 5-hour error.

#### 9.2.1 `attendance:purge-passes` — AMENDED 2026-09-17

Runs **after** `purge-photos` (03:45 vs 03:30) so the common case — a pass
whose photo already expired months ago — costs no file I/O at all.

Read `attendance.passRetentionDays` (§2.9). **If it is `0`, do nothing and
return** — that is the "never delete" sentinel, and it must be the first
branch in the method so an accidental edit to the ordering cannot turn it into
a deletion.

Otherwise let `effectiveDays = max(passRetentionDays, photoRetentionDays)` and
`cutoff = today(Tashkent) − effectiveDays`. Then, in batches of 2 000 ordered
by `received_at`, for passes with `received_at < cutoff`:

1. **Delete the photo file first**, via `IFileStorage.DeleteAsync`, **whether
   or not the photo retention has expired yet**. This is the step that is easy
   to get wrong: `photo_key` may still be populated because
   `passRetentionDays < photoRetentionDays` was configured, or because the
   photo purge has not reached that day yet. Deleting the row first would
   strand the file forever — nothing else in the system knows the key. The
   `effectiveDays` floor above makes that configuration impossible in the
   first place; this step makes it harmless even if it happens.
2. **Delete the pass row.** Hard `DELETE`, not soft — `AttendancePass` is not
   `ISoftDeletable` (§2.3) and a soft-deleted row would defeat the entire
   purpose, which is to reclaim rows and disk.
3. Loop until no rows remain under the cutoff or 5 minutes elapse, whichever
   comes first. The remainder is picked up tomorrow; the first run after this
   feature ships may take several nights on a mature database, and that is
   fine.

**`attendance_days` is not touched.** No `DELETE`, no recompute, no
`processed_at` reset. Deleting a pass must never mark its day dirty — a
recompute triggered afterwards would rebuild the day from the *surviving*
passes and quietly rewrite a year-old tabel to zero. State this in the job's
doc comment in capitals; it is the single most damaging mistake available in
this module.

For the same reason the job takes no `processed_at` into account: passes older
than the retention are long processed, and a pending one that old means the
recompute queue is broken — deleting it would hide the breakage. Log a warning
and **skip** any row that is **matched** (`resolved_employee_id IS NOT NULL`)
**and** has `processed_at IS NULL` under the cutoff.

**AMENDED 2026-09-17 (B7 review) — unmatched passes past the cutoff ARE
deleted**, with their photo file, exactly like processed ones. The earlier text
skipped every `processed_at IS NULL` row, which also kept every never-mapped
person id (visitor cards, test cards) forever. Such a pass can never reach a
tabel again: before its first delete the job raises the hidden
`attendance.frozenThrough` date, and the single recompute writer
(`AttendanceDayRecomputer`) never recomputes a date on or before it — so a late
identity cannot resurrect the day either. The selection and the delete both use
`local_date < cutoff AND (processed_at IS NOT NULL OR resolved_employee_id IS NULL)`
(B7 cut by `local_date`, not `received_at` — the index exists and the freeze
date is exact), and the delete **re-checks that condition in the `DELETE`
itself**: if the backfill attaches the pass between the list and the delete,
the row is now matched-and-unprocessed and survives (under READ COMMITTED a
`DELETE` that waited on the backfill's row lock re-evaluates the condition on
the updated row); if the delete wins, the backfill finds no row and the frozen
date is not recomputed. The only residue of the first order is a surviving row
whose photo file was already removed; it is deleted harmlessly once processed.
The warning counts only the matched-and-unprocessed rows.

### 9.3 The dirty-queue recompute

1. `SELECT DISTINCT resolved_employee_id, local_date FROM attendance_passes
    WHERE processed_at IS NULL AND resolved_employee_id IS NOT NULL
    ORDER BY min(received_at) LIMIT 5000`
   The `ORDER BY` is not cosmetic: without it a backlog larger than the batch
   could keep re-reading the same arbitrary 5 000 rows and never drain.
2. Expand each `(E, d)` to `(E, d−1)`, `(E, d)`, `(E, d+1)` and de-duplicate.
   The neighbours are required because an overnight shift's passes belong to
   the previous date (§5.3).
3. Recompute each pair per §5, upserting `attendance_days` by
   `(employee_id, local_date)`.
4. `UPDATE attendance_passes SET processed_at = now()` for the rows read in
   step 1.
5. The whole thing is idempotent: running it twice produces identical rows.
   ⚠️ It must **not** use `DELETE` + `INSERT` on `attendance_days` — a report
   running concurrently would see a missing row. Upsert.

Unmatched passes keep `processed_at = NULL` forever until they resolve; the
partial index keeps that cheap, and it is what makes the backfill in §4.3 work
with no extra bookkeeping.

---

## 10. Build order and parallelism

### 10.1 Sequential — shared files, one author each, never in parallel

| File | Why |
|---|---|
| `api/src/Bonvi.Infrastructure/Persistence/Migrations/*` + `AppDbContextModelSnapshot.cs` | EF serialises the whole model into one snapshot; two migrations authored in parallel conflict irreconcilably |
| `api/src/Bonvi.Domain/Identity/Permissions.cs` | permission catalogue + role defaults, one list |
| `api/src/Bonvi.Domain/Settings/SystemSettingKeys.cs` | one ordered definition list |
| `api/src/Bonvi.Infrastructure/Persistence/AppDbContext.cs` | the `DbSet` block |
| `api/src/Bonvi.Application/Common/DependencyInjection.cs` | one DI list |
| `api/src/Bonvi.Application/Attendance/AttendanceRegistration.cs` | one DI list |
| `api/src/Bonvi.Application/Attendance/Dtos/AttendanceDtos.cs` | every backend unit touches it |
| `api/src/Bonvi.Application/Attendance/Abstractions/IAttendanceReadModel.cs` | filters + rows, shared by all report units |
| `api/src/Bonvi.Api/Program.cs` | endpoint registration + Hangfire wiring — three units add one line each, and they are ordered, not concurrent (§10.2.1) |
| `api/src/Bonvi.Application/Abstractions/IBackgroundJobQueue.cs` | one port, two implementations, two units (§9.1.1) |
| `api/src/Bonvi.Api/Extensions/RateLimitingExtensions.cs` | new `Ingest` policy + `ExcludedPaths` |
| `api/src/Bonvi.Api/Extensions/ErrorLogExtensions.cs` | the token path scrubber (§3.3) |
| `api/src/Bonvi.Domain/Abstractions/` + `Bonvi.Infrastructure/Repositories/AttendanceRepositories.cs` | one repository file for six aggregates |
| `web/src/features/verification/api/attendance-api.ts` + `attendance-queries.ts` | every frontend unit depends on them |
| `web/src/shared/i18n/locales/*.json` (4) | one author, or two agents write the same key twice with different text |
| `web/src/app/layout/nav-items.ts` + `sidebar.tsx` (the count wiring) | shared files |

### 10.2 Units

| Unit | Touches | Depends on | Parallel? |
|---|---|---|---|
| **B1 — schema** | `Bonvi.Domain/Attendance/**` (7 entities, enums, `AttendanceRules`); `Persistence/Configurations/AttendanceConfigurations.cs`; `AppDbContext.cs`; `AttendanceRepositories.cs`; the migration | — | **No.** First, alone. Everything compiles against it |
| **B2 — permissions + settings** | `Permissions.cs`; `SystemSettingKeys.cs` | — | **Yes**, with B1 — disjoint files, no shared type |
| **B3 — ingest** | `Application/Attendance/Ingest/**`; `IDatabaseFault` + `PostgresFault.cs` (one method); `Api/Endpoints/AttendanceIngestEndpoints.cs`; `Application/Attendance/Ingest/IngestRegistration.cs` (**its own** DI file, §10.2.1); `Program.cs` (one line); `RateLimitingExtensions.cs`; `ErrorLogExtensions.cs` | B1 | **No** while it holds three shared Api files |
| **B4 — schedules + timesheet engine** | `Application/Attendance/Scheduling/**`, `Application/Attendance/Computation/**` (resolver, pairing, day builder) | B1 | **Yes**, with B3 — no shared file, and the engine is pure logic over the domain |
| **B5 — read models + reports + export** | `Abstractions/IAttendanceReadModel.cs` (**creates**); `Dtos/AttendanceDtos.cs` (**creates**); `ReadModels/AttendanceReadModel.cs`; `Infrastructure/Reports/AttendanceExcelWriter.cs`; the report/board/timesheet query handlers | B1 | **Yes**, with B3 and B4. It reads `attendance_days` rows and never calls the engine; the status semantics it renders are defined in §5.6 and §5.7.1, not in B4's code |
| **B6 — management use cases + endpoints** | devices, identities, import, schedules, assignments, recompute; `Api/Endpoints/AttendanceEndpoints.cs`; `AttendanceRegistration.cs`; `DependencyInjection.cs`; `Program.cs` (one line); extends `AttendanceDtos.cs` | B1, B2, B4, B5 | **No.** One agent — it owns four shared files |
| **B7 — background jobs** | `Startup/BackgroundJobs.cs`; `Bonvi.Api.csproj`; `Program.cs` (Hangfire lines); the four job classes | B3, B4, B6 | **No.** Last backend unit |
| **F1 — client contract** | `attendance-api.ts`, `attendance-queries.ts`, `lib/attendance-presentation.ts`, `lib/attendance-search.ts` | §6 only (no running backend) | **Yes**, from the start — §6 is precise enough to type against |
| **F2 — board + passes** | `attendance-board-page.tsx`, `attendance-passes-page.tsx`, `attendance-pass-photo-dialog.tsx`, 2 route files | F1 | **Yes**, with F3/F4/F5 |
| **F3 — timesheet + reports** | `attendance-timesheet-page.tsx`, `attendance-timesheet-grid.tsx`, `attendance-reports-page.tsx`, 2 route files | F1 | **Yes** |
| **F4 — mapping + import** | `attendance-mapping-page.tsx`, `attendance-import-dialog.tsx`, `attendance-identity-dialog.tsx`, 1 route file | F1 | **Yes** |
| **F5 — schedules + devices** | `attendance-schedules-page.tsx`, `attendance-schedule-form-dialog.tsx`, `attendance-assignment-dialog.tsx`, `attendance-devices-page.tsx`, `attendance-device-form-dialog.tsx`, `attendance-device-token-dialog.tsx`, 2 route files | F1 | **Yes** |
| **W — wiring** | `nav-items.ts`, `sidebar.tsx` count, 4 locale files | F2–F5 | **No.** Single author, last |
| **T — tests** | `api/tests/**` | B6, B7 | **No.** After the backend is green |

#### 10.2.1 DI registration is split so B3 and B5 never collide

`AddAttendance()` in `AttendanceRegistration.cs` is written by **B6** and
calls into two sub-registrations:

```
Application/Attendance/Ingest/IngestRegistration.cs   → AddAttendanceIngest()   (B3)
Application/Attendance/AttendanceRegistration.cs      → AddAttendance()         (B6)
Application/Common/DependencyInjection.cs             → services.AddAttendance() (B6)
```

B3 needs its handler resolvable the moment its endpoint exists, so it owns a
registration file of its own; B5 writes handlers but registers nothing — B6
registers them, and B6 runs after B5. Precedent: `SafetyRegistration.cs` is
one file per module, and this module is simply big enough to need two.

⚠️ **`Program.cs` is touched by B3, B6 and B7 — one line each** (
`MapAttendanceIngestEndpoints()`, `MapAttendanceEndpoints()`, the Hangfire
wiring). That is safe **only because those three units never run at the same
time**: the §10.3 order puts B3 before B6 before B7, and the two units that do
run alongside B3 (B4, B5) never open the file. If anyone reorders the plan so
that two of B3/B6/B7 run in parallel, the registration lines must be pulled
out into a single wiring step first. The same rule applies to
`AttendanceDtos.cs` and `IAttendanceReadModel.cs`: **B5 creates both, B6
extends them**, and B5 runs before B6. B4 touches neither — the computation
engine works on domain types only. B3 touches neither either: the ingest
response record (`{ ok, stored, duplicates }`) lives in
`Application/Attendance/Ingest/`, next to the only code that produces it.

#### 10.2.2 What the 2026-09-17 amendment adds to each unit

No new unit, no change to the dependency graph or the order. The amendment
lands inside units that already exist:

| Unit | Added by the amendment |
|---|---|
| **B1** | `work_schedule_days.break_start` / `break_end`, `ck_work_schedule_days_break_window`, the validation of §2.4.0 incl. the §2.4.0.1 normalisation, and the three `davomat.jadval_tanaffus_*` codes — **all done and verified**, including the §6.9.1 rename (`xodim_yoq`, `jadval_yoq`). The `attendance_days` break columns came in a second migration (§2.12) |
| **B2** | `attendance.passRetentionDays` (§2.9) and `AttendanceRules.DefaultPassRetentionDays` / `FormerEmployeeAlertHours` |
| **B4** | `breakTaken` / `breakDeduction` in the pairing engine (§5.5.1) — the largest single change in the amendment. ⚠️ Includes the window-to-instant formula: `plan_start_at + (break_start − start_time)` with `TimeOnly` subtraction, never `D` composed with `break_start`. **AMENDED 2026-09-17:** also the `presentInWindow` cap (§13 #14) and the exact-truncation rule that keeps `breakDeduction ≤ closed sum` (§5.5.1) |
| **B5** | `formerEmployeeAlerts` on the board read model (§6.5) — **one entry per person**, from one method shared with `GET /former-employee-alerts`; `GET /passes/{id}` and the full `AttendancePassDto`; the timesheet paging fields, `lastComputedAt` and the two break fields (§6.6); `davomat.otish_topilmadi` |
| **B6** | `breakStart`/`breakEnd` on the schedule DTOs (§6.4); the cross-key retention check `davomat.saqlash_muddati_nomuvofiq` |
| **B7** | the `attendance:purge-passes` job (§9.2.1) |
| **F1/F3** | the break line in the cell popover, derived from the two stored fields (§8.6); F1 aligns its types with the DTO field sets of §6.2–§6.6 and points `useFormerEmployeeAlerts` at `GET /former-employee-alerts` |
| **F2** | the board alert (§8.4); the `?passId=` deep link through `GET /passes/{id}` and `validatePassesSearch` (§8.2) |
| **F5** | the break-window fields in the schedule day editor (§6.4) |
| **W** | the new i18n keys (§8.8), the danger-toned nav badge and the two settings rows (§8.7, §8.9) |
| **T** | §11.1 #51–74 incl. #58a–c, #64a, #74a–b; §11.2 #7a–b |

⚠️ **B1 and B4 must not be given to the same agent as a "since you are in
there" bundle.** B1 is schema and validation; B4 is the arithmetic that every
hours figure in the system depends on. They are separately verifiable (C1 and
C4) and separately reviewable, and the amendment is exactly the kind of change
where bundling them hides a maths error behind a migration diff.

### 10.3 Order

```
[ B1 | B2 | F1 ]
        ↓
[ B3 | B4 | B5 ]  →  B6  →  B7  →  T
        F1        →  [ F2 | F3 | F4 | F5 ]  →  W
```

The frontend can be written while the backend is still being typed: §6 is the
contract both sides build against. After B6 lands, run `./bonvi gen` (the API
must be up) and replace the hand-written types in `attendance-api.ts` with the
generated ones — **that swap is part of F1's definition of done, not an
optional cleanup.**

Then, in order: `./bonvi check` → `./bonvi test` → `./bonvi pnpm build`.

### 10.4 Verifiable checkpoints

Each phase is provable on its own, without the next one existing:

* **C1 (after B1):** `./bonvi migrate` applies cleanly to a **restored
  backup** of production in a scratch database; `\d+ attendance_passes` shows
  the six indexes; `\d+ work_schedule_days` shows `break_start`, `break_end`
  and `ck_work_schedule_days_break_window` (**AMENDED 2026-09-17**); no existing
  table changed.
* **C2 (after B2):** `./bonvi test` green; the two new permissions appear in
  the HR manager role after seeding; `/sozlamalar` shows the retention field.
* **C3 (after B3):** `curl` a Hikvision-shaped multipart body with a real
  token → 200, one row in `attendance_passes`, one JPEG in the volume; repeat
  the identical call → 200 with `duplicates: 1` and still one row; the same
  call with a wrong token → 401 and nothing written; the API log contains
  `/api/attendance/ingest/***`, never the token.
* **C4 (after B4):** the engine's unit tests pass, including the midnight
  crossing and the odd-pass cases in §5.7 and — **AMENDED 2026-09-17** — the
  whole break-window block (§11.1 #51–64). The pair that must be green before
  this checkpoint counts: "never left" and "left for exactly the window"
  produce the **same** `worked_minutes`. No database needed.
* **C5 (after B5):** an integration test that calls `IAttendanceReadModel`
  **directly** (no HTTP — the endpoints do not exist until B6) returns a full
  31-day cell array per employee for a seeded month, with the synthesized
  cells of §5.7.1 in the right places; `AttendanceExcelWriter` produces a file
  that opens in Excel with real date cells and summable duration columns.
* **C6 (after B6):** `POST /devices` → `POST /identities` → the unmatched pass
  from C3 is resolved **and** a tabel cell appears at `GET /timesheet`,
  without anyone calling recompute. This works because B6 registers
  `InlineBackgroundJobQueue` (§9.1.1), so the backfill and the day rebuild run
  inside the request. If this checkpoint needs a manual nudge, the §4.3 wiring
  is wrong — fix it here, not later.
* **C7 (after B7):** with the Hangfire server started, leave three passes
  unprocessed, wait one 15-minute cycle (or trigger the recurring job from
  code), and confirm the dirty queue drains and `processed_at` is filled;
  backdate a photo past the photo retention and confirm the purge job deletes
  the file, nulls `photo_key` and `raw_event`, and leaves the pass row.
  **AMENDED 2026-09-17 — the retention half of this checkpoint is now three
  separate proofs, and all three must be shown:**
  1. with `attendance.passRetentionDays = 365`, a pass backdated 400 days
     disappears **and** its photo file is gone from the volume;
  2. the `attendance_days` row for that same date is **still present and
     numerically identical** — compare `worked_minutes`, `late_minutes`,
     `early_leave_minutes` and `status` before and after. This is the
     checkpoint that catches the one catastrophic mistake available here
     (§9.2.1): a purge that marks the day dirty and lets the recompute rewrite
     a year-old tabel to zero;
  3. with the setting at `0`, the same backdated pass survives the job
     untouched.
* **C8 (after W):** `./bonvi check` green — 4 locales aligned, all 41 error
  codes translated; the nav section appears for an HR manager and is entirely
  absent for a user with neither permission.

---

## 11. Test plan

### 11.1 `test-backend` — must cover

**Ingest authentication** (`Bonvi.Api.IntegrationTests`):
1. No token → 401 `davomat.ingest_ruxsat_yoq`, nothing written.
2. Unknown token → 401, **the same code and message** as (1).
3. Valid token, `is_active = false` → 403 `davomat.qurilma_faolsiz`, nothing written.
4. Valid token, `auth_mode = TokenAndHmac`, missing/wrong signature → 401.
5. Valid token + correct HMAC → 200, one row.
6. Token in the header and token in the path both work and produce identical rows.
7. `allowed_ips` set and the source outside it → 401.
8. Body over 2 MB → 413 from Kestrel, handler never entered.
9. More than 200 events → 400 `davomat.hodisa_kop`.
10. The ingest endpoint is reachable **without** a JWT while `/api/attendance/passes` is not — pinning the deliberate asymmetry.

**Dedupe and idempotency:**
11. The same Hikvision body posted twice → 200 both times, `stored: 1` then `duplicates: 1`, exactly one row, exactly one file on disk.
12. Two events with the same `serialNo` from **different** devices → two rows (the key is per device).
13. Firmware without `serialNo`: same person, same second, same direction, twice → one row; a second apart → **two** rows.
14. A duplicate does not move `last_event_at` and does not clear `processed_at`.

**Parsing:**
15. Real-shaped Hikvision multipart (JSON part + JPEG part) → all mapped fields correct, photo stored, `raw_event` populated.
16. Multipart with no picture part → pass stored, `photo_key` null.
17. Picture part that is not an image by content inspection → pass stored, photo dropped.
18. `dateTime` without an offset → `offset_assumed = true`, `occurred_at` = local − 5 h.
19. `eventType` that is not `AccessControllerEvent` → 200, `stored: 0`, no row.
20. Plain-JSON body on a `Generic` device → stored; the same body on a `Hikvision` device → 400 `davomat.hodisa_oqilmadi` (parser is chosen by the registry, never sniffed).

**Time:**
21. Skew of 10 minutes → stored, `clock_skew_seconds ≈ 600`, device `last_skew_seconds` updated.
22. Skew of 40 days → 400 `davomat.vaqt_yaroqsiz`, nothing written.
23. A pass at 23:30 Tashkent has `local_date` = that day, not the UTC day. **This test fails if anyone stores local time in a `timestamptz`.**

**Mapping and unmatched:**
24. Pass for an unknown person id → stored with `resolved_employee_id = NULL`, and it appears in `/identities/unmatched` with `passCount = 1`.
25. Creating the identity resolves **all** historical passes for that id and clears their `processed_at`.
26. After the recompute job runs, the corresponding `attendance_days` rows exist.
27. Deleting the identity does **not** un-resolve past passes.
28. Creating a second identity for the same person id → 409 `davomat.moslik_takror`; creating a second identity for the same **employee** with a different person id → allowed.
29. Import: a fixture .xlsx with one exact-PINFL row, one exact-name row, one ambiguous, one unknown, one already-mapped → the preview classifies all five correctly and **writes nothing**.
30. Import confirm creates only the approved rows and reports the skipped ones.

**Timesheet maths** (`Bonvi.Application.Tests`, no database):
31. **AMENDED 2026-09-17 — this case was reversed. The old assertion (worked 540) is now wrong; do not keep it.** 09:00 in, 18:00 out on a 09:00–18:00 schedule, `break_minutes = 60`, **no break window** → worked 540, planned 480, late 0, early 0, overtime 60. **Pins the NULL-window branch of §5.5.1** — without a window there is no deduction, which is also what protects every schedule created before the amendment.
32. 09:15 in with `grace_in = 10` → late 5.
33. 09:08 in with `grace_in = 10` → late 0.
34. 17:40 out with `grace_out = 10` → early 10.
35. Odd passes ending on `In` → `MissingExit`, `early_leave_minutes = 0`.
36. First pass is `Out` → `MissingEntry`.
37. Ten passes (5 pairs) → the five intervals summed.
38. Two `In` passes 20 s apart → one counted, `ignored_pass_count = 1`; 20 **minutes** apart → both kept.
39. **Midnight crossing:** schedule 20:00–08:00, passes 19:52 Mon and 08:07 Tue → **one** row dated Monday, worked 735, overtime 15 (735 − 720: the 8-minute early arrival counts too — step 10 compares totals, not the tail after `plan_end_at`; *AMENDED 2026-09-17*, the earlier "7" contradicted step 10), `plan_end_at` on Tuesday.
40. Midnight crossing with a missing exit → `MissingExit` on Monday and **no** row on Tuesday.
41. No schedule + passes → `NoSchedule`, worked computed, planned/late/early 0.
42. Schedule says day off + passes → `DayOffWorked`, late/early 0.
43. Working day, no passes, active employment → `Absent`.
44. Terminated before the date, passes exist → `NotEmployed`.
45. Two employments, one primary → schedule and org snapshot from the primary; **one** row, not two.
46. Unknown-direction device → alternation assigns In/Out/In/Out and nothing is written back to `attendance_passes`.
47. Known and unknown directions mixed → known ones are never overwritten.
48. Schedule precedence: employee override beats position beats department beats default; a department assignment on a **parent** department applies to a child; the nearest ancestor wins.
49. Two assignments in the same tier with different `effective_from` → the later one wins on a date after it and the earlier one on a date before it.
50. Recompute is idempotent: run it twice, compare every column of every row.

**Break window — AMENDED 2026-09-17** (`Bonvi.Application.Tests`, no database).
All on 09:00–18:00, `break_minutes = 60`, window 13:00–14:00 unless stated.

51. **Never left the building** (09:00 in, 18:00 out) → `breakTaken = 0`, `breakDeduction = 60`, **worked 480**. Pins "stayed inside ⇒ full break deducted".
52. **Left for exactly the break** (09:00, 13:00, 14:00, 18:00) → `breakTaken = 60`, `breakDeduction = 0`, **worked 480**. Pins "left for lunch for the full window ⇒ deduction 0". ⚠️ Assert #51 and #52 produce the **same** `worked_minutes` in one test if you can — that equality is the entire point of the amendment, and a reader of the test file should not have to hold two numbers in their head to see it.
53. Left 13:00→13:30 (half the break) → `breakTaken = 30`, `breakDeduction = 30`, worked 480.
54. Left 12:30→14:00 (longer than the break) → `breakTaken = 60`, `breakDeduction = **0, never negative**`, worked 450.
55. Left 10:00→10:30 (outside the window) → `breakTaken = 0`, `breakDeduction = 60`, worked 450 — the half hour is missing from the sum **and** the break is charged.
56. Two exits inside the window (13:05→13:20, 13:40→13:55) → overlaps sum, `breakTaken = 30`.
57. Exit straddling the window edge (12:45→13:25) → only the in-window 25 minutes count.
58. **Went home before the window** (09:00 in, 12:55 out) → `breakTaken = 0`, `presentInWindow = 0`, **`breakDeduction = 0`, worked 235**. **AMENDED 2026-09-17 (§13 #14 decided): the earlier assertion of 60 / 175 is withdrawn — replace it, do not keep both.** Pins the presence cap: a window the employee was in the building for none of deducts nothing.
58a. **AMENDED — arrived mid-window** (13:30 in, 18:00 out) → `breakTaken = 0`, `presentInWindow = 30`, **`breakDeduction = 30`**, worked 240. Also pins the head rule: the time before `first_in_at` is not an outside span.
58b. **AMENDED — left inside the window, never returned** (09:00 in, 13:15 out) → `breakTaken = 0`, `presentInWindow = 15`, **`breakDeduction = 15`**, worked 240.
58c. **AMENDED — break shorter than the window, never left** (window 13:00–14:00, `break_minutes = 30`, 09:00 in, 18:00 out) → `presentInWindow = 60`, **`breakDeduction = 30`**, worked 510. Pins that the cap limits the charge and never raises it to the window's length.
59. `MissingExit` with a window (09:00, 13:00, 14:00, no exit) → closed sum 240, **`breakTaken = 0`** (AMENDED 2026-09-17 — corrected from 60: with one closed interval there is no gap *between closed intervals*), `presentInWindow = 0`, `breakDeduction = 0`, worked 240, status `MissingExit`.
60. **AMENDED 2026-09-17 — the deduction never exceeds the minutes worked.** A single closed interval 13:20→13:40 inside the window, `break_minutes = 60` → `presentInWindow = 20`, **`breakDeduction = 20`**, `worked_minutes = 0` — assert `breakDeduction == 20`, so the test fails if the zero came from the §5.5 step-5 floor instead of the cap. Add a property-style test over generated pass sequences asserting `breakDeduction <= presentInWindow <= closedSum` with the exact-truncation rule of §5.5.1; that inequality, not the floor, is what keeps `worked_minutes` non-negative.
61. **Overnight window** — shift 20:00→08:00, window 00:30→01:30, out 00:35 → in 01:10 → `breakTaken = 35`, `breakDeduction = 25`, still one row on the start date. **Also assert `breakWindowStart` is Tuesday 00:30 (Tashkent), not Monday.** Pins the window-to-instant formula of §5.5.1: composing the date with the raw clock value puts the window on Monday, `breakTaken` becomes 0 and the test fails — which is the only way a night-shift-only bug gets caught.
62. `DayOffWorked` → no window, no deduction, raw sum.
63. Window validation (`Bonvi.Domain.Tests`), with the codes B1 shipped: only one of the two set → `davomat.jadval_tanaffus_toliq_emas`; `breakStart == breakEnd` → `davomat.jadval_tanaffus_oynasi`; a window outside the shift → `davomat.jadval_tanaffus_oynasi`, tested on a **normal** shift *and* on an **overnight** one, in both directions (window before the start, window past the end); a **valid** overnight window (20:00→08:00 with 00:30→01:30) is **accepted**; `breakMinutes = 481` → `davomat.jadval_tanaffus_uzunligi`; a window on a non-working day is **accepted and nulled**; a window with `breakMinutes = 0` is **accepted**. Plus (the §6.9.1 rename landed 2026-09-17): empty employee id on an identity → `davomat.xodim_yoq`, empty schedule id on an assignment → `davomat.jadval_yoq`, both 400.
64. `planned_minutes` is unchanged by all of the above — it still subtracts `break_minutes` in every case, including the NULL-window one. Pins the comparability argument in §5.5 step 6.
64a. **AMENDED 2026-09-17 — persisted** (integration: recompute, then read the row). For the cases #51, #52, #53, #58, #58a and #58c, `attendance_days.break_taken_minutes == breakTaken` and `break_deduction_minutes == breakDeduction`; a synthesized timesheet cell (§5.7.1) carries `0` for both.

**Pass retention — AMENDED 2026-09-17:**

65. `attendance.passRetentionDays = 0` → the purge-passes job deletes **nothing**, even for a pass backdated ten years. First branch of §9.2.1.
66. Retention 365, a pass backdated 400 days with a photo still on disk → the row is gone **and** the file is gone. Pins "delete the file first".
67. The same pass's `attendance_days` row is **still there**, with identical `worked_minutes`, `late_minutes` and `status`. ⚠️ This is the test that stops the worst bug in the module; assert every numeric column, not just existence.
68. **AMENDED 2026-09-17** — a **matched** pass backdated 400 days with `processed_at IS NULL` → **skipped**, not deleted, and a warning is logged; an **unmatched** pass backdated 400 days → **deleted** together with its photo file (§9.2.1).
69. Saving `passRetentionDays = 30` while `photoRetentionDays = 60` → 400 `davomat.saqlash_muddati_nomuvofiq`; saving `0` in the same situation → **accepted** (the sentinel is not a smaller number).
70. With the stored pair inverted by hand, the job uses `max(pass, photo)` and strands no file.

**Former-employee alert — AMENDED 2026-09-17:**

71. A pass today for an employee whose only employment terminated last month → `GET /board` returns one `formerEmployeeAlerts` entry with the person's name, termination date, device and time — **without** the recompute job having run. Pins "the moment it happens".
72. The same, with `departmentId` and `search` filters applied that exclude the person → the alert is **still** returned. Pins "filters do not apply to the alert".
73. A pass for a currently employed person → the array is empty.
74. A pass 30 hours old for a leaver → not in the array (24-hour window).
74a. **AMENDED 2026-09-17 — one entry per person.** The same leaver passes three times within 24 h → `formerEmployeeAlerts` has **one** entry, referencing the newest pass, with `passCount = 3`; `GET /former-employee-alerts` returns `count = 1` and an `items` array equal, field for field, to the board's.
74b. **AMENDED — `GET /passes/{id}`** returns distinct `photoKey` (snapshot) and `employeePhotoKey` (profile photo) for a matched pass that has both; NULL `employee*`, `department` and `position` for an unmatched pass; 404 `davomat.otish_topilmadi` for an unknown id **and** for a pass removed by the retention job.

**RBAC** (`Bonvi.Api.IntegrationTests`) — the mandatory per-endpoint test:
75. A user with **no** attendance permission gets 403 on every endpoint in the §7.3 table.
76. A user with `attendance.view` gets 200 on board/passes/timesheet/reports/schedules-GET and **403** on devices, identities, import, recompute, and every write.
77. A user with `attendance.manage` but **not** `reports.export` gets 403 on all four `/export/*` endpoints.
78. The HR manager seed role has both permissions; the `recruiting_bot` role has neither.
79. `GET /devices` never returns a token; `POST /devices` returns it exactly once and a subsequent `GET /devices/{id}` does not.
80. **AMENDED** — both retention settings are writable only with `settings.manage`: an HR manager with `attendance.manage` gets 403 from `PUT /api/settings`. The retention is a disk-and-evidence policy, not day-to-day attendance work.

**Regression guards:**
81. `/openapi/v1.json` returns 200 — the `Guid = default` trap (§0.2 #8).
82. `Permissions.All` contains both new codes and `Exists()` returns true for them.
83. The architecture tests still pass (Application must not have gained an ASP.NET or Npgsql reference through the ingest work).

### 11.2 `test-migration` — mandatory, the model changed

1. `upgrade head` on an **empty** database succeeds; all seven tables exist with the exact index list from §2.
2. `upgrade head` on a **restored production backup** (from `backups/`, into a scratch database — ⚠️ never against production itself) succeeds and `SELECT count(*)` on `employees`, `employments`, `candidates` is unchanged.
3. The migration's `Up` contains **no** `DropColumn`, `DropTable`, `AlterColumn` or `RenameColumn` touching a pre-existing table.
4. `downgrade` to the previous migration drops exactly the seven new tables and leaves everything else intact; `upgrade head` again works.
5. The unique indexes actually bite: two identities with the same `(provider, external_person_id)` → violation; soft-delete the first, insert again → **allowed**.
6. `ux_schedule_assignments_scope` rejects a second `Default` assignment with the same `effective_from` (this is the `NULLS NOT DISTINCT` check — without it, the test passes for the wrong reason, so assert the constraint name in the error).
7. `ck_schedule_assignments_scope` rejects `scope = Department` with `employee_id` set.
7a. **AMENDED 2026-09-17** — `ck_work_schedule_days_break_window` (§2.4.0) rejects a row with `break_start` set and `break_end` NULL, and rejects a break window on a row with `is_working = false`; it **accepts** both-NULL on a working day, and **accepts** a window with `break_minutes = 0` (the shipped constraint has no `break_minutes` clause — §2.4.0). Assert the constraint name in the error, otherwise the test can pass because some other constraint fired.
7b. **AMENDED 2026-09-17** — `20260917072458_DavomatTanaffusNatijasi`: `upgrade` adds `attendance_days.break_taken_minutes` and `break_deduction_minutes` as `integer NOT NULL DEFAULT 0` and existing rows read `0`; `downgrade` drops exactly those two columns and nothing else. After the pre-merge consolidation (§2.12) this case folds into case 1 — the columns are then part of `CreateTable`.
8. `ux_attendance_passes_device_dedupe` rejects the replay.
9. Hangfire's schema appearing on first run does not break `upgrade head` or the EF model snapshot check.

### 11.3 `test-frontend`

1. `attendance-presentation.ts`: minutes → `h:mm` for 0, 59, 60, 540, 735; status → colour/label for all 8 values.
2. The timesheet grid renders a full month for an employee with three stored days and 28 synthesized ones without crashing.
3. `./bonvi check` passes: 4 locales aligned, all 41 error codes present, no `t(...)` key missing.
4. `./bonvi pnpm build` passes with the generated types in use.

---

## 12. Definition of Done

- [ ] `./bonvi test` green, including every case in §11.1 — in particular the
      RBAC table (§11.1 #75–80) and the midnight-crossing maths (#39, #40)
- [ ] **AMENDED 2026-09-17** — the break-window pair passes: "never left" and
      "left for exactly the window" both yield the **same** `worked_minutes`
      (§11.1 #51, #52), a schedule day with **no** window deducts nothing
      (#31), and — **AMENDED 2026-09-17** — a window the employee was in the
      building for none of deducts nothing either (#58, §13 #14). If #31 still asserts the pre-amendment 540 with a window set,
      the rule was implemented from the stale text — stop and re-read §5.5.1
- [ ] **AMENDED 2026-09-17** — the pass purge deletes a backdated pass **and**
      its photo file, leaves the `attendance_days` row byte-identical
      (§11.1 #66, #67), and does nothing at all when
      `attendance.passRetentionDays = 0` (#65)
- [ ] **AMENDED 2026-09-17** — a pass by a terminated employee raises the
      red, non-dismissible board alert **within one refresh cycle** and before
      any recompute job runs, and the same count shows on the nav badge and
      the devices page (§8.4, §8.7, §11.1 #71–74)
- [ ] `./bonvi check` green — 4 locales aligned, all 41 error codes from §6.9
      translated, no template key unaccounted for
- [ ] `./bonvi gen` succeeds (i.e. `/openapi/v1.json` is not 500) **and** the
      frontend uses the generated DTO types, not hand-written copies
- [ ] `./bonvi pnpm build` passes
- [ ] exactly **one** new EF migration exists **at merge** — **AMENDED
      2026-09-17:** today there are two (`DavomatModuli` +
      `DavomatTanaffusNatijasi`), consolidated once before merge when no
      builder is running (§2.12; **do not squash earlier**) — it creates seven
      tables (including `work_schedule_days.break_start` / `break_end`,
      `attendance_days.break_taken_minutes` / `break_deduction_minutes` and
      `ck_work_schedule_days_break_window` — §2.4.0, §2.6), and it contains no
      destructive statement against any pre-existing table
- [ ] the migration is applied to a **restored production backup** in a
      scratch database and the existing data is intact afterwards.
      ⚠️ Never against the production database itself — that needs the user's
      explicit go-ahead first, and the point of this check is to find the
      surprise *before* that conversation
- [ ] `Employee.cs` and `Employment.cs` are **byte-identical** to their state
      on `main` (§2.11)
- [ ] every new endpoint is RBAC-protected except the two ingest routes, and
      those two are the only `.AllowAnonymous()` writes in the system
- [ ] a real (or curl-simulated) Hikvision multipart post creates one pass
      with a stored photo; the identical post again creates nothing
- [ ] the ingest token never appears in `logs/` — verified by grepping the
      error log and `docker logs api` after a round of ingest traffic
- [ ] mapping an unmatched person id makes their historical passes appear in
      the tabel **without** anyone pressing "recompute"
- [ ] the photo purge job deletes a backdated photo, nulls `photo_key` and
      `raw_event`, and leaves the pass row intact
- [ ] both retention settings are visible and editable on `/sozlamalar`
      (they are in the `GROUPS` list — §8.9), and `passRetentionDays` shows
      its «0 — hech qachon» hint permanently
- [ ] the live board refreshes every 30 s without blanking, and shows the
      clock-skew banner when a device's clock is pushed 10 minutes forward
- [ ] the timesheet grid's employee and totals columns stay frozen while the
      day columns scroll, inside the `h-dvh overflow-hidden` shell
- [ ] the four Excel exports open in Excel with real date cells and summable
      numeric duration columns
- [ ] the «Verifikatsiya» section is visible to an HR manager, its
      manage-only items are hidden from a `attendance.view`-only user, and the
      whole section is absent for a user with neither permission
- [ ] `docs/ASSUMPTIONS.md` has one line for each §13 decision the user did
      not answer

---

## 13. Open questions — each with a standing decision

If the user says nothing, these decisions stand. Log them in
`docs/ASSUMPTIONS.md` in the existing one-line format.

> **Three of these were answered on 2026-09-17.** #1 and #3 were decided
> **against** the standing decision and #4 was confirmed with a demand
> attached. Their text below is now the **owner's answer**, not my
> recommendation — implement what is written here. #14 is new and still open.

1. ✅ **ANSWERED 2026-09-17 — REVERSED. Is the unpaid break subtracted from
   worked minutes?**
   *My standing decision was: **no**. The owner decided: **yes**, against a
   defined window.*
   `work_schedule_days` gains `break_start` / `break_end` (§2.4.0);
   `breakDeduction = max(0, min(break_minutes − breakTaken, presentInWindow))`
   where `breakTaken` is the overlap between the window and the periods the
   employee was outside the building, and `presentInWindow` the overlap with
   the time he was inside (the cap was added by #14 below);
   `worked_minutes` subtracts it (§5.5.1). With **no**
   window there is **no** deduction — which is what keeps every pre-existing
   schedule behaving as before.
   ⚠️ **If you are reading an older copy of this SPEC, or a summary of it, the
   rule you have is wrong.** The reversed rule lives in §5.5.1, the edge cases
   in §5.7, the tests in §11.1 #51–64. This changes every hours figure in the
   system, which is why it is still question #1.

2. **Photo retention default — 60 days?**
   *Decision: 60 days, configurable 7…365* (§2.9). The owner's number. At 60
   days the volume holds ~13 GB, which the VPS can carry; 365 days would be
   ~80 GB and would need a disk conversation first.

3. ✅ **ANSWERED 2026-09-17 — CHANGED. Is a pass ever deletable?**
   *My standing decision was: **never, and rows are kept forever**. The owner
   decided: **not by hand — but the archive ages out on a policy.***
   New setting `attendance.passRetentionDays`, Number, default **365**, min 30,
   max 3650, **`0` = never delete** (§2.9). The nightly
   `attendance:purge-passes` job (§9.2.1) deletes expired pass rows and their
   photo files. **`attendance_days` is never deleted by it** — the tabel, the
   hours and the lateness numbers outlive the raw evidence.
   What did **not** change: a pass is still not hand-deletable and there is
   still no delete endpoint (§1.2 #5). The distinction, restated there, is
   that a retention policy is uniform, declared in advance and visible to
   everyone, whereas hand-deletion is selective, silent and after the fact.
   The first is records management; only the second destroys the record's
   authority.

4. ✅ **ANSWERED 2026-09-17 — CONFIRMED, with a demand attached. What happens
   to passes of an employee who was terminated but whose badge still works?**
   *The owner's words: **a terminated employee must not get through the
   turnstile.***
   v1 is unchanged in substance — we record and flag, HR removes the person
   from the device by hand, and the write-back seam stays **unbuilt**
   (§1.2 #1). But the flagging must be **load-bearing, not a status value
   buried in a table**: the pass raises a red, non-dismissible alert at the
   **top of the live board**, a compact warning on the devices page, and a
   danger-toned nav badge — within one refresh cycle, computed from the pass
   itself and not from the nightly recompute (§6.5, §8.4, §8.7).
   Also recorded: **the owner intends to turn the write-back seam on later**
   (§2.13). It is committed future work now, not speculation — do not prune
   it, and do not let a refactor quietly close it.

5. **Does the live board include people whose device cannot report direction?**
   *Decision: no, and the screen says so* (§6.5). Alternation on a
   half-finished day would state confidently that someone is inside when he
   left through a different door.

6. **Clock-skew tolerance — 5 minutes; hard rejection beyond 30 days?**
   *Decision: yes* (§3.8). Under 5 minutes the device is fine; between 5
   minutes and 30 days the data is still true about *who* passed and is used
   with a flag; beyond 30 days the timestamp is garbage and would scatter rows
   across meaningless dates.

7. **Bounce window — 60 seconds?**
   *Decision: 60 s* (`AttendanceRules.BounceSeconds`). Two reads of the same
   direction within a minute are one person and one door. A longer window
   would swallow a genuine quick step-out; a shorter one leaves double reads
   in the log.

8. **Are `attendance.view` and `attendance.manage` both given to the HR
   manager role?**
   *Decision: yes* (§7.2). Whoever reads the tabel is whoever fixes the
   mapping behind it; splitting them makes every unmatched pass wait for the
   boss. The separation still exists for any future narrower role.

9. **Is the ingest endpoint listed in the OpenAPI document?**
   *Decision: yes* (§3.3). Hiding it is obscurity, not security, and it would
   break `./bonvi gen` for the ingest DTOs used by tests.

10. **Day-window slack — ±6 hours, ±1 calendar day?**
    *Decision: yes* (§5.3). Covers an early arrival and long overtime on a
    12-hour night shift without letting an afternoon pass be claimed by
    yesterday's shift.

11. **Recompute cadence — every 15 minutes?**
    *Decision: 15 minutes, plus a nightly full pass over the last 3 days*
    (§9.2). The tabel is not a realtime artefact; the board is, and the board
    reads passes directly. A tighter loop would rewrite the same rows all day
    for no reader.

12. **`Cycle` (2/2) schedules — seam only, or build them?**
    *Decision: seam only* (§2.4.2, §1.2 #7). The rotation rules are not known,
    night shifts across midnight already work, and the seam
    (`WorkScheduleKind` + the `day_index` semantics) means adding cycles later
    is a resolver change, not a schema rewrite.

13. **Who may change the two retention settings?** — raised by the
    2026-09-17 amendment.
    *Decision: `settings.manage` only, i.e. the super administrator* (§8.9,
    §11.1 #80). They sit on the existing `/sozlamalar` page and inherit its
    permission; no new permission is introduced. Reason: `passRetentionDays`
    decides when evidence is destroyed and `photoRetentionDays` decides how
    much disk the VPS needs. Neither is day-to-day attendance work, and
    `attendance.manage` is held by every HR manager (§7.2). If the head of the
    company should be able to set them, the fix is to move the keys out of
    `settings.manage`, not to widen that permission.

14. ✅ **DECIDED 2026-09-17 — against my recommendation. Should the break
    deduction be limited to the time the employee was present in the window?**
    *My recommendation was **no** — implement the formula exactly as first
    given. The coordinator decided **yes**, applying the rule as it was put to
    the owner, "nobody is punished twice", which charging a lunch to someone
    who was not in the building contradicts.*
    `presentInWindow` = minutes of closed intervals overlapping the window;
    `breakDeduction = max(0, min(break_minutes − breakTaken, presentInWindow))`
    (§5.5.1). Consequences: leaving at 12:55 before a 13:00 window deducts 0
    (§5.5.1 row 6, §11.1 #58); arriving at 13:30 deducts 30 (#58a); leaving at
    13:15 without returning deducts 15 (#58b); a 30-minute break in a 60-minute
    window, never leaving, deducts 30 (#58c). The deduction can never exceed
    the minutes worked (§5.5.1).
    Attribution, for anyone tempted to reverse it: this was decided by the
    coordinator on the owner's stated rule, not by the owner directly. If the
    owner rules otherwise, this entry and §5.5.1 change together.
