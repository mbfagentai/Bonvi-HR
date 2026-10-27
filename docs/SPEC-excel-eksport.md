# SPEC — Excel export for every HR list

> Status: authoritative. Written 2026-10-22. Owner's words: «bu menularni hammasida tanlangan filter
> ta'sir qilgan holatda excelga yuklash mumkin bo'lsin … deyarli har bir qismlarga Excelga Export qilish
> tugmasini qo'shasan, bu tizimda hisobot uchun kerakli ma'lumotlarni chiqarib olish uchun».

## 1. Rule (same for every list)

An export button on a list downloads **every row that matches the filters and sort currently on screen**,
in the same order, without paging. These existing exports already work this way and are the reference:
`GET /api/reports/employees/export` (`ExportEmployeesQueryHandler`) and `GET /api/incentives/export`
(`ExportIncentivesQueryHandler`).

| Rule | Value |
|---|---|
| Endpoint | `GET <list endpoint>/export`. Query parameters are exactly those of the list endpoint, without `page` and `pageSize`. |
| Filter | Reuse the list handler's own filter builder or read model call. Never write a second copy of the filter logic: every filter added to the list later must reach the export automatically. |
| Row limit | `ExcelExport.MaxRows` (5000). Fetch `MaxRows + 1`; if `Total > MaxRows`, return `ExcelExport.TooMany(total)` (400 `eksport.juda_kop`). Never truncate silently. |
| Permission | The list's own view permission **and** `reports.export` (`.RequireAuthorization(view, Permissions.ReportsExport)`). |
| Extra visibility rules | The list's extra rules apply unchanged. Examples: audit security entries, salary under `salary.view`, phone-only/masked fields. The file never shows more than the screen shows to the same user. |
| Rate limit | `.RequireRateLimiting(RateLimitPolicies.Export)`. |
| Audit | `AuditAction.Export` with the filters (the search text itself is NOT stored, only `HasSearch: true`) and the row count. This is the pattern of `ExportIncentivesQueryHandler`. |
| Writer | `IExcelTableWriter` (`Bonvi.Application.Reports.Export`). Columns are declared in the use case as `ExcelColumn<TRow>(Header, Value, Kind)`. |
| Cell kinds | Dates are `DateOnly`. Timestamps are `DateTimeOffset`, written in Tashkent time. Counts are numbers. Free text is `LongText`. `bool` is written as «Ha»/«Yo'q». |
| File name | `ExcelExport.FileName(slug, today)` → `Bonvi_<slug>_<yyyy-MM-dd>.xlsx`. |
| Response | `Results.File(content, xlsx mime, fileName)`. Document it with `.Produces(200, xlsx)`, `.ProducesProblem(400/403/429)`, `.WithName("Export…ToExcel")` and an Uzbek summary/description in the same style as the incentives export. |
| Headers | Uzbek Latin. Each header is the same text as the column header on screen (uz-Latn locale). |

**Columns** are the columns of the web table, in the same order, plus the extras listed in §2. The extras
must be fields the list DTO or read-model row **already** carries. Do not add joins or new queries for the
file. If a listed extra is not on the row, skip it and say so in the report.

## 2. Lists in scope

