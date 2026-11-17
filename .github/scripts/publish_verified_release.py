import hashlib
import json
import os
from pathlib import Path
import sys
from urllib.request import Request, urlopen

from release_contract import validate_assets


directory, version = Path(sys.argv[1]), sys.argv[2]
validate_assets(directory, version)
base = 'https://api.github.com/repos/ShikkesoraSIM/torii'
headers = {
    'Authorization': 'Bearer ' + os.environ['TORII_RELEASE_TOKEN'],
    'Accept': 'application/vnd.github+json',
    'Content-Type': 'application/json',
    'User-Agent': 'Torii-release-verification',
}


def request(path, payload=None):
    body = json.dumps(payload).encode() if payload is not None else None
    req = Request(base + path, data=body, headers=headers, method='PATCH' if body else 'GET')
    with urlopen(req, timeout=60) as response:
        return json.load(response)


release = None
page = 1
while release is None:
    releases = request(f'/releases?per_page=100&page={page}')
    release = next((item for item in releases if item['tag_name'] == 'v' + version), None)
    if len(releases) < 100:
        break
    page += 1
if release is None:
    raise ValueError('Release draft not found')
if not release['draft']:
    raise ValueError('Expected an unpublished draft release')
uploaded = {asset['name']: asset for asset in release['assets']}
expected = {file.name: file for file in directory.iterdir() if file.is_file()}
if set(uploaded) != set(expected):
    raise ValueError('Uploaded release assets do not match validated staging')
for name, file in expected.items():
    if uploaded[name]['size'] != file.stat().st_size:
        raise ValueError('Uploaded size mismatch: ' + name)
    with file.open('rb') as stream:
        digest = 'sha256:' + hashlib.file_digest(stream, 'sha256').hexdigest()
    if uploaded[name].get('digest') and uploaded[name]['digest'] != digest:
        raise ValueError('Uploaded hash mismatch: ' + name)
published = request('/releases/' + str(release['id']), {
    'draft': False, 'prerelease': version.endswith('-nova'),
    'make_latest': 'false' if version.endswith('-nova') else 'true',
})
print(published['html_url'])
