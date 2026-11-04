# SPEC — Candidate questionnaire built in HRMentor (form constructor)

> Status: authoritative. Written 2026-11-01. Owner's words (2026-10-31): «1 oydan keyin man nomzod
> anketasining ko'rinishini o'zgartiraman, misol anketaga yangi 4 ta bo'lim qo'shishim mumkin, 2 ta bo'limini
> olib tashlashim mumkin yoki umuman boshqacha ko'rinishda yangi nomzod anketasini ishlab chiqsam, yangi
> blankani hrmentorga yuklasam yoki hrmentorda o'zgartirsam, avtomatik ravishda hr.bonvi.uzdagi nomzod anketa
> blankasi ham yangisiga o'zgarishi kerak, bunda man faqat hrmentorni o'zgartiraman». 2026-11-01: «Ha boshla,
> mukammal qilib boshla». Local only until the owner confirms the goal is reached («maqsadimga yetsam keyin
> serverga chiqaramiz»).
>
> The contract (definition format, profile, publish rules, HRMentor API) lives in HRMentor
> `docs/SPEC.md` §19.3–§19.6 and is not repeated here. This file is BonviHR's side. It extends
> `docs/SPEC-planshet-anketa.md` (the built-in HR-F02) and `docs/SPEC-blankalar-hrmentor.md` (sync, key,
> texts revision).

## 1. Problem and outcome

Today every change to the candidate form — a question, a section, a rule, the PDF — is a developer task in
this repository (3.0 → 3.1 on 2026-10-27 took a day of code, tests and a release). `SPEC-blankalar-hrmentor`
moved only the **words** to HRMentor.

| Measure | Now | Target | Checked |
|---|---|---|---|
| Developer work needed to add / remove a section or question of HR-F02 | 1 release (code + tests + deploy) | 0 — HR publishes in HRMentor | AC-AK2, AC-AK3 |
| Time from «E'lon qilish» in HRMentor to the new form on `/anketa` | — | ≤ 5 min (sync job), immediately with «Hozir yangilash» | AC-AK2 |
| Forms lost or rejected because HR published while a candidate was filling | — | 0 (version pinning) | AC-AK4, AC-AK5 |

## 2. Flow

1. **Profile push.** The `hrmentor:sync` job (every 5 min, and «Hozir yangilash») pushes BonviHR's profile —
   the bindings of §3 and the seed of §4 — with the texts catalog. Same hash → nothing is sent.
2. **Definition pull.** The same run GETs the published definition. A new version is checked (§5.2),
   stored as an immutable snapshot and becomes current. `published: null` → no current definition → the
   built-in form. HRMentor unreachable → the last stored state stays.
3. **Form.** `/anketa` asks the session; with a current definition it renders the **dynamic wizard** (§7)
   from it, otherwise the built-in wizard as today.
4. **Submit.** The dynamic wizard sends `definitionVersion`; the server validates against **that** stored
   version (§5.3), finds or registers the candidate by the bound fields (§6.1), maps bound fields into the
   candidate and its questionnaire (§6.2), stores the form, renders the PDF from the same version (§8) and
   attaches it.
5. **HR** sees the form in the «Onlayn anketa» card with its printed version («v4.0»); the PDF re-renders
   from the stored snapshot at any time.
6. **Preview.** HRMentor's «Ko'rish» opens `/anketa/korinish` here: the HR user (signed in, `recruiting.view`)
   sees the draft exactly as candidates will, can fill it and download a sample PDF; nothing is stored.

## 3. Profile — bindings (BonviHR → HRMentor)

The bindings are the only way an answer reaches BonviHR's own data. Everything else is stored in the form's
answers and printed on the PDF.

