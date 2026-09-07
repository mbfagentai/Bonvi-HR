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
- **Suhbat holati Redis'ga ko'chirilmadi** — xotirada qoladi. Sabab va
  narxi `docs/ASSUMPTIONS.md` da; qisqasi: bot deploy paytida qayta
  ishga tushsa, yarim to'ldirilgan anketa yo'qoladi.

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

⚠️ **Ish taklifi funksiyasiga TEGILMADI** — u SPEC 3-bo'limida alohida
bosqichda o'chiriladi. Hozir ikkala yo'l ham ishlaydi:
`POST /candidates/{id}/hire` (yangi, taklifsiz) va
`POST /offers/{id}/accept` (eski). Ikkalasi bir xil
`HireEmployeeCommand` oqimini chaqiradi va bir-birini takroriy qabuldan
to'sadi (`IEmploymentRepository.FindByCandidateAsync`).
