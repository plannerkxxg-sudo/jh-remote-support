"""Fetch a signed release into this employee's repo; never change employee settings."""
import argparse
import io
import json
import os
from pathlib import Path
import stat
import tempfile
import zipfile
from release_common import FEED_URL, RUNTIME_FILES, download, verify_envelope, verify_asset, version


def unpack_verified(raw):
    result = {}
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        infos = archive.infolist()
        if len(infos) != len(RUNTIME_FILES):
            raise ValueError('Unexpected release file count')
        for info in infos:
            if (info.filename not in RUNTIME_FILES or info.filename in result or info.is_dir()
                    or stat.S_ISLNK(info.external_attr >> 16) or info.file_size > 1500000):
                raise ValueError('Unsafe release archive entry')
            result[info.filename] = archive.read(info)
        if sum(map(len, result.values())) > 10000000:
            raise ValueError('Release archive too large')
    return result


def install(root, envelope, archive, public_key):
    root = Path(root).resolve()
    release = verify_envelope(envelope, public_key)
    state_path = root/'updates/installed-release.json'
    if state_path.exists():
        current = json.loads(state_path.read_text())
        current_version = version(current['version'])
        if version(release['version']) < current_version:
            raise ValueError('Release rollback blocked')
        if version(release['version']) == current_version:
            if current.get('sha256') != release['files']['relay']['sha256']:
                raise ValueError('Existing version changed its payload')
            return False
    verify_asset(archive, release['files']['relay'])
    files = unpack_verified(archive)
    # The inner student EXE must match the separately signed counselor asset.
    verify_asset(files['relay/downloads/JHRemoteSupport.exe'], release['files']['client'])
    manifest_state = json.dumps({'version':release['version'],
        'sha256':release['files']['relay']['sha256'], 'clientSha256':release['files']['client']['sha256']}, indent=2).encode()+b'\n'
    files['updates/installed-release.json'] = manifest_state
    previous = {}
    try:
        for name, content in files.items():
            target = root/name
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.is_symlink() or any(parent.is_symlink() for parent in target.parents if parent != root.parent):
                raise ValueError('Symlink target blocked')
            previous[name] = target.read_bytes() if target.exists() else None
            with tempfile.NamedTemporaryFile(dir=target.parent, delete=False) as temp:
                temp.write(content)
                temp_name = temp.name
            os.replace(temp_name, target)
    except Exception:
        for name, content in previous.items():
            if content is None:
                (root/name).unlink(missing_ok=True)
            else:
                (root/name).write_bytes(content)
        raise
    return True


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', default=str(Path(__file__).resolve().parents[1]))
    args = parser.parse_args()
    root = Path(args.root).resolve()
    envelope = download(FEED_URL, 65536)
    release = verify_envelope(envelope, root/'updates/publisher-public.pem')
    state = root/'updates/installed-release.json'
    if state.exists():
        current = json.loads(state.read_text())
        if version(release['version']) < version(current['version']):
            raise ValueError('Release rollback blocked')
        if release['version'] == current['version']:
            if current['sha256'] != release['files']['relay']['sha256']:
                raise ValueError('Existing version payload changed')
            print('Already current')
            return
    archive = download(release['files']['relay']['url'], release['files']['relay']['size'])
    changed = install(root, envelope, archive, root/'updates/publisher-public.pem')
    print('Verified update applied' if changed else 'Already current')

if __name__ == '__main__':
    main()
