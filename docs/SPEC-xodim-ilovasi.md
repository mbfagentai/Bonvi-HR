# SPEC — Employee app (PWA): remote check-in and my attendance

> Status: authoritative for this wave. Written 2026-09-22. Branch: `verification`.
> Language: English, same rule as `SPEC-verifikatsiya.md` (§0.2): **code comments
> stay Uzbek**, error codes stay Uzbek, UI text goes through i18n in 4 languages.
> Source: the owner, 2026-09-22.

---

## A. What the owner decided

| # | Decision |
|---|---|
| A1 | The app's purpose is a **remote "I'm at work" mark** — an employee working away from a terminal marks arrival and departure from their phone, and it counts towards attendance. |
| A2 | **Location is required** for that mark and is checked against a known site. |
| A3 | **No face**, in any form: no selfie, no matching, no camera. |
| A4 | It is a **PWA**, not a store app (decided 2026-09-19): employees sign in with a login and password issued by HR. |

## B. What location can and cannot prove — read this first

Browser geolocation **can be faked**. On Android a mock-location app needs no
root; on both platforms developer tools can override the reading. A phone mark
is therefore **not proof of presence** — it is a recorded claim with a
coordinate attached, and a deterrent because the claim is visible and auditable.

Three consequences that shape the whole design:

1. **The face terminal stays the source of truth.** Mobile check-in is for
   people who cannot reach a terminal, not an alternative for people who can.
   Hence §C: it is off for everyone until HR turns it on per employee.
2. **Every mobile mark is labelled as such** — in the journal, on the timesheet
   cell tooltip and in exports. HR must never have to guess whether a day came
   from a terminal or a phone.
3. **The server never trusts the client's clock or its idea of "inside".** Time
   is the server's; the distance is computed server-side from the coordinates.

Two more platform limits, so nobody plans around them later:

* **Foreground only.** A PWA cannot read location in the background on either
  iOS or Android. "Open the app and mark" works; "track the employee through the
  day" does not, and is not in scope.
* **iOS permission is sticky.** If the employee denies location once, re-granting
  is done in iOS Settings, sometimes only by removing and re-adding the app. The
  app therefore needs an explicit "permission denied" screen with instructions,
  not a silent failure.

Accuracy in practice: 5–20 m outdoors, 20–100 m inside a building. Enough for
"is he at the site", not for "which room".

## C. Who may use it

New flag on the employee: `mobile_check_in_allowed`, **default false**. HR turns
it on from the employee card, one person at a time.

Rejected alternative: allow everybody and rely on the geofence. A phone mark is
spoofable (§B), so a company-wide switch would quietly turn the terminal into an
optional device — the opposite of why it was bought.

## D. Data model

### D1. `work_sites` — where a mark is accepted

| Column | Note |
|---|---|
| `name` | unique, case-insensitive (same rule as other reference records) |
| `latitude`, `longitude` | decimal degrees |
| `radius_meters` | default 200, range 50–5000 |
| `is_active` | inactive sites reject marks but keep history |

Soft-deletable and auditable like every reference table. Distance is computed
with the haversine formula in the application layer — PostGIS is **not** added
for this: a handful of sites and one comparison per mark do not justify an
extension that every future deployment would have to install.

### D2. `employee_accounts` — the app's sign-in

Separate table, **not** rows in `users` (already decided in
`SPEC-verifikatsiya-sorovlar.md` §C, unchanged here):

| Column | Note |
|---|---|
| `employee_id` | unique — one account per employee |
| `login` | phone number, unique, same normalisation as `users` |
| `password_hash` | same hasher as `users` |
| `must_change_password` | HR issues a one-time password; the employee replaces it on first sign-in |
| `is_active`, `last_login_at`, `failed_attempts`, `locked_until` | same lockout rule as `users` |

Tokens for these accounts carry a **separate audience** (`bonvi-employee`) and
**no permission claims**. An employee token presented to an HR endpoint fails
audience validation — it cannot be "almost authorised".

### D3. `mobile_check_ins` — the coordinate behind a pass

One row per accepted mark, referencing the pass it produced:

