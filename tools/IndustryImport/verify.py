"""Read-only integrity and catalog relationship checks."""

import argparse
from collections import Counter
import hashlib
import json
import sqlite3
import sys
from contextlib import closing
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DATA = ROOT / "src" / "Bing.MockData.Catalogs" / "Data"
DEFAULT_RAW = ROOT / "tools" / "CatalogImport" / "raw"
DEFAULT_RELATIONSHIP_REPORT = ROOT / "artifacts" / "domain-gap-closure" / "catalog-relations.json"
EXPECTED_VERSIONS = {
    "cldr": "49.0.0-import-v2",
    "pci-ids": "fe7fceb2e1153d495e1deefacf59c5d61c36ef8e",
    "ourairports": "git:711cf6754ef05a1ccedc2482cc1e838032249d3c",
}


def connect(path):
    return sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True)


def scalar(db, sql, params=()):
    return db.execute(sql, params).fetchone()[0]


def examples(db, sql, params=(), limit=10):
    return [dict(row) for row in db.execute(sql, params).fetchmany(limit)]


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def classify_missing_targets(missing_counts, source_targets, present_classification, present_reason,
                             absent_classification, absent_reason):
    groups = {present_classification: [], absent_classification: []}
    for target, count in sorted(missing_counts.items()):
        classification = present_classification if target in source_targets else absent_classification
        groups[classification].append({"target": target, "referenceRows": count})
    return [
        {
            "classification": classification,
            "referenceRows": sum(item["referenceRows"] for item in groups[classification]),
            "distinctMissingTargets": len(groups[classification]),
            "targets": groups[classification],
            "sourceAllowedMissingCount": 0,
            "reason": reason,
        }
        for classification, reason in ((present_classification, present_reason),
                                       (absent_classification, absent_reason))
    ]


def cldr_source(raw_dir):
    folder = raw_dir / "cldr" / "49.0.0"
    territory_en = read_json(folder / "territories-en.json")["main"]["en"]["localeDisplayNames"]["territories"]
    territory_zh = read_json(folder / "territories-zh.json")["main"]["zh"]["localeDisplayNames"]["territories"]
    currency_en = read_json(folder / "currencies-en.json")["main"]["en"]["numbers"]["currencies"]
    currency_zh = read_json(folder / "currencies-zh.json")["main"]["zh"]["numbers"]["currencies"]
    currency_regions = read_json(folder / "currencyData.json")["supplemental"]["currencyData"]["region"]
    currency_en_names = {
        code: entry.get("displayName") if isinstance(entry, dict) else None
        for code, entry in currency_en.items()
    }
    currency_zh_names = {
        code: entry.get("displayName") if isinstance(entry, dict) else None
        for code, entry in currency_zh.items()
    }

    relation_keys = Counter()
    current_currency_targets = set()
    ended_currency_targets = set()
    for region, entries in currency_regions.items():
        if not isinstance(entries, list):
            continue
        history_index = 0
        for entry in entries:
            if not isinstance(entry, dict):
                continue
            for code in entry:
                relation_keys[f"region-currency|{region}|{code}|{history_index}"] += 1
                interval = entry[code]
                if isinstance(interval, dict) and interval.get("_to") is not None:
                    ended_currency_targets.add(code)
                else:
                    current_currency_targets.add(code)
            history_index += 1

    return {
        "territoryEnNames": territory_en,
        "territoryZhNames": territory_zh,
        "currencyEnNames": currency_en_names,
        "currencyZhNames": currency_zh_names,
        "territoryEnTargets": set(territory_en),
        "territoryZhTargets": set(territory_zh),
        "territoryTargets": set(territory_en) | set(territory_zh),
        "currencyTargets": set(currency_en) | set(currency_zh),
        "currencyEnTargets": {code for code, name in currency_en_names.items() if name is not None},
        "currencyZhTargets": {code for code, name in currency_zh_names.items() if name is not None},
        "regionReferenceTargets": set(currency_regions),
        "relationCurrencyTargets": {key.split("|")[2] for key in relation_keys},
        "currentCurrencyTargets": current_currency_targets,
        "endedCurrencyTargets": ended_currency_targets,
        "relationKeys": relation_keys,
    }


def relation_coverage(source_keys, catalog_keys):
    source_only = source_keys - catalog_keys
    catalog_only = catalog_keys - source_keys
    return {
        "sourceRows": sum(source_keys.values()),
        "catalogRows": sum(catalog_keys.values()),
        "sourceReferencesAbsentFromCatalog": sum(source_only.values()),
        "catalogReferencesAbsentFromSource": sum(catalog_only.values()),
        "sourceOnlyExamples": list(sorted(source_only.elements()))[:10],
        "catalogOnlyExamples": list(sorted(catalog_only.elements()))[:10],
    }


