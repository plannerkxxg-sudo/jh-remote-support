using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace JHSupport {
    sealed class UpdateAsset {
        public string Url, Hash; public int Size;
    }
    sealed class UpdateRelease {
        public Version Version; public string Envelope;
        public UpdateAsset Client, Relay;
    }
    static class UpdateVerifier {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static Dictionary<string, object> Object(object value) {
            var result = value as Dictionary<string, object>;
            if (result == null) throw new InvalidDataException("Invalid update object");
            return result;
        }
        static string Text(Dictionary<string, object> value, string key) {
            object result;
            if (!value.TryGetValue(key, out result) || !(result is string)) throw new InvalidDataException("Invalid update field");
            return (string)result;
        }
        public static Version ReadVersion(string value) {
            Version parsed;
            if (!Regex.IsMatch(value ?? "", @"^(0|[1-9][0-9]{0,4})(\.(0|[1-9][0-9]{0,4})){3}$")
                    || !Version.TryParse(value, out parsed)) throw new InvalidDataException("Invalid update version");
            return parsed;
        }
        static UpdateAsset Asset(Dictionary<string, object> value, string filename, int maximum) {
            string address = Text(value, "url");
            Uri uri;
            string prefix = "/" + UpdateTrust.Repository + "/releases/download/";
            if (!Uri.TryCreate(address, UriKind.Absolute, out uri) || uri.Scheme != "https"
                    || uri.Authority != "github.com" || uri.Query != "" || uri.Fragment != "" || uri.UserInfo != ""
                    || !uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("Update URL is outside the trusted repository");
            string tail = uri.AbsolutePath.Substring(prefix.Length);
            string[] parts = tail.Split('/');
            if (parts.Length != 2 || !Regex.IsMatch(parts[0], @"^[A-Za-z0-9._-]{1,80}$") || parts[1] != filename)
                throw new InvalidDataException("Invalid update asset path");
            object size;
            if (!value.TryGetValue("size", out size) || !(size is int)) throw new InvalidDataException("Invalid update size");
            int length = (int)size;
            string hash = Text(value, "sha256");
            if (length < 1024 || length > maximum || !Regex.IsMatch(hash, "^[a-f0-9]{64}$"))
                throw new InvalidDataException("Invalid update asset metadata");
            return new UpdateAsset { Url = address, Hash = hash, Size = length };
        }
        public static UpdateRelease Parse(string envelope) {
            if (UpdateTrust.UpdateMarker.Length == 0 || envelope == null || Utf8.GetByteCount(envelope) > 65536) throw new InvalidDataException("Update manifest too large");
            var json = new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 12 };
            var wrapper = Object(json.DeserializeObject(envelope));
            byte[] payload = Convert.FromBase64String(Text(wrapper, "payload"));
            byte[] signature = Convert.FromBase64String(Text(wrapper, "signature"));
            if (payload.Length > 16384 || signature.Length != 384) throw new InvalidDataException("Invalid signature size");
            using (var rsa = new RSACryptoServiceProvider(new CspParameters(24))) {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(UpdateTrust.PublicKeyXml);
                if (rsa.KeySize != 3072 || !rsa.VerifyData(payload, "SHA256", signature))
                    throw new InvalidDataException("업데이트 서명이 올바르지 않습니다.");
            }
            var data = Object(json.DeserializeObject(Utf8.GetString(payload)));
            object schema;
            if (!data.TryGetValue("schema", out schema) || !(schema is int) || (int)schema != 1
                    || Text(data, "product") != "JHRemoteSupport") throw new InvalidDataException("Wrong update product/schema");
            var files = Object(data["files"]);
            if (files.Count != 2) throw new InvalidDataException("Unexpected update assets");
            return new UpdateRelease { Version = ReadVersion(Text(data, "version")), Envelope = envelope,
                Client = Asset(Object(files["client"]), "JHRemoteSupport.exe", 1500000),
                Relay = Asset(Object(files["relay"]), "relay-update.zip", 10000000) };
        }
        public static string Hash(byte[] bytes) {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public static void VerifyBytes(byte[] bytes, UpdateAsset asset) {
            if (bytes.Length != asset.Size || Hash(bytes) != asset.Hash) throw new InvalidDataException("업데이트 파일이 손상되었거나 변경됐습니다.");
        }
        public static void VerifyExecutable(string path, UpdateRelease release) {
            VerifyBytes(File.ReadAllBytes(path), release.Client);
            var assembly = AssemblyName.GetAssemblyName(path);
            if (assembly.Name != "JHRemoteSupport" || assembly.Version != release.Version)
                throw new InvalidDataException("업데이트 프로그램 버전이 일치하지 않습니다.");
        }
    }

    static class AutoUpdater {
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        static string UpdateFolder {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JHRemoteSupport", "updates"); }
        }
        public static Version CurrentVersion { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        static byte[] Fetch(string address, int maximum) {
            Uri uri = new Uri(address);
            for (int attempt = 0; attempt < 8; attempt++) {
                if (uri.Scheme != "https" || uri.UserInfo != "") throw new InvalidDataException("HTTPS update required");
                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.AllowAutoRedirect = false; request.Timeout = 10000; request.ReadWriteTimeout = 10000;
                request.UserAgent = "JHRemoteSupport-Updater/0.10";
                request.Credentials = null;
                using (var response = (HttpWebResponse)request.GetResponse()) {
                    int status = (int)response.StatusCode;
                    if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308) {
                        uri = new Uri(uri, response.Headers["Location"]); continue;
                    }
                    if (status != 200 || response.ContentLength > maximum) throw new InvalidDataException("Invalid update response");
                    using (var input = response.GetResponseStream())
                    using (var output = new MemoryStream()) {
                        byte[] buffer = new byte[16384]; int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0) {
                            if (output.Length + count > maximum) throw new InvalidDataException("Update download too large");
                            output.Write(buffer, 0, count);
                        }
                        return output.ToArray();
                    }
                }
            }
            throw new InvalidDataException("Too many update redirects");
        }
        public static Task<UpdateRelease> CheckAsync(bool force) {
            return Task.Run(delegate {
                var release = UpdateVerifier.Parse(Utf8.GetString(Fetch(UpdateTrust.FeedUrl, 65536)));
                string failed = Path.Combine(UpdateFolder, "failed-version.txt");
                if (!force && File.Exists(failed) && File.ReadAllText(failed).Trim() == release.Version.ToString()) return null;
                return release.Version > CurrentVersion ? release : null;
            });
        }
        public static Task<string> StageAsync(UpdateRelease release) {
            return Task.Run(delegate {
                string directory = Path.Combine(UpdateFolder, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                string staged = Path.Combine(directory, "new-client.exe");
                try {
                    byte[] bytes = Fetch(release.Client.Url, release.Client.Size);
                    UpdateVerifier.VerifyBytes(bytes, release.Client);
                    File.WriteAllBytes(staged, bytes);
                    UpdateVerifier.VerifyExecutable(staged, release);
                    File.WriteAllText(Path.Combine(directory, "manifest.json"), release.Envelope, Utf8);
                    return directory;
                } catch { Directory.Delete(directory, true); throw; }
            });
        }
        public static void Discard(string directory) {
            try { if (directory != null) Directory.Delete(directory, true); } catch (Exception) { }
        }
        static bool SamePath(string first, string second) {
            return String.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        public static void Launch(string directory) {
            string current = Assembly.GetExecutingAssembly().Location;
            if (!String.Equals(Path.GetFileName(current), "JHRemoteSupport.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("상담원 파일 이름을 JHRemoteSupport.exe로 두고 다시 실행하세요.");
            // Test writability before exiting the running app; Program Files may need a user-writable folder.
            string probe = Path.Combine(Path.GetDirectoryName(current), ".jh-update-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write)) { }
            File.Delete(probe);
            var process = Process.GetCurrentProcess();
            var job = new { target = current, oldHash = UpdateVerifier.Hash(File.ReadAllBytes(current)),
                parentId = process.Id, parentStartTicks = process.StartTime.ToUniversalTime().Ticks.ToString() };
            File.WriteAllText(Path.Combine(directory, "job.json"), new JavaScriptSerializer().Serialize(job), Utf8);
            string helper = Path.Combine(directory, "updater.exe");
            File.Copy(current, helper, false);
            Process.Start(new ProcessStartInfo(helper, "--apply-update \"" + Path.Combine(directory, "job.json") + "\"") { UseShellExecute = false });
        }
        public static void Apply(string jobPath) {
            string job = Path.GetFullPath(jobPath);
            string directory = Path.GetDirectoryName(job);
            string root = Path.GetFullPath(UpdateFolder) + Path.DirectorySeparatorChar;
            if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(job) != "job.json"
                    || !Regex.IsMatch(Path.GetFileName(directory), "^[a-f0-9]{32}$") || new FileInfo(job).Length > 16384)
                throw new InvalidDataException("Invalid update job");
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(job, Utf8));
            string target = Path.GetFullPath(Convert.ToString(data["target"]));
            if (target.StartsWith(@"\\") || !String.Equals(Path.GetFileName(target), "JHRemoteSupport.exe", StringComparison.OrdinalIgnoreCase)
                    || (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Invalid update target");
            var release = UpdateVerifier.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json"), Utf8));
            string staged = Path.Combine(directory, "new-client.exe");
            UpdateVerifier.VerifyExecutable(staged, release);
            var old = AssemblyName.GetAssemblyName(target);
            if (old.Name != "JHRemoteSupport" || release.Version <= old.Version)
                throw new InvalidDataException("Update rollback blocked");
            if (UpdateVerifier.Hash(File.ReadAllBytes(target)) != Convert.ToString(data["oldHash"]))
                throw new InvalidDataException("Running client changed during update");
            Process parent = null;
            try { parent = Process.GetProcessById(Convert.ToInt32(data["parentId"])); }
            catch (ArgumentException) { } // Parent may already have exited normally.
            if (parent != null) {
                using (parent) {
                    if (parent.StartTime.ToUniversalTime().Ticks.ToString() != Convert.ToString(data["parentStartTicks"])
                            || !SamePath(parent.MainModule.FileName, target) || !parent.WaitForExit(30000))
                        throw new InvalidDataException("The original counselor process has not exited");
                }
            }
            if (UpdateVerifier.Hash(File.ReadAllBytes(target)) != Convert.ToString(data["oldHash"]))
                throw new InvalidDataException("Update target changed");
            string backup = Path.Combine(Path.GetDirectoryName(target), "JHRemoteSupport.previous.exe");
            try { ReplaceVerified(staged, target, backup, release); }
            catch {
                File.WriteAllText(Path.Combine(UpdateFolder, "failed-version.txt"), release.Version.ToString());
                try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch (Exception) { }
                throw;
            }
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch {
                File.Copy(backup, target, true);
                File.WriteAllText(Path.Combine(UpdateFolder, "failed-version.txt"), release.Version.ToString());
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                throw;
            }
            // The helper cannot delete its own running file; remove payloads and job only.
            foreach (string name in new[] { "new-client.exe", "manifest.json", "job.json" }) {
                try { File.Delete(Path.Combine(directory, name)); } catch (Exception) { }
            }
        }
        internal static void ReplaceVerified(string staged, string target, string backup, UpdateRelease release) {
            UpdateVerifier.VerifyExecutable(staged, release);
            string next = target + ".update-" + Guid.NewGuid().ToString("N");
            try {
                File.Copy(staged, next, false);
                UpdateVerifier.VerifyExecutable(next, release);
                if (File.Exists(backup)) File.Delete(backup);
                File.Replace(next, target, backup);
            } finally { if (File.Exists(next)) File.Delete(next); }
        }
    }
}
