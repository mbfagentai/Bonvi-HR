# Deploy ro'yxati

> Prod: `root@189.74.98.57`, `/opt/bonvi-hr`, `https://hr.bonvi.uz`.
> Server 2 GB RAM — shu sababli deploy image'larni KETMA-KET quradi.

## ✅ ISUP sinov terminali ulandi, xodimlar rasmi bilan yozilmoqda — 2026-10-12 (Toshkent)

Prod `1340f29` → `93fe7e8` faqat ISUP qismlari (api/bot qayta qurilmadi):
* `b0fb062` shlyuz: passthrough javobi NUL gacha o'qiladi (SDK `dwReturnedXMLLen` = 0 qoldiradi) —
  faqat `isup-gateway` qayta qurildi (rollback tegi `bonvi-hr-prod-isup-gateway:rollback-1340f29`).
* `c5e2bce` Caddy: `isup.bonvi.uz` (RSA sertifikat) — `web` qayta qurildi (rollback tegi
  `bonvi-hr-prod-web:rollback-b0fb062`). Cloudflare'da A `isup` → server (zona NS: wally/kianchau).
* `93fe7e8` yangi xizmat `isup-face-tls` (nginx, 7661, ECDHE + DHE-RSA-AES-GCM) — terminal faqat
  RSA/DHE-RSA kalit almashuvini biladi, Caddy bermaydi (tcpdump'dagi ClientHello bilan aniqlandi).
Prod `.env` (zaxiralar `.env.bak-2026-10-12-*`): `ISUP_PUBLIC_ADDRESS=189.74.98.57`,
`ISUP_PUBLIC_DOMAIN=hr.bonvi.uz` (ularsiz terminal ro'yxatdan o'tmaydi — DAS javobi bo'sh),
`ISUP_FACE_SITE=isup.bonvi.uz`, `ISUP_FACE_URL_BASE=https://isup.bonvi.uz:7661/isup-face/`.
`.env.isup`: `device1` (terminal Device ID'da chiziqcha qabul qilmaydi), `Isup__SdkLogLevel` sinovdan keyin olib tashlandi.
Natija 08:48: 172/172 xodim rasmi bilan terminalga yozildi, xato 0.
Terminal (seriya J39518971, ofis): ISUP 5.0 → `hr.bonvi.uz:7660`, DNS `8.8.8.8`/`1.1.1.1`.

**Orqaga qaytarish:** `docker compose … rm -sf isup-face-tls`, `.env` dagi `ISUP_FACE_URL_BASE` ni
olib tashlash, `up -d isup-gateway`; kod — yuqoridagi rollback teglari.

## ✅ Reliz `1340f29` — tashqi API «to'liq o'qish» kaliti — CHIQARILDI 2026-10-12 09:03 (Toshkent)

Prod `3f03319` → `1340f29`. Rollback: image tegi `rollback-3f03319` (api, bot, web), zaxira stamp
`2026-10-12_035701`. Migratsiya `ApiKeyScope` qo'llandi (`api_keys.scope`, mavjud kalitlar 1 = Basic).
Egasi `data/deploy/2026-10-11-toliq-oqish.sh` ni o'zi yurgizdi. Tekshirildi: sayt va `/health/*` 200,
«o'qish roli tayyor», API jurnalida ERR yo'q, bot qayta yaratilmadi; `/api/public/v1/full` kalitsiz 401,
eski (Basic) kalit bilan 403 `tashqi.doira_yetmaydi`, `/ping` 200.

Qolgani egasida: Sozlamalar → Tashqi API → yangi kalit «To'liq o'qish» → boshliq botiga; eski «Shef API»
va «API KEY TESTING» kalitlarini bekor qilish. Qo'llanma: `docs/API-TASHQI.md` 6a-bo'lim.

**Orqaga qaytarish:** `docker tag …:rollback-3f03319 …:latest` (api, bot, web) + `up -d`. Eski kod `scope`
ustunini bilmaydi va unga tegmaydi — ustun qolaversa ham zararsiz.

## ✅ Reliz `3f03319` — tashqi API filtri + №91 tuzatishlari — CHIQARILDI 2026-10-11 21:44 (Toshkent)

Prod `494c23b` → `3f03319` (kod: faqat `PublicApiReadModel` — filtrlar javobda ko'rinadigan
ish joyiga qaraydi, `status` = asosiy munosabat). Rollback: image tegi `rollback-494c23b`
(api, bot, web), zaxira stamp `2026-10-11_162855`. Migratsiya YO'Q; prod nusxasida sinash
qilinmadi (nusxa olish ruxsat tizimida to'xtaydi; o'zgarish faqat o'qish so'rovi, 2502 test
o'tdi). Egasi `data/f91-import-skriptlar/prod/fix-all.sh` ni o'zi yurgizdi (auto-rejim
deploy va prod yozuvini to'xtatgan edi):
1. №91 import qilingan 2001 xodimning `updated_at` := hozir — `updatedSince` bilan
   sinxronlovchilar (MBF_AGENT, Shef API) ularni oladi.
2. 158 rasm `uploads-data` ga (158/158 joyida).
3. `infra/deploy.sh` 21:39–21:44; bot va web qayta yaratilmadi.

Tekshirildi (tashqi kalit bilan): `status=Active/Probation/Terminated` = 426/105/1498
(yig'indi 2029, yozuvdagi `status` bilan ziddiyat 0; oldin Terminated 1550 edi), rasmlar 200,
ETag 304, kalitsiz/noto'g'ri kalit 401, yozish 405, JSHSHIR bo'yicha qidiruv 0, sayt va
`/health/*` 200, API jurnalida ERR yo'q.

## ✅ Ma'lumot: №91 xodimlar bazasi (toza to'plam) — YOZILDI 2026-10-11 ~20:27 (Toshkent)

Kod o'zgarmadi (prod `494c23b`), faqat bazaga to'g'ridan-to'g'ri yozildi (egasining
ruxsati bilan). Import oldi zaxira stamp **`2026-10-11_152636`**. Fayllar serverda
`/root/f91-2026-10-11/` (import, rollback, dryrun/import jurnali), local nusxasi
`data/f91-import-skriptlar/prod/` (README.txt).

Sinov: prod nusxasini localga olish ruxsat tizimida to'xtadi, shuning uchun prod'ning
o'zida bitta tranzaksiyada import → rollback SQL → sanoq/nazorat yig'indisi solishtirildi →
ROLLBACK (farq 0). Undan oldin local'da prod'ga o'xshatilgan bazada import, takroriy
himoya («already imported») va rollback sinalgan.

Natija: 2001 xodim (425 faol + 79 sinovda + ketganlar), 2170 mehnat munosabati, 7335
hodisa, 1301 sinov muddati, 153 ketish suhbati, belgilar (qora ro'yxat 23, rezerv 6,
vazifa bersa bo'ladi 13), 76 arxiv nomzodi (yopiq «№91 архиви (Excel)» vakansiyasida).
Qo'shilmadi: HR prod'da kiritgan 26 kishi (kartasiga tegilmadi), telefoni prod'da bor 9
nomzod, ushlab turilgan 70 kishi — ro'yxat `data/f91-kutilayotganlar-2026-10-11.xlsx`.
Yangi ma'lumotnoma: 1 bo'lim («1-Департамент»), 5 lavozim. API jurnalida ERR yo'q;
xodim ilovasi hisoblarini fon vazifasi o'zi ochdi (15:38 UTC dan).

Rasm fayllari (158 ta) 21:39 da qo'yildi (reliz `3f03319` bo'limiga qarang).

**Orqaga qaytarish:**
- Faqat №91 ma'lumoti: `docker compose -f docker-compose.prod.yml exec -T postgres sh -c
  'psql -U $POSTGRES_USER -d $POSTGRES_DB' < /root/f91-2026-10-11/rollback.sql` — faqat
  import qo'shgan ID'larni (va ular uchun ochilgan ilova hisoblarini) o'chiradi; yangi
  bo'lim/lavozim boshqa joyda ishlatilmagan bo'lsagina o'chadi. HR o'sha xodimlarga
  davomat/grafik/yo'qlik bog'lagan bo'lsa — tranzaksiya to'xtaydi, hech narsa yarim qolmaydi.
- To'liq: zaxira `2026-10-11_152636` dan tiklash (undan keyingi HR kiritganlari ham ketadi).

## ✅ Reliz `494c23b` — ISUP shlyuzi (terminallar o'zi ulanadi) — CHIQARILDI 2026-10-10 17:17 (Toshkent)

Prod `54567ad` → `494c23b` (`f2cddf5` taxminlar, `494c23b` ISUP shlyuzi —
`docs/SPEC-verifikatsiya-isup.md`, ishlatish `docs/ISUP-RUNBOOK.md`). Rollback:
image tegi `rollback-54567ad` (api, bot, web), zaxira stamp `2026-10-10_120906`
(deploy oldi). Migratsiya YO'Q; API, bot va veb kodi o'zgarmagan — faqat
Caddyfile (`/isup-face/*`), shuning uchun prod nusxasida sinash qilinmadi.

* Prod `.env` ga `COMPOSE_PROFILES=isup` qo'shildi (zaxirasi
  `.env.bak-2026-10-10-isup`). `.env.isup` hali YO'Q — terminal qo'shilganda
  yaratiladi (shablon `.env.isup.example`).
* Yangi konteynerlar: `isup-gateway` (~50 MB, faqat ichki `isup` tarmog'ida —
  internetga chiqishi YO'Q, tekshirildi) va `isup-proxy` (socat, 7660 TCP+UDP).
  `api` va `web` ichki `isup` tarmog'iga ham ulandi.
* Tekshirildi: shlyuz «ISUP CMS tinglamoqda: 0.0.0.0:7660», 7660/TCP
  tashqaridan ochiq, 8091 tashqaridan yopiq, `/isup-face/<noto'g'ri>` → 404,
  `../health` va `%2e%2e` aylanib o'tishlari Caddy'dan o'tmaydi (SPA), sayt va
  `/health/{live,ready}` 200, API jurnalida ERR yo'q. Bot qayta yaratilmadi.
* Ofisdagi 2 terminal (192.168.1.16/.17) hali Verifix'da — tegilmagan.

**Orqaga qaytish:**
- Faqat ISUP'ni o'chirish: prod `.env` dan `COMPOSE_PROFILES=isup` qatorini olib
  tashlash, so'ng `docker compose -f docker-compose.prod.yml --profile isup rm -sf
  isup-gateway isup-proxy`. Qolgan tizimga ta'siri yo'q.
- Kod: `docker tag …:rollback-54567ad …:latest` (api, bot, web) + `up -d`.

## ✅ Reliz `54567ad` — xabarnoma bir marta + nomzodlar qora ro'yxati + Jobster importi — CHIQARILDI 2026-10-07 13:54 (Toshkent)

Prod `64a1e32` → `54567ad` (`69328a5` xabarnoma bir urinish, `ec0434c` nomzod
qora ro'yxati). Rollback: image tegi `rollback-64a1e32` (api, bot, web), zaxira
stamp `2026-10-07_084557` (deploy oldi). Migratsiya `NotificationDeliveryOutcome`
qo'llandi (2 ta bo'sh ustun). API jurnalida ERR yo'q, bot API bilan 200, sayt 200.

**Jobster importi — 2026-10-07 ~13:56, to'g'ridan-to'g'ri bazaga (egasining
ruxsati bilan).** Fayllar serverda `/root/jobster-2026-10-07/` (import, rollback,
oldin/keyin sanoqlari). Import oldi zaxira stamp **`2026-10-07_085539`**.
Avval prod nusxasida (`bonvi_jbtest`) sinaldi: import → rollback → sanoqlar va
nomzodlar nazorat yig'indisi AYNAN bir xil; sinov bazasi o'chirildi.

Natija: 6740 ariza → 4722 yangi karta (rad etilgan 4063, ishga olingan 455,
qora ro'yxat 119, rezerv 85) + 53 ta mavjud kartaga «Jobster тарихи» qo'shildi;
24 ta yopiq «… (Jobster)» vakansiya. Faol 161 nomzod o'zgarmadi. Rad xabari
navbati 0 — importdan hech kimga xabar ketmadi.

Egasiga aytilgan: mavjud kartalardan ikkitasi Jobster qora ro'yxatida —
«Mirolimbekov Jahongir» (yopiq edi → qora ro'yxatga o'tdi) va **«Усмонов
Исроилжон»** (FAOL, 2026-10-02 dan «Метан ва пропан…» vakansiyasida — tegilmadi,
HR qaror qiladi).

**Orqaga qaytish:**
- Faqat Jobster ma'lumoti: `docker compose -f docker-compose.prod.yml exec -T
  postgres sh -c 'psql -U $POSTGRES_USER -d $POSTGRES_DB' <
  /root/jobster-2026-10-07/jobster-rollback.sql` — faqat import qo'shganini
  oladi (yangi kartalar, vakansiyalar, izoh qo'shimchasi, qora ro'yxat holati).
  HR o'sha kartalarga fayl/suhbat qo'shgan bo'lsa, skript tranzaksiyani to'xtatadi
  (FK) — hech narsa yarim qolmaydi.
- To'liq: zaxira `2026-10-07_085539` dan tiklash (undan keyingi YANGI arizalar
  ham ketadi — oxirgi chora).
- Kod: `docker tag …:rollback-64a1e32 …:latest` (api, bot, web) + `up -d`.
  ⚠️ Eski kod `status = 6` ni bilmaydi — kodni qaytarishdan OLDIN Jobster
  rollback SQL ni yurgizing (yoki qora ro'yxatdagilar «Jarayonda» bo'lib ko'rinadi).

## ✅ Reliz `64a1e32` — AI ulanish (MCP) + «Umumiy anketa» — CHIQARILDI 2026-10-05 13:48 (Toshkent)

Prod `90f11d7` → `64a1e32`. Rollback: image tegi `rollback-90f11d7` (api, bot,
web), zaxira stamp `2026-10-05_083600`. Prod nusxasida sinaldi (Production
rejimi, prod `Dockerfile`): migratsiya `McpReadOnly` qo'llandi, ma'lumot
o'zgarmadi, `mcp.connect` faqat super administratorga. Deploy 13:38–13:48,
ish vaqti, HR faolligi past edi. Natija: API jurnalida ERR yo'q, bot API bilan
200, sayt/ilova 200, `/mcp` 503 va `/oauth/*` 404 (MCP sozlanmagan — ataylab),
28 xodim / 230 nomzod, «Umumiy anketa» ochiq.

**MCP hali YOQILMAGAN.** Yoqish — quyidagi 2-, 4–8-qadamlar (egasi aytganda).
HR sahifani yangilaydi (F5).

**2026-10-11 ~20:50 (Toshkent) — server tomoni SOZLANDI** (egasining ruxsati bilan):
prod `.env` ga `MCP_DB_PASSWORD` (`openssl rand -hex 24`) va
`MCP_PUBLIC_BASE_URL=https://hr.bonvi.uz` qo'shildi (zaxira `.env.bak-2026-10-11-mcp`),
faqat `api` qayta yaratildi (`up -d --no-deps --no-build api`, image o'sha). Jurnal:
«AI ulanish (MCP): o'qish roli tayyor — bonvi_mcp», ERR yo'q, sayt va `/health/*` 200,
bot 200. Holat «O'chirilgan» (o'chirgich sukut bo'yicha o'chiq): `/mcp` 503,
`/.well-known/oauth-authorization-server` 404. Qolgani — 8-qadam (egasi yoqadi).
Qaytarish: `.env` dan ikki qatorni olib tashlash, `up -d --no-deps --no-build api`,
so'ng `ALTER ROLE bonvi_mcp NOLOGIN;`.

### Reliz tafsiloti (tarix uchun)

Spetsifikatsiya: `docs/SPEC-mcp.md`. Faqat o'qish; pasport ma'lumotlari va
fayllar baza darajasida yopiq. Deploy faqat egasi aytganda.

**Deploy'dan keyin hech narsa o'z-o'zidan ochilmaydi:** `MCP_DB_PASSWORD`
berilmasa MCP «serverda sozlanmagan»; berilsa ham o'chirgich sukut bo'yicha
O'CHIQ — egasi «Tizim sozlamalari» → «AI ulanish» da yoqadi. HR menejeri
roliga `mcp.connect` berilmaydi (seeder oddiy rolga tegmaydi).

1. Migratsiya `McpReadOnly` — 5 ta yangi `mcp_*` jadval + `mcp_apply_ro_grants`
   funksiyasi. Hech narsa o'chirilmaydi. (`Bootstrap__AutoMigrate` bilan o'zi.)
2. Prod `.env`:
   - `MCP_DB_PASSWORD=` — `openssl rand -base64 24` (yangi, dev'dagidan BOSHQA);
   - `MCP_PUBLIC_BASE_URL=https://hr.bonvi.uz`.
3. Caddy — `web/Caddyfile` (web image bilan keladi): `/mcp`, `/oauth/*`,
   `/.well-known/oauth-*` → `api:8080`; SPA CSP `/oauth/*` ga qo'yilmaydi
   (aks holda `form-action 'self'` rozilikdan keyingi qaytishni to'sadi).
   Lokal sinalgan 2026-10-01 (`caddy:2.11.4-alpine`).
4. API ishga tushgach jurnalda: `AI ulanish (MCP): o'qish roli tayyor — bonvi_mcp`.
   Yo'q bo'lsa — `AI ulanish (MCP) YOPIQ:`/`tekshiruvi yiqildi:` qatoridagi sabab.
5. Tekshiruv (tashqaridan): `GET /.well-known/oauth-authorization-server` —
   o'chirgich o'chiq paytda 404, yoqilgandan keyin 200 va `issuer` =
   `https://hr.bonvi.uz`; `POST /mcp` tokensiz → 401 + `WWW-Authenticate`.
6. Deploy'dan OLDIN build konteynerida: `dotnet list package --vulnerable
   --include-transitive` — `ModelContextProtocol.AspNetCore` 2.2.0 va uning
   bog'liqliklarida ma'lum zaiflik yo'qligini tekshirish (2026-10-01: toza).
7. Yoqishdan oldin haqiqiy claude.ai (kompyuter + telefon) va ChatGPT bilan
   bitta sinov ulanishi: rozilik sahifasi popup'da ochilib, qaytishi va
   «Uzish» dan keyin 401 bo'lishi (COOP/CSP o'zgarishlari faqat brauzerda ko'rinadi).
8. Egasi: «Rollar» da kerakli rolga «AI orqali o'qish (MCP)» ni beradi,
   «Tizim sozlamalari» da o'chirgichni yoqadi, claude.ai → Connectors →
   `https://hr.bonvi.uz/mcp` (qo'llanma: `docs/MCP.md`).

**Shu relizda yana — «Umumiy anketa» ochib-yopiladigan bo'ldi (2026-10-05).**
Egasining qarori: qaysi vakansiya yopiq bo'lsa, unga anketa topshirib
bo'lmaydi. HR uni boshqalar kabi almashtirgich bilan yopadi (oldin ogohlantirish
chiqadi); yopiq bo'lsa bot «📋 Anketa to'ldirish» bosilishi bilanoq aytadi.
API ishga tushganda uni endi QAYTA OCHMAYDI. Deploy'dan keyin: HR sahifani
yangilaydi (F5); bot ham yangilanishi SHART (yangi `general-pool` so'rovi) —
eski bot yangi API bilan ishlayveradi, lekin yopiq anketani boshida aytmaydi.

Rollback: image rollback yetarli — eski image `mcp_*` jadvallarini bilmaydi va
ularga tegmaydi; `bonvi_mcp` roli faqat o'qiydi. Rollback'dan keyin rol paroli
tirik qolmasin: `ALTER ROLE bonvi_mcp NOLOGIN;`. To'liq tozalash kerak bo'lsa:
`DROP OWNED BY bonvi_mcp; DROP ROLE bonvi_mcp;` (`DROP OWNED` bu yerda faqat
rolga BERILGAN ruxsatlarni oladi — rol hech narsaga ega emas; usiz `DROP ROLE`
«privileges for table …» bilan rad etiladi).

## ✅ Reliz `90f11d7` — Verifikatsiya (davomat) moduli — CHIQARILDI 2026-09-30 21:08 (Toshkent)

Prod `75d5f02` → `90f11d7`. Rollback: image tegi `rollback-75d5f02` (api, bot,
web), zaxira stamp `2026-09-30_155838`. Deploy 20:59–21:08, HR faol emas edi
(oxirgi soatda faqat bot so'rovlari). Natija: tarix 40 → 56, oxirgisi
`EmployeeMobileFlagSukuti`, sukut `false`; API jurnalida WRN/ERR yo'q;
Hangfire o'rnatildi; 28 xodimga ilova hisobi; bot qayta yaratilmadi (kodi
o'zgarmagan), 21:03:00 dagi 4 ta «HTTP request failed» — API qayta ishga
tushgan soniya. Tashqaridan: `/` va `/ilova/` 200, `/api/employee-auth/login`
401 (xodim ilovasi yopiq), `/api/me` 401. Disk 75% (9.5 GB bo'sh).

Qolgan qadam — egasi: kerakli rollarga «Verifikatsiya» darajasini beradi
(xohlaganda). HR sahifani yangilaydi (F5).

### Reliz tafsiloti (tarix uchun)


Prod `75d5f02` → `main` (merge `d3d4b34` + tuzatishlar). Egasi 2026-09-30
da so'radi: HR loyihasiga zarar yetmasin, muammo bo'lsa oson qaytilsin.

**HR menejer uchun deploy'dan keyin HECH NARSA o'zgarmaydi:** seeder oddiy
rolga tegmaydi, ya'ni «Verifikatsiya» bo'limi, xodim kartasidagi «Davomat»
yorlig'i va «Yo'qlik turlari» egasi «Rollar» → rol → **Verifikatsiya**
darajasini bergandagina ko'rinadi (Ko'rish / O'zgartirish = so'rovlar
bo'yicha qaror / To'liq = qurilma, jadval, moslik). Super administratorda
bo'lim darhol bor.

**Xodim ilovasi YOPIQ** (`EMPLOYEE_APP_ENABLED` prod `.env` da yo'q →
`false`): `/api/employee-auth`, `/api/me` marshrutlari yo'q. `/ilova/`
statik sahifasi ochiladi, lekin kirib bo'lmaydi. Ochishdan oldin xavfsizlik
tuzatishlari kerak (`docs/ASSUMPTIONS.md`, 2026-09-30).

**Migratsiyalar — 16 ta**, `main` nikidan KEYIN qo'llanadi (sanalari
oldinroq — EF ID bo'yicha qo'llaydi): 15 ta verifikatsiya (14 yangi jadval,
`device_person_no_seq`, `employees.mobile_check_in_allowed` ustuni) +
`20260930094327_EmployeeMobileFlagSukuti` (shu ustunga DEFAULT false —
image rollback uchun). Mavjud HR jadvallarida birorta qator o'zgarmaydi.
API ishga tushganda `hangfire` sxemasi yaratiladi va har bir xodimga ilova
hisobi ochiladi (jurnal: «Xodim ilovasi hisoblari: N ta»).

**Sinalgan (2026-09-30, lokal, dev nusxa — 2217 xodim):** `main` sxemasidan
ko'tarish xatosiz, 37 jadval sanog'i o'zgarmadi; eski image migratsiyalangan
bazada o'qish + xodim qo'shish (200); zaxiradan tiklash B banddan keyin
sanoqlar AYNAN bir xil. `./bonvi test` 2311/2311, web + ilova build, prod
web image (Caddyfile `validate`).

**Deploy oldidan:**
- [ ] Rollback teglari: `docker tag bonvi-hr-prod-{api,bot,web}:latest …:rollback-75d5f02`
- [ ] Yangi zaxira: `docker compose -f docker-compose.prod.yml exec backup /scripts/run-all.sh`
- [ ] Disk ≥ 5 GB (`df -h /`); web image endi ikkita build (HR + ilova)
- [x] Prod nusxasida (serverda, `bonvi_migtest`, zaxira `2026-09-30_100256`,
      egasi ruxsat berdi): idempotent skript 2 s, 37 jadval sanog'i
      o'zgarmadi (tarix 40 → 56); eski image (`75d5f02`) migratsiyalangan
      nusxada sog'lom, o'qish + xodim qo'shish 200; B band + `restore.sh`
      uslubida tiklash — 0 xato, sanoqlar AYNAN bir xil. `bonvi_migtest`
      va vaqtinchalik konteynerlar o'chirildi.
- [ ] Prod oxirgi migratsiyasi `20260929061633_VakansiyaNomiVakansiyaningOzida`
- [ ] Ish vaqtidan tashqarida (`employees` ga ALTER, `lock_timeout` 3s)
- [ ] `(nohup ./infra/deploy.sh > /tmp/deploy-<commit>.log 2>&1 < /dev/null &)`

**Deploy'dan keyin:**
- [ ] `__EFMigrationsHistory` +16, oxirgisi `20260930094327_EmployeeMobileFlagSukuti`
- [ ] API jurnalida WRN/ERR yo'q; «Hangfire SQL objects installed»; «hisob ochildi»
- [ ] HR menejer hisobida menyu avvalgidek («Verifikatsiya», «Yo'qlik turlari» yo'q)
- [ ] Xodim kartasi, nomzodlar, vakansiyalar, rollar ochiladi; bot javob beradi
- [ ] `curl -s -o /dev/null -w '%{http_code}' https://hr.bonvi.uz/api/employee-auth/login -X POST` → 401 (marshrut yo'q)
- [ ] Egasi: kerakli rollarga «Verifikatsiya» darajasini beradi (xohlaganda)

**Orqaga qaytish** (`infra/sql/verifikatsiya-rollback.sql`):
- **Faqat kod** (baza qoladi) — qo'shimcha SQL KERAK EMAS:
  `docker tag …:rollback-75d5f02 …:latest` (api, bot, web) va
  `docker compose -f docker-compose.prod.yml up -d`. Eski image yangi
  jadvallarni ko'rmaydi, `mobile_check_in_allowed` sukuti bilan xodim qo'sha oladi.
- **Ma'lumot ham** (zaxiradan) — AVVAL fayldagi **B** bandi (yangi jadvallar,
  `hangfire`, ketma-ketlikni o'chirish), keyin odatdagi `restore.sh`. B siz
  `pg_restore --clean` bazani YARIM tiklaydi (repetitsiyada ko'rilgan).

**Keyinga (bu relizni to'smaydi, egasi tartibini belgilaydi):**
- Xodim ilovasini ochishdan oldin: HR bermagan hisob kira olmasin; telefon
  bo'yicha umumiy xato chegarasi; anonim so'rov hajmi (16 KB) va xeshlashga
  umumiy navbat; HR qo'ygan zaif parollar rad etilsin.
- Qurilmalar ulanganda: davomat yozuvlari har bir ochiq HR bildirishnoma
  oqimini uyg'otadi (`DataChangeInterceptor`) — yuklama; agent tokeniga
  `AllowedIps`, `/photo` faqat ro'yxatdagilarga.

## ✅ Reliz `7a10e88` — CHIQARILDI 2026-09-27 19:31 (Toshkent)

Prod `9184b0b` → `7a10e88`. Rollback: image tegi `rollback-9184b0b`, zaxira
stamp `2026-09-27_142308`. Prod nusxasida oldindan sinaldi (faqat migratsiya
va yangi ustun; eski image yangi bazada ishlaydi; zaxiradan tiklash xatosiz,
sanoqlar aynan bir xil). Deploy'dan keyin: 500 yo'q, bot qayta yaratilmadi.
Qolgan qadam — egasi «Bosh rahbar» ga «Qo'shimcha haqni ko'rish» ni beradi.

| Commit | Nima |
|---|---|
| `6b17404` | Qo'shimcha vazifa haqi okladga QO'SHILADI (kartada «Jami»), vazifa tugash sanasi, yangi ruxsat `salary.extra.view` |
| `688a020` | Summa maydonlari uch xonadan ajratiladi (`MoneyInput`), kartadagi oklad bloki |
| `64e90ab` | Kiritilmagan oklad «kiritilmagan» deb ko'rsatiladi |
| `6d6c452` | Reliz oldi tekshiruv tuzatishlari: bo'shatishda vazifalar yopiladi, summa maskasi xavfsiz, «Jami» faqat oklad bo'lsa |

**Tayyorlik (2026-09-27):** mustaqil tekshiruvdan o'tgan (to'suvchi topilmadi,
topilganlar `6d6c452` da tuzatilgan); `./bonvi test` 1653/1653,
`./bonvi pnpm build`, `./bonvi check` yashil; migratsiya snapshot'i izchil.
Keyingi o'zgarishlar qo'shilsa — ular ham shu jadvalga yoziladi va
tekshiruv qaytadan o'tkaziladi.

**Migratsiya:** `20260927105152_QoshimchaVazifaTugashi` — faqat bitta bo'sh
ustun (`employment_events.duty_end_date`). Eski image uni bilmaydi va
tegmaydi — image rollback xavfsiz. `Down` ustunni o'chiradi (tugash sanalari
yo'qoladi) — kerak bo'lsa `Down` emas, image rollback.

**Deploy oldidan** (xavfsiz tartib — `bonvi-hr-xavfsiz-deploy`):
- [ ] Rollback teglari: `docker tag bonvi-hr-prod-{api,bot,web}:latest …:rollback-9184b0b`
- [ ] Yangi zaxira: `docker compose -f docker-compose.prod.yml exec backup /scripts/run-all.sh`
- [ ] Disk: `df -h /` — 5 GB dan kam bo'lsa, egasidan so'rab
      `docker builder prune -f --filter until=168h` (build keshi har deploy'da o'sadi)
- [ ] Prod nusxasida sinov — dump'ni lokal bazaga tiklash uchun **egasining
      aniq ruxsati** kerak (ruxsat tizimi buni to'xtatadi)
- [ ] `nohup ./infra/deploy.sh > /tmp/deploy-<commit>.log 2>&1 < /dev/null &`

**Deploy'dan keyin:**
- [ ] Egasi: «Rollar» → «Bosh rahbar» → **«Qo'shimcha haqni ko'rish»** ni
      belgilaydi (mavjud rolga yangi ruxsat jimgina berilmaydi)
- [ ] HR sahifani yangilaydi (F5)
- [ ] ⚠️ Kutilgan: HR menejerlar qo'shimcha vazifa SUMMASINI ko'rmay qoladi
      (egasining qarori) — xato emas
- [ ] Haqli vazifasi bor xodim kartasi: oklad, vazifa, «Jami» to'g'ri


## ✅ Reliz `75d5f02` — CHIQARILDI 2026-09-29 13:56 (Toshkent)

Prod `7a10e88` → `75d5f02` (`7d816d3` vakansiyalar bitta joyda, `967a943`
switch, `75d5f02` bot chidamliligi). Rollback: image tegi
`rollback-7a10e88`, zaxira stamp `2026-09-29_084927`. Migratsiya oldindan
prod nusxasida (serverning o'zida, `bonvi_migtest`) sinaldi: 8 → 14
vakansiya (6 ta vakansiyasiz nom yopiq bo'lib o'tdi), 125 nomzod, yetim
bog'lanish 0. Deploy'dan keyin: «Umumiy anketa» qayta ochildi
(`DatabaseInitializer` WRN), bot `@Bonvi_HR_bot` ishga tushdi, birinchi
suhbat Redis'da 24 soat TTL bilan ko'rindi. Kutilgan shovqin:
`/api/notifications/stream` 500 (web qayta ishga tushishi) va botning
ishga tushishdagi 3202/3208 (API hali ko'tarilmagan).

### Reliz tafsiloti (tarix uchun)

Migratsiya `20260929061633_VakansiyaNomiVakansiyaningOzida`: «Vakansiya
nomlari» ma'lumotnomasi (`vacancy_titles`) va mas'ul xodim
(`vacancies.responsible_employee_id`) olib tashlanadi, nom
`vacancies.title` ga ko'chadi. Vakansiyasiz nomlar — YOPIQ vakansiya
bo'lib o'tadi (egasining talabi: ma'lumotnomadagi har bir nom vakansiyalar
bo'limida bo'lishi kerak).

**Deploy oldidan — prod zaxirasi nusxasida (o'qish uchun, sxema eski):**

```sql
-- A: nechta nom yopiq vakansiya bo'lib qo'shiladi (ma'lumot uchun)
select count(*) from vacancy_titles t
where t.deleted_at is null
  and not exists (select 1 from vacancies v join vacancy_titles t2 on t2.id = v.vacancy_title_id
                  where v.deleted_at is null and lower(t2.name) = lower(t.name));

-- B: ⛔ 0 bo'lishi SHART — aks holda migratsiya ataylab to'xtaydi (izoh
--    2000 belgiga sig'maydi, kesib yozilmaydi). Izohni qo'lda qisqartiring.
select count(*) from vacancy_titles t
left join vacancies v on v.vacancy_title_id = t.id
where btrim(coalesce(t.note, ''), E' \t\r\n') <> ''
  and (v.id is not null or t.deleted_at is null)
  and length(case
        when v.id is null or btrim(coalesce(v.note, ''), E' \t\r\n') = '' then t.note
        when v.note = t.note then v.note
        else v.note || E'\n\n' || t.note end) > 2000;

-- D: bir nomda bir nechta TIRIK vakansiya (holatidan qat'i nazar) —
--    ko'chirishdan keyin ro'yxatda bir xil nomli qatorlar bo'ladi
--    (tahrirlash ishlaydi; faqat NOMNI o'zgartirish band nomga o'tmaydi)
select lower(t.name), count(*) from vacancies v join vacancy_titles t on t.id = v.vacancy_title_id
where v.deleted_at is null group by 1 having count(*) > 1;

-- E: sozlamadagi «Suhbat muddati» — qator bo'lmasa sukut 1440 ishlaydi.
--    1440 dan kichik bo'lsa, «Tizim sozlamalari» da 1440 qiling
--    (bot endi shu qiymatni HAQIQATAN ishlatadi; ilgari e'tiborsiz edi)
select value from system_settings where key = 'bot.conversationTimeoutMinutes';

-- F: «Umumiy anketa» nomida bir nechta tirik vakansiya — 1 dan ko'p
--    bo'lsa, ishga tushishdagi qayta ochish O'TKAZIB yuboriladi, qo'lda hal qiling
select v.id, v.status from vacancies v join vacancy_titles t on t.id = v.vacancy_title_id
where v.deleted_at is null and lower(t.name) = 'umumiy anketa';

-- C: bir nomda bir nechta OCHIQ vakansiya (bot ikkita bir xil tugma
--    ko'rsatadi) — 0 bo'lmasa, deploy'dan keyin HR ortiqchasini yopadi
select lower(t.name), count(*) from vacancies v join vacancy_titles t on t.id = v.vacancy_title_id
where v.deleted_at is null and v.status = 1 group by 1 having count(*) > 1;
```

**⚠️ Image rollback bu relizdan keyin XAVFSIZ EMAS:** eski image
`vacancy_titles` va `vacancy_title_id` ni so'raydi. Qaytish — zaxiradan
tiklash (yoki `Down`: ma'lumotnomani nomlardan qayta yig'adi, mas'ul
xodimlar qaytmaydi).

**Shu relizga qo'shilgan — bot chidamliligi (2026-09-29):**
- Bot endi Redis'ga ulanadi (`ConnectionStrings__Redis`, compose'da
  `depends_on: redis healthy`) — yarim anketa deploy'da yo'qolmaydi.
- Suhbat muddati sukut 1440 daqiqa; prod `.env` da
  `TELEGRAM__CONVERSATIONTIMEOUT=` bo'sh — ya'ni compose sukuti ishlaydi.
- ⚠️ API ishga tushganda YOPIQ «Umumiy anketa» ni QAYTA OCHADI (jurnal:
  `DatabaseInitializer[2012]`). Prod'da u 2026-09-11 dan yopiq — bu
  kutilgan va shu relizning maqsadi.

**Deploy'dan keyin:**
- [ ] Ma'lumotnomalar menyusida «Vakansiya nomlari» yo'q
- [ ] Vakansiyalar ro'yxatida eski nomlar bor (vakansiyasizlari — «Yopiq»)
- [ ] Yopish / qayta ochish bir bosishda ishlaydi; bot ochiq vakansiyalarni ko'rsatadi
- [ ] «Umumiy anketa» — qulf belgisi bilan, ochiq
- [ ] Bot jurnalida `RedisConversationBackup[3301]` yo'q (Redis nusxasi ishlayapti)
- [ ] Botda «📋 Anketa to'ldirish» orqali sinov arizasi o'tadi (keyin HR o'chiradi)

## Oldindan

- [ ] Lokalda hammasi yashil: `./bonvi build` · `./bonvi test` ·
      `./bonvi check` · `./bonvi pnpm build`
- [ ] `git push` — `deploy.sh` `git pull --ff-only` qiladi, ya'ni
      o'zgarishlar origin'da bo'lishi SHART
- [ ] **Yangi zaxira**: prod'da

      ```
      docker compose -f docker-compose.prod.yml exec backup /scripts/run-all.sh
      ```

      ⚠️ `./bonvi backup` prod'da ISHLAMAYDI: `bonvi` skripti dev
      compose faylini qidiradi va «postgres ishlamayapti» deb to'xtaydi.
      Kunlik avtomatik zaxira 22:00 UTC da olinadi, ya'ni ertalabki
      deploy oldida u eskirgan bo'ladi.

      ⚠️ `.env` dagi `BACKUP_CRON_SCHEDULE` qiymati QO'SHTIRNOQDA
      bo'lishi shart (`"0 22 * * *"`). Qo'shtirnoqsiz yozilsa `bonvi`
      skripti uni buyruq deb o'qiydi va `exit 127` bilan o'ladi —
      2026-09-10 da prod'da aynan shu holat topilgan va tuzatilgan.
- [ ] Ish vaqtidan tashqarida: bot qayta ishga tushganda YARIM
      TO'LDIRILGAN anketalar yo'qoladi (holat xotirada)

## Deploy

```
ssh root@189.74.98.57
cd /opt/bonvi-hr
docker compose -f docker-compose.prod.yml exec backup /scripts/run-all.sh
./infra/deploy.sh
```

Skript 7 qadam: `git pull` → api build → bot build → **backend up
(postgres, redis, api, bot)** → OpenAPI klient → web build → up.

⚠️ Migratsiyalar API ko'tarilishida O'ZI qo'llanadi
(`Bootstrap__AutoMigrate`). Prod qaysi migratsiyada turganini deploy
oldidan tekshiring — bir nechtasi birdan qo'llanishi mumkin:

```sql
SELECT migration_id FROM "__EFMigrationsHistory" ORDER BY 1 DESC LIMIT 1;
```

Bu deploy'da eng muhimi — `20260910051756_NomzodIzohVaFayllar`. U
nomzodning rezyume MA'LUMOTINI `candidates` dagi to'rtta ustundan yangi
`candidate_files` jadvaliga KO'CHIRADI va keyin o'sha ustunlarni
tashlaydi. Butun migratsiya bitta tranzaksiyada: yo hammasi, yo hech
narsa. Nusxa DOIM `DROP` dan oldin ketadi (avtomatik yaratilgan
variantda teskari edi — qo'lda tuzatilgan), va buni uchta test qadab
qo'ygan (`CandidateFileMigrationTests`).

### ⚠️ Ko'chirishdan oldin — uchta SQL tekshiruvi

Uchalasi ham `0` qaytarishi SHART. Aks holda migratsiya to'xtaydi
(ma'lumot yo'qolmaydi — tranzaksiya orqaga qaytadi) yoki hech qayerga
ishora qilmaydigan «arvoh» fayl yozuvi paydo bo'ladi:

```sql
-- kalit yangi ustunga (varchar(500)) sig'maydi
SELECT count(*) FROM candidates WHERE length(resume_file_key) > 500;
-- bo'sh satrli kalit — «arvoh» fayl yozuvi yasaydi
SELECT count(*) FROM candidates WHERE btrim(resume_file_key) = '';
-- ko'chiriladigan yozuvlar soni: deploy'dan keyin candidate_files
-- dagi qatorlar soni SHU songa teng bo'lishi kerak
SELECT count(*) FROM candidates WHERE resume_file_key IS NOT NULL;
```

⚠️ **Ko'chirish prod hajmida hali sinalmagan.** `backups/` dagi
sakkizta zaxiraning birortasida ham rezyumesi bor nomzod yo'q, ya'ni
mahalliy sinov bu yo'lni qamrab olmadi. Deploy oldidan prod zaxirasini
mahalliy scratch bazaga tiklab, migratsiyani o'sha yerda bir marta
o'tkazing va nomzod kartasida eski rezyume ko'rinishini tekshiring.

⚠️ `ix_candidates_next_contact_date` `CONCURRENTLY` emas (tranzaksiya
ichida bo'lishi mumkin ham emas), ya'ni migratsiya davomida `candidates`
eksklyuziv qulflanadi. Bugungi hajmda bu soniyaning bo'lagi, lekin
`candidates` ustidagi uzoq ochiq tranzaksiya migratsiyani kutishga
majbur qiladi — HR ishlamayotgan paytda deploy qiling.

### ⛔ Hal qilinmagan: `job_offers` jadvali o'chadi

`20260907070127_TaklifOlibTashlandi` `job_offers` jadvalini tashlaydi.
Uning izohida «prod bazasida jadval BO'SH» deyilgan — bu NOTO'G'RI:
oxirgi uchta zaxirada bitta haqiqiy yozuv bor («Birinchi taklif»,
5 500 000 so'm, `sent_at 2026-08-28`). `Down` uni qaytarmaydi. Deploy
o'sha yozuvni butunlay yo'qotadi.

Deploy'dan OLDIN qaror qiling:

```sql
SELECT * FROM job_offers;
```

Test yozuvi bo'lsa — davom eting. Kerak bo'lsa — avval alohida
saqlang.

## Deploy'dan keyin

- [ ] `docker compose -f docker-compose.prod.yml ps` — hammasi `Up`
- [ ] Bot jurnalida `BotWorker[3203] Bot ishga tushdi: @...`
- [ ] **Tizim sozlamalari → «Ommaviy oferta havolasi»** ni to'ldiring.
      Bo'sh qolsa bot oferta qadamini chizmaydi (xato emas, lekin
      rozilik yozilmaydi)
- [ ] Botda `/start`: menyuda 💼 Vakansiyalar va 📋 Anketa to'ldirish
      ko'rinishi; vakansiya tanlangach «Ariza topshirish» chiqishi
- [ ] Nomzodlar → Voronka: kartani boshqa ustunga sudrash
- [ ] Nomzod kartasi: «Anketa» bo'limi va surat
- [ ] **Ko'chirish tekshiruvi**: yuqoridagi uchinchi SQL bergan son
      bilan `SELECT count(*) FROM candidate_files;` TENG bo'lishi kerak
- [ ] Eski rezyumesi bor nomzod kartasini oching — fayl ro'yxatda
      ko'rinishi va yuklab olinishi kerak
- [ ] Bosqich tugmasi (masalan «Гаплашилди») — forma ochilishi, izoh va
      keyingi aloqa sanasi saqlanishi
- [ ] Nomzodlar ro'yxati: «Keyingi aloqa» ustuni va «Bugun aloqa kerak»
      filtri
- [ ] «Rad etilganlar» bo'limi: o'zi voz kechganlar ham shu yerda,
      yorlig'i alohida
- [ ] Yangi rasm yuklang — kartada ko'rsatilgan hajm manbadan sezilarli
      kichik bo'lishi kerak (serverda kichraytiriladi)

## Kutilgan o'zgarishlar (xato emas)

- Bosh sahifadagi **«ochiq vakansiyalar» soni bittaga oshadi** —
  «Umumiy anketa» xizmat vakansiyasi ham `Open` holatda. HR ekranlarida
  u oddiy vakansiya, botda esa ro'yxatda KO'RINMAYDI.
- Nomzodlar ro'yxatida yangi «Umumiy anketa» ustuni/filtri paydo
  bo'lishi mumkin — botdan vakansiyasiz kelgan arizalar o'sha yerda.

## Orqaga qaytish

⚠️ **`Down` endi ma'lumot yo'qotadi** — ilgari bu ro'yxatda teskarisi
yozilgan edi va u `NomzodIzohVaFayllar` uchun to'g'ri emas. Qaytarilsa:

- nomzodning **birinchi** fayli to'rtta ustunga tiklanadi, qolgan
  fayllar yozuvlari YO'QOLADI (fayllarning o'zi diskda yetim qoladi),
- barcha amal izohlari (`candidate_notes`) yo'qoladi,
- barcha «keyingi aloqa» sanalari yo'qoladi.

Kod qaytarish kerak, lekin baza sxemasi kerak bo'lmasa — `Down` ni
umuman ishlatmang. Faqat kodni qaytaring:

```
git checkout <eski-commit>
docker compose -f docker-compose.prod.yml up -d --build
```

Baza sxemasini ham qaytarish kerak bo'lsa — `Down` emas, ZAXIRADAN
tiklash: `./bonvi restore backups/<fayl>.dump`. Yuqoridagi uch xil
yo'qotish shu sababli.

### ⚠️ `ab2c3ba` (rollar, 2026-09-26) dan OLDINGI versiyaga qaytish

Kod qaytarilsa, **rollar yo'qotishli** bo'ladi — sxema o'zgarmagan, lekin
ruxsat kodlari o'zgargan:

- eski seeder `hr_manager` va `main_boss` ni har ishga tushishda eski
  standartga qaytaradi — egasi ekrandan qilgan barcha tahrirlar yo'qoladi;
- egasi yaratgan rollarda faqat yangi 8 ta amal kodi bor
  (`employment.hire` … `employment.correct`), eski kod ularni TANIMAYDI —
  bu rol egalari mehnat amallarining hammasidan mahrum bo'ladi.

Eski image'ni ishga tushirishdan OLDIN rollarga eski kodni qaytaring:

```sql
UPDATE roles
   SET permissions = array_append(permissions, 'employment.manage')
 WHERE permissions && ARRAY['employment.hire','employment.transfer','employment.duty',
                            'employment.salary','employment.terminate',
                            'employment.probation.extend','employment.probation.complete',
                            'employment.correct']
   AND NOT 'employment.manage' = ANY(permissions);
```

Yangi versiyaga qaytganda seeder `employment.manage` ni yana 8 ta kodga
bo'ladi — qo'shimcha qadam kerak emas. Egasiga ogohlantiring: rol
tahrirlari eski versiyada saqlanmaydi.

## 2026-09-27 tuzatishidan keyin — oklad tekshiruvi

2026-09-16 … 2026-09-27 oralig'ida mehnat yo'li yozuvini tuzatish
(buyruq raqami, sana, izoh) ba'zi holatda joriy okladni almashtirib
yuborardi — qo'shimcha vazifa yozuvida okladga vazifa HAQI tushardi.
Deploy'dan keyin prod'da bir marta tekshiring (faqat o'qiydi):

```
docker compose -f docker-compose.prod.yml exec -T postgres \
  sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' \
  < infra/sql/oklad-tuzatish-tekshiruvi.sql
```

Bo'sh natija — xato hech kimga tushmagan. Qator chiqsa, okladni HR
ekrandan «Oklad» amali bilan to'g'rilaydi (tarixda iz qoladi); bazani
qo'lda tahrirlamang.

## Tekshiruv materiallari (2026-09-27) — migratsiya `TekshiruvMateriallari`

- Yangi jadval `employee_check_files`; har bir mavjud mehnat.uz skrinshoti
  unga birinchi material bo'lib **nusxalanadi** (fayl diskda o'z joyida,
  faqat kalit ko'chadi). Deploy'dan keyin tekshirish — faqat KO'CHIRILGAN
  qatorlar sanaladi (HR yangi fayl yuklasa ham to'g'ri qoladi):

  ```sql
  SELECT (SELECT count(*) FROM employees WHERE mehnat_uz_screenshot_key IS NOT NULL) AS eski,
         (SELECT count(*) FROM employee_check_files
           WHERE size_bytes IS NULL AND file_name LIKE 'mehnat-uz-skrinshot%') AS kochgan;
  ```

  Ikkala son teng bo'lishi kerak.
- **HR sahifani yangilasin** (F5 / Cmd+R). Ochiq qolgan eski sahifa
  skrinshotni «yuklanmagan» deb ko'rsatadi va eski yuklash/o'chirish
  so'rovlari 404 oladi — ma'lumot yozilmaydi, lekin chalkashtiradi.
- `employees.mehnat_uz_screenshot_*` ustunlari **ataylab o'chirilmagan** —
  eski image'ga qaytilsa, u shu ustunlarni o'qiydi va ishlayveradi. Ular
  keyingi relizlardan birida, qo'lda yozilgan migratsiya bilan olib tashlanadi.
- `Down` faqat yangi jadvalni o'chiradi: deploy'dan KEYIN yuklangan
  materiallarning yozuvlari yo'qoladi (fayllar diskda qoladi). Ularni saqlash
  kerak bo'lsa — `Down` emas, zaxiradan tiklash.
- ⚠️ **Deploy oldi zaxiradan tiklash** (`restore.sh`, `pg_restore --clean`)
  yangi `employee_check_files` jadvalini O'CHIRMAYDI — u zaxirada yo'q, va
  uning `employees` ga tashqi kaliti `pk_employees` ni qayta yaratishga
  xalaqit beradi (5 ta xato, skript `set -e` bilan to'xtaydi). Keyingi
  deploy'da migratsiya esa jadvalni qayta yaratolmay yiqilardi. Tiklashdan
  OLDIN:

  ```sql
  DROP TABLE IF EXISTS employee_check_files CASCADE;
  ```

  Prod nusxasida sinalgan (2026-09-27): shundan keyin tiklash xatosiz va
  barcha jadval sanoqlari deploy oldidagi bilan AYNAN bir xil.

### Eski image'ga qaytib, keyin YANA shu versiyaga chiqilsa

Migratsiya bu holatda qayta ishlamaydi (u allaqachon qo'llangan), eski kod
esa shu vaqt ichida skrinshotlarni almashtirishi yoki o'chirishi mumkin
(eski kod eski faylni diskdan ham o'chiradi). Ikkita holat:

- rollback paytida yuklangan skrinshot yangi ro'yxatda yo'q;
- rollback paytida almashtirilgan yoki o'chirilgan skrinshotning ko'chgan
  qatori endi yo'q faylga ishora qiladi («Faylni ochib bo'lmadi»).

Shu sababli qayta chiqqandan keyin bir marta (ikkalasi ham idempotent):

```sql
BEGIN;

-- 1. Rollback paytida yuklangan skrinshotlar ro'yxatga qo'shiladi
INSERT INTO employee_check_files
    (id, employee_id, file_key, file_name, content_type, size_bytes, created_at, created_by)
SELECT gen_random_uuid(), e.id, e.mehnat_uz_screenshot_key,
       'mehnat-uz-skrinshot' || CASE e.mehnat_uz_screenshot_content_type
           WHEN 'application/pdf' THEN '.pdf' WHEN 'image/png' THEN '.png'
           WHEN 'image/webp' THEN '.webp' WHEN 'image/jpeg' THEN '.jpg' ELSE '' END,
       coalesce(e.mehnat_uz_screenshot_content_type, 'application/octet-stream'),
       NULL, coalesce(e.updated_at, e.created_at), NULL
  FROM employees AS e
 WHERE e.mehnat_uz_screenshot_key IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM employee_check_files f
                    WHERE f.employee_id = e.id AND f.file_key = e.mehnat_uz_screenshot_key);

-- 2. Eski kod almashtirgan yoki o'chirgan skrinshotning ko'chgan qatori
--    yumshoq o'chiriladi (fayli diskda yo'q)
UPDATE employee_check_files AS f
   SET deleted_at = now()
  FROM employees AS e
 WHERE f.employee_id = e.id
   AND f.deleted_at IS NULL
   AND f.size_bytes IS NULL
   AND f.file_name LIKE 'mehnat-uz-skrinshot%'
   AND f.file_key IS DISTINCT FROM e.mehnat_uz_screenshot_key;

COMMIT;
```

⚠️ Rollback paytida eski interfeys yangi versiyada yuklangan materiallarni
KO'RMAYDI (u faqat bitta skrinshotni biladi) — ular yo'qolmagan, qayta
chiqqach yana ko'rinadi.

## Qo'shimcha haq alohida ruxsat bilan (2026-09-27) — migratsiya `QoshimchaVazifaTugashi`

- Migratsiya faqat bitta bo'sh ustun qo'shadi (`employment_events.duty_end_date`).
  Eski image bu ustunni bilmaydi va unga tegmaydi — image rollback xavfsiz.
- ⚠️ **XATTI-HARAKAT O'ZGARADI:** qo'shimcha vazifa HAQI endi alohida
  `salary.extra.view` ruxsati bilan ko'rinadi. HR menejerlarida u YO'Q —
  deploy'dan keyin ular vazifa summalarini ko'rmay qoladi (egasining qarori).
- **Deploy'dan keyin egasi:** «Rollar» → «Bosh rahbar» → «Qo'shimcha haqni
  ko'rish» ni belgilaydi. Super administratorga u avtomatik tushadi (seeder
  uni har ishga tushishda `Permissions.All` ga tenglaydi).