def missing_history_counts(db, missing_predicate):
    ended, open_ended = db.execute(f"""
        SELECT sum(CASE WHEN nullif(trim(json_extract(s.PayloadJson, '$._to')), '') IS NOT NULL THEN 1 ELSE 0 END),
               sum(CASE WHEN nullif(trim(json_extract(s.PayloadJson, '$._to')), '') IS NULL THEN 1 ELSE 0 END)
        FROM Records s WHERE s.Kind='region-currency' AND {missing_predicate}
    """).fetchone()
    return {"endedRows": ended or 0, "withoutEndDateRows": open_ended or 0}


def snapshot(db, dataset_id, expected_version=None):
    db.row_factory = sqlite3.Row
    info = dict(db.execute("SELECT Id, Version, SnapshotDate, RecordCount, SourceSha256 FROM CatalogInfo").fetchone())
    actual_count = scalar(db, "SELECT count(*) FROM Records")
    if info["Id"] != dataset_id or info["RecordCount"] != actual_count:
        raise ValueError(f"{dataset_id} CatalogInfo does not match its Records table")
    expected_version = expected_version or EXPECTED_VERSIONS[dataset_id]
    return {
        "id": info["Id"],
        "version": info["Version"],
        "snapshotDate": info["SnapshotDate"],
        "recordCount": actual_count,
        "sourceSha256": info["SourceSha256"],
        "expectedVersion": expected_version,
        "versionMatchesExpected": info["Version"] == expected_version,
    }


def relation_result(source_rows, missing_count, empty_reference_count=0, allowed_missing_count=0,
                    allowed_reason=None, unexpected_examples=None, allowed_examples=None):
    unexpected = missing_count + empty_reference_count - allowed_missing_count
    return {
        "sourceRows": source_rows,
        "missingReferenceCount": missing_count,
        "emptyReferenceCount": empty_reference_count,
        "sourceAllowedMissingCount": allowed_missing_count,
        "unexpectedMissingCount": unexpected,
        "sourceAllowedMissingReason": allowed_reason or "No source-defined missing-value exception; missing references are not ignored.",
        "unexpectedExamples": unexpected_examples or [],
        "allowedMissingExamples": allowed_examples or [],
    }


def key_relation(db, relation, source_kind, reference_fields, target_key_expression, source_rows=None):
    expressions = [f"json_extract(s.PayloadJson, '$.{field}')" for field in reference_fields]
    source_rows = source_rows if source_rows is not None else scalar(
        db, "SELECT count(*) FROM Records WHERE Kind=?", (source_kind,))
    empty_predicate = " OR ".join(f"nullif(trim({expression}), '') IS NULL" for expression in expressions)
    present_predicate = " AND ".join(f"nullif(trim({expression}), '') IS NOT NULL" for expression in expressions)
    empty_count = scalar(db, f"SELECT count(*) FROM Records s WHERE s.Kind=? AND ({empty_predicate})", (source_kind,))
    missing_count = scalar(db, f"""
        SELECT count(*) FROM Records s
        WHERE s.Kind=? AND ({present_predicate})
          AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key={target_key_expression})
    """, (source_kind,))
    missing_examples = examples(db, f"""
        SELECT s.Key AS sourceKey, s.PayloadJson AS payload
        FROM Records s
        WHERE s.Kind=? AND ({present_predicate})
          AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key={target_key_expression})
        ORDER BY s.Key LIMIT 10
    """, (source_kind,)) if missing_count else []
    empty_examples = examples(db, f"""
        SELECT s.Key AS sourceKey, s.PayloadJson AS payload
        FROM Records s WHERE s.Kind=? AND ({empty_predicate})
        ORDER BY s.Key LIMIT 10
    """, (source_kind,)) if empty_count else []
    return relation_result(source_rows, missing_count, empty_count,
                           unexpected_examples=missing_examples + empty_examples)


