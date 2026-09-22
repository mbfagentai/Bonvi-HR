# INTAKE — Verifix employee mobile app (screenshot record)

Source material: 13 screenshots of the Android app `uz.datalab.verifix_hr`, app
version **2.1.339**, supplied by the client on 2026-09-22.

Purpose of this document: a factual, citable record of what the screenshots show,
detailed enough that UX and frontend work can proceed without reopening the images.
**This document does not propose an implementation.** Everything in sections 1–9 is
read directly off the pixels. Everything concluded rather than read is isolated in
section 11. Everything absent is listed in section 12.

All app labels are quoted **verbatim**. Uzbek Latin, Uzbek Cyrillic and Russian
appear mixed in the source; that mixing is preserved here exactly as seen.

---

## 1. Inventory

Folder: `/Users/abboskhan/Desktop/verifix images/`
All files: JPEG, 1080 × 2400 px, portrait, Android status bar included.
All 13 were readable; none were blurred, cropped or corrupt.

| ID | File name | Size | Device clock | Screen shown |
|----|-----------|------|--------------|--------------|
| S01 | `Screenshot_2026-09-22-15-13-48-644_uz.datalab.verifix_hr.jpg` | 333 KB | 15:13 | "Asosiy" — top of scroll |
| S02 | `Screenshot_2026-09-22-15-13-51-746_uz.datalab.verifix_hr.jpg` | 398 KB | 15:13 | "Asosiy" — scrolled to bottom |
| S03 | `Screenshot_2026-09-22-15-13-56-161_uz.datalab.verifix_hr.jpg` | 412 KB | 15:13 | "Taqvim" — top of scroll |
| S04 | `Screenshot_2026-09-22-15-13-59-966_uz.datalab.verifix_hr.jpg` | 357 KB | 15:13 | "Taqvim" — scrolled, mid |
| S05 | `Screenshot_2026-09-22-15-14-02-411_uz.datalab.verifix_hr.jpg` | 329 KB | 15:14 | "Taqvim" — scrolled to bottom |
| S06 | `Screenshot_2026-09-22-15-14-06-881_uz.datalab.verifix_hr.jpg` | 245 KB | 15:14 | "Profil" — tab root |
| S07 | `Screenshot_2026-09-22-15-14-11-607_uz.datalab.verifix_hr.jpg` | 327 KB | 15:14 | "Profil" — employee detail, top |
| S08 | `Screenshot_2026-09-22-15-14-13-905_uz.datalab.verifix_hr.jpg` | 300 KB | 15:14 | "Profil" — employee detail, scrolled |
| S09 | `Screenshot_2026-09-22-15-14-15-520_uz.datalab.verifix_hr.jpg` | 213 KB | 15:14 | "Profil" — employee detail, bottom |
| S10 | `Screenshot_2026-09-22-15-14-22-646_uz.datalab.verifix_hr.jpg` | 239 KB | 15:14 | "Sozlamalar" — top |
| S11 | `Screenshot_2026-09-22-15-14-24-375_uz.datalab.verifix_hr.jpg` | 258 KB | 15:14 | "Sozlamalar" — scrolled to bottom |
| S12 | `Screenshot_2026-09-22-15-47-18-101_uz.datalab.verifix_hr.jpg` | 237 KB | 15:47 | Bottom sheet "So'rov turini tanlang" over "Asosiy" |
| S13 | `Screenshot_2026-09-22-15-47-23-771_uz.datalab.verifix_hr.jpg` | 222 KB | 15:47 | "Ish joyida yo'qlik so'rovi" — create form |

Device chrome present in every shot (not part of the app): clock, network-speed
readout (e.g. `167 KB/s`), VPN/link glyph, `4G`, two signal meters, battery
(83 % in S01–S11, 78 % in S12–S13). Battery level and clock confirm S12/S13 were
captured ~33 minutes after the rest, in a separate session.

The account shown belongs to a real person (name, photo, phone number, home
village, date of birth, employee number). **Treat these screenshots as personal
data.**

---

## 2. Screen map

Four bottom-tab roots, three pushed (full-screen) routes, one modal bottom sheet.

```
Bottom tab bar (4 items, always in this order)
├── "Asosiy"        (home icon)            → S01, S02
├── "Kalendar"      (calendar icon w/ "1") → S03, S04, S05   [screen title is "Taqvim"]
├── "Yangiliklar"   (scroll/document icon) → NEVER SHOWN
└── "Profil"        (person icon)          → S06
        ├── push → "Profil" employee detail   → S07, S08, S09   (via "Ma'lumotlar")
        ├── push → "Sozlamalar"               → S10, S11
        ├── push → "Xavfsizlik"               → NEVER SHOWN
        ├── push → "Mening kuzatuvlarim"      → NEVER SHOWN
        └── push → "Yordam"                   → NEVER SHOWN

Modal bottom sheet "So'rov turini tanlang"  → S12   (opened from "So'rov yaratish")
        └── push → "Ish joyida yo'qlik so'rovi" → S13  (chosen type "Ish joy. yo'q.")
```

Pushed routes and the request form **have no bottom tab bar**. The modal sheet
covers the tab bar.

Note the label/title mismatch: the tab is labelled **"Kalendar"**, the screen it
opens is titled **"Taqvim"**.

---

## 3. Screen-by-screen record

### 3.1 "Asosiy" (home tab) — S01 (top), S02 (scrolled)

**Header** (sticky; content scrolls underneath it — in S02 the header stays
opaque over the scrolled list, separated by a hairline):
- Large title, left: `"Asosiy"`
- Subtitle under the title, grey: `"Ish kuni - 22 Sentabr 2026 (sesh)"`
- Right: outlined bell icon with a **dark-red rounded-square badge** reading `11`.
  The badge overlaps the bell's upper-right. Badge digits are near-white.

**Card 1 — schedule (no "Barchasi" link)**
- Header row: a **solid blue rounded pill/capsule glyph** (not an outline icon)
  + label `"Jadval"`; right-aligned grey value `"08:30 - 18:00"`
- Body, two columns split by a thin vertical divider:
  - Left: small **blue** down-triangle + big time `"08:35"`
  - Right: small **red** up-triangle + big time `"12:33"`
  - No captions under these two times on this screen.

