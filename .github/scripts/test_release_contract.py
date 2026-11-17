import hashlib
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
import zipfile

from release_contract import archive, required_assets, validate_assets


class ReleaseContractTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.version = '2026.1117.1-nova'
        for name in required_assets(self.version):
            (self.root / name).write_bytes(b'fixture')
        for channel, suffix in [('win', ''), ('win-arm64', '-win-arm64')]:
            name = f'torii-{self.version}{suffix}-full.nupkg'
            entry = {'Version': self.version, 'FileName': name, 'Size': 7, 'SHA256': hashlib.sha256(b'fixture').hexdigest()}
            (self.root / f'releases.{channel}.json').write_text(json.dumps({'Assets': [entry]}))
            (self.root / f'assets.{channel}.json').write_text(json.dumps([{'RelativeFileName': name}]))

    def test_complete_release(self):
        self.assertEqual(len(validate_assets(self.root, self.version)), 19)

    def test_each_platform_is_required(self):
        for name in required_assets(self.version):
            with self.subTest(name=name):
                contents = (self.root / name).read_bytes()
                (self.root / name).unlink()
                with self.assertRaises(ValueError):
                    validate_assets(self.root, self.version)
                (self.root / name).write_bytes(contents)

    def test_mixed_stream_is_rejected(self):
        (self.root / 'torii-2026.1117.1-torii-full.nupkg').write_bytes(b'old')
        with self.assertRaisesRegex(ValueError, 'Mixed streams'):
            validate_assets(self.root, self.version)

    def test_changed_package_is_rejected(self):
        (self.root / f'torii-{self.version}-full.nupkg').write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError, 'hash mismatch'):
            validate_assets(self.root, self.version)

    def test_missing_installer_reference_is_rejected(self):
        (self.root / 'assets.win.json').write_text('[{"RelativeFileName":"old-Setup.exe"}]')
        with self.assertRaisesRegex(ValueError, 'Asset index'):
            validate_assets(self.root, self.version)

    def test_unix_executable_permissions(self):
        source = self.root / 'source'
        source.mkdir()
        (source / 'torii').write_bytes(b'executable')
        (source / 'Install Torii.command').write_text('#!/bin/bash\n')
        (source / 'data.json').write_text('{}')
        archive(source, self.root / 'test.zip')
        with zipfile.ZipFile(self.root / 'test.zip') as packed:
            self.assertEqual((packed.getinfo('torii').external_attr >> 16) & 0o777, 0o755)
            self.assertEqual((packed.getinfo('Install Torii.command').external_attr >> 16) & 0o777, 0o755)
        archive(source, self.root / 'test.tar.gz')
        with tarfile.open(self.root / 'test.tar.gz') as packed:
            self.assertEqual(packed.getmember('torii').mode, 0o755)
            self.assertEqual(packed.getmember('data.json').mode, 0o644)


if __name__ == '__main__':
    unittest.main()