def validate_cldr(data_dir, raw_dir=None):
    path = data_dir / "cldr.sqlite3"
    with closing(connect(path)) as db:
        info = snapshot(db, "cldr")
        total = scalar(db, "SELECT count(*) FROM Records WHERE Kind='region-currency'")
        region_refs = key_relation(db, "region-currency.region -> region", "region-currency", ["region"],
                                   "'region|' || json_extract(s.PayloadJson, '$.region')", total)
        empty_currency = scalar(db, """
            SELECT count(*) FROM Records s WHERE s.Kind='region-currency'
              AND nullif(trim(json_extract(s.PayloadJson, '$.currencyCode')), '') IS NULL
        """)
        missing_currency = scalar(db, """
            SELECT count(*) FROM Records s WHERE s.Kind='region-currency'
              AND nullif(trim(json_extract(s.PayloadJson, '$.currencyCode')), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key='currency|' || json_extract(s.PayloadJson, '$.currencyCode'))
        """)
        missing_examples = examples(db, """
            SELECT s.Key AS sourceKey, json_extract(s.PayloadJson, '$.currencyCode') AS currencyCode,
                   json_extract(s.PayloadJson, '$._from') AS validFrom, json_extract(s.PayloadJson, '$._to') AS validTo
            FROM Records s WHERE s.Kind='region-currency'
              AND nullif(trim(json_extract(s.PayloadJson, '$.currencyCode')), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key='currency|' || json_extract(s.PayloadJson, '$.currencyCode'))
            ORDER BY s.Key LIMIT 10
        """) if missing_currency else []
        missing_currency_counts = dict(db.execute("""
            SELECT json_extract(s.PayloadJson, '$.currencyCode'), count(*)
            FROM Records s WHERE s.Kind='region-currency'
              AND nullif(trim(json_extract(s.PayloadJson, '$.currencyCode')), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key='currency|' || json_extract(s.PayloadJson, '$.currencyCode'))
            GROUP BY 1 ORDER BY 1
        """))
        missing_currency_history = missing_history_counts(db, """
            nullif(trim(json_extract(s.PayloadJson, '$.currencyCode')), '') IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key='currency|' || json_extract(s.PayloadJson, '$.currencyCode'))
        """)
        currency_refs = relation_result(
            total, missing_currency, empty_currency,
            allowed_reason="CLDR region-currency records require currency targets; historical _to is not an absent-reference exception.",
            unexpected_examples=missing_examples)
        currency_refs["missingReferenceDateEvidence"] = {
            **missing_currency_history,
            "reason": "_to marks the reference as ended history but does not make its missing currency target an allowed absence.",
        }

        missing_region_counts = dict(db.execute("""
            SELECT json_extract(s.PayloadJson, '$.region'), count(*)
            FROM Records s WHERE s.Kind='region-currency'
              AND nullif(trim(json_extract(s.PayloadJson, '$.region')), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key='region|' || json_extract(s.PayloadJson, '$.region'))
            GROUP BY 1 ORDER BY 1
        """))
        missing_region_history = missing_history_counts(db, """
            nullif(trim(json_extract(s.PayloadJson, '$.region')), '') IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key='region|' || json_extract(s.PayloadJson, '$.region'))
        """)
        region_examples = examples(db, """
            SELECT s.Key AS sourceKey, json_extract(s.PayloadJson, '$.region') AS region,
                   json_extract(s.PayloadJson, '$.currencyCode') AS currencyCode,
                   json_extract(s.PayloadJson, '$._from') AS validFrom, json_extract(s.PayloadJson, '$._to') AS validTo
            FROM Records s WHERE s.Kind='region-currency'
              AND nullif(trim(json_extract(s.PayloadJson, '$.region')), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key='region|' || json_extract(s.PayloadJson, '$.region'))
            ORDER BY s.Key
        """) if missing_region_counts else []
        region_refs["unexpectedExamples"] = region_examples
        region_refs["sourceAllowedMissingReason"] = "A region-currency record requires an imported region target; historical dates do not make a missing target an allowed absence."
        region_refs["missingReferenceDateEvidence"] = {
            **missing_region_history,
            "reason": "_to marks the assignment as ended history but does not make its missing region target an allowed absence.",
        }

        if raw_dir is not None:
            source = cldr_source(raw_dir)
            region_refs["missingReferenceClassifications"] = classify_missing_targets(
                missing_region_counts, source["territoryTargets"],
                "sourceTerritoryNamePresentButNotImported",
                "The pinned English or Chinese territory file names this target, so the CLDR importer can add it locally.",
                "VERIFIED_SOURCE_BOUNDARY",
                "Neither pinned territory display-name input contains this referenced territory; this is an upstream source boundary and must remain a missing reference.")
            currency_refs["missingReferenceClassifications"] = classify_missing_targets(
                missing_currency_counts, source["currencyTargets"],
                "sourceCurrencyNamePresentButNotImported",
                "The pinned English or Chinese currency file names this target, so its absent catalog record is a locally repairable import coverage gap.",
                "VERIFIED_SOURCE_BOUNDARY",
                "Neither pinned currency display-name input contains this code; no source-backed currency entity is available in this snapshot.")
            catalog_relation_keys = Counter(row[0] for row in db.execute(
                "SELECT Key FROM Records WHERE Kind='region-currency'"))
            source_coverage = relation_coverage(source["relationKeys"], catalog_relation_keys)
            catalog_regions = {
                row[0].split("|", 1)[1]: {"name": row[1], "payload": json.loads(row[2])}
                for row in db.execute("SELECT Key, Name, PayloadJson FROM Records WHERE Kind='region'")
            }
            catalog_currencies = {
                row[0].split("|", 1)[1]: {"name": row[2], "payload": json.loads(row[1])}
                for row in db.execute("SELECT Key, PayloadJson, Name FROM Records WHERE Kind='currency'")
            }
            expected_region_codes = source["territoryTargets"] | source["regionReferenceTargets"]
            regions_without_names = source["regionReferenceTargets"] - source["territoryTargets"]
            region_metadata_matches = all(
                code in catalog_regions
                and catalog_regions[code]["name"] == source["territoryEnNames"].get(code, code)
                and catalog_regions[code]["payload"].get("nameEn") == source["territoryEnNames"].get(code)
                and catalog_regions[code]["payload"].get("nameZh") == source["territoryZhNames"].get(code)
                and catalog_regions[code]["payload"].get("nameEnMissing") is (code not in source["territoryEnTargets"])
                and catalog_regions[code]["payload"].get("nameZhMissing") is (code not in source["territoryZhTargets"])
                for code in expected_region_codes
            ) and set(catalog_regions) == expected_region_codes
            catalog_current_currencies = {
                code for code, record in catalog_currencies.items() if record["payload"].get("isCurrent") is True
            }
            expected_currency_codes = source["currencyTargets"] | source["relationCurrencyTargets"]
            currency_metadata_matches = all(
                code in catalog_currencies
                and catalog_currencies[code]["name"] == source["currencyEnNames"].get(code, code)
                and catalog_currencies[code]["payload"].get("nameEn") == source["currencyEnNames"].get(code)
                and catalog_currencies[code]["payload"].get("nameZh") == source["currencyZhNames"].get(code)
                and catalog_currencies[code]["payload"].get("nameEnMissing") is (code not in source["currencyEnNames"])
                and catalog_currencies[code]["payload"].get("nameZhMissing") is (code not in source["currencyZhNames"])
                and catalog_currencies[code]["payload"].get("isCurrent") is (code in source["currentCurrencyTargets"])
                for code in expected_currency_codes
            ) and set(catalog_currencies) == expected_currency_codes
            source_entity_coverage = {
                "regions": {
                    "sourceLocalizedNameCodes": len(source["territoryTargets"]),
                    "sourceCurrencyRegionCodes": len(source["regionReferenceTargets"]),
                    "catalogRecords": len(catalog_regions),
                    "missingReferencedCodes": sorted(source["regionReferenceTargets"] - set(catalog_regions)),
                    "sourceCodesMissingFromCatalog": sorted(expected_region_codes - set(catalog_regions)),
                    "catalogCodesAbsentFromSource": sorted(set(catalog_regions) - expected_region_codes),
                    "referencedCodesWithoutEnglishName": sorted(source["regionReferenceTargets"] - source["territoryEnTargets"]),
                    "referencedCodesWithoutChineseName": sorted(source["regionReferenceTargets"] - source["territoryZhTargets"]),
                    "referencedCodesWithoutEnglishOrChineseName": sorted(regions_without_names),
                    "localizedMetadataMatchesSource": region_metadata_matches,
                },
                "currencies": {
                    "sourceLocalizedNameCodes": len(source["currencyEnTargets"] | source["currencyZhTargets"]),
                    "sourceRelationCodes": len(source["relationCurrencyTargets"]),
                    "sourceCurrentRelationCodes": len(source["currentCurrencyTargets"]),
                    "sourceEndedRelationCodes": len(source["endedCurrencyTargets"]),
                    "catalogRecords": len(catalog_currencies),
                    "catalogCurrentRecords": len(catalog_current_currencies),
                    "catalogNonCurrentRecords": len(catalog_currencies) - len(catalog_current_currencies),
                    "missingSourceCodes": sorted((source["currencyTargets"] | source["relationCurrencyTargets"]) - set(catalog_currencies)),
                    "catalogCodesAbsentFromSource": sorted(set(catalog_currencies) - expected_currency_codes),
                    "historicalRelationCodesNowIncluded": sorted(source["relationCurrencyTargets"] - source["currentCurrencyTargets"]),
                    "currentFlagsMatchSource": catalog_current_currencies == source["currentCurrencyTargets"],
                    "localizedMetadataMatchesSource": currency_metadata_matches,
                },
            }
        else:
            source_coverage = None
            source_entity_coverage = None

    return {
        "snapshot": info,
        "sourceRelationCoverage": source_coverage,
        "sourceEntityCoverage": source_entity_coverage,
        "checks": {
            "region-currency.region -> region": region_refs,
            "region-currency.currencyCode -> currency": currency_refs,
        },
    }