**Card 2 — "Qaydnoma" (log) — S01**
- Header row: blue target/crosshair icon + `"Qaydnoma"`; right, blue text link `"Barchasi"`
- Three rows, newest first:

  | Leading icon | Title | Sub-label | Trailing time | Trailing icons |
  |---|---|---|---|---|
  | arrow-into-bracket, **lime/olive green** | `"Kirish"` | `"Офис"` (Cyrillic) | `14:22` | filled circular face glyph + map-pin outline |
  | arrow-out-of-bracket, **dark red** | `"Chiqish"` | `"Офис"` | `12:33` | same pair |
  | arrow-into-bracket, **lime/olive green** | `"Kirish"` | `"Офис"` | `10:04` | same pair |

  The two trailing icons sit on a second line under the time, right-aligned, and are
  dim grey. The face glyph reads as a face/avatar (face-recognition capture); the pin
  reads as location. Neither has a caption.
  Only 3 rows are shown; the `08:35` entry from Card 1 is not among them.

**Section header (not a card)**
- Left: `"Modullar"` (white); right: `"Hammasi"` + right-chevron (white, not blue)

**Module tiles — a row of four equal square tiles, dark card fill, monochrome grey
outline icons above a grey label:**

| # | Icon | Label |
|---|---|---|
| 1 | running person | `"Qatnashish"` |
| 2 | clipboard with `!` | `"So'rovlar"` |
| 3 | stack of banknotes | `"To'lov"` |
| 4 | open book | `"Hisobotlar"` |

All four tiles look identical in weight; none is highlighted or disabled-looking.

**Card 3 — "So'rovlar" (requests) — visible in full only in S02**
- Header row: blue list icon + `"So'rovlar"`; right, blue text link `"Barchasi"`
- Three request rows, each: bold-ish title line + date/time line + full-name line,
  with a right-aligned **coloured status word** on the title line.

  | Title (verbatim, Cyrillic) | Date / time line | Person | Status | Status colour |
  |---|---|---|---|---|
  | `"Ишхона иши билан"` | `"22 sentyabr. 2026, 12:33-14:22"` | `"Абдужалилов Аббос Равшанжон ўғли"` | `"Kutilmoqda"` | yellow |
  | `"Ишхона иши билан"` | `"16 sentyabr. 2026, 14:28-16:30"` | `"Абдужалилов Аббос Равшанжон ўғли"` | `"Kutilmoqda"` | yellow |
  | `"Ишхона иши билан"` | `"19 sentyabr. 2026, 08:30-11:57"` | `"Абдужалилов Аббос Равшанжон ўғли"` | `"Tasdiqlangan"` | blue |

  Rows are **not** in date order (22, 16, 19).
- Last element inside the card: a **full-width solid blue primary button**
  `"So'rov yaratish"`, with generous corner rounding.

**Bottom navigation**: `"Asosiy"` active (blue icon + blue label); the other three
inactive (near-white icon, grey label).

---

### 3.2 "Taqvim" (calendar tab) — S03 (top), S04 (mid), S05 (bottom)

**Header**: large title `"Taqvim"` only. **No subtitle, no bell.** Sticky (in S04/S05
the header bar stays over the scrolled content).

**Card 1 — month calendar**
- Month navigation row: square grey left-chevron button, centred grey month label
  `"Sentabr 2026"`, square grey right-chevron button. The chevron buttons have a
  lighter grey fill than the card; the month label is the **same grey as the chevrons**,
  not white.
- Weekday header, lowercase, grey, 7 columns:
  `"du"  "se"  "chor"  "pay"  "ju"  "sha"  "ya"`
  → week starts on Monday.
- 6-row day grid of rounded square tiles (~108 × 108 source px, corner radius ≈ 25 %
  of the tile). Days are zero-padded two-digit (`01`, `02` … `30`).
- Grid contents for September 2026 exactly as shown:
  - Row 1: `31` (grey, out-of-month) `01 02 03 04• 05• 06`
  - Row 2: `07 08 09 10 11 12 13`
  - Row 3: `14• 15 16 17 18 19• 20`
  - Row 4: `21 22 23 24 25 26 27`
  - Row 5: `28 29 30 01 02 03 04` (the last four grey, out-of-month)
  - (`•` marks a small dot — see §7.)

**Card 2 — today's two times**
- Two columns, no card title, no icon:
  - Left: big `"08:35"` with small grey caption `"kirish"` below
  - Right: big `"12:33"` with small grey caption `"chiqish"` below
- These are the same two values shown without captions in the "Jadval" card on
  "Asosiy".

**Card 3 — "Ish jadvali"**
- Header row: blue bar-chart icon + `"Ish jadvali"` (no "Barchasi" link)
- Two columns:
  - Left: `"08:30 - 18:00"` with caption `"ish kuni"`
  - Right: `"12:00 - 13:00"` with caption `"tanaffus"`

**Card 4 — "Qaydnoma"**
- Identical component to the "Asosiy" one: blue target icon + `"Qaydnoma"` +
  blue `"Barchasi"` link, same three rows (`Kirish 14:22`, `Chiqish 12:33`,
  `Kirish 10:04`, all `"Офис"`, all with the face + pin glyph pair).

**Card 5 — "So'rovlar"**
- Blue list icon + `"So'rovlar"`. **No "Barchasi" link here** (unlike "Asosiy").
- Exactly one row: `"Ишхона иши билан"` / `"22 sentyabr. 2026, 12:33-14:22"` /
  `"Абдужалилов Аббос Равшанжон ўғли"` / status `"Kutilmoqda"` (yellow).
- **No "So'rov yaratish" button in this card.**

**Card 6 — "Oylik statistika Sentabr 2026"** (S05)
- Header row: blue briefcase icon + title `"Oylik statistika Sentabr 2026"`
  (the month/year is part of the title string).
- Left: a **donut chart** with a hollow centre containing the number `30`.
  Three visible segments, thick stroke, flat colours, no labels on the arcs.
- Right: a three-row legend, each row = colour dot + value line + grey caption,
  separated by hairline dividers:

  | Dot colour | Value | Caption |
  |---|---|---|
  | green (mint) | `"13 (43.3 %)"` | `"vaqtida"` |
  | yellow | `"8 (26.7 %)"` | `"kechikish"` |
  | grey-blue | `"8 (26.7 %)"` | `"qoldi"` |

