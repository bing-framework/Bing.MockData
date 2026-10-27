"""Statically project the pinned bankcardinfo rules into the legacy bank model."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import tarfile
import tempfile
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIR = ROOT / "tools/data/bankcardinfo-2.0.4"
ARCHIVE_PATH = SOURCE_DIR / "bankcardinfo-2.0.4.tgz"
PACKAGE_DIR = SOURCE_DIR / "package"
BASELINE_PATH = SOURCE_DIR / "legacy-banks-baseline.json"
PRESERVED_BASELINE_PATH = ROOT / "artifacts/bank-resource-closure/legacy-banks-baseline.json"
OUTPUT_PATH = ROOT / "src/Bing.MockData/Datas/Resources/Banks.json"
EVIDENCE_PATH = ROOT / "artifacts/bank-resource-closure/banks-conversion.json"
METADATA_PATH = SOURCE_DIR / "source.json"
PACKAGE_NOTICE_DIR = ROOT / "src/Bing.MockData/Datas/Notices"
ISC_NOTICE_PATH = PACKAGE_NOTICE_DIR / "bankcardinfo-ISC.txt"
PROVENANCE_NOTICE_PATH = PACKAGE_NOTICE_DIR / "bankcardinfo-NOTICE.txt"

EXPECTED_ARCHIVE_SHA256 = "76a663e1e34185984a86cc43c639a24c9e0fb3cda6fb0beb7099f25cb2fd5f00"
SOURCE_COMMIT = "f0393148c98247348b515ece601939de51f12b23"
SOURCE_REPOSITORY = "https://github.com/navyxie/bankcardinfo"
NPM_TARBALL = "https://registry.npmjs.org/bankcardinfo/-/bankcardinfo-2.0.4.tgz"
SOURCE_ALIAS_TO_CODE = {
    "PSBC": "100",
    "ICBC": "102",
    "ABC": "103",
    "BOC": "104",
    "CCB": "105",
    "COMM": "301",
    "CMB": "308",
    "CMBC": "305",
}
CARD_TYPES = {"DC": 1, "CC": 2, "SCC": 3, "PC": 4}
EXPECTED_COUNTS = {
    "sourceBankCount": 124,
    "sourceAliasCount": 121,
    "mappedSourceBankCount": 8,
    "sourceArchiveExpandedRules": 1824,
    "sourceArchiveDuplicateExpansions": 19,
    "sourceArchiveUniqueRules": 1805,
    "expandedSourceRules": 855,
    "duplicateExpansions": 10,
    "uniqueSourceRules": 845,
    "legacyRules": 832,
    "intersection": 778,
    "added": 67,
    "removed": 54,
    "typeChanges": 39,
    "outsideCoverageBanks": 116,
    "outsideCoverageAliases": 113,
    "outsideCoverageRules": 969,
    "outsideCoverageUniqueRules": 960,
    "outsideCoverageDuplicateExpansions": 9,
    "minPanLength": 15,
    "maxPanLength": 19,
}
EXPECTED_ARCHIVE_MEMBERS = {
    "package/package.json",
    "package/.npmignore",
    "package/README.md",
    "package/index.js",
    "package/test.js",
    "package/.coveralls.yml",
    "package/.travis.yml",
}


class ConversionError(ValueError):
    """Raised when the pinned JavaScript data literal leaves its supported subset."""


class RegexLiteral:
    def __init__(self, pattern: str, flags: str, offset: int):
        self.pattern = pattern
        self.flags = flags
        self.offset = offset


class Token:
    def __init__(self, kind: str, value: Any, start: int, end: int):
        self.kind = kind
        self.value = value
        self.start = start
        self.end = end


def _location(text: str, offset: int) -> tuple[int, int]:
    line = text.count("\n", 0, offset) + 1
    previous_newline = text.rfind("\n", 0, offset)
    return line, offset - previous_newline


def _fail_at(text: str, offset: int, source_name: str, message: str) -> ConversionError:
    line, column = _location(text, offset)
    return ConversionError(f"{source_name}:{line}:{column}: {message}")


class Lexer:
    """Tokenize only the JSON-like object/array and regex-literal subset we accept."""

    PUNCTUATION = {"[", "]", "{", "}", ":", ","}

    def __init__(self, text: str, start: int, source_name: str):
        self.text = text
        self.index = start
        self.source_name = source_name

    def next_token(self) -> Token:
        text = self.text
        while self.index < len(text) and text[self.index].isspace():
            self.index += 1
        start = self.index
        if start >= len(text):
            return Token("eof", None, start, start)

        char = text[start]
        if char in self.PUNCTUATION:
            self.index += 1
            return Token(char, char, start, self.index)

        if char == '"':
            cursor = start + 1
            while cursor < len(text):
                if text[cursor] == "\\":
                    cursor += 2
                    continue
                if text[cursor] == '"':
                    cursor += 1
                    raw = text[start:cursor]
                    try:
                        value = json.loads(raw)
                    except json.JSONDecodeError as error:
                        raise _fail_at(text, start, self.source_name, f"invalid JSON string: {error.msg}") from error
                    self.index = cursor
                    return Token("string", value, start, cursor)
                if text[cursor] in "\r\n":
                    raise _fail_at(text, start, self.source_name, "newline in string literal")
                cursor += 1
            raise _fail_at(text, start, self.source_name, "unterminated string literal")

        if char == "/":
            cursor = start + 1
            escaped = False
            while cursor < len(text):
                current = text[cursor]
                if current in "\r\n":
                    raise _fail_at(text, start, self.source_name, "newline in regex literal")
                if escaped:
                    escaped = False
                elif current == "\\":
                    escaped = True
                elif current == "/":
                    close = cursor
                    cursor += 1
                    flags_start = cursor
                    while cursor < len(text) and text[cursor].isalpha() and text[cursor].isascii():
                        cursor += 1
                    value = RegexLiteral(text[start + 1:close], text[flags_start:cursor], start)
                    self.index = cursor
                    return Token("regex", value, start, cursor)
                cursor += 1
            raise _fail_at(text, start, self.source_name, "unterminated regex literal")

        if char.isalpha() and char.isascii() or char in "_$":
            cursor = start + 1
            while cursor < len(text) and (text[cursor].isascii() and (text[cursor].isalnum() or text[cursor] in "_$")):
                cursor += 1
            self.index = cursor
            return Token("identifier", text[start:cursor], start, cursor)

        raise _fail_at(text, start, self.source_name, f"unsupported token {char!r}")


class Parser:
    def __init__(self, lexer: Lexer):
        self.lexer = lexer
        self.buffer: Token | None = None
        self.last: Token | None = None

    def peek(self) -> Token:
        if self.buffer is None:
            self.buffer = self.lexer.next_token()
        return self.buffer

    def take(self) -> Token:
        token = self.peek()
        self.buffer = None
        self.last = token
        return token

    def expect(self, kind: str) -> Token:
        token = self.take()
        if token.kind != kind:
            raise _fail_at(self.lexer.text, token.start, self.lexer.source_name, f"expected {kind!r}, found {token.value!r}")
        return token

    def parse_array(self) -> list[Any]:
        self.expect("[")
        values = []
        if self.peek().kind == "]":
            self.take()
            return values
        while True:
            values.append(self.parse_value())
            token = self.take()
            if token.kind == "]":
                return values
            if token.kind != ",":
                raise _fail_at(self.lexer.text, token.start, self.lexer.source_name, "expected ',' or ']' in array")
            if self.peek().kind == "]":
                self.take()
                return values

    def parse_object(self) -> dict[str, Any]:
        self.expect("{")
        values: dict[str, Any] = {}
        if self.peek().kind == "}":
            self.take()
            return values
        while True:
            key = self.expect("identifier")
            self.expect(":")
            if key.value in values:
                raise _fail_at(self.lexer.text, key.start, self.lexer.source_name, f"duplicate object field {key.value!r}")
            values[key.value] = self.parse_value()
            token = self.take()
            if token.kind == "}":
                return values
            if token.kind != ",":
                raise _fail_at(self.lexer.text, token.start, self.lexer.source_name, "expected ',' or '}' in object")
            if self.peek().kind == "}":
                self.take()
                return values

    def parse_value(self) -> Any:
        token = self.peek()
        if token.kind == "[":
            return self.parse_array()
        if token.kind == "{":
            return self.parse_object()
        if token.kind in ("string", "regex"):
            return self.take().value
        raise _fail_at(self.lexer.text, token.start, self.lexer.source_name, f"unsupported value {token.value!r}")


def parse_source(text: str, source_name: str = "package/index.js") -> list[dict[str, Any]]:
    """Parse the bankcardList literal without evaluating any JavaScript."""
    assignments = list(re.finditer(r"\bvar\s+bankcardList\s*=\s*\[", text))
    if len(assignments) != 1:
        raise ConversionError(f"{source_name}: expected exactly one bankcardList array assignment, found {len(assignments)}")
    assignment = assignments[0]
    array_start = assignment.end() - 1
    parser = Parser(Lexer(text, array_start, source_name))
    banks = parser.parse_array()
    if parser.last is None:
        raise ConversionError(f"{source_name}: bankcardList array was empty at the parser level")
    terminator = re.match(r"\s*(?:;|(?=function\b))", text[parser.last.end:])
    if terminator is None:
        raise _fail_at(text, parser.last.end, source_name, "bankcardList literal must end with ';'")

    parsed_banks: list[dict[str, Any]] = []
    for bank_index, bank in enumerate(banks, 1):
        if not isinstance(bank, dict):
            raise ConversionError(f"{source_name}: bank entry {bank_index} must be an object")
        if set(bank) != {"bankName", "bankCode", "patterns"}:
            raise ConversionError(f"{source_name}: bank entry {bank_index} has unsupported fields: {sorted(set(bank) ^ {'bankName', 'bankCode', 'patterns'})}")
        name, alias, patterns = bank["bankName"], bank["bankCode"], bank["patterns"]
        if not isinstance(name, str) or not name.strip():
            raise ConversionError(f"{source_name}: bank entry {bank_index} has invalid bankName")
        if not isinstance(alias, str) or not re.fullmatch(r"[A-Z0-9]{2,16}", alias, flags=re.ASCII):
            raise ConversionError(f"{source_name}: bank entry {bank_index} has invalid bankCode {alias!r}")
        if not isinstance(patterns, list) or not patterns:
            raise ConversionError(f"{source_name}: {alias} patterns must be a nonempty array")

        parsed_patterns = []
        for pattern_index, item in enumerate(patterns, 1):
            if not isinstance(item, dict) or set(item) != {"reg", "cardType"}:
                fields = sorted(set(item) ^ {"reg", "cardType"}) if isinstance(item, dict) else []
                raise ConversionError(f"{source_name}: {alias} pattern {pattern_index} has invalid shape/fields {fields}")
            regex = item["reg"]
            card_type = item["cardType"]
            if not isinstance(regex, RegexLiteral):
                raise ConversionError(f"{source_name}: {alias} pattern {pattern_index} reg must be a regex literal")
            if regex.flags != "g":
                raise _fail_at(text, regex.offset, source_name, f"{alias} pattern {pattern_index} uses unsupported regex flags {regex.flags!r}")
            if not isinstance(card_type, str) or card_type not in CARD_TYPES:
                raise _fail_at(text, regex.offset, source_name, f"{alias} pattern {pattern_index} uses unsupported cardType {card_type!r}")
            match = re.fullmatch(r"\^\((?P<prefixes>[0-9]*(?:\|[0-9]*)*)\)\\d\{(?P<suffix>[0-9]+)\}\$", regex.pattern, flags=re.ASCII)
            if match is None:
                raise _fail_at(text, regex.offset, source_name, f"{alias} pattern {pattern_index} has unsupported regex syntax /{regex.pattern}/{regex.flags}")
            prefixes = match.group("prefixes").split("|")
            suffix_length = int(match.group("suffix"))
            if suffix_length < 1:
                raise _fail_at(text, regex.offset, source_name, f"{alias} pattern {pattern_index} has nonpositive suffix length")
            if any(prefix and not 3 <= len(prefix) <= 10 for prefix in prefixes):
                raise _fail_at(text, regex.offset, source_name, f"{alias} pattern {pattern_index} has a prefix outside the supported empty-or-3-10 ASCII digits")
            line, column = _location(text, regex.offset)
            parsed_patterns.append({
                "prefixes": prefixes,
                "suffixLength": suffix_length,
                "cardType": CARD_TYPES[card_type],
                "cardTypeCode": card_type,
                "patternIndex": pattern_index,
                "sourceLine": line,
                "sourceColumn": column,
                "regex": regex.pattern,
            })
        parsed_banks.append({"name": name, "alias": alias, "sourceIndex": bank_index,
                             "sourceFile": source_name, "patterns": parsed_patterns})
    return parsed_banks


def expand_source(banks: list[dict[str, Any]]) -> dict[str, Any]:
    """Expand alternatives, rejecting source-internal type ambiguity and deduplicating exact keys."""
    occurrences = []
    unique_rules = []
    duplicates = []
    first_by_key = {}
    type_by_prefix_length = {}
    raw_counts: dict[int, int] = {}
    alias_raw_counts: dict[str, int] = {}
    for bank in banks:
        alias = bank["alias"]
        raw_counts[bank["sourceIndex"]] = 0
        alias_raw_counts[alias] = alias_raw_counts.get(alias, 0)
        for pattern in bank["patterns"]:
            for prefix_index, prefix in enumerate(pattern["prefixes"], 1):
                raw_counts[bank["sourceIndex"]] += 1
                alias_raw_counts[alias] += 1
                item = {
                    "sourceAlias": alias,
                    "sourceBankName": bank["name"],
                    "sourceBankIndex": bank["sourceIndex"],
                    "sourceFile": bank.get("sourceFile", "package/index.js"),
                    "patternIndex": pattern["patternIndex"],
                    "prefixIndex": prefix_index,
                    "cardPrefix": prefix,
                    "suffixLength": pattern["suffixLength"],
                    "cardType": pattern["cardType"],
                    "cardTypeCode": pattern["cardTypeCode"],
                    "sourceLine": pattern["sourceLine"],
                    "sourceColumn": pattern["sourceColumn"],
                    "regex": pattern["regex"],
                }
                ambiguity_key = (alias, prefix, pattern["suffixLength"])
                prior_type = type_by_prefix_length.get(ambiguity_key)
                if prior_type is not None and prior_type["cardType"] != pattern["cardType"]:
                    raise ConversionError(
                        f"{item['sourceFile']}:{item['sourceLine']}:{item['sourceColumn']}: {alias} prefix/length cross-type conflict "
                        f"{prefix}/{pattern['suffixLength']}: type {prior_type['cardTypeCode']} at line {prior_type['sourceLine']}, "
                        f"type {pattern['cardTypeCode']}"
                    )
                type_by_prefix_length.setdefault(ambiguity_key, item)
                key = (alias, prefix, pattern["suffixLength"], pattern["cardType"])
                first = first_by_key.get(key)
                if first is None:
                    first_by_key[key] = item
                    item["disposition"] = "retained"
                    unique_rules.append(item)
                else:
                    item["disposition"] = "duplicate"
                    item["duplicateOf"] = {
                        "sourceLine": first["sourceLine"],
                        "patternIndex": first["patternIndex"],
                        "prefixIndex": first["prefixIndex"],
                    }
                    duplicates.append(item)
                occurrences.append(item)

    return {
        "occurrences": occurrences,
        "uniqueRules": unique_rules,
        "duplicates": duplicates,
        "rawCounts": raw_counts,
        "aliasRawCounts": alias_raw_counts,
        "sourceInternalTypeConflicts": 0,
    }


def _baseline_keys(baseline: list[dict[str, Any]]) -> tuple[set[tuple[str, str, int, int]], list[int]]:
    if not isinstance(baseline, list):
        raise ConversionError("legacy baseline must be a JSON array")
    codes = []
    keys = set()
    for bank in baseline:
        if not isinstance(bank, dict) or not isinstance(bank.get("Code"), str):
            raise ConversionError("legacy baseline contains a malformed bank")
        code = bank["Code"]
        codes.append(code)
        rules = bank.get("AllRules")
        if not isinstance(rules, list):
            raise ConversionError(f"legacy baseline bank {code} has no AllRules array")
        for rule in rules:
            try:
                key = (code, rule["CardPrefix"], int(rule["Length"]), int(rule["Type"]))
            except (KeyError, TypeError, ValueError) as error:
                raise ConversionError(f"legacy baseline bank {code} has a malformed rule") from error
            if key in keys:
                raise ConversionError(f"legacy baseline has duplicate full key: {key}")
            keys.add(key)
    if len(codes) != len(set(codes)):
        raise ConversionError("legacy baseline has duplicate bank codes")
    if set(codes) != set(SOURCE_ALIAS_TO_CODE.values()):
        raise ConversionError("legacy baseline codes do not match the fixed eight-bank mapping")
    return keys, [len(bank["AllRules"]) for bank in baseline]


def _rule_key(rule: dict[str, Any], code: str) -> tuple[str, str, int, int]:
    return (code, rule["cardPrefix"], rule["suffixLength"], rule["cardType"])


def serialize_json(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def build_conversion(source_banks: list[dict[str, Any]], baseline: list[dict[str, Any]],
                     source_hashes: dict[str, str] | None = None,
                     archive_members: list[str] | None = None,
                     archive_sha256: str = EXPECTED_ARCHIVE_SHA256) -> tuple[list[dict[str, Any]], dict[str, Any], dict[str, Any]]:
    expansion = expand_source(source_banks)
    unique_rules = expansion["uniqueRules"]
    baseline_keys, legacy_counts = _baseline_keys(baseline)
    alias_by_code = {code: alias for alias, code in SOURCE_ALIAS_TO_CODE.items()}
    source_codes = {rule["sourceAlias"]: SOURCE_ALIAS_TO_CODE.get(rule["sourceAlias"]) for rule in unique_rules}
    outside_aliases = {alias for alias, code in source_codes.items() if code is None}
    if set(alias_by_code) != set(SOURCE_ALIAS_TO_CODE.values()):
        raise ConversionError("internal mapping mismatch")

    by_alias: dict[str, list[dict[str, Any]]] = {}
    for rule in unique_rules:
        by_alias.setdefault(rule["sourceAlias"], []).append(rule)
    output = []
    old_key_order = []
    source_key_set = set()
    for bank in baseline:
        code = bank["Code"]
        alias = alias_by_code[code]
        rules = by_alias.get(alias, [])
        if not rules:
            raise ConversionError(f"mapped source bank {alias} has no rules")
        if any(not rule["cardPrefix"] for rule in rules):
            raise ConversionError(f"mapped source bank {alias} contains an empty card prefix")
        output_rules = [
            {"CardPrefix": rule["cardPrefix"], "Length": rule["suffixLength"], "Type": rule["cardType"]}
            for rule in rules
        ]
        output.append({
            "Name": bank["Name"],
            "AbbrName": bank.get("AbbrName"),
            "Code": code,
            "AllRules": output_rules,
        })
        for old_rule in bank["AllRules"]:
            old_key_order.append((code, old_rule["CardPrefix"], int(old_rule["Length"]), int(old_rule["Type"])))
        for rule in rules:
            source_key_set.add(_rule_key(rule, code))

    legacy_key_set = set(old_key_order)
    intersection = legacy_key_set & source_key_set
    added = [rule for rule in unique_rules if rule["sourceAlias"] in SOURCE_ALIAS_TO_CODE
             and _rule_key(rule, SOURCE_ALIAS_TO_CODE[rule["sourceAlias"]]) not in legacy_key_set]
    removed = []
    for bank in baseline:
        for rule in bank["AllRules"]:
            key = (bank["Code"], rule["CardPrefix"], int(rule["Length"]), int(rule["Type"]))
            if key not in source_key_set:
                removed.append({"Code": bank["Code"], "CardPrefix": rule["CardPrefix"], "Length": int(rule["Length"]), "Type": int(rule["Type"])})

    old_by_prefix_length: dict[tuple[str, str, int], set[int]] = {}
    source_by_prefix_length: dict[tuple[str, str, int], set[int]] = {}
    for code, prefix, suffix, card_type in legacy_key_set:
        old_by_prefix_length.setdefault((code, prefix, suffix), set()).add(card_type)
    for code, prefix, suffix, card_type in source_key_set:
        source_by_prefix_length.setdefault((code, prefix, suffix), set()).add(card_type)
    type_changes = []
    for group_key in sorted(old_by_prefix_length.keys() & source_by_prefix_length.keys()):
        old_types = old_by_prefix_length[group_key]
        source_types = source_by_prefix_length[group_key]
        if old_types != source_types:
            type_changes.append({
                "Code": group_key[0], "CardPrefix": group_key[1], "Length": group_key[2],
                "OldTypes": sorted(old_types), "SourceTypes": sorted(source_types),
            })

    same_prefix_length_changes = []
    for rule in removed:
        code, prefix, old_suffix, card_type = rule["Code"], rule["CardPrefix"], rule["Length"], rule["Type"]
        replacement = [item for item in unique_rules if item["sourceAlias"] == alias_by_code[code]
                      and item["cardPrefix"] == prefix and item["cardType"] == card_type
                      and item["suffixLength"] != old_suffix]
        if replacement:
            same_prefix_length_changes.append({
                "Code": code, "CardPrefix": prefix, "OldLength": old_suffix,
                "SourceLengths": sorted({item["suffixLength"] for item in replacement}), "Type": card_type,
            })

    mapped_occurrences = [item for item in expansion["occurrences"] if item["sourceAlias"] in SOURCE_ALIAS_TO_CODE]
    mapped_unique_rules = [rule for rule in unique_rules if rule["sourceAlias"] in SOURCE_ALIAS_TO_CODE]
    mapped_duplicates = [item for item in expansion["duplicates"] if item["sourceAlias"] in SOURCE_ALIAS_TO_CODE]
    raw_count = len(mapped_occurrences)
    unique_count = len(mapped_unique_rules)
    pan_lengths = [len(rule["cardPrefix"]) + rule["suffixLength"] for rule in unique_rules
                   if rule["sourceAlias"] in SOURCE_ALIAS_TO_CODE]
    per_bank = []
    old_by_code = {bank["Code"]: bank for bank in baseline}
    for source_bank in source_banks:
        alias = source_bank["alias"]
        code = SOURCE_ALIAS_TO_CODE.get(alias)
        source_bank_rules = by_alias.get(alias, [])
        if code is None:
            continue
        old_bank = old_by_code[code]
        source_keys = {_rule_key(rule, code) for rule in source_bank_rules}
        old_keys = {(code, r["CardPrefix"], int(r["Length"]), int(r["Type"])) for r in old_bank["AllRules"]}
        per_bank.append({
            "sourceIndex": source_bank["sourceIndex"], "sourceAlias": alias, "legacyCode": code,
            "sourceName": source_bank["name"], "legacyName": old_bank["Name"],
            "expanded": expansion["aliasRawCounts"][alias], "unique": len(source_bank_rules),
            "duplicates": expansion["aliasRawCounts"][alias] - len(source_bank_rules),
            "legacy": len(old_bank["AllRules"]), "intersection": len(source_keys & old_keys),
            "added": len(source_keys - old_keys), "removed": len(old_keys - source_keys),
            "typeChanges": sum(1 for change in type_changes if change["Code"] == code),
        })

    outside_banks = []
    for source_bank in source_banks:
        if source_bank["alias"] not in SOURCE_ALIAS_TO_CODE:
            source_bank_occurrences = [item for item in expansion["occurrences"]
                                       if item["sourceBankIndex"] == source_bank["sourceIndex"]]
            entry_keys = {(item["cardPrefix"], item["suffixLength"], item["cardType"])
                          for item in source_bank_occurrences}
            outside_banks.append({
                "sourceIndex": source_bank["sourceIndex"], "sourceAlias": source_bank["alias"],
                "sourceName": source_bank["name"], "expandedRules": len(source_bank_occurrences),
                "uniqueRules": len(entry_keys),
                "duplicateExpansions": len(source_bank_occurrences) - len(entry_keys),
            })
    outside_aliases = {bank["alias"] for bank in source_banks if bank["alias"] not in SOURCE_ALIAS_TO_CODE}
    outside_occurrences = [item for item in expansion["occurrences"] if item["sourceAlias"] in outside_aliases]
    outside_unique_rules = [rule for rule in unique_rules if rule["sourceAlias"] in outside_aliases]

    data_json = serialize_json(output)
    report = {
        "formatVersion": 1,
        "status": "PASS",
        "source": {
            "package": "bankcardinfo", "version": "2.0.4", "archive": "tools/data/bankcardinfo-2.0.4/bankcardinfo-2.0.4.tgz",
            "archiveSha256": archive_sha256, "indexSha256": (source_hashes or {}).get("package/index.js"),
            "repository": SOURCE_REPOSITORY, "repositoryCommit": SOURCE_COMMIT,
        },
        "legacyBaseline": {
            "path": "tools/data/bankcardinfo-2.0.4/legacy-banks-baseline.json",
            "sha256": (source_hashes or {}).get("legacy-banks-baseline.json"),
            "rules": len(legacy_key_set),
        },
        "mapping": [{"sourceAlias": alias, "legacyCode": code} for alias, code in SOURCE_ALIAS_TO_CODE.items()],
        "coverage": {
            "sourceBanks": len(source_banks), "sourceAliases": len({bank["alias"] for bank in source_banks}),
            "mappedSourceBanks": sum(1 for bank in source_banks if bank["alias"] in SOURCE_ALIAS_TO_CODE),
            "mappedBanks": len(SOURCE_ALIAS_TO_CODE),
            "outsideCoverageBanks": len(outside_banks),
            "outsideCoverageAliases": len(outside_aliases),
            "outsideCoverageRules": len(outside_occurrences),
            "outsideCoverageUniqueRules": len(outside_unique_rules),
            "outsideCoverageDuplicateExpansions": sum(1 for item in outside_occurrences if item["disposition"] == "duplicate"),
            "outsideCoverage": outside_banks,
        },
        "rules": {
            "expandedSource": raw_count, "duplicateExpansions": len(mapped_duplicates),
            "uniqueSource": unique_count, "sourceInternalCrossTypeConflicts": expansion["sourceInternalTypeConflicts"],
            "sourceArchiveExpanded": len(expansion["occurrences"]),
            "sourceArchiveDuplicateExpansions": len(expansion["duplicates"]),
            "sourceArchiveUniqueRules": len(unique_rules),
            "sourceOccurrences": expansion["occurrences"],
        },
        "differences": {
            "intersection": len(intersection), "added": len(added), "removed": len(removed),
            "typeChanges": type_changes, "samePrefixDifferentLength": same_prefix_length_changes,
            "addedRules": [{"Code": SOURCE_ALIAS_TO_CODE[rule["sourceAlias"]], "CardPrefix": rule["cardPrefix"],
                            "Length": rule["suffixLength"], "Type": rule["cardType"],
                            "sourceLine": rule["sourceLine"], "sourcePatternIndex": rule["patternIndex"],
                            "sourcePrefixIndex": rule["prefixIndex"]} for rule in added],
            "removedRules": removed,
        },
        "closure": {
            "legacy": f"{len(intersection)} + {len(removed)} = {len(legacy_key_set)}",
            "source": f"{len(intersection)} + {len(added)} = {len(source_key_set)}",
            "legacyClosed": len(intersection) + len(removed) == len(legacy_key_set),
            "sourceClosed": len(intersection) + len(added) == len(source_key_set),
        },
        "fullPanLength": {"minimum": min(pan_lengths), "maximum": max(pan_lengths),
                          "definition": "prefix digit count + suffix Length; suffix Length includes the Luhn check digit"},
        "perBank": per_bank,
        "output": {
            "path": "src/Bing.MockData/Datas/Resources/Banks.json",
            "sha256": hashlib.sha256(data_json).hexdigest(), "bytes": len(data_json),
            "banks": len(output), "rules": sum(len(bank["AllRules"]) for bank in output),
        },
        "archiveMembers": archive_members or [],
    }
    metadata = {
        "package": "bankcardinfo", "version": "2.0.4", "published": "2018-04-20T11:29:29.785Z",
        "sourceRepository": SOURCE_REPOSITORY, "sourceCommitLocator": f"{SOURCE_REPOSITORY}/tree/{SOURCE_COMMIT}",
        "npmTarball": NPM_TARBALL, "archiveSha256": archive_sha256,
        "author": {"name": "navyxie", "attribution": "package.json author field; not asserted to be the copyright holder"},
        "license": {
            "declaredInPackageJson": "ISC", "standaloneLicenseInPublishedArchive": False,
            "supplementalText": "src/Bing.MockData/Datas/Notices/bankcardinfo-ISC.txt",
            "supplementalTextIsUpstreamFile": False,
        },
        "includedPackageFiles": source_hashes or {},
        "legacyBaselineSnapshot": {
            "path": "tools/data/bankcardinfo-2.0.4/legacy-banks-baseline.json",
            "sha256": (source_hashes or {}).get("legacy-banks-baseline.json"),
        },
        "archiveMembers": archive_members or [],
        "sourceDescription": "The upstream README describes the bundled rules as a 2013 domestic bankcard collection and documents an Alipay API fallback. This conversion reads only the static bankcardList literal; it does not execute JavaScript or make API requests.",
        "scope": {
            "sourceBankCount": len(source_banks), "sourceAliasCount": len({bank["alias"] for bank in source_banks}),
            "mappedSourceBankCount": sum(1 for bank in source_banks if bank["alias"] in SOURCE_ALIAS_TO_CODE),
            "mappedBankCount": len(SOURCE_ALIAS_TO_CODE),
            "mappedAliases": list(SOURCE_ALIAS_TO_CODE), "outsideCoverageBanks": len(outside_banks),
            "outsideCoverageRules": len(outside_occurrences),
            "outsideCoverageUniqueRules": len(outside_unique_rules),
        },
    }
    return output, report, metadata


def _notice_payloads() -> tuple[bytes, bytes]:
    isc = """ISC License (standard text supplied as a supplemental notice)