| Binding | Kinds | Req. | Values / scale | Goes to |
|---|---|---|---|---|
| `vacancy` | vacancy | ✔ | — | the candidate's vacancy (general pool when `null`); validated first (SPEC-planshet-anketa §4) |
| `photo` | photo | ✔ | — | form photo + candidate photo when empty |
| `candidate.lastName` | short_text | ✔ | — | `FullName`, matching |
| `candidate.firstName` | short_text | ✔ | — | `FullName`, matching |
| `candidate.middleName` | short_text | | — | `FullName` |
| `candidate.birthDate` | date | ✔ | — | `BirthDate` (when empty), matching; BonviHR also enforces age 14–100 whatever the field says |
| `candidate.phone` | phone | ✔ | — | `Phone`, matching |
| `candidate.address` | short_text, long_text | | — | `Address` (when empty) |
| `candidate.referral` | referral | | — | `ReferralChannelId` + detail (when the candidate has none) |
| `signature` | signature | ✔ | — | the signature PNG; the typed name must contain the bound last and first name |
| `consent.personalData` | consent | ✔ | — | `CandidateQuestionnaire.OfferAcceptedAt` = submission time |
| `questionnaire.education` | choice | | `SecondarySchool VocationalSchool HigherIncomplete HigherComplete HigherMaster` | `Education` |
| `questionnaire.maritalStatus` | choice | | `Married Unmarried Divorced Widowed` | `MaritalStatus` |
| `questionnaire.languages` | levels | | rows read: `Russian`; scale 0–5 | `RussianLevel` (row `Russian`, first aspect, the SPEC-planshet-anketa §5 table); `OtherLanguages` (every other row and «other» row: «Ingliz tili: so'zlashuv 2, o'qish 3, yozish 2», labels from the definition) |
| `questionnaire.computerSkills` | levels | | — | `OfficeSkills` — rows of the group with id `office` when the field has one, else every row with a level: «MS Word — 3, MS Excel — 2» |
| `questionnaire.expectedSalary` | money | | — | `ExpectedSalary` range (SPEC-planshet-anketa §5) |
| `questionnaire.lastSalary` | money | | — | `LastSalary` «3 000 000 so'm» |
| `questionnaire.hasWorkExperience` | yes_no | | — | `false` → `WorkExperience` «Avval ishlamagan» |
| `questionnaire.workExperience` | repeat | | — | `WorkExperience`: one line per row, the row's non-empty values joined with « — » (a `period` as «03.2017–hozir») |
| `questionnaire.driverLicense` | yes_no | | — | `HasDriverLicense` |
| `questionnaire.driverLicenseCategories` | multi_choice | | `A B C D BC CE DE` | `DriverLicenseCategories` «B, C» |
| `questionnaire.extraNote` | long_text | | — | `ExtraNote` |

