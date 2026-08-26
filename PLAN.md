# BONVI HR — Tizim Rejasi

> **Maqsad:** Bugun 3 xil joyda (Excel + Jobster + Verifix) sochilib yotgan HR jarayonini bitta o'z platformamizga yig'ish.
> **Ijrochi:** 1 dasturchi + Claude MAX. **Joylashuv:** Qo'qon, Bonvi (MBF Company guruhi).
> **Hujjat maqomi:** Bu fayl loyihaning yagona navigatsiya nuqtasi. Har bir qadamdan keyin shu yerdagi checkbox belgilanadi.

---

## 0. Uch bosqich — umumiy rasm

| Bosqich | Nima quriladi | Nimani almashtiradi | Holat |
|---|---|---|---|
| **1** | Xodimlar boshqaruvi (core HR) | Excel "№91 бланк" (20 ta sheet) | 🔨 **Hozir shu** |
| **2** | Rekruting: vakansiya → anketa → suhbat → job offer | Jobster | ⏸ Keyin |
| **3** | Davomat: Face ID, keldi-ketdi, hisob-kitob | Verifix | ⏸ Keyin |

**Asosiy tamoyil:** 1-bosqich shunday quriladiki, 2 va 3-bosqich unga *modul sifatida ulanadi*, qayta yozish talab qilinmaydi. Buning uchun 1-bosqichda `Employee` (shaxs) va `Employment` (mehnat munosabati) modeli to'g'ri qo'yiladi — 2-bosqich `Candidate → Employee` konversiyasini, 3-bosqich `Employment → Attendance` bog'lanishini shundan oladi.

---

## 1. Hozirgi holat: Excel tahlili (faktlar)

`№91 бланк Ходимлар.xlsx` — 20 ta sheet, ~11 000 yozuv. Tahlil natijasi:

### 1.1 Sheet'lar va ularning haqiqiy roli

| Sheet | Yozuv | Aslida bu nima |
|---|---|---|
| 91 Умумий ходимлар | **540** | Joriy aktiv xodimlar — *hosila ko'rinish (view)* |
| Лист70 / Лист70 (копия) | 1175 | Yuqoridagining nusxasi + oklad qoralamasi → **tashlanadi** |
| 91.7 Архив | 1715 | Barcha vaqtlar tarixi — *hosila ko'rinish* |
| 91.2 Янги ишга келганлар | 1960 | **Hodisa:** ishga qabul (buyruq bilan) |
| 91.6 Ишдан кетган ходимлар | 1260 | **Hodisa:** ishdan bo'shatish |
| 91.3 Лавозими ўзгарган | 1975 | **Hodisa:** lavozim/bo'lim/oklad o'zgarishi |
| 91.4 Қушимча вазифалар | 1464 | **Hodisa:** qo'shimcha vazifa berish |
| 91.5 Синов муддатидаги | 1779 | **Holat:** sinov muddati + xulosa |
| 91.8 Стажировкадан кетганлар | 231 | **Hodisa:** sinov ichida ketish |
| 91.1 Кенгаш хулосаси | 3186 | **2-bosqich** (rekruting): kengash ovoz berishi |
| 91.9 Резерв | 65 | **Teg:** rezerv nomzodlar |
| 91.10 Чёрный список | 61 | **Teg:** qora ro'yxat |
| 91.12 Вазифа берса бўладиган | ~100 | **Teg:** ko'proq yuklama ko'taradi |
| Назоратга олинган ходимлар | ~10 | **Teg:** nazoratdagi xodim |
| 91.15 Қобилятлар жадвали | 554 | **Profil:** til, kompyuter, ta'lim, qiziqish |
| 91.14 Тақдирланган ходимлар | 34 | **Hodisa:** rag'bat |
| 91.16 Қайта алоқа | 192 | **Anketa:** exit interview (Google Form) |
| Бахтсиз ходисалар | 4 | **Jurnal:** ish paytidagi hodisalar |
| 91.11 Керакли Кадрлар | *hidden* | Ishlatilmayapti → tashlanadi |

> **Eng muhim xulosa:** "91 Умумий" va "91.7 Архив" — bu jadval emas, **hisoblanadigan natija**. Haqiqiy manba — hodisalar oqimi (qabul → o'zgarish → bo'shatish). Tizim shu tamoyilda quriladi: *bitta jadvalni qo'lda 8 joyga ko'chirish yo'qoladi.*

### 1.2 Ma'lumot sifati — nega spravochnik shart

| Maydon | Excelda unique | Bo'lishi kerak | Muammo |
|---|---|---|---|
| Лавозим | **228** | ~150 (tozalangan) | `Коровул` / `Қоровул`, `Ҳисобчи` / `Хисобчи` |
| Бўлим | **73** | ~70 | `Маркетинг бўлими` / `Маркетинг булими` |
| Йўналиш | **26** | **8** | Qiymat kombinatsiya: `Вело, метан, шина, логистика` |
| Ишдан кетиш сабаби | **232** | ~15 | `Ўз хохиши билан` / `Ўз ихтиёри билан` / `Иш ёқмади` / `иш ёқмади` |
| Мехнат дафтарчаси | **30** | **~10** | `VIKOR` / `Вело унион` / `Риспетто` — bu **yuridik shaxs** |

Ikkita **jimgina** xato — Excel'da ko'rinmaydi, lekin qidiruvni buzadi:

- **Pasportlar kirill harflarida.** `АВ5725936` dagi «А» — U+0410 (kirill), lotin «A» (U+0041) emas.
  Ko'zga bir xil, kompyuterga boshqa belgi. Excel'da 607 kirill va 495 lotin harf aralashgan.
  Lotin «AB» bo'yicha qidirilsa — yozuv ko'z oldida turgan bo'lsa ham topilmaydi.
- **JSHSHIR nazorat raqami.** Algoritm 535 ta haqiqiy yozuvda tekshirildi — **98% mos keldi**.
  Qolgan 2% (9 ta xodim) — haqiqiy kiritish xatolari. Masalan `21707764200043`
  «1876-yilda tug'ilgan» deb chiqadi. Yana 32 ta yozuvda JSHSHIR ichidagi sana
  Excel'dagi tug'ilgan sana bilan mos kelmaydi.

Uchta strukturaviy topilma:

1. **"Мехнат дафтарчаси" = yuridik shaxs.** VIKOR, Rispetto, Velo union, Vicor metan MChJ, Kokand Bicycles, BONVI plus, MBF, Next phone, Pattern... Kompaniya — bu holding.
2. **Xodim bir vaqtda 2 firmada ishlashi mumkin:** `VIKOR + Вело унион 0,5 ставка ўриндош`. Demak `Employee ≠ Employment` — bitta shaxsda bir nechta mehnat munosabati bo'ladi.
3. **Yo'nalish — ko'p qiymatli.** Bitta xodim `Вело, метан, шина, логистика` ga tegishli → many-to-many.

---

## 2. Texnologiya tanlovi

### 2.1 Yakuniy stack

Backend va frontend — **ikki mustaqil loyiha**, alohida repo, alohida deploy. Ular faqat HTTP/OpenAPI orqali gaplashadi.

#### Backend — C# / .NET 10 LTS

| Qatlam | Tanlov | Nega |
|---|---|---|
| **Platforma** | **.NET 10.0.400 (LTS)** + ASP.NET Core Web API | LTS — 2028-yilgacha qo'llab-quvvatlanadi. Ishlab chiqarish tizimi uchun majburiy shart. |
| **Til** | **C# 14** | Kompilyatsiya paytida tip tekshiruvi + `nullable reference types` + `TreatWarningsAsErrors` → xatolar kodda qoladi, ishlab turgan tizimda emas. |
| **ORM** | **EF Core 10.0.11** + Npgsql 10.0.3 | Migratsiya tarixi, LINQ tip xavfsizligi, tranzaksiya boshqaruvi. |
| **Arxitektura** | Clean Architecture + **vertical slice** handler'lar | Mediator kutubxonasisiz: har bir amal = bitta `Command/Query` + `Handler` klass. Ortiqcha qatlam yo'q. |
| **Validatsiya** | **FluentValidation 12** | Biznes qoidalari deklarativ, testlanadigan. |
| **Auth** | ASP.NET Core Identity + **JWT** (access+refresh) | Rollar: `admin`, `hr_manager`, `hr_operator`, `dept_head`, `viewer`. |
| **Fon vazifalar** | **Hangfire 1.8** (PostgreSQL storage) | Sinov muddati eslatmasi, tug'ilgan kun, hisobot, Excel import. **Dashboard'i bor** — vazifa bajarildimi, ko'rinib turadi. |
| **Mapping** | **Mapperly 4.3** | Source-generator: kompilyatsiya paytida kod yoziladi, runtime'da refleksiya yo'q. Bepul. |
| **Log** | **Serilog 10** (structured) | HR tizimida "kim nima qildi" savoli doim bo'ladi. Yuqori unumdorlik uchun `[LoggerMessage]` manba-generatori. |
| **API hujjati** | `Microsoft.AspNetCore.OpenApi` + **Scalar 2.17** | .NET 10 tug'ma OpenAPI. Frontend kod generatsiyasining manbasi. |
| **Excel I/O** | **ClosedXML 0.105** (MIT) | Import (migratsiya) va eksport. *EPPlus tijoriy — ishlatilmaydi.* |
| **Test** | **xUnit v3** + Shouldly + NSubstitute | .NET 10 ning yangi `Microsoft.Testing.Platform` ustida. Integratsiya testlari **haqiqiy PostgreSQL** da (mock emas). |
| **Fayl** | **Lokal disk** (`IFileStorage` porti ortida) | Passport skani, anketa, buyruq, shartnoma, xodim rasmi. Ortiqcha konteyner yo'q; S3/MinIO adapteri keyin bitta sinf bilan qo'shiladi. |

#### Frontend — TypeScript SPA

