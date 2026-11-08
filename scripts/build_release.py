"""Prepare immutable assets; signing happens only in the publisher workflow."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import shutil
import zipfile
from release_common import REPOSITORY, RUNTIME_FILES, version


def build(root, release_version, out):
    version(release_version)
    root, out = Path(root), Path(out)
    out.mkdir(parents=True, exist_ok=True)
    exe = root/'relay/downloads/JHRemoteSupport.exe'
    binary = exe.read_bytes()
    if not 1024 <= len(binary) <= 1500000 or binary[:2] != b'MZ':
        raise ValueError('Missing real release EXE')
    digest = hashlib.sha256(binary).hexdigest()
    expected = (root/'relay/downloads/JHRemoteSupport.sha256.txt').read_text().strip()
    if digest != expected:
        raise ValueError('EXE SHA256 file does not match')
    shutil.copyfile(exe, out/'JHRemoteSupport.exe')
    (root/'relay/release-version.json').write_text(json.dumps({'version':release_version})+'\n')
    with zipfile.ZipFile(out/'relay-update.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for name in RUNTIME_FILES:
            archive.write(root/name, name)
    files = {}
    tag = 'v' + release_version
    for key, name in [('client','JHRemoteSupport.exe'),('relay','relay-update.zip')]:
        data = (out/name).read_bytes()
        files[key] = {'url':f'https://github.com/{REPOSITORY}/releases/download/{tag}/{name}',
            'size':len(data), 'sha256':hashlib.sha256(data).hexdigest()}
    payload = {'schema':1,'product':'JHRemoteSupport','version':release_version,
        'publishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(), 'files':files}
    (out/'update-payload.json').write_text(json.dumps(payload,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    return payload

if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--version', required=True)
    parser.add_argument('--out', default='release-out')
    args=parser.parse_args()
    build(Path(__file__).resolve().parents[1],args.version,args.out)
