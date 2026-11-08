using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: AssemblyTitle("JH Remote Support")]
[assembly: AssemblyProduct("JH Remote Support")]
[assembly: AssemblyDescription("Windows remote support with visible, per-session user consent")]
[assembly: AssemblyVersion("0.10.0.0")]
[assembly: AssemblyFileVersion("0.10.0.0")]

namespace JHSupport {
    static class NetworkPolicy {
        public static void Configure() {
            ServicePointManager.CheckCertificateRevocationList = true;
            // Standalone csc builds otherwise inherit legacy .NET TLS defaults.
            // Apply before any System.Net connection is created.
            AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", false);
            AppContext.SetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions", false);
            Version os = RuntimeRequirements.OsVersion();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && os.Major == 6 && os.Minor == 1)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }
        public static bool Verify() {
            bool strong, defaults;
            return AppContext.TryGetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", out strong) && !strong
                && AppContext.TryGetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions", out defaults) && !defaults
                && (ServicePointManager.SecurityProtocol == (SecurityProtocolType)0
                    || (Environment.OSVersion.Platform == PlatformID.Win32NT && RuntimeRequirements.OsVersion().Major == 6
                        && RuntimeRequirements.OsVersion().Minor == 1 && ServicePointManager.SecurityProtocol == SecurityProtocolType.Tls12));
        }
    }

    static class Program {
        [STAThread] public static void Main(string[] args) {
            // Keep this entry point on .NET 4.0 APIs; run modern code only after checking prerequisites.
            bool checkOnly = args.Length == 1 && (args[0] == "--verify-network-policy"
                || args[0] == "--verify-profile" || args[0] == "--verify-setup");
            if (!checkOnly) {
                string requirement = RuntimeRequirements.Check();
                if (requirement != null) {
                    MessageBox.Show(requirement, "실행 환경 확인");
                    return;
                }
            }
            try { Run(args); }
            catch (Exception ex) {
                MessageBox.Show("프로그램을 시작하지 못했습니다.\n" + ex.GetBaseException().GetType().Name
                    + RuntimeRequirements.SaveError(ex), "원격지원 실행 오류");
            }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void Run(string[] args) {
            NetworkPolicy.Configure();
            if (args.Length == 2 && args[0] == "--apply-update") { AutoUpdater.Apply(args[1]); return; }
            if (args.Length == 1 && args[0] == "--verify-network-policy") {
                Environment.ExitCode = NetworkPolicy.Verify() ? 0 : 1;
                return;
            }
            if (args.Length == 1 && args[0] == "--verify-profile") {
                Environment.ExitCode = DownloadProfile.Load() == null ? 1 : 0;
                return;
            }
            if (args.Length == 1 && args[0] == "--verify-setup") {
                bool valid = ConnectionSettings.Normalize("https://alice.onrender.com/") == "wss://alice.onrender.com/support"
                    && ConnectionSettings.Normalize("wss://bob.onrender.com/support") == "wss://bob.onrender.com/support"
                    && ConnectionSettings.Normalize("http://127.0.0.1:8765/") == "ws://127.0.0.1:8765/support"
                    && ConnectionSettings.Normalize("http://remote.example/") == null
                    && ConnectionSettings.Normalize("https://user:password@remote.example/") == null
                    && ConnectionSettings.Normalize("https://remote.example/?token=bad") == null
                    && Branding.Verify() && DownloadProfile.VerifyStableNames() && RuntimeRequirements.Verify();
                Environment.ExitCode = valid ? 0 : 1;
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (DownloadProfile.IsStudentFilename() && DownloadProfile.Load() == null) {
                MessageBox.Show("학생용 파일 이름에서 접속 정보를 읽지 못했습니다. 홈페이지에서 새 파일을 받아 이름을 바꾸지 않고 실행하세요.", "원격지원 접속 정보");
                return;
            }
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) {
                foreach (Form window in Application.OpenForms) {
                    var support = window as SupportForm;
                    if (support != null) support.StopForError();
                }
                MessageBox.Show("프로그램 오류로 지원을 종료합니다.\n" + e.Exception.GetBaseException().GetType().Name
                    + RuntimeRequirements.SaveError(e.Exception), "원격지원 오류");
                Application.Exit();
            };
            Application.Run(new SupportForm());
        }
    }

    static class Branding {
        public const string DefaultDescription = "화면을 보며 홈페이지 가입과 신청 절차를 도와드립니다.";
        public static string Clean(string value) {
            return Regex.Replace(value ?? "", @"[\p{Cc}\p{Cf}]", "").Trim();
        }
        public static string Title(string name) {
            string value = Clean(name);
            return value == "" ? "원격지원" : value + " 원격지원";
        }
        public static string Description(string value) {
            value = Clean(value);
            return value == "" ? DefaultDescription : value;
        }
        public static string PageUrl(string relayAddress, string name, string description) {
            var relay = new Uri(relayAddress);
            return (relay.Scheme == "wss" ? "https://" : "http://") + relay.Authority
                + "/?name=" + Uri.EscapeDataString(Clean(name))
                + "&description=" + Uri.EscapeDataString(Description(description));
        }
        public static string StudentNotice(string name, string description) {
            string owner = Clean(name);
            if (owner == "") owner = "담당자";
            return Description(description) + "\n\n승인하면 " + owner + "에게 화면이 열립니다.\n"
                + "화면과 마우스·키보드 조작을 공유합니다.\n언제든 위의 ‘즉시 종료’로 멈출 수 있습니다.";
        }
        public static string Invitation(string relayAddress, string name, string description, string code) {
            string title = Title(name);
            string intro = Description(description);
            bool same = String.Equals(Regex.Replace(title, @"\s+", ""),
                Regex.Replace(intro, @"\s+", ""), StringComparison.Ordinal);
            return title + (same ? "" : "\n" + intro)
                + "\n접속 주소: " + PageUrl(relayAddress, name, description)
                + "\n접속 코드: " + code
                + "\n\n코드 입력 → 다운로드한 파일 실행 → 지원 승인 순서로 진행해주세요.";
        }
        public static bool Verify() {
            string page = PageUrl("wss://alice.onrender.com/support", "민주쌤", "가입 & 신청 <안내>");
            string duplicate = Invitation("wss://alice.onrender.com/support", "정현쌤", "정현쌤 원격지원", "000007");
            string spaced = Invitation("wss://alice.onrender.com/support", "아진쌤", " 아진쌤  원격지원 ", "000007");
            string distinct = Invitation("wss://alice.onrender.com/support", "민주쌤", "수강신청을 도와드립니다.", "000007");
            return Title("민주쌤") == "민주쌤 원격지원" && Title("아진쌤") == "아진쌤 원격지원"
                && Title("") == "원격지원" && Clean("민주\n쌤") == "민주쌤"
                && page.StartsWith("https://alice.onrender.com/?name=")
                && page.Contains(Uri.EscapeDataString("민주쌤"))
                && page.Contains("%26") && page.Contains("%3C")
                && StudentNotice("아진쌤", "신청 안내").Contains("아진쌤에게")
                && Description("") == DefaultDescription
                && duplicate.StartsWith("정현쌤 원격지원\n접속 주소: ")
                && duplicate.Contains("\n접속 코드: 000007\n")
                && spaced.StartsWith("아진쌤 원격지원\n접속 주소: ")
                && distinct.StartsWith("민주쌤 원격지원\n수강신청을 도와드립니다.\n접속 주소: ");
        }
    }

    static class ConnectionSettings {
        static readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JHRemoteSupport", "settings.json");
        public static string Normalize(string value) {
            if (String.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            if (!value.Contains("://")) value = "https://" + value;
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || String.IsNullOrEmpty(uri.Host)
                    || uri.UserInfo != "" || uri.Query != "" || uri.Fragment != ""
                    || (uri.AbsolutePath != "/" && uri.AbsolutePath != "/support")) return null;
            bool secure = uri.Scheme == "https" || uri.Scheme == "wss";
            bool local = (uri.Scheme == "http" || uri.Scheme == "ws") && uri.IsLoopback;
            if (!secure && !local) return null;
            var target = new UriBuilder(secure ? "wss" : "ws", uri.Host, uri.IsDefaultPort ? -1 : uri.Port, "/support");
            return target.Uri.AbsoluteUri;
        }
        public static Dictionary<string, object> Load() {
            try {
                if (!File.Exists(path)) return null;
                var value = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
                if (value == null || !value.ContainsKey("relayUrl") || Normalize(Convert.ToString(value["relayUrl"])) == null) return null;
                return value;
            } catch (Exception) { return null; }
        }
        public static void Save(string relay, string name, string description) {
            string url = Normalize(relay);
            if (url == null) throw new InvalidDataException("접속 주소가 올바르지 않습니다.");
            name = Branding.Clean(name); description = Branding.Clean(description);
            if (name.Length < 1 || name.Length > 40 || description.Length > 200)
                throw new InvalidDataException("이름은 1~40자, 소개 문구는 200자 이내로 입력하세요.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(new { relayUrl = url, displayName = name, description = description }), Encoding.UTF8);
        }
    }

    sealed class ConnectionDialog : Form {
        readonly TextBox site = new TextBox(), displayName = new TextBox(), description = new TextBox();
        public string RelayUrl { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public ConnectionDialog(string relay, string name, string intro) {
            Text = "처음 한 번 · 내 원격지원 이름과 사이트 설정";
            Size = new Size(620, 560); MinimumSize = MaximumSize = Size;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = MaximizeBox = false;
            Font = new Font("맑은 고딕", 10);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 9, ColumnCount = 1, Padding = new Padding(20) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "이름과 소개 문구를 한 번 저장하세요.\n프로그램 제목과 학생에게 보내는 사이트·안내에 적용됩니다." }, 0, 0);
            layout.Controls.Add(new Label { Text = "내 사이트 주소 (예: https://내사이트.onrender.com)", Dock = DockStyle.Fill }, 0, 1);
            site.Dock = DockStyle.Fill; site.Text = relay == null ? "" : relay.Replace("wss://", "https://").Replace("ws://", "http://").Replace("/support", "/");
            site.MaxLength = 1024; layout.Controls.Add(site, 0, 2);
            layout.Controls.Add(new Label { Text = "내 이름 (예: 민주쌤, 아진쌤)", Dock = DockStyle.Fill }, 0, 3);
            displayName.Dock = DockStyle.Fill; displayName.MaxLength = 40; displayName.Text = name;
            layout.Controls.Add(displayName, 0, 4);
            layout.Controls.Add(new Label { Text = "사이트 소개 문구 (선택, 최대 200자)", Dock = DockStyle.Fill }, 0, 5);
            description.Dock = DockStyle.Fill; description.Multiline = true; description.MaxLength = 200;
            description.Text = intro; layout.Controls.Add(description, 0, 6);
            var preview = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(15, 118, 110) };
            Action refresh = delegate { preview.Text = "표시 제목: " + Branding.Title(displayName.Text); };
            displayName.TextChanged += delegate { refresh(); }; refresh(); layout.Controls.Add(preview, 0, 7);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
            var save = new Button { Text = "저장", Width = 90, Height = 32 };
            var cancel = new Button { Text = "취소", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
            save.Click += delegate {
                string normalized = ConnectionSettings.Normalize(site.Text);
                if (normalized == null || Branding.Clean(displayName.Text) == "") {
                    MessageBox.Show(this, "본인 사이트의 HTTPS 주소와 표시 이름을 입력하세요."); return;
                }
                RelayUrl = normalized; DisplayName = Branding.Clean(displayName.Text);
                Description = Branding.Clean(description.Text); DialogResult = DialogResult.OK;
            };
            buttons.Controls.Add(save); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, 8);
            Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
        }
    }

    static class DownloadProfile {
        // Remains in the compiled release. The server refuses old EXE templates.
        public const string StableFormat = "JH_STABLE_DOWNLOAD_V1";
        public static bool IsStudentFilename() {
            return Path.GetFileName(Assembly.GetExecutingAssembly().Location).StartsWith("JHSupport-", StringComparison.OrdinalIgnoreCase);
        }
        public static Dictionary<string, object> FromFilename(string filename) {
            var match = Regex.Match(Path.GetFileName(filename),
                @"^JHSupport-(?<code>[0-9]{6})-(?<kind>tls|local)-(?<host>[a-z0-9.-]+)(?:~(?<port>[0-9]{1,5}))?(?: \([0-9]+\))?\.exe$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) return null;
            string hostname = match.Groups["host"].Value.ToLowerInvariant();
            if (hostname.Length > 180) return null;
            foreach (string label in hostname.Split('.'))
                if (!Regex.IsMatch(label, @"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")) return null;
            bool local = match.Groups["kind"].Value.Equals("local", StringComparison.OrdinalIgnoreCase);
            if (local && hostname != "localhost" && hostname != "127.0.0.1") return null;
            int port = -1;
            if (match.Groups["port"].Success && (!Int32.TryParse(match.Groups["port"].Value, out port) || port < 1 || port > 65535)) return null;
            Uri uri;
            try { uri = new UriBuilder(local ? "ws" : "wss", hostname, port, "/support").Uri; }
            catch (UriFormatException) { return null; }
            return new Dictionary<string, object> {
                {"mode", "host"}, {"code", match.Groups["code"].Value}, {"relayUrl", uri.AbsoluteUri},
                {"displayName", ""}, {"description", ""}, {"format", StableFormat}
            };
        }
        public static bool VerifyStableNames() {
            var secure = FromFilename("JHSupport-000007-tls-alice.onrender.com (2).exe");
            var local = FromFilename("JHSupport-123456-local-127.0.0.1~8765.exe");
            var port = FromFilename("JHSupport-123456-tls-alice.example~8443.exe");
            return secure != null && Convert.ToString(secure["code"]) == "000007"
                && Convert.ToString(secure["relayUrl"]) == "wss://alice.onrender.com/support"
                && local != null && Convert.ToString(local["relayUrl"]) == "ws://127.0.0.1:8765/support"
                && port != null && Convert.ToString(port["relayUrl"]) == "wss://alice.example:8443/support"
                && FromFilename("JHSupport-123456-local-remote.example.exe") == null
                && FromFilename("JHSupport-123456-tls-bad..example.exe") == null
                && FromFilename("JHSupport-123456-tls-remote.example~0.exe") == null
                && FromFilename("JHSupport-123456-tls-remote.example~65536.exe") == null
                && FromFilename("JHSupport-123456-tls--bad.example.exe") == null
                && FromFilename("JHSupport-12A456-tls-alice.example.exe") == null
                && FromFilename("JHSupport-123456-tls-alice.example.exe.bat") == null
                && FromFilename("JHRemoteSupport.exe") == null;
        }
        public static Dictionary<string, object> Load() {
            var stable = FromFilename(Assembly.GetExecutingAssembly().Location);
            if (stable != null) return stable;
            // Read old v0.7 profiles for compatibility; new downloads never append them.
            try {
                byte[] marker = Encoding.ASCII.GetBytes("JHCONFIGv1");
                using (var file = File.Open(Assembly.GetExecutingAssembly().Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    if (file.Length < marker.Length + 4) return null;
                    file.Seek(-marker.Length, SeekOrigin.End);
                    byte[] actual = new byte[marker.Length];
                    if (file.Read(actual, 0, actual.Length) != actual.Length) return null;
                    for (int i = 0; i < marker.Length; i++) if (marker[i] != actual[i]) return null;
                    file.Seek(-marker.Length - 4, SeekOrigin.End);
                    byte[] size = new byte[4];
                    if (file.Read(size, 0, 4) != 4) return null;
                    int length = BitConverter.ToInt32(size, 0);
                    if (length < 1 || length > 4096 || length > file.Length - marker.Length - 4) return null;
                    file.Seek(-marker.Length - 4 - length, SeekOrigin.End);
                    byte[] bytes = new byte[length];
                    if (file.Read(bytes, 0, length) != length) return null;
                    var profile = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(bytes));
                    if (profile == null || !profile.ContainsKey("mode") || Convert.ToString(profile["mode"]) != "host" || !profile.ContainsKey("code") || !profile.ContainsKey("relayUrl")) return null;
                    if (!Regex.IsMatch(Convert.ToString(profile["code"]), "^[0-9]{6}$")) return null;
                    Uri uri;
                    if (!Uri.TryCreate(Convert.ToString(profile["relayUrl"]), UriKind.Absolute, out uri)
                            || uri.AbsolutePath != "/support" || uri.UserInfo != "" || uri.Query != "" || uri.Fragment != ""
                            || !(uri.Scheme == "wss" || (uri.Scheme == "ws" && uri.IsLoopback))) return null;
                    return profile;
                }
            } catch (Exception) { return null; }
        }
    }

    sealed class WireMessage {
        public byte[] Bytes; public SocketMessageType Type;
        public WireMessage(byte[] bytes, SocketMessageType type) { Bytes = bytes; Type = type; }
    }

    sealed class Session : IDisposable {
        readonly ManagedWebSocket socket = new ManagedWebSocket();
        readonly CancellationTokenSource cancel = new CancellationTokenSource();
        readonly BlockingCollection<WireMessage> outgoing = new BlockingCollection<WireMessage>(256);
        readonly JavaScriptSerializer json = new JavaScriptSerializer();
        public event Action<Dictionary<string, object>> Message;
        public event Action<byte[]> Frame;
        public event Action<string> Closed;
        int disposed;
        public async Task Connect(Uri uri) {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token)) {
                deadline.CancelAfter(10000);
                await socket.ConnectAsync(uri, deadline.Token);
            }
            Task.Run((Func<Task>)SendLoop);
            Task.Run((Func<Task>)ReceiveLoop);
        }
        public void Send(object message) {
            if (Volatile.Read(ref disposed) != 0) return;
            string text;
            lock (json) { text = json.Serialize(message); }
            try {
                if (!outgoing.TryAdd(new WireMessage(Encoding.UTF8.GetBytes(text), SocketMessageType.Text)))
                    Fail("입력 전송이 지연되어 연결을 종료했습니다.");
            } catch (InvalidOperationException) { }
        }
        public void SendFrame(byte[] bytes) {
            if (bytes.Length > 512000 || outgoing.Count > 2 || Volatile.Read(ref disposed) != 0) return;
            try { outgoing.TryAdd(new WireMessage(bytes, SocketMessageType.Binary)); }
            catch (InvalidOperationException) { }
        }
        async Task SendLoop() {
            try {
                foreach (WireMessage item in outgoing.GetConsumingEnumerable(cancel.Token)) {
                    using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token)) {
                        deadline.CancelAfter(5000);
                        await socket.SendAsync(item.Bytes, item.Type, deadline.Token);
                    }
                }
            } catch (Exception) { Fail("서버 연결이 끊겼습니다."); }
        }
        async Task ReceiveLoop() {
            try {
                while (!cancel.IsCancellationRequested) {
                    SocketMessage result = await socket.ReceiveAsync(cancel.Token);
                    if (result.Type == SocketMessageType.Close) { Fail("원격지원이 종료되었습니다."); return; }
                    if (result.Type == SocketMessageType.Binary) {
                        if (Frame != null) Frame(result.Bytes);
                    } else {
                        Dictionary<string, object> message;
                        lock (json) { message = json.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(result.Bytes)); }
                        if (Message != null && message != null) Message(message);
                    }
                }
            } catch (Exception) { Fail("서버 연결이 끊겼거나 잘못된 데이터를 받았습니다."); }
        }
        void Fail(string reason) {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            cancel.Cancel(); outgoing.CompleteAdding(); socket.Abort();
            if (Closed != null) Closed(reason);
        }
        public void Dispose() { Fail("연결을 종료했습니다."); }
    }

    sealed class ScreenView : PictureBox {
        public ScreenView() {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true; SizeMode = PictureBoxSizeMode.Zoom;
            BackColor = Color.FromArgb(15, 23, 42);
        }
        protected override bool IsInputKey(Keys keyData) { return true; }
        protected override bool IsInputChar(char charCode) { return true; }
    }

    sealed class SupportForm : Form {
        readonly TextBox code = new TextBox(), name = new TextBox();
        string relayAddress = "";
        string siteDescription = "";
        bool brandingSetupRequired;
        readonly TextBox studentNotice = new TextBox { Multiline = true, ReadOnly = true,
            BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, TabStop = false };
        readonly Button host = new Button(), join = new Button(), stop = new Button(), copy = new Button();
        readonly Button settings = new Button();
        readonly Label status = new Label(), hint = new Label();
        readonly ScreenView viewer = new ScreenView();
        readonly System.Windows.Forms.Timer capture = new System.Windows.Forms.Timer();
        readonly Button updateButton = new Button();
        readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer { Interval = 1800000 };
        bool checkingUpdate, applyingUpdate;
        readonly HashSet<int> heldKeys = new HashSet<int>();
        readonly HashSet<string> heldButtons = new HashSet<string>();
        Session session;
        bool hosting, approved, hotkeyRegistered;
        string stopHotkey = "";
        Rectangle remoteBounds;
        long lastMove;
        byte[] latestFrame;
        bool frameScheduled;
        readonly object frameLock = new object();
        readonly Dictionary<string, object> downloadProfile = DownloadProfile.Load();

        public SupportForm() {
            Native.SetProcessDPIAware();
            Text = "원격지원 · v0.10 배포 테스트";
            Size = new Size(1120, 800); MinimumSize = new Size(820, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("맑은 고딕", 10);
            BackColor = Color.FromArgb(245, 247, 250);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(16) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            SetupButton(host, "지원받기", 112); SetupButton(join, "코드 발급", 112);
            SetupButton(copy, "안내 복사", 100); SetupButton(stop, "즉시 종료", 110);
            code.Width = 170; code.Height = 32; code.MaxLength = 6; code.Margin = new Padding(6, 7, 6, 0); code.CharacterCasing = CharacterCasing.Upper;
            name.Width = 105; name.MaxLength = 40; name.ReadOnly = true; name.Margin = new Padding(0, 7, 6, 0);
            actions.Controls.Add(name); actions.Controls.Add(join); actions.Controls.Add(code); actions.Controls.Add(copy);
            actions.Controls.Add(host); actions.Controls.Add(stop);
            root.Controls.Add(actions, 0, 0);
            status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft;
            status.ForeColor = Color.FromArgb(15, 118, 110);
            status.Text = "지원자는 코드 발급, 지원받는 사람은 코드 입력 후 지원받기를 누르세요.";
            root.Controls.Add(status, 0, 1); viewer.Dock = DockStyle.Fill;
            root.Controls.Add(viewer, 0, 2);
            hint.Text = "상대방 승인 후 연결됩니다. 화면을 클릭하면 키보드 입력이 전달됩니다.";
            hint.Dock = DockStyle.Fill; hint.TextAlign = ContentAlignment.MiddleLeft;
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            updateButton.Text = "업데이트 확인"; updateButton.Width = 118; updateButton.Height = 30;
            updateButton.Anchor = AnchorStyles.Right;
            updateButton.Click += async delegate { await CheckUpdates(true); };
            updateTimer.Tick += async delegate { await CheckUpdates(false); };
            settings.Text = "연결 설정"; settings.Width = 92; settings.Height = 30; settings.Anchor = AnchorStyles.Right;
            footer.Controls.Add(hint, 0, 0); footer.Controls.Add(updateButton, 1, 0); footer.Controls.Add(settings, 2, 0);
            root.Controls.Add(footer, 0, 3); Controls.Add(root);
            settings.Click += delegate { ConfigureConnection(); };
            copy.Enabled = false; stop.Enabled = false;
            host.Click += async delegate { await Start(true); };
            join.Click += async delegate { await Start(false); };
            stop.Click += delegate { End("원격지원을 종료했습니다."); };
            copy.Click += delegate {
                try {
                    Clipboard.SetText(Branding.Invitation(relayAddress, name.Text, siteDescription, code.Text));
                    status.Text = "웹주소와 코드를 복사했습니다. 지원받는 사람에게 전달하세요.";
                }
                catch (Exception) { status.Text = "복사하지 못했습니다. 코드를 직접 전달하세요."; }
            };
            viewer.MouseMove += delegate(object sender, MouseEventArgs e) {
                long now = Environment.TickCount & Int32.MaxValue;
                if (now - lastMove < 25 && now >= lastMove) return;
                lastMove = now; Pointer("move", e);
            };
            viewer.MouseDown += delegate(object sender, MouseEventArgs e) {
                viewer.Focus(); Pointer("down", e); viewer.Capture = true;
            };
            viewer.MouseUp += delegate(object sender, MouseEventArgs e) { Pointer("up", e); viewer.Capture = false; };
            viewer.MouseWheel += delegate(object sender, MouseEventArgs e) { Pointer("wheel", e); };
            viewer.KeyDown += delegate(object sender, KeyEventArgs e) { Key(e, true); };
            viewer.KeyUp += delegate(object sender, KeyEventArgs e) { Key(e, false); };
            viewer.KeyPress += delegate(object sender, KeyPressEventArgs e) { e.Handled = true; };
            viewer.LostFocus += delegate { if (approved && !hosting && session != null) session.Send(new { type = "release" }); };
            Deactivate += delegate { if (approved && !hosting && session != null) session.Send(new { type = "release" }); };
            capture.Interval = 160;
            capture.Tick += delegate { CaptureScreen(); };
            FormClosing += delegate { updateTimer.Stop(); End("종료되었습니다."); };
            Shown += async delegate {
                foreach (Keys candidate in new[] { Keys.F10, Keys.F11, Keys.F9 }) {
                    if (Native.RegisterHotKey(Handle, 9001, 0x0001 | 0x0002 | 0x4000, (int)candidate)) {
                        hotkeyRegistered = true;
                        stopHotkey = "Ctrl + Alt + " + candidate.ToString();
                        break;
                    }
                }
                hint.Text = hotkeyRegistered ? "종료 단축키: " + stopHotkey : "종료 단축키를 사용할 수 없습니다. ‘즉시 종료’ 버튼을 사용하세요.";
                if (downloadProfile != null) await Start(true);
                else {
                    if (String.IsNullOrEmpty(relayAddress) || brandingSetupRequired) ConfigureConnection();
                    updateTimer.Start();
                    await CheckUpdates(false);
                }
            };
            FormClosed += delegate { if (hotkeyRegistered) Native.UnregisterHotKey(Handle, 9001); };
            if (downloadProfile != null) {
                relayAddress = Get(downloadProfile, "relayUrl");
                code.Text = Get(downloadProfile, "code");
                siteDescription = Get(downloadProfile, "description");
                Text = Branding.Title(Get(downloadProfile, "displayName")) + " · 지원받기 (v0.10 테스트)";
                Size = new Size(620, 400); MinimumSize = new Size(560, 360);
                actions.Controls.Remove(name); actions.Controls.Remove(join); actions.Controls.Remove(code);
                actions.Controls.Remove(copy); actions.Controls.Remove(host);
                settings.Visible = false; updateButton.Visible = false;
                root.Controls.Remove(viewer);
                studentNotice.Dock = DockStyle.Fill; studentNotice.TextAlign = HorizontalAlignment.Center;
                studentNotice.BackColor = BackColor;
                studentNotice.Text = Branding.StudentNotice(Get(downloadProfile, "displayName"), siteDescription);
                root.Controls.Add(studentNotice, 0, 2);
                status.Text = "접속 요청을 준비하고 있습니다…";
            } else {
                var stored = ConnectionSettings.Load();
                if (stored != null) {
                    relayAddress = ConnectionSettings.Normalize(Get(stored, "relayUrl"));
                    if (!String.IsNullOrWhiteSpace(Get(stored, "displayName"))) name.Text = Get(stored, "displayName");
                    siteDescription = Get(stored, "description");
                    brandingSetupRequired = !stored.ContainsKey("description");
                    Text = Branding.Title(name.Text) + " · v0.10 배포 테스트";
                }
            }
        }
        async Task CheckUpdates(bool manual) {
            if (downloadProfile != null || IsDisposed || applyingUpdate || checkingUpdate) return;
            if (session != null) {
                if (manual) MessageBox.Show(this, "원격지원 대기를 취소하거나 지원을 종료한 뒤 업데이트하세요.");
                return;
            }
            checkingUpdate = true; updateButton.Enabled = false;
            string staged = null;
            try {
                UpdateRelease release = await AutoUpdater.CheckAsync(manual);
                if (IsDisposed || session != null) return;
                if (release == null) {
                    if (manual) MessageBox.Show(this, "현재 최신 버전입니다. v" + AutoUpdater.CurrentVersion);
                    return;
                }
                status.Text = "새 버전을 다운로드하고 확인하는 중입니다…";
                staged = await AutoUpdater.StageAsync(release);
                if (IsDisposed || session != null) return;
                applyingUpdate = true;
                AutoUpdater.Launch(staged);
                staged = null;
                Close();
            } catch (Exception ex) {
                applyingUpdate = false;
                RuntimeRequirements.SaveError(ex);
                if (manual && !IsDisposed) MessageBox.Show(this,
                    "업데이트를 적용하지 못했습니다. 기존 프로그램은 그대로 사용할 수 있습니다.\n"
                    + "새 버전이 아직 발행되지 않았거나 연결/파일 확인에 실패했을 수 있습니다.\n"
                    + ex.GetBaseException().GetType().Name, "업데이트 확인");
                if (!IsDisposed && session == null) status.Text = "업데이트를 적용하지 않았습니다. 기존 버전으로 이용할 수 있습니다.";
            } finally {
                AutoUpdater.Discard(staged);
                checkingUpdate = false;
                if (!IsDisposed) updateButton.Enabled = true;
            }
        }
        bool ConfigureConnection() {
            if (downloadProfile != null || session != null) return false;
            using (var dialog = new ConnectionDialog(relayAddress, name.Text, siteDescription)) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                try {
                    ConnectionSettings.Save(dialog.RelayUrl, dialog.DisplayName, dialog.Description);
                    relayAddress = dialog.RelayUrl; name.Text = dialog.DisplayName;
                    siteDescription = dialog.Description; brandingSetupRequired = false;
                    Text = Branding.Title(name.Text) + " · v0.10 배포 테스트";
                    status.Text = "이름과 소개 문구를 저장했습니다. 안내 복사로 개인화 사이트를 전달하세요.";
                    return true;
                } catch (Exception) {
                    MessageBox.Show(this, "설정을 저장하지 못했습니다. 사용자 폴더의 쓰기 권한을 확인하세요."); return false;
                }
            }
        }
        void SetupButton(Button b, string text, int width) {
            b.Text = text; b.Width = width; b.Height = 36; b.Margin = new Padding(0, 4, 8, 0);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = b == stop ? Color.FromArgb(185, 28, 28) : Color.FromArgb(15, 118, 110);
            b.ForeColor = Color.White;
        }
        static string Get(Dictionary<string, object> d, string key) { return d.ContainsKey(key) ? Convert.ToString(d[key]) : ""; }
        void UI(Action action) {
            if (!IsDisposed && IsHandleCreated) {
                try { BeginInvoke(action); } catch (InvalidOperationException) { }
            }
        }
        async Task Start(bool asHost) {
            if (applyingUpdate) return;
            if (downloadProfile == null && (String.IsNullOrEmpty(relayAddress) || brandingSetupRequired) && !ConfigureConnection()) return;
            Uri uri;
            if (!Uri.TryCreate(relayAddress, UriKind.Absolute, out uri)
                    || uri.AbsolutePath != "/support" || uri.Query != "" || uri.UserInfo != "" || uri.Fragment != ""
                    || !(uri.Scheme == "wss" || (uri.Scheme == "ws" && uri.IsLoopback))) {
                MessageBox.Show(this, "프로그램에 포함된 접속 설정을 읽을 수 없습니다. 최신 파일을 다시 받아주세요.", "접속 설정"); return;
            }
            string inputCode = code.Text.Replace("-", "").Replace(" ", ""), supporterName = name.Text.Trim();
            if ((asHost && !Regex.IsMatch(inputCode, "^[0-9]{6}$")) || (!asHost && (supporterName.Length < 1 || supporterName.Length > 40))) {
                MessageBox.Show(this, "지원받는 사람은 숫자 6자리 코드를, 지원자는 이름(1~40자)을 입력하세요."); return;
            }
            if (asHost && downloadProfile == null && MessageBox.Show(this, "승인 후 지원자가 기본 모니터 화면을 보고 마우스·키보드를 조작합니다.\n연결 중 이 창을 열어두세요. 언제든 즉시 종료할 수 있습니다.\n\n입력한 코드로 원격지원을 요청할까요?", "지원받기", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            End(""); hosting = asHost; approved = false;
            Session current;
            try { current = new Session(); session = current; }
            catch (Exception ex) {
                status.Text = "연결 기능을 시작하지 못했습니다: " + ex.GetBaseException().GetType().Name;
                MessageBox.Show(this, status.Text + RuntimeRequirements.SaveError(ex), "연결 오류");
                return;
            }
            current.Message += delegate(Dictionary<string, object> message) { UI(delegate { if (session == current) OnMessage(message); }); };
            current.Closed += delegate(string reason) { UI(delegate { if (session == current) End(reason); }); };
            current.Frame += delegate(byte[] bytes) {
                lock (frameLock) {
                    latestFrame = bytes;
                    if (frameScheduled) return;
                    frameScheduled = true;
                }
                UI(delegate {
                    byte[] newest;
                    lock (frameLock) { newest = latestFrame; latestFrame = null; frameScheduled = false; }
                    if (session == current && approved && !hosting && newest != null) ShowFrame(newest);
                });
            };
            host.Enabled = join.Enabled = code.Enabled = name.Enabled = false;
            settings.Enabled = false;
            copy.Enabled = false; stop.Enabled = true;
            status.Text = "서버에 연결하는 중입니다…";
            try {
                await current.Connect(uri);
                if (session != current) { current.Dispose(); return; }
                if (!hosting) current.Send(new { type = "create", name = supporterName, description = siteDescription });
                else current.Send(new { type = "join", code = inputCode });
            } catch (Exception ex) {
                if (session == current) {
                    RuntimeRequirements.SaveError(ex);
                    string cause = ex.GetBaseException() is System.Security.Authentication.AuthenticationException
                        ? "암호화 또는 인증서 확인에 실패했습니다. PC 날짜·Windows 업데이트·루트 인증서를 확인하세요."
                        : "서버 주소·인터넷 연결·실행 환경을 확인하세요.";
                    End("연결 실패: " + cause + " (" + ex.GetBaseException().GetType().Name + ")");
                }
            }
        }
        void OnMessage(Dictionary<string, object> msg) {
            string type = Get(msg, "type");
            if (type == "created" && !hosting) {
                string value = Get(msg, "code");
                if (!Regex.IsMatch(value, "^[0-9]{6}$")) { End("잘못된 코드를 받았습니다."); return; }
                code.Text = value;
                copy.Enabled = true; status.Text = "코드를 지원받는 사람에게 전달하세요. 코드는 10분간 유효합니다.";
                hint.Text = "상대방이 코드를 입력하고 승인하면 화면이 열립니다.";
            } else if (type == "request" && hosting && !approved) {
                siteDescription = Get(msg, "description");
                Text = Branding.Title(Get(msg, "name")) + " · 지원받기 (v0.10 테스트)";
                studentNotice.Text = Branding.StudentNotice(Get(msg, "name"), siteDescription);
                Session current = session;
                var result = MessageBox.Show(this, "지원자 표시 이름: " + Get(msg, "name") + "\n접속 서버: " + new Uri(relayAddress).Authority + "\n※ 이름은 지원자가 직접 입력했으며 신원 인증된 이름이 아닙니다.\n\n기본 모니터 화면 공유와 마우스·키보드 조작을 허용할까요?\n60초 안에 선택하세요.", "원격지원 요청", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (session == current && current != null) current.Send(new { type = result == DialogResult.Yes ? "accept" : "reject" });
            } else if (type == "waiting") {
                status.Text = "상대방의 승인을 기다리는 중입니다…";
            } else if (type == "ready" && !hosting) {
                status.Text = "요청이 취소되었습니다. 지원받는 사람의 코드 입력을 기다립니다.";
            } else if (type == "active") {
                approved = true; copy.Enabled = false;
                if (hosting) {
                    remoteBounds = Screen.PrimaryScreen.Bounds;
                    status.Text = "● 원격지원 연결 중 — 화면과 마우스·키보드를 공유하고 있습니다.";
                    BackColor = Color.FromArgb(254, 226, 226);
                    hint.Text = hotkeyRegistered ? "즉시 종료: " + stopHotkey + " 또는 위의 종료 버튼" : "종료 단축키를 사용할 수 없습니다. 위의 종료 버튼을 사용하세요.";
                    capture.Start();
                } else {
                    status.Text = "● 연결되었습니다. 아래 화면을 클릭하여 조작하세요.";
                    viewer.Focus();
                }
            } else if (type == "input" && hosting && approved) {
                ApplyInput(msg);
            } else if (type == "release" && hosting) {
                ReleaseInput();
            } else if (type == "ended" || type == "error") {
                End(Get(msg, "message"));
            }
        }
        internal void StopForError() { End("오류로 원격지원을 종료했습니다."); }
        void End(string reason) {
            approved = false; capture.Stop(); ReleaseInput();
            Session old = session; session = null;
            if (old != null) old.Dispose();
            host.Enabled = join.Enabled = code.Enabled = name.Enabled = true;
            settings.Enabled = true;
            copy.Enabled = stop.Enabled = false; viewer.Capture = false;
            if (viewer.Image != null) { Image oldImage = viewer.Image; viewer.Image = null; oldImage.Dispose(); }
            if (!String.IsNullOrEmpty(reason)) status.Text = reason;
            BackColor = Color.FromArgb(245, 247, 250);
        }
        void CaptureScreen() {
            if (!hosting || !approved || session == null) return;
            try {
                remoteBounds = Screen.PrimaryScreen.Bounds;
                var b = remoteBounds;
                using (var full = new Bitmap(b.Width, b.Height)) {
                    using (Graphics g = Graphics.FromImage(full)) {
                        g.CopyFromScreen(b.Location, Point.Empty, b.Size);
                        Point cursor = Cursor.Position;
                        if (b.Contains(cursor)) {
                            var p = new Point(cursor.X - b.X, cursor.Y - b.Y);
                            g.FillEllipse(Brushes.OrangeRed, p.X - 4, p.Y - 4, 8, 8);
                        }
                    }
                    double scale = Math.Min(1.0, Math.Min(1280.0 / b.Width, 1280.0 / b.Height));
                    using (var resized = new Bitmap(full, Math.Max(1, (int)(b.Width * scale)), Math.Max(1, (int)(b.Height * scale))))
                    using (var bytes = new MemoryStream()) {
                        var enc = Array.Find(ImageCodecInfo.GetImageEncoders(), delegate(ImageCodecInfo item) { return item.MimeType == "image/jpeg"; });
                        using (var parameters = new EncoderParameters(1)) {
                            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 55L);
                            resized.Save(bytes, enc, parameters);
                        }
                        session.SendFrame(bytes.ToArray());
                    }
                }
            } catch (Exception) { End("화면을 공유할 수 없습니다. 잠금 화면이나 권한 요청을 PC에서 직접 처리한 뒤 다시 연결하세요."); }
        }
        void ShowFrame(byte[] bytes) {
            try {
                using (var stream = new MemoryStream(bytes))
                using (var decoded = Image.FromStream(stream)) {
                    if (decoded.Width > 1280 || decoded.Height > 1280) throw new InvalidDataException();
                    Image image = new Bitmap(decoded), old = viewer.Image;
                    viewer.Image = image; if (old != null) old.Dispose();
                }
            } catch (Exception) { End("화면 데이터를 읽지 못했습니다."); }
        }
        void Pointer(string action, MouseEventArgs e) {
            if (hosting || !approved || session == null || viewer.Image == null) return;
            double scale = Math.Min((double)viewer.ClientSize.Width / viewer.Image.Width, (double)viewer.ClientSize.Height / viewer.Image.Height);
            double width = viewer.Image.Width * scale, height = viewer.Image.Height * scale;
            double x = (e.X - (viewer.ClientSize.Width - width) / 2) / width;
            double y = (e.Y - (viewer.ClientSize.Height - height) / 2) / height;
            if ((x < 0 || x > 1 || y < 0 || y > 1) && action != "up" && !viewer.Capture) return;
            string button = e.Button == MouseButtons.Right ? "right" : e.Button == MouseButtons.Middle ? "middle" : "left";
            session.Send(new { type = "input", @event = action, x = Math.Max(0, Math.Min(1, x)), y = Math.Max(0, Math.Min(1, y)), button = button, delta = Math.Max(-1200, Math.Min(1200, e.Delta)) });
        }
        void Key(KeyEventArgs e, bool down) {
            if (hosting || !approved || session == null) return;
            session.Send(new { type = "input", @event = "key", key = (int)e.KeyCode, down = down });
            e.Handled = true;
        }
        void ApplyInput(Dictionary<string, object> m) {
            try {
                string action = Get(m, "event");
                if (action == "key") {
                    int key = Convert.ToInt32(m["key"]); bool down = Convert.ToBoolean(m["down"]);
                    if (key < 1 || key > 255) return;
                    if (down) heldKeys.Add(key); else heldKeys.Remove(key);
                    if (!Native.Key(key, down)) { End("입력을 전달할 수 없습니다. 관리자 권한 창이나 잠금 화면은 해당 PC에서 직접 처리하세요."); return; }
                } else {
                    double x = Convert.ToDouble(m["x"]), y = Convert.ToDouble(m["y"]);
                    if (Double.IsNaN(x) || Double.IsNaN(y) || x < 0 || x > 1 || y < 0 || y > 1) return;
                    if (!Native.SetCursorPos(remoteBounds.X + (int)(x * (remoteBounds.Width - 1)), remoteBounds.Y + (int)(y * (remoteBounds.Height - 1)))) { End("마우스를 전달할 수 없습니다. 해당 PC의 잠금이나 권한 요청을 직접 처리하세요."); return; }
                    string button = Get(m, "button");
                    if (action == "down" || action == "up") {
                        if (button != "left" && button != "right" && button != "middle") return;
                        if (action == "down") heldButtons.Add(button); else heldButtons.Remove(button);
                        if (!Native.Button(button, action == "down")) { End("마우스 입력을 전달할 수 없어 종료했습니다."); return; }
                    } else if (action == "wheel" && !Native.Wheel(Convert.ToInt32(m["delta"]))) { End("스크롤 입력을 전달할 수 없어 종료했습니다."); return; }
                }
            } catch (Exception) { End("입력 처리 중 오류가 발생해 원격지원을 종료했습니다."); }
        }
        void ReleaseInput() {
            foreach (int key in heldKeys) Native.Key(key, false); heldKeys.Clear();
            foreach (string button in heldButtons) Native.Button(button, false); heldButtons.Clear();
        }
        protected override void WndProc(ref Message m) {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 9001) End("종료 단축키로 원격지원을 종료했습니다.");
            base.WndProc(ref m);
        }
    }

    static class Native {
        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public UIntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION U; }
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, int key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
        static bool Send(INPUT input) { return SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT))) == 1; }
        public static bool Key(int key, bool down) {
            bool extended = key == 33 || key == 34 || key == 35 || key == 36 || key == 37 || key == 38 || key == 39 || key == 40 || key == 45 || key == 46 || key == 91 || key == 92 || key == 111 || key == 163 || key == 165;
            var input = new INPUT { type = 1 };
            input.U.ki.wVk = (ushort)key;
            input.U.ki.wScan = (ushort)MapVirtualKey((uint)key, 0);
            input.U.ki.dwFlags = (down ? 0u : 2u) | (extended ? 1u : 0u);
            return Send(input);
        }
        public static bool Button(string button, bool down) {
            var input = new INPUT { type = 0 };
            input.U.mi.dwFlags = button == "right" ? (down ? 8u : 16u) : button == "middle" ? (down ? 32u : 64u) : (down ? 2u : 4u);
            return Send(input);
        }
        public static bool Wheel(int delta) {
            var input = new INPUT { type = 0 }; input.U.mi.dwFlags = 0x0800;
            input.U.mi.mouseData = unchecked((uint)delta); return Send(input);
        }
    }
}
