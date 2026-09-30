# DATA MODEL — Employee app (PWA): work sites, accounts, mobile check-in

> Status: design, authoritative for the schema of this wave. Written 2026-09-22.
> Branch: `verification`. Companion to `SPEC-xodim-ilovasi.md` (§D is the sketch;
> this file is the decided version).
> Language: English, same rule as the SPECs — code comments stay Uzbek, error
> codes stay Uzbek.
> Scope: **design only**. No code, no migration files. Every table, column,
> constraint, index and delete rule below is a decision with a stated reason and
> a written-down rejected alternative.

---

## 0. Read this first — decisions the owner has to make, and one bug

### 0.1 ⚠️ OWNER DECISION — one existing table changes shape

`employees` gains **`mobile_check_in_allowed boolean NOT NULL`** (SPEC §C). This
is the only existing table this wave touches. Everything else is new tables, new
enum values, and one new row in `attendance_devices`.

The house rule for this wave was "no existing table changes shape except by
adding a **nullable** column". A nullable flag would mean three states
(`true` / `false` / `NULL`) for a two-state question, and every read would have
to write `?? false` — the third state is a lie we would have to keep
translating. So: `NOT NULL`, and the owner is asked to approve that one line.

Cost of the change is genuinely small: `employees` holds ~950 rows, and
`ALTER TABLE … ADD COLUMN … NOT NULL DEFAULT false` is **metadata-only** on
PostgreSQL 11+ (this deployment is 18.6). Lock is `ACCESS EXCLUSIVE` for roughly
a millisecond — see §E.3 for the lock-queue caveat, which is the real risk, not
the rewrite.

**Rejected alternative:** put the flag on `employee_accounts` instead, so no
existing table is touched at all. Rejected because the flag answers *"does this
person's job happen away from a terminal?"* — a fact about the person's work,
not about their login. It must survive a deactivate/re-activate of the account,
and HR must be able to list allowed people including those who have no account
yet. Putting it on the account would silently reset that HR decision the first
time an account is re-issued, and nobody would notice until a driver could not
mark for a week.

### 0.2 ⚠️ A bug the SPEC's model would ship, with a one-year fuse

`attendance_passes` is **hard-deleted** by the retention job — not soft-deleted:

```
api/src/Bonvi.Infrastructure/Repositories/AttendanceRepositories.cs:434
    .ExecuteDeleteAsync(cancellationToken)
```

Default retention is `AttendanceRules.DefaultPassRetentionDays = 365`. If
`mobile_check_ins.pass_id` carries the module's usual `ON DELETE RESTRICT`, the
purge job starts throwing a foreign-key violation **exactly one year after the
first mobile mark**, and it fails for every batch thereafter — the disk keeps
growing and the failure surfaces as a nightly job error nobody connects to this
wave.

`mobile_check_ins.pass_id` is therefore **`ON DELETE CASCADE`**, deliberately and
as the one place in this design where CASCADE is right: the coordinate has no
meaning without the pass it explains, and the pass's retention policy must stay
the module's single retention rule. Note that `ExecuteDeleteAsync` bypasses EF's
change tracker, so the **database-level** cascade is what makes this work — an
EF-side `DeleteBehavior.Cascade` alone would not fire.

### 0.3 Two smaller corrections to the SPEC

* **§D2 column names.** The SPEC says `failed_attempts`. The existing table calls
  it `failed_login_attempts` (`User.FailedLoginAttempts`). Match the codebase.
* **§D4's safety net is half a net.** `PassMethod` really is guarded — the
  frontend has `METHOD_LABEL_KEY: Readonly<Record<PassMethod, string>>`
  (`web/src/features/verification/lib/attendance-presentation.ts:313`), which
  fails to compile until `Mobile` is added. `DirectionSource` has **no**
  `Record<DirectionSource, …>` map anywhere in `web/src`, so
  `DirectionSource.Employee` will arrive silently. Whatever renders it needs a
  map added by hand in this wave, or the new value shows as a raw string.

### 0.4 One thing the SPEC does not mention at all

The employee PWA needs a **token pair** (§E), which means refresh tokens. They
cannot go in `refresh_tokens`: that table's FK is
`user_id → users(id) ON DELETE CASCADE` and `user_id` is `NOT NULL`. A new table
is required — see §B.4.

---

## A. `work_sites` — the geofence catalogue

### A.1 Shape

`WorkSite : ReferenceEntity` in `Bonvi.Domain.Attendance`, table `work_sites`,
configured through `builder.ConfigureReference("work_sites")`.

Inheriting `ReferenceEntity` buys, in one line and with no chance of forgetting
one of them: `id`, `name` (with the `UzbekNames` ICU collation), `is_active`,
`note`, `IAuditable` (`created_at/by`, `updated_at/by`), `ISoftDeletable`
(`deleted_at/by`), the `(is_active, name)` listing index, and the house rule
that `name` is `text` with no length cap.

| Column | Type | Null | Default | Constraint |
|---|---|---|---|---|
| `id` | `uuid` | no | — | PK |
| `name` | `text` | no | — | `ux_work_sites_name` on `lower(name)`, `WHERE deleted_at IS NULL` |
| `latitude` | `numeric(9,6)` | no | — | `ck_work_sites_coordinates`: `BETWEEN -90 AND 90` |
| `longitude` | `numeric(9,6)` | no | — | same check: `BETWEEN -180 AND 180` |
| `radius_meters` | `integer` | no | — (domain default 200) | `ck_work_sites_radius`: `BETWEEN 50 AND 5000` |
| `is_active` | `boolean` | no | — | — |
| `note` | `text` | yes | — | free text, from `ReferenceEntity` |
| audit + soft-delete | | | | from `ReferenceEntity` |

Nothing here is nullable except `note`, and `note` is nullable because "no
comment" is a real and common state that no default can stand in for.

### A.2 `numeric(9,6)` — what a GPS coordinate actually needs

9 total digits, 6 after the point → 3 digits before it. Latitude needs at most
2 (±90), longitude at most 3 (±180). Both fit exactly, with no room for a
nonsense value to be stored.

Six decimal places is **~11 cm** of latitude, and at Tashkent's latitude (41°N)
**~8 cm** of longitude. GPS accuracy in practice is 5–20 m outdoors and
20–100 m indoors (SPEC §B). The stored precision is therefore two orders of
magnitude finer than the measurement — there is nothing left to gain and the
column stays narrow.

**Rejected: `double precision`.** Two reasons, neither of them the money rule
(this is not money). First, a coordinate is displayed, re-entered and compared
by humans; a value that round-trips as `41.31108099999999` in a JSON response
generates support tickets forever. Second, any future SQL-side comparison on a
float column is a trap waiting for someone to write `latitude = 41.311081`.

**Rejected: `numeric(9,7)` or finer.** 1.1 cm of resolution for a 200-metre
fence. That is not precision, it is decoration.

**Rejected: PostGIS `geography(Point,4326)`.** Confirmed — see §A.4.

**Rejected: a tighter CHECK restricted to Uzbekistan's bounding box**
(lat 37–46, lon 55.9–73.2). It would catch the single most likely data-entry
error — swapped lat/long, which the global check does *not* catch because
(69.24, 41.31) is a perfectly valid coordinate in Somalia. But the holding could
open a site abroad, and then the fix is a migration under time pressure. The
bounding-box test belongs in the application as a **warning on the HR form**
("this coordinate is not in Uzbekistan — is it right?"), overridable, not as a
constraint. Written down here so it does not get re-invented as a CHECK.

### A.3 Radius bounds: 50–5000 metres

