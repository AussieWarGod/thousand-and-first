// SDK-free fixtures for the registry-backed publisher CLI. Executable cases drive the exact
// composition SteamPublish uses - registry open, retained-attempt decision, attempt directory,
// then UploadCleanup - against a temporary layout through the registry's internal test seam.
// They never open C:\taf-workshop-state.dRBivM and never initialize Steam. Source cases read
// SteamPublish.cs itself: it references the Steamworks-bound SteamUploadPort, so it cannot be
// compiled into this SDK-free assembly, and its ordering is proved from the file it ships.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ThousandAndFirst.WorkshopSteam
{
    public static class WorkshopSteamCliTests
    {
        private const ulong Item = WorkshopItemLock.StagingItem;
        private const string Legacy = "3796495680.active.attempt.json";
        private static int passed, failed;

        public static int Run()
        {
            passed = failed = 0;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            { Console.Error.WriteLine("Windows required: publisher CLI fixtures NOT RUN."); return 2; }
            Case("fixed registry root is the compiled constant", FixedRootConstant);
            Case("alternate state root is refused before every side effect", AlternateRootRefusedFirst);
            Case("item mutex is held through decision, port and cleanup", LockHeldThroughCleanup);
            Case("retained attempt refuses inspection and submission without creating", RetainedRefuses);
            Case("submit creates the attempt directory before the protocol runs", AttemptBeforeProtocol);
            Case("a failed protocol retains the attempt directory", FailureRetains);
            Case("cleanup failure runs every step and keeps report context", CleanupUncertainty);
            Case("abandoned mutex is uncertainty, never retry authority", AbandonedIsUncertainty);
            Case("legacy attempt filename and receipt bytes are unchanged", LegacyReceipt);
            Case("inspection proves held identities and lists bounded names only", InspectionProofs);
            Case("inspection never opens retained descendants", InspectionDoesNotOpenDescendants);
            Case("inspection refuses more than 64 immediate entries", InspectionBounds);
            Case("CLI null and canonical item admission precede state", CanonicalArguments);
            Case("statuses and exit codes are declared and documented", StatusTable);
            Console.WriteLine("Workshop publisher CLI fixtures: passed=" + passed + " failed=" + failed);
            return failed == 0 ? 0 : 1;
        }

        private static void FixedRootConstant()
        {
            Check(WorkshopReleaseRegistry.StateRoot == @"C:\taf-workshop-state.dRBivM");
            string cli = Source("SteamPublish.cs");
            Check(cli.Contains("args.Length != 7"));
            Check(cli.Contains("string.Equals(stateRoot, WorkshopReleaseRegistry.StateRoot, StringComparison.Ordinal)"));
            // The caller-selected attempt root is gone; the registry decides every path.
            Check(!cli.Contains("private static string AttemptPath"));
        }

        private static void AlternateRootRefusedFirst()
        {
            string cli = Source("SteamPublish.cs");
            int shape = cli.IndexOf("args.Length != 7", StringComparison.Ordinal);
            int refusal = cli.IndexOf("return ExitStateRootRefused;", StringComparison.Ordinal);
            Check(shape > 0 && refusal > shape);
            // Nothing between the argument shape and the refusal touches disk, state or the SDK.
            string prelude = cli.Substring(shape, refusal - shape);
            foreach (string effect in new[] { "File.", "Directory.", "Path.", "new FileStream", "SteamAPI" })
                Check(prelude.IndexOf(effect, StringComparison.Ordinal) < 0);
            foreach (string later in new[] { "ParseItem(args[3])", "ReadNote(args[4])",
                "WorkshopReleaseRegistry.Open(", "UploadPackage.Open(", "new SteamUploadPort(",
                "BeginFirstAttempt()", "UploadProtocol().Run(" })
                Check(cli.IndexOf(later, StringComparison.Ordinal) > refusal);
        }

        private static void LockHeldThroughCleanup()
        {
            string root = Root();
            List<string> steps = new List<string>();
            WorkshopReleaseRegistry registry = WorkshopReleaseRegistry.OpenCore(root, Item);
            // The registry owns the item mutex: no second lease exists while it is open.
            Expect<InvalidOperationException>(delegate { using (WorkshopItemLock.Acquire(Item)) { } });
            registry.RequireExact(); steps.Add("decision");
            string attempt = registry.BeginFirstAttempt(); steps.Add("attempt");
            UploadCleanup.Run(
                delegate { registry.RequireExact(); steps.Add("port"); },
                delegate { registry.RequireExact(); steps.Add("package"); },
                delegate { registry.Dispose(); steps.Add("registry"); });
            Check(string.Join(",", steps.ToArray()) == "decision,attempt,port,package,registry");
            // Released only by the registry disposal that cleanup ran last.
            using (WorkshopItemLock held = WorkshopItemLock.Acquire(Item)) Check(held.IsHeld);
            Check(Directory.Exists(Path.GetDirectoryName(attempt)) && !File.Exists(attempt));
            string cli = Source("SteamPublish.cs");
            int release = cli.IndexOf("UploadCleanup.Run(", StringComparison.Ordinal);
            Check(release > 0);
            string order = cli.Substring(release);
            Check(order.IndexOf("port.Dispose()", StringComparison.Ordinal)
                < order.IndexOf("package.Dispose()", StringComparison.Ordinal));
            Check(order.IndexOf("package.Dispose()", StringComparison.Ordinal)
                < order.IndexOf("registry.Dispose()", StringComparison.Ordinal));
        }

        private static void RetainedRefuses()
        {
            string root = Root(), attempt;
            using (WorkshopReleaseRegistry first = WorkshopReleaseRegistry.OpenCore(root, Item))
            {
                Check(!first.HasRetainedAttempt());
                attempt = first.BeginFirstAttempt();
                Check(first.HasRetainedAttempt());
            }
            using (WorkshopReleaseRegistry again = WorkshopReleaseRegistry.OpenCore(root, Item))
            {
                Check(again.HasRetainedAttempt());
                Check(Contains(again.Inspect(), "retainedEntries=1"));
                Expect<WorkshopReleaseRegistry.RetainedAttempt>(delegate { again.BeginFirstAttempt(); });
            }
            Check(Directory.GetFileSystemEntries(Path.Combine(Seat(root), "attempts")).Length == 1);
            Check(!File.Exists(attempt));
        }

        private static void AttemptBeforeProtocol()
        {
            string root = Root();
            List<string> order = new List<string>();
            using (WorkshopReleaseRegistry registry = WorkshopReleaseRegistry.OpenCore(root, Item))
            {
                // Check mode names the path the port would use and creates nothing.
                string planned = registry.PlannedAttemptPath();
                Check(!Directory.Exists(Path.GetDirectoryName(planned)));
                string attempt = registry.BeginFirstAttempt();
                Check(attempt == planned);
                order.Add("attempt_directory");
                Check(Directory.Exists(Path.GetDirectoryName(attempt)) && !File.Exists(attempt));
                using (UploadAttemptLease receipt =
                    UploadAttemptLease.Create(attempt, Encoding.UTF8.GetBytes("exact attempt bytes")))
                { order.Add("protocol"); Check(receipt.Revalidate()); registry.RequireExact(); }
            }
            Check(order.Count == 2 && order[0] == "attempt_directory" && order[1] == "protocol");
            string cli = Source("SteamPublish.cs");
            Check(cli.IndexOf("registry.BeginFirstAttempt()", StringComparison.Ordinal)
                < cli.IndexOf("new UploadProtocol().Run(", StringComparison.Ordinal));
        }

        private static void FailureRetains()
        {
            string root = Root(), attempt = null;
            WorkshopReleaseRegistry registry = WorkshopReleaseRegistry.OpenCore(root, Item);
            bool threw = false;
            try
            {
                attempt = registry.BeginFirstAttempt();
                throw new IOException("simulated protocol failure");
            }
            catch (IOException) { threw = true; }
            finally { UploadCleanup.Run(delegate { registry.Dispose(); }); }
            Check(threw && Directory.Exists(Path.GetDirectoryName(attempt)));
            using (WorkshopReleaseRegistry later = WorkshopReleaseRegistry.OpenCore(root, Item))
            {
                Check(later.HasRetainedAttempt());
                Expect<WorkshopReleaseRegistry.RetainedAttempt>(delegate { later.BeginFirstAttempt(); });
            }
            Check(Directory.GetFileSystemEntries(Path.Combine(Seat(root), "attempts")).Length == 1);
        }

        private static void CleanupUncertainty()
        {
            List<string> steps = new List<string>();
            bool aggregated = false;
            try
            {
                UploadCleanup.Run(
                    delegate { steps.Add("port"); throw new IOException("port cleanup failed"); },
                    delegate { steps.Add("package"); },
                    delegate { steps.Add("registry"); throw new IOException("registry cleanup failed"); });
            }
            catch (AggregateException error) { aggregated = true; Check(error.InnerExceptions.Count == 2); }
            Check(aggregated && steps.Count == 3 && steps[0] == "port" && steps[2] == "registry");
            string cli = Source("SteamPublish.cs");
            int branch = cli.IndexOf("if (cleanupFailure != null)", StringComparison.Ordinal);
            Check(branch > 0);
            string tail = cli.Substring(branch);
            foreach (string kept in new[] { "outcomeStatus", "CleanupUncertain", "cleanupErrorTypes",
                "ExitCleanupUncertain" })
                Check(tail.Contains(kept));
            // Context survives; exception text never does.
            Check(cli.Contains("report[\"attempt\"] = attempt;"));
            Check(cli.Contains("report[\"receiptSHA\"] = package.ReceiptSHA;"));
            Check(cli.Contains("report[\"planSHA\"] = package.PlanSHA;"));
            Check(!cli.Contains(".Message"));
        }

        private static void AbandonedIsUncertainty()
        {
            Check(typeof(WorkshopReleaseRegistry.AbandonedLock).IsSubclassOf(typeof(IOException)));
            Check(Source("WorkshopReleaseRegistry.cs").Contains("if (held.Abandoned) throw new AbandonedLock("));
            string cli = Source("SteamPublish.cs");
            int at = cli.IndexOf("catch (WorkshopReleaseRegistry.AbandonedLock error)", StringComparison.Ordinal);
            Check(at > 0);
            string branch = cli.Substring(at, 420);
            Check(branch.Contains("LockAbandonedUncertain") && branch.Contains("ExitLockAbandoned"));
            // Nothing in the entry converts that uncertainty into another attempt.
            Check(cli.Contains("{ \"retryAuthorized\", false },"));
            Check(!cli.Contains("\"retry\"") && !cli.Contains("Retry("));
        }

        private static void LegacyReceipt()
        {
            string root = Root();
            byte[] bytes = Encoding.UTF8.GetBytes(
                "{\"item\":\"3796495680\",\"packagePath\":\"C:\\\\pkg\",\"contentPath\":\"C:\\\\pkg\"}");
            using (WorkshopReleaseRegistry registry = WorkshopReleaseRegistry.OpenCore(root, Item))
            {
                string attempt = registry.BeginFirstAttempt();
                Check(Path.GetFileName(attempt) == Legacy);
                using (UploadAttemptLease receipt = UploadAttemptLease.Create(attempt, bytes))
                {
                    Check(Equal(bytes, ReadHeld(attempt)));
                    Check(receipt.Revalidate());
                }
            }
            // The port still writes the historical v1 pairing and the legacy observation name.
            Check(Source("SteamUploadPort.Observation.cs")
                .Contains("PackagePath = leased.ContentPath, ContentPath = leased.ContentPath"));
            Check(Source("UploadAttemptLease.cs").Contains("observation.Item + \".active.attempt.json\""));
        }

        private static void InspectionProofs()
        {
            string root = Root();
            using (WorkshopReleaseRegistry registry = WorkshopReleaseRegistry.OpenCore(root, Item))
            {
                string[] fresh = registry.Inspect();
                Check(Contains(fresh, "retainedEntries=0"));
                Check(Line(fresh, "markerSHA=").Length == 74);
                Check(Line(fresh, "itemDirectoryIdentity=").Length == 22 + 64);
                string attempt = registry.BeginFirstAttempt();
                using (UploadAttemptLease receipt =
                    UploadAttemptLease.Create(attempt, Encoding.UTF8.GetBytes("exact attempt bytes")))
                {
                    string[] proofs = registry.Inspect();
                    Check(Contains(proofs, "retainedEntry=0001") && Contains(proofs, "retainedEntries=1"));
                    Check(Contains(proofs, "inspectionScope=held-registry-identities-and-immediate-entry-names-only"));
                    foreach (string proof in proofs) Check(!proof.StartsWith("attemptFile=", StringComparison.Ordinal));
                    Check(receipt.Revalidate());
                }
                // Proofs carry paths and digests only: no account or SDK identity.
                foreach (string line in registry.Inspect())
                    Check(line.IndexOf("steamid", StringComparison.OrdinalIgnoreCase) < 0
                        && line.IndexOf("account", StringComparison.OrdinalIgnoreCase) < 0);
            }
        }

        private static void StatusTable()
        {
            string cli = Source("SteamPublish.cs");
            foreach (string status in new[] { "state_root_refused", "attempt_retained",
                "lock_abandoned_uncertain", "cleanup_uncertain", "inspected", "checked_not_submitted",
                "check_refused" })
                Check(cli.Contains("\"" + status + "\""));
            foreach (string code in new[] { "ExitStateRootRefused = 6", "ExitAttemptRetained = 7",
                "ExitLockAbandoned = 8", "ExitCleanupUncertain = 10" })
                Check(cli.Contains(code));
            Check(cli.Contains("args[0] != \"inspect\""));
            string doc = Source("PUBLISHING.md");
            foreach (string status in new[] { "state_root_refused", "attempt_retained",
                "lock_abandoned_uncertain", "cleanup_uncertain", "inspected" })
                Check(doc.Contains(status));
            Check(doc.Contains("C:\\taf-workshop-state.dRBivM"));
        }

        private static void InspectionDoesNotOpenDescendants()
        {
            using (WorkshopReleaseRegistry registry = WorkshopReleaseRegistry.OpenCore(Root(), Item))
            {
                string attempt = registry.BeginFirstAttempt();
                using (FileStream blocked = new FileStream(attempt, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    blocked.WriteByte(42); blocked.Flush(true);
                    Check(Contains(registry.Inspect(), "retainedEntry=0001"));
                    Check(blocked.Length == 1);
                }
            }
            string source = Source("WorkshopReleaseRegistry.cs");
            string inspect = source.Substring(source.IndexOf("public string[] Inspect()", StringComparison.Ordinal));
            inspect = inspect.Substring(0, inspect.IndexOf("private static string Sha(", StringComparison.Ordinal));
            foreach (string forbidden in new[] { "Directory.GetFiles(", "Directory.GetDirectories(",
                "Directory.Exists(", "new FileInfo(", "new FileStream(", "FileSha(", "SearchOption.AllDirectories" })
                Check(!inspect.Contains(forbidden));
        }

        private static void InspectionBounds()
        {
            string root = Root();
            using (WorkshopReleaseRegistry registry = WorkshopReleaseRegistry.OpenCore(root, Item))
            {
                string attempts = Path.Combine(Seat(root), "attempts");
                for (int i = 0; i < 64; i++) Directory.CreateDirectory(Path.Combine(attempts, "entry-" + i));
                Check(Contains(registry.Inspect(), "retainedEntries=64"));
                Directory.CreateDirectory(Path.Combine(attempts, "entry-64"));
                Expect<InvalidDataException>(delegate { registry.Inspect(); });
                Check(registry.HasRetainedAttempt());
                Expect<WorkshopReleaseRegistry.RetainedAttempt>(delegate { registry.BeginFirstAttempt(); });
                Check(Directory.GetFileSystemEntries(attempts).Length == 65);
            }
        }

        private static void CanonicalArguments()
        {
            string cli = Source("SteamPublish.cs");
            Check(cli.Contains("args == null || args.Length != 7"));
            Check(!cli.Contains("Report(AttemptRetained, args.Length > 3 ?"));
            Check(cli.Contains("args != null && args.Length > 3 ?"));
            Check(cli.Contains("item.ToString(CultureInfo.InvariantCulture) != value"));
            Check(cli.IndexOf("ParseItem(args[3])", StringComparison.Ordinal)
                < cli.IndexOf("WorkshopReleaseRegistry.Open(item)", StringComparison.Ordinal));
            string entry = Source("UploadTests.cs");
            Check(entry.IndexOf("return WorkshopItemLockTests.Main(args)", StringComparison.Ordinal)
                < entry.IndexOf("if (args == null || args.Length != 1)", StringComparison.Ordinal));
        }

        private static string Root()
        {
            string path = Path.Combine(Path.GetTempPath(), "taf-cli-test." + Guid.NewGuid().ToString("N"));
            Check(path.IndexOf("taf-workshop-state", StringComparison.OrdinalIgnoreCase) < 0);
            Directory.CreateDirectory(path);
            Console.WriteLine("retained fixture=" + path);
            return path;
        }

        private static string Seat(string root) { return Path.Combine(root, "registry", "3796495680"); }

        private static string SourceDirectory([CallerFilePath] string self = null)
        { return Path.GetDirectoryName(self); }

        private static string Source(string name)
        {
            string path = Path.Combine(SourceDirectory(), name);
            if (!File.Exists(path)) throw new FileNotFoundException("publisher source not found: " + path);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true)))
                return reader.ReadToEnd();
        }

        private static byte[] ReadHeld(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] value = new byte[(int)stream.Length];
                stream.ReadExactly(value);
                return value;
            }
        }

        private static bool Contains(string[] proofs, string line)
        {
            foreach (string value in proofs) if (value == line) return true;
            return false;
        }

        private static string Line(string[] proofs, string prefix)
        {
            foreach (string value in proofs)
                if (value.StartsWith(prefix, StringComparison.Ordinal)) return value;
            throw new Exception("missing proof " + prefix);
        }

        private static bool Equal(byte[] a, byte[] b)
        { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

        private static void Expect<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (Exception error)
            {
                if (error is T) return;
                throw new Exception("unexpected refusal " + error.GetType().Name, error);
            }
            throw new Exception("expected " + typeof(T).Name);
        }

        private static void Case(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); }
        }

        private static void Check(bool value) { if (!value) throw new Exception("assertion failed"); }
    }
}
