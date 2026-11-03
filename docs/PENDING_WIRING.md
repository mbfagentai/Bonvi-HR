# Ulanmagan qismlar — YOPILDI (2026-09-06)

Bu fayl bosqichlar PARALLEL qilinayotganda kerak edi: har bir agent
o'zidan keyingiga nima qolganini shu yerga yozardi. Barcha uchlar
bog'landi, quyida yakuniy holat.

| Nima | Holat |
|---|---|
| `bot.offerUrl` — bot tomoni (`BotSettings.OfferUrl`, 21-qadam) | ✅ `Bonvi.Bot/Bonvi/BonviClient.cs`, `BotWorker` |
| `bot.offerUrl` — sozlamalar ekrani + 4 til | ✅ `system-settings-page.tsx`, `locales/*.json` |
| `vacancyId` ixtiyoriy → «Umumiy anketa» | ✅ `RegisterCandidateCommandHandler.ResolveVacancyAsync` |
| Nomzod surati — bot yuklashi | ✅ `TelegramClient.DownloadFileAsync` → `BonviClient.UploadCandidatePhotoAsync` |
| Anketa + surat — nomzod kartasida | ✅ `candidate-questionnaire-card.tsx`, `candidate-photo.tsx` |

## Ataylab ulanmagan (o'zgarish emas, QAROR)

- **`recruiting.generalPoolVacancyId`** — `SystemSettingKind.Hidden`.
  `GET /api/settings` javobiga umuman tushmaydi va ekranda ko'rinmaydi:
  bu tizimning ichki bog'lanishi, foydalanuvchi sozlamasi emas. Botga
  ham ochilmaydi — vakansiyani server o'zi tanlaydi.
- ~~Suhbat holati Redis'ga ko'chirilmadi~~ — 2026-09-29 dan holatning
  nusxasi Redis'da, bot qayta ishga tushsa ham anketa davom etadi
  (`docs/SPEC-anketa.md`, 4.2).

## Ochiq qolgan yagona nuqta

- **`hasPhoto` nomzodlar RO'YXATIDA o'qilmaydi.** Maydon API javobida
  bor (`CandidateListItemDto`), lekin `web/.../candidates-page.tsx`
  rasmni chizmaydi — surat faqat nomzod kartasida. SPEC ham shuni
  talab qilgan, ya'ni bu kamchilik emas. Ro'yxatda ham rasm kerak
  bo'lsa (xodimlar ro'yxatida shunday qilingan), ma'lumot allaqachon
  joyida — faqat ekranni o'zgartirish qoladi.

---

# Ulanmagan qismlar — NOMZOD YAKUNLARI (2026-09-07)

`docs/SPEC-nomzod-yakuni.md` ning 1 va 2-bo'limi BACKENDDA tayyor
(rezerv holati, rezervga o'tkazish/qaytarish, taklifsiz ishga olish) va
ishga olinganlar sukut ro'yxatdan chiqarildi. Frontend ATAYLAB
tegilmagan — topshiriq faqat backendga berilgan.

> **Frontend ULANDI (2026-09-07).** Quyidagi jadval yopildi.

| Nima | Qayerda | Holat |
|---|---|---|
| `Reserve` holati mijoz tomonda | `web/src/features/recruiting/api/recruiting-api.ts` → `CANDIDATE_STATUSES` | ✅ qo'shildi |
| `Reserve` uchun rang/ikonka | `web/src/features/recruiting/lib/recruiting-presentation.ts` → `CANDIDATE_LOOK` | ✅ kulrang + xatcho'p |
| «Rezervga» / «Rezervdan qaytarish» tugmalari | `candidate-card-page.tsx` + `candidate-action-dialog.tsx` | ✅ |
| «Ishga olish» dialogi (`POST /candidates/{id}/hire`) | `candidate-hire-dialog.tsx` (YANGI) | ✅ |
| 4 tildagi tarjimalar (`reserve`, `hire`) | `web/src/shared/i18n/locales/*.json` | ✅ `./bonvi check` yashil |
| OpenAPI → TypeScript qayta yaratish (`./bonvi gen`) | `web/src/shared/api/generated/` | ✅ bajarildi |

⚠️ **Eng muhimi birinchi qator edi.** `CANDIDATE_STATUSES` da `Reserve`
bo'lmagani uchun mijozdagi `oneOf(...)` noma'lum qiymatni JIMGINA
`InProgress` ga tushirardi (`recruiting-api.ts`) — ya'ni rezervdagi
nomzod ekranda «Jarayonda» bo'lib ko'rinardi. Endi ro'yxat, filtr,
voronka va karta beshta holatni ham to'g'ri ko'rsatadi.

**Rezervdagi nomzodni ishga olish — IKKI so'rov.** Server uni
to'g'ridan-to'g'ri qabul qilmaydi («nomzod.jarayonda_emas», 409), shu
sababli oyna avval `/return-from-reserve`, keyin `/hire` chaqiradi.
Foydalanuvchi uchun bu bitta tugma; qaror va uning narxi
`docs/ASSUMPTIONS.md` da.

### Frontendda ATAYLAB qilinmagani

- **Ish taklifi tabiga TEGILMADI** — u SPEC 3-bo'limida butunlay
  o'chiriladi. `offer-accept-dialog.tsx` va yangi
  `candidate-hire-dialog.tsx` bugun bir-birining nusxasi bo'lib turadi
  (bir xil `Section`, bir xil natija ekrani); umumiy komponentga
  chiqarish faqat taklif o'chirilgach ma'noli — o'shanda birinchisi
  butunlay ketadi.
- **`hiredEmployeeId` RO'YXAT javobida `Guid.Empty` qaytadi** (nomzod
  kartasida esa to'g'ri `null`). Bugun ro'yxat bu maydonni umuman
  chizmaydi, ya'ni zarari yo'q; lekin ro'yxatga «xodim kartasi»
  havolasi qo'shilsa, u rad etilgan nomzodda ham paydo bo'lib,
  mavjud bo'lmagan xodimga olib borardi.

> **Ish taklifi O'CHIRILDI (2026-09-07).** SPEC 3-bo'limi bajarildi:
> `job_offers` jadvali, endpoint'lari, bildirishnomalari, ekranlari va
> `taklif.*` xato kodlari butunlay olib tashlandi
> (`20260907070127_TaklifOlibTashlandi`). Nomzoddan xodimga o'tishning
> yagona yo'li — `POST /candidates/{id}/hire`.

---

# Tashqi API — BACKEND TAYYOR, frontend boshqa agentda (2026-09-18)

`docs/SPEC-tashqi-api.md` ning §2–§5 va §7–§9 bo'limlari bajarildi. §6 dagi
**ichki boshqaruv endpoint'lari ham tayyor** — qolgani sozlamalardagi ekran.

| Nima | Qayerda | Holat |
|---|---|---|
| `api_keys` jadvali + migratsiya | `20260918063103_TashqiApiKalitlari` | ✅ |
| `X-API-Key` sxemasi, ommaviy siyosat | `Bonvi.Api/Extensions/ApiKeyAuthenticationExtensions.cs` | ✅ |
| `GET /api/public/v1/...` (faqat `GET`) | `Bonvi.Api/Endpoints/PublicApiEndpoints.cs` | ✅ |
| `GET/POST /api/api-keys`, `POST /{id}/revoke` | `Bonvi.Api/Endpoints/ApiKeyEndpoints.cs` | ✅ |
| Tezlik cheklovi (prefiks bo'yicha) | `RateLimitingExtensions` + `appsettings.json` | ✅ |
| Alohida OpenAPI hujjati (`/openapi/public.json`) | `Program.cs` | ✅ |
| Xato kodlari 4 tilda | `web/src/shared/i18n/locales/*.json` | ✅ |
| Iste'molchi qo'llanmasi | `docs/API-TASHQI.md` | ✅ |

## Qolgan ish — FRONTEND (boshqa agent)

**Sozlamalar → «Tashqi API» bo'limi** (SPEC §6), ruxsat `settings.manage`:

- kalitlar jadvali: nomi, prefiksi, yaratilgan, oxirgi ishlatilgan
  (nisbiy, Toshkent vaqti), holati (faol / bekor qilingan);
- «Yangi kalit» — nom so'raydi, so'ng to'liq kalitni **bir marta**
  ko'rsatadi: nusxa olish tugmasi va ochiq ogohlantirish bilan;
- «Bekor qilish» — tasdiqlash oynasi; bekor qilingan kalit jadvalda
  kulrang bo'lib qoladi.

Kerakli endpoint'lar ichki OpenAPI hujjatida (`/openapi/v1.json`), ya'ni
`./bonvi gen` ni ishga tushirish yetadi. Xato kodlarining tarjimasi
allaqachon to'rt tilda.

> ⚠️ Javobdagi `key` maydoni FAQAT `POST /api/api-keys` da keladi va
> boshqa hech qayerda yo'q. Uni holatga saqlab qo'ymang — oyna
> yopilganda u yo'qolishi KERAK.

> **Frontend BAJARILDI (2026-09-18).** Sozlamalar sahifasida «Tashqi API»
> bo'limi bor: kalitlar jadvali (nomi, prefiksi, yaratilgan, oxirgi so'rov
> + IP, holati), «Yangi kalit» ikki qadamli oynasi (nom → to'liq kalit BIR
> MARTA, nusxa olish tugmasi bilan) va «Kalitni bekor qilish» tasdiq
> oynasi. Fayllar: `web/src/features/settings/api/api-keys-api.ts`,
> `api-keys-queries.ts`, `components/api-keys-section.tsx`,
> `api-key-create-dialog.tsx`, `api-key-revoke-dialog.tsx`; tarjimalar
> `settings.apiKeys.*` (4 til). Yangi marshrut ham, menyu bandi ham
> QO'SHILMADI — bo'lim mavjud `/sozlamalar` sahifasining ichida, ya'ni
> `App`/`Sidebar` kabi umumiy fayllarga tegilmadi.

# Ulanmagan qismlar — VERIFIKATSIYA (davomat) moduli, 2026-09-17

`docs/SPEC-verifikatsiya.md` bo'yicha modul PARALLEL qurilmoqda. Har bir
qism o'zidan keyingiga nima qolganini shu yerga yozadi; hammasi
bog'langach, bu bo'lim ham «YOPILDI» deb belgilanadi.

## B2 (ruxsatlar + sozlamalar) dan qolgan uchlar

- **`/sozlamalar` ekrani ikkala yangi maydonni CHIZMAYDI.**
  `web/src/features/settings/components/system-settings-page.tsx` dagi
  `GROUPS` xaritasi qo'lda yozilgan va unda yo'q kalitni jimgina
  tashlab ketadi. `attendance.photoRetentionDays` va
  `attendance.passRetentionDays` uchun yozuv + 4 tilda izoh kerak
  (W qismi).
- **`0 = hech qachon o'chirilmasin` ekrandan tanlanmaydi.** Sahifa
  `min={setting.min}` qo'yadi, sozlama DTO'si esa `AllowZero` ni
  tashimaydi. Yo DTO'ga qo'shiladi, yo sahifa shu kalit uchun
  `min={0}` beradi. Serverda 0 allaqachon to'g'ri qabul qilinadi.
- **B7 tozalash ishlari** ikkala sozlamani O'QIB ishlashi shart:
  `passRetentionDays == 0` — hech narsa o'chirilmaydi; sozlama yozuvi
  yo'q bo'lsa, `AttendanceRules.DefaultPhotoRetentionDays` ga qaytiladi.
- **Testlar (T):** HR menejerida ikkala ruxsat borligi; ruxsatsiz
  foydalanuvchida 403; `AllowZero` chegaralari — 0 ✓, 29 ✗, 30 ✓,
  3650 ✓, 3651 ✗.

## B1 (sxema + migratsiya) — YOPILDI, C1 tasdiqlangan

- Migratsiya `20260917063405_DavomatModuli` dev bazaning NUSXASIDA
  orqaga (`Down`) va oldinga (`Up`) yurgizildi: faqat modulning 7 ta
  jadvali paydo bo'ladi/yo'qoladi; mavjud jadvallarning ustunlari,
  indekslari va qator sonlari o'zgarmadi. `./bonvi build` yashil,
  `./bonvi test` — 1508/1508.
- **`./bonvi check` QIZIL — kutilgan holat.** B1 kiritgan 18 ta xato
  kodi hali 4 tilga tarjima qilinmagan (W qismi):
  `biriktirma_obyekt_yoq`, `davr_notogri`, `imzo_siri_yoq`,
  `jadval_kun_takror`, `jadval_kun_yetishmaydi`, `jadval_nom_bosh`,
  `jadval_tanaffus_oynasi`, `jadval_tanaffus_toliq_emas`,
  `jadval_tanaffus_uzunligi`, `jadval_topilmadi`,
  `jadval_turi_qollanmaydi`, `jadval_vaqt_bosh`, `jadval_vaqt_teng`,
  `jadval_yon_berish`, `qurilma_nom_bosh`, `shaxs_raqami_bosh`,
  `vaqt_yaroqsiz`, `xodim_topilmadi` (hammasi `davomat.` prefiksi bilan).
  Keyingi qismlar qo'shadigan kodlar ham shu ro'yxatga yoziladi.
