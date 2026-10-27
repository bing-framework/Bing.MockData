"""Build an auditable PCI snapshot without unresolved subsystem vendor links."""

import argparse
from collections import Counter
from contextlib import closing
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import tempfile

from verify import DEFAULT_DATA, DEFAULT_RAW, EXPECTED_VERSIONS, validate_pci_snapshot


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_OUTPUT = DEFAULT_DATA / "pci-ids-resolved.sqlite3"
DEFAULT_MANIFEST = DEFAULT_DATA / "pci-ids-resolved.manifest.json"
DEFAULT_EXCLUSIONS = DEFAULT_DATA / "pci-ids-resolved.exclusions.json"
DEFAULT_EVIDENCE = ROOT / "artifacts" / "catalog-closure" / "pci"
DATASET_ID = "pci-ids-resolved"
DERIVATION_VERSION = "resolved-v1"
SOURCE_ID = "pci-ids"
SOURCE_VERSION = EXPECTED_VERSIONS[SOURCE_ID]
DERIVED_VERSION = SOURCE_VERSION + "-" + DERIVATION_VERSION
MAX_CATALOG_BYTES = 512 * 1024 * 1024
EXCLUSION_REASON = "subsystem references a subvendor ID with no vendor record in the pinned source snapshot"
RECORD_COLUMNS = ("Ordinal", "Key", "Kind", "CountryCode", "Name", "PayloadJson")


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _connect_readonly(path):
    return sqlite3.connect(Path(path).resolve().as_uri() + "?mode=ro", uri=True)


def _catalog_info(db):
    rows = db.execute("SELECT Id, Version, SourceUri, License, SnapshotDate, RecordCount, SourceSha256, Coverage, FileFormatVersion FROM CatalogInfo").fetchall()
    if len(rows) != 1:
        raise ValueError("PCI source must contain exactly one CatalogInfo row")
    names = ("Id", "Version", "SourceUri", "License", "SnapshotDate", "RecordCount",
             "SourceSha256", "Coverage", "FileFormatVersion")
    return dict(zip(names, rows[0]))


def _schema_signature(db):
    return db.execute("""
        SELECT type, name, tbl_name, sql FROM sqlite_master
        WHERE name NOT LIKE 'sqlite_%' ORDER BY type, name
    """).fetchall()


def _record_rows(db):
    return db.execute(
        "SELECT Ordinal, Key, Kind, CountryCode, Name, PayloadJson FROM Records ORDER BY Ordinal"
    ).fetchall()


def _source_exclusions(rows, source_version):
    vendor_keys = {row[1] for row in rows if row[2] == "vendor"}
    exclusions = []
    for ordinal, key, kind, country, name, payload_json in rows:
        if kind != "subsystem":
            continue
        payload = json.loads(payload_json)
        subvendor = payload.get("subvendorId")
        if subvendor is None or not str(subvendor).strip():
            continue
        target = "vendor|" + str(subvendor)
        if target in vendor_keys:
            continue
        exclusions.append({
            "originalOrdinal": ordinal,
            "originalKey": key,
            "targetKey": target,
            "reason": EXCLUSION_REASON,
            "sourceVersion": source_version,
            "derivationVersion": DERIVATION_VERSION,
            "original": {
                "Key": key,
                "Kind": kind,
                "CountryCode": country,
                "Name": name,
                "PayloadJson": payload_json,
            },
        })
    return exclusions


def _atomic_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile("w", encoding="utf-8", newline="\n", dir=path.parent,
                                         prefix=path.name + ".", suffix=".building", delete=False) as stream:
            temporary = Path(stream.name)
            json.dump(value, stream, ensure_ascii=False, sort_keys=True, indent=2)
            stream.write("\n")
        os.replace(temporary, path)
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()