[Upstream copyright notice not supplied; no copyright holder or year is inferred.]

Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted, provided that the above copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED \"AS IS\" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

The package.json in bankcardinfo@2.0.4 declares the ISC license. This file is a supplemental transcription of the standard ISC terms; it is not represented as a file from the upstream archive.
"""
    provenance = """Bankcard rule data provenance

Source: bankcardinfo npm package 2.0.4
Archive: https://registry.npmjs.org/bankcardinfo/-/bankcardinfo-2.0.4.tgz
Archive SHA-256: 76a663e1e34185984a86cc43c639a24c9e0fb3cda6fb0beb7099f25cb2fd5f00
Repository: https://github.com/navyxie/bankcardinfo
Source locator: f0393148c98247348b515ece601939de51f12b23
Package author field: navyxie (attribution only; not asserted as copyright holder)
Package license field: ISC

The published archive contains no standalone LICENSE file and provides no copyright holder/year notice. The companion bankcardinfo-ISC.txt is supplemental standard license text, not an upstream file.

Conversion scope: statically parse only the bankcardList literal in package/index.js; do not execute JavaScript or call the package's API fallback. Expand prefix alternatives and retain unique (source bank, prefix, suffix digit count, card type) rules in source order. The eight source aliases map to legacy codes as recorded in artifacts/bank-resource-closure/banks-conversion.json. The suffix digit count includes the Luhn check digit. The converted rules are sample-generation inputs, not a current official directory and not evidence that a generated number is issued, active, or payable.
"""
    return isc.encode("utf-8"), provenance.encode("utf-8")


def validate_reference_counts(report: dict[str, Any]) -> None:
    observed = {
        "sourceBankCount": report["coverage"]["sourceBanks"],
        "mappedSourceBankCount": report["coverage"]["mappedSourceBanks"],
        "sourceArchiveExpandedRules": report["rules"]["sourceArchiveExpanded"],
        "sourceArchiveDuplicateExpansions": report["rules"]["sourceArchiveDuplicateExpansions"],
        "sourceArchiveUniqueRules": report["rules"]["sourceArchiveUniqueRules"],
        "expandedSourceRules": report["rules"]["expandedSource"],
        "duplicateExpansions": report["rules"]["duplicateExpansions"],
        "uniqueSourceRules": report["rules"]["uniqueSource"],
        "legacyRules": report["legacyBaseline"]["rules"],
        "intersection": report["differences"]["intersection"],
        "added": report["differences"]["added"],
        "removed": report["differences"]["removed"],
        "typeChanges": len(report["differences"]["typeChanges"]),
        "outsideCoverageBanks": report["coverage"]["outsideCoverageBanks"],
        "sourceAliasCount": report["coverage"]["sourceAliases"],
        "outsideCoverageAliases": report["coverage"]["outsideCoverageAliases"],
        "outsideCoverageRules": report["coverage"]["outsideCoverageRules"],
        "outsideCoverageUniqueRules": report["coverage"]["outsideCoverageUniqueRules"],
        "outsideCoverageDuplicateExpansions": report["coverage"]["outsideCoverageDuplicateExpansions"],
        "minPanLength": report["fullPanLength"]["minimum"],
        "maxPanLength": report["fullPanLength"]["maximum"],
    }
    if observed != EXPECTED_COUNTS:
        raise ConversionError(f"pinned-source count mismatch: expected {EXPECTED_COUNTS}, observed {observed}")
    if not report["closure"]["legacyClosed"] or not report["closure"]["sourceClosed"]:
        raise ConversionError("full-key count closure failed")


def _verify_pinned_package() -> tuple[bytes, dict[str, bytes], list[str]]:
    archive_bytes = ARCHIVE_PATH.read_bytes()
    archive_sha256 = hashlib.sha256(archive_bytes).hexdigest()
    if archive_sha256 != EXPECTED_ARCHIVE_SHA256:
        raise ConversionError(f"pinned npm archive SHA-256 mismatch: {archive_sha256}")
    package_files: dict[str, bytes] = {}
    with tarfile.open(ARCHIVE_PATH, mode="r:gz") as archive:
        members = archive.getmembers()
        names = sorted(member.name for member in members if member.isfile())
        if set(names) != EXPECTED_ARCHIVE_MEMBERS:
            raise ConversionError(f"pinned npm archive member list changed: {names}")
        if any(Path(name).name.casefold() in {"license", "license.txt", "license.md"} for name in names):
            raise ConversionError("archive unexpectedly contains a standalone license file")
        wanted = {"package/index.js", "package/package.json", "package/README.md"}
        for member in members:
            if member.name in wanted:
                stream = archive.extractfile(member)
                if stream is None:
                    raise ConversionError(f"cannot read archive member {member.name}")
                package_files[member.name] = stream.read()
    if set(package_files) != {"package/index.js", "package/package.json", "package/README.md"}:
        raise ConversionError("pinned package is missing a required source file")
    for name, payload in package_files.items():
        extracted = PACKAGE_DIR / Path(name).name
        if not extracted.is_file() or extracted.read_bytes() != payload:
            raise ConversionError(f"extracted source does not match pinned archive: {name}")
    metadata = json.loads(package_files["package/package.json"].decode("utf-8"))
    if metadata.get("name") != "bankcardinfo" or metadata.get("version") != "2.0.4":
        raise ConversionError("npm package identity mismatch")
    if metadata.get("license") != "ISC" or metadata.get("author", {}).get("name") != "navyxie":
        raise ConversionError("npm package author/license metadata mismatch")
    return archive_bytes, package_files, names


def _write_if_changed(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() == payload:
        return
    descriptor, temporary_name = tempfile.mkstemp(prefix=path.name + ".", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(payload)
        os.replace(temporary_name, path)
    except BaseException:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass
        raise


def run_conversion() -> dict[str, Any]:
    archive_bytes, package_files, archive_members = _verify_pinned_package()
    baseline_bytes = BASELINE_PATH.read_bytes()
    if PRESERVED_BASELINE_PATH.exists() and PRESERVED_BASELINE_PATH.read_bytes() != baseline_bytes:
        raise ConversionError("versioned legacy baseline differs from the preserved artifact baseline")
    baseline = json.loads(baseline_bytes.decode("utf-8-sig"))
    source_text = package_files["package/index.js"].decode("utf-8-sig")
    source_banks = parse_source(source_text)
    source_hashes = {
        name: hashlib.sha256(data).hexdigest()
        for name, data in package_files.items()
    }
    source_hashes["legacy-banks-baseline.json"] = hashlib.sha256(baseline_bytes).hexdigest()
    output, report, metadata = build_conversion(
        source_banks, baseline, source_hashes, archive_members, hashlib.sha256(archive_bytes).hexdigest())
    validate_reference_counts(report)
    output_bytes = serialize_json(output)
    evidence_bytes = serialize_json(report)
    metadata_bytes = serialize_json(metadata)
    isc_notice_bytes, provenance_notice_bytes = _notice_payloads()
    protected = BASELINE_PATH.resolve()
    destinations = [OUTPUT_PATH.resolve(), EVIDENCE_PATH.resolve(), METADATA_PATH.resolve(),
                    ISC_NOTICE_PATH.resolve(), PROVENANCE_NOTICE_PATH.resolve()]
    if protected in destinations:
        raise ConversionError("refusing to overwrite the preserved legacy baseline")
    if len(set(destinations)) != len(destinations):
        raise ConversionError("conversion output destinations must be distinct")
    _write_if_changed(OUTPUT_PATH, output_bytes)
    _write_if_changed(EVIDENCE_PATH, evidence_bytes)
    _write_if_changed(METADATA_PATH, metadata_bytes)
    _write_if_changed(ISC_NOTICE_PATH, isc_notice_bytes)
    _write_if_changed(PROVENANCE_NOTICE_PATH, provenance_notice_bytes)
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_args()
    report = run_conversion()
    summary = {
        "status": report["status"], "expanded": report["rules"]["expandedSource"],
        "duplicates": report["rules"]["duplicateExpansions"], "unique": report["rules"]["uniqueSource"],
        "intersection": report["differences"]["intersection"], "added": report["differences"]["added"],
        "removed": report["differences"]["removed"], "typeChanges": len(report["differences"]["typeChanges"]),
        "outputSha256": report["output"]["sha256"],
    }
    print(json.dumps(summary, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
