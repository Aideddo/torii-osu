import hashlib
import os
from pathlib import Path
import re
import subprocess
import sys
import zipfile

apk, keystore, expected_code = sys.argv[1:]
tools = sorted((Path(os.environ['ANDROID_SDK_ROOT']) / 'build-tools').iterdir())[-1]
signer = subprocess.run([str(tools / 'apksigner.bat'), 'verify', '--print-certs', apk], capture_output=True, text=True, check=True)
match = re.search(r'Signer #1 certificate SHA-256 digest: ([0-9a-fA-F]+)', signer.stdout)
certificate = subprocess.run([
    str(Path(os.environ['JAVA_HOME']) / 'bin/keytool.exe'), '-exportcert',
    '-keystore', keystore, '-storepass:env', 'ANDROID_KEYSTORE_PASSWORD',
    '-alias', os.environ['ANDROID_KEY_ALIAS'],
], capture_output=True, check=True).stdout
if not match or match[1].lower() != hashlib.sha256(certificate).hexdigest():
    raise ValueError('APK does not use the original signing certificate')
badging = subprocess.run([str(tools / 'aapt.exe'), 'dump', 'badging', apk], capture_output=True, text=True, check=True).stdout
package = re.search(r"package: name='([^']+)' versionCode='([^']+)'", badging)
if not package or package[1] != 'sh.shikkesora.torii' or package[2] != expected_code:
    raise ValueError('Android package identity/version code mismatch')
with zipfile.ZipFile(apk) as archive:
    for name in ('libbass.so', 'libbassmix.so', 'libbass_fx.so'):
        if b'libc.so.6' in archive.read('lib/arm64-v8a/' + name):
            raise ValueError('Desktop glibc audio library shipped inside Android APK')
print('Original certificate, application ID, version code and bionic audio verified.')