- Below the chart, a two-column footer inside the same card:
  - Left: `"251 soat"` / caption `"rejaga muvofiq"`
  - Right: `"147 soat 7 min"` / caption `"ishlab chiqilgan"`
- Arithmetic as printed: 13 + 8 + 8 = **29**, not the 30 in the donut centre;
  43.3 + 26.7 + 26.7 = **96.7 %**. Recorded as seen, not corrected.

**Bottom navigation**: `"Kalendar"` active.

---

### 3.3 "Profil" (profile tab root) — S06

**Header**: large title `"Profil"` only. No back button, no trailing action.

**Card 1 — identity**
- Circular avatar photo (real photo, not initials) + name on one line:
  `"Абдужалилов Аббос"` (Cyrillic, **short form — first + last only**)
- Below, a row of two buttons filling the card width:
  - Left, **grey/neutral** button: ID-card icon + `"Ma'lumotlar"`
  - Right, **muted blue** button (dark navy fill, light blue text): `"O'zgartirish"`
    + right-chevron
  Both are pill-shaped with a large radius; they are the same height but different widths
  (the blue one is slightly wider).

**Card 2 — branch**
- Building icon (outline, white) + two-line block:
  - `"MBF"` (white, primary)
  - `"Joriy filial"` (grey, caption **below** the value — value-above-label pattern)
- Whole card looks tappable but shows no chevron.

**Card 3 — settings list** (four rows, hairline-free, all in one card)

| Icon | Label | Trailing |
|---|---|---|
| gear | `"Sozlamalar"` | right-chevron |
| padlock | `"Xavfsizlik"` | right-chevron |
| a looping/path glyph | `"Mening kuzatuvlarim"` | right-chevron |
| `Aa` glyph | `"Til"` | value text `"O'zbekcha"` (no chevron) |

**Card 4 — support** (own card, separated by a gap)
- headset icon + `"Yordam"` + right-chevron

**Destructive action**
- Full-width button, **dark desaturated maroon fill with pale pink text**:
  `"Chiqish"`

**Footer**
- Centred grey caption: `"Dasturi versiyasi 2.1.339"`

**Bottom navigation**: `"Profil"` active.

---

### 3.4 "Profil" — employee detail — S07 (top), S08 (mid), S09 (bottom)

Reached from `"Ma'lumotlar"`. Read-only.

**Header** — different pattern from the tab roots:
- Left: back chevron
- **Centred** small title `"Profil"`
- Right: a "two people" icon (no label, purpose not shown)
- Sticky: in S08/S09 content scrolls under it.

**Hero block (not a card)**
- Large circular avatar, centred
- Centred full name over two lines:
  `"Абдужалилов Аббос Равшанжон ўғли"`

**Card 1 — personal (no card title, no icon)** — value-above-label pairs:
- `"28.01.2004"` / `"Tug'ilgan kun"`
- `"Erkak"` / `"Jins"`
- `"Хамракулов Рузматжон"` / `"Rahbar"`  ← value Cyrillic, label Uzbek Latin

**Card 2 — "Kontaktlar"** (blue contact-card icon + title)
- `"+998976662675"` / `"Telefon"` — trailing: copy icon, share icon, and a
  **solid olive-green filled square button with a phone glyph** (the only
  green-filled control anywhere in the material)
- `"Ko'rsatilmagan"` / `"E-mail"` — trailing: copy, share, envelope icons, **all
  visibly dimmed/disabled** (they sit on faint rounded backgrounds)
- `"Ko'rsatilmagan"` / `"Mintaqa"` — no trailing controls
- `"Ўзбекистон тумани Ўқчи қишлоғи"` / `"Yashash manzili"` — no trailing controls
- `"Ko'rsatilmagan"` / `"Doimiy ro'yxatdan o'tgan manzili"` — no trailing controls

`"Ko'rsatilmagan"` is the app's empty-value placeholder, rendered in the same grey
as a real value, only slightly dimmer.

**Card 3 — "Ish haqida malumotlar"** (blue list icon + title)
> The title is printed **without the apostrophe**: `"Ish haqida malumotlar"`,
> while the button on the previous screen reads `"Ma'lumotlar"`. Typo is in the source.
- `"Ички бухгалтерия"` / `"Bo'lim"`
- `"Ички бухгалтерия"` / `"Jamoa"`   ← same value for both fields
- `"Менеджер"` / `"Lavozim"`
- `"10.08.2026"` / `"Qabul qilingan sana"`

**Card 4 — "Hisob raqamlari"** (blue "lines" icon + title)
- `"Ko'rsatilmagan"` / `"STIR"`
- `"Ko'rsatilmagan"` / `"ShJBPH"`
- `"Ko'rsatilmagan"` / `"JShShIR"`
- `"0000000651"` / `"Xodim raqami"` — trailing copy icon (enabled, white)

The screen ends here; empty space below the last card. **No bottom navigation.**

---

### 3.5 "Sozlamalar" — S10 (top), S11 (bottom)

**Header**: back chevron + **left-aligned** title `"Sozlamalar"`. Not centred —
a third header variant.

**Group 1 — `"Interfeys"`** (group title printed inside the card, top-left, small,
bluish-grey)
- `"Tizimdagi kabi"` (value, large) / `"mavzu"` (label, small, below) — trailing right-chevron
- `"O'zbekcha"` (value) / `"Ilova tili"` (label, below) — **no chevron**

**Group 2 — `"Xavfsizlik"`**
- `"Parolni o'zgartirish"` — no trailing control, no chevron
- `"PIN-kodni o'zgartirish"` — no trailing control; a hairline divider appears **below** this row
- `"PIN-kodni olib tashlash"` — hairline below
- `"Touch-ID bilan kiriting"` — trailing **toggle switch, OFF**: the track is dark
  maroon/red and the knob is white and sits on the **right**. (Visually this reads
  like an ON switch in a red state; there is nothing on screen that confirms which
  state it is. Recorded as ambiguous.)

Hairlines appear only between the last three rows of this group, not between the
first two — inconsistent divider usage in the source.

