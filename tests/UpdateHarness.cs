using System;
using System.IO;
namespace JHSupport {
    static class UpdateHarness {
        public static int Main(string[] args) {
            try {
                UpdateRelease release = UpdateVerifier.Parse(File.ReadAllText(args[1]));
                if (args[0] == "parse") { Console.WriteLine(release.Version); return 0; }
                if (args[0] == "verify") { UpdateVerifier.VerifyExecutable(args[2],release); Console.WriteLine("verified"); return 0; }
                if (args[0] == "replace") {
                    AutoUpdater.ReplaceVerified(args[2],args[3],args[4],release);
                    Console.WriteLine("replaced"); return 0;
                }
                throw new InvalidOperationException();
            } catch (Exception ex) { Console.WriteLine(ex.GetBaseException().GetType().Name); return 2; }
        }
    }
}
