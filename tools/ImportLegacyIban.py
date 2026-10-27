"""Offline projection of pinned MIT iban.js 0.0.14 into legacy tab-separated rules.
Only the existing 54-country surface is retained. No JavaScript is executed.
"""
import hashlib
import json
from pathlib import Path
import re
import shutil
import sqlite3

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools/data/iban-js-0.0.14"
RESOURCES = ROOT / "src/Bing.MockData/Datas/Resources"


def pattern(structure):
    classes = {"A": "0-9A-Z", "B": "0-9A-Z", "C": "A-Z", "F": "0-9", "L": "A-Z", "U": "A-Z", "W": "0-9A-Z"}
    if not re.fullmatch(r"(?:[ABCFLUW][0-9]{2})+", structure):
        raise ValueError("Unsupported BBAN structure: " + structure)
    blocks = [(structure[i], int(structure[i+1:i+3])) for i in range(0, len(structure), 3)]
    return "".join("["+classes[k]+"]{"+str(n)+"}" for k,n in blocks), sum(n for _,n in blocks)


def main():
    manifest = json.loads((SOURCE / "manifest.json").read_text(encoding="utf-8"))
    for name, digest in manifest["sourceFiles"].items():
        assert hashlib.sha256((SOURCE / name).read_bytes()).hexdigest() == digest, name
    text = (SOURCE / "iban.js").read_text(encoding="utf-8")
    rows = re.findall(r'new Specification\("([A-Z]{2})",\s*(\d+),\s*"([A-Z0-9]+)",\s*"([A-Z0-9]+)"\)',text)
    assert len(rows) == 102 and len({row[0] for row in rows}) == 102
    cldr = ROOT / "src/Bing.MockData.Catalogs/Data/cldr.sqlite3"
    db = sqlite3.connect(cldr.as_uri()+"?mode=ro",uri=True)
    try:
        names = {r["code"]:r["nameEn"] for (s,) in db.execute("SELECT PayloadJson FROM Records WHERE Kind='region'") for r in [json.loads(s)]}
    finally:
        db.close()
    iban, bban, accepted = [], [], []
    for code, size, structure, example in sorted(rows):
        if code not in manifest["countries"]:
            continue
        regex, length = pattern(structure)
        assert length + 4 == int(size)
        assert re.fullmatch(code+r"[0-9]{2}"+regex,example)
        digits = "".join(str(ord(c)-55) if c.isalpha() else c for c in example[4:]+example[:4])
        assert int(digits) % 97 == 1, code
        name = names[code]
        iban.append(name+"\t"+code+"\t^"+code+r"[0-9]{2}"+regex+"$")
        bban.append(name+"\t"+code+"\t^"+regex+"$")
        accepted.append(code)
    assert accepted == manifest["countries"]
    outputs = []
    for name, lines in [("IBAN.txt",iban),("BBAN.txt",bban)]:
        target = RESOURCES / name
        target.write_text("\n".join(lines)+"\n",encoding="utf-8")
        outputs.append(dict(file=name,records=len(lines),sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    notices = ROOT / "src/Bing.MockData/Datas/Notices"
    notices.mkdir(exist_ok=True)
    shutil.copyfile(SOURCE / "LICENSE", notices / "iban-js-LICENSE.txt")
    report = dict(status="PASS",sourceVersion="iban-0.0.14",sourceRecords=len(rows),retainedCountries=accepted,
                  excludedRecords=len(rows)-len(accepted),exclusionReason="outside existing 54-country legacy API coverage",
                  conversion="iban-legacy-v1; ASCII uppercase electronic format; name projection CLDR 49.0.0",outputs=outputs)
    out = ROOT / "artifacts/catalog-closure/resources/iban-conversion.json"
    out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8")
    manifest["conversion"] = report
    (SOURCE / "manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