def pci_source_evidence(path, targets, wanted_references):
    vendors = set()
    references = set()
    vendor_id = device_id = None
    in_class = False
    with path.open(encoding="utf-8") as source:
        for line in source:
            if not line or line.startswith("#"):
                continue
            if line.startswith("C "):
                in_class = True
                vendor_id = device_id = None
                continue
            depth = len(line) - len(line.lstrip("\t"))
            fields = line.lstrip("\t").split()
            if not fields:
                continue
            identifier = fields[0].lower()
            if depth == 0 and len(identifier) == 4 and all(character in "0123456789abcdef" for character in identifier):
                vendor_id, device_id, in_class = identifier, None, False
                if identifier in targets:
                    vendors.add(identifier)
            elif in_class:
                continue
            elif depth == 1 and vendor_id is not None and len(identifier) == 4 and all(character in "0123456789abcdef" for character in identifier):
                device_id = identifier
            elif depth == 2 and vendor_id is not None and device_id is not None and len(fields) >= 2:
                subvendor_id, subdevice_id = fields[0].lower(), fields[1].lower()
                if len(subvendor_id) == len(subdevice_id) == 4 and all(
                        character in "0123456789abcdef" for character in subvendor_id + subdevice_id):
                    reference = (vendor_id, device_id, subvendor_id, subdevice_id)
                    if reference in wanted_references:
                        references.add(reference)
    return vendors, references


