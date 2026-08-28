"""Minimal .xlsx reader (no openpyxl). Unzips the workbook and parses the XML directly.

Usage:
    from xlsx_lib import Workbook
    wb = Workbook("/path/to/file.xlsx")
    rows = wb.rows("91 Умумий ходимлар")   # -> [(row_number, {col_index: value}), ...]

Column indexes are 1-based (A=1, B=2, ...). Empty cells are omitted from the dict.
"""
import os
import re
import shutil
import tempfile
import zipfile
import xml.etree.ElementTree as ET

NS = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
RNS = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"

_COL_RE = re.compile(r"([A-Z]+)")


def col_num(ref):
    """'BC12' -> 55"""
    n = 0
    for ch in _COL_RE.match(ref).group(1):
        n = n * 26 + (ord(ch) - 64)
    return n


class Workbook:
    def __init__(self, path, workdir=None):
        self.path = path
        self._tmp = None
        if workdir is None:
            workdir = tempfile.mkdtemp(prefix="xlsx_")
            self._tmp = workdir
        self.base = workdir
        if not os.path.exists(os.path.join(self.base, "xl", "workbook.xml")):
            with zipfile.ZipFile(path) as z:
                z.extractall(self.base)
        self._shared = self._load_shared()
        self.sheets = self._load_sheets()

    def close(self):
        if self._tmp:
            shutil.rmtree(self._tmp, ignore_errors=True)
            self._tmp = None

    def _load_shared(self):
        p = os.path.join(self.base, "xl", "sharedStrings.xml")
        if not os.path.exists(p):
            return []
        return [
            "".join(t.text or "" for t in si.iter(NS + "t"))
            for si in ET.parse(p).getroot()
        ]

    def _load_sheets(self):
        rels = {
            r.get("Id"): r.get("Target")
            for r in ET.parse(
                os.path.join(self.base, "xl", "_rels", "workbook.xml.rels")
            ).getroot()
        }
        out = []  # (name, state, target)
        wb = ET.parse(os.path.join(self.base, "xl", "workbook.xml")).getroot()
        for sh in wb.find(NS + "sheets"):
            out.append(
                (sh.get("name"), sh.get("state") or "visible", rels[sh.get(RNS + "id")])
            )
        return out

    def _cell(self, c):
        t = c.get("t")
        if t == "s":
            v = c.find(NS + "v")
            return self._shared[int(v.text)] if v is not None else ""
        if t == "inlineStr":
            return "".join(x.text or "" for x in c.iter(NS + "t"))
        if t == "str":
            v = c.find(NS + "v")
            return v.text or "" if v is not None else ""
        v = c.find(NS + "v")
        return v.text if v is not None else ""

    def _resolve(self, name_or_file):
        for name, _state, target in self.sheets:
            if name == name_or_file:
                return os.path.join(self.base, "xl", target.lstrip("/"))
        p = os.path.join(self.base, "xl", "worksheets", name_or_file)
        if os.path.exists(p):
            return p
        raise KeyError(name_or_file)

    def rows(self, name_or_file):
        """-> [(row_number:int, {col_index:int -> stripped str})]"""
        root = ET.parse(self._resolve(name_or_file)).getroot()
        out = []
        data = root.find(NS + "sheetData")
        if data is None:
            return out
        for row in data:
            cells = {}
            for c in row:
                ref = c.get("r")
                if not ref:
                    continue
                val = self._cell(c)
                if val is None:
                    continue
                val = val.strip()
                if val:
                    cells[col_num(ref)] = val
            out.append((int(row.get("r")), cells))
        return out
