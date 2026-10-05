# Bot anketasi — savol matnlari (uchala tilda)

> **Manba:** @MBF_jbot. Savollarning MA'NOSI va TARTIBI o'sha, matni
> qayta yozilgan: eski botda imlo xatolari bor («талим даражангиз»,
> «Мутахассислигингиз ( мавжуд бўлса)») va ruscha yarim tarjima.
> Nomzod ko'radigan birinchi ekranda bunday xato ishonchni yo'qotadi.
>
> **Qoida:** har bir matn `BotTexts` da uchta tarmoq bilan yoziladi —
> `Cyrillic`, `Russian`, va `_ =>` (lotin). Ruscha tarmoq TUSHIB
> QOLMASIN: kompilyator buni ushlay olmaydi, nomzod esa lotincha matn
> ko'radi.
>
> Xabarlar `parse_mode=HTML` bilan ketadi — `<i>`, `<b>` ishlaydi.

---

## Umumiy tugmalar

| Kalit | Кирилл | Lotin | Русский |
|---|---|---|---|
| `SkipButton` | Ўтказиб юбориш | O‘tkazib yuborish | Пропустить |
| `YesButton` | Ҳа | Ha | Да |
| `NoButton` | Йўқ | Yo‘q | Нет |
| `SubmitButton` | 📨 Юбориш | 📨 Yuborish | 📨 Отправить |
| `AgreeButton` | Розиман | Roziman | Согласен |

---

## 4. Tug'ilgan kun

> **Кирилл:** 🗓 Туғилган кунингизни ёзинг (кк.оо.йййй).<br>Масалан: `10.10.1990`
> **Lotin:** 🗓 Tug‘ilgan kuningizni yozing (kk.oo.yyyy).<br>Masalan: `10.10.1990`
> **Русский:** 🗓 Укажите дату рождения (дд.мм.гггг).<br>Например: `10.10.1990`

**Tushunilmaganda:**
> Кирилл: Сана тушунарсиз. Илтимос, `кк.оо.йййй` кўринишида ёзинг — масалан `10.10.1990`.
> Lotin: Sana tushunarsiz. Iltimos, `kk.oo.yyyy` ko‘rinishida yozing — masalan `10.10.1990`.
> Русский: Не удалось разобрать дату. Напишите в виде `дд.мм.гггг` — например `10.10.1990`.

⚠️ Kelajakdagi sana va 14 yoshdan kichik / 80 yoshdan katta sana ham shu
xato bilan rad etiladi. Chegara BOTDA (`BirthDateParser.MinAge/MaxAge`),
serverniki esa kengroq (`Candidate.ValidateBirthDate` — 100 yil): bot
nomzodni ekran oldida to'xtatadi, server esa HR qo'lda kiritadigan
nostandart holatlarga joy qoldiradi. Farq ataylab va u
`BirthDateParser` da izohlangan.

---

## 7. Ma'lumot darajasi — tugmalar

> Кирилл: 🎓 Маълумот даражангиз қандай?
> Lotin: 🎓 Ma’lumot darajangiz qanday?
> Русский: 🎓 Какой у вас уровень образования?

| Enum | Кирилл | Lotin | Русский |
|---|---|---|---|
| `SecondarySchool` | Ўрта (мактаб) | O‘rta (maktab) | Среднее (школа) |
| `VocationalSchool` | Ўрта махсус (коллеж, лицей) | O‘rta maxsus (kollej, litsey) | Среднее специальное |
| `HigherComplete` | Олий (бакалавр) | Oliy (bakalavr) | Высшее (бакалавр) |
| `HigherIncomplete` | Олий (тугалланмаган) | Oliy (tugallanmagan) | Высшее (незаконченное) |

---

## 8. Mutaxassisligi *(o'tkazib yuborish mumkin)*

> Кирилл: 📌 Мутахассислигингиз нима? Бўлмаса, «Ўтказиб юбориш»ни босинг.
> Lotin: 📌 Mutaxassisligingiz nima? Bo‘lmasa, «O‘tkazib yuborish»ni bosing.
> Русский: 📌 Ваша специальность? Если нет — нажмите «Пропустить».

---

## 9. Ish tajribasi *(o'tkazib yuborish mumkin)*

> Кирилл: 💼 Иш тажрибангизни ёзинг: корхона, лавозим ва йиллар.<br>Масалан: <i>Dream Job МЧЖ, кассир, 2015–2018</i>
> Lotin: 💼 Ish tajribangizni yozing: korxona, lavozim va yillar.<br>Masalan: <i>Dream Job MChJ, kassir, 2015–2018</i>
> Русский: 💼 Опишите опыт работы: компания, должность и годы.<br>Например: <i>Dream Job MChJ, кассир, 2015–2018</i>

