"""Signed release format. Public downloads only; OpenSSL verifies RSA/SHA-256."""
import base64
import hashlib
import json
from pathlib import Path
import re
import ssl
import subprocess
import tempfile
from urllib.parse import urlsplit
from urllib.request import Request, build_opener, HTTPSHandler, HTTPRedirectHandler

REPOSITORY = 'plannerkxxg-sudo/jh-remote-support'
FEED_URL = f'https://github.com/{REPOSITORY}/releases/latest/download/update-manifest.json'
RUNTIME_FILES = (
    'relay/server.py', 'relay/webapp.py', 'relay/branding.py', 'relay/requirements.txt',
    'relay/Dockerfile', 'relay/release-version.json', 'relay/web/index.html', 'relay/web/app.js', 'relay/web/style.css',
    'relay/downloads/JHRemoteSupport.exe', 'relay/downloads/JHRemoteSupport.sha256.txt',
)


def version(value):
    if not isinstance(value, str) or not re.fullmatch(r'(0|[1-9][0-9]{0,4})(\.(0|[1-9][0-9]{0,4})){3}', value):
        raise ValueError('Invalid release version')
    return tuple(map(int, value.split('.')))


def asset_url(url, filename):
    if not isinstance(url, str):
        raise ValueError('Invalid asset URL')
    u = urlsplit(url)
    prefix = f'/{REPOSITORY}/releases/download/'
    if (u.scheme != 'https' or u.netloc != 'github.com' or u.query or u.fragment
            or not u.path.startswith(prefix)):
        raise ValueError('Asset must belong to the trusted release repository')
    rest = u.path[len(prefix):].split('/')
    if len(rest) != 2 or not re.fullmatch(r'[A-Za-z0-9._-]{1,80}', rest[0]) or rest[1] != filename:
        raise ValueError('Invalid release asset path')
    return url


def no_duplicates(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON key')
        result[key] = value
    return result


def verify_envelope(raw, public_key):
    if len(raw) > 65536:
        raise ValueError('Manifest too large')
    envelope = json.loads(raw, object_pairs_hook=no_duplicates)
    payload = base64.b64decode(envelope['payload'], validate=True)
    signature = base64.b64decode(envelope['signature'], validate=True)
    if len(payload) > 16384 or len(signature) != 384:
        raise ValueError('Invalid signed manifest size')
    with tempfile.TemporaryDirectory() as temp:
        temp = Path(temp)
        (temp/'payload').write_bytes(payload)
        (temp/'signature').write_bytes(signature)
        result = subprocess.run(['openssl', 'dgst', '-sha256', '-verify', str(public_key),
            '-signature', str(temp/'signature'), str(temp/'payload')], capture_output=True)
        if result.returncode:
            raise ValueError('Update signature verification failed')
    data = json.loads(payload, object_pairs_hook=no_duplicates)
    if data.get('schema') != 1 or data.get('product') != 'JHRemoteSupport':
        raise ValueError('Wrong release product/schema')
    version(data['version'])
    if not isinstance(data.get('files'), dict) or set(data['files']) != {'client', 'relay'}:
        raise ValueError('Missing release assets')
    for key, filename, maximum in [('client','JHRemoteSupport.exe',1500000), ('relay','relay-update.zip',10000000)]:
        entry = data['files'][key]
        asset_url(entry['url'], filename)
        if (type(entry['size']) is not int or not 1024 <= entry['size'] <= maximum
                or not re.fullmatch(r'[a-f0-9]{64}', entry['sha256'])):
            raise ValueError('Invalid asset size/hash')
    return data


class SecureRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if urlsplit(newurl).scheme != 'https':
            raise ValueError('HTTPS downgrade blocked')
        return super().redirect_request(req, fp, code, msg, headers, newurl)


def download(url, maximum):
    if urlsplit(url).scheme != 'https':
        raise ValueError('HTTPS required')
    context = ssl.create_default_context()
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    opener = build_opener(HTTPSHandler(context=context), SecureRedirect())
    request = Request(url, headers={'User-Agent':'JHRemoteSupport-Updater/0.10'})
    with opener.open(request, timeout=30) as response:
        body = bytearray()
        while True:
            chunk = response.read(min(65536, maximum + 1 - len(body)))
            if not chunk:
                break
            body.extend(chunk)
            if len(body) > maximum:
                raise ValueError('Download exceeds size limit')
        return bytes(body)


def verify_asset(data, entry):
    if len(data) != entry['size'] or hashlib.sha256(data).hexdigest() != entry['sha256']:
        raise ValueError('Release file hash/size mismatch')
