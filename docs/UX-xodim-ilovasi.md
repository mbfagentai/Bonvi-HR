# UX — Employee app (PWA): structure, patterns and design language

> Status: authoritative for the interface of the employee PWA. Written 2026-09-22.
> Branch: `verification`. Companion to `SPEC-xodim-ilovasi.md`, which stays
> authoritative on **behaviour** (trust model, API shape, validation order).
> Where this document and the SPEC disagree about behaviour, the SPEC wins.
>
> **Language rule.** This document is English. Every UI string it proposes is
> written in **uz-Latn** and is a *proposal for the key's default value only* —
> nothing is hard-coded. All strings go through i18n in **four locales**:
> `uz-Latn`, `uz-Cyrl`, `ru`, `en`, exactly like the HR app
> (`web/src/shared/i18n/locales/`). Punctuation follows the HR locale files
> exactly, and the two marks are different characters: `o‘` and `g‘` use
> **U+2018** (`Qo‘shish`, `Yo‘qlik`), while a glottal stop after a vowel uses
> **U+2019** (`ma’lumot`, `noma’lum`). ASCII `'` appears nowhere in a UI
> string. The HR app's `web/scripts/check-locales.mjs` already enforces key
> parity across the four locales and that every backend error code has a
> translation; this app needs its own copy of that check — the four codes in
> §C.5 are exactly the kind it exists to catch — and the apostrophe rule is
> worth adding to it.
>
> **Scope rule.** Screens exist only where we have data. Where a Verifix screen
> has no data behind it in our system, this document says so and drops it.
> Nothing here is a placeholder for a future module.

---

## 0. Two constraints that shape everything below

### 0.1 Separate build, zero shared imports

The app is a **separate Vite build in its own directory**, served under
`/ilova/`. It does not import a single file from the HR app's `src/`: not a
component, not a hook, not a token file, not `format.ts`.

Anything that looks the same as the HR app is the same **by value, copied once
into this app's own files** — a token hex, a label string, a status meaning.
The reason is not purity, it is blast radius: the HR web app is in production
and in daily use by HR and the director; a phone app for ~950 employees must
not be able to break it, not through a shared component, not through a shared
CSS file, not through a shared build.

> ⚠️ **Conflict with the SPEC, declared openly.** `SPEC-xodim-ilovasi.md` §G
> says "same Vite app, separate route tree", and rejects a second build. The
> owner's later instruction for this wave is a separate build in its own
> directory. This document follows the later instruction. What does **not**
> change from §G: the public path stays `/ilova/*`, the service worker scope
> stays `/ilova/`, the HR routes stay on the worker's deny-list, and the
> generated API types still come from one `./bonvi gen`. The architect should
> confirm the build split before `build-frontend` starts; everything else in
> this document is unaffected either way.

### 0.2 An employee sees only their own data

Every screen here reads from `/api/me/*` with the `bonvi-employee` audience.
There is no employee list, no colleague, no department view, no search across
people. The only person in this app is the person holding the phone.

Consequences that are easy to forget and must not be:
* No screen takes an employee id in its route. If a route needs an id, it is
  the id of *a record of mine* (a request), never of a person.
* Nothing in the UI hints that other people's data exists — no "my team", no
  comparison, no ranking.
* **No camera anywhere, in any form.** No selfie, no photo attachment, no QR,
  no `<input type="file" capture>`. The app must never construct a
  `getUserMedia` call. This is the owner's decision (SPEC §A3), and a stray
  camera permission prompt would break the promise the app is sold on.
* **No push notifications in this wave.** Therefore no bell icon, no badge, no
  notification settings section. A dead bell that never rings is worse than no
  bell — Verifix's home screen carries an `11` badge that leads to a list the
  employee cannot act on.

---

## A. Screen inventory

### A.1 What Verifix has, and what we do with it

> **AMENDED 2026-10-20** → `SPEC-verifikatsiya-erkin-jadval.md` §8: the request-type sheet now lists all 8 Verifix types in Verifix order; rows 4–8 are visible but disabled («Tez orada») until their forms are specified.

Verifix's employee app is the structural reference. Its shape:

* a **bottom tab bar** with four tabs: `Asosiy · Kalendar · Yangiliklar · Profil`;
* a **home screen** that is a vertical stack of cards: schedule, today's marks
  (`Qaydnoma`), a 4-tile `Modullar` grid, a requests card;
* a **calendar screen**: month grid of coloured day tiles → selected-day detail
  below (in/out, schedule, marks, requests, monthly donut);
* a **profile screen**: avatar card → pushed detail, a list-group of rows with
  chevrons, a pushed settings screen with grouped sections, a danger sign-out
  row, the app version at the bottom;
* **bottom sheets** for choosing, and **pushed full-screen forms** with a single
  primary button pinned at the bottom.

We keep all of that. The table below is the disposal of every Verifix surface.

| Verifix surface | Our decision | Why |
|---|---|---|
| Tab: `Asosiy` | **Keep** as `Asosiy` | Still the landing screen, but it gains the app's one real job (check-in) which Verifix does not have — Verifix's home is read-only because its marks come from a terminal. |
| Tab: `Kalendar` | **Keep**, renamed `Taqvim` | `/api/me/attendance?year&month` is exactly this screen. |
| Tab: `Yangiliklar` | **Drop** | Owner's decision, 2026-09-22: no news feature, no data, no placeholder. |
| Tab: `Profil` | **Keep** | `/api/me` + settings + sign out. |
| Home tile: `Qatnashish` | **Folded into `Taqvim`** | It is the same attendance data; a tile that opens a second attendance screen would give the employee two places to look for one answer. |
| Home tile: `So‘rovlar` | **Promoted to a tab** | See A.2. |
| Home tile: `To‘lov` | **Drop** | No payroll data exists in BonviHR. |
| Home tile: `Hisobotlar` | **Drop** | No employee-facing reports exist, and HR's reports are other people's data. |
| The `Modullar` grid itself | **Drop** | A grid exists to fan out to modules. With payroll, reports and news gone, it would hold one tile that duplicates a tab. The grid is a vestige of a bigger app. |
| `Qaydnoma` card (today's marks) | **Keep**, renamed `O‘tishlar` | Signature element of the reference app. See the data gap in A.4. |
| Bell + `11` badge | **Drop** | No push in this wave (§0.2). |
| Bottom sheet "So‘rov turini tanlang" (8 request categories) | **Keep the sheet as a component, drop it as a screen** | Verifix's eight entries are eight *modules*. We have exactly one: an absence request. Its kind (`Ishxona ishi bilan`, `Kasallik`, …) comes from the `absence_types` catalog and is a **field inside the form**, opened with the same bottom sheet. Mirroring the screen would mean inventing seven request kinds. |
| Pushed `Profil → Ma’lumotlar` detail (birthday, gender, manager, contacts, employment, account numbers) | **Drop in this wave** | `/api/me` returns name, photo, department and the mobile flag — nothing else. See the data note in A.5. |
| `Sozlamalar → PIN-kod`, `Touch-ID` | **Drop** | A PWA cannot offer a reliable OS-level biometric unlock on both platforms; a half-working security control is worse than none. |
| `Sozlamalar → Bildirishnomalar` | **Drop** | No push (§0.2). Not shown disabled — shown not at all. |
| `Profil → Mening kuzatuvlarim`, `Yordam`, `Joriy filial` | **Drop** | No data, no support desk in the product, no branch concept on `/api/me`. |

### A.2 The tab bar

**Four tabs, in this order:**

```
Asosiy        /ilova              home + the check-in action
Taqvim        /ilova/taqvim       my month
So‘rovlar     /ilova/sorovlar     my absence requests
Profil        /ilova/profil       me, settings, sign out
```

Why four and not three:

* The employee owns exactly **four things** in this system: *today* (and the act
  of marking it), *my month*, *my requests*, *my account*. One tab per thing.
  There is no fifth thing, and nothing on the list can be merged without making
  one tab answer two questions.
* Verifix also has four. Keeping the count and the position of `Profil` (last)
  and the landing tab (first) means the employees who use Verifix today do not
  have to relearn where to put their thumb.
* `So‘rovlar` is a tab and not a tile because it is the only screen in the app
  where the employee *creates* something, and because HR's decision on a request
  is the only asynchronous thing that ever happens to an employee here. Buried
  two taps deep, it would be checked less often than it needs to be.

Why this order: frequency, left to right. Check-in is daily and often twice a
day. The calendar is consulted weekly ("was I marked late on Tuesday?").
Requests are monthly. Profile is a handful of times a year.

Tab bar rules (defined once, here):
* Always visible on the four tab roots. Always hidden on pushed screens.
* Label + icon, label always shown (never icon-only — three of the four words
  are short in all four locales; `So‘rovlar` is the longest at 9 characters and
  fits in a quarter of a 320 px screen at 11 px).
* The active tab is the accent colour; the others are `--fg-tertiary`.
* Tapping the active tab again scrolls its screen to the top. Tapping it twice
  does **not** reset the month or the filters — losing a chosen month to a
  mis-tap is a worse outcome than a long scroll.

### A.3 Full screen list

> **AMENDED 2026-10-20** → `SPEC-verifikatsiya-sorov-tahriri.md` §7: request detail screens get «O‘zgartirish» (Pending only) opening the form in edit mode; day panel shows «Shaxsiy» time.

Route slugs are Uzbek, matching the labels, so a URL read aloud in a support
call is understandable.

**Auth stack** — no tab bar, no back chevron.

| Screen | Route | Purpose | Data |
|---|---|---|---|
| Kirish | `/ilova/kirish` | Sign in with the profile phone number and the password HR gave; one line under the form: `Логин — телефон рақамингиз, паролни HR беради` | `POST /api/employee-auth/login` |
| ~~Parolni o‘rnatish~~ | ~~`/ilova/parol-ornatish`~~ | **Removed 2026-09-27** — the password is set by HR only (SPEC §M4) | — |
| Parolni unutdim | `/ilova/parol-yordam` | Static: no self-service reset or change; ask HR — HR can view the password or give a new one | none |

**Tab 1 — Asosiy**

| Screen | Route | Purpose | Data |
|---|---|---|---|
| Asosiy | `/ilova` | The check-in action, today's state, today's marks, pending requests | `GET /api/me`, today's slice of `GET /api/me/attendance`, `POST /api/me/check-in`, `GET /api/me/requests?status=Pending` |

**Tab 2 — Taqvim**

| Screen | Route | Purpose | Data |
|---|---|---|---|
| Taqvim | `/ilova/taqvim` | Month grid + selected-day detail + month totals | `GET /api/me/attendance?year&month` |

**Tab 3 — So‘rovlar**

| Screen | Route | Purpose | Data |
|---|---|---|---|
| So‘rovlar | `/ilova/sorovlar` | My requests, pending on top | `GET /api/me/requests` |
| So‘rov yuborish | `/ilova/sorovlar/yangi` | Create an absence request | `POST /api/me/requests`, absence-type catalog |
| So‘rov | `/ilova/sorovlar/:id` | One request: period, kind, note, status, HR's decision note; cancel while pending | `GET /api/me/requests`, `POST /api/me/requests/{id}/cancel` |

**Tab 4 — Profil**

| Screen | Route | Purpose | Data |
|---|---|---|---|
| Profil | `/ilova/profil` | Photo, name, department, whether mobile check-in is on; the settings list; sign out; version | `GET /api/me` |
| Sozlamalar | `/ilova/profil/sozlamalar` | Theme, language, install, app version / update | local |
| ~~Parolni o‘zgartirish~~ | ~~`/ilova/profil/parol`~~ | **Removed 2026-09-27** — the employee cannot change the password (SPEC §M4) | — |

**Cross-cutting screens** — reachable from more than one place, defined once.

| Screen | Route | Purpose |
|---|---|---|
| Joylashuv ruxsati | `/ilova/joylashuv` | The permission-denied recovery screen (§C.7). Reached from a failed check-in and from Sozlamalar. |
| Topilmadi | `/ilova/*` fallback | Unknown path inside the app; one button back to `Asosiy`. |

That is **12 screens** (10 since 2026-09-27: the two password screens were removed), of which 4 are tab roots and 3 are auth. There is no
screen in this list that does not serve a `/api/me/*` endpoint from SPEC §E, and
no `/api/me/*` endpoint that has no screen.

### A.4 Data gaps — flagged, not invented

Two screens in this inventory need fields the SPEC does not yet promise. They
are listed here so the architect decides, rather than `build-frontend` guessing.

**[GAP-1] Today's marks list (`O‘tishlar` card on Asosiy, and the day detail on Taqvim).**
The Verifix structure we are mirroring shows a *list* of the day's crossings
(`Kirish 14:22 · Chiqish 12:33 · Kirish 10:04`), not just the first and last.
`GET /api/me/attendance?year&month` is specified as "the same cells HR sees",
and an HR day cell carries the status, first in, last out and worked minutes —
not the individual passes.

*Recommended minimal shape* (the app needs nothing more): each day cell carries
`marks: [{ time, direction, source, siteName? }]`, where `source` is one of
`Terminal` / `Mobil ilova`, derived from `PassMethod`. No device id, no photo,
no coordinate — an employee has no use for any of them and the coordinate is
their own location, which the app already knew.

*If the gap is not closed*: the card degrades to two rows, `Birinchi kirish` and
`Oxirgi chiqish`, built from the cell's existing fields. The card must be built
so this degradation is a data condition, not a rewrite.

**[GAP-2] The direction of the next mark.**
The check-in button's label depends on whether my last accepted mark today was
`Kirish` or `Chiqish` (§C.2). That is the same field as GAP-1's last element.
Until GAP-1 is closed, derive it from `lastOut`/`firstIn` presence, and after
any successful check-in from the response of `POST /api/me/check-in`, which
must return the accepted mark's `occurredAt` (server time), `direction` and
`workSiteName`. **This response shape is a hard requirement, not a gap**: the
success state shows the *server's* time, never the phone's (SPEC §B3).

**[Deferred, not a gap] The `Ma’lumotlar` profile detail.**
If the owner later wants Verifix's employee card (birthday, position, hire date,
manager, contacts), it is one pushed screen at `/ilova/profil/malumotlar`
reading four or five more fields on `GET /api/me`. It is out of this wave
because `/api/me` does not carry them and because none of it helps an employee
mark attendance. Do not build a stub.

