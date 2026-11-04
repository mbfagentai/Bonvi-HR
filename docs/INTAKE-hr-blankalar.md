# INTAKE — HR paket blanks (HR-F01 … HR-F19) for the «Blankalar» menu

Written 2026-11-03 by the intake analyst. Facts only; no design. Field labels are copied verbatim
(Uzbek, including the source's own typos). Everything in §1–§4 is read from a source. §5 is inference
and is marked that way.

Sources: `data/hr-paket/*.txt` (plain-text conversions of the owner's docx). Citations use the file
code (e.g. `F09 §5` = `HR-F09_Ishlarni_topshirish_akti.txt`, section 5).

**Personal data:** F01, F04, F05, F07, F08 and F09 are partly filled in with real names, positions,
dates, a salary and a list of channels. HR-00 and HR-04 name the HR staff. None of these values are
copied here. Where a filled value shows how a field is used, this file describes the kind of value only.

---

## 0. Legend

**Type codes**
| Code | Meaning |
|---|---|
| `text` / `longtext` | single line / multi-line free text |
| `int`, `pct`, `money` | integer, percent, amount. The one filled amount is written `5 000 000`, with no decimals and no currency (F01 §1.8) |
| `date`, `datetime`, `time-range`, `date-range`, `month`, `year` | calendar values |
| `yesno` | two-way ☐ ha ☐ yo'q |
| `choice[a/b/c]` | single choice from ☐ options; `+text` / `+date` / `+int` means an option asks for an extra value |
| `multi[...]` | several ticks allowed |
| `check` | a single tick-box ("done", "entered in register") |
| `score1-5` | rating 1–5 |
| `ref:employee` / `ref:candidate` / `ref:dept` / `ref:direction` / `ref:position` | link to a record |
| `ref:Fxx` | link to another blank instance (e.g. «HR-F04 №») |
| `order` | order (buyruq) number + date |
| `docno` | the blank's own running number (№ ______) |
| `sign` | signature line (person/role + usually date) |
| `file` | attachment |
| `computed` | derived value (formula given or implied) |
| `static` | fixed printed text, no input |

**Req column.** The blanks almost never mark fields as required. `R` = required by an explicit rule (cited).
Blank = not stated. **Rows.** `REPEAT(n)` = a table with *n* empty rows on paper, so rows are added
dynamically. `FIXED` = printed row labels.

**Roles (HR-00, HR-04 §1).** HR-1 = head of HR department. HR-2 = personnel administration. HR-3 = timesheet,
attendance, discipline, reporting. HR-4 = recruitment and onboarding. HR-5 and HR-8 are future hires: HR-5 takes
onboarding, training and assessment. HR-8 takes position passports and analytics (HR-04 §1, «2026 dekabr» /
«2026 noyabr–dekabr»). Other actors: «Bo'lim rahbari» (department head), «Ustoz-murabbiy» (mentor),
«Ish haqi hisobchisi» (payroll accountant, accounting department), «Bosh buxgalter», «Bosh direktor», «Yurist»,
«Mehnat muhofazasi xizmati», «Kengash» (hiring council), candidate, employee.

---

## 1. Inventory

| File | Size | What it is | Read? |
|---|---|---|---|
| HR-00_Hujjatlar_reestri.txt | 6.7 KB | Register: package structure, blank × filler × process, per-role «my documents», 4-week rollout | yes |
| HR-04_Funksiyalarni_4_xodimga_taqsimlash.txt | 19.7 KB | Function split across HR-1…HR-4, turnover analysis, F03 (old «78») rewrite | yes (role/blank parts) |
| HR-F01 … HR-F19 (19 files) | 1.1–3.7 KB each | The 19 blanks | yes, all 19 |
| HR-R01 … HR-R08 (8 files) | 2.5–4.2 KB each | Process regulations | yes |

Conversion limits that affect this intake:
- Table cell boundaries are lost, and empty cells are sometimes dropped. Because of this, the column that holds
  the ☐ options in F04 §2 is ambiguous (see Q5).
- The signature lines and ☐ marks come through as text, so it is impossible to tell whether a ☐ was ticked.
- The original docx files were not opened. Their location is not given in the brief.

---

## 2. Per-blank facts

### HR-F01 — Bo'sh o'rin so'rovi (ЗАЯВКА)
| | |
|---|---|
| Purpose | Department head requests a vacancy to be filled |
| Trigger | Vacancy appears: «xodim ketdi», «yangi lavozim», «hajm oshdi», «almashtirish» (F01 §1.6) |
| Fills | Bo'lim rahbari (§1–2). HR-4 issues the №, receives the request and fills §4 milestones |
| Approves (order) | So'rovchi → Qabul qildi HR-4 → Tasdiqladi HR-1 → Bosh buxgalter («Shtat jadvaliga mos», only if new position) (F01 §3). R02 step 3: HR-1 decides staff and fund «bosh buxgalter bilan» |
| Deadlines | HR-1 within 2 working days (header, R02 step 3). HR-4 logs it the same day (R02 step 2). Ad within 1 working day of approval (R02 step 4). Close norm: «ishchi 14 / mutaxassis 30 / rahbar 60» days, measured from approval to start (R02 §1) |
| Rule | No request without a position passport: «Pasport yo'q lavozimga ishga qabul so'rovi (HR-F01) qabul qilinmaydi» (F10 header, R02 §2.2) |
| Register | HR-X02 «So'rovlar» |
| Source state | Partly filled sample (not copied) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | So'rov № (HR-4 beradi) | docno | | issued by HR-4 |
| hdr | Sana | date | | |
| 1.1 | Yo'nalish (lug'atdan) | ref:direction | | «lug'atdan» = from dictionary (R08 §3, 16 directions) |
| 1.2 | Bo'lim / sex | ref:dept | | prefill: requester's department |
| 1.3 | Lavozim (shtat jadvalidagi nom) | ref:position | | |
| 1.4 | Lavozim pasporti mavjudmi | choice[ha (kod: text) / yo'q — avval HR-F10 bo'yicha tuziladi] | R (R02 §2.2) | prefill: does an F10 exist for the position |
| 1.5 | Nechta o'rin | int | | |
| 1.6 | So'rov sababi | choice[xodim ketdi (FISH: ref:employee) / yangi lavozim / hajm oshdi / almashtirish] | | |
| 1.7 | Ish rejimi (smena, soat) | text | | sample value is a time range |
| 1.8 | Ish haqi (tasdiqlangan shtat bo'yicha) | money | | prefill: position's approved salary, if the system holds it |
| 1.9 | Kerakli sana | date | | |
| 2 | Talab rows FIXED: «Ma'lumot», «Kasbiy tajriba (yil)», «Malaka razryadi / sertifikat», «Amaliy sinov (nima bajaradi, kim baholaydi)», «Boshqa» — column «Majburiy / afzallik» | text per row | | Ambiguous. The sample puts the requirement itself in this column («kamida 1 yil», «shart emas»), not a mandatory/preferred flag (Q8) |
| 3 | Tasdiq table: Kim / Lavozim / Imzo / Sana; rows So'rovchi, Qabul qildi (HR-4), Tasdiqladi (HR-1), Shtat jadvaliga mos (yangi lavozim bo'lsa) (Bosh buxgalter) | sign ×4 | | |
| 4 | HR-4 belgilari: Bosqich / Sana / Izoh; rows «E'lon joylandi», «Birinchi suhbat», «Kengashga chiqarildi» (HR-F04 №), «Ishga chiqdi» (buyruq №), «So'rov yopildi (kun)» | FIXED: date + text; ref:F04; order; computed | | «So'rov yopildi (kun)» = days approval→start against norm 14/30/60. The sample's «Izoh» for «E'lon joylandi» lists channels |

Computed: «So'rov yopildi (kun)». Attachments: none stated.

### HR-F02 — Nomzod anketasi (already built, see `docs/SPEC-planshet-anketa.md`)
- Filled by the candidate «markaziy ofisda, HR-4 huzurida». Retention: personal file if hired, otherwise
  6 months (F02 header).
- If the candidate cannot reach the office, HR-4 fills it by phone or video and the original signature
  is taken on the first working day (HR-04 §2.5).
- Sections: 1 Shaxsiy ma'lumotlar (9 rows) · 2 Ma'lumot va malaka (REPEAT 3) · 3 Mehnat faoliyati (REPEAT 3,
  last 3 jobs) · 4 Nomzodning javoblari (6) · 5 Rozilik va tasdiq (candidate sign) · 6 HR-4 xulosasi
  (4 criteria score1-5 + Qaror choice[Kengashga / zaxira bazaga / rad] + HR-4 sign).
- Header links: «So'rov № (HR-F01)», «Lavozim».
- **Version note:** this .txt is «Versiya 1.0». The platform was built from `HR-F02_Nomzod_anketasi_v2.docx`
  (SPEC-planshet-anketa sources). The two versions were not compared.

### HR-F03 — Nomzodni tekshirish (surishtiruv) blankasi — sobiq № 78
| | |
|---|---|
| Purpose | Reference/background check with the candidate's written consent |
| Scope | Full check (I–IV) only for material-responsible roles («omborchi, kassir, inkassator, sotuvchi, haydovchi-ekspeditor, ta'minotchi») and manager roles. Other roles: only sections I and II (F03 header) |
| Trigger | After F02, before F04 (header). R02 step 8 |
| Fills | I: candidate. II–IV: HR-4 |
| Approves | HR-4 signs. HR-1 approves (only for material-responsible or manager roles) (§IV) |
| Deadline | 3 working days (R02 step 8). Section I «suhbat kunida» (rule 1) |
| Rules | No consent → II–III not done, and the council is told «tekshiruv o'tkazilmadi» (rule 1). Nobody outside the candidate's named referees is contacted (rule 2). Access: HR-1, HR-4, council members only (rule 4). Not hired → destroyed after 6 months, HR-2 marks it in the register (rule 3). «[?]» final text pending lawyer |
| Naming | HR-04 §5 calls the full form «HR-F78» |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| I | Men, ______ (F.I.Sh.), ______ lavozimiga nomzod sifatida … | ref:candidate + ref:position | | prefill: candidate, vacancy position |
| I.1 | Oldingi ish joyi (nomi: ______) — ish davri, lavozimi, ketish sababi, rahbar tavsiyasi | text + choice[roziman / rozi emasman] | | prefill: employer name from F02 §3 row 1 |
| I.2 | Men ko'rsatgan tavsiya beruvchi shaxslar bilan aloqa (quyida) | choice[roziman / rozi emasman] | | |
| I.3 | Ma'lumotlarim ishga qabul qilinmagan taqdirda 6 oy saqlanib, so'ng yo'q qilinishi | check «tanishdim» | | |
| I | Tavsiya beruvchi shaxslar: F.I.Sh. / Kimligi (sobiq rahbar, hamkasb va h.k.) / Telefon | REPEAT(2) text, text, phone | R min 2 («kamida 2 nafar») | |
| I | consent paragraph | static | | |
| I | Nomzod imzosi / Sana | sign | | |
| II.1 | Kim bilan gaplashildi (lavozimi, telefon) | text | | |
| II.2 | Ish davri tasdiqlandimi (nomzod aytgan sanalar bilan mos) | choice[ha / yo'q / qisman] | | |
| II.3 | Lavozimi va vazifalari tasdiqlandimi | yesno | | |
| II.4 | Ketish sababi (ish beruvchi so'zi bilan) | text | | |
| II.5 | Intizom, davomat, moddiy javobgarlik bo'yicha e'tirozlar | choice[yo'q / bor: +text] | | |
| II.6 | Qayta ishga olarmidingiz? | choice[ha / yo'q / javob bermadi] | | |
| II.7 | Ma'lumot berishdagi ishonchlilik darajasi | choice[aniq / o'rtacha / shubhali] | | |
| III | Tavsiya beruvchi / Nomzod haqida fikri (qisqa) / Ishonchlilik choice[aniq / o'rtacha / shubhali] | REPEAT(2) | | only if I.2 = roziman. Rows mirror the referees in §I |
| IV | Ishga olish mumkin — e'tiroz yo'q / … — kuzatuv bilan (+text) / Ishga olish tavsiya etilmaydi (sabab: +text) | choice | | |
| IV | Tekshiruvni o'tkazdi: HR-4 (imzo) Sana · Tasdiqladi (moddiy javobgar / rahbar lavozimlar uchun): HR-1 (imzo) Sana | sign ×2 (HR-1 conditional) | | |

### HR-F04 — Ishga qabul kengashi bayonnomasi
| | |
|---|---|
| Purpose | Minutes of the weekly hiring council: decision per candidate |
| Trigger | Weekly council (R02 §2.4, «haftada 1 marta». HR-04 §2.5: «payshanba») |
| Fills | HR-4 / HR-1 (HR-00). The secretary is labelled «HR-1 (kotib» / «Kotib (HR-1)». R02: HR-1 personally runs it for manager roles |
| Signs | Each attending member (§1 Imzo). Footer: Rais, Kotib (HR-1) |
| Quorum | «tasdiqlangan tarkibning kamida 2/3 qismi» (header, R02 §2.4) |
| Deadline | §4 steps «1 ish kuni ichida» after the council |
| Source state | No version/filler header line (unlike most blanks). Partly filled sample (not copied) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Bayonnoma № / Sana | docno, date | | |
| hdr | Ishtirok etdi: ______ nafar a'zo | computed int | | = ticked «Ishtirok». Quorum check ≥ 2/3 of the approved membership |
| 1 | Kengash a'zolari: F.I.Sh. / Lavozim / Ishtirok / Imzo | REPEAT (sample 4 rows: Rais, Bo'lim rahbari ×2, HR-1 (kotib)) ref:employee, text, check, sign | | prefill: approved council membership |
| 2 | Ko'rib chiqilgan nomzodlar: Nomzod F.I.Sh. / Lavozim / bo'lim / So'rov № (F01) / Anketa № (F02) / Qaror / Amaliy sinov bahosi / Surishtiruv (F03) / Ish haqi / Ishga chiqish sanasi | REPEAT (sample 10 rows) ref:candidate, ref:position+dept, ref:F01, ref:F02, ?, ?, ?, money, date | | prefill: candidate, vacancy, F01 №, F02 №, F03 outcome. The «Qaror» options are not printed. R02 step 10 = «qabul / zaxira / rad». The sample has free-text comments. The ☐ set «o'tdi / o'tmadi / talab qilinmaydi» belongs either to «Amaliy sinov bahosi» or «Surishtiruv (F03)» (Q5) |
| 3 | Alohida shartlar: Nomzod / Shart (sinov muddati, ustoz, o'qitish, moddiy javobgarlik shartnomasi va h.k.) | REPEAT ref:candidate, text | | |
| 4 | Kengashdan keyingi qadamlar: Qadam / Bajarildi (sana). FIXED: «Nomzodlarga natija yetkazildi», «Qabul qilinganlarga HR-F05 … berildi», «Hujjatlar HR-2 ga topshirildi (buyruq uchun)», «Reestrga kiritildi (HR-X02)», «Zaxira bazaga kiritilganlar» | date ×5 | | |
| ftr | Rais / Kotib (HR-1) | sign ×2 | | |

### HR-F05 — Ishga taklif va qabul uchun hujjatlar ro'yxati
| | |
|---|---|
| Purpose | Part 1 is the job offer. Part 2 is the document checklist. Part 3 is the first-day checklist |
| Trigger | Positive council decision (F04) |
| Fills | Part 1: HR-4. Part 2: candidate brings the documents, HR-2 checks them. Part 3: HR-2 and HR-4 |
| Signs | Part 1: HR-4, then Nomzod («taklifni qabul qilaman»). Part 2: Nomzod, HR-2. Part 3: per-step |
| Deadlines | Issued «Kengashdan keyin 1 ish kuni ichida» (header). Docs to HR-2 «Ishga chiqishdan 1 kun oldin» (R01 2.1). Contract and order on the first day. Reestr «buyruq kuni». mehnat.uz «3 ish kuni ichida» (Part 3, R01 2.1) |
| Rule | «nomzodga ish haqi va sharoitni noto'g'ri va'da qilish (F05 da yozilganidan farqli)» forbidden (R02 §6) |
| Source state | Partly filled sample (not copied) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| 1 | Hurmatli ______ | ref:candidate | | prefill: candidate name |
| 1 | Ishga qabul Kengashining «__» ____ y. № ____ bayonnomasiga asosan | ref:F04 (date + №) | | prefill: F04 |
| 1 | Lavozim | ref:position | | prefill: F04 §2 / vacancy |
| 1 | Bo'lim / yo'nalish | ref:dept | | prefill |
| 1 | Ish joyi (manzil) | text | | |
| 1 | Ish rejimi | text | | prefill: F01 §1.7 |
| 1 | Ish haqi (sinov muddatida / keyin) | money ×2 | | prefill: F04 §2 «Ish haqi» |
| 1 | Sinov muddati | static «3 oy (Mehnat kodeksi bo'yicha)» + date-range «dan … gacha» | | computed: start + 3 months (Q9) |
| 1 | Ishga chiqish sanasi | date | | prefill: F04 §2 |
| 1 | Bevosita rahbar | ref:employee | | prefill: department head |
| 1 | Ustoz-murabbiy | ref:employee | | feeds F06 |
| 1 | Taklif «__» ____ gacha amal qiladi | date | | |
| 1 | HR-4 / Sana · Nomzod (taklifni qabul qilaman) / Sana | sign ×2 | | |
| 2 | FIXED 12 rows: Hujjat / Asl / nusxa (printed) / Keltirildi (☐) / HR-2 belgisi. Rows: Pasport yoki ID-karta · JSHSHIR (ПИНФЛ) · Ma'lumot to'g'risidagi diplom / attestat · Malaka sertifikatlari (…) · Mehnat daftarchasi (qog'oz, bo'lsa) / elektron — mehnat.uz orqali · Harbiy hisob hujjati (erkaklar) · Tibbiy ko'rik xulosasi (xavfli ishlab chiqarish va oziq-ovqat uchun majburiy) · 3×4 fotosurat — 2 dona · Ariza (ishga qabul qilish to'g'risida) · Bank kartasi rekvizitlari (ish haqi uchun) · Moddiy javobgarlik shartnomasi (omborchi, kassir, inkassator, haydovchi-ekspeditor uchun) · Kengash bayonnomasi nusxasi (HR-4 dan) | check + check per row | conditional rows: men only, hazardous/food only, material-responsible only | Whether scans are uploaded is not stated. The paper only ticks. R01 3: no medical diagnosis in the personal file |
| 2 | Hujjatlar to'liq: ☐ ha ☐ yo'q — yetishmayotgani: ______ muddat: ______ | yesno + text + date | | computed from ticks |
| 2 | Nomzod / HR-2 / Sana | sign ×2 | | |
| 3 | FIXED 6 rows (Qadam / Kim / Sana / imzo): Mehnat shartnomasi imzolandi, buyruq № ____ (HR-2) · Lavozim pasporti bilan tanishuv (HR-F11) (HR-4) · Mehnat muhofazasi yo'riqnomasi (jurnal belgisi) (Mehnat muhofazasi xizmati) · Ustozga biriktirildi, moslashtirish rejasi berildi (HR-F06) (HR-4) · Reestrga kiritildi (HR-X01, 91.2) (HR-2) · mehnat.uz ga kiritildi (3 ish kuni ichida) (HR-2) | sign+date per row. Row 1 has order. Rows link ref:F11, ref:F06 | | |

### HR-F06 — Moslashtirish rejasi (1–30–60–90 kun) va ustoz biriktirish
| | |
|---|---|
| Purpose | Onboarding plan with checkpoints. Mentor assignment |
| Trigger | Hire. Mentor chosen on day −1 (R03 §3) |
| Fills | HR-4 (later HR-5) together with the department head. Each checkpoint row is checked by the role printed in «Kim tekshiradi» |
| Signs | Xodim, Ustoz, Bo'lim rahbari, HR-4 (§4). Day 90 decision: HR-1 |
| Copies | xodim, ustoz, bo'lim rahbari, shaxsiy ish |
| Mentor rule | ≥ 1 year in the same position, no disciplinary penalty. ≥ 15 min daily for the first 30 days. Mentor bonus via R07 if the hire passes (F06 §1) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Xodim F.I.Sh. | ref:employee | | prefill |
| hdr | Lavozim / bo'lim | ref:position, ref:dept | | prefill: employment |
| hdr | Ishga chiqqan sana | date | | prefill: hire date |
| hdr | Sinov muddati tugash sanasi (+90 kun) | computed date | | = hire date + 90 days (system has a probation end date) |
| hdr | Bevosita rahbar | ref:employee | | prefill |
| hdr | Ustoz-murabbiy (F.I.Sh., lavozim) | ref:employee | | prefill: F05 part 1 |
| 1 | Ustoz-murabbiy majburiyatlari | static | | |
| 2 | FIXED 10 rows: Kun / Nima bo'lishi kerak (printed) / Kim tekshiradi (printed) / Natija / baho / Sana, imzo | see below | | |
| 2 | 1 (HR-4) | check «bajarildi» + sign | | |
| 2 | 1–7 (Ustoz), 8–30 (Ustoz), 31–60 (Ustoz kuzatadi), 61–85 (—) | free text + sign | | Natija cell is empty on paper |
| 2 | 7 (HR-4): 7-kun testi … o'tish ≥ 70% | pct + choice[o'tdi / qayta (14-kun)] | | pass computable from % ≥ 70 |
| 2 | 30 (Bo'lim rahbari) | choice[davom / o'qitish / to'xtatish] | | |
| 2 | 60 (Bo'lim rahbari): rahbar bahosi (1–5) | score1-5 (implied) | | cell is blank on paper |
| 2 | 85 (HR-4, bo'lim rahbari) | pct | | links to F07 |
| 2 | 90 (HR-1): sinovdan o'tdi / o'tmadi | choice[o'tdi / o'tmadi] | | Q10 |
| 3 | Xodimning birinchi 30 kundagi savollari: Sana / Savol / muammo / Javob / yechim (ustoz yozadi) | REPEAT(3) date, text, text | | |
| 4 | Xodim / Ustoz / Bo'lim rahbari / HR-4 / Sana | sign ×4 | | |

Tests are run in Google Forms (R03 §3, R05 §3). The blank only records the %.

### HR-F07 — Sinov muddati yakuniy bahosi
| | |
|---|---|
| Purpose | Final probation assessment |
| Trigger | Day 85 of probation (R03 §3, F06 row 85) |
| Fills | §1 and §3: department head. §2: HR-4. §4: employee. §5: HR-1 |
| Signs (order) | Bo'lim rahbari + Ustoz → Xodim (agree/object) → HR-1 decision. Day 88 = HR-1 decision. Day 90 = HR-2 order (R03 §3) |
| Source state | No version/filler header line. The header names a single legal entity, which differs from «MBF Company» used by other blanks. Partly filled sample (not copied) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Xodim F.I.Sh. / Lavozim / bo'lim / Ishga chiqqan sana / Sinov tugash sanasi / Bo'lim rahbari / Ustoz | ref:employee, ref:position+dept, date, date, ref:employee ×2 | | prefill: employment + probation record + F06 |
| 1 | FIXED 7 criteria (Mezon / Baho / Izoh (misol bilan)): Lavozim pasportidagi vazifalarni bilish va bajarish · Ish sifati (brak, xatolar, qayta ishlash) · Ish hajmi va tezligi (me'yorga nisbatan) · Mehnat intizomi (kechikish, davomat, tartib-qoidalar) · Mehnat muhofazasi qoidalariga rioya · Jamoada ishlash, ustoz va rahbar bilan munosabat · Mustaqillik (nazoratsiz ishlay oladimi) | score1-5 + text per row | | 1 = «juda past», 5 = «a'lo» |
| 1 | O'rtacha ball | computed | | = mean of 7 scores. Bands: «≥ 3,5 — o'tdi; 3,0–3,4 — shartli …; < 3,0 — o'tmadi» (Q11) |
| 2 | Testlar: rows 7-kun, 85-kun × Sana / Natija (%) / O'tish ≥ 70% | date, pct, check | | prefill: F06 rows 7 and 85. Pass = % ≥ 70 |
| 3 | Rahbar xulosasi | choice[Sinov muddatidan o'tdi — doimiy ishga tavsiya qilinadi / Shartli — qo'shimcha o'qitish rejasi (nima, qachongacha): +text / Sinov muddatidan o'tmadi — sabab (aniq, misollar bilan): +text] | | |
| 3 | Bo'lim rahbari / Sana · Ustoz | sign ×2 | | |
| 4 | Baho bilan tanishdim. ☐ roziman ☐ e'tirozim bor: ______ · Xodim / Sana | choice + text, sign | | R03 §5: no decision before the employee has seen the result |
| 5 | HR-1 qarori | choice[Tasdiqlayman / Qo'shimcha suhbat / Yuristga (shartnomani bekor qilish tartibi)] + sign + date | | |
| 5 | Reestrga kiritildi (HR-X02) | check | | |

R05 §2: the 85-day pass needs «≥ 70% va rahbar bahosi ≥ 3,5».

### HR-F08 — Chiqish suhbati varag'i (exit interview)
| | |
|---|---|
| Purpose | Record the leaving reason (K-code) and the exit interview |
| Trigger | Resignation or dismissal (R01 2.4 step 3). A leaver during probation gets it «shu kuni» (R03 §4) |
| Fills | §1–2: HR-4. §3: HR-3 |
| Signs | HR-4, Xodim («suhbat o'tkazildi») |
| Deadlines | By the last working day (R01 2.4). HR-3 checks the register code on the 5th working day (§4) |
| Source state | No version header. Partly filled sample (not copied) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Xodim F.I.Sh. / Lavozim / bo'lim / yo'nalish / Ishga kirgan / Ketish sanasi | ref:employee, refs, date, date | | prefill: employment (hire date, termination date) |
| hdr | Staj (kun) | computed | | = Ketish sanasi − Ishga kirgan. R08 §3: «Formula — qo'lda yozilmaydi» |
| hdr | Ketish tashabbusi | choice[xodim / korxona] | | |
| 1 | FIXED 12 codes K01–K12 × «Asosiy» / «Qo'shimcha» | Asosiy: single choice. Qo'shimcha: multi, max 2 | R: reason code mandatory (HR-04 §2, KPI «100%») | «asosiy — bitta, qo'shimcha — ko'pi bilan ikkita». K06 has +text «qayer, nima yaxshi». K09 «tafsilot yozilmaydi». Codes: K01 Ish haqi kam / kechikadi · K02 Ish sharoiti og'ir … · K03 Ish rejimi … · K04 Usta / rahbar bilan munosabat · K05 Ish yoqmadi / kutganim boshqa edi · K06 Boshqa ish topdi · K07 Chet elga ketdi · K08 Oilaviy sabab / yashash joyi uzoq · K09 Sog'liq · K10 Korxona tashabbusi — intizom buzilishi · K11 Korxona tashabbusi — malaka yetarli emas / sinovdan o'tmadi · K12 Shtat qisqarishi / ish hajmi kamayishi |
| 2 | (only «xodim ketishi tashabbusi bo'lganda») Q1 Qolishingiz uchun nima o'zgarishi kerak edi? | longtext | | conditional on initiative = xodim |
| 2 | Q2 Ish haqi bozorga nisbatan qanday (kam / teng / yuqori)? Qayerda qancha taklif qilishdi? | choice[kam/teng/yuqori] (implied) + text | | options are inside the question text, with no ☐ |
| 2 | Q3 Rahbaringiz/ustangiz bilan ishlash qanday bo'ldi (1–5)? | score1-5 | | |
| 2 | Q4 Birinchi oyda sizga yetarli o'rgatishdimi? · Q5 Ish sharoitida eng katta muammo nima? | text | | |
| 2 | Q6 Korxonaga qaytishga tayyormisiz (sharoit o'zgarsa)? | yesno → «ha bo'lsa zaxira bazaga» | | link: candidate reserve pool (R02 §4) |
| 2 | Q7 Tanishlaringizga bu yerda ishlashni tavsiya qilasizmi? | yesno + «nega» text | | |
| 3 | Topshirish-qabul qilish akti (HR-F09) | choice[tugallandi / jarayonda / ochilmagan — sabab: +text] | | prefill: status of the linked F09 |
| 3 | Moddiy javobgarlik (inventarizatsiya) | choice[yopildi / talab qilinmaydi] | | prefill: F09 §5 |
| 3 | Kirish huquqlari, kalitlar, texnika qaytarildi | check | | prefill: F09 §3 |
| 4 | HR-4 / Xodim (suhbat o'tkazildi) / Sana | sign ×2 | | |
| 4 | Reestrga kod kiritildi (HR-3 tekshiradi, 5-ish kuni) | check | | |

### HR-F09 — Ishlarni topshirish-qabul qilish akti (obxodnoy o'rniga)
| | |
|---|---|
| Purpose | Handover of work, files, access and contacts. Clearance for the final settlement |
| Trigger | «bo'shatish», «ichki o'tkazish», «uzoq ta'til/kasallik (30+ kun)» (header). R06 §4 step 1 |
| Fills | HR-3 opens it. Topshiruvchi + Qabul qiluvchi fill §1–5 (R06 §4 step 2). Department head checks. HR-3 controls |
| Signs (order) | Topshirdim → Qabul qildim → Bo'lim rahbari → HR-3 (nazorat) → HR-1. Then HR-3 gives HR-2 the mark «akt tugallangan — bo'shatish buyrug'iga ruxsat» |
| Deadlines | Opened «ariza kuni». Completed «oxirgi kundan 3 ish kuni oldin» (R06 §4, R01 2.4). Leave >30 days: «ta'tildan 5 kun oldin» (R01 2.3) |
| Hard rule | No dismissal order and no final settlement without the mark (R01 2.4 step 5, R06 §4 step 6). If the employee refuses, a dalolatnoma is drawn up with 2 witnesses (F13 part 3) (R06 §4) |
| Source state | No version header. Partly filled sample. §5 has hand-added rows with named staff (not copied) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Akt № / Ochilgan sana / Tugallanishi kerak | docno, date, date | | «Tugallanishi kerak» = last day − 3 working days (R06) |
| hdr | Turi | choice[bo'shatish / ichki o'tkazish / uzoq ta'til/kasallik (30+ kun)] | | |
| hdr | Topshiruvchi F.I.Sh. / Lavozim / bo'lim | ref:employee + refs | | prefill |
| hdr | Qabul qiluvchi F.I.Sh. / Lavozim (zaxira xodim yoki yangi xodim yoki rahbar) | ref:employee + text | | prefill: reserve employee from F14 (F14 footer) |
| 1 | Tugallanmagan ishlar va majburiyatlar: Ish / topshiriq / shartnoma · Holati (% yoki bosqich) · Keyingi qadam va muddat · Kimga o'tadi | REPEAT(4) text, text, text+date, ref:employee | | |
| 2 | Fayllar, hujjatlar, jurnallar: Nomi (fayl, papka, jurnal, reestr) · Qayerda (yo'l / shkaf) · Oxirgi yangilangan · Topshirildi · Qabul qiluvchi imzosi | REPEAT(5) text, text, date, check, sign | | prefill: F10 §3 for this position («lavozim pasportining 3-bo'limi bo'yicha») |
| 3 | Kirish huquqlari, kalitlar, texnika: Nomi (tizim login, Telegram guruh admin, kalit, muhr, telefon, noutbuk, transport) · Qaytarildi / o'tkazildi · Kim qabul qildi · Sana | REPEAT(4) text, check, ref:employee, date | | prefill: F10 §4 |
| 4 | Tashqi va ichki kontaktlar: Tashkilot / shaxs · Masala · Telefon / kanal · Qabul qiluvchi tanishtirildi | REPEAT(3) text, text, text, check | | prefill: F10 §4 contacts |
| 5 | Inventarizatsiya o'tkazildi | choice[ha — dalolatnoma № + sana / talab qilinmaydi] | | |
| 5 | Kamomad | choice[yo'q / bor: +text] + (F.I.SH) «buxgalteriya bilan hal qilinadi» | | |
| 5 | Transport va DAN bo'yicha qarzdorlik haqida ma'lumot | choice[yo'q / bor: +text] + responsible (F.I.SH) | | hand-added row, named person |
| 5 | Mol-mulk xisobchisi moddiy javovgaliklarni topshirganligi | choice[yo'q / bor: +text] + responsible | | hand-added |
| 5 | Oylik xisobchisidan umumiy qarzdorligi to'g'risida ma'lumot | choice[yo'q / bor: +text] + responsible | | hand-added. HR-00 also says the payroll accountant puts a «F09 (belgi)» |
| 6 | Lavozim pasporti yangilandi | choice[o'zgarish yo'q / o'zgarishlar HR-8/HR-1 ga berildi (sana: +date)] | | |
| 7 | Topshirdim / Qabul qildim / Bo'lim rahbari / HR-3 (nazorat) / HR-1 / Sana | sign ×5 | | |
| 7 | HR-2 uchun belgi: akt tugallangan — bo'shatish buyrug'iga ruxsat ☐ (HR-3 imzosi) | check + sign | | gate for dismissal order |

### HR-F10 — Lavozim pasporti — shablon (6 bo'lim)
| | |
|---|---|
| Purpose | Documents a position (not a person): instruction, processes, files, access, calendar, reserves |
| Trigger | Position without a passport (needed before F01). Any change («har o'zgarishda»). At least once a year |
| Fills | Bo'lim rahbari + lavozim egasi, helped by HR-8 (until then HR-1). The holder logs 5 days of work (R06 §2 step 2) |
| Approves (order) | HR-8/HR-1 checks the standard (3 working days) → HR-1 approves (Bosh direktor for the HR department) (3 working days) → HR-2 gets acknowledgement via F11 (3 working days) (R06 §2) |
| Deadlines | 2 weeks per position. Update within 5 working days of a change (R06 §2) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Lavozim kodi | text | | format «yo'nalish-bo'lim-№» (R06 §2 step 1) |
| hdr | Lavozim nomi / Yo'nalish / bo'lim | ref:position, ref:direction+dept | | prefill |
| hdr | Bo'ysunadi / Bo'ysunuvchilar | ref:position (1) / ref:position (many) | | |
| hdr | Zaxira xodim (lavozimi) | ref:employee/position | | prefill: F14 |
| hdr | Pasport versiyasi / sana · Keyingi ko'rib chiqish | text+date, date | | next review ≤ 1 year |
| 1 | Lavozim yo'riqnomasi … «Bu yerga ilova qilinadi yoki havola» | file or URL | | HR-LY-00 standard (not provided) |
| 2 | Jarayon reglamentlari: Jarayon nomi · Qadamlar (1, 2, 3…) — nima, qaysi hujjat/dastur bilan · Natija · Davriyligi · Reglament kodi / fayl | REPEAT(5) text, longtext, text, text, text/file | | rows are referenced by № from F10 §6 and F14 §1 |
| 3 | Ishchi fayllar va instrumentlar registri: Nomi (fayl, jadval, jurnal, dastur, shablon) · Qayerda (papka yo'li / shkaf / tizim) · Nima uchun kerak · Kim yangilaydi · Zaxira nusxa | REPEAT(5) text ×5 | | source for F09 §2 |
| 4a | Tizim / kalit / jihoz / guruh · Kirish darajasi · Kim beradi / kim qaytarib oladi | REPEAT(3) | | source for F09 §3 |
| 4b | Tashqi / ichki kontakt (tashkilot, lavozim) · Qaysi masala bo'yicha · Aloqa kanali | REPEAT(3) | | source for F09 §4 |
| 5 | Kalendar: FIXED periods Kunlik / Haftalik / Oylik / Choraklik / Yillik × Ish · Muddat (kun / soat) · Kimga natija | text ×3 per period | | one row per period on paper |
| 6 | Zaxira xodim va uni o'qitish holati: rows 1-zaxira, 2-zaxira × Zaxira xodim (lavozimi) · O'qitilgan jarayonlar (2-bo'limdan №) · O'qitish sanasi · «Zaxira kuni» sinovi (sana, natija) · Keyingi sinov | ref:employee, multi ref:§2 rows, date, date+text, date | | prefill: F14. «2-zaxira» for key positions (R06 §3) |
| ver | Tasdiq va versiyalar: Versiya · Sana · O'zgarish · Tuzdi · Tasdiqladi | REPEAT (row 1.0 «Dastlabki» printed) | | version history |
| ftr | Lavozim egasi tanishdi / Sana (HR-F11) | sign | | |

### HR-F11 — Hujjat bilan tanishuv varag'i
| | |
|---|---|
| Purpose | Employee acknowledges each document and each new version |
| Trigger | Hire, new or updated passport, regulation, internal rules, order, new version (header, R01, R06) |
| Fills / keeps | HR-2. Filed in the personal file |
| Signs | Employee per row. «Tanishtirdi» per row |
| Rule | Test on that document 7 days after acknowledgement (HR-T00). Refusal → dalolatnoma with 2 witnesses (F13 part 3) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Xodim F.I.Sh. / Lavozim / bo'lim | ref:employee | | prefill |
| tbl | Hujjat nomi va kodi · Versiya · Tanishuv sanasi · Xodim imzosi («o'qidim, tushundim, bajarishga majburman») · Tanishtirdi (lavozim, imzo) · Test sanasi / natija | REPEAT(10) text/ref, text, date, sign, sign, date+pct | | Rows 1–7 have printed titles: Lavozim yo'riqnomasi / pasporti · Ichki mehnat tartibi qoidalari · Jarayon reglamenti: (×2) · Mehnat muhofazasi yo'riqnomasi (jurnal № ____) — «Tanishtirdi» printed «Mehnat muhofazasi xizmati» · Maxfiylik / shaxsga doir ma'lumotlar qoidasi · Buyruq № ____. Rows 8–10 are blank. Test date is computable as acknowledgement + 7 days |

### HR-F12 — Rag'batlantirish (bonus) / jarima taqdimnomasi
| | |
|---|---|
| Purpose | Monthly department submission of bonuses and bonus reductions/penalties, each with a basis document |
| Trigger | Month end (R07 §3 «Oy oxiri»). Discipline outcome (F13 part 4 «mukofotni kamaytirish (HR-F12 orqali)»). Mentor bonus after the hire passes F07 |
| Fills | Bo'lim rahbari |
| Signs (order) | Bo'lim rahbari → HR-3 («qabul qildi, asoslar bor», working days 1–2) → HR-1 per-row approval + signature (by working day 3) → Ish haqi hisobchisi (qabul qildi). HR-3 may return it («Qaytarildi (asos yo'q)») |
| Rule | «asos hujjatsiz taqdimnoma qabul qilinmaydi». The penalty is in practice a bonus reduction under internal rules. «[?]» legal limit pending lawyer (header, R07) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Taqdimnoma № / Oy / Bo'lim / yo'nalish | docno, month, ref:dept | | prefill: submitter's department |
| 1 | Rag'batlantirish: Xodim F.I.Sh. · Lavozim · Asos (nima uchun — aniq natija, raqam) · Asos hujjat (hisobot, akt, KPI jadvali) · Summa yoki % · HR-1 tasdig'i | REPEAT(3) ref:employee, prefill position, text, text/file, money-or-pct, check (HR-1) | Asos hujjat R | Bonus types in R07 §5: KPI, sifat, ustozlik, zaxira, tavsiya, intizom |
| 1 | Row 4 FIXED: «Ustozlik (sinovdan o'tgan xodim: ______)» · Asos hujjat «HR-F07 №» | ref:employee + ref:F07 | | |
| 2 | Mukofotni kamaytirish / jarima: Xodim F.I.Sh. · Lavozim · Buzilish (sana, nima) · Asos hujjat (tushuntirish xati / akt HR-F13 №, davomat reestri) · Chora (mukofot % / summa) · HR-1 tasdig'i | REPEAT(3) ref:employee, prefill, date+text, ref:F13 / text, money-or-pct, check | Asos hujjat R | prefill: F13 decision. Attendance data is in the system |
| 3 | Bo'lim rahbari / HR-3 (qabul qildi, asoslar bor) / HR-1 (tasdiqladi) / Ish haqi hisobchisi (qabul qildi) — each with Sana | sign ×4 | | |
| 3 | Qaytarildi (asos yo'q): № ____ — sabab: ____ HR-3 | int (row №) + text + sign | | |

### HR-F13 — Mehnat intizomi: tushuntirish xati talabi, dalolatnoma va intizomiy chora loyihasi
| | |
|---|---|
| Purpose | Discipline case: record → demand explanation → act if no reply → proposed measure → decision → order |
| Trigger | A violation on the day it happens. Also an F11 or F09 refusal (part 3 only) |
| Fills | Part 1: Bo'lim rahbari or HR-3 (HR-00: department heads fill «F13 (1-qism)»). Part 2: HR-3 hands it over, the employee writes. Part 3: HR-3 with witnesses. Part 4: HR-3 |
| Decides (order) | HR-3 (draft) → HR-1 decision → Bosh direktor order prepared by HR-2 → employee acknowledges the order |
| Deadlines | Explanation 2 working days. Act on working day 3. Draft 2 working days. HR-1 2 working days. Order «qonun muddati ichida» (R07 §4). «[?]» lawyer to confirm |
| Escalation | 3 days of unexcused absence in a row: HR-3 tells HR-1 the same day (R07 §4) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| 1 | Xodim F.I.Sh. / Lavozim / bo'lim | ref:employee | | prefill |
| 1 | Buzilish sanasi, vaqti | datetime | | prefill: attendance event if from the attendance system |
| 1 | Qayd etdi (lavozim) | ref:employee/position | | prefill: current user |
| 1 | Buzilish turi | choice/multi[kechikish (+int «daqiqa») / sababsiz kelmaslik / ish vaqtida ish joyida yo'q / mehnat muhofazasi qoidasi / topshiriq bajarilmadi / mast holat / boshqa: +text] | | single vs multi not stated. Lateness minutes can be prefilled from attendance |
| 1 | Nima bo'ldi (aniq, guvohlar) | longtext | | |
| 1 | Dalil (davomat tizimi, foto, hujjat) | file(s) + text | | the only explicit photo or document evidence field in the package |
| 2 | Hurmatli ____! «__» ____ y. sodir bo'lgan ____ yuzasidan … | static, filled from part 1 | | |
| 2 | Talabni topshirdi / Sana · Xodim oldim / Sana | sign ×2 | | if the employee refuses → part 3 |
| 2 | Xodim tushuntirishi (o'z qo'li bilan yoki ilova) · Imzo · Sana | longtext or file + sign | | |
| 3 | «__» ____ y. xodim ____ ☐ tushuntirish xatini 2 ish kuni ichida bermaganini ☐ hujjat (____) bilan tanishishdan / olishdan bosh tortganini | date, ref:employee, choice + text | | |
| 3 | 1. / 2. / 3. ________ (lavozim, imzo) | sign ×3 (witnesses) | | Q14: text elsewhere says 2 witnesses |
| 4 | Oldingi 12 oyda buzilishlar (reestr bo'yicha) | computed int/list | | = this employee's F13 cases in the last 12 months |
| 4 | Xodim tushuntirishida keltirilgan sabab uzrlimi | yesno | | |
| 4 | Zarar (bo'lsa) | text/money | | |
| 4 | Taklif qilinadigan chora | choice[og'zaki ogohlantirish (buyruqsiz) / hayfsan / mukofotni kamaytirish (HR-F12 orqali) / mehnat shartnomasini bekor qilish (faqat yurist xulosasi bilan)] | | «mukofotni kamaytirish» → F12 §2 |
| 4 | HR-3 / Sana | sign | | |
| 4 | HR-1 qarori | choice[tasdiqlayman / chora o'zgartirildi: +text / chora qo'llanmaydi — sabab: +text] + sign + date | | |
| 4 | Buyruq № / sana (HR-2) · Xodim buyruq bilan tanishdi / Sana | order + sign | | |

### HR-F14 — Zaxira xodim tayinlash, o'qitish va «zaxira kuni» sinovi varag'i
| | |
|---|---|
| Purpose | Assign and train a backup ("continues the work in absence", not promotion) and log the backup's test days and stand-in periods |
| Trigger | Every position needs ≥ 1 trained backup (key positions need 2) (header, R06 §3) |
| Fills | Bo'lim rahbari (HR-1 for the HR department). The primary employee and the backup sign each training row |
| Approves | Assignment: rahbar proposes → HR-1 approves → HR-2 issues the order (R06 §3). HR-1 checks quarterly |
| Deadlines | Training 4–8 weeks (R04 §2, R06 §3). «Zaxira kuni» ≥ once a year |
| Rule | The primary and the backup are never on leave at the same time (HR-2 builds the leave schedule from this sheet) |
| Kept | As an annex to F10 §6 |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Asosiy lavozim (kodi, nomi) | ref:position | | prefill: F10 |
| hdr | Asosiy xodim F.I.Sh. / Zaxira xodim F.I.Sh. / Zaxira xodim lavozimi | ref:employee ×2, prefill position | | |
| hdr | Tayinlash buyrug'i № / sana | order | | |
| hdr | Zaxira uchun qo'shimcha haq (bo'lsa, HR-R07 bo'yicha) | money | | |
| 1 | O'qitish rejasi: Jarayon (pasportdan) · O'qitish usuli (ko'rsatish / birga bajarish / mustaqil bajarish) · Reja sana · Bajarildi (sana) · Asosiy xodim imzosi · Zaxira imzosi | REPEAT(5) ref:F10 §2 row, choice, date, date, sign, sign | | prefill rows from F10 §2 |
| 1 | Row 6 FIXED: «Fayllar va kirish huquqlari bilan tanishuv (pasport 3–4-bo'lim)» | same columns | | |
| 1 | O'qitish yakunlandi ☐ (barcha jarayonlar mustaqil bajarildi) Sana · Bo'lim rahbari | check + date + sign | | |
| 2 | «Zaxira kuni» sinovi: Sinov sanasi · Bajarilgan ishlar (jarayon №) · Xatolar / muammolar · Baho (1–5) · Xulosa ☐ tayyor ☐ qo'shimcha o'qitish · Tekshirdi (rahbar) | REPEAT(2) date, multi ref:F10 §2, text, score1-5, choice, sign | | the primary employee does not take part |
| 3 | Zaxira ishlagan davrlar: Davr · Sabab · Bajarilgan ishlar · Muammolar · Rahbar bahosi | REPEAT(2) date-range, text, text, text, text/score | | prefill: absences of the primary employee (leave/sick) |

### HR-F15 — Oylik HR hisoboti — Bosh direktorga (1 bet)
| | |
|---|---|
| Purpose | One-page monthly KPI report |
| Trigger | Monthly |
| Fills | HR-3 enters the numbers on working day 4. HR-1 writes the conclusion on working day 5 (header, R08 §4) |
| Signs | Tayyorladi HR-1. «Bosh direktor tanishdi» + «Ko'rsatmalar» |
| Source | «Dashboard'dan avtomatik» (HR-X01 Dashboard, HR-X02, HR-X03) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Hisobot oyi / Tayyorladi: HR-1 / Sana | month, sign, date | | |
| 1 | Matrix: rows Jami xodimlar · Metan zavodi · Propan zavodi · Velosiped/skuter zavodi · Omborlar · Savdo (ofis + viloyat) · Boshqa yo'nalishlar × cols Oy boshida · Kirgan · Ketgan · Oy oxirida · O'tgan oy · Yil boshidan | FIXED int matrix | | all computable from hires/terminations by direction |
| 1 | Tekshiruv: oy boshi + kirgan − ketgan = oy oxiri ☐ mos ☐ farq: ____ (sabab: ____) | computed + text | | formula stated |
| 2 | Ketish tahlili, cols Bu oy · Yil boshidan · Maqsad. Rows: Aylanma (yillik hisobda, %) [2027: ≤ 50%] · ≤ 90 kun ishlab ketganlar (soni / %) [≤ 25%] · Eng ko'p ketish — yo'nalish (soni) · Eng ko'p sabab kodi (K01–K12) · Chiqish suhbati o'tkazilganlik (%) [100%] · Topshirish akti bilan ketganlar (%) [100%] | computed | | sources: F08, F09, termination data. Annualised-turnover formula not stated |
| 3 | Ishga qabul va moslashtirish, cols Bu oy · Maqsad. Rows: Ochiq bo'sh o'rinlar (oy oxirida) / shundan 30 kundan ortiq · Yopilgan so'rovlar / o'rtacha yopish muddati (kun) [14/30/60] · 7-kun testi o'tkazilganlik / o'rtacha natija [100% / ≥ 70%] · Sinov muddati yakunlanganlar: o'tdi / o'tmadi [o'tganlar ≥ 85%] | computed | | sources: F01, F06, F07 |
| 4 | Intizom, tabel, hujjatlar, col Bu oy. Rows: Tabel ish haqi hisobchisiga topshirilgan sana (me'yor: 1-ish kuni) · Intizomiy holatlar (reestr): kechikish / kelmaslik / boshqa · Qo'llangan choralar (soni, turi) · Reestrlar SAP / mehnat.uz bilan farqi · Lavozim pasportli lavozimlar ulushi (%) · Zaxirasi o'qitilgan kalit lavozimlar (%) | date / computed / text | | sources: F18, F13, F10, F14. SAP/mehnat.uz difference is external |
| 5 | HR-1 xulosasi …: Masala · Taklif · Kimdan qaror kerak · Muddat | REPEAT max 3 | | «ko'pi bilan 3 ta» |
| ftr | Bosh direktor tanishdi / Sana / Ko'rsatmalar | sign + longtext | | |

### HR-F16 — HR jarayonlari yillik auditi dalolatnomasi
| | |
|---|---|
| Purpose | Annual audit of the 8 HR processes and KPIs |
| Trigger | Every December (header, R08 §4) |
| Fills | HR-1 (auditor) + an independent participant (yurist / bosh buxgalter / tashqi) |
| Signs | HR-1 → Mustaqil ishtirokchi → Bosh direktor tasdiqladi |
| Rule | Process rated «qoniqarsiz» → the owner submits a fix plan within 30 days |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Audit yili / O'tkazilgan sana / Auditor: HR-1 / Mustaqil ishtirokchi (yurist / bosh buxgalter / tashqi) | year, date, sign, ref:user + choice | | |
| 1 | FIXED 8 rows F1…F8 (Jarayon · Reglament · Egasi printed) × Checklist ballari (HR-A03) «___ / ___» · Bajarilish % · KPI (reja / fakt) · Baho ☐ yaxshi ☐ qoniqarli ☐ qoniqarsiz · Asosiy kamchilik | int/int, computed pct, text, choice (computable), text | | Scale: «yaxshi ≥ 90% checklist; qoniqarli 70–89%; qoniqarsiz < 70%». Checklist HR-A03 (132 points) not provided |
| 2 | Yillik KPI (HR-01 Nizom, 7-bo'lim): FIXED 8 rows (Kadrlar aylanmasi · Lavozim pasportli lavozimlar ulushi · Bo'sh o'rin yopish muddati · Sinovdan o'tganlar ulushi · 7-kun testi o'tganlar · Ketishda topshirish akti · Zaxirasi o'qitilgan kalit lavozimlar · Mehnat inspeksiyasi / sud yo'qotishi [Maqsad printed 0]) × Maqsad · Fakt · Bajarildi | text/num ×2 + check | | Fakt is computable from the system for most rows |
| 3 | Uzluksizlik testi: «Yil davomida o'tkazilgan «zaxira kuni» sinovlari (soni / muvaffaqiyatli)» · «Ketgan kalit xodimlar (soni) / ish to'xtagan holatlar» · «Bilim bazasida yo'q hujjatlar (aniqlangan)» | text/int | | first row computable from F14 §2 |
| 4 | Xulosa va keyingi yil rejasi: Kamchilik · Chora · Egasi · Muddat | REPEAT(3) | | |
| 4 | Reglamentlarga o'zgartirish kiritish kerak: ☐ yo'q ☐ ha — qaysi: ____ | choice + text | | → F11 for the new version |
| ftr | HR-1 / Mustaqil ishtirokchi / Bosh direktor tasdiqladi / Sana | sign ×3 | | |

### HR-F17 — Top-menejer mehnat shartnomasiga ilova (KPI, javobgarlik, maxfiylik, topshirish)
| | |
|---|---|
| Purpose | Contract annex for senior managers |
| Applies to | «bo'lim/zavod rahbarlari, boshqarma boshliqlari, bosh buxgalter, HR-1 va shunga teng lavozimlar» |
| Fills | HR-1 prepares. The lawyer review is mandatory |
| Signs | Bosh direktor + Xodim. Yurist ekspertizasi. HR-1 |
| Status | «Versiya 1.0 (loyiha)» — draft until the lawyer approves (HR-00 legal note) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Mehnat shartnomasi № ____ dan ____ ga Ilova № ___ | text (contract №), date, int | | |
| hdr | Xodim / Lavozim | ref:employee, ref:position | | prefill |
| 1 | 1.1. Lavozim maqsadi (lavozim pasportidan) · 1.2. Mustaqil qaror qabul qiladi · 1.3. Bosh direktor bilan kelishadi · 1.4. Imzo huquqi / moliyaviy chegara (summa) | longtext ×3, money | | 1.1 prefill: F10 / instruction |
| 2 | KPI · O'lchov birligi · Maqsad (chorak) · Maqsad (yil) · Ma'lumot manbasi · Vazn (%) | REPEAT(4) + FIXED row 5 «Bo'limda lavozim pasportlari va zaxira xodimlar to'liqligi» (%, yil 100%, «HR-1 reestri») | | |
| 2 | Jami — Vazn 100% | computed, Σ weights must be 100 | | |
| 2 | Mukofot rule | static | | «KPI bajarilishi ≥ 90% — to'liq; 70–89% — proporsional; < 70% — mukofot yo'q». Paid within 15 days after the quarter (R07). How «proporsional» is computed is not stated |
| 3 | 3.1 «… shartnoma imzolangandan ___ oy ichida» | int (months) | | rest of §3 static |
| 4 | 4.2 «… ishdan keyin ___ yil davomida …» | int (years) | | rest static |
| 5 | 5.1 «topshirish davri — kamida ___ ish kuni» · 5.3 «… ishga kirishdan ___ oy ichida» | int ×2 | | links F09, F14 |
| 6 | Moddiy javobgarlik | static | | |
| 7 | Bosh direktor / Xodim / Sana · Yurist ekspertizasi / Sana · HR-1 | sign ×4 | | |

### HR-F18 — Oylik tabel va reestrlarni ish haqi hisobchisiga topshirish varag'i
| | |
|---|---|
| Purpose | Signed handover of timesheet, orders and bonus/penalty register to payroll, with deadline tracking |
| Trigger | Monthly. Orders on the order day |
| Fills | HR-3 (tabel on working day 1, penalties/bonuses on working day 3). HR-2 (orders). HR-4 (mentor bonus list) |
| Receives / controls | Ish haqi hisobchisi receives. HR-1 and Bosh buxgalter control |
| Signs (order) | Topshirdi HR-3 → Qabul qildi ish haqi hisobchisi → Bosh buxgalter (hisob tekshirildi) |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | Hisobot oyi | month | | |
| 1 | FIXED 10 rows: Metan zavodi · Propan zavodi · Velosiped/skuter zavodi · Omborlar · Savdo — ofis · Savdo — viloyat · Buxgalteriya / kassa / SAP · Boshqaruv, marketing, telefon, import, deklarant · Xavfsizlik · Choyxona, inkassatsiya, qurilish, transport — × Xodimlar soni (reestr 91 bo'yicha) · Tabelda xodimlar soni · Farq (0 bo'lishi kerak) · Bo'lim rahbari / tabelchi imzosi · Davomat tizimi bilan solishtirildi · HR-3 tekshirdi (sana) | prefill int, int, computed, sign, check, date | | «reestr 91» headcount = active employees per direction from the system. Farq = reestr − tabel |
| 1 | Jami | computed sums | | |
| 2 | FIXED 4 rows (Hujjat · Soni · Topshirdi (lavozim) [printed] · Sana): Oy davomidagi buyruqlar (qabul, o'tkazish, ta'til, bo'shatish, kasallik varaqalari) [HR-2] · Jarima/bonus taqdimnomalari (HR-F12), HR-1 tasdiqlagan [HR-3] · Ketganlar bo'yicha akt belgisi (HR-F09) — yakuniy hisob-kitobga ruxsat [HR-3] · Ustozlik uchun rag'batlantirish ro'yxati [HR-4] | int (computable), date | | counts computable from F12, F09, F07 and orders |
| 3 | FIXED 4 rows (Qadam · Me'yor [printed] · Fakt (sana) · Kechikish (kun) · Sabab): Tabel topshirildi [1-ish kuni] · Jarima/bonus reestri topshirildi [3-ish kuni] · Hisob-kitob varaqalari tayyor [5-ish kuni] · SAP ga kiritildi [7-ish kuni [?]] | date, computed int, text | | Kechikish = Fakt − Me'yor in working days (needs a working-day calendar) |
| 4 | Topshirdi HR-3 / Sana · Qabul qildi ish haqi hisobchisi / Sana · Bosh buxgalter (hisob tekshirildi) / Sana | sign ×3 | | |

### HR-F19 — Ichki o'tkazish / lavozim o'zgartirish / qo'shimcha vazifa yuklash so'rovi
| | |
|---|---|
| Purpose | Request a transfer, position change, extra duty, staffing change or new position |
| Fills | Bo'lim rahbari (the receiving side) |
| Approves (order, §4) | Xodim (consent) → Hozirgi bo'lim rahbari → Qabul qiluvchi bo'lim rahbari → HR-4 (logs in HR-X02) → Bosh buxgalter (shtat va fond) → HR-1 (2 working days) → Bosh direktor (new position or staffing change only). Then the HR-2 order within 3 working days of approval (R01 2.2) |
| Legal | Transfer only with the employee's written consent. Extra duty only with extra pay and a written agreement. «[?]» lawyer |

| § | Label (verbatim) | Type | Req | Prefill / note |
|---|---|---|---|---|
| hdr | So'rov № / Sana | docno, date | | |
| 1 | So'rov turi | choice[Boshqa lavozimga o'tkazish (doimiy) / Vaqtincha o'tkazish (+date-range) / Qo'shimcha vazifa yuklash / Lavozim nomini / shtat birligini o'zgartirish / Yangi lavozim ochish] | | single vs multi not stated |
| 2 | Two columns «Hozirgi» / «Yangi» for: Xodim F.I.Sh. · Yo'nalish / bo'lim · Lavozim (shtat jadvali bo'yicha) · Ish haqi · Bevosita rahbar | Hozirgi = prefill from employment. Yangi = ref:dept/position/money/employee | | «Xodim F.I.Sh.» under «Yangi» has no meaning for a transfer (Q19) |
| 2 | Lavozim pasporti mavjud (kod) — Yangi: ☐ ha: ____ ☐ yo'q — HR-F10 bo'yicha tuziladi | choice + text | | prefill: does an F10 exist |
| 3 | Nima uchun (aniq sabab: ish hajmi, xodim ketdi, malaka oshdi, tuzilma o'zgardi) | longtext | | |
| 3 | Hozirgi lavozimda ishni kim davom ettiradi (zaxira / yangi xodim / vazifa yopiladi) | text (options in label, no ☐) | | prefill: F14 backup |
| 3 | Hozirgi ishlar topshiriladi (HR-F09) | choice[ha, akt № (ref:F09) / talab qilinmaydi] | | |
| 3 | Shtat jadvaliga ta'siri (birlik qo'shiladi / kamayadi / o'zgarmaydi) | text (options in label, no ☐) | | |
| 3 | Yangi lavozim uchun sinov / moslashtirish kerakmi | choice[ha — HR-F06 / yo'q] | | |
| 4 | Kim · Qaror · Imzo · Sana. Rows: Xodim (roziman / rozi emasman) ☐☐ · Hozirgi bo'lim rahbari (qo'yib beradi) ☐ rozi ☐ e'tiroz: · Qabul qiluvchi bo'lim rahbari · HR-4 (jurnalga kiritildi, HR-X02) · Bosh buxgalter (shtat va fond) · HR-1 ☐ tasdiqlayman ☐ rad — sabab: · Bosh direktor (yangi lavozim / shtat o'zgarishi) | choice(+text) + sign + date per row | Xodim consent R (legal note) | |
| ftr | Buyruq № / sana (HR-2) · Yangi lavozim pasporti bilan tanishuv (HR-F11) ☐ · Reestr 91 yangilandi ☐ | order, check ×2 | | links F11 |

---

## 3. Summary matrix

Links: E = employee, C = candidate, V = vacancy / vacancy request, P = position (passport), D = department / direction.

| Blank | Fills | Approves / signs (order) | Sign | Repeating table | Attach. | Links |
|---|---|---|---|---|---|---|
| F01 | Bo'lim rahbari; HR-4 (№, §4) | HR-4 → HR-1 → Bosh buxgalter (new position) | Y | N (fixed rows) | – | V, P, D, E (leaver) |
| F02* | Nomzod; HR-4 (§6) | HR-4 | Y (candidate) | Y (education, jobs) | – | C, V |
| F03 | Nomzod (I); HR-4 (II–IV) | HR-4 → HR-1 (material-responsible / manager roles) | Y (candidate) | Y (referees) | – | C |
| F04 | HR-4 / HR-1 (kotib) | members → Rais + Kotib | Y (multi) | Y (members, candidates, conditions) | – | C, V, F01, F02, F03 |
| F05 | HR-4 (1); Nomzod + HR-2 (2); HR-2, HR-4, MM xizmati (3) | HR-4 → Nomzod; HR-2 | Y (candidate) | N (fixed 12 docs) | physical docs; upload not stated | C, F04, F06, F11 |
| F06 | HR-4 (HR-5) + rahbar; ustoz | Xodim, Ustoz, Rahbar, HR-4; HR-1 (day 90) | Y | Y (Q&A log) + fixed checkpoints | – | E, mentor, F07, F11 |
| F07 | Rahbar; HR-4 (tests); Xodim | Rahbar + Ustoz → Xodim → HR-1 | Y | N | – | E, F06 |
| F08 | HR-4; HR-3 (§3) | HR-4, Xodim | Y | N (fixed K-codes) | – | E, F09, reserve pool |
| F09 | HR-3 opens; topshiruvchi + qabul qiluvchi | Topshiruvchi → Qabul qiluvchi → Rahbar → HR-3 → HR-1 → mark to HR-2 | Y | Y (4 tables) | dalolatnoma № (ref only) | E ×2, P (F10 §3–4), F14 |
| F10 | Rahbar + lavozim egasi (HR-8/HR-1 help) | HR-8/HR-1 check → HR-1 (HR dept: Bosh direktor) → holder acknowledges (F11) | Y | Y (processes, files, access, contacts, versions) | §1 instruction file or link | P, D, F14 |
| F11 | HR-2 | Xodim + Tanishtirdi per row | Y (per row) | Y (documents) | – | E, document/version |
| F12 | Bo'lim rahbari | HR-3 → HR-1 (per row) → Ish haqi hisobchisi | Y | Y (bonus, penalty) | basis document (file or ref) | E, D, F07, F13 |
| F13 | Rahbar or HR-3; Xodim; HR-3 | HR-3 → HR-1 → order (HR-2) → Xodim | Y (incl. witnesses) | N (witness lines ×3) | Dalil (foto, hujjat); explanation «ilova» | E, F12, attendance |
| F14 | Rahbar (HR dept: HR-1); asosiy + zaxira | HR-1 approves assignment; rahbar signs results | Y | Y (training, test days, stand-in periods) | – | E ×2, P, F10 |
| F15 | HR-3 (numbers) → HR-1 | Bosh direktor «tanishdi» | Y | Y (issues ≤ 3) + fixed matrices | – | aggregate (D) |
| F16 | HR-1 + independent | HR-1, Mustaqil → Bosh direktor | Y | Y (action plan) + fixed rows | – | aggregate |
| F17 | HR-1 | Yurist → Bosh direktor + Xodim; HR-1 | Y | Y (KPI) | – (it is itself a contract annex) | E, P |
| F18 | HR-3; HR-2; HR-4 | HR-3 → Ish haqi hisobchisi → Bosh buxgalter | Y | N (fixed 10 + 4 + 4 rows) | – | D, F09, F12 |
| F19 | Qabul qiluvchi bo'lim rahbari | Xodim → hozirgi rahbar → qabul qiluvchi rahbar → HR-4 → Bosh buxgalter → HR-1 → Bosh direktor (conditional) | Y | N | – | E, D, P, F06, F09, F10, F11 |

*F02 is already in the platform.

Process chains (from R01–R08):
- **Hiring:** F01 → (F10 if the passport is missing) → F02 → F03 → F04 → F05 → F11 + F06 → F07 (day 85) → order.
- **Leaving:** application → F09 (HR-3 opens) + F08 (HR-4) → F09 mark → dismissal order → F18 → final settlement.
- **Discipline / pay:** F13 → (F12 §2) → F18 → payroll. Monthly: F12 → F18 → F15.
- **Continuity:** F10 → F14 → F11. Annual: F16.
- **Transfer:** F19 → F09 / F06 / F11 → order.
- **Senior managers:** F17.

## 3b. Field types needed (generic form engine)

1. Short text, long text
2. Integer, percent (with pass threshold, e.g. ≥ 70%), money (UZS, no decimals seen)
3. Date, datetime (F13), time range as text (F01 «Ish rejimi»), date range (F05 probation, F14 §3, F19 temporary transfer), month (F12, F15, F18), year (F16)
4. Yes/no
5. Single choice. Options may reveal extra input («bor: ___», «sabab: ___», «ha (kod: ___)», «kechikish (__ daqiqa)», «akt № ___»)
6. Multi choice with a max count (F08 «Qo'shimcha» ≤ 2). Ambiguous single/multi cases: F13 Buzilish turi, F19 So'rov turi
7. Check (single tick: done / entered / returned)
8. Score 1–5 (F02, F06, F07, F08, F14), with a computed average and band mapping (F07)
9. References: employee, candidate, department, direction (dictionary of 16), position, vacancy / vacancy request, another blank instance by № (F04, F07, F09, F13, F01, F02, F03)
10. Order reference (buyruq № + date). Document running № (auto-issued, e.g. «So'rov № (HR-4 beradi)»)
11. Signature step: role or person + date + decision. Many are conditional (F03 HR-1, F01 Bosh buxgalter, F19 Bosh direktor)
12. Repeating table with typed columns. Minimum row count (F03 referees ≥ 2), maximum row count (F15 §5 ≤ 3)
13. Fixed-row table / matrix (printed row labels × typed columns): F05 §2, F06 §2, F07 §1, F08 §1, F15, F16, F18
14. Computed fields: average, sum, difference, date + N days, working-day difference, quorum, Σ weights = 100
15. Conditional section or row visibility (F03 II–III by consent; F08 §2 by initiative; F05 rows by gender, role or hazard)
16. Static text / consent blocks with filled-in placeholders (F03 I, F05 part 1, F13 parts 2–3, F17 §3–6)
17. File attachment / URL (F10 §1, F12 basis document, F13 evidence and explanation). **No file format is stated anywhere.**
18. Prefill from system data plus links to rows of another blank (F14 and F10 §6 → F10 §2 rows; F09 §2–4 ← F10 §3–4)

---

## 4. Workflows observed (from regulations)
- **Monthly calendar** (R07 §3, R08 §4, F12, F15, F18): working day 1 tabel (F18) · days 1–2 F12 received · day 3 F12 approved and passed to payroll · day 4 F15 numbers · day 5 F15 conclusion + register reconciliation · day 5 payslips · day 7 SAP «[?]».
- **Probation timeline** (R03 §3): −1 mentor · 1 first day (F11, F06) · 7 test · 14 retest · 30 · 60 · 85 test + F07 · 88 HR-1 decision · 90 HR-2 order.
- **Council:** weekly, Thursday (HR-04 §2.5), quorum 2/3.

---

## 5. Inferred (not read; confidence noted)
| # | Inference | Confidence | Evidence |
|---|---|---|---|
| I1 | F01 overlaps the existing «Vakansiya so'rovlari» feature (`VacancyRequest`: department, requester, title, openings, needed-by, note, status) | high | `docs/SPEC-vakansiya-sorovi.md` §1 |
| I2 | Prefill sources exist in the system: employee name/phone/address, employment (department, position, hire date, salary, status, termination date + reason), probation (start/end/status/conclusion), department head (`Department.HeadEmployeeId`), awards/penalties (`Incentive`: amount or percent, reason), employment events (Transfer, AdditionalDuty, ProbationResult, Termination), termination-reason dictionary, absences, attendance | high | `api/src/Bonvi.Domain/Employees/*.cs`, `Organization/*.cs`, `Catalog/SimpleCatalogs.cs` (property names only were read) |
| I3 | The system has no «lavozim kodi» field on `Position` (it has name, department, grade, probation days, text items), so the F10 code has no current home | medium | `Organization/Position.cs` property list |
| I4 | F08 K01–K12 probably map to the existing `TerminationReason` dictionary, but the dictionary's contents were not checked | low | entity name only |
| I5 | Paper «Imzo» lines for internal roles become an approval step by a logged-in user. Candidate and employee signatures (F02, F03, F05, F06, F07, F08, F11, F13) need a different mechanism | medium | inferred from who signs. Employees already use the PWA (project memory) |
| I6 | The filled samples show the blanks are in active paper use since about 2026-10 (dates around 22–30.10.2026) | medium | F01, F05, F07, F08 sample dates |

---

## 6. Unreadable / missing / questions

**Missing material referenced by the blanks:** HR-A01, HR-A02 (attestation sheet; no F-blank for attestation), HR-A03 checklist (132 points, needed for F16 §1), HR-LY-00 (passport/instruction standard), HR-T00–T04 (tests, Google Forms), HR-01 Nizom §7 (KPI targets for F16 §2), Excel registers HR-X01/X02/X03 (sources for F15/F16), Mukofot nizomi (R07 §5), the F02 v2 docx and the original docx files of the other blanks.

**Open questions / contradictions (for plan-analyst → owner):**
1. **F01 vs the existing vacancy request:** F01 has «Ish haqi», but the owner decided on 2026-10-30 «No salary in the request» (SPEC-vakansiya-sorovi). Should F01 replace, extend, or sit beside the current request?
2. **F02 version:** the txt is v1.0 and the platform implements v2. Confirm v2 is the master.
3. **Signatures online:** are click-approvals enough for internal roles? How do candidates and employees «sign» (tablet signature, PWA confirm, printed PDF)? Is a printable PDF of each blank needed, as for F02?
4. **Role mapping:** blanks name HR-1…HR-4 (+ future HR-5, HR-8), «Mehnat muhofazasi xizmati», «Ish haqi hisobchisi», «Bosh buxgalter», «Yurist», «Kengash». Do these map to platform roles/permissions or to named users? Who covers HR-5/HR-8 duties now?
5. **F04 §2 column ambiguity:** does the ☐ «o'tdi / o'tmadi / talab qilinmaydi» belong to «Amaliy sinov bahosi» or to «Surishtiruv (F03)»? «Qaror» has no printed options; R02 says qabul / zaxira / rad. Free text or choice?
6. **F04 secretary:** both the table and the footer say «Kotib (HR-1)». In the filled sample the secretary row is held by the person HR-00 lists as HR-3, and member roles differ from HR-00. Is council membership a fixed approved list or chosen per meeting?
7. **Missing header metadata:** F04, F07, F08 and F09 have no version or filler header. F07 names a single legal entity rather than «MBF Company». Is a blank issued per legal entity?
8. **F01 §2 «Majburiy / afzallik»:** is it a flag (mandatory / preferred) plus a value, or just the value?
9. **Probation length:** «3 oy» (F05) vs «+90 kun» (F06) vs the per-position `ProbationDays` in the system. Which rule wins?
10. **Probation outcome set:** F06 day 90 = o'tdi / o'tmadi (HR-1). R03 day 88 and F07 = o'tdi / shartli / o'tmadi. Which day and which options?
11. **F07 bands gap:** «≥ 3,5 o'tdi; 3,0–3,4 shartli; < 3,0 o'tmadi» leaves 3.41–3.49 undefined, and a 7-criteria mean such as 24/7 = 3.43 falls in it. Should the system also enforce R05's «test ≥ 70% AND ≥ 3,5», or only suggest?
12. **Test results:** typed in from Google Forms or integrated? (F06, F07, F11 test columns)
13. **Direction lists disagree:** R08 dictionary has 16 directions, F15 has 7 rows, F18 has 10 rows with merged groups, and the system has `Direction`. Which grouping applies per report? HR-04 also left open which direction two legal entities belong to.
14. **Witness count:** F13 part 3 has 3 signature lines, but F11, R06 and R07 say «2 guvoh».
15. **F09 §5 hand-added rows** (transport/DAN debt, property accountant, payroll debt) with named responsible staff: are they official, and which platform users are those responsible people?
16. **Legal drafts «[?]»:** F03, F12, F13, F17 and F19 are pending lawyer review, and R07/F18 «7-ish kuni [?]». Can they go online before sign-off?
17. **Computed reports (F15, F16):** generate from platform data, or let HR type them in? The source says «Dashboard'dan avtomatik» (Excel), and «SAP / mehnat.uz farqi» is external.
18. **F12 approval:** on HR-1 approval, should each row become an award/penalty record in the employee card? Is «Summa yoki %» either/or?
19. **F19 §2 «Yangi» column for «Xodim F.I.Sh.»:** meaningless for a transfer. Is it the replacement person or not used? F13 «Buzilish turi» and F19 «So'rov turi»: single or multiple?
20. **Attachments:** no format or size is stated. Should F05 part 2 store scans of the 12 documents (passport, PINFL — sensitive), or only tick them? R01 forbids medical diagnoses in the personal file.
21. **Retention / access:** F03 is visible only to HR-1, HR-4 and council members, and is destroyed after 6 months if the candidate is not hired. F02 has the same 6-month rule. Should the platform enforce automatic deletion?
22. **F06 §2 day 60:** «rahbar bahosi (1–5)» has no printed cell. Confirm a 1–5 score is stored.
