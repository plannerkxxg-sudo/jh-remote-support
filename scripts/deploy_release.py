"""Deploy only pending verified updates; defer while a support room is open."""
import argparse
import json
import os
from pathlib import Path
import time
from urllib.parse import urlsplit
from urllib.request import Request, build_opener, HTTPRedirectHandler
from release_common import download


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        raise ValueError('Deploy hook redirect blocked')


def status(site):
    parsed = urlsplit(site)
    if parsed.scheme != 'https' or parsed.username or parsed.password or parsed.query or parsed.fragment or parsed.path not in ('','/'):
        raise ValueError('SUPPORT_SITE_URL must be your HTTPS site origin')
    return json.loads(download(site.rstrip('/')+'/api/status', 65536))


def deploy(root, site, hook, force=False, read_status=status, sleep=time.sleep):
    root=Path(root)
    state=json.loads((root/'updates/installed-release.json').read_text())
    expected=state['clientSha256']
    confirmed=root/'updates/deploy-confirmed.json'
    if not force and confirmed.exists() and json.loads(confirmed.read_text()).get('clientSha256') == expected and json.loads(confirmed.read_text()).get('version') == state['version']:
        return 'already-confirmed'
    current=read_status(site)
    if current.get('sha256') == expected and current.get('downloadReady') is True and current.get('deployedVersion') == state['version']:
        confirmed.write_text(json.dumps({'clientSha256':expected,'version':state['version']},indent=2)+'\n')
        return 'confirmed'
    busy=current.get('busySessions')
    if type(busy) is not int or busy < 0:
        raise ValueError('Install v0.10 manually once before enabling automatic deployments')
    if busy:
        return 'deferred-support-in-progress'
    parsed=urlsplit(hook)
    if parsed.scheme != 'https' or parsed.netloc != 'api.render.com' or not parsed.path.startswith('/deploy/srv-') or parsed.fragment:
        raise ValueError('Invalid Render deploy hook')
    # Never print this URL or server response. The query contains a secret.
    request=Request(hook, data=b'', method='POST')
    try:
        with build_opener(NoRedirect()).open(request,timeout=30) as response:
            if response.status not in (200,201,202):
                raise ValueError('Deploy request rejected')
    except Exception:
        raise RuntimeError('Render deploy request failed; inspect the service dashboard') from None
    for _ in range(48):
        sleep(5)
        try:
            current=read_status(site)
            if current.get('sha256') == expected and current.get('downloadReady') is True and current.get('deployedVersion') == state['version']:
                confirmed.write_text(json.dumps({'clientSha256':expected,'version':state['version']},indent=2)+'\n')
                return 'confirmed'
        except Exception:
            pass
    raise RuntimeError('Deployment was requested but the new EXE hash is not live yet; next run retries')

if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--force',action='store_true')
    args=parser.parse_args()
    site=os.getenv('SUPPORT_SITE_URL','')
    hook=os.getenv('RENDER_DEPLOY_HOOK_URL','')
    if not site or not hook:
        raise SystemExit('Set SUPPORT_SITE_URL variable and RENDER_DEPLOY_HOOK_URL secret once')
    print(deploy(Path(__file__).resolve().parents[1],site,hook,args.force))
