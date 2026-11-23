"""Package existing Linux publishes in an isolated Linux container."""
import hashlib
from pathlib import Path
import subprocess
import sys
from urllib.request import urlopen

IMAGE = 'buildpack-deps@sha256:f53cd4c58cdb96a467b98696cd510a949ef4ccd7d23e07b75107de754773007a'
TOOLS = {
    'appimagetool-x86_64.AppImage': ('AppImage/appimagetool', 'e8c1169c4af66c3184d7ee110713d4be04befe671fa358e2c1591ee840553b03'),
    'runtime-x86_64': ('AppImage/type2-runtime', '617aa77314d57c8b8e4fae9c3f0b35af7b30d5529cb69e98f64f92510ed98a46'),
    'runtime-aarch64': ('AppImage/type2-runtime', '9a72a744d221576c9347ec074243acc5c3c7568896b12714e0e82e9f0bf47900'),
}


def prepare_tools(cache):
    cache.mkdir(parents=True, exist_ok=True)
    for name, (repo, expected) in TOOLS.items():
        path = cache / name
        if not path.exists():
            with urlopen(f'https://github.com/{repo}/releases/download/continuous/{name}', timeout=120) as response:
                data = response.read()
            if hashlib.sha256(data).hexdigest() != expected:
                raise ValueError('Tool changed upstream; review new pin: ' + name)
            path.write_bytes(data)
        with path.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != expected:
                raise ValueError('AppImage tool checksum mismatch: ' + name)


def build(root, cache):
    prepare_tools(cache)
    script = '''set -eu
cd /tmp
cp /tools/appimagetool-x86_64.AppImage ./tool
chmod +x ./tool
./tool --appimage-extract >/dev/null
for pair in x64:x86_64 arm64:aarch64; do
  rid=${pair%%:*}; arch=${pair##*:}
  dir=/tmp/Torii-$rid.AppDir
  mkdir -p "$dir/usr/bin"
  cp -a /client/publish/linux-$rid/. "$dir/usr/bin/"
  chmod +x "$dir/usr/bin/torii"
  cp /client/assets/torii-512.png "$dir/torii.png"
  printf '%s\n' '#!/bin/sh' 'HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)' 'exec "$HERE/usr/bin/torii" "$@"' > "$dir/AppRun"
  chmod +x "$dir/AppRun"
  printf '%s\n' '[Desktop Entry]' 'Type=Application' 'Name=Torii' 'Exec=torii %U' 'Icon=torii' 'Categories=Game;' 'Terminal=false' 'MimeType=x-scheme-handler/osu;x-scheme-handler/osump;' > "$dir/torii.desktop"
  ARCH=$arch ./squashfs-root/AppRun --runtime-file /tools/runtime-$arch "$dir" /client/Releases/Torii-linux-$rid.AppImage
done
'''
    subprocess.run(['docker', 'run', '--rm', '--mount', f'type=bind,source={root},target=/client',
                    '--mount', f'type=bind,source={cache},target=/tools,readonly',
                    IMAGE, 'bash', '-c', script], check=True)
    for arch, machine in [('x64', 62), ('arm64', 183)]:
        path = root / 'Releases' / f'Torii-linux-{arch}.AppImage'
        with path.open('rb') as stream:
            header = stream.read(64)
        if header[:4] != b'\x7fELF' or header[8:11] != b'AI\x02' or int.from_bytes(header[18:20], 'little') != machine:
            raise ValueError('Invalid AppImage runtime: ' + arch)
    print('Both AppImage architectures packaged and runtime headers verified.')


if __name__ == '__main__':
    build(Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve())
