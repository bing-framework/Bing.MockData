#!/usr/bin/env python3
"""Project fixed, checked-in catalog inputs into legacy embedded resource shapes."""

from __future__ import annotations

import argparse
import ast
import hashlib
import json
import re
import sqlite3
from pathlib import Path
from typing import Any, Iterable


FAKER_COMMIT = "df289bfde6b6c739bbac696ef432d69c9ec33ac1"
CLDR_VERSION = "49.0.0-import-v2"
GEONAMES_VERSION = "modified:2026-10-24"
CHINA_VERSION = "2025-12-31"
CHINA_MUNICIPALITY_CODES = {"110000", "120000", "310000", "500000"}
NON_CITY_PREFECTURE_LABELS = {"市辖区", "省直辖县级行政区划", "自治区直辖县级行政区划"}
NON_COUNTRY_CLDR_CODES = {"EU", "EZ", "QO", "UN", "XA", "XB", "ZZ"}
CN_TRANSLATIONS = {
    "black": "黑色",
    "maroon": "栗色",
    "white": "白色",
    "green": "绿色",
    "purple": "紫色",
    "blue": "蓝色",
    "silver": "银色",
    "gray": "灰色",
    "yellow": "黄色",
    "lightyellow": "淡黄色",
    "greenyellow": "绿黄色",
    "fuchsia": "紫红色",
    "aqua": "水绿色",
    "skyblue": "天蓝色",
    "olive": "橄榄色",
}
EXAMPLE_EMAIL_DOMAINS = ["example.com", "example.net", "example.org"]


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_faker_names(source: str, fields: Iterable[str]) -> dict[str, list[str]]:
    """Extract OrderedDict string keys from AST literals; never evaluate source."""
    wanted = set(fields)
    found: dict[str, list[str]] = {}
    tree = ast.parse(source)
    for node in ast.walk(tree):
        if not isinstance(node, ast.Assign):
            continue
        targets = [target.id for target in node.targets if isinstance(target, ast.Name)]
        field = next((name for name in targets if name in wanted), None)
        if field is None:
            continue
        call = node.value
        if not isinstance(call, ast.Call) or not isinstance(call.func, ast.Name) or call.func.id != "OrderedDict":
            raise ValueError(f"{field} must be an OrderedDict literal")
        if len(call.args) != 1 or not isinstance(call.args[0], (ast.Tuple, ast.List)):
            raise ValueError(f"{field} must contain a literal sequence")
        names: list[str] = []
        for entry in call.args[0].elts:
            if not isinstance(entry, (ast.Tuple, ast.List)) or len(entry.elts) != 2:
                raise ValueError(f"{field} contains a non-pair entry")
            name = entry.elts[0]
            if not isinstance(name, ast.Constant) or not isinstance(name.value, str):
                raise ValueError(f"{field} contains a non-string name")
            if not name.value or name.value != name.value.strip():
                raise ValueError(f"{field} contains an empty or padded name")
            names.append(name.value)
        if field in found:
            raise ValueError(f"{field} is defined more than once")
        found[field] = names
    missing = wanted - found.keys()
    if missing:
        raise ValueError(f"missing Faker fields: {', '.join(sorted(missing))}")
    return found


def parse_career_rows(source: str) -> list[list[str]]:
    start = source.find("private static readonly string[][] Rows")
    end = source.find("internal static CareerProfile[] Profiles", start)
    if start < 0 or end < 0:
        raise ValueError("could not locate CareerCatalog.Rows")
    segment = source[start:end]
    rows: list[list[str]] = []
    for match in re.finditer(r"new\[\]\s*\{(?P<body>.*?)\}", segment, re.DOTALL):
        literals = re.findall(r'"(?:\\.|[^"\\])*"', match.group("body"))
        row = [json.loads(literal) for literal in literals]
        if len(row) != 6 or any(not value for value in row):
            raise ValueError("CareerCatalog rows must contain six non-empty strings")
        rows.append(row)
    if not rows:
        raise ValueError("CareerCatalog contains no rows")
    return rows


