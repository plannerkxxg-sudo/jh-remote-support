"""Consent-based, in-memory WebSocket relay. Deploy only behind HTTPS/WSS."""
import asyncio
import json
import os
import secrets
import time
from collections import defaultdict, deque
from dataclasses import dataclass
from http import HTTPStatus

from websockets.asyncio.server import serve
from websockets.exceptions import ConnectionClosed
from webapp import HttpApp
from branding import clean_text

CODE_LENGTH = 6
WAIT_TTL = 600
ACTIVE_TTL = 7200
PENDING_TTL = 60


def health(connection, request):
    """HTTP readiness/warm-up endpoint, sharing the WebSocket listener."""
    if request.headers.get("Upgrade", "").lower() != "websocket":
        if request.path in ("/", "/healthz"):
            return connection.respond(HTTPStatus.OK, "JH Remote Support Relay ready\n")
        return connection.respond(HTTPStatus.NOT_FOUND, "Not found\n")
    if request.path != "/support":
        return connection.respond(HTTPStatus.NOT_FOUND, "Not found\n")
    return None


@dataclass
class Room:
    code: str
    supporter: object
    created: float
    name: str
    host: object = None
    pending_at: float = 0
    active: bool = False
    description: str = ""


class Relay:
    def __init__(self):
        self.rooms = {}
        self.members = {}
        self.connections = set()
        self.attempts = defaultdict(deque)
        self.rates = defaultdict(deque)

    async def send(self, ws, value):
        payload = value if isinstance(value, bytes) else json.dumps(value)
        await asyncio.wait_for(ws.send(payload), timeout=3)

    def limited(self, table, key, limit, period):
        now = time.monotonic()
        q = table[key]
        while q and now - q[0] > period:
            q.popleft()
        if len(q) >= limit:
            return True
        q.append(now)
        return False

    async def error(self, ws, message):
        await self.send(ws, {"type": "error", "message": message})

    async def handler(self, ws):
        if ws.request.path != "/support" or len(self.connections) >= 300:
            await ws.close(1008, "Unavailable")
            return
        self.connections.add(ws)
        try:
            async for raw in ws:
                await self.message(ws, raw)
        except (ConnectionClosed, asyncio.TimeoutError, OSError):
            pass
        finally:
            await self.remove(ws)
            self.connections.discard(ws)
            self.rates.pop(ws, None)

    async def message(self, ws, raw):
        member = self.members.get(ws)
        if isinstance(raw, bytes):
            if member and member[1] == "host":
                room = member[0]
                if (room.active and room.supporter and len(raw) <= 512_000
                        and raw.startswith(b"\xff\xd8")
                        and not self.limited(self.rates, ws, 15, 1)):
                    await self.send(room.supporter, raw)
            return
        if len(raw) > 2048:
            await ws.close(1008, "Message too large")
            return
        try:
            msg = json.loads(raw)
            if not isinstance(msg, dict):
                raise ValueError()
        except (ValueError, TypeError):
            await self.error(ws, "잘못된 요청입니다.")
            return
        kind = msg.get("type")
        if kind == "create":
            if member or len(self.rooms) >= 100:
                await self.error(ws, "새 연결을 만들 수 없습니다.")
                return
            ip = ws.remote_address[0]
            if self.limited(self.attempts, (ip, "create"), 10, 60):
                await self.error(ws, "잠시 후 다시 시도하세요.")
                return
            code = f"{secrets.randbelow(1_000_000):06d}"
            while code in self.rooms:
                code = f"{secrets.randbelow(1_000_000):06d}"
            name = msg.get("name")
            if not isinstance(name, str) or not name.strip() or len(name) > 40:
                await self.error(ws, "지원자 이름을 1~40자로 입력하세요.")
                return
            name = clean_text(name, 40)
            description = clean_text(msg.get("description", ""), 200)
            if not name or description is None:
                await self.error(ws, "지원자 이름과 소개 문구를 확인하세요. 소개 문구는 200자 이내입니다.")
                return
            room = Room(code, ws, time.monotonic(), name, description=description)
            self.rooms[code] = room
            self.members[ws] = (room, "supporter")
            await self.send(ws, {"type": "created", "code": code, "expires": WAIT_TTL})
        elif kind == "join":
            ip = ws.remote_address[0]
            if member or self.limited(self.attempts, (ip, "join"), 10, 60):
                await self.error(ws, "잠시 후 다시 시도하세요.")
                return
            code = str(msg.get("code", "")).replace("-", "").replace(" ", "").upper()
            if len(code) != CODE_LENGTH or any(c not in "0123456789" for c in code):
                await self.error(ws, "숫자 6자리 코드를 입력하세요.")
                return
            room = self.rooms.get(code)
            if not room or room.host or time.monotonic() - room.created > WAIT_TTL:
                await self.error(ws, "코드가 만료되었거나 연결할 수 없습니다.")
                return
            room.host = ws
            room.pending_at = time.monotonic()
            self.members[ws] = (room, "host")
            await self.send(room.supporter, {"type": "waiting"})
            await self.send(ws, {"type": "request", "name": room.name})
        elif kind == "accept" and member and member[1] == "host":
            room = member[0]
            if room.host and not room.active:
                room.active = True
                await self.send(room.supporter, {"type": "active"})
                await self.send(room.host, {"type": "active"})
        elif kind == "reject" and member and member[1] == "host":
            room = member[0]
            if room.host and not room.active:
                host = room.host
                room.host = None
                self.members.pop(host, None)
                await self.send(host, {"type": "ended", "message": "원격지원 요청을 거절했습니다."})
                await host.close()
                await self.send(room.supporter, {"type": "ready"})
        elif kind == "input" and member and member[1] == "supporter":
            room = member[0]
            if room.active and not self.limited(self.rates, ws, 250, 1) and self.valid_input(msg):
                await self.send(room.host, msg)
        elif kind == "stop":
            await self.remove(ws)
            await ws.close()
        elif kind == "release" and member and member[1] == "supporter" and member[0].active:
            await self.send(member[0].host, {"type": "release"})
        else:
            await self.error(ws, "현재 상태에서는 사용할 수 없는 요청입니다.")

    @staticmethod
    def valid_input(msg):
        event = msg.get("event")
        if event in ("move", "down", "up", "wheel"):
            if not all(type(msg.get(k)) in (int, float) and 0 <= msg[k] <= 1 for k in ("x", "y")):
                return False
            if event in ("down", "up"):
                return msg.get("button") in ("left", "right", "middle")
            if event == "wheel":
                return type(msg.get("delta")) is int and -1200 <= msg["delta"] <= 1200
            return True
        if event == "key":
            return type(msg.get("key")) is int and 1 <= msg["key"] <= 255 and type(msg.get("down")) is bool
        return False

    async def remove(self, ws):
        member = self.members.pop(ws, None)
        if not member:
            return
        room, role = member
        if role == "host" and not room.active:
            room.host = None
            try:
                await self.send(room.supporter, {"type": "ready"})
            except (ConnectionClosed, asyncio.TimeoutError):
                pass
            return
        self.rooms.pop(room.code, None)
        peer = room.host if role == "supporter" else room.supporter
        room.active = False
        if peer:
            self.members.pop(peer, None)
            try:
                await self.send(peer, {"type": "ended", "message": "원격지원이 종료되었습니다."})
                await peer.close()
            except (ConnectionClosed, asyncio.TimeoutError):
                pass

    async def sweep(self):
        while True:
            await asyncio.sleep(5)
            now = time.monotonic()
            for room in list(self.rooms.values()):
                if now - room.created > (ACTIVE_TTL if room.active else WAIT_TTL):
                    await self.remove(room.supporter)
                    await room.supporter.close()
                elif room.host and not room.active and now - room.pending_at > PENDING_TTL:
                    host = room.host
                    await self.remove(host)
                    await host.close()
            for key, q in list(self.attempts.items()):
                if not q or now - q[-1] > 60:
                    self.attempts.pop(key, None)


async def main():
    relay = Relay()
    async with serve(relay.handler, os.getenv("BIND_HOST", "127.0.0.1"),
                     int(os.getenv("PORT", "8765")), max_size=512_000,
                     max_queue=4, compression=None, ping_interval=20, ping_timeout=20,
                     open_timeout=10, close_timeout=3, process_request=HttpApp(relay)):
        print("Relay ready. Public access requires the supplied HTTPS reverse proxy.")
        await relay.sweep()


if __name__ == "__main__":
    asyncio.run(main())