**Group 3 — `"Bildirishnomalar"`**
- `"Ish kunining boshlanishi va oxiri haqi…"` — **truncated with an ellipsis**;
  trailing right-chevron
- `"Boshqalar"` — trailing right-chevron

**Group 4 — `"Ilova haqida"`**
- `"2867d350-a14e-11f1-9a09-09a8bff084e8"` / `"Seriya raqami"`
- `"2.1.339"` / `"Versiyasi"`

Screen ends; empty space below. **No bottom navigation.**

---

### 3.6 Bottom sheet "So'rov turini tanlang" — S12

Opened over the "Asosiy" screen. The background screen is **dimmed** (visible:
the "Qaydnoma" tail, the "Modullar"/"Hammasi" row and the four module tiles, all
darkened; the bottom tab bar is fully covered).

- Sheet: dark panel, **rounded top corners only**, occupying ~ the lower 2/3 of
  the screen. No drag handle visible.
- Sheet title, left-aligned, plain weight, no divider under it: `"So'rov turini tanlang"`
- Eight rows, each = grey monochrome outline icon (left, fixed gutter) + label.
  Rows are tall and generously spaced; no chevrons, no dividers, no descriptions.

| # | Icon (as drawn) | Label (verbatim) |
|---|---|---|
| 1 | running person | `"Ish joy. yo'q."` |
| 2 | two crossing arrows (shuffle) | `"Kun almash."` |
| 3 | calendar with a pencil | `"Jadval o'zg."` |
| 4 | walking person with two horizontal arrows | `"Belgilash"` |
| 5 | map pin (solid) | `"Joylashuv so'rovi"` |
| 6 | briefcase with a clock | `"Qo'shimcha ish vaqti"` |
| 7 | person with a minus sign | `"Ishdan bo'shatish"` |
| 8 | beach umbrella | `"Ta'til"` |

Labels 1–3 are **abbreviated with trailing full stops** (`"Ish joy. yo'q."`,
`"Kun almash."`, `"Jadval o'zg."`) while 4–8 are spelled out. The abbreviation is
in the source, not a crop.

- Pinned at the bottom of the sheet: a **full-width solid blue button** `"Yopish"`.

Row 8 (`"Ta'til"`) is the last one rendered and sits directly above the button;
whether the list scrolls to reveal more types cannot be determined from a single
static frame.

---

### 3.7 "Ish joyida yo'qlik so'rovi" — request create form — S13

Reached by choosing `"Ish joy. yo'q."` in the sheet. Note the screen title is the
**expanded** form of the abbreviated sheet label.

**Header**: back chevron + left-aligned title `"Ish joyida yo'qlik so'rovi"`.

**Card 1 — the request fields** (single card, mixed control styles)

| # | Control type | Content shown |
|---|---|---|
| 1 | **Outlined dropdown** (rounded rect, 1 px border, caret ▾ right) | `"Soatlik"` |
| 2 | **Outlined dropdown** (same style) | `"Ишхона иши билан"` (Cyrillic) |
| 3 | Row: value-above-label + **outlined text button** on the right | value `"Ko'rsatilmagan"`, label `"sana"`, button `"Belgilash"` |
| 4 | Row: value-above-label + trailing **tap/hand icon** | value `"Ko'rsatilmagan"`, label `"Boshlanish vaqti"` |
| 5 | Row: value-above-label + trailing **tap/hand icon** | value `"Ko'rsatilmagan"`, label `"Tugash vaqti"` |

Rows 3/4 and 4/5 are separated by full-bleed hairlines; rows 1 and 2 are not.
Labels are lowercase for `"sana"` but sentence-case for `"Boshlanish vaqti"` /
`"Tugash vaqti"` — inconsistent in the source.
Three different affordances are used for three pickers on the same card: a bordered
button (`"Belgilash"`), and twice a bare hand/tap glyph.

**Card 2 — comment**
- Header row: blue speech-bubble icon + `"Izoh"`
- A single **outlined multi-line text input**, roughly 3 lines tall, placeholder
  `"Izoh matni"`. Empty.

**Footer bar**
- Separated from the content by a hairline, pinned at the bottom of the screen:
  a **full-width solid blue button** `"Yuborish"`.

No character counter, no required-field markers, no attachment control, no
approver picker, no validation messages are visible. **No bottom navigation.**

---

## 4. Visual language

### 4.1 Palette (sampled from the JPEGs; values are as rendered, so ±1–2 per channel)

| Role | Hex | Where it appears | What it means |
|---|---|---|---|
| Page background | `#10131A` | every screen behind the cards, and the bottom tab bar | canvas |
| Card / surface | `#202332` | every card, module tile, bottom sheet | one single elevation level |
| Surface +1 | `#2F3241` | `"Ma'lumotlar"` button, calendar month-nav buttons, future/out-of-month day tiles | a "quiet control" fill |
| Primary blue | `#255DCC` | `"Jadval"` pill, all card-header icons, `"Barchasi"` links, selected day `22`, active tab, `"So'rov yaratish"` / `"Yuborish"` / `"Yopish"` buttons, `"Tasdiqlangan"` status | brand / interactive / confirmed |
| Muted blue fill | `#24335A` | `"O'zgartirish"` button fill | secondary action |
| Light blue on muted | `#C2D5FF` | `"O'zgartirish"` label | — |
| Primary text | `#E2E2EE` | page titles, card titles, big times, `"Modullar"`, `"Hammasi"`, day numbers on dark tiles | highest emphasis |
| Body / secondary text | `#A9BBD1` … `#AABBCF` | list-row titles (`"Kirish"`, `"Chiqish"`), request titles, settings rows, group titles, form values | normal reading text — **noticeably bluish, not neutral grey** |
| Muted text | `#AEB4C0` / `#B0B4C0` | header subtitle, `"08:30 - 18:00"`, module tile labels, inactive tab labels | captions |
| Dim text | `#6B7889` / `#6C7888` | trailing times (`14:22`), weekday headers, month label `"Sentabr 2026"` | lowest emphasis |
| Yellow | `#FED94D` (tile) / `#F4D86B` (text) | calendar worked days, `"Kutilmoqda"` status | pending / late / "a day with data" |
| Yellow foreground | `#594200` | day number and dot **on** yellow tiles | dark brown-on-yellow, not black |
| Lime / olive green | `#B8D78B` | `"Kirish"` arrow icon, calendar day `19` tile | entry / a special day state |
| Mint green | `#06D7A0` | donut segment + legend dot for `"vaqtida"` | on time |
| Donut yellow | `#FFD167` | donut segment + legend dot for `"kechikish"` | late |
| Donut grey-blue | `#788DAA` | donut segment + legend dot for `"qoldi"` | remaining |
| Dark red | `#822A3A` | `"Chiqish"` arrow icon, notification badge, Touch-ID toggle track | exit / attention |
| Maroon fill | `#482938` | `"Chiqish"` (logout) button fill | destructive |
| Pale pink | `#E3C2C9` | `"Chiqish"` (logout) label | destructive text |
| Olive-green fill | `#7F9A2E`-ish (solid) | the call button next to `"Telefon"` | the only "success/go" filled button |
| Button label on blue | `#E3F2FF` | `"So'rov yaratish"`, `"Yuborish"`, `"Yopish"` | near-white, slightly cool |

