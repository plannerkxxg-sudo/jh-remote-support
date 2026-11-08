// Headless test entry point, excluded from the distributed application build.
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace JHSupport {
    static class TransportHarness {
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        public static int Main(string[] args) {
            try { Run(args).GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) {
                Console.WriteLine(Json.Serialize(new { error = ex.GetBaseException().GetType().Name, message = ex.GetBaseException().Message }));
                return 2;
            }
        }
        static async Task<SocketMessage> Read(ManagedWebSocket socket) {
            using (var timeout = new CancellationTokenSource(5000)) return await socket.ReceiveAsync(timeout.Token);
        }
        static Task SendJson(ManagedWebSocket socket, object value) {
            return socket.SendAsync(Encoding.UTF8.GetBytes(Json.Serialize(value)), SocketMessageType.Text, CancellationToken.None);
        }
        static string Type(SocketMessage value) {
            return Convert.ToString(Json.Deserialize<System.Collections.Generic.Dictionary<string, object>>(Encoding.UTF8.GetString(value.Bytes))["type"]);
        }
        static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Test failed"); }
        static async Task Run(string[] args) {
            if (args[0] == "paths") {
                Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
                Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); return;
            }
            if (args[0] == "trust") {
                var store = new System.Security.Cryptography.X509Certificates.X509Store(
                    System.Security.Cryptography.X509Certificates.StoreName.Root,
                    System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser);
                store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadWrite);
                store.Add(new System.Security.Cryptography.X509Certificates.X509Certificate2(args[1]));
                store.Close(); Console.WriteLine("trusted-test-ca"); return;
            }
            if (args[0] == "requirements") {
                Require(RuntimeRequirements.Verify()); Console.WriteLine("requirements-ok"); return;
            }
            using (var socket = new ManagedWebSocket()) {
                using (var connect = new CancellationTokenSource(args[0] == "timeout" ? 200 : 5000))
                    await socket.ConnectAsync(new Uri(args[1]), connect.Token);
                if (args[0] == "echo") {
                    byte[] data;
                    if (args[2].StartsWith("size:")) {
                        data = new byte[Int32.Parse(args[2].Substring(5))];
                        for (int i = 0; i < data.Length; i++) data[i] = (byte)'x';
                    } else data = Convert.FromBase64String(args[2]);
                    await socket.SendAsync(data, args[3] == "text" ? SocketMessageType.Text : SocketMessageType.Binary, CancellationToken.None);
                } else if (args[0] == "concurrent") {
                    var tasks = new Task[24];
                    for (int i = 0; i < tasks.Length; i++) tasks[i] = socket.SendAsync(Encoding.UTF8.GetBytes("message-" + i), SocketMessageType.Text, CancellationToken.None);
                    await Task.WhenAll(tasks);
                    for (int i = 0; i < tasks.Length; i++) await Read(socket);
                    Console.WriteLine("concurrent-ok"); return;
                } else if (args[0] == "cancel") {
                    using (var cancel = new CancellationTokenSource(200)) await socket.ReceiveAsync(cancel.Token);
                    throw new InvalidOperationException("Receive did not cancel");
                } else if (args[0] == "relay") {
                    await SendJson(socket, new { type = "create", name = "아진쌤", description = "수강신청 안내" });
                    SocketMessage created = await Read(socket);
                    Require(Type(created) == "created");
                    string code = Convert.ToString(Json.Deserialize<System.Collections.Generic.Dictionary<string, object>>(Encoding.UTF8.GetString(created.Bytes))["code"]);
                    using (var student = new ManagedWebSocket()) {
                        using (var timeout = new CancellationTokenSource(5000)) await student.ConnectAsync(new Uri(args[1]), timeout.Token);
                        await SendJson(student, new { type = "join", code = code });
                        Require(Type(await Read(socket)) == "waiting");
                        Require(Type(await Read(student)) == "request");
                        await SendJson(student, new { type = "accept" });
                        Require(Type(await Read(student)) == "active");
                        Require(Type(await Read(socket)) == "active");
                        byte[] screen = new byte[] {255,216,0,1,2,255,217};
                        await student.SendAsync(screen, SocketMessageType.Binary, CancellationToken.None);
                        Require(Convert.ToBase64String((await Read(socket)).Bytes) == Convert.ToBase64String(screen));
                        await SendJson(socket, new { type = "input", @event = "key", key = 65, down = true });
                        Require(Type(await Read(student)) == "input");
                    }
                    Console.WriteLine("relay-ok"); return;
                }
                SocketMessage result = await Read(socket);
                Console.WriteLine(Json.Serialize(new { type = (int)result.Type, data = Convert.ToBase64String(result.Bytes) }));
            }
        }
    }
}
