using System;
using System.Reflection;

namespace ThousandAndFirst.WorkshopSteam
{
    public static class UploadTests
    {
        public static int Main(string[] args)
        {
            if (args != null && args.Length == 2 && args[0] == "--child")
                return WorkshopItemLockTests.Main(args);
            if (args == null || args.Length != 1)
            {
                Console.Error.WriteLine("Expected one Workshop upload test suite.");
                return 2;
            }
            switch (args[0])
            {
                case "protocol": return UploadProtocolTests.Run();
                case "package": return UploadPackageTests.Run();
                case "attempt": return UploadAttemptLeaseTests.Run();
                case "evidence": return global::ExtraObservationTests.Main();
                case "record": return Evidence.UploadSubmissionRecordTests.Run();
                case "observation": return ObservationWriteTests.Run();
                case "aftermath": return UploadAftermathTests.Run();
                case "cleanup": return UploadCleanupTests.Run();
                case "lock": return WorkshopItemLockTests.Run();
                case "lease": return Standalone(typeof(global::RegistryLeaseTests));
                case "registry": return Standalone(typeof(global::WorkshopReleaseRegistryTests));
                case "finalization": return ReleaseFinalizationTests.Run();
                case "finalization-windows": return ReleaseFinalizationTests.RunWindows();
                case "cli": return WorkshopSteamCliTests.Run();
                default: Console.Error.WriteLine("Unknown Workshop upload test suite."); return 2;
            }
        }

        // The closed helper suites keep their own private entry points. They are compiled here
        // byte-identical and dispatched by reflection rather than edited to expose a runner.
        private static int Standalone(Type suite)
        {
            MethodInfo entry = suite.GetMethod("Main",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (entry == null)
            {
                Console.Error.WriteLine("Standalone suite has no entry point.");
                return 2;
            }
            object[] parameters = entry.GetParameters().Length == 0 ? null : new object[] { new string[0] };
            object value = entry.Invoke(null, parameters);
            return value is int ? (int)value : 1;
        }
    }
}