| Qatlam | Tanlov | Nega |
|---|---|---|
| **Build** | **Vite 8.2** | Framework emas — bundler/dev-server. To'liq build ~0.5 soniya. |
| **Framework** | **React 19.2** | HR = og'ir jadval + murakkab forma. Bu vazifada TanStack ekotizimi eng kuchli. |
| **Til** | **TypeScript 7.0** (strict) | ✅ tanlangan. TS 7 — Go'da yozilgan yangi kompilyator, eskisidan sezilarli tez. |
| **Marshrut** | **TanStack Router 1.170** | Tip-xavfsiz marshrut: URL parametri ham tekshiriladi. Fayl asosida. |
| **Server holati** | **TanStack Query 5.101** | Kesh, invalidatsiya, optimistik yangilanish. |
| **Jadval** | **TanStack Table 9.1** | 540 qator, 19 ustun, server-side filtr/sort/guruh. |
| **UI** | **shadcn/ui + Tailwind CSS 4.3** | Kod bizniki — cheklovsiz o'zgartiriladi. Tailwind 4 da mavzu CSS ichida (config fayl yo'q). |
| **Forma** | **React Hook Form 7.85 + Zod 4** | |
| **API klient** | **Orval 8.24** (OpenAPI → TS) | ⭐ Quyida — 2.3-bo'lim. |
| **Ko'p tillilik** | **i18next 26 + react-i18next 17** | 4 til: `uz-Latn`, `uz-Cyrl`, `ru`, `en`. Quyida — 2.5-bo'lim. |
| **Ochiluvchi menyu** | **Radix UI** | Klaviatura, fokus tuzog'i, ekran o'quvchisi — tayyor va to'g'ri. |
| **Grafik** | **Recharts 3.10** | Dashboard diagrammalari. |

#### Umumiy infratuzilma

| Komponent | Tanlov |
|---|---|
| **DB** | **PostgreSQL 18.6** ✅ |
| **Kesh** | **Redis 8.8** ✅ — output cache, sessiya, rate limit |
| **Konteyner** | **Docker Compose** ✅ |
| **Reverse proxy** | **Caddy 2.11** — avtomatik HTTPS, konfigi 5 qator |
| **Xato kuzatuvi** | Sentry (api + web) — S11 da |

> **Ish tamoyili: xost mashinaga HECH NARSA o'rnatilmaydi.**
> `.NET`, `Node`, `pnpm`, `psql` — hammasi konteyner ichida ishlaydi.
> Yagona kirish nuqtasi — `./bonvi` skripti (`./bonvi build`, `./bonvi test`, `./bonvi gen`, ...).
> Talab: faqat Docker.

### 2.2 Nega Python emas

Sizning mezoningiz — tezlik, sifat, ishonchlilik, mustahkamlik, kengayish. Solishtirish:

| Mezon | C# / .NET | Python (FastAPI/Django) |
|---|---|---|
| Tip xavfsizligi | Kompilyatorda **majburiy** | `type hints` — ixtiyoriy, runtime'da tekshirilmaydi |
| Refaktoring | Maydon o'zgarsa — kompilyator barcha joyni ko'rsatadi | Tizim jimgina buziladi, xato foydalanuvchida chiqadi |
| Pul / oklad | Native `decimal` | `Decimal` bor, ORM mapping'da xato oson |
| Parallellik | Haqiqiy async, GIL yo'q | GIL — faqat process bilan kengayadi (xotira narxi) |
| Fon vazifalar | Platformaga tug'ma (`BackgroundService`, Hangfire) | Celery — alohida infratuzilma va broker |
| Uzoq muddatli qo'llab-quvvatlash | LTS, 3 yil kafolat | Framework versiyalari tezroq buziladi |

> **Halol baho:** 540 xodim va ~20 parallel foydalanuvchida *xom tezlik* hal qiluvchi emas — Python ham eplaydi. Farq **kod hajmi 50 000 qatordan oshganda** ko'rinadi: 3-bosqichda davomat, oklad va jarima hisobi qo'shilganda, kompilyator tekshiruvi bor kod bilan yo'q kod orasidagi tafovut ulkan. HR tizimida jimgina xato = noto'g'ri hisoblangan oylik.

**Face ID xavotiri o'rinsiz:** qurilma sotuvchining o'zinikidan, tanib olishni **qurilma bajaradi**, biz faqat hodisani qabul qilamiz (HTTP/TCP). Bu C# uchun oddiy ish. Agar kelajakda ML kerak bo'lsa — alohida kichik Python servisi qo'shiladi.

### 2.3 Ikki til — bitta shartnoma (eng muhim qism)

Backend va frontend ajratilganda tiplar ulashilmaydi. Buni **kod generatsiyasi** hal qiladi:

```
C# Controller  →  OpenAPI spec (avtomatik)  →  Orval  →  TS tip + React Query hook
```

Backend'da `EmployeeDto` ga maydon qo'shildi → `pnpm gen` → frontend TypeScript'i darrov qizaradi.
**Bu monorepo'dan yaxshiroq:** shartnoma manbasi endi qo'lda yozilgan tip emas, haqiqiy API'ning o'zi. Ikki tomon hech qachon ajrab ketmaydi.

### 2.4 Dizayn tizimi — iOS uslubi

Butun interfeys **Apple Human Interface Guidelines** ruhida quriladi. Bu bezak emas —
HR xodimi kuniga soatlab shu ekranda ishlaydi, ortiqcha vizual shovqin charchatadi.

| Tamoyil | Amalda |
|---|---|
| Chuqurlik soya bilan emas, **qatlam** bilan | Fon `#F4F6F9` kulrang → karta oq. Soya deyarli sezilmaydi. |
| Ko'p **bo'sh joy** | Karta ichi 20px, bloklar orasi 16–24px. Zichlik emas — nafas. |
| Rang faqat **ma'no** uchun | iOS tizim ranglari: yashil — muvaffaqiyat, qizil — xato, sariq — ogohlantirish. Bezak uchun rang ishlatilmaydi. |
| **Yumshoq burchak** | 8 / 12 / 16 / 20px. O'tkir burchak yo'q. |
| **Tizim shrifti** | `-apple-system` birinchi o'rinda → macOS va iOS'da bu SF Pro. |
| **Bosilganda javob** | `scale(0.97)` — iOS'ning tugma bosilishi hissi. |
| **Segmented control** | Davr tanlash, ko'rinish rejimi — iOS'ning belgisi. |
| **Shaffof yuqori panel** | `backdrop-filter: blur(20px)` — kontent ostidan o'tib ko'rinadi. |

**Brend rangi logotipdan olindi:** `#154067` (chuqur navy). Undan 11 pog'onali
shkala qurilgan (`brand-50` … `brand-950`).

**Semantik tokenlar.** Komponentlar xom rangni emas, ma'noli nomni ishlatadi:
`--bg`, `--bg-elevated`, `--fg-secondary`, `--accent`, `--border`.
Tungi rejim faqat shu tokenlarni almashtiradi — komponentlarga tegilmaydi.

Tayyor primitivlar: `Card` · `StatCard` · `Button` · `Badge` · `SegmentedControl` ·
`Input` · `Avatar` · `IconTile` · `Skeleton` · `PageHeader` · `LanguageSwitcher` · `ThemeToggle`.

### 2.5 To'rt til

| Kod | Til | Nega kerak |
|---|---|---|
| `uz-Latn` | O'zbekcha (lotin) | Standart. Yosh xodimlar. |
| `uz-Cyrl` | Ўзбекча (кирилл) | **Bonvi'ning butun hujjati kirillda** — «№91 бланк», buyruqlar, anketalar. HR shu yozuvda o'qiydi. |
| `ru` | Русский | Verifix va Jobster interfeysi ruscha; ba'zi xodimlar ruscha qulay. |
| `en` | English | Texnik hujjat, tashqi integratsiya. |

> O'zbek tilining ikki yozuvi — bu ikki alohida til emas. BCP-47 standarti bo'yicha
> `uz-Latn` / `uz-Cyrl` deb belgilanadi (`uz` yoki `uz-UZ` emas), aks holda
> brauzer ularni bir-biriga aralashtirib yuboradi.

**Xato xabarlari qanday tarjima qilinadi.** Backend xabar emas, **kod** qaytaradi:

```
C#:  Error.Validation("pinfl.nazorat_raqami", "…")
        ↓  { "title": "pinfl.nazorat_raqami" }
TS:  t('errors.pinfl.nazorat_raqami')  →  foydalanuvchi tilida
```

Shu sababli bitta backend to'rtala tilda to'g'ri xato ko'rsatadi va
serverga til yuborish shart emas.

### 2.6 Ko'rib chiqilgan, tanlanmagan

| Variant | Nega yo'q |
|---|---|
| **Next.js** | SSR/server-actions qiymati backend C# bo'lgach yo'qoladi, lekin saqlanishi kerak bo'lgan Node.js runtime qoladi. Ichki panel — SEO kerak emas. Statik SPA mustahkamroq. |
| **Vue** | Yaxshi framework, lekin jadval/forma ekotizimi (TanStack + shadcn) React tomonda yetukroq. |
| **MediatR / AutoMapper** | 2025-yildan tijoriy litsenziyaga o'tdi. O'rniga: oddiy handler klasslar + Mapperly (bepul). |
| **Go** | 2-3x tezroq, lekin bu yukda farq sezilmaydi; ekotizimi (EF Core darajasidagi ORM, Hangfire) zaifroq. |
| **Supabase / Firebase** | Biznes mantiq DB ichiga ko'chadi. HR qoidalari murakkab — kod ichida turishi kerak. |

---

## 3. Arxitektura

### 3.1 Asosiy qaror: Hodisa jurnali (Event-sourced HR)

```
                    ┌──────────────────────────────┐
   Excel'da:        │ 20 ta sheet, qo'lda ko'chirish │
                    └──────────────────────────────┘
                                  ↓
                    ┌──────────────────────────────┐
   Bizda:           │  EmploymentEvent (yagona)    │
                    └──────────────┬───────────────┘
                                   │ hisoblanadi
        ┌──────────────┬───────────┼───────────┬──────────────┐
        ↓              ↓           ↓           ↓              ↓
   Aktiv xodim    Yangi kelgan  Ketgan   Lavozim o'zg.    Arxiv
     (91)           (91.2)      (91.6)     (91.3)        (91.7)
```

`EmploymentEvent` turlari: `Hire`, `Transfer`, `Promotion`, `AdditionalDuty`, `SalaryChange`, `ProbationResult`, `Termination`, `Rehire`.
Har bir hodisa: `buyruq raqami`, `buyruq sanasi`, `kuchga kirish sanasi`, `oldingi qiymat → yangi qiymat`, `kim kiritdi`, `izoh`.

**Natija:** HR bitta forma to'ldiradi → 8 ta ro'yxat avtomatik yangilanadi. Aynan u so'ragan narsa: *"шу битта бланка тўлдирилиши керак, тўлдирганда мана бу маълумот ҳаммаси ўзига кириб кетиши керак"*.

### 3.2 Loyiha strukturasi (qurilgan holat)