def _verify_source(path, raw_dir):
    path = Path(path)
    with closing(_connect_readonly(path)) as db:
        info = _catalog_info(db)
        if info["Id"] != SOURCE_ID or info["Version"] != SOURCE_VERSION:
            raise ValueError("PCI source identity or pinned version does not match the expected snapshot")
        if info["FileFormatVersion"] != 1:
            raise ValueError("PCI source file format version is not supported")
        count = db.execute("SELECT count(*) FROM Records").fetchone()[0]
        if info["RecordCount"] != count:
            raise ValueError("PCI source CatalogInfo count does not match its Records table")
        ordinals = db.execute("SELECT min(Ordinal), max(Ordinal), count(DISTINCT Ordinal) FROM Records").fetchone()
        if count and ordinals != (1, count, count):
            raise ValueError("PCI source ordinals must be unique and continuous")
        rows = _record_rows(db)
        schema = _schema_signature(db)

    source_report = validate_pci_snapshot(path, SOURCE_ID, SOURCE_VERSION, raw_dir)
    checks = source_report["checks"]
    source_link = checks["subsystem.subvendorId -> vendor"]
    if source_link["emptyReferenceCount"]:
        raise ValueError("PCI source contains an empty subsystem subvendor reference")
    for name, check in checks.items():
        if name != "subsystem.subvendorId -> vendor" and check["unexpectedMissingCount"]:
            raise ValueError("PCI source has a relationship failure outside subsystem subvendor references")
    if raw_dir is not None:
        evidence = source_report["sourceEvidence"]
        if evidence is None or evidence["catalogMissingReferencesAbsentFromRaw"] != 0:
            raise ValueError("PCI catalog missing references do not match the pinned raw source")

    exclusions = _source_exclusions(rows, info["Version"])
    if len(exclusions) != source_link["missingReferenceCount"]:
        raise ValueError("PCI exclusions do not account for every missing subsystem subvendor reference")
    return info, rows, schema, exclusions, source_report


def _build_database(source_path, temporary_path, exclusions, source_info, source_schema):
    excluded_keys = [item["originalKey"] for item in exclusions]
    source_uri = Path(source_path).resolve().as_uri() + "?mode=ro"
    with closing(sqlite3.connect(source_uri, uri=True)) as source_db:
        with closing(sqlite3.connect(temporary_path)) as derived_db:
            source_db.backup(derived_db)
            derived_db.row_factory = None
            with derived_db:
                if excluded_keys:
                    derived_db.executemany("DELETE FROM Records WHERE Key = ?", [(key,) for key in excluded_keys])
                # Move the primary keys out of the positive range before assigning gap-free ordinals.
                derived_db.execute("UPDATE Records SET Ordinal = -Ordinal")
                keys = derived_db.execute("SELECT Key FROM Records ORDER BY -Ordinal").fetchall()
                derived_db.executemany(
                    "UPDATE Records SET Ordinal = ? WHERE Key = ?",
                    [(index, key[0]) for index, key in enumerate(keys, 1)],
                )
                count = len(keys)
                coverage = (source_info["Coverage"].rstrip("; ") +
                            f"; derived from {SOURCE_ID}@{source_info['Version']}; excluded {len(exclusions)} "
                            "subsystem rows whose subvendor IDs have no vendor definition")
                cursor = derived_db.execute("""
                    UPDATE CatalogInfo SET Id = ?, Version = ?, RecordCount = ?, Coverage = ?
                    WHERE Id = ?
                """, (DATASET_ID, DERIVED_VERSION, count, coverage, SOURCE_ID))
                if cursor.rowcount != 1:
                    raise ValueError("PCI source CatalogInfo row could not be converted")
            result = derived_db.execute("PRAGMA integrity_check").fetchone()[0]
            if result != "ok":
                raise ValueError("Derived PCI SQLite integrity check failed: " + str(result))
            if _schema_signature(derived_db) != source_schema:
                raise ValueError("Derived PCI catalog schema or indexes differ from the source snapshot")
            app_id = derived_db.execute("PRAGMA application_id").fetchone()[0]
            user_version = derived_db.execute("PRAGMA user_version").fetchone()[0]
            if (app_id, user_version) != (1112364081, 1):
                raise ValueError("Derived PCI SQLite header metadata differs from the catalog format")
            derived_info = _catalog_info(derived_db)
            derived_rows = _record_rows(derived_db)
    if derived_info["SourceSha256"] != source_info["SourceSha256"]:
        raise ValueError("Derived PCI source checksum metadata changed unexpectedly")
    return derived_info, derived_rows


