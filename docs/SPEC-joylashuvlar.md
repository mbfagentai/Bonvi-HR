# SPEC — Joylashuvlar (work sites: circle + polygon, HR page with a map)

Status: approved by the owner 2026-09-23. Extends `SPEC-xodim-ilovasi.md` §E3
and §F and `DATA-MODEL-xodim-ilovasi.md` §A. Where they disagree, **this file
wins** — in particular §F's "no map in this wave" is reversed by the owner.

## 0. Owner decisions (2026-09-23)

| # | Question | Decision |
|---|---|---|
| D1 | Shape of a site | **Both.** Per site HR picks *circle* (centre + radius) or *polygon* (vertices drawn on the map) |
| D2 | Polygon tolerance | A polygon accepts a check-in inside it **or within N metres of its edge**; N default **50** (GPS indoors drifts 30–100 m) |
| D3 | Which sites can an employee use | **Any active site** — unchanged from today (nearest active site wins). No per-employee binding |
| D4 | Map | **Leaflet + OpenStreetMap**, with an **Esri World Imagery** satellite layer toggle. No API key, no billing |
| D5 | Address | New free-text `address` field, shown in the list and in the employee app is NOT required |

## A. Data model — `work_sites` (additive migration `WorkSiteShapes`)

Existing columns stay as they are (`name`, `latitude`, `longitude`,
`radius_meters`, `is_active`, `note`, audit, soft-delete).

| New column | Type | Null | Default | Rule |
|---|---|---|---|---|
| `address` | `text` | yes | — | free text, trimmed, ≤ 300 chars |
| `shape` | `text` | no | `'circle'` | `ck_work_sites_shape`: `IN ('circle','polygon')` |
| `polygon` | `jsonb` | yes | — | array of `{ "lat": number, "lng": number }`, 3–50 vertices, open ring (first ≠ last). `ck_work_sites_polygon`: `shape = 'polygon'` ⇔ `polygon IS NOT NULL` |
| `edge_buffer_meters` | `integer` | no | `50` | `ck_work_sites_edge_buffer`: `BETWEEN 0 AND 500`. Only meaningful for polygons |

* For a **polygon**, `latitude`/`longitude` hold the **vertex centroid**
  (server computes it; the client never sends it) — used for "nearest site" in
  the rejection message and for centring the map. `radius_meters` keeps its
  CHECK (50–5000) and is left at whatever it was / 200 for polygons; it is not
  used for them.
* Existing rows migrate to `shape = 'circle'`, `edge_buffer_meters = 50`. No
  data is rewritten. **Read the generated migration** — it must contain no
  DROP.
