"""Integration tests for the actual C# transport. Set JH_TRANSPORT_COMMAND to a JSON argv.
Windows example: ["C:/temp/TransportHarness.exe"]
Linux example: ["mono", "/tmp/TransportHarness.exe"]
Compile with ManagedWebSocket.cs, RuntimeRequirements.cs, TransportHarness.cs and System.Web.Extensions.
"""
import asyncio
import base64
import hashlib
import json
import os
from pathlib import Path
import ssl
import struct
import sys
import time
import unittest

from websockets.asyncio.server import serve
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'relay'))
from server import Relay

COMMAND = json.loads(os.environ.get('JH_TRANSPORT_COMMAND', '[]'))
GUID = b'258EAFA5-E914-47DA-95CA-C5AB0DC85B11'


def frame(opcode, payload=b'', final=True):
    first = (0x80 if final else 0) | opcode
    n = len(payload)
    size = bytes([n]) if n < 126 else b'\x7e' + struct.pack('!H', n) if n <= 65535 else b'\x7f' + struct.pack('!Q', n)
    return bytes([first]) + size + payload


@unittest.skipUnless(COMMAND, 'Set JH_TRANSPORT_COMMAND to the compiled C# test harness')
class ManagedTransportTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.servers = []
        self.tasks = []
        self.processes = []
        self.server_errors = []
        self.loop = asyncio.get_running_loop()
        self.old_exception_handler = self.loop.get_exception_handler()
        def expected_tls_disconnect(loop, context):
            if isinstance(context.get('exception'), (ssl.SSLError, ConnectionResetError)):
                return  # Negative TLS tests deliberately abort the server handshake.
            loop.default_exception_handler(context)
        self.loop.set_exception_handler(expected_tls_disconnect)

    async def asyncTearDown(self):
        for process in self.processes:
            if process.returncode is None:
                process.kill()
                await process.wait()
        for server in self.servers:
            server.close()
            await server.wait_closed()
        for task in self.tasks:
            if not task.done():
                task.cancel()
        await asyncio.gather(*self.tasks, return_exceptions=True)
        self.loop.set_exception_handler(self.old_exception_handler)
        if self.server_errors:
            raise self.server_errors[0]

    async def run_client(self, mode, url='', *args, env=None):
        process = await asyncio.create_subprocess_exec(*COMMAND, mode, url, *args,
                    stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE, env=env)
        self.processes.append(process)
        stdout, stderr = await asyncio.wait_for(process.communicate(), 9)
        output = stdout.decode().strip()
        return process.returncode, output, stderr.decode()

    async def raw_server(self, action, ssl_context=None):
        async def handler(reader, writer):
            self.tasks.append(asyncio.current_task())
            try:
                request = await asyncio.wait_for(reader.readuntil(b'\r\n\r\n'), 2)
                key = next(line.split(b':', 1)[1].strip() for line in request.split(b'\r\n')
                           if line.lower().startswith(b'sec-websocket-key:'))
                accept = base64.b64encode(hashlib.sha1(key + GUID).digest())
                response = b'HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: ' + accept + b'\r\n\r\n'
                await action(reader, writer, response)
            except (asyncio.IncompleteReadError, ConnectionResetError, BrokenPipeError):
                pass
            except Exception as error:
                self.server_errors.append(error)
            finally:
                writer.close()
                try:
                    await writer.wait_closed()
                except (ssl.SSLError, ConnectionResetError, BrokenPipeError):
                    pass
        server = await asyncio.start_server(handler, '127.0.0.1', 0, ssl=ssl_context)
        self.servers.append(server)
        port = server.sockets[0].getsockname()[1]
        return f'{"wss" if ssl_context else "ws"}://127.0.0.1:{port}/support'

    async def valid(self, mode, url, *args):
        status, output, error = await self.run_client(mode, url, *args)
        self.assertEqual(status, 0, (output, error))
        return output

    async def rejected(self, url, mode='receive'):
        status, output, error = await self.run_client(mode, url)
        self.assertEqual(status, 2, (output, error))
        self.assertIn('error', json.loads(output))

    async def test_requirements(self):
        self.assertEqual(await self.valid('requirements', ''), 'requirements-ok')

    async def test_real_server_echo_sizes_unicode_and_concurrent_writes(self):
        async def echo(ws):
            try:
                async for value in ws:
                    await ws.send(value)
            except Exception:
                pass  # Client deliberately aborts after the test.
        server = await serve(echo, '127.0.0.1', 0, compression=None, max_size=512000)
        self.servers.append(server)
        url = f'ws://127.0.0.1:{server.sockets[0].getsockname()[1]}/support'
        for payload in [b'', b'x'*125, b'x'*126, b'x'*65535, b'x'*65536, bytes(range(256))*2000]:
            data = base64.b64encode(payload).decode()
            # Avoid OS argv limits for large payloads: the harness generates them.
            arg = data if len(data) < 10000 else 'size:' + str(len(payload))
            with self.subTest(payload_bytes=len(payload)):
                output = await self.valid('echo', url, arg, 'binary')
            self.assertEqual(base64.b64decode(json.loads(output)['data']), payload if len(data) < 10000 else b'x'*len(payload))
        payload = '아진쌤 원격지원 😀'.encode()
        output = await self.valid('echo', url, base64.b64encode(payload).decode(), 'text')
        self.assertEqual(base64.b64decode(json.loads(output)['data']), payload)
        self.assertEqual(await self.valid('concurrent', url), 'concurrent-ok')

    async def test_real_relay_code_consent_screen_input(self):
        relay = Relay()
        server = await serve(relay.handler, '127.0.0.1', 0, compression=None, max_size=512000)
        self.servers.append(server)
        url = f'ws://127.0.0.1:{server.sockets[0].getsockname()[1]}/support'
        self.assertEqual(await self.valid('relay', url), 'relay-ok')

    async def test_fragmentation_ping_pong_and_client_mask(self):
        async def action(reader, writer, response):
            text = '원격지원 😀'.encode()
            writer.write(response + frame(1, text[:4], False) + frame(9, b'keepalive') + frame(0, text[4:]))
            await writer.drain()
            header = await reader.readexactly(2)
            self.assertEqual(header, b'\x8a\x89')
            mask = await reader.readexactly(4)
            masked = await reader.readexactly(9)
            self.assertEqual(bytes(c ^ mask[i % 4] for i, c in enumerate(masked)), b'keepalive')
        url = await self.raw_server(action)
        output = json.loads(await self.valid('receive', url))
        self.assertEqual(base64.b64decode(output['data']).decode(), '원격지원 😀')

    async def test_close_handshake(self):
        async def action(reader, writer, response):
            writer.write(response + frame(8, b'\x03\xe8'))
            await writer.drain()
            self.assertEqual(await reader.readexactly(2), b'\x88\x82')
            mask = await reader.readexactly(4)
            data = await reader.readexactly(2)
            self.assertEqual(bytes(c ^ mask[i % 4] for i, c in enumerate(data)), b'\x03\xe8')
        url = await self.raw_server(action)
        self.assertEqual(json.loads(await self.valid('receive', url))['type'], 8)

    async def test_invalid_handshakes(self):
        for mode in ['accept', 'status', 'upgrade', 'duplicate', 'extension', 'subprotocol', 'huge', 'folded']:
            async def action(reader, writer, response, mode=mode):
                if mode == 'accept': response = response.replace(b'Sec-WebSocket-Accept:', b'Wrong-Accept:')
                if mode == 'status': response = response.replace(b'101 Switching', b'200 Switching')
                if mode == 'upgrade': response = response.replace(b'Connection: Upgrade', b'Connection: close')
                if mode == 'duplicate': response = response[:-2] + b'Sec-WebSocket-Accept: bad\r\n\r\n'
                if mode == 'extension': response = response[:-2] + b'Sec-WebSocket-Extensions: permessage-deflate\r\n\r\n'
                if mode == 'subprotocol': response = response[:-2] + b'Sec-WebSocket-Protocol: not-requested\r\n\r\n'
                if mode == 'huge': response = b'HTTP/1.1 101 Switching Protocols\r\nX-Large: ' + b'a'*17000
                if mode == 'folded': response = response[:-2] + b' folded: invalid\r\n\r\n'
                writer.write(response)
                await writer.drain()
            await self.rejected(await self.raw_server(action))

    async def test_invalid_frames_and_message_limits(self):
        invalid = [
            b'\xc1\x00', b'\x81\x80', frame(3), frame(0), frame(9, b'', False),
            frame(8, b'x'), frame(8, b'\x03\xed'), frame(1, b'\xff'), frame(8, b'\x03\xe8\xff'),
            b'\x82\x7e\x00\x01x', b'\x82\x7f\x00\x00\x00\x00\x00\x00\x00\x01x',
            b'\x82\x7f\x80\x00\x00\x00\x00\x00\x00\x00',
            b'\x82\x7f' + struct.pack('!Q', 512001),
            frame(1, b'a', False) + frame(2, b'b'),
            frame(2, b'x'*300000, False) + b'\x80\x7f' + struct.pack('!Q', 300000),
        ]
        for data in invalid:
            async def action(reader, writer, response, data=data):
                writer.write(response + data)
                await writer.drain()
            await self.rejected(await self.raw_server(action))

    async def test_cancel_stalled_read_and_connect(self):
        async def action(reader, writer, response):
            writer.write(response)
            await writer.drain()
            await reader.read()
        url = await self.raw_server(action)
        started = time.monotonic()
        await self.rejected(url, 'cancel')
        self.assertLess(time.monotonic() - started, 3)
        async def stalled(reader, writer, response):
            await reader.read()
        await self.rejected(await self.raw_server(stalled), 'timeout')

    async def test_plaintext_public_url_rejected(self):
        await self.rejected('ws://example.com/support')

    async def test_trusted_tls_and_hostname_verification(self):
        cert = os.environ.get('JH_TEST_TLS_CERT')
        key = os.environ.get('JH_TEST_TLS_KEY')
        store = os.environ.get('JH_TEST_TRUSTED_STORE')
        if not cert or not key or not store:
            self.skipTest('Set TLS fixture and isolated trust store paths')
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        context.minimum_version = ssl.TLSVersion.TLSv1_2
        context.load_cert_chain(cert, key)
        async def action(reader, writer, response):
            writer.write(response + frame(1, 'TLS 접속 성공'.encode()))
            await writer.drain()
        env = dict(os.environ, XDG_CONFIG_HOME=store, XDG_DATA_HOME=store)
        url = await self.raw_server(action, context)
        status, output, error = await self.run_client('receive', url, env=env)
        self.assertEqual(status, 0, (output, error))
        self.assertEqual(base64.b64decode(json.loads(output)['data']).decode(), 'TLS 접속 성공')
        wrong = os.environ.get('JH_TEST_WRONG_TLS_CERT')
        if wrong:
            bad_context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
            bad_context.load_cert_chain(wrong, key)
            status, output, error = await self.run_client('receive', await self.raw_server(action, bad_context), env=env)
            self.assertEqual(status, 2, (output, error))
            self.assertIn('error', json.loads(output))

    async def test_untrusted_tls_rejected(self):
        cert = os.environ.get('JH_TEST_TLS_CERT')
        key = os.environ.get('JH_TEST_TLS_KEY')
        if not cert or not key:
            self.skipTest('Set local TLS fixture paths')
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        context.load_cert_chain(cert, key)
        async def action(reader, writer, response):
            writer.write(response + frame(1, b'should not connect'))
            await writer.drain()
        await self.rejected(await self.raw_server(action, context))

if __name__ == '__main__':
    unittest.main()