* **Floor 50 m.** Indoor GPS accuracy runs to 100 m. A 20-metre fence would
  reject people who are standing inside the building, and the failure looks to
  them like the app being broken. 50 m is the smallest fence that is not a
  guaranteed false-negative generator.
* **Ceiling 5000 m.** Past a few kilometres the fence stops meaning "at the
  site" and starts meaning "in the city" — at which point it is not a control,
  it is a checkbox someone ticked. The ceiling is what stops a support request
  ("it keeps rejecting him") from being answered with `radius = 50000`.

The bound lives in **both** the domain (`WorkSite.Create` / `Reconfigure`) and
the database — the same rule `work_schedule_days` states: a domain check cannot
guard a row that arrives by migration or hand-written SQL.

### A.4 Distance check: application + haversine — **confirmed**, with a firmer reason

The SPEC's reason ("a handful of sites and one comparison per mark do not
justify an extension that every future deployment would have to install") is
correct as far as it goes. Two stronger ones:

1. **Volume.** The check-in endpoint runs at most ~100 times a day (≈50 allowed
   employees × 2 marks). Each run compares one point against ≤ 20 active sites.
   That is 2 000 haversine evaluations per day — microseconds of CPU, total. No
   index, no extension and no query planner can beat "fetch 20 rows and loop"
   at this size, because the fetch already dominates.
2. **PostGIS is a deployment dependency, not a library.** It has to be installed
   in the container image, enabled per-database by a superuser, version-matched
   on restore, and it makes `pg_dump`/restore between machines fragile. The
   project runs Docker-only on a 2 GB VPS (see the server note in the project
   memory). Taking on a C extension to avoid a 12-line function is the kind of
   trade that is invisible on day one and expensive on the day the database has
   to be moved.

**What would overturn this:** sites in the hundreds *and* a query like "which
site is nearest to each of the last 10 000 marks". Neither exists. If both ever
do, the migration path is additive — a `geography` column alongside the two
numerics, backfilled, with a GiST index. Nothing in this design blocks it.

**One thing the application layer must get right, because SQL is not there to
catch it:** the haversine implementation must use a single agreed Earth radius
constant (6 371 008.8 m, the IUGG mean) in one place. Two copies with two
constants produce two different answers to "is he inside", and the difference
shows up only at the fence edge — the exact place people argue.

### A.5 Uniqueness, soft delete, audit

* **`ux_work_sites_name` — UNIQUE on `lower(name)` WHERE `deleted_at IS NULL`.**
  Raw SQL in the migration, as with the other nine reference tables: EF's fluent
  API cannot express an index over an expression. Case-insensitive because
  «Qo'qon ombori» and «qo'qon ombori» are one place, and the partial filter
  because a deleted site's name must become reusable.
* **Soft delete: yes.** `mobile_check_ins.work_site_id` is `RESTRICT`, so a used
  site can never be hard-deleted; soft delete is the only deletion HR gets, and
  it keeps every historic check-in explainable.
* **`is_active` vs `deleted_at`.** Two different statements, both needed:
  `is_active = false` means "marks are rejected here from now on, but it is
  still a place we have" (a seasonal warehouse); `deleted_at` means "this row
  was a mistake or is retired". The check-in path filters on
  `is_active AND deleted_at IS NULL`; the HR catalogue shows inactive rows and
  hides deleted ones — identical to every other reference table.
* **Audit: yes**, via `IAuditable`. "Who widened the fence to 3 km?" is a
  question that will be asked, and `updated_by` is the only place the answer can
  live.

### A.6 Which permission guards it — flagged, not decided here

SPEC §F puts the screen under **Ma'lumotnomalar → Obyektlar**, which implies
`catalog.view` / `catalog.manage`. But `radius_meters` is an attendance control,
not a label: someone with `catalog.manage` and no `attendance.manage` could
widen every fence to 5 km and nothing in the permission model would object.

**Recommendation:** read under `catalog.view`, **write under
`attendance.manage`**. One catalogue with a split permission is unusual in this
codebase, which is why it is flagged rather than assumed.

---

## B. `employee_accounts` — sign-in for ~950 employees

### B.1 Shape

> ⚠️ **Superseded in part on 2026-09-27 (§H):** `login`, `must_change_password`,
> `is_active`, `failed_login_attempts`, `locked_until` and
> `ux_employee_accounts_login` were dropped, `password_protected` was added.
> The table below is the original design.

`EmployeeAccount : AggregateRoot, IAuditable` in `Bonvi.Domain.Identity`, table
`employee_accounts`. **Not** `ISoftDeletable` — see §B.6.

| Column | Type | Null | Constraint / note |
|---|---|---|---|
| `id` | `uuid` | no | PK, `Guid.CreateVersion7()` like every other aggregate |
| `employee_id` | `uuid` | no | FK → `employees(id)` **RESTRICT**; `ux_employee_accounts_employee` UNIQUE |
| `login` | `text` | no | normalised phone; `ux_employee_accounts_login` UNIQUE |
| `password_hash` | `text` | no | same hasher as `users` |
| `must_change_password` | `boolean` | no | `true` at issue and at every HR reset |
| `password_set_at` | `timestamptz` | no | when the current hash was written, by whoever wrote it |
| `is_active` | `boolean` | no | HR's manual off switch |
| `last_login_at` | `timestamptz` | **yes** | `NULL` = never signed in — a real, load-bearing state |
| `failed_login_attempts` | `integer` | no | same name and rule as `users` |
| `locked_until` | `timestamptz` | **yes** | `NULL` = not locked |
| `created_at/by`, `updated_at/by` | | | `IAuditable`; `*_by` carry no FK, per the house rule for audit columns |

Only two nullable columns, and each one has a stated meaning that no default
value can express.

