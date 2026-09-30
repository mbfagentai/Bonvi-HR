# SPEC — Device agent and enrollment sync (Verifikatsiya, wave 6)

> Status: authoritative for this wave. Written 2026-09-21. Branch: `verification`.
> Source: the owner, 2026-09-21, after the first live end-to-end pass on the
> MinMoe terminal (DS-K1T341AMF, fw V3.7.0) through a temporary bridge script.

---

## A. What the owner decided

| # | Decision |
|---|---|
| A1 | **Parallel with Verifix.** The terminal's HTTP push stays Verifix's; Bonvi never touches the device's notification settings. |
| A2 | Employees reach the terminal **automatically** (hire, photo change) **and** by a **manual button** ("Qayta yozish" on the card, "Hammasini sinxronlash" on the device). |
| A3 | On **termination** the person is **removed from every terminal automatically** — otherwise a former employee keeps opening the door. |

## B. Facts established on the live device (2026-09-21)

* The firmware supports **one** HTTP push target. Writing a second one silently
  replaced Verifix's (restored within a minute, no passes lost) — hence A1: Bonvi
  only ever **reads** events and **writes persons**, never notification config.
* `POST /ISAPI/AccessControl/AcsEvent?format=json` returns the event log
  (`employeeNoString`, `time`, `serialNo`, `currentVerifyMode`); no
  `attendanceStatus` (direction arrives as Unknown — the engine alternates).
* `pictureURL` is absent because the device has `saveCapPic=false`,
  `uploadCapPic=false`. Turning them on is a device change that Verifix would
  also see — **owner's call, not done**.
* Capacity 3000 persons / 3000 faces. `UserInfo/Record` + `FDLib/FaceDataRecord`
  (multipart, JPEG ≤ ~200 KB) enroll a person and a face.

## C. Why an agent, and why it pulls

The production API (hr.bonvi.uz, a VPS) **cannot reach** a terminal on the office
LAN. So every device operation runs on an **agent inside the LAN**, and the agent
initiates every connection: it pulls events from the device and pushes them to
Bonvi; it pulls the **roster** (who must be on the device) from Bonvi and makes
the device match it. No inbound port is opened anywhere.

