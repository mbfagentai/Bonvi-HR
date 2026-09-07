# SPEC — To'liq anketa (Telegram bot)

> **Manba:** @MBF_jbot (Jobster) — eski bot. Uning anketa oqimi
> screenshot'lar orqali o'rganildi (2026-09-06).
> **Talab:** nomzod OCHIQ VAKANSIYA BO'LMASA HAM anketa to'ldira olsin.
> **Chegara:** platforma prod'da ishlab turibdi — yangilanishdan keyin
> ishlayotgan hech narsa buzilmasligi kerak.

---

## 1. Qabul qilingan qarorlar

Foydalanuvchi 2026-09-06 da tanladi:

| Savol | Qaror | Sabab |
|---|---|---|
| Ariza va anketa alohidami? | **Bitta to'liq oqim** | Eski botga eng yaqini; HR bir xil to'liqlikdagi ma'lumot oladi |
| «Филиал» qadami | **Olinmaydi** | Bitta bosh ofis lokatsiyasi yetadi (2026-08-30 qarori kuchida) |
| Nomzod surati (selfi) | **Olinadi** | Nomzod kartasida rasm ko'rinadi |
| Kutilayotgan maosh | **Qat'iy oraliqlar** | Javoblar guruhlanadi; eski botdagi oraliqlar |
| Vakansiyasiz nomzod qayerga tushadi | **«Umumiy anketa» doimiy vakansiyasi** | `Candidate.VacancyId` majburiy bo'lib qoladi → migratsiya xavfi nol |

---

## 2. Ma'lumot modeli

### 2.1 Yangi jadval: `candidate_questionnaires` (1:1)

Alohida jadval, `candidates` ga ustun qo'shilmaydi. **Sabab:** `candidates`
— tizimning eng ko'p o'qiladigan jadvali (voronka, hisobotlar, qidiruv).
Unga 13 ta nullable ustun qo'shish har bir `SELECT` ni kengaytirardi,
holbuki bu maydonlar FAQAT nomzod kartasida ochilganda kerak.

| Ustun | Tur | Bo'sh | Izoh |
|---|---|---|---|
| `id` | uuid | ✗ | PK (GUID v7 — loyihadagi barcha jadvallar kabi) |
| `candidate_id` | uuid | ✗ | FK → `candidates.id`, `ON DELETE CASCADE`, **unikal indeks** (bitta nomzodda bitta anketa) |
| `education` | int | ✗ | enum: 1 O'rta maktab, 2 O'rta maxsus, 3 Oliy (bakalavr), 4 Oliy (tugallanmagan) |
| `work_experience` | text(2000) | ✓ | erkin matn, o'tkazib yuborish mumkin |
| `marital_status` | int | ✗ | enum: 1 Oilali, 2 Bo'ydoq, 3 Ajrashgan |
| `russian_level` | int | ✗ | enum: 1 Past, 2 O'rta, 3 Ilg'or |
| `other_languages` | text(500) | ✓ | erkin matn |
| `office_skills` | text(1000) | ✓ | erkin matn (Word/Excel/… %) |
| `last_salary` | text(200) | ✓ | erkin matn — «kelishilgan», «3 mln» kabi javoblar uchun |
| `extra_note` | text(2000) | ✓ | «o'zingiz haqingizda qo'shimcha» |
| `has_driver_license` | bool | ✗ | |
| `driver_license_categories` | text(32) | ✓ | faqat `has_driver_license = true` da: B, BC, BCE, BCED |
| `expected_salary` | int | ✗ | enum: 1 (1–2 mln), 2 (2–3), 3 (3–4), 4 (5–7) |
| `referral_source` | int | ✗ | enum: 1 Tanishlar, 2 Instagram/Facebook, 3 OLX.uz, 4 hh.uz, 5 Telegram, 6 Boshqa |
| `offer_accepted_at` | timestamptz | ✓ | ommaviy oferta bilan rozilik VAQTI — huquqiy fakt, bayroq emas. `NULL` = rozilik SO'RALMAGAN (havola sozlanmagan, 21-qadam chizilmagan) |
| `created_at` / `updated_at` | timestamptz | | audit |

