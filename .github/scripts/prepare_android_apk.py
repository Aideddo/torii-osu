from pathlib import Path
import struct
import sys
import zipfile

source, libraries, output = map(Path, sys.argv[1:])
native = {p.name: p for p in libraries.glob('*.so')}
if not {'libbass.so', 'libbassmix.so', 'libbass_fx.so'} <= native.keys():
    raise ValueError('Missing bionic audio libraries')
for name, path in native.items():
    header = path.read_bytes()[:64]
    if header[:6] != b'\x7fELF\x02\x01' or struct.unpack_from('<H', header, 18)[0] != 183:
        raise ValueError('Not an Android ARM64 library: ' + name)
with zipfile.ZipFile(source) as original, zipfile.ZipFile(output, 'w') as target:
    copied = set()
    for item in original.infolist():
        if item.filename.upper().startswith('META-INF/'):
            continue
        path = Path(item.filename)
        if item.filename.startswith('lib/arm64-v8a/') and path.name in native:
            data = native[path.name].read_bytes()
            copied.add(path.name)
        else:
            data = original.read(item)
        if item.filename == 'resources.arsc' or item.filename.endswith('.so'):
            item.compress_type = zipfile.ZIP_STORED
        target.writestr(item, data)
    for name in native.keys() - copied:
        target.writestr('lib/arm64-v8a/' + name, native[name].read_bytes(), compress_type=zipfile.ZIP_STORED)
print('APK prepared with bionic ARM64 libraries; signing is required next.')
