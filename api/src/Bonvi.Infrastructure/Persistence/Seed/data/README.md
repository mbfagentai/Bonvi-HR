# Bonvi HR — ma'lumotnoma (reference) ro'yxatlari

Bu papkadagi JSON fayllar HR'ning Excel bloknotidan (`№91 бланк Ходимлар (7).xlsx`)
avtomatik tarzda tozalab olingan. Butun tizim shu ro'yxatlarga tayanadi:
540 aktiv xodim, 1715 arxiv yozuvi va 1260 ishdan ketish yozuvi shular bo'yicha
ko'chiriladi.

**Fayllarni qo'lda tahrirlamang.** Qoidalar `tools/data/refs_spec.py` da yozilgan,
o'zgartirgandan keyin qayta yarating:

```bash
python3 tools/data/build_reference_data.py "~/Downloads/Telegram Desktop/№91 бланк Ходимлар  (7).xlsx"
python3 tools/data/build_reference_data.py --report    # to'liq tahlil hisoboti
```

Skript deterministik: bir xil kirish → bir xil chiqish. `openpyxl` kerak emas,
`.xlsx` zip sifatida ochilib XML to'g'ridan-to'g'ri o'qiladi (`tools/data/xlsx_lib.py`).

---

## 1. Manba

| Sheet | Nomi | Yozuv | Nima olindi |
|---|---|---|---|
| `sheet1.xml` | 91 Умумий ходимлар | **540** | 5=yo'nalish, 6=bo'lim, 7=lavozim, 13=yuridik shaxs + **49 ta guruh sarlavhasi** |
| `sheet10.xml` | 91.7 Архив | 1715 | 5=yo'nalish, 6=bo'lim, 7=lavozim |
| `sheet6.xml` | 91.6 Ишдан кетган ходимлар | 1585 (1260 sababli) | 4=bo'lim, 5=lavozim, **10=ishdan ketish sababi** |
| `sheet18.xml` | 91.14 Тақдирланган ходимлар | 34 | 4=bo'lim, 5=lavozim, 6=rag'bat turi |

