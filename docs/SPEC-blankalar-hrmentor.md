# SPEC — Blanks and HR-F02 texts from HRMentor

> Status: authoritative. Written 2026-10-31. Owner's words: «hrmentor da birorta blanka o'zgaradigan bo'lsa
> hr.bonvi.uzdagi blankalar ham o'zgaradigan bo'lishi kerak». Chosen with the owner the same day:
> - **all 19 blanks** of HRMentor (HR-F01 … HR-F19) are shown in hr.bonvi.uz as files, always their
>   current version;
> - for **HR-F02** (the online form `/anketa` and its PDF, `docs/SPEC-planshet-anketa.md`) HR edits the
>   **question texts and option lists in HRMentor**, and hr.bonvi.uz applies them without a developer.
>   New questions, sections and validation rules stay developer work here.
>
> Counterpart: HRMentor `docs/SPEC.md` §18 (the contract: catalog, served texts, blank routes).

## 1. Who owns what

| Thing | Owner | Direction |
|---|---|---|
| Which texts and lists HR-F02 has — keys, defaults, placeholders, limits (the *catalog*) | this repository | pushed to HRMentor |
| HR's changes to those texts and lists (the *overrides*) | HRMentor panel | pulled from HRMentor |
| The blank files | HRMentor library | pulled from HRMentor |

Every call goes **hr.bonvi.uz → HRMentor** (`https://docs.bonvi.uz`) with one HRMentor API key that holds only
`forms:sync`. HRMentor never calls us. HRMentor down or the key revoked → the form, the PDF and the blanks page keep
working on the last good copy; nothing here fails because of HRMentor.

## 2. Connection — settings

Two settings in the «HRMentor» group of Sozlamalar (`settings.manage`):

| Key | Kind | Rule |
|---|---|---|
| `hrmentor.baseUrl` | Text | `https://…`, or `http://` only to this machine — localhost, 127.0.0.1, [::1], host.docker.internal (`HrMentorUrlPattern`, same rule in `HrMentorConnection.From`), e.g. `https://docs.bonvi.uz` |
| `hrmentor.apiKey` | Secret | `^hrm_[A-Za-z0-9_-]{43}$`, encrypted like the bot token, never returned |

Both set → integration on. Either empty → off: built-in texts, the blanks page says «HRMentor ulanmagan».
Server-written state (Hidden settings): `hrmentor.catalogHash` (last pushed), `hrmentor.lastAttemptAt`,
`hrmentor.lastSuccessAt`, `hrmentor.lastError` (an error code, never a secret or a body).

## 3. Sync job

Hangfire recurring job `HrMentorSyncJob`, every 5 minutes, one at a time (`DisableConcurrentExecution`), and
`POST /api/hrmentor/blanks/sync` runs the same code at once. Three independent steps; a failed step logs a warning, records
`lastError` and leaves its last good data, the others still run.

1. **Catalog** — build it (§4); when its SHA-256 differs from `hrmentor.catalogHash`, `PUT /api/v1/digital-forms/HR-F02/catalog`,
   then store the hash.
2. **Texts** — `GET /api/v1/digital-forms/HR-F02/texts`; a revision we do not have yet → shape-checked (unknown keys,
   lists and values dropped, texts longer than the catalog limit dropped) and stored as a new row of
   `form_text_snapshots`. 404 (catalog never pushed) → nothing.
3. **Blanks** — `GET /api/v1/blanks`; for every item whose `(document_id, version_id)` we do not have, download
   `/api/v1/blanks/{id}/file`, check size (≤ 50 MB) and SHA-256 against the list, store it through `IFileStorage`
   under `blanks/{documentId}/{versionId}{ext}`, upsert `hr_blanks`, delete the replaced file. Items no longer listed →
   row and file deleted.

HTTP client `HrMentorClient` (Infrastructure, typed `HttpClient`): `Authorization: Bearer <key>`, timeout 20 s (file
downloads 120 s), JSON bodies ≤ 2 MB, request/response logging off (the key must not reach logs).

## 4. Catalog (what HR can edit)

Built in Application (`Recruiting/Forms/Texts`), never by hand-written JSON:

- **Form texts** — every `kiosk.*` string of the online form in `uz-Latn`, `uz-Cyrl`, `ru`. The web locale files are the
  source; `web/scripts/export-kiosk-texts.mjs` writes them to the embedded resource
  `Bonvi.Application/Recruiting/Forms/Texts/kiosk-texts.json`, and `pnpm check:locales` fails when that export is stale.
  Grouped by wizard screen (`screen.<name>`, the titles HR sees are the uz-Latn screen titles) plus «Umumiy» (start,
  done, buttons, errors). Option labels are not items — they are lists.
- **Blank texts** — every Uzbek literal of the PDF (`QuestionnairePdfTexts`: key → default), grouped by the paper's
  sections, `target: "blank"`, uz-Latn only.
- **Lists** (values = what answers store):

| List | Values | Kind |
|---|---|---|
| `housing` | Apartment, House | closed |
| `marital` | Married, Unmarried, Divorced, Widowed | closed |
| `military` | Served, Reserve, Deferred, NotServed | closed |
| `driverLicense` | A, B, C, D, BC, CE, DE | closed |
| `education` | SecondarySchool … HigherMaster | closed |
| `startAfter` | OnHireDay, OneWeek, TenDays, FifteenDays | closed |
| `relation` | Father … Other | closed |
| `language` | Uzbek, Russian, English, Chinese, Other | closed, nothing may be hidden (the Russian row is required) |
| `computer` | the 11 skills in groups os / office / special | **open** — HR adds and removes skills |

