# SPEC — Candidate questionnaire HR-F02 (public link, office tablet)

> Status: authoritative. Written 2026-10-24. Owner's words: «offline kelgan nomzodlarga odatda varoqda
> ma'lumot to'ldirtiramiz, shuni planshetda qilmoqchimiz … kelgan nomzodga planshet beramiz, forma orqali
> anketa to'ldiradi, keyin uni avtomatik tizimga yozadi, to'ldirilgan anketani nomzod hujjati sifatida ham
> xuddi shunday ko'rinishda generatsiya qilib PDF holatida nomzod profili hujjatlar bo'limiga saqlab qo'yishi
> kerak avtomatik».
>
> Sources (owner's files, outside the repo): `~/Documents/Temping/MBF_anketa/HR-F02_Nomzod_anketasi_v2.docx`
> (the paper blank, 6 pages) and `HR-F02_Online_forma_maydonlari.xlsx` (field list: keys, types, required,
> conditions, mapping to existing fields). The field list is the source of truth for fields and rules;
> the docx is the source of truth for the PDF look.

## 0. Revision 2026-10-26 — public link (owner chose variant C)

Owner: «C variant menga maqul chunki bizga juda ko'p nomzodlar keladi, har biri uchun link ochib berish va
faqat bitta wifida qilish juda muammoli». The first build (kiosk key, HR login on the tablet, tablet name,
«Planshetlar» dialog) is **replaced**: `https://hr.bonvi.uz/anketa` is a public form like Google Forms —
open the link, fill, submit. No login, no pairing, no tablet name. Nothing of the first build was deployed,
so the kiosk-key parts are removed, not kept alongside. Owner's next step (later, not now): the bot will
send this link to applicants of important vacancies so their full HR-F02 lands on their existing card (§4).

## 1. Flow

1. Anyone with the link opens `https://hr.bonvi.uz/anketa` — on the office tablet, or on their own phone or
   computer. `/anketa?vakansiya=<id>` preselects a vacancy (for the bot link later).
2. The candidate taps «Anketani boshlash», picks a language, fills the wizard, takes a selfie, signs with a
   finger, submits.
3. The server, in one transaction:
   - finds the candidate (§4) or registers a new one (source **OnlineForm**);
   - stores the answers as an immutable form document with a sequential **Anketa №**;
   - fills the existing candidate fields and the existing `CandidateQuestionnaire` (§5);
   - renders the PDF in the paper layout (§6) and attaches it to the candidate's files («Hujjatlar»).
4. The page shows «Rahmat! Anketa №N qabul qilindi». In tablet mode (§7) it clears everything and returns
   to the start screen.
5. HR sees the candidate in the funnel, the PDF in the card's files, and an «Onlayn anketa» card.

Out of scope now (next phase, owner decides): the B part (HR section 12–14: document checklist, tests,
interview scores, decision) as data in the system; HR editing the answers (versioned, original kept). In
this phase the PDF prints the B part as the blank paper has it, so HR can still fill it by hand.

## 2. Access — public, anonymous

No key, no login. The two endpoints are `AllowAnonymous`; every other endpoint keeps its current auth.

| Item | Rule |
|---|---|
| Removed | Kiosk key and everything around it: `ApiKeyScope.Kiosk`, the `kiosk` policy and `UseBonviApiKeyAuthentication("/api/kiosk")`, `/api/recruiting/kiosk-devices*`, `planshet.nomi_band`, the `api_keys` index changes in the migration, the pairing web screen, the «Planshetlar» dialog. The HR-session notice stays, in tablet mode only (§7). `PublicApiPolicy` keeps requiring scope Basic/FullRead (harmless, stays). |
| Client IP | Prod is behind Cloudflare → Caddy → API, and the API sees a Cloudflare edge address (audit IPs are Cloudflare edges since 2026-10-12). Caddy gets a global `servers { trusted_proxies static <Cloudflare IPv4+IPv6 ranges>; client_ip_headers Cf-Connecting-Ip }` and `header_up X-Bonvi-Client-Ip {client_ip}` on `/api/*` (set, never passed through from the client). The API's `X-Forwarded-For` handling is **not** changed (no blast radius on login limits, audit, device endpoints). The anketa limiter and the audit row read `X-Bonvi-Client-Ip` when present, else `RemoteIpAddress` (dev). Verify on prod that the API port is not published past Caddy; if it is, the header must not be trusted. |
| Rate limit | Per client IP: session `RateLimitPolicies.Questionnaire` 60 / minute; submit `RateLimitPolicies.QuestionnaireSubmit` 20 / hour (the office tablets share one IP; a form takes ≥ 10 minutes). Global submit cap `RateLimiting:QuestionnaireDaily` 500 / day across all IPs (protects disk and HR from a distributed flood). 429 with the existing `rate_limit.oshib_ketdi` problem. All three configurable. |
| Honeypot | The form posts an extra empty `website` part, hidden from people (off-screen, `tabindex=-1`, `autocomplete=off`, `aria-hidden`). Non-empty → 400 `anketa.belgi` and nothing written. |
| Audit | One row per accepted form, `AuditActorKind.Anonymous`, actor name «Onlayn anketa», the client IP above. |
| Body limit | Submit endpoint: 12 MB request body; form values ≤ 256 KB each, ≤ 16 values; `answers` ≤ 256 000 characters (checked before JSON parsing). |
| Later if spam appears | Cloudflare Turnstile on submit (needs the owner's Cloudflare account). Not now. |

### 2.1 Endpoints — public (paths kept from the first build)

`GET /api/kiosk/questionnaire/session` → 200
```json
{ "today": "2026-10-24",
  "vacancies": [ { "id": "uuid", "title": "Operator" } ],
  "generalPoolAvailable": true,
  "referralChannels": [ { "id": "uuid", "name": "Tanishlar orqali", "asksDetail": true } ] }
```
`vacancies` = vacancies that accept candidates (`AcceptsCandidates`), general pool excluded, sorted by title.
`referralChannels` (added 2026-10-26, `docs/SPEC-qayerdan-bilgan.md`) = active «Qayerdan bilgan» channels, alphabetical.

`POST /api/kiosk/questionnaire` — `multipart/form-data`:

| Part | Content |
|---|---|
| `answers` | JSON string, schema §3 |
| `clientSubmissionId` | uuid generated by the tablet when the wizard starts |
| `language` | `uz-Latn` \| `uz-Cyrl` \| `ru` — the language the candidate used |
| `photo` | image, JPEG or PNG by content, required, ≤ 5 MB, goes through `SquarePhotoStore` (pixel budget) like the HR photo upload. Any photo problem → `anketa.rasm_yaroqsiz` |
| `signature` | PNG, required, ≤ 300 KB and ≤ 1200 × 600 px (checked from the header before decoding). The server re-draws it on white and stores its own PNG, never the client bytes |

| `website` | honeypot, must be empty (§2) |

→ 201 `{ "formNumber": 123, "submittedAt": "2026-10-24T09:41:00Z" }`. **Nothing about the candidate record
is returned** (no id, no name, no "you already applied"): the endpoint is public.
Same `clientSubmissionId` again → 200 with the same body, nothing new is written (idempotent; a lost
response on bad Wi-Fi must not create a second candidate), also when the two requests race (unique-index
violation → re-read → 200). `anketa.takror` is no longer used (no device to compare).
Text fields reject control, bidi-override, zero-width, private-use and unassigned characters, and newlines
outside long-text fields → 400 `anketa.belgi`.
400 problem+json with the first validation error code (`anketa.*`) and `detail` naming the field path.

### 2.2 Endpoints — HR

| Method / path | Permission | Result |
|---|---|---|
| `GET /api/recruiting/candidates/{id}/forms` | `recruiting.view` | `[{ id, number, formCode: "HR-F02", formVersion: "3.1", submittedAt, language, fileId }]` newest first (`formVersion` is the row's own version — forms submitted before 2026-10-27 stay `"3.0"`). `fileId` = the attached PDF in candidate files, `null` if it could not be attached or was deleted |
| `GET /api/recruiting/candidates/{id}/forms/{formId}/pdf` | `recruiting.view` | `application/pdf`, rendered fresh from the stored answers, photo and signature (works even if the attached file was deleted) |

### 2.3 Hardening (2026-10-27)

Owner: «анкета ҳаволасини эслаб қолиб кейин яна ўзи кириб оладиган одамлар бўлиши мумкин … тизимга кириб
қандайдир ёмон ишлар қилиши мумкин, хавфсизликни максимал олдини олиш керак». Re-opening the form is intended
(Google Forms style); the goal is that the form gives nothing beyond «submit one form», that bots cannot
flood it, and that candidates never even see the HR system's address.

1. **Form-only site `anketa.bonvi.uz`** (replaces the redirect block). Caddy `{$ANKETA_SITE::9998}` serves
   ONLY: `/` → 302 `/anketa` (query kept); `/anketa` and `/anketa/*` → the SPA's `index.html`; the built
   static files the page needs (assets, fonts, icons — whatever the build emits under its asset paths);
   `/api/kiosk/*` → `api:8080` with `header_up X-Bonvi-Client-Ip {client_ip}` (client copy stripped at the
   site's ingress like the main site). **Everything else → 404**: `/kirish`, every other `/api/*`, `/mcp`,
   `/oauth/*`, `/.well-known/*`, `/health/*`, `/ilova/*`, `/isup-face/*`. Same security headers and CSP as
   the main site (plus Turnstile below), same access-log redaction. The HR «Anketa havolasi» dialog shows
   this address when `VITE_ANKETA_URL` is set. `hr.bonvi.uz/anketa` keeps working (tablets already set up).
   The kiosk page must contain no link or navigation to HR routes when opened on this site (the tablet-mode
   «HR tizimiga qaytish» button cannot appear there anyway — different origin, no HR session).
2. **Invisible bot check — Cloudflare Turnstile** (free; owner creates the widget in the Cloudflare dashboard
   for `hr.bonvi.uz` and `anketa.bonvi.uz`). Config: `Turnstile:SiteKey` (public) and `Turnstile:SecretKey`
   (secret, prod `.env` only, never in chat/code/logs). Both empty → disabled (dev, tests, until the owner
   adds keys). Session response gains `turnstileSiteKey: string | null`; the web renders the widget
   (managed/invisible mode, the candidate's language) on the last screen and sends the token as multipart
   part `turnstileToken`. Server verifies it with `https://challenges.cloudflare.com/turnstile/v0/siteverify`
   (secret + token + client IP from §2) **before** any other work except the honeypot; invalid/missing/expired
   → 400 `anketa.bot_tekshiruvi` («Tekshiruvdan o'tmadi. Sahifani yangilab, qayta yuboring.»), nothing written.
   Cloudflare unreachable / timeout (5 s) → **fail closed** with the same code (owner asked for maximum
   protection; the draft survives and the candidate retries). Idempotent replays of an already accepted
   `clientSubmissionId` return 200 without a new token check. CSP gains `https://challenges.cloudflare.com`
   in `script-src` and `frame-src` on both sites.
3. **Real client IP everywhere it is about a person**, not only the form: the HR and employee login
   limiters, the refresh/session limiters, the anonymous part of the global limiter, `audit_logs.ip_address`
   and the IPs stored on refresh tokens / employee sessions now use `ClientIp` (`X-Bonvi-Client-Ip`, which
   Caddy sets on every API proxy and strips from clients). Before this, on prod they all saw a Cloudflare
   edge: login attempts of unrelated people shared one bucket and audit IPs were useless. Device / ingest /
   ISUP / attendance-agent endpoints keep `RemoteIpAddress` (unchanged behaviour for terminals).

## 3. Answers document (`answers` JSON, form HR-F02 v3.1)

Keys follow the owner's field list. `?` = optional. Text fields are trimmed; empty string = absent.
Limits: short text ≤ 200, address ≤ 300, long text ≤ 1000 (`extraNote` ≤ 2000).
Enums are sent and stored as their names (`"TenDays"`); a number or an unknown name makes the whole
document unreadable → 400 `anketa.json` (no `field`).

> **Revision 3.1 (2026-10-27, HR manager's changes, owner approved).** Driver licence category `BE` →
> `BC`; new required `hasWorkExperience` (section 5) that decides whether jobs, references and the two
> job-related «success» questions are required or dropped; total experience (`totalExperienceYears`,
> `totalExperienceMonths`) removed; the start date becomes the choice `startAfter`; `hasLoan` required;
> `motivators` and `successDefinition` required. Forms stored as 3.0 keep `form_version = 3.0` and their
> answers; the removed keys in them are ignored like any unknown key, and `availableFrom` is kept on the
> record only so a 3.0 form still prints its date (§6.3). The validator never copies `availableFrom` into a
> new form.
>
> **Open 3.0 pages at deploy time** (owner: «serverga chiqarishda uzilish bo'lmasin»). The form page never
> reloads by itself, so a tablet or phone opened before the deploy keeps sending the 3.0 shape. Such a
> submission — no `hasWorkExperience`, no `startAfter`, but `availableFrom` (3.0 always sent it; 3.1 never
> does) — is validated with the 3.0 rules (`QuestionnaireFormValidator.IsLegacy`: `availableFrom ≥ today`
> required, jobs/references/loan/success answers optional, category `BE` allowed) and stored with
> `form_version = 3.0`. From 3.1 the session returns `formVersion`; the form compares it with its own
> (`KIOSK_FORM_VERSION`) on the start screen and reloads once per server version, so later releases reach
> an open tablet without a manual refresh.

**0. Vacancy and photo**
- `vacancyId?` — uuid of an accepting vacancy; `null` = «Umumiy (istalgan lavozim)» → general pool vacancy.
- photo → multipart part.

**1. Personal**
- `lastName`, `firstName` (≤ 60, required), `middleName?` (≤ 60)
- `birthDate` (required, age 16–70 on `today`)
- ~~`gender`, `passportNumber`, `passportIssuedBy`, `passportIssuedAt`, `pinfl`~~ — **removed 2026-10-26**
  (owner, marking the two rows «Jinsi / JSHSHIR» and «Pasport / Kim tomonidan va qachon berilgan» on the blank:
  «бу жойи шарт эмас — кенгаш суҳбатидан ўтса паспортни оламиз … паспорт жуда шахсий маълумот, анкетада сўраш
  шарт эмас»). Not asked, not stored, not printed; if a client still sends them the server ignores them like any unknown key (they never reach the database or the PDF).
- `registeredAddress` (required), `actualAddress` (required; the tablet has a «same as registered» tick that copies the value)
- `housingType?`: `Apartment | House`
- `mahalla?`
- `phone` (required, Uzbek mobile, normalized by `PhoneNumber`), `phoneExtra?` (same rule)
- `telegram?` (`@username`, 5–32 chars of `[A-Za-z0-9_]`, `@` optional on input, stored without `@`)
- `email?` (email format)
- `maritalStatus`: `Married | Unmarried | Divorced | Widowed` (required)
- `childrenCount?` 0–20
- `militaryService?`: `Served | Reserve | Deferred | NotServed` — optional for everyone (no gender any more); the label keeps the paper's «(erkaklar uchun)»
- `militaryServiceMonths?` 1–60 — only when `Served`
- `hasDriverLicense` (required bool); `driverLicenseCategories`: subset of `A B C D BC CE DE` (case-insensitive,
  stored upper-case in this order), at least one when `true`, empty when `false`. `BE` (3.0) is rejected like
  any unknown category → `anketa.notogri` at `driverLicenseCategories`
- `hasCar?` bool; `carModel?` only when `hasCar = true`

**2. Education**
- `educationLevel`: `SecondarySchool | VocationalSchool | HigherIncomplete | HigherComplete | HigherMaster` (required)
- `educations`: 0–5 × `{ institution (required), faculty?, startYear?, endYear?, diplomaNumber? }`; years 1950–today's year, `startYear ≤ endYear`
- `schoolName?`, `strongSubjects?`, `courses?` (long)

**3. Languages** — `languages`: `[{ language: "Uzbek|Russian|English|Chinese|Other", otherName?, speaking, reading, writing }]`,
levels 0–5. The Russian row is required (all three levels). `Other` needs `otherName`; at most 3 `Other` rows.
Rows the candidate left untouched are not sent.

**4. Computer** — `computerSkills`: map `key → 1..4`; absent key = does not know. **Shortened 2026-10-26**
(owner: «шуларни ҳам менимча озгина қисқартириш керак» on the 39-item table; the company hires mostly
operators, welders and office staff). 11 fixed keys in three groups, in this order:
- OS — «Operatsion tizim»: `windows` «Windows», `mobileOs` «Smartfon (Android / iOS)»
- Office — «Ofis dasturlari»: `msWord` «MS Word», `msExcel` «MS Excel», `msPowerPoint` «MS PowerPoint»,
  `googleWorkspace` «Google Docs / Sheets»
- Special — «Maxsus dasturlar»: `oneC` «1C», `didox` «Didox, soliq.uz portallari», `crm` «CRM (AmoCRM,
  Bitrix24)», `autocad` «AutoCAD», `photoshop` «Photoshop / Canva / CorelDraw»
- `computerSkillsOther?` free text, label «Boshqa dasturlar va dasturlash tillari» (replaces the removed
  Programming column and the removed rarely-used items).
Removed keys: `linux, macos, msAccess, msOutlook, notion, acrobat, sap, powerBi, solidworks, corelCanvaFigma,
python, javascript, csharp, java, cpp, php, sql, htmlCss, kotlinSwift, oneCLanguage`. A removed or unknown
key → 400 (nothing is deployed, so no old clients exist).

**5. Work**
- `hasWorkExperience` (required bool) — «Avval biror joyda ishlaganmisiz?». Missing → `anketa.majburiy` at
  `hasWorkExperience`.
- `true` → `jobs`: 1–5 × `{ organization (required), position?, from? "yyyy-MM", to? "yyyy-MM", current: bool, leaveReason? }`
  (`current = true` → `to` absent; `from ≤ to`; newest first as entered). Empty or missing → `anketa.majburiy`
  at `jobs`; more than 5 → `anketa.notogri` at `jobs`.
- `false` → whatever came in `jobs`, `references`, `lastJobAchievement` and `bestIdea` is ignored (not
  checked) and stored as `jobs: []`, `references: []`, the two texts absent.
- ~~`totalExperienceYears`, `totalExperienceMonths`~~ — **removed in 3.1**.

**6. References** — `references`: when `hasWorkExperience = true` 1–3 × `{ fullName (required),
organizationPosition?, phone? }` (empty or missing → `anketa.majburiy` at `references`); when `false` dropped (§5).

**7. Family** — `familyMembers`: 1–8 × `{ relation, fullName (required), birthYear?, workplace?, phone?, address? }`,
`relation`: `Father | Mother | Spouse | Brother | Sister | Child | Other` (owner's list: Otasi, Onasi, Turmush o'rtog'i,
Akasi / ukasi, Opasi / singlisi, Farzandi, Boshqa). `birthYear` 1920–today's year.

**8. Conditions**
- ~~`referralSource`~~ — **replaced 2026-10-26** (`docs/SPEC-qayerdan-bilgan.md` §5): `referralChannelId` — uuid of an
  ACTIVE channel from the session's `referralChannels` (required; inactive/unknown → `qayerdan.topilmadi`);
  `referralDetail?` (≤ 200) required when that channel `asksDetail`, otherwise not stored
- `hasAcquaintances` (required); `acquaintancesDetail?` required when `true`
- `lastSalary?`, `expectedSalary` (required) — whole so'm, 0 … 1 000 000 000
- `startAfter` (required) — «Ishga qachondan chiqa olasiz?»: `OnHireDay | OneWeek | TenDays | FifteenDays`
  («Qabul qilingan kundan», «… 1 hafta keyin», «… 10 kun keyin», «… 15 kun keyin»). Missing → `anketa.majburiy`
  at `startAfter`. ~~`availableFrom`~~ (a date, 3.0) is no longer asked, not required and never stored for a
  new form.
- `canWorkShifts`, `canWorkOvertime`, `canTravel` (required bools)
- `hasOtherJob` (required); `otherJobDetail?` required when `true`
- `hasLoan` (required, 3.1); `loanMonthlyPayment` required when `hasLoan = true` — whole so'm, 1 … 1 000 000 000
  (missing → `anketa.majburiy`, 0 / negative / over the cap → `anketa.notogri`, both at `loanMonthlyPayment`);
  dropped when `false`
- `agreesToTrial` (required bool), `agreesParentsInvite?`
- `startConditions?` (long)

**9. Health and extra**
- `hasHealthRestriction` (required); `healthRestrictionDetail?` required when `true`
- `hasFamilyPatient?`
- `hasConviction` (required); `convictionDetail?` required when `true`
- `relativesConviction?`; `relativesConvictionDetail?` only when `true` (the paper has «Ha: ____»)
- `abroadTrips?` (long)

**10. About you** — long texts. Since 2026-10-29 (owner: candidates left them blank or typed «.») **every**
question is required and an answer needs at least 2 letters (`MinAnswerLetters`; «Ha» passes, «.», «-», «5» do
not) — a letterless answer is `anketa.majburiy` at that field, so a page left open from before still shows it.
`lastJobAchievement, bestIdea, previousTeamOpinion` — asked and required only when `hasWorkExperience = true`,
dropped when `false`. A 3.0 page keeps its own rules: the first four required (no letter rule), the rest
optional.

**11. Consent** — `consentAccuracy, consentPersonalData, consentReferenceCheck, consentNotifyChanges,
consentConfidentiality` must all be `true`. `signatureName` (required) must contain both `lastName` and
`firstName` (case-insensitive; the apostrophe variants `' ʻ ʼ ‘ ’ \`` are treated as one character; Latin and
Cyrillic spellings are compared through the same transliteration the name search key uses). The drawn
signature is the `signature` part.

Validation lives in one place on the server (Application, FluentValidation or the project's existing style).
The tablet mirrors the same rules so server errors are rare.

## 4. Which candidate gets the form

| Situation | Result |
|---|---|
| A candidate with the same phone digits **and** the same last and first name (compared Latin/Cyrillic-insensitive, apostrophes ignored) is **InProgress** (e.g. applied via the bot and came for the interview) | The form is attached to that candidate (the newest such). No new candidate. Vacancy and stage are not changed. |
| No such InProgress candidate (new walk-in; same phone but another name — a mistyped digit must not file one person's passport, convictions and health under another; or a closed/rejected/reserve/blacklisted older record) | A new candidate is registered: source `OnlineForm`, chosen vacancy (or general pool), first active stage, like the bot. The card's existing phone-match warning shows HR the history. The page never says anything about it. |

The chosen vacancy is validated **first, on both paths** (404 `vakansiya.topilmadi`, 409
`vakansiya.qabul_qilmaydi` / `nomzod.umumiy_anketa_yoq`), so the response never depends on whether the
phone matched.

Blacklist and re-apply cooldown do **not** block the form: the card's phone-match warning shows HR the
history and HR decides (the same person may be standing in the office).

This rule is what the owner's bot plan relies on: a bot applicant who opens the link and types the same
phone and name gets the full HR-F02 and PDF on the card the bot already made.

## 5. Writing into existing fields

New candidate: `FullName`, `BirthDate`, `Phone`, `Address` = `actualAddress`, photo, `Source = OnlineForm`.
Existing candidate: fill **only empty** fields (`BirthDate`, `Address`, photo). Names are not changed.

`CandidateQuestionnaire` (only when the candidate has none — the bot's questionnaire is immutable and stays):

| Questionnaire field | From |
|---|---|
| `Education` | `educationLevel` (new value `HigherMaster = 5`) |
| `MaritalStatus` | `maritalStatus` (new value `Widowed = 4`) |
| `RussianLevel` | Russian `speaking`: 0–1 → Elementary, 2 → Intermediate, 3 → UpperIntermediate, 4 → Advanced, 5 → Native |
| `ExpectedSalary` | amount → range: < 2 M → From1To2, < 3 M → From2To3, < 4 M → From3To4, < 5 M → **From4To5 (new = 5)**, < 7 M → From5To7, ≥ 7 M → **From7 (new = 6)** |
| ~~`ReferralSource`~~ | removed 2026-10-26 — the channel goes to the candidate itself (`Candidate.ReferralChannelId` + detail), filled only when the candidate has none (`docs/SPEC-qayerdan-bilgan.md` §4–§5) |
| `OfferAcceptedAt` | submission time (the personal-data consent moment) |
| `WorkExperience` | one line per job «Tashkilot — Lavozim (MM.yyyy–MM.yyyy / hozir), sabab: …»; `hasWorkExperience = false` → «Avval ishlamagan» (the «Umumiy: X yil Y oy» line is gone with 3.1) |
| `OtherLanguages` | non-Russian rows «Ingliz tili: so'zlashuv 2, o'qish 3, yozish 2» |
| `OfficeSkills` | office group «MS Word — 3, MS Excel — 2, …» |
| `LastSalary` | `lastSalary` formatted «3 000 000 so'm» |
| `ExtraNote` | `extraNote` |
| `HasDriverLicense`, `DriverLicenseCategories` | same; categories in the paper order joined with «, » (`B, C`, `BC, CE`) — without a separator `B`+`C` and the 3.1 category `BC` would read the same. The bot keeps writing its own button values (`B`, `BC`, `BCE`, `BCED`) unchanged |

Enum additions are appended at the end (existing integer values never move). The bot keeps its buttons and
never sends the new values. Check that nothing in `Bonvi.Bot` deserializes these enums from API responses;
if it does, make it tolerant.

`CandidateSource.OnlineForm = 4` (renamed from `Tablet`; value unchanged, never deployed) — label «Onlayn
anketa». The funnel/new-application counters treat it like Manual (a real new application), the
notification collector treats it like non-bot (`CandidateNew`).

## 6. Stored form and PDF

### 6.1 Table `candidate_forms` (own aggregate root, not loaded with the candidate)

| Column | Type | Note |
|---|---|---|
| `id` | uuid | PK |
| `candidate_id` | uuid | FK → `candidates.id` ON DELETE CASCADE, indexed |
| `number` | int | from sequence `candidate_form_number_seq`, unique — the «Anketa №» |
| `form_code` | varchar(16) | `HR-F02` |
| `form_version` | varchar(8) | `3.1` since 2026-10-27 (§3 revision). `3.0` — the owner's blank v3.0 (2026-10-26: passport, PINFL and gender removed, computer table shortened); 3.0 rows stay as they are (no data migration), 2.0 was never deployed |
| `answers` | jsonb | §3, as validated and normalized |
| `language` | varchar(8) | |
| `photo_key` | varchar(500) | the stored photo used in the PDF (a copy independent of the candidate photo, so changing the candidate photo later does not change the signed form) |
| `signature_key` | varchar(500) | PNG |
| `client_submission_id` | uuid | unique — idempotency |
| `file_id` | uuid null | the attached `CandidateFile` (PDF) |
| `submitted_at` | timestamptz | |

Immutable after insert (the candidate's own words, same rule as `CandidateQuestionnaire`).
Answers contain sensitive data (convictions, health, loans): readable only with `recruiting.view`, never logged.

### 6.2 Attaching the PDF

File name: `Anketa-HR-F02-{number}-{LastName}-{FirstName}.pdf` (characters unsafe in file names replaced by `-`).
Attached through `Candidate.AttachFile` (content type `application/pdf`, `createdBy = null`). If the
candidate already has `MaxFiles` files or the size cap is hit, the form is **still saved**, `file_id` stays
null, a warning is logged, and HR gets the PDF from the «Onlayn anketa» card. The submission never fails
because of the files cap. When the candidate is hired, `CandidateHandover` moves the PDF to the employee's
documents like every other candidate file (the paper rule «ishga qabul qilinsa, shaxsiy ish yig'majildida»).

### 6.3 PDF look — same as the paper blank

Engine: **PDFsharp-MigraDoc 6.2.4** (MIT). Pin it: **6.5.0 and later are AGPL-3.0** — never upgrade past
6.2.x without a licence decision. Fonts embedded as resources: Liberation Sans (Regular/Bold/Italic/BoldItalic,
SIL OFL — metric-compatible with Arial, which the blank uses) and DejaVu Sans for the ☐ ☒ glyphs. Licence
files go next to the fonts. Logo: the PNG from the docx (`word/media/…png`, MBF Company).

Match the blank (A4, margins ≈ 1.5 cm):
- Page header: left «MBF Company — HR hujjatlar paketi», right «HR-F02 · Nomzod anketasi · v3.1», gray 8 pt.
  The version printed here and in the title block is the stored form's own `form_version` (a 3.0 form
  re-rendered later still says 3.0).
  Footer: centered «Sahifa X / Y».
- Title block: logo left; «NOMZOD ANKETASI» (navy `#1F3A5F`, bold 16 pt), «Ishga qabul qilish uchun»,
  «Blanka HR-F02 · Versiya 3.1»; photo box 3 × 4 cm on the right with the candidate photo.
- Info table: Anketa № | Sana | So'rov № (HR-F01) | Lavozim | Bo'lim. Lavozim = vacancy title («Umumiy» for the
  general pool). So'rov № and Bo'lim stay empty (not in the system).
- Instruction line in gray italic. «A QISM  NOMZOD TOMONIDAN TO'LDIRILADI» navy bar with white bold text.
- Sections 1–11 with the paper's tables: label cells filled `#F2F5F9` bold, table headers navy with white bold
  text, borders light gray, section titles navy bold uppercase, hints gray italic (`#6B7686`).
- Choices print every option: ☒ for the chosen one, ☐ for the rest, exactly the paper's option texts.
  Conditional extras inline («☒ O'tagan, muddati: 12 oy», «☒ Bor, toifasi: B, C»).
- Section 5 starts with «Avval ishlaganmisiz?» ☐ Ha ☐ Yo‘q; the «Umumiy ish tajribangiz» row is gone (3.1).
  Section 8 question 5 prints the four `startAfter` choices one per line; a 3.0 form (no `startAfter`, a stored
  `availableFrom`) prints its date as before.
- Tables print at least the paper's rows (education 3, jobs 4, references 2, family 5, languages 5,
  computer 3 columns — OS, office, special — × 5 rows, then a «Boshqa dasturlar va dasturlash tillari:
  ____» line) and grow when there are more entries. Language rows: the four fixed
  languages + «Boshqa: <name>».
- Money as «3 000 000 so'm»; dates «24.10.2026»; months «03.2021».
- Section 11: the full consent text with the candidate's full name filled in, «Sana: 24.10.2026», and the
  drawn signature image on the «Imzo» line. Under it a small gray provenance line:
  «Onlayn to'ldirildi: 24.10.2026 14:41 · Til: uz-Latn».
- B part (12–14) printed as the blank has it, empty, with two ticks pre-set: «Rasm (3 × 4)» ☒ (izoh
  «Onlayn olingan») and «Imzolangan anketa (A qism)» ☒ (izoh «Onlayn imzolangan»).
- Final gray line «Saqlash: …» as on the blank.
- Labels are always Uzbek Latin (the company blank). Answers are printed as typed.

Rendering happens in Infrastructure behind an Application interface (`IQuestionnairePdfRenderer`), input is
a plain model (answers + vacancy title + number + dates + photo bytes + signature bytes).

## 7. Web — public form (`/anketa`, outside the HR shell)

- Route `web/src/app/routes/anketa.tsx`, feature folder `web/src/features/kiosk/`. No HR auth guard, no key.
  Its own axios instance; it never touches the HR auth storage or the HR axios instance.
- **Short address.** One constant link for everyone. Preferred: `anketa.bonvi.uz` — the form-only site of
  §2.3.1 (dead port `:9998` by default; the owner adds the DNS record in Cloudflare and
  `ANKETA_SITE=anketa.bonvi.uz` in prod `.env`, and the web build gets `VITE_ANKETA_URL`).
  `hr.bonvi.uz/anketa` keeps working.
- **Two modes**, chosen on the device, not by the server:
  - **Personal device** (default — a candidate's phone or computer): draft in `sessionStorage`
    (`bonvi.kiosk.draft`) with `savedAt`, discarded after 2 hours; no idle reset; Done screen «Rahmat!
    Anketangiz qabul qilindi. Anketa №N. HR bo'limi siz bilan bog'lanadi.» with no auto-return.
  - **Tablet mode** (the office tablet): switched on once by opening `/anketa?planshet=1` (stored in
    `localStorage` `bonvi.kiosk.tablet`, try/catch), off with `?planshet=0`. Then the first build's kiosk
    rules apply: draft discarded after 15 minutes, start-screen draft dropped after 2 minutes without
    touch, 10 minutes idle in the wizard → «Davom etasizmi?» 60 s → reset, Done says «Planshetni HR
    xodimiga qaytaring» and returns to Start after 20 s, language resets to uz-Latn. A small «Planshet
    rejimi» label at the bottom of the start screen so HR can see it is on. The HR-session notice («Bu brauzerda HR hisobi
    ochiq» → «HR hisobidan chiqish va davom etish») also applies in tablet mode only: an HR session left on
    the shared tablet's browser would open HR pages to the next candidate.
- `?vakansiya=<id>` preselects that vacancy when it is in the session list (ignored otherwise).
- Screens: **Start** (big «Anketani boshlash», language picker uz-Latn / uz-Cyrl / ru; no vacancy and pool
  closed → start disabled with the notice), **Wizard**, **Done**.
- **Wizard layout (owner: the first version looked «rasvoroq»).** Short screens, one topic each, instead
  of 6 long steps, so a screen fits a phone with little scrolling. Suggested split (adjust if a screen
  is still long; built as 25 screens, the «Pasport» screen removed 2026-10-26): Vakansiya va rasm · Shaxsiy ma'lumotlar · Manzil · Aloqa · Oila holati,
  harbiy xizmat, guvohnoma · Ma'lumoti · Tillar · Kompyuter · Mehnat faoliyati · Tavsiya beruvchilar ·
  Oila a'zolari · Ish sharoitlari · Sog'liq · O'zingiz haqingizda · Rozilik va imzo. Header shows the
  section name and «4 / 16» with a progress bar. Larger, dark labels (body size), inputs ≥ 52 px,
  single column on phones (< 640 px), two columns only where both fields are short. Repeating rows
  (education, jobs, references, family) as compact summary rows («Uzgaz MChJ — Operator, 03.2017 – hozir»)
  with edit / remove; «+ Qo'shish» opens the row's fields in a bottom sheet. Choice groups as large tiles.
  Back / Next pinned at the bottom with safe-area padding. Works from 360 px wide to landscape tablets.
- Computer skills (11 items) are an inline list per group, each row with level buttons 1–4; no level = does
  not know; tapping the chosen level again clears it (same as language levels).
- Per-screen validation with inline errors and scroll to the first error; «Bekor qilish» with confirmation.
- Photo: `<input type="file" accept="image/*" capture="user">`, preview, «Qayta olish»; downscale in the
  browser to ≤ 1600 px JPEG before upload. On a computer without a camera the same input picks a file.
- Signature: canvas with pointer events (finger, stylus or mouse), «Tozalash», exported as PNG.
- Honeypot `website` field (§2), visually hidden.
- iOS look (project design rule). All texts in the 4 locale files under `kiosk.*`.

## 8. Web — HR

- Nomzodlar page: header action «Anketa havolasi» (`recruiting.view`) → small dialog with the public link
  (copy button), the tablet-mode link (`…/anketa?planshet=1`) with one line on how to use it, and the
  short address when configured (`VITE_ANKETA_URL`, falls back to `<origin>/anketa`). A QR code only if
  a QR library is already a dependency; do not add one for this.
- Candidate card: «Onlayn anketa» card when forms exist — «Anketa №123 · 24.10.2026 14:41 · O'zbekcha» +
  «PDF» button (the on-the-fly endpoint). The attached PDF also shows in the files card.
- Source label «Onlayn anketa» (and filter option) wherever sources are listed; labels for the new enum
  values (education «Oliy (magistr)», marital «Beva», salary «4–5 mln», «7 mln dan yuqori», referral
  «E'lon», «Kollej / OTM») in the questionnaire card, exports and filters.

## 9. Tests (must pass before «done»)

- Unit: answers validator (required, conditional, limits, signature-name match incl. Cyrillic/Latin),
  salary-range mapping, questionnaire mapping, PDF renderer produces a valid multi-page PDF for a full and a
  minimal form.
- Integration:
  - session and submit work with no credentials at all (general pool excluded from the list);
  - nothing else became anonymous: a sample of HR endpoints still 401 without a token; the removed
    `/api/recruiting/kiosk-devices` routes are gone (404/405);
  - honeypot filled → 400, nothing written;
  - submit rate limit per client IP: `X-Bonvi-Client-Ip` A hits the limit → 429, IP B still accepted;
    global daily cap → 429;
  - submit walk-in → candidate `OnlineForm`, first stage, questionnaire mapped, photo set, form row,
    PDF attached (`%PDF` header), audit row with Anonymous actor «Onlayn anketa» and the client IP;
  - submit with the phone of an InProgress candidate → no new candidate, form + PDF on the existing one,
    only empty fields filled, bot questionnaire untouched;
  - same `clientSubmissionId` twice → one form, same number;
  - files cap reached → 201, form saved, `fileId` null;
  - HR forms list / PDF: `recruiting.view` 200, without 403.

## 10. Rollout

New NuGet package and embedded fonts; one migration (`candidate_forms` + sequence). Caddyfile changes
(Cloudflare trusted proxies, `X-Bonvi-Client-Ip`, optional `ANKETA_SITE` block) — test on the prod copy
that login, audit IPs and the device/ingest endpoints behave exactly as before. Optional env: prod
`ANKETA_SITE`, web build `VITE_ANKETA_URL`. Deploy follows the usual safe-deploy checklist. After deploy
HR opens `…/anketa?planshet=1` once on the office tablet. Recommend the tablet's own lock mode (Android
«Ekranni mahkamlash» / iPad «Guided Access») so candidates stay on the page.
