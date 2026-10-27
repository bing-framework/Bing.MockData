import json
import tempfile
import unittest
from pathlib import Path

from import_resources import (
    parse_career_rows,
    parse_css_named_colors,
    parse_faker_names,
    project_china_divisions,
    project_city_names,
    project_country_names,
    project_simplified_chinese,
    write_json,
    write_lines,
)


class LegacyResourceImportTests(unittest.TestCase):
    def test_faker_names_are_read_from_ast_literals(self):
        source = """
from collections import OrderedDict
class Provider:
    first_names_female = OrderedDict((('Ada', 0.6), ('Bea', 0.4)))
    first_names_male = OrderedDict((('Alan', 1.0),))
    last_names = OrderedDict((('Smith', 1.0),))
"""
        parsed = parse_faker_names(source, ("first_names_female", "first_names_male", "last_names"))
        self.assertEqual(["Ada", "Bea"], parsed["first_names_female"])
        self.assertEqual(["Smith"], parsed["last_names"])

    def test_faker_parser_rejects_non_literal_names_without_executing_them(self):
        source = """
from collections import OrderedDict
def should_not_run():
    raise RuntimeError('executed')
class Provider:
    first_names_female = OrderedDict(((should_not_run(), 1.0),))
"""
        with self.assertRaisesRegex(ValueError, "non-string name"):
            parse_faker_names(source, ("first_names_female",))

    def test_career_rows_preserve_bilingual_profile_columns(self):
        source = r'''
private static readonly string[][] Rows =
{
    new[] { "行业", "Industry", "部门", "Department", "岗位", "Job" },
    new[] { "支持", "Support", "服务", "Service", "代表", "Representative" },
};
internal static CareerProfile[] Profiles(string locale) { }
'''
        self.assertEqual(
            [["行业", "Industry", "部门", "Department", "岗位", "Job"],
             ["支持", "Support", "服务", "Service", "代表", "Representative"]],
            parse_career_rows(source),
        )

    def test_css_table_parser_requires_148_unique_hex_colors(self):
        values = "|".join(f"color{i:03d} {i:06X}" for i in range(148))
        source = """private static readonly string[] Values = ("{values}").Split('|');""".format(values=values)
        parsed = parse_css_named_colors(source)
        self.assertEqual(148, len(parsed))
        self.assertEqual(("color000", "000000"), parsed[0])

    def test_cldr_projection_keeps_two_letter_named_regions(self):
        rows = [
            ("US", json.dumps({"nameEn": "United States", "nameEnMissing": False})),
            ("001", json.dumps({"nameEn": "World", "nameEnMissing": False})),
            ("ZZ", json.dumps({"nameEn": "Unknown Region", "nameEnMissing": False})),
            ("AA", json.dumps({"nameEn": "", "nameEnMissing": True})),
        ]
        names, excluded = project_country_names(rows)
        self.assertEqual(["United States"], names)
        self.assertEqual(
            {"nonRegionCode": 1, "nonCountryRegionCode": 1, "missingEnglishName": 1},
            excluded,
        )

    def test_geonames_uses_ascii_names_with_explicit_name_fallback(self):
        rows = [
            ("Sao Tome", json.dumps({"asciiName": "Sao Tome"}), "city"),
            ("東京", json.dumps({"asciiName": ""}), "city"),
            ("ignored", "{}", "postal"),
        ]
        names, fallback_count = project_city_names(rows)
        self.assertEqual(["Sao Tome", "東京"], names)
        self.assertEqual(1, fallback_count)

    def test_china_projection_uses_actual_parent_paths_and_id_prefixes(self):
        records = [
            {"Code": "110000", "Name": "北京市", "Level": 1, "ParentCode": None},
            {"Code": "110100", "Name": "市辖区", "Level": 2, "ParentCode": "110000"},
            {"Code": "110101", "Name": "东城区", "Level": 3, "ParentCode": "110100"},
            {"Code": "500000", "Name": "重庆市", "Level": 1, "ParentCode": None},
            {"Code": "500101", "Name": "万州区", "Level": 3, "ParentCode": "500000"},
        ]
        projected = project_china_divisions(records)
        self.assertEqual(["北京市", "重庆市"], projected["ChineseRegionList.txt"])
        self.assertEqual(["北京市", "重庆市"], projected["ChineseCityNames.txt"])
        self.assertEqual(["北京市市辖区东城区", "重庆市万州区"], projected["ProvinceCity.txt"])
        self.assertEqual(
            [{"Key": "11", "Value": "北京"}, {"Key": "50", "Value": "重庆"}],
            projected["ChineseAreaCode.json"],
        )

    def test_china_projection_rejects_missing_parent(self):
        records = [
            {"Code": "110000", "Name": "北京市", "Level": 1, "ParentCode": None},
            {"Code": "110101", "Name": "东城区", "Level": 3, "ParentCode": "110100"},
        ]
        with self.assertRaisesRegex(ValueError, "missing China division parent"):
            project_china_divisions(records)

    def test_independent_character_pool_is_unique_basic_cjk(self):
        source = "常用字符常用数据，中文 A1文本"
        self.assertEqual(list("常用字符数据中文本"), project_simplified_chinese(source))

    def test_legacy_resource_shapes_remain_plain_lines_and_json(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            write_lines(root / "items.txt", ["one", "two"])
            write_json(root / "items.json", ["one", "two"])
            self.assertEqual("one\ntwo\n", (root / "items.txt").read_text(encoding="utf-8"))
            self.assertEqual(["one", "two"], json.loads((root / "items.json").read_text(encoding="utf-8")))
            self.assertFalse((root / "items.txt").read_bytes().startswith(b"\xef\xbb\xbf"))


if __name__ == "__main__":
    unittest.main()
