# Deploy ro'yxati

> Prod: `root@189.74.98.57`, `/opt/bonvi-hr`, `https://hr.bonvi.uz`.
> Server 2 GB RAM — shu sababli deploy image'larni KETMA-KET quradi.

## ✅ Reliz `7a10e88` — CHIQARILDI 2026-09-27 19:31 (Toshkent)

Prod `9184b0b` → `7a10e88`. Rollback: image tegi `rollback-9184b0b`, zaxira
stamp `2026-09-27_142308`. Prod nusxasida oldindan sinaldi (faqat migratsiya
va yangi ustun; eski image yangi bazada ishlaydi; zaxiradan tiklash xatosiz,
sanoqlar aynan bir xil). Deploy'dan keyin: 500 yo'q, bot qayta yaratilmadi.
Qolgan qadam — egasi «Bosh rahbar» ga «Qo'shimcha haqni ko'rish» ni beradi.

| Commit | Nima |
|---|---|
| `6b17404` | Qo'shimcha vazifa haqi okladga QO'SHILADI (kartada «Jami»), vazifa tugash sanasi, yangi ruxsat `salary.extra.view` |
| `688a020` | Summa maydonlari uch xonadan ajratiladi (`MoneyInput`), kartadagi oklad bloki |
| `64e90ab` | Kiritilmagan oklad «kiritilmagan» deb ko'rsatiladi |
| `6d6c452` | Reliz oldi tekshiruv tuzatishlari: bo'shatishda vazifalar yopiladi, summa maskasi xavfsiz, «Jami» faqat oklad bo'lsa |

**Tayyorlik (2026-09-27):** mustaqil tekshiruvdan o'tgan (to'suvchi topilmadi,
topilganlar `6d6c452` da tuzatilgan); `./bonvi test` 1653/1653,
`./bonvi pnpm build`, `./bonvi check` yashil; migratsiya snapshot'i izchil.
Keyingi o'zgarishlar qo'shilsa — ular ham shu jadvalga yoziladi va
tekshiruv qaytadan o'tkaziladi.

**Migratsiya:** `20260927105152_QoshimchaVazifaTugashi` — faqat bitta bo'sh
ustun (`employment_events.duty_end_date`). Eski image uni bilmaydi va
tegmaydi — image rollback xavfsiz. `Down` ustunni o'chiradi (tugash sanalari
yo'qoladi) — kerak bo'lsa `Down` emas, image rollback.

**Deploy oldidan** (xavfsiz tartib — `bonvi-hr-xavfsiz-deploy`):
- [ ] Rollback teglari: `docker tag bonvi-hr-prod-{api,bot,web}:latest …:rollback-9184b0b`
- [ ] Yangi zaxira: `docker compose -f docker-compose.prod.yml exec backup /scripts/run-all.sh`
- [ ] Disk: `df -h /` — 5 GB dan kam bo'lsa, egasidan so'rab
      `docker builder prune -f --filter until=168h` (build keshi har deploy'da o'sadi)