### A.5 What is on each screen, in order

Screen content is written top to bottom. The order is the answer to "what did
the employee open the app for", most likely answer first.

**Asosiy**
1. **Header** — large title `Bugun`, subtitle `22-sentabr, seshanba` and, when a
   schedule exists, `Ish kuni 08:30–18:00`. Top-right: my photo, 32 px, tapping
   it goes to `Profil` (Verifix's position for identity; the bell's slot, now
   that the bell is gone).
2. **Check-in card** — the app's one job. Full specification in §C.
3. **Bugungi holat** — status chip (`Ishladi` / `Kechikdi …` / `Kelmadi` /
   `Sababli` / `Dam olish kuni`), `Kirish 08:35`, `Chiqish —`, worked time.
   Present even before the first mark, showing the schedule and empty times, so
   the card's position never moves during the day.
4. **O‘tishlar** — today's marks, newest first, each row: direction icon +
   word, source chip, time. `Barchasi` link → `Taqvim` with today selected.
   (GAP-1.)
5. **So‘rovlarim** — only when I have a pending request; up to two, then
   `Barchasi` → the `So‘rovlar` tab. Hidden entirely when there are none: an
   empty card on the busiest screen is noise, and the tab is one tap away.
6. **Install hint** — Android prompt card or the iOS "Share → Add to Home
   Screen" hint, dismissible, at most once per week, never on the first session
   (§G.4).

**Taqvim**
1. Month header: `‹ Sentabr 2026 ›`. The forward arrow is disabled past the
   current month — there is no attendance in the future to look at.
2. Month grid, 7 columns, Monday first (`du se chor pay ju sha ya`, as Verifix).
3. **Selected-day panel** — defaults to today. Kirish / Chiqish big, worked
   time, late minutes when any.
4. **Ish jadvali** — the day's schedule and break window, when one exists.
5. **O‘tishlar** — that day's marks (GAP-1).
6. **So‘rovlar** — any request covering that day, tappable to its detail.
7. **Oylik natija** — month totals: worked days, absent days, excused days,
   late count, planned hours vs worked hours. A donut like Verifix's is
   optional; the numbers are not.
8. **Belgilar** (legend) — one line per status used in this month only.

**So‘rovlar**
1. Section `Qaror kutmoqda` — pending requests, newest first.
2. Section `Qaror qabul qilingan` — decided and cancelled, newest first.
3. Bottom-pinned primary button `So‘rov yuborish`.

**Profil**
1. Identity card: photo, full name, department/position.
2. `Telefondan belgilash` status row — `Yoqilgan` / `Yoqilmagan`, with one line
   of explanation when off. This is the answer to the single most likely support
   question this app will generate, so it is on the profile, not hidden in
   settings.
3. List group: `Sozlamalar`, `Joylashuv ruxsati`. (`Parolni o‘zgartirish` was
   removed on 2026-09-27 — SPEC §M4.)
4. Danger row: `Chiqish`.
5. Footer: `Ilova versiyasi 1.0.0`.

**Sozlamalar**
* Group `Interfeys`: `Mavzu` (`Tizimdagi kabi` / `Yorug‘` / `Tungi`), `Til`
  (the four locales, each in its own name, as the HR `LOCALES` table).
* Group `Joylashuv`: current permission state + link to `/ilova/joylashuv`.
* Group `Ilova haqida`: version, and `Yangilash` when a new service worker is
  waiting (§G.5).

---

## B. Navigation map

### B.1 Shape

```
/ilova/kirish ──┐  auth stack: no tab bar, no back
/ilova/parol-yordam ──┘

┌── tab bar (always visible on the four roots) ─────────────┐
│ /ilova            Asosiy                                  │
│ /ilova/taqvim     Taqvim                                  │
│ /ilova/sorovlar   So‘rovlar                               │
│ /ilova/profil     Profil                                  │
└───────────────────────────────────────────────────────────┘
        │                    │                  │
        │                    │                  ├─ push /ilova/profil/sozlamalar
        │                    │                  └─ push /ilova/joylashuv
        │                    ├─ push /ilova/sorovlar/yangi
        │                    └─ push /ilova/sorovlar/:id
        └─ push /ilova/joylashuv     (from a failed check-in)
```

**Pushed screens hide the tab bar** and carry a back chevron at the top left
with the title centred — exactly Verifix's pushed `Profil` and `Sozlamalar`
screens. This is the only distinction between "a tab" and "a page", and it is
absolute: if the tab bar is visible, back does not exist; if back is visible,
the tab bar does not.

### B.2 History rules

One history stack, not four.

* **Switching tabs `replace`s** the current entry. Back therefore never walks
  backwards through a trail of tab visits — the single most common complaint
  about web apps wearing a tab bar.
* **Pushing** a detail, form or settings screen pushes a real entry, so the
  hardware back button and the iOS edge-swipe pop it. Nothing is intercepted.
* **Back on a tab root that is not `Asosiy`** goes to `Asosiy` (one interception,
  the Android convention; harmless on iOS where the gesture does not exist at a
  stack root).
* **Back on `Asosiy`** is left to the browser. In an installed PWA that means
  nothing happens; in a browser tab it leaves the app. We do not try to trap it,
  and we never show an "are you sure you want to exit" dialog.
* **Scroll position** is restored when popping back to a tab root, and reset to
  the top when pushing.

### B.3 Guards

| Condition | Behaviour |
|---|---|
| No token, any `/ilova/*` route | redirect to `/ilova/kirish?keyin=<encoded path>`; after sign-in, go to `keyin` or `/ilova` |
| ~~`must_change_password`~~ | removed 2026-09-27 — there is no forced first-run step |
| 403 `xodim.hisob_faol_emas` | the employee was terminated (or deleted): every `/api/me/*` call is refused at once; the screen shows the message, and the session ends when the refresh is refused |
| 401 from any endpoint | clear the token, redirect to `/ilova/kirish`, toast `Sessiya tugadi. Qaytadan kiring.` |
| 403 / audience mismatch | same as 401 — an employee token that is not accepted is never "almost authorised" |
| Unknown `/ilova/*` path | `Topilmadi` screen, one button `Asosiyga qaytish` |

### B.4 Deep links