**`password_set_at` is not in the SPEC and is worth its 8 bytes.** It answers
three questions that otherwise have no answer: "how old is this one-time
password that is sitting unused in a drawer" (`must_change_password AND
last_login_at IS NULL AND password_set_at < …`), "when did this person last
change their own password", and — the reason `updated_at` will not do — it
survives the next unrelated edit. `updated_at` is overwritten the moment HR
toggles `is_active`.

**Rejected: a separate `password_changed_at` next to `password_issued_at`.**
Two columns for one fact, kept in sync by hand. `password_set_at` plus
`must_change_password` distinguishes "HR issued it" from "the employee set it"
with no second column and no way for the two to disagree.

**Rejected: `employee_id` as the primary key.** It would enforce one-account-per-
employee structurally and save an index. Rejected because every aggregate in
this codebase keys on `Entity.Id`, `employee_refresh_tokens` needs a stable
account key that is independent of the person, and re-pointing a PK is the one
schema change that is never cheap.

### B.2 The login format — how `users` normalises, and matching it exactly

`users.login` is a `PhoneNumber` value object
(`api/src/Bonvi.Domain/ValueObjects/PhoneNumber.cs`), persisted through an EF
value conversion to a plain string column with **no length cap** (`text`), with
a dedicated `ValueComparer` so EF does not emit spurious UPDATEs.

The normalisation, exactly:

1. keep ASCII digits only — `+998 90 508 88 88`, `90-508-88-88` and `905088888`
   all collapse to digits;
2. 9 digits → prepend `998`;
3. 10–15 digits not starting with `998` → rejected as `telefon.mamlakat_kodi`
   ("only Uzbek numbers"), a *different* error from a wrong length, on purpose;
4. anything not exactly 12 digits → `telefon.uzunlik`;
5. first digit of the operator code may not be `0` or `1` → `telefon.operator`;
6. stored as `"+" + digits`, i.e. **`+998905088888`**, always 13 characters.

`employee_accounts.login` reuses `PhoneNumber` verbatim — same value object,
same converter, same comparer, same `text` column. Not a copy of the rules: the
rules themselves.

Three consequences that have to be written down:

* **An employee login and an HR login may be the same phone number.** Two
  tables, two unique indexes, no shared constraint. This is correct, not a bug:
  an HR manager who is also an employee legitimately has both, with two
  passwords, and the endpoint (`/api/auth/login` vs
  `/api/employee-auth/login`) decides which table is consulted. Cross-table
  uniqueness would need a trigger or a shared login table, and would buy nothing.
* **The login is a *copy* of `employees.phone`, not a reference.** *(Reversed
  by the owner on 2026-09-27 — the login is now `employees.phone` itself; §H.)* HR's form
  pre-fills from the employee card; after that the two live separate lives.
  Rejected alternative: derive the login from `employees.phone` at read time —
  then correcting a typo in an HR field silently becomes an authentication
  event, and the person is locked out with no trace of why.
* ⚠️ **`employees.phone` is not unique.** The index is
  `HasIndex(Phone).HasFilter("phone IS NOT NULL")` — a search index, nothing
  more (`EmployeeConfiguration.cs:166`), and duplicate phones are a known
  problem in the imported data. `ux_employee_accounts_login` **is** unique, so
  the second person sharing a number cannot get an account until HR fixes the
  duplicate or gives one of them a different login. At 950 people this will
  happen in the first week. The error must say so in plain Uzbek — not
  "conflict" — or HR will conclude the feature is broken.

### B.3 Password hashing, the one-time password, lockout

* **Hashing: reuse `IPasswordHasher` / `BonviPasswordHasher` unchanged** —
  PBKDF2-HMAC-SHA512, 100 000 iterations, per-password 128-bit salt, with
  `NeedsRehash` already handled. Same interface, same injected service, no
  second implementation. A second hasher is a second thing to get wrong.
* **One-time password.** HR creates the account → the server generates the
  password, stores only its hash, sets `must_change_password = true` and
  `password_set_at = now`, and returns the clear text **once** in the create
  response. Nothing stores it; there is no endpoint that can return it again.
  Identical to the device ingest token (`AttendanceDevice.TokenPrefix` and its
  "shown once" comment) — the same pattern, so HR already knows the ritual.
  A reset repeats exactly this and does **not** clear `last_login_at`: "has this
  person ever signed in" must survive a password reset.
* **Lockout: copy `users` exactly** — 5 consecutive failures → `locked_until =
  now + 15 min`, and `failed_login_attempts` resets to `0` at the same moment.
  Worth knowing about the behaviour being copied: because the counter resets,
  **the lockout never escalates** — an attacker gets 5 tries every 15 minutes
  forever. That is a deliberate anti-griefing choice in `User`
  (`User.cs:258-274`) and consistency beats cleverness here.
* ⚠️ **Lockout as a prank vector.** With 5 accounts, "anyone who knows your
  login can lock you out for 15 minutes" is theoretical. With 950 accounts
  whose logins are each other's phone numbers, it is a Tuesday. This is not
  fixed by the schema: the fix is an IP rate limit on
  `/api/employee-auth/login` using the existing `RateLimitingExtensions`
  policies. Named here so it is not discovered during the security review.

### B.4 `employee_refresh_tokens` — the table the SPEC forgot

`refresh_tokens.user_id` is `NOT NULL` with `ON DELETE CASCADE` to `users`.
Employee accounts are deliberately not rows in `users` (SPEC-sorovlar §C), so
they cannot use it.

New table, mirroring `RefreshToken` field for field:

| Column | Type | Null | Note |
|---|---|---|---|
| `id` | `uuid` | no | PK |
| `employee_account_id` | `uuid` | no | FK → `employee_accounts(id)` **CASCADE** |
| `token_hash` | `text` | no | SHA-256 hex; UNIQUE |
| `expires_at` | `timestamptz` | no | |
| `created_at` | `timestamptz` | no | |
| `created_by_ip` | `text` | yes | not yet known at issue time in some paths |
| `revoked_at` | `timestamptz` | yes | `NULL` = live |
| `revoked_by_ip` | `text` | yes | only set when revoked |
| `replaced_by_token_hash` | `text` | yes | rotation chain; `NULL` = end of chain |

**`ON DELETE CASCADE` here, deliberately** — the same reasoning `RefreshToken`
already states: a refresh token that outlives its account is a security hole,
and this is the one relationship where the child is worthless without the
parent. It is also the only path by which an account row can ever be hard-
deleted without leaving live credentials behind.

**Rejected: add `employee_account_id uuid NULL` to `refresh_tokens` and make
`user_id` nullable.** That is a shape change to an existing table, it breaks the
invariant "a refresh token belongs to a user", and every read path that touches
`token.UserId` (repository, rotation, revoke-all, logout) would have to learn a
second subject type. Two small tables beat one table with two meanings.

**Rejected: no refresh tokens — a long-lived access token instead.** A JWT
cannot be revoked server-side (the `JwtTokenService` comment says so
explicitly). "The employee lost their phone" would then have no answer short of
rotating the signing key and logging out the entire company.

**Config note, not schema:** employee tokens carry audience `bonvi-employee`,
`sub` = the **account id** (not the employee id, so that a re-issued account
produces distinguishable tokens), an `emp` claim for the employee id, and **no
permission claims at all** (SPEC §D2). `JwtOptions` needs a second
audience/lifetime pair; that is configuration, not a migration.

### B.5 The exact relationship to `employees` — termination and soft delete

**FK: `employee_id → employees(id) ON DELETE RESTRICT.`** Never CASCADE.
`employees` is soft-deleted in normal operation, so a hard `DELETE` is either an
accident or a repair script — and in both cases it must stop and ask rather than
silently take the credential record with it. This matches
`attendance_identities`, `attendance_days` and `absence_requests`, all of which
are `Restrict` against `employees` for the same reason.

⚠️ **Soft delete of the employee does not, by itself, stop the sign-in.** The
account row is found by `login`, not by walking from the employee, so EF's
global `deleted_at IS NULL` query filter on `Employee` never gets a chance to
apply. Without an explicit check, a soft-deleted employee keeps signing in
forever. No constraint can express this. It is therefore a **named invariant
with a required test**:

> **INV-1.** `/api/employee-auth/login` loads the `Employee` through the normal
> (filtered) query and refuses if it is not found.

The same handler enforces the employment rule:

> **INV-2.** Sign-in is refused when the employee has no employment covering
> today — the same `EmployedOn(today)` predicate the device roster already uses
> (`EnrollmentReadModel.RosterAsync`).

INV-2 is the rule that cannot be forgotten. HR's manual `is_active = false` is
the *other* mechanism, for a different question: "employed, but suspended".
Two switches, two meanings, neither substituting for the other.

* **Termination takes effect on the employment dates**, not on the moment HR
  types it in — consistent with how terminated people leave the terminal roster.
* **Re-hire reopens the account automatically**, old password and all, because a
  new `Employment` makes INV-2 pass again. That is defensible (it is the
  person's own password) but HR should be prompted to reset it. Not built now;
  recorded so it is a decision rather than a surprise.
* **Failure responses must not distinguish these cases.** "Wrong password",
  "terminated" and "soft-deleted" return the same error to an unauthenticated
  caller; the reason goes to the log. Otherwise the login endpoint becomes a
  free API for "does this phone number belong to a current Bonvi employee".

### B.6 No soft delete on `employee_accounts`

`is_active` is the off switch, `IAuditable` records who flipped it, and
`audit_logs` (`AuditAction.Update` / `Login` / `LoginFailed`) records the
history. There is no question that a `deleted_at` on this table would answer.

The cost of adding it anyway would be concrete: both unique indexes
(`employee_id`, `login`) would need `WHERE deleted_at IS NULL`, which means a
soft-deleted account no longer blocks a new one — and that is exactly the wrong
behaviour for a credential row, where "this login is taken" should stay true.
Precedent in this codebase for "no soft delete when there is no history
question": `RefreshToken`, `AttendancePass`, `DeviceEnrollment`,
`WorkScheduleDate`.

---

## C. `mobile_check_ins` — the coordinate behind an accepted mark

### C.1 Verdict: keep it out of `attendance_passes` — **confirmed, reason overturned**

The SPEC's stated reason is wrong about PostgreSQL:

> "Four columns that are null for 99 % of rows would sit in every one of those
> reads."

They would not. PostgreSQL stores NULLs in a per-row null bitmap — a NULL costs
one *bit*, not the column's width. `attendance_passes` has ~20 columns, so the
bitmap is already 3 bytes (`ceil(20/8)`); four more columns keeps it at 3 bytes
(`ceil(24/8)`). The storage cost of the rejected design is, precisely, **zero**.
And every hot read is column-projected anyway (`PassRowProjection`,
`BoardPass`), so the wide table would not be read any wider.

The decision is still right, for four reasons that do hold:

1. **`attendance_passes` stores what the device said, not what we concluded.**
   That is the aggregate's central documented invariant — the reason the
   timesheet's direction guesses are never written back to it. A coordinate the
   *employee's phone* claimed, plus a site the *server* matched and a distance
   the *server* computed, are conclusions. They do not belong in the evidence
   row.
2. **The write path.** The ingest handler is the module's hottest and riskiest
   code — it relies on catching a unique-violation as "duplicate". Widening the
   entity it constructs, for a feature that writes ~100 rows a day, puts the
   2-million-row-a-year path at risk to save a join.
3. **A nullable `work_site_id` on `attendance_passes` would be its fifth FK**,
   pointing at a soft-deletable catalogue, on the table the retention job
   hard-deletes from. That is new coupling on the one table that must stay
   boring.
4. **The 99 %-NULL column invites the next one.** Once `latitude` is on the
   pass, the selfie key and the battery level follow, and in two waves the
   evidence table has a feature column for every client that ever wrote to it.

**The read patterns that decide it,** since that is what was asked:

| Read | Rows | Needs the join? |
|---|---|---|
| Timesheet month (`GET /api/me/attendance`, HR grid) | 950 × 31 | **No** — reads `attendance_days` |
| Live board (30 s poll) | ~7 000 passes | **No** — `BoardPass` projects 5 columns |
| Journal list page | 50 passes | Yes, for the mobile ones: one `WHERE pass_id = ANY(…)` |
| Journal row detail | 1 pass | Yes, one indexed lookup |
| Timesheet cell tooltip "came from the phone" | per cell | **No** — `attendance_passes.method = Mobile` answers it |
| Excel export | a month of passes | Yes if the site column is exported — one extra batched query |

Only the journal joins, and the journal already fans out to `DeviceLabelsAsync`,
`EmployeeCardsAsync`, `EmploymentsAsync` and `CatalogNamesAsync` for every page.
One more batched `WHERE pass_id = ANY(…)` is the pattern that file is built out
of.

### C.2 Shape

`MobileCheckIn : Entity` (not an aggregate root — it is only ever reached
through its pass), no `IAuditable`, no `ISoftDeletable`, exactly like
`AttendancePass`: nobody edits a check-in and nobody deletes one by hand.

| Column | Type | Null | Constraint |
|---|---|---|---|
| `id` | `uuid` | no | PK |
| `pass_id` | `uuid` | no | FK → `attendance_passes(id)` **CASCADE** (§0.2); `ux_mobile_check_ins_pass` UNIQUE |
| `employee_id` | `uuid` | no | FK → `employees(id)` **RESTRICT** |
| `work_site_id` | `uuid` | no | FK → `work_sites(id)` **RESTRICT** |
| `latitude` | `numeric(9,6)` | no | `BETWEEN -90 AND 90` |
| `longitude` | `numeric(9,6)` | no | `BETWEEN -180 AND 180` |
| `accuracy_meters` | `numeric(5,1)` | no | `>= 0` |
| `distance_meters` | `numeric(6,1)` | no | `>= 0` |
| `site_radius_meters` | `integer` | no | `BETWEEN 50 AND 5000` |
| `created_at` | `timestamptz` | no | server now, = the pass's `occurred_at` |

**Nothing is nullable.** A row exists only for an *accepted* mark, and an
accepted mark has, by construction, a site, a distance and a coordinate.

**`site_radius_meters` is not in the SPEC and it is the column that makes the
row mean something.** The acceptance decision is `distance_meters <=
radius_meters`. If only the distance is frozen and the radius is read live from
`work_sites`, then the day HR widens a fence from 200 m to 2 000 m, every
historic row becomes unjudgeable — "180 m, accepted" and "1 400 m, accepted" sit
side by side with no way to tell which was correct at the time. Freezing both
operands is the same reasoning `AttendancePass` uses for copying
`identity_provider` off the device instead of joining to it.

**Not frozen: the site's coordinates.** `distance_meters` already encodes the
relationship to the centre at that moment; freezing the centre as well would
answer no additional question.

**`employee_id` is deliberate duplication** of `attendance_passes.
resolved_employee_id`. The justification is not performance (the table holds
~36 000 rows a year): it is that the two columns record *different facts*. The
check-in row says "this signed-in employee sent this coordinate" — the claim as
made. The pass column says "this pass was attributed to this employee" — a
resolution that the backfill job (§4.3 of the verification SPEC) is allowed to
write later. They happen to agree for every row this design creates, and the
single writer guarantees it; no constraint can express it cheaply, so it is
stated as an invariant:

> **INV-3.** `mobile_check_ins.employee_id` equals the `resolved_employee_id` of
> the referenced pass at insert time. Enforced by the single writer.

**`accuracy_meters numeric(5,1)`, not `integer`.** Validation rejects anything
over 100 m, so the stored range is 0–100 and `numeric(5,1)` is generous. The one
decimal is there because rounding `0.4` to `0` would display as perfect
accuracy, which is a lie the tooltip would tell.

**`distance_meters numeric(6,1)`** — max 99 999.9, which comfortably exceeds the
5 000 m radius ceiling while still expressing that the value is bounded.

**`created_at` kept, not derived from the pass.** Two reasons: an evidence row
that cannot be dated without a join is a poor evidence row; and it is the value
that a future approval flow would replay as the pass's `occurred_at` (§F.3) —
without it, approving at 18:00 a mark made at 09:00 would record a 09:00 arrival
as an 18:00 one.

**Rejected: `ip_address` / `user_agent`.** Tempting as a spoofing signal, but
Uzbek mobile carriers NAT everything, so the IP distinguishes almost nothing,
and the user agent is a string the client chooses. Personal data with no
question attached to it.

---

## D. How a mobile mark becomes an `AttendancePass`

### D.1 The synthetic device row — confirmed, and costed against the alternative

`AttendancePass.DeviceId` stays `NOT NULL`. Mobile marks point at one row in
`attendance_devices` with a **well-known constant id**
(`AttendanceDevice.MobileAppId`), inserted by migration.

**Cost of the rejected alternative — nullable `DeviceId` — every read path,
searched, not guessed:**

| # | File / line | What changes |
|---|---|---|
| 1 | `Domain/Attendance/AttendancePass.cs:80` + `Record(…)` | `Guid` → `Guid?` on the property and the factory |
| 2 | `Configurations/AttendanceConfigurations.cs:300` | FK becomes optional |
| 3 | **`AttendanceConfigurations.cs:324`** | ⚠️ `ux_attendance_passes_device_dedupe` on `(device_id, dedupe_key)` — see below |
| 4 | `AttendanceConfigurations.cs:337` | `ix_attendance_passes_device_time` still works, NULLs indexed |
| 5 | `ReadModels/AttendanceReadModel.cs:79, 99` | `PassRow.DeviceId` + `PassRowProjection` |
| 6 | `AttendanceReadModel.cs:264, 311–314` | `DeviceLabelsAsync(…)` input must filter nulls; three `GetValueOrDefault(row.DeviceId)` become null-conditional |
| 7 | `AttendanceReadModel.cs:481, 546` | `BoardPass.DeviceId` + its projection |
| 8 | `AttendanceReadModel.cs:671, 704` | board's `DeviceLabelsAsync` + lookup |
| 9 | `AttendanceReadModel.cs:789, 811, 853–854` | former-employee alerts: projection, labels, two lookups |
| 10 | `AttendanceReadModel.cs:2611, 2627, 2630, 2640` | unmatched groups: `Dictionary<key, Guid>` → `Guid?`, and `lastDevice[key]` feeds a non-null DTO field |
| 11 | `AttendanceReadModel.cs:2390–2393` | device event counts: `GroupBy(pass => pass.DeviceId)` yields a nullable key, and `ToDictionary` on it breaks |
| 12 | `Dtos/AttendanceDtos.cs:102` | `AttendancePassDto.DeviceId` → `Guid?` |
| 13 | `Dtos/AttendanceDtos.cs:182` | pass-detail DTO → `Guid?` |
| 14 | `Dtos/AttendanceDtos.cs:906` | `LastDeviceId` → `Guid?` |
| 15 | `Repositories/AttendanceRepositories.cs:124` | `HasPassesAsync` still compiles; semantics unchanged |
| 16 | Frontend generated types + `attendance-passes-page.tsx:249`, `employee-attendance-tab.tsx:546`, `attendance-pass-photo-dialog.tsx:311`, `attendance-former-employee-alert.tsx:90` | the UI already handles `deviceName === null`, but `deviceId: string` → `string \| null` regenerates through orval and ripples |
| 17 | Tests: `AttendanceReadModelTests.cs:311`, `AttendanceIngestApiTests.cs:861, 1299`, `AttendanceRetentionJobsTests.cs:319` | assertions on a now-nullable `Guid` |

**Row 3 is the one that decides it.** PostgreSQL treats NULLs as *distinct* in a
unique index by default. With `device_id IS NULL`, the module's single most
important index — the one the ingest path deliberately relies on by catching its
violation as "duplicate" — **silently stops deduplicating mobile marks**. It is
fixable (`NULLS NOT DISTINCT`, PG 15+, already used for
`ux_schedule_assignments_scope`), but the fix means rebuilding a unique index on
a two-million-row table, and the failure mode if it is missed is duplicate
attendance rather than an error.

**Cost of the synthetic row, honestly:** one migration INSERT, three command
guards, one enum value, one dropdown exclusion. Roughly 17 files against 6.
Confirmed.

### D.2 What the synthetic row actually contains — the parts the SPEC left open

| Column | Value | Why |
|---|---|---|
| `id` | a fixed constant `AttendanceDevice.MobileAppId` | the only way code can recognise it; a name lookup would break the first time someone renames it |
| `name` | «Mobil ilova» | the column HR already reads in the journal |
| `provider` | **new** `AttendanceProvider.Mobile` | see §D.3 |
| `identity_provider` | **new** `AttendanceIdentityProvider.Mobile` | see §D.5 |
| `direction_role` | `ReportsDirection` | the direction does arrive in the event — the employee sends it |
| `auth_mode` | `Token` | unused; the row never authenticates |
| `token_hash` | **32 zero bytes** | see below |
| `token_prefix` | `"00000000"` | display only |
| `is_active` | **`false`** | see below |
| `location_note`, `serial_number`, `model`, `legal_entity_id`, `department_id`, `allowed_ips`, `hmac_secret` | `NULL` | none of them describe this row |

**`token_hash` = 32 zero bytes.** The column is `NOT NULL`, exactly 32 bytes by
domain invariant, and carries a unique index **with no soft-delete filter** (a
deleted device's token must stay dead). The authenticator looks the device up by
`SHA256(presented_token)`. An all-zero SHA-256 output is unreachable in
practice, so this row can never be authenticated — deterministic, requires no
`pgcrypto`, and needs no secret to exist anywhere. This must be documented on
`AttendanceDevice` so that nobody later "fixes" it by rotating the token.

**`is_active = false`, and that is the truth, not a hack.** `is_active` means
"this device's token is accepted". A row whose token can never be accepted is,
by definition, inactive. It also closes two real holes for free:

* `AttendanceDeviceAuthenticator` refuses inactive devices before anything else
  runs (`AttendanceDeviceAuthenticator.cs:114`) — so the ingest endpoint and the
  agent roster are both shut, with no new code.
* ⚠️ **`EnrollmentReadModel.EmployeeRowsAsync` lists every *active* device for
  an employee card.** With `is_active = true`, every one of the 950 employee
  cards would sprout a permanent «Mobil ilova — Rasm yo'q / kutilmoqda» row that
  never resolves, because there is no agent to enrol anyone into a phone. The
  SPEC's §D5 did not account for this. `is_active = false` makes it disappear
  with no code change.

**Guards required (code, not schema)** — the SPEC says the row "is not deletable
from the devices screen"; that needs to be three guards, not one:

* `DeleteDeviceCommandHandler` — refuse `MobileAppId`. (It already refuses any
  device that has passes, but on day 1 there are none.)
* `SetDeviceActiveCommandHandler` — refuse activating `MobileAppId`, or the
  enrollment problem above comes back.
* `RotateDeviceTokenCommandHandler` and `UpdateDeviceCommandHandler` — refuse
  `MobileAppId`; rotating would issue a working ingest token for the app, which
  is precisely what the SPEC forbids.

### D.3 `AttendanceProvider.Mobile` — a fourth enum addition the SPEC did not list

`provider` says which **parser** reads this device's events. There is no parser
for the app — the check-in handler builds the pass directly. Labelling the row
`Generic` ("plain JSON — intermediary agent, ZKTeco adapter and all tests")
would put a small lie in the data and show «Generic» as the provider chip on
every mobile row in the journal.

`AttendanceProvider.Mobile = 3` is additive (the column is `integer`; no DB
migration). Its cost, found by searching:

* `web/src/features/verification/api/attendance-api.ts:219` —
  `ATTENDANCE_PROVIDERS = Object.values(AttendanceProvider)` feeds the
  **device registration form's** provider dropdown. Without a filter, HR could
  register a real terminal as `Mobile`, and ingest would then throw
  `InvalidOperationException` at `_parsers.Single(…)` — a 500, not a validation
  error. **Two fixes, both required:** exclude `Mobile` from
  `ATTENDANCE_PROVIDERS`, and reject `provider = Mobile` in
  `RegisterDeviceCommand` / `UpdateDeviceCommand` with a proper Uzbek error
  code. The server guard is the real one; the dropdown is courtesy.

### D.4 `PassMethod.Mobile` and `DirectionSource.Employee`

Both are stored as `integer` (EF's default enum mapping; there is no PostgreSQL
enum type anywhere in this schema) and serialised as **strings** on the wire.
Adding a value is therefore a **code-only change — no migration** — and old
rows keep their meaning.

* `PassMethod.Mobile = 7` — "identified because they were signed in".
* `DirectionSource.Employee = 4` — "the direction came from the person, not from
  a device or from a device's configured role".

Frontend impact, verified: `METHOD_LABEL_KEY: Readonly<Record<PassMethod,
string>>` breaks the build until extended — the intended safety net, and it
works. `PASS_METHODS = Object.values(PassMethod)` picks up the new filter option
automatically. **`DirectionSource` has no such map**, so `Employee` arrives
silently (§0.3).

### D.5 `raw_person_id` for a pass that had no device to read a number from

`raw_person_id` is `NOT NULL`, ≤ 64 chars, and the domain rejects blank
(`davomat.shaxs_raqami_bosh`). It has to hold something true.

**Decision: the employee's `id` (a 36-character uuid), in a new identity space
`AttendanceIdentityProvider.Mobile = 5`.**

`AttendanceIdentityProvider` answers "*whose* numbers are these" — a question
deliberately kept separate from "how is it parsed". The app's answer is: these
are Bonvi employee ids, and the person is known because they signed in. That is
literally true, it is stable, it is unique, it cannot collide with a Verifix,
Hikvision or ZKTeco number (different space *and* an unmistakable format), and
it fits `MaxExternalPersonIdLength` with room to spare.

Rejected:

* **the Verifix person number from `attendance_identities`** — the employee may
  not have one (employees with no photo are never enrolled), so the field would
  be un-fillable for exactly the field workers this feature is for; and it would
  claim a terminal read a number it never read;
* **the login phone number** — PII in the module's hottest table, and it
  changes;
* **the literal string `"mobile"` for everyone** — destroys the meaning of
  `(identity_provider, raw_person_id)`, which is the key the unmatched screen and
  the backfill job group by;
* **`AttendanceIdentityProvider.Manual`** — it means "hand-entered numbers, for
  test and debug". Mixing production marks into the debug space makes the debug
  space untrustworthy.

⚠️ Consequence: `Mobile` appears in the identities screen's provider dropdown,
where HR could hand-create a `Mobile` identity. Harmless (nothing resolves
through that space) but confusing — exclude it from the *create* form, keep it
in the *filter*.

Mobile passes are resolved at insert, so they never appear in
`ix_attendance_passes_unmatched` (partial, `WHERE resolved_employee_id IS NULL`)
and never reach the unmatched screen. Correct by construction.

### D.6 The dedupe key

**Reuse the existing `t:` format verbatim**, produced server-side:

```
t:{employeeId}|{occurredAt:yyyyMMdd'T'HHmmss UTC}|{(int)direction}
```

≈ 58 characters, well inside the 200-character column. Same builder, same rules,
no new format for anyone to learn — and the uniqueness is per
`(device_id, dedupe_key)`, where all mobile passes share one `device_id`, so the
employee id in the key is what keeps two people's simultaneous marks apart.

**Rejected: a client-supplied idempotency key** (the `s:` form, which exists for
Hikvision's `serialNo`). The client here is an employee's phone — a
hostile-capable input. A replay with a fresh key would walk straight past the
dedupe index. Server-derived only.

⚠️ **The gap this leaves, and how to close it.** The key truncates
`occurredAt` to the second, and `occurredAt = server now`. Two POSTs 1.2 seconds
apart produce *different* keys, so the unique index does not stop them — only
the 60-second bounce rule does (SPEC §E4), and that rule is a read-then-write:
two concurrent requests can both read "no recent mark". On a flaky phone
connection with a double tap, this is reachable.

**Mitigation, no new schema:** the check-in handler runs in one transaction and
takes a row lock on the `employee_accounts` row it has already loaded
(`SELECT … FOR UPDATE`) **before** the bounce check. That serialises one
employee's check-ins against each other and nothing else. The account row is the
natural lock anchor: exactly one per employee, already in hand, and never
contended by any other code path.

**Rejected: a partial unique index over `date_trunc('minute', created_at)`.**
It has a boundary hole (two marks either side of :59.9/:00.0) and encodes a
business rule in an index expression where nobody will find it.

### D.7 The time columns, which are easy to get silently wrong

`AttendancePass.Record` derives five stored columns from the single
`occurredAt` argument. For a mobile mark the handler **must** pass
`now.ToOffset(BonviTime.TashkentOffset)`, not UTC:

| Column | With Tashkent offset (correct) | With UTC (wrong) |
|---|---|---|
| `occurred_at` | the instant, UTC | same |
| `device_local_time` | 10:30 | **05:30** |
| `device_offset_minutes` | 300 | **0** |
| `local_date` | correct Tashkent date | wrong for marks between 19:00 and midnight |
| `clock_skew_seconds` | 0 | 0 |

Passing UTC produces a journal that shows every phone mark five hours early and a
`local_date` that is off by one for evening marks — which lands the mark on the
wrong day of the timesheet. `offset_assumed` is `false`: the server did not
assume the offset, it chose it.

`received_at = occurred_at = server now`, so `clock_skew_seconds = 0` and
`HasClockDrift` is `false`. A phone mark cannot have clock drift, because the
server never reads the phone's clock — which is §B of the SPEC, expressed in
columns.

---

## E. Indexes and migration order

### E.1 Every endpoint's query, and the index that serves it

| Endpoint / query | SQL shape | Index |
|---|---|---|
| `POST /api/employee-auth/login` | `WHERE login = ?` | `ux_employee_accounts_login` |
| — its employee check (INV-1/2) | `employees` by PK, `employments WHERE employee_id = ?` | existing |
| refresh / rotate | `WHERE token_hash = ?` | `ux_employee_refresh_tokens_hash` |
| logout-all, expiry sweep | `WHERE employee_account_id = ? AND expires_at …` | `ix_employee_refresh_tokens_account_expires` |
| `POST /api/employee-auth/password` | by PK | PK |
| `GET /api/me` | employee by PK (+ the new flag, same row) | PK |
| `GET /api/me/attendance?year&month` | `attendance_days WHERE employee_id = ? AND local_date BETWEEN ?` | **existing** `ux_attendance_days_employee_date` |
| — its pass synthesis | `attendance_passes WHERE resolved_employee_id = ? AND occurred_at …` | **existing** `ix_attendance_passes_employee_time` |
| `POST /api/me/check-in` step 1 | account by PK + employee by PK | PK |
| — step 3, nearest site | `work_sites WHERE is_active AND deleted_at IS NULL` | `ix_work_sites_is_active_name` (≤ 20 rows; the scan is the plan) |
| — step 4, bounce rule | `attendance_passes WHERE resolved_employee_id = ? AND occurred_at > now()-60s` | **existing** `ix_attendance_passes_employee_time` — exact match, backwards scan, 1 row |
| — insert pass | — | `ux_attendance_passes_device_dedupe` (write) |
| — insert check-in | — | `ux_mobile_check_ins_pass` (write) |
| `GET /api/me/requests`, `POST …` | `absence_requests WHERE employee_id = ? …` | **existing** `(employee_id, from_date, to_date)` |
| HR journal: site + distance on a page | `mobile_check_ins WHERE pass_id = ANY(?)` | `ux_mobile_check_ins_pass` |
| HR employee card → «Ilova» | `WHERE employee_id = ?` | `ux_employee_accounts_employee` |
| HR catalogue «Obyektlar» | `WHERE is_active ORDER BY name` | `ix_work_sites_is_active_name` |

**Result worth stating plainly: this wave adds no index to any existing table.**
Every query against the attendance tables lands on an index that is already
there — which is a good sign that the feature is shaped like the module it is
joining.

### E.2 The complete new index list — seven, each with its query

| Index | Table | Definition | Serves |
|---|---|---|---|
| `ux_work_sites_name` | `work_sites` | UNIQUE `lower(name)` WHERE `deleted_at IS NULL` (raw SQL) | blocks duplicate site names |
| `ix_work_sites_is_active_name` | `work_sites` | `(is_active, name)` — from `ConfigureReference` | the catalogue list and the check-in's site scan |
| `ux_employee_accounts_employee` | `employee_accounts` | UNIQUE `(employee_id)` | one account per employee; the HR card lookup |
| `ux_employee_accounts_login` | `employee_accounts` | UNIQUE `(login)` | sign-in; blocks duplicate logins |
| `ux_employee_refresh_tokens_hash` | `employee_refresh_tokens` | UNIQUE `(token_hash)` | every refresh |
| `ix_employee_refresh_tokens_account_expires` | `employee_refresh_tokens` | `(employee_account_id, expires_at)` | revoke-all and the expiry sweep |
| `ux_mobile_check_ins_pass` | `mobile_check_ins` | UNIQUE `(pass_id)` | one row per pass; the journal join |

**Deliberately absent, with the trigger that would add them:**

* `mobile_check_ins (employee_id, created_at)` — nothing reads it yet. Add it
  when the "phone marks" anomaly report ships ("show me this person's marks and
  how far away each one was").
* `mobile_check_ins (work_site_id)` — add it when a site card shows its mark
  count.
* `employees (mobile_check_in_allowed) WHERE mobile_check_in_allowed` — a
  partial index in the style of the existing `needs_review` one. Add it when a
  screen lists allowed employees across all 950.
* **No index on the FK columns of `mobile_check_ins`.** PostgreSQL does not
  index foreign keys automatically, and an unindexed referencing column costs a
  sequential scan of the child **only when the parent row is deleted**.
  `work_sites` and `employees` are soft-deleted (never `DELETE`d), so that scan
  never happens. Writing this down so it is a decision rather than an omission.
* **No spatial index.** ≤ 20 rows; see §A.4.

### E.3 Migration order — five migrations, one decision each

| # | Name | Contents | Depends on |
|---|---|---|---|
| M1 | `AddWorkSites` | `work_sites` + both its indexes (the `lower(name)` one as raw SQL) + both CHECKs | — |
| M2 | `AddEmployeeAccounts` | `employee_accounts` + `employee_refresh_tokens` + their four indexes | — |
| M3 | `AddEmployeeMobileFlag` | the one column on `employees` | — |
| M4 | `AddMobileCheckIns` | `mobile_check_ins` + its FKs + CHECKs + unique index | M1 |
| M5 | `SeedMobileAppDevice` | the one `attendance_devices` row | — |

M3 is its own migration on purpose: it is the **only** migration in this wave
that touches an existing table, so it should be one line long, reviewable at a
glance, and revertible on its own.

**M3, step by step** (the add → backfill → NOT NULL sequence, collapsed
correctly rather than crammed):

1. `ALTER TABLE employees ADD COLUMN mobile_check_in_allowed boolean NOT NULL DEFAULT false;`
   On PG 11+ this is **metadata-only** — no table rewrite, no backfill pass. The
   950 existing rows get `false` by the catalog default, which is also the
   semantically correct value: nobody was allowed before this wave existed.
2. `ALTER TABLE employees ALTER COLUMN mobile_check_in_allowed DROP DEFAULT;`
   Required by the house convention stated at the top of
   `AttendanceConfigurations.cs`: **no `HasDefaultValue` anywhere in this
   schema** — the row always carries the value the domain chose. EF's generated
   `AddColumn(nullable: false, defaultValue: false)` leaves a permanent DB
   default that the model snapshot does not know about, so it has to be dropped
   explicitly.

There is no separate-migration-per-step here because there is no old column, no
data to transform and no window during which the column is half-populated. The
three-step dance exists to avoid a long `NOT NULL` validation scan and a
rewrite; neither happens.

**Downgrades — all five lose data. Every one needs the header
`# WARNING: data loss — backup required`:**

