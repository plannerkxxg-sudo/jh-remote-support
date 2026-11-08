// RFC 6455 client transport without Windows 8's WinHTTP WebSocket dependency.
// No compression, cookies, credentials, redirects, certificate bypass or TLS downgrade.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JHSupport {
    enum SocketMessageType { Text = 1, Binary = 2, Close = 8 }
    sealed class SocketMessage {
        public readonly SocketMessageType Type;
        public readonly byte[] Bytes;
        public SocketMessage(SocketMessageType type, byte[] bytes) { Type = type; Bytes = bytes; }
    }

    sealed class ManagedWebSocket : IDisposable {
        public const string CompatibilityMarker = "JH_MANAGED_WEBSOCKET_V1";
        const int MaxMessage = 512000, MaxHeader = 16384;
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        readonly TcpClient tcp = new TcpClient();
        readonly SemaphoreSlim writer = new SemaphoreSlim(1, 1);
        Stream stream;
        int aborted, reading, started;
        bool connected;

        public async Task ConnectAsync(Uri uri, CancellationToken token) {
            if (uri == null || !uri.IsAbsoluteUri || uri.UserInfo != "" || uri.Fragment != ""
                    || !(uri.Scheme == "wss" || (uri.Scheme == "ws" && uri.IsLoopback)))
                throw new ArgumentException("공개 서버에는 보안 연결(wss)이 필요합니다.");
            if (Interlocked.Exchange(ref started, 1) != 0) throw new InvalidOperationException("Already connected");
            try {
                using (token.Register(Abort)) {
                    token.ThrowIfCancellationRequested();
                    tcp.NoDelay = true;
                    await tcp.ConnectAsync(uri.DnsSafeHost, uri.Port).ConfigureAwait(false);
                    stream = tcp.GetStream();
                    if (uri.Scheme == "wss") {
                        var tls = new SslStream(stream, false); // Default certificate + hostname validation.
                        stream = tls;
                        // Windows 7's defaults may omit TLS 1.2. Never fall back to TLS 1.0/1.1.
                        Version os = RuntimeRequirements.OsVersion();
                        bool win7 = Environment.OSVersion.Platform == PlatformID.Win32NT
                            && os.Major == 6 && os.Minor == 1;
                        await tls.AuthenticateAsClientAsync(uri.DnsSafeHost, new X509CertificateCollection(),
                            win7 ? SslProtocols.Tls12 : SslProtocols.None, true).ConfigureAwait(false);
                        if (tls.SslProtocol != SslProtocols.Tls12 && (int)tls.SslProtocol != 12288)
                            throw new AuthenticationException("TLS 1.2 이상이 필요합니다.");
                    }
                    byte[] nonce = new byte[16];
                    using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(nonce);
                    string key = Convert.ToBase64String(nonce);
                    string host = uri.IdnHost;
                    if (host.Contains(":")) host = "[" + host.Trim('[', ']') + "]";
                    if (!uri.IsDefaultPort) host += ":" + uri.Port;
                    string request = "GET " + uri.PathAndQuery + " HTTP/1.1\r\nHost: " + host
                        + "\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: " + key
                        + "\r\nSec-WebSocket-Version: 13\r\n\r\n";
                    byte[] bytes = Encoding.ASCII.GetBytes(request);
                    await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
                    string response = await ReadHeader(token).ConfigureAwait(false);
                    ValidateHandshake(response, key);
                    token.ThrowIfCancellationRequested();
                    if (Volatile.Read(ref aborted) != 0) throw new OperationCanceledException();
                    connected = true;
                }
            } catch { Abort(); throw; }
        }

        async Task<string> ReadHeader(CancellationToken token) {
            using (var data = new MemoryStream()) {
                byte[] one = new byte[1];
                int tail = 0;
                while (data.Length < MaxHeader) {
                    if (await stream.ReadAsync(one, 0, 1, token).ConfigureAwait(false) != 1)
                        throw new EndOfStreamException("WebSocket handshake interrupted");
                    byte value = one[0];
                    if (value > 127 || (value < 32 && value != 9 && value != 10 && value != 13))
                        throw new InvalidDataException("Invalid HTTP header");
                    data.WriteByte(value);
                    tail = (tail << 8) | value;
                    if (tail == 0x0D0A0D0A) return Encoding.ASCII.GetString(data.ToArray());
                }
                throw new InvalidDataException("HTTP header too large");
            }
        }
        static bool HasToken(string value, string token) {
            foreach (string part in value.Split(','))
                if (String.Equals(part.Trim(), token, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        static void ValidateHandshake(string response, string key) {
            string[] lines = response.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] status = lines[0].Split(' ');
            if (status.Length < 2 || status[0] != "HTTP/1.1" || status[1] != "101")
                throw new InvalidDataException("서버가 WebSocket 연결을 허용하지 않았습니다.");
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length && lines[i] != ""; i++) {
                int colon = lines[i].IndexOf(':');
                if (colon < 1 || Char.IsWhiteSpace(lines[i][0])) throw new InvalidDataException("Invalid HTTP header");
                string name = lines[i].Substring(0, colon);
                foreach (char ch in name)
                    if (!((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z')
                            || (ch >= '0' && ch <= '9') || "!#$%&'*+-.^_`|~".IndexOf(ch) >= 0))
                        throw new InvalidDataException("Invalid HTTP header name");
                string value = lines[i].Substring(colon + 1).Trim();
                if (headers.ContainsKey(name)) {
                    if (!String.Equals(name, "Connection", StringComparison.OrdinalIgnoreCase)
                            && !String.Equals(name, "Upgrade", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Duplicate HTTP header");
                    headers[name] += "," + value;
                } else headers.Add(name, value);
            }
            string upgrade, connection, accept;
            string expected;
            using (var sha = SHA1.Create()) expected = Convert.ToBase64String(sha.ComputeHash(
                Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            if (!headers.TryGetValue("Upgrade", out upgrade) || !HasToken(upgrade, "websocket")
                    || !headers.TryGetValue("Connection", out connection) || !HasToken(connection, "Upgrade")
                    || !headers.TryGetValue("Sec-WebSocket-Accept", out accept) || accept != expected
                    || headers.ContainsKey("Sec-WebSocket-Extensions") || headers.ContainsKey("Sec-WebSocket-Protocol"))
                throw new InvalidDataException("서버의 WebSocket 응답이 올바르지 않습니다.");
        }

        public Task SendAsync(byte[] bytes, SocketMessageType type, CancellationToken token) {
            if (bytes == null || bytes.Length > MaxMessage || (type != SocketMessageType.Text && type != SocketMessageType.Binary))
                throw new ArgumentException("Invalid WebSocket message");
            if (type == SocketMessageType.Text) StrictUtf8.GetString(bytes);
            return WriteFrame((int)type, bytes, token);
        }
        async Task WriteFrame(int opcode, byte[] payload, CancellationToken token) {
            if (!connected || Volatile.Read(ref aborted) != 0) throw new IOException("WebSocket closed");
            using (token.Register(Abort)) {
                await writer.WaitAsync(token).ConfigureAwait(false);
                try {
                    token.ThrowIfCancellationRequested();
                    byte[] mask = new byte[4];
                    using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(mask);
                    using (var frame = new MemoryStream()) {
                        frame.WriteByte((byte)(0x80 | opcode));
                        int length = payload.Length;
                        if (length < 126) frame.WriteByte((byte)(0x80 | length));
                        else if (length <= 65535) {
                            frame.WriteByte(0xFE); frame.WriteByte((byte)(length >> 8)); frame.WriteByte((byte)length);
                        } else {
                            frame.WriteByte(0xFF);
                            for (int shift = 56; shift >= 0; shift -= 8) frame.WriteByte((byte)((ulong)length >> shift));
                        }
                        frame.Write(mask, 0, 4);
                        for (int i = 0; i < length; i++) frame.WriteByte((byte)(payload[i] ^ mask[i % 4]));
                        byte[] data = frame.ToArray();
                        await stream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
                    }
                } finally { writer.Release(); }
            }
        }
        async Task<byte[]> ReadExact(int length, CancellationToken token) {
            byte[] data = new byte[length];
            int offset = 0;
            while (offset < length) {
                token.ThrowIfCancellationRequested();
                int count = await stream.ReadAsync(data, offset, length - offset, token).ConfigureAwait(false);
                if (count == 0) throw new EndOfStreamException("WebSocket connection lost");
                offset += count;
            }
            return data;
        }
        static bool ValidCloseCode(int code) {
            return (code >= 1000 && code <= 1014 && code != 1004 && code != 1005 && code != 1006)
                || (code >= 3000 && code <= 4999);
        }
        public async Task<SocketMessage> ReceiveAsync(CancellationToken token) {
            if (Interlocked.Exchange(ref reading, 1) != 0) throw new InvalidOperationException("Concurrent receive");
            try {
                using (token.Register(Abort))
                using (var message = new MemoryStream()) {
                    int messageOpcode = 0, fragments = 0;
                    while (true) {
                        if (++fragments > 4096) throw new InvalidDataException("Too many WebSocket fragments");
                        byte[] header = await ReadExact(2, token).ConfigureAwait(false);
                        bool final = (header[0] & 0x80) != 0;
                        int opcode = header[0] & 15;
                        bool control = opcode >= 8;
                        if ((header[0] & 0x70) != 0 || (header[1] & 0x80) != 0
                                || !(opcode == 0 || opcode == 1 || opcode == 2 || opcode == 8 || opcode == 9 || opcode == 10))
                            throw new InvalidDataException("Invalid WebSocket frame");
                        ulong length = (ulong)(header[1] & 127);
                        if (control && (!final || length > 125)) throw new InvalidDataException("Invalid control frame");
                        if (length == 126 || length == 127) {
                            bool longLength = length == 127;
                            byte[] size = await ReadExact(longLength ? 8 : 2, token).ConfigureAwait(false);
                            if (longLength && (size[0] & 0x80) != 0) throw new InvalidDataException("Invalid frame size");
                            length = 0;
                            foreach (byte value in size) length = (length << 8) | value;
                            if ((!longLength && length < 126) || (longLength && length < 65536))
                                throw new InvalidDataException("Non-minimal frame size");
                        }
                        if (length > MaxMessage || (!control && length + (ulong)message.Length > MaxMessage))
                            throw new InvalidDataException("WebSocket message too large");
                        byte[] payload = await ReadExact((int)length, token).ConfigureAwait(false);
                        if (opcode == 8) {
                            if (payload.Length == 1) throw new InvalidDataException("Invalid close frame");
                            if (payload.Length >= 2) {
                                if (!ValidCloseCode((payload[0] << 8) | payload[1])) throw new InvalidDataException("Invalid close code");
                                StrictUtf8.GetString(payload, 2, payload.Length - 2);
                            }
                            using (var close = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                                close.CancelAfter(5000);
                                await WriteFrame(8, payload, close.Token).ConfigureAwait(false);
                            }
                            Abort();
                            return new SocketMessage(SocketMessageType.Close, payload);
                        }
                        if (opcode == 9) {
                            using (var pong = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                                pong.CancelAfter(5000);
                                await WriteFrame(10, payload, pong.Token).ConfigureAwait(false);
                            }
                            continue;
                        }
                        if (opcode == 10) continue;
                        if (opcode == 0) {
                            if (messageOpcode == 0) throw new InvalidDataException("Unexpected continuation");
                        } else {
                            if (messageOpcode != 0) throw new InvalidDataException("Interleaved messages");
                            messageOpcode = opcode;
                        }
                        message.Write(payload, 0, payload.Length);
                        if (!final) continue;
                        byte[] bytes = message.ToArray();
                        if (messageOpcode == 1) StrictUtf8.GetString(bytes);
                        return new SocketMessage((SocketMessageType)messageOpcode, bytes);
                    }
                }
            } catch { Abort(); throw; }
            finally { Interlocked.Exchange(ref reading, 0); }
        }
        public void Abort() {
            if (Interlocked.Exchange(ref aborted, 1) != 0) return;
            // Closing the underlying socket interrupts .NET Framework async TLS reads too.
            try { tcp.Close(); } catch (Exception) { }
            try { if (stream != null) stream.Dispose(); } catch (Exception) { }
        }
        public void Dispose() { Abort(); }
    }
}