The questionnaire is written only when the candidate has none (the bot's stays — same rule as today). A
binding the published form does not carry leaves its target empty; HRMentor warns HR before publishing
(§19.8 there).

Code: `Bonvi.Application/Recruiting/Forms/Dynamic/FormProfile.cs` (the table as data — one place for the
push, the §5.2 check and the mapping).

## 4. Seed — the built-in HR-F02 v3.1 as a definition

`HrF02Seed` builds today's form (SPEC-planshet-anketa §3) as a `schema: 2` definition, label `3.1`, with
the 25 kiosk screens grouped into the paper's 11 candidate sections and the B part (sections 12–14) as office
sections. Labels come from the same sources as today (the bundled kiosk texts for the three languages, the
PDF wording from `QuestionnairePdfTexts`), so the seed and the built-in form say the same words. Rules the
built-in validator applies are expressed with the generic keys: age 16–70 (`min_age`/`max_age`), «Rus tili»
required row, ≤ 3 other languages, `hasWorkExperience` conditions, the 2-letter rule on the «about»
answers, the loan amount 1…1 000 000 000, the driver categories, the same-as-registered address tick.

HR's first step in HRMentor is to publish the seed unchanged (label «4.0») — from then on `/anketa` runs on
the dynamic engine with the form people already know — and then to change it.

## 5. Storage and versions

### 5.1 Data (one additive migration)
- New `form_definitions`: `code varchar(16)`, `version int`, `label varchar(8)`, `definition jsonb`,
  `sha256 char(64)`, `received_at timestamptz`; PK (`code`, `version`). Never updated or deleted.
- `candidate_forms.definition_version int NULL`, FK (`form_code`, `definition_version`) →
  `form_definitions` ON DELETE RESTRICT. `NULL` = the built-in form (all existing rows). For a dynamic form
  `form_version` = the definition's `label`, `texts_revision` = `NULL`.
- Hidden settings: `hrmentor.definitionVersion` (current version, empty = built-in),
  `hrmentor.profileHash` (last pushed profile).

### 5.2 Accepting a pulled definition
Stored only when all hold, otherwise not stored and `lastError` = `hrmentor.definition_invalid` (the
previous current stays):
- `sha256` matches the canonical JSON; `code` = `HR-F02`; `schema` = 2; size ≤ 512 KB;
- it passes BonviHR's own checker — the same rules as HRMentor §19.5 against BonviHR's profile (defence
  in depth: BonviHR renders and validates only what it understands; an unknown kind or key is rejected);
- the version is not lower than the current one (a lower version → ignored, like texts revisions).
A version already stored with a different `sha256` → rejected (`hrmentor.definition_invalid`).

### 5.3 Which version a submission uses
| Request | Used |
|---|---|
| `definitionVersion` = a stored version | that version — even if a newer one is current or the form was withdrawn since |
| `definitionVersion` unknown | 400 `anketa.versiya` («Anketa yangilandi. Sahifani yangilang.»); the draft survives |
| no `definitionVersion` | the built-in form (3.1, or 3.0 by the existing legacy rule) — a page opened before the first publish keeps working |

The session returns `definition: { version, label, definition }` or `null`, and `texts` only when
`definition` is `null`. The page reloads once when the server's current version differs from the one it
rendered at start (the same mechanism as `formVersion`). `formVersion` itself stays the app's built-in
blank version (3.1) while a definition is live — it drives the post-deploy reload, the definition's label
travels in `definition.label` (build decision 2026-11-01).

## 6. Submit — the dynamic path

### 6.1 Validation and matching
`DynamicFormValidator` (Application) validates the answers document against the definition:
- `answers` is `{ fieldId: value }` (HRMentor §19.3 value shapes); keys of unknown fields are ignored (as
  today); answers of hidden fields (a false `visible_if`) are dropped before anything else;
- required, kinds, lengths, ranges, option values, `min_letters`, ages (on the submission day, Tashkent
  time), years, months, `period` (from ≤ to, `current` → no `to`), `year_range`, `levels`
  (`required_rows`, scale, `other.max`, names), `repeat` (`min_rows`, `max_rows`, column rules), phone
  normalisation, Telegram, e-mail, character rules (no control / bidi / zero-width / private-use
  characters, no newline outside long texts), consents, the signature name;
- errors use the existing codes (`anketa.majburiy`, `anketa.notogri`, `anketa.uzun`, `anketa.belgi`,
  `anketa.json`, `anketa.rozilik`, `anketa.imzo_ism`; an age out of range is `anketa.notogri`; one new
  code `anketa.versiya` §5.3) with `detail` = the field path (`jobs[1].period`,
  `languages.rows.Russian`).
Matching, vacancy checks, photo, signature, honeypot, bot check, rate limits, idempotency, audit: exactly as
SPEC-planshet-anketa §2–§4, reading the bound values.

### 6.2 Mapping
§3 «Goes to». Implemented once (`DynamicFormMapping`), producing the same `CandidateRegistration` /
questionnaire inputs the built-in mapping produces, so everything after it (candidate creation, handover to
employee, notifications, funnel counters) is shared.

## 7. Web — the dynamic wizard

`web/src/features/kiosk/dynamic/`: types of the definition, visibility evaluation, the client mirror of
§6.1, the draft ↔ answers conversion, one renderer per kind, the wizard. It reuses the built-in form's
parts: start, done, photo capture, signature pad, bot check, idle dialog, `KioskText`, `KioskTextArea`,
`DigitsField`, `MonthField`, `ChoiceGroup`, `YesNo`, `MultiChoice`, `CheckRow`, `LevelPicker`,
`SummaryRow` / `RowSheet` for repeats, the error scroll.
- One definition screen = one wizard step; a screen whose fields are all hidden is skipped; the header
  shows the section title and «4 / 16».
- Texts come from the definition in the chosen language; the wizard's own words (Keyingi, Orqaga, errors)
  stay in the locale files.
- Draft: `sessionStorage` (`localStorage` rules of the tablet mode unchanged) under
  `bonvi.kiosk.dyn.HR-F02.<version>`; a draft of another version is dropped.
- `/anketa/korinish` — the preview: HR session required (`recruiting.view`), a «Ko'rinish — yuborilmaydi»
  ribbon, the last step shows «Namuna PDF» instead of «Yuborish».
- Build decisions (2026-11-01): a definition published while `/anketa` is open needs no reload — the next
  «Boshlash» takes it, a walk in progress keeps its pinned version (a removed one → `anketa.versiya` with a
  reload button); the preview ribbon also offers a screen picker and «Yangilash» (refetch, answers kept),
  stores nothing and does not require photo or signature; constructor-only and preview words stay out of
  the HRMentor texts catalogue.

## 8. PDF — from the definition

`DynamicFormPdfRenderer` (Infrastructure, PDFsharp-MigraDoc 6.2.4, the bundled fonts and logo) prints in the
house style of the built-in PDF (SPEC-planshet-anketa §6.3):
- header «MBF Company — HR hujjatlar paketi» / «{printed_code} · {title} · v{label}», footer «Sahifa X / Y»;
- title block: logo, `pdf.title`, `pdf.subtitle`, «Blanka {printed_code} · Versiya {label}», photo box;
- info table Anketa № | Sana | So'rov № «(HR-F01)» / Lavozim | Bo'lim, as the built-in PDF and the paper
  HR-F02 v2.0 print it (2026-11-01; the request number and the department are written by hand); the last
  cell's name is `pdf.head_department` when HR set it (the paper v2.0 says «Yo'nalish», ≤ 60 characters,
  HRMentor SPEC §19.3), otherwise «Bo'lim»; `pdf.instruction`; the `candidate_part` bar;
