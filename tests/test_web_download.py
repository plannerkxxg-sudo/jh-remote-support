"""Exercise stable release bytes, visible connection names, and consent over HTTP/WS."""
import asyncio
import hashlib
import json
import os
import struct
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from urllib.parse import urlencode

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "relay"))
from server import Relay, WAIT_TTL
from webapp import HttpApp
from websockets.asyncio.server import serve
from websockets.asyncio.client import connect


class WebTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.template = Path(self.temp.name) / "test.exe"
        # A PE-shaped byte fixture, not an executable or a Windows run test.
        self.binary = bytearray(1024)
        self.binary[:2] = b"MZ"
        struct.pack_into("<I", self.binary, 0x3c, 128)
        self.binary[128:132] = b"PE\x00\x00"
        self.binary[180:180 + len(HttpApp.STABLE_MARKER)] = HttpApp.STABLE_MARKER
        self.binary[300:300 + len(HttpApp.COMPATIBILITY_MARKER)] = HttpApp.COMPATIBILITY_MARKER
        self.binary[400:400 + len(HttpApp.UPDATE_MARKER)] = HttpApp.UPDATE_MARKER
        self.template.write_bytes(self.binary)
        self.relay = Relay()
        self.app = HttpApp(self.relay, template=self.template)
        self.service = await serve(self.relay.handler, "127.0.0.1", 0, process_request=self.app)
        self.port = self.service.sockets[0].getsockname()[1]
        self.environment = patch.dict(os.environ, {"PUBLIC_BASE_URL": f"http://127.0.0.1:{self.port}"})
        self.environment.start()
        self.clients = []

    async def asyncTearDown(self):
        for client in self.clients:
            await client.close()
        self.service.close()
        await self.service.wait_closed()
        self.environment.stop()
        self.temp.cleanup()

    async def get(self, path):
        reader, writer = await asyncio.open_connection("127.0.0.1", self.port)
        writer.write((f"GET {path} HTTP/1.1\r\nHost: malicious.invalid\r\nConnection: close\r\n\r\n").encode())
        await writer.drain()
        raw = await asyncio.wait_for(reader.read(), 2)
        writer.close()
        await writer.wait_closed()
        headers, body = raw.split(b"\r\n\r\n", 1)
        return int(headers.split(b" ", 2)[1]), headers, body

    async def code(self):
        client = await connect(f"ws://127.0.0.1:{self.port}/support")
        self.clients.append(client)
        await client.send(json.dumps({"type": "create", "name": "정현쌤"}))
        return json.loads(await client.recv())["code"]

    async def test_website_assets_and_health(self):
        for path, expected in (("/", b"connect-form"), ("/app.js", b"/api/download-info?code="),
                               ("/style.css", b"@media"), ("/healthz", b"Relay ready")):
            status, headers, body = await self.get(path)
            self.assertEqual(status, 200)
            self.assertIn(expected, body)
            self.assertIn(b"Referrer-Policy: no-referrer", headers)

    async def test_download_preserves_release_and_uses_trusted_server_in_filename(self):
        code = await self.code()
        status, headers, binary = await self.get("/download?code=" + code)
        self.assertEqual(status, 200)
        self.assertEqual(binary, self.binary)
        self.assertIn(b"application/vnd.microsoft.portable-executable", headers)
        self.assertIn(code.encode(), headers)
        self.assertIn(f'JHSupport-{code}-local-127.0.0.1~{self.port}.exe'.encode(), headers)
        self.assertIn(hashlib.sha256(binary).hexdigest().encode(), headers)
        self.assertNotIn(b"malicious.invalid", headers)
        self.assertFalse(self.relay.rooms[code].active)
        self.assertIsNone(self.relay.rooms[code].host)

    async def test_invalid_expired_and_unknown_codes_cannot_download(self):
        code = await self.code()
        self.relay.rooms[code].created -= WAIT_TTL + 1
        self.assertEqual((await self.get('/download?code=' + code))[0], 404)
        self.assertEqual((await self.get('/download?code=BAD'))[0], 400)
        self.assertEqual((await self.get('/download?code=999999'))[0], 404)
        self.assertEqual((await self.get('/../../client/RemoteSupport.cs'))[0], 404)

    async def test_old_alphanumeric_codes_are_rejected(self):
        self.assertEqual((await self.get('/download?code=ABCD2345EFGH'))[0], 400)
        self.assertEqual((await self.get('/download?code=12A456'))[0], 400)

    async def test_leading_zero_code_is_preserved_in_filename(self):
        with patch('server.secrets.randbelow', return_value=7):
            code = await self.code()
        self.assertEqual(code, '000007')
        status, headers, binary = await self.get('/download?code=' + code)
        self.assertEqual(status, 200)
        self.assertIn(b'JHSupport-000007-', headers)
        self.assertEqual(binary, self.binary)

    async def test_no_exe_returns_error_not_a_fake_download(self):
        code = await self.code()
        self.template.unlink()
        self.assertEqual((await self.get('/download?code=' + code))[0], 503)
        self.template.write_bytes(b'not an exe')
        self.assertEqual((await self.get('/download?code=' + code))[0], 503)

    async def test_render_url_and_unsafe_url_rejection(self):
        with patch.dict(os.environ, {"PUBLIC_BASE_URL": "", "RENDER_EXTERNAL_HOSTNAME": "my-relay.onrender.com"}):
            self.assertEqual(HttpApp.relay_url(), 'wss://my-relay.onrender.com/support')
        with patch.dict(os.environ, {"PUBLIC_BASE_URL": "http://remote.example"}):
            self.assertIsNone(HttpApp.relay_url())

    async def test_active_or_pending_session_cannot_download_again(self):
        code = await self.code()
        host = await connect(f"ws://127.0.0.1:{self.port}/support")
        self.clients.append(host)
        await host.send(json.dumps({"type": "join", "code": code}))
        self.assertEqual(json.loads(await host.recv())["type"], "request")
        self.assertEqual((await self.get('/download?code=' + code))[0], 404)

    async def test_download_rate_limit(self):
        for _ in range(10):
            self.assertEqual((await self.get('/download?code=BAD'))[0], 400)
        self.assertEqual((await self.get('/download?code=BAD'))[0], 429)

    async def test_teacher_name_and_description_replace_all_student_facing_branding(self):
        query = urlencode({"name": "민주쌤", "description": "민주쌤이 수강신청을 도와드립니다."})
        status, headers, body = await self.get('/?' + query)
        self.assertEqual(status, 200)
        page = body.decode()
        self.assertIn('<title>민주쌤 원격지원</title>', page)
        self.assertIn('민주쌤이 수강신청을 도와드립니다.', page)
        self.assertIn('민주쌤에게 새 코드를 요청', page)
        self.assertIn('name=%EB%AF%BC%EC%A3%BC%EC%8C%A4', page)
        self.assertNotIn('정현쌤', page)
        self.assertNotIn('{{SITE_TITLE}}', page)
        self.assertIn(b"script-src 'self'", headers)

    async def test_personalized_links_remain_independent_and_do_not_change_server_defaults(self):
        with patch.dict(os.environ, {"SUPPORT_NAME": "", "SUPPORT_DESCRIPTION": ""}):
            for owner in ('민주쌤', '아진쌤'):
                status, _, body = await self.get('/?' + urlencode({'name': owner}))
                self.assertEqual(status, 200)
                self.assertIn(f'<title>{owner} 원격지원</title>', body.decode())
            _, _, body = await self.get('/')
            self.assertIn('<title>원격지원</title>', body.decode())
            self.assertNotIn('민주쌤', body.decode())
            self.assertNotIn('아진쌤', body.decode())

    async def test_deployment_branding_is_reused_after_app_restart(self):
        with patch.dict(os.environ, {"SUPPORT_NAME": "아진쌤", "SUPPORT_DESCRIPTION": "아진쌤의 신청 안내"}):
            for app in (self.app, HttpApp(self.relay, template=self.template)):
                from websockets.http11 import Request
                from websockets.datastructures import Headers
                response = app(None, Request('/', Headers()))
                self.assertIn('<title>아진쌤 원격지원</title>', response.body.decode())
                self.assertIn('아진쌤의 신청 안내', response.body.decode())
            status, _, body = await self.get('/api/site')
            self.assertEqual(status, 200)
            self.assertEqual(json.loads(body)['name'], '아진쌤')

    async def test_branding_is_plain_text_in_html_and_attributes(self):
        name = '\"><script>alert(1)</script>'
        description = '<img src=x onerror=alert(1)>{{SUPPORT_NAME}} & "소개"'
        status, _, body = await self.get('/?' + urlencode({'name': name, 'description': description}))
        self.assertEqual(status, 200)
        page = body.decode()
        self.assertNotIn('<script>alert(1)</script>', page)
        self.assertNotIn('<img src=x', page)
        self.assertIn('&lt;script&gt;', page)
        self.assertIn('&lt;img src=x', page)
        self.assertIn('{{SUPPORT_NAME}} &amp; &quot;소개&quot;', page)

    async def test_invalid_or_excessive_branding_parameters_are_rejected(self):
        for query in ({'name': 'x' * 41}, {'description': 'x' * 201}):
            self.assertEqual((await self.get('/?' + urlencode(query)))[0], 400)
        self.assertEqual((await self.get('/?' + urlencode({str(i): 'x' for i in range(9)})))[0], 400)
        self.assertEqual((await self.get('/?name=' + 'x' * 4097))[0], 400)

    async def test_actual_room_owner_is_received_at_consent_not_from_download_query(self):
        supporter = await connect(f'ws://127.0.0.1:{self.port}/support')
        self.clients.append(supporter)
        await supporter.send(json.dumps({'type': 'create', 'name': '아진쌤', 'description': '아진쌤의 신청 안내'}))
        code = json.loads(await supporter.recv())['code']
        status, _, binary = await self.get('/download?' + urlencode({'code': code, 'name': '다른쌤'}))
        self.assertEqual(status, 200)
        self.assertEqual(binary, self.binary)
        self.assertFalse(self.relay.rooms[code].active)
        student = await connect(f'ws://127.0.0.1:{self.port}/support')
        self.clients.append(student)
        await student.send(json.dumps({'type':'join', 'code':code}))
        message = json.loads(await student.recv())
        self.assertEqual(message['type'], 'request')
        self.assertEqual(message['name'], '아진쌤')
        self.assertEqual(message['description'], '아진쌤의 신청 안내')
        self.assertFalse(self.relay.rooms[code].active)

    async def test_old_client_downloads_still_receive_an_owner_name(self):
        code = await self.code()  # v0.6 create messages have no description field.
        student = await connect(f'ws://127.0.0.1:{self.port}/support')
        self.clients.append(student)
        await student.send(json.dumps({'type':'join', 'code':code}))
        message = json.loads(await student.recv())
        self.assertEqual(message['name'], '정현쌤')
        self.assertEqual(message['description'], '')

    async def test_different_sessions_and_teachers_receive_identical_executable_hash(self):
        first = await self.code()
        second = await self.code()
        a = (await self.get('/download?' + urlencode({'code':first, 'name':'민주쌤'})))[2]
        b = (await self.get('/download?' + urlencode({'code':second, 'name':'아진쌤'})))[2]
        self.assertEqual(a, b)
        self.assertEqual(a, self.binary)
        self.assertEqual(hashlib.sha256(a).digest(), hashlib.sha256(self.binary).digest())

    async def test_preflight_metadata_matches_exact_download_bytes(self):
        code = await self.code()
        status, _, body = await self.get('/api/download-info?code=' + code)
        info = json.loads(body)
        self.assertEqual(status, 200)
        _, headers, binary = await self.get(info['downloadUrl'])
        self.assertIn(info['filename'].encode(), headers)
        self.assertEqual(info['sha256'], hashlib.sha256(binary).hexdigest())
        self.assertEqual(info['size'], len(binary))
        self.assertEqual(info['version'], '0.10')
        self.assertFalse(self.relay.rooms[code].active)

    async def test_old_exe_is_rejected_instead_of_opening_wrong_client_mode(self):
        code = await self.code()
        self.template.write_bytes(self.binary.replace(HttpApp.STABLE_MARKER, b'X' * len(HttpApp.STABLE_MARKER)))
        for path in ('/download?code=', '/api/download-info?code='):
            self.assertEqual((await self.get(path + code))[0], 503)
        status = json.loads((await self.get('/api/status'))[2])
        self.assertFalse(status['downloadReady'])
        self.assertIsNone(status['sha256'])

    async def test_v08_client_without_compatible_transport_is_rejected(self):
        self.template.write_bytes(self.binary.replace(HttpApp.COMPATIBILITY_MARKER, b'X' * len(HttpApp.COMPATIBILITY_MARKER)))
        code = await self.code()
        self.assertEqual((await self.get('/download?code=' + code))[0], 503)
        self.assertFalse(json.loads((await self.get('/api/status'))[2])['downloadReady'])

    async def test_status_reports_deployed_version_and_busy_room_count(self):
        before = json.loads((await self.get('/api/status'))[2])
        self.assertEqual(before['busySessions'], 0)
        self.assertEqual(before['deployedVersion'], '0.10.0.0')
        await self.code()
        self.assertEqual(json.loads((await self.get('/api/status'))[2])['busySessions'], 1)

    async def test_client_without_updater_is_rejected(self):
        self.template.write_bytes(self.binary.replace(HttpApp.UPDATE_MARKER, b'X' * len(HttpApp.UPDATE_MARKER)))
        code = await self.code()
        self.assertEqual((await self.get('/download?code=' + code))[0], 503)

    async def test_signed_release_suffix_is_preserved_byte_for_byte(self):
        # An opaque signature-shaped suffix, not a real certificate validation.
        original = bytes(self.binary) + b'CERTIFICATE-TABLE-FIXTURE-DO-NOT-ALTER'
        self.template.write_bytes(original)
        code = await self.code()
        binary = (await self.get('/download?code=' + code))[2]
        self.assertEqual(binary, original)

    async def test_preflight_rejects_expired_code_and_has_separate_rate_limit(self):
        code = await self.code()
        self.relay.rooms[code].created -= WAIT_TTL + 1
        self.assertEqual((await self.get('/api/download-info?code=' + code))[0], 404)
        for _ in range(9):
            self.assertEqual((await self.get('/api/download-info?code=BAD'))[0], 400)
        self.assertEqual((await self.get('/api/download-info?code=BAD'))[0], 429)

    async def test_filename_contract_and_unsafe_authorities(self):
        cases = [
            ('wss://alice.onrender.com/support', 'JHSupport-000007-tls-alice.onrender.com.exe'),
            ('wss://alice.example:8443/support', 'JHSupport-000007-tls-alice.example~8443.exe'),
            ('ws://127.0.0.1:8765/support', 'JHSupport-000007-local-127.0.0.1~8765.exe'),
            ('ws://localhost:8765/support', 'JHSupport-000007-local-localhost~8765.exe'),
        ]
        for url, expected in cases:
            self.assertEqual(HttpApp.download_filename('000007', url), expected)
        for url in ('ws://remote.example/support', 'wss://user:pass@remote.example/support',
                    'wss://remote.example:0/support', 'wss://remote.example:65536/support',
                    'wss://bad..example/support', 'wss://remote.example/support?code=1',
                    'wss://-bad.example/support', 'wss://remote.example/other'):
            self.assertIsNone(HttpApp.download_filename('000007', url), url)
        self.assertIsNone(HttpApp.download_filename('AB1234', 'wss://example.com/support'))

    async def test_excessive_download_queries_are_rejected(self):
        self.assertEqual((await self.get('/api/download-info?' + '&'.join(f'x{i}=1' for i in range(9))))[0], 400)
        self.assertEqual((await self.get('/download?code=' + '1' * 4100))[0], 400)

    async def test_search_console_verification_value_is_escaped(self):
        with patch.dict(os.environ, {'GOOGLE_SITE_VERIFICATION':'test"><script>unsafe</script>'}):
            status, _, body = await self.get('/')
            self.assertEqual(status,200)
            self.assertIn(b'name="google-site-verification"',body)
            self.assertIn(b'test&quot;&gt;&lt;script&gt;unsafe&lt;/script&gt;',body)
            self.assertNotIn(b'<script>unsafe</script>',body)


if __name__ == '__main__':
    unittest.main()