| Migration | `Down` | What is lost |
|---|---|---|
| M1 | `DROP TABLE work_sites` | every site HR entered; blocked by M4's FK until M4 is down first |
| M2 | `DROP TABLE` ×2 | 950 password hashes and every session. Re-upgrading means re-issuing every one-time password by hand |
| M3 | `DROP COLUMN` | HR's per-person decisions. Re-upgrading resets everyone to `false`, silently |
| M4 | `DROP TABLE mobile_check_ins` | the coordinates behind accepted marks. The **passes survive**, so the timesheet is unaffected — the evidence is not |
| M5 | `DELETE FROM attendance_devices WHERE id = <const>` | **fails by design** once any mobile pass exists (FK `RESTRICT` from `attendance_passes`). Write it as `DELETE … WHERE id = ? AND NOT EXISTS (SELECT 1 FROM attendance_passes WHERE device_id = ?)` so it is a clean no-op instead of an error |

Downgrade order is M5 → M4 → M3 → M2 → M1.

**M5 must be idempotent** — `INSERT … ON CONFLICT (id) DO NOTHING` — so that a
re-run, a restored dump, or a test fixture that seeds it does not produce a
duplicate-key failure.

**Lock risk.** Every table here is new or small, so the rewrite risk is zero.
The real risk is the **lock queue**:

