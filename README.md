# Bonvi HR

Bonvi (MBF Company, Qo'qon) uchun HR boshqaruv tizimi.
To'liq reja va bosqichlar: [PLAN.md](./PLAN.md)

---

## Boshlash

**Talab:** faqat **Docker**. Xost mashinaga .NET, Node yoki PostgreSQL o'rnatilmaydi.

```bash
cp .env.example .env      # parollarni o'zgartiring
./bonvi up
```

| Manzil | Nima |
|---|---|
| http://localhost:7173 | Veb interfeys |
| http://localhost:7080 | API |
| http://localhost:7080/scalar | API hujjati (interaktiv) |
| http://localhost:7080/health | Holat tekshiruvi |

> **Portlar 7xxx yo'lagida** — bu loyihaga ajratilgan. Standart portlar (5432, 6379,
> 5173, 8080) ataylab ishlatilmagan: ular boshqa loyihalar bilan to'qnashadi.
> Batafsil: `docker-compose.yml` sarlavhasidagi izoh.

---

## Buyruqlar

Hamma narsa `./bonvi` orqali — har bir buyruq konteyner ichida bajariladi.

```bash
./bonvi up                  # ishga tushirish
./bonvi down                # to'xtatish
./bonvi logs api            # loglar
./bonvi ps                  # holat
./bonvi reset               # ⚠️ hamma ma'lumotni o'chirish

./bonvi build               # backend build
./bonvi test                # barcha testlar
./bonvi migrate <Nom>       # yangi migratsiya
./bonvi db-update           # migratsiyani qo'llash
./bonvi psql                # PostgreSQL konsoli

./bonvi pnpm <args>         # frontend paket menejeri
./bonvi gen                 # ⭐ OpenAPI → TypeScript klient

./bonvi sh api|web|db       # konteyner ichiga kirish

./bonvi etl                 # ⭐ Excel → baza: quruq yurish (bazaga yozmaydi)
./bonvi etl --apply         # haqiqiy ko'chirish
```

---

## Tuzilma

```
BonviHR/
├─ bonvi                    # yagona buyruq nuqtasi (Docker o'ramchisi)
├─ docker-compose.yml       # development
├─ docker-compose.prod.yml  # production
│
├─ api/                     # Backend — C# / .NET 10  (mustaqil loyiha)
│  ├─ src/
│  │  ├─ Bonvi.Domain/          # biznes qoidalari — HECH KIMGA bog'liq emas
│  │  ├─ Bonvi.Application/     # use-case'lar — faqat Domain'ni biladi
│  │  ├─ Bonvi.Infrastructure/  # PostgreSQL, Redis, fayl, Hangfire
│  │  └─ Bonvi.Api/             # HTTP endpoint, auth, DI
│  ├─ tools/
│  │  └─ Bonvi.Etl/             # Excel → baza ko'chirish (bir martalik vosita)
│  └─ tests/
│     ├─ Bonvi.Domain.Tests/         # sof mantiq, bazasiz
│     ├─ Bonvi.Application.Tests/    # use-case, mock repozitoriy
│     ├─ Bonvi.Architecture.Tests/   # ⭐ qatlam qoidalari himoyasi
│     ├─ Bonvi.Etl.Tests/            # ko'chirish qoidalari, Excel faylsiz
│     └─ Bonvi.Api.IntegrationTests/ # haqiqiy PostgreSQL ustida
│
├─ data/                    # Excel manba va ko'chirish hisoboti (git'ga tushmaydi)
│
└─ web/                     # Frontend — Vite + React 19 + TS 7  (mustaqil loyiha)
   └─ src/
      ├─ app/               # marshrutlar, provayderlar
      ├─ features/          # modul bo'yicha: employees, departments, ...
      └─ shared/            # api klient, ui, lib
```

**Backend va frontend butunlay mustaqil:** alohida build, alohida konteyner, alohida deploy.
Ular faqat HTTP orqali gaplashadi. Har birini alohida git repoga ajratish uchun
hech narsa o'zgartirish shart emas.

---

## Ma'lumot ko'chirish (Excel → baza)

Bonvi'ning 10 yillik ma'lumoti `№91 бланк Ходимлар.xlsx` faylida — 20 varaq,
~11 000 yozuv. Uni ko'chirish uchun alohida vosita bor: `api/tools/Bonvi.Etl`.

```bash
# 1. Excel faylni data/ jildiga qo'ying
cp "~/Downloads/№91 бланк Ходимлар.xlsx" data/

# 2. Quruq yurish — bazaga hech narsa yozilmaydi
./bonvi etl

# 3. Hisobotni Excel'da oching va ko'rib chiqing
open data/etl-report.csv

# 4. Hammasi joyida bo'lsa — haqiqiy ko'chirish
./bonvi etl --apply
```

