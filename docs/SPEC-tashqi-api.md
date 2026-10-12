# SPEC — Tashqi API (read-only employee data for sister projects)

> Status: authoritative. Written 2026-09-18. Branch: `main`.
> Language: English, same rule as the other SPECs — **code comments stay
> Uzbek**, error codes stay Uzbek (`tashqi.*`), UI text goes through i18n in
> four languages, the consumer guide is Uzbek.
>
> Owner decisions, 2026-09-18: **API key** auth (not a service account),
> **open on the internet** (no IP allowlist), and the field set in §5.
> Do not re-litigate them.

---

## 1. Goal and boundary

Other projects of the holding need employee data from BonviHR. The traffic is
**one-way**: they read, they never write. Nothing they send can change anything
here.

**Therefore:**

* The public surface is a **separate endpoint group** that maps **`GET` only**.
  No `POST`/`PUT`/`PATCH`/`DELETE` is ever registered on it — not disabled, not
  guarded: never mapped.
* It has its **own authentication scheme**. An API key must not authenticate any
  internal endpoint, and a normal user JWT must not authenticate a public one.
  Both directions are covered by tests (§9).
* It exposes **only** the fields in §5. Everything else — salary, JSHSHIR
  (`Pinfl`), passport, address, criminal record, review notes, documents,
  mehnat.uz links and screenshots, candidates, users, incidents, incentives,
  exit interviews — never leaves. §9 has a test that asserts this against the
  serialized JSON, not against the DTO shape.

Out of scope for v1: write access, webhooks, per-key scopes, IP allowlists,
OAuth, per-consumer field filtering.

---

## 2. Data model — one new table

`api_keys` (EF entity `ApiKey`, `AggregateRoot`, `IAuditable`):

| Column | Type | Rule |
|---|---|---|
| `id` | uuid | |
| `name` | text, required, ≤ 120 | Which project this key belongs to, e.g. "BonviCall". Unique among non-revoked keys. |
| `key_prefix` | text(8), required, unique | Plain, indexed — the lookup handle and the only part shown in the UI and logs. |
| `key_hash` | bytea(32), required | SHA-256 of the **whole** key. The key itself is never stored and never recoverable. |
| `last_used_at` | timestamptz, null | |
| `last_used_ip` | text, null | |
| `revoked_at` / `revoked_by` | timestamptz / uuid, null | Revoked keys stay as history; they are never deleted. |
| `created_at` / `created_by` / `updated_at` / `updated_by` | | `IAuditable`, as elsewhere. |

* **Key format:** `bonvi_<prefix8>_<secret32>`, where `prefix8` and `secret32`
  come from a cryptographic RNG (`RandomNumberGenerator`), base32/URL-safe
  alphabet, no ambiguous characters. Follow `AttendanceDevice`'s token pattern
  on the `verification` branch in spirit — but this table is independent; do not
  wait for that branch and do not reference it.
* **Validation:** look the key up **by prefix**, then compare the hash with
  `CryptographicOperations.FixedTimeEquals`. A missing prefix and a wrong secret
  must cost the same time and return the same error.
* **`last_used_at` must not turn every read into a write.** Update it at most
  **once per minute per key**, out of the request path (fire-and-forget), and
  never let its failure fail the request. Write down in the code comment why.
* Migration name in the project's Uzbek style, e.g. `TashqiApiKalitlari`. After
  `--autogenerate`, **read the migration** — it likes to emit spurious DROPs.

---

## 3. Authentication

* Header: `X-API-Key: bonvi_…`. Nothing else is accepted — no query parameter,
  no cookie (a key in a query string lands in access logs).
* Failure → **401** `tashqi.kalit_notogri` for missing, malformed, unknown,
  revoked. One message for all of them: the caller must not learn which.
* **The key is never logged**, never written to the error log, never echoed in a
  problem response. Only `key_prefix` may appear in logs. Check
  `ErrorLogExtensions` and the request logging path before you finish.
* Rate limit: a dedicated policy, partitioned **by key prefix** (not by IP —
  several projects may share an office IP), 120 requests / minute, sliding, in
  the style of the existing `RateLimitPolicies.Api`; 429 carries `Retry-After`.
  An unauthenticated request partitions by IP and gets a much smaller budget.