* M4 adds a FK from an empty child to `attendance_passes` (~2 M rows). Creating
  the constraint validates the **child**, not the parent, so there is no scan of
  the big table — but it briefly takes `SHARE ROW EXCLUSIVE` on
  `attendance_passes`, which conflicts with the `ROW EXCLUSIVE` that every
  ingest `INSERT` holds. Milliseconds normally; **indefinite** if a long-running
  report is holding a lock, and everything that arrives behind the waiting DDL
  queues up too.
* M3 takes `ACCESS EXCLUSIVE` on `employees` for about a millisecond, with the
  same queue caveat.

**Mitigation for both:** `SET lock_timeout = '3s';` before the DDL, so a blocked
migration fails fast and retries instead of stalling the terminals, and deploy
in the quiet window (terminals are silent between roughly 21:00 and 06:00
Tashkent).

---

## F. What this model makes hard later — the honest list

### F.1 Multiple sites per employee — **already works; the opposite is the work**

Today there is no employee ↔ site link at all: a mark is accepted at the
**nearest active site within its radius**, whichever that is. An employee who
visits four warehouses is already fully supported, with no schema change.

What is *not* supported is **restricting** a person to specific sites. That
needs a new join table (`employee_work_sites`) and a decision about what "no
rows" means (any site, or no site — and getting that wrong locks out everyone
who has no assignment). Cost: one table, one step in the validation chain. Not a
corner.