def _verify_partition(source_rows, derived_rows, exclusions):
    source_by_key = {row[1]: row for row in source_rows}
    derived_by_key = {row[1]: row for row in derived_rows}
    excluded_by_key = {item["originalKey"]: item for item in exclusions}
    source_keys = set(source_by_key)
    derived_keys = set(derived_by_key)
    excluded_keys = set(excluded_by_key)
    if len(derived_by_key) != len(derived_rows) or len(excluded_by_key) != len(exclusions):
        raise ValueError("Duplicate keys found in the derived records or exclusion list")
    if derived_keys & excluded_keys or derived_keys | excluded_keys != source_keys:
        raise ValueError("Derived records and exclusions do not partition the source keys")

    source_order = []
    for row in source_rows:
        ordinal, key, kind, country, name, payload_json = row
        if key in derived_by_key:
            derived = derived_by_key[key]
            if derived[2:] != row[2:]:
                raise ValueError("A retained PCI record changed content: " + key)
            source_order.append(key)
        else:
            exclusion = excluded_by_key[key]
            original = exclusion["original"]
            expected_record = {"Key": key, "Kind": kind, "CountryCode": country,
                               "Name": name, "PayloadJson": payload_json}
            if exclusion["originalOrdinal"] != ordinal or original != expected_record:
                raise ValueError("An excluded PCI record is not preserved exactly: " + key)
            if exclusion["sourceVersion"] != SOURCE_VERSION or exclusion["reason"] != EXCLUSION_REASON:
                raise ValueError("An excluded PCI record is missing its source or reason metadata: " + key)
            payload = json.loads(payload_json)
            subvendor = payload.get("subvendorId")
            expected_target = "vendor|" + str(subvendor) if subvendor is not None else None
            if (kind != "subsystem" or not str(subvendor or "").strip() or
                    exclusion["targetKey"] != expected_target or
                    exclusion["derivationVersion"] != DERIVATION_VERSION):
                raise ValueError("An excluded PCI record has incorrect target or derivation metadata: " + key)
    if [row[1] for row in derived_rows] != source_order:
        raise ValueError("Derived PCI record order does not preserve source order")
    if [row[0] for row in derived_rows] != list(range(1, len(derived_rows) + 1)):
        raise ValueError("Derived PCI ordinals are not continuous")

    expected = {item["originalKey"] for item in _source_exclusions(source_rows, SOURCE_VERSION)}
    if expected != excluded_keys:
        raise ValueError("Exclusion list contains records beyond unresolved subsystem subvendors")
    return {
        "sourceRecords": len(source_rows),
        "derivedRecords": len(derived_rows),
        "excludedRecords": len(exclusions),
        "disjoint": True,
        "unionMatchesSource": True,
        "retainedContentUnchanged": True,
        "relativeOrderPreserved": True,
        "ordinalsContinuous": True,
    }


def _kind_counts(rows):
    counts = Counter(row[2] for row in rows)
    return dict(sorted(counts.items()))


