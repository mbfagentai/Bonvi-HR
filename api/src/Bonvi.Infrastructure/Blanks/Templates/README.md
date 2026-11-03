# Blank template definitions (wave 1)

Seeded definitions of HR-F06, F07, F08, F09, F13 (`docs/SPEC-blankalar.md` §4, content per §4.10).
Source of labels: `docs/INTAKE-hr-blankalar.md` §2, wording checked against `data/hr-paket/HR-F*.txt`.
A published definition is never edited: any change is a new file version (`version` bump) picked up by the seeder.

## Conventions used in every file

- `schema: 1`; `code` equals the file name; `version: "1.0"`; `legalStatus` written out explicitly
  (`final` everywhere except F13 = `draft`, owner Q2). `organization` omitted (default header text).
- Labels and option texts are verbatim from the paper. Apostrophes are ASCII `'` as in INTAKE (the F09 source
  mixes `’`). Paper typos are kept («xisobchisi», «javovgaliklarni»).
- Paper section numbers («1.», «2.») are dropped from section titles, as in the §4.9 example. F13 keeps
  «1-qism … 4-qism» because the parts are referenced by name in the text («olishdan bosh tortsa — 3-qism»).
- Signature lines become **steps**, not fields. «Sana» next to a signature is the step's `acted_at`.
- The blank's own «№» (F09 «Akt №») is the submission number, not a field.
- The subject is always field `employee` (ref, `prefill: subject.fullName`, `readOnly`) — copied from §4.9.
- «Lavozim / bo'lim» is one `text` field prefilled from `subject.positionName` (one prefill per field; see gaps).
- Table column ids are unique across the whole file (not only per table) so no reader has to disambiguate.
- Labels that the paper does not print (option `extra` prompts, docRef links, a few section titles) are short
  authored Uzbek words; each is listed below.
- Option values: `^[a-z0-9_]{1,32}$` — so §4.10's `extraInterview` is written `extra_interview`.

## HR-F06 — Moslashtirish rejasi (9 sections, 26 fields incl. statics/columns, 10 steps)

Mapping: header → `hdr`; §1 → `s1` (static, verbatim paragraph); §2 checkpoint table → one step-owned section per
checkpoint row with a printed «Nima bo'lishi kerak … Kim tekshiradi …» static (`d1`, `d7`, `d30`, `d60`, `d85`),
mentor rows 1–7 / 8–30 / 31–60 → `open` section `notes`; day 90 → decision of step `day90`; §3 → `open` table
`questions`; §4 signatures → order-1 group (employee, mentor, head, HR-4). Steps exactly as §4.9 + day1 (2),
day60 (4), day85 (5).

Judgement calls:
- `probationEnd` = prefill `subject.probationEndDate` (as §4.9), not a computed «hire + 90»; label keeps «(+90 kun)».
- Day-7 «☐ o'tdi ☐ qayta (14-kun)» → computed `day7Pass` (≥ 70) with hint «o'tmasa — qayta (14-kun)»; no choice.
- Day-85 also gets a computed `day85Pass` (same rule; R05 «≥ 70%»).
- `day1Done` (check) is required: the HR-4 day-1 step exists to tick it.
- Row «61–85 (—)» has no checker and no result cell → not modelled.
- Step dues use the §4.9 convention «day N = hireDate + N» (so day1 due = hire + 1).
- `questions` third column is «Javob / yechim» (paper), not «Javob / yechim (ustoz)» (§4.9 abridged).
- Section titles authored: «Xodim» (hdr, as §4.9), «1-kun», «30-kun», «60-kun», «85-kun testi»; `notes` reuses
  the paper title «Reja va nazorat nuqtalari».

§4.8 checklist (verified by hand + a throw-away script): ☑ only §4 top/section/field/step keys ☑ only §4.3 types
☑ ids match regex, unique ☑ every `fill.step` names an existing step ☑ computed refs point to earlier fields, no
cycle ☑ every `due.field` is a date field defined in sections ☑ option values valid ☑ no table nesting
☑ `field:mentor` → ref employee ☑ actors ∈ §6.1 ☑ prefill keys ∈ §4.6 ☑ ≤ 500 fields, ≤ 30 steps.

## HR-F07 — Sinov muddati yakuniy bahosi (6 sections, 19 fields, 5 steps)

Mapping: header → `hdr` (+ docRef `f06`); §1 → `crit` (fixedRows c1–c7: `score` + `note`; computed `avgScore`,
`avgBand`) and §3 → `concl`, both filled by step `signHead`; §2 → `tests` (fixedRows d7/d85) filled by `hr4Tests`;
§4 → `ack` (static «Baho bilan tanishdim.») + decision of `signEmployee`; §5 → `hr1` («Reestrga kiritildi (HR-X02)»
check) + decision of `hr1Decision` (canReject).