The part that is genuinely lost: historic check-ins record **where**, not
**whether it was permitted**. If the question "was he allowed at that site on
that date" ever has to be answered about the past, it cannot be — a
`was_assigned` column would have to be backfilled with a guess. Mitigating that
today would mean building the assignment model before anyone has asked for it,
which is a worse trade.

### F.2 Per-site working hours — **this is the real corner**

Schedules resolve through `schedule_assignments`, whose four-level scope
(`Employee > Position > Department > Default`) is nailed down by
`ck_schedule_assignments_scope` — a CHECK that enumerates exactly four valid
row shapes — and by `ux_schedule_assignments_scope`, a `NULLS NOT DISTINCT`
unique index over the same four columns. Adding a `WorkSite` scope means:

* a new enum value **and** a rewrite of that CHECK constraint (a shape change on
  an existing table);
* a rewrite of that unique index;
* a new nullable `work_site_id` on `schedule_assignments`;
* an answer to a genuinely ambiguous precedence question — is a site stronger
  than a position? A guard at the Qo'qon site who is also "Qorovul" has two
  schedules and nothing in the current hierarchy says which wins.

And then the part that is not a schema problem at all: **the computation would
become circular.** `AttendanceDayBuilder` resolves the schedule for an
`(employee, date)` pair *before* it knows anything about where the person was,
and §5.3 uses that schedule's window to decide which date each pass belongs to.
A site-scoped schedule would make the schedule depend on the passes, and the
passes depend on the schedule.

