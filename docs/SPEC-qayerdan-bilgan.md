# SPEC — «Ish haqida qayerdan bilgan» as an editable catalog, required, with statistics

> Status: authoritative. Written 2026-10-26. Owner (screenshot of the candidate card's «Анкета» →
> «Иш ҳақида қаердан: Telegram»): «Иш хакида каердан билганлигини киритишни мажбурий килиш керак.
> Статистика учун бунда dropdown билан танлансин, ва турларни мен таҳрирлай олай? ёки HR'лар таҳрирлай олсин,
> яъни янги тур қўшиш ёки таҳрирлаш … масалан telegram, instagram, internetdan … tanishlardan».

## 1. Today

- `ReferralSource` is a hard-coded enum (`Bonvi.Domain/Recruiting/CandidateQuestionnaire.cs`): Acquaintance 1,
  SocialNetwork 2, Olx 3, HeadHunter 4, Telegram 5, Other 6, Announcement 7, EducationalInstitution 8.
- It lives only in `candidate_questionnaires.referral_source` (int, not null) — i.e. only bot candidates have it.
  The online anketa (HR-F02, uncommitted, not deployed) also maps into it. HR's manual «Nomzod qo'shish» has no
  such field, so manual candidates never have it.
- Prod (read-only, 2026-10-26): 5207 candidates; 275 bot questionnaires — Telegram 249, Other 11,
  Acquaintance 9, SocialNetwork 4, HeadHunter 2; 4865 manual (mostly the Jobster import), 68 Excel.

## 2. Target

1. **Catalog** «Qayerdan bilgan» — HR edits it, no developer, no deploy: add, rename, deactivate.
2. **Candidate-level field** — every candidate can have it, whatever the channel, so statistics cover all.
3. **Required** for every NEW candidate: bot, online anketa, HR's manual add.
4. **Statistics** — a report: how many candidates came from each source and how many were hired.

## 3. Catalog `ReferralChannel` (table `referral_channels`)

- A `ReferenceEntity` like the others (name + active + note; name unique by `lower(name)` with
  `ux_referral_channels_name`; `uz-Cyrl-x-icu` collation; lists alphabetical; soft delete per the base
  class). Follow `AbsenceType` exactly — it is the closest analogue (a catalog with one behaviour flag),
  including the web catalog kind, routes, permissions, audit labels and export.
- Extra flag `AsksDetail` (bool, default false) — UI label «Aniqlik so'ralsin» with hint «Masalan,
  «Tanishlar orqali» — kim tavsiya qilgani so'raladi». When the chosen channel has it, a free-text detail
  (≤ 200) is required («Kim tavsiya qildi / qaysi manba?»).