`pass_id` · `employee_id` · `work_site_id` · `latitude` · `longitude` ·
`accuracy_meters` · `distance_meters` · `created_at`

Why a separate table and not columns on `attendance_passes`: the passes table is
the hot path of the whole module (hundreds of thousands of rows, read by every
timesheet query). Four columns that are null for 99 % of rows would sit in every
one of those reads. The journal joins this table only for the rows it shows.

### D4. Enum additions

* `PassMethod.Mobile` — how the person was identified: they were signed in.
* `DirectionSource.Employee` — the direction came from the person, not a device.

Both are additive; every `Record<enum, …>` map in the frontend will fail to
compile until it is extended, which is the intended safety net (§8.3).

### D5. The device row

`AttendancePass.DeviceId` stays **required**, and mobile marks point at one
synthetic device row named «Mobil ilova». Rejected alternative: make `DeviceId`
nullable. That would ripple through the journal, the exports, the dashboard and
every read-model join for one flag, and it would make "which terminal did he
pass" answerable only with a null check. With a device row the journal already
says «Mobil ilova» in the column HR already reads.

The row is created by a migration, is not deletable from the devices screen, and
its ingest token is never issued: nothing may post events *as* the app.

## E. Employee API — `/api/me/*`, audience `bonvi-employee`

| Endpoint | Purpose |
|---|---|
| `POST /api/employee-auth/login` | login + password → token pair |
| `POST /api/employee-auth/password` | replace password (required while `must_change_password`) |
| `GET  /api/me` | name, photo, department, whether mobile check-in is allowed, **and the absence-type catalogue** (the app needs it to create a request and must not call an HR endpoint for it) |
| `GET  /api/me/attendance?year&month` | my month: the same cells HR sees, **plus the marks behind each day** — `marks: [{ time, direction, source, siteName? }]`, `source` ∈ `Terminal` \| `Mobil ilova`. Without them the app cannot draw the journal card, which is the screen's signature element |
| `POST /api/me/check-in` | `{ direction, latitude, longitude, accuracyMeters }` → returns the accepted mark's **server** `occurredAt`, `direction` and `workSiteName`. The receipt shows the server's time, never the phone's (§B3) |
| `GET  /api/me/requests`, `POST /api/me/requests`, `POST /api/me/requests/{id}/cancel` | my absence requests (phase 1 endpoints already exist for HR; these are the employee-scoped twins) |

`POST /api/me/check-in` validation order, each with its own error code:

1. account active and `mobile_check_in_allowed` → else `davomat.mobil_ruxsat_yoq`;
2. `accuracyMeters` ≤ 100 → else `davomat.joylashuv_aniq_emas` (asks the
   employee to step outside and retry);
3. the nearest active site within its radius → else `davomat.obyektdan_uzoq`,
   with the distance to the nearest site in the message;
4. no accepted mark for this employee in the last 60 seconds (the existing
   `AttendanceRules.BounceSeconds`) → else `davomat.takroriy_belgi`.

On success: one `AttendancePass` (method `Mobile`, source `Employee`, direction
as sent, `occurredAt` = **server now**), one `mobile_check_ins` row, the day
queued for recompute — exactly the path a terminal pass takes, so the timesheet,
the dashboard and the reports need no new rules.

## F. HR side

* Employee card → **Ilova** section: create account (shows the one-time password
  once, like the device token), reset password, deactivate, and the
  «Telefondan belgilash» switch (§C).
* **Ma'lumotnomalar → Obyektlar**: the sites catalog, with a coordinate field
  and a radius. A small map is **not** in this wave — the coordinate can be
  pasted from any map app; a map component is a separate decision.
* Journal: mobile passes show «Mobil ilova» as the device and the site name plus
  distance in the row detail.
* Timesheet cell tooltip: a line saying the mark came from the phone.

## G. The PWA itself