**Hodisa varaqlari** (ETL ularni ham o'qiydi, shuning uchun ulardagi har bir
imlo `aliases.json` da kalit bo'lishi shart):

| Varaq | Birinchi qator | Nima olindi |
|---|---|---|
| 91.2 Янги ишга келганлар | 5 | 8=yo'nalish, 9=bo'lim, 10=lavozim |
| 91.3 Лавозими ўзгарган ходимлар | 6 | 5/6/7 = eski yo'nalish/bo'lim/lavozim, 10/11/12 = yangi |
| 91.4 Қушимча вазифалар берилган | 6 | 5=bo'lim, 6=lavozim, 9=qo'shimcha vazifa bo'limi |
| 91.5 Синов муддатидаги ходимлар | 3 | 4=bo'lim, 5=lavozim |
| 91.8 Стажировкадан кетганлар | 5 | 4=bo'lim, 5=lavozim |

> Erkin matnli ustunlar (91.3 «Изох», 91.4 «Қўшимча вазифаси», 91.5 «Изох»)
> **ataylab** manba emas: ular nasr, ma'lumotnoma qiymati emas.

> **Diqqat — arxivdagi ustun siljishi.** `91.7 Архив` sarlavhasida 13-ustun
> «Судимость», 14-ustun «Мехнат дафтарчаси» deb yozilgan, lekin haqiqiy
> ma'lumot mos kelmaydi: 13-ustunda telefon raqamlari, JSHSHIR, sanalar va
> «Текширилди» yotibdi (446 ta turli qiymat, 134 tasi «Текширилди»).
> Shuning uchun **yuridik shaxslar faqat asosiy varaqdan** olindi.

## 2. Natija

| Ma'lumotnoma | Xom qiymat | Kanonik | Fayl |
|---|---|---|---|
| Yuridik shaxs | 30 | **20** | `legal-entities.json` |
| Yo'nalish | 173 (asosiyda 26) | **8** | `directions.json` |
| Bo'lim | 631 + 88 blok sarlavhasi (49 ta detal + 39 ta jamlanma) | **96** (daraxt bilan) | `departments.json` |
| Lavozim | 1244 (asosiyda 228) | **481** | `positions.json` |
| Ishdan ketish sababi | 232 | **21** | `termination-reasons.json` |
| Rag'bat turi | 2 | **7** | `award-types.json` |
| Jazo turi | Excel'da yo'q | **7** | `penalty-types.json` |
| Hujjat turi | Excel'da yo'q | **15** | `document-types.json` |
| Yo'qlik turi | Excel'da yo'q — Verifix'dan, **qo'lda** yozilgan | **3** | `absence-types.json` |

### Qamrov (`aliases.json` bo'yicha to'ldirilgan katakchalar)

Ikki xil o'lchov bor va ularni farqlash muhim:

* **known** — qiymat `aliases.json` da bor (kod bilan yoki ataylab `null` bilan).
  ETL «ma'lumotnomada yo'q» satrini faqat **known bo'lmagan** katak uchun yozadi.
* **coded** — qiymat haqiqiy kanonik kodga tushdi.

| Varaq / ustun | known | coded |
|---|---|---|
| **asosiy — ЙЎНАЛИШИ / БЎЛИМИ / ЛАВОЗИМИ / Мех. дафтарчаси** | **100 %** | 99.6 / 91.8 / 100 / 100 % |
| 91.6 — Бўлим / Лавозими / Ишдан кетиш сабаби | **100 %** | 98.9 / 99.0 / 99.7 % |
| 91.7 — ЙЎНАЛИШИ / БЎЛИМИ / ЛАВОЗИМИ | **100 %** | 77.8 / 97.9 / 95.8 % |
| 91.14 — Бўлим / Лавозими / Рағбат | **100 %** | 94.4 / 100 / 100 % |
| 91.2 — yo'nalish / bo'lim / lavozim | **100 %** | 89.2 / 98.5 / 98.6 % |
| 91.3 — eski bo'lim / lavozim | **100 %** | 90.8 / 98.4 % |
| 91.3 — yangi bo'lim / lavozim | **100 %** | 90.6 / 95.4 % |
| 91.4 — bo'lim / lavozim / qo'shimcha vazifa bo'limi | **100 %** | 87.2 / 88.1 / 92.1 % |
| 91.5 — bo'lim / lavozim | **100 %** | 98.3 / 98.4 % |
| 91.8 — bo'lim / lavozim | **100 %** | 95.2 / 67.2 % |

**Barcha varaq va ustunlarda known = 100 %** — ETL hech qachon «bu qiymat
ma'lumotnomada yo'q» demaydi. `coded` past bo'lgan joylarda katak ataylab
bo'sh qoldiriladi: yo qiymat boshqa ustunga tegishli (91.8 «Лавозими» ustunida
`Бириктирилган ходим` ismlari — 75 katak), yo yo'nalish o'rniga bo'lim nomi
yozilgan (91.7 va 91.2/91.3), yo qaysi kanonik yozuv ekani noaniq (6.6-bo'lim).

---

## 3. `aliases.json` — eng muhim fayl

Excel'dagi **har bir xom qiymat** kanonik kodga bog'langan. Tuzilishi:

```jsonc
{
  "legalEntities":      { "VIKOR": "vikor", "Риспетто": "rispetto", … },
  "directions":         { "Вело.": "velo", "Вело, метан, шина": "velo|metan|shina", … },
  "departments":        { "Маркетинг булими": "marketing-bolimi", … },
  "departmentBlocks":   { "ОМБОР БЎЛИМИ ( 28 ходим)": "asosiy-ombor", … },  // qo'shimcha, pastda
  "positions":          { "Коровул": "qorovul", "Қоровул": "qorovul", … },
  "terminationReasons": { "Ўз ихтиёри билан": "oz-xohishi", … },
  "awardTypes":         { "Йил якунида ташаккурнома берилди": "tashakkurnoma", … },
  "unmapped":           { "directions": [...], "positions": [...], … }
}
```

Foydalanish qoidalari:

1. **Kalit — Excel'dagi xom qiymat, `.strip()` qilingan holda** (boshqa hech qanday
   o'zgartirishsiz). Katakni o'qidingiz → `strip()` → jadvaldan qidiring.
2. **Qiymat `null` bo'lishi mumkin** — bu qiymat ma'lumotnomaga tegishli emas degani
   (sana, telefon raqami, izoh, sarlavha qoldig'i, noto'g'ri ustunga yozilgan
   qiymat). Bunday katakni **bo'sh qoldiring**, taxmin qilmang va hisobotga
   yozmang. `unmapped` ro'yxatiga esa faqat **noaniq** qolgan qiymatlar tushadi:
   hal qilingan chiqindi (`POSITION_JUNK`, `DEPARTMENT_JUNK`, sarlavha/sana)
   u yerda takrorlanmaydi — ro'yxat HR uchun ish rejasi, chiqindi arxivi emas.
3. **`|` — kombinatsiya.** Bir katakda bir nechta kod bo'lishi mumkin:
   `"Вело, метан, шина, логистика"` → `"velo|metan|shina|logistika"`.
   Yo'nalishlarda bu odatiy hol (19 ta kombinatsiya), yuridik shaxslarda
   6 ta xodim bir vaqtda ikki-uch korxonada ro'yxatdan o'tgan
   (`"VIKOR+Rispetto"` → `"vikor|rispetto"`). `code.split("|")` qiling.

### ⭐ `departmentBlocks` — sxemadan tashqari qo'shimcha kalit

Bu spetsifikatsiyada yo'q edi, lekin **bo'limlar daraxtining asosiy dalili** shu.
Sabab: `91 Умумий ходимлар` varag'i jismonan 49 ta blokka bo'lingan — har bir
blokdan oldin sarlavha qatori turadi (masalan `"VICOR METAN" МЧЖ ҚК (204 ходим)`
yoki `СПИЦА ТОРТИШ БЎЛИМИ ( 5 нафар )`). 6-ustun («БЎЛИМИ») esa 80 ta Rispetto
xodimi uchun shunchaki `"Rispetto" МЧЖ` deb yozilgan — ya'ni **haqiqiy bo'lim
faqat blok sarlavhasida bor.**

Shuning uchun ko'chirishda:

```
xodimning bo'limi = aliases.departments[ 6-ustun ]
                    ?? aliases.departmentBlocks[ yuqoridagi blok sarlavhasi ]
```

Rispetto zavodi uchun esa **teskari tartib to'g'riroq** (blok aniqroq):
6-ustuni `"Rispetto" МЧЖ` bo'lganda blokni oling.

Blok sarlavhasini topish: 2-ustunda matn bor, lekin 7-ustun (lavozim) bo'sh
bo'lgan qator — bu sarlavha; keyingi sarlavhagacha bo'lgan hamma xodim shunga tegishli.
(`tools/data/build_reference_data.py` → `read_sources()` shuni qiladi.)

---

## 4. Tozalash qoidalari (`tools/data/normalize.py`)

Solishtirish kaliti (`norm()`) quyidagilarni bir xillashtiradi:

| Nima | Misol |
|---|---|
| Registr | `Маркетинг бўлими` = `маркетинг бўлими` |
| Ortiqcha bo'shliq va tinish belgilari | `Вело.` = `Вело`, `Вело уста:` = `Вело уста` |
| Kirill imlo variantlari `ў/у қ/к ғ/г ҳ/х ё/е щ/ш ы/и` | `Коровул` = `Қоровул`, `Ҳисобчи` = `Хисобчи`, `булим` = `бўлим` |
| Lotin/kirill homoglifi `a c e o p x y k m t b h` | `VIKOR` = `vikor`, `Viсor` (kirill `с` bilan) alohida yozilgan |
| Qisqartmalar | `б/н` = `билан`, `б/ми` = `бўлими`, `х/ми` = `ходими`, `шлиф/ка` = `шлифовка` |
| Sinonim so'z shakllari | `булим`/`булими`/`бўлим`, `ишчи`/`ходими`, `конвейер`/`конвеер`/`конвейр` |
| Bir so'zli imlo xatolari | `лабарант`→`лаборант`, `загатовка`→`заготовка`, `шлиповка`→`шлифовка`, `сваршиг`→`сваршик`, `букуш`→`букиш` |

**Kanonik nom kirillda qoladi** (HR shu yozuvda o'qiydi), `code` esa lotin slug.
Bir guruh ichidan nom tanlash: to'g'ri o'zbek-kirill harflari ko'p bo'lgani
(`Қоровул` > `Коровул`, `Дўкон` > `Дукон`), keyin eng ko'p uchragani.
Bir nechta imlo birlashganda nom qo'lda belgilangan
(`refs_spec.CANONICAL_POSITION_NAME`) — aks holda sarlavhaga viloyat nomi yoki
payvandlash posti tushib qolar edi.

**Kodlar barqaror.** Nom o'zgarsa ham kod o'zgarmaydi — kodlar `refs_spec.py` da
qo'lda yozilgan (lavozimlardan tashqari, ular nomdan slug qilinadi; lavozim
nomini o'zgartirsangiz eski kodni `CANONICAL_POSITION_NAME` orqali saqlang).

---

## 5. Qabul qilingan qarorlar

### 5.1 Bo'limlar daraxti — **2 pog'ona, faqat ishonchli joyda**

Daraxt faqat **uch mustaqil manba bir xil natija bergan joyda** qurildi:
blok sarlavhasi + 5-ustun (yo'nalish) + 13-ustun (yuridik shaxs).

**4 ta zavod ota-bo'lim bo'ldi:**

| Ota-bo'lim | Xodim | Bola | Dalil |
|---|---|---|---|
| `metan-zavod` «Метан завод» | 204 | 22 sex | blok `"VICOR METAN" МЧЖ ҚК` + `ЙЎНАЛИШИ = "Viсor metan" МЧЖ ҚК` |
| `velo-union-zavod` «Вело унион (Пропан) заводи» | 45 | 11 | blok `"VELO UNION" МЧЖ(ПРОПАН ЗАВОД)` + `ЙЎНАЛИШИ = "Velo union" МЧЖ` |
| `rispetto-zavod` «Риспетто велосипед заводи» | 80 | 12 | 12 ta alohida blok, 6-ustun hammasida `"Rispetto" МЧЖ` |
| Boshqa bo'linmalar | — | — | tekis (`parentCode = null`) |

Yana 4 ta kichik, lekin dalili aniq bog'lanish (blok a'zolarining 6-ustuni
otasini ko'rsatgani uchun): `Импорт бўлими` + `Сунъий интеллект` → `Ташқи таъминот бўлими`;
`Колл центр` → `Маркетинг бўлими`; `Тошкент офис` → `Телефон савдо`;
`Дўкон вело` → `Вело йўналиши`; `Ёйма вело` → `Вело запчасть`;
`Метан дўкон` → `Метан установка`; `Вело устахона` → `Асосий омбор`.

**Ataylab tekis qoldirilganlar** (dalil yetarli emas — noto'g'ri daraxtdan
tekis ro'yxat yaxshiroq):

* Ofis/boshqaruv bo'limlari (`1-Департамент`, `HR`, `Бухгалтерия`, `Ғазна`,
  `АХО`, `Назорат`, `Хавфсизлик` …) — varaqdagi tartib ierarxiyani isbotlamaydi.
* Savdo bo'linmalari (`Савдо бўлими`, `Вело йўналиши`, `Скутер йўналиши`,
  `Вилоят савдо`, `Қўқон бўлими`, `Онлайн савдо`, `Автошина`, `Телефон савдо`).
  Ular varaqda ketma-ket turadi, lekin `Савдони режалаштириш` blokidagi
  xodimning 6-ustuni `Ички бухгалтерия` — ya'ni ketma-ketlik ierarxiya emas.
* `Промзона`, `Промонтаж`, `Скутер заводи`, `Сара уруғ`, `ПТЭМ`, `Кидс дўкон`.

> **HR bilan aniqlash kerak:** yuqoridagi ofis va savdo bo'limlari qaysi
> departamentga bo'ysunadi? `1-Департамент` va `7-Департамент` bir-biriga
> nisbatan qanday joylashgan? `Промзона` va `Промонтаж` — bu alohida maydonlarmi
> yoki `Метан завод`ning bir qismimi?

### 5.2 Ikkita zavodda bir xil nomli sexlar — 17 xodim

`Краска бўлими`, `Механика бўлими`, `Хаво билан синаш бўлими`, `Лаборатория бўлими`,
`Энергетика бўлими`, `Омбор бўлими`, `Таъминот бўлими` — bu nomlar **ham Метан
заводда, ham Вело унион заводда** ishlatiladi. Alias jadvali `xom qiymat → bitta
kod` bo'lgani uchun ular bitta tugun qilindi va **ko'pchilik joylashgan
`metan-zavod` ga biriktirildi.**

Natijada 540 xodimdan **17 tasi** (Вело унион blokidagilar) daraxtda Метан завод
ostida ko'rinadi:

| Sex | Вело унион xodimi |
|---|---|
| `mexanika-bolimi` | 4 |
| `havo-bilan-sinash-bolimi` | 4 |
| `ombor-bolimi` | 3 |
| `kraska-bolimi` | 3 |
| `taminot-bolimi` | 1 |
| `laboratoriya-bolimi` | 1 |
| `energetika-bolimi` | 1 |

Bu xodimlarning haqiqiy zavodi **yo'qolmaydi** — `ЙЎНАЛИШИ` va yuridik shaxs
ustunlarida saqlanadi. Agar HR sexlarni ajratishni istasa, `refs_spec.py` da
`kraska-bolimi-vu` kabi alohida tugunlar qo'shish kerak, lekin unda 6-ustunni
blok bilan birga o'qish shart bo'ladi.

### 5.3 Yo'nalishlar — 8 ta, kombinatsiyalar `|` bilan

`velo`, `metan`, `shina`, `skuter`, `propan`, `logistika`, `telefon`, `engil-tip`.

* `"Viсor metan" МЧЖ ҚК` (346 yozuv) va `"Vikor metan" МЧЖ` (273) — bular
  yo'nalish ustuniga yozilgan **yuridik shaxs nomlari**, mazmunan `metan`.
* `"Velo union" МЧЖ`, `"Rispetto" МЧЖ`, `"Kokand Bicycles" МЧЖ` → `velo`.
* `покришка / покрышка / Покришка заводи` → `shina` (ikkalasi ham shina degani).
* `Вело.`, `Вело метан`, `Вело,метан, шина,логистика` va boshqa 19 ta
  kombinatsiya `|` bilan yozildi.

### 5.4 Lavozimlar — nima birlashtirildi

| Guruh | Nechta xom qiymat | Sabab |
|---|---|---|
| `viloyat-sotuvchisi` «Вилоят сотувчиси» | 21 | `Карши сотувчи`, `Бухоро сотувчи`, `Хоразм сотувчи` … — viloyat nomi lavozim emas |
| `xaydovchi` «Хайдовчи» | 14 | `Хайдовчи(ISUZU)`, `(Автобус)`, `(Камаз)`, `Шафер` — mashina turi lavozim emas |
| `viloyat-bolimi-omborchisi` | 12 | 10 ta viloyat omborchisi bitta lavozim |
| `svarshik` «Сварщик» | 12 | `Сваршик(Кольцевой)`, `(Перехватка)`, `(Горловина-донышко)` … — payvandlash **posti bo'lim**, lavozim bitta |
| `viloyat-ombor-xizmatchisi` | 11 | 9 ta viloyat ombor xizmatchisi |
| `hisobchi` / `yordamchi-hisobchi` | 3 + 3 | `Бухгалтер` = `Ҳисобчи` = `Хисобчи` |
| `qorovul` «Қоровул» | 2 | `Коровул` / `Қоровул` |

Uzun izohlar sarlavhadan olib tashlandi:
`Юк ташувчи(омборга келган юкларни тушуради ва юклайди)` → `Юк ташувчи`
(20 belgidan uzun qavs ichidagi matn kesiladi).

`grade` nomdan hisoblanadi: 1 = шогирд/ёрдамчи, 2 = ходим/ишчи, 3 = уста/оператор/
мутахассис, 4 = катта уста, 5 = бошлиқ/мудир/раҳбар/менежер, 6 = директор/бош ….

`departmentCode` faqat lavozim nomi o'z sexini aytib turgan joyda to'ldirildi
(`Плазмада кесиш` → `plazmada-kesish-bolimi`). Qolgan hamma joyda `null`:
lavozimlar korxona bo'ylab umumiy, xodimning bo'limi 6-ustundan olinadi.

**Ro'yxatga har bir haqiqiy lavozim kiradi.** Aktiv 540 xodimning lavozimlari
(228 xom → **178** kanonik) ustiga arxiv, ishdan ketganlar va hodisa
varaqlaridagi tarixiy lavozimlar qo'shiladi, jami **481**. Imlo variantlari
`POSITION_ALIAS` va `POSITION_REWRITE` orqali mavjud lavozimga tushadi
(`Каровул` → `Қоровул`, `Грузчик`/`Юкчи` → `Юк ташувчи`, `Загатовшик` →
`Заготовшик`, `Деклорант` → `Декларант`, `Печкачи` → `Печка ходими`,
`Резьба бўлими` → `Резьба очиш ходими` …). Lavozim bo'lmagan qiymatlar
(`POSITION_JUNK`) `unmapped` ga tushadi — 6.4-bo'lim.

`build_reference_data.ARCHIVE_MIN_FREQ = 1` — ya'ni bir marta uchragan lavozim
ham ro'yxatga kiradi. Agar HR faqat tez-tez uchraydiganlarini xohlasa,
bu sonni oshiring va qayta yarating.

### 5.5 Ishdan ketish sabablari — 232 → 21

Qoidalar tartibli, birinchi mos kelgani g'olib (`refs_spec.TERMINATION_RULES`).
Qavs ichidagi aniq sabab umumiy iboradan ustun turadi:
`Жавоби берилди(Сабабсиз ишга келмади)` → `intizom-buzilishi` (`javobi-berildi` emas).

| Kod | Yozuv | Xom variant | Nomi |
|---|---|---|---|
| `oz-xohishi` | 386 | 6 | Ўз хохиши билан |
| `javobi-berildi` | 198 | 15 | Жавоби берилди (раҳбарият қарори) |
| `qisqartirish` | 97 | 11 | Иш ўрни қисқартирилди (сокращение) |
| `ish-yoqmadi` | 94 | 12 | Иш ёқмади |
| `sogligi` | 94 | 19 | Соғлиғи кўтармади |
| `boshqa-ishga-otdi` | 70 | 28 | Бошқа ишга ўтиб кетди |
| `chetga-ketdi` | 68 | 12 | Четга ишлагани кетди |
| `intizom-buzilishi` | 67 | **39** | Меҳнат интизомини бузгани учун |
| `oylik-kamlik` | 45 | 13 | Ойлик камлик қилди |
| `oilaviy-sharoit` | 32 | 20 | Оилавий шароит сабабли |
| `oqishga-ketdi` | 27 | 10 | Ўқишга кетди |
| `layoqatsizlik` | 23 | 16 | Ишига лаёқатсизлиги учун |
| `bir-kun-ishlab-ketdi` | 17 | 7 | Бир-икки кун ишлаб кетиб қолди |
| `uyi-uzoq` | 12 | 4 | Уйи узоқлик қилди |
| `sinovdan-otmadi` | 6 | 3 | Синов муддатидан ўтмади |
| `boshqa` | 6 | 4 | Бошқа сабаб |
| `vafot-etdi` | 4 | 4 | Вафот этди |
| `armiyaga-ketdi` / `ichki-otkazish` / `shartnoma-tugadi` / `dekret` | 3/3/2/2 | 1/2/1/1 | — |

`isVoluntary` / `isRehireable`: `intizom-buzilishi`, `layoqatsizlik`,
`javobi-berildi`, `sinovdan-otmadi`, `vafot-etdi` — qayta ishga olinmaydi;
qolganlari olinadi. **Buni HR tasdiqlashi kerak** — «qora ro'yxat» mantig'i
`91.10 Чёрный список` varag'ida alohida yuritiladi.

### 5.6 Excel'da bo'lmagan ro'yxatlar

* **`penalty-types.json`** — Excel'da umuman yo'q. O'zbekiston Mehnat kodeksi
  181-moddasi (ҳайфсан, ишдан бўшатиш) va korxona amaliyoti asosida 7 ta tur
  tuzildi, `severity` 1…5. **HR tasdiqlashi kerak.**
* **`document-types.json`** — Excel ustunlaridan va suhbatdan yig'ildi:
  91-blanka, anketa, mehnat shartnomasi, buyruq, ariza, pasport nusxasi,
  mehnat daftarchasi, diplom, tibbiy ko'rik, sudlanganlik ma'lumotnomasi
  (18-ustun), harbiy hisob, lavozim yo'riqnomasi, tushuntirish xati,
  tashakkurnoma, obxodnoy varaqa (`91.6` 12-ustun).
* **`award-types.json`** — Excel'da atigi 2 ta qiymat bor edi
  (`Йил якунида ташаккурнома берилди` va `Ота-онасига ва узига Ташаккурнома
  берилди уйига олиб бориб`); ikkalasi ham kiritilgan, ustiga odatiy 5 ta tur
  qo'shildi (pul mukofoti, qimmatbaho sovg'a, faxriy yorliq, qo'shimcha ta'til,
  lavozimga ko'tarilish).

---

## 6. Tushunarsiz qolgan qiymatlar — **HR bilan aniqlanishi kerak**

Hammasi `aliases.json` → `unmapped` da ham bor. Aksariyati noto'g'ri ustunga
tushgan ma'lumot, ya'ni **ma'lumot yo'qolmaydi** — boshqa jadvaldan topiladi.

### 6.1 «ЙЎНАЛИШИ» ustunidagi yo'nalish bo'lmagan qiymatlar (376 arxiv yozuvi)

Bular **ataylab** yo'nalishga bog'lanmagan: ular shahar/viloyat nomi, bo'lim
nomi yoki lavozim — noto'g'ri ustunga yozilgan. **ETL ularni e'tiborsiz
qoldirsin** (`aliases.directions[...] === null` → yo'nalish qo'yilmaydi).
Ma'lumot yo'qolmaydi: deyarli hammasi `aliases.departments` yoki
`aliases.positions` da o'z kodiga ega.

| Qiymat | Nima bo'lishi mumkin | Qayerdan olinadi |
|---|---|---|
| `Промонтаж` (127), `Промзона` (89), `Промзона заводи`, `Промонтаж омбор`, `Промонтаж омбор янги` | maydon/zavod nomi | `aliases.departments` da bor |
| `Вилоят савдо` (40), `Тошкент`, `Бухоро`, `Хоразм`, `Самарканд`, `Наманган`, `Навоий`, `Нукус`, `Карши`, `Денов`, `Гулистон`, `Бешкек` | viloyat savdosi | `aliases.departments` → `viloyat-savdo` |
| `Сара уруғ` (20) va `Сара уруғ, Птэм`, `…, янги омбор` | alohida biznes | `aliases.departments` → `sara-urug` |
| `Кидс дўкон`, `Вокзал офис`, `Ташкент офис`, `Қўқон савдо` | do'kon/ofis | `aliases.departments` da bor |
| `ПТЭМ` (4) | **noma'lum qisqartma** | bo'lim sifatida `ptem` kodi berildi |
| `Оuter-source-using` (4) | **noma'lum** — autsorsing xodimimi? | bo'lim sifatida `outer-source-using` kodi berildi ❗ nomi HR bilan aniqlansin |
| `Юк ташувчи`, `Конвеер ходими`, `Компрессор ходими`, `Вилоят назоратчиси` | lavozim | `aliases.positions` da bor |
| 19 ta sana (`45250.0`, `46046.0` …) | xato kiritilgan | — |

### 6.2 «БЎЛИМИ» ustunidagi bo'lim bo'lmagan qiymatlar (31 xom qiymat)

`-`, `Булим`, `Умумий`, `йук`, `2 кун ишлаб келмади`, `кетди 26.09.2023`,
`Вело,метан,шина,логистика` (yo'nalish) + 24 ta sana/raqam.
Hammasi bo'sh qoldiriladi.

`Оuter-source-using` esa **bog'landi** — `outer-source-using` kodli bo'lim
yaratildi (nomi Excel'dagidek qoldirildi, HR to'g'ri nomini aytishi kerak).

### 6.3 «Ишдан кетиш сабаби» ustunida (4 yozuv)

`Exit Interview ўтказилган санаси` (sarlavha qatori),
`Қўқон ш, Бирлик 21`, `Ўзбекистон т. Ўқчи қишлоғи`, `Данғара тумани Туркман қ-ғи`
(manzillar — 9-ustundan siljib ketgan).

### 6.4 Lavozim ustunidagi 40 ta chiqindi qiymat

Lavozim ustunida bog'lanmagan qiymat **atigi 40 ta** qoldi va ularning
hech biri lavozim emas:

| Turi | Soni | Namuna |
|---|---|---|
| Excel serial raqami / sana | 22 | `45231.0`, `46046.0`, `44317.0` |
| Xodim ismi lavozim o'rniga | 11 | `Нуриддин`, `Мирзоакбар`, `Отабек`, `Никита`, `Ахад ака`, `Аюбхон`, `Маликхон`, `Абдурахим`, `Эргашали`, `А`, `Абдуллаев Мухсинжон Закирович` |
| Sana matni | 2 | `17.02.2025 ишдан кетган`, `17.02.2025дан ишдан кетган` |
| Bo'lim / yo'nalish nomi | 4 | `Вело`, `Вело, метан, шина`, `Дукон`, `Тошкент офис`, `Транспорт бўлими` |
| Izoh | 1 | `Умумий каралади` |

Hammasi `POSITION_JUNK` da ataylab belgilangan. ETL bunday katakni bo'sh
qoldirsin.

**Arxiv va ishdan ketganlar varaqlarida asosiy ro'yxatda yo'q 423 ta xom
qiymat bor. Ulardan 383 tasi bog'landi:** 168 tasi aktiv lavozimning imlo
varianti sifatida
(`Каровул`→`Қоровул`, `Форрош`→`Фаррош`, `Грузчик`/`Юкчи`/`Спиццани грузчики`→
`Юк ташувчи`, `Загатовшик`/`Закготовшик`→`Заготовшик`, `Деклорант`→`Декларант`,
`Печкачи`/`Печка (краска)`→`Печка ходими`, `Спицачи`/`Спица ходими`→
`Спица терувчи`, `Чархчи`→`Чархлаш ходими`, `Резьба`/`Резба`/`Резьба бўлими`→
`Резьба очиш ходими`, `Краска бўлими`/`Краска б/ми ходими`→`Краска ходими`,
`Лаборант ишчи`/`Лаборатори ходими`→`Лаборант`, `Тошкент хайдовчи`/
`Тошкент шафёр`→`Хайдовчи`, `Наманган бўлими омборчи`→`Вилоят бўлими омборчиси`,
`Менеджер`→`Менежер` …), qolgan 215 tasi esa **123 ta yangi lavozim** sifatida
`positions.json` ga qo'shildi:

`HR manager` (→`HR менежер`), `HR директор`, `HRBP`, `Рекрутер`,
`Директор`, `Директор ёрдамчиси`, `Финансовый директор`, `Финансовый аналитик`,
`Савдо аналитики`, `Аналитик`, `Прораб`, `Прораблар бошлиғи`, `Энергетик`,
`Қуёш панеллари энергетиги`, `Таржимон`, `Ўқитувчи`,
`Ўқитиш ва ривожлантириш менежери`, `Инженер конструктор` (→`Конструкторчи`),
`Эксковаторчи`, `Хужжат тўлдирувчи`, `Салатчи`, `Тергетолог`,
`Мобилограф ва дизайнер`, `Савдо агенти`, `Чакана сотувчи`, `Улгуржи сотувчи`,
`Линия раҳбари`, `Линия назоратчиси`, `Моторол устаси`, `Вилоят устаси`,
`Экспорт савдо ходими`, `Оператор` va boshqalar.

> **HR uchun savol:** bu 123 ta tarixiy lavozimning hammasi ham hozir kerakmi?
> Kerak bo'lmaganlarini o'chirmang — `IsActive = false` qiling (ma'lumotnoma
> yozuvi hech qachon o'chirilmaydi, chunki eski buyruqlarda unga havola bor).

### 6.5 Boshqa aniqlanishi kerak bo'lgan nuqtalar

1. `VIKOR` va `VIKOR сервиз`, shuningdek `Метан сервиз` — alohida yuridik
   shaxsmi yoki bir xilmi? Hozir alohida (har birida 1 xodim).
2. `Байскл` (`Байск`) faqat kombinatsiyada uchraydi
   (`Max inter+Байск+Риспетто+Вело унион`) — to'liq nomi nima?
3. `Ғазна бўлими` va `Касса бўлими` — jamlanma jadvalda ikkalasi ham 3 xodim
   ko'rsatadi, shuning uchun bitta bo'lim (`gazna-bolimi`) deb olindi. To'g'rimi?
4. `Скутер заводи` (3 xodim) va `Скутер ва моторол йиғув бўлими` (6 xodim) —
   biri ikkinchisining ichidami?
5. `7-Департамент` (1 xodim — корхона раҳбари) va `1-Департамент` (10 xodim)
   o'rtasidagi munosabat.
6. `Outer-source-using` bo'limining haqiqiy nomi (autsorsing xodimlarimi?).
7. `ПТЭМ` qisqartmasi nimani anglatadi?


### 6.6 Hodisa varaqlaridan qolgan noaniq qiymatlar

Hodisa varaqlari (91.2–91.5, 91.8) qo'shilgandan keyin `unmapped` ro'yxatida
**73 ta bo'lim** va **32 ta lavozim** qiymati qoldi. Ular `null` ga bog'langan
(katak bo'sh qoladi), lekin **chiqindi emas** — HR aniqlashi kerak.

Qabul qilingan qoida — **taxmin qilinmaydi**:

* qiymat **bo'lim/sex/maydon/hudud nomini** aytsa → tegishli bo'limga bog'lanadi
  (`Плазма`, `Учини букиш`, `Қўриқлаш бўлими`, `Самарқанд бўлими` …);
* qiymat **lavozim** bo'lsa → faqat `POSITION_DEPARTMENT_PREFIX` da o'sha ish
  uchun uy-bo'lim allaqachon e'lon qilingan bo'lsa bog'lanadi
  (`Лаборант` → `laboratoriya-bolimi`);
* qolgan hamma holatda **bog'lanmaydi**.

Shu sababli bog'lanmay qolgan eng katta guruhlar:

| Qiymat | Katak | Nega bog'lanmadi |
|---|---|---|
| `Кара хайдовчи` (+ `Кара хайдовчиси`, `Хайдовчи`) | ~59 | haydovchilar Транспорт бўлимига ham, ombor/zavodga ham tegishli bo'lishi mumkin |
| `Электрик` / `электрик` / `Электр` | ~38 | Энергетика бўлими faqat metan zavodida; boshqa maydonlarda ham elektriklar bor |
| `1 линия`, `3 линия`, `2/3 линия рахбари` | ~39 | «liniya» qaysi bo'limga tegishli ekani Excel'da yozilmagan |
| `Слесар` / `Слесарь` / `Токарь` | ~34 | Механика бўлимi taxmini — tasdiqlanmagan |
| `Смена рахбари` (+ ёрдамчиси) | ~30 | smena qaysi zavodники ekani noma'lum |
| `Сваршик` / `Сварщик` / `Сваршиг` | ~32 | «Вело унион» zavodida 6 ta payvandlash bo'limi bor |
| `Юк ташувчи`, `Грузчик`, `Юкчи`, `Омборга грузчик` | ~19 | maydon ko'rsatilmagan |
| `Ошпаз`, `Идиш ювувчи`, `Фаррош`, `Худуд фарроши` | ~25 | Ошхона metan zavodida, Риспеттода alohida |
| `Сокчи` | 12 | Хавфсизлик bo'limi yoki Риспетто qorovullari |

Lavozim tarafida qolgan 32 ta qiymat (`POSITION_UNCLEAR`):

* `Устини букиш` (+ станок устаси/шогирти) — zavodda `Учини букиш` va
  `Остини букиш` bor, qaysi biri ekani aniq emas (12 katak);
* bir katakda ikkita ish: `камера назоратчиси, табельчи`, `кассир ва табелчи`,
  `Сотувчи, хайдовчи`, `сотувчи ва ўқитувчи`, `Чархчи бўёқчи` …;
* darajasi yo'q yalang'och so'z: `Бошлиғи`, `Бошлик`, `Бўлим ходими`, `Ишчиси`;
* 91.4 dagi uzun vazifa tavsiflari (`Нарх назорати, усталар шартномаси, …`).