def validate_pci_snapshot(path, dataset_id, expected_version, raw_dir=None):
    with closing(connect(path)) as db:
        info = snapshot(db, dataset_id, expected_version)
        checks = {
            "device.vendorId -> vendor": key_relation(db, "device.vendorId -> vendor", "device", ["vendorId"],
                                                       "'vendor|' || json_extract(s.PayloadJson, '$.vendorId')"),
            "subsystem.vendorId/deviceId -> device": key_relation(db, "subsystem.vendorId/deviceId -> device", "subsystem", ["vendorId", "deviceId"],
                "'device|' || json_extract(s.PayloadJson, '$.vendorId') || '|' || json_extract(s.PayloadJson, '$.deviceId')"),
            "subsystem.subvendorId -> vendor": key_relation(db, "subsystem.subvendorId -> vendor", "subsystem", ["subvendorId"],
                "'vendor|' || json_extract(s.PayloadJson, '$.subvendorId')"),
            "subclass.classId -> class": key_relation(db, "subclass.classId -> class", "subclass", ["classId"],
                "'class|' || json_extract(s.PayloadJson, '$.classId')"),
            "interface.classId/subclassId -> subclass": key_relation(db, "interface.classId/subclassId -> subclass", "interface", ["classId", "subclassId"],
                "'subclass|' || json_extract(s.PayloadJson, '$.classId') || '|' || json_extract(s.PayloadJson, '$.subclassId')"),
        }
        missing_subvendors = dict(db.execute("""
            SELECT json_extract(s.PayloadJson, '$.subvendorId'), count(*)
            FROM Records s WHERE s.Kind='subsystem'
              AND nullif(trim(json_extract(s.PayloadJson, '$.subvendorId')), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key='vendor|' || json_extract(s.PayloadJson, '$.subvendorId'))
            GROUP BY 1 ORDER BY 1
        """))
        missing_subsystem_rows = db.execute("""
            SELECT json_extract(s.PayloadJson, '$.vendorId'), json_extract(s.PayloadJson, '$.deviceId'),
                   json_extract(s.PayloadJson, '$.subvendorId'), json_extract(s.PayloadJson, '$.subdeviceId')
            FROM Records s WHERE s.Kind='subsystem'
              AND nullif(trim(json_extract(s.PayloadJson, '$.subvendorId')), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key='vendor|' || json_extract(s.PayloadJson, '$.subvendorId'))
            ORDER BY s.Key
        """).fetchall()
        missing_subvendor_examples = examples(db, """
            SELECT s.Key AS sourceKey, json_extract(s.PayloadJson, '$.subvendorId') AS subvendorId,
                   json_extract(s.PayloadJson, '$.subdeviceId') AS subdeviceId,
                   json_extract(s.PayloadJson, '$.vendorId') AS deviceVendorId,
                   json_extract(s.PayloadJson, '$.deviceId') AS deviceId
            FROM Records s WHERE s.Kind='subsystem'
              AND nullif(trim(json_extract(s.PayloadJson, '$.subvendorId')), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM Records t WHERE t.Key='vendor|' || json_extract(s.PayloadJson, '$.subvendorId'))
            ORDER BY s.Key
        """) if missing_subvendors else []
    for result in checks.values():
        result["sourceAllowedMissingReason"] = "pci.ids encodes the required identifiers in the nested hierarchy; no null or orphan-parent exception is documented."
    source_evidence = None
    if raw_dir is not None:
        raw_path = raw_dir / "pciids" / EXPECTED_VERSIONS["pci-ids"] / "pci.ids"
        wanted_references = {tuple(row) for row in missing_subsystem_rows}
        raw_vendor_ids, raw_references = pci_source_evidence(raw_path, set(missing_subvendors), wanted_references)
        source_evidence = {
            "missingReferenceClassifications": classify_missing_targets(
            missing_subvendors, raw_vendor_ids,
            "rawVendorDeclarationPresentButNotImported",
            "The raw pci.ids file declares this top-level vendor ID, so its absent catalog record is locally repairable.",
            "VERIFIED_SOURCE_BOUNDARY",
            "The raw pci.ids file contains subsystem rows using this ID but has no matching top-level vendor declaration; retain these as real source-level dangling references."),
            "rawReferenceRows": len(raw_references),
            "catalogMissingReferenceRows": len(wanted_references),
            "catalogMissingReferencesAbsentFromRaw": len(wanted_references - raw_references),
            "sourceAllowedMissingCount": 0,
            "rawReferenceExamples": [list(row) for row in sorted(raw_references)[:10]],
        }
        checks["subsystem.subvendorId -> vendor"]["unexpectedExamples"] = missing_subvendor_examples
        checks["subsystem.subvendorId -> vendor"]["missingReferenceClassifications"] = source_evidence["missingReferenceClassifications"]
        checks["subsystem.subvendorId -> vendor"]["sourceAllowedMissingReason"] = "pci.ids provides no documented exception for missing subsystem vendor targets; all such references remain missing."
    return {"snapshot": info, "sourceEvidence": source_evidence, "checks": checks}