**Nega enum, matn emas:** javoblar tugmadan keladi, ya'ni to'plam yopiq.
Matn bo'lsa HR ularni guruhlay olmasdi va til almashganda («Оилали» /
«Oilali») bir xil javob ikki xil qator bo'lib qolardi.

### 2.2 `candidates` jadvaliga bitta ustun

| Ustun | Tur | Izoh |
|---|---|---|
| `photo_key` | text(256), nullable | Selfi. `users.photo_key` / `employees.photo_key` bilan AYNAN bir xil naqsh va bir xil saqlash quvuri (`SquarePhotoStore`, 512×512 JPEG) |

**Nega anketa jadvalida emas:** rasm nomzodning O'ZINIKI, anketa
javobi emas. HR keyinchalik uni nomzodlar ro'yxatida ko'rsatishni
so'raganda (xodimlar ro'yxatidagidek) u joyida turgan bo'ladi.

### 2.3 Mavjud ustunlar — qayta ishlatiladi

`full_name`, `birth_date`, `address`, `phone`, `speciality`,
`telegram_chat_id`, `telegram_language`. Yangi ustun kerak emas.

### 2.4 Migratsiya

Faqat **qo'shimcha**: bitta yangi jadval + bitta nullable ustun.
`DROP` yo'q, `NOT NULL` qo'shilmaydi, mavjud yozuvlarga tegilmaydi →
mavjud ma'lumot uchun xavf nol. `downgrade` jadvalni va ustunni
o'chiradi.

---

## 3. «Umumiy anketa» vakansiyasi

Ochiq vakansiya bo'lmaganda ham nomzod yozuvi biror vakansiyaga
tegishli bo'lishi kerak (`Candidate.VacancyId` — `Guid`, nullable emas).

- `DatabaseInitializer` idempotent tarzda `VacancyTitle` = «Umumiy
  anketa» va unga bog'liq `Vacancy` (holati `Open`) yaratadi.
- Uning `Id` si yangi tizim sozlamasida turadi:
  `recruiting.generalPoolVacancyId`.
- **Bot vakansiyalar ro'yxatida uni KO'RSATMAYDI** — u tanlanadigan
  ish o'rni emas, tushadigan joy. Filtr `GetOpenVacanciesQuery` da.
- HR uchun u oddiy vakansiya: voronka, bosqichlar, rad etish — hammasi
  ishlaydigan holicha qoladi.

---

## 4. Bot oqimi

`ConversationStep` 5 tadan 22 taga o'sadi. Tartib eski botdan, ikkita
farq bilan (pastda).