**Reconciliation, not an event queue.** The agent does not replay "hired",
"photo changed", "terminated" messages. It compares *desired* (Bonvi's roster)
with *actual* (the device's user list) and fixes the difference. That covers all
three triggers with one mechanism, survives the agent being offline for a week,
and cannot drift.

**The agent never deletes a person it did not create.** Bonvi-managed persons
live in their own number space (`9` + 8 digits, allocated by Bonvi). Anything
else on the device — Verifix's people on a shared terminal — is invisible to the
reconciler.

## D. Server

### D1. Person numbers are identities

The number written to the device **is** an `AttendanceIdentity` with provider
`Hikvision` — the existing mapping table, so a pass resolves to the employee with
no new lookup. Numbers are allocated by the server when an employee first appears
in a roster: `9` followed by an 8-digit sequence, unique by the existing
`(provider, external_person_id)` index.

### D2. Desired roster

An employee is on the roster of a device when they have an employment covering
today **and** a photo. Without a photo a face terminal cannot recognise them, so
they are listed with `reason: "photo_missing"` and the card says so.

### D3. `device_enrollments` — last known state per (device, employee)

`status`: `Synced` · `Failed` · `Removed`; `photoKey` that was written;
`error` (device's own message, e.g. face quality); `syncedAt`; `forceVersion`
(bumped by the manual buttons). The server never guesses success: only the
agent's report sets `Synced`.

### D4. Agent endpoints — device-token auth (`X-Bonvi-Device-Token`), like ingest

* `GET  /api/attendance/agent/roster` — `[{ employeeId, personNo, name, photoVersion, force }]`
* `GET  /api/attendance/agent/photo/{employeeId}` — the employee photo bytes
* `POST /api/attendance/agent/report` — `[{ employeeId, personNo, action: upsert|remove, ok, error, rejected }]`
* `POST /api/attendance/agent/status` — `{ heldRemovals, foreignPersons }` once per reconcile (§L, 2026-09-27)
* Events keep using `POST /api/attendance/ingest` (Generic JSON).
* Every agent call carries `X-Bonvi-Terminal-Serial` (§L).

### D5. HR endpoints — JWT, `attendance.manage`

* `GET  /api/attendance/devices/{id}/enrollments` — per-device summary and rows
* `GET  /api/attendance/employees/{id}/enrollments` — per-employee rows (card).
  Until 2026-09-27 this was wrongly under `attendance.view`; it is `manage` now
  (the rows carry the terminal person number) and the card is not rendered
  without it.
* `POST /api/attendance/devices/{id}/enrollments/approve-removals` — confirm
  removals held by the mass-removal guard, for 10 minutes (§L)
* `POST /api/attendance/employees/{id}/enrollments/resync` — the card button
* `POST /api/attendance/devices/{id}/enrollments/resync` — "sync everything":
  asks the agent to **reconcile now**. It does NOT force-rewrite every person
  (owner, 2026-09-21: "qayta ustiga yozib qo'ymaydimi?") — see E, "In place".

## E. Agent (`Bonvi.DeviceAgent`, .NET worker, Docker)

Same stack and shape as `Bonvi.Bot`. Config from environment only — Bonvi URL,
device token, device host, device user and password; nothing in git.

* **Events loop** (5 s): `AcsEvent` from the last `serialNo` → Generic JSON →
  ingest. Cursor kept in a small state file on a volume.
* **Roster loop** (60 s, or immediately when any `force` is set): fetch roster;
  list device users (`UserInfo/Search`); for each Bonvi person missing or with a
  changed `photoVersion` → write user + face; for each Bonvi-range person not in
  the roster → delete; report every outcome.
* ~~Faces are resized to ≤ 180 KB JPEG before upload.~~ Not done — see H6:
  the agent checks the size (≤ 200 KB) **before** writing the person and
  reports a too-large photo as a rejection.
* **In place, never delete-and-recreate** (owner, 2026-09-21). A person that is
  on the device but stale (photo or name changed, face missing — `numOfFace = 0`,
  or the card's "Qayta yozish") is **modified under the same number**
  (`UserInfo/Modify`, face replaced for the same `FPID`). The number never
  changes, so passes before and after stay on one employee, and there is no
  window in which the person cannot open the door. A person that is present and
  current is **not touched**. Pressing "sync everything" twice is a no-op the
  second time.
* The card's "Qayta yozish" is the only thing that forces a rewrite, and only
  for that one employee.

## F. Screens

* ~~Employee card → Davomat → a "Qurilmalar" line per device, and **Qayta
  yozish**~~ — **removed 2026-09-27 (owner):** redundant. The devices page
  lists every failed person with photo, and a new photo or name re-arms the
  person automatically (§E, §L). The endpoints `GET /employees/{id}/enrollments`
  and `POST /employees/{id}/enrollments/resync` stay (manage-only, tested) but
  no screen calls them.
* Devices page → per device: enrolled / failed / pending counts, the failed
  people with photos, last agent contact, and **Hammasini sinxronlash**.

## G. Definition of done

* Tests: roster rules (no photo, terminated, number allocation idempotent),
  agent auth (wrong token 401, deactivated device 403), report sets status,
  resync bumps `force`.
* The temporary `bridge.py` is retired; the agent reproduces today's live test:
  enroll → pass → journal.

---

## H. Amalga oshirilgani (2026-09-21)

Shartnomadan chetga chiqilgan yoki aniqlashtirilgan joylar — qoralama
emas, bajarilgan qaror:

| # | Qaror | Sabab |
|---|---|---|
| H1 | Raqam `9` + **8 xonali ketma-ketlik** (`device_person_no_seq`), «eng kattasi + 1» EMAS | Ikki agent bir vaqtda ro'yxat so'rasa, ikkinchi usul ikkalasiga bir xil raqam berardi |
| H2 | Faza tekshiruvi — **uzunlik 9 va birinchi belgi `9`**, raqamlilik SHART EMAS | Birinchi uchta xodim vaqtinchalik skript bilan `9`+xodim id'sining oxirgi 8 belgisi (`93EAF62FD`) bilan yozilgan; ular qayta raqamlanmasligi kerak |
| H3 | Rasmi yo'q xodim agent ro'yxatiga **umuman tushmaydi** (§D2 dagi `reason: photo_missing` o'rniga) | Agent uni har 60 soniyada qayta urinib, terminaldan bir xil xatoni olib turardi. HR ekranida u «Rasm yo'q» bo'lib ko'rinadi — holat SERVERDA hisoblanadi (`EnrollmentState`) |
| H4 | «Hammasini sinxronlash» — faqat **xato** qatorlarni qayta urinishga qo'yadi | Yetishmagan odamni agent tugmasiz ham yozadi (har tekshiruv = solishtirish), joyida turganiga esa tegilmaydi. Egasining «qayta ustiga yozib qo'ymiydimi?» savoliga javob |
| H5 | Hisobotda `personNo` ham bor | O'chirilgan odamni agent ro'yxatdan topa olmaydi — terminalda faqat raqam qoladi; server uni mosliklar orqali xodimga qaytaradi |
| H6 | Agent rasmni **qayta siqmaydi** | Bonvi rasmni 512×512 JPEG (sifat 85) qilib saqlaydi — ~40–120 KB, terminalning ~200 KB chegarasidan past. Oshib ketsa bu XATO holat va u ekranda ko'rinishi kerak |
| H7 | Agent `docker-compose` da **`agent` profili** ostida — sukut bo'yicha ko'tarilmaydi | Terminal mijozning tarmog'ida; ishlab chiqishda u yo'q. Ishlab chiqarishda konteyner ofisdagi mashinada ishlaydi |
| H8 | Yo'nalish agentda **doim `Unknown`** | Terminal `attendanceStatus` bermaydi (§B); navbat bilan kirish/chiqishga ajratish serverda, `PassPairing` da |

### ISAPI va .NET: uchta tuzoq (2026-09-21, tirik qurilmada)

Uchalasi ham BIR XIL javob berardi — `400 MessageParametersLack`, «tanada
maydon yetishmayapti» — holbuki tana to'g'ri edi. Aynan bir xil so'rov
`curl` va Python'dan o'tardi. Farqlar faqat tarmoq oqimini o'qib topildi:

| # | Tuzoq | To'g'ri yo'l |
|---|---|---|
| 1 | `JsonSerializerDefaults.Web` nomlarni camelCase qiladi: `UserInfo` → `userInfo` | `PropertyNamingPolicy = null` — nom aynan kodda yozilganidek |
| 2 | `MultipartFormDataContent.Add(part, "FaceDataRecord")` qism nomini TIRNOQSIZ yozadi va rasmga `filename*=utf-8''…` qo'shadi | `ContentDisposition` ni qo'lda, nomlarni tirnoq bilan |
| 3 | .NET `boundary` ni tirnoqqa oladi (`boundary="…"`) | `ContentType.Parameters` ni tozalab, tirnoqsiz qo'yish |

Shuningdek `charset=utf-8` ham olib tashlangan (`Content-Type:
application/json`) — u o'zi xato bermadi, lekin `curl` bilan bir xil bo'lsin.

⚠️ **Xulosa:** ISAPI so'rovlarini o'zgartirgandan keyin natijani tirik
qurilmada tekshiring. Firmware'ning xato matni sababni KO'RSATMAYDI.

### Dev muhitida sinash (macOS)

Docker Desktop VM'si Wi-Fi LAN'dagi qurilmaga chiqa olmaydi — konteynerdan
terminalga so'rov muddati tugaydi. Ishlab chiqarishda muammo yo'q: agent
ofisdagi mashinada, terminal bilan bitta tarmoqda ishlaydi. Sinash uchun
xostda oddiy TCP relay ko'tarilib, `AGENT__DEVICEHOST=host.docker.internal:<port>`
qilinadi.

### Yo'l-yo'riq: agentni yoqish

```
# .env
AGENT__DEVICETOKEN=<qurilma kartasida bir marta ko'rsatilgan token>
AGENT__DEVICEHOST=192.168.1.50
AGENT__DEVICEUSER=admin
AGENT__DEVICEPASSWORD=...

COMPOSE_PROFILES=agent ./bonvi up
```

Ishlab chiqarish (ofisdagi mashina) uchun alohida fayl va qo'llanma:
`docker-compose.agent.yml` va `docs/AGENT-RUNBOOK.md` (§L).

⚠️ Noto'g'ri parol bilan urinish terminalda hisobni QULFLAYDI
(`retryLoginTime` kamayadi). Parolni taxmin qilib ko'rmang.

---

## I. Kelajak kunlar va soat og'ishi (2026-09-21, egasining so'rovi)

### I1. Kelajakdagi ish kuni «Kelmadi» EMAS

Muammo: oy o'rtasida ochilgan tabelning qolgan yarmi qizil edi va
«kelmagan kunlar» sanog'i oy oxiridagi son bilan bir xil ko'rinardi.
Bazada ertangi kunga allaqachon `Absent` qatorlari yotardi (yo'qlik
so'rovi qamragan oraliq qayta hisoblanganda yaratilgan).

Yechim — yangi holat `AttendanceDayStatus.Planned` («Kun kelmagan»):

* bugundan keyingi ish kuni, o'tishi yo'q → `Planned`, katak BO'SH;
* qator SAQLANMAYDI (`ShouldStore = false`) — mavjudlari qayta
  hisoblashda o'chadi;
* hech qaysi sanoqqa kirmaydi: na kelgan, na kelmagan;
* **istisno** — tasdiqlangan yo'qlik so'rovi kelajak qoidasidan USTUN:
  keyingi haftaga olingan ruxsat bugundanoq «Sababli» bo'lib ko'rinadi
  va yozuvi saqlanadi, chunki qaror BUGUN qabul qilingan;
* o'tishi bor kelajak sana (tungi smenaning chiqishi) odatdagidek
  hisoblanadi.

«Bugun» hisoblovchiga TASHQARIDAN beriladi (`AttendanceDayBuilder`
konstruktori) — sinf toza funksiya bo'lib qolishi uchun.

### I2. Soat og'ishi — to'g'ridan-to'g'ri o'lchov

Eski o'lchov `qabul qilingan − sodir bo'lgan` edi. Qurilma o'zi push
qilganda bu soat farqi edi; agent TORTIB olganda esa unga yetkazib
berish kechikishi ham qo'shiladi. 2026-09-21 da agent 13 daqiqa o'chib
turdi va qaytganda eski o'tishlarni yubordi — soati soniyagacha to'g'ri
terminal «nosoz» bo'lib ko'rindi.

Yechim: agent har tekshiruvda terminaldan uning JORIY vaqtini so'raydi
(`GET /ISAPI/System/time`) va `POST /api/attendance/agent/clock` orqali
yuboradi. Server uni o'z joriy vaqti bilan solishtiradi va farqni
`attendance_devices.clock_offset_seconds` ga yozadi. Ekran va
`HasClockDrift` endi shu ustundan o'qiydi; eski `last_skew_seconds`
zaxira bo'lib qoladi (o'zi push qiladigan qurilmalar uchun).

⚠️ Yana bitta ISAPI tuzog'i: **GET so'rovida tana bo'lmasligi kerak** —
bo'sh `StringContent` ham `Content-Length: 0` qo'shadi va firmware
so'rovni rad etadi.

---

## J. O'tish suratlari — jonli kadr (2026-09-22, egasining so'rovi)

Egasi: suratlar bizning bazaga ham tushsin, **lekin Verifix uzilib
qolmasin**.

### Nega terminalning o'z surati emas

Terminal o'tish suratini faqat `AcsCfg.saveCapPic` / `uploadCapPic`
yoqilganda saqlaydi. Bu **butun qurilmaning** sozlamasi — alohida qabul
qiluvchi uchun yoqib bo'lmaydi. Yoqilsa Verifix'ga boradigan hodisalar
ham surat bilan keladi va tanasi bir necha yuz kilobaytga o'sadi; Verifix
qabul chegarasi kichik bo'lsa, hodisalar rad etiladi. Sinab ko'rish
aynan «qurilma sozlamasiga tegmaymiz» (§A1) va'dasini buzish bo'lardi.

### Yechim

Agent yangi o'tishni o'qigan zahoti kameradan **jonli kadr** oladi
(`GET /ISAPI/Streaming/channels/101/picture` — oddiy o'qish so'rovi,
qurilma sozlamasiga tegmaydi) va uni hodisaga `photoBase64` bo'lib
qo'shadi. Qabul yo'li buni allaqachon qo'llab-quvvatlaydi — serverda
o'zgarish yo'q.

* MinMoe'da kanal `101` (`1` — 404). 79 KB JPEG, 0.07 soniya.
* Hodisa halqasi 5 → **2 soniya**: kadr o'tgan odamni hali terminal
  oldida ushlashi uchun.
* ⚠️ Kadr faqat **6 soniyadan yangi** o'tishga biriktiriladi. Agent
  uzilib qaytganda yuboriladigan eski o'tishlar SURATSIZ qoladi —
  ularning kadrida boshqa odam yoki bo'sh xona bo'lardi, noto'g'ri
  surat esa suratsizlikdan yomonroq.
* Bir aylanishda bitta kadr — va u faqat ENG YANGI o'tishga (2026-09-27 dan).
  Ilgari o'sha 2 soniyadagi hamma yangi o'tishga biriktirilardi: kadrda esa
  oxirgi odam turadi, va o'nta o'tishli bo'lakda bir xil kadr o'n marta
  ketib, qabul yo'lining 2 MB chegarasidan oshardi (413).
* Kadr — tanib olish lahzasidan 1–3 soniya keyingi surat, tanib olingan
  kadrning o'zi emas. Nazorat uchun («rostan u o'tdimi») bu yetarli.
* Sozlama: `AGENT__SNAPSHOTCHANNEL` (bo'sh — kadr olinmaydi).


---

## K. Uzilishdan keyin o'tish yo'qolmasligi (2026-09-26, egasining savoli)

Savol: «internet bo'lmay qolsa, o'tishlar terminalda saqlanib turadimi va
aloqa qaytgach serverga yuklanadimi?» Asosi to'g'ri edi — terminal
hodisani o'z jurnalida saqlaydi, o'qilgani uchun o'chirmaydi, kursor faqat
server qabul qilgandan keyin suriladi. Lekin uchta teshik bor edi va
ularning har biri o'tishni JIMGINA yo'qotardi:

| # | Teshik | Tuzatish |
|---|---|---|
| 1 | Sahifa oxiri «so'ralgan 50 tadan kam keldi» bilan aniqlanardi. Firmware sahifaga 30 tadan bersa, uzilishdan keyin faqat birinchi 30 ta ketardi. Odamlar ro'yxatida ham xuddi shunday. | Oxirini terminal javobi aytadi: `responseStatusStrg` (`MORE`/`OK`/`NO MATCH`), bo'lmasa `totalMatches`, u ham bo'lmasa — bo'sh sahifagacha. |
| 2 | Butun o'qishga 8 soniya. Bir necha kunlik qoldiq sig'masdi — agent abadiy aylanardi. | Muddat har SO'ROVGA. Qoldiq 300 talik bo'laklarda, to'xtamasdan; har bo'lakdan keyin kursor saqlanadi. |
| 3 | Qidiruv oralig'i AGENT soatida edi («hozir − 1 daqiqa»). Terminal soati bir daqiqadan ko'proq orqada bo'lsa, uzilishsiz ham o'tish yo'qolardi. | Oraliq oxirgi uzatilgan hodisaning TERMINAL vaqtidan boshlanadi (`LastEventTime`), terminal «hozir»idan keyin emas; terminal soati har daqiqada o'lchanadi. |

Yo'l-yo'lakay yopilgan ikki holat:

* **Buzilgan terminal soati** (2000 yoki kelgusi yil). Server 30 kundan uzoq
  og'ishni rad etadi va bitta shunday hodisa butun so'rovni yiqitadi —
  agent o'sha bo'lakni cheksiz qayta yuborib, keyingi hamma o'tishni
  tiqib qo'yardi. Endi 29 kundan uzoqlari yuborilmaydi va jurnalga
  yoziladi (6109).
* **Jurnal tozalanib, raqamlar 1 dan qayta boshlansa.** Oxirgi uzatilgan
  vaqtdan KECH hodisa raqamidan qat'i nazar yangi hisoblanadi; qabul
  kaliti endi `minmoe-{raqam}-{vaqt}` — aks holda yangi «5-hodisa» oylar
  oldingisining takrori deb tashlanardi.

Eski holat fayli bilan birinchi ishga tushishda takror yuborilmaydi
(faqat tartib raqami bo'yicha, oxirgi o'qishdan bir soat oldindan). Holat
fayli UMUMAN yo'q bo'lsa — 48 soat orqaga (`AGENT__COLDSTARTHOURS`, §L).

Testlar: `Bonvi.DeviceAgent.Tests` — soxta terminal (sahifaga 30 tadan,
soati surilgan) va soxta server (uzilgan). Har bir tuzatish vaqtincha
qaytarilganda o'z testi yiqilishi tekshirilgan.

⚠️ **Tirik terminalda hali sinalmagan** (ofis tarmog'idan tashqarida
edik). Birinchi sinovda: agentni to'xtatib, 40–50 ta o'tish qiling,
yoqing — hammasi jurnalga tushishi kerak.

---

## L. Audit tuzatishlari (2026-09-27)

Terminal yo'li, agent va qurilmalar ekranining auditi. Har bir qator —
bajarilgan qaror; testlari `Bonvi.DeviceAgent.Tests` (`ReconcileTests`,
`EventForwardingAuditTests`), `AttendanceAgentHardeningApiTests`,
`AttendanceDeviceHealthTests`, `EnrollmentStateAndHealthTests` da.

### L1. Solishtirish — hamma-yoki-hech narsa emas

| # | Qaror | Sabab |
|---|---|---|
| L1.1 | Har bir xodim ALOHIDA: rasm yuklanmasa (tarmoq, 5xx, muddat) yoki terminal rad etsa — o'sha qator `ok:false`, qolganlari davom etadi. Ketma-ket **3** ta vaqtinchalik xato — aylanish to'xtaydi (terminal/server umuman javob bermayapti) | Ilgari bitta `HttpRequestException` butun aylanishni yiqitardi: hisobot yo'qolar, o'chirish bajarilmas, birinchi o'rnatishdagi ~500 kishi hech qachon oxirigacha yozilmasdi |
| L1.2 | Tartib: terminal seriyasi → soat → ro'yxat → **o'chirish** → yozish | O'chirish — xavfsizlik (ishdan ketgan odam eshikni ochmasin); yozish o'n daqiqalab cho'zilishi mumkin |
| L1.3 | Hisobot har **20** qatorda va `finally` da (o'z 8 soniyalik muddati bilan) yuboriladi | Uzilish yoki konteyner to'xtashi bajarilgan ishni yo'qotmasin |
| L1.4 | Rasm **birinchi** tekshiriladi (yo'q yoki > 200 KB) — terminalga umuman tegilmaydi | Ilgari odam yozilib, keyin rasm rad etilardi: terminalda yuzsiz odam qolardi |
| L1.5 | Xato ikki xil: **qat'iy** (`rejected: true` — terminal 4xx, rasm yo'q/katta) va **vaqtinchalik**. Server qat'iy rad etilgan belgini eslaydi (`device_enrollments.rejected_version`) va ro'yxat uni `hold: true` qilib beradi — agent uni yozmaydi ham, o'chirmaydi ham. Belgi o'zgarsa (rasm, ism, «Qayta yozish», «Hammasini sinxronlash») — qayta urinadi | Ilgari rad etilgan odam har 60 soniyada, kuniga ~1440 marta qayta urinilardi. «Hammasini sinxronlash» izohidagi «aks holda xato holatida turib qolardi» endi haqiqat |
| L1.6 | Muvaffaqiyatsiz o'chirish `ok:false` bo'lib keladi → holat `RemoveFailed` → ekranda **«Xato»** | Ilgari har doim «bajarildi» deb kelardi va HR «O'chirilgan» ni ko'rardi, odam esa eshikni ochib yuraverardi |
| L1.7 | Ro'yxatdan chiqqan, lekin terminalga HECH QACHON yozilmagan odam — «O'chirilgan» (olib tashlaydigan narsa yo'q) | «Navbatda» abadiy turib qolardi |

### L2. Token — jismoniy terminalga bog'langan; ommaviy o'chirish to'sig'i

* Agent har aylanishning boshida `GET /ISAPI/System/deviceInfo` (faqat
  o'qish) dan seriyani oladi va HAR so'rovda `X-Bonvi-Terminal-Serial`
  sarlavhasida yuboradi. Server birinchi kelganini yozadi
  (`attendance_devices.terminal_serial`, trust on first use) va keyin
  boshqasi yoki seriyasiz kelgan ro'yxat/hisobot/soat/holat so'rovini
  `davomat.terminal_mos_emas` (409) bilan rad etadi. Qabul yo'lida sarlavha
  ixtiyoriy (o'zi push qiladigan terminal yubormaydi), lekin berilsa —
  tekshiriladi. Seriya qurilmalar ro'yxatida ko'rinadi. Terminal
  almashtirilsa — tokenni yangilash bog'lanishni tozalaydi.
* HR kiritadigan `serial_number` bilan solishtirilmaydi — u yorliqdan qo'lda
  yoziladi va bitta xato agentni butunlay to'xtatardi.
* Ro'yxat endi O'CHIRILADIGAN raqamlarni ham beradi (`removeNumbers`) —
  faqat SHU server ajratgan, ro'yxatda yo'q raqamlar. Bonvi fazasidagi
  boshqa raqam **begona**: agent unga tegmaydi va sonini aytadi
  (`foreignPersons`). Ilgari bitta terminalga ulangan ikki server (ishchi va
  sinov) bir-birining odamlarini har daqiqada o'chirib turardi.
* **Ommaviy o'chirish to'sig'i:** ro'yxat bo'sh bo'lsa yoki terminaldagi
  Bonvi odamlarining **beshdan biridan** ko'pi (kamida 4 ta) bir aylanishda
  o'chishi kerak bo'lsa — agent o'chirmaydi, sonini `POST /agent/status`
  bilan yuboradi (`heldRemovals`), HR esa qurilma kartasida sariq
  ogohlantirishni va «tasdiqlash» tugmasini ko'radi. Tugma to'siqni
  **10 daqiqaga** ochadi (`removals_approved_until`); doimiy o'chirib qo'yish
  yo'q.
* ⚠️ **Qolgan xavf:** ikki server bitta terminalga yozsa, ikkalasi ham bir
  xil raqam ajratishi mumkin (`900000001`) — agent o'sha raqamdagi odamni
  «o'ziniki» deb ustiga yozadi. Buni koddan to'liq yopib bo'lmaydi; lekin
  endi ikkala serverning kartasida ham «begona» ogohlantirishi chiqadi.
  Qoida: sinov agentini hech qachon ishchi terminalga ulamang.

### L3. Raqam o'zgarmaydi

* Bonvi fazasida xodimga BITTA raqam — bazadagi unikal indeks
  `ux_attendance_identities_managed_employee` (`provider, employee_id`,
  faqat 9 belgili «9…» raqamlar). Ikki agent so'rovi bir vaqtda ajratsa,
  ikkinchisi `davomat.agent_royxat_band` (409) oladi va keyingi so'rovda
  birinchisining raqamini ko'radi.
* Raqamlar YARATILISH tartibida o'qiladi — `Prefer` har safar o'sha raqamni
  tanlaydi.
* «Mosliklar» sahifasida tizim raqamini uzib bo'lmaydi
  (`davomat.moslik_bonvi_raqami`, ekranda qulf), xodimga ikkinchisini qo'lda
  qo'shib ham bo'lmaydi (`davomat.moslik_bonvi_raqami_bor`).
* Rasmi o'chirilgan, lekin terminalga allaqachon yozilgan xodim ro'yxatda
  QOLADI (o'sha belgi bilan, qayta yozishsiz) — faqat ishdan ketganda
  chiqadi. Ilgari rasmni almashtirish uchun o'chirish odamni hamma
  terminaldan olib tashlardi.
* Belgi endi ismni ham o'z ichiga oladi: `rasm:majburiyat:ism izi` (8
  belgili SHA-256). Familiya o'zgarsa, agent odamni o'sha raqam ostida qayta
  yozadi. ⚠️ Format o'zgargan kuni ilgari yozilgan har bir odam bir marta
  qayta yoziladi (joyida, zararsiz).

### L4. Hodisalar halqasi

| # | Qaror | Sabab |
|---|---|---|
| L4.1 | Qidiruv boshi — alohida kursor `SearchAnchor`: oxirgi uzatilgan (tartib raqami bo'yicha eng oxirgi) hodisaning vaqti, PASAYISHI mumkin. Terminal soati undan ham orqada bo'lsa, boshi o'sha lahzaga QOTIRILADI va saqlanadi. `LastEventTime` faqat o'sadi va faqat «yangimi» savoliga javob beradi | Soat orqaga to'g'rilangandan keyin qidiruv «hozir − 2 daqiqa» ga tushardi; agent ikki daqiqadan uzoq uzilsa, oradagi o'tishlar yo'qolardi |
| L4.2 | Rad etilgan urinishlar (6, 7, 10, 76 va h.k.) o'tish sifatida YUBORILMAYDI; `minor` qabul yo'liga ham uzatiladi va server ikkinchi marta tashlaydi. Ro'yxat bitta faylda (`HikvisionEventTypes.cs`) — server va agent uni birga kompilyatsiya qiladi | Rad etilgan urinish navbat bilan kirish/chiqishni kunning qolgan qismiga ag'darardi |
| L4.3 | Holat fayli yo'q bo'lsa 48 soat orqaga o'qiladi (`AGENT__COLDSTARTHOURS`) | Qayta yuborish zararsiz (kalit — raqam + vaqt) |
| L4.4 | Tartib aniq so'raladi (`timeReverseOrder: false`); teskari sahifa sezilsa — oxirigacha o'qiladi va ENG ESKI 300 tasi uzatiladi | Teskari tartibda eng yangi 300 tasi uzatilib, eskilari kursor ortida qolardi |
| L4.5 | Jonli kadr faqat eng yangi o'tishga | §J |

### L5. Qayta urinish va jurnal

* Xatodan keyin kutish ikki barobardan o'sadi: 2 → 4 → 8 … **60 soniya**.
  Stek izi faqat birinchi xatoda; keyingilari bitta qator; tiklanganda
  «tiklandi».
* Terminal paroli rad etilsa (401/403), Bonvi tokeni rad etilsa yoki token
  boshqa terminalga bog'langan bo'lsa — **10 daqiqa**: tez urinish terminal
  hisobini qulflaydi (§H).
* `docker-compose` da agent jurnali 10 MB × 3 fayl bilan cheklangan (ilgari
  10 daqiqada 4281 qator, chegarasiz).

### L6. Aloqa holati — agent va terminal ALOHIDA

* `agent_seen_at` — agent endpointlari (ro'yxat, hisobot, soat, holat);
  `terminal_seen_at` — terminalning O'ZI javob bergani (soat o'qildi, hodisa
  keldi). Ilgari hammasi `last_seen_at` ga tushardi: agent ro'yxat so'rab
  turganda besh kundan beri javob bermayotgan terminal «hozirgina» deb
  ko'rinardi.
* Holat (`linkState`) serverda: agent **10 daqiqadan** beri jim —
  `AgentOffline`; agent tirik-u terminal jim (yoki hech qachon javob
  bermagan) — `TerminalUnreachable`. Chegara **jadvalga bog'liq emas**:
  agent o'tish bo'lmasa ham har daqiqada gapiradi, ya'ni tunda va dam olish
  kunida ham jimlik — uzilish. O'zi push qiladigan terminal uchun jimlik
  hech narsa demaydi.
* Uzilish — **QIZIL** (egasi, 2026-09-27): Statistika sahifasida
  (`deviceHealth.outages`, `GET /dashboard` va `/dashboard/trend`,
  `attendance.view`) va Qurilmalar sahifasida — qurilma nomi va qachondan
  beri bilan.
* «Ulangan, lekin hodisalari rad etilyapti» — endi AYNAN rad etilgan
  so'rovdan (`last_rejected_at`), `last_seen_at − last_event_at > 30 daqiqa`
  taxminidan emas: u agent qurilmasida har kechasi yolg'on yonardi.

### L7. Ekran

* «Mobil ilova» qatori qurilmalar ro'yxatida faqat O'QISH uchun: almashtirgich,
  tahrir, token va o'chirish tugmalari yo'q, qatori bosilmaydi. Jurnaldagi
  turi «Mobil ilova» (ilgari «Umumiy JSON»).
* Yozish holati qatorlarida xodimning HAQIQIY rasmi.
* `enrollmentResynced` / `enrollmentAgentSeen` tarjimalarida `{n}` → `{{n}}`
  (xom ko'rinardi); `check-locales.mjs` endi bitta qavsli o'rin belgisini
  xato deb topadi.

### L8. Ishlab chiqarish

`docker-compose.agent.yml` (ofisdagi mashina uchun alohida fayl: tom, qayta
ishga tushish, jurnal chegarasi, muhit) va `docs/AGENT-RUNBOOK.md`.