def validate_pci(data_dir, raw_dir=None):
    """Validate the unmodified source snapshot; the 46 source-level links remain failures."""
    return validate_pci_snapshot(data_dir / "pci-ids.sqlite3", "pci-ids",
                                 EXPECTED_VERSIONS["pci-ids"], raw_dir)


def validate_pci_resolved(data_dir):
    """Validate the explicitly derived relationship-closed PCI snapshot."""
    version = EXPECTED_VERSIONS["pci-ids"] + "-resolved-v1"
    return validate_pci_snapshot(data_dir / "pci-ids-resolved.sqlite3",
                                 "pci-ids-resolved", version)


def validate_ourairports(data_dir):
    path = data_dir / "ourairports.sqlite3"
    with closing(connect(path)) as db:
        info = snapshot(db, "ourairports")
        airport_count = scalar(db, "SELECT count(*) FROM Records WHERE Kind='airport'")
        region_count = scalar(db, "SELECT count(*) FROM Records WHERE Kind='region'")
        checks = {
            "airport.iso_country -> country": key_relation(db, "airport.iso_country -> country", "airport", ["iso_country"],
                "'country|' || json_extract(s.PayloadJson, '$.iso_country')", airport_count),
            "airport.iso_region -> region": key_relation(db, "airport.iso_region -> region", "airport", ["iso_region"],
                "'region|' || json_extract(s.PayloadJson, '$.iso_region')", airport_count),
            "region.iso_country -> country": key_relation(db, "region.iso_country -> country", "region", ["iso_country"],
                "'country|' || json_extract(s.PayloadJson, '$.iso_country')", region_count),
        }
        for name, field in (("airport.iso_country -> country", "iso_country"),
                            ("airport.iso_region -> region", "iso_region"),
                            ("region.iso_country -> country", "iso_country")):
            target = "countries.csv code" if field == "iso_country" else "regions.csv code"
            checks[name]["sourceAllowedMissingReason"] = (
                f"The OurAirports data dictionary defines {field} as a reference to a {target}; it does not define blank as an allowed missing value."
            )

        mismatch_count = scalar(db, """
            SELECT count(*) FROM Records a JOIN Records r
              ON r.Key='region|' || json_extract(a.PayloadJson, '$.iso_region')
            WHERE a.Kind='airport'
              AND nullif(trim(json_extract(a.PayloadJson, '$.iso_country')), '') IS NOT NULL
              AND nullif(trim(json_extract(a.PayloadJson, '$.iso_region')), '') IS NOT NULL
              AND json_extract(a.PayloadJson, '$.iso_country') != json_extract(r.PayloadJson, '$.iso_country')
        """)
        mismatch_examples = examples(db, """
            SELECT a.Key AS airportKey, json_extract(a.PayloadJson, '$.iso_country') AS airportCountry,
                   json_extract(a.PayloadJson, '$.iso_region') AS airportRegion,
                   json_extract(r.PayloadJson, '$.iso_country') AS regionCountry
            FROM Records a JOIN Records r ON r.Key='region|' || json_extract(a.PayloadJson, '$.iso_region')
            WHERE a.Kind='airport'
              AND nullif(trim(json_extract(a.PayloadJson, '$.iso_country')), '') IS NOT NULL
              AND nullif(trim(json_extract(a.PayloadJson, '$.iso_region')), '') IS NOT NULL
              AND json_extract(a.PayloadJson, '$.iso_country') != json_extract(r.PayloadJson, '$.iso_country')
            ORDER BY a.Key LIMIT 10
        """) if mismatch_count else []
        checks["airport.iso_country matches region.iso_country"] = relation_result(
            airport_count, mismatch_count, allowed_reason="Country and region references must describe the same source location; mismatches are not allowed.",
            unexpected_examples=mismatch_examples)

        unassigned_count = scalar(db, """
            SELECT count(*) FROM Records a JOIN Records r
              ON r.Key='region|' || json_extract(a.PayloadJson, '$.iso_region')
            WHERE a.Kind='airport' AND json_extract(r.PayloadJson, '$.local_code')='U-A'
        """)
        unassigned_examples = examples(db, """
            SELECT a.Key AS airportKey, json_extract(a.PayloadJson, '$.iso_region') AS regionCode
            FROM Records a JOIN Records r ON r.Key='region|' || json_extract(a.PayloadJson, '$.iso_region')
            WHERE a.Kind='airport' AND json_extract(r.PayloadJson, '$.local_code')='U-A'
            ORDER BY a.Key LIMIT 10
        """) if unassigned_count else []

    return {
        "snapshot": info,
        "checks": checks,
        "sourceDefinedUnassignedRegionReferences": {
            "count": unassigned_count,
            "reason": "OurAirports data dictionary explicitly defines local_code U-A as its pseudo-region for an airport not assigned to a region; these remain valid region references, not missing links.",
            "examples": unassigned_examples,
        },
    }