| # | Qadam | Javob turi | O'tkazib yuborish |
|---|---|---|---|
| 1 | Til | tugma | ✗ |
| 2 | Vakansiya | tugma | ochiq vakansiya bo'lmasa — avtomatik; «Anketa to'ldirish» yo'lida — HAR DOIM |
| 3 | F.I.Sh. | matn | ✗ |
| 4 | Tug'ilgan kun | matn `kk.oo.yyyy` | ✗ |
| 5 | Yashash manzili | matn | ✗ |
| 6 | Telefon | **kontakt tugmasi** | ✗ |
| 7 | Ta'lim darajasi | tugma (4) | ✗ |
| 8 | Mutaxassisligi | matn | ✓ |
| 9 | Ish tajribasi | matn | ✓ |
| 10 | Oilaviy holat | tugma (3) | ✗ |
| 11 | Rus tili darajasi | tugma (3) | ✗ |
| 12 | Boshqa tillar | matn | ✓ |
| 13 | Office dasturlar | matn | ✓ |
| 14 | Oxirgi maosh | matn | ✓ |
| 15 | Qo'shimcha ma'lumot | matn | ✓ |
| 16 | Haydovchilik guvohnomasi | tugma (Ha/Yo'q) | ✗ |
| 17 | Guvohnoma toifasi | tugma (B/BC/BCE/BCED) | faqat 16 = Ha |
| 18 | Surat | rasm | ✓ |
| 19 | Kutilayotgan maosh | tugma (4) | ✗ |
| 20 | Qayerdan bildingiz | tugma (6) | ✗ |
| 21 | Ommaviy oferta | havola + «Roziman» | `bot.offerUrl` bo'sh bo'lsa qadam CHIZILMAYDI, rozilik `NULL` bo'lib saqlanadi |
| 22 | Yakuniy tasdiq | to'ldirilgan anketa + «Yuborish» | ✗ |

### 4.0 Anketaga IKKI yo'l — doimiy menyudan

| Menyu tugmasi | Nima qiladi | Natija |
|---|---|---|
| 💼 Vakansiyalar | ochiq o'rinlar ro'yxatini ko'rsatadi. Nomzod birini tanlaydi → vakansiya nomi va «📝 Ariza topshirish» tugmasi chiqadi → shundan keyin savollar boshlanadi. Ochiq o'rin bo'lmasa — holat aytiladi va anketa taklif qilinadi | tanlangan vakansiyaga |
| 📋 Anketa to'ldirish | vakansiyani HECH QACHON so'ramaydi, darhol 3-qadamdan boshlanadi | «Umumiy anketa»ga |

Qolgan 20 qadam ikkala yo'lda ham AYNAN bir xil.

Ikkala qaror ham foydalanuvchidan (2026-09-07):
1. **«Anketa to'ldirish» alohida tugma** — ochiq vakansiya bor bo'lganda
   ham nomzod ro'yxatdan birini tanlashga majbur bo'lmasligi kerak:
   uning maqsadi aniq bir o'rin emas, umuman ishga joylashish bo'lishi
   mumkin.
2. **«Ariza topshirish» faqat vakansiya tanlangandan keyin** — menyudagi
   tugma endi ro'yxatni ochadi, ariza esa aniq bir o'ringa bog'langan
   holda taklif qilinadi. Ro'yxatdan tugma bosish «qiziqdim» degani,
   «yigirmata savolga javob beraman» degani emas.

Eski botda ham shunday tuzilgan: «Бўш иш ўринлари» yonida «Ўзингиз
ҳақингизда маълумот қолдиринг» alohida turadi.

### 4.1 Eski botdan farqlar (ataylab)

1. **Filial qadami yo'q** — qaror 1-bo'limda.
2. **Telefon faqat kontakt tugmasi orqali.** Eski bot qo'lda yozishga
   ruxsat beradi va `+9980000000` kabi qiymatni uch marta rad etib,
   nomzodni charchatadi. Bizda bu qoida allaqachon bor va saqlanadi:
   terish xatosi bilan yozilgan raqam HR ni nomzodga umuman
   bog'lana olmaydigan holatga qo'yadi.
3. **Takroriy ariza tekshiruvi 6-qadamda** (telefon ma'lum bo'lgan
   zahoti), 22-qadamda emas — nomzod 16 ta savolga javob berib bo'lib
   «siz allaqachon ariza bergansiz» degan xabarni olmasligi kerak.

### 4.2 Yarim qolgan suhbat

Holat XOTIRADA qoladi (`ConversationStore`) — Redis yoki baza
QO'SHILMAYDI. Sabab: bot hech qanday saqlash tizimiga bog'lanmasligi
kerak degan mavjud qoida (izohi `ConversationStore` da) va prod'ga
yangi bog'liqlik qo'shish aynan hozir xavf.

**Narxi va yumshatilishi:** bot qayta ishga tushsa yarim anketa
yo'qoladi. Deploy paytida bu real. Shu sababli:
- `TELEGRAM__CONVERSATIONTIMEOUT` 60 daqiqada qoladi (anketa ~5 daqiqa);
- suhbat topilmaganda bot «/start» emas, darhol til tanlashni
  ko'rsatadi (mavjud xatti-harakat);
- deploy'ni ish vaqtidan tashqari qilish tavsiya etiladi.

### 4.3 Matnlar

Uchala tilda (`uz-Cyrl`, `uz-Latn`, `ru`) QO'LDA yoziladi —
`BotTexts` dagi mavjud qoida. Har bir `switch` da uchala tarmoq ham
alohida yoziladi, `_ =>` faqat lotinni bildiradi.

---

## 5. API

### 5.1 `POST /api/recruiting/candidates` — kengaytiriladi

`RegisterCandidateRequest` ga bitta ixtiyoriy blok qo'shiladi:
`Questionnaire` (yuqoridagi maydonlar). **Barchasi ixtiyoriy** →
eski so'rov tanasi o'zgarishsiz ishlayveradi.

⭐ **Bu deploy tartibini belgilaydi:** avval API, keyin bot. Oraliqda
eski bot yangi API bilan muammosiz ishlaydi.

### 5.2 Yangi: nomzod surati

| Endpoint | Ruxsat | Izoh |
|---|---|---|
| `POST /api/recruiting/candidates/{id}/photo` | `recruiting.manage` | `multipart/form-data`, maydon `file`. Bot ham shu orqali yuklaydi |
| `GET /api/recruiting/candidates/{id}/photo` | `recruiting.view` | `image/jpeg`, `ETag` + `304` |
| `DELETE /api/recruiting/candidates/{id}/photo` | `recruiting.manage` | idempotent |

Xodim rasmi bilan AYNAN bir xil quvur (`SquarePhotoStore`,
`PhotoHttp`) — ikkinchi quvur yozilmaydi.

### 5.3 Yangi: anketani o'qish

`GET /api/recruiting/candidates/{id}` javobiga `questionnaire` bloki
qo'shiladi (nullable). Alohida endpoint YO'Q: HR uni har doim nomzod
kartasi bilan birga ko'radi.

---

## 6. Tizim sozlamalari — yangi kalitlar

| Kalit | Tur | Izoh |
|---|---|---|
| `recruiting.generalPoolVacancyId` | Text | «Umumiy anketa» vakansiyasining `Id` si. `DatabaseInitializer` yozadi, ekranda KO'RINMAYDI |
| `bot.offerUrl` | Text (URL naqshi) | Ommaviy oferta havolasi. Bo'sh bo'lsa 21-qadam umuman chizilmaydi |

---

## 7. Frontend

Nomzod kartasiga «Anketa» bo'limi qo'shiladi:
- surat (bo'lsa) — xodim kartasidagi komponent qayta ishlatiladi;
- javoblar ikki ustunli ro'yxat, bo'sh maydonlar CHIZILMAYDI;
- anketa yo'q nomzodda (HR qo'lda kiritgan yoki eski bot yozuvi) —
  bo'lim umuman ko'rinmaydi, «ma'lumot yo'q» degan bo'sh blok emas.

Yangi ekran, yangi marshrut YO'Q.

---

## 8. Bosqichlar

| # | Ish | Tegiladigan joy |
|---|---|---|
| 1 | Domen + migratsiya | `Bonvi.Domain`, `Bonvi.Infrastructure` |
| 2 | API: ro'yxatga olish, rasm, o'qish | `Bonvi.Application`, `Bonvi.Api` |
| 3 | «Umumiy anketa» seed + sozlamalar | `DatabaseInitializer`, `SystemSettingKeys` |
| 4 | Bot: 22 qadam + matnlar | `Bonvi.Bot` |
| 5 | Frontend: anketa bo'limi | `web/src/features/recruiting` |
| 6 | Testlar + build | `pytest`-analog: `dotnet test`, `npm run build` |

Har bosqichdan keyin build va testlar yashil bo'lishi shart —
oxirida bir marta emas.

## 9. Prod xavfsizligi

- Migratsiya faqat qo'shimcha, `DROP` yo'q → orqaga qaytarish xavfsiz.
- API eski bot bilan mos → deploy tartibi: **API → bot → web**.
- Deploy oldidan `./bonvi backup`.
- Eski nomzod yozuvlariga tegilmaydi: `questionnaire` ularda `null`.
