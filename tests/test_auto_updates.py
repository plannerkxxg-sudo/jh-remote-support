"""Signed fixtures are public. Publisher private keys are never needed by employees/tests."""
import base64
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch, MagicMock
import zipfile
import warnings

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'scripts'))
import release_common as common
import sync_release
import deploy_release
FIXTURES=ROOT/'tests/fixtures'
UPDATE_COMMAND=json.loads(os.environ.get('JH_UPDATE_COMMAND','[]'))


@unittest.skipUnless(shutil.which('openssl'), 'OpenSSL command is required for server update verification')
class SignedUpdateTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.root=Path(self.temp.name)
        self.envelope=(FIXTURES/'update-manifest.json').read_bytes()
        self.archive=(FIXTURES/'relay-update.zip').read_bytes()
        self.key=ROOT/'updates/publisher-public.pem'
    def tearDown(self): self.temp.cleanup()

    def test_valid_signature_and_exact_assets(self):
        release=common.verify_envelope(self.envelope,self.key)
        common.verify_asset(self.archive,release['files']['relay'])
        common.verify_asset((FIXTURES/'JHRemoteSupport.exe').read_bytes(),release['files']['client'])

    def test_modified_payload_and_signature_rejected(self):
        for name in ['payload','signature']:
            value=json.loads(self.envelope)
            data=bytearray(base64.b64decode(value[name]));data[len(data)//2]^=1
            value[name]=base64.b64encode(data).decode()
            with self.assertRaises(ValueError): common.verify_envelope(json.dumps(value).encode(),self.key)

    def test_missing_huge_duplicate_fields_rejected(self):
        value=json.loads(self.envelope)
        with self.assertRaises(ValueError): common.verify_envelope(b'x'*65537,self.key)
        duplicate='{"payload":"bad","payload":'+json.dumps(value['payload'])+',"signature":'+json.dumps(value['signature'])+'}'
        with self.assertRaises(ValueError): common.verify_envelope(duplicate.encode(),self.key)

    def test_url_and_version_limits(self):
        for value in ['0.10.0.0','1.2.3.4']: common.version(value)
        for value in ['1.2.3','01.2.3.4','-1.2.3.4','1.2.3.4.exe','999999.1.1.1']:
            with self.assertRaises(ValueError):common.version(value)
        prefix='https://github.com/'+common.REPOSITORY+'/releases/download/v0.10.0.0/'
        common.asset_url(prefix+'JHRemoteSupport.exe','JHRemoteSupport.exe')
        for url in [prefix.replace('https:','http:')+'JHRemoteSupport.exe',
                    prefix.replace('github.com','github.com.evil.invalid')+'JHRemoteSupport.exe',
                    prefix+'JHRemoteSupport.exe?other=1',prefix+'nested/JHRemoteSupport.exe',
                    prefix.replace(common.REPOSITORY,'other/repo')+'JHRemoteSupport.exe']:
            with self.assertRaises(ValueError):common.asset_url(url,'JHRemoteSupport.exe')

    def test_install_preserves_employee_settings_and_rollbacks_rejected(self):
        (self.root/'relay').mkdir()
        (self.root/'relay/.env').write_text('SUPPORT_NAME=아진쌤\n')
        (self.root/'settings.json').write_text('민주쌤 사용자 설정')
        self.assertTrue(sync_release.install(self.root,self.envelope,self.archive,self.key))
        self.assertEqual((self.root/'relay/.env').read_text(),'SUPPORT_NAME=아진쌤\n')
        self.assertEqual((self.root/'settings.json').read_text(),'민주쌤 사용자 설정')
        self.assertFalse(sync_release.install(self.root,self.envelope,self.archive,self.key))
        state=self.root/'updates/installed-release.json'
        value=json.loads(state.read_text());value['version']='0.11.0.0';state.write_text(json.dumps(value))
        with self.assertRaises(ValueError):sync_release.install(self.root,self.envelope,self.archive,self.key)

    def test_tampered_download_keeps_old_files(self):
        (self.root/'relay').mkdir();old=self.root/'relay/server.py';old.write_text('old')
        data=bytearray(self.archive);data[len(data)//2]^=1
        with self.assertRaises(ValueError):sync_release.install(self.root,self.envelope,bytes(data),self.key)
        self.assertEqual(old.read_text(),'old')
        self.assertFalse((self.root/'updates/installed-release.json').exists())

    def test_archive_traversal_symlink_duplicate_and_extra_files_rejected(self):
        for kind in ['traversal','symlink','duplicate','extra']:
            output=io.BytesIO()
            with warnings.catch_warnings(), zipfile.ZipFile(io.BytesIO(self.archive)) as source, zipfile.ZipFile(output,'w') as target:
                warnings.simplefilter('ignore', UserWarning)
                for index,name in enumerate(common.RUNTIME_FILES):
                    if kind=='traversal' and index==0:
                        target.writestr('../outside.py',source.read(name))
                    elif kind=='symlink' and index==0:
                        item=zipfile.ZipInfo(name);item.external_attr=0o120777<<16
                        target.writestr(item,'outside')
                    elif kind=='duplicate' and index==0:
                        target.writestr(common.RUNTIME_FILES[1],source.read(name))
                    else:target.writestr(name,source.read(name))
                if kind=='extra':target.writestr('relay/.env','replace staff environment')
            with self.assertRaises(ValueError):sync_release.unpack_verified(output.getvalue())

    def test_replace_failure_restores_old_server(self):
        (self.root/'relay').mkdir();old=self.root/'relay/server.py';old.write_text('old')
        replace=os.replace
        count=0
        def fail_second(src,dst):
            nonlocal count
            count+=1
            if count==2:raise OSError('simulated disk failure')
            return replace(src,dst)
        with patch('sync_release.os.replace',side_effect=fail_second):
            with self.assertRaises(OSError):sync_release.install(self.root,self.envelope,self.archive,self.key)
        self.assertEqual(old.read_text(),'old')
        self.assertFalse((self.root/'updates/installed-release.json').exists())


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        (self.root/'updates').mkdir()
        (self.root/'updates/installed-release.json').write_text(json.dumps({'version':'0.10.0.0','clientSha256':'a'*64}))
        self.hook='https://api.render.com/deploy/srv-test?key=not-a-real-secret'
    def tearDown(self):self.temp.cleanup()

    def test_busy_support_defers_without_calling_hook(self):
        with patch('deploy_release.build_opener') as opener:
            result=deploy_release.deploy(self.root,'https://staff.onrender.com',self.hook,read_status=lambda _: {'busySessions':1})
            self.assertEqual(result,'deferred-support-in-progress');opener.assert_not_called()
        self.assertFalse((self.root/'updates/deploy-confirmed.json').exists())

    def test_unknown_old_server_requires_bootstrap(self):
        with self.assertRaises(ValueError):deploy_release.deploy(self.root,'https://staff.onrender.com',self.hook,read_status=lambda _: {})

    def test_confirmed_deployment_does_not_wake_free_server(self):
        (self.root/'updates/deploy-confirmed.json').write_text(json.dumps({'clientSha256':'a'*64,'version':'0.10.0.0'}))
        reader=MagicMock(side_effect=AssertionError('should not wake Render'))
        self.assertEqual(deploy_release.deploy(self.root,'https://staff.onrender.com',self.hook,read_status=reader),'already-confirmed')
        reader.assert_not_called()

    def test_hook_request_is_followed_by_live_hash_confirmation(self):
        reader=MagicMock(side_effect=[{'busySessions':0},{'downloadReady':True,'sha256':'a'*64,'deployedVersion':'0.10.0.0'}])
        response=MagicMock();response.__enter__.return_value.status=200
        with patch('deploy_release.build_opener') as opener:
            opener.return_value.open.return_value=response
            result=deploy_release.deploy(self.root,'https://staff.onrender.com',self.hook,read_status=reader,sleep=lambda _:None)
        self.assertEqual(result,'confirmed')
        self.assertTrue((self.root/'updates/deploy-confirmed.json').exists())

    def test_hook_failure_does_not_leak_url_and_remains_retryable(self):
        with patch('deploy_release.build_opener') as opener:
            opener.return_value.open.side_effect=RuntimeError(self.hook)
            with self.assertRaises(RuntimeError) as caught:
                deploy_release.deploy(self.root,'https://staff.onrender.com',self.hook,read_status=lambda _: {'busySessions':0})
            self.assertNotIn(self.hook,str(caught.exception))
        self.assertFalse((self.root/'updates/deploy-confirmed.json').exists())


@unittest.skipUnless(UPDATE_COMMAND,'Set JH_UPDATE_COMMAND to the compiled C# update harness')
class CounselorUpdateTests(unittest.TestCase):
    def run_client(self,*args):
        return subprocess.run(UPDATE_COMMAND+list(args),capture_output=True,text=True,timeout=10)

    def test_real_csharp_signature_and_file_verification(self):
        result=self.run_client('verify',str(FIXTURES/'update-manifest.json'),str(FIXTURES/'JHRemoteSupport.exe'))
        self.assertEqual(result.returncode,0,(result.stdout,result.stderr))

    def test_tampered_manifest_or_exe_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            temp=Path(temp)
            wrapper=json.loads((FIXTURES/'update-manifest.json').read_text())
            signature=bytearray(base64.b64decode(wrapper['signature']));signature[1]^=1
            wrapper['signature']=base64.b64encode(signature).decode()
            (temp/'bad.json').write_text(json.dumps(wrapper))
            self.assertEqual(self.run_client('parse',str(temp/'bad.json')).returncode,2)
            (temp/'bad.exe').write_bytes((FIXTURES/'JHRemoteSupport.exe').read_bytes()+b'changed')
            self.assertEqual(self.run_client('verify',str(FIXTURES/'update-manifest.json'),str(temp/'bad.exe')).returncode,2)

    def test_atomic_replace_preserves_previous_and_rejects_bad_new_file(self):
        with tempfile.TemporaryDirectory() as temp:
            temp=Path(temp);target=temp/'JHRemoteSupport.exe';backup=temp/'previous.exe'
            target.write_bytes(b'old counselor executable')
            bad=temp/'bad.exe';bad.write_bytes(b'bad')
            manifest=str(FIXTURES/'update-manifest.json')
            self.assertEqual(self.run_client('replace',manifest,str(bad),str(target),str(backup)).returncode,2)
            self.assertEqual(target.read_bytes(),b'old counselor executable')
            result=self.run_client('replace',manifest,str(FIXTURES/'JHRemoteSupport.exe'),str(target),str(backup))
            self.assertEqual(result.returncode,0,(result.stdout,result.stderr))
            self.assertEqual(backup.read_bytes(),b'old counselor executable')
            self.assertEqual(target.read_bytes(),(FIXTURES/'JHRemoteSupport.exe').read_bytes())

if __name__=='__main__':unittest.main()