def validate_relationships(data_dir, raw_dir=DEFAULT_RAW):
    results = {
        "cldr": validate_cldr(data_dir, raw_dir),
        "pci-ids": validate_pci(data_dir, raw_dir),
        "ourairports": validate_ourairports(data_dir),
    }
    checks = [check for dataset in results.values() for check in dataset["checks"].values()]
    version_mismatches = [dataset["snapshot"]["id"] for dataset in results.values()
                          if not dataset["snapshot"]["versionMatchesExpected"]]
    failures = [check for check in checks if check["unexpectedMissingCount"] != 0]
    coverage_failures = [dataset["sourceRelationCoverage"] for dataset in results.values()
                         if dataset.get("sourceRelationCoverage") and
                         (dataset["sourceRelationCoverage"]["sourceReferencesAbsentFromCatalog"] or
                          dataset["sourceRelationCoverage"]["catalogReferencesAbsentFromSource"])]
    source_evidence_failures = [dataset["sourceEvidence"] for dataset in results.values()
                                if dataset.get("sourceEvidence") and
                                dataset["sourceEvidence"]["catalogMissingReferencesAbsentFromRaw"]]
    cldr_entities = results["cldr"].get("sourceEntityCoverage")
    entity_coverage_failures = bool(cldr_entities) and (
        cldr_entities["regions"]["missingReferencedCodes"]
        or cldr_entities["regions"]["sourceCodesMissingFromCatalog"]
        or cldr_entities["regions"]["catalogCodesAbsentFromSource"]
        or not cldr_entities["regions"]["localizedMetadataMatchesSource"]
        or cldr_entities["currencies"]["missingSourceCodes"]
        or cldr_entities["currencies"]["catalogCodesAbsentFromSource"]
        or not cldr_entities["currencies"]["currentFlagsMatchSource"]
        or not cldr_entities["currencies"]["localizedMetadataMatchesSource"]
    )
    source_boundary_references = sum(
        item["referenceRows"]
        for dataset in results.values()
        for check in dataset["checks"].values()
        for item in check.get("missingReferenceClassifications", [])
        if item["classification"] == "VERIFIED_SOURCE_BOUNDARY"
    )
    return {
        "check": "catalog-relationships",
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "mode": "relationship-only; no catalog or raw-input hashes recomputed",
        "status": "PASS" if not failures and not version_mismatches and not coverage_failures and not source_evidence_failures and not entity_coverage_failures else "FAIL",
        "versionMismatches": version_mismatches,
        "sourceCoverageMismatchCount": len(coverage_failures),
        "sourceEvidenceMismatchCount": len(source_evidence_failures),
        "sourceEntityCoverageMismatch": bool(entity_coverage_failures),
        "verifiedSourceBoundaryReferenceCount": source_boundary_references,
        "catalogs": results,
        "failureCount": len(failures),
    }