Uchta tamoyil bu vositani ishonchli qiladi:

1. **Hech qachon ma'lumot o'ylab topilmaydi.** Qiymat noaniq bo'lsa — maydon
   bo'sh qoladi va hisobotga tushadi. Taxminan to'ldirilgan ma'lumot bo'sh
   maydondan yomonroq: uni keyin hech kim tekshirmaydi.
2. **Standart rejim — quruq yurish.** Bazaga yozish uchun `--apply` ataylab
   talab qilinadi va u ulanish satrisiz umuman ishlamaydi.
3. **Idempotent.** Qayta ishga tushirilsa dublikat yaratmaydi — xodim JSHSHIR,
   pasport yoki ism+tug'ilgan sana bo'yicha topiladi.

**Hisobot — vositaning asosiy mahsuloti**, bazadagi yozuvlar emas. «540 xodim
ko'chirildi» degan xabar foydasiz; «12 ta JSHSHIR noto'g'ri, mana qaysi
qatorlarda» — foydali. CSV ataylab: HR uni Excel'da ochadi, filtrlaydi va
bevosita ish ro'yxati sifatida ishlatadi.

Qadamlarni alohida yurgizish mumkin:

```bash
./bonvi etl --steps employees,employments   # faqat xodim va mehnat munosabati
./bonvi etl --limit 50                      # har varaqdan 50 qator (tez sinov)
```

---

## Ikki til — bitta shartnoma

C# va TypeScript orasida tip ulashib bo'lmaydi. Buni kod generatsiyasi hal qiladi:

```
C# Controller  →  OpenAPI  →  Orval  →  TypeScript tip + React Query hook
```

```bash
# Backend'da DTO o'zgardi
./bonvi gen
# → web/src/shared/api/generated/ yangilandi
# → frontend'da mos kelmagan joy darhol qizaradi
```

Generatsiya qilingan kod qo'lda tahrirlanmaydi va git'ga tushmaydi —
u har doim backend'dan qayta hosil qilinadi.

---

## Arxitektura qoidalari

Bog'liqlik yo'nalishi: **`Api → Infrastructure → Application → Domain`**

| Qatlam | Nimani bilishi mumkin | Nimani bilmaydi |
|---|---|---|
| `Domain` | hech kimni | EF Core, ASP.NET, PostgreSQL — hech biri |
| `Application` | `Domain` | EF Core, HTTP |
| `Infrastructure` | `Application`, `Domain` | `Api` |
| `Api` | `Application`, `Infrastructure` | Npgsql'ga to'g'ridan-to'g'ri |

Bu qoidalar **hujjatda emas, testda** yozilgan:
`tests/Bonvi.Architecture.Tests` — qoida buzilsa build yiqiladi.

```bash
./bonvi test    # 465 test, ~5 soniya
```

---

## Texnologiyalar

**Backend:** .NET 10 LTS · EF Core 10 · PostgreSQL 18 · Redis 8 · Hangfire · FluentValidation · Mapperly · Serilog · xUnit v3
**Frontend:** Vite 8 · React 19 · TypeScript 7 · Tailwind 4 · TanStack Query/Router/Table · React Hook Form + Zod · Orval

Versiyalar `api/Directory.Packages.props` va `web/package.json` da markazlashgan.

---

## Muhim tafsilotlar

- **Portlar** 7xxx yo'lagida: `7080` api · `7173` web · `7432` postgres · `7379` redis.
  Yangi port kerak bo'lsa avval tekshiring: `lsof -nP -iTCP:<port> -sTCP:LISTEN`.
- **Konteyner nomlari** qisqa: `api`, `web`, `postgres`, `redis` (Docker Desktop'da
  ular `bonvi-hr` guruhi ostida ko'rinadi). Bu nomlar butun mashina bo'ylab yagona —
  boshqa loyiha ham shu nomni olsa, `container_name` ni o'zgartirish kerak bo'ladi.
- **PostgreSQL 18** volume yo'li: `/var/lib/postgresql` (17 va undan oldingilarda `/data` qo'shimchasi bor edi).
- **Testlar** compose'dagi PostgreSQL'da har safar yangi vaqtinchalik baza yaratadi — development bazasiga tegmaydi.
- **`artifacts/`** — barcha `bin/obj` shu yerda. Docker'da bu named volume, shu sababli bind-mount sekinligi yo'q.
- **Pasport raqamlari** kirill gomogliflaridan lotinga avtomatik normallashtiriladi (`АВ` → `AB`).
- **JSHSHIR** nazorat raqami tekshiriladi — algoritm 535 ta haqiqiy yozuvda sinalgan (98% moslik).