---

## 10. Oilaviy holat — tugmalar

> Кирилл: 👨‍👩‍👧 Оилавий ҳолатингиз?
> Lotin: 👨‍👩‍👧 Oilaviy holatingiz?
> Русский: 👨‍👩‍👧 Ваше семейное положение?

| Enum | Кирилл | Lotin | Русский |
|---|---|---|---|
| `Married` | Оилали | Oilali | Женат / замужем |
| `Unmarried` | Турмуш қурмаган | Turmush qurmagan | Холост / не замужем |
| `Divorced` | Ажрашган | Ajrashgan | В разводе |

---

## 11. Rus tili darajasi — tugmalar

> Кирилл: 🇷🇺 Рус тилини қай даражада биласиз?
> Lotin: 🇷🇺 Rus tilini qay darajada bilasiz?
> Русский: 🇷🇺 Насколько хорошо вы владеете русским языком?

| `LanguageLevel` | Кирилл | Lotin | Русский |
|---|---|---|---|
| `Elementary` | Бошланғич — тушунмайман | Boshlang‘ich — tushunmayman | Начальный — не понимаю |
| `Intermediate` | Ўрта — тушунаман, қийналиб гапираман | O‘rta — tushunaman, qiynalib gapiraman | Средний — понимаю, говорю с трудом |
| `Advanced` | Яхши — эркин гаплашаман | Yaxshi — erkin gaplashaman | Свободный — говорю свободно |

⚠️ Enumda beshta daraja bor (`UpperIntermediate`, `Native` ham), botda
esa uchtasi ko'rsatiladi — sabab `CandidateQuestionnaire.RussianLevel`
izohida. Qolgan ikkitasini HR keyin qo'lda aniqlashtiradi.

---

## 12. Boshqa tillar *(o'tkazib yuborish mumkin)*

> Кирилл: 🌐 Яна қайси тилларни биласиз ва қай даражада?<br>Масалан: <i>инглиз — ўрта, тожик — эркин</i>
> Lotin: 🌐 Yana qaysi tillarni bilasiz va qay darajada?<br>Masalan: <i>ingliz — o‘rta, tojik — erkin</i>
> Русский: 🌐 Какими ещё языками владеете и на каком уровне?<br>Например: <i>английский — средний, таджикский — свободно</i>

---

## 13. Office dasturlari *(o'tkazib yuborish mumkin)*

> Кирилл: 💻 Офис дастурларини қай даражада биласиз? Фоизда ёзинг.<br>Масалан: <i>Word 80%, Excel 60%, Photoshop 20%</i>
> Lotin: 💻 Ofis dasturlarini qay darajada bilasiz? Foizda yozing.<br>Masalan: <i>Word 80%, Excel 60%, Photoshop 20%</i>
> Русский: 💻 Насколько владеете офисными программами? Укажите в процентах.<br>Например: <i>Word 80%, Excel 60%, Photoshop 20%</i>

---

## 14. Oxirgi maosh *(o'tkazib yuborish mumkin)*

> Кирилл: 💵 Охирги иш жойингизда ойлик маошингиз қанча эди?
> Lotin: 💵 Oxirgi ish joyingizda oylik maoshingiz qancha edi?
> Русский: 💵 Какой была ваша зарплата на последнем месте работы?

---

## 15. Qo'shimcha ma'lumot *(o'tkazib yuborish mumkin)*

> Кирилл: ✍️ Ўзингиз ҳақингизда яна нима қўшимча қилмоқчисиз?<br>Масалан: <i>кучли томоним — тез ўрганаман; заиф томоним — шошқалоқлик</i>
> Lotin: ✍️ O‘zingiz haqingizda yana nima qo‘shimcha qilmoqchisiz?<br>Masalan: <i>kuchli tomonim — tez o‘rganaman; zaif tomonim — shoshqaloqlik</i>
> Русский: ✍️ Что ещё хотите рассказать о себе?<br>Например: <i>сильная сторона — быстро учусь; слабая — тороплюсь</i>

---

## 16. Haydovchilik guvohnomasi — tugmalar

> Кирилл: 🚗 Ҳайдовчилик гувоҳномангиз борми?
> Lotin: 🚗 Haydovchilik guvohnomangiz bormi?
> Русский: 🚗 У вас есть водительское удостоверение?

Tugmalar: `YesButton` / `NoButton`.