---

## 4. Endpoints — all `GET`, prefix `/api/public/v1`

| Route | Returns |
|---|---|
| `/ping` | `{ ok: true, key: "<name>", serverTime }` — integration self-test. |
| `/employees` | Paged list, §5 payload. |
| `/employees/{id}` | One employee, §5 payload; 404 `tashqi.xodim_topilmadi`. |
| `/employees/{id}/photo` | The photo bytes, or 404. `ETag` + `Cache-Control: private, max-age=86400`, same as the internal photo route. |
| `/employees/deletions` | `?since=<utc>` → `[{ id, deletedAt }]`, so a consumer can drop records that disappeared. |
| `/reference/legal-entities`, `/reference/departments`, `/reference/positions`, `/reference/directions` | `{ id, name }` (departments also `parentId`). |

`/employees` query parameters:

* `page` (from 1), `pageSize` (default 50, max 200) — same parsing helpers the
  internal lists use.
* `updatedSince` — UTC instant; returns records changed after it, so consumers
  can sync incrementally. Ordered by `updatedAt` ascending when it is given,
  by full name otherwise.
* `status` — `Probation | Active | Terminated`; omitted = everyone except
  soft-deleted.
* `legalEntityId`, `departmentId` (with sub-departments, like the internal
  list), `positionId`, `directionId`.
* `search` — **name and phone only** (`EmployeeSearchQuery.MatchingPublic`).
  The internal rule also matches JSHSHIR and passport; outside, that turns
  the endpoint into an identity oracle — a key holder could ask "is this
  JSHSHIR yours?" and get a full name back, even though the value itself is
  never returned. Owner's decision, 2026-09-18. Guard test:
  `PublicApiLeakTests.Jshshir_va_pasport_boyicha_tashqaridan_qidirib_bolmaydi`.

Soft-deleted employees are **never** returned by `/employees`; that is what
`/employees/deletions` exists for.

---

## 5. Payload — exactly this, nothing more

```jsonc
{
  "id": "…",
  "fullName": "…", "shortName": "…",
  "lastName": "…", "firstName": "…", "middleName": "…",
  "birthDate": "1994-02-11",          // owner: personal group approved
  "gender": "Male",
  "phone": "+998…",                   // owner: contact group approved
  "telegramUrl": "https://t.me/…",
  "photoKey": "…",                    // presence flag + cache buster; bytes come from /photo
  "status": "Active",                 // the primary employment's status
  "hireDate": "2021-03-01",
  "terminationDate": null,
  "employments": [                    // organizational group
    {
      "id": "…",
      "legalEntity": { "id": "…", "name": "…" },
      "department":  { "id": "…", "name": "…" },
      "position":    { "id": "…", "name": "…" },
      "rate": 1.0,
      "status": "Active",
      "hireDate": "2021-03-01",
      "terminationDate": null,
      "isPrimary": true
    }
  ],
  "directions": [ { "id": "…", "name": "…" } ],
  "updatedAt": "2026-09-18T07:11:00Z"
}
```

An employee may hold several employments (§Employment remarks: two firms, 0.5
rate, or a re-hire). Return the **active** ones, newest first, `isPrimary` on the
one the internal list treats as primary; the top-level `status`, `hireDate` and
`terminationDate` mirror that primary one.

`salary`, `rate` money, `pinfl`, `passport`, `address`, `speciality`,
`hasCriminalRecord`, `criminalRecordNote`, `mehnatUz*`, `needsReview`,
`reviewNote`, `flags` — **absent from the JSON**, not null-valued.

---

## 6. Key management UI

`Sozlamalar` gets a **«Tashqi API»** section, permission `Permissions.SettingsManage`:

* Table of keys: name, prefix (`bonvi_ab12cd34_…`), created, last used (relative,
  Tashkent time), status (active / revoked).
* **«Yangi kalit»** — asks for a name, then shows the full key **once**, with a
  copy button and a clear warning that it will never be shown again.
* **«Bekor qilish»** — confirmation dialog; revoked keys stay in the table,
  greyed, and stop working immediately.
