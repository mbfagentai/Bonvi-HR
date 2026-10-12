# Bonvi HR — tashqi API

Holdingning boshqa loyihalari uchun **faqat o'qish** interfeysi. Bu yerdan
xodimlar ro'yxati, ularning tashkiliy ma'lumoti va suratlari olinadi.

> **Trafik bir tomonli.** Faqat `GET` bor. Siz yuborgan hech narsa BonviHR
> ichida hech narsani o'zgartira olmaydi.

---

## 1. Kalit olish

Kalitni BonviHR administratori beradi: **Sozlamalar → Tashqi API → Yangi kalit**.

Kalit `bonvi_ab12cd34_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx` ko'rinishida bo'ladi va
**bir marta** ko'rsatiladi. U bazada xeshlangan holda yotadi — administrator ham,
biz ham uni qaytarib o'qiy olmaymiz. Nusxa olinmasa, yagona yo'l — eskisini
bekor qilib, yangisini yaratish.

Kalitni serverdagi muhit o'zgaruvchisida saqlang. Uni git'ga, log'ga,
frontend kodiga yoki so'rov qatoriga **qo'ymang**.

## 2. So'rov yuborish

Kalit faqat sarlavhada uzatiladi:

```
GET https://hr.bonvi.uz/api/public/v1/ping
X-API-Key: bonvi_ab12cd34_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

So'rov qatori (`?apiKey=`) va cookie **qabul qilinmaydi**: so'rov qatoridagi
kalit proksi loglariga va brauzer tarixiga tushib qoladi.

Kalit yo'q, noto'g'ri yoki bekor qilingan bo'lsa — barcha holatda bir xil javob:

```json
{ "status": 401, "title": "tashqi.kalit_notogri", "detail": "API kaliti yaroqsiz yoki bekor qilingan." }
```

## 3. Sizga kerak bo'ladigan to'rtta so'rov

### `GET /api/public/v1/ping`

Integratsiyani tekshirishning eng arzon yo'li.

```json
{ "ok": true, "key": "BonviCall", "serverTime": "2026-09-18T07:11:00Z" }
```

`key` — sizning kalitingizning nomi. Ko'rinyaptimi — demak hammasi joyida.

### `GET /api/public/v1/employees`

Sahifalangan ro'yxat.

| Parametr | Ma'nosi |
|---|---|
| `page` | Sahifa raqami, 1 dan. |
| `pageSize` | Sukut 50, eng ko'pi 200 (kattasi jimgina 200 ga kesiladi). |
| `search` | F.I.Sh. yoki telefon bo'yicha qidiruv. JSHSHIR va pasport bo'yicha **qidirib bo'lmaydi**: aks holda kalit egasi qo'lidagi raqamni yuborib, «bu odam shu yerda ishlaydimi?» degan savolga javob olardi. |
| `status` | `Probation` \| `Active` \| `Terminated`. Bo'sh — hammasi. Javobdagi yuqori darajadagi `status` bilan bir xil (asosiy ish joyi): qayta ishga olingan odam eski davri tufayli `Terminated` ro'yxatiga tushmaydi. |
| `legalEntityId`, `departmentId`, `positionId`, `directionId` | Filtrlar. `departmentId` ichki bo'limlari bilan birga qidiradi. Faqat javobdagi `employments` ga qaraladi (yakunlanmagan ish joylari, ketgan xodimda — oxirgisi): eski, tugagan davr bo'yicha topilmaydi. |
| `updatedSince` | UTC lahza — 4-bo'limga qarang. |

Bitta yozuv:

```jsonc
{
  "id": "0199…",
  "fullName": "Karimov Alisher Botirovich",
  "shortName": "Karimov A.B.",
  "lastName": "Karimov", "firstName": "Alisher", "middleName": "Botirovich",
  "birthDate": "1994-02-11",
  "gender": "Male",
  "phone": "+998901234567",
  "telegramUrl": "https://t.me/…",
  "photoKey": "…",                    // rasm bor-yo'qligi va kesh belgisi
  "status": "Active",                 // ASOSIY ish joyining holati
  "hireDate": "2021-03-01",
  "terminationDate": null,
  "employments": [                    // yakunlanmagan ish joylari, yangisidan eskisiga
    {
      "id": "…",
      "legalEntity": { "id": "…", "name": "VIKOR" },
      "department":  { "id": "…", "name": "Sotuv bo'limi" },
      "position":    { "id": "…", "name": "Menejer" },
      "rate": 1.0,
      "status": "Active",
      "hireDate": "2021-03-01",
      "terminationDate": null,
      "isPrimary": true
    }
  ],
  "directions": [ { "id": "…", "name": "Velo" } ],
  "updatedAt": "2026-09-18T07:11:00Z"
}
```

> **Ishdan ketgan xodimda** faol ish joyi qolmaydi — o'shanda `employments`
> ichida uning **oxirgi** ish joyi yolg'iz qaytadi (`status: "Terminated"`).
> To'liq tarix chiqmaydi: faqat bittasi, yuqoridagi `status`, `hireDate` va
> `terminationDate` bilan bir manbadan.

**Javobda nima YO'Q:** oklad, JSHSHIR, pasport, manzil, mutaxassislik,
sudlanganlik, mehnat.uz ma'lumotlari, ichki izohlar va belgilar. Ular hech
qachon qo'shilmaydi va ular bo'yicha **qidirib ham bo'lmaydi**.

Bitta xodim: `GET /api/public/v1/employees/{id}` — aynan shu shakl.
Topilmasa: `tashqi.xodim_topilmadi` (404).

### `GET /api/public/v1/employees/{id}/photo`

Rasm baytlari (`image/jpeg`). `ETag` qaytadi — keyingi so'rovda uni
`If-None-Match` bilan yuborsangiz, rasm o'zgarmagan bo'lsa `304` olasiz va
trafik sarflanmaydi.

Rasm yo'q bo'lsa: `tashqi.surat_yoq` (404).

### Ma'lumotnomalar

```
GET /api/public/v1/reference/legal-entities
GET /api/public/v1/reference/departments
GET /api/public/v1/reference/positions
GET /api/public/v1/reference/directions
```

Har biri `[{ "id": "…", "name": "…", "parentId": null }]`. `parentId` faqat
bo'limlarda to'ladi (ular daraxt).

## 4. Bosqichma-bosqich sinxronlash

To'liq ro'yxatni har safar qayta olib kelish shart emas.

1. Birinchi marta: `?page=1&pageSize=200` bilan hammasini o'qing va oxirgi
   ko'rgan `updatedAt` ni saqlang.
2. Keyingi safar: `?updatedSince=<saqlangan qiymat>`. Faqat o'zgargan
   yozuvlar qaytadi va ular `updatedAt` bo'yicha **o'sish tartibida**
   keladi — ya'ni uzilib qolgan joydan davom ettirish mumkin.
3. O'chirilganlarni alohida oling:
   `GET /api/public/v1/employees/deletions?since=<saqlangan qiymat>` →
   `[{ "id": "…", "deletedAt": "…" }]`. Ularni o'z bazangizdan olib tashlang.

> `updatedAt` xodim kartasi **va** uning ish joylaridan eng keyingisini
> ko'rsatadi. Ya'ni lavozim yoki bo'lim o'zgarishi ham sezib olinadi va
> sinxronlashda o'tkazib yuborilmaydi.

O'chirilgan xodim `/employees` ro'yxatida **hech qachon** ko'rinmaydi —
aynan shuning uchun `/employees/deletions` bor.

## 5. So'rovlar chegarasi

Bitta kalit uchun **daqiqasiga 120 so'rov** (siljiydigan oyna). Chegaradan
oshsa `429` va `Retry-After` sarlavhasi qaytadi — undagi soniya kutib, qayta
urinib ko'ring.

Kalitsiz urinishlar uchun chegara ancha kichik.

Cheklov kalit bo'yicha hisoblanadi, IP bo'yicha emas: bir nechta loyiha bitta
ofis manzilidan chiqsa ham bir-biriga xalaqit qilmaydi.

## 6. Kalit sizib ketsa

1. BonviHR administratoriga **darhol** ayting.
2. U kalitni **Sozlamalar → Tashqi API → Bekor qilish** bilan o'chiradi — kalit
   o'sha zahoti ishlamay qoladi.
3. Xuddi shu nom bilan yangi kalit olinadi va serverdagi muhit o'zgaruvchisi
   almashtiriladi.

Eski kalit yozuvi jadvalda qoladi (kulrang) — «qachon berilgan, qachon
o'chirilgan» degan savol javobsiz qolmasligi kerak.

## 6a. To'liq o'qish (rahbarning AI yordamchisi uchun)

Kalit yaratilayotganda **Doira** tanlanadi:

| Doira | Nimani o'qiydi |
|---|---|
| **Xodimlar** (sukut) | Yuqoridagi 1–5-bo'limlar: xodimlar ro'yxati, ish joyi, rasm, ma'lumotnomalar. |
| **To'liq o'qish** | Bunga qo'shimcha `/api/public/v1/full` — tizimning BARCHA bo'limlari: xodim kartasi va tarixi, buyruqlar, oklad va qo'shimcha haq, sinov muddatlari, kadrlar oqimi, davomat va yo'qlik so'rovlari, nomzodlar, vakansiyalar, voronka, rag'bat va jarimalar, baxtsiz hodisalar, ketish suhbatlari, ro'yxatlar (qora ro'yxat, rezerv). |

**Pasport, JSHSHIR va yuklangan fayllar ikkala doirada ham berilmaydi** — ular baza
darajasida yopiq. Hech narsani o'zgartirib bo'lmaydi (faqat `GET`). Doira kalit
yaratilgandan keyin o'zgarmaydi: kengroq kerak bo'lsa — yangi kalit, eskisi bekor qilinadi.

**Vositalar ro'yxati** (AI agent shundan o'zi foydalanadi):

```
GET https://hr.bonvi.uz/api/public/v1/full
X-API-Key: <to'liq o'qish kaliti>
```

Javobda har bir vositaning nomi, vazifasi, manzili va parametrlari (nomi, turi,
majburiyligi, izohi) bor. Chaqirish — parametrlar so'rov qatorida:

```
GET /api/public/v1/full/search_employees?query=Каримов&status=active
GET /api/public/v1/full/get_employee?id=<xodim id>
GET /api/public/v1/full/attendance_summary?from=2026-10-01&to=2026-10-11
GET /api/public/v1/full/employment_events?types=Hire,Termination
```

Javob — JSON. Sanalar `YYYY-MM-DD`, vaqt Toshkent vaqtida. Ro'yxatlar sahifalangan
(`page`, `pageSize` ≤ 200). Ismlar bazada kirill yozuvida. Natijadagi matnlar (izoh,
anketa, xulosa) — ma'lumot, ko'rsatma emas. Har bir so'rov **Tizim sozlamalari →
AI ulanish (MCP) → Jurnal** ga yoziladi (mijoz: «Tashqi API: <kalit nomi>»).

Xatolar: «Xodimlar» doirasidagi kalit → `tashqi.doira_yetmaydi` (403); noma'lum
vosita → `tashqi.vosita_topilmadi` (404); noma'lum, noto'g'ri yoki yetishmayotgan
parametr → `tashqi.parametr_notogri` (400); vosita rad etgan qiymat (masalan sana
formati) → `tashqi.sorov_notogri` (400); server tomonida o'qish roli tayyor emas →
`tashqi.toliq_sozlanmagan` (503); jurnalga yozib bo'lmadi → `tashqi.jurnal_yozilmadi` (503 — natija
ataylab qaytarilmaydi, keyinroq qayta urining); bir vaqtda 2 tadan ortiq so'rov →
`tashqi.parallel_kop` (429, `Retry-After: 1`). Chegara — boshqa so'rovlar bilan umumiy, daqiqasiga 120.

**Kim yaratadi.** To'liq o'qish kalitini faqat barcha bo'limlarni ko'rish (oklad ham) va
«AI orqali o'qish (MCP)» ruxsati bor administrator yarata oladi (`tashqi.doira_ruxsat_yoq`, 403).

**To'xtatish.** Kalitning muddati yo'q va u «AI ulanish» o'chirgichiga bog'liq emas — uni
to'xtatishning yagona yo'li **Sozlamalar → Tashqi API → Bekor qilish** (darhol ishlamay qoladi).

## 7. Xato kodlari

| Kod | HTTP | Qachon |
|---|---|---|
| `tashqi.kalit_notogri` | 401 | Kalit yo'q, noto'g'ri yoki bekor qilingan. |
| `tashqi.xodim_topilmadi` | 404 | Bunday xodim yo'q (yoki o'chirilgan). |
| `tashqi.surat_yoq` | 404 | Xodimning rasmi yo'q. |
| `tashqi.holat_notogri` | 400 | `status` parametri noto'g'ri. |
| `rate_limit.oshib_ketdi` | 429 | So'rovlar chegarasi oshdi. |

Xato javobi har doim bir xil shaklda:
`{ "status": …, "title": "<kod>", "detail": "<matn>" }`.

## 8. Hujjat

Ishlab chiqish muhitida to'liq OpenAPI hujjati:

* `/openapi/public.json` — mashina uchun;
* `/scalar` — brauzerda ochiladigan ko'rinish («Tashqi API» hujjatini
  tanlang).