| # | Screen | Endpoint | View permission | Slug | Columns: on-screen ones + extras |
|---|---|---|---|---|---|
| 1 | Nomzodlar, Rad etilganlar, and the «Nomzodlar» tab of Rezerv / Qora ro'yxat | `GET /api/recruiting/candidates/export` | `recruiting.view` | `nomzodlar` | F.I.Sh., Telefon, Vakansiya, Bosqich, Holati, Manba, Ariza sanasi, Keyingi aloqa, Kiritilgan. When the row has them: Yopilgan sana, Sabab |
| 2 | Vakansiyalar | `GET /api/recruiting/vacancies/export` | `recruiting.view` | `vakansiyalar` | on-screen columns |
| 3 | Sinov muddati | `GET /api/probations/export` | `employment.view` | `sinov_muddati` | F.I.Sh., Lavozimi, Bo'limi, Boshlangan, Tugaydi, Qolgan kun (number; negative = overdue), Holati, Natija |
| 4 | Buyruqlar | `GET /api/employments/events/export` | `employment.view` | `buyruqlar` | Sana, Turi, F.I.Sh., Lavozimi, Bo'limi, Tafsilot (old → new, one line), Izoh. Salary only if the row carries it for this user |
| 5 | Rezerv / Qora ro'yxat «Xodimlar» tab, Nazoratdagilar, Qobiliyatli | `GET /api/employees/flagged/export` | `lists.view` | `belgilar` (slug from the flag type if the row has it) | F.I.Sh., Lavozimi, Bo'limi, Belgi, Belgi qo'yilgan sana, Sabab, Holati (faol/yopilgan) |
| 6 | So'rovlar → Yo'qlik | `GET /api/attendance/requests/export` | `attendance.view` | `yoqlik_sorovlari` | Xodim, Bo'limi, Yo'qlik turi, Sanadan, Sanagacha, Vaqt (or «Butun kun»), Izoh, Holat, Qaror izohi, Qaror qilgan, Qaror sanasi, Ilovadan (Ha/Yo'q) |
| 7 | So'rovlar → Jadval o'zgarishi | `GET /api/attendance/schedule-requests/export` | `attendance.view` | `jadval_sorovlari` | Xodim, Bo'limi, Kunlar, Ish vaqti, Tanaffus, Izoh, Holat, Qaror izohi, Qaror qilgan, Qaror sanasi |
| 8 | Davomat → Statistika (employee table) | `GET /api/attendance/dashboard/export` | `attendance.view` | `davomat_statistika` | F.I.Sh., Lavozim, Bo'lim, Kelgan, Ketgan, Holat, Kechikish (daq). Same date, org filters, status chip and search as the screen |
| 9 | Audit jurnali | `GET /api/audit/export` | `audit.view` | `audit` | Vaqt, Kim, Roli, Amal, Nima, Yozuv turi, O'zgarishlar (one line), IP if on the row. The security kind follows the list's own permission rule |
| 10 | Qayta aloqa | `GET /api/exit-interviews/export` | `feedback.view` | `qayta_aloqa` | F.I.Sh., Bo'limi, Lavozimi, Ketgan sana, Suhbat sanasi, Tavsiya qiladimi, Ketish sababi |
| 11 | Baxtsiz hodisalar | `GET /api/safety/incidents/export` | `safety.view` | `baxtsiz_hodisalar` | on-screen columns + description/conclusion if on the row |

These already have exports and do not change: Xodimlar / Ishdan ketganlar, Rag'bat va jarima, Tabel,
O'tishlar, Davomat hisobotlari.

These are **out of scope** because they are configuration, not reporting data. Add them only if the owner asks:
Foydalanuvchilar, Rollar, Sozlamalar, Ma'lumotnomalar, Qurilmalar, Moslashtirish, Ish jadvallari, Joylashuvlar.

## 3. Web

- Shared pieces:
  - `features/reports/api/list-export.ts`: `useListExport()` / `downloadListExport({ path, params, slug })`.
  - `features/reports/components/list-export.tsx`: `ListExportButton`, `ListExportError`.
- Placement: the top filter row of the list card, next to «Filtrlar» / «Filtrlarni tozalash». This is the same place as on Xodimlar.
- Visibility: shown only with the list's view permission **and** `reports.export`.
- Params: the list's own query params (filters + sort + date range), built by the same function the list
  query uses, without `page`/`pageSize`.
- Disabled when the date range is inverted (the list already refuses to query then).
- Error: `ListExportError` inside the card, under the toolbar.

## 4. Done when

- Every endpoint in §2 has integration tests in the style of `IncentiveExportApiTests`:
  - 401 without a token;
  - 403 with only one of the two permissions;
  - 200 with an xlsx body whose rows equal the list endpoint's rows (count and order) for a narrowed filter;
  - an `Export` audit row is written.
- `./bonvi test` is green (run per project if the Docker VM runs out of memory).
- Web: typecheck, `vite build` and `./bonvi check` pass. Every list in §2 shows the button; clicking it
  downloads a file that opens with the same rows as the screen.
