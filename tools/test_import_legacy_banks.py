"""Focused regression tests for the pinned bankcardinfo static converter."""

from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import tarfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("import_legacy_banks", ROOT / "tools/ImportLegacyBanks.py")
CONVERTER = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(CONVERTER)


def fixture_source(patterns: str, bank_fields: str = 'bankName: "样例银行", bankCode: "TEST"') -> str:
    return f"var bankcardList = [{{{bank_fields}, patterns: [{patterns}]}}];"


class ImportLegacyBanksTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source_dir = ROOT / "tools/data/bankcardinfo-2.0.4"
        cls.archive_path = cls.source_dir / "bankcardinfo-2.0.4.tgz"
        cls.package_dir = cls.source_dir / "package"
        cls.baseline_path = cls.source_dir / "legacy-banks-baseline.json"
        cls.preserved_baseline_path = ROOT / "artifacts/bank-resource-closure/legacy-banks-baseline.json"
        cls.source_text = (cls.package_dir / "index.js").read_text(encoding="utf-8-sig")
        cls.source_banks = CONVERTER.parse_source(cls.source_text)
        cls.baseline_bytes = cls.baseline_path.read_bytes()
        if cls.preserved_baseline_path.exists():
            if cls.preserved_baseline_path.read_bytes() != cls.baseline_bytes:
                raise AssertionError("versioned baseline differs from the preserved artifact baseline")
        cls.baseline = json.loads(cls.baseline_bytes.decode("utf-8-sig"))
        with tarfile.open(cls.archive_path, "r:gz") as archive:
            archive_entries = archive.getmembers()
            cls.archive_members = sorted(member.name for member in archive_entries if member.isfile())
            wanted = {"package/index.js", "package/package.json", "package/README.md"}
            cls.package_files = {
                member.name: (cls.package_dir / Path(member.name).name).read_bytes()
                for member in archive_entries if member.name in wanted
            }
        cls.source_hashes = {
            name: hashlib.sha256(payload).hexdigest() for name, payload in cls.package_files.items()
        }
        cls.source_hashes["legacy-banks-baseline.json"] = hashlib.sha256(cls.baseline_bytes).hexdigest()
        cls.archive_hash = hashlib.sha256(cls.archive_path.read_bytes()).hexdigest()

    def test_pinned_dataset_counts_and_full_key_closure(self) -> None:
        output, report, _ = CONVERTER.build_conversion(
            self.source_banks, self.baseline, self.source_hashes, self.archive_members, self.archive_hash
        )
        CONVERTER.validate_reference_counts(report)

        self.assertEqual(124, report["coverage"]["sourceBanks"])
        self.assertEqual(121, report["coverage"]["sourceAliases"])
        self.assertEqual(8, report["coverage"]["mappedSourceBanks"])
        self.assertEqual(116, report["coverage"]["outsideCoverageBanks"])
        self.assertEqual(113, report["coverage"]["outsideCoverageAliases"])
        self.assertEqual(969, report["coverage"]["outsideCoverageRules"])
        self.assertEqual(960, report["coverage"]["outsideCoverageUniqueRules"])
        self.assertEqual(9, report["coverage"]["outsideCoverageDuplicateExpansions"])
        self.assertEqual(855, report["rules"]["expandedSource"])
        self.assertEqual(10, report["rules"]["duplicateExpansions"])
        self.assertEqual(845, report["rules"]["uniqueSource"])
        self.assertEqual(1824, report["rules"]["sourceArchiveExpanded"])
        self.assertEqual(19, report["rules"]["sourceArchiveDuplicateExpansions"])
        self.assertEqual(1805, report["rules"]["sourceArchiveUniqueRules"])
        self.assertEqual(832, report["legacyBaseline"]["rules"])
        self.assertEqual(778, report["differences"]["intersection"])
        self.assertEqual(67, report["differences"]["added"])
        self.assertEqual(54, report["differences"]["removed"])
        self.assertEqual(39, len(report["differences"]["typeChanges"]))
        self.assertEqual(15, len(report["differences"]["samePrefixDifferentLength"]))
        self.assertEqual((15, 19), (
            report["fullPanLength"]["minimum"], report["fullPanLength"]["maximum"]
        ))
        self.assertTrue(report["closure"]["legacyClosed"])
        self.assertTrue(report["closure"]["sourceClosed"])
        self.assertEqual(845, sum(len(bank["AllRules"]) for bank in output))

    def test_checked_in_outputs_are_deterministic_and_baseline_is_read_only(self) -> None:
        baseline_hash_before = hashlib.sha256(self.baseline_path.read_bytes()).hexdigest()
        outputs = [
            CONVERTER.build_conversion(
                self.source_banks, self.baseline, self.source_hashes, self.archive_members, self.archive_hash
            )
            for _ in range(2)
        ]
        self.assertEqual(outputs[0], outputs[1])
        output, report, metadata = outputs[0]
        self.assertEqual(
            CONVERTER.serialize_json(output),
            (ROOT / "src/Bing.MockData/Datas/Resources/Banks.json").read_bytes(),
        )
        # Evidence is generated locally; a clean checkout need not contain artifacts.
        report_path = ROOT / "artifacts/bank-resource-closure/banks-conversion.json"
        if report_path.exists():
            self.assertEqual(CONVERTER.serialize_json(report), report_path.read_bytes())
        self.assertEqual(
            CONVERTER.serialize_json(metadata),
            (self.source_dir / "source.json").read_bytes(),
        )
        isc_notice, provenance_notice = CONVERTER._notice_payloads()
        self.assertEqual(isc_notice, (ROOT / "src/Bing.MockData/Datas/Notices/bankcardinfo-ISC.txt").read_bytes())
        self.assertEqual(provenance_notice, (ROOT / "src/Bing.MockData/Datas/Notices/bankcardinfo-NOTICE.txt").read_bytes())
        self.assertEqual(baseline_hash_before, hashlib.sha256(self.baseline_path.read_bytes()).hexdigest())
        if self.preserved_baseline_path.exists():
            self.assertEqual(self.baseline_bytes, self.preserved_baseline_path.read_bytes())

    def test_fixture_expansion_preserves_order_and_deduplicates_exact_keys(self) -> None:
        source = fixture_source('{reg: /^(123|123|124)\\d{4}$/g, cardType: "DC"}')
        banks = CONVERTER.parse_source(source, "fixture.js")
        expansion = CONVERTER.expand_source(banks)

        self.assertEqual(3, len(expansion["occurrences"]))
        self.assertEqual(2, len(expansion["uniqueRules"]))
        self.assertEqual(1, len(expansion["duplicates"]))
        self.assertEqual(["123", "124"], [item["cardPrefix"] for item in expansion["uniqueRules"]])
        self.assertEqual("duplicate", expansion["occurrences"][1]["disposition"])
        self.assertEqual(1, expansion["occurrences"][1]["duplicateOf"]["prefixIndex"])

    def test_unknown_fields_and_duplicate_object_properties_fail_with_location(self) -> None:
        invalid_fields = fixture_source(
            '{reg: /^(123)\\d{4}$/g, cardType: "DC", unexpected: "x"}'
        )
        duplicate_property = fixture_source(
            '{reg: /^(123)\\d{4}$/g, cardType: "DC"}',
            'bankName: "样例银行", bankCode: "TEST", bankCode: "OTHER"',
        )

        with self.assertRaisesRegex(CONVERTER.ConversionError, "invalid shape/fields"):
            CONVERTER.parse_source(invalid_fields, "invalid-fields.js")
        with self.assertRaisesRegex(CONVERTER.ConversionError, r"duplicate object field 'bankCode'") as context:
            CONVERTER.parse_source(duplicate_property, "duplicate-field.js")
        self.assertIn("duplicate-field.js:1:", str(context.exception))

    def test_unknown_token_card_type_and_regex_form_fail_closed(self) -> None:
        unsupported_value = fixture_source('{reg: /^(123)\\d{4}$/g, cardType: makeType()}')
        invalid_type = fixture_source('{reg: /^(123)\\d{4}$/g, cardType: "UNKNOWN"}')
        invalid_regex = fixture_source('{reg: /^(123)\\d+$/g, cardType: "DC"}')
        invalid_flags = fixture_source('{reg: /^(123)\\d{4}$/i, cardType: "DC"}')

        for source in (unsupported_value, invalid_type, invalid_regex, invalid_flags):
            with self.subTest(source=source), self.assertRaises(CONVERTER.ConversionError):
                CONVERTER.parse_source(source, "unsupported.js")

    def test_cross_type_conflict_fails_instead_of_first_wins(self) -> None:
        source = fixture_source(
            '{reg: /^(123)\\d{4}$/g, cardType: "DC"}, '
            '{reg: /^(123)\\d{4}$/g, cardType: "CC"}'
        )
        banks = CONVERTER.parse_source(source, "cross-type.js")

        with self.assertRaisesRegex(CONVERTER.ConversionError, "prefix/length cross-type conflict") as context:
            CONVERTER.expand_source(banks)
        self.assertIn("cross-type.js", str(context.exception))
        self.assertIn("123/4", str(context.exception))


if __name__ == "__main__":
    unittest.main()
