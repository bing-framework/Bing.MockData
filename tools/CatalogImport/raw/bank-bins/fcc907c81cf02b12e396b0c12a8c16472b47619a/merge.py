#!/usr/bin/env python3
"""Merge open BIN datasets into one file with provenance.

Sources (both CC-BY-4.0):
  iannuttall/binlist-data    - last change 2020-12-24 (archived)
  venelinkochev/bin-list-data - last change 2025-02-11

Rule per field, for a BIN present in both:
  * both non-blank and different  -> take the newer source, log a conflict
  * one blank                     -> take the other
  * agree                         -> keep
Output: out/bins.csv, out/conflicts.csv, out/summary.json
"""
import csv, json, re, sys, collections, datetime
SEEN = {"iannuttall": "2020-12-24", "venelinkochev": "2025-02-11"}
FUNDING = {"CREDIT", "DEBIT", "CHARGE CARD"}
OUT_COLS = ["bin", "brand", "type", "category", "issuer", "issuer_phone", "issuer_url", "alpha_2", "alpha_3", "country", "source", "seen"]

def clean(s):
    return re.sub(r"\s+", " ", (s or "")).strip()

def norm_type(t):
    t = clean(t).upper()
    return t if t in FUNDING else ""          # a few hundred upstream rows carry issuer names here

def norm_key(s):
    return re.sub(r"[^A-Z0-9]", "", clean(s).upper())

def load_ia(path):
    d = {}
    with open(path, newline="", encoding="utf-8", errors="replace") as f:
        for r in csv.DictReader(f):
            b = clean(r["bin"])
            if not b.isdigit(): continue
            d[b] = {"brand": clean(r["brand"]).upper(), "type": norm_type(r["type"]), "category": clean(r["category"]).upper(),
                    "issuer": clean(r["issuer"]).upper(), "issuer_phone": clean(r["bank_phone"]), "issuer_url": clean(r["bank_url"]),
                    "alpha_2": clean(r["alpha_2"]).upper(), "alpha_3": clean(r["alpha_3"]).upper(), "country": clean(r["country"]).upper()}
    return d

def load_vk(path):
    d = {}
    with open(path, newline="", encoding="utf-8", errors="replace") as f:
        for r in csv.DictReader(f):
            b = clean(r["BIN"])
            if not b.isdigit(): continue
            d[b] = {"brand": clean(r["Brand"]).upper(), "type": norm_type(r["Type"]), "category": clean(r["Category"]).upper(),
                    "issuer": clean(r["Issuer"]).upper(), "issuer_phone": clean(r["IssuerPhone"]), "issuer_url": clean(r["IssuerUrl"]),
                    "alpha_2": clean(r["isoCode2"]).upper(), "alpha_3": clean(r["isoCode3"]).upper(), "country": clean(r["CountryName"]).upper()}
    return d

OUT = sys.argv[1] if len(sys.argv) > 1 else "out"

def main():
    import os; os.makedirs(OUT, exist_ok=True)
    ia, vk = load_ia("src/iannuttall.csv"), load_vk("src/venelinkochev.csv")
    fields = ["brand", "type", "category", "issuer", "issuer_phone", "issuer_url", "alpha_2", "alpha_3", "country"]
    out, conflicts = [], []
    stats = collections.Counter()
    for b in sorted(set(ia) | set(vk), key=lambda x: (len(x), x)):
        o, v = ia.get(b), vk.get(b)
        if o and v:
            row = {}; changed = False
            for k in fields:
                a, c = o[k], v[k]
                if a and c and norm_key(a) != norm_key(c):
                    row[k] = c; changed = True
                    if k in ("brand", "type", "category", "issuer", "alpha_2"):
                        conflicts.append({"bin": b, "field": k, "iannuttall": a, "venelinkochev": c, "chosen": "venelinkochev"})
                        stats["conflict_" + k] += 1
                elif c:
                    row[k] = c
                    if not a: stats["filled_" + k] += 1
                else:
                    row[k] = a
            row.update({"bin": b, "source": "merged", "seen": SEEN["venelinkochev"]}); stats["merged"] += 1
        elif v:
            row = dict(v, bin=b, source="venelinkochev", seen=SEEN["venelinkochev"]); stats["only_venelinkochev"] += 1
        else:
            row = dict(o, bin=b, source="iannuttall", seen=SEEN["iannuttall"]); stats["only_iannuttall"] += 1
        # country name consistency: if alpha_2 is set but the name is blank, leave blank (importer maps by alpha_2)
        out.append(row)
    with open(OUT + "/bins.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=OUT_COLS, extrasaction="ignore"); w.writeheader(); w.writerows(out)
    with open(OUT + "/conflicts.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["bin", "field", "iannuttall", "venelinkochev", "chosen"]); w.writeheader(); w.writerows(conflicts)
    summary = dict(sorted(stats.items()))
    summary.update({"total": len(out), "input_iannuttall": len(ia), "input_venelinkochev": len(vk),
                    "short_bins_lt6": sum(1 for r in out if len(r["bin"]) < 6), "no_issuer": sum(1 for r in out if not r["issuer"]),
                    "no_country": sum(1 for r in out if not r["alpha_2"]), "generated": datetime.date.today().isoformat()})
    json.dump(summary, open(OUT + "/summary.json", "w"), indent=2)
    print(json.dumps(summary, indent=2))

if __name__ == "__main__":
    main()