Judgement calls:
- `dueDate` = `probationEnd` (the HR-1 decision must land before probation ends; R03 day 88). Not in §4.10.
- `mentor` is required (needed to resolve `field:mentor`), entered by hand — no prefill from F06 exists (gap).
- `score` column required (an average of fewer than 7 marks is meaningless); `note` optional.
- `avgBand` is a text computed «suggestion»; the band legend is the field `hint`, verbatim from paper.
- Paper header names a single legal entity (not «MBF Company») → default `organization` used.
- Authored labels: docRef «Moslashtirish rejasi (HR-F06)», extras «Reja», «Sabab», «E'tiroz»; table labels
  «Mezon», «Test» (paper column headers).

§4.8 checklist: ☑ keys ☑ types ☑ ids unique ☑ `fill.step` → existing steps ☑ `criteria[*].score` exists, `avgBand`
→ earlier `avgScore`, no cycle ☑ `dueDate` → date field ☑ option values (`extra_interview`) ☑ no nesting
☑ `field:mentor` → ref employee ☑ actors/prefill valid ☑ limits. ⚠ `testPassed` refs sibling column `testPercent`
(see gap 1).

## HR-F08 — Chiqish suhbati varag'i (5 sections, 22 fields, 3 steps)

Mapping: header → `hdr` (computed `stajDays` = diffDays(hireDate, leaveDate); `initiative` choice); §1 →
`reasons` (`mainReason` choice required, `extraReasons` multi maxSelected 2, both K01–K12); §2 → `interview`
(Q1 `when initiative = employee`); §3 → `handover` filled by step `hr3Status`; §4 «Reestrga kod kiritildi» →
`open` section `registry`; signatures → steps hr3 (1), subjectEmployee + hr4 (2).