def parse_css_named_colors(source: str) -> list[tuple[str, str]]:
    marker = "private static readonly string[] Values = ("
    start = source.find(marker)
    if start < 0:
        raise ValueError("could not locate CssNamedColors.Values")
    match = re.search(r'\(\"(?P<values>(?:\\.|[^\"\\])*)\"\)\.Split\(\'\|\'\)', source[start:])
    if match is None:
        raise ValueError("could not parse CSS color table literal")
    raw = json.loads('"' + match.group("values") + '"')
    pairs: list[tuple[str, str]] = []
    for value in raw.split("|"):
        fields = value.split()
        if len(fields) != 2 or not re.fullmatch(r"[0-9A-Fa-f]{6}", fields[1]):
            raise ValueError(f"invalid CSS color entry: {value!r}")
        pairs.append((fields[0], fields[1].upper()))
    names = [name for name, _ in pairs]
    if len(names) != len(set(names)):
        raise ValueError("CSS color names are not unique")
    if len(pairs) != 148:
        raise ValueError(f"expected 148 CSS named colors, got {len(pairs)}")
    return pairs


def project_country_names(rows: Iterable[tuple[str, str]]) -> tuple[list[str], dict[str, int]]:
    result: list[str] = []
    excluded = {"nonRegionCode": 0, "nonCountryRegionCode": 0, "missingEnglishName": 0}
    for code, payload_json in rows:
        if not re.fullmatch(r"[A-Z]{2}", code):
            excluded["nonRegionCode"] += 1
            continue
        if code in NON_COUNTRY_CLDR_CODES:
            excluded["nonCountryRegionCode"] += 1
            continue
        payload = json.loads(payload_json)
        name = payload.get("nameEn")
        if payload.get("nameEnMissing") or not isinstance(name, str) or not name.strip():
            excluded["missingEnglishName"] += 1
            continue
        result.append(name)
    if not result:
        raise ValueError("CLDR projection produced no country or territory names")
    return result, excluded


def project_city_names(rows: Iterable[tuple[str, str, str]]) -> tuple[list[str], int]:
    result: list[str] = []
    fallback_count = 0
    for name, payload_json, kind in rows:
        if kind != "city":
            continue
        payload = json.loads(payload_json)
        ascii_name = payload.get("asciiName")
        if isinstance(ascii_name, str) and ascii_name.strip():
            result.append(ascii_name)
        else:
            if not name.strip():
                raise ValueError("GeoNames city has neither ASCII nor source name")
            result.append(name)
            fallback_count += 1
    if not result:
        raise ValueError("GeoNames projection produced no cities")
    return result, fallback_count