```
BonviHR/
├─ bonvi                       # ⭐ yagona buyruq nuqtasi (Docker o'ramchisi)
├─ docker-compose.yml          # development: postgres, redis, api, web
├─ docker-compose.prod.yml     # production
├─ PLAN.md · README.md · .env.example
│
├─ api/                        # BACKEND — mustaqil loyiha
│  ├─ Bonvi.sln
│  ├─ global.json              # SDK versiyasi qadalgan + test runner
│  ├─ Directory.Build.props    # umumiy qoidalar (strict, artifacts, analizatorlar)
│  ├─ Directory.Packages.props # ⭐ barcha paket versiyalari BITTA joyda
│  ├─ Dockerfile               # ko'p bosqichli production build
│  │
│  ├─ src/
│  │  ├─ Bonvi.Domain/                    # ⬅ HECH KIMGA BOG'LIQ EMAS
│  │  │  ├─ Common/                       #    Entity, AggregateRoot, ValueObject,
│  │  │  │                                #    Result<T>, Error, IAuditable
│  │  │  ├─ ValueObjects/                 #    Pinfl, PassportSeries, PhoneNumber,
│  │  │  │                                #    FullName, Money
│  │  │  └─ Abstractions/                 #    IClock, IRepository<T>, IUnitOfWork
│  │  │
│  │  ├─ Bonvi.Application/               # ⬅ faqat Domain'ga bog'liq
│  │  │  ├─ Abstractions/                 #    ICurrentUser, IFileStorage (portlar)
│  │  │  ├─ Common/DependencyInjection.cs
│  │  │  └─ <Modul>/Queries|Commands/     #    Query + Handler + Dto (vertikal kesim)
│  │  │
│  │  ├─ Bonvi.Infrastructure/            # ⬅ portlarni AMALGA OSHIRADI
│  │  │  ├─ Persistence/                  #    AppDbContext (snake_case + soft delete)
│  │  │  │  ├─ Interceptors/              #    AuditableInterceptor
│  │  │  │  ├─ Configurations/            #    IEntityTypeConfiguration (entity boshiga 1)
│  │  │  │  └─ Migrations/
│  │  │  ├─ Services/SystemClock.cs
│  │  │  └─ Storage/LocalFileStorage.cs
│  │  │
│  │  └─ Bonvi.Api/                       # ⬅ kompozitsiya ildizi
│  │     ├─ Endpoints/                    #    mavzu bo'yicha guruhlar
│  │     ├─ Common/                       #    CurrentUser, GlobalExceptionHandler
│  │     ├─ Extensions/ResultExtensions   #    Result → HTTP status
│  │     └─ Program.cs
│  │
│  └─ tests/
│     ├─ Bonvi.Domain.Tests/              # sof mantiq, bazasiz (millisekund)
│     ├─ Bonvi.Application.Tests/         # use-case, mock repozitoriy
│     ├─ Bonvi.Architecture.Tests/        # ⭐ qatlam qoidalari himoyasi
│     └─ Bonvi.Api.IntegrationTests/      # haqiqiy PostgreSQL ustida
│
└─ web/                        # FRONTEND — mustaqil loyiha
   ├─ package.json · vite.config.ts · orval.config.ts
   ├─ Dockerfile · Caddyfile
   └─ src/
      ├─ app/                  # routes/ (fayl asosida), providers, routeTree.gen.ts
      ├─ features/<modul>/     # api/ · components/ · schemas/
      ├─ shared/
      │  ├─ api/               # http-client (JWT + refresh), generated/ (Orval)
      │  ├─ ui/                # shadcn komponentlari
      │  └─ lib/               # auth-storage, cn
      └─ styles/globals.css    # Tailwind 4 mavzusi
```

**Qat'iy qoida (buzilmaydi):**
`Domain` → hech kimga bog'liq emas · `Application` → faqat `Domain` ·
`Infrastructure` → portlarni amalga oshiradi · `Api` → faqat `Application`.
**EF Core entity'si `Api` ga hech qachon chiqmaydi** — faqat DTO.

### 3.2.1 Domen identifikatorlari va EF Core

Identifikatorni **domen beradi** (`Entity.NewId()` → GUID v7), baza emas.
Bu ataylab: obyekt yaratilgan zahoti to'liq shakllangan bo'ladi va uni
saqlamasdan turib domen hodisalarida ishlatish mumkin.

Buning bitta nozik oqibati bor. EF Core kuzatilayotgan otaning
navigatsiyasidan **yangi bola** topganda, uning holatini `IsKeySet` bo'yicha
taxmin qiladi: kalit to'ldirilgan bo'lsa — «bu bazada mavjud» deb hisoblab,
`INSERT` o'rniga `UPDATE` yuboradi. Natijada
«expected to affect 1 row(s), but actually affected 0».

Muammo **faqat mavjud agregatga bola qo'shilganda** yuzaga chiqadi. Yangi
agregat yaratilganda butun graf `Added` bo'lgani uchun u ko'rinmaydi —
shu sababli yaratish va urug'lantirish testlaridan o'tib ketadi va faqat
ishlab turgan tizimda 500 xato bo'lib chiqadi.

Yechim `AppDbContext` da bitta joyda: `ChangeTracker.Tracked` ishlovchisi
so'rovdan kelmagan (`FromQuery == false`) va `Modified` deb belgilangan
yozuvni `Added` ga o'tkazadi. Bu **barcha** agregatlarga tegishli —
lavozim vazifalari, mehnat hodisalari, hujjatlar, rag'bat va jarimalar.

### 3.3 Arxitektura qoidalari — hujjatda emas, testda

`tests/Bonvi.Architecture.Tests` qatlamlar orasidagi bog'liqlikni **avtomatik tekshiradi**:

| Test | Nimani himoyalaydi |
|---|---|
| `Domain_boshqa_qatlamlarga_boglanmasligi_kerak` | Domain sof qoladi |
| `Domain_texnologiya_kutubxonalariga_boglanmasligi_kerak` | EF Core, ASP.NET, Hangfire, Serilog — Domain'da yo'q |
| `Application_Infrastructure_va_Api_ga_boglanmasligi_kerak` | Use-case'lar amalga oshirishni bilmaydi |
| `Application_EF_Core_ga_boglanmasligi_kerak` | Use-case'lar faqat repozitoriy interfeysi orqali |
| `Application_ASP_NET_Core_ga_boglanmasligi_kerak` | Use-case'lar HTTP haqida bilmaydi |
| `Infrastructure_Api_ga_boglanmasligi_kerak` | Teskari bog'liqlik yo'q |
| `Api_Npgsql_ga_todan_togri_boglanmasligi_kerak` | Veb qatlami DB drayveri bilan ishlamaydi |

Qoida buzilsa — **build yiqiladi**, kod yetkazib berilmaydi.

### 3.4 Modullar ro'yxati (1-bosqich)

