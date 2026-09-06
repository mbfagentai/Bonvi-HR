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

