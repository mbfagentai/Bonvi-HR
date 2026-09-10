# Deploy ro'yxati — nomzod izohi, fayllar, «Rad etilganlar»

> Prod: `root@189.74.98.57`, `/opt/bonvi-hr`, HTTP (HTTPS hali yo'q).
> Server 2 GB RAM — shu sababli deploy image'larni KETMA-KET quradi.

## Oldindan

- [ ] Lokalda hammasi yashil: `./bonvi build` · `./bonvi test` ·
      `./bonvi check` · `./bonvi pnpm build`
- [ ] `git push` — `deploy.sh` `git pull --ff-only` qiladi, ya'ni
      o'zgarishlar origin'da bo'lishi SHART
- [ ] **Yangi zaxira**: prod'da `./bonvi backup`. Kunlik zaxira 22:00 UTC
      da olinadi, ya'ni ertalabki deploy oldida u eskirgan bo'ladi
- [ ] Ish vaqtidan tashqarida: bot qayta ishga tushganda YARIM
      TO'LDIRILGAN anketalar yo'qoladi (holat xotirada)

## Deploy

```
ssh root@189.74.98.57
cd /opt/bonvi-hr
./bonvi backup
./infra/deploy.sh
```

Skript 7 qadam: `git pull` → api build → bot build → **backend up
(postgres, redis, api, bot)** → OpenAPI klient → web build → up.

⚠️ Migratsiyalar API ko'tarilishida O'ZI qo'llanadi
(`Bootstrap__AutoMigrate`). Prod qaysi migratsiyada turganini deploy
oldidan tekshiring — bir nechtasi birdan qo'llanishi mumkin:

```sql
SELECT migration_id FROM "__EFMigrationsHistory" ORDER BY 1 DESC LIMIT 1;
```

Bu deploy'da eng muhimi — `20260910051756_NomzodIzohVaFayllar`. U
nomzodning rezyume MA'LUMOTINI `candidates` dagi to'rtta ustundan yangi
`candidate_files` jadvaliga KO'CHIRADI va keyin o'sha ustunlarni
tashlaydi. Butun migratsiya bitta tranzaksiyada: yo hammasi, yo hech
narsa. Nusxa DOIM `DROP` dan oldin ketadi (avtomatik yaratilgan
variantda teskari edi — qo'lda tuzatilgan), va buni uchta test qadab
qo'ygan (`CandidateFileMigrationTests`).

### ⚠️ Ko'chirishdan oldin — uchta SQL tekshiruvi

Uchalasi ham `0` qaytarishi SHART. Aks holda migratsiya to'xtaydi
(ma'lumot yo'qolmaydi — tranzaksiya orqaga qaytadi) yoki hech qayerga
ishora qilmaydigan «arvoh» fayl yozuvi paydo bo'ladi:

```sql
-- kalit yangi ustunga (varchar(500)) sig'maydi
SELECT count(*) FROM candidates WHERE length(resume_file_key) > 500;
-- bo'sh satrli kalit — «arvoh» fayl yozuvi yasaydi
SELECT count(*) FROM candidates WHERE btrim(resume_file_key) = '';
-- ko'chiriladigan yozuvlar soni: deploy'dan keyin candidate_files
-- dagi qatorlar soni SHU songa teng bo'lishi kerak
SELECT count(*) FROM candidates WHERE resume_file_key IS NOT NULL;
```

⚠️ **Ko'chirish prod hajmida hali sinalmagan.** `backups/` dagi
sakkizta zaxiraning birortasida ham rezyumesi bor nomzod yo'q, ya'ni
mahalliy sinov bu yo'lni qamrab olmadi. Deploy oldidan prod zaxirasini
mahalliy scratch bazaga tiklab, migratsiyani o'sha yerda bir marta
o'tkazing va nomzod kartasida eski rezyume ko'rinishini tekshiring.

⚠️ `ix_candidates_next_contact_date` `CONCURRENTLY` emas (tranzaksiya
ichida bo'lishi mumkin ham emas), ya'ni migratsiya davomida `candidates`
eksklyuziv qulflanadi. Bugungi hajmda bu soniyaning bo'lagi, lekin
`candidates` ustidagi uzoq ochiq tranzaksiya migratsiyani kutishga
majbur qiladi — HR ishlamayotgan paytda deploy qiling.

### ⛔ Hal qilinmagan: `job_offers` jadvali o'chadi

`20260907070127_TaklifOlibTashlandi` `job_offers` jadvalini tashlaydi.
Uning izohida «prod bazasida jadval BO'SH» deyilgan — bu NOTO'G'RI:
oxirgi uchta zaxirada bitta haqiqiy yozuv bor («Birinchi taklif»,
5 500 000 so'm, `sent_at 2026-08-28`). `Down` uni qaytarmaydi. Deploy
o'sha yozuvni butunlay yo'qotadi.

Deploy'dan OLDIN qaror qiling:

```sql
SELECT * FROM job_offers;
```

Test yozuvi bo'lsa — davom eting. Kerak bo'lsa — avval alohida
saqlang.

## Deploy'dan keyin

- [ ] `docker compose -f docker-compose.prod.yml ps` — hammasi `Up`
- [ ] Bot jurnalida `BotWorker[3203] Bot ishga tushdi: @...`
- [ ] **Tizim sozlamalari → «Ommaviy oferta havolasi»** ni to'ldiring.
      Bo'sh qolsa bot oferta qadamini chizmaydi (xato emas, lekin
      rozilik yozilmaydi)
- [ ] Botda `/start`: menyuda 💼 Vakansiyalar va 📋 Anketa to'ldirish
      ko'rinishi; vakansiya tanlangach «Ariza topshirish» chiqishi
- [ ] Nomzodlar → Voronka: kartani boshqa ustunga sudrash
- [ ] Nomzod kartasi: «Anketa» bo'limi va surat
- [ ] **Ko'chirish tekshiruvi**: yuqoridagi uchinchi SQL bergan son
      bilan `SELECT count(*) FROM candidate_files;` TENG bo'lishi kerak
- [ ] Eski rezyumesi bor nomzod kartasini oching — fayl ro'yxatda
      ko'rinishi va yuklab olinishi kerak
- [ ] Bosqich tugmasi (masalan «Гаплашилди») — forma ochilishi, izoh va
      keyingi aloqa sanasi saqlanishi
- [ ] Nomzodlar ro'yxati: «Keyingi aloqa» ustuni va «Bugun aloqa kerak»
      filtri
- [ ] «Rad etilganlar» bo'limi: o'zi voz kechganlar ham shu yerda,
      yorlig'i alohida
- [ ] Yangi rasm yuklang — kartada ko'rsatilgan hajm manbadan sezilarli
      kichik bo'lishi kerak (serverda kichraytiriladi)

## Kutilgan o'zgarishlar (xato emas)

- Bosh sahifadagi **«ochiq vakansiyalar» soni bittaga oshadi** —
  «Umumiy anketa» xizmat vakansiyasi ham `Open` holatda. HR ekranlarida
  u oddiy vakansiya, botda esa ro'yxatda KO'RINMAYDI.
- Nomzodlar ro'yxatida yangi «Umumiy anketa» ustuni/filtri paydo
  bo'lishi mumkin — botdan vakansiyasiz kelgan arizalar o'sha yerda.

## Orqaga qaytish

⚠️ **`Down` endi ma'lumot yo'qotadi** — ilgari bu ro'yxatda teskarisi
yozilgan edi va u `NomzodIzohVaFayllar` uchun to'g'ri emas. Qaytarilsa:

- nomzodning **birinchi** fayli to'rtta ustunga tiklanadi, qolgan
  fayllar yozuvlari YO'QOLADI (fayllarning o'zi diskda yetim qoladi),
- barcha amal izohlari (`candidate_notes`) yo'qoladi,
- barcha «keyingi aloqa» sanalari yo'qoladi.

Kod qaytarish kerak, lekin baza sxemasi kerak bo'lmasa — `Down` ni
umuman ishlatmang. Faqat kodni qaytaring:

```
git checkout <eski-commit>
docker compose -f docker-compose.prod.yml up -d --build
```

Baza sxemasini ham qaytarish kerak bo'lsa — `Down` emas, ZAXIRADAN
tiklash: `./bonvi restore backups/<fayl>.dump`. Yuqoridagi uch xil
yo'qotish shu sababli.
