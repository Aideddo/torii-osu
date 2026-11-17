import os
from pathlib import Path
import plistlib

app = Path(os.environ['TORII_BUNDLE_PATH'])
version = os.environ['TORII_BUNDLE_VERSION']
repo_root = Path(__file__).resolve().parents[2]
is_net10 = (
    os.environ.get('TORII_RUNTIME_MAJOR') == '10'
    or (repo_root / '.github' / 'satori-net10.json').is_file()
)
min_version = '12.0' if is_net10 else '14.0'
data = {
    'CFBundleName': 'Torii', 'CFBundleDisplayName': 'Torii',
    'CFBundleIdentifier': 'com.shikkesora.torii', 'CFBundleExecutable': 'torii',
    'CFBundleIconFile': 'torii.icns', 'CFBundlePackageType': 'APPL',
    'CFBundleInfoDictionaryVersion': '6.0', 'CFBundleShortVersionString': version,
    'CFBundleVersion': version, 'LSMinimumSystemVersion': min_version,
    'LSApplicationCategoryType': 'public.app-category.games',
    'NSHighResolutionCapable': True, 'NSSupportsAutomaticGraphicsSwitching': True,
    'CFBundleURLTypes': [{
        'CFBundleURLName': 'Torii game links',
        'CFBundleURLSchemes': ['osu', 'osump'],
    }],
}
with (app / 'Contents/Info.plist').open('wb') as stream:
    plistlib.dump(data, stream)
(app / 'Contents/PkgInfo').write_bytes(b'APPL????')