- Permission: `catalog.view` / `catalog.manage` (same as every catalog; the HR manager role has both, the
  owner's admin has all). Web route `/malumotnomalar/qayerdan-bilgan`, in the catalogs menu.
- Deleting a channel that candidates use: follow what the catalog base does for used rows (deactivate
  instead / 409); an inactive channel stays on old candidates and in statistics, but is not offered for
  new choices.
- Seed (in the migration, fixed GUIDs so the data move is deterministic), names = today's labels:
  | Old enum | Name | AsksDetail |
  |---|---|---|
  | Telegram 5 | Telegram | no |
  | SocialNetwork 2 | Instagram / Facebook | no |
  | Olx 3 | OLX.uz | no |
  | HeadHunter 4 | hh.uz | no |
  | Announcement 7 | E'lon | no |
  | EducationalInstitution 8 | Kollej / OTM | no |
  | Acquaintance 1 | Tanishlar orqali | yes |
  | Other 6 | Boshqa | yes |
  HR renames/adds later («Internet», «Instagram» separately, …). Use the typographic apostrophe the
  project uses elsewhere in catalog names (check existing seed data).

## 4. Candidate field

- `Candidate.ReferralChannelId` (uuid null, FK → `referral_channels`, ON DELETE RESTRICT, indexed) and
  `Candidate.ReferralDetail` (varchar(200) null). Null only for legacy candidates and Excel imports.
- Migration (one): create + seed the catalog; add the two columns; copy
  `candidate_questionnaires.referral_source` → `candidates.referral_channel_id` through the seed map;
  then drop `candidate_questionnaires.referral_source`. Down: re-add the int column, map back by the seed
  GUIDs (any other channel → 6 Other), drop the new columns and the table. `SET lock_timeout` like the
  recent migrations. Read the generated migration for spurious drops. A migration test covers the
  mapping both ways on rows for all 8 values.
- The online anketa's not-yet-deployed migration (`20261026072156_PlanshetAnketasi`) is earlier in the
  chain; this migration comes after it.

## 5. Where it is set — and required

| Channel | Rule |
|---|---|
| Telegram bot | Already a required step. Its buttons now come from the API (active channels, alphabetical) instead of the enum. If the chosen channel `AsksDetail`, the bot asks one follow-up free-text question (required, ≤ 200). For `uz-Cyrl` the button text is the name transliterated with the project's existing Latin→Cyrillic helper if the bot already has one; otherwise the name as stored. `ru` shows the name as stored. Conversations already in progress at deploy time with an old enum value stored in their state must not crash: re-ask that step (or map 1..8 to the seed GUIDs) — tolerant either way. |
| Online anketa (HR-F02) | Session returns `referralChannels: [{ id, name, asksDetail }]`; answers carry `referralChannelId` (uuid of an ACTIVE channel; required) and `referralDetail` (required when `asksDetail`). Replaces the enum `referralSource` and the old «Acquaintance/Other needs detail» rule. PDF section 8 row 1 prints every active channel ☐/☒ (plus the chosen one if it became inactive) with «, kim/qaysi: <detail>» after the chosen one. Matching an existing candidate (§4 of SPEC-planshet-anketa): fill the referral only when the candidate has none. |
| HR «Nomzod qo'shish» | Required dropdown «Ish haqida qayerdan bilgan» (active channels) + detail input when `asksDetail` (required then). |
| HR edit | The same dropdown. A candidate that has a channel cannot be cleared back to empty; a legacy candidate with none may stay empty when HR edits other fields. Changing it is allowed (HR corrects mistakes). |
| Excel import, Jobster/legacy data | Not required (empty allowed). |
| Re-apply from the bot by an existing candidate | Same as a new application — required. |

Errors (Uzbek codes in the project's style, translated in all 4 locales): channel missing, channel not found
or inactive, detail missing. Inactive channels are rejected only for a NEW choice; saving a candidate whose
existing (now inactive) channel is unchanged must still work.

## 6. Where it is shown

- Candidate card: move «Ish haqida qayerdan bilgan» from the «Anketa» (questionnaire) card to the main
  personal-info card, shown for every candidate («—» when empty), with the detail after a dash
  («Tanishlar orqali — Rustamov Bahodir»). The questionnaire card no longer has the row.
- Candidates list: filter «Qayerdan bilgan» (active + inactive channels, plus «Ko'rsatilmagan»); the
  candidates Excel export gets the column. No new list column (the list is one-line compact).
- Hand-over to employee (`CandidateHandover`): the note line uses the channel name (+ detail).
- MCP tools that print the questionnaire's referral: use the candidate's channel name.
- Audit: catalog changes like other catalogs; candidate referral change through the existing candidate
  audit.

## 7. Statistics — Reports page («Hisobotlar»)

New section «Nomzodlar qayerdan bilgan», built like the turnover-reasons section (`turnover-reasons.tsx`
and its query): period filter by the candidate's application date (`created_at`, Tashkent dates, the
project's from/to date filter), optional vacancy filter. Table: channel · candidates · hired (reached the
hired outcome / became an employee — use whatever the funnel already counts as hired) · hired % ; inactive
channels included if they have rows; a «Ko'rsatilmagan» row for null. Sorted by candidates desc. A simple
horizontal bar like the turnover chart. Excel export through the existing list-export pattern. Permission:
the same as the other recruiting numbers on the Reports page (check `ReportsView` + recruiting rules
already used there; do not widen access to candidate data).

## 8. Tests

- Domain: channel create/rename/activate, `AsksDetail`; candidate set/clear rules.
- Application/Integration: catalog CRUD + RBAC (`catalog.manage` 200, without 403); create candidate
  without channel → 400; with inactive channel → 400; `AsksDetail` without detail → 400; edit legacy
  candidate without channel → OK; cannot clear; bot submit with a channel id; anketa submit with
  `referralChannelId`; report numbers (period, vacancy, null row, hired); export.
- Migration test: data move 1..8 ↔ seed GUIDs both ways, column dropped/restored.

## 9. Rollout

One migration with a data move (275 prod rows). Bot and API deploy together (the bot reads channels from
the API). Deploy follows the safe-deploy checklist with the prod-copy test; add a line to the
«⏸ Navbatdagi reliz» paragraph.
