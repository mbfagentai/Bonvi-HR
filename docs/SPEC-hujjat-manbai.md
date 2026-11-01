# SPEC — HRMentor is the only source of documents; hr.bonvi.uz takes its own

> Status: authoritative. Written 2026-11-01. Owner's words: «boshqa platformalarda mustaqil ravishda hujjat
> yoki blanka yaratilmaydi, avval hr mentorga yuklayman yoki o'zgartiraman hamma dastur hr mentordan olishi
> kerak o'ziga tegishli va kerakli hujjatlarni». Approved the same day: «Boshla san aytganlaring to'g'ri».
>
> Counterpart: HRMentor `docs/SPEC.md` §20 — the decision (alternatives, scenarios), the platform registry
> and the pull API. This file is hr.bonvi.uz's side only.

## 1. The rule

- **S1.** hr.bonvi.uz creates no document template of its own: blanks, regulations, procedures, the
  candidate questionnaire and the user manual are uploaded or edited in HRMentor first and taken from
  there. What this system produces from its own data (a candidate's filled form and its PDF, Excel exports,
  reports) is not a template and stays here.
- **S2.** What we pull is a cache: the last good copy keeps working while HRMentor is down. A built-in copy
  in our code (D1–D3 below) is only the cold-start and emergency fallback, and HRMentor decides when it is
  used (HR-F02: nothing published, or «Asl formaga qaytarish»).
- **S6.** The §18 catalog and the §19.4 profile stay in our code: they describe what this system can render
  and store, they are not documents.

## 2. Pulling our documents (sync step 3, «Blankalar»)

`HrMentorSync` step 3 asks HRMentor for **this platform's** documents — `GET /api/v1/platform/documents` —
and downloads changed files from `GET /api/v1/platform/documents/{id}/file` (same size and SHA-256 checks,
same storage, same «remove what left the list» as today). At the end of the step it reports what it holds —
`PUT /api/v1/platform/holdings` with every tracked blank's `document_id` and `version_id` — so HRMentor's panel
shows «hr.bonvi.uz: Yangi / Eski versiya / Olmagan» per document. A failed report is the step's error; the
blanks themselves are already saved.

Transition: the key gets `platform:sync` and is bound to the platform «hr.bonvi.uz» in HRMentor's panel
(HRMentor §20.12). Until then HRMentor answers 403 `platforms.key_not_bound`, or 403 when the key lacks the
permission, or 404 when HRMentor predates §20; in each of those cases the step falls back to the old
`GET /api/v1/blanks` and records the source in the Hidden setting `hrmentor.blanksSource` (`platform` |
`legacy`). The «Blankalar» page then says «HRMentor kaliti hali platformaga bog'lanmagan — blankalar eski
ro'yxatdan olinmoqda». The fallback is removed with HRMentor's `/blanks` (no later than 2027-01-31).

An empty platform list on the first switch counts as «not ready» too: while the last good list did not come
from the platform, an empty answer (HR granted the permission before assigning the folder) takes the old list
instead of removing every blank. Once a list has come from the platform, an empty one is HR's decision and the
blanks that left it are removed.

Nothing else falls back. A switched-off platform (403 `platforms.platform_inactive`) is the step's error
`hrmentor.platform_off` and the last good copy stays: HR switched it off on purpose, and the old list would
undo that. 401, any other 403, a timeout or a 5xx are the step's error as today. The holdings report goes only
on the platform path; on the legacy path there is no platform to report to.

A non-form document HR assigns to hr.bonvi.uz is listed on the same page like a blank. A type column comes
with the first non-form assignment (trigger, not built now).

## 3. Guard — `HujjatManbaiQulfiTests` (Bonvi.Architecture.Tests)