## 17. Guvohnoma toifasi — faqat «Ha» dan keyin

> Кирилл: 🚗 Гувоҳнома тоифаси қайси?
> Lotin: 🚗 Guvohnoma toifasi qaysi?
> Русский: 🚗 Какой категории удостоверение?

Tugmalar: `B` · `BC` · `BCE` · `BCED` — tarjima qilinmaydi.

---

## 18. Surat *(o'tkazib yuborish mumkin)*

> Кирилл: 🤳 Суратингизни юборинг. Телефон камерасидан олсангиз ҳам бўлади.
> Lotin: 🤳 Suratingizni yuboring. Telefon kamerasidan olsangiz ham bo‘ladi.
> Русский: 🤳 Отправьте свою фотографию. Можно сделать селфи на телефон.

**Rasm o'rniga matn kelsa:**
> Кирилл: Бу расм эмас. Расм юборинг ёки «Ўтказиб юбориш»ни босинг.
> Lotin: Bu rasm emas. Rasm yuboring yoki «O‘tkazib yuborish»ni bosing.
> Русский: Это не фотография. Отправьте фото или нажмите «Пропустить».

---

## 19. Kutilayotgan maosh — tugmalar

> Кирилл: 💰 Қанча ойлик маош кутяпсиз?
> Lotin: 💰 Qancha oylik maosh kutyapsiz?
> Русский: 💰 На какую зарплату вы рассчитываете?

| Enum | Matn (uchala tilda bir xil) |
|---|---|
| `From1To2Million` | 1–2 млн |
| `From2To3Million` | 2–3 млн |
| `From3To4Million` | 3–4 млн |
| `From5To7Million` | 5–7 млн |

Lotin tilida «mln», ruschada «млн».

---

## 20. Qayerdan bildingiz — tugmalar

> Кирилл: 📣 Иш ҳақида қаердан билдингиз?
> Lotin: 📣 Ish haqida qayerdan bildingiz?
> Русский: 📣 Откуда вы узнали о вакансии?

| Enum | Кирилл | Lotin | Русский |
|---|---|---|---|
| `Acquaintance` | Танишлар орқали | Tanishlar orqali | От знакомых |
| `SocialNetwork` | Instagram, Facebook | Instagram, Facebook | Instagram, Facebook |
| `Olx` | OLX.uz | OLX.uz | OLX.uz |
| `HeadHunter` | hh.uz | hh.uz | hh.uz |
| `Telegram` | Telegram | Telegram | Telegram |
| `Other` | Бошқа манба | Boshqa manba | Другой источник |

---

## 21. Ommaviy oferta

Sozlamada (`bot.offerUrl`) havola bo'lsa ko'rsatiladi; **bo'sh bo'lsa bu
qadam butunlay o'tkazib yuboriladi** va anketa 22-qadamga o'tadi.

> Кирилл: 📄 Юборишдан олдин оммавий оферта билан танишиб чиқинг:<br>{havola}<br><br>Давом этиш учун «Розиман»ни босинг.
> Lotin: 📄 Yuborishdan oldin ommaviy oferta bilan tanishib chiqing:<br>{havola}<br><br>Davom etish uchun «Roziman»ni bosing.
> Русский: 📄 Перед отправкой ознакомьтесь с публичной офертой:<br>{havola}<br><br>Нажмите «Согласен», чтобы продолжить.

---

## 22. Yakuniy tasdiq

To'ldirilgan anketa QAYTA KO'RSATILADI, keyin:

> Кирилл: Барча маълумот тўғрими?
> Lotin: Barcha ma’lumot to‘g‘rimi?
> Русский: Все данные указаны верно?

Tugmalar: `SubmitButton` va «Бошидан бошлаш» / «Boshidan boshlash» /
«Начать заново» (`/start` bilan bir xil ish).

**Xulosa qatorlari** — faqat TO'LDIRILGAN maydonlar chiziladi, bo'shlari
umuman ko'rinmaydi (eski botda «Ўтказиб юбориш» degan javob xulosaga
yozilib qolgan — bu xato, takrorlamang):

```
👤 Ф.И.Ш.: {ism}
🗓 Туғилган кун: {sana}
🏠 Манзил: {manzil}
📱 Телефон: {raqam}
💼 Вакансия: {vakansiya}
🎓 Маълумот: {daraja}
📌 Мутахассислик: {matn}
💼 Иш тажрибаси: {matn}
👨‍👩‍👧 Оилавий ҳолат: {javob}
🇷🇺 Рус тили: {daraja}
🌐 Бошқа тиллар: {matn}
💻 Офис дастурлари: {matn}
💵 Охирги маош: {matn}
✍️ Қўшимча: {matn}
🚗 Ҳайдовчилик гувоҳномаси: {javob}{, toifa}
🤳 Сурат: юборилди
💰 Кутилаётган маош: {oraliq}
📣 Қаердан билди: {javob}
📄 Оммавий оферта: розилик берилди
```

