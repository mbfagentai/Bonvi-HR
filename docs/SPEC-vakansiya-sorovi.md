# SPEC — Vacancy requests from department heads

Owner request (2026-10-30): a department head sends a request to open a new vacancy; HR keeps the list,
reviews each request and opens the vacancy.

Owner decisions (2026-10-30):
- ~~Employee app «Jamoa» tab~~ — owner changed it the same day: «verifix qismidagi o'zgarishlarni
  qilmay qo'yaver, menu yaratsang yetadi». Only an HR panel menu; the employee app is not touched.
  The request is entered in that menu (department + requester + 3–4 fields).
- HR reviews and opens the vacancy.
- On approval a «draft» vacancy: HR gets the open-vacancy form pre-filled from the request, edits it and
  saves. Nothing goes live until HR saves (see ASSUMPTIONS 2026-10-30).
- No salary in the request.

## 1. Model — `VacancyRequest` (Recruiting)

| Field | Type | Rule |
|---|---|---|
| `DepartmentId` | Guid | existing department |
| `RequestedByEmployeeId` | Guid | who asked; defaults to the department's head |
| `Title` | string ≤ 200 | required — position / vacancy name as the head calls it |
| `Openings` | int 1..`Vacancy.MaxOpenings` | required, default 1 |
| `NeededBy` | DateOnly? | optional — «from when» |
| `Note` | string ≤ 2000 | optional — reason and requirements |
| `Status` | Pending=1, Approved=2, Rejected=3, Cancelled=4 | |
| `DecidedAt`, `DecidedBy`, `DecisionNote` | | set by HR; reject reason required |
| `VacancyId` | Guid? | the vacancy HR opened on approval |
| audit stamps + soft delete | | like every aggregate |

Transitions: only `Pending` changes: cancel, approve (with a vacancy) or reject (reason).
No edit — a wrong request is cancelled and entered again.

## 2. API

HR panel:
- `GET  /api/recruiting/vacancy-requests` — `status`, `from`/`to` (created), `search` (title, department,
  head), paging; `RecruitingView`.
- `GET  /api/recruiting/vacancy-requests/export` — same filter; `RecruitingView` + `ReportsExport`.
- `POST /api/recruiting/vacancy-requests` — `{departmentId, requestedByEmployeeId?, title, openings, neededBy?, note?}`;
  `RecruitingView` (whoever sees the menu may record a request).
- `POST /api/recruiting/vacancy-requests/{id}/cancel` — `RecruitingManage`.
- `POST /api/recruiting/vacancy-requests/{id}/approve` — `{title, openings, openedAt?, note?}` opens the
  vacancy (same rules as `POST /api/recruiting/vacancies`) and links it, in one save; `RecruitingManage`.
- `POST /api/recruiting/vacancy-requests/{id}/reject` — `{reason}` required; `RecruitingManage`.

## 3. Notifications

`NotificationType.VacancyRequested` (RecruitingView): one per pending request, collected like
`CandidateReapplied`. Disappears once decided.

## 4. UI

- HR web: Recruiting → «Vakansiya so'rovlari» (`/vakansiya-sorovlari`): status tabs (Yangi default),
  date filter, search, export, paging. Row → dialog with details and two actions: «Vakansiya ochish»
  (pre-filled vacancy form) and «Rad etish» (reason).
- «+ Yangi so'rov» in the same page: department, requester (pre-set to the department head), position,
  count, needed-by, note.

## 5. Heads send requests themselves (owner, 2026-10-31)

The owner reversed the «HR menu only» decision: heads send from the employee app «Jamoa» tab and from
their bot. Both surfaces share `TeamVacancyRequests` (scope check, requester, list, cancel).

| Surface | Endpoints | Scope | Requester |
|---|---|---|---|
| Employee app «Jamoa» | `GET/POST /api/me/team/vacancy-requests`, `POST …/{id}/cancel` | the head's departments (`TeamAccess`) | the head |
| Head's bot | `GET/POST /api/public/v1/team/vacancy-requests`, `POST …/{id}/cancel` | the key's department subtree | department head, else nearest ancestor head in the subtree; none → `tashqi.boshliq_yoq` (400) |

- `departmentId` may be omitted when the scope has one root.
- Out-of-scope department → `jamoa.bolim_topilmadi` / `tashqi.bolim_topilmadi` (404); out-of-scope
  request → `vakansiya_sorovi.topilmadi` (404) — same answer as «does not exist».
- The HR flow (§2–§4) is unchanged; the `VacancyRequested` notification fires for these too.
- App: a «Vakansiya so'rovlari» card on the «Jamoa» screen (status, HR reason, candidates/hired) and the
  form `/jamoa/vakansiya/yangi`; tapping a pending row withdraws it.