**Honest statement: per-site working hours are not a data-model tweak, they are
a redesign of §5.2 and §5.3 of `SPEC-verifikatsiya.md`.** If the owner wants
them, that has to be said before Wave B, not after.

### F.3 A check-in that needs approval before it counts — **cheap, if we leave the seam**

Today the handler writes an `AttendancePass` immediately, and the pass is the
fact the timesheet is built from. There is no "pending pass" state and there
must not be one: `AttendancePass` has no status column and no edit path by
design.

Two ways to add approval later:

* **(a) A status column on `attendance_passes`.** A shape change on the hot
  table, plus a filter in `AttendanceDayBuilder`, the board, the journal, the
  exports, the dirty queue and the retention job — six places where forgetting
  one produces wrong attendance silently. **Do not do this.**
* **(b) Do not write the pass until approval.** `mobile_check_ins` grows a
  `status` column, `pass_id` becomes nullable, and the pass is created by the
  approval handler. Cost: one `ALTER COLUMN … DROP NOT NULL` (a widening — no
  rewrite in PostgreSQL), one column, one partial unique index
  (`UNIQUE (pass_id) WHERE pass_id IS NOT NULL`), one endpoint. **Zero change to
  the attendance engine.**

This design keeps `pass_id NOT NULL` **today**, because there is no pending
state and a nullable column with no NULL to explain is exactly the kind of
"just in case" field that turns into a mystery in a year. Path (b) is recorded
as a described-not-built seam, in the same style this codebase already uses for
`DeviceAuthMode.TokenAndHmac` and `WorkScheduleKind.Cycle`.

