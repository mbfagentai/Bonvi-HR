# SPEC — nomzodlar uchun qora ro'yxat

> Egasining qarori (2026-10-07): «qora ro'yxatga nomzodlar tabini qo'sh,
> xuddi rezerv bo'limi kabi». Sabab — Jobster'dan ko'chirilayotgan 121
> qora ro'yxatdagi odam (sudlangan, janjal bilan ketgan va h.k.): ular
> oddiy «rad etilgan» bo'lib qolsa, 6 oydan keyin botdan yana ariza
> tashlay oladi va HR ularni faqat eski kartani qidirsa ko'radi.
>
> Bu `docs/SPEC-qayta-ariza.md` 5-bo'limidagi oldingi qarorni («alohida
> qora ro'yxat bo'limi shart emas») BEKOR qiladi.

## 1. Yangi bo'lim YO'Q — mavjud menyu

«Qora ro'yxat» menyusi (`/qora-royxat`) «Rezerv» bilan bir xil bo'ladi:
ikkita tab — **Nomzodlar** | **Xodimlar**. Nomzodlar tabi faqat
`recruiting.view` ruxsati borlarga ko'rinadi; busiz sahifa avvalgidek
faqat xodimlarni ko'rsatadi.

## 2. Holat

`CandidateStatus.Blacklisted = 6` — sanoq OXIRIGA, ustun `int`, migratsiya
kerak emas (`Reserve = 5` bilan bir xil yo'l).

| Qoida | Nima uchun |
|---|---|
| Sabab **majburiy** | xodimlar qora ro'yxatidagi qoida bilan bir xil: jiddiy qaror sababsiz qo'yilmaydi |
| Jarayondagi, rad etilgan, voz kechgan va rezervdagi nomzodga qo'yiladi | odatda HR odamni rad etgandan KEYIN qora ro'yxatga oladi |
| Ishga olingan nomzodga qo'yilmaydi | u endi xodim — xodimlar qora ro'yxati ishlatiladi |
| Sabab lentaga ham tushadi (`CandidateNoteKind.Blacklisted`) | rad etish/rezerv bilan bir xil: «nega» degan savol tarixda qoladi |
| «Voronkaga qaytarish» ishlaydi, sabab majburiy | xato qo'yilgan bo'lsa chiqarib olish yo'li |
| Kundalik ro'yxatda va «Rad etilganlar» da KO'RINMAYDI | faqat «Qora ro'yxat → Nomzodlar» (`?status=Blacklisted`) |

## 3. Bot

- Qayta ariza tekshiruvi: odamning (telefon yoki Telegram hisobi bo'yicha)
  birorta arizasi qora ro'yxatda bo'lsa — anketa **muddatsiz** qabul
  qilinmaydi (`ApplicationBlockReason.Blacklisted`).
- Nomzodga sabab AYTILMAYDI: botdagi umumiy muloyim rad javobi
  (`BotTexts.ApplicationNotAccepted`) chiqadi.
- Telegram orqali kelgan JARAYONDAGI nomzod qora ro'yxatga olinsa, unga
  oddiy rad xabari bir marta ketadi (rad etilganlar bilan bir xil navbat) —
  javobsiz qolmaydi va qora ro'yxat oshkor bo'lmaydi. Rad etilgan nomzodda
  xabar belgisiga tegilmaydi (ketgan yoki baribir ketadi). O'zi VOZ KECHGAN
  yoki REZERVDAGI nomzodga xabar YUBORILMAYDI — oylar oldin ketgan odamga
  to'satdan rad xabari kelishi «ortimizdan qaror qilindi» degan ishora
  bo'lardi.

## 4. HR qo'lda kiritganda

To'siq yo'q (qayta ariza qoidasi bilan bir xil — HR odamni ko'rib turibdi),
lekin telefon bo'yicha topilgan mavjud karta «Qora ro'yxat» belgisi bilan
ko'rinadi.

## 5. Jobster importi

Qora ro'yxatdagi 121 kishi `Blacklisted` holatida kiradi, sababi
Jobster'dagi «Определение» dan. Import yozuvlarida Telegram suhbati YO'Q,
ya'ni ularga hech qanday xabar ketmaydi. Bazada allaqachon bor odamning
YOPIQ kartasi qora ro'yxatga o'tadi (voz kechgan/rezervdagida rad xabari
belgisi «yuborilgan» qo'yiladi — domendagi qoida bilan bir xil); FAOL
kartaga tegilmaydi, izohida ⛔ chiqadi va HR o'zi qaror qiladi.
