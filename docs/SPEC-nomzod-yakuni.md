# SPEC — nomzodning uchta yakuni

> Foydalanuvchi qarori (2026-09-07): «kelgan nomzodlarni biz yoki xodim
> sifatida olamiz, yoki rezervga o'tkazishimiz mumkin, yoki butunlay
> qabul qilinmadi deb o'tkazish mumkin».
> Ikkinchi qaror: **ish taklifi funksiyasi butunlay o'chiriladi.**

## 1. Yakunlar

| Yakun | Ma'nosi | Holat |
|---|---|---|
| **Xodim sifatida olindi** | nomzod xodim kartasiga aylanadi | `Hired` (bor) |
| **Rezerv** | hozir olinmadi, lekin kerak bo'lsa chaqiriladi | `Reserve` — **YANGI** |
| **Qabul qilinmadi** | korxona rad etdi | `Rejected` (bor) |

`Withdrawn` (nomzodning O'ZI voz kechgan) SAQLANADI — u korxonaning
qarori emas va hisobotda boshqa ma'no beradi.

⚠️ `CandidateStatus` bazada `int` bo'lib yotadi, ya'ni yangi qiymat
qo'shish uchun **migratsiya kerak emas**. Qiymat OXIRIGA qo'shiladi
(`Reserve = 5`) — o'rtaga qo'yilsa bazadagi eski qiymatlar siljib
ketardi.

**Rezerv nomi tasodifiy emas:** xodimlarda ham aynan shu tushuncha bor
(`EmployeeFlagType.Reserve`, Excel «91.9 Резерв») va HR uni shu nom
bilan biladi.

## 2. To'g'ridan-to'g'ri ishga olish

Yangi use-case: nomzod → xodim, **taklifsiz**.

⭐ U o'zi xodim YARATMAYDI — 1-bosqichning mavjud `HireEmployeeCommand`
oqimini chaqiradi (hozirgi `AcceptJobOfferCommand` bilan aynan bir xil
sabab): o'sha oqim ichida JSHSHIR bo'yicha dublikat qidiruvi, qora
ro'yxat blokirovkasi, stavka chegarasi va **majburiy sinov muddati**
bor. Ikkinchi «qabul» yo'li yozilsa, yangi xodim `/sinov` ro'yxatiga
umuman tushmasdi.

Majburiy maydon **ikkita**: yuridik shaxs va lavozim. Qolganining
zaxirasi bor (sinov muddati — lavozimdagi qiymat yoki 90 kun; sana —
bugun). Ekranda ular «Qo'shimcha» ostida yig'iladi, JSHSHIR esa OCHIQ
qoladi — dublikat qidiruvi aynan unga tayanadi.

## 3. Ish taklifi — o'chiriladi

Prod'da `job_offers` jadvalida **0 qator** va ishga olingan nomzod ham
yo'q, ya'ni funksiya hech qachon ishlatilmagan — ma'lumot yo'qolmaydi.

Tegiladigan joylar (~60 fayl): domen, EF konfiguratsiyasi va
migratsiya (`DROP TABLE job_offers`), repozitoriy, use-case'lar,
endpoint'lar, **bildirishnomalar** (`NotificationType`,
`NotificationKey`, `NotificationCollector`, `NotificationReadModel` —
«taklif muddati tugayapti» ogohlantirishi), frontend tab va dialoglari,
testlar, 4 tildagi tarjimalar.

⚠️ **TARTIB QAT'IY:** avval 2-bo'lim (to'g'ridan-to'g'ri ishga olish)
ishlab, testdan o'tsin; taklif faqat SHUNDAN KEYIN o'chirilsin. Hozir
ishga olishning yagona yo'li taklif orqali o'tadi — uni oldin o'chirish
tizimni ishga olish imkoniyatisiz qoldirardi.

## 4. Ekranda

Nomzod kartasidagi amallar qatori:

| Tugma | Natija |
|---|---|
| **Ishga olish** | dialog → xodim yaratiladi, nomzod `Hired` |
| **Rezervga** | qisqa tasdiq → `Reserve` |
| **Qabul qilinmadi** | mavjud rad etish dialogi → `Rejected` |
| Boshqa vakansiyaga o'tkazish | o'zgarmaydi |

«Takliflar» tabi yo'qoladi.

Rezervdagi nomzod ro'yxatda filtr bilan topilishi kerak — u aynan shu
uchun saqlanadi.