* i18n in `uz-Latn`, `uz-Cyrl`, `ru`, `en`. iOS/HIG feel like the rest, light and
  dark, usable at phone width.

**Internal management endpoints** (normal JWT auth, `SettingsManage`, *not* part
of the public surface):

| Route | Does |
|---|---|
| `GET /api/api-keys` | Lists keys — name, prefix, created, last used, revoked. **Never** the secret. |
| `POST /api/api-keys` | `{ name }` → creates and returns `{ id, name, prefix, key }`; `key` appears in this one response and nowhere else, ever. |
| `POST /api/api-keys/{id}/revoke` | Revokes; already-revoked is idempotent (200, not an error). |

---

## 7. Error codes (Uzbek, project style)

| Code | HTTP | When |
|---|---|---|
| `tashqi.kalit_notogri` | 401 | Missing, malformed, unknown or revoked key. |
| `tashqi.kalit_nomi_band` | 409 | A non-revoked key with that name already exists. |
| `tashqi.xodim_topilmadi` | 404 | Unknown employee id on the public route. |
| `tashqi.surat_yoq` | 404 | Employee has no photo. |

Translations in all four locale files.

---

## 8. OpenAPI

The public group is documented, but **as its own OpenAPI document** so a
consumer sees only what it may call. ⚠️ Known trap (2026-08-29): a request
record with `Guid = default` as a parameter default takes the **whole** document
down with a 500. Query-parameter defaults on the new routes must be nullable, and
`/scalar` must be opened and checked before the work is called done.

---

## 9. Tests — the ones that matter

* Missing key → 401; wrong secret with a valid prefix → 401; revoked key → 401;
  each returns `tashqi.kalit_notogri` and nothing else.
* A valid API key on an **internal** endpoint (e.g. `/api/employees`) → 401/403.
* A valid **user JWT** on a public endpoint → 401. The public surface has exactly
  one way in.
* **Field-leak test:** serialize a fully populated employee (salary, JSHSHIR,
  passport, address, criminal record, review note filled) through the public
  endpoint and assert the raw JSON contains none of those values or property
  names. This test is the point of the module — write it first.
* Soft-deleted employee absent from `/employees`, present in `/employees/deletions`.
* `updatedSince` returns a record edited after it and not one edited before.
* Paging: `pageSize` over 200 clamps; page beyond the end returns an empty list,
  not an error.
* Rate limit: over the budget → 429 with `Retry-After`.
* `last_used_at` is updated, and a failure to update it does not fail the read.
* Key creation returns the key once; reading the key list never returns the
  secret; the hash in the database does not equal the key.

---

## 10. Consumer guide

`docs/API-TASHQI.md`, Uzbek, short: how to get a key, the header, the four calls
a consumer actually needs (`/ping`, `/employees`, `/employees/{id}/photo`,
incremental sync with `updatedSince` + `/employees/deletions`), the rate limit,
and what to do when a key leaks.

---

## 10a. Full-read scope (2026-10-11)

Keys now carry a scope (`api_keys.scope`, migration `ApiKeyScope`, existing rows = 1 `Basic`).
`FullRead` keys additionally reach `GET /api/public/v1/full` (tool catalog) and
`GET /api/public/v1/full/{tool}` — the MCP read tools (`McpSetup.ToolTypes`) invoked over
plain HTTP with query-string arguments, acting with `PublicFullReadEndpoints.ReadPermissions`
(every view permission the tools check, salary included). Same guarantees as MCP: read-only
DB role, SELECT-only interceptor, response scrubber — passport, PINFL and file keys never leave.
Each call is written to `mcp_audit` (client `api-key:<prefix>`, tool `api:<name>`). Independent of
the MCP on/off switch; requires the MCP read-only role to be provisioned (`McpRuntime.IsStoreReady`).
Basic keys get 403 `tashqi.doira_yetmaydi`. Scope is immutable — widen by issuing a new key.

## 11. Definition of done

* `./bonvi test` green; `./bonvi check` green; frontend build green.
* Migration exists, `upgrade head` works, the generated file was read.
* Every new endpoint is covered by the §9 RBAC and leak tests.
* `/scalar` opens (both documents).
* Nothing from the `verification` branch is pulled in; this work stays on `main`.
