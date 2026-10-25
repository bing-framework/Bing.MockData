"""Small fixtures for strict catalog relationship validation."""

import json
import sqlite3
import tempfile
import unittest
from contextlib import closing
from pathlib import Path

from verify import validate_cldr, validate_ourairports, validate_pci


class CatalogRelationshipTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.data_dir = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def write_catalog(self, filename, catalog_id, version, records):
        path = self.data_dir / filename
        with closing(sqlite3.connect(path)) as db:
            with db:
                db.execute("CREATE TABLE CatalogInfo (Id TEXT, Version TEXT, SnapshotDate TEXT, RecordCount INTEGER, SourceSha256 TEXT)")
                db.execute("CREATE TABLE Records (Ordinal INTEGER, Key TEXT, Kind TEXT, CountryCode TEXT, Name TEXT, PayloadJson TEXT)")
                db.execute("INSERT INTO CatalogInfo VALUES (?, ?, ?, ?, ?)",
                           (catalog_id, version, "2026-10-25", len(records), "a" * 64))
                db.executemany("INSERT INTO Records VALUES (?, ?, ?, ?, ?, ?)", [
                    (index, key, kind, country, name, json.dumps(payload))
                    for index, (key, kind, country, name, payload) in enumerate(records, 1)
                ])

    def write_cldr_raw(self, territories_en, territories_zh, currencies_en, currencies_zh, currency_regions):
        folder = self.data_dir / "raw" / "cldr" / "49.0.0"
        folder.mkdir(parents=True, exist_ok=True)
        documents = {
            "territories-en.json": {"main": {"en": {"localeDisplayNames": {"territories": territories_en}}}},
            "territories-zh.json": {"main": {"zh": {"localeDisplayNames": {"territories": territories_zh}}}},
            "currencies-en.json": {"main": {"en": {"numbers": {"currencies": currencies_en}}}},
            "currencies-zh.json": {"main": {"zh": {"numbers": {"currencies": currencies_zh}}}},
            "currencyData.json": {"supplemental": {"currencyData": {"region": currency_regions}}},
        }
        for filename, document in documents.items():
            (folder / filename).write_text(json.dumps(document), encoding="utf-8")
        return self.data_dir / "raw"

    def test_cldr_historical_currency_target_is_real_missing_link_and_locally_repairable(self):
        self.write_catalog("cldr.sqlite3", "cldr", "49.0.0-import-v2", [
            ("region|US", "region", "US", "United States", {"code": "US"}),
            ("currency|USD", "currency", None, "US Dollar", {"code": "USD"}),
            ("region-currency|US|OLD|0", "region-currency", "US", "OLD",
             {"region": "US", "currencyCode": "OLD", "_to": "2000-01-01"}),
            ("region-currency|US|USD|1", "region-currency", "US", "USD",
             {"region": "US", "currencyCode": "USD", "_from": "1792-01-01"}),
        ])
        raw_dir = self.write_cldr_raw(
            {"US": "United States"}, {"US": "美国"},
            {"OLD": {"displayName": "Old Dollar"}, "USD": {"displayName": "US Dollar"}},
            {"OLD": {"displayName": "旧美元"}, "USD": {"displayName": "美元"}},
            {"US": [{"OLD": {"_to": "2000-01-01"}}, {"USD": {"_from": "1792-01-01"}}]},
        )

        result = validate_cldr(self.data_dir, raw_dir)["checks"]["region-currency.currencyCode -> currency"]

        self.assertEqual(1, result["missingReferenceCount"])
        self.assertEqual(0, result["sourceAllowedMissingCount"])
        self.assertEqual(1, result["unexpectedMissingCount"])
        self.assertEqual(1, result["missingReferenceDateEvidence"]["endedRows"])
        self.assertEqual(0, result["missingReferenceDateEvidence"]["withoutEndDateRows"])
        classification = result["missingReferenceClassifications"][0]
        self.assertEqual("sourceCurrencyNamePresentButNotImported", classification["classification"])
        self.assertEqual([{"target": "OLD", "referenceRows": 1}], classification["targets"])
        self.assertEqual(0, classification["sourceAllowedMissingCount"])

    def test_cldr_reports_source_missing_active_currency_entity_as_unexpected(self):
        self.write_catalog("cldr.sqlite3", "cldr", "49.0.0-import-v2", [
            ("region|US", "region", "US", "United States", {"code": "US"}),
            ("region-currency|US|MISSING|0", "region-currency", "US", "MISSING",
             {"region": "US", "currencyCode": "MISSING"}),
        ])
        raw_dir = self.write_cldr_raw(
            {"US": "United States"}, {"US": "美国"}, {}, {},
            {"US": [{"MISSING": {}}]},
        )

        result = validate_cldr(self.data_dir, raw_dir)["checks"]["region-currency.currencyCode -> currency"]

        self.assertEqual(1, result["missingReferenceCount"])
        self.assertEqual(0, result["sourceAllowedMissingCount"])
        self.assertEqual(1, result["unexpectedMissingCount"])
        classification = result["missingReferenceClassifications"][1]
        self.assertEqual("VERIFIED_SOURCE_BOUNDARY", classification["classification"])
        self.assertEqual(1, classification["referenceRows"])

    def test_cldr_historical_region_missing_from_pinned_inputs_remains_source_boundary(self):
        self.write_catalog("cldr.sqlite3", "cldr", "49.0.0-import-v2", [
            ("region|US", "region", "US", "United States", {"code": "US"}),
            ("region-currency|ZZ|OLD|0", "region-currency", "ZZ", "OLD",
             {"region": "ZZ", "currencyCode": "OLD", "_to": "2000-01-01"}),
        ])
        raw_dir = self.write_cldr_raw(
            {"US": "United States"}, {"US": "美国"},
            {"OLD": {"displayName": "Old Currency"}}, {"OLD": {"displayName": "旧货币"}},
            {"ZZ": [{"OLD": {"_to": "2000-01-01"}}]},
        )

        result = validate_cldr(self.data_dir, raw_dir)
        check = result["checks"]["region-currency.region -> region"]
        self.assertEqual(1, check["missingReferenceCount"])
        self.assertEqual(0, check["sourceAllowedMissingCount"])
        self.assertEqual(1, check["unexpectedMissingCount"])
        self.assertEqual("VERIFIED_SOURCE_BOUNDARY",
                         check["missingReferenceClassifications"][1]["classification"])
        self.assertEqual(0, result["sourceRelationCoverage"]["sourceReferencesAbsentFromCatalog"])
        self.assertEqual(0, result["sourceRelationCoverage"]["catalogReferencesAbsentFromSource"])

    def test_pci_hierarchy_links_all_levels(self):
        self.write_catalog("pci-ids.sqlite3", "pci-ids", "fe7fceb2e1153d495e1deefacf59c5d61c36ef8e", [
            ("vendor|1111", "vendor", None, "Vendor", {"vendorId": "1111"}),
            ("vendor|2222", "vendor", None, "Subsystem vendor", {"vendorId": "2222"}),
            ("device|1111|0001", "device", None, "Device", {"vendorId": "1111", "deviceId": "0001"}),
            ("subsystem|1111|0001|2222|0002", "subsystem", None, "Subsystem",
             {"vendorId": "1111", "deviceId": "0001", "subvendorId": "2222", "subdeviceId": "0002"}),
            ("class|01", "class", None, "Mass storage", {"classId": "01"}),
            ("subclass|01|06", "subclass", None, "SATA", {"classId": "01", "subclassId": "06"}),
            ("interface|01|06|01", "interface", None, "AHCI", {"classId": "01", "subclassId": "06", "interfaceId": "01"}),
        ])

        result = validate_pci(self.data_dir)

        self.assertTrue(all(check["unexpectedMissingCount"] == 0 for check in result["checks"].values()))
        self.assertEqual(5, len(result["checks"]))

    def test_pci_missing_subvendor_in_raw_remains_a_source_level_missing_link(self):
        self.write_catalog("pci-ids.sqlite3", "pci-ids", "fe7fceb2e1153d495e1deefacf59c5d61c36ef8e", [
            ("vendor|1111", "vendor", None, "Vendor", {"vendorId": "1111"}),
            ("device|1111|0001", "device", None, "Device", {"vendorId": "1111", "deviceId": "0001"}),
            ("subsystem|1111|0001|0000|0002", "subsystem", None, "Subsystem",
             {"vendorId": "1111", "deviceId": "0001", "subvendorId": "0000", "subdeviceId": "0002"}),
        ])
        raw_path = self.data_dir / "raw" / "pciids" / "fe7fceb2e1153d495e1deefacf59c5d61c36ef8e" / "pci.ids"
        raw_path.parent.mkdir(parents=True, exist_ok=True)
        raw_path.write_text("1111 Vendor\n\t0001 Device\n\t\t0000 0002 Subsystem\n", encoding="utf-8")

        result = validate_pci(self.data_dir, self.data_dir / "raw")
        check = result["checks"]["subsystem.subvendorId -> vendor"]

        self.assertEqual(1, check["missingReferenceCount"])
        self.assertEqual(0, check["sourceAllowedMissingCount"])
        self.assertEqual(1, check["unexpectedMissingCount"])
        self.assertEqual(1, result["sourceEvidence"]["rawReferenceRows"])
        self.assertEqual(1, result["sourceEvidence"]["catalogMissingReferenceRows"])
        self.assertEqual(0, result["sourceEvidence"]["catalogMissingReferencesAbsentFromRaw"])
        self.assertEqual("VERIFIED_SOURCE_BOUNDARY",
                         check["missingReferenceClassifications"][1]["classification"])
        self.assertEqual([{"target": "0000", "referenceRows": 1}],
                         check["missingReferenceClassifications"][1]["targets"])

    def test_cldr_source_localization_values_and_missing_flags_are_checked(self):
        self.write_catalog("cldr.sqlite3", "cldr", "49.0.0-import-v2", [
            ("region|US", "region", "US", "United States",
             {"code": "US", "nameEn": "United States", "nameZh": "美国",
              "nameEnMissing": False, "nameZhMissing": False}),
            ("currency|USD", "currency", None, "US Dollar",
             {"code": "USD", "nameEn": "US Dollar", "nameZh": "美元",
              "nameEnMissing": False, "nameZhMissing": False, "isCurrent": True}),
            ("region-currency|US|USD|0", "region-currency", "US", "USD",
             {"region": "US", "currencyCode": "USD", "_from": "1792-01-01"}),
        ])
        raw_dir = self.write_cldr_raw(
            {"US": "United States"}, {"US": "美国"},
            {"USD": {"displayName": "US Dollar"}}, {"USD": {"displayName": "美元"}},
            {"US": [{"USD": {"_from": "1792-01-01"}}]},
        )

        result = validate_cldr(self.data_dir, raw_dir)
        self.assertTrue(result["sourceEntityCoverage"]["regions"]["localizedMetadataMatchesSource"])
        self.assertTrue(result["sourceEntityCoverage"]["currencies"]["localizedMetadataMatchesSource"])

        with closing(sqlite3.connect(self.data_dir / "cldr.sqlite3")) as db:
            with db:
                db.execute("UPDATE Records SET PayloadJson=? WHERE Key='region|US'", (
                    json.dumps({"code": "US", "nameEn": "United States", "nameZh": "美国",
                                "nameEnMissing": True, "nameZhMissing": False}),))
        result = validate_cldr(self.data_dir, raw_dir)
        self.assertFalse(result["sourceEntityCoverage"]["regions"]["localizedMetadataMatchesSource"])

    def test_airports_accept_documented_unassigned_region_but_not_empty_reference(self):
        self.write_catalog("ourairports.sqlite3", "ourairports", "git:711cf6754ef05a1ccedc2482cc1e838032249d3c", [
            ("country|US", "country", "US", "United States", {"code": "US"}),
            ("region|US-U-A", "region", "US", "Unassigned", {"code": "US-U-A", "iso_country": "US", "local_code": "U-A"}),
            ("airport|1", "airport", "US", "Airport", {"iso_country": "US", "iso_region": "US-U-A"}),
            ("airport|2", "airport", "US", "Unlocated airport", {"iso_country": "US", "iso_region": ""}),
        ])

        result = validate_ourairports(self.data_dir)
        region_check = result["checks"]["airport.iso_region -> region"]

        self.assertEqual(0, region_check["missingReferenceCount"])
        self.assertEqual(1, region_check["emptyReferenceCount"])
        self.assertEqual(0, region_check["sourceAllowedMissingCount"])
        self.assertEqual(1, region_check["unexpectedMissingCount"])
        self.assertEqual(1, result["sourceDefinedUnassignedRegionReferences"]["count"])


if __name__ == "__main__":
    unittest.main()