Judgement calls:
- Only Q1 is conditional (§4.10), although the paper section heading says «(xodim ketishi tashabbusi bo'lganda)»
  for all of §2; the heading is kept as the section title.
- `hireDate`, `leaveDate`, `initiative` required (staj and the Q1 condition depend on them).
- `dueDate` = `leaveDate` (R01 2.4: by the last working day). Not in §4.10.
- K06 extra «qayer, nima yaxshi» optional (paper does not demand it). Q2 split into choice + text, Q7 into
  yesno + text «nega» (unconditional — paper places it after «yo'q» but does not say «only if»).
- Registry check is `open` because HR-3 does it on the 5th working day, after the signatures.
- Authored: section title «Reestr», docRef label «Topshirish-qabul qilish akti (HR-F09) №», extra «sabab» (paper).

§4.8 checklist: ☑ keys ☑ types ☑ ids unique ☑ `when` → earlier `initiative` ☑ `fill.step` → `hr3Status`
☑ computed refs earlier ☑ `dueDate` → date ☑ option values `k01`… ☑ no nesting ☑ actors/prefill valid ☑ limits.

## HR-F09 — Ishlarni topshirish-qabul qilish akti (7 sections, 38 fields, 6 steps)

Mapping: header → `hdr` (`openedDate` prefill today, `mustCompleteBy` = `dueDate`, `kind`, `receiver`); §1–§4 →
tables `tasks`, `documents`, `access`, `contacts` (minRows 0, maxRows 50); §5 → `s5` four choices with «bor» extra +
responsible ref; §6 → `s6`; §7 signatures + HR-2 mark → steps 1–6 per §4.10.

Judgement calls:
- Step 6 (`hr3Clearance`, «… bo'shatish buyrug'iga ruxsat») has `when kind = dismissal`: there is no dismissal
  order for «ichki o'tkazish» / «uzoq ta'til». Remove the `when` if the owner wants the mark always.
- §1 «Keyingi qadam va muddat» split into text + date column «Muddat» (INTAKE: text+date).
- §2 per-row «Qabul qiluvchi imzosi» is not a column: the receiver signs the whole act (step `received`).
- §5 hand-added rows kept with paper wording; the named persons on the sample became an empty ref employee
  field «F.I.SH» (no personal data copied). Kamomad's «(buxgalteriya bilan hal qilinadi)» is the hint.
- «Inventarizatsiya o'tkazildi: ha — dalolatnoma № … sana …» → option «ha» with one text extra
  «dalolatnoma № , sana» (an option has a single extra).
- §5/§6 fields not required (paper: «moddiy javobgar lavozimlar uchun», and «talab qilinmaydi» exists).
- `receiver` required (actor of step 2). No `canReject` anywhere (not in §4.10).

§4.8 checklist: ☑ keys ☑ types ☑ ids unique (columns too) ☑ step `when` → earlier `kind` ☑ `dueDate` → date
☑ option values ☑ no nesting, maxRows ≤ 200 ☑ `field:receiver` → ref employee ☑ actors/prefill valid ☑ limits.

## HR-F13 — Mehnat intizomi (6 sections, 21 fields, 7 steps; `legalStatus: draft`)

Mapping: 1-qism → `part1` (draft); 2-qism demand text → `part2` (static with `{{employee}}`, `{{violationAt}}`,
`{{violationTypes}}`); employee explanation → `explanationSec` filled by `signEmployee` (inPerson, decision
received/refused); 3-qism → `part3` (`when` explanation and file empty) filled by `witness1`; 4-qism → `part4`
filled by `hr3Measure`; order → `orderSec` (`order` field) filled by `hr2Order`.

Judgement calls:
- «Buzilish turi» is `multi` (§4.10); `late` has int extra «daqiqa», `other` text extra «boshqa», both required.
- `evidence` file: image + pdf, camera `environment`, maxFiles 10; INTAKE «file(s) + text» → extra text field
  `evidenceNote`.
- `explanationFile` accepts pdf/image/office (a scanned or typed letter), camera, maxFiles 5.
- Witness condition treats an attached explanation file as «explanation given» (§4.10 names only the text).
- `prior12m` is an `int` with `prefill: subject.priorF13Count12m`, `readOnly` — not `type: computed`, because a
  computed field needs an `expr` and no expression can read a prefill source (see gap 4).
- `part3` act: `actDate` (max today), `actReason` choice (label authored «Holat»; «hujjat (____)» → extra
  «hujjat»), act static text paraphrased around placeholders, witness positions as two text fields (paper
  «(lavozim, imzo)»); witness names are typed at signing (`signer_name`).
- «Zarar (bo'lsa)» is `text` (INTAKE «text/money»: covers non-monetary damage).
- HR-1 «chora o'zgartirildi: ____» / «chora qo'llanmaydi — sabab: ____» → decision options with text extras.
- Authored: step titles «Guvoh 1 / 2 (lavozim, imzo)», «HR-3: intizomiy chora loyihasi», section «Buyruq (HR-2)».

§4.8 checklist: ☑ keys ☑ types ☑ ids unique ☑ section/step `when` → earlier `explanation`, `explanationFile`
☑ `fill.step` → existing steps ☑ placeholders → earlier fields ☑ option values ☑ no nesting ☑ `witness` steps
inPerson ☑ actors/prefill valid (`currentUser.employeeId`, `subject.priorF13Count12m`) ☑ limits.

## SPEC gaps (for the SPEC owner / validator author — nothing here invents a key)

1. **Row-scoped refs in table computed columns.** §4.10 asks for a computed «pass» per F07 test row, but §4.5 only
   defines `{"ref":"field"}` and `{"ref":"table[*].col"}`. F07 `testResults.testPassed` uses `{"ref":"testPercent"}`
   meaning «sibling column, same row». The validator/evaluator must resolve a bare ref inside a column against
   sibling columns first — or the SPEC must define another form (then only this one expr changes).
2. **«Later-defined» vs `fill.step`.** §4.8 rejects refs to «later-defined items», but `fill.step` always points
   to `steps`, which come after `sections` (also in §4.9). Read it as: fields must be defined earlier; steps may be
   referenced from any section.
3. **Step `when` timing.** Steps are materialized at submit (§5) and `when` false → Skipped then. F13 witness steps
   depend on `explanation`, which is entered later by step 2 — at submit it is always empty, so witnesses would
   never be skipped. The engine must re-evaluate a Waiting step's `when` when its group opens.
4. **Prefill vs computed.** F13 `prior12m` is called «computed» in §4.10 but no `expr` can read a prefill source;
   modelled as readOnly `int` + prefill. Its value is frozen at draft creation (count may change before step 4).
5. **Step decision extras.** Options with `extra` are used in step decisions (F07 «e'tirozim bor», F13 HR-1). §3.4
   stores only `decision` varchar(64) + `comment`; where the extra value is stored (comment? `answers_patch`?) is
   not defined.
6. **«Either A or B required».** F13 explanation is «longtext or file»; no construct says «one of». Both optional.
7. **Prefill gaps (§4.6).** No source for: «Lavozim / bo'lim» as one string (position + department), mentor from
   the latest approved F06 (F07), F09 status/inventory/access from the linked F09 (F08 §3), F06 85-day % into F07,
   F14 reserve employee into F09 receiver. All left for manual entry.
8. **Working-day dues.** F13 (2 working days), F09 «oxirgi kundan 3 ish kuni oldin», F08 «5-ish kuni» need
   working-day arithmetic; `due` only adds calendar days, so those steps have no `due`.
9. **Decision-dependent steps.** F13 `hr2Order` should be skipped when HR-1 chose «chora qo'llanmaydi» or the
   measure is «og'zaki ogohlantirish (buyruqsiz)»; `when` cannot read a step decision (only fields), and the
   measure can change at HR-1. Step stays unconditional; HR-2 enters the order or HR-1 rejects.
10. **F13 refusal vs in-person signature.** Step 2 is inPerson with decision «olishdan bosh tortdi», but an
    in-person act requires the employee's signature PNG (§6.2) — a refusing employee will not sign. Needed:
    operator signs on the employee's behalf with decision `refused`, or a non-signature path.
11. **F13 «Xodim buyruq bilan tanishdi / Sana».** On paper, not in §4.10's step list → not modelled. Candidate
    for step 7 (subjectEmployee, inPerson) in v1.1.
12. **F08 cross-field rule.** `extraReasons` must not repeat `mainReason`; no constraint exists for that.
13. **Placeholder rendering.** `{{fieldId}}` formatting for ref/datetime/multi values (F13 `part2`) is not
    specified; the renderer should print label / Tashkent date-time / option labels.