**There is no white (`#FFFFFF`) anywhere except the toggle knob and the badge
digits.** The scheme is built on blue-tinted greys throughout.

Three distinct greens are in use for three different things (lime `#B8D78B`
for check-in and day 19, mint `#06D7A0` for the "on time" statistic, olive for
the call button). Three distinct reds likewise (`#822A3A` icon/badge, `#482938`
button fill, `#E3C2C9` text).

### 4.2 Shape and radii

- **Cards**: corner radius ≈ 38 px on a 1080-px-wide capture ≈ **14 CSS px at a
  393 pt viewport**. Same radius on every card, the module tiles and the bottom
  sheet's top corners.
- **Calendar day tiles**: ~108 × 108 source px with radius ≈ 26 px → ~**25 % of
  the tile**, i.e. a soft squircle, clearly rounder in proportion than the cards.
- **Primary buttons** (`"So'rov yaratish"`, `"Yuborish"`, `"Yopish"`): fully
  rounded / pill-like, radius roughly half the height.
- **`"Ma'lumotlar"` / `"O'zgartirish"`**: pill-shaped, same treatment.
- **Notification badge**: rounded square (a superellipse), not a circle.
- **Outlined inputs / dropdowns** (S13): moderate radius, ~10 CSS px, 1 px border
  in a grey slightly lighter than the card.
- **Avatar**: perfect circle, both in the list card and the hero.
- No visible drop shadows anywhere. Elevation is expressed **only** by the
  `#10131A` → `#202332` fill step.

### 4.3 Typography hierarchy

Sans-serif, geometric-humanist, single family throughout, weights read as
Regular and Medium only (no bold, no light-weight display face). Digits are
lining and appear tabular in the time readouts. Measured cap heights, converted
to approximate CSS px at a 393 pt viewport:

| Level | Example | ≈ CSS px | Colour | Notes |
|---|---|---|---|---|
| Page title | `"Asosiy"`, `"Taqvim"`, `"Profil"` | **25** | `#E2E2EE` | regular weight, left aligned, generous |
| Big metric | `"08:35"`, `"12:33"` | **27** | `#E2E2EE` | largest text in the app |
| List-row title | `"Kirish"`, `"Chiqish"` | **18** | `#A9BBD1` | bigger than the card title above it |
| Section label | `"Modullar"`, `"Interfeys"` | **15** | `#E2E2EE` / `#A9BBD1` | |
| Header subtitle | `"Ish kuni - 22 …"` | **15** | `#AEB4C0` | |
| "Barchasi" link | `"Barchasi"` | **17** | `#255DCC` | larger than the card title it sits beside |
| Card title | `"Jadval"`, `"Qaydnoma"` | **14** | `#E2E2EE` | |
| Module tile label | `"Qatnashish"` | **15** | `#AEB4C2` | |
| Weekday header | `"du"`, `"chor"` | **14** | `#6B7889` | |
| Month label | `"Sentabr 2026"` | **24** | `#6B7889` | large but very low contrast |
| Request title / status | `"Ишхона …"`, `"Kutilmoqda"` | **13** | `#A9BBD1` / yellow | |
| Trailing time | `"14:22"` | **13** | `#6C7888` | |
| Tab label | `"Asosiy"` | **12** | blue / `#B0B4C0` | |

Two hierarchy inversions are visible in the source and should be noticed, not
copied blindly: **`"Barchasi"` is larger than the card title next to it**, and
**list-row titles are larger than the card title above them**.

**Value-above-label** is the dominant data pattern (S06, S07–S09, S10, S13): the
value is the large, higher-contrast line and the field name is the small grey line
**underneath** it. The calendar's `"08:35 / kirish"` block and `"Ish jadvali"` use
the same pattern. In lists (`"Kirish" / "Офис"`) the order is the opposite —
label on top, detail underneath.

### 4.4 Iconography

- Two families are mixed:
  1. **Blue filled/outline "section" icons** — used only in card headers:
     target (`"Qaydnoma"`), list (`"So'rovlar"`, `"Ish haqida malumotlar"`),
     bar chart (`"Ish jadvali"`), briefcase (`"Oylik statistika"`), contact card
     (`"Kontaktlar"`), lines (`"Hisob raqamlari"`), speech bubble (`"Izoh"`).
     These are small, dense, and clearly a different set from the rest.
  2. **Monochrome grey outline icons** — everything else: module tiles, the
     request-type sheet, the profile list (gear, padlock, path, `Aa`, headset),
     the tab bar.
- Stroke weight is fairly heavy (~2.5 px at 1080 width) and consistent within
  each family.
- Semantic colouring of icons is used sparingly and only for the check-in /
  check-out arrows (lime / dark red) and the `"Jadval"` pill (blue).
- The `"Jadval"` card header uses a **solid blue capsule shape**, not a
  recognisable pictogram.
