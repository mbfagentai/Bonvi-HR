# Deploy ro'yxati — anketa + voronka drag & drop

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
(`Bootstrap__AutoMigrate`). Ikkitasi qo'llanadi:
`20260906150159_NomzodAnketasi` (yangi jadval + nullable ustun) va
`20260906163052_OfertaRoziligiIxtiyoriy` (`DROP NOT NULL`). Ikkalasi ham
faqat qo'shimcha — mavjud ma'lumotga tegmaydi.

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

## Kutilgan o'zgarishlar (xato emas)

- Bosh sahifadagi **«ochiq vakansiyalar» soni bittaga oshadi** —
  «Umumiy anketa» xizmat vakansiyasi ham `Open` holatda. HR ekranlarida
  u oddiy vakansiya, botda esa ro'yxatda KO'RINMAYDI.
- Nomzodlar ro'yxatida yangi «Umumiy anketa» ustuni/filtri paydo
  bo'lishi mumkin — botdan vakansiyasiz kelgan arizalar o'sha yerda.

## Orqaga qaytish

Migratsiyalarning `Down` i ishlaydi va ma'lumot yo'qotmaydi
(ikkinchisi `SET NOT NULL` dan oldin bo'sh qiymatlarni to'ldiradi).
Qaytish kerak bo'lsa:

```
git checkout <eski-commit>
docker compose -f docker-compose.prod.yml up -d --build
```

Baza sxemasini ham qaytarish kerak bo'lsa — zaxiradan tiklash eng
ishonchli yo'l: `./bonvi restore backups/<fayl>.dump`.
