import argparse
import hashlib
import json
from pathlib import Path
import re
import tarfile
import zipfile


DESKTOP_RIDS = ('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')


def required_assets(version):
    if not re.fullmatch(r'\d{4}\.\d+\.\d+-(nova|torii)', version):
        raise ValueError('Use a complete Nova or Torii release version')
    return {
        'install-win-x64.exe', 'install-win-arm64.exe',
        'portable-win-x64.zip', 'portable-win-arm64.zip',
        'portable-linux-x64.tar.gz', 'portable-linux-arm64.tar.gz',
        'portable-osx-x64.zip', 'portable-osx-arm64.zip',
        'Torii-macOS-Intel.zip', 'Torii-macOS-AppleSilicon.zip', 'torii.apk',
        'assets.win.json', 'assets.win-arm64.json',
        'releases.win.json', 'releases.win-arm64.json', 'RELEASES', 'RELEASES-win-arm64',
        f'torii-{version}-full.nupkg', f'torii-{version}-win-arm64-full.nupkg',
    }


def validate_assets(directory, version):
    directory = Path(directory)
    required = required_assets(version)
    missing = sorted(name for name in required if not (directory / name).is_file() or not (directory / name).stat().st_size)
    if missing:
        raise ValueError('Missing release assets: ' + ', '.join(missing))
    packages = {p.name for p in directory.glob('*.nupkg')}
    expected_packages = {f'torii-{version}-full.nupkg', f'torii-{version}-win-arm64-full.nupkg'}
    if packages != expected_packages:
        raise ValueError('Mixed streams or stale packages in release staging')
    for channel in ('win', 'win-arm64'):
        feed = json.loads((directory / f'releases.{channel}.json').read_text(encoding='utf-8-sig'))
        entries = feed.get('Assets', [])
        if len(entries) != 1 or entries[0].get('Version') != version:
            raise ValueError(f'Incorrect version or package count in {channel} feed')
        entry = entries[0]
        package = directory / entry['FileName']
        if package.name not in expected_packages or not package.is_file():
            raise ValueError('Updater feed references a missing package')
        if package.stat().st_size != entry['Size']:
            raise ValueError('Updater package size mismatch')
        with package.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest().lower() != entry['SHA256'].lower():
                raise ValueError('Updater package hash mismatch')
        index = json.loads((directory / f'assets.{channel}.json').read_text(encoding='utf-8-sig'))
        for asset in index:
            if Path(asset['RelativeFileName']).name != asset['RelativeFileName'] or not (directory / asset['RelativeFileName']).is_file():
                raise ValueError('Asset index references a missing or invalid filename')
    return sorted(required)


def executable(relative):
    return relative.name == 'torii' or relative.suffix in ('.command', '.sh', '.dylib', '.so')


def archive(source, output):
    source, output = Path(source), Path(output)
    if output.name.endswith('.tar.gz'):
        with tarfile.open(output, 'w:gz') as target:
            for path in sorted(source.rglob('*')):
                info = target.gettarinfo(str(path), str(path.relative_to(source)).replace('\\', '/'))
                info.uid = info.gid = 0
                info.uname = info.gname = ''
                info.mode = 0o755 if path.is_dir() or executable(path) else 0o644
                if path.is_file():
                    with path.open('rb') as stream:
                        target.addfile(info, stream)
                else:
                    target.addfile(info)
    else:
        with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as target:
            for path in sorted(source.rglob('*')):
                if not path.is_file():
                    continue
                info = zipfile.ZipInfo(path.relative_to(source).as_posix())
                info.create_system = 3
                info.external_attr = (0o100755 if executable(path) else 0o100644) << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                target.writestr(info, path.read_bytes())


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('operation', choices=('validate', 'archive'))
    parser.add_argument('directory', type=Path)
    parser.add_argument('value')
    args = parser.parse_args()
    if args.operation == 'validate':
        print('\n'.join(validate_assets(args.directory, args.value)))
    else:
        archive(args.directory, args.value)