All under `/ilova`, all safe to paste into a chat and open on a phone.

```
/ilova                                    Asosiy
/ilova/taqvim                             this month, today selected
/ilova/taqvim?oy=2026-09                  a month, its 1st selected
/ilova/taqvim?oy=2026-09&kun=2026-09-22   a month, a day selected
/ilova/sorovlar                           my requests
/ilova/sorovlar?holat=Pending             filtered (the list's own section links)
/ilova/sorovlar/yangi                     new request form
/ilova/sorovlar/yangi?kun=2026-09-22      new request, date prefilled from the calendar
/ilova/sorovlar/:id                       one request
/ilova/profil                             profile
/ilova/profil/sozlamalar                  settings
/ilova/joylashuv                          location permission help
/ilova/kirish?keyin=<path>                sign in, then continue
```

Query keys are Uzbek (`oy`, `kun`, `holat`, `keyin`) so the URL reads in the
same language as the screen. `oy` is `YYYY-MM`, `kun` is a full `YYYY-MM-DD`
(never a bare day number — a day that does not carry its month is a bug waiting
for a month-boundary). An invalid or future `oy` falls back to the current
month without an error screen.

### B.5 Where mobile differs — it does not

There is no desktop layout. The app is one column, capped at 480 px and centred,
at every width. On a tablet or a laptop browser it is the same app in a column,
with the tab bar still at the bottom. Building a second layout for a device no
employee will use it on would double the surface for no user.

---

## C. The check-in screen — the core

Everything else in this app is a record. This is the only place the employee
*does* something, and it is done outdoors, in a hurry, possibly in sunlight,
possibly on a bad connection, by someone who is late. It gets more design than
the rest of the app combined.

### C.1 The card

One card, one button, nothing competing with it.

```
┌─────────────────────────────────────────┐
│                                         │
│   [status line — one sentence]          │
│                                         │
│   ┌───────────────────────────────┐     │
│   │   →]   Ishga keldim           │     │   64 px tall, full width
│   └───────────────────────────────┘     │
│                                         │
│   [result / hint line — one sentence]   │
│                                         │
└─────────────────────────────────────────┘
```

Fixed rules:
* **One button.** Direction is never a choice the employee makes from two
  buttons side by side — a two-button card invites marking `Chiqish` at 08:30.
  The app knows which direction is next (§C.2) and offers exactly that one.
* **The button never disappears and never moves.** Its label and state change;
  its position on the screen does not. A control that vanishes when it is not
  usable teaches the employee that the app is unreliable.
* **The button is always the accent blue**, in both directions. `Ishdan ketdim`
  is not destructive and must not be red; red would make going home look like
  deleting something.
* Changing direction manually is **not** offered. If the app's idea of the next
  direction is wrong, the fix is a request to HR, not a free-text mark — a
  mobile mark is already the weakest evidence in the system (SPEC §B) and
  letting the employee choose its direction would weaken it further.

### C.2 States before and after

`dir` is the direction of my last accepted mark today.

| # | State | When | Status line | Button | Result line |
|---|---|---|---|---|---|
| 1 | **Tayyor** | no mark today | `Bugun hali belgilamadingiz` | `Ishga keldim` (enabled) | schedule: `Ish kuni 08:30 da boshlanadi` |
| 2 | **Ishda** | `dir = Kirish` | `Ish kunidasiz` | `Ishdan ketdim` (enabled) | `08:35 da ishga keldingiz · Bosh ofis` |
| 3 | **Kun yakunlandi** | `dir = Chiqish` | `Ish kuningiz yakunlandi` | `Ishga keldim` (enabled) | `12:33 da ketdingiz · bugun 3 soat 58 daq.` |
| 4 | **Ruxsat yo‘q** | `mobileCheckInAllowed = false` | `Telefondan belgilash yoqilmagan` | `Ishga keldim` (**disabled**) | `Bu imkoniyat hisobingizda o‘chirilgan. HR bo‘limiga murojaat qiling.` |
| 5 | **Aniqlanmoqda** | fetching position | unchanged | `Joylashuv aniqlanmoqda…` (busy) | `Brauzer so‘rasa, «Ruxsat berish» ni tanlang` |
| 6 | **Yuborilmoqda** | POST in flight | unchanged | `Yuborilmoqda…` (busy, **not cancellable**) | — |
| 7 | **Qabul qilindi** | 200 | `Qabul qilindi` | `Qayta urinish` disabled with a 60 s countdown | `Ishga keldingiz · 08:35 · Bosh ofis` |
| 8 | **Rad etildi** | 4xx | error title (§C.5) | per error (§C.5) | error body |
| 9 | **Internet yo‘q** | `navigator.onLine === false` or a network failure before send | `Internet yo‘q` | `Ishga keldim` (disabled) | `Belgilash uchun internet kerak. Ulanib, qayta urinib ko‘ring.` |

State 3 → state 1 is a real transition: people leave for lunch and come back,
and Verifix's own journal shows `Kirish · Chiqish · Kirish` in one day. Marking
a second entry is normal and is never warned about.

**States 1–3 are derived from data, 5–8 from the last attempt.** After a
success (7), the card settles into state 2 or 3 after 5 seconds but **keeps the
confirmation line**, because the confirmation is the receipt: the employee's
proof that the mark exists is that sentence with the server's time in it.

### C.3 Empty, loading, no-permission, error of the card itself