- SPEC muallifi «tungi smenadagi normallashtirishda nuqson» borligini
  aytib ulgurmay to'xtadi. `WorkScheduleDay.ValidateBreakWindow` SPEC
  §2.4.0.1 bilan solishtirildi — mos keladi (`TimeOnly` ayirmasi 24
  soat bo'yicha aylanadi). Muallif qaytganda aniq holatni aytadi.

## F1 (frontend API qatlami) — YOPILDI

- DTO tiplari QO'LDA yozilgan; B6 dan keyin `./bonvi gen` bilan
  generatsiya qilingan tiplarga almashtiriladi — bu F1 ning o'z
  shartnomasi (§10.3), ixtiyoriy tozalash emas.
- W uchun: `attendance.weekdayMon` … `attendance.weekdaySun` — yettita
  hafta kuni kaliti ham kerak.

## Koordinator qarorlari (2026-09-17)

- **Ikkinchi migratsiya — ATAYLAB, vaqtincha.**
  `20260917072458_DavomatTanaffusNatijasi` `attendance_days` ga
  `break_taken_minutes` / `break_deduction_minutes` qo'shadi (tabel
  katagida «nega 8 soat emas, 7 soat» degan savolga javob). Birinchi
  migratsiya dev bazaga qo'llangan va parallel agentlar ishlab turgan
  paytda uni qayta yaratish ularning bazasini buzardi. **Merge'dan
  oldin, hech bir agent ishlamayotganda, ikkalasi BITTA `DavomatModuli`
  ga birlashtiriladi** (SPEC DoD: modul uchun bitta migratsiya).
- **Tanaffus ushlanmasi binoda bo'lgan vaqt bilan cheklanadi:**
  `max(0, min(break_minutes − breakTaken, presentInWindow))`. Tushlikdan
  oldin uyga ketgan yoki oyna o'rtasida kelgan odamdan u ICHKARIDA
  bo'lmagan tushlik ushlanmaydi.
- **Xato kodlari tuzatildi:** bo'sh `employeeId` → `davomat.xodim_yoq`,
  bo'sh `workScheduleId` → `davomat.jadval_yoq`. `_topilmadi` faqat 404
  uchun. B6: ikkala `_topilmadi` kodini `Error.NotFound` bilan chiqaring,
  400 ni 404 ga «to'g'rilab» yubormang. W: to'rttalasini ham tarjima qiling.
- **B7 (pass tozalash):** birinchi tekshiruv — `passRetentionDays == 0`;
  qayta ishlanmagan o'tishlar o'chirilmaydi; tozalash hech qachon kunni
  qayta hisoblashga belgilamaydi — aks holda bir yillik tabel jimgina
  nolga aylanadi (SPEC §9.2.1).