def project_china_divisions(records: list[dict[str, Any]]) -> dict[str, list[Any]]:
    by_code: dict[str, dict[str, Any]] = {}
    for record in records:
        code = record.get("Code")
        name = record.get("Name")
        level = record.get("Level")
        if not isinstance(code, str) or not re.fullmatch(r"\d{6}", code) or not isinstance(name, str) or not name:
            raise ValueError("invalid China division record")
        if level not in (1, 2, 3) or code in by_code:
            raise ValueError(f"invalid or duplicate China division code {code}")
        by_code[code] = record

    def path_for(code: str) -> list[dict[str, Any]]:
        path: list[dict[str, Any]] = []
        seen: set[str] = set()
        current = by_code[code]
        while current is not None:
            current_code = current["Code"]
            if current_code in seen:
                raise ValueError(f"cycle in China division parents at {current_code}")
            seen.add(current_code)
            path.append(current)
            parent_code = current.get("ParentCode")
            if parent_code is None:
                current = None
            else:
                current = by_code.get(parent_code)
                if current is None:
                    raise ValueError(f"missing China division parent {parent_code}")
                if current["Level"] >= path[-1]["Level"]:
                    raise ValueError(f"invalid China division level path at {current_code}")
        path.reverse()
        if not path or path[0]["Level"] != 1:
            raise ValueError(f"China division {code} does not lead to a province")
        return path

    for code in by_code:
        path_for(code)

    provinces = [row for row in records if row["Level"] == 1]
    prefectures = [row for row in records if row["Level"] == 2]
    counties = [row for row in records if row["Level"] == 3]
    if not provinces or not counties:
        raise ValueError("China division hierarchy is incomplete")
    area_codes: list[dict[str, str]] = []
    seen_prefixes: set[str] = set()
    for province in provinces:
        prefix = province["Code"][:2]
        if prefix in seen_prefixes:
            raise ValueError(f"duplicate Chinese identity-card province prefix {prefix}")
        seen_prefixes.add(prefix)
        short_name = province["Name"]
        for suffix in ("特别行政区", "壮族自治区", "回族自治区", "维吾尔自治区", "自治区", "省", "市"):
            if short_name.endswith(suffix):
                short_name = short_name[: -len(suffix)]
                break
        area_codes.append({"Key": prefix, "Value": short_name})

    city_names = [
        row["Name"]
        for row in records
        if (row["Level"] == 2 and row["Name"] not in NON_CITY_PREFECTURE_LABELS)
        or row["Code"] in CHINA_MUNICIPALITY_CODES
    ]
    province_city = ["".join(item["Name"] for item in path_for(row["Code"])) for row in counties]
    return {
        "ChineseRegionList.txt": [row["Name"] for row in provinces],
        "ChineseCityNames.txt": city_names,
        "ProvinceCity.txt": province_city,
        "ChineseAreaCode.json": area_codes,
    }


def project_simplified_chinese(source: str) -> list[str]:
    result: list[str] = []
    seen: set[str] = set()
    for char in source:
        if "\u4e00" <= char <= "\u9fff" and char not in seen:
            result.append(char)
            seen.add(char)
    return result


def read_catalog(path: Path) -> tuple[sqlite3.Connection, dict[str, Any]]:
    connection = sqlite3.connect(f"file:{path.resolve().as_posix()}?mode=ro", uri=True)
    connection.row_factory = sqlite3.Row
    row = connection.execute("SELECT * FROM CatalogInfo LIMIT 1").fetchone()
    if row is None:
        connection.close()
        raise ValueError(f"catalog has no metadata row: {path}")
    return connection, dict(row)


def write_lines(path: Path, values: Iterable[str]) -> None:
    path.write_text("\n".join(values) + "\n", encoding="utf-8", newline="")


def write_json(path: Path, value: Any) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="")


def inspect_old_resource(path: Path) -> dict[str, Any]:
    raw = path.read_bytes()
    text = raw.decode("utf-8-sig")
    if path.suffix == ".json":
        count = len(json.loads(text))
    elif path.name == "SimplifiedChinese.txt":
        count = len(text.rstrip("\r\n"))
    else:
        count = len([line for line in text.splitlines() if line])
    return {"records": count, "sha256": sha256_bytes(raw)}