- The `"Kalendar"` tab icon contains the numeral `1` inside the calendar frame
  (a static glyph, not today's date, which is 22).

### 4.5 Spacing rhythm (source px at 1080 width; divide by ~2.75 for CSS px at 393 pt)

- Screen side margin: **45 px** (≈16 CSS px) — identical on every screen.
- Card width: 990 px (full width minus the two margins).
- Gap between stacked cards: **46 px** (≈17 CSS px) for adjacent cards; ~80 px
  where a card group changes (e.g. before the `"So'rovlar"` card on "Asosiy").
- Card inner padding: ~40 px horizontally (≈15 CSS px).
- Calendar day-tile gutter: ~22 px, tile pitch ~130 px.
- The `"Modullar"` section header sits in the gap **between** cards, not inside one.
- Rows inside list cards are tall (~150 px / ~55 CSS px), giving a very airy,
  low-density feel throughout.

### 4.6 Dark / light theme

Every screenshot is dark. `"Sozlamalar"` shows the theme is a user setting with the
value `"Tizimdagi kabi"` ("same as the system") under the label `"mavzu"`, i.e. the
app supports at least system / light / dark, but **no light screenshot was supplied
and the available options were never opened**.

What the dark shots tell us about a future light theme:
- The design leans on a **two-step surface system only** (`#10131A` canvas →
  `#202332` card, plus `#2F3241` for quiet controls). A light theme needs the same
  three steps inverted; there are no shadows to fall back on.
- Text is **blue-tinted grey, not neutral** — a light theme has to keep that tint
  or the app will look like a different product.
- The calendar's yellow `#FED94D` and lime `#B8D78B` tiles are **light fills with
  dark foregrounds already**. They will barely separate from a light canvas and
  are the first thing that needs re-toning.
- `"Kutilmoqda"` yellow text (`#F4D86B`) on a light background will fail contrast
  outright.
- The maroon logout button (`#482938` fill, `#E3C2C9` text) is a dark-only
  construction; light needs an inverted pair.

---

## 5. Status vocabulary (every status word visible in the material)

| Word (verbatim) | Colour | Where seen | Notes |
|---|---|---|---|
| `"Kutilmoqda"` | yellow `#F4D86B` | S01, S02 (×2), S04, S05 | plain coloured text, right-aligned; **no pill, no background, no icon** |
| `"Tasdiqlangan"` | blue `#255DCC` (brand blue) | S02 | same treatment |

Only these two statuses occur in the screenshots. Rejected / cancelled / in-review
states, if any, were never shown. The status is rendered as **bare coloured text on
the same line as the request title** — there is no badge component anywhere in the
material.

---

## 6. Data values observed (for later verification)

- Today in the app's world: `22 Sentabr 2026`, a Tuesday (`"(sesh)"`, and day 22
  sits in the `"se"` column).
- Work schedule: `08:30 - 18:00`; break `12:00 - 13:00`.
- Today's log: `Kirish 10:04`, `Chiqish 12:33`, `Kirish 14:22`, all location `"Офис"`;
  plus `08:35` shown as the day's first in/`"kirish"` figure.
- Monthly statistics, September 2026: donut centre `30`; `13 (43.3 %) vaqtida`;
  `8 (26.7 %) kechikish`; `8 (26.7 %) qoldi`; `251 soat rejaga muvofiq`;
  `147 soat 7 min ishlab chiqilgan`.
- Requests listed: 3 (22 Sep 12:33–14:22 pending, 16 Sep 14:28–16:30 pending,
  19 Sep 08:30–11:57 confirmed).
- Notification badge count: `11`.
- Employee record: DOB `28.01.2004`; gender `"Erkak"`; manager
  `"Хамракулов Рузматжон"`; phone `+998976662675`; address
  `"Ўзбекистон тумани Ўқчи қишлоғи"`; department and team both
  `"Ички бухгалтерия"`; position `"Менеджер"`; hire date `10.08.2026`;
  employee number `0000000651`; branch `"MBF"`.
- App: version `2.1.339`; installation serial `2867d350-a14e-11f1-9a09-09a8bff084e8`.
- Calendar for September 2026 as coloured: 20 yellow days (01–18, 20, 21),
  1 green day (19), 1 blue day (22), 8 plain-grey days (23–30), 5 out-of-month
  greyed cells (Aug 31 and Oct 01–04).

---

## 7. Calendar day-tile colour coding (S03)

