"""Same-origin site; distribute an immutable, inspectable Windows binary."""
import hashlib
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
from branding import settings, render


class HttpApp:
    UPDATE_MARKER = "JH_SIGNED_UPDATES_V1".encode("utf-16le")
    COMPATIBILITY_MARKER = "JH_MANAGED_WEBSOCKET_V1".encode("utf-16le")
    STABLE_MARKER = "JH_STABLE_DOWNLOAD_V1".encode("utf-16le")

    def __init__(self, relay, template=None):
        self.relay = relay
        self.web = Path(__file__).parent / "web"
        self.template = Path(template) if template else Path(__file__).parent / "downloads" / "JHRemoteSupport.exe"
        self.download_attempts = defaultdict(deque)
        self.info_attempts = defaultdict(deque)

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
        try:
            parts = urlsplit(base)
            parts.port  # Reject invalid ports instead of raising in a request handler.
        except ValueError:
            return None
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

    @staticmethod
    def download_filename(code, relay_url):
        """Visible connection hints in the filename, never in executable bytes.

        No hiding, packing, executable modification, or security-setting changes.
        The peer's name is obtained from the room and confirmed before sharing.
        """
        if not re.fullmatch(r"[0-9]{6}", code):
            return None
        try:
            uri = urlsplit(relay_url)
            host = (uri.hostname or "").encode("idna").decode("ascii").lower()
            port = uri.port
        except (ValueError, UnicodeError):
            return None
        if (uri.scheme not in ("wss", "ws") or uri.path != "/support"
                or uri.username or uri.password or uri.query or uri.fragment):
            return None
        # File names must be valid on Windows and unambiguous to the client.
        if not host or len(host) > 180 or any(not re.fullmatch(
                r"[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?", label) for label in host.split(".")):
            return None
        local = uri.scheme == "ws"
        if local and host not in ("localhost", "127.0.0.1"):
            return None
        if port is not None and not 1 <= port <= 65535:
            return None
        authority = host + ("~" + str(port) if port is not None else "")
        return "JHSupport-" + code + ("-local-" if local else "-tls-") + authority + ".exe"

    def release(self):
        try:
            if self.template.stat().st_size > 1_500_000:
                return None
            binary = self.template.read_bytes()
        except OSError:
            return None
        if (not self.is_pe(binary) or self.STABLE_MARKER not in binary
                or self.COMPATIBILITY_MARKER not in binary or self.UPDATE_MARKER not in binary):
            return None
        # Do not modify or strip certificate data: serve the exact release bytes.
        return binary, hashlib.sha256(binary).hexdigest()

    @staticmethod
    def deployed_version():
        try:
            path = Path(__file__).parent / "release-version.json"
            if path.stat().st_size > 2048:
                return None
            value = json.loads(path.read_text())["version"]
            return value if isinstance(value, str) and re.fullmatch(r"[0-9]+(?:\.[0-9]+){3}", value) else None
        except (OSError, ValueError, KeyError, TypeError):
            return None

    def __call__(self, connection, request):
        if request.headers.get("Upgrade", "").lower() == "websocket":
            return None if request.path == "/support" else self.response(404, {"message": "Not found"})
        route = urlsplit(request.path)
        if route.path == "/healthz":
            return self.response(200, "JH Remote Support Relay ready\n", "text/plain; charset=utf-8")
        if route.path in ("/", "/api/site"):
            if len(route.query) > 4096:
                return self.response(400, {"message": "안내 링크가 너무 깁니다. 담당자에게 새 안내를 요청하세요."})
            try:
                brand = settings(parse_qs(route.query, keep_blank_values=True, max_num_fields=8))
            except ValueError as error:
                return self.response(400, {"message": str(error)})
            if route.path == "/api/site":
                return self.response(200, brand)
            try:
                page = render((self.web / "index.html").read_text(encoding="utf-8"), brand)
                return self.response(200, page, "text/html; charset=utf-8")
            except OSError:
                return self.response(503, {"message": "접속 페이지가 준비되지 않았습니다."})
        if route.path in ("/style.css", "/app.js"):
            filename, mime = {
                "/style.css": ("style.css", "text/css; charset=utf-8"),
                "/app.js": ("app.js", "text/javascript; charset=utf-8"),
            }[route.path]
            try:
                return self.response(200, (self.web / filename).read_bytes(), mime)
            except OSError:
                return self.response(503, {"message": "접속 페이지가 준비되지 않았습니다."})
        if route.path == "/api/status":
            release = self.release()
            url = self.relay_url()
            ready = bool(release and url and self.download_filename("000000", url))
            return self.response(200, {"downloadReady": ready, "version": "0.10",
                "busySessions": len(self.relay.rooms), "deployedVersion": self.deployed_version(),
                "downloadFormat": "stable-exe-filename-v1",
                "sha256": release[1] if release else None})
        if route.path in ("/download", "/api/download-info"):
            ip = connection.remote_address[0]
            attempts = self.download_attempts if route.path == "/download" else self.info_attempts
            if self.relay.limited(attempts, ip, 10, 60):
                return self.response(429, {"message": "다운로드 요청이 많습니다. 잠시 후 다시 시도해주세요."})
            if len(attempts) > 1000:
                now = time.monotonic()
                expired = [key for key, value in attempts.items() if not value or now - value[-1] > 60]
                for key in expired:
                    del attempts[key]
            if len(route.query) > 4096:
                return self.response(400, {"message": "접속 코드 요청이 올바르지 않습니다."})
            try:
                query = parse_qs(route.query, max_num_fields=8)
            except ValueError:
                return self.response(400, {"message": "접속 코드 요청이 올바르지 않습니다."})
            code = query.get("code", [""])[0].replace("-", "").replace(" ", "").upper()
            if not re.fullmatch("[0-9]{6}", code):
                return self.response(400, {"message": "숫자 6자리 접속 코드를 정확히 입력해주세요."})
            room = self.relay.rooms.get(code)
            if not room or room.host or time.monotonic() - room.created > 600:
                return self.response(404, {"message": "코드가 만료되었거나 사용 중입니다. 담당자에게 새 코드를 요청하세요."})
            url = self.relay_url()
            filename = self.download_filename(code, url) if url else None
            if not filename:
                return self.response(503, {"message": "접속 설정이 준비되지 않았습니다. 담당자에게 알려주세요."})
            release = self.release()
            if not release:
                return self.response(503, {"message": "담당자가 v0.10 실행파일을 서버에 올려야 합니다. 잠시 후 다시 시도해주세요."})
            binary, digest = release
            if route.path == "/api/download-info":
                return self.response(200, {"version": "0.10", "filename": filename,
                    "sha256": digest, "size": len(binary), "downloadUrl": "/download?code=" + code})
            return self.response(200, binary, "application/vnd.microsoft.portable-executable", {
                "Content-Disposition": 'attachment; filename="' + filename + '"',
                "X-Content-SHA256": digest,
            })
        return self.response(404, {"message": "페이지를 찾을 수 없습니다."})