---

## Yakuniy xabar

Mavjud `Accepted` matni ishlatiladi. Farqi bitta: «Umumiy anketa»ga
tushgan nomzodga vakansiya nomi O'RNIGA boshqa gap yoziladi —
«ochiq vakansiya yo'q» degani nomzodga «arizangiz rad etildi» bo'lib
tuyulmasligi kerak:

> Кирилл: ✅ Анкетангиз қабул қилинди!<br><br>Ҳозирча очиқ иш ўрни йўқ, лекин анкетангиз базада сақланади. Мос вакансия чиққанда ХР бўлими сиз билан боғланади.
> Lotin: ✅ Anketangiz qabul qilindi!<br><br>Hozircha ochiq ish o‘rni yo‘q, lekin anketangiz bazada saqlanadi. Mos vakansiya chiqqanda HR bo‘limi siz bilan bog‘lanadi.
> Русский: ✅ Ваша анкета принята!<br><br>Сейчас открытых вакансий нет, но анкета сохранится в базе. Когда появится подходящая вакансия, отдел кадров свяжется с вами.

---

## «Umumiy anketa» yopiq bo'lsa (2026-10-05)

Egasining qarori: qaysi vakansiya yopiq bo'lsa, unga anketa topshirib
bo'lmaydi — «Umumiy anketa» ham. U HR ekranida boshqa vakansiyalar kabi
yopiladi va ochiladi; yopiq bo'lsa pastki menyuda **«📋 Anketa to'ldirish»
tugmasi umuman ko'rinmaydi** (30 soniyagacha kechikish bilan — eski menyu
nomzod ekranida qolgan bo'lsa, tugma bosilganda bot holatni aytib, menyuni
yangilaydi), va nomzodga bu anketa BOSHIDA aytiladi
(`BotTexts.QuestionnaireClosed`, `NoOpenVacanciesNoQuestionnaire`,
`ApplicationsClosed`).

**«📋 Anketa to'ldirish» bosildi, ochiq vakansiyalar bor** — matndan keyin ro'yxat chiqadi:

> Кирилл: 📋 Ҳозирча умумий анкета қабул қилинмаяпти.<br><br>Қуйидаги очиқ вакансиялардан бирини танлаб ариза топширишингиз мумкин.
> Lotin: 📋 Hozircha umumiy anketa qabul qilinmayapti.<br><br>Quyidagi ochiq vakansiyalardan birini tanlab ariza topshirishingiz mumkin.
> Русский: 📋 Сейчас общая анкета не принимается.<br><br>Вы можете выбрать одну из открытых вакансий ниже и подать заявку.

**Ochiq vakansiya ham yo'q** («Anketa to'ldirish» yoki «Vakansiyalar» bosilganda):

> Кирилл: Ҳозирча очиқ иш ўрни йўқ ва анкета қабул қилинмаяпти. 🙁<br><br>Очиқ вакансия чиққанда «💼 Вакансиялар» бўлимида кўринади.
> Lotin: Hozircha ochiq ish o‘rni yo‘q va anketa qabul qilinmayapti. 🙁<br><br>Ochiq vakansiya chiqqanda «💼 Vakansiyalar» bo‘limida ko‘rinadi.
> Русский: Сейчас открытых вакансий нет, и анкеты не принимаются. 🙁<br><br>Когда появится открытая вакансия, она будет в разделе «💼 Вакансии».

**To'ldirish paytida qabul yopilib qoldi** (tanlangan vakansiya ham, «Umumiy anketa» ham) — suhbat unutiladi:

> Кирилл: Кечирасиз, сиз анкета тўлдираётган пайтда ариза қабул қилиш ёпилди — аризангиз юборилмади. 😔<br><br>Очиқ вакансия чиққанда қайта топширишингиз мумкин.
> Lotin: Kechirasiz, siz anketa to‘ldirayotgan paytda ariza qabul qilish yopildi — arizangiz yuborilmadi. 😔<br><br>Ochiq vakansiya chiqqanda qayta topshirishingiz mumkin.
> Русский: Извините, пока вы заполняли анкету, приём заявок закрылся — заявка не отправлена. 😔<br><br>Когда появится открытая вакансия, вы сможете подать заявку снова.