def derive_pci(source_path=DEFAULT_DATA / "pci-ids.sqlite3", output_path=DEFAULT_OUTPUT,
               manifest_path=DEFAULT_MANIFEST, exclusions_path=DEFAULT_EXCLUSIONS,
               raw_dir=DEFAULT_RAW, evidence_dir=None):
    source_path = Path(source_path).resolve()
    output_path = Path(output_path).resolve()
    manifest_path = Path(manifest_path).resolve()
    exclusions_path = Path(exclusions_path).resolve()
    if source_path == output_path:
        raise ValueError("Derived output must not overwrite the original PCI snapshot")
    if len({output_path, manifest_path, exclusions_path}) != 3:
        raise ValueError("PCI output, manifest, and exclusion paths must be distinct")

    output_path.parent.mkdir(parents=True, exist_ok=True)
    source_hash_before = sha256_file(source_path)
    source_info, source_rows, source_schema, exclusions, source_report = _verify_source(source_path, raw_dir)
    temporary_path = None
    try:
        with tempfile.NamedTemporaryFile(dir=output_path.parent, prefix=output_path.name + ".",
                                         suffix=".building", delete=False) as stream:
            temporary_path = Path(stream.name)
        derived_info, derived_rows = _build_database(source_path, temporary_path, exclusions,
                                                     source_info, source_schema)
        closure = _verify_partition(source_rows, derived_rows, exclusions)
        resolved_report = validate_pci_snapshot(temporary_path, DATASET_ID, DERIVED_VERSION)
        failed_resolved = {
            name: check for name, check in resolved_report["checks"].items()
            if check["unexpectedMissingCount"] != 0
        }
        if failed_resolved:
            raise ValueError("Derived PCI relationships are not closed: " + json.dumps(failed_resolved, sort_keys=True))
        if source_hash_before != sha256_file(source_path):
            raise ValueError("Original PCI snapshot changed during derivation")
        size = temporary_path.stat().st_size
        if size > MAX_CATALOG_BYTES:
            raise ValueError("Derived PCI catalog exceeds the 512 MiB file limit")

        exclusions_document = {
            "datasetId": DATASET_ID,
            "derivationVersion": DERIVATION_VERSION,
            "sourceDatasetId": SOURCE_ID,
            "sourceVersion": source_info["Version"],
            "excludedRecordCount": len(exclusions),
            "records": exclusions,
        }
        exclusions_bytes = (json.dumps(exclusions_document, ensure_ascii=False, sort_keys=True,
                                       indent=2) + "\n").encode("utf-8")
        exclusions_sha = hashlib.sha256(exclusions_bytes).hexdigest()
        output_sha = sha256_file(temporary_path)
        manifest = {
            "Id": DATASET_ID,
            "Version": derived_info["Version"],
            "SourceDatasetId": SOURCE_ID,
            "SourceVersion": source_info["Version"],
            "DerivationVersion": DERIVATION_VERSION,
            "SourceUri": source_info["SourceUri"],
            "License": source_info["License"],
            "SnapshotDate": source_info["SnapshotDate"],
            "SourceSha256": source_info["SourceSha256"],
            "SourceCatalogSha256": source_hash_before,
            "RecordCount": len(derived_rows),
            "ExcludedRecordCount": len(exclusions),
            "KindCounts": _kind_counts(derived_rows),
            "Output": output_path.name,
            "OutputSha256": output_sha,
            "OutputBytes": size,
            "ExclusionsFile": exclusions_path.name,
            "ExclusionsSha256": exclusions_sha,
            "Coverage": derived_info["Coverage"],
            "Closure": closure,
        }

        os.replace(temporary_path, output_path)
        temporary_path = None
        _atomic_json(exclusions_path, exclusions_document)
        _atomic_json(manifest_path, manifest)
        report = {
            "status": "PASS",
            "source": source_report,
            "derived": resolved_report,
            "closure": closure,
            "manifest": manifest,
            "sourceCatalogSha256After": sha256_file(source_path),
        }
        if report["sourceCatalogSha256After"] != source_hash_before:
            raise ValueError("Original PCI snapshot changed after derivation")
        if evidence_dir is not None:
            _atomic_json(Path(evidence_dir) / "derivation-validation.json", report)
        return report
    finally:
        if temporary_path is not None and temporary_path.exists():
            temporary_path.unlink()


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=DEFAULT_DATA / "pci-ids.sqlite3")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--exclusions", type=Path, default=DEFAULT_EXCLUSIONS)
    parser.add_argument("--raw-dir", type=Path, default=DEFAULT_RAW)
    parser.add_argument("--evidence-dir", type=Path, default=DEFAULT_EVIDENCE)
    args = parser.parse_args(argv)
    report = derive_pci(args.source, args.output, args.manifest, args.exclusions,
                        args.raw_dir, args.evidence_dir)
    print(json.dumps(report, ensure_ascii=False, sort_keys=True, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
