# -*- coding: utf-8 -*-
"""Cyrillic normalisation helpers shared by the reference-data builder.

The Excel source mixes Uzbek-Cyrillic orthography (ў/қ/ғ/ҳ) with the plain
Russian letters (у/к/г/х), Latin homoglyphs, stray punctuation and several
abbreviation styles for "бўлими" / "билан" / "ходими".  `norm()` folds all of
that into one comparison key; `prefer()` picks the nicest surviving spelling.
"""
import re

# Latin letters that look identical to Cyrillic ones (people paste from both
# layouts, and the same company name appears as "Vikor" and "Viсor").  Folding
# happens AFTER lower-casing so that "VIKOR" and "vikor" reach the same key.
HOMOGLYPH = str.maketrans({
    "a": "а", "c": "с", "e": "е", "o": "о", "p": "р", "x": "х", "y": "у",
    "k": "к", "m": "м", "t": "т", "b": "в", "h": "н",
})

# Uzbek-Cyrillic specials folded onto their bare Russian counterparts.
FOLD = str.maketrans({
    "ў": "у", "Ў": "у", "ғ": "г", "Ғ": "г", "қ": "к", "Қ": "к",
    "ҳ": "х", "Ҳ": "х", "ё": "е", "Ё": "е", "щ": "ш", "Щ": "ш",
    "ъ": "", "ь": "", "ї": "и", "і": "и", "ә": "а", "ы": "и",
})

# Uzbek-Cyrillic letters that mark the "good" spelling of a word.
NICE = "ўқғҳЎҚҒҲ"

WORDS = {
    "булим": "булими", "булими": "булими", "булимида": "булими",
    "булимига": "булими", "булимлари": "булими",
    "бми": "булими", "бм": "булими",
    "бн": "билан", "бнс": "билан",
    "ходим": "ходими", "ходими": "ходими", "хми": "ходими",
    "ишчи": "ходими", "ишчиси": "ходими", "ишчилари": "ходими",
    "бошлик": "бошлиги", "бошлиги": "бошлиги", "бошликлари": "бошлиги",
    "омборхизматчиси": "омбор хизматчиси",
    "конвейер": "конвеер", "конвеер": "конвеер", "конвейр": "конвеер",
    # single-token misspellings that repeat across several sheets; folding them
    # here fixes the department column and the position column at once.
    "лабарант": "лаборант", "лаборатори": "лаборатория",
    "загатовка": "заготовка", "загатовшик": "заготовшик",
    "шлиповка": "шлифовка", "шлиповкачи": "шлифовкачи",
    "сваршиг": "сваршик", "букуш": "букиш",
    # "HR"/"hr" fold to "нr" through the homoglyph table; put it back so the
    # rules in refs_spec.py can be written the way a human reads them.
    "нr": "hr",
    "оператор": "оператори", "оператори": "оператори",
    "департамент": "департаменти", "департаменти": "департаменти",
}

_PUNCT = re.compile(r"[«»\"“”'`().,:;!?/\\\-–—+*=\[\]{}|]")
_WS = re.compile(r"\s+")


# "б/н" -> "билан", "б/ми" -> "бўлими", "х/ми" -> "ходими" and friends.
# HR writes these everywhere: "Хаво б/н синаш б/ми", "Ташқи шлиф/ка х/ми".
_ABBR = [
    (re.compile(r"\bб\s*[/\-]?\s*н\b"), " билан "),
    (re.compile(r"\bб\s*[/\-]\s*ми\b"), " булими "),
    (re.compile(r"\bб\s*[/\-]\s*м\b"), " булими "),
    (re.compile(r"\bх\s*[/\-]\s*ми\b"), " ходими "),
    (re.compile(r"\bшлиф\s*[/\-]\s*ка\b"), " шлифовка "),
]


def norm(value):
    """Comparison key: case-, orthography-, punctuation- and abbreviation-insensitive."""
    s = value.lower().translate(HOMOGLYPH).translate(FOLD)
    for pattern, repl in _ABBR:
        s = pattern.sub(repl, s)
    s = _PUNCT.sub(" ", s)
    s = _WS.sub(" ", s).strip()
    return " ".join(WORDS.get(tok, tok) for tok in s.split())


def nice_score(value):
    """Higher = better Uzbek-Cyrillic spelling."""
    return sum(1 for ch in value if ch in NICE)


def prefer(variants):
    """Pick the canonical display spelling out of the raw variants of one group.

    `variants` is an iterable of (raw_value, frequency).  Preference order:
    proper Uzbek-Cyrillic letters, then frequency, then shortest, then alphabetical.
    """
    best = None
    for raw, freq in variants:
        clean = raw.strip().strip(".,:;-").strip()
        key = (nice_score(clean), freq, -len(clean), clean)
        if best is None or key > best[0]:
            best = (key, clean)
    return best[1]


_SLUG = {
    "а": "a", "б": "b", "в": "v", "г": "g", "ғ": "g", "д": "d", "е": "e",
    "ё": "yo", "ж": "j", "з": "z", "и": "i", "й": "y", "к": "k", "қ": "q",
    "л": "l", "м": "m", "н": "n", "о": "o", "п": "p", "р": "r", "с": "s",
    "т": "t", "у": "u", "ў": "o", "ф": "f", "х": "x", "ҳ": "h", "ц": "ts",
    "ч": "ch", "ш": "sh", "щ": "sh", "ъ": "", "ы": "i", "ь": "", "э": "e",
    "ю": "yu", "я": "ya",
}


def slug(value):
    """Cyrillic (or Latin) name -> stable lowercase latin slug."""
    s = value.lower()
    out = []
    for ch in s:
        if ch in _SLUG:
            out.append(_SLUG[ch])
        elif ch.isascii() and ch.isalnum():
            out.append(ch)
        else:
            out.append("-")
    s = "".join(out)
    s = re.sub(r"-+", "-", s).strip("-")
    return s
