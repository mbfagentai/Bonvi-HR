#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Build the Bonvi HR reference lists (ma'lumotnomalar) out of the HR Excel file.

    python3 tools/data/build_reference_data.py [path-to-xlsx] [--report]

Reads  : «№91 бланк Ходимлар.xlsx»
Writes : api/src/Bonvi.Infrastructure/Persistence/Seed/data/*.json

The rules live in refs_spec.py, the Cyrillic folding in normalize.py.
Re-running the script is safe: the output is fully deterministic.
"""
import collections
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from normalize import norm, prefer, slug          # noqa: E402
from xlsx_lib import Workbook                     # noqa: E402
import refs_spec as S                             # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(REPO, "api", "src", "Bonvi.Infrastructure",
                   "Persistence", "Seed", "data")
DEFAULT_SRC = os.path.expanduser(
    "~/Downloads/Telegram Desktop/№91 бланк Ходимлар  (7).xlsx")

SHEET_MAIN = "91 Умумий ходимлар"
SHEET_ARCHIVE = "91.7 Архив"
SHEET_LEFT = "91.6 Ишдан кетган ходимлар"
SHEET_AWARDS = "91.14 Тақдирланган ходимлар"

# Event sheets.  The ETL walks these too, so every raw spelling they contain has
# to be a key in aliases.json — otherwise the migration report fills up with
# "not in the reference list" lines and the department/position of the event is
# silently lost.  Layout: (sheet, first data row, {column: reference name}).
# Columns were taken from the header rows; free-text columns (91.3 «Изох»,
# 91.4 «Қўшимча вазифаси», 91.5 «Изох») are deliberately NOT sources — they are
# prose, not reference values.
EVENT_SHEETS = [
    ("91.2 Янги ишга келганлар", 5, {
        8: "directions", 9: "departments", 10: "positions",
    }),
    ("91.3 Лавозими ўзгарган ходимлар", 6, {
        5: "directions", 6: "departments", 7: "positions",      # previous job
        10: "directions", 11: "departments", 12: "positions",   # new job
    }),
    ("91.4 Қушимча вазифалар берилган", 6, {
        5: "departments", 6: "positions",                       # main job
        9: "departments",                                       # extra-duty unit
    }),
    ("91.5 Синов муддатидаги ходимлар", 3, {
        4: "departments", 5: "positions",
    }),
    ("91.8 Стажировкадан кетганлар", 5, {
        4: "departments", 5: "positions",
    }),
]

# Same layout, for the four sheets that are read row-by-employee: only the
# caption / header noise of the skipped rows is picked up from these columns.
NOISE_SHEETS = [
    (SHEET_MAIN, 3, {5: "directions", 6: "departments", 7: "positions"}),
    (SHEET_ARCHIVE, 3, {5: "directions", 6: "departments", 7: "positions"}),
    (SHEET_LEFT, 5, {4: "departments", 5: "positions"}),
    (SHEET_AWARDS, 4, {4: "departments", 5: "positions"}),
]

NUMERIC = re.compile(r"^[\d\s.,+\-eE]+$")

# Excel repeats a caption row above every month block ("2025 ЙИЛ.",
# "АВГУСТ ОЙИДА ҚЎШИМЧА ВАЗИФА БЕРИЛГАН ХОДИМЛАР РЎЙХАТИ") and the frozen
# header row is re-typed every few hundred rows.  Both land in ordinary data
# columns, so every reference has to recognise them.  They are mapped to null:
# "this cell is not a reference value, leave it empty and say nothing".
CAPTION = re.compile(r"^\d{4} йил$|ойида .*ходимлар")
HEADER_LEFTOVER = {
    "йуналиш", "йуналиши", "булими", "лавозими", "лавозим", "лаозими",
    "олдинги лавозими", "янги лавозими", "изох", "оклад", "сана", "ракам",
    "буйрук", "буйрук раками", "ф и о", "ф и ш", "фио", "бириктирилган ходими",
    "барча ходимлар", "узгаришсиз", "тугатилган",
}


def is_noise(raw):
    """Caption row or a re-typed column header that leaked into the data."""
    key = norm(raw)
    return key in HEADER_LEFTOVER or bool(CAPTION.search(key))


def nkeys(mapping):
    """Re-key a hand-written alias dict through norm() so both sides match."""
    return {norm(k): v for k, v in mapping.items()}


def nset(values):
    return {norm(v) for v in values}


# ---------------------------------------------------------------- extraction
def read_sources(src):
    wb = Workbook(src)
    rows_main = wb.rows(SHEET_MAIN)

    employees, blocks, current = [], collections.OrderedDict(), None
    for r, c in rows_main:
        if r < 3:
            continue
        if c.get(2) and c.get(7) and c.get(4):
            employees.append((current, c))
        elif c.get(2) and not c.get(7):
            current = c[2]
            blocks.setdefault(current, 0)
        if current and c.get(2) and c.get(7):
            blocks[current] = blocks.get(current, 0) + 1

    archive = [c for r, c in wb.rows(SHEET_ARCHIVE) if r >= 3 and c.get(2)]
    left = [c for r, c in wb.rows(SHEET_LEFT) if r >= 5 and c.get(2)]
    awards = [c for r, c in wb.rows(SHEET_AWARDS) if r >= 4 and c.get(2)]

    # The four sheets above are read row-by-employee, so the group-header and
    # month-caption rows in between never reach the tables.  The ETL walks the
    # sheet top to bottom, so it does meet them — collect the noise those rows
    # leave in the reference columns and let it become an explicit null.
    noise = collections.defaultdict(collections.Counter)
    for sheet, first_row, columns in NOISE_SHEETS:
        for r, cells in wb.rows(sheet):
            if r < first_row:
                continue
            for col, ref in columns.items():
                value = cells.get(col)
                if value and (NUMERIC.match(value) or is_noise(value)):
                    noise[ref][value] += 1

    # reference name -> Counter(raw value) over all event sheets, plus the
    # per-column breakdown the coverage report prints.
    events = collections.defaultdict(collections.Counter)
    event_columns = []                     # (label, reference, Counter(raw))
    for sheet, first_row, columns in EVENT_SHEETS:
        rows = [c for r, c in wb.rows(sheet) if r >= first_row]
        for col in sorted(columns):
            ref = columns[col]
            cells = collections.Counter(c[col] for c in rows if c.get(col))
            events[ref].update(cells)
            event_columns.append(
                (f"{sheet.split(' ')[0]:5s} col {col:<2d} {ref}", ref, cells))
    for ref, cells in noise.items():
        events[ref].update(cells)
    return employees, blocks, archive, left, awards, events, event_columns


def counter(rows, col):
    return collections.Counter(r[col] for r in rows if r.get(col))


# ------------------------------------------------------------- legal entities
def build_legal_entities(raws):
    entities = [
        {"code": c, "name": n, "fullName": f, "taxId": t, "sortOrder": i + 1}
        for i, (c, n, f, t) in enumerate(S.LEGAL_ENTITIES)
    ]
    known = {e["code"] for e in entities}
    lookup = nkeys(S.LEGAL_ENTITY_ALIAS)
    alias, unmapped = {}, []
    for raw in sorted(raws):
        key = norm(raw)
        code = lookup.get(key)
        if code is None and NUMERIC.match(raw):
            code = None
        if code:
            for part in code.split("|"):
                assert part in known, f"legal entity {part} not declared"
        alias[raw] = code
        if code is None:
            unmapped.append(raw)
    return entities, alias, unmapped


# ----------------------------------------------------------------- directions
def build_directions(raws):
    directions = [
        {"code": c, "name": n, "color": col, "sortOrder": i + 1}
        for i, (c, n, col) in enumerate(S.DIRECTIONS)
    ]
    known = {d["code"] for d in directions}
    tokens = nkeys(S.DIRECTION_TOKEN)
    not_direction = nset(S.DIRECTION_NOT_A_DIRECTION)

    def resolve(raw):
        """-> (code or None, known) — known=False means "nobody could tell"."""
        if NUMERIC.match(raw) or is_noise(raw):
            return None, True
        if norm(raw) in not_direction:
            return None, True
        codes = []
        for chunk in re.split(r"[,;/+]", raw):
            chunk = norm(chunk)
            if not chunk:
                continue
            if chunk in not_direction:
                continue
            if chunk in tokens:
                codes.append(tokens[chunk])
                continue
            words = [tokens.get(w) for w in chunk.split()]
            if words and all(words):
                codes.extend(words)
            else:
                return None, False
        out, seen = [], set()
        for c in codes:
            for part in c.split("|"):
                if part not in seen:
                    seen.add(part)
                    out.append(part)
        return ("|".join(out) if out else None), True

    alias, unmapped = {}, []
    for raw in sorted(raws):
        code, resolved = resolve(raw)
        if code:
            for part in code.split("|"):
                assert part in known, f"direction {part} not declared ({raw})"
        alias[raw] = code
        if not resolved:
            unmapped.append(raw)
    return directions, alias, unmapped


# ---------------------------------------------------------------- departments
def build_departments(raws, block_raws):
    departments, order = [], {}
    for i, (code, name, parent, legal) in enumerate(S.DEPARTMENTS):
        departments.append({
            "code": code, "name": name, "parentCode": parent,
            "legalEntityCode": legal, "sortOrder": i + 1,
        })
        order[code] = i + 1
    known = set(order)
    for d in departments:
        assert d["parentCode"] in (None,) or d["parentCode"] in known, d

    by_name = {norm(d["name"]): d["code"] for d in departments}
    lookup = dict(by_name)
    lookup.update(nkeys(S.DEPARTMENT_ALIAS))
    junk = nset(S.DEPARTMENT_JUNK)

    rewrites = [(re.compile(p), c) for p, c in S.DEPARTMENT_REWRITE]

    def resolve(raw, table, junk_set):
        """-> (code or None, known) — known=False means "nobody could tell"."""
        if NUMERIC.match(raw) or is_noise(raw):
            return None, True
        key = norm(raw)
        if key in junk_set:
            return None, True
        code = table.get(key)
        if code is None:
            # every regional label ("Самарқанд бўлими", "Денов савдо", …) is
            # the one «Вилоят савдо» unit — an explicit list would need ~60 rows
            for pattern, target in rewrites:
                if pattern.match(key):
                    code = target
                    break
        return code, code is not None

    alias, unmapped = {}, []
    for raw in sorted(raws):
        code, resolved = resolve(raw, lookup, junk)
        assert code is None or code in known, f"department {code} ({raw})"
        alias[raw] = code
        if not resolved:
            unmapped.append(raw)

    block_table = dict(lookup)
    block_table.update(nkeys(S.DEPARTMENT_BLOCK_ALIAS))
    block_junk = nset(S.DEPARTMENT_BLOCK_JUNK) | junk
    block_alias, block_unmapped = {}, []
    for raw in sorted(block_raws):
        # group headers carry a head-count suffix: "ОМБОР БЎЛИМИ ( 28 ходим)"
        cleaned = re.sub(r"\(?\s*\d+\s*(ходим|нафар)\s*\)?", " ", raw)
        code, resolved = resolve(cleaned, block_table, block_junk)
        assert code is None or code in known, f"block {code} ({raw})"
        block_alias[raw] = code
        if not resolved:
            block_unmapped.append(raw)
    return departments, alias, unmapped, block_alias, block_unmapped


# ------------------------------------------------------------------ positions
def grade_of(name):
    key = norm(name)
    for grade, words in S.GRADE_RULES:
        for w in words:
            if key == norm(w) or key.endswith(" " + norm(w)) or key.startswith(norm(w) + " "):
                return grade
    return 2


ARCHIVE_MIN_FREQ = 1   # every real historical title also becomes canonical


def build_positions(main_counts, extra_counts, dept_codes):
    """The canonical catalogue is the one the 540 active employees actually use.

    Historical-only titles join it when they were used at least
    ARCHIVE_MIN_FREQ times.  Everything that is not a real job title
    (personal names, dates, department labels) is listed in POSITION_JUNK and
    reported as unmapped instead.
    """
    junk = nset(S.POSITION_JUNK)
    unclear = nset(S.POSITION_UNCLEAR)
    raw_counts = collections.Counter(main_counts)
    raw_counts.update(extra_counts)

    long_note = re.compile(r"\s*\([^)]{20,}\)?\s*$")

    def display_of(raw):
        # "Юк ташувчи(омборга келган юкларни тушуради ва юклайди)" -> "Юк ташувчи"
        return long_note.sub("", raw).strip() or raw

    literal = {norm(k): norm(v) for k, v in S.POSITION_ALIAS.items()}

    def key_of(raw):
        key = norm(display_of(raw))
        key = literal.get(key, key)
        for pattern, repl in S.POSITION_REWRITE:
            if pattern.match(key):
                return pattern.sub(repl, key)
        return key

    def is_title(raw):
        return (not NUMERIC.match(raw)
                and not is_noise(raw)
                and norm(raw) not in junk
                and norm(raw) not in unclear)

    active_keys = {key_of(raw) for raw in main_counts if is_title(raw)}
    history = collections.Counter()
    for raw, freq in extra_counts.items():
        if is_title(raw):
            history[key_of(raw)] += freq
    canonical_keys = active_keys | {k for k, n in history.items()
                                    if n >= ARCHIVE_MIN_FREQ}

    groups = collections.defaultdict(list)      # key -> [(display name, freq)]
    members_of = collections.defaultdict(list)  # key -> [original raw value]
    alias, unmapped = {}, []
    for raw, freq in raw_counts.items():
        key = key_of(raw)
        if not is_title(raw) or key not in canonical_keys:
            # null in aliases.json = "deliberately not a position, leave the
            # cell empty".  Only the values nobody could classify go on the
            # unmapped to-do list; declared junk is a settled question.
            alias[raw] = None
            settled = NUMERIC.match(raw) or is_noise(raw) or norm(raw) in junk
            if norm(raw) in unclear or not settled:
                unmapped.append(raw)
            continue
        groups[key].append((display_of(raw), freq))
        members_of[key].append(raw)

    prefixes = nkeys(S.POSITION_DEPARTMENT_PREFIX)
    positions, code_of = [], {}
    used = set()
    for key in sorted(groups, key=lambda k: (-sum(f for _, f in groups[k]), k)):
        name = S.CANONICAL_POSITION_NAME.get(key) or prefer(groups[key])
        code = slug(name)[:60].strip("-") or "position"
        base, n = code, 2
        while code in used:
            code, n = f"{base}-{n}", n + 1
        used.add(code)
        dept = None
        for pref, dcode in prefixes.items():
            if key.startswith(pref) and dcode in dept_codes:
                dept = dcode
                break
        positions.append({
            "code": code, "name": name, "departmentCode": dept,
            "grade": grade_of(name), "probationDays": None, "duties": [],
            "sortOrder": 0, "_freq": sum(f for _, f in groups[key]),
        })
        code_of[key] = code

    positions.sort(key=lambda p: (-p["_freq"], p["name"]))
    for i, p in enumerate(positions):
        p["sortOrder"] = i + 1
        del p["_freq"]

    for key, raws in members_of.items():
        for raw in raws:
            alias[raw] = code_of[key]
    return positions, alias, unmapped


# --------------------------------------------------------- termination / award
def build_ruleset(raws, definitions, rules, junk_values, fallback):
    items = []
    for i, row in enumerate(definitions):
        if len(row) == 4:
            code, name, a, b = row
            items.append({"code": code, "name": name, "isVoluntary": a,
                          "isRehireable": b, "sortOrder": i + 1})
        else:
            code, name, a = row
            items.append({"code": code, "name": name, "hasAmount": a,
                          "sortOrder": i + 1})
    known = {i["code"] for i in items}
    compiled = [(code, [re.compile(p) for p in pats]) for code, pats in rules]
    junk = nset(junk_values)

    alias, unmapped = {}, []
    for raw in sorted(raws):
        key = norm(raw)
        code = None
        if not NUMERIC.match(raw) and not is_noise(raw) and key not in junk:
            for candidate, patterns in compiled:
                if any(p.search(key) for p in patterns):
                    code = candidate
                    break
            if code is None:
                code = fallback
        assert code is None or code in known, f"{code} not declared ({raw})"
        alias[raw] = code
        if code is None:
            unmapped.append(raw)
    return items, alias, unmapped


# ---------------------------------------------------------------------- write
def dump(name, payload):
    path = os.path.join(OUT, name)
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, ensure_ascii=False, indent=2)
        fh.write("\n")
    return path


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    src = args[0] if args else DEFAULT_SRC
    os.makedirs(OUT, exist_ok=True)

    employees, blocks, archive, left, awards, events, event_columns = read_sources(src)
    main_rows = [c for _, c in employees]

    dir_raw = (set(counter(main_rows, 5)) | set(counter(archive, 5))
               | set(events["directions"]))
    dep_raw = (set(counter(main_rows, 6)) | set(counter(archive, 6))
               | set(counter(left, 4)) | set(counter(awards, 4))
               | set(events["departments"]))
    main_pos_counts = counter(main_rows, 7)
    extra_pos_counts = collections.Counter()
    for extra in (counter(archive, 7), counter(left, 5), counter(awards, 5),
                  events["positions"]):
        extra_pos_counts.update(extra)
    pos_counts = collections.Counter(main_pos_counts)
    pos_counts.update(extra_pos_counts)
    legal_raw = set(counter(main_rows, 13))
    term_raw = set(counter(left, 10))
    award_raw = set(counter(awards, 6))

    legal, legal_alias, legal_un = build_legal_entities(legal_raw)
    directions, dir_alias, dir_un = build_directions(dir_raw)
    departments, dep_alias, dep_un, blk_alias, blk_un = build_departments(
        dep_raw, set(blocks))
    dept_codes = {d["code"] for d in departments}
    positions, pos_alias, pos_un = build_positions(
        main_pos_counts, extra_pos_counts, dept_codes)
    reasons, term_alias, term_un = build_ruleset(
        term_raw, S.TERMINATION_REASONS, S.TERMINATION_RULES,
        S.TERMINATION_JUNK, "boshqa")
    award_types, award_alias, award_un = build_ruleset(
        award_raw, S.AWARD_TYPES, S.AWARD_RULES, set(), "tashakkurnoma")

    penalties = [{"code": c, "name": n, "hasAmount": a, "severity": s,
                  "sortOrder": i + 1}
                 for i, (c, n, a, s) in enumerate(S.PENALTY_TYPES)]
    documents = [{"code": c, "name": n, "isRequired": r, "sortOrder": i + 1}
                 for i, (c, n, r) in enumerate(S.DOCUMENT_TYPES)]

    dump("legal-entities.json", legal)
    dump("directions.json", directions)
    dump("departments.json", departments)
    dump("positions.json", positions)
    dump("termination-reasons.json", reasons)
    dump("award-types.json", award_types)
    dump("penalty-types.json", penalties)
    dump("document-types.json", documents)
    dump("aliases.json", {
        "legalEntities": legal_alias,
        "directions": dir_alias,
        "departments": dep_alias,
        "departmentBlocks": blk_alias,
        "positions": pos_alias,
        "terminationReasons": term_alias,
        "awardTypes": award_alias,
        "unmapped": {
            "legalEntities": sorted(legal_un),
            "directions": sorted(dir_un),
            "departments": sorted(dep_un),
            "departmentBlocks": sorted(blk_un),
            "positions": sorted(pos_un),
            "terminationReasons": sorted(term_un),
            "awardTypes": sorted(award_un),
        },
    })

    print(f"employees in «{SHEET_MAIN}»: {len(main_rows)}   blocks: {len(blocks)}")
    print(f"archive: {len(archive)}   left: {len(left)}   awards: {len(awards)}")
    print()
    table = [
        ("legal entities", legal_raw, legal, legal_un),
        ("directions", dir_raw, directions, dir_un),
        ("departments", dep_raw, departments, dep_un),
        ("department blocks", set(blocks), departments, blk_un),
        ("positions", set(pos_counts), positions, pos_un),
        ("termination reasons", term_raw, reasons, term_un),
        ("award types", award_raw, award_types, award_un),
    ]
    print(f"{'reference':22s} {'raw':>6s} {'canonical':>10s} {'unmapped':>9s}")
    for label, raw, canon, un in table:
        print(f"{label:22s} {len(raw):6d} {len(canon):10d} {len(un):9d}")
    print(f"{'penalty types':22s} {0:6d} {len(penalties):10d} {0:9d}")
    print(f"{'document types':22s} {0:6d} {len(documents):10d} {0:9d}")

    aliases = {"legalEntities": legal_alias, "directions": dir_alias,
               "departments": dep_alias, "positions": pos_alias,
               "terminationReasons": term_alias, "awardTypes": award_alias}
    print("\n cell coverage")
    print("   known = the value is in aliases.json at all (code or a"
          " deliberate null);\n"
          "   coded = it resolves to a canonical code.  Everything the ETL"
          " reports as\n"
          "   «not in the reference list» is 100% - known.")
    print(f"   {'sheet / column':28s} {'known':>15s} {'coded':>15s}")
    checks = [
        ("91    ЙЎНАЛИШИ",  counter(main_rows, 5), "directions"),
        ("91    БЎЛИМИ",    counter(main_rows, 6), "departments"),
        ("91    ЛАВОЗИМИ",  counter(main_rows, 7), "positions"),
        ("91    Мех.дафт.", counter(main_rows, 13), "legalEntities"),
        ("91.7  ЙЎНАЛИШИ",  counter(archive, 5), "directions"),
        ("91.7  БЎЛИМИ",    counter(archive, 6), "departments"),
        ("91.7  ЛАВОЗИМИ",  counter(archive, 7), "positions"),
        ("91.6  Бўлим",     counter(left, 4), "departments"),
        ("91.6  Лавозими",  counter(left, 5), "positions"),
        ("91.6  Сабаби",    counter(left, 10), "terminationReasons"),
        ("91.14 Бўлим",     counter(awards, 4), "departments"),
        ("91.14 Лавозими",  counter(awards, 5), "positions"),
        ("91.14 Рағбат",    counter(awards, 6), "awardTypes"),
    ] + [(label, cells, ref) for label, ref, cells in event_columns]
    for label, cells, table in checks:
        total = sum(cells.values())
        known = sum(n for v, n in cells.items() if v in aliases[table])
        coded = sum(n for v, n in cells.items() if aliases[table].get(v))
        kp = 100.0 * known / total if total else 100.0
        cp = 100.0 * coded / total if total else 100.0
        print(f"   {label:28s} {known:6d}/{total:<5d} {kp:5.1f}% "
              f"{coded:6d}/{total:<5d} {cp:5.1f}%")

    if "--report" in sys.argv:
        print("\n\n=== biggest merge groups ===")
        for label, alias in (("directions", dir_alias), ("departments", dep_alias),
                             ("positions", pos_alias),
                             ("termination reasons", term_alias)):
            grouped = collections.defaultdict(list)
            for raw, code in alias.items():
                if code:
                    grouped[code].append(raw)
            print(f"\n-- {label} --")
            for code, members in sorted(grouped.items(),
                                        key=lambda kv: -len(kv[1]))[:12]:
                print(f"  {code:32s} <- {len(members):3d} raw")
                if len(members) <= 6:
                    for m in sorted(members):
                        print(f"        {m}")
        print("\n\n=== unmapped ===")
        for label, un in (("legalEntities", legal_un), ("directions", dir_un),
                          ("departments", dep_un), ("departmentBlocks", blk_un),
                          ("positions", pos_un), ("terminationReasons", term_un)):
            print(f"\n-- {label}: {len(un)} --")
            for v in sorted(un):
                print(f"    {v}")


if __name__ == "__main__":
    main()