| Tile fill | Number colour | Days in the shot | Reading from the material |
|---|---|---|---|
| Yellow `#FED94D` | dark brown `#594200` | 01–18, 20, 21 | every past day of the month except 19 — includes Saturdays and Sundays (06 `ya`, 12 `sha`, 13 `ya`, 20 `ya`), so it is **not** a "working day" marker |
| Lime green `#B8D78B` | near-white `#EAF2FF` | **19 only** (a Saturday) | a single distinguished past day; the same lime as the `"Kirish"` arrow |
| Blue `#255DCC` | near-white | **22 only** | today / the selected day (the content below the calendar is that day's data) |
| Dark grey `#2F3241` | light `#E0E2EF` | 23–30 | future days of the displayed month |
| Dark grey `#2F3241` | same grey, heavily faded | 31 (previous month), 01–04 (next month) | out-of-month spill cells; the fill is identical to future days, only the **text opacity** differs |

There is no legend anywhere on the screen, and the tiles carry no numbers other
than the day.

**Dot markers.** A single small dot sits in the **top-right corner of the tile**,
overlapping the day number, on exactly four days: **04, 05, 14, 19**.
- On yellow tiles (04, 05, 14) the dot is the same dark brown `#594200` as the number.
- On the green tile (19) the dot is the same near-white `#EAF2FF` as the number.
So the dot is drawn in the tile's **foreground colour**, i.e. it is one marker type,
not a colour-coded one. It is ~14 px across at 1080 width (≈5 CSS px).
The dot never appears on grey (future / out-of-month) tiles in this shot.
**What the dot means is not stated anywhere in the material.**

---

## 8. Request types (`"So'rov turini tanlang"`) — verbatim list

Reproduced from S12, in the exact on-screen order:

1. `"Ish joy. yo'q."` — running-person icon
2. `"Kun almash."` — shuffle (two crossing arrows) icon
3. `"Jadval o'zg."` — calendar-with-pencil icon
4. `"Belgilash"` — walking-person-with-arrows icon
5. `"Joylashuv so'rovi"` — solid map-pin icon
6. `"Qo'shimcha ish vaqti"` — briefcase-with-clock icon
7. `"Ishdan bo'shatish"` — person-with-minus icon
8. `"Ta'til"` — beach-umbrella icon

Sheet title: `"So'rov turini tanlang"`. Dismiss button: `"Yopish"` (full-width, blue).

Only type 1 was opened. The expanded title it leads to is
`"Ish joyida yo'qlik so'rovi"`, confirming `"Ish joy. yo'q."` is an abbreviation of
"Ish joyida yo'qlik". The expansions of `"Kun almash."` and `"Jadval o'zg."` are
**not** shown and must not be guessed.

---

## 9. Request creation form (`"Ish joyida yo'qlik so'rovi"`) — exact fields

| Order | Field label as printed | Control | Value shown | Empty-state text |
|---|---|---|---|---|
| 1 | *(no label)* | outlined dropdown with ▾ | `"Soatlik"` | — (pre-filled) |
| 2 | *(no label)* | outlined dropdown with ▾ | `"Ишхона иши билан"` | — (pre-filled) |
| 3 | `"sana"` | row + outlined button `"Belgilash"` | — | `"Ko'rsatilmagan"` |
| 4 | `"Boshlanish vaqti"` | row + hand/tap icon | — | `"Ko'rsatilmagan"` |
| 5 | `"Tugash vaqti"` | row + hand/tap icon | — | `"Ko'rsatilmagan"` |
| 6 | `"Izoh"` (card title) | outlined multi-line text area | empty | placeholder `"Izoh matni"` |

Submit: `"Yuborish"` — full-width blue button in a pinned bottom bar.

The two dropdowns arrive **pre-filled** and carry no labels at all, so what they
mean is only inferable (see §11). The values they show are `"Soatlik"` (Uzbek
Latin) and `"Ишхона иши билан"` (Cyrillic) — and `"Ишхона иши билан"` is exactly
the string that appears as the **title** of every request in the lists, which ties
dropdown 2 to the request title.

---

## 10. Inconsistencies present in the source (recorded, not corrected)

1. **Three scripts in one product.** Uzbek Latin (UI chrome), Uzbek Cyrillic
   (names, `"Ишхона иши билан"`, `"Менеджер"`) and Russian (`"Офис"`) appear on the
   same screen.
2. **Month names in two systems.** Header: `"22 Sentabr 2026"`; calendar header:
   `"Sentabr 2026"`; request rows: `"22 sentyabr. 2026"` — Uzbek `Sentabr`
   capitalised vs Russian-transliterated `sentyabr.` lowercase **with a trailing
   full stop**.
3. **Tab label ≠ screen title**: `"Kalendar"` vs `"Taqvim"`.
4. **Typo**: `"Ish haqida malumotlar"` (missing apostrophe) vs `"Ma'lumotlar"`.
5. **Three header patterns**: large left title (tab roots), small centred title
   with back + trailing icon (employee detail), left title with back
   (Sozlamalar, request form).
6. **The same `"So'rovlar"` card differs between screens**: on "Asosiy" it has a
   `"Barchasi"` link and a `"So'rov yaratish"` button; on "Taqvim" it has neither.
7. **Request list not sorted** (22 Sep, 16 Sep, 19 Sep).
8. **Donut arithmetic**: 13 + 8 + 8 = 29 against a centre value of 30; percentages
   total 96.7 %.
9. **Three picker affordances on one card** in S13 (`"Belgilash"` button, hand icon,
   hand icon).
10. **Label casing** inconsistent in S13: `"sana"` lowercase, `"Boshlanish vaqti"` /
    `"Tugash vaqti"` sentence case.
11. **Dividers used inconsistently** inside the `"Xavfsizlik"` settings group.
12. **A setting is truncated in its own row**:
    `"Ish kunining boshlanishi va oxiri haqi…"`.
13. **Typographic apostrophe is unresolvable from a JPEG.** The app renders
    `So'rovlar`, `To'lov`, `Ko'rsatilmagan`, `O'zbekcha` with a curl that could be
    U+2018, U+2019, U+02BB or ASCII `'`. ASCII `'` is used throughout this document
    as a stand-in. **Do not copy our spelling from here for a string-by-string
    comparison without confirming the codepoint.**

---

## 11. Inferred — NOT read off the screen

Each item is a conclusion, with the evidence and a confidence level.

- **The app is single-employee, self-service (not a manager view).** *High.*
  Every request in every list belongs to the logged-in person; "Profil" offers
  logout and personal settings; no approve/reject control appears anywhere.
  (The "two people" icon in the employee-detail header is the one thing that
  might contradict this — it was never opened.)
- **`"Kirish"` / `"Chiqish"` = check-in / check-out events, captured at a terminal
  with a face camera and a location.** *High.* The arrow direction, the paired
  face + pin glyphs on every row, and the clock times.
- **Blue day `22` = the selected day, and the calendar drives the whole page
  below it.** *High.* 22 is both today and the day whose log, schedule and request
  are shown underneath.
- **Yellow calendar days = days that have attendance data / are in the past.**
  *Medium.* All past days are yellow regardless of weekday, all future days are
  grey. The alternative reading — yellow = "late" — is contradicted by the
  statistic card, which says only 8 days were `"kechikish"` while 20 days are yellow.
- **Green day `19` = a day with a distinguishing state, plausibly "fully correct /
  no deviation" or "a day tied to the approved request".** *Low.* Evidence both
  ways: 19 Sep is exactly the date of the one `"Tasdiqlangan"` request, which is
  suggestive; but 19 Sep is also a Saturday, so a weekend/holiday reading cannot be
  excluded. **Do not build on this.**
- **The corner dot = "this day has something attached" (a request, a note or a
  correction).** *Low.* It appears on 04, 05, 14 and 19; 19 is the confirmed-request
  day, but 04, 05 and 14 have no counterpart anywhere else in the material, and the
  16 and 22 Sep request days carry **no** dot. Unexplained by the evidence available.
- **Dropdown 1 (`"Soatlik"`) = the duration unit of the absence (hourly vs. daily).**
  *Medium.* The word means "hourly", the form then asks for a start and an end
  **time** on a single **date**, and every listed request carries an hour range.
- **Dropdown 2 (`"Ишхона иши билан"`) = the reason, and it becomes the request
  title.** *High.* The string is identical to the title of all three requests in
  the lists.
- **`"Kutilmoqda"` → `"Tasdiqlangan"` is the happy path of a two-state (or more)
  approval flow.** *Medium.* Only these two values were observed; a rejection state
  almost certainly exists but was not seen.
- **The four `"Modullar"` tiles open the four modules whose screens were never
  shown, and `"Hammasi"` opens a full module list.** *Medium.* Standard affordance;
  the `"Hammasi"` chevron implies there are more than four modules.
- **The Touch-ID toggle is OFF.** *Low.* The knob is on the right (normally ON) but
  the track is red (normally OFF/destructive). Genuinely ambiguous — the two
  conventions contradict each other here.
- **The app's type scale is roughly 12 / 13 / 14 / 15 / 17 / 18 / 25 / 27 CSS px**
  at a 393 pt viewport. *Medium.* Derived from cap-height measurements on JPEG
  glyphs, so ±1 px.

---

## 12. Not visible in the material / open questions

### 12.1 Screens referenced by controls but never shown

| Entry point | Screen behind it | Seen in |
|---|---|---|
| Tab `"Yangiliklar"` | the whole news module | every screenshot's tab bar |
| Module tile `"Qatnashish"` | attendance module | S01, S02 |
| Module tile `"So'rovlar"` | requests module (full list) | S01, S02 |
| Module tile `"To'lov"` | payroll / payments module | S01, S02 |
| Module tile `"Hisobotlar"` | reports module | S01, S02 |
| `"Hammasi" ›` next to `"Modullar"` | the full module catalogue | S01, S02 |
| `"Barchasi"` on `"Qaydnoma"` | full attendance log | S01, S03, S04 |
| `"Barchasi"` on `"So'rovlar"` | full request list | S01, S02 |
| Bell icon + badge `11` | the notification list | S01, S02 |
| `"O'zgartirish" ›` on the profile card | profile-edit screen | S06 |
| `"Xavfsizlik" ›` | security screen (separate from the settings group of the same name) | S06 |
| `"Mening kuzatuvlarim" ›` | "my observations/tracking" — meaning unknown | S06 |
| `"Til"` row | language picker and the list of languages | S06 |
| `"Yordam" ›` | support screen | S06 |
| `"Chiqish"` (logout) | the confirmation dialog, if any | S06 |
| Two-people icon in the employee-detail header | colleagues / org list? | S07–S09 |
| `"Tizimdagi kabi" ›` under `"mavzu"` | the theme picker and its options | S10 |
| `"Ish kunining boshlanishi va oxiri haqi…" ›` | notification settings detail | S10, S11 |
| `"Boshqalar" ›` | other notification settings | S10, S11 |
| `"MBF" / "Joriy filial"` card | branch switcher? | S06 |
| Request types 2–8 in the sheet | seven more request forms | S12 |
| Tapping a request row | request detail / history / approver | S01, S02, S04 |
| `"Belgilash"` / hand icons in S13 | the date picker and the two time pickers | S13 |
| Both dropdowns in S13 | their option lists | S13 |
| `"Yuborish"` | the success state / confirmation | S13 |

### 12.2 States never shown

Login / onboarding, PIN entry, biometric prompt, empty states (no requests, no
log entries), loading and skeleton states, error and offline states, validation
errors on the form, pull-to-refresh, any dialog or toast, the light theme, a
different month in the calendar, a day other than today selected, a rejected
request, landscape, tablet.

### 12.3 Questions only the client can answer — hand to `plan-analyst`

1. **What does the corner dot on a calendar day mean?** (04, 05, 14, 19 have one;
   16 and 22 — both request days — do not.)
2. **What does the green day mean?** Is 19 Sep green because the request for that
   day was approved, because it was a worked Saturday, or for another reason?
3. **Why are Saturdays and Sundays yellow like weekdays?** Does yellow encode
   "has data", "worked", or something else?
4. **Why does the donut say 30 while the legend adds to 29 / 96.7 %?** What is the
   missing day classified as?
5. **What exactly are `"251 soat rejaga muvofiq"` and `"147 soat 7 min ishlab
   chiqilgan"` computed from** — the whole month, or only up to today?
6. **What are the full names behind `"Kun almash."` and `"Jadval o'zg."`,** and
   what does each of the eight request types actually do?
7. **Are there more than eight request types** (does the sheet scroll)?
8. **What is the complete set of request statuses,** and what colour does each get?
   Is there a rejection reason?
9. **What are the options of dropdown 1** (`"Soatlik"` — hourly; is there a daily
   variant?) and **dropdown 2** (the reason list)?
10. **Is `"Izoh"` mandatory?** Are attachments ever supported?
11. **Who approves a request, and does the employee see the approver / the history?**
12. **What is `"Mening kuzatuvlarim"`?**
13. **What is the difference between `"Bo'lim"` and `"Jamoa"`,** given both show
    `"Ички бухгалтерия"` here?
14. **What are `"STIR"`, `"ShJBPH"`, `"JShShIR"`** in the client's own terms, and are
    they ever filled? Does the employee see them or only HR?
15. **Which fields does `"O'zgartirish"` let the employee edit?**
16. **Does the branch card (`"MBF"` / `"Joriy filial"`) switch branches,** i.e. can a
    person belong to more than one?
17. **Is `"Yangiliklar"` in scope for us at all?** Nothing about it is known.
18. **Which modules are behind `"Hammasi"`** beyond the four tiles?
19. **Is the language switch Uzbek-Latin / Uzbek-Cyrillic / Russian,** and is the
    Cyrillic data (names, `"Ишхона иши билан"`, `"Офис"`) stored that way or
    transliterated for display?
20. **Which apostrophe character does the client expect in Uzbek Latin strings?**
21. **Is the light theme actually used by anyone,** or is everyone on dark?
22. **What does the notification badge count** (`11` here) — unread notifications of
    what kind?
23. **What exactly do the face glyph and the pin on a log row indicate** — that a
    photo was captured and coordinates recorded? Can the employee open them?
24. **`08:35` appears as the day's `"kirish"` but is absent from the three-row log.**
    How is the headline in/out pair chosen when there are several entries?

---

*Record compiled by `plan-intake` on 2026-09-22 from the 13 files listed in §1.
No source file was moved, renamed or modified.*
