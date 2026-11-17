import os
from pathlib import Path
import plistlib

app = Path(os.environ['TORII_BUNDLE_PATH'])
version = os.environ['TORII_BUNDLE_VERSION']
data = {
    'CFBundleName': 'Torii', 'CFBundleDisplayName': 'Torii',
    'CFBundleIdentifier': 'com.shikkesora.torii', 'CFBundleExecutable': 'torii',
    'CFBundleIconFile': 'torii.icns', 'CFBundlePackageType': 'APPL',
    'CFBundleInfoDictionaryVersion': '6.0', 'CFBundleShortVersionString': version,
    'CFBundleVersion': version, 'LSMinimumSystemVersion': '14.0',
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