- **W (sozlamalar ekrani):** ikkala saqlash muddati kaliti
  `system-settings-page.tsx` dagi `GROUPS` ga qo'shilmasa, ekranda
  ko'rinmaydi. SPEC `passRetentionDays ≥ photoRetentionDays` deydi —
  bu hozir serverda tekshirilmaydi (zarari yo'q: o'tish o'chsa surati
  ham birga o'chadi), lekin ekran ogohlantirishi kerak.

## `./bonvi gen` almashtiruvida F1 qo'shishi kerak bo'lgan maydonlar

SPEC yakuniy §6 matni F1 yozib bo'lgandan KEYIN aniqlandi. Generatsiya
qilingan tiplarga o'tishda quyidagilar klientga ham tushishi shart:

- timesheet javobida `pageCount`; har bir katakda
  `breakTakenMinutes` / `breakDeductionMinutes` (popover shu ikkisini
  ko'rsatadi; «tanaffus oynasi belgilanmagan» xabari QAYTARIB OLINDI —
  cheklov tufayli «0 / 0» «oynada binoda bo'lmagan» degani ham bo'lishi
  mumkin);
- o'tishda `skewWarning` — SERVER hisoblaydi, klient qayta hisoblamaydi
  (`isSkewWarning` yordamchisi faqat shu maydonni o'qishi kerak);
- ishdan ketgan xodim ogohlantirishi — bitta ODAM uchun bitta yozuv,
  `passCount` bilan; alohida `/former-employee-alerts` endpoint'i,
  nav belgisi uni faqat `attendance.view` egasi uchun chaqiradi;
- `GET /passes/{id}` — `?passId=` havolasi endi serverdan o'qiladi
  (`davomat.otish_topilmadi` — 404);
- `/schedules/resolve` javobi kengroq: `employeeName`, asosiy mehnat
  munosabati va tashkiliy id/nomlar, `matchedDepartmentId`/`Name`
  (g'olib bo'lim OTA bo'lim bo'lishi mumkin), `day`;
- import yuklashda ixtiyoriy `provider` maydoni (sukut `Verifix`).

## B4 (jadval hal qilish + tabel hisoblash) — C4 tasdiqlangan

Hisoblash — sof mantiq: `Application/Attendance/Scheduling/**` va
`Application/Attendance/Computation/**`. Baza, HTTP, Hangfire va DI yo'q;
106 ta birlik testi bazasiz o'tadi (`Bonvi.Application.Tests/Attendance/**`).

- **DI ga ro'yxatdan o'tadigan narsa YO'Q (B6).** `ScheduleResolver` va
  `AttendanceDayBuilder` servis emas: ular bitta to'plamning ma'lumoti
  ustida quriladi (`new ScheduleResolver(biriktirmalar, jadvallar,
  new DepartmentTree(bo'limlar))` → `new AttendanceDayBuilder(resolver)`).
  Qolganlari statik (`PrimaryEmployment`, `DayPlan.For`, `PassPairing`,
  `PassAttribution`).
- **Qayta hisoblash (B6 `POST /recompute`, B7 ishlari) — YUKLASH SHARTNOMASI.**
  Buzilsa natija jimgina noto'g'ri emas, ko'pincha istisno bo'ladi:
  - o'tishlar: `local_date` ∈ `AttendanceDayBuilder.PassDatesNeeded(from, to)`
    (ikki tomonga +1 kun — tungi smenaning chiqishi ertangi sanada),
    `resolved_employee_id IS NOT NULL`, bazadan to'g'ridan-to'g'ri
    `PassReading` ga proyeksiya (`raw_event` o'qilmasin). Boshqa xodimning
    yoki bog'lanmagan o'tishi berilsa — `ArgumentException`;
  - biriktirmalar: tirik biriktirmalarning HAMMASI (yoki kamida
    [from − 2, to + 2] ni qamraganlari);
  - jadvallar: biriktirmalar ko'rsatgan hammasi, KUNLARI BILAN
    (`Include(Days)`). Jadval yo'q → konstruktorda istisno; kunlar
    yuklanmagan → `DayPlan.For` da istisno;
  - bo'limlar: hammasi (~73). ⚠️ O'chirilgan (soft delete) bo'lim EF
    filtrida tushib qolsa, daraxt bo'ylab yurish o'sha yerda to'xtaydi —
    o'chirilgan oraliq bo'lim bo'lishi mumkin bo'lsa `IgnoreQueryFilters`;
  - munosabatlar: xodimning BARCHASI, tugaganlari ham.
- **Yozish (B7).** `Build` har bir so'ralgan sana uchun natija qaytaradi.
  `ShouldStore == true` → `AttendanceDay.Compute/Recompute(result.Facts,
  clock.UtcNow)` bilan upsert. `ShouldStore == false`, lekin qator BOR
  (masalan jadval dam olish kuniga o'zgartirilgan) — SPEC bu holatni
  aytmaydi; qator qolsa u eski `Absent` ni ko'rsataveradi. Taklif: shu
  bitta qatorni o'chirish (§9.3 dagi taqiq DELETE+INSERT juftligiga
  tegishli, bitta eskirgan qatorni o'chirishga emas) — koordinator hal qiladi.
- **`GET /schedules/resolve` (B6):** `PrimaryEmployment.Select(munosabatlar,
  sana)` → `null` bo'lsa «ishlamaydi»; aks holda `resolver.Resolve(...)` →
  `Schedule`, `Tier`, `Assignment`, `Day` (reja lahzalari, UTC).
- **Katak izohi (B5/F3) — OCHIQ SAVOL.** Saqlangan ikki ustun
  (`break_taken_minutes`, `break_deduction_minutes`) «oyna belgilanmagan»
  (`breakNoWindow`) va «oyna bor, ushlanadigan narsa yo'q» holatlarini
  AJRATMAYDI — ikkalasida ham (0, 0). Hisoblash
  `ComputedAttendanceDay.HasBreakWindow` ni beradi, lekin u yozilmaydi.
  Yo ustun qo'shiladi, yo izoh ikki holatni birlashtiradi. Jadvalni
  o'qish vaqtida qo'shib olish noto'g'ri: jadval keyin tahrirlangan
  bo'lishi mumkin.
- **B1 izohi eskirgan:** `AttendanceDayFacts` dagi `BreakDeductionMinutes`
  `<param>` i hali `max(0, break_minutes − BreakTakenMinutes)` deydi;
  amaldagi formula oynadagi borliq bilan cheklangan (§5.5.1).
- **`WorkSchedule.IsActive` hal qilishda hisobga OLINMAYDI** (§5.2 shart
  qo'ymaydi; qaror `ASSUMPTIONS.md` da). B6: tirik biriktirmasi bor
  jadvalni faolsizlantirish jimgina hech narsani o'zgartirmaydi — ekran
  buni aytishi yoki DELETE kabi rad etishi kerak.
- **T:** §11.1 #64a (hisoblangan qiymat qatorga aynan yoziladi) va #50
  ning bazali varianti (ikki marta qayta hisoblash — har bir ustun bir
  xil) integratsiya testida qoladi; hisoblashning o'zi idempotentligi
  birlik testida tekshirilgan.

## B4 hisobotidan koordinator qarorlari (2026-09-17)

- **Jadvalsiz kunda ortiqcha ish = 0** (kodda tuzatildi,
  `AttendanceDayBuilder`, test #41 yangilandi; 106/106). Jadval bor dam
  olish kunida ishlangan vaqt to'liq ortiqcha ish bo'lib qoladi.
- **B6/B7 — eskirgan qatorni o'chirish:** qayta hisoblashda
  `ShouldStore == false`, lekin qator bor bo'lsa — qator O'CHIRILADI.
  `attendance_days` hosila ma'lumot; aks holda jadval dam olish kuniga
  o'zgargan kunda «Kelmadi» qolib ketadi.
- **B6 — biriktirilgan jadvalni faolsizlantirish RAD ETILADI** (yangi
  xato kodi kerak, masalan `davomat.jadval_biriktirilgan`). Hisob
  mexanizmi `IsActive` ga qaramaydi — ruxsat berilsa, ekran va tabel bir
  -biriga zid gapirardi.
- **§5.3 chegara holati — ATAYLAB tegilmadi:** atribusiya oynasi ikkala
  chekkada ham yopiq, ya'ni aynan 14:00:00 dagi o'tish (bugun dam olish
  kuni bo'lsa) kechagi tungi smenaga tushadi. Bu bitta soniya; bugun
  ish kuni bo'lsa markazga yaqinlik qoidasi uni to'g'ri kunga beradi.

## B5 (o'qish modellari + hisobotlar + eksport) — C5 tasdiqlangan

O'qish porti `IAttendanceReadModel` (+ `IAttendanceExcelWriter`), uning
amalga oshirilishi va handlerlar TAYYOR; DI ga hech narsa yozilmagan va
endpoint yo'q — ikkalasi B6 ning ishi (§10.2.1). C5:
`AttendanceReadModelTests` (7 ta, haqiqiy PostgreSQL, HTTP'siz) —
avgust tabelida 5 xodim × 31 katak, hosil qilingan kataklar joyida;
Excel fayli qayta o'qilib tekshirildi (sana — sana, daqiqa — son,
`SUM` formulasi ishlaydi). 540 xodim × 31 kun sinovida tabel 9 ta
so'rov (sahifa 25 da ham, 540 da ham), ~0,3 s; eksport ~2 s.

- **B6 — DI.** Infrastructure (`AddAttendanceServices`):
  `AddScoped<IAttendanceReadModel, AttendanceReadModel>()`,
  `AddSingleton<IAttendanceExcelWriter, AttendanceExcelWriter>()`
  (`EmployeeExcelWriter` bilan bir xil). Application (`AddAttendance`):
  `SearchPassesQueryHandler`, `GetPassQueryHandler`,
  `GetBoardQueryHandler`, `GetFormerEmployeeAlertsQueryHandler`,
  `GetTimesheetQueryHandler`, `GetAttendanceExceptionsQueryHandler`,
  `GetAttendanceSummaryQueryHandler`, `ExportTimesheetQueryHandler`,
  `ExportPassesQueryHandler`, `ExportAttendanceExceptionsQueryHandler`,
  `ExportAttendanceSummaryQueryHandler` — hammasi `AddScoped`.
- **B6 — endpointlar** (`Application/Attendance/Queries/**` dagi so'rov
  yozuvlari parametrlarning AYNAN ro'yxati):
  `GET /passes` (`SearchPassesQuery`), `GET /passes/{id}` (`GetPassQuery`,
  404 `davomat.otish_topilmadi`), `GET /board`, `GET /former-employee-alerts`,
  `GET /timesheet`, `GET /reports/exceptions`, `GET /reports/summary` —
  `attendance.view`; to'rtta `GET /export/*` — o'sha so'rov yozuvlari,
  `reports.export` + `RateLimitPolicies.Export`, javob
  `Results.File(Content, xlsx, FileName)` (`ReportsEndpoints` shakli).
  `/board` ning `WithDescription` ida «filtrlar ishdan ketganlar
  ogohlantirishiga ta'sir qilmaydi» deb yozilishi SHART (§6.5).
  `direction` / `method` sanoq bo'lib bog'lanadi (`ListIncidentsQuery`
  kabi), `kind` esa satr — noto'g'ri qiymatga kod bilan javob beradi.
- **B6 — `GET /passes/{id}/photo` B5 da YO'Q.** `FindPassAsync` qaytargan
  `PhotoKey` bo'yicha `PhotoHttp.Write`; `null` → 404.
- **B6 — §2.10 dagi ikki metod B5 da YO'Q:** `ListUnmatchedAsync` va
  `FindEmployeeCandidatesAsync` (moslik/import). Port faylida bo'limi
  belgilab qo'yilgan — o'sha yerga qo'shiladi.
- **W — 4 tilga tarjima qilinadigan YANGI kodlar (SPEC §6.9 dagi 40 tadan
  tashqari):** `davomat.otish_topilmadi` (SPEC ga yozilgan),
  `davomat.oy_notogri` (tabelda yil/oy noto'g'ri — `davr_notogri` ning
  ma'nosi «tugash boshlanishdan oldin», u bilan aralashtirilmadi),
  `davomat.hisobot_turi_notogri` (chetlanish turi yo'q yoki noma'lum),
  `davomat.eksport_juda_kop` (umumiy `eksport.juda_kop` tarjimasida
  «5000 ta» yozilgan, davomat chegarasi boshqa: 20 000 qator / tabelda
  2 000 xodim). B5 ishlatadigan eski kodlar: `davomat.davr_uzun`,
  `davomat.davr_notogri`.
- ~~`AttendanceRules.FormerEmployeeAlertHours` yo'q~~ — **YOPILDI:**
  koordinator qo'shdi, read model endi shu konstantani ishlatadi.
- ~~§5.1/§5.2 ning ikkinchi nusxasi~~ — **YOPILDI (2026-09-17):**
  `AttendanceCalendar` o'chirildi. Yozuvi yo'q katak B4 ning
  `AttendanceDayBuilder` ini NOL o'tish bilan yurgizib olinadi
  (`Facts.Status`), taxtaning «kutilgan / kelmagan / kechikkan» sanog'i
  `ScheduleResolver` + `AttendanceDayBuilder` dan; asosiy munosabat —
  `PrimaryEmployment.Select`. C5 testlari o'zgartirilmasdan yashil.
  Yagona qolgan «nusxa» — ishdan ketganlar ogohlantirishidagi SQL sharti:
  u `PrimaryEmployment.Covers` ning bazadagi ko'rinishi (nishon har 30
  soniyada so'raladi, xotirada tekshirish kunlik o'tishlarni tortib
  kelardi); qamrov qoidasi o'zgarsa, birga o'zgaradi.
- **B6/B7 yuklovchisi uchun eslatma:** read model jadvallarni va bo'limlarni
  `IgnoreQueryFilters` bilan o'qiydi (o'chirilgan oraliq bo'lim daraxtni
  uzmasin; tirik biriktirma ko'rsatgan o'chirilgan jadval o'qish ekranini
  yiqitmasin). Qayta hisoblash yuklovchisi ham shunday qilmasa, o'sha
  kamdan-kam holatda saqlangan va hosil qilingan katak farq qilishi mumkin.
- **F1 / `./bonvi gen` — qo'lda yozilgan tiplardan farqlar:** taxtada
  `asOf` (server vaqti, «yangilandi HH:mm» uchun); xulosa qatorida
  `photoKey` (xodim rasmi); chetlanish va xulosada `employeeName` endi
  `null` bo'lmaydi; tabel javobida `pageCount`. Rasm kaliti qoidasi SPEC
  bo'yicha: faqat o'tishda `photoKey` — turniket surati, `employeePhotoKey`
  — xodim kartasi.
- **T.** Handler darajasidagi tekshiruvlar (davr > 92 kun → `davr_uzun`,
  `from > to` → `davr_notogri`, 13-oy → `oy_notogri`, `kind` yo'q →
  `hisobot_turi_notogri`, eksport chegarasi → `eksport_juda_kop`) hali
  yozilmagan — ular endpoint bilan birga (B6/T) mazmunliroq. RBAC (§11.1
  #75–77) ham T da.

## B3 (qurilma hodisalarini qabul qilish) — C3 tasdiqlangan

Qabul yo'li ishlaydi: `POST /api/attendance/ingest/{token}` va
`POST /api/attendance/ingest` (`X-Bonvi-Device-Token`). Quyidagilar
keyingi qismlarga qoldi.

### B6 (boshqaruv use-case'lari) uchun

- **`Application/Common/DependencyInjection.cs` dagi
  `services.AddAttendanceIngest()` qatorini `services.AddAttendance()` bilan
  ALMASHTIRING**, `AddAttendance()` esa `AddAttendanceIngest()` ni ichidan
  chaqirsin. Ikkalasi qolsa ham yiqilmaydi (`TryAdd*`), lekin ikki joydan
  ro'yxatga olish keyingi o'quvchini chalg'itadi.
- **Token FAQAT `AttendanceDeviceToken` orqali:** `Generate()` (javobda bir
  marta ko'rsatiladi), `Hash(token)` → `token_hash`, `PrefixOf(token)` →
  `token_prefix`. Boshqacha xeshlash (masalan base64url dan ochib) — hech bir
  qurilma o'tmaydi.
- **Moslik yaratilganda, o'chirilganda va import tasdiqlanganda —
  SAQLASHDAN KEYIN `AttendanceIdentityCache.Evict(provider, rawPersonId)`.**
  Aks holda uzilgan moslik 60 soniya davomida o'tishlarni eski xodimga
  yozadi.
- **HMAC siri** `ISecretProtector.Protect` bilan shifrlab saqlanadi; qabul
  uni `Unprotect` qilib, UTF-8 baytlarini HMAC-SHA256 kaliti sifatida
  ishlatadi. Imzo: `X-Bonvi-Signature: v1=<64 ta kichik hex>` xom tana ustida.
- **`allowed_ips` saqlashda tekshirilsin** (CIDR yoki yakka IPv4/IPv6,
  vergul bilan). Qabul o'qib bo'lmaydigan elementni «hech narsani qamramaydi»
  deb oladi, ya'ni xato yozilgan ro'yxat qurilmani JIMGINA qulflaydi.
- **`IAttendanceReadModel`** Infrastructure'dagi yangi `AddAttendanceServices`
  blokiga qo'shiladi (repozitoriylar allaqachon o'sha yerda).
- Qurilmalar sahifasi «terminal ulangan, lekin hodisalari rad etilyapti»
  holatini `last_seen_at` va `last_event_at` farqidan ko'rsata oladi: rad
  etilgan tana (400) `last_seen_at` ni yozadi, `last_event_at` ni emas.

### B7 (fon ishlari) uchun

- ⚠️ **Yetim suratlarni `attendance/**` ostidan supurish HOZIRGI kalit
  sxemasida MUMKIN EMAS.** `LocalFileStorage` kalitni `yyyy/MM/<guid>.<ext>`
  qilib yasaydi va o'tish surati xodim rasmi hamda hujjatlar bilan BIR
  papkada yotadi. «Hech bir o'tish havola qilmagan fayl» deb o'chirish xodim
  rasmlari va shartnoma skanlarini o'chirib yuborardi. Yo `IFileStorage` ga
  papka/prefiks qo'shiladi (va `PassPhotoStore` uni ishlatadi), yo yetim
  supurish tashlab ketiladi. Qabul yo'lining o'zi yetim qoldirmaydi: takror
  va DB nosozligida surat shu so'rovning o'zida o'chiriladi — yetim faqat
  jarayon aynan fayl bilan qator orasida yiqilsa paydo bo'ladi.
- Surat tozalash `photo_key` ni `IFileStorage.DeleteAsync` bilan o'chiradi —
  kalit yuqoridagi shaklda, prefiksiz.

### W (tarjimalar) uchun

- B3 chiqaradigan kodlar (hammasi SPEC §6.9 da bor, yangi kod YO'Q):
  `davomat.ingest_ruxsat_yoq` (401), `davomat.qurilma_faolsiz` (403),
  `davomat.hodisa_oqilmadi` (400), `davomat.hodisa_kop` (400); qayta
  ishlatilganlari — `davomat.vaqt_yaroqsiz`, `davomat.shaxs_raqami_bosh`.
  Bu javoblarni faqat qurilma ko'radi, lekin `./bonvi check` hammasini talab
  qiladi.

### T (testlar) uchun

- Yozilgan: `AttendanceIngestApiTests` (10) + `IngestTokenScrubberTests` (6) —
  §11.1 #1, #2, #3, #9, #11, #14, #15, #16, #19, #20, #22, #24 (saqlash
  qismi) va 413 tanasi.
- Qolgani: #4–#7 (imzo, IP ro'yxati, ikki marshrut bir xil qator), #12–#13
  (boshqa qurilma; `serialNo` siz bir soniya ichida/farqida), #17 (rasm
  emas), #18 (siljishsiz Hikvision vaqti), #21, #23, #10 (`/passes` JWT siz
  yopiq — B6 endpointi kerak).
- `SecurityTests.Tezlik_siyosati_nomlari_bir_biridan_farq_qiladi`
  ro'yxatiga `RateLimitPolicies.Ingest` qo'shilsin.
- ⚠️ Konsol logini ushlaydigan test faqat «token YO'Q» ni tekshiradi:
  parallel xostlar statik `Log.Logger` ni bo'lishgani uchun «yashirilgan yo'l
  BOR» tekshiruvi to'liq to'plamda beqaror bo'lardi. Ijobiy tomon
  `IngestTokenScrubberTests` da.

### Dev bazada qoldirilgan sinov ma'lumoti (C6 uchun ataylab)

- Qurilma `0199a1b2-0000-7000-8000-00000000c3c3` («C3 sinov — kirish
  turniketi», Hikvision, Verifix raqamlari) va uning bitta BOG'LANMAGAN
  o'tishi — `raw_person_id = 10427`, surati bilan. SPEC §10.4 C6 aynan shu
  o'tishni moslik yaratilgach bog'lanishini tekshiradi. Token bazada yo'q
  (faqat xesh); kerak bo'lsa qurilmaning tokeni B6 ning `rotate-token`
  endpointi bilan almashtiriladi.

## B5 hisobotidan koordinator yozuvlari (2026-09-17)

- **W — yana 4 ta xato kodi:** `davomat.otish_topilmadi`,
  `davomat.oy_notogri`, `davomat.hisobot_turi_notogri`,
  `davomat.eksport_juda_kop` (umumiy `eksport.juda_kop` tarjimasida
  «5000 ta» qattiq yozilgan, bu yerda chegara boshqa).
- **F1 almashtiruvi uchun yana 2 maydon:** taxtada `asOf`, xulosa
  qatorida `photoKey`.
- `AttendanceRules.FormerEmployeeAlertHours` (24) va
  `DefaultPassRetentionDays` (365) qo'shildi; `SystemSettingKeys`
  endi 365 ni shu konstantadan o'qiydi — sukut qiymati bitta joyda.
- `AttendanceCalendar` (bo'sh kataklarni to'ldirish) hisob mexanizmi
  kodiga o'tkazilmoqda — saqlangan va to'ldirilgan katak bir xil kod
  bilan hosil bo'lishi uchun.

## B3 hisobotidan koordinator qarori (2026-09-17)

- **B7 — yetim suratlarni supurish v1 dan OLIB TASHLANDI.** O'tish
  suratlari xodim rasmlari va hujjatlari bilan bir `yyyy/MM/` jildida;
  u yerni supurish HR fayllarini o'chirishi mumkin. Tozalash faqat
  qatordagi `photo_key` bo'yicha ishlaydi. Keyinchalik kerak bo'lsa —
  avval `IFileStorage` ga prefiks qo'shiladi.
- Hikvision `subEventType` 76 — yuz tekshiruvi MUVAFFAQIYATSIZ (SPEC
  «barmoq izi» degan edi); barmoq izi bilan o'tish — 38. B3 shunday
  qurdi, SPEC §3.4 ga mos ravishda tuzatilishi kerak.

## F2+F3 — yangi tarjima kalitlari

SPEC §8.8 da YO'Q, lekin F2 (taxta, jurnal, surat oynasi) va F3 (tabel,
hisobotlar) ishlatadi. W to'rt tilga yozadi; quyida mo'ljallangan
o'zbekcha (lotin) matn. `{{…}}` — parametr nomi aynan shunday.

⚠️ **§8.8 dagi `emptyTitle`/`emptyText` uch guruhda (taxta, jurnal, tabel)
takrorlangan** — bitta `attendance` blokida ular bitta kalit bo'lib qoladi.
Shuning uchun `attendance.emptyTitle`/`emptyText` FAQAT taxtada («Hozir hech
kim ichkarida emas»), jurnal va tabel esa o'z kalitlarini ishlatadi
(`passesEmpty*`, `timesheetEmpty*`).

| Kalit | uz-Latn |
|---|---|
| `attendance.allPasses` | Barcha o‘tishlar |
| `attendance.arrived` | Kelganlar |
| `attendance.boardUnknownDirectionNote` | Yo‘nalishni aytmaydigan qurilmalardagi o‘tishlar bu taxtaga kirmaydi — ular jurnalda va tabelda bor. |
| `attendance.codePresent` | I |
| `attendance.codeAbsent` | K |
| `attendance.codeDayOff` | D |
| `attendance.codeDayOffWorked` | DI |
| `attendance.codeMissingExit` | ChY |
| `attendance.codeMissingEntry` | KY |
| `attendance.codeNoSchedule` | JY |
| `attendance.currentMonth` | Joriy oy |
| `attendance.date` | Sana |
| `attendance.dayOffWorkedDays` | Dam olish kuni ishlagan |
| `attendance.elapsed` | ichkarida {{duration}} |
| `attendance.feedTitle` | So‘nggi o‘tishlar |
| `attendance.formerEmployeeAlertsFailed` | Ishdan ketganlar ogohlantirishini tekshirib bo‘lmadi |
| `attendance.lateBy` | {{n}} daq. kechikdi |
| `attendance.lateHint` | Hali kelmaganlar bilan birga |
| `attendance.matchStatus` | Moslik |
| `attendance.minutes` | {{n}} daq. |
| `attendance.nextMonth` | Keyingi oy |
| `attendance.notComputedYet` | Bu sahifada hali hisoblangan kun yo‘q |
| `attendance.notFoundText` | Filtr yoki sana oralig‘ini o‘zgartirib ko‘ring |
| `attendance.onTimeHint` | Kutilganlardan kechikmaganlar ulushi |
| `attendance.onTimeRate` | Vaqtida |
| `attendance.openEmployee` | Xodim kartasi |
| `attendance.passesEmptyText` | Qurilma ulangan bo‘lsa, o‘tishlar shu yerda paydo bo‘ladi. Boshqa kunni ko‘rish uchun sanani o‘zgartiring. |
| `attendance.passesEmptyTitle` | Bugun hali o‘tish yo‘q |
| `attendance.passesSearchPlaceholder` | F.I.Sh. yoki qurilmadagi raqam… |
| `attendance.passesSubtitle` | {{n}} ta o‘tish |
| `attendance.plannedDays` | Rejadagi kunlar |
| `attendance.prevMonth` | Oldingi oy |
| `attendance.profilePhotoLabel` | Xodim kartasidagi rasm |
| `attendance.reportEmptyText` | Davr yoki filtrlarni o‘zgartirib ko‘ring |
| `attendance.reportEmptyTitle` | Bu davrda yozuv yo‘q |
| `attendance.reportsSourceNote` | Hisobotlar hisoblangan kunlardan tuziladi: yangi o‘tish bir necha daqiqada tushadi. |
| `attendance.skewMinutes` | Qurilma soati {{n}} daqiqaga farq qilgan |
| `attendance.snapshotLabel` | Turniket surati |
| `attendance.timesheetEmptyText` | Tanlangan oy va filtrlarda xodim topilmadi |
| `attendance.timesheetEmptyTitle` | Bu oy uchun tabel bo‘sh |
| `attendance.viewFeed` | O‘tishlar |
| `attendance.viewInside` | Ichkaridagilar |
| `attendance.worked` | Ishlangan |

**§8.8 kalitlarining parametrlari** (matnni yozganda nomlar aynan shunday
bo'lsin, aks holda `{{…}}` ekranda xom chiqadi):
`formerEmployeeAlertTitlePlural` {{n}} · `formerEmployeeTerminatedOn` {{date}} ·
`formerEmployeePassCount` {{n}} · `refreshedAt` {{time}} · `clockWarning` {{n}}
(qurilmalar soni) · `since` {{time}} · `lastComputedAt` {{time}} (SANA + soat —
o'tgan oy ham ko'riladi) · `recomputeStarted` {{employees}} {{days}} ·
`recomputeRange` {{from}} {{to}} · `breakDeducted` {{deducted}} ·
`breakTakenPart` {{taken}}.

**Boshqa nom maydonlaridan qayta ishlatilganlar** (yangi kalit kerak emas):
`common.*` (all, dateFrom, dateTo, empty, filters, loading, retry, search,
searchPlaceholder), `employee.*` (department, legalEntity, position, status),
`employees.*` (anyDepartment, anyLegalEntity, anyPosition, clearFilters,
exportFailed, exportTooOften, exporting, loadFailed, viewLabel), `photo.none`,
`nav.verification` (jurnaldagi yuz juftligi ustunining sarlavhasi).

## F2+F3 — jonli backend bilan tekshirilishi kerak

Backend endpointlari F2/F3 yozilayotganda hali yo'q edi; hammasi §6 va B5
DTO'lariga qarab tiplangan. Quyidagilar faqat ishlayotgan server bilan
tasdiqlanadi.

- **F1 tiplariga qo'shilgan maydonlar** (`./bonvi gen` almashtiruvida
  generatsiya qilingan modelda BOR bo'lishi shart): `AttendancePass.skewWarning`,
  `FormerEmployeeAlert.passCount`, `AttendanceBoard.asOf`,
  `TimesheetCell.breakTakenMinutes` / `breakDeductionMinutes`,
  `Timesheet.pageCount`, `AttendanceSummaryRow.photoKey`; yangi chaqiruvlar
  `fetchPass` (`GET /passes/{id}`) va `fetchFormerEmployeeAlerts`
  (`GET /former-employee-alerts` → `{ count, items }`).
- **`useFormerEmployeeAlerts` endi TAXTANI emas, `/former-employee-alerts` ni
  so'raydi** (imzo o'zgarmadi — massiv qaytaradi). Yangi
  `useFormerEmployeeAlertsQuery` to'liq holat bilan. Kalit
  `['attendance','former-employee-alerts']` — W ning qizil nav nishoni va
  taxtadagi qizil blok BITTA kesh yozuvini bo'lishadi; nishon uchun shu
  hook'ni `enabled: hasPermission('attendance.view')` bilan chaqiring.
- **Oqimdagi «N daq. kechikdi» belgisi:** `GET /reports/exceptions?kind=Late`
  (bugun, `pageSize=200`) qatorining `firstInAt` i o'tishning `occurredAt`
  i bilan AYNAN bir lahza bo'lishi kerak (`first_in_at` o'tishdan
  ko'chiriladi). Server lahzani o'zgartirib yozsa (masalan soniyani
  qirqsa) — belgi hech qachon chiqmaydi. Bir kunda 200 dan ortiq
  kechikkan bo'lsa, ortig'i belgisiz qoladi.
- **`?passId=` havolasi:** o'chirilgan o'tishda 404 tanasida
  `title: "davomat.otish_topilmadi"` — oyna aynan shu tarjimani
  ko'rsatadi.
- **Qayta hisoblash tugmasi (tabel, `attendance.manage`):** oy uchun
  `from = 1-sana`, `to = min(oy oxiri, bugun)`; javob `202` va
  `{ jobId, employeeCount, dayCount }`.
- **Tabel ko'rinishi brauzerda:** qotirilgan xodim/jami ustunlari va
  sarlavha qatori `max-h-[70dvh]` quti ichida (Chrome + Safari, tungi
  rejim); chetdagi kataklarda oynacha qirqilmasligi; 25 × 31 katakdagi
  775 ta Radix ildizining tezligi.
- **Turniket surati o'lchami:** katta oynada `object-contain` — Hikvision
  kadrining haqiqiy nisbatida yuz kesilmasligini ko'z bilan tasdiqlash.
- **Eksport fayl nomlari** `Content-Disposition` dan o'qiladi (§6.7).
- **Qurilmalar sahifasiga havola YO'Q:** taxtadagi soat banneri va
  «qurilma ulanmagan» bo'sh holati hozircha faqat matn — `qurilmalar`
  marshruti F2 yozilayotganda yo'q edi. W: `attendance.manage` egasiga
  `/verifikatsiya/qurilmalar` havolasini qo'shish ixtiyoriy.
- **`isSkewWarning` (`attendance-presentation.ts`) endi hech kim
  ishlatmaydi** va u 300 soniyani klientda qayta hisoblaydi — `./bonvi gen`
  almashtiruvida o'chirilsin (F5 qurilma kartasi `skewWarning` ni DTO'dan
  o'qiydi).

## F2+F3 hisobotidan koordinator yozuvlari (2026-09-17)

- **Kechikish belgisi o'tish qatorida — vaqtinchalik yechim.** Sahifa
  hozir «Kechikkanlar» hisobotidagi `firstInAt` ni o'tish vaqti bilan
  solishtirib topadi (200 qator chegarasi bilan). B6 ga o'tish DTO'siga
  `lateMinutes: int?` qo'shish so'raldi (faqat kunning birinchi
  kirishida). Qo'shilsa — gen almashtiruvida sahifa shu maydonga
  o'tkaziladi va solishtirish kodi o'chiriladi.
- **Brauzerda tekshirish (test-e2e / qo'lda):** tabel sahifasida 25
  xodim × 31 kun = 775 ta popover ildizi. Sekinlik sezilsa — bitta
  umumiy, tanlangan katakka bog'langan popover'ga o'tkaziladi.
- `isSkewWarning` (klientda 300 s ni qayta hisoblaydi) — endi
  ishlatilmaydi, gen almashtiruvida o'chiriladi.


## F4+F5 (moslashtirish, import, jadvallar, qurilmalar) — typecheck va build yashil

Sahifalar: `/verifikatsiya/moslashtirish`, `/verifikatsiya/jadvallar`,
`/verifikatsiya/qurilmalar`. Menyu va tarjimalar — W.

### F1 tiplariga kiritilgan o'zgarishlar

- `ScheduleResolution` — §6.4 ning to'liq maydonlari: `employeeName`,
  `employmentId`, yuridik shaxs / bo'lim / lavozim id+nom,
  `matchedDepartmentId/Name` (faqat `scope = Department` da o'qiladi),
  `day: WorkScheduleDay | null`.
- `toDeviceCreated` — javob endi ICHMA-ICH `{ device, token }` deb o'qiladi
  (§6.2 AMENDED). Avval yassi o'qilardi.
- `previewIdentityImport(file, provider)` + `usePreviewIdentityImport`
  mutatsiyasi endi `{ file, provider }` oladi; `provider` forma maydoni
  sifatida DOIM yuboriladi.
- Ikki eskirgan izoh tuzatildi (`WorkScheduleDayInput.breakStart`,
  `breakWindowCrossesMidnight`): klient endi oynani oldindan tekshiradi.
- Yangi fayllar (F1 ga tegishli emas): `lib/attendance-schedule-rules.ts`
  (domen ko'zgusi, `scheduleOutcome`), `lib/attendance-manage-presentation.ts`
  (qurilma/moslik yorliqlari, `EMPLOYEE_PHOTO_PROBE`),
  `components/attendance-employee-picker.tsx` (rasmli xodim tanlash),
  `components/attendance-manage-states.tsx` (ruxsat yo'q / yuklab bo'lmadi).

### Jonli backend bilan tekshirilishi kerak (B6 hali yo'q edi)

1. **`POST /devices` va `POST /devices/{id}/rotate-token`** javobi aynan
   `{ device, token }`; token oynasidagi URL `buildIngestUrl` bilan
   (`VITE_API_URL` + `/api/attendance/ingest/{token}`) — prod'da Caddy
   ortidagi haqiqiy tashqi manzil chiqishini ko'z bilan tasdiqlash.
2. **`GET /schedules/resolve`** — `employmentId` / `scope` nulllari,
   `matchedDepartment*` ota bo'lim holatida, `day` ichidagi
   `breakStart/breakEnd`. Narvon (xodim → lavozim → bo'lim → hamma) shu
   maydonlardan chiziladi.
3. **`POST /identities/import`** `provider` forma maydonini qabul qiladi;
   `rows.length === totalRows`. ⚠️ **B6 ga taklif:** `ExactPinfl` /
   `ExactName` / `AlreadyMapped` qatorlarida taklif qilingan xodimni
   `candidates` ga ham qo'shing (`photoKey`, bo'lim, lavozim bilan) —
   klient uni allaqachon o'qiydi va tasdiqlashdan oldin YUZ ko'rsatadi.
   Hozir rasm «so'rab ko'riladi» (`EMPLOYEE_PHOTO_PROBE`).
4. **`ScheduleAssignmentDto` da xodim rasmi kaliti yo'q** — xodim
   biriktirmalarida rasm `/api/employees/{id}/photo` dan so'rab ko'riladi.
   B6: `photoKey` qo'shilsa, `EMPLOYEE_PHOTO_PROBE` o'rniga o'sha.
5. **`GET /identities/unmatched`** — `samplePassId` rasmi BOR o'tishga
   ishora qilishi shart: klient `/passes/{samplePassId}/photo` ni kalit
   tekshiruvisiz so'raydi (404 → yuz belgisi).
6. **`DELETE /schedules/{id}`** — «tirik biriktirma» qaysi to'plam? Klient
   `GET /schedule-assignments` (filtrsiz) dagi HAMMA biriktirmani (tugaganlari
   ham) sanaydi va o'chirish tugmasini o'chirib qo'yadi. Server faqat
   amaldagi/kelajakdagilarni sanasa, klient ortiqcha to'sadi — bir xil
   qilinsin. Rad javobi `davomat.jadval_biriktirilgan` (409).
7. **`PUT /devices/{id}`** — `allowedIps: null` «tozalash» deb olinadi
   (faqat `hmacSecret` uch holatli: `null` qoldirish, `""` tozalash, matn
   almashtirish). `allowedIps` noto'g'ri yozilsa B6 qaytaradigan kod §6.9
   da yo'q — u ham tarjima qilinishi kerak; forma uni tepadagi
   ogohlantirishda ko'rsatadi.
8. **Jadval tekshiruvi mosligi:** 20:00→08:00 smena + 00:30–01:30 oyna
   (to'g'ri), 07:30–08:30 (xato, `jadval_tanaffus_oynasi`), faqat
   `breakStart` (`jadval_tanaffus_toliq_emas`), 09:00→09:00
   (`jadval_vaqt_teng`) — klient va server bir xil javob berishini
   tekshirish.
9. **Ruxsatlar yonma-yonligi:** xodim tanlash `GET /api/employees`
   (`employees.view`), bo'lim/lavozim/firma ro'yxatlari
   `catalog.view` talab qiladi. Faqat `attendance.manage` bor foydalanuvchida
   ular 403 bo'lib, tanlov bo'sh/xato chiqadi — HrManager rolida
   hammasi borligini tasdiqlash.
10. **`GET /devices?includeInactive=`** — «Faol / Hammasi» tabi shu
    parametr bilan; `lastSeenAt` bor, `lastEventAt` 30 daqiqadan ortiq
    orqada bo'lsa «hodisalar rad etilyapti» izohi chiqadi (B3 izohi).

### W uchun: §8.8 kalitlari — interpolatsiya nomlari (aynan shunday)

| Kalit | O'zgaruvchilar | Mo'ljallangan matn (uz-Latn) |
|---|---|---|
| `attendance.weekSummary` | `{{days}}`, `{{hours}}` | Haftada {{days}} kun · {{hours}} soat |
| `attendance.importConfirm` | `{{n}}` | Tasdiqlash ({{n}} ta) |
| `attendance.unlinkConfirm` | `{{personId}}`, `{{name}}` | {{personId}} raqami {{name}} dan uziladi. |
| `attendance.breakWindow` | `{{range}}` | Tanaffus oynasi: {{range}} |
| `attendance.formerEmployeeShort` | `{{n}}` | So‘nggi 24 soatda ishdan ketgan {{n}} xodim o‘tdi |
| `attendance.resolvedBy` | `{{scope}}` | Yutgan daraja: {{scope}} |

Ma'nosi joyiga bog'liq §8.8 kalitlari: `unmatchedSubtitle` — «Qurilmada
bor, lekin xodimga bog‘lanmagan raqamlar. Bog‘lansa, eski o‘tishlari ham
tabelga tushadi.»; `breakWindowHint` — «Tanaffus ishlangan vaqtdan faqat
oyna belgilangan kunlarda ayiriladi — oyna ichida binoda bo‘lmagan vaqt
ayirilmaydi.»; `breakWindowNone` — «Tanaffus yo‘q»; `breakWindowNoneHint`
— «Oyna belgilanmagan — tanaffus ishlangan vaqtdan ayirilmaydi»;
`nextDay` — «ertasi kuni»; `tokenOnceWarning` — «Bu token boshqa
ko‘rsatilmaydi»; `rotateConfirm` — «Eski token shu zahoti ishlamay
qoladi: terminalga yangi manzil kiritilguncha o‘tishlar qabul
qilinmaydi.»; `deleteConfirm` (qurilma) — «Qurilma ro‘yxatdan
o‘chiriladi.»; `importDrop` — «Faylni shu yerga tashlang yoki tanlash
uchun bosing»; `provider` — «Hodisa formati»; `identityProvider` —
«Raqamlar manbai»; `directionRole` — «Yo‘nalish»; `active` — «Faol».

Boshqa modullardan QAYTA ishlatilgan kalitlar (yangi emas):
`employees.note`, `employee.department`, `employee.legalEntity`,
`employees.chooseDepartment`, `employees.choosePosition`,
`catalog.deactivate`, `common.*`. Xato kodlari (`davomat.*`) §6.9 dagi
ro'yxatdan — yangi kod qo'shilmadi.

## F4+F5 — yangi tarjima kalitlari

Hammasi `attendance.` ostida; qavsda interpolatsiya o'zgaruvchilari.

**Umumiy / vaqt**
- `agoNow` — hozirgina
- `agoMinutes` ({{n}}) — {{n}} daq. oldin
- `agoHours` ({{n}}) — {{n}} soat oldin
- `neverSeen` — Hali ulanmagan
- `openEnded` — muddatsiz
- `notSpecified` — Ko‘rsatilmagan
- `source` — Manba
- `copied` — Nusxalandi
- `copyFailed` — Nusxalab bo‘lmadi — matn belgilandi, Ctrl+C bosing
- `showPasses` — O‘tishlar

**Xodim tanlash**
- `employeeSearchPlaceholder` — Xodimni F.I.Sh. bo‘yicha qidiring
- `chooseAnother` — Boshqasini tanlash
- `pickerMore` ({{n}}) — Yana {{n}} ta — qidiruvni aniqlashtiring
- `searchOtherEmployee` — Boshqa xodimni qidirish
- `employeePhoto` — Xodim kartasidagi rasm
- `deviceSnapshot` — Turniket surati

**Moslashtirish**
- `mappingSubtitle` — Turniketdagi shaxs raqamlarini xodimlarga bog‘lang — bog‘lanmagan o‘tish tabelga tushmaydi
- `unmatchedEmptyTitle` — Barcha o‘tishlar xodimlarga bog‘langan
- `unmatchedEmptyText` — Yangi raqam paydo bo‘lsa, shu yerda yuzi bilan chiqadi.
- `passCountShort` ({{n}}) — {{n}} ta o‘tish
- `linkSubtitle` — Turniket suratini xodim rasmi bilan solishtiring
- `linkManual` — Qo‘lda bog‘lash
- `linkManualSubtitle` — Terminalga hali o‘tmagan odamni oldindan bog‘lash
- `linkRetroactiveHint` ({{n}}, {{from}}) — Bog‘langach, shu raqamning {{n}} ta eski o‘tishi ({{from}} dan beri) xodimga yoziladi va o‘sha kunlar tabeli avtomatik qayta hisoblanadi.
- `linkRetroactiveHintManual` — Bu raqam bilan avval qilingan o‘tishlar bo‘lsa, ular ham xodimga yoziladi va tabel avtomatik qayta hisoblanadi.
- `displayNameInDevice` — Qurilmadagi F.I.Sh.
- `displayNameInDeviceHint` — Ixtiyoriy — keyin solishtirish uchun
- `identitiesCount` ({{n}}) — {{n}} ta moslik
- `identitiesSearchPlaceholder` — Shaxs raqami yoki xodim F.I.Sh.
- `identitiesEmptyTitle` — Hali moslik yo‘q
- `identitiesEmptyText` — Verifix ro‘yxatini yuklang yoki raqamlarni birma-bir bog‘lang.
- `inDeviceAs` ({{name}}) — Qurilmada: {{name}}
- `linkedAt` — Bog‘langan
- `sourceImport` — Import
- `sourceManual` — Qo‘lda
- `unlinkHint` — Eski o‘tishlar shu xodimda qoladi. Bog‘lanish umuman xato bo‘lgan bo‘lsa — to‘g‘risini yarating va davrni qayta hisoblang.
- `identityProviderManual` — Qo‘lda kiritilgan

**Import**
- `importSubtitle` — Verifix’dan eksport qilingan xodimlar ro‘yxati (.xlsx yoki .csv)
- `importProviderHint` — Qurilmalardagi raqamlar qaysi tizimdan berilgan
- `importLimitsHint` ({{rows}}, {{mb}}) — Eng ko‘pi {{rows}} qator, {{mb}} MB
- `importFileSize` ({{size}}) — Hajmi: {{size}}
- `importRowsRead` ({{n}}) — {{n}} qator o‘qildi
- `importAnotherFile` — Boshqa fayl
- `importTally` ({{send}}, {{skip}}, {{unresolved}}) — Bog‘lanadi: {{send}} · O‘tkaziladi: {{skip}} · Qaror kutmoqda: {{unresolved}}
- `importSkipUnresolved` ({{n}}) — Qolgan {{n}} tasini o‘tkazib yuborish
- `importUnresolvedHint` ({{n}}) — {{n}} ta qatorda xodim tanlanmagan — tanlang yoki o‘tkazib yuboring.
- `importEmptyFile` — Faylda ma’lumot qatori topilmadi
- `importFilterAttention` ({{n}}) — Qaror kerak ({{n}})
- `importFilterAll` ({{n}}) — Hammasi ({{n}})
- `importNothingToDecide` — Qaror talab qiladigan qator yo‘q
- `importRowNumber` ({{n}}) — {{n}}-qator
- `importNoNameInFile` — Faylda F.I.Sh. yo‘q
- `pinflInFile` ({{pinfl}}) — JSHSHIR: {{pinfl}}
- `importAlreadyMappedTo` ({{name}}) — Allaqachon bog‘langan: {{name}}
- `importDecision` — Qaror
- `importDecisionConfirm` — Tasdiqlash
- `importDecisionChoose` — Xodimni tanlash
- `importWillSkip` — Bu qator o‘tkazib yuboriladi
- `importCandidates` — Mos kelishi mumkin bo‘lgan xodimlar
- `importCreated` — Bog‘landi
- `importSkipped` — O‘tkazildi
- `importFailed` — Xato
- `importFailedTitle` — Bog‘lanmagan qatorlar
- `importRetroactiveHint` — Har bir yangi moslik uchun eski o‘tishlar xodimga yoziladi va tabel avtomatik qayta hisoblanadi.
- `importAfterHint` — Qayta hisoblash fonda bajariladi — tabel bir necha daqiqada yangilanadi.

**Jadvallar**
- `schedulesSubtitle` ({{n}}) — {{n}} ta jadval
- `scheduleCreate` — Yangi jadval
- `scheduleCreateTitle` — Yangi ish jadvali
- `scheduleEditTitle` — Jadvalni tahrirlash
- `scheduleInactive` — Nofaol
- `schedulesEmptyTitle` — Ish jadvali yo‘q
- `schedulesEmptyText` — Jadvalsiz kunlar tabelda «Jadval yo‘q» bo‘lib qoladi — birinchi jadvalni yarating.
- `graceHint` — Shu daqiqagacha kechikish va erta ketish hisoblanmaydi (0–120)
- `graceSummary` ({{in}}, {{out}}) — imtiyoz {{in}}/{{out}} daq.
- `copyMondayToWeekdays` — Dushanbani seshanba–jumaga nusxalash
- `dayPlanned` ({{hours}}) — Reja: {{hours}}
- `clearBreakWindow` — Oynani tozalash
- `breakWindowNoneShort` — oynasiz
- `noBreak` — tanaffussiz
- `scheduleNotAssigned` — Hech kimga biriktirilmagan
- `assignmentCount` ({{n}}) — {{n}} ta biriktirma
- `scheduleDeleteTitle` — Jadvalni o‘chirish
- `scheduleDeleteConfirm` — Jadval o‘chiriladi. Hisoblangan tabel kunlari o‘zgarmaydi.
- `scheduleAssignedTitle` — Jadval biriktirilgan
- `scheduleAssignedText` ({{n}}) — Bu jadvalga {{n}} ta biriktirma bog‘langan — avval ularni o‘chiring. Biriktirilgan jadvalni o‘chirib ham, faolsizlantirib ham bo‘lmaydi.

**Biriktirmalar va «kim uchun amal qiladi»**
- `assignmentCreate` — Biriktirish
- `assignmentCreateTitle` — Jadvalni biriktirish
- `assignTo` — Kimga
- `schedule` — Jadval
- `chooseSchedule` — Jadvalni tanlang
- `scopePrecedenceHint` — Ustunlik: xodim → lavozim → bo‘lim (ota bo‘lim ham) → hamma uchun
- `scopePositionHint` — Lavozim bo‘limdan ustun: qo‘riqchi qaysi bo‘limda bo‘lmasin, qo‘riqchi smenasida
- `scopeDepartmentHint` — Ichki bo‘limlarga ham tarqaladi, agar ularning o‘z biriktirmasi bo‘lmasa
- `scopeDefaultHint` — Boshqa hech qanday biriktirmasi yo‘q barcha xodimlarga amal qiladi
- `scopeDefaultTarget` — Hamma uchun
- `effectiveToHint` — Bo‘sh — muddatsiz
- `assignmentsAll` — Hammasi
- `assignmentsInForce` — Bugun amalda
- `assignmentInForce` — Amalda
- `assignmentFuture` — Kelajakda
- `assignmentEnded` — Tugagan
- `assignmentsEmptyTitle` — Biriktirma yo‘q
- `assignmentsEmptyText` — Jadval biriktirilmaguncha xodimlarning kunlari hisoblanmaydi.
- `assignmentsNoneInForce` — Bugun amalda bo‘lgan biriktirma yo‘q
- `assignmentDeleteTitle` — Biriktirmani o‘chirish
- `assignmentDeleteConfirm` — Biriktirma o‘chiriladi — bu xodimlarga jadval keyingi darajadan olinadi.
- `resolvePreviewSubtitle` — Xodim va sanani tanlang — qaysi jadval va qaysi daraja orqali tushishi ko‘rinadi
- `resolveDate` — Sana
- `resolvePickEmployee` — Xodimni tanlang
- `resolvedSchedule` ({{date}}) — {{date}} dagi jadval
- `resolveNotEmployedTitle` — Ishlamagan
- `resolveNotEmployed` ({{date}}) — Xodim {{date}} da hech bir firmada ishlamagan — bu kun tabelda hisoblanmaydi.
- `resolveNoScheduleTitle` — Jadval topilmadi
- `resolveNoSchedule` ({{date}}) — {{date}} uchun hech bir biriktirma mos kelmadi — kun «Jadval yo‘q» bo‘lib qoladi.
- `resolveInheritedTitle` — Ota bo‘limdan meros
- `resolveInherited` ({{matched}}, {{own}}) — Jadval «{{matched}}» bo‘limidan kelgan: «{{own}}» bo‘limining o‘z biriktirmasi yo‘q.
- `resolveEmployment` ({{context}}) — Mehnat munosabati: {{context}}
- `tierWon` — Yutdi
- `tierNoMatch` — mos kelmadi
- `tierNotChecked` — ko‘rilmadi

**Qurilmalar**
- `devicesSubtitle` ({{n}}) — {{n}} ta qurilma
- `deviceCreate` — Qurilma qo‘shish
- `deviceCreateTitle` — Yangi qurilma
- `deviceCreateSubtitle` — Saqlangach, terminalga kiritiladigan manzil bir marta ko‘rsatiladi
- `deviceEditTitle` — Qurilmani sozlash
- `devicesActiveOnly` — Faol
- `devicesAll` — Hammasi
- `devicesEmptyTitle` — Qurilma hali ulanmagan
- `devicesEmptyAllTitle` — Qurilma yo‘q
- `devicesEmptyText` — Turniketni ro‘yxatga oling va berilgan manzilni uning HTTP Listening sozlamasiga kiriting.
- `devicesClockWarningTitle` ({{n}}) — {{n}} ta qurilmaning soati adashgan
- `devicesClockWarningText` — Terminal soatini tarmoq vaqtiga (NTP) sozlang — o‘tishlar baribir qabul qilinadi, lekin vaqti siljigan.
- `clock` — Soat
- `clockOk` — Aniq
- `skewSeconds` ({{n}}) — {{n}} soniya
- `skewMinutes` ({{n}}) — {{n}} daqiqa
- `eventsRejected` — Ulangan, lekin hodisalar qabul qilinmayapti
- `deviceNameHint` — Masalan: Markaziy ofis — kirish turniketi
- `locationHint` — Masalan: 1-qavat, chap turniket
- `providerGeneric` — Umumiy JSON
- `providerHint` — Hikvision terminallari uchun — Hikvision
- `identityProviderHint` — Terminaldagi shaxs raqamlarini kim bergan — mosliklar shu bo‘yicha qidiriladi
- `roleReportsHint` — Terminal kirish/chiqishni o‘zi aytadi
- `roleEntryOnlyHint` — Bu terminaldagi har bir o‘tish — kirish
- `roleExitOnlyHint` — Bu terminaldagi har bir o‘tish — chiqish
- `deviceSectionBasics` — Qurilma
- `deviceSectionLocation` — Joylashuv
- `deviceSectionEvents` — Hodisalar
- `deviceSectionSecurity` — Xavfsizlik
- `authModeToken` — Token
- `authModeTokenAndHmac` — Token + imzo
- `authModeTokenHint` — Hikvision terminallari uchun yagona mumkin bo‘lgan usul
- `authModeTokenAndHmacHint` — Faqat imzo qo‘ya oladigan vositachi dastur uchun (X-Bonvi-Signature)
- `hmacSecret` — Imzo siri
- `hmacSecretHint` — Majburiy. Saqlangach qayta ko‘rsatilmaydi
- `hmacSecretStored` — Imzo siri kiritilgan
- `hmacSecretWillClear` — Imzo siri o‘chiriladi
- `hmacSecretReplace` — Almashtirish
- `hmacSecretClear` — O‘chirish
- `hmacSecretKeep` — Bekor qilish
- `hmacSecretNew` — Yangi imzo siri
- `deactivateTitle` — Qurilmani faolsizlantirish
- `deactivateConfirm` — Faolsiz qurilmaning o‘tishlari qabul qilinmaydi va tabelga tushmaydi.
- `deviceDeleteTitle` — Qurilmani o‘chirish
- `deviceHasPassesHint` — O‘tishlar jurnali qaysi terminaldan kelganini yo‘qotmasligi kerak — qurilmani faolsizlantiring.
- `tokenLabel` — Token
- `tokenOnceWarningText` — Hozir nusxalab terminalga kiriting. Yo‘qolsa — tokenni almashtirish kerak bo‘ladi.
- `tokenRotatedHint` — Eski token o‘chirildi: terminalga yangi manzil kiritilguncha o‘tishlar qabul qilinmaydi.
- `ingestUrlHint` — Hikvision: Tarmoq → Kengaytirilgan → HTTP Listening → manzil
- `tokenHeaderHint` — Sarlavha orqali yuboradigan qurilmalar uchun: X-Bonvi-Device-Token
- `tokenSavedClose` — Saqladim, yopish

## B6 (boshqaruv use-case'lari + endpointlar) — C6 tasdiqlangan

`Api/Endpoints/AttendanceEndpoints.cs` — qabuldan tashqari HAMMA endpoint
(§6.2–§6.7, 30 ta yo'l), `AddAttendance()` (qabul yo'lini ichidan chaqiradi),
`IBackgroundJobQueue` + sinxron `InlineBackgroundJobQueue`, qayta hisoblash
yadrosi `AttendanceDayRecomputer`. C6 dev API da: `POST /devices` → `POST
/identities` (10427 → Абдуқодиров Шерзод) → o'tish bog'landi, `processed_at`
qo'yildi, 17.09 kun yozuvi paydo bo'ldi, `GET /timesheet` katagi
`firstInAt` bilan; `/recompute` chaqirilmagan. `/openapi/v1.json` — 200.
`./bonvi test` — 1675/1675.

### B7 (fon ishlari) uchun

- **Navbat almashtiriladi, port o'zgarmaydi.** `Infrastructure/DependencyInjection.cs`
  → `AddAttendanceServices` dagi `AddScoped<IBackgroundJobQueue, InlineBackgroundJobQueue>()`
  qatori Hangfire serverini yoqadigan AYNAN o'sha shart bilan almashtiriladi
  (`Testing` — sinxron, qolgani — Hangfire). ⚠️ Port SPEC dagidan farq qiladi:
  `Task<string> EnqueueAsync<TJob>(Expression<Func<TJob, Task>>)` — vazifalar
  asinxron va `/recompute` javobiga `jobId` kerak. Hangfire varianti:
  `Task.FromResult(client.Enqueue(job))`.
- **Tayyor vazifa sinflari (Application/Attendance/Recompute):**
  `AttendanceBackfillJob.RunAsync(provider, rawPersonId, employeeId, ct)` va
  `AttendanceRecomputeRangeJob.RunAsync(from, to, employeeId?, ct)` — DI da
  scoped. Iflos navbat va tungi hisoblash YANGI yozuvchi yaratmasin:
  `AttendanceDayRecomputer.RecomputeAsync(xodimlar, sanalar)` (yuklash —
  `IAttendanceComputationSource`, B4 shartnomasi va B5 bilan bir xil qoidalar;
  `ShouldStore == false` bo'lgan eski qatorni o'chiradi) + o'qilgan o'tishlar
  uchun `IAttendancePassRepository.MarkProcessedAsync`. Tungi:
  `AttendanceRecomputeRangeJob.RunAsync(bugun−2, bugun, null)`.
- **Poyga:** ikki vazifa bir (xodim, sana) ni BIR VAQTDA birinchi marta
  yozsa, ikkinchisi `ux_attendance_days_employee_date` ga urilib yiqiladi.
  Hangfire qayta urinishi natijani tiklaydi (hisoblash idempotent) — lekin
  sinxron navbatda bu so'rov 500 bo'ladi. `WorkerCount = 2` da kam uchraydi;
  kuzatilsa — upsert (`ON CONFLICT`) ga o'tkaziladi.
- **To'ldirishdagi tor oyna:** qabul moslik saqlanishidan OLDIN o'qib, saqlangandan
  KEYIN yozgan o'tish bog'lanmay qoladi (to'ldirish uni ko'rmaydi). Iflos
  navbat uni ushlamaydi (`resolved_employee_id IS NULL`). Kerak bo'lsa —
  tungi «moslanmagan, lekin mosligi bor» supurishi: `AttendanceBackfillJob`
  ni o'sha raqamlar uchun qayta chaqirish yetadi (idempotent).
- **Import tasdig'i** har yaratilgan moslik uchun alohida to'ldirish vazifasi
  qo'yadi; sinxron navbatda 540 qatorlik birinchi import bitta so'rovda
  ~540 × 8 so'rov. Hangfire kelgach bu fonga o'tadi — sinxronni prod'da
  qoldirmang.

### W (tarjimalar) uchun

- **YANGI kodlar (SPEC §6.9 da yo'q):** `davomat.surat_yoq` (404 — o'tish bor,
  surati yo'q/tozalangan), `davomat.ip_royxati_notogri` (400 — qurilma IP
  ro'yxati o'qilmaydi), `davomat.shaxs_raqami_uzun` (400 — 64 belgidan uzun),
  `davomat.jadval_faolsiz` (409 — faolsiz jadvalni biriktirib bo'lmaydi).
- **SPEC dagi, endi chiqariladigan B6 kodlari:** `qurilma_topilmadi`,
  `qurilma_nom_takror`, `qurilma_seriya_takror`, `qurilma_otishlari_bor`,
  `moslik_topilmadi`, `moslik_takror`, `xodim_topilmadi` (404),
  `import_ustun_topilmadi`, `import_fayl_turi`, `import_fayl_katta`,
  `import_qator_kop`, `jadval_topilmadi` (404), `jadval_nom_takror`,
  `jadval_biriktirilgan`, `biriktirma_topilmadi`, `biriktirma_takror`,
  `saqlash_muddati_nomuvofiq` (hammasi `davomat.` bilan). Boshqa modulning
  qayta ishlatilganlari: `yuridik_shaxs.topilmadi`, `bolim.topilmadi`,
  `lavozim.topilmadi`.

### F1 / F5 (`./bonvi gen` almashtiruvi) uchun

- **§6.4 da yo'q ikki endpoint:** `POST /schedules/{id}/activate` va
  `/deactivate` (`attendance.manage`). Deaktivatsiya DELETE bilan bir xil
  qoida: tirik (muddati o'tgan ham) biriktirmasi bor bo'lsa — 409
  `davomat.jadval_biriktirilgan`. Ekranda tugma bo'lmasa, hech narsa buzilmaydi.
- **Qo'shimcha maydonlar:** `ScheduleAssignmentDto.photoKey` (faqat xodim
  pog'onasida), `AttendancePassDto.lateMinutes` (faqat kunning KECHIKKAN
  birinchi kirishida, qolganlarida `null`; bugungi hisoblanmagan kun —
  taxtadagi hisoblashdan, eski qatorsiz kun — `null`), `AttendanceRecomputeAcceptedDto.jobId`
  doim satr.
- **So'rov tanalarida enum maydonlari ixtiyoriy:** qurilmada berilmasa —
  Hikvision / Verifix / ReportsDirection / Token; moslik va import tasdig'ida
  `provider` berilmasa — Verifix.
- Import `failed[].code` endi `davomat.shaxs_raqami_uzun` ham bo'lishi mumkin.

### T (testlar) uchun

- Yozilgan: `AttendanceManagementApiTests` (14 — C6, eskirgan qatorni o'chirish,
  biriktirilgan jadval, bo'sh/mavjud bo'lmagan id kodlari, qurilma tokeni va
  HMAC, surat + ETag, CSV/XLSX import, saqlash muddati, jurnaldagi kechikish,
  RBAC matritsasi: faqat `view` → har bir `manage` va eksportda 403, hech
  qaysi ruxsat → hamma joyda 403, tokensiz → 401), `IdentityImportSheetTests`
  (5), `AllowedIpListTests` (19 holat).
- Qolgani: §11.1 #50 bazali varianti (ikki marta qayta hisoblash — har ustun
  bir xil), #64a (hisoblangan qiymat qatorga aynan), import 5 000 qator
  chegarasining HTTP varianti, `/export/*` fayl mazmuni endpoint orqali.
- ⚠️ `RateLimitPolicies.Export` sinov muhitida ham YOQIQ (daqiqada 5,
  foydalanuvchi bo'yicha) va `/recompute` ham shu siyosatda: bitta test
  sinfida admin nomidan 5 tadan ko'p `/recompute` yoki eksport — 429.

### Dev bazada qoldirilgan (C6 izi, ataylab)

- Qurilma «C6 sinov — B6 boshqaruv qurilmasi» (o'tishsiz, tokeni hech qayerga
  yozilmagan), moslik `10427` → Абдуқодиров Шерзод, uning 2026-09-17 kun
  yozuvi (`NoSchedule` — dev bazada jadval yo'q).

## W (menyu + 4 til + sozlamalar) — YOPILDI, C8 tasdiqlangan

- `./bonvi check` yashil: 4 til bir xil (har birida 1723 kalit), 48 ta
  `davomat.*` kodi tarjimada. «Verifikatsiya» bo'limi menyuda
  «Rekruting» dan keyin; qizil sanoq — ishdan ketgan xodim o'tishi,
  to'q sariq — moslanmagan raqamlar.
- **Ko'rinadigan o'zgarish:** guruh ichidagi bandlar endi HAR guruhda
  ruxsat bo'yicha filtrlanadi (ilgari faqat yuqori darajada edi).
  `recruiting.view` siz foydalanuvchi endi bo'sh «Rekruting» guruhini
  ko'rmaydi.
- **Yopiq guruh sarlavhasi** ichidagi eng jiddiy sanoqni ko'rsatadi
  (avval qizil) — aks holda qizil ogohlantirish guruh ochilmaguncha
  ko'rinmasdi.
- `useUnmatchedCount` ga yangilanish oralig'i gen almashtiruvida
  qo'shilmoqda.
- **Tekshirish kerak:** build'da asosiy chunk 744 kB haqida ogohlantirish;
  tarjimalar ~110 kB qo'shdi. Ogohlantirish ilgari ham bor edi — lekin
  hajm o'sgani qa-performance ko'rigida baholansin.

## `./bonvi gen` almashtiruvi (F1, SPEC §10.3) — YOPILDI

`attendance-api.ts` endi server DTO sining birorta qo'lda yozilgan
nusxasini saqlamaydi: hamma tip `@/shared/api/generated/model` dan
taxallus, JSON chaqiruvlar generatsiya qilingan fetcher'lar orqali
(`@/shared/api/generated/attendance/attendance`). Qo'lda faqat uchta yo'l
qoldi va sababi faylda yozilgan: multipart import (generatsiya qilingan
chaqiruv `Content-Type` ni qo'lda qo'yadi — `http-client.ts` tuzog'i),
o'tish surati va to'rtta eksport (blob + `Content-Disposition`).
`./bonvi gen` yashil, typecheck toza, `./bonvi pnpm build` yashil.

- **`integer | string` → `AsSent<T>`.** .NET 10 OpenAPI hamma butun sonni
  `integer | string` deb yozadi (`JsonSerializerDefaults.Web` →
  `AllowReadingFromString`, bu O'QISH qoidasi). Klient tipni generatsiya
  qilingan modeldan `number` ga toraytiradi (`AsSent`, bitta `asSent`
  tasdig'i). Jonli API bilan tekshirildi: 15 ta javob (taxta, jurnal,
  bitta o'tish, tabel ×2, moslanmaganlar, mosliklar, jadvallar,
  biriktirmalar, qurilmalar ×2, ogohlantirishlar, chetlanishlar, xulosa,
  resolve) generatsiya qilingan model bilan kalitma-kalit mos, butun sonlar
  JSON son, sanoq qiymatlari ro'yxatda. ⚠️ `/schedules`,
  `/schedule-assignments`, `/identities/unmatched` dev bazada BO'SH —
  ular elementli holatda ko'rilmadi (DTO recordlari tekshirildi).
- **Kechikish belgisi:** taxtadagi «Kechikkanlar» hisobotini `firstInAt`
  bo'yicha solishtirish (200 qator chegarasi bilan) O'CHIRILDI — belgi
  `AttendancePass.lateMinutes` dan. `useAttendanceExceptions` ning `live`
  parametri ham ketdi (yagona chaqiruvchisi shu edi).
- **`isSkewWarning` va `CLOCK_SKEW_TOLERANCE_SECONDS` O'CHIRILDI** — 300
  soniyaning klientdagi nusxasi; manba faqat serverning `skewWarning`.
- **`EMPLOYEE_PHOTO_PROBE` O'CHIRILDI.** Biriktirma xodimi rasmi —
  `ScheduleAssignment.photoKey`; importdagi aniq moslik taklifi — nomzoddan
  (server uni `candidates` ga doim qo'yadi). Kalit kelmagan istisnoda —
  bosh harflar.
- **Jadvalni yoqish/o'chirish:** jadval kartasida «Faol» almashtirgichi.
  Yoqish darhol; o'chirish oyna bilan, biriktirmasi bor jadvalda tugma
  o'chiq va `davomat.jadval_biriktirilgan` tushuntirishi. Faolsiz jadvalda
  «Biriktirish» tugmasi yo'q, biriktirish oynasi faqat faol jadvallarni
  taklif qiladi, `davomat.jadval_faolsiz` jadval maydoni ostida chiqadi.
- **Shartnoma bo'yicha tuzatilgan klient taxminlari** (server haq):
  `BoardItem.sinceAt`/`lastPassId`, `AttendanceBoard.asOf`,
  `AttendanceIdentity.createdAt`, `UnmatchedGroup.firstSeenAt`/`lastSeenAt`,
  chetlanish va xulosadagi `employeeName` — majburiy; ulardagi `null`
  tarmoqlari olib tashlandi. Enum zaxiralari (`oneOf`), yarim tanaffus
  oynasini tashlash va `matchedDepartment*` ni `scope` bilan to'sish ham
  ketdi — ularni server kafolatlaydi. Qoldirilgan: `TimeOnly` → «HH:mm»
  qirqish (`<input type="time">` uchun) va ogohlantirishlarni eng yangisi
  tepada saralash.
- **Qo'lda qolgan klient tiplari (sababi bilan):** `ExceptionKind` (server
  `kind` ni satr sifatida bog'laydi — OpenAPI da sanoq yo'q),
  `AttendanceExportFile` (fayl, DTO emas), `attendance-search.ts` dagi
  manzil/filtr holati.
- **`useUnmatchedCount`** — 60 soniyada yangilanadi, `placeholderData`
  bilan (nishon miltillamaydi).

### Server tomonida ko'rib chiqish uchun (tegilmadi)

- **Butun loyiha:** OpenAPI dagi `integer | string` — 98 ta modelda.
  `ConfigureHttpJsonOptions` da `NumberHandling = Strict` yoki sxema
  transformeri bilan hujjat javobni aniq tasvirlaydi va `AsSent` kerak
  bo'lmay qoladi. Strict string-raqamli so'rovlarni rad etadi — shu
  sababli qaror backend egasiniki.
- `GET /reports/exceptions` (+ eksporti): `kind` hujjatda `string?`, amalda
  majburiy sanoq. `[Required]` + enum bog'lash klientdagi `ExceptionKind`
  nusxasini keraksiz qilardi.
- Summary `from`/`to` ixtiyoriy (sukut — oy boshidan bugungacha); SPEC
  §6.6 buni aytmaydi. Klient baribir doim yuboradi.

## Gen almashtiruvi — yangi tarjima kalitlari

`./bonvi check` hozir FAQAT shu ikki kalit uchun qizil (4 tilga qo'shilishi
kerak):

| Kalit | uz-Latn |
|---|---|
| `attendance.scheduleDeactivateTitle` | Jadvalni faolsizlantirish |
| `attendance.scheduleDeactivateConfirm` | Faolsiz jadval yangi biriktirmalarda tanlanmaydi. Hisoblangan tabel kunlari o‘zgarmaydi — istalgan vaqtda qayta faollashtirish mumkin. |

Qayta ishlatilgan (yangi emas): `attendance.active` (almashtirgich yorlig'i),
`catalog.deactivate` (tugma), `attendance.scheduleAssignedTitle` /
`scheduleAssignedText` (to'silgan holat — matni faolsizlantirishni ham
aytadi), `davomat.jadval_biriktirilgan`, `davomat.jadval_faolsiz`.

## B7 (fon vazifalari) — C7 tasdiqlangan

Hangfire ulandi: `Api/Startup/BackgroundJobs.cs` (PostgreSQL, `hangfire` sxemasi,
`WorkerCount = 2`, faqat `default` navbati, dashboard marshruti YO'Q). Navbat va
server BITTA shartda: `Testing` — sinxron navbat, server yo'q; qolgan HAMMA muhit
(Development ham) — `HangfireBackgroundJobQueue` + server. Takroriy vazifalar (cron
UTC): `attendance:recompute-dirty` `*/15`, `attendance:nightly-recompute` 22:10
(03:10 Toshkent), `attendance:purge-photos` 22:30, `attendance:purge-passes` 22:45.
Qayta hisoblash yo'li bitta qoldi — hammasi `AttendanceDayRecomputer` orqali.
`./bonvi test` — 1689/1689; `./bonvi db-update` va
`ef migrations has-pending-model-changes` — o'zgarishsiz.

C7 dev API da (vazifalar koddan `RecurringJobManager.TriggerJob` bilan turtildi,
15 daqiqa kutilmadi):
- iflos navbat: kutayotgan 3 → 0, `processed_at` to'ldi; tor oynadagi o'tish
  (moslik SQL bilan, to'ldirishsiz) bog'landi — jurnal: «1 (xodim, sana), 2 kun
  yozildi, 3 o'tish belgilandi, 1 raqam kechikib bog'landi»;
- surat tozalash: 61 kunga surilgan surat — fayl yo'q, `photo_key`/`raw_event`
  NULL, `photo_purged_at` qo'yildi, qator bor; yangi surat tegilmadi;
- o'tish tozalash: 400 kunga surilgan 2 o'tish (suratli) — `passRetentionDays = 0`
  da hammasi joyida; 365 da qator ham, fayl ham yo'q; 2025-08-13 kun yozuvining
  `md5(row)` oldin ham, keyin ham `829bf643…`; `frozenThrough = 2025-09-17`;
- keyin `POST /recompute` (2025-08-12…14) Hangfire orqali (job 9, `DateOnly`
  argumentlari to'g'ri saqlandi) — muzlagan kun qatori yana `829bf643…`;
- `POST /identities` → `AttendanceBackfillJob` Hangfire'da (job 10, enum argument) — bajarildi.

### Koordinator uchun (qarorlar — `ASSUMPTIONS.md` da ham)

- ⚠️ **YANGI himoya — muzlatilgan kunlar.** Tozalangan kunni qayta hisoblashning
  uchta yo'li bor edi va SPEC faqat bittasini (tozalashning o'zi belgilamasin)
  yopgan: qo'lda `/recompute`, iflos navbatning ±1 qo'shnisi (chegara yonidagi
  o'tish), eski o'tishli kechikkan moslik. Uchalasi ham bir yillik tabelni omon
  qolgan o'tishlardan jimgina nolga qurardi. Yopildi YAGONA yozuvchida:
  `AttendanceDayRecomputer` `attendance.frozenThrough` sanasigacha (o'zi ham)
  kunlarga tegmaydi. Qiymat yashirin sozlama (`SystemSettingKeys`, `Hidden`),
  faqat o'sadi va tozalash birinchi qatorni o'chirishdan OLDIN yozadi.
- **O'tish tozalash `local_date` bo'yicha**, SPEC dagi `received_at` emas (indeks
  va muzlatish chegarasi aniqligi). Surat tozalash SPEC dagidek `received_at`.
- ~~Bog'lanmagan eski o'tishlar o'chirilmaydi~~ — **QAROR O'ZGARDI (koordinator,
  2026-09-17):** muddati o'tgan bog'lanmagan o'tish surat fayli bilan O'CHIRILADI;
  faqat bog'langan-u hisobga olinmagani qoladi (ogohlantirish 5304). Shart:
  `local_date < chegara AND (processed_at IS NOT NULL OR resolved_employee_id IS NULL)`,
  `DeleteExpiredAsync` uni `DELETE` ning o'zida qayta tekshiradi (to'ldirish
  poygasi). EventId 5305 olib tashlandi. SPEC §9.2.1 va §11.1 #68 AMENDED.
- Koordinator qabul qildi: muzlatish, `local_date` chegarasi, o'chmagan fayl
  qatori + ogohlantirish, suratsiz o'tishning `raw_event` i o'tish bilan birga
  yashashi, muzlagan davr uchun `/recompute` 200 (v1).

### Ochiq qolgan

- **Suratsiz o'tishning `raw_event` i 60 kunda TOZALANMAYDI.** SPEC §3.10 qidiruvi
  `photo_key IS NOT NULL` (indeks ham shunday), ya'ni qurilmadagi ism faqat o'tish
  qatori bilan birga (sukut 365 kun) ketadi. Kartali terminallarda bu ko'p qator
  bo'lishi mumkin. — **YOPILDI (koordinator): qabul qilindi**, `raw_event` o'zi
  tavsiflagan o'tish bilan birga yashaydi; 60 kunlik qoida faqat suratga tegishli.
- **Muzlagan davrni `/recompute` qilish 200 qaytaradi**, vazifa esa muzlagan
  sanalarni jimgina o'tkazib yuboradi (jurnalda «N muzlagan sana»). Ekranda aytish
  kerak bo'lsa — handler darajasida tekshiruv va yangi kod (masalan
  `davomat.davr_muzlagan`, HALI YO'Q) + W tarjimasi. — **v1 uchun qabul qilindi.**
- **Vazifalar holati faqat jurnalda** (EventId 5300–5304, 5306–5308; `docker logs api |
  grep Davomat`). Dashboard yo'q (§1.2 #12); «vazifa yiqildi» ogohlantirishi
  xato jurnali fayliga Hangfire'ning o'zidan Warning bo'lib tushadi.
- Production'da birinchi ishga tushish `hangfire` sxemasini O'ZI yaratadi (baza
  egasi huquqi kerak — prod'dagi foydalanuvchi egasi). `./bonvi backup` sxemani
  ham oladi — zararsiz.

### W / F uchun

- Yangi `davomat.*` kodi YO'Q. Yangi endpoint YO'Q. `/recompute` javobidagi
  `jobId` endi Hangfire raqami (`"9"`), `inline-…` emas — shakli o'zgarmadi (satr).

### T uchun

- Yozilgan: `AttendanceRecomputeJobsTests` (6: iflos navbat + ±1 + guruhlar
  kesishmasligi + ikkinchi yurish tegmasligi, kechikib bog'lash, unikal indeks
  poygasi, eskirgan qatorni ikki marta o'chirish poygasi, tungi hisoblash, navbat
  faqat `Testing` da sinxron), `AttendanceRetentionJobsTests` (7: surat tozalash +
  sozlamasiz sukut, #65, #66–#67 har ustun bilan, muzlatish — qo'lda va ±1
  qo'shni, #68, #70, 2 050 qatorlik to'plamlar + o'chmagan fayl),
  `LayerDependencyTests.Application_Hangfire_ga_boglanmasligi_kerak`.
- Qolgani: §11.2 #9 avtomatik testi (Hangfire sxemasi `upgrade head` va model
  suratini buzmaydi — dev da qo'lda tasdiqlandi); 5 daqiqalik vaqt chegarasi.

### Dev bazada qoldirilgan (C7 izi, ataylab)

- Qurilma «C7 sinov — B7 fon vazifalari» (`01a0aee0-4636-7eef-a143-d21b39e831c8`,
  tokeni hech qayerga yozilmagan) va uning 7 o'tishi: 10427 ning 17.09 dagi 3 tasi,
  `c7-late-1` ning 16.09 dagi 2 tasi, `c7-photo` ning 2 tasi (bittasining surati
  tozalangan, `received_at` 61 kun orqaga surilgan).
- Mosliklar `c7-late-1` (SQL bilan, to'ldirishsiz) va `c7-photo` → Абдуқодиров
  Шерзод; uning 16.09 va 17.09 kun yozuvlari yangilandi, 2025-08-13 kun yozuvi
  (o'tishlari tozalangan) — C7 isboti sifatida qoldirildi.
- `system_settings`: `attendance.frozenThrough = 2025-09-17` (muddat sozlamasi
  sukutga qaytarildi — qator o'chirildi). `hangfire` sxemasida 1–10 vazifalar.

## Migratsiyalar BIRLASHTIRILDI (2026-09-17, koordinator)

- Modul uchun endi BITTA migratsiya: `20260917063405_DavomatModuli`.
  `DavomatTanaffusNatijasi` ning ikki ustuni (`break_taken_minutes`,
  `break_deduction_minutes`) asl faylning `attendance_days` blokiga
  ko'chirildi; B1 ning izohlari va xom SQL bilan yaratilgan to'rtta
  indeks (`lower(name)`, `NULLS NOT DISTINCT`) SAQLANDI.
- Tekshiruv: model surati birlashtirishdan oldingisi bilan bayt-bayt bir
  xil; `has-pending-model-changes` — o'zgarish yo'q; dev bazaning
  nusxasida `Down` → `Up`: mavjud jadvallarning ustun/indeks/qator soni
  o'zgarmadi, modul sxemasi (ustunlar + indekslar md5) dev bilan bir xil.
  Testlar 1689/1689.
- Dev bazada `__EFMigrationsHistory` dan faqat
  `20260917072458_DavomatTanaffusNatijasi` qatori o'chirildi — ma'lumot
  yo'qolmadi, sxema allaqachon to'liq edi.

## B7 (fon ishlari) — C7 tasdiqlangan; bitta tuzatish ketmoqda

- Qabul qilingan: `attendance.frozenThrough` muzlatish, `local_date`
  bo'yicha kesish, o'chmaydigan fayl bo'lsa qator qoladi, suratsiz
  o'tishda `raw_event` o'tish bilan birga yashaydi, muzlagan davrni
  qayta hisoblash 200 qaytarib jim o'tkazib yuboradi (v1).
- **Tuzatilmoqda:** muddati o'tgan BOG'LANMAGAN o'tishlar ham
  o'chiriladi (egasining 1 yillik qoidasi; muzlatish tufayli ular hech
  qachon tabelga tusha olmaydi). Faqat bog'langan-u hisoblanmagan eski
  o'tishlar qoladi — navbat buzilganining belgisi.

## Xodim ilovasi (PWA, `/ilova/`) — ulanish holati (2026-09-27, D qism)

### Production'da berish — TAYYORLANGAN, deploy QILINMAGAN

- `web/Caddyfile`: HR SPA blokidan OLDIN ikki blok qo'shildi —
  `handle /ilova` (aniq yo'l → `/ilova/` ga 308) va `handle /ilova/*`
  (`/srv/ilova` dan; ilova ichidagi marshrutlar `/ilova/index.html` ga).
  Kesh: `/ilova/assets/*` (Vite xeshlangan fayllari) — bir yil `immutable`,
  `try_files` SIZ (eski xesh 404 beradi, HTML bo'lib keshlanmaydi);
  qolgani (`index.html`, `sw.js`, `manifest.webmanifest`, ikonkalar) —
  `no-cache`, ya'ni yangilanish darhol ko'rinadi.
  `Permissions-Policy` ATAYLAB yo'q (geolokatsiya kerak). Umumiy CSP
  ilovaga yetarli (shu domendagi `/api`, manifest, service worker,
  `data:`/`blob:` rasm; ilovada inline skript va tashqi manzil yo'q).
  `/api/*`, `/health/*` va HR SPA xulqi o'zgarmadi — lokal Caddy
  konteynerida soxta `/srv` bilan tekshirildi (`/`, `/xodimlar`,
  `/assets/*` → HR; `/ilovax` → HR; `/ilova` → 308; `/ilova/taqvim` →
  ilova index; `/ilova/assets/yoq.js` → 404).
- `web/Dockerfile`: yangi `ilova-build` bosqichi (`app/` dan, pnpm
  `--frozen-lockfile`), natija runtime'da `/srv/ilova` ga. HR `build`
  bosqichi o'zgarmadi. Manba — nomli kontekst `ilova-src`
  (`docker-compose.prod.yml` → `web.build.additional_contexts`), image
  konteksti avvalgidek `./web`. Faqat `ilova-build` bosqichi lokalda
  (commit qilingan `app/` nusxasidan) qurib ko'rildi — o'tdi; to'liq
  image (HR build bilan) qurilmadi.
- `app/.dockerignore` qo'shildi (`node_modules`, `dist` va h.k.).
- **Talab:** serverda Docker Compose ≥ 2.17 (`additional_contexts`) va
  BuildKit. Deploy'dan oldin `docker compose version` bilan tekshirilsin.
- **Deploy — FAQAT egasining aniq ruxsati bilan**, xavfsiz deploy
  tartibida: rollback tag + baza/fayl zaxirasi + nusxada sinov, keyin
  deploy. Prod'da 3 HR ishlaydi.
- Deploy'dan keyin qo'lda tekshiruv: `https://hr.bonvi.uz/ilova` → 308
  → ilova; HR sahifalari avvalgidek; javobda `Permissions-Policy` yo'q;
  telefonda «Bosh ekranga qo'shish», keyin oflayn ochilishi
  (`start_url: '/ilova/'` — manifest tuzatishi D qismda, #14).

### API rejimi

- Build argumenti `APP_API_MODE` (→ `VITE_API_MODE`), sukut `real`:
  ilova shu domendagi `/api/*` ga so'raydi (`VITE_API_URL` bo'sh — CORS
  kerak emas). `mock` — faqat namoyish/sinov uchun: prod `.env` da
  `APP_API_MODE=mock` qo'yib, image qayta quriladi. `.env.example` ga
  bu o'zgaruvchi qo'shilmadi (sukut yetarli).

### HR tomoni

- Xodim kartasi → «Ilova» kartasi (`employee-app-access-card`,
  `web/src/features/verification`): hisob ochish (bir martalik parol
  FAQAT BIR MARTA ko'rsatiladi), parolni tiklash, faolsizlantirish /
  faollashtirish va «Telefondan belgilash» o'chirgichi — hammasi
  `attendance.manage` ostida (`/api/employees/{id}/app-access*`,
  `/api/employees/{id}/mobile-check-in`). Xodim hisoblari tizim
  foydalanuvchilaridan alohida va rol muharririda ATAYLAB yo'q.

### Dev

- Dev'da ilova `http://localhost:7174/ilova/`. So'rovlar Vite'ning
  `/api` proksisi orqali (`app/vite.config.ts`: `VITE_API_PROXY` yoki
  `http://localhost:7080`) — shu domen, CORS kerak emas. Agar
  `VITE_API_URL` ga ABSOLYUT manzil berilsa, API `Cors:Origins` ga
  `http://localhost:7174` qo'shilishi shart (hozir faqat 7173 bor).
- `docker-compose.yml` → `app` xizmati: `VITE_API_URL` olib tashlandi,
  `VITE_API_PROXY: http://api:8080` qo'shildi (konteyner ichidan API'ga
  proksi). ⚠️ Ishlab turgan `app` konteyneri eski muhit bilan turibdi —
  yangi qiymat konteyner QAYTA YARATILGANDA kuchga kiradi
  (`./bonvi up`); agent konteynerlarga tegmadi.
- Ilova endi SUKUT bo'yicha haqiqiy API bilan ishlaydi (`VITE_API_MODE`
  berilmasa — `real`). Mock faqat `VITE_API_MODE=mock` bilan
  (namoyish, qo'lda sinash; `localStorage['bonvi.ilova.senariy']`).

### Bitta API nusxasi faraz qilingan

- Kirish urinishlari hisoblagichi (login + IP bo'yicha blok) va
  yangilash tokenining qisqa «qayta javob» keshi JARAYON ICHIDA
  saqlanadi. API bitta konteynerda ishlaydi — bu yetarli; qayta ishga
  tushganda holat yo'qoladi (blok bekor bo'ladi, 60 soniyalik oyna
  ichidagi takror yangilash qayta kirishni talab qiladi). Replika
  qo'shilsa — Redis'ga ko'chirish kerak.

## Xodim ilovasi — hisob avtomatik, login profil telefoni, parol HR da (2026-09-27)

Egasining qarori (SPEC-xodim-ilovasi §M). Yuqoridagi «HR tomoni» bandidagi
«Ilova» KARTASI (hisob ochish, bir martalik parol, faolsizlantirish) endi
YO'Q — o'rniga xodim kartasi → «Davomat» → «Ilova» segmenti.

### Deploydan oldin / deploy paytida

- **Migratsiya `EmployeeAccountsFromProfilePhone`** — `employee_accounts`
  dan `login` (+ `ux_employee_accounts_login`), `must_change_password`,
  `is_active`, `failed_login_attempts`, `locked_until` TASHLANADI,
  `password_protected` qo'shiladi. Prod'da bu jadval bo'sh (ilova hali
  deploy qilinmagan) — ma'lumot yo'qolmaydi; dev'dagi mavjud hisoblarda
  shifrlangan nusxa yo'q → HR «Yangi parol yasash» ni bosadi (eski parol
  ishlashda davom etadi).
- **Dev API'ni qayta ishga tushirish kerak** (migratsiya faqat ishga
  tushishda qo'llanadi) va keyin `./bonvi gen` — HR veb-klienti yangi
  endpointlarga (`revealEmployeePassword`, `regenerateEmployeePassword`,
  `setEmployeePassword`) bog'langan.
- **Birinchi ishga tushish:** Hangfire `employee-app:provision-accounts`
  navbatga qo'yiladi va ~540 xodimga hisob ochadi (har biri ~0,1 s xesh →
  bir necha o'n soniya, fonda; ishga tushishni to'smaydi). Keyin har 15
  daqiqada (:08/:23/:38/:53 UTC). Log: EventId 5403.
- **Parolni ko'rsatish shifrlash kalitiga bog'liq** (`Settings:SecretKey` — env `SETTINGS__SECRETKEY`,
  bo'lmasa `Jwt:SigningKey` dan — Telegram tokeni bilan bir xil kalit).
  Kalit almashsa, barcha saqlangan parollar «ko'rsatib bo'lmaydi» bo'ladi
  (parollarning o'zi ishlaydi) — HR kerak bo'lganda yangisini yasaydi.
- **Telefoni yo'q yoki takror raqamli xodimlar** kira olmaydi — HR «Ilova»
  segmentida sababni ko'radi va profilni tuzatadi. Prod'dagi takror
  raqamlar ro'yxatini deploydan oldin chiqarib berish foydali
  (`SELECT phone, count(*) FROM employees WHERE deleted_at IS NULL AND
  phone IS NOT NULL GROUP BY phone HAVING count(*) > 1`).
- **Ilova (PWA):** eski versiya keshda qolgan telefonlarda `/parol-ornatish`
  ekrani endi hech qachon ochilmaydi (server `mustChangePassword` bermaydi);
  eski `localStorage['bonvi.ilova.parol-almashsin']` yangi versiyada
  o'chiriladi.

### Olib tashlangan endpointlar

`POST /api/employees/{id}/app-access`, `…/app-access/reset-password`,
`…/app-access/deactivate`, `…/app-access/activate`,
`POST /api/employee-auth/password` (va uning tezlik cheklovi yo'li
`EmployeePasswordPaths`).

## Blankalar web (W2, 2026-11-03) — open seams

| What | Where | Owner |
|---|---|---|
| Nav count «Menga kutilmoqda» | DONE (S12): `useBlankWaitingCount` → `counts['blanks']` in `sidebar.tsx` (list #6 `waitingForMe`, `pageSize=1`) | — |
| Step actions #12–#16 | DONE (S12): generated calls #12–#15; #16 hand-written blob GET (generated fn is typed `void`) | — |
| Files #17–#20 | DONE (S13): #17 upload / #18 remove use the generated `uploadBlankAttachment` / `removeBlankAttachment`; #19 attachment and #20 PDF stay hand-written blob GETs (generated fns are typed `void`) | — |
| PDF button, `<BlankViewPage>`, employee card tab «Blankalar» | DONE (S12); #20 live since S7 | — |
| Shared C#/TS computed vectors | DONE (S13): `web/src/features/blanks/lib/__fixtures__/computed-vectors.json` is a byte copy of `api/tests/Bonvi.Application.Tests/Blanks/computed-vectors.json`; `BlankComputedVectorsMirrorTests` fails on drift (`./bonvi test` mounts `web/src` read-only at `/web`, `BONVI_WEB_DIR=/web`). TS evaluator now covers `round`, `addMonths`, `addWorkingDays`, `diffWorkingDays`, `gt`, `lte`, `and`, `or`, `not`; `{}` counts as empty (C# `BlankValues.IsEmpty`) | — |
| `upgrade head` on a prod-copy DB (SPEC §14 S13) | NOT DONE in S13 — needs a prod backup restored into a scratch DB; do it as part of the pre-deploy routine (rollback tag + backup + copy test) | deploy |
| Phase 2/3 (SPEC §14) | not started: F03/F05 (candidate subject + candidate card), F11/F12/F14/F19, Excel export, builder UI, import seam, employee-app channel, F01 (owner Q1) | later briefs |