* Polygon validation in the domain (`WorkSite.Create` / `Reconfigure`), not
  only in the API: 3–50 vertices, each lat/lng in range, no two consecutive
  identical vertices, **non-self-intersecting** (simple O(n²) segment check —
  n ≤ 50), area > 0, every vertex within **5000 m of the centroid** (the same
  ceiling as a circle's radius — otherwise a polygon would bypass it). A closed
  ring (first = last) is rejected, not auto-fixed. Error codes:
  `obyekt.polygon_nuqtalar_soni`, `obyekt.polygon_kesishadi`,
  `obyekt.polygon_koordinata`, `obyekt.polygon_katta`, `obyekt.polygon_zaxira`,
  `obyekt.shakl_notogri`, `obyekt.manzil_uzun`.
* The column defaults in the table above are used for the **backfill only** and
  dropped afterwards (`AddEmployeeMobileFlag` precedent): a model default would
  silently turn a deliberate 0 m buffer into 50.

## B. Geofence rule (server — `MobileCheckInCommand` step 3)

For every active site compute an **outside distance** `d` in metres:

* circle: `d = haversine(point, centre) − radius_meters` (negative = inside)
* polygon: `d = 0` if the point is inside (ray casting), else the distance to
  the nearest edge; the check-in is accepted when `d ≤ edge_buffer_meters`.
  Use a local equirectangular projection around the point (metres per degree
  at that latitude) — at ≤ 5 km scale the error is far below GPS error.

Pick the site with the smallest `d` **after** subtracting each site's own
allowance (circle: accepted when `d ≤ 0`; polygon: when `d ≤ buffer`). If none
accepts → `davomat.obyektdan_uzoq` with the nearest site's name and its
distance **to the boundary** (not to the centre — "you are 120 m outside
Zavod" is what the employee needs).

**Frozen operands** (`mobile_check_ins`, DATA-MODEL §E rationale). Accepted
rows always satisfy `distance_meters ≤ site_radius_meters`:

* circle: `distance_meters` = distance to the **centre**, `site_radius_meters`
  = radius (unchanged meaning — every pre-existing row is a circle row);
* polygon: `distance_meters` = distance to the **edge** (0 inside),
  `site_radius_meters` = the edge buffer.

`site_shape` on `mobile_check_ins` says which rule judged the row. The
accept/reject decision compares distances rounded to the centimetre, so a
point exactly on a diagonal edge of a 0-buffer polygon is accepted.

## C. API — `/api/attendance/work-sites` (existing group; RBAC unchanged)

Read: `attendance.view` **or** `catalog.view` (widened from `catalog.view` only). Write: `attendance.manage`.

`WorkSiteDto` gains: `address: string | null`, `shape: 'Circle' | 'Polygon'`
(PascalCase on the wire like every enum; lowercase in the DB),
`polygon: { lat, lng }[] | null`, `edgeBufferMeters: number`.

`SaveWorkSiteCommand`: `name`, `address?`, `shape`, `latitude?`,
`longitude?`, `radiusMeters?` (required for circle), `polygon?` (required for
polygon), `edgeBufferMeters?` (polygon; default 50), `note?`.

Regenerate the TS client (`./bonvi gen`) after the API change.

## D. HR web — Verifikatsiya → **Joylashuvlar** (`/verifikatsiya/joylashuvlar`)

Nav item under the Verifikatsiya group (icon `MapPin`), visible with
`attendance.view`; create/edit/deactivate buttons only with
`attendance.manage`.

**List page:** table — Nomi, Manzil, Shakl («Doira 200 m» / «Ko'pburchak ·
zaxira 50 m»), Holat (Faol/Faolsiz). Search by name. «Faolsizlarni ko'rsatish»
toggle. «Joylashuv qo'shish» button. Empty state explains that without at
least one site every phone check-in is rejected.

**Editor (drawer or page, match the closest existing pattern):**

* Fields: Nomi*, Manzil, Izoh.
* Shape switch: **Doira | Ko'pburchak**.
* Map (Leaflet, ~420 px tall): base layer toggle *Xarita* (OSM) / *Sun'iy
  yo'ldosh* (Esri World Imagery). Initial view: the site, else Tashkent
  (41.3111, 69.2797), zoom 16.
* Address search box → Nominatim (`nominatim.openstreetmap.org/search`,
  `countrycodes=uz`, on button press / Enter only, never per keystroke —
  Nominatim's usage policy) → flies the map there.
* «Mening joylashuvim» button (browser geolocation) — HR is often standing in
  the warehouse when they set it up.
* **Circle:** click the map sets the centre (draggable marker); radius via
  quick chips **100 · 200 · 300 · 500 m** plus a number input (50–5000). The
  circle is drawn live.
* **Polygon:** click adds a vertex; vertices are draggable; «Oxirgi nuqtani
  o'chirish» and «Tozalash» buttons; the polygon is drawn live with its
  buffer shown as a lighter outline is NOT required — show the buffer value
  as text. Edge buffer chips **0 · 25 · 50 · 100 m** + input (0–500).
  Save is disabled until ≥ 3 vertices; server errors (self-intersection)
  are shown inline.
* Coordinates are also shown read-only under the map (copyable), so HR can
  cross-check with Google/Yandex maps.

Libraries: `leaflet` + `react-leaflet` (React 19 compatible version). Tiles
over https. Attribution must stay visible (OSM + Esri terms).

## E. Employee app

No UI change. The mock site name stays until the real API is wired.

## F. Tests (Definition of Done)

* Domain: polygon validation (count, range, self-intersection), centroid.
* Geofence: point inside polygon; outside within buffer (accepted); outside
  beyond buffer (rejected, distance to edge reported); circle behaviour
  unchanged; nearest-site choice mixes circle and polygon sites correctly.
* API: RBAC — `attendance.view` can list, cannot create; `attendance.manage`
  can create both shapes; invalid polygon → 400 with the error code.
* Migration: upgrade on a DB with existing circle rows keeps them intact.
* Web: `npm run build` passes.