| # | Modul | Nimani qamraydi | Excel manbasi |
|---|---|---|---|
| M0 | `Common` + `Auth` + `Audit` | RBAC, login, kim-nima-o'zgartirdi jurnali | — |
| M1 | `Organization` | Yuridik shaxs, Yo'nalish, Bo'lim (daraxt), Ish joyi | Мехнат дафтарчаси, Йўналиш, Бўлим |
| M2 | `Catalog` | Lavozim + lavozim vazifalari, sabab/rag'bat/jarima turlari | Лавозим (228 ta) |
| M3 | `Employees` | Shaxs kartasi: FIO, JSHSHIR, passport, manzil, telefon, rasm | 91 Умумий |
| M4 | `Employment` | Mehnat munosabati + hodisalar jurnali + buyruqlar | 91.2 / 91.3 / 91.4 / 91.6 |
| M5 | `Probation` | Sinov muddati + xulosa + avto-eslatma | 91.5 / 91.8 |
| M6 | `Flags` | Rezerv, Qora ro'yxat, Nazorat, Vazifa berса bo'ladigan | 91.9 / 91.10 / 91.12 / Назорат |
| M7 | `Competency` | Til, kompyuter (%), ta'lim, qiziqish, qobiliyat | 91.15 |
| M8 | `Incentives` | Rag'bat va **jarima** (Excelda yo'q — yangi) | 91.14 |
| M9 | `Documents` | Fayl saqlash: shartnoma, buyruq, anketa, skan | (Telegram'dagi rasmlar) |
| M10 | `Feedback` | Exit interview anketasi (Google Form o'rniga) | 91.16 |
| M11 | `Safety` | Baxtsiz hodisalar jurnali | Бахтсиз ходисалар |
| M12 | `Reporting` | Dashboard, hisobotlar, Excel eksport | — |

---

## 4. Ma'lumot modeli (asosiy jadvallar)

```
LegalEntity ──┐
Direction ────┤
Department (ParentId → o'zi) ─┤
Position ──┬── PositionDuty   │
           │                  │
Employee ──┴──< Employment >──┘        Employment: bitta shaxs, bir nechta firma
   │                  │                            (0.5 stavka, o'rindosh)
   │                  ├──< EmploymentEvent   (Hire/Transfer/Termination/...)
   │                  ├──< Probation
   │                  └──< ExitInterview
   │
   ├──< EmployeeDirection   (M2M — "Вело, метан, шина")
   ├──< EmployeeDocument    (MinIO fayl)
   ├──< EmployeeCompetency
   ├──< EmployeeFlag        (Reserve | Blacklist | Monitoring | Capable)
   ├──< Incentive           (Award | Penalty)
   └──< Incident

User ──< Role ──< Permission        AuditLog (barcha o'zgarishlar)
```

**Kalit qarorlar:**
- `Employee.Pinfl` (JSHSHIR) — `UNIQUE`, asosiy identifikator. Passport o'zgaradi, JSHSHIR — yo'q.
- Hech narsa jismonan o'chirilmaydi — `DeletedAt` (soft delete). HR ma'lumoti tarixiy hujjat.
- Barcha sana `DateOnly` (vaqt zonasi muammosi yo'q), barcha pul `decimal(14,2)`.
- `Employment.Status`: `Probation | Active | Terminated`. `IsPrimary` + `Rate` (1.0 / 0.5).
- `Pinfl`, `PhoneNumber`, `Money` — **Value Object**, primitiv `string` emas: noto'g'ri qiymat obyekt yaratilishida rad etiladi.
- Har bir hodisada `OrderNo` + `OrderDate` — qidiruv shu bo'yicha ketadi.

---

## 5. Biznes qoidalar (Domain qatlamida, DB'da emas)

Bular tizimning asosiy qiymati — Excel buni qila olmaydi:

1. **Bo'shatishda blok.** Xodim `Blacklist` yoki `Monitoring` da bo'lsa — ogohlantirish + tasdiq talab qilinadi. *(HR aynan shuni so'radi.)*
2. **Qayta ishga olishda tekshiruv.** JSHSHIR bo'yicha: ilgari ishlaganmi, qora ro'yxatdami, necha marta kelib-ketgan.
3. **Sinov muddati avtomatik.** `Hire` hodisasida `HireDate + 3 oy` → `Probation`. Tugashiga **7 kun** qolganda HR ga bildirishnoma (Hangfire cron).
4. **Lavozim → vazifa avtomatik.** `Position` tanlanganda uning `PositionDuty` ro'yxati yuklanadi. *(HR: "лавозимни танлаганда автоматически вазифаси ҳам юкланади".)*
5. **15 kunlik ogohlantirish.** Bo'shash arizasi kiritilganda `+15 kun` sanasi hisoblanadi va kuzatiladi.
6. **Stavka nazorati.** Bitta shaxsning barcha aktiv `Employment` stavkalari yig'indisi > 1.5 bo'lsa — ogohlantirish.
7. **Audit.** Har bir o'zgarish: kim, qachon, nima edi → nima bo'ldi.

### 5.1 Kim tizimdan foydalanadi

**Xodimlar tizimga kirmaydi.** Bu o'z-o'ziga xizmat portali emas — HR bo'limi
uchun ish quroli. Shu sababli rollar kam va aniq:

| Rol | Kim | Ruxsatlar |
|---|---|---|
| `admin` | Asosiy rahbar / tizim administratori | **21** — barchasi, jumladan foydalanuvchi boshqaruvi |
| `hr_manager` | HR menejeri — Bonvi'da **3 kishi** | **20** — butun kadrlar ishi, oylik ish haqi ko'rinadi; foydalanuvchi boshqaruvi yopiq |

Atigi ikkita rol. «Operator» darajasi kerak emas — HR bo'limidagi uchala
xodim ham to'liq huquqli menejer. Bo'lim boshliqlari va xodimlar uchun rol
ataylab yaratilmagan.

`salary.view` shunga qaramay **alohida ruxsat** sifatida ajratilgan: hozir
u ikkala rolda ham bor, lekin kelajakda kimningdir okladini yopish kerak
bo'lsa, yangi rol yaratish kifoya — kodga tegilmaydi.

### 5.2 Autentifikatsiya xavfsizligi

| Qoida | Sabab |
|---|---|
| Noma'lum login va noto'g'ri parol — **bir xil xato** | Aks holda hujumchi qaysi raqamlar ro'yxatdan o'tganini aniqlab oladi |
| Foydalanuvchi topilmasa ham **soxta xesh tekshiriladi** | Javob vaqtidagi farq bo'yicha login mavjudligi bilinmasligi uchun |
| **5 ta xatodan keyin 15 daqiqa blok** | Brute-force sekinlashadi; butunlay bloklanmaydi — aks holda raqib xodimni ataylab qulflab qo'yardi |
| Refresh token **xesh holida** saqlanadi | Baza sizib chiqsa ham tokenlardan foydalanib bo'lmaydi |
| **Token aylantirish + qayta ishlatishni aniqlash** | Bekor qilingan token ishlatilsa — o'g'irlik belgisi, barcha sessiyalar yopiladi (OWASP tavsiyasi) |
| Access token **15 daqiqa** | JWT'ni serverda bekor qilib bo'lmaydi, shu sababli u tez eskirishi kerak |
| Login endpoint'i uchun **qat'iy tezlik cheklovi** | Bu yagona parol tanlash mumkin bo'lgan nuqta |
| Sukut bo'yicha **barcha endpoint himoyalangan** | Ochiqlik ataylab e'lon qilinadi (`AllowAnonymous`) — himoyani unutishdan ko'ra xavfsizroq |

---

## 6. Ish rejasi — 1-bosqich

> Har bir qadam: **kod → test → shu faylda ✅**. Qadam tugamaguncha keyingisiga o'tilmaydi.

### S0 — Fundament ⏱ 1 kun ✅ **BAJARILDI**
- [x] Xost mashinaga hech narsa o'rnatilmadi — barcha vositalar konteynerda
- [x] `./bonvi` — yagona buyruq nuqtasi (up/down/build/test/migrate/gen/psql/sh)
- [x] Ikki mustaqil loyiha: `api/` va `web/` (alohida build, alohida konteyner, alohida deploy)
- [x] `Bonvi.sln` — 4 ta manba + 4 ta test loyihasi, bog'liqlik yo'nalishi qadalgan
- [x] `Directory.Build.props`: `Nullable`, `TreatWarningsAsErrors`, `AnalysisLevel`, `ArtifactsPath`
- [x] `Directory.Packages.props`: barcha paket versiyalari markazlashgan
- [x] `global.json`: SDK 10.0.400 qadalgan + `Microsoft.Testing.Platform` runner
- [x] `docker-compose.yml`: postgres 18.6, redis 8.8, api (hot reload), web (HMR)
- [x] `docker-compose.prod.yml` + ko'p bosqichli `Dockerfile` (api va web)
- [x] Vite 8 + React 19 + TS 7 + Tailwind 4 + TanStack skeleti

### S1 — Kernel, Auth, Kontrakt ⏱ 2–3 kun ✅ **BAJARILDI**
- [x] `Bonvi.Domain/Common`: `Entity`, `AggregateRoot`, `ValueObject`, `Result<T>`, `Error`, `IAuditable`, `ISoftDeletable`, `IDomainEvent`
- [x] Value Object'lar: `Pinfl` (nazorat raqami + sana), `PassportSeries` (gomoglif normalizatsiyasi), `PhoneNumber`, `FullName`, `Money`
- [x] `AppDbContext`: snake_case + yumshoq o'chirish global filtri
- [x] `AuditableInterceptor` — kim/qachon avtomatik to'ldiriladi, chetlab o'tib bo'lmaydi
- [x] `GlobalExceptionHandler` + `ResultExtensions` (Result → HTTP status)
- [x] `IFileStorage` porti + `LocalFileStorage` (path traversal himoyasi bilan)
- [x] **OpenAPI + Scalar** ishga tushdi → **Orval generatsiyasi tekshirildi** ⭐
- [x] `Bonvi.Architecture.Tests` — 7 ta qoida, avtomatik himoya
- [x] Birinchi vertikal kesim: `GET /api/system/info` (Query → Handler → Dto → Endpoint → TS hook)
- [x] **103 test, 0 xato, ~1 soniya**
- [x] **Dizayn tizimi (iOS uslubi)**: tokenlar, 11 ta primitiv, yorug'/tungi rejim
- [x] **4 til**: `uz-Latn` · `uz-Cyrl` · `ru` · `en` — 123 tadan kalit, hammasi mos
- [x] Ilova qobig'i: yon menyu (yig'iladigan), yuqori panel, til va mavzu almashtirgich
- [x] Brend: logotip + `#154067` navy shkalasi
- [x] **JWT auth**: access 15 daq / refresh 7 kun, token aylantirish + o'g'irlikni aniqlash
- [x] **`User`, `Role`, `RefreshToken`, `AuditLog`** entity'lari + birinchi migratsiya
- [x] **Ruxsatga asoslangan avtorizatsiya** — 15 ta ruxsat, 2 ta rol
- [x] **Xavfsizlik**: sarlavhalar, CSP, tezlik cheklovi (login uchun qat'iyroq), forwarded headers
- [x] **Yuklamaga chidamlilik**: javob siqish, output cache, `/health/live` + `/health/ready`, so'rov taymauti
- [x] **Frontend**: kirish sahifasi, himoyalangan marshrut, auth konteksti, telefon maskasi
- [x] **158 test, 0 xato**

### S2 — Ma'lumotnomalar (M1 + M2) ⏱ 2 kun ✅ **BAJARILDI**
- [x] **Domain**: `ReferenceEntity` asosi + `LegalEntity`, `Direction`, `Department` (daraxt), `Position` + `PositionDuty`
- [x] **Domain**: `TerminationReason`, `AwardType`, `PenaltyType`, `DocumentType`
- [x] **Repozitoriy portlari**: `IReferenceRepository<T>`, `IDepartmentRepository`, `IPositionRepository`
- [x] **Domain testlari** — 156 test (nom normalizatsiyasi, daraxt, vazifalar, validatsiya)
- [x] **Infrastructure**: 9 jadval, 42 indeks, migratsiya, idempotent seeder
- [x] **Excel tozalandi** — quyidagi jadval
- [x] **Application + Api**: generik use-case'lar, 62 ta katalog endpoint'i
- [x] **Frontend**: 8 ta sahifa (6 tasi bitta komponentdan), daraxt, vazifalar muharriri
- [x] **TypeScript klienti** OpenAPI'dan generatsiya qilindi (50 yo'l, 67 amal)
- [x] **249 test, 0 xato**

#### Tozalash natijasi (butun Excel bo'yicha, arxiv ham)

| Ma'lumotnoma | Xom qiymat | Kanonik | Qisqarish |
|---|---|---|---|
| Lavozim | **1244** | 481 | 2.6× |
| Ishdan ketish sababi | **232** | 21 | 11× |
| Bo'lim | **631** | 96 (43 ildiz + 53 ostki) | 6.6× |
| Yo'nalish | **173** | 8 | 22× |
| Yuridik shaxs | 30 | 20 | — |
| Rag'bat / jarima / hujjat turi | — | 7 / 7 / 15 | yangi |

> **Raqamlar S3 da yangilandi.** Dastlab jadval faqat to'rtta varaqdan
> qurilgan edi (asosiy ro'yxat, arxiv, `91.6`, `91.14`). S3 da ma'lum
> bo'ldiki, hodisa varaqlarida (`91.2`, `91.3`, `91.4`, `91.5`, `91.8`)
> sex nomlari umuman boshqacha yozilgan va ularning 24–36 % i jadvalda
> yo'q edi. Varaqlar qo'shildi, natijada **barcha varaq va ustunlarda
> qamrov 100 %** ga chiqdi. Tarixiy lavozimlar (`Табелчи`, `Сокчи`,
> `Экскаваторчи`…) kanonik ro'yxatga qo'shildi — shu sababli kanonik
> lavozim 197 dan 481 ga ko'tarildi.

Yakuniy qamrov: asosiy varaqning barcha 4 ustuni **100%**, arxivda lavozim
95.8%, bo'lim 97.9%, ishdan ketish sababi 99.7%. Bog'lanmagan 40 ta qiymat —
sof chiqindi (Excel serial raqami, xodim ismi, sana matni).

Eng ko'p birlashtirilgan guruhlar: `intizom-buzilishi` ← 39 variant,
`boshqa-ishga-otdi` ← 28, `oilaviy-sharoit` ← 20, `sogligi` ← 19.

**109 ta qiymat tushunarsiz qoldi** — `aliases.json` dagi `unmapped`
bo'limida, HR bilan aniqlanishi kerak. (S2 da 40 ta edi, lekin ularning
ko'pi hal qilingan chiqindi edi; endi u yerda faqat **haqiqatan noaniq**
qiymatlar turibdi.)

Ular ikki turga bo'linadi va ikkalasi ham **taxmin qilinmagan**:

- **Bo'lim ko'rsatilmagan lavozim** — `Кара хайдовчи` (~59 katak):
  transport bo'limimi yoki ombor/zavodmi; `Электрик` (~38): energetika
  faqat metan zavodida bor; `Сваршик` (~32): «Velo union» da oltita
  payvandlash bo'limi bor; `Слесар` / `Токарь` (~34), `Смена рахбари` (~30).
- **Bir katakda ikki ish** — `кассир ва табелчи`, `Сотувчи, хайдовчи`;
  hamda yalang'och `Бошлиғи` / `Ишчиси`.

**`aliases.json`** — S3 uchun eng muhim artefakt: Excel'dagi har bir xom qiymat
kanonik kodga bog'langan. Bo'limlar uchun ikki xil jadval bor (`departments`
va `departmentBlocks`) — sheet'lardagi ikki xil yozuv uslubi uchun.

**Muhim qaror — nom normalizatsiyasi.** Barcha ma'lumotnoma nomlari saqlashdan
oldin tozalanadi: chetdagi va ichidagi ortiqcha bo'shliqlar olib tashlanadi.
Ajratgich sifatida **barcha** bo'shliq belgilari hisobga olinadi — tabulyatsiya
va uzilmaydigan bo'shliq (U+00A0) ham. Excel eksportida ular tez-tez uchraydi
va ko'zga oddiy probeldek ko'rinadi, lekin bir xil nomni ikki xil yozuvga
aylantirib yuboradi. Aynan shu Excel'dagi tarqoqlikning bir sababi edi.

**Muhim qaror — kanonik nom kirillda.** Bonvi'ning barcha hujjatlari
(«№91 бланк», buyruqlar, anketalar) kirillda va HR shu yozuvda o'qiydi.
Shu sababli ma'lumotnoma nomlari kirillda saqlanadi, `code` esa lotin slug —
u barqaror va integratsiya uchun ishlatiladi.

### S3 — Ma'lumot migratsiyasi (ETL) ⏱ 2–3 kun ✅ **VOSITA TAYYOR** — HR ko'rigini kutmoqda  ⚠️ eng nozik qadam

**Kirish:** `№91 бланк Ходимлар.xlsx` — 20 sheet, ~11 000 yozuv.
**Chiqish:** to'ldirilgan baza + qo'lda tuzatish talab qiladigan yozuvlar ro'yxati.

> **Tartib tuzatildi.** Dastlabki rejada S3 (ETL) S4 (xodim kartasi) dan oldin
> turardi, lekin ko'chirish uchun avval `Employee` va `Employment` entity'lari
> kerak — ularsiz ma'lumotni joylashtiradigan joy yo'q. Shu sababli S4/S5 ning
> **domen qismi** S3 bilan birga bajarilmoqda.

#### Domen tayyor ✅

| Entity | Nima uchun |
|---|---|
| `Employee` | Shaxs. JSHSHIR unikal, lekin **majburiy emas** — Excel'da 3 tasida yo'q, 9 tasida xato |
| `Employment` | Mehnat munosabati. Bitta shaxsda bir nechta bo'lishi mumkin (o'rindoshlik, qayta kelish) |
| `EmploymentEvent` | ⭐ Tizimning yuragi: 8 ta hodisa turi, o'zgartirib bo'lmaydi |
| `Probation` | Sinov muddati, 7 kun qolganda ogohlantirish |
| `EmployeeFlag` | Rezerv / qora ro'yxat / nazorat / qobiliyatli — to'rt varaq o'rniga bitta jadval |

**211 domen testi** yozildi (avvalgi 156 dan +55).

#### Bosqichlari

- [x] `api/tools/Bonvi.Etl` — konsol dasturi (ClosedXML bilan o'qiydi), `./bonvi etl`
- [x] **1-qadam: ma'lumotnomalar.** `Seed/data/*.json` + `aliases.json` orqali.
      Alias jadvali hodisa varaqlarini ham qamrab olish uchun **qayta qurildi** —
      quyidagi jadvalga qarang.
- [x] **2-qadam: xodimlar** — uchta varaqdan: `91 Умумий` (540), `91.2` (1960),
      `91.7 Архив` (1715). JSHSHIR → pasport → ism+tug'ilgan sana tartibida
      birlashtiriladi. Natija: **2347 alohida shaxs**.
- [x] **3-qadam: mehnat munosabatlari** (`91.2` — 1960 ta qabul + karta
      varaqlaridan 617 ta qoldiq). Natija: **2538 munosabat**.
- [x] **4-qadam: hodisalar** (`91.3` lavozim o'zgarishi, `91.4` qo'shimcha
      vazifa, `91.6` bo'shatish, `91.8` stajirovkadan ketish). Natija:
      **3527 hodisa**.
- [x] **5-qadam: teglar** (`91.9` rezerv, `91.10` qora ro'yxat, `91.12`,
      `Назорат`) va **sinov muddatlari** (`91.5` + karta varaqlari) —
      **1334 sinov muddati**, **32 belgi**.
- [ ] **Profil boyitish** (`91.15` qobiliyatlar 554, `91.14` rag'bat 34,
      `91.16` exit interview 192, baxtsiz hodisalar) — **S8/S9 ga ko'chirildi.**
      Sabab: bu ma'lumotni joylashtiradigan entity'lar (`EmployeeSkill`,
      `EmployeeAward`, `ExitInterview`) hali domenda yo'q. Ularsiz ko'chirish
      mumkin emas, mavjud jadvalga tiqishtirish esa modelni buzardi.
- [x] `--dry-run` rejimi — **standart holat**; yozish uchun `--apply` ataylab
      talab qilinadi va u `--connection` siz umuman ishlamaydi
- [x] Migratsiya hisoboti (CSV): qadam, varaq, qator, xodim, muammo, qiymat, chora
- [x] **120 ta ETL testi** — Excel faylsiz va bazasiz ishlaydi

#### Hal qilinishi kerak bo'lgan muammolar (Excel tahlilidan ma'lum)

| Muammo | Yechim |
|---|---|
| **Sana** Excel serial raqamda (`46212`) | `1899-12-30 + N kun` → `DateOnly` |
| **JSHSHIR** 9 tasida nazorat raqami xato, 3 tasida umuman yo'q | Import qilinadi, lekin `Pinfl` maydoni bo'sh qoladi + tuzatish ro'yxatiga tushadi |
| **JSHSHIR ichidagi sana** 32 tasida tug'ilgan sana bilan mos emas | Ogohlantirish, import to'xtamaydi |
| **Pasport kirill harflarida** (607 kirill / 495 lotin) | `PassportSeries.Normalize` avtomatik lotinga o'giradi |
| **Telefon**: 13 ta katakda ikkita raqam, 1 tasida to'rtta | `PhoneNumber.SplitMultiple` ajratadi, birinchisi asosiy |
| **Yo'nalish** kombinatsiya (`Вело, метан, шина`) | `aliases.json` da `velo\|metan\|shina` — ko'p-ga-ko'p bog'lanish |
| **Bir xodim ikki firmada** (`VIKOR + Вело унион 0,5 ставка`) | Ikkita `Employment`, `Rate = 0.5`, biri `IsPrimary` |
| **Sinov muddati**: 411 tasida «Тугатилган», qolganida sana | Matn → `Probation.Status`, sana → `EndDate` |
| **Dublikat xodim** | JSHSHIR → pasport → (FIO + tug'ilgan sana) tartibida qidiriladi |

#### Quruq yurish natijasi

| Qadam | O'qildi | Yaratiladi | Mavjud | Tashlandi |
|---|---:|---:|---:|---:|
| Xodimlar | 4215 | **2347** | 1853 | 15 |
| Mehnat munosabatlari | 1960 | **2538** | 0 | 39 |
| Hodisalar | 4171 | **3527** | 38 | 606 |
| Sinov muddatlari | 2868 | **1334** | 995 | 358 |
| Belgilar | 143 | **32** | 2 | 109 |

Hisobotda **4739 satr** — HR uchun ish ro'yxati. Eng kattalari:

| Muammo | Soni | Bu nima |
|---|---:|---|
| JSHSHIR kiritilmagan | 1564 | Arxiv va `91.2` da ustun bo'sh — **1540 alohida odam** |
| Qabul buyrug'i topilmadi | 617 | `91.2` da yo'q; munosabat karta qatoridan yaratildi |
| Pasport qabul qilinmadi | 408 | Ustun siljigan — katakda manzil matni turibdi |
| Sudlanganlik ustunida son | 327 | O'sha ustunga JSHSHIR yoki sana tushib qolgan |
| Xodim topilmadi | 319 + 147 | 147 tasida ehtimoliy moslik ko'rsatildi (pastga qarang) |
| Sinov muddati sanasiz | 218 | «Тугатилган» yozilgan, lekin sanalari yo'q |

#### S3 da topilgan va tuzatilgan uchta muammo

**1. Alias jadvali hodisa varaqlarini qamrab olmagan edi.** U S2 da faqat
to'rtta varaqdan qurilgan. Hodisa varaqlarida sex nomlari (`Учини букиш`,
`Кара хайдовчи`, `Хаво б/н синаш б/ми`) umuman yo'q edi va ular bo'sh
qolardi. Jadval qayta qurildi:

| Varaq / ustun | Oldin | Keyin |
|---|---:|---:|
| `91.2` bo'lim | 76.0% | **100%** |
| `91.2` lavozim | 86.3% | **100%** |
| `91.3` eski bo'lim | 67.2% | **100%** |
| `91.3` yangi bo'lim | 64.2% | **100%** |
| `91.3` yangi lavozim | 75.6% | **100%** |
| `91.4` lavozim | 72.9% | **100%** |
| `91.5`, `91.6`, `91.7`, `91 Умумий` | — | **100%** |

**2. 223 ta katakni ClosedXML umuman o'qiy olmasdi.** Ular — JSHSHIR va
telefon raqamlari, lekin katakka tasodifan `dd.mm.yyyy` formati berilgan.
`32612914200075` ni sanaga aylantirib bo'lmaydi va kutubxona
«Not a legal OleAut date» deb yiqilardi — **butun ko'chirish bitta katak
tufayli to'xtardi**. Endi xom son aylantirishsiz o'qiladi va hammasi
qutqariladi. `GetFormattedString()` ishlatilmaydi: u bunday katakda
`«31.12.1899»` qaytaradi, ya'ni JSHSHIR o'rniga soxta sana yozilardi.

**3. Arxiv varag'ining sarlavhasi ma'lumotiga to'g'ri kelmaydi.** Ustunlar
qiymat turi bo'yicha o'lchab chiqildi: 13-ustun sarlavhada «Судимость»
deb yozilgan va haqiqatan shunday, lekin 15-ustun «Телеграмм» deb
yozilgani holda 54 ta qiymatning 42 tasi — sana. U endi o'qilmaydi.

#### Ataylab QILINMAGAN narsa

Hodisa varaqlarida otasining ismi tez-tez tushib qolgan:
«Абдуллаев Умиджон» ↔ «Абдуллаев Умиджон Мўйдинжон ўғли». Bunday **99 ta**
odamda familiya+ism bo'yicha yagona nomzod topiladi. ETL ularni
**o'zi bog'lamaydi**: O'zbekistonda aka-uka bir xil familiya va ismda
bo'lishi odatiy hol, ya'ni «yagona moslik» hali «bir xil odam» degani emas.
Noto'g'ri odamga yozilgan ishdan bo'shatish buyrug'i — yo'qolgan yozuvdan
ancha yomonroq. Nomzod hisobotda ko'rsatiladi, qarorni HR qabul qiladi.

#### Tamoyillar

1. **ETL hech qachon ma'lumotni o'ylab topmaydi.** Qiymat noaniq bo'lsa —
   bo'sh qoldiriladi va hisobotga tushadi. Taxminan to'ldirilgan ma'lumot
   bo'sh maydondan yomonroq: uni keyin hech kim tekshirmaydi.
2. **Idempotent.** Qayta ishga tushirilsa dublikat yaratmaydi.
3. **Avval `--dry-run`.** Hisobot ko'rilmaguncha bazaga yozilmaydi.
4. **Hodisalar tarixi saqlanadi.** `91.7 Архив` dagi 1715 yozuv ham
   ko'chiriladi — «kim qachon ishlagan» savoli javobsiz qolmasligi kerak.

### S4 — Xodim kartasi (M3) ⏱ 3 kun ✅ **BAJARILDI**
- [x] `Employee` CRUD, JSHSHIR unikalligi. O'CHIRISH ENDPOINT'I ATAYLAB YO'Q:
      HR modelida xodim «bo'shatiladi», «o'chirilmaydi» — tarixni yo'q qilish
      buyruq raqamiga ega yozuvlarni javobsiz qoldirardi
- [x] **Xodim rasmi** — ⚠️ *aniq talab, 2026-08-22*: ro'yxatda bosh harflar
      (`АА`, `БО`) emas, **xodimning haqiqiy rasmi** ko'rinishi kerak — jadval
      ko'rinishida ham, kartochkada ham. Holat:
    - [x] `Employee.PhotoKey` + `SetPhoto` — domenda bor
    - [x] `EmployeeSearchRow.PhotoKey` — ro'yxat so'rovi uni qaytaradi
    - [x] `IFileStorage` + `LocalFileStorage` (path traversal himoyasi bilan)
    - [x] `POST /api/employees/{id}/photo` — markazdan kvadrat qirqib 512×512
          JPEG qiladi, eskisini o'chiradi. Tekshirildi: 600×600 PNG (104 KB)
          → 512×512 JPEG (8 KB)
    - [x] `GET /api/employees/{id}/photo` — `ETag` va kesh sarlavhalari bilan;
          `If-None-Match` da `304`
    - [x] `DELETE /api/employees/{id}/photo` — idempotent (`204`, rasm bo'lmasa ham)
    - [x] Fayl turi **mazmunga qarab** aniqlanadi: rasm bo'lmagan fayl `400`
          («Fayl rasm emas yoki buzilgan») — kengaytmaga ham, mijoz yozgan
          `Content-Type` ga ham ishonilmaydi
    - [x] Frontend: `photoKey` bo'lsa rasm, bo'lmasa bosh harflar — jadval,
          kartochka va profilda; profilda yuklash va o'chirish oynasi
    - [x] 4 tilga 18 ta kalit
    - [x] **Manba aniqlandi:** Google Disk, ommaviy ko'chirish kerak emas —
          HR qo'lda yuklaydi (9-bo'lim, 7-savol)

> **Rasm kutubxonasi — SkiaSharp, ImageSharp emas.** ImageSharp «Six Labors
> Split License» ostida va daromad chegarasidan yuqori tijorat tashkilotidan
> pullik litsenziya talab qiladi. Bonvi — ishlab chiqarish holdingi, ya'ni bu
> haqiqiy xavf. SkiaSharp esa MIT va konteynerda qo'shimcha tizim
> kutubxonalarisiz ishlaydi.
>
> **`<img src>` bilan ishlamaydi.** Autentifikatsiya `Authorization`
> sarlavhasi orqali (cookie yo'q), shu sababli rasm axios bilan `blob`
> qilib olinadi va `URL.createObjectURL` orqali ko'rsatiladi; kesh kaliti
> `photoKey` ni o'z ichiga oladi, ya'ni rasm almashtirilsa kesh o'zi yangilanadi.
- [x] **Xodimlar ro'yxati:** TanStack Table — server-side filtr/sort/paginatsiya
      (`IEmployeeReadModel` proyeksiyasi, 7 filtr, 6 saralash ustuni)
- [x] Global qidiruv: FIO / JSHSHIR / telefon / passport
- [x] **Xodim profili** — oltita tab: Umumiy · Mehnat tarixi · Hujjatlar ·
      Qobiliyatlar · Rag'bat/Jarima · Belgilar. Ro'yxat DINAMIK: ruxsati
      bo'lmagan tab umuman chizilmaydi; har bir tab o'z ma'lumotini
      ochilganda so'raydi
- [x] Kartochka ko'rinishi (rasmli grid) — HR aynan shuni ko'rsatdi.
      `web/src/features/employees/components/employees-grid.tsx` + jadval/kartochka
      almashtirgichi; tanlangan ko'rinish manzilda saqlanadi (`?view=grid`) —
      sahifa yangilansa yoki havola ulashilsa ham yo'qolmaydi

### S5 — Mehnat munosabati va hodisalar (M4) ⏱ 2–3 kun ✅ **BAJARILDI**
- [x] `Employment` + `EmploymentEvent` (Domain qoidalari bilan)
- [x] **Yagona forma:** Ishga qabul (shaxs + firma + bo'lim + lavozim + oklad + buyruq)
- [x] **Lavozim o'zgarishi / bo'lim ko'chirish** — avvalgi qiymat avtomatik to'ladi
- [x] **Qo'shimcha vazifa berish**
- [x] **Oklad o'zgarishi**
- [x] **Ishdan bo'shatish** — sabab ma'lumotnomadan + **qora ro'yxat bloki ishlaydi**
- [x] **Xodim timeline'i** — butun mehnat yo'li bitta vertikal lentada
- [x] `/buyruqlar` — barcha hodisalar bo'yicha umumiy ro'yxat. Backend
      allaqachon bor edi: `GET /api/employments/events` (qidiruv, tur,
      sana oralig'i, bo'lim, yuridik shaxs, sahifalash). Endi frontend ham:
      `web/src/app/routes/_app/buyruqlar.tsx` + `web/src/features/employments/`
      (qo'lda yozilgan API klienti, toolbar, jadval, manzilda saqlanadigan filtrlar)

> **Hammasi xodim kartasidan bajariladi.** HR odamni ochib, o'sha yerda
> ko'chiradi, vazifa beradi, okladni o'zgartiradi, belgi qo'yadi va
> bo'shatadi — boshqa ekranga o'tmaydi. Har bir amal buyruq raqami va
> sanasi bilan rasmiylashtiriladi va darhol tarixga tushadi.
>
> **Bo'shatishdagi blok tekshirildi:** qora ro'yxatdagi xodimni bo'shatishga
> urinilganda server `409` va `xodim.qora_royxatda` qaytaradi. HR sababni
> o'qib, ongli ravishda tasdiqlaydi — shundan keyingina amal bajariladi.

### S6 — Sinov muddati (M5) ⏱ 1 kun ✅ **BAJARILDI**
- [x] Avtomatik yaratish (hire + 3 oy), xulosa kiritish, uzaytirish — backend
      (`/api/probations`, `…/resolve`, `…/extend`)
- [x] ~~Hangfire cron: har kuni 09:00 → bildirishnoma~~ → **Eslatma tizim
      ichida** (qaror 22.08.2026)
- [x] **«Sinov muddati» sahifasi** (`/sinov`): yuqorida «N ta muddat tugayapti»
      ogohlantirishi, holat bo'yicha filtr, qolgan kun ustuni, yakunlash va
      uzaytirish dialoglari
- [x] Yon menyu va bosh sahifada sanoq. Bosh sahifada allaqachon bor edi;
      endi yon menyuda ham (`web/src/app/layout/sidebar.tsx`) — sinovdagilar
      soni belgi sifatida. Yig'ilgan menyuda (76px) raqamga joy yo'q, shuning
      uchun faqat nuqta chiziladi

> **Nega Hangfire kerak emas.** Dastlabki rejada har kuni 09:00 da ishlaydigan
> vazifa ko'zda tutilgandi. Lekin bu tizimdan **faqat 3 HR menejeri va rahbar**
> foydalanadi va eslatma ularga tizim ichida ko'rsatiladi. Bunda:
>
> - ma'lumot **jonli so'rovdan** olinadi, ya'ni doim aniq — kechagi vazifa
>   natijasi emas;
> - Hangfire serveri, uning jadvali va nosozliklarini kuzatish **umuman
>   kerak emas**;
> - «vazifa ishlamay qolgan, hech kim sezmagan» degan xato holati yo'q.
>
> Cron faqat xabar **tashqariga** ketganda ma'noga ega bo'lardi (Telegram,
> pochta). Bunday talab paydo bo'lsa, `Hangfire.Core` va `Hangfire.PostgreSql`
> paketlari allaqachon qo'shilgan — faqat ulash qoladi.

**«Стажировкадан кетганлар» (91.8) alohida sahifa qilinmadi.** U ishdan
bo'shatishning bir turi: ETL uni sinov muddati natijasi (`LeftEarly`) sifatida
yozadi, ya'ni sinov muddatlari sahifasida holat bo'yicha filtrlab ko'rsa
bo'ladi. Alohida sahifa bir xil ma'lumotni ikkinchi joyda ko'rsatardi — Excel'da
aynan shu tarqoqlik muammo edi.

### S7 — Teglar va ro'yxatlar (M6) ⏱ 2 kun ✅ **BAJARILDI**
- [x] `EmployeeFlag`: Reserve / Blacklist / Monitoring / Capable — sabab, sana, mas'ul
- [x] **Uch sahifa** (`/rezerv`, `/qora-royxat`, `/nazorat`) + xodim profilida
      ko'rinish. Uchalasi **bitta komponentdan** (`FlagListPage`) — ustunlari va
      amallari bir xil, faqat sarlavha, ikonka va matni farq qiladi
- [x] «Faqat kuchdagilar» almashtirgichi, belgini yopish (izoh bilan)
- [x] «Qobiliyatli» (`Capable`, 91.12) sahifasi —
      `web/src/app/routes/_app/qobiliyatli.tsx` (`FlagListPage type="capable"`)
      + yon menyuda bandi + `nav.capable` tarjimasi
- [x] **Bo'shatishdagi blok qoidasi ishga tushdi** — tekshirildi: `409` +
      `xodim.qora_royxatda`, tasdiqlangandan keyin o'tadi
- [x] Belgi qo'yish xodim kartasidan (qora ro'yxat uchun sabab majburiy)
- [x] Qayta ishga olishda avtomatik tekshiruv — `EmployeeFlagGuard`
      (`Employees/Common/EmployeeFlagGuard.cs`) orqali `HireEmployeeCommand`
      ichida to'liq ishlaydi: qora ro'yxatda bo'lsa `409` bilan bloklaydi,
      nazoratda bo'lsa ogohlantirish bilan o'tkazadi. `HireEmployeeTests.cs`
      da testlangan

> **Belgi qo'yish ro'yxat sahifasida ATAYLAB yo'q.** Belgi xodimga
> qo'yiladi, ro'yxatga emas — shu sababli u xodim profilidan boshlanadi.
> Ro'yxat sahifasida ko'rish va yopish qoladi.
>
> **Yopish — o'chirish emas.** Yozuv joyida qoladi, unga tugash sanasi
> qo'yiladi: «bu odam qachondir qora ro'yxatda bo'lganmi?» degan savolga
> javob kerak bo'ladi, o'chirilgan yozuv esa uni javobsiz qoldirardi.

### S8 — Profil boyitish (M7 + M8 + M9) ⏱ 2 kun
- [x] Qobiliyatlar: til, kompyuter (0–100%), ta'lim, mutaxassislik, qiziqish —
      `EmployeeCompetency` agregati (`employee_competencies` jadvali).
      Bitta xodimga bitta profil — tarix emas, upsert. **Tuzatish:**
      «91.15 Қобилятлар жадвали» varag'ida rejada yozilgan beshtadan tashqari
      yana bitta ustun bor ekan — «Бошка қобилиятлар». U ham
      `OtherAbilities` sifatida qo'shildi, jami olti maydon
- [x] Rag'bat va jarima: tur, sana, summa, sovg'a, sabab, buyruq —
      `Incentive` agregati (rag'bat va jarima **bitta** agregatda,
      `IncentiveKind` bilan — `EmployeeFlag`ning to'rt turi kabi naqsh).
      Jadval: `incentives`. Yangi ruxsatlar: `incentives.view`,
      `incentives.manage`
- [x] Hujjatlar: tur bo'yicha guruh, ko'rish/yuklab olish —
      `EmployeeDocument` agregati (`employee_documents`). MinIO YO'Q (pastdagi
      izoh): mavjud `IFileStorage` + `LocalFileStorage` ishlatiladi. Fayl turi
      MAZMUN bo'yicha aniqlanadi (`IFileContentInspector`), chegara 20 MB,
      ruxsat berilgani PDF va rasm (JPEG/PNG/WebP)
- [ ] Baholash anketasi — **to'g'ridan-to'g'ri tizimda to'ldiriladi** (chop etish yo'q).
      **S8 dan qolgan yagona band** — hali nol holatda

> **`HasAmount` endi tirik qoida.** `AwardType.HasAmount` / `PenaltyType.HasAmount`
> maydonlari S2 dan beri mavjud edi, lekin hech qayerda ishlatilmasdi. Endi ular
> haqiqiy cheklov: pullik tur tanlansa summa majburiy, pulsiz turda esa summa
> taqiqlanadi.
>
> **Tur va xodim o'zgarmas.** Rag'bat/jarima yozuvida tahrirlashda tur, sana,
> summa, sabab, buyruq o'zgartirilishi mumkin, lekin `Kind` va `EmployeeId`
> — yo'q: «rag'batni jarimaga aylantirish» tuzatish emas, boshqa faktni
> yozish bo'lardi va tarixni soxtalashtirardi. Xato yozuv o'chiriladi va
> qaytadan kiritiladi.
>
> **Jarima uchun sabab majburiy.** Mehnat kodeksi bo'yicha intizomiy chora
> hujjatlashtirilgan sabab talab qiladi — bu `EmployeeFlag.Blacklist`
> dagi qoidaning aynan o'zi.
>
> **MinIO qo'shilmaydi.** Hujjatlar bandida dastlab «MinIO'ga yuklash»
> ko'zda tutilgan edi. Qaror: kerak emas — rasm saqlash allaqachon
> `IFileStorage` porti + `LocalFileStorage` orqali ishlaydi; MinIO yangi
> konteyner, yangi port va yangi hisob ma'lumotlari degani, bu mashinada esa
> portlar band (8+ boshqa compose loyihasi bor). Port abstraksiyasi mavjud
> bo'lgani uchun kerak bo'lsa keyinchalik almashtirish bitta sinf bilan bo'ladi.

### S9 — Qayta aloqa va xavfsizlik (M10 + M11) ⏱ 1 kun ✅ **BAJARILDI**
- [x] Exit interview: **ichki forma** — `ExitInterview` agregati mehnat
      munosabatiga bog'lanadi, `/qayta-aloqa` sahifasi, alohida ruxsat
      (`feedback.view`/`feedback.manage`)
- [ ] Exit interview'ning **ommaviy havolasi** — ATAYLAB qoldirildi.
      Autentifikatsiyasiz endpoint alohida xavfsizlik yuzasi: havola tokeni,
      amal qilish muddati, bekor qilish, tezlik cheklovi va spam himoyasi
      kerak. Ichki forma HR qo'lida sinovdan o'tgach, alohida qo'shiladi
- [x] Baxtsiz hodisalar jurnali (13 maydon, komissiya xulosasi) —
      `Incident` agregati, `/hodisalar` sahifasi, `safety.*` ruxsatlari.
      Komissiya xulosasi ALOHIDA amal (`…/conclude`), tahrirlash emas:
      hodisa darhol qayd etiladi, xulosa esa keyin beriladi

### S10 — Dashboard va hisobotlar (M12) ⏱ 2 kun
- [x] Bosh sahifa: jami xodim, sinovdagilar, oylik kelgan/ketgan, bugungi
      tug'ilgan kunlar — `GET /api/dashboard/stats` +
      `web/src/app/routes/_app/index.tsx`
- [x] Bo'lim/yo'nalish kesimi — **backend**: `GET /api/dashboard/breakdown`.
      (Ilgari `DashboardCatalogSizesDto` faqat ma'lumotnoma yozuvlari SONINI
      qaytarardi — bu xodimlar taqsimoti emas edi)
- [x] Kadrlar oqimi va ketish sabablari taqsimoti — **backend**:
      `GET /api/reports/turnover` (oylik qatorlar + sabablar). Shtat ODAM
      bo'yicha sanaladi, bosh sahifa bilan bir xil qoida
- [x] **Excel eksport** — **backend**: `GET /api/reports/employees/export`.
      Ekrandagi ro'yxatning AYNAN o'zi: filtr qurish va DTO ga aylantirish
      `SearchEmployeesQueryHandler` dan qayta ishlatiladi, oklad ustuni ham
      shu yerda `salary.view` ga bog'liq
- [x] **Uchalasining EKRANI ham qurildi.** Bo'lim/yo'nalish kesimi —
      bosh sahifada karta (diagramma emas, «bar list»; yo'nalishda foiz
      ATAYLAB yo'q — bitta xodim bir nechta yo'nalishga tegishli bo'la
      oladi, ya'ni yig'indi xodimlar sonidan oshadi). Kadrlar oqimi va
      ketish sabablari — yangi `/hisobotlar` sahifasi (`reports.view`),
      6/12/24 oylik oyna, `recharts` nihoyat ishga tushdi. Excel eksport —
      xodimlar toolbar'ida tugma (`reports.export`), ekrandagi filtr va
      saralashning aynan o'zi bilan
- [ ] Bo'lim boshliqlari uchun cheklangan ko'rinish — avtorizatsiya
      arxitekturasiga o'zgarish talab qiladi (pastdagi «Xavflar» ga qarang)

### S11 — Ishga tushirish ⏱ 1 kun
- [x] Production Docker build (multi-stage), Caddy + HTTPS —
      `docker-compose.prod.yml` + `web/Caddyfile` (S0/S1 da qurilgan,
      haqiqiy serverda hali sinalmagan)
- [x] `pg_dump` avtomatik zaxira (kunlik, 30 kun saqlash) — prod'da `backup`
      konteyneri (`crond`, 22:00 UTC = 03:00 Toshkent). **Baza va fayllar
      BIRGA olinadi**: `photo_key` diskdagi faylga ishora qiladi, alohida
      vaqtda olingan zaxiralar bir-biriga mos kelmasdi. `./bonvi backup`,
      `./bonvi backup-list`, `./bonvi restore`
- [ ] Sentry — hali yo'q. `/health/live` + `/health/ready` allaqachon bor.
      ~~Hangfire dashboard~~ — Hangfire S6 da rejadan olib tashlangan
- [ ] HR uchun qo'llanma + 1 kunlik o'qitish

**1-bosqich jami: ~20–23 ish kuni.**

### Xavflar

Ikkita audit (backend va frontend) uchta jiddiy xavfni topdi. Bular funksiya
yetishmasligi emas — e'tiborsiz qolsa muammoga aylanadigan joylar. Shu
sababli alohida bo'lim: har biri bir nechta S-qadamga (S10, S11) tegishli,
bitta bandga sig'dirib bo'lmaydi.

1. ✅ **`pg_dump` zaxirasi yo'q edi — HAL QILINDI.** Prod'da `backup`
   konteyneri kunlik zaxira oladi (baza + fayllar, bitta stamp bilan),
   30 kundan eskisi o'zi o'chadi. Qolgan ish — zaxirani HAQIQIY serverda
   bir marta tiklab ko'rish: sinalmagan zaxira zaxira emas.
2. **Bo'lim darajasidagi ruxsat tushunchasi yo'q.** Hozirgi avtorizatsiya
   modeli faqat ikkita global rol (5.1-bo'lim) — barcha ruxsatlar butun
   tizim bo'yicha, bitta bo'limga cheklab bo'lmaydi. S10 dagi «bo'lim
   boshliqlari uchun cheklangan ko'rinish» shu sababli oddiy ekran emas: u
   avtorizatsiya arxitekturasiga (rol → bo'lim bog'lanishi) o'zgarish talab
   qiladi. Buni erta bilish muhim — S10 ga yetganda qayta loyihalash
   kutilmagan bo'lib chiqmasin.
3. ✅ **Excel eksport uchun yarim tayyorgarlik — HAL QILINDI.**
   `RateLimitPolicies.Export` siyosati va `reports.export` ruxsati endi
   haqiqiy endpoint'ga (`GET /api/reports/employees/export`) ulandi.
4. ✅ **`recharts` ishlatilmayotgan edi — HAL QILINDI.** U endi
   `/hisobotlar` sahifasidagi kadrlar oqimi grafigida ishlaydi. Ranglar
   CSS o'zgaruvchisi orqali beriladi, ya'ni mavzu almashganda grafik ham
   almashadi (`getComputedStyle` nega rad etilganini kod izohi tushuntiradi).
5. **Ma'lumot hali ko'chirilmagan.** Bu dasturchi xavfi emas, lekin eng
   kattasi: `./bonvi etl --apply` hali ishga tushirilmagan va bazada atigi
   59 xodim bor. Butun tizim haqiqiy hajmda (2347 xodim, 3527 hodisa)
   sinalmagan — sahifalash, qidiruv tezligi va eksport hajmi shu paytgacha
   kichik ma'lumotda tekshirilgan.

---

## 7. Migratsiya strategiyasi (parallel ishlash)

Excel'ni birdan tashlab bo'lmaydi — HR har kuni ishlaydi.

1. **1–3 hafta:** tizim quriladi, Excel asosiy manba bo'lib qolaveradi.
2. **4-hafta:** ETL bilan yakuniy yuklash. HR **ikkala joyda** 1 hafta ishlaydi (parallel run).
3. **Solishtirish:** haftalik `diff` hisoboti — Excel va tizim orasidagi farq.
4. **Farq 0 bo'lgach:** Excel `read-only` ga o'tkaziladi, arxiv sifatida saqlanadi.

---

## 8. 2 va 3-bosqichga tayyorgarlik (hozir qilinadigan kichik ishlar)

| Tayyorgarlik | Nega |
|---|---|
| `Employee.Source` (`Manual` / `Jobster` / `TelegramBot`) | 2-bosqichda nomzod avtomatik keladi |
| `EmploymentEvent.CandidateId` (nullable FK) | Rekruting → xodim konversiyasi |
| `Employment.ExternalRef` (Verifix ID) | 3-bosqichda davomat bog'lanishi |
| Hangfire + webhook endpoint infratuzilmasi | Integratsiya kanali tayyor turadi |
| Telegram bot servisi (skeleton) | 2-bosqich anketa boti shu ustiga quriladi |

**2-bosqich (Jobster o'rniga)** — voronka: `Anketa → Telefon suhbat → Suhbat 1 → Kengash → Rahbar suhbati → Job Offer → Ishga qabul`. Bu 1-bosqichning `EmploymentEvent.Hire` ga ulanadi.
**3-bosqich (Verifix o'rniga)** — Face ID qurilma → `AttendanceLog` → smena qoidalari → keldi-ketdi hisoboti. HR aytdi: Verifix'ning **10–15%** ini ishlatishadi, oyiga katta pul ketadi. Kunlik 10 martagacha kirish-chiqish bo'ladi — smena mantiqi shu yerda hal qilinadi.

---

## 9. Aniqlanishi kerak bo'lgan savollar (HR bilan)

Bular ishni to'xtatmaydi — oqilona standart bilan boshlanadi, javob kelganda to'g'rilanadi.

1. **Yo'nalish ↔ Bo'lim ↔ Lavozim** ierarxiyasi qanday? — *Standart: mustaqil, xodimda uchalasi ko'rsatiladi.*
2. **Bo'lim daraxti** necha bosqichli? (`7-Департамент → Метан завод → Плазма бўлими`?) — *Standart: cheksiz daraxt.*
3. ~~**Kim tizimga kiradi?**~~ ✅ **Javob olindi (22.08.2026):** xodimlar bu tizimdan
   foydalanmaydi va ularga login-parol berilmaydi. Tizim faqat **xodimlarni boshqarish**
   uchun: undan **3 ta HR menejeri va asosiy rahbar** foydalanadi.
   Shu sababli rollar atigi ikkita (5.1-bo'lim).
4. **Oklad** kimga ko'rinadi? (Excelda ochiq — tizimda alohida ruxsat qilinsinmi?)
5. **Sinov muddati** hamma uchun 3 oymi, yoki lavozimga qarab farqmi?
6. **Jarima** tizimi qanday ishlaydi? (Excelda yo'q, lekin so'ralgan — turlari va summasi kerak.)
7. ~~**Xodim rasmlari hozir qayerda?**~~ ✅ **Javob olindi (22.08.2026):**
   rasmlar **Google Disk'da**. Ommaviy ko'chirish **kerak emas** va Google Drive
   integratsiyasi yozilmaydi — tizimda rasm yuklash imkoniyati bo'lsa yetarli,
   HR ularni o'zi qo'lda yuklaydi.
8. `Текширув`, `Судланганлик`, `MEHNAT.UZ` ustunlari qanday to'ldiriladi, majburiymi?

---

## 10. Keyingi qadam

**S0 · S1 · S2 tugadi, S3 vositasi tayyor** (HR ko'rigini kutmoqda).
**S4, S5, S6, S7, S9 va S10 ning hisobot qismi to'liq bajarildi** —
backend ham, ekran ham. S8 dan bitta band qoldi (baholash anketasi),
S10 dan bo'lim boshliqlari ko'rinishi, S11 esa zaxiradan tashqari ochiq.

Qurilgan poydevor:

```
./bonvi up      →  postgres 18.6 · redis 8.8 · API :7080 · Web :7173
./bonvi test    →  710 test / 0 xato / ~15 s
./bonvi check   →  4 til + kod ishlatgan kalitlar + backend xato kodlari
./bonvi gen     →  C# DTO → TypeScript tip + React Query hook
./bonvi etl     →  Excel → baza ko'chirish (quruq yurish standart)
./bonvi backup  →  baza + fayllar zaxirasi (prod'da kunlik, avtomatik)
```

Ilova qobig'i ekran balandligida QOTIB turadi (`h-dvh overflow-hidden`) —
yon menyu va yuqori panel hech qachon surilmaydi, suriladigan yagona joy
kontent maydoni. Ilgari bu `position: sticky` bilan qilingan edi va u
ota elementdagi `overflow`/`transform` tufayli jimgina buzilardi.

Ishlaydi: kirish (JWT), token yangilash, ruxsat tekshiruvi (21 ta ruxsat,
2 ta rol), foydalanuvchi va rollarni boshqarish, 4 tilli interfeys,
yorug'/tungi rejim, xavfsizlik sarlavhalari, tezlik cheklovi, 8 ta
ma'lumotnoma (62 endpoint), bo'lim daraxti, lavozim vazifalari, xodim
kartasi (oltita tab), mehnat munosabati va hodisalar (`/buyruqlar`), sinov
muddati, to'rtta teg ro'yxati, qobiliyatlar profili, rag'bat/jarima,
hujjatlar, exit interview, baxtsiz hodisalar jurnali va kunlik avtomatik
zaxira.

**Ko'chirish vositasi tayyor va quruq yurishdan o'tdi**: 2347 xodim,
2538 mehnat munosabati, 3527 hodisa, 1334 sinov muddati, 32 belgi. Bu
hali bazaga qo'llanmagan — joriy dev muhitida atigi 59 xodim yozuvi bor.

### Navbatdagi ish — tartib bo'yicha

1. **ETL 6-qadam — profil boyitish.** `91.15` qobiliyatlar (554 qator) va
   `91.14` rag'bat (34 qator) hali ko'chirilmaydi. Endi buni yozish mumkin:
   `EmployeeCompetency` va `Incentive` agregatlari S8 da qurildi, `Steps/`
   da esa mos qadam hali yo'q.
2. **S8 — baholash anketasi.** S8 dan qolgan yagona band.
3. **S11** — Sentry, HR uchun qo'llanma va o'qitish, hamda zaxirani
   haqiqiy serverda bir marta TIKLAB ko'rish.
4. **Bo'lim boshliqlari uchun cheklangan ko'rinish** (S10) — bu oddiy ekran
   emas: hozirgi avtorizatsiya modelida faqat ikkita global rol bor va
   ruxsatni bitta bo'limga cheklab bo'lmaydi. «Xavflar» dagi 2-bandga
   qarang, ish rol → bo'lim bog'lanishidan boshlanadi.

> **Alohida, parallel oqim — ETL ma'lumotini ko'chirish.** Yuqoridagi
> ro'yxat dasturchi ishi; real ma'lumotni ko'chirish esa HR ga bog'liq va
> bulardan mustaqil ketadi: avval hisobot ko'rib chiqiladi
> (`data/etl-report.csv`, 4739 satr — eng muhimi: 1540 xodimda JSHSHIR
> yo'q, 99 odamda otasining ismi bog'lanmagan, `unmapped`dagi 109 qiymat —
> S2 bo'limidagi «Tozalash natijasi»ga qarang), shundan keyin
> `./bonvi etl --apply` ishga tushadi.

> **Texnik qarz — qo'lda yozilgan API klientlari.**
> `web/src/features/*/api/*-api.ts` fayllari hamon qo'lda yozilgan, holbuki
> OpenAPI to'liq va `./bonvi gen` ishlaydi. Bu shunchaki uslub masalasi
> emas: ilgari aynan shu qo'lda yozish tufayli `flags`/`activeFlags`
> nomlari chalkashib, ro'yxatdagi nishonlar HECH QACHON chizilmagan edi.
> Endi ko'chirilishi kerak bo'lgan ekranlar soni o'ndan oshdi — qanchalik
> kechiktirilsa, shunchalik qimmatlashadi.

9-bo'limdagi **1, 2 va 4-savolga** javob olsak, ma'lumot sxemasi bir marta
to'g'ri quriladi.
