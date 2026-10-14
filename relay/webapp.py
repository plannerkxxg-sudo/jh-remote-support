"""Same-origin landing page and per-code native client downloads."""
import json
import os
import re
import struct
import time
from collections import defaultdict, deque
from http import HTTPStatus
from pathlib import Path
from urllib.parse import parse_qs, urlsplit
from websockets.datastructures import Headers
from websockets.http11 import Response


class HttpApp:
    MARKER = b"JHCONFIGv1"

    def __init__(self, relay, template=None):
        self.relay = relay
        self.web = Path(__file__).parent / "web"
        self.template = Path(template) if template else Path(__file__).parent / "downloads" / "JHRemoteSupport.exe"
        self.download_attempts = defaultdict(deque)

    @staticmethod
    def response(status, body, content_type="application/json; charset=utf-8", extra=None):
        if isinstance(body, dict):
            body = json.dumps(body, ensure_ascii=False).encode("utf-8")
        elif isinstance(body, str):
            body = body.encode("utf-8")
        headers = Headers({
            "Content-Type": content_type, "Content-Length": str(len(body)),
            "Cache-Control": "no-store", "Referrer-Policy": "no-referrer",
            "X-Content-Type-Options": "nosniff", "X-Frame-Options": "DENY",
            "Content-Security-Policy": "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'",
        })
        if extra:
            headers.update(extra)
        return Response(status, HTTPStatus(status).phrase, headers, body)

    @staticmethod
    def relay_url():
        base = os.getenv("PUBLIC_BASE_URL", "").strip().rstrip("/")
        render_host = os.getenv("RENDER_EXTERNAL_HOSTNAME", "").strip()
        if not base and render_host and render_host.endswith(".onrender.com"):
            base = "https://" + render_host
        parts = urlsplit(base)
        loopback = parts.hostname in ("127.0.0.1", "localhost", "::1")
        if (not parts.hostname or parts.username or parts.password or parts.path or parts.query or parts.fragment
                or not (parts.scheme == "https" or (parts.scheme == "http" and loopback))):
            return None
        return ("wss" if parts.scheme == "https" else "ws") + "://" + parts.netloc + "/support"

    @staticmethod
    def is_pe(binary):
        if len(binary) < 1024 or len(binary) > 1_500_000 or binary[:2] != b"MZ":
            return False
        offset = struct.unpack_from("<I", binary, 0x3c)[0]
        return offset <= len(binary) - 4 and binary[offset:offset + 4] == b"PE\x00\x00"

    def __call__(self, connection, request):
        if request.headers.get("Upgrade", "").lower() == "websocket":
            return None if request.path == "/support" else self.response(404, {"message": "Not found"})
        route = urlsplit(request.path)
        if route.path == "/healthz":
            return self.response(200, "JH Remote Support Relay ready\n", "text/plain; charset=utf-8")
        if route.path in ("/", "/style.css", "/app.js"):
            filename, mime = {
                "/": ("index.html", "text/html; charset=utf-8"),
                "/style.css": ("style.css", "text/css; charset=utf-8"),
                "/app.js": ("app.js", "text/javascript; charset=utf-8"),
            }[route.path]
            try:
                return self.response(200, (self.web / filename).read_bytes(), mime)
            except OSError:
                return self.response(503, {"message": "접속 페이지가 준비되지 않았습니다."})
        if route.path == "/api/status":
            return self.response(200, {"downloadReady": self.template.is_file() and bool(self.relay_url()), "version": "0.2"})
        if route.path == "/download":
            ip = connection.remote_address[0]
            if self.relay.limited(self.download_attempts, ip, 10, 60):
                return self.response(429, {"message": "다운로드 요청이 많습니다. 잠시 후 다시 시도해주세요."})
            if len(self.download_attempts) > 1000:
                now = time.monotonic()
                self.download_attempts = defaultdict(deque, {k: v for k, v in self.download_attempts.items() if v and now - v[-1] <= 60})
            code = parse_qs(route.query).get("code", [""])[0].replace("-", "").replace(" ", "").upper()
            if not re.fullmatch("[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{12}", code):
                return self.response(400, {"message": "12자리 접속 코드를 정확히 입력해주세요."})
            room = self.relay.rooms.get(code)
            if not room or room.host or time.monotonic() - room.created > 600:
                return self.response(404, {"message": "코드가 만료되었거나 사용 중입니다. 정현쌤에게 새 코드를 요청하세요."})
            url = self.relay_url()
            if not url:
                return self.response(503, {"message": "접속 설정이 준비되지 않았습니다. 정현쌤에게 알려주세요."})
            try:
                binary = self.template.read_bytes()
            except OSError:
                return self.response(503, {"message": "지원 프로그램 배포를 준비 중입니다. 정현쌤에게 알려주세요."})
            if not self.is_pe(binary):
                return self.response(503, {"message": "지원 프로그램을 확인하지 못했습니다. 정현쌤에게 알려주세요."})
            profile = json.dumps({"relayUrl": url, "code": code, "mode": "host"}, ensure_ascii=False).encode("utf-8")
            data = binary + profile + struct.pack("<I", len(profile)) + self.MARKER
            return self.response(200, data, "application/octet-stream", {
                "Content-Disposition": 'attachment; filename="JHSupport-' + code + '.exe"',
            })
        return self.response(404, {"message": "페이지를 찾을 수 없습니다."})
