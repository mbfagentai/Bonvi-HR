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
* `POST /api/attendance/agent/report` — `[{ employeeId, personNo, action: upsert|remove, ok, error }]`
* Events keep using `POST /api/attendance/ingest` (Generic JSON).

### D5. HR endpoints — JWT, `attendance.manage`

* `GET  /api/attendance/devices/{id}/enrollments` — per-device summary and rows
* `GET  /api/attendance/employees/{id}/enrollments` — per-employee rows (card)
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
* Faces are resized to ≤ 180 KB JPEG before upload.
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

* Employee card → Davomat → a "Qurilmalar" line per device: `Yozildi` /
  `Kutilmoqda` / `Xato: …` / `Rasm yo'q`, and **Qayta yozish**.
* Devices page → per device: enrolled / failed / pending counts, last agent
  contact, and **Hammasini sinxronlash**.

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
* Bir aylanishda bitta kadr — o'sha 2 soniyadagi hamma yangi o'tishga.
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
(faqat tartib raqami bo'yicha, oxirgi o'qishdan bir soat oldindan).

Testlar: `Bonvi.DeviceAgent.Tests` — soxta terminal (sahifaga 30 tadan,
soati surilgan) va soxta server (uzilgan). Har bir tuzatish vaqtincha
qaytarilganda o'z testi yiqilishi tekshirilgan.

⚠️ **Tirik terminalda hali sinalmagan** (ofis tarmog'idan tashqarida
edik). Birinchi sinovda: agentni to'xtatib, 40–50 ta o'tish qiling,
yoqing — hammasi jurnalga tushishi kerak.
