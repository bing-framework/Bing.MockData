"""Versioned offline catalog archive. The external manifest is the trust anchor.

No runtime downloads. Restore publishes a new directory only after verifying every
file; cancellation and failures remove only the staging directory owned by this run.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import sqlite3
import stat
import tempfile
import urllib.request
import zipfile

CHUNK = 1024 * 1024


def check():
    """Injection point for cooperative cancellation in callers/tests."""


def safe_name(name):
    # Portable across Windows and Unix, including NTFS streams and DOS devices.
    if not isinstance(name, str) or not name or "\\" in name:
        raise ValueError(f"Unsafe archive path: {name!r}")
    parts = name.split("/")
    for part in parts:
        if (part in ("", ".", "..") or part.endswith((" ", "."))
                or re.search(r'[<>:"|?*\x00-\x1f]', part)
                or re.match(r"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", part, re.I)):
            raise ValueError(f"Unsafe archive path: {name!r}")
    return name


def digest_file(path, cancel=check):
    digest = hashlib.sha256()
    size = 0
    with open(path, "rb") as stream:
        while True:
            cancel()
            block = stream.read(CHUNK)
            if not block:
                break
            digest.update(block)
            size += len(block)
    return size, digest.hexdigest()


def json_write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def pack(data_root, archive, manifest, version, cancel=check):
    if not str(data_root).strip():
        raise ValueError("Explicit data root must not be empty")
    root, archive, manifest = Path(data_root).resolve(), Path(archive).resolve(), Path(manifest).resolve()
    if archive == manifest:
        raise ValueError("Archive and manifest paths must differ")
    if not root.is_dir() or not version.strip():
        raise ValueError("Data root and nonempty version are required")
    if archive.exists() or manifest.exists():
        raise FileExistsError("Bundle outputs already exist; choose a new version/output")
    if root in archive.parents or root in manifest.parents:
        raise ValueError("Bundle outputs must be outside the input data root")
    files = sorted(p for p in root.rglob("*") if p.is_file())
    if not files:
        raise ValueError("Empty data root")
    names = [safe_name(p.relative_to(root).as_posix()) for p in files]
    if len(set(n.casefold() for n in names)) != len(names):
        raise ValueError("Case-insensitive path collision")
    archive.parent.mkdir(parents=True, exist_ok=True)
    manifest.parent.mkdir(parents=True, exist_ok=True)
    temporary = archive.with_name(archive.name + ".partial")
    entries, datasets = [], []
    temporary_file = temporary.open("xb")
    try:
        # Exclusive creation also protects an interrupted earlier packaging run.
        with temporary_file, zipfile.ZipFile(temporary_file, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as output:
            for source, name in zip(files, names):
                cancel()
                if source.is_symlink() or root not in source.resolve().parents:
                    raise ValueError(f"Symlink input: {source}")
                digest = hashlib.sha256()
                size = 0
                info = zipfile.ZipInfo(name, date_time=(2026, 10, 27, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = (stat.S_IFREG | 0o644) << 16
                with source.open("rb") as input_stream, output.open(info, "w", force_zip64=True) as target:
                    while True:
                        cancel()
                        block = input_stream.read(CHUNK)
                        if not block:
                            break
                        size += len(block)
                        digest.update(block)
                        target.write(block)
                entries.append(dict(path=name, bytes=size, sha256=digest.hexdigest()))
                if name.endswith(".sqlite3"):
                    connection = sqlite3.connect(source.as_uri() + "?mode=ro", uri=True)
                    try:
                        connection.row_factory = sqlite3.Row
                        metadata = dict(connection.execute("SELECT * FROM CatalogInfo").fetchone())
                    finally:
                        connection.close()
                    datasets.append(dict(path=name, metadata=metadata))
        size, sha = digest_file(temporary, cancel)
        result = dict(formatVersion=1, version=version, archive=dict(name=archive.name, bytes=size, sha256=sha), files=entries, datasets=datasets)
        cancel()
        temporary.rename(archive)
        json_write(manifest, result)
        return result
    except BaseException:
        # Neither destination existed on entry; never remove another input.
        temporary.unlink(missing_ok=True)
        raise


def validate_manifest(manifest, version):
    if manifest.get("formatVersion") != 1 or manifest.get("version") != version:
        raise ValueError("Trusted manifest version mismatch")
    if not manifest.get("files"):
        raise ValueError("Manifest has no files")
    names = set()
    for item in [manifest["archive"]] + manifest["files"]:
        if type(item["bytes"]) is not int or item["bytes"] < 0 or not re.fullmatch(r"[0-9a-f]{64}", item["sha256"]):
            raise ValueError("Invalid size or SHA-256 in trusted manifest")
    for item in manifest["files"]:
        name = safe_name(item["path"])
        if name.casefold() in names:
            raise ValueError("Duplicate manifest path")
        names.add(name.casefold())
    return {item["path"]: item for item in manifest["files"]}


class HttpsOnly(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if urllib.parse.urlsplit(newurl).scheme != "https":
            raise ValueError("Archive redirects must remain HTTPS")
        return super().redirect_request(req, fp, code, msg, headers, newurl)


def restore(source, manifest_path, destination, version, cancel=check):
    trusted = json.loads(Path(manifest_path).read_text(encoding="utf-8-sig"))
    entries = validate_manifest(trusted, version)
    destination = Path(destination).absolute()
    if destination.exists() or destination.is_symlink():
        raise FileExistsError("Restore destination must be new")
    destination.parent.mkdir(parents=True, exist_ok=True)
    parent = destination.parent.resolve()
    destination = parent / destination.name
    stage = Path(tempfile.mkdtemp(prefix=".catalog-restore-", dir=parent)).resolve()
    try:
        cancel()
        archive_path = stage / "input.zip"
        if source.startswith("https://"):
            input_stream = urllib.request.build_opener(HttpsOnly()).open(source, timeout=30)
        elif "://" in source:
            raise ValueError("Only fixed HTTPS URLs or local archives are supported")
        else:
            input_stream = open(source, "rb")
        digest, count = hashlib.sha256(), 0
        with input_stream, archive_path.open("xb") as output:
            while True:
                cancel()
                block = input_stream.read(CHUNK)
                if not block:
                    break
                count += len(block)
                if count > trusted["archive"]["bytes"]:
                    raise ValueError("Archive exceeds trusted size")
                digest.update(block)
                output.write(block)
        if count != trusted["archive"]["bytes"] or digest.hexdigest() != trusted["archive"]["sha256"]:
            raise ValueError("Archive size or SHA-256 mismatch")
        content = stage / "data"
        content.mkdir()
        with zipfile.ZipFile(archive_path) as archive:
            actual = archive.infolist()
            if len(actual) != len(entries) or {e.filename for e in actual} != set(entries):
                raise ValueError("Archive files do not match trusted manifest")
            for entry in actual:
                cancel()
                name = safe_name(entry.filename)
                expected = entries[name]
                if entry.is_dir() or stat.S_ISLNK(entry.external_attr >> 16) or entry.flag_bits & 1:
                    raise ValueError("Directories, symlinks and encrypted entries are not allowed")
                if entry.file_size != expected["bytes"]:
                    raise ValueError(f"Entry size mismatch: {name}")
                target = content.joinpath(*name.split("/"))
                if content not in target.resolve().parents:
                    raise ValueError("Extraction path escapes staging root")
                target.parent.mkdir(parents=True, exist_ok=True)
                digest, count = hashlib.sha256(), 0
                with archive.open(entry) as input_stream, target.open("xb") as output:
                    while True:
                        cancel()
                        block = input_stream.read(CHUNK)
                        if not block:
                            break
                        count += len(block)
                        if count > expected["bytes"]:
                            raise ValueError(f"Decompressed entry exceeds trusted size: {name}")
                        digest.update(block)
                        output.write(block)
                if count != expected["bytes"] or digest.hexdigest() != expected["sha256"]:
                    raise ValueError(f"Entry SHA-256 mismatch: {name}")
        cancel()
        if destination.exists():
            raise FileExistsError("Restore destination was created during validation")
        content.rename(destination)
        return dict(status="PASS", version=version, destination=str(destination), files=len(entries), archiveSha256=trusted["archive"]["sha256"])
    finally:
        # Validate exact resolved owned target before recursive cleanup on Windows.
        if stage.parent != parent or not stage.name.startswith(".catalog-restore-"):
            raise RuntimeError("Invalid cleanup target")
        shutil.rmtree(stage)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    p = commands.add_parser("pack")
    p.add_argument("--data-root", required=True)
    p.add_argument("--archive", required=True)
    p.add_argument("--manifest", required=True)
    p.add_argument("--version", required=True)
    r = commands.add_parser("restore")
    r.add_argument("--source", required=True)
    r.add_argument("--manifest", required=True)
    r.add_argument("--destination", required=True)
    r.add_argument("--version", required=True)
    r.add_argument("--report")
    args = parser.parse_args()
    def interrupt(signum, frame):
        raise KeyboardInterrupt("Catalog bundle operation cancelled")
    signal.signal(signal.SIGTERM, interrupt)
    if args.command == "pack":
        result = pack(args.data_root, args.archive, args.manifest, args.version)
        print(json.dumps(dict(version=result["version"], files=len(result["files"]), archive=result["archive"])))
    else:
        try:
            result = restore(args.source, args.manifest, args.destination, args.version)
        except BaseException as error:
            if args.report:
                Path(args.report).parent.mkdir(parents=True, exist_ok=True)
                json_write(args.report, dict(status="CANCELLED" if isinstance(error,KeyboardInterrupt) else "FAIL", version=args.version, error=str(error)))
            raise
        if args.report:
            Path(args.report).parent.mkdir(parents=True, exist_ok=True)
            json_write(args.report, result)
        print(json.dumps(result))


if __name__ == "__main__":
    main()