Placeholders keep the i18next form `{{name}}`; PDF texts with values use the same form (`{{months}}`).

## 5. Applying the texts

### 5.1 Online form
- `GET /api/kiosk/questionnaire/session` gains `texts`: `null` (no snapshot) or
  `{ revision, strings: { "uz-Latn": { key: text }, "uz-Cyrl": {…}, "ru": {…} }, lists: { id: { groups?: { id: { lang: text } }, options: [ { value, group?, label: { lang: text }, hidden } ] } } }`
  — only `form` overrides; blank texts are not sent.
- The web applies `strings` over its own `kiosk.*` resources for the kiosk page only, and reads lists through one
  provider (`useKioskLists`) whose defaults are the constants of `kiosk-options.ts`. Hidden options are not offered; a
  restored draft holding one keeps it. HR-added computer skills show with their label in their group.

### 5.2 Server validation
- Computer skill keys accepted = the code's 11 ∪ every value of the latest snapshot's `computer` list (hidden ones too:
  a page opened before HR hid it must still submit). Any other key → 400 as today.
- Closed lists stay the enums they are; a hidden value is still accepted.

### 5.3 PDF
- The renderer reads every Uzbek text through `PdfTexts`: HR's override (apostrophes made typographic — `o‘`, `g‘`,
  `’` — like the paper) ?? the current literal. With no override the PDF stays byte-for-byte what it prints today.
- Option rows follow the effective list: HR's order and labels; a hidden option prints only when it was chosen; HR-added
  computer skills print in their group's column.
- `candidate_forms.texts_revision` (new, nullable) records the snapshot in force at submission. The attached PDF and
  every later re-render (`/forms/{formId}/pdf`) use that revision, so a signed form never changes its wording
  afterwards. `null` (older forms, or no snapshot) → built-in texts.
- The questionnaire mapping (`OfficeSkills` «MS Word — 3, …») uses the same effective labels.

## 6. Blanks page (HR)

- `GET /api/hrmentor/blanks` (`employees.view`) → `{ connected, lastAttemptAt, lastSuccessAt, lastError, textsRevision, items:
  [ { id, code, title, versionNo, publishedAt, fileName, contentType, sizeBytes } ] }`, by code.
- `GET /api/hrmentor/blanks/{id}/file` (`employees.view`) → the stored file, `Content-Disposition: attachment` (RFC 5987 name).
- `POST /api/hrmentor/blanks/sync` (`settings.manage`) → runs the job now (≤ 120 s) and returns the GET body.
- Web: route `/blanka-fayllari`, menu item «Blanka fayllari» (renamed 2026-11-04 from `/blankalar` «Blankalar»,
  which the generic blank engine of `docs/SPEC-blankalar.md` took; API moved from `/api/blanks` the same day); a searchable table — Kod, Nomi, Versiya, Yangilangan, Hajmi,
  «Yuklab olish»; the line «HRMentor dan oxirgi yangilanish: 10:41»; «Hozir yangilash» for `settings.manage`; when not
  connected a card pointing to Sozlamalar → HRMentor.

## 7. Data (one migration)

- `form_text_snapshots`: `id` uuid PK, `form_code` varchar(16), `revision` int, `payload` jsonb, `fetched_at`
  timestamptz; unique `(form_code, revision)`. All revisions are kept (small; §5.3 needs them).
- `hr_blanks`: `id` uuid PK, `document_id` uuid unique, `code` varchar(32), `title` varchar(300), `version_id` uuid,
  `version_no` int, `published_at` timestamptz null, `file_name` varchar(300), `content_type` varchar(150),
  `size_bytes` bigint, `sha256` char(64), `file_key` varchar(500), `synced_at` timestamptz.
- `candidate_forms.texts_revision` int null.

Additive only: no drop, no NOT NULL on existing rows.

## 8. Tests (must pass before «done»)

- Unit: catalog completeness (every kiosk key of the export and every `QuestionnairePdfTexts` key is in the catalog
  once; every list value matches its enum); snapshot shape check; effective lists (order, hidden, HR-added skills);
  validator accepts an HR-added skill and still refuses an unknown key; `PdfTexts` typographic apostrophes and
  placeholders; PDF renders with an override and with a hidden/added option.
- Integration: session returns `texts` from the latest snapshot; submit stores `texts_revision`; the PDF of a form
  keeps its revision's wording after a newer snapshot; blanks routes — 401 without a token, 403 without
  `employees.view`, sync 403 without `settings.manage`; sync job against a fake HRMentor (catalog pushed once per hash,
  snapshot stored, files downloaded and SHA-checked, removed blank deleted, HRMentor down → last data kept).

## 9. Rollout

1. Deploy HRMentor (its §18) first; a super admin creates the API key «hr.bonvi.uz» with only `forms:sync`.
2. Deploy this; in Sozlamalar → HRMentor paste the URL and the key; «Hozir yangilash» on the Blanks page.
3. Check: 19 blanks listed and downloadable; HR-F02 appears in HRMentor «Raqamli blankalar»; a changed question text
   shows on `/anketa` within 5 minutes and on a new form's PDF.
Rollback: clear the two settings (integration off, built-in texts) or roll the images back; the migration is additive.