- [ ] Prod nusxasida sinov — dump'ni lokal bazaga tiklash uchun **egasining
      aniq ruxsati** kerak (ruxsat tizimi buni to'xtatadi)
- [ ] `nohup ./infra/deploy.sh > /tmp/deploy-<commit>.log 2>&1 < /dev/null &`

**Deploy'dan keyin:**
- [ ] Egasi: «Rollar» → «Bosh rahbar» → **«Qo'shimcha haqni ko'rish»** ni
      belgilaydi (mavjud rolga yangi ruxsat jimgina berilmaydi)
- [ ] HR sahifani yangilaydi (F5)
- [ ] ⚠️ Kutilgan: HR menejerlar qo'shimcha vazifa SUMMASINI ko'rmay qoladi
      (egasining qarori) — xato emas
- [ ] Haqli vazifasi bor xodim kartasi: oklad, vazifa, «Jami» to'g'ri


## ⏳ Navbatdagi reliz — vakansiya nomi vakansiyaning o'zida (2026-09-29) — DEPLOY QILINMAGAN

Migratsiya `20260929061633_VakansiyaNomiVakansiyaningOzida`: «Vakansiya
nomlari» ma'lumotnomasi (`vacancy_titles`) va mas'ul xodim
(`vacancies.responsible_employee_id`) olib tashlanadi, nom
`vacancies.title` ga ko'chadi. Vakansiyasiz nomlar — YOPIQ vakansiya
bo'lib o'tadi (egasining talabi: ma'lumotnomadagi har bir nom vakansiyalar
bo'limida bo'lishi kerak).

**Deploy oldidan — prod zaxirasi nusxasida (o'qish uchun, sxema eski):**

```sql
-- A: nechta nom yopiq vakansiya bo'lib qo'shiladi (ma'lumot uchun)
select count(*) from vacancy_titles t
where t.deleted_at is null
  and not exists (select 1 from vacancies v join vacancy_titles t2 on t2.id = v.vacancy_title_id
                  where v.deleted_at is null and lower(t2.name) = lower(t.name));

-- B: ⛔ 0 bo'lishi SHART — aks holda migratsiya ataylab to'xtaydi (izoh
--    2000 belgiga sig'maydi, kesib yozilmaydi). Izohni qo'lda qisqartiring.
select count(*) from vacancy_titles t
left join vacancies v on v.vacancy_title_id = t.id
where btrim(coalesce(t.note, ''), E' \t\r\n') <> ''
  and (v.id is not null or t.deleted_at is null)
  and length(case
        when v.id is null or btrim(coalesce(v.note, ''), E' \t\r\n') = '' then t.note
        when v.note = t.note then v.note
        else v.note || E'\n\n' || t.note end) > 2000;

-- D: bir nomda bir nechta TIRIK vakansiya (holatidan qat'i nazar) —
--    ko'chirishdan keyin ro'yxatda bir xil nomli qatorlar bo'ladi
--    (tahrirlash ishlaydi; faqat NOMNI o'zgartirish band nomga o'tmaydi)
select lower(t.name), count(*) from vacancies v join vacancy_titles t on t.id = v.vacancy_title_id
where v.deleted_at is null group by 1 having count(*) > 1;

-- C: bir nomda bir nechta OCHIQ vakansiya (bot ikkita bir xil tugma
--    ko'rsatadi) — 0 bo'lmasa, deploy'dan keyin HR ortiqchasini yopadi
select lower(t.name), count(*) from vacancies v join vacancy_titles t on t.id = v.vacancy_title_id
where v.deleted_at is null and v.status = 1 group by 1 having count(*) > 1;
```

**⚠️ Image rollback bu relizdan keyin XAVFSIZ EMAS:** eski image
`vacancy_titles` va `vacancy_title_id` ni so'raydi. Qaytish — zaxiradan
tiklash (yoki `Down`: ma'lumotnomani nomlardan qayta yig'adi, mas'ul
xodimlar qaytmaydi).

**Deploy'dan keyin:**
- [ ] Ma'lumotnomalar menyusida «Vakansiya nomlari» yo'q
- [ ] Vakansiyalar ro'yxatida eski nomlar bor (vakansiyasizlari — «Yopiq»)
- [ ] Yopish / qayta ochish bir bosishda ishlaydi; bot ochiq vakansiyalarni ko'rsatadi

## Oldindan

- [ ] Lokalda hammasi yashil: `./bonvi build` · `./bonvi test` ·
      `./bonvi check` · `./bonvi pnpm build`
- [ ] `git push` — `deploy.sh` `git pull --ff-only` qiladi, ya'ni
      o'zgarishlar origin'da bo'lishi SHART
- [ ] **Yangi zaxira**: prod'da

      ```
      docker compose -f docker-compose.prod.yml exec backup /scripts/run-all.sh
      ```

      ⚠️ `./bonvi backup` prod'da ISHLAMAYDI: `bonvi` skripti dev
      compose faylini qidiradi va «postgres ishlamayapti» deb to'xtaydi.
      Kunlik avtomatik zaxira 22:00 UTC da olinadi, ya'ni ertalabki
      deploy oldida u eskirgan bo'ladi.

      ⚠️ `.env` dagi `BACKUP_CRON_SCHEDULE` qiymati QO'SHTIRNOQDA
      bo'lishi shart (`"0 22 * * *"`). Qo'shtirnoqsiz yozilsa `bonvi`
      skripti uni buyruq deb o'qiydi va `exit 127` bilan o'ladi —
      2026-09-10 da prod'da aynan shu holat topilgan va tuzatilgan.
- [ ] Ish vaqtidan tashqarida: bot qayta ishga tushganda YARIM
      TO'LDIRILGAN anketalar yo'qoladi (holat xotirada)

## Deploy

```
ssh root@189.74.98.57
cd /opt/bonvi-hr
docker compose -f docker-compose.prod.yml exec backup /scripts/run-all.sh
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

### ⚠️ `ab2c3ba` (rollar, 2026-09-26) dan OLDINGI versiyaga qaytish

Kod qaytarilsa, **rollar yo'qotishli** bo'ladi — sxema o'zgarmagan, lekin
ruxsat kodlari o'zgargan:

- eski seeder `hr_manager` va `main_boss` ni har ishga tushishda eski
  standartga qaytaradi — egasi ekrandan qilgan barcha tahrirlar yo'qoladi;
- egasi yaratgan rollarda faqat yangi 8 ta amal kodi bor
  (`employment.hire` … `employment.correct`), eski kod ularni TANIMAYDI —
  bu rol egalari mehnat amallarining hammasidan mahrum bo'ladi.

Eski image'ni ishga tushirishdan OLDIN rollarga eski kodni qaytaring:

```sql
UPDATE roles
   SET permissions = array_append(permissions, 'employment.manage')
 WHERE permissions && ARRAY['employment.hire','employment.transfer','employment.duty',
                            'employment.salary','employment.terminate',
                            'employment.probation.extend','employment.probation.complete',
                            'employment.correct']
   AND NOT 'employment.manage' = ANY(permissions);
```

Yangi versiyaga qaytganda seeder `employment.manage` ni yana 8 ta kodga
bo'ladi — qo'shimcha qadam kerak emas. Egasiga ogohlantiring: rol
tahrirlari eski versiyada saqlanmaydi.

## 2026-09-27 tuzatishidan keyin — oklad tekshiruvi

2026-09-16 … 2026-09-27 oralig'ida mehnat yo'li yozuvini tuzatish
(buyruq raqami, sana, izoh) ba'zi holatda joriy okladni almashtirib
yuborardi — qo'shimcha vazifa yozuvida okladga vazifa HAQI tushardi.
Deploy'dan keyin prod'da bir marta tekshiring (faqat o'qiydi):

```
docker compose -f docker-compose.prod.yml exec -T postgres \
  sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' \
  < infra/sql/oklad-tuzatish-tekshiruvi.sql
```

Bo'sh natija — xato hech kimga tushmagan. Qator chiqsa, okladni HR
ekrandan «Oklad» amali bilan to'g'rilaydi (tarixda iz qoladi); bazani
qo'lda tahrirlamang.

## Tekshiruv materiallari (2026-09-27) — migratsiya `TekshiruvMateriallari`

- Yangi jadval `employee_check_files`; har bir mavjud mehnat.uz skrinshoti
  unga birinchi material bo'lib **nusxalanadi** (fayl diskda o'z joyida,
  faqat kalit ko'chadi). Deploy'dan keyin tekshirish — faqat KO'CHIRILGAN
  qatorlar sanaladi (HR yangi fayl yuklasa ham to'g'ri qoladi):

  ```sql
  SELECT (SELECT count(*) FROM employees WHERE mehnat_uz_screenshot_key IS NOT NULL) AS eski,
         (SELECT count(*) FROM employee_check_files
           WHERE size_bytes IS NULL AND file_name LIKE 'mehnat-uz-skrinshot%') AS kochgan;
  ```

  Ikkala son teng bo'lishi kerak.
- **HR sahifani yangilasin** (F5 / Cmd+R). Ochiq qolgan eski sahifa
  skrinshotni «yuklanmagan» deb ko'rsatadi va eski yuklash/o'chirish
  so'rovlari 404 oladi — ma'lumot yozilmaydi, lekin chalkashtiradi.
- `employees.mehnat_uz_screenshot_*` ustunlari **ataylab o'chirilmagan** —
  eski image'ga qaytilsa, u shu ustunlarni o'qiydi va ishlayveradi. Ular
  keyingi relizlardan birida, qo'lda yozilgan migratsiya bilan olib tashlanadi.
- `Down` faqat yangi jadvalni o'chiradi: deploy'dan KEYIN yuklangan
  materiallarning yozuvlari yo'qoladi (fayllar diskda qoladi). Ularni saqlash
  kerak bo'lsa — `Down` emas, zaxiradan tiklash.
- ⚠️ **Deploy oldi zaxiradan tiklash** (`restore.sh`, `pg_restore --clean`)
  yangi `employee_check_files` jadvalini O'CHIRMAYDI — u zaxirada yo'q, va
  uning `employees` ga tashqi kaliti `pk_employees` ni qayta yaratishga
  xalaqit beradi (5 ta xato, skript `set -e` bilan to'xtaydi). Keyingi
  deploy'da migratsiya esa jadvalni qayta yaratolmay yiqilardi. Tiklashdan
  OLDIN:

  ```sql
  DROP TABLE IF EXISTS employee_check_files CASCADE;
  ```

  Prod nusxasida sinalgan (2026-09-27): shundan keyin tiklash xatosiz va
  barcha jadval sanoqlari deploy oldidagi bilan AYNAN bir xil.

### Eski image'ga qaytib, keyin YANA shu versiyaga chiqilsa

Migratsiya bu holatda qayta ishlamaydi (u allaqachon qo'llangan), eski kod
esa shu vaqt ichida skrinshotlarni almashtirishi yoki o'chirishi mumkin
(eski kod eski faylni diskdan ham o'chiradi). Ikkita holat:

- rollback paytida yuklangan skrinshot yangi ro'yxatda yo'q;
- rollback paytida almashtirilgan yoki o'chirilgan skrinshotning ko'chgan
  qatori endi yo'q faylga ishora qiladi («Faylni ochib bo'lmadi»).

Shu sababli qayta chiqqandan keyin bir marta (ikkalasi ham idempotent):

```sql
BEGIN;

-- 1. Rollback paytida yuklangan skrinshotlar ro'yxatga qo'shiladi
INSERT INTO employee_check_files
    (id, employee_id, file_key, file_name, content_type, size_bytes, created_at, created_by)
SELECT gen_random_uuid(), e.id, e.mehnat_uz_screenshot_key,
       'mehnat-uz-skrinshot' || CASE e.mehnat_uz_screenshot_content_type
           WHEN 'application/pdf' THEN '.pdf' WHEN 'image/png' THEN '.png'
           WHEN 'image/webp' THEN '.webp' WHEN 'image/jpeg' THEN '.jpg' ELSE '' END,
       coalesce(e.mehnat_uz_screenshot_content_type, 'application/octet-stream'),
       NULL, coalesce(e.updated_at, e.created_at), NULL
  FROM employees AS e
 WHERE e.mehnat_uz_screenshot_key IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM employee_check_files f
                    WHERE f.employee_id = e.id AND f.file_key = e.mehnat_uz_screenshot_key);

-- 2. Eski kod almashtirgan yoki o'chirgan skrinshotning ko'chgan qatori
--    yumshoq o'chiriladi (fayli diskda yo'q)
UPDATE employee_check_files AS f
   SET deleted_at = now()
  FROM employees AS e
 WHERE f.employee_id = e.id
   AND f.deleted_at IS NULL
   AND f.size_bytes IS NULL
   AND f.file_name LIKE 'mehnat-uz-skrinshot%'
   AND f.file_key IS DISTINCT FROM e.mehnat_uz_screenshot_key;

COMMIT;
```

⚠️ Rollback paytida eski interfeys yangi versiyada yuklangan materiallarni
KO'RMAYDI (u faqat bitta skrinshotni biladi) — ular yo'qolmagan, qayta
chiqqach yana ko'rinadi.

## Qo'shimcha haq alohida ruxsat bilan (2026-09-27) — migratsiya `QoshimchaVazifaTugashi`

- Migratsiya faqat bitta bo'sh ustun qo'shadi (`employment_events.duty_end_date`).
  Eski image bu ustunni bilmaydi va unga tegmaydi — image rollback xavfsiz.
- ⚠️ **XATTI-HARAKAT O'ZGARADI:** qo'shimcha vazifa HAQI endi alohida
  `salary.extra.view` ruxsati bilan ko'rinadi. HR menejerlarida u YO'Q —
  deploy'dan keyin ular vazifa summalarini ko'rmay qoladi (egasining qarori).
- **Deploy'dan keyin egasi:** «Rollar» → «Bosh rahbar» → «Qo'shimcha haqni
  ko'rish» ni belgilaydi. Super administratorga u avtomatik tushadi (seeder
  uni har ishga tushishda `Permissions.All` ga tenglaydi).

