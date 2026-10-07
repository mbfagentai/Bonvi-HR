# SPEC — takroriy ariza to'sig'i

> Talab (2026-09-08, foydalanuvchi): «botdan bir marta to'liq ariza tashlagan
> odam 6 oygacha ariza tashlolmasligi kerak».
>
> Muammoning o'zi: bitta telefon raqami bilan ikki kun ichida ikkita anketa
> tushgan («Каримов азиз» va «Каримов Азиз Акмал ўғли», `+998999808700`,
> ikkalasi ham rad etilgan).

## 1. Nima noto'g'ri edi

Bot takroriy arizani ALLAQACHON tekshirardi — 6-qadamda, telefon raqami
kelgan zahoti (`BotWorker.HandlePhoneAsync`). Lekin so'rov
`status=InProgress` bilan ketardi: to'siq faqat HALI KO'RILAYOTGAN ariza
uchun ishlardi. Rad etilgan nomzod ertasiga qayta topshira olardi —
ekrandagi ikkita yozuv aynan shundan.

## 2. Qaror

Yopilgan arizadan keyin **N oy** o'tmaguncha bot yangi anketa qabul
qilmaydi. `N` — «Tizim sozlamalari» dagi `recruiting.reapplyMonths`,
sukut **6 oy**, chegara 1..120.

Foydalanuvchi mutlaq taqiqni ATAYLAB tanlamadi: odamning holati
o'zgaradi va bir yildan keyingi urinish spam emas. Mutlaq taqiq kerak
bo'lsa, qiymatni 120 oyga qo'yish yetarli — dasturchisiz.

### Qoidalar

- Muddat **`ClosedAt`** dan hisoblanadi, ariza topshirilgan sanadan
  emas: HR qarorni bir oy keyin yozishi mumkin va o'shanda hisob o'sha
  kundan boshlanishi kerak.
- **Jarayondagi** ariza avvalgidek to'sadi, muddatdan qat'i nazar.
- **Ishga olingan** nomzodga ham shu qoida: u endi xodim va yangi
  anketa yozuvi HR ish jadvalini ikkilantirardi.
- **Vakansiya bo'yicha AJRATILMAYDI**: to'siq odamga qo'yiladi, arizaga
  emas. Aks holda bir odam har kuni boshqa vakansiyaga anketa tashlab,
  o'sha muammoni qaytarardi. Yon ta'siri bor va u qabul qilingan: bitta
  vakansiyaga rad etilgan odam olti oy davomida boshqasiga ham
  topshira olmaydi.
- **Ikkita kalit: telefon VA Telegram hisobi.** Faqat telefon yetmaydi —
  bir odamda ikkita SIM bo'lishi mumkin va u o'sha hisobdan yangi raqam
  bilan qaytib kelardi.
- Qoida **botga** tegishli. HR qo'lda kiritganda to'siq YO'Q — u odamni
  ko'rib turibdi va qarorni o'zi qabul qiladi.

## 3. Qoida SERVERDA, botda emas

Bot faqat savol beradi:

```
GET /api/recruiting/candidates/application-check
    ?phone=…&telegramChatId=…
```

Javob:

```json
{ "allowed": false, "reason": "Cooldown",
  "appliedAt": "2026-09-07T…", "vacancyTitle": "Umumiy anketa",
  "stageName": "Anketa", "availableFrom": "2027-03-07" }
```

`reason`: `None` | `Active` | `Cooldown`.

**Nega alohida endpoint.** Ilgari bot qoidani O'ZI yozardi (qidiruv
so'roviga `status=InProgress` qo'shib). Muddat sozlamadan kelgach, bot
uni takrorlashi kerak bo'lardi — va HR ekranda muddatni o'zgartirganda
bot eski qoida bilan ishlab qolardi.

**Javob kelmasa — o'tkazamiz.** Server javob bermayotganida odamni «siz
allaqachon ariza topshirgansiz» degan yolg'on xabar bilan qaytarish eng
yomon natija bo'lardi.

## 4. Nomzod nima ko'radi

| Holat | Xabar |
|---|---|
| Jarayondagi ariza | qachon topshirgani, bosqichi, «ko'rib chiqilmoqda — javobni o'zimiz aytamiz» |
| Muddat ichida | qachon topshirgani va **qaysi sanadan keyin** qayta topshirishi mumkin |
| Tafsilot yetmasa | quruq, xushmuomala rad javobi — sana taxmin qilinmaydi |

Uchala matn ham uchala tilda (`BotTexts`).

## 5. Nima QILINMADI

**Nomzodlar uchun qora ro'yxat.** Avval qurilgan, keyin butunlay olib
tashlangan (foydalanuvchi qarori: «alohida qora ro'yxat degan bo'lim
qo'shish shart emas»).

> ⚠️ **2026-10-07 dan BEKOR:** egasi Jobster importidan keyin qora ro'yxatni
> qaytardi — alohida bo'lim emas, «Qora ro'yxat» menyusidagi «Nomzodlar» tabi
> sifatida. Qora ro'yxatdagi odam botdan MUDDATSIZ to'siladi
> (`docs/SPEC-nomzod-qora-royxat.md`). Quyidagi matn tarix uchun qoldirildi.
> Talab faqat takroriy arizani to'xtatish edi va
uni muddat qoidasining o'zi hal qiladi. Migratsiya commit qilinmagani
uchun bazada bo'sh ustun ham qolmadi.

⚠️ XODIMLAR qora ro'yxati (`EmployeeFlagType.Blacklist`, `/qora-royxat`)
BOSHQA narsa va unga tegilmagan: u ishdan ketayotgan xodimga qo'yiladi.

## 6. Sinovlar

- muddat ichida qayta ariza rad etiladi va SANA aytiladi
- muddat o'tgach ariza o'tadi
- jarayondagi ariza muddatdan qat'i nazar to'sadi
- muddat sozlamadan olinadi va chegaradan tashqariga chiqmaydi
- to'siq Telegram hisobi bo'yicha ham ishlaydi (yangi raqam, eski hisob)
- begona raqam o'tadi — to'siq odamga qo'yilgan, hammaga emas
- endpoint faqat bot hisobiga ochiq
- server sababni MATN bo'lib qaytaradi va bot uni o'qiy oladi
  (converter'siz to'siq jimgina ochilib ketardi)