* **Loading** (`/api/me` and today's cell not yet in): the card renders at full
  height with a skeleton status line and a disabled button already carrying a
  label. Never a spinner in place of the button — the button's arrival must not
  make the screen jump under a thumb already moving towards it.
* **`/api/me` failed**: the card shows `Ma’lumot yuklanmadi` and a `Qayta
  urinish` button. The check-in button stays present and disabled. We do **not**
  let a check-in proceed without knowing whether the flag is on: the server
  would reject it anyway (`davomat.mobil_ruxsat_yoq`) and the employee would
  have burned a location prompt for nothing.
* **No permission** (state 4): the control is **shown, disabled, with a
  reason**. This is the app-wide rule for the «no permission» state, defined
  once in §E.9. Hiding it would produce the support call «ilovamda tugma
  yo‘q», which a disabled button with one sentence answers by itself.

### C.4 The location sequence, step by step

The order below exists to protect **the one location prompt iOS gives us**.

1. **Tap.** `getCurrentPosition` is called only from inside the tap handler.
   Never on screen load, never on a timer, never in an effect. Safari will
   silently refuse a geolocation request that is not gesture-initiated, and the
   employee would see a button that does nothing.
2. **Pre-prompt sheet — first time only.** Before the browser is asked the very
   first time, a bottom sheet:
   > **Joylashuv kerak**
   > Belgilash uchun obyektga yaqinligingiz tekshiriladi. Joylashuv faqat shu
   > tugmani bosganingizda olinadi — ilova sizni kun bo‘yi kuzatmaydi.
   > `Davom etish` · `Bekor qilish`

   Shown when `navigator.permissions.query({ name: 'geolocation' })` reports
   `prompt`, or — where that API is missing, which includes older iOS Safari —
   when our own `localStorage` flag says we have never asked. `Davom etish`
   continues **within the same gesture chain**; if a browser turns out to be
   strict about that, the sheet's button is itself the gesture, which is why
   the sheet is a sheet and not a toast.

   The pre-prompt is shown **once, ever**. It is not a nag.
3. **The browser dialog.** Our UI goes to state 5. No blocking overlay of our
   own: the OS dialog is already modal, and a spinner behind it is invisible
   anyway. The hint line tells the employee which button to press, in their
   language, because the OS dialog is in the phone's language and may not be
   theirs.
4. **Reading the position.**
   `{ enableHighAccuracy: true, timeout: 15000, maximumAge: 0 }`.
   * `maximumAge: 0` is not negotiable: a cached fix from an hour ago, taken at
     home, would produce an accepted mark at the office.
   * If the first fix reports `accuracy > 100 m`, do **one silent retry**: a
     `watchPosition` for up to 10 s, keep the best fix, then clear the watch.
     GPS routinely starts at ±150 m and settles to ±15 m within a few seconds,
     and this turns most `davomat.joylashuv_aniq_emas` rejections into successes
     without the employee ever seeing an error. The button stays in state 5 with
     `Joylashuv aniqlanmoqda…` throughout — one wait, not two.
   * We still send whatever we ended up with. **The server re-checks accuracy**
     (SPEC §E step 2); the client never decides a mark is invalid on its own.
5. **Send.** State 6. The button is not cancellable here — a cancelled request
   may still have created a pass, and "I pressed cancel but it marked me" is the
   worst possible outcome for a trust-sensitive feature.
6. **Result.** State 7 or 8.

After a success, the button is disabled for **60 seconds** with a visible
countdown (`Qayta urinish (52)`), matching `AttendanceRules.BounceSeconds`. This
is the client preventing `davomat.takroriy_belgi` rather than reporting it: a
double tap on a slow connection is the ordinary cause, and an error message for
an ordinary action is a design failure.

### C.5 Every rejection, by code

Each rejection replaces the status and result lines in the same card — no modal,
no navigation, no toast. The employee's eyes are already on the card.

| Code | Tone | Title | Body | Action |
|---|---|---|---|---|
| `davomat.mobil_ruxsat_yoq` | neutral | `Telefondan belgilash yoqilmagan` | `Bu imkoniyat hisobingizda o‘chirilgan. HR bo‘limiga murojaat qiling.` | **No retry button.** Retrying cannot succeed. Secondary `Yangilash` refetches `/api/me`; the card then latches into state 4 for the session. |
| `davomat.joylashuv_aniq_emas` | warning | `Joylashuv aniq emas` | `Hozirgi aniqlik ±{{n}} m. Ochiq joyga chiqing, bino yoki mashina ichidan chiqib qayta urinib ko‘ring.` | `Qayta urinish` (primary). Show the measured accuracy — a number the employee can act on beats "try again". |
| `davomat.obyektdan_uzoq` | warning | `Obyektdan uzoqdasiz` | `Eng yaqin obyekt: {{name}} — {{distance}}. Belgilash faqat obyekt hududida ishlaydi.` | `Qayta urinish`. Distance is formatted by §F.6 (`420 m` / `3.4 km`). No map — the SPEC defers map components (§F), and a map would take a second permission's worth of screen for information the sentence already carries. |
| `davomat.takroriy_belgi` | **info, not error** | `Siz hozir belgilagansiz` | `Oxirgi belgi {{time}} da qabul qilingan.` | No button. The countdown from §C.4 is already running; when it reaches zero the card returns to its normal state. Never coloured red: nothing went wrong. |

Non-server failures, same card:

| Failure | Title | Body | Action |
|---|---|---|---|
| `PERMISSION_DENIED` | — | — | Go straight to `/ilova/joylashuv` (§C.7). No error text in the card: the card cannot fix this and the recovery screen can. |
| `POSITION_UNAVAILABLE` | `Joylashuvni aniqlab bo‘lmadi` | `Telefonda joylashuv (GPS) yoqilganini tekshiring va qayta urinib ko‘ring.` | `Qayta urinish` |
| `TIMEOUT` | `Joylashuv aniqlanmadi` | `Signal zaif bo‘lishi mumkin. Ochiq joyga chiqib qayta urinib ko‘ring.` | `Qayta urinish` |
| `navigator.geolocation` missing, or insecure context | `Bu brauzerda joylashuv ishlamaydi` | `Ilovani Safari yoki Chrome’da oching va bosh ekranga qo‘shing.` | link to `/ilova/joylashuv` |
| 5xx | `Server javob bermadi` | `Biroz kutib, qayta urinib ko‘ring.` | `Qayta urinish` |
| **Network failure *after* the request was sent** | `Javob kelmadi` | `Belgilash yuborildi, lekin javob kelmadi. Belgi qabul qilingan bo‘lishi mumkin.` | **`Holatni tekshirish`**, not `Qayta urinish` |

That last row is the one correctness trap in this screen and must not be
simplified away. A dropped response is **ambiguous**: the pass may exist. An
automatic retry would create a second pass, or bounce off
`davomat.takroriy_belgi` and tell the employee something false. `Holatni
tekshirish` refetches today; if a new mark appeared, the card shows state 7 with
the server's time, as if the original attempt had simply been slow. It is the
only honest recovery.

### C.6 Offline: no queueing, ever

The check-in button is disabled offline and the attempt is **not** stored for
later. The reason is in SPEC §B3: `occurredAt` is *server now*. A mark composed
at 08:35 and delivered at 11:00 would be stamped 11:00 — a wrong attendance
record created by a helpful-looking feature. Nothing in this app writes to a
background sync queue.

The message says what to do instead: `Belgilash uchun internet kerak. Ulanib,
qayta urinib ko‘ring.` The rest of the app stays readable from cache (§G).

### C.7 The permission-denied recovery screen — `/ilova/joylashuv`

On iOS a refusal is sticky: the browser will not ask again, and the employee
must change it in Settings, occasionally by removing and re-adding the home
screen icon (SPEC §B). A silent failure here strands a person outside a
building, so this is a real screen and not a toast.

Structure:

1. **Title** `Joylashuvga ruxsat berilmagan` and one sentence of why it is
   needed: `Belgilash uchun obyektga yaqinligingiz tekshiriladi.`
2. **Numbered steps for this device**, chosen from four variants. Detection is
   UA plus `navigator.standalone` — imprecise by nature, so the screen also
   carries a `Boshqa qurilma` disclosure that reveals all four sets, and no
   variant is ever hidden behind detection alone.

   | Variant | Steps (uz-Latn) |
   |---|---|
   | **iPhone, bosh ekranga qo‘shilgan** | 1. iPhone’da `Sozlamalar` ni oching. 2. Ro‘yxatdan `Bonvi` ilovasini toping. 3. `Joylashuv` → `Ilovadan foydalanganda`. Ilova ro‘yxatda bo‘lmasa: nishonchani bosh ekrandan o‘chiring va qaytadan qo‘shing. |
   | **iPhone, Safari** | 1. Manzil satridagi `ᴀA` tugmasini bosing. 2. `Veb-sayt sozlamalari` → `Joylashuv` → `So‘rash`. Yoki: `Sozlamalar` → `Safari` → `Joylashuv` → `So‘rash`. |
   | **Android, o‘rnatilgan ilova** | 1. `Sozlamalar` → `Ilovalar` → `Bonvi`. 2. `Ruxsatlar` → `Joylashuv` → `Ilovadan foydalanganda ruxsat berish`. |
   | **Android, Chrome** | 1. Manzil satridagi qulf belgisini bosing. 2. `Ruxsatlar` → `Joylashuv` → `Ruxsat berish`. |

3. **`Tekshirish` button.** Re-queries the permission state. When it becomes
   `granted` or `prompt`, navigate back to `Asosiy` with a toast `Joylashuvga
   ruxsat berildi`. When it is still `denied`, an inline line: `Hali ham
   ruxsat yo‘q. Brauzerni yopib, qaytadan oching.`
4. **A way out that does not depend on the phone.** The last block:
   > **Ruxsat berib bo‘lmayapti?**
   > Turniketdan o‘ting — u asosiy hisoblash usuli. Yoki HR bo‘limiga murojaat
   > qiling: kun qo‘lda to‘g‘rilanadi.

   Nobody may be left on this screen with no next action. The terminal is the
   source of truth anyway (SPEC §B1), so pointing at it is not a consolation
   prize — it is the correct instruction.

---

## D. Design language

Standing project rule: **Apple's Human Interface Guidelines in spirit**. Verifix
gives the structure; iOS gives the feel. Concretely that means: one tint colour
for everything interactive, colour spent only on meaning, generous white space,
large soft corners, depth by layering rather than by shadow, and motion that is
short and eased.

All values below are **literal**. Nothing is computed at runtime — see §F.2 for
why `color-mix()` is banned here even though the HR app uses it.

### D.1 What we do better than the Verifix screenshots, and why

The owner asked for better colour. These are the specific defects being fixed.

1. **The calendar is a wall of yellow.** In the Verifix month grid, 20 of 21
   past workdays are filled with saturated yellow. Yellow in their own donut on
   the same screen means *kechikish*. A colour that appears on 95 % of cells
   carries no information — it is either alarm fatigue or decoration, and the
   one green day is impossible to interpret.
   **Ours:** an ordinary worked day is a **plain surface** — no fill, normal
   text. Colour is spent only on days that need attention. This is not an
   invention: it is the rule the HR timesheet already follows
   (`DAY_STATUS_LOOK.Present` is `bg-elevated` / `fg`), so employee and HR see
   the same day the same way, which is the whole point of showing the employee
   "the same cells HR sees".
2. **Lateness competes with status.** Verifix has no way to say "came, but
   late" — the day is simply yellow.
   **Ours:** status is the cell's *fill*, lateness is a **3 px bar along the
   bottom edge**. Two facts, two channels, neither hiding the other — again the
   HR rule (`LATE_BAR_CLASS`).
3. **Today is a filled blue box.** It out-shouts every status colour on the
   grid, so on the current day you cannot see the day's own state.
   **Ours:** today is a **2 px accent ring** and a bold number; the *selected*
   day is the filled one. On first open today is both, and when you select
   another day today keeps its ring.
4. **Dark only.** A check-in happens outdoors, in daylight, often at 08:30 in
   summer. A dark-only interface at low screen brightness is the worst possible
   choice for that moment.
   **Ours:** light is the default, dark follows the system, both are
   first-class, and the theme is switchable in `Sozlamalar`.
5. **Marginal contrast.** Their secondary text, the yellow `Kutilmoqda` label
   and the grey donut legend sit at or below 4.5:1 on the dark card.
   **Ours:** every foreground/background pair in §D.3 has a stated role, body
   text is AA at minimum, and **pure system yellow is never used as text**.
6. **Status colour with no word attached.** Their marks list identifies the
   source with two tiny unlabelled glyphs (a face, a pin).
   **Ours:** colour is never the only carrier of meaning. Every status is a
   **word plus a dot plus a tint**, and the word is the part that is translated.
   Text sitting on a status tint uses the normal foreground colour, not the hue
   — that guarantees AA on every tint in both themes without tuning nine hues,
   and it is what iOS list rows actually do.
7. **Invented abbreviations.** `Ish joy. yo‘q.`, `Jadval o‘zg.`, `Ish kunining
   boshlanishi va oxiri haqi…` — labels truncated to fit a layout.
   **Ours:** list rows wrap to two lines; abbreviations are never invented to
   fit; the label vocabulary is fixed in §F.7 and sized for the longest locale
   (`ru` is typically the widest).
8. **No primary action.** Their home is a read-only digest because their marks
   come from a terminal. Ours has exactly one job and the screen says so.
9. **Small icon-only targets.** Bell, chevrons and the donut legend are below a
   comfortable thumb.
   **Ours:** 44 × 44 minimum for everything tappable, stated in §D.7, with one
   documented exception at 320 px.

### D.2 Colour roles — surfaces and content

Two themes, same token names. A component reads the token, never a hex.

| Token | Light | Dark | Role |
|---|---|---|---|
| `--bg` | `#F4F6F9` | `#0E1013` | page background |
| `--bg-elevated` | `#FFFFFF` | `#1A1D22` | card, sheet, tab bar |
| `--bg-sunken` | `#EDF0F4` | `#131519` | inset block, day-off cell |
| `--bg-pressed` | `#E8EDF3` | `#2A2F37` | the pressed state of a row or tile |
| `--fg` | `#16191D` | `#FAFBFC` | primary text |
| `--fg-secondary` | `#5C636E` | `#C6CDD8` | labels, captions |
| `--fg-tertiary` | `#98A0AC` | `#98A2B0` | least important text, disabled |
| `--fg-on-accent` | `#FFFFFF` | `#FFFFFF` | text on a filled accent surface |
| `--border` | `#E3E8EF` | `#2E343C` | card and separator lines |
| `--border-strong` | `#CFD6E0` | `#3E4650` | input outlines |

Depth is a **surface change**, not a shadow. The only shadows in the app are
the tab bar's hairline top border (not a shadow at all) and one soft shadow
under a bottom sheet. iOS builds hierarchy by layering; a drop shadow on every
card is an Android-era habit.

### D.3 Accent and danger

| Token | Light | Dark | Role |
|---|---|---|---|
| `--accent` | `#0071E3` | `#0071E3` | filled interactive surface; white text is **4.70:1**, AA |
| `--accent-pressed` | `#0058B1` | `#0058B1` | the same control, pressed — iOS darkens, it does not shrink |
| `--accent-text` | `#0066CC` | `#409CFF` | links, active tab label, chevrons |
| `--accent-soft` | `#E0EFFF` | `#172D45` | tinted surface (selected day, info tint) |
| `--accent-on-soft` | `#0066CC` | `#409CFF` | text on `--accent-soft` |
| `--danger` | `#D70015` | `#D70015` | filled destructive surface; white text **5.38:1**, AA |
| `--danger-text` | `#D70015` | `#FF6961` | destructive text row (`Chiqish`) |
| `--ring` | `#007AFF` | `#0A84FF` | focus ring |

`--accent` and `--danger` deliberately do **not** change between themes: white
text sits on them, and brightening them for dark mode would drop that text below
AA. Only the *text* tints brighten.

**One filled accent per screen.** The check-in button on `Asosiy`, the submit
button on a form, the create button on `So‘rovlar`. If a second filled blue
button appears on a screen, one of them is not the primary action and should be
a plain text button.

### D.4 Semantic status colours

Each status has a **hue** (for the dot and the icon), a **tint** (for the cell
or chip background), and its text is always `--fg`. Tints are opaque literals,
pre-mixed against the card surface — not alpha, because the same alpha over a
card and over the page background produces two different colours.

| Status | uz-Latn | Hue | Tint (light) | Tint (dark) |
|---|---|---|---|---|
| on-time / worked | `Ishladi` | — *(none)* | `--bg-elevated` | `--bg-elevated` |
| late | `Kechikdi` | `#FF9500` | `#FFF2E0` | `#3F301D` |
| absent | `Kelmadi` | `#FF3B30` | `#FFE7E6` | `#3F2224` |
| excused | `Sababli` | `#007AFF` | `#E0EFFF` | `#172D45` |
| day off | `Dam olish kuni` | `#8E8E93` | `#EDF0F4` | `#131519` |
| worked on a day off | `Dam olish kuni ishladi` | `#AF52DE` | `#F5EAFB` | `#322540` |
| incomplete day | `Chiqish yo‘q` / `Kirish yo‘q` | `#FF9500` | `#FFF2E0` | `#3F301D` |
| no schedule | `Jadval yo‘q` | — | diagonal hatch in `--border` | same |
| future workday | *(no label)* | — | `--bg-elevated`, `--fg-tertiary` | same |
| pending | `Kutilmoqda` | `#FF9500` | `#FFF2E0` | `#3F301D` |
| approved | `Tasdiqlangan` | `#34C759` | `#E7F8EB` | `#1E382B` |
| rejected | `Rad etilgan` | `#FF3B30` | `#FFE7E6` | `#3F2224` |
| cancelled | `Bekor qilingan` | `#8E8E93` | `#EDF0F4` | `#131519` |
| success (check-in accepted) | `Qabul qilindi` | `#34C759` | `#E7F8EB` | `#1E382B` |

Three rules that come with this table and must not be broken:

* **`Ishladi` has no colour.** It is the normal case. This is the fix for
  defect 1 in §D.1.
* **Lateness is not a status**, it is a number. A person can arrive late and
  still be `Ishladi`. It renders as a 3 px bottom bar in `#FF9500` on the day
  cell, plus the minutes in text. This mirrors the HR timesheet exactly.
* **The meanings in this table are copied from the HR app by value and must not
  drift.** `Sababli` is blue in both apps because an approved absence is
  documented information, not a problem; `Kelmadi` is red in both; `Dam olish
  kuni ishladi` is purple in both. If HR later changes a meaning, both tables
  change together, by hand, on purpose.

### D.5 Radii

| Token | Value | Used by |
|---|---|---|
| `--r-chip` | `8px` | status chip, small tag |
| `--r-control` | `12px` | button, input, select, list row highlight |
| `--r-card` | `16px` | card, calendar day cell |
| `--r-sheet` | `20px` | bottom sheet (top corners only), large panel |
| `--r-hero` | `28px` | the check-in button |
| `--r-pill` | `999px` | avatar, dot, segmented control |

Corners are never sharp. The check-in button gets the largest radius in the app
because it is the largest control in the app; a 12 px corner on a 64 px tall
full-width button looks like a box, not a button.

### D.6 Spacing

A 4 px base, and only these steps:

```
4  8  12  16  20  24  32  40  48
```

Fixed decisions so nobody has to choose:
* Page gutter: **16 px**; **12 px** below 360 px viewport width.
* Gap between cards in a vertical stack: **12 px**.
* Card inner padding: **16 px**.
* Gap between a label and its value inside a row: **2 px** (the one sub-step,
  because a label and its value are one object, as in Verifix's rows).
* Space above the first card, below the header: **16 px**.
* Bottom padding on every scrollable screen:
  `calc(64px + 16px + env(safe-area-inset-bottom, 0px))` — the tab bar is fixed
  and must never cover the last row of content.

### D.7 Type scale

Declared in `rem` with a 16 px root, so the phone's text-size setting still
works. The px column is the iOS name.

| Token | rem | px | Role |
|---|---|---|---|
| `--t-caption2` | `0.6875` | 11 | tab bar label, legend |
| `--t-caption1` | `0.75` | 12 | timestamp, meta |
| `--t-footnote` | `0.8125` | 13 | secondary line in a row |
| `--t-subhead` | `0.9375` | 15 | body default |
| `--t-callout` | `1` | 16 | **every form input, without exception** (§F.4) |
| `--t-body` | `1.0625` | 17 | list row primary text, button label |
| `--t-title3` | `1.25` | 20 | card title |
| `--t-title2` | `1.375` | 22 | check-in button label |
| `--t-title1` | `1.75` | 28 | screen title |
| `--t-hero` | `2.125` | 34 | the big times on the day panel (`08:35`) |

Font family: the system stack, no web font.

```
-apple-system, BlinkMacSystemFont, "SF Pro Text", "Segoe UI",
Roboto, system-ui, sans-serif
```

No web font is downloaded. On iOS this is SF Pro, which is what an iOS-feeling
app should be set in; on Android it is Roboto. It also removes a render-blocking
download on a phone that may be on a weak connection outside a building — the
exact moment the app is used.

Line height 1.4 for body, 1.2 for titles and for the hero numerals. Numerals in
times, durations and the calendar are **tabular** (`font-variant-numeric:
tabular-nums`) so `08:35` and `11:11` are the same width and a column of times
does not shimmer.

### D.8 Touch targets and motion

* **44 × 44 px minimum** for anything tappable. Where a visual element is
  smaller, the hit area is padded out to 44.
* List row height: **52 px** minimum, **64 px** when it has two lines.
* Tab bar item: 48 px tall plus `env(safe-area-inset-bottom)`.
* Check-in button: **64 px tall, full width**.
* Calendar day cell: **44 px tall**, width `1fr`. *Documented exception:* at a
  320 px viewport the cell is ~40 px wide. Accepted, because the alternative is
  a horizontally scrolling month, and 320 px devices (iPhone SE 1, 5s) are a
  small and shrinking part of the fleet. Above 360 px the cell is ≥ 44 px both
  ways.
* Gap between two adjacent tappable things: never less than 8 px.

Motion: 150 ms for a state change, 250 ms for a sheet or a screen transition,
easing `cubic-bezier(0.25, 0.1, 0.25, 1)`. A pressed control **darkens**; it
does not scale, bounce or ripple. All of it is disabled under
`prefers-reduced-motion: reduce`, where transitions become instant.

---

## E. Component set

Everything here lives in this app's own `src/ui/`. **Nothing is imported from
the HR web app** (§0.1). The list is deliberately short: 16 components cover all
12 screens. If a thirteenth screen ever needs a seventeenth component, that is a
signal to check whether the screen is really new.

Each component is named once, with its states. A state not listed here does not
exist, and a component built twice belongs in this list before the second copy
is written.

### E.1 Shell

**`AppShell`** — the page frame. Renders the fixed `TabBar`, the scroll
container, the safe-area padding, the offline bar and the update toast slot.
*States:* `tabs` (tab bar visible) · `pushed` (tab bar hidden, `NavBar` shown).

**`TabBar`** — four items, fixed to the bottom, hairline top border, elevated
surface, `padding-bottom: env(safe-area-inset-bottom)`.
*Item states:* `active` · `inactive` · `pressed`.

**`NavBar`** — pushed-screen header: back chevron left, centred title, optional
single action right.
*States:* `default` · `with-action`.

**`ScreenHeader`** — tab-root header: large title, optional subtitle, optional
trailing avatar. Does not collapse on scroll (a collapsing large title is a
native behaviour that web imitations get wrong; a static one is honest).

### E.2 Surfaces

**`Card`** — elevated surface, 16 px radius, 16 px padding, optional title row
with an optional trailing link (`Barchasi`).
*States:* `default` · `interactive` (whole card is a link; gets a pressed state).

**`ListGroup` / `ListRow`** — the iOS grouped list: rounded container, hairline
separators inset to the text, rows of 52/64 px.
*Row variants:* `navigation` (chevron) · `value` (right-aligned value text) ·
`toggle` · `danger` (the `Chiqish` row) · `static`.
*Row states:* `default` · `pressed` · `disabled` · `loading`.

**`Sheet`** — bottom sheet, 20 px top corners, backdrop, drag-to-dismiss where
supported, always also dismissible by a button. Used for: the location
pre-prompt, the absence-type picker, the date picker, language and theme.
*States:* `open` · `closing`. Never nested, never more than one at a time.

### E.3 Actions

**`Button`**
*Variants:* `primary` (filled accent — one per screen) · `secondary` (tinted,
`--accent-soft`) · `plain` (text only) · `danger` (filled `--danger`).
*Sizes:* `hero` (64 px, the check-in button) · `regular` (48 px) · `compact`
(36 px, inline).
*States:* `default` · `pressed` · `disabled` · `busy` (spinner replaces the
icon, label changes to a working verb, width does not change) · `countdown`
(disabled with a seconds counter, used after a successful mark).

**`BottomAction`** — a form's primary button pinned above the safe area with a
blurred/solid backdrop behind it, as in Verifix's `Yuborish`. Exists so no form
ever puts its save button at the end of a long scroll.

### E.4 Data display

**`StatusChip`** — hue dot + tint background + `--fg` word. Takes a status key
from the table in §D.4 and nothing else; there is no free-text chip.
*Sizes:* `sm` (in a list row) · `md` (on a card).

**`MarkRow`** — one attendance mark: direction icon (green in, red out),
direction word (`Kirish` / `Chiqish`), source chip (`Turniket` / `Mobil ilova`),
time right-aligned in tabular numerals. This is Verifix's `Qaydnoma` row, with
the two mystery glyphs replaced by a word.
*States:* `default` · `skeleton`.

**`MonthGrid`** — 7 columns, Monday first, weekday header row, leading/trailing
days of the adjacent months dimmed and not tappable.
*Cell states:* `worked` · `late` (worked + bottom bar) · `absent` · `excused` ·
`dayOff` · `dayOffWorked` · `incomplete` · `noSchedule` · `future` ·
`notEmployed` · `today` (ring) · `selected` (filled accent) · `skeleton`.

**`DayPanel`** — the selected day's summary: two hero times (`08:35` / `12:33`)
with `kirish` / `chiqish` labels under them, worked duration, late minutes.
*States:* `default` · `no-data` · `skeleton`.

**`StatRow`** — a labelled number for the month totals. No chart component is
mandatory; if a donut is added later it is one more variant here, not a new
charting dependency.

### E.5 Forms

**`Field`** — label above, control, helper text below, error text below that.
Required fields are marked with `*` after the label. One column, always.
*States:* `default` · `focused` · `error` · `disabled` · `readonly`.

**`TextInput`**, **`TextArea`** — 16 px font, minimum (§F.4).
**`SelectRow`** — a `ListRow` that opens a `Sheet` of options. Used for the
absence type and for anything else with a closed list; never a native `<select>`
on a form, because its rendering differs wildly between the phones in the fleet.
**`DateRow`**, **`TimeRow`** — a `ListRow` that opens a date/time `Sheet`.
Native `<input type="date">` is used *inside* the sheet where it is available
and falls back to three scrolling columns where it is not (§F.3).

Form rules, defined once for all three forms in the app:
* Validation on **blur** and on **submit**, never on every keystroke.
* The error message sits **under the field**, in `--danger-text`, and the field
  gets a `--danger` outline. A summary at the top is not used — the forms are
  short enough that the first error can simply be scrolled to.
* On submit failure, scroll to the first invalid field and focus it.
* The primary action is a `BottomAction`; `Bekor qilish` is the back chevron,
  not a second button.
* **Unsaved changes:** leaving a dirty form via back shows a `Sheet`:
  `Saqlanmagan o‘zgarishlar bor. Chiqilsinmi?` · `Chiqish` / `Qolish`. It is
  shown only when something was actually typed.

### E.6 Feedback

**`Toast`** — bottom, above the tab bar, 4 seconds, one at a time, dismissible.
*Tones:* `success` · `error` · `info`. Used for: sign-out, password changed,
request sent, request cancelled, a new version available.
A toast never carries the only copy of important information. The check-in
result is **not** a toast (§C.5) — it belongs in the card that produced it.

**`ConfirmSheet`** — title, one line of consequence, two buttons. The confirming
button carries the verb, never `OK`.
*Confirmation policy for the whole app* — exactly three actions confirm:
`Chiqish` (sign out), `So‘rovni bekor qilish`, and leaving a dirty form.
Everything else, including the check-in itself, does **not** confirm: a
confirmation before every mark would double the taps of the app's core action
twice a day, and the mark is reversible in the sense that matters (HR can see
and correct it).

**`InlineMessage`** — a tinted block with an icon, a title and a body, inside a
card. This is what §C.5's rejections render into. *Tones:* `info` · `warning` ·
`danger` · `success`.

**`EmptyState`** — icon, title, one line, and an action button when an action
exists.

**`Skeleton`** — grey blocks matching the shape of what is loading.

### E.7 Install and update

**`InstallCard`** — Android's `beforeinstallprompt` card, and the iOS
"Share → Add to Home Screen" hint. One component, two variants (§G.4).
*States:* `android-prompt` · `ios-hint` · `hidden`.

**`UpdateToast`** — "a new version is ready", with `Yangilash` (§G.5).

### E.8 Icons

> **AMENDED 2026-10-20**: seven request-type icons added (shuffle, calendar-edit, person-arrows, map-pin, briefcase-clock, person-minus, umbrella) — `app/src/ui/icon.tsx`.

One icon set, outline style, 24 px default, stroke 1.5 — a family that matches
SF Symbols in weight. The fifteen icons the app actually uses:

```
home · calendar · document-text · user           (tab bar)
arrow-right-on-rectangle · arrow-left-on-rectangle  (kirish / chiqish)
map-pin · clock · chevron-left · chevron-right
check-circle · exclamation-triangle · x-circle · information-circle
cog
```

No icon is ever the only label for an action (§H.3).

### E.9 Universal states — defined once, for every data view

Every screen and every card that reads data implements all four. There is no
screen-specific variation; if a screen wants a different empty state, it gets
different *text*, not a different pattern.

| State | Pattern |
|---|---|
| **Loading** | **Skeleton, not a spinner**, wherever the shape of the result is known — which is every screen in this app. The skeleton occupies the final layout's height so nothing jumps under a moving thumb. A spinner is used in exactly one place: inside a `busy` button, where there is no shape to preview. First-ever load of a screen shows skeletons; a background refetch of already-shown data shows nothing at all (no spinner, no dimming). |
| **Empty** | `EmptyState`: icon, one title, one sentence, and **the action that resolves it** when one exists. `So‘rovlar` empty → `Hali so‘rov yo‘q` / `Yo‘qlik uchun so‘rov yuboring.` + `So‘rov yuborish`. A month with no data → `Bu oyda ma’lumot yo‘q` / `Boshqa oyni tanlang.` + nothing, because the month arrows are already on screen. |
| **Error** | `InlineMessage` in `danger` tone **in place of the card's content, not in place of the screen**: the header, the tab bar and the other cards stay. Title, one sentence, and a `Qayta urinish` button that refetches only that query. A failed check-in is the exception with its own error language (§C.5). |
| **No permission** | **Shown, disabled, with the reason** — never hidden. The only permission an employee has is `mobile_check_in_allowed`, and the only control it gates is the check-in button, which renders as state 4 in §C.2. Hiding the button would produce a support call ("my app has no button") that a disabled button with one sentence answers by itself. |

A fifth state, specific to this app: **Stale** — cached data shown while
offline. Rendered as a bar at the top of the screen, not a takeover:
`Internet yo‘q. {{time}} dagi ma’lumot.` (§G.2).

---

## F. Compatibility constraints, formatting and vocabulary

The app must run on **every phone in the client's hands**, including old ones.
That is a design input, not an afterthought: it decides the CSS we may write,
the build target, and in one case the framework version.

### F.1 The stated floor

| Platform | Floor | Why |
|---|---|---|
| iOS / iPadOS Safari | **12.2** | Service workers arrived in 11.3; 12.2 is the oldest version still found in the wild that runs a PWA without known SW bugs. Below this the app shows the gate screen (F.5). |
| Android Chrome | **80** | Chrome updates independently of Android, so an Android 7 phone usually carries a recent Chrome. 80 is a safe floor for optional chaining after transpilation. |
| Samsung Internet | **12** | Chromium 79-based; treated as Chrome 80. |
| Android WebView / in-app browsers | best effort | Telegram's in-app browser cannot install a PWA. The sign-in screen detects it and shows `Ilovani Chrome yoki Safari’da oching`. |

Everything below is derived from that table. Where a feature is above the floor
it is either not used or used as progressive enhancement inside `@supports`.

### F.2 CSS baseline

**Forbidden outright** — these are above the floor and would break layout, not
just polish:

| Feature | Available from | Verdict |
|---|---|---|
| `:has()` | Safari 15.4 | **no** |
| container queries (`@container`) | Safari 16 | **no** — the HR timesheet uses them; this app does not |
| `subgrid` | Safari 16 | **no** |
| `color-mix()` | Safari 16.2 | **no** — this is why every token in §D is a literal hex, even though the HR app computes its tints |
| `@layer` | Safari 15.4 | **no** |
| `@property` | Safari 16.4 | **no** |
| `dvh` / `svh` / `lvh` | Safari 15.4 | **enhancement only**, see below |
| `aspect-ratio` | Safari 15 | **no** — the calendar uses a fixed cell height instead |
| `inset` shorthand | Safari 14.1 | **no** — write `top/right/bottom/left` |
| flexbox `gap` | Safari 14.1 | **avoid**, see below |
| `:focus-visible` | Safari 15.4 | see §H.2 |
| `overscroll-behavior` | Safari 16 | **enhancement only** |

> ⚠️ **Tailwind CSS v4 cannot be used in this app.** It requires Safari 16.4 /
> Chrome 111 by design — it is built on `@property`, cascade layers and
> `color-mix()`, three of the four items forbidden above. The HR app is on
> Tailwind 4 and stays there; this app uses **Tailwind 3.4** with its own config
> in its own directory, or hand-written CSS with custom properties. This is the
> single strongest technical reason the two apps are separate builds (§0.1), and
> it should be confirmed by the architect before `build-frontend` starts.

**Height and the iOS toolbar.** The app does **not** use a full-height
`overflow: hidden` shell. The document scrolls normally; the tab bar is
`position: fixed` at the bottom and the scroll container carries bottom padding
equal to the tab bar plus the safe area. This sidesteps the entire
`100vh`-is-wrong-on-iOS problem instead of patching it. `100dvh` may be used
only inside `@supports (height: 100dvh)` and only for a bottom sheet's maximum
height.

**Spacing between stacked elements** uses adjacent-sibling margins
(`> * + * { margin-top: … }`, i.e. Tailwind 3's `space-y-*`), **not** flexbox
`gap`, which is missing below Safari 14.1 and would silently collapse every
vertical rhythm in the app on an iPhone running iOS 13. `gap` is allowed in
**grid** layouts, where it has been supported since Safari 10.1 — which is what
the calendar uses.

**Safe areas.** `<meta name="viewport" content="width=device-width,
initial-scale=1, viewport-fit=cover">` — `viewport-fit=cover` is required or
`env()` returns zero. Then:

```
tab bar:  padding-bottom: calc(8px + env(safe-area-inset-bottom, 0px))
screens:  padding-bottom: calc(80px + env(safe-area-inset-bottom, 0px))
header:   padding-top:    calc(12px + env(safe-area-inset-top, 0px))
sheets:   padding-bottom: calc(16px + env(safe-area-inset-bottom, 0px))
```

The `, 0px` fallback matters: browsers without `env()` support must not receive
an invalid `calc()` and drop the whole declaration.

**Tap feedback.** `-webkit-tap-highlight-color: transparent` globally — but
only together with an explicit `:active` state on every interactive element.
Removing the platform's feedback without replacing it makes the app feel broken
on a slow connection, which is precisely when the employee taps twice.

### F.3 JavaScript baseline

* Build target: `['es2017', 'safari12', 'chrome80']`. esbuild lowers optional
  chaining, nullish coalescing and class fields; nothing else is assumed.
* **No top-level `await`.**
* **Banned APIs** (Safari 15.4+ or later, and all easily avoided):
  `crypto.randomUUID`, `structuredClone`, `Array.prototype.at`, `Object.hasOwn`,
  `Array.prototype.findLast`, `ResizeObserver` (Safari 13.4).
* `navigator.permissions.query({ name: 'geolocation' })` is **Safari 16+**, so
  it is treated as optional everywhere; the `localStorage` fallback in §C.4
  exists exactly for this.
* `beforeinstallprompt` is Chromium-only and is feature-detected, never assumed.
* **React 18.3**, not 19 — it matches the project's default stack and does not
  require anything above the floor. No `use()`, no server components.
* Routing: the route table in §B.4 is fixed; the library is the architect's
  call, but it must support history `replace` for tab switches and manual
  scroll restoration. The HR app's TanStack Router setup is **not** reused.
* Date and time: **no `Intl` for the Uzbek locales**. Browsers mis-handle
  `uz-Latn-UZ` and `uz-Cyrl-UZ` tags and will silently emit the wrong separator
  — the HR app already formats by hand for this reason (`shared/i18n/format.ts`).
  This app gets its own `src/lib/format.ts` with the same rules, copied by
  value.
* Native `<input type="date">` and `type="time"` render very differently across
  the fleet, so they are used only *inside* our own `Sheet`, where a poor native
  picker is contained and can be swapped for scrolling columns per platform.

### F.4 Inputs must not zoom

Every `input`, `textarea` and `select` has a computed font-size of **at least
16 px**. Below 16 px, iOS Safari zooms the page on focus and does not zoom back
out, leaving the employee on a horizontally panned screen with a keyboard up.

This is why `--t-callout` (16 px) is listed in §D.7 as "every form input,
without exception". It is not a style choice.

The `maximum-scale=1` / `user-scalable=no` viewport workaround is **forbidden**:
it disables pinch-zoom for everyone, which is an accessibility failure, and
iOS 10+ ignores it anyway.

### F.5 Layout from 320 px upward

One column at every width. Content is capped at **480 px** and centred; beyond
that the page background fills the rest.

| Width | Behaviour |
|---|---|
| **320 px** (iPhone SE 1 / 5s) | gutter 12 px; calendar cell ~40 px wide × 44 tall (the documented exception in §D.7); tab labels at 11 px fit in 80 px slots; hero numerals drop from 34 px to 28 px |
| **360–430 px** (the bulk of the fleet) | gutter 16 px; calendar cell ≥ 44 px square; everything at its declared size |
| **> 480 px** (tablet, desktop browser) | the column stops growing and centres; no second layout, no sidebar |

Rules that keep it true:
* No fixed pixel widths on anything that holds text.
* `min-width: 0` on every flex child that contains text, or long words push the
  layout sideways.
* `overflow-wrap: anywhere` on server-supplied strings — a site name, an absence
  type, an HR decision note. These are user-entered and can be a single
  40-character word.
* Two-line wrap is allowed in list rows; truncation with `…` is allowed only on
  the *secondary* line, never on a primary label. §D.1 defect 7.
* `overflow-x: hidden` on the shell is **not** allowed as a fix — it hides the
  symptom and makes the real overflow impossible to find.

**The gate screen.** If `serviceWorker`, `geolocation` or `localStorage` is
missing, the app renders a single static screen instead of booting:
`Brauzeringiz eskirgan` / `Ilova ishlashi uchun Safari yoki Chrome’ning yangi
versiyasi kerak. Turniketdan o‘ting yoki HR bo‘limiga murojaat qiling.`
A half-working app on an unsupported phone generates worse support calls than a
clear refusal.

### F.6 Formatting — one rule, one helper

All of it lives in `src/lib/format.ts` in this app. Every value is rendered in
**Tashkent time (UTC+5)**; the wire format stays UTC. This is the project's
standing rule and the single most repeated bug in the module's history.

| Kind | Rule | uz-Latn | en |
|---|---|---|---|
| date, numeric | fixed, locale-independent | `22.09.2026` | `2026-09-22` |
| date, long | day, month name, weekday | `22-sentabr, seshanba` | `22 September, Tuesday` |
| month title | month name + year | `Sentabr 2026` | `September 2026` |
| time | 24 h, always two digits | `08:35` | `08:35` |
| duration | hours then minutes, minutes alone under an hour | `7 soat 12 daq.` · `48 daq.` | `7 h 12 min` |
| late | minutes | `12 daqiqa kechikdi` | `12 minutes late` |
| distance | metres under 1 km, then one decimal | `420 m` · `3.4 km` | same |
| accuracy | signed, metres | `±35 m` | same |
| number | space as the thousands separator | `1 250` | `1,250` |

* **No relative times.** Never `2 soat oldin`. This app is about exact times,
  and a relative time rendered from cached data while offline is simply false.
* **No money formatter.** There is no money anywhere in this app, and adding
  the helper would invite a payroll screen we have no data for.
* Weekday and month names are **translated strings in the locale files**, not
  `Intl` output (§F.3).

### F.7 Label vocabulary — one word per concept

Twelve screens must not invent five words for one thing. These are the default
`uz-Latn` values; the other three locales follow the same key. Where the HR web
app already has a word for a shared record, **the HR word wins**, so an employee
and an HR officer discussing the same row use the same noun.

**Actions**

| Concept | Word |
|---|---|
| save | `Saqlash` |
| cancel an action / close a sheet | `Bekor qilish` |
| submit a form | `Yuborish` |
| confirm | `Tasdiqlash` |
| close | `Yopish` |
| back | `Orqaga` |
| retry | `Qayta urinish` |
| refresh data | `Yangilash` |
| sign in | `Kirish` |
| sign out | `Chiqish` |
| create a new request | `So‘rov yuborish` |
| cancel my request | `So‘rovni bekor qilish` |
| see the full list | `Barchasi` |
| select from a sheet | `Tanlang` |
| continue | `Davom etish` |

**Nouns (aligned with the HR app)**

| Concept | Word | Note |
|---|---|---|
| an attendance mark / crossing | `O‘tish`, pl. `O‘tishlar` | HR's word; Verifix says `Qaydnoma`, we do not |
| entering | `Kirish` | also the sign-in verb — context separates them and both are correct Uzbek |
| leaving | `Chiqish` | also sign out, same note |
| the act of marking from the phone | `Belgilash` | Verifix's word, kept |
| terminal | `Turniket` | HR's word |
| this app as a mark source | `Mobil ilova` | must match the synthetic device row (SPEC §D5) exactly |
| work site | `Obyekt` | matches `Ma’lumotnomalar → Obyektlar` |
| schedule | `Ish jadvali` | |
| break | `Tanaffus` | |
| absence request | `So‘rov`, pl. `So‘rovlar` | |
| absence kind | `Yo‘qlik turi` | |
| my note on a request | `Izoh` | |
| HR's note on a decision | `Qaror izohi` | |
| month calendar | `Taqvim` | the employee's word; HR calls its grid `Tabel` |

**Statuses** — exactly the words in §D.4, which are exactly the HR app's
(`attendance.statusPresent` etc.): `Ishladi` · `Kelmadi` · `Sababli` ·
`Dam olish kuni` · `Dam olish kuni ishladi` · `Chiqish yo‘q` · `Kirish yo‘q` ·
`Jadval yo‘q` · `Kutilmoqda` · `Tasdiqlangan` · `Rad etilgan` ·
`Bekor qilingan`.

**Forbidden.** Do not introduce: `Ok`, `Yubor`, `Saqla` (imperative short
forms), `Qaydnoma`, `Otmetka`, `Check-in`, `Login`, or any abbreviation not in
this table. Every button label is a full word or phrase — §D.1 defect 7.

**Mixed scripts.** A server-supplied value (an absence type, a site name) is
rendered as it is stored, even when it is Cyrillic inside a Latin interface —
we do not transliterate data. It is never interpolated into the middle of a
translated sentence; it sits in its own labelled field or in quotation marks, so
the sentence stays readable in the chosen locale. This is the fix for §D.1
defect 4.

---

## G. Offline and the PWA shell

### G.1 Scope and the deny-list

Service worker scope is **`/ilova/`** and nothing wider (SPEC §G). Every route
outside it is on the worker's navigation deny-list, so the HR app is never
cached by a worker it did not ask for and never served a stale bundle. This is
worth restating because it is a one-line configuration mistake with a
production-sized consequence: an HR user visiting `/ilova` once would otherwise
pick up a worker that then intercepts their HR navigations.

```
scope:                    /ilova/
navigateFallback:         /ilova/index.html
navigateFallbackDenylist: everything not starting with /ilova/
```

### G.2 What is cached, and what is never cached

| Resource | Strategy | Why |
|---|---|---|
| app shell: HTML, JS, CSS, icons, manifest | **precache** | the app must open with no network |
| `GET /api/me` | **network-first**, 7-day fallback | name and photo do not change; showing them offline is free |
| `GET /api/me/attendance?year&month` | **network-first**, 7-day fallback, keyed by month | opening the calendar on the metro should show last week |
| `GET /api/me/requests` | **network-first**, 1-day fallback | |
| `POST /api/me/check-in` | **never cached, never queued, never retried in the background** | §C.6 — the server stamps the time, so a delayed mark is a wrong mark |
| `POST /api/employee-auth/*` | never cached | |
| employee photo | cache-first, 30 days | |

**No web fonts** are precached because none are used (§D.7). The whole precache
is the shell and the icons.

**The stale bar.** When a screen renders from the cache, a bar sits at the top:
`Internet yo‘q. {{time}} dagi ma’lumot.` It is a bar and not a full-screen
takeover, because cached attendance is genuinely useful and hiding it behind an
"offline" illustration would be worse than not caching at all. The bar
disappears by itself when the connection returns and the refetch succeeds.

**With no network at all**, the app still opens, the tab bar works, all four
tabs render their cached content or their empty states, and exactly one thing is
disabled: the check-in button, state 9 in §C.2, with a sentence saying why.

### G.3 The offline screen, in full

There is no separate offline *screen*. There are three offline *states*:

1. **Shell cached, data cached** → the stale bar plus real content.
2. **Shell cached, no data ever cached** (first run happened offline, or a month
   never visited) → the normal `EmptyState` pattern with offline wording:
   `Internet yo‘q` / `Ulanganingizda ma’lumot yuklanadi.` + `Qayta urinish`.
3. **Shell not cached** (the very first visit is offline) → the browser's own
   offline page. We cannot do better and we do not try to.

### G.4 Install

**Android (Chromium).** Capture `beforeinstallprompt`, prevent the mini-infobar,
and store the event. Show `InstallCard` on `Asosiy`:
> **Ilovani o‘rnating**
> Bosh ekrandan bir bosishda oching. `O‘rnatish` · `Keyinroq`

Timing rules, so it is an offer and not a nag: never in the first session; only
from the second successful sign-in onward; dismissible; if dismissed, not shown
again for **7 days**; never shown again after `appinstalled` fires. A permanent
entry stays in `Sozlamalar → Ilova haqida` for anyone who dismissed it.

**iOS.** There is no prompt API; the user must do it by hand. When the UA is
iOS Safari and `navigator.standalone !== true`, show the same card with the
iOS variant:
> **Bosh ekranga qo‘shing**
> Pastdagi ⎋ (`Ulashish`) tugmasini bosing → `Bosh ekranga qo‘shish`.

with the Share glyph drawn inline in the sentence, because the word for it
differs between iOS language settings and the icon is what the user will
actually look for. Same timing rules. Not shown inside Telegram's or
Instagram's in-app browser, where the option does not exist — there the
sign-in screen shows `Ilovani Safari’da oching` instead.

**Why installing matters here, beyond convenience.** Two real effects, both
worth telling the employee about only indirectly (through the card's existence,
not its text):
* An installed iOS web app keeps its **own** location permission grant, separate
  from Safari's. An employee who denied location in Safari can recover simply by
  installing (§C.7 says this, in the "if you cannot grant it" block).
* In Safari, script-writable storage can be evicted after **7 days without
  interaction**, which signs the employee out. Installed home-screen web apps
  are not subject to the same cap. Either way, a lost token is treated as an
  ordinary sign-in, never as an error: the sign-in screen after eviction shows
  no scary message, just the form with the phone number prefilled.

**Manifest**: `name` `Bonvi`, `short_name` `Bonvi`, `start_url` `/ilova`,
`scope` `/ilova/`, `display` `standalone`, `orientation` `portrait`,
`theme_color` and `background_color` from `--bg-elevated` of the **light**
theme (iOS does not switch the splash by system theme; a dark splash flashing
before a light app is worse than the reverse). Icons at 192, 512 and a 512
maskable, plus `apple-touch-icon` at 180 — iOS reads the link tag, not the
manifest, on older versions.

### G.5 New version available

`vite-plugin-pwa` in `prompt` mode — never `autoUpdate`.

* When a new worker is waiting, `UpdateToast` appears at the bottom:
  `Yangi versiya tayyor` · `Yangilash`. Dismissible.
* `skipWaiting` fires **only** on an explicit tap, and then the page reloads.
* It is **never** fired while a check-in request is in flight, and the toast is
  suppressed entirely while the check-in card is in state 5 or 6 (§C.2).
  Reloading the app between "sent" and "answered" would produce the ambiguous
  case in §C.5 for no reason at all.
* A permanent fallback lives in `Sozlamalar → Ilova haqida`: the current version
  string and a `Yangilash` row, enabled only when an update is waiting. This is
  the row a support call can be talked through.

---

## H. Accessibility

Not a separate pass. Three of these four items are already load-bearing for the
ordinary user on a phone in the sun.

### H.1 Contrast

| Content | Target |
|---|---|
| body text, labels, times | **≥ 4.5:1** (WCAG AA) |
| large text — ≥ 24 px, or ≥ 19 px bold | ≥ 3:1 |
| icon-only control glyphs, input outlines, the focus ring, the 3 px late bar | **≥ 3:1** (WCAG 1.4.11) |
| decorative tints (status cell fills) | no target — they never carry meaning alone |

The token table in §D.2–D.4 is built to satisfy this: white on `--accent` is
4.70:1, white on `--danger` is 5.38:1, and text on every status tint is `--fg`,
which is 16.9:1 on the dark card and 17.4:1 on the light one. **Colour is never
the only carrier of meaning** — every status is a word, every direction is a
word, and the late bar is accompanied by the minutes in text.

### H.2 Focus

* Ring: `2px solid var(--ring)`, `outline-offset: 2px`. Never `outline: none`
  without a replacement.
* Applied via `:focus-visible` where supported. Below Safari 15.4 the same rule
  is applied on `:focus`, which means a tapped button also shows the ring on an
  old iPhone. That is accepted: a visible ring on tap is a cosmetic flaw, an
  invisible focus for a keyboard user is a defect.
* Focus order follows the DOM, which follows the reading order in §A.5. No
  positive `tabindex` anywhere.
* Opening a `Sheet` moves focus into it and traps it; closing returns focus to
  the control that opened it.
* The forced password screen focuses the first field on mount. No other screen
  autofocuses — an autofocused field on a phone throws the keyboard up over the
  content.

### H.3 Screen reader labels for icon-only controls

Every icon-only control carries an `aria-label`. The list is short enough to
enumerate, so there is no excuse for a missing one:

| Control | `aria-label` (uz-Latn) |
|---|---|
| back chevron | `Orqaga` |
| previous month | `Oldingi oy` |
| next month | `Keyingi oy` |
| profile avatar in the header | `Profil` |
| close a sheet | `Yopish` |
| dismiss the install card | `Yopish` |
| tab bar items | the visible label is the accessible name — the tabs are not icon-only |

Structural labels:
* The tab bar is `role="tablist"` with `aria-current="page"` on the active item.
* Each calendar day is a `button` whose accessible name is the whole fact:
  `22-sentabr, Ishladi, 08:35–12:33, 12 daqiqa kechikdi`. Today additionally
  carries `aria-current="date"`; the selected day carries `aria-pressed="true"`.
  The grid is **not** given `role="grid"` — a list of well-labelled buttons is
  navigable by every screen reader we can test on, and a half-implemented grid
  role is worse than none.
* The legend under the calendar is real text, so the colour meanings are
  available without sight.
* The check-in card's result line is `role="status"` `aria-live="polite"` for a
  success, and `aria-live="assertive"` for a rejection — a rejection means the
  employee is standing outside believing they are marked, and it must interrupt.
* The countdown on the disabled button is **not** live — a number announcing
  itself every second is unusable. Its `aria-label` says
  `Qayta urinish — 52 soniyadan keyin` and updates silently.

### H.4 Text, motion and language

* Type is in `rem` (§D.7), so the phone's text-size setting scales the app.
  Layouts must survive 200 % text: that is why rows wrap to two lines and
  nothing holding text has a fixed width (§F.5).
* `prefers-reduced-motion: reduce` turns every transition instant and removes
  the sheet's slide.
* `<html lang>` is set from the active locale (`uz-Latn`, `uz-Cyrl`, `ru`, `en`)
  and updated when it changes, so the screen reader picks the right voice.
  A Cyrillic server value inside a Latin screen is wrapped in an element with
  its own `lang` (§F.7).
* Touch targets: §D.8. The one 320 px exception is on a non-essential control —
  the calendar can also be driven by the month arrows and the day panel.

---

## I. Open items for the architect

Five decisions this document deliberately does not make, each with a
recommendation.

1. **Separate build vs. separate route tree.** §0.1. The owner's instruction for
   this wave is a separate build; SPEC §G says otherwise. §F.2 (Tailwind 4 will
   not run on the target phones) is an independent argument for the separate
   build, and is probably decisive.
2. **[GAP-1] the per-day marks list** on `GET /api/me/attendance`. §A.4.
   Without it the `O‘tishlar` card degrades to first-in/last-out, and the
   structural mirror of Verifix's signature screen is weakened.
3. **[GAP-2] the check-in response shape** — it must return the accepted mark's
   server `occurredAt`, `direction` and `workSiteName`. This one is a hard
   requirement, not a preference: the success state shows the server's time
   (§C.2 state 7).
4. **Absence-type catalog on the employee side.** `POST /api/me/requests` needs
   the list of active `absence_types` to populate the picker. The SPEC's
   `/api/me/*` table does not include it. Recommended: return it inside
   `GET /api/me`, since it is small, rarely changes, and the app already caches
   that response.
5. **Router library.** §F.3. The route table is fixed; the library is not.

---

## Summary for `build-frontend`

* **12 screens** (10 after the 2026-09-27 removal of the two password screens), listed in §A.3. Four tab roots, three auth screens, five
  pushed. Nothing else exists.
* **4 tabs**: `Asosiy · Taqvim · So‘rovlar · Profil`, in that order. No news, no
  payroll, no reports, no module grid, no notification bell.
* **One primary action in the whole app**, specified state by state in §C.
* **Four universal states** (§E.9) apply to every data view, with the same
  pattern everywhere: skeleton, empty-with-an-action, inline error with retry,
  and disabled-with-a-reason for no permission.
* **16 components** (§E), in this app's own `src/ui/`. Zero imports from the HR
  app.
* **Tokens are literal hexes** (§D). No `color-mix()`, no Tailwind 4, no
  container queries, no `:has()`, no flexbox `gap`, 16 px inputs, safe-area
  insets everywhere, 320 px upward.
* **All strings through i18n in four locales** — `uz-Latn`, `uz-Cyrl`, `ru`,
  `en` — with the vocabulary in §F.7 and no synonyms.