One detail that makes (b) work, and the reason `created_at` earns its place on
`mobile_check_ins`: when the pass is finally created, its `occurred_at` must be
the **check-in** instant, not the approval instant. Approving at 18:00 a mark
made at 09:00 must not record a 09:00 arrival as an 18:00 one.

### F.4 The rest of the honest list

* **One account per employee.** `ux_employee_accounts_employee` means a person
  holding two employments in two legal entities gets one login. Correct today —
  one person, one phone — and it matches the `Employee` / `Employment` split.
  Relaxing it later means dropping a unique index and answering "which employee
  context does this token act in", which touches the token claims.
* **Duplicate `employees.phone` blocks the second account** (§B.2). This will
  happen. It is data hygiene, not schema, but it will look like a bug.
* **Changing the login format later is cheap only before the passwords are
  handed out.** If 950 phone-number logins turn out to be too error-prone and
  the company wants employee numbers, the migration is 950 rows — but every
  one-time password has to be re-issued unless the new login is derivable. Do
  that thinking now, not after the first 200 people have signed in.
* **`work_sites` can never be hard-deleted once used** (`RESTRICT` from
  `mobile_check_ins`). Intended. Soft delete is the only deletion HR gets.
* **`mobile_check_ins` is deleted by the passes' retention policy** (§0.2), so a
  coordinate older than `attendance.passRetentionDays` is gone. If the
  coordinates ever need to outlive the passes — a labour dispute a year later —
  that is a *different* retention rule and the CASCADE has to be replaced by a
  nulled-out `pass_id` plus its own purge job. Cheap to change, expensive to
  discover after the first year has been purged.
* **Nothing records what the fence was *before* an edit.** `site_radius_meters`
  on the check-in freezes the threshold at decision time, which covers the only
  question that matters ("was this mark correctly accepted?"). What is still
  unanswerable is "why was he rejected last Tuesday" — rejections are not
  stored at all. Storing them would mean a row per failed attempt on a table
  that is otherwise evidence of accepted facts, and an obvious abuse channel
  (anyone can generate rows by tapping). Deliberately not built; the rejection
  reason goes to the log.
* **The lockout is a griefing vector at 950 accounts** (§B.3). Schema cannot fix
  it; the rate limiter must.

---

## G. Changes after the audit (2026-09-27)

* **§B.3 lockout — superseded.** Login failures are no longer counted on the
  account row. They are counted per *(typed login, source IP)* in process
  (`EmployeeLoginThrottle`): unknown numbers lock identically (the lock no
  longer tells anyone that a number is an employee's), and an attacker locks
  their own IP, not the colleague everywhere. `failed_login_attempts` /
  `locked_until` stay, but now only count **wrong current passwords** on
  `POST /api/employee-auth/password` (a session that tries to guess its own
  password). No schema change. The in-process state assumes one API instance
  (true today); a second instance means moving it to Redis.
* **`absence_requests` gains two nullable columns** (migration
  `AbsenceRequestSubmitter`, additive, no default, no FK — same as
  `created_by` / `decided_by`): `submitted_by_account_id` and
  `cancelled_by_account_id` hold the employee's **app account** when the
  employee created/withdrew the request from the app. `created_by` /
  `decided_by` stay HR `users` ids only — an employee account id is never
  written there (a cancel from the app used to write `Guid.Empty` into
  `decided_by`).
* **Rejected check-ins** are still not stored (§F.4); the reason, accuracy, site
  and distance are logged (warning level, so the file log keeps them). The
  coordinate is not logged.

---

## H. Owner decision (2026-09-27) — migration `EmployeeAccountsFromProfilePhone`

The owner rejected the manual account flow (SPEC §M). One migration changes
`employee_accounts`; no other table changes shape.

| Column | Type | Null | Note |
|---|---|---|---|
| `id` | `uuid` | no | PK |
| `employee_id` | `uuid` | no | FK → `employees(id)` RESTRICT; `ux_employee_accounts_employee` UNIQUE — also the race guard for automatic creation |
| `password_hash` | `text` | no | same hasher as `users` |
| `password_protected` | `text` | **yes** | **new** — `ISecretProtector` (AES-GCM, `v1.<base64url>`) copy of the plain password so HR can view it; `NULL` for rows created before this migration (the plain value is unrecoverable; the old password keeps working until HR generates a new one) |
| `password_set_at` | `timestamptz` | no | when HR last gave a password |
| `last_login_at` | `timestamptz` | yes | unchanged |
| `created_at/by`, `updated_at/by` | | | `IAuditable` |

**Dropped:** `login` + `ux_employee_accounts_login` (the login is
`employees.phone`, looked up at sign-in over the existing
`ix_employees_phone` partial index — two point lookups: phone → at most two
non-deleted employees, then `ux_employee_accounts_employee`);
`must_change_password` (the employee never changes the password);
`is_active` (no manual off switch — termination is the switch; existing
`false` rows are thereby re-opened); `failed_login_attempts` / `locked_until`
(they only served the removed password-change endpoint; the login lock is
`EmployeeLoginThrottle`, §G).

**Invariants after the change**

* **INV-1/INV-2 unchanged** and now also enforced on every `/api/me/*` request
  (`EmployeeAccessGate`: account → non-deleted employee → employment covering
  today), so termination stops an already-issued access token immediately.
* A phone held by **two or more non-deleted** employees resolves to no account
  (sign-in refused, generic error). A soft-deleted employee's phone counts for
  nobody.
* Every non-deleted employee gets exactly one account (Hangfire job
  `employee-app:provision-accounts`, startup + every 15 min, plus
  ensure-on-read). Creation races end in `ux_employee_accounts_employee`
  (23505), which `PostgresFault.IsDuplicateEmployeeAccount` turns into "re-read
  the winner".

**`Down`** restores the columns: `is_active = true`, `must_change_password =
false`, lockout empty, and `login` rebuilt from `employees.phone` — or the
account id when the phone is empty or held by another account's employee, so
the restored unique index cannot fail. `password_protected` is lost.