def run(repo_root: Path, output_dir: Path, evidence_dir: Path) -> dict[str, Any]:
    resource_dir = repo_root / "src/Bing.MockData/Datas/Resources"
    output_dir.mkdir(parents=True, exist_ok=True)
    evidence_dir.mkdir(parents=True, exist_ok=True)
    baseline_path = Path(__file__).with_name("legacy-baseline.json")
    baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
    old_before = {
        name: inspect_old_resource(resource_dir / name)
        for name in baseline
        if (resource_dir / name).exists()
    }
    if old_before != baseline and output_dir.resolve() == resource_dir.resolve():
        # First invocation may replace the baseline resources; a later invocation must still be deterministic.
        allowed_generated = (evidence_dir / "manifest.json").exists()
        if not allowed_generated:
            raise ValueError("legacy resource baseline differs before the first generation")

    faker_path = repo_root / "tools/data/faker-names/en_US.py"
    faker_manifest_path = repo_root / "tools/data/faker-names/manifest.json"
    faker_manifest = json.loads(faker_manifest_path.read_text(encoding="utf-8"))
    if faker_manifest.get("commit") != FAKER_COMMIT:
        raise ValueError("Faker manifest commit does not match the pinned provider input")
    faker_source = faker_path.read_text(encoding="utf-8-sig")
    faker_names = parse_faker_names(faker_source, ("first_names_female", "first_names_male", "last_names"))
    career_path = repo_root / "src/Bing.MockData/Data/ConfigurableSamples.cs"
    career_source = career_path.read_text(encoding="utf-8-sig")
    career_rows = parse_career_rows(career_source)
    color_rows = parse_css_named_colors(career_source)

    cldr_path = repo_root / "src/Bing.MockData.Catalogs/Data/cldr.sqlite3"
    geonames_path = repo_root / "src/Bing.MockData.Catalogs/Data/geonames-cities.sqlite3"
    cldr, cldr_info = read_catalog(cldr_path)
    geonames, geonames_info = read_catalog(geonames_path)
    try:
        if cldr_info["Id"] != "cldr" or cldr_info["Version"] != CLDR_VERSION:
            raise ValueError(f"unexpected CLDR snapshot: {cldr_info['Id']} {cldr_info['Version']}")
        if geonames_info["Id"] != "geonames-cities" or geonames_info["Version"] != GEONAMES_VERSION:
            raise ValueError(f"unexpected GeoNames snapshot: {geonames_info['Id']} {geonames_info['Version']}")
        cldr_region_rows = cldr.execute("SELECT COUNT(*) FROM Records WHERE Kind = 'region'").fetchone()[0]
        country_rows = (
            (row["CountryCode"] or "", row["PayloadJson"])
            for row in cldr.execute("SELECT CountryCode, PayloadJson FROM Records WHERE Kind = 'region' ORDER BY Ordinal")
        )
        country_names, country_excluded = project_country_names(country_rows)
        city_rows = (
            (row["Name"], row["PayloadJson"], row["Kind"])
            for row in geonames.execute("SELECT Name, PayloadJson, Kind FROM Records WHERE Kind = 'city' ORDER BY Ordinal")
        )
        city_names, city_fallbacks = project_city_names(city_rows)
    finally:
        cldr.close()
        geonames.close()

    china_path = resource_dir / "ChinaDivisions2025.json"
    china_records = json.loads(china_path.read_text(encoding="utf-8-sig"))
    if len(china_records) != 3212:
        raise ValueError(f"unexpected China division snapshot row count: {len(china_records)}")
    china = project_china_divisions(china_records)
    colors_en = [name for name, _ in color_rows]
    colors_zh = [CN_TRANSLATIONS.get(name, name) for name in colors_en]
    jobs_en = [row[5] for row in career_rows]
    jobs_zh = [row[4] for row in career_rows]

    outputs: dict[str, tuple[Any, str]] = {
        "FemaleNames.txt": (faker_names["first_names_female"], "lines"),
        "MaleNames.txt": (faker_names["first_names_male"], "lines"),
        "LastNames.txt": (faker_names["last_names"], "lines"),
        "CountryNames.txt": (country_names, "lines"),
        "CityNames.txt": (city_names, "lines"),
        "ChineseRegionList.txt": (china["ChineseRegionList.txt"], "lines"),
        "ChineseCityNames.txt": (china["ChineseCityNames.txt"], "lines"),
        "ProvinceCity.txt": (china["ProvinceCity.txt"], "lines"),
        "ChineseAreaCode.json": (china["ChineseAreaCode.json"], "json"),
        "Jobs.en.json": (jobs_en, "json"),
        "Jobs.zh-cn.json": (jobs_zh, "json"),
        "Colors.en.json": (colors_en, "json"),
        "Colors.zh-cn.json": (colors_zh, "json"),
        "Emails.json": (EXAMPLE_EMAIL_DOMAINS, "json"),
    }
    for name, (value, kind) in outputs.items():
        path = output_dir / name
        if kind == "json":
            write_json(path, value)
        else:
            write_lines(path, value)

    charset_source_path = Path(__file__).parent / "sources/simplified-chinese.txt"
    charset_source = charset_source_path.read_text(encoding="utf-8")
    charset = project_simplified_chinese(charset_source)
    if len(charset) < 500:
        raise ValueError(f"independent Chinese character pool is too small: {len(charset)}")
    (output_dir / "SimplifiedChinese.txt").write_text("".join(charset), encoding="utf-8", newline="")
    outputs["SimplifiedChinese.txt"] = (charset, "characters")

    raw_china_path = repo_root / "tools/data/yescallop-areacodes-2da87e7/2025.txt"
    source_specs: dict[str, dict[str, Any]] = {}
    for name in outputs:
        if name in ("FemaleNames.txt", "MaleNames.txt", "LastNames.txt"):
            source_specs[name] = {
                "source": "Faker en_US person provider",
                "version": f"git:{FAKER_COMMIT}",
                "uri": "https://github.com/joke2k/faker/blob/df289bfde6b6c739bbac696ef432d69c9ec33ac1/faker/providers/person/en_US/__init__.py",
                "license": "MIT",
                "inputSha256": sha256_file(faker_path),
                "transformation": "AST-extract OrderedDict string keys; weights discarded; source order preserved",
            }
        elif name == "CountryNames.txt":
            source_specs[name] = {
                "source": cldr_info["Id"],
                "version": cldr_info["Version"],
                "uri": cldr_info["SourceUri"],
                "license": cldr_info["License"],
                "inputSha256": sha256_file(cldr_path),
                "upstreamSourceSha256": cldr_info["SourceSha256"],
                "transformation": "CLDR region rows projected to two-letter country/territory codes with English names; groups, pseudo-regions, unknown region and missing names excluded",
            }
        elif name == "CityNames.txt":
            source_specs[name] = {
                "source": geonames_info["Id"],
                "version": geonames_info["Version"],
                "uri": geonames_info["SourceUri"],
                "license": geonames_info["License"],
                "inputSha256": sha256_file(geonames_path),
                "upstreamSourceSha256": geonames_info["SourceSha256"],
                "transformation": "GeoNames city rows projected to asciiName, with source Name fallback only when asciiName is empty; source order preserved",
            }
        elif name in ("ChineseRegionList.txt", "ChineseCityNames.txt", "ProvinceCity.txt", "ChineseAreaCode.json"):
            source_specs[name] = {
                "source": "yescallop/areacodes 2025-12-31 administrative divisions",
                "version": CHINA_VERSION,
                "uri": "https://github.com/yescallop/areacodes/tree/2da87e7648f0da7e10dea39e8a613107ed3fe273",
                "license": "CC0-1.0",
                "inputSha256": sha256_file(china_path),
                "upstreamRawSha256": sha256_file(raw_china_path),
                "transformation": {
                    "ChineseRegionList.txt": "Province-level names only; replaces seven legacy macro-region labels with 34 source province names",
                    "ChineseCityNames.txt": "All prefecture-level names plus the four direct-administered municipalities represented by their source province records",
                    "ProvinceCity.txt": "County-level records projected by concatenating the actual province-to-county parent path; no missing level is fabricated",
                    "ChineseAreaCode.json": "Province Code first two digits mapped to the province short name; these are Chinese identity-card prefixes, not telephone area codes",
                }[name],
            }
        elif name in ("Jobs.en.json", "Jobs.zh-cn.json"):
            source_specs[name] = {
                "source": "project-authored CareerCatalog rows",
                "version": "project-samples-2026-10",
                "uri": "src/Bing.MockData/Data/ConfigurableSamples.cs#CareerCatalog",
                "license": "MIT",
                "inputSha256": sha256_file(career_path),
                "transformation": "CareerCatalog job field projected in declaration order; repeated jobs across different profiles are retained",
            }
        elif name in ("Colors.en.json", "Colors.zh-cn.json"):
            source_specs[name] = {
                "source": "W3C CSS Color 4 named colors, mirrored by project CSS table",
                "version": "CSS Color 4 snapshot checked 2026-10-25",
                "uri": "https://www.w3.org/TR/2026/CRD-css-color-4-20261022/#named-colors",
                "license": "W3C Software and Document License 2023; project-generated name projection",
                "inputSha256": sha256_file(career_path),
                "transformation": "All 148 verified CSS identifiers; zh-cn has 15 existing project labels and falls back to the stable English identifier for the remaining 133",
            }
        elif name == "Emails.json":
            source_specs[name] = {
                "source": "IANA reserved example domains",
                "version": "RFC 2606",
                "uri": "https://www.rfc-editor.org/rfc/rfc2606",
                "license": "Not applicable; reserved documentation domains",
                "inputSha256": None,
                "transformation": "Exact reserved host names only; no unreserved subdomains are created",
            }
        elif name == "SimplifiedChinese.txt":
            source_specs[name] = {
                "source": "project-authored simulated common-character set",
                "version": "project-samples-2026-10",
                "uri": "tools/LegacyResourceImport/sources/simplified-chinese.txt",
                "license": "MIT",
                "inputSha256": sha256_file(charset_source_path),
                "transformation": "Keep unique Basic CJK Unified Ideographs in source order; discard whitespace and non-CJK characters",
            }

    entries = []
    for name, (value, _) in outputs.items():
        path = output_dir / name
        if name.endswith(".json"):
            decoded = json.loads(path.read_text(encoding="utf-8"))
            record_count = len(decoded)
        elif name == "SimplifiedChinese.txt":
            record_count = len(value)
        else:
            record_count = len(value)
        old = baseline.get(name)
        entry = {
            "resource": name,
            "source": source_specs[name],
            "legacyBaseline": old,
            "outputRecords": record_count,
            "uniqueOutputRecords": len(set(value)) if name != "ChineseAreaCode.json" else len({item["Key"] for item in value}),
            "outputSha256": sha256_file(path),
            "outputBytes": path.stat().st_size,
        }
        if name == "CountryNames.txt":
            entry["sourceRows"] = cldr_region_rows
            entry["excluded"] = country_excluded
        if name == "CityNames.txt":
            entry["sourceRows"] = geonames_info["RecordCount"]
            entry["asciiNameFallbacks"] = city_fallbacks
        if name in ("Jobs.en.json", "Jobs.zh-cn.json"):
            entry["sourceRows"] = len(career_rows)
        if name in ("Colors.en.json", "Colors.zh-cn.json"):
            entry["sourceRows"] = len(color_rows)
        if name in ("ChineseRegionList.txt", "ChineseCityNames.txt", "ProvinceCity.txt", "ChineseAreaCode.json"):
            entry["sourceRows"] = len(china_records)
            entry["chinaDivisionSourceSha256"] = sha256_file(raw_china_path)
        entries.append(entry)

    iban_manifest_path = repo_root / "tools/data/iban-js-0.0.14/manifest.json"
    iban_manifest = json.loads(iban_manifest_path.read_text(encoding="utf-8"))
    iban_conversion = iban_manifest.get("conversion", {})
    iban_outputs = iban_conversion.get("outputs", [])
    if iban_conversion.get("status") != "PASS" or len(iban_outputs) != 2:
        raise ValueError("fixed IBAN/BBAN derivation manifest is incomplete")
    external_iban_outputs: dict[str, Any] = {}
    for output in iban_outputs:
        name = output["file"]
        path = resource_dir / name
        if name not in ("IBAN.txt", "BBAN.txt") or not path.is_file():
            raise ValueError(f"unexpected IBAN/BBAN output {name}")
        actual_sha = sha256_file(path)
        actual_records = len([line for line in path.read_text(encoding="utf-8-sig").splitlines() if line])
        if actual_sha != output["sha256"] or actual_records != output["records"]:
            raise ValueError(f"IBAN/BBAN output does not match its source manifest: {name}")
        external_iban_outputs[name] = {
            "records": actual_records,
            "sha256": actual_sha,
            "status": "PASS",
        }
    banks = inspect_old_resource(resource_dir / "Banks.json")

    manifest = {
        "formatVersion": 1,
        "generatedBy": "tools/LegacyResourceImport/import_resources.py",
        "inputs": {
            "fakerCommit": FAKER_COMMIT,
            "cldrVersion": cldr_info["Version"],
            "geonamesVersion": geonames_info["Version"],
            "chinaDivisionVersion": CHINA_VERSION,
        },
        "resources": entries,
        "externallyVerified": {
            "sourceManifest": "tools/data/iban-js-0.0.14/manifest.json",
            "sourceVersion": iban_conversion["sourceVersion"],
            "license": iban_manifest["license"],
            "sourceRecords": iban_conversion["sourceRecords"],
            "retainedCountries": len(iban_conversion["retainedCountries"]),
            "excludedRecords": iban_conversion["excludedRecords"],
            "outputs": external_iban_outputs,
        },
        "blockedUnchanged": {
            "Banks.json": {
                "status": "BLOCKED_EXTERNAL",
                "source": "repository legacy snapshot; upstream unverified",
                "license": None,
                "records": banks["records"],
                "sha256": banks["sha256"],
                "reason": "No verified source/license was available for replacement; original bytes retained.",
            },
        },
    }
    faker_output_records = []
    for name in ("FemaleNames.txt", "MaleNames.txt", "LastNames.txt"):
        path = output_dir / name
        faker_output_records.append({
            "file": name,
            "records": len(outputs[name][0]),
            "sha256": sha256_file(path),
        })
    faker_manifest["records"] = len(faker_names["first_names_male"])
    faker_manifest["sha256"] = next(item["sha256"] for item in faker_output_records if item["file"] == "MaleNames.txt")
    faker_manifest["outputs"] = faker_output_records
    write_json(faker_manifest_path, faker_manifest)
    write_json(Path(__file__).with_name("manifest.json"), manifest)
    write_json(evidence_dir / "manifest.json", manifest)
    write_json(evidence_dir / "verification.json", {
        "status": "PASS",
        "inputVersions": manifest["inputs"],
        "verifiedResources": len(entries),
        "recordsByResource": {entry["resource"]: entry["outputRecords"] for entry in entries},
        "countryProjection": {
            "cldrRegionRows": cldr_region_rows,
            "outputRows": len(country_names),
            "excluded": country_excluded,
        },
        "cityProjection": {
            "geoNamesCityRows": geonames_info["RecordCount"],
            "outputRows": len(city_names),
            "asciiNameFallbacks": city_fallbacks,
        },
        "chinaProjection": {
            "sourceRows": len(china_records),
            "parentPathsValidated": len(china_records),
            "provinceRows": len(china["ChineseRegionList.txt"]),
            "provincePrefixRows": len(china["ChineseAreaCode.json"]),
            "countyPaths": len(china["ProvinceCity.txt"]),
        },
        "externalDerivations": manifest["externallyVerified"],
        "blocked": manifest["blockedUnchanged"],
    })
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--evidence-dir", type=Path, default=Path("artifacts/catalog-closure/resources"))
    args = parser.parse_args()
    root = args.repo_root.resolve()
    output = args.output_dir.resolve() if args.output_dir else root / "src/Bing.MockData/Datas/Resources"
    evidence = args.evidence_dir if args.evidence_dir.is_absolute() else root / args.evidence_dir
    manifest = run(root, output, evidence.resolve())
    print(json.dumps({
        "output": str(output),
        "evidence": str(evidence.resolve()),
        "resources": len(manifest["resources"]),
        "counts": {item["resource"]: item["outputRecords"] for item in manifest["resources"]},
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