- sections numbered «1. SHAXSIY MA'LUMOTLAR» (`pdf_title` or the Latin title, upper-case);
- scalar fields as label | value rows (label cells shaded); `choice` / `multi_choice` / `yes_no` print every
  option ☑ / ☐; `repeat` as a table with the column headers and max(`print_rows`, rows) rows; `levels` as a
  table of rows × aspects (+ «Boshqa: name» rows; grouped rows under group sub-headers); `info` as a
  paragraph; `consent` with the name filled in and ☑; the signature block (name, date, image) and the
  provenance line «Onlayn to'ldirildi: … · Til: …»;
- a field hidden by its condition prints empty (the paper shows every question); `pdf_hidden` fields are
  not printed;
- the `office_part` bar and the office sections printed empty; a `checklist` item with `auto` is ☑ with its
  note when the form has that item (photo, signature);
- `pdf.footer` as the last gray line; money «3 000 000 so'm», dates «24.10.2026», months «03.2021».
- build decisions (2026-11-01): a `levels` field with empty `print_rows` prints every option; narrow fields
  pair two per row, wide ones (long text, multi choice, referral, vacancy, long option lists) take a full
  row; the built-in and dynamic renderers share one render gate; the preview prints «Namuna» in place of
  the form number.
Built-in forms (3.0, 3.1) keep their own renderer; their PDFs never change.

## 9. API changes