1. **PDF templates.** Every type that depends on `PdfSharp` or `MigraDoc` is on a list: permanent —
   `DynamicFormPdfRenderer` (prints HRMentor's published definition), `BundledFontResolver` (fonts); debt —
   `QuestionnairePdfRenderer` (D1). A new type fails.
2. **Template files.** Office, PDF and RTF files (`.doc .docx .dotx .odt .pdf .rtf .xls .xlsx .xltx`) under
   `api/src`, `web/src`, `web/public`, `app/src`, `app/public` are on a list: debt — the two manual PDFs
   (D5). A new file fails.
3. **Built-in form sources.** The debt types `HrF02Seed`, `HrF02TextCatalog`, `QuestionnairePdfTexts` and
   the file `Bonvi.Bot/Conversation/BotTexts.cs` still exist while they are listed.
4. **Ratchet.** A debt item that is gone but still listed fails too, so the list only shrinks.

Reflection covers the four assemblies the test project references; a source scan of `api/src` (code lines,
not comments) covers the rest (Bot, DeviceAgent, …). The file checks read the repository root: `./bonvi test`
mounts it read-only at `/repo` (`BONVI_REPO_DIR`); on a host checkout the root is found upward from the test.

## 4. Debt register (same as HRMentor §20.10)

| # | Built-in document | How it leaves | Trigger |
|---|---|---|---|
| D1 | HR-F02 v3.1 online form (kiosk) and its PDF (`QuestionnairePdfRenderer`) | Stays only as the S2 fallback once HR publishes HR-F02 in HRMentor; removal decided after 60 days of a served definition with no withdraw | release + 60 days |
| D2 | `HrF02TextCatalog` defaults, `QuestionnairePdfTexts` | Go with D1 | with D1 |
| D3 | `HrF02Seed` | One-time import; never the source after HRMentor's first publish | with D1 |
| D4 | The bot questionnaire (`BotTexts.cs`) | The bot asks HRMentor's published definition (own SPEC); until then no new bot question is added in code | the next change of the bot's questions |
| D5 | `web/public/qollanma/*.pdf` | HR uploads the manual to HRMentor and assigns it to hr.bonvi.uz; the help link opens the pulled copy | the next manual update |

## 5. Tests (must pass before «done»)

- Sync: platform list → files downloaded through the platform route, holdings reported (every tracked blank,
  its version), `blanksSource = platform`.
- Sync: 403 `platforms.key_not_bound`, 403 without the permission, 404 → legacy list, `blanksSource =
  legacy`; any other error is the step's error as today (no fallback that hides a real failure).
- The guard of §3, proven red once by adding a PDF type and a template file.
- The change wait of §7: not connected → nothing asked; first answer → sync; same revision → no sync; a new
  revision → sync at once; a failed sync keeps the old revision; not ready / off / network errors keep it
  and name the code; the loop starts, syncs on a change and stops cleanly; the client's query, bounds and
  error mapping; the back-off steps.

## 6. Rollout

Local only until the owner says otherwise. Order: HRMentor first (its §20.12), then this system, then bind
the key; the fallback keeps the page full in between.

## 7. Hearing of a change at once (HRMentor `docs/SPEC.md` §21, added 2026-11-01)

The owner, the same day: «hrmentorda xujjat yangilansa barcha platforma va loyihalar avtomatik tarzda xujjat
yangilanganini bilib o'zlarini zudlik yangi xujjatlar bilan yangilab olishlari kerak». The five-minute
`hrmentor:sync` alone means 0–5 minutes of delay; this section takes it to seconds.

- **The wait.** A hosted service, `HrMentorChangeWatcher` (Api), keeps one `GET /api/v1/platform/changes`
  open (`wait=25`, `since=<last revision>`). Each step runs in its own scope through `HrMentorChangeWatch`
  (Application). HRMentor answers at once when what this platform would pull — its documents, their
  assignment, the HR-F02 texts revision or the published definition — is not what `since` names, and
  otherwise after 25 s.
- **A new revision → sync now.** The same `HrMentorSync` run as the five-minute job, waiting up to two
  minutes for a run in progress rather than skipping it. Only a clean run moves `since` to the new revision;
  a failed or busy run keeps the old one, so the next step tries again. On start `since` is empty, so the
  first answer syncs: nothing that changed while hr.bonvi.uz was down is missed.
- **A lasting error does not hold the wait back** (2026-11-01). When every failed step failed on what HR
  saved — texts or definition we cannot accept, a blank file that does not match its list or is too large
  (`HrMentorErrors.IsLasting`) — asking the same state again gives the same answer, so the step is
  `SyncedWithError`: `since` moves on, the rest of the run is applied, and the five-minute job re-checks the
  failed step. Network, timeout, 5xx, other 4xx and busy stay transient and keep the old `since`. Measured
  before: one invalid texts answer held every HR change for up to five minutes.
- **Our own writes never wake us.** The catalog, the profile and the holdings report change none of the
  fields HRMentor's revision covers (§21.4 R1), so a sync does not call the next.
- **Back-off** (`HrMentorWatchDelay`): after a sync, wait again at once — but syncs in a row with no
  unchanged answer between them slow down like errors (5 s doubling to 5 min), so an HRMentor that answers
  a new revision every time cannot drive a sync loop; after an unchanged answer, 1 s;
  not connected 30 s (and `since` is dropped — the next connection may be another platform); key not bound
  or an HRMentor without the route (404) 5 min; platform switched off 1 min; any other error 5 s doubling to
  5 min.
- **The safety net stays.** `hrmentor:sync` keeps running every five minutes; if the wait breaks, a change
  still arrives within five minutes. The legacy `/blanks` list (key not bound) is served only by that job.
- **Journal:** EventId 4810 synced, 4811 a state change (code only), 4812 a crashed step (exception type
  only), 4813 resumed. No key, address or body is written.
- **Not in `Testing`.** The service is registered only where Hangfire is; tests drive the step and the loop
  themselves.

