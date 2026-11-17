import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import runpy
import sys
import tempfile
import unittest
from unittest.mock import patch


class PublishVerifiedReleaseTests(unittest.TestCase):
    def execute(self, mutate=None):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            payload = b'package'
            (root / 'test.zip').write_bytes(payload)
            release = {'id': 1, 'draft': True, 'tag_name': 'v2026.1117.1-nova', 'assets': [{
                'name': 'test.zip', 'size': len(payload),
                'digest': 'sha256:' + hashlib.sha256(payload).hexdigest(),
            }]}
            if mutate:
                mutate(release)
            calls = []

            def respond(request, timeout):
                calls.append(request)
                data = [release] if request.method == 'GET' else {'html_url': 'release-url'}
                return io.BytesIO(json.dumps(data).encode())

            with patch.object(sys, 'argv', ['publish', str(root), '2026.1117.1-nova']), \
                    patch.dict(os.environ, TORII_RELEASE_TOKEN='fixture'), \
                    patch('release_contract.validate_assets'), \
                    patch('urllib.request.urlopen', side_effect=respond), \
                    contextlib.redirect_stdout(io.StringIO()):
                try:
                    runpy.run_path(str(Path(__file__).with_name('publish_verified_release.py')))
                finally:
                    self.calls = calls

    def test_valid_draft_is_published_as_prerelease(self):
        self.execute()
        self.assertEqual([call.method for call in self.calls], ['GET', 'PATCH'])
        payload = json.loads(self.calls[-1].data)
        self.assertFalse(payload['draft'])
        self.assertTrue(payload['prerelease'])
        self.assertEqual(payload['make_latest'], 'false')

    def test_incomplete_upload_stays_draft(self):
        with self.assertRaisesRegex(ValueError, 'assets do not match'):
            self.execute(lambda release: release.update(assets=[]))
        self.assertEqual(len(self.calls), 1)

    def test_wrong_hash_stays_draft(self):
        with self.assertRaisesRegex(ValueError, 'hash mismatch'):
            self.execute(lambda release: release['assets'][0].update(digest='wrong'))
        self.assertEqual(len(self.calls), 1)

    def test_wrong_size_stays_draft(self):
        with self.assertRaisesRegex(ValueError, 'size mismatch'):
            self.execute(lambda release: release['assets'][0].update(size=100))
        self.assertEqual(len(self.calls), 1)