* ⚠️ **A separate Vite build in its own directory** (`app/`), served at
  `/ilova`. This reverses the first draft of this SPEC, which wanted one app
  with a second route tree, and the reason is not taste:
  **the HR app runs Tailwind 4.3 + React 19 + Vite 8, and Tailwind 4 requires
  Safari 16.4 / Chrome 111.** The owner's requirement is that the employee app
  work on *any* phone the staff actually own. Those two cannot live in one
  build. The employee app therefore gets Tailwind 3.4 and React 18.3, pinned to
  an old browser floor, and the HR bundle is not touched at all — which also
  satisfies the owner's second requirement, that this work cannot damage what
  already runs.
* **Browser floor: iOS 12.2 / Chrome 80.** Banned in this app: `:has()`,
  container queries, subgrid, `color-mix()`, `@property`, `@layer`,
  `aspect-ratio`, and flexbox `gap` (Safari gained it only in 14.1 — grid `gap`
  is fine). `dvh` is an enhancement, never a dependency. Every input is at
  least 16 px so iOS does not zoom on focus, and `user-scalable=no` is
  forbidden. Safe-area insets with `, 0px` fallbacks.
* The two apps share nothing at build time — no imported components. They share
  the API contract, and the generated client is produced for both by the same
  `./bonvi gen`.
* `vite-plugin-pwa` with **scope `/ilova/`** and the HR routes on the service
  worker's deny-list: the HR app must not start being cached by a worker it
  never asked for.
* Screens: sign-in → home (big «Ishga keldim» / «Ishdan ketdim», today's state,
  last mark) → my month → my requests → settings (password, permission help).
* iOS: no install prompt exists, so the sign-in screen carries a short "Share →
  Add to Home Screen" hint. Push notifications stay out of scope (§C of the
  requests SPEC).

## H. Phasing

1. **Wave A — server**: sites, accounts, auth, check-in, HR screens. Testable
   without a phone (the endpoint takes coordinates).
2. **Wave B — the app**: the `/ilova` route tree and the PWA shell.

Wave A is useful on its own: HR can already register sites and issue accounts.

## I. Definition of done

* Tests: geofence (inside / outside / inactive site), accuracy rejection, the
  bounce rule, the per-employee flag, audience isolation (an employee token is
  rejected by an HR endpoint **and** an HR token by `/api/me/*`), and a test
  proving a mobile mark lands on the timesheet exactly like a terminal pass.
* Migration read by hand; `./bonvi check`, typecheck and build green.
* The one-time password is shown **once** and never retrievable, like the device
  token.


---

## K. Decisions taken after planning (2026-09-22)

The data model and UX waves changed four things in §D and §G. Recorded here so
the sections above are not silently out of date:

| # | Decision | Why |
|---|---|---|
| K1 | `mobile_check_ins.pass_id` is `ON DELETE CASCADE` — the only cascade in the module | `attendance_passes` is **hard-deleted** by the retention job (`ExecuteDeleteAsync`, 365 days). With the module's usual `RESTRICT` the nightly purge would start throwing an FK violation exactly one year after the first mobile mark, and fail every night after |
| K2 | `employees` gains `mobile_check_in_allowed boolean NOT NULL DEFAULT false` — one existing table does change shape | A nullable flag would give three states for a two-state question. 950 rows, metadata-only |
| K3 | A separate `employee_refresh_tokens` table | `refresh_tokens.user_id` is `NOT NULL` → `users`; employee accounts cannot borrow it, and widening it would put two subject types in every rotation path |
| K4 | The «Mobil ilova» device row is registered **inactive**, with a token hash of 32 zero bytes | Inactive is the truth (nothing may post events *as* the app) and it closes a hole the first draft missed: the employee card lists every **active** device, so an active synthetic row would print a permanent «Mobil ilova — Rasm yo'q» line on all 950 cards |
| K5 | `AttendanceProvider.Mobile` and `AttendanceIdentityProvider.Mobile` are added; `raw_person_id` holds the employee's uuid in that new space | The Verifix number is unfillable for photo-less employees — exactly the field workers this feature is for |
| K6 | The check-in handler takes `SELECT … FOR UPDATE` on the employee's account row before the bounce check | Two taps 1.2 s apart produce different dedupe keys, so the unique index does not catch them; only the 60-second bounce rule does, and it is read-then-write |