| Method / path | Permission | Change |
|---|---|---|
| `GET /api/kiosk/questionnaire/session` | anonymous | + `definition` (§5.3) |
| `POST /api/kiosk/questionnaire` | anonymous | + part `definitionVersion`; the dynamic path (§6) |
| `GET /api/recruiting/candidates/{id}/forms` | `recruiting.view` | `formVersion` = the label for dynamic forms (unchanged shape) |
| `GET /api/recruiting/candidates/{id}/forms/{formId}/pdf` | `recruiting.view` | renders a dynamic form from its snapshot |
| `GET /api/kiosk/questionnaire/preview` | `recruiting.view` | the draft from HRMentor (live, 20 s timeout) + the session data; `definition.version` = 0, label «draft» when unset; `turnstileSiteKey` and `texts` null; 409 `hrmentor.not_connected`, 502 `hrmentor.*` (a draft with HRMentor issues or failing BonviHR's checker → `hrmentor.definition_invalid`) |
| `POST /api/kiosk/questionnaire/preview/pdf` | `recruiting.view` | multipart like submit; validates against the draft and returns the PDF; nothing stored; rate limit 30 / hour per user |
| `GET /api/hrmentor/blanks` | `employees.view` | + `form: { version, label, receivedAt } \| null` for the status line «Anketa: HRMentor v4.0» / «Ichki anketa v3.1» |

## 10. Rules register

| ID | Rule | Example |
|---|---|---|
| R1 | Required bindings are always present — BonviHR refuses a definition without them | v5 without `candidate.phone` → not stored, `lastError` `hrmentor.definition_invalid`, `/anketa` stays on v4 |
| R2 | A submission is validated and printed by the version the candidate saw | page opened on v4 at 10:00, v5 published 10:05, submit 10:20 with `definitionVersion=4` → stored with `definition_version=4`, PDF «v4.0» |
| R3 | Nothing published → the built-in form | withdraw at 11:00 → after the next sync the session has `definition: null`; a form still open on v4 submits fine (v4 is stored) |
| R4 | Snapshots and old forms never change | form №120 (v4) re-rendered in 2027 prints v4's sections even though v9 is current |
| R5 | Answers of hidden fields are dropped | `hasCar=false` + `carModel="Nexia"` → `carModel` not stored, printed empty |
| R6 | Bound data fills only empty candidate fields; the bot's questionnaire is never overwritten | existing InProgress candidate with a bot questionnaire → only the form + PDF are added |

## 11. Out of scope
The Telegram bot questionnaire (separate, shorter); more than one candidate form at a time (e.g. per
vacancy); HR editing submitted answers; the B part as data; filters or reports on unbound answers;
retiring the built-in form (a separate decision once the dynamic form has run for 60 days — debt row in
`docs/ASSUMPTIONS.md`; the built-in PDF renderer stays forever for 3.0/3.1 forms).

## 12. Non-functional
- Session with a definition: ≤ 150 KB gzip for a 400-field form; first wizard render ≤ 1 s on the office
  tablet.
- Submit p95 ≤ 3 s including the PDF for a seed-sized form (measured in the integration test run).
- The definition JSON is data, never code: no HTML, no expressions beyond `visible_if`; React escapes every
  text; size and count limits are enforced before parsing deeper.
- Personal data: answers stay `recruiting.view`-only and are never logged; the preview stores nothing.

## 13. Acceptance
- AC-AK1 The first sync pushes the profile and seed once; the second sends nothing (hash).
- AC-AK2 HRMentor publishes the seed as 4.0 → after one sync the session serves it and `/anketa` renders the
  dynamic wizard with the same 25 steps.
- AC-AK3 A version with four new sections and two removed → stored; a submission is validated by it, the
  PDF prints its sections; mapped candidate fields are filled from the bound fields.
- AC-AK4 Version pinning (R2). AC-AK5 A built-in page without `definitionVersion` after a publish → accepted
  as 3.1.
- AC-AK6 Withdraw → built-in form again (R3). AC-AK7 An invalid definition (missing required binding, sha
  mismatch, unknown kind) → not stored, `lastError`, the previous stays (R1).
- AC-AK8 HRMentor unreachable → the last stored definition keeps serving.
- AC-AK9 **Seed equivalence**: the same person's answers submitted through the built-in 3.1 path and
  through the published seed produce the same candidate fields and the same questionnaire values.
- AC-AK10 The dynamic validator rejects each §6.1 rule with the right code and path; hidden answers are
  dropped (R5).
- AC-AK11 Preview: 401 without a session, 403 without `recruiting.view`; the sample PDF is returned and
  nothing is written.
- AC-AK12 Old 3.0 / 3.1 PDFs render exactly as before (existing renderer tests stay green).
- AC-AK13 Web: typecheck and build green; every kind renders at 360 / 768 / 1280 px (screenshots).

## 14. Phases (both repositories, local only)
1. SPECs (this file, HRMentor §19) — done 2026-11-01.
2. HRMentor backend: definition, profile, issues, changes, API, migration 0022, tests.
3. BonviHR backend: profile + seed, sync push / pull, storage + migration, dynamic validator, mapping,
   PDF, session / submit / preview endpoints, tests (incl. AC-AK9).
4. BonviHR web: dynamic wizard + preview page.
5. HRMentor panel: the constructor.
6. Both stacks running locally: publish the seed, change it, fill the form, check the PDF; screenshots.
7. Optional: HRMentor's proposal from an uploaded blank (AI, §19.10 there).
