"""Real local WebSocket tests, including consent and disconnect behavior."""
import asyncio
import json
import sys
import unittest
from unittest.mock import patch
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "relay"))
from server import Relay, WAIT_TTL
from webapp import HttpApp
from websockets.asyncio.server import serve
from websockets.asyncio.client import connect
from websockets.exceptions import ConnectionClosed


class RelayTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.relay = Relay()
        self.http_app = HttpApp(self.relay)
        self.server = await serve(self.relay.handler, "127.0.0.1", 0, max_size=512000, process_request=self.http_app)
        port = self.server.sockets[0].getsockname()[1]
        self.url = f"ws://127.0.0.1:{port}/support"
        self.clients = []
        self.port = port

    async def test_http_health_check_shares_the_websocket_port(self):
        reader, writer = await asyncio.open_connection("127.0.0.1", self.port)
        writer.write(b"GET /healthz HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n")
        await writer.drain()
        response = await asyncio.wait_for(reader.read(), 1)
        writer.close()
        await writer.wait_closed()
        self.assertIn(b"200 OK", response)
        self.assertIn(b"JH Remote Support Relay ready", response)

    async def asyncTearDown(self):
        for client in self.clients:
            await client.close()
        self.server.close()
        await self.server.wait_closed()

    async def client(self):
        client = await connect(self.url)
        self.clients.append(client)
        return client

    async def send(self, client, **msg):
        await client.send(json.dumps(msg))

    async def recv(self, client):
        return json.loads(await asyncio.wait_for(client.recv(), 1))

    async def silence(self, client):
        with self.assertRaises(asyncio.TimeoutError):
            await asyncio.wait_for(client.recv(), 0.06)

    async def pair(self, accept=True):
        supporter = await self.client()
        await self.send(supporter, type="create", name="정현쌤")
        created = await self.recv(supporter)
        self.assertEqual(created["type"], "created")
        self.assertEqual(len(created["code"]), 6)
        self.assertRegex(created["code"], r"^[0-9]{6}$")
        host = await self.client()
        await self.send(host, type="join", code=created["code"])
        self.assertEqual((await self.recv(supporter))["type"], "waiting")
        request = await self.recv(host)
        self.assertEqual(request, {"type": "request", "name": "정현쌤", "description": ""})
        if accept:
            await self.send(host, type="accept")
            self.assertEqual((await self.recv(host))["type"], "active")
            self.assertEqual((await self.recv(supporter))["type"], "active")
        return supporter, host, created["code"]

    async def test_supporter_issues_code_host_approves_and_data_flows(self):
        supporter, host, _ = await self.pair()
        frame = b"\xff\xd8test-frame\xff\xd9"
        await host.send(frame)
        self.assertEqual(await asyncio.wait_for(supporter.recv(), 1), frame)
        event = dict(type="input", event="key", key=65, down=True)
        await self.send(supporter, **event)
        self.assertEqual(await self.recv(host), event)
        await self.send(supporter, type="release")
        self.assertEqual(await self.recv(host), {"type": "release"})

    async def test_six_digit_code_preserves_zeroes_and_retries_collision(self):
        first = await self.client()
        second = await self.client()
        with patch('server.secrets.randbelow', side_effect=[7, 7, 42]):
            await self.send(first, type="create", name="first")
            first_code = (await self.recv(first))["code"]
            await self.send(second, type="create", name="second")
            second_code = (await self.recv(second))["code"]
        self.assertEqual(first_code, "000007")
        self.assertEqual(second_code, "000042")
        host = await self.client()
        await self.send(host, type="join", code=first_code)
        self.assertEqual((await self.recv(host))["type"], "request")

    async def test_no_screen_or_input_before_host_approval(self):
        supporter, host, _ = await self.pair(accept=False)
        await host.send(b"\xff\xd8private\xff\xd9")
        await self.send(supporter, type="input", event="key", key=65, down=True)
        await self.silence(supporter)
        await self.silence(host)
        await self.send(supporter, type="accept")
        self.assertEqual((await self.recv(supporter))["type"], "error")
        self.assertFalse(next(iter(self.relay.rooms.values())).active)

    async def test_reject_keeps_supporter_waiting_and_never_streams(self):
        supporter, host, code = await self.pair(accept=False)
        await self.send(host, type="reject")
        self.assertEqual((await self.recv(host))["type"], "ended")
        self.assertEqual((await self.recv(supporter))["type"], "ready")
        self.assertIn(code, self.relay.rooms)
        replacement = await self.client()
        await self.send(replacement, type="join", code=code)
        self.assertEqual((await self.recv(replacement))["type"], "request")

    async def test_disconnect_revokes_code_and_notifies_supporter(self):
        supporter, host, code = await self.pair()
        await host.close()
        self.assertEqual((await self.recv(supporter))["type"], "ended")
        self.assertNotIn(code, self.relay.rooms)
        stranger = await self.client()
        await self.send(stranger, type="join", code=code)
        self.assertEqual((await self.recv(stranger))["type"], "error")

    async def test_supporter_stop_ends_host(self):
        supporter, host, code = await self.pair()
        await self.send(supporter, type="stop")
        self.assertEqual((await self.recv(host))["type"], "ended")
        self.assertNotIn(code, self.relay.rooms)

    async def test_second_host_cannot_take_over(self):
        supporter, host, _ = await self.pair(accept=False)
        intruder = await self.client()
        code = next(iter(self.relay.rooms))
        await self.send(intruder, type="join", code=code)
        self.assertEqual((await self.recv(intruder))["type"], "error")
        await self.send(host, type="accept")
        self.assertEqual((await self.recv(supporter))["type"], "active")

    async def test_malformed_input_wrong_role_and_large_messages(self):
        supporter, host, _ = await self.pair()
        await self.send(supporter, type="input", event="move", x=-1, y=0.5)
        await self.send(supporter, type="input", event="key", key=999, down=True)
        await self.silence(host)
        await self.send(host, type="input", event="key", key=65, down=True)
        self.assertEqual((await self.recv(host))["type"], "error")
        await host.send('{')
        self.assertEqual((await self.recv(host))["type"], "error")
        await supporter.send(' ' * 2049)
        self.assertEqual((await self.recv(host))["type"], "ended")

    async def test_expired_code_and_join_rate_limit(self):
        supporter = await self.client()
        await self.send(supporter, type="create", name="test")
        code = (await self.recv(supporter))["code"]
        self.relay.rooms[code].created -= WAIT_TTL + 1
        client = await self.client()
        await self.send(client, type="join", code=code)
        self.assertEqual((await self.recv(client))["type"], "error")
        for _ in range(10):
            await self.send(client, type="join", code="999999")
            result = await self.recv(client)
        self.assertEqual(result["message"], "잠시 후 다시 시도하세요.")

    async def test_separate_sessions_cannot_cross_streams(self):
        supporter_a, host_a, _ = await self.pair()
        supporter_b, host_b, _ = await self.pair()
        await host_a.send(b"\xff\xd8A\xff\xd9")
        self.assertEqual(await supporter_a.recv(), b"\xff\xd8A\xff\xd9")
        await self.silence(supporter_b)
        await self.send(supporter_b, type="input", event="move", x=0.5, y=0.5)
        self.assertEqual((await self.recv(host_b))["event"], "move")
        await self.silence(host_a)

    async def test_branding_description_validation_and_consent_identity(self):
        supporter = await self.client()
        for description in ('x' * 201, {'html': 'bad'}, None):
            await self.send(supporter, type='create', name='민주쌤', description=description)
            self.assertEqual((await self.recv(supporter))['type'], 'error')
            self.assertEqual(self.relay.rooms, {})
        await self.send(supporter, type='create', name='\u200b')
        self.assertEqual((await self.recv(supporter))['type'], 'error')
        await self.send(supporter, type='create', name='민주쌤', description='가입·신청 안내')
        code = (await self.recv(supporter))['code']
        self.assertEqual(self.relay.rooms[code].description, '가입·신청 안내')
        host = await self.client()
        await self.send(host, type='join', code=code)
        self.assertEqual(await self.recv(host), {'type': 'request', 'name': '민주쌤', 'description': '가입·신청 안내'})
        self.assertFalse(self.relay.rooms[code].active)


if __name__ == "__main__":
    unittest.main()
