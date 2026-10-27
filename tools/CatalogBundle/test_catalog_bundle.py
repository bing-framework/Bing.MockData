import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import zipfile
from catalog_bundle import pack, restore, json_write, digest_file


class BundleTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="catalog-bundle-test-")
        self.root = Path(self.temp.name)
        self.data = self.root / "source"
        self.data.mkdir()
        (self.data / "data.json").write_text('{"版本":1}', encoding="utf-8")
        (self.data / "notices").mkdir()
        (self.data / "notices/license.txt").write_text("fixture", encoding="utf-8")
        self.archive, self.manifest, self.dest = (self.root / n for n in ("data.zip", "lock.json", "restored"))
        self.lock = pack(self.data, self.archive, self.manifest, "fixture-v1")

    def tearDown(self):
        self.temp.cleanup()

    def run_restore(self, cancel=lambda: None):
        return restore(str(self.archive), self.manifest, self.dest, "fixture-v1", cancel)

    def assert_clean(self):
        self.assertFalse(self.dest.exists())
        self.assertEqual([], list(self.root.glob(".catalog-restore-*")))

    def test_roundtrip_and_existing_destination_preserved(self):
        result = self.run_restore()
        self.assertEqual("PASS", result["status"])
        for source in self.data.rglob("*"):
            if source.is_file():
                self.assertEqual(source.read_bytes(), (self.dest / source.relative_to(self.data)).read_bytes())
        with self.assertRaises(FileExistsError):
            self.run_restore()
        self.assertTrue((self.dest / "data.json").exists())

    def test_pack_rejects_empty_root_and_colliding_outputs_before_writing(self):
        archive, manifest = self.root/"new.zip", self.root/"new.json"
        for root in ("", "   "):
            with self.subTest(root=root), self.assertRaisesRegex(ValueError,"root.*empty"):
                pack(root,archive,manifest,"v1")
        with self.assertRaisesRegex(ValueError,"paths must differ"):
            pack(self.data,archive,archive,"v1")
        self.assertFalse(archive.exists())
        self.assertFalse(manifest.exists())
        self.assertFalse(archive.with_name(archive.name+".partial").exists())

    def test_corruption_and_missing_archive(self):
        self.archive.write_bytes(self.archive.read_bytes() + b"broken")
        with self.assertRaisesRegex(ValueError, "trusted size"):
            self.run_restore()
        self.assert_clean()
        self.archive.unlink()
        with self.assertRaises(FileNotFoundError):
            self.run_restore()
        self.assert_clean()

    def test_wrong_version_missing_entry_and_file_hash(self):
        with self.assertRaisesRegex(ValueError, "version"):
            restore(str(self.archive), self.manifest, self.dest, "wrong")
        self.lock["files"][0]["sha256"] = "0" * 64
        json_write(self.manifest, self.lock)
        with self.assertRaisesRegex(ValueError, "SHA-256"):
            self.run_restore()
        self.assert_clean()
        self.lock["files"].pop()
        json_write(self.manifest, self.lock)
        with self.assertRaisesRegex(ValueError, "files do not match"):
            self.run_restore()
        self.assert_clean()

    def test_portable_path_traversal_and_symlinks(self):
        for name in ("../escaped", "/absolute", "C:/file", "a/../../b", "a\\b", "x:stream", "nul.txt", "a./file"):
            with self.subTest(name=name):
                self.lock["files"][0]["path"] = name
                json_write(self.manifest, self.lock)
                with self.assertRaisesRegex(ValueError, "Unsafe"):
                    self.run_restore()
                self.assert_clean()
        name = "link"
        info = zipfile.ZipInfo(name)
        info.external_attr = 0o120777 << 16
        with zipfile.ZipFile(self.archive, "w") as z:
            z.writestr(info, "outside")
        size, sha = digest_file(self.archive)
        self.lock["archive"].update(bytes=size, sha256=sha)
        self.lock["files"] = [dict(path=name, bytes=7, sha256=hashlib.sha256(b"outside").hexdigest())]
        json_write(self.manifest, self.lock)
        with self.assertRaisesRegex(ValueError, "symlinks"):
            self.run_restore()
        self.assert_clean()

    def test_cancellation_at_copy_and_extraction_cleans_staging(self):
        for when in (1, 3, 6, 8):
            calls = 0
            def cancel():
                nonlocal calls
                calls += 1
                if calls == when:
                    raise InterruptedError("cancelled")
            with self.subTest(when=when), self.assertRaises(InterruptedError):
                self.run_restore(cancel)
            self.assert_clean()

    def test_manifest_case_collision_and_https_only(self):
        self.lock["files"].append(dict(self.lock["files"][0], path="DATA.JSON"))
        json_write(self.manifest, self.lock)
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            self.run_restore()
        self.lock["files"].pop()
        json_write(self.manifest, self.lock)
        with self.assertRaisesRegex(ValueError, "HTTPS"):
            restore("http://example.com/data.zip", self.manifest, self.dest, "fixture-v1")
        self.assert_clean()


if __name__ == "__main__":
    unittest.main()
