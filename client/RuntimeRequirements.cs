using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace JHSupport {
    static class RuntimeRequirements {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NativeVersion {
            public int Size, Major, Minor, Build, Platform;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string ServicePack;
        }
        [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
        static extern int RtlGetVersion(ref NativeVersion version);
        public static Version OsVersion() {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
                try {
                    var actual = new NativeVersion(); actual.Size = Marshal.SizeOf(typeof(NativeVersion));
                    if (RtlGetVersion(ref actual) == 0) return new Version(actual.Major, actual.Minor, actual.Build);
                } catch (Exception) { }
            }
            return Environment.OSVersion.Version;
        }
        public static string CheckValues(PlatformID platform, Version os, int release) {
            if (platform != PlatformID.Win32NT) return "이 프로그램은 Windows PC용입니다.";
            if (os.Major < 6 || (os.Major == 6 && os.Minor == 0))
                return "Windows 7 SP1 이상의 PC가 필요합니다. Windows XP/Vista는 지원하지 않습니다.";
            if (os.Major == 6 && os.Minor == 1 && os.Build < 7601)
                return "Windows 7 서비스 팩 1(SP1)이 필요합니다. PC 관리 담당자에게 업데이트를 요청하세요.";
            if (os.Major == 6 && os.Minor == 2)
                return "Windows 8.0은 지원하지 않습니다. Windows 7 SP1·8.1·10·11 환경을 확인하세요.";
            if (release < 528040)
                return ".NET Framework 4.8 이상이 필요합니다.\nWindows 7 SP1은 4.8을 설치하세요(4.8.1은 설치할 수 없습니다).\n공식 다운로드: https://dotnet.microsoft.com/download/dotnet-framework/net48\n설치 후 PC를 다시 시작하고 홈페이지에서 새 파일을 받으세요.";
            return null;
        }
        public static int FrameworkRelease() {
            try {
                object value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", "Release", 0);
                return Convert.ToInt32(value);
            } catch (Exception) { return 0; }
        }
        public static string Check() {
            return CheckValues(Environment.OSVersion.Platform, OsVersion(), FrameworkRelease());
        }
        public static bool Verify() {
            return CheckValues(PlatformID.Win32NT, new Version(6, 1, 7601), 528049) == null
                && CheckValues(PlatformID.Win32NT, new Version(10, 0, 22631), 533320) == null
                && CheckValues(PlatformID.Win32NT, new Version(6, 1, 7600), 528049) != null
                && CheckValues(PlatformID.Win32NT, new Version(6, 2, 9200), 528040) != null
                && CheckValues(PlatformID.Win32NT, new Version(6, 1, 7601), 461808) != null
                && CheckValues(PlatformID.Win32NT, new Version(6, 3, 9600), 528040) == null
                && CheckValues(PlatformID.Win32NT, new Version(5, 1, 2600), 528040) != null;
        }
        public static string Diagnostic() {
            return "JH Remote Support " + Assembly.GetExecutingAssembly().GetName().Version
                + "\r\nOS: " + OsVersion()
                + "\r\n64-bit OS: " + Environment.Is64BitOperatingSystem
                + "\r\nCLR: " + Environment.Version + "\r\n.NET Release: " + FrameworkRelease()
                + "\r\nTransport: " + ManagedWebSocket.CompatibilityMarker + " / TLS 1.2 or newer";
        }
        public static string SaveError(Exception error) {
            try {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JHRemoteSupport", "logs");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "last-error.txt");
                Exception cause = error.GetBaseException();
                // Local only. Never include session codes, captured screens, JSON payloads or passwords.
                File.WriteAllText(path, Diagnostic() + "\r\nTime UTC: " + DateTime.UtcNow.ToString("o")
                    + "\r\nException: " + cause.GetType().FullName + "\r\n" + cause.StackTrace, Encoding.UTF8);
                return "\n진단 파일: " + path;
            } catch (Exception) { return ""; }
        }
    }
}