def verify_full(data_dir, output_path):
    report = {"catalogs": [], "relationships": {}}
    for file in sorted(data_dir.glob("*.sqlite3")):
        if ".building." in file.name:
            raise RuntimeError("Unfinished catalog: " + str(file))
        with closing(connect(file)) as db:
            db.row_factory = sqlite3.Row
            info = dict(db.execute("SELECT * FROM CatalogInfo").fetchone())
            count = scalar(db, "SELECT count(*) FROM Records")
            assert count == info["RecordCount"] and count > 0, file
            assert file.stat().st_size <= 512 * 1024 * 1024, file
            assert scalar(db, "PRAGMA quick_check") == "ok", file
            assert scalar(db, "SELECT count(*) FROM Records WHERE NOT json_valid(PayloadJson)") == 0, file
            assert scalar(db, "SELECT min(Ordinal) FROM Records") == 1 and scalar(db, "SELECT max(Ordinal) FROM Records") == count, file
            assert scalar(db, "SELECT count(DISTINCT Key) FROM Records") == count, file
            assert info["License"] and info["SourceUri"] and len(info["SourceSha256"]) == 64, file
            with file.open("rb") as stream:
                digest = hashlib.file_digest(stream, "sha256").hexdigest()
            entry = {"file": file.name, "bytes": file.stat().st_size, "sha256": digest, "records": count,
                     "version": info["Version"], "kinds": dict(db.execute("SELECT Kind,count(*) FROM Records GROUP BY Kind"))}
            report["catalogs"].append(entry)
            if file.stem == "icd10cm":
                missing = scalar(db, "SELECT count(*) FROM Records r WHERE json_extract(PayloadJson,'$.parentCode') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Records p WHERE p.Key=json_extract(r.PayloadJson,'$.parentCode'))")
                assert missing == 0
                report["relationships"]["icdMissingParents"] = missing
                assert scalar(db, "SELECT count(*) FROM Records WHERE length(json_extract(PayloadJson,'$.parentCode'))>=length(Key)") == 0
            if file.stem == "vehicles":
                missing = scalar(db, "SELECT count(*) FROM Records r WHERE Kind='model' AND NOT EXISTS(SELECT 1 FROM Records p WHERE p.Key='make|'||json_extract(r.PayloadJson,'$.manufacturerId'))")
                assert missing == 0
                report["relationships"]["vehicleMissingManufacturers"] = missing
            if file.stem == "ror":
                ids = {row[0] for row in db.execute("SELECT Key FROM Records")}
                total = missing = 0
                for row in db.execute("SELECT PayloadJson FROM Records"):
                    for relationship in json.loads(row[0]).get("relationships", []):
                        total += 1
                        missing += relationship["id"] not in ids
                report["relationships"]["ror"] = {"relationships": total, "outsideSnapshot": missing}
                assert missing == 0
            if file.stem.startswith("rxnorm-cpc-relations-"):
                db.execute("ATTACH DATABASE ? AS terms", (str(data_dir / "rxnorm-cpc.sqlite3"),))
                missing = scalar(db, "SELECT count(*) FROM main.Records r WHERE NOT EXISTS(SELECT 1 FROM terms.Records t WHERE t.Key=CASE WHEN json_extract(r.PayloadJson,'$.stype1')='AUI' THEN 'term|'||json_extract(r.PayloadJson,'$.rxaui1') ELSE 'medication|'||json_extract(r.PayloadJson,'$.rxcui1') END) OR NOT EXISTS(SELECT 1 FROM terms.Records t WHERE t.Key=CASE WHEN json_extract(r.PayloadJson,'$.stype2')='AUI' THEN 'term|'||json_extract(r.PayloadJson,'$.rxaui2') ELSE 'medication|'||json_extract(r.PayloadJson,'$.rxcui2') END)")
                report["relationships"][file.stem] = {"outsideCpcTerms": missing}
                assert missing == 0
        print(file.name, count, flush=True)
    report["status"] = "PASS"
    report["totalRecords"] = sum(catalog["records"] for catalog in report["catalogs"])
    if (data_dir / "pci-ids-resolved.sqlite3").exists():
        report["relationships"]["pciOriginal"] = validate_pci(data_dir)
        resolved = validate_pci_resolved(data_dir)
        report["relationships"]["pciResolved"] = resolved
        assert all(check["unexpectedMissingCount"] == 0 for check in resolved["checks"].values())
        assert resolved["snapshot"]["versionMatchesExpected"]
        report["pciOriginalStrictStatus"] = "FAIL_SOURCE_MISSING_REFERENCES" if any(check["unexpectedMissingCount"] for check in report["relationships"]["pciOriginal"]["checks"].values()) else "PASS"
        report["pciResolvedStrictStatus"] = "PASS"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--relationships-only", action="store_true",
                        help="check CLDR, PCI and OurAirports relations without rehashing catalog files")
    parser.add_argument("--data-dir", type=Path, default=DEFAULT_DATA)
    parser.add_argument("--raw-dir", type=Path, default=DEFAULT_RAW)
    parser.add_argument("--output", type=Path, default=None)
    args = parser.parse_args(argv)

    if args.relationships_only:
        report = validate_relationships(args.data_dir, args.raw_dir)
        output_path = args.output or DEFAULT_RELATIONSHIP_REPORT
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(json.dumps(report, indent=2, ensure_ascii=False))
        return 0 if report["status"] == "PASS" else 1

    verify_full(args.data_dir, args.output or ROOT / "artifacts" / "domain-upgrade" / "catalog-validation.json")
    return 0


if __name__ == "__main__":
    sys.exit(main())
