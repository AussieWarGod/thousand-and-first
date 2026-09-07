using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ThousandAndFirst.WorkshopSteam.Evidence;

namespace ThousandAndFirst.WorkshopSteam
{
    /// <summary>Synthetic policy and Windows custody fixtures. No SDK calls, fixed registry access,
    /// native finalization, real delivery, upload, deletion or evidence replacement occurs here.</summary>
    public static partial class ReleaseFinalizationTests
    {
        private const string Item = "3796495680", Stamp = "2026-09-06T11:00:00.0000000Z";
        private const string Later = "2026-09-06T11:01:00.0000000Z", Content = @"C:\synthetic\content";
        private static int passed, failed;
        private static string Hash(char c) { return new string(c, 64); }

        public static int Run()
        {
            passed = failed = 0;
            Case("canonical original attempt and clean submission bind", () => {
                byte[] attempt = Attempt(); var submitted = Observation(attempt);
                ReleaseSubmissionObservation result; string path;
                Check(ReleaseFinalizationRules.TrySubmission(attempt, Bytes(submitted), Item, out result, out path));
                Check(path == Content && result.RequestVersion == "0.3.1");
            });
            foreach (ReleaseSubmissionCompletion completion in Enum.GetValues(typeof(ReleaseSubmissionCompletion)))
                Case("completion gate " + completion, () => {
                    var observation = Observation(Attempt(), completion: completion);
                    bool accepted = ReleaseFinalizationRules.TrySubmission(Attempt(), Bytes(observation), Item, out _, out _);
                    Check(accepted == (completion == ReleaseSubmissionCompletion.Ok));
                    var installation = Installation(observation);
                    byte[] final = ReleaseFinalizationRules.FinalizationBytes(1, "", ReleaseFinalizationRules.Sha(installation.Encode()));
                    Check(ReleaseFinalizationRules.Finalized(observation, installation.Encode(), final, 1, "", Hash('d'), Hash('e'), out _)
                        == (completion == ReleaseSubmissionCompletion.Ok));
                });
            Case("unobserved unknown and wrong-item callback cannot be finalized", () => {
                foreach (bool callback in new[] { false, true })
                {
                    ReleaseSubmissionObservation observation; string refusal;
                    Check(ReleaseSubmissionObservation.TryCreate(ReleaseFinalizationRules.Sha(Attempt()), null, Item,
                        "0.3.1", Hash('b'), Hash('c'), Stamp, callback, callback ? (int?)1 : null,
                        callback ? "3794797472" : null, callback ? (bool?)false : null, callback ? (bool?)false : null,
                        ReleaseSubmissionCompletion.Unknown, null, out observation, out refusal));
                    Check(!ReleaseFinalizationRules.TrySubmission(Attempt(), Bytes(observation), Item, out _, out _));
                    Check(!ReleaseFinalizationRules.Binds(observation, Installation(observation), Hash('d'), Hash('e')));
                }
            });
            foreach (string altered in new[] { "wrong-item", "wrong-hash", "previous", "version", "plan", "receipt", "early" })
                Case("attempt/submission mismatch " + altered, () => {
                    byte[] attempt = Attempt();
                    var observation = Observation(attempt, version: altered == "version" ? "0.3.2" : "0.3.1",
                        attemptSHA: altered == "wrong-hash" ? Hash('f') : null,
                        previous: altered == "previous" ? Hash('f') : null,
                        plan: altered == "plan" ? Hash('f') : null, receipt: altered == "receipt" ? Hash('f') : null,
                        stamp: altered == "early" ? "2026-09-06T10:00:00.0000000Z" : Stamp);
                    Check(!ReleaseFinalizationRules.TrySubmission(attempt, Bytes(observation),
                        altered == "wrong-item" ? "3794797472" : Item, out _, out _));
                });
            Case("every truncated attempt and submission refuses", () => {
                byte[] attempt = Attempt(), submission = Bytes(Observation(Attempt()));
                for (int i = 0; i < attempt.Length; i++)
                {
                    byte[] cut = new byte[i]; Array.Copy(attempt, cut, i);
                    Check(!ReleaseFinalizationRules.TrySubmission(cut, Bytes(Observation(cut)), Item, out _, out _));
                }
                for (int i = 0; i < submission.Length; i++)
                {
                    byte[] cut = new byte[i]; Array.Copy(submission, cut, i);
                    Check(!ReleaseFinalizationRules.TrySubmission(attempt, cut, Item, out _, out _));
                }
            });
            foreach (string mutation in new[] { "space", "field-order", "duplicate", "missing", "unknown", "guid", "visibility", "path" })
                Case("noncanonical original attempt " + mutation, () => {
                    string original = Encoding.UTF8.GetString(Attempt()); string changed = original;
                    if (mutation == "space") changed += " ";
                    if (mutation == "field-order") changed = original.Replace("\"planSHA\":\"" + Hash('b') + "\",\"receiptSHA\":\"" + Hash('c') + "\"",
                        "\"receiptSHA\":\"" + Hash('c') + "\",\"planSHA\":\"" + Hash('b') + "\"");
                    if (mutation == "duplicate") changed = original.Replace("{", "{\"item\":\"" + Item + "\",");
                    if (mutation == "missing") changed = original.Replace("\"manifestId\":\"r_ThousandAndFirst\",", "");
                    if (mutation == "unknown") changed = original.Replace("{", "{\"extra\":\"value\",");
                    if (mutation == "guid") changed = original.Replace("11111111-1111-1111-1111-111111111111", "not-a-guid");
                    if (mutation == "visibility") changed = original.Replace("\"steamVisibility\":2", "\"steamVisibility\":0");
                    if (mutation == "path") changed = original.Replace("content", "..");
                    Check(changed != original); byte[] bytes = Encoding.UTF8.GetBytes(changed);
                    Check(!ReleaseFinalizationRules.TrySubmission(bytes, Bytes(Observation(bytes)), Item, out _, out _));
                });
            Case("malformed UTF8 never becomes valid attempt bytes", () => {
                byte[] bytes = Attempt(); bytes[15] = 0xff;
                Check(!ReleaseFinalizationRules.TrySubmission(bytes, Bytes(Observation(bytes)), Item, out _, out _));
            });
            foreach (string requested in new[] { null, "", "0.3.1", "0.3.0", "0.3.01", "0.4.1", "1.3.2", "0.3.-1", "0.3.2.0", "0.3.1000000000" })
                Case("later version refuses " + (requested ?? "null"), () => Check(!ReleaseFinalizationRules.Higher(requested, "0.3.1")));
            Case("numeric strict higher patch permits gaps and decimal carry", () => {
                Check(ReleaseFinalizationRules.Higher("0.3.10", "0.3.9"));
                Check(ReleaseFinalizationRules.Higher("0.3.99", "0.3.1"));
                Check(!ReleaseFinalizationRules.Higher("0.3.9", "0.3.10"));
            });
            foreach (string altered in new[] { "marker", "directory", "ordinal", "previous", "installation", "trailing", "foreign-submission" })
                Case("finalization exact binding " + altered, () => {
                    var observation = Observation(Attempt()); var installation = Installation(observation);
                    byte[] bytes = installation.Encode();
                    byte[] final = ReleaseFinalizationRules.FinalizationBytes(1, "", ReleaseFinalizationRules.Sha(bytes));
                    if (altered == "installation") bytes = Installation(observation, inventory: Hash('f')).Encode();
                    if (altered == "trailing") Array.Resize(ref final, final.Length + 1);
                    Check(!ReleaseFinalizationRules.Finalized(altered == "foreign-submission" ? Observation(Attempt(), plan: Hash('f')) : observation,
                        bytes, final, altered == "ordinal" ? 2 : 1, altered == "previous" ? Hash('f') : "",
                        altered == "marker" ? Hash('f') : Hash('d'), altered == "directory" ? Hash('f') : Hash('e'), out _));
                });
            Case("every finalization truncation refuses", () => {
                var observation = Observation(Attempt()); byte[] installed = Installation(observation).Encode();
                byte[] final = ReleaseFinalizationRules.FinalizationBytes(1, "", ReleaseFinalizationRules.Sha(installed));
                for (int i = 0; i < final.Length; i++)
                {
                    byte[] cut = new byte[i]; Array.Copy(final, cut, i);
                    Check(!ReleaseFinalizationRules.Finalized(observation, installed, cut, 1, "", Hash('d'), Hash('e'), out _));
                }
            });
            Case("finalization sequence and previous-link bounds", () => {
                foreach (int ordinal in new[] { 0, 65, int.MaxValue })
                    Refuses(() => ReleaseFinalizationRules.FinalizationBytes(ordinal, Hash('d'), Hash('e')));
                Refuses(() => ReleaseFinalizationRules.FinalizationBytes(2, "", Hash('e')));
                Check(ReleaseFinalizationRules.FinalizationBytes(64, Hash('d'), Hash('e')).Length < 512);
            });
            Case("native creation API accepts no synthetic result or arbitrary attempt selector", () => {
                string source = Source("SteamVerify.cs");
                Check(source.Contains("internal DeliveryResult VerifyAndFinalize(UploadPackage package, out string finalizationSHA)"));
                Ordered(source, "internal DeliveryResult VerifyAndFinalize", "ReadHistory();", "SteamInstalledDelivery.Verify(package",
                    "ReleaseInstallationObservation.TryCapture(", "latest.Attempt.CreateInstallation(", "UploadAttemptLease.Create(attemptPath");
                Check(!source.Contains("Directory.Delete(") && !source.Contains("File.Delete(") && !source.Contains(".Move("));
                Check(!Source("WorkshopReleaseRegistry.Finalization.cs").Contains("SteamInstalledDelivery"));
                Check(!Source("ReleaseInstallationObservation.cs").Contains("DeliveryResult"));
            });
            Case("CLI retains first-attempt branch and cleanup-before-positive output", () => {
                string publish = Source("SteamPublish.cs"), verify = Source("SteamVerify.cs");
                Ordered(publish, "if (retained) registry.RequireFinalizedHistory();", "UploadPackage.Open(", "registry.PlannedAttemptPath(package)",
                    "registry.BeginAttempt(package) : registry.BeginFirstAttempt()", "UploadProtocol().Run(");
                Ordered(verify, "using (WorkshopReleaseRegistry registry", "using (UploadPackage package", "registry.VerifyAndFinalize(",
                    "if (result == null)", "Console.WriteLine(JsonSerializer.Serialize(new");
            });
            Case("source contract: verify observes; only explicit finalize reaches retained writes", () => {
                string source = Source("SteamVerify.cs");
                int mainEnd = source.IndexOf("public sealed partial class WorkshopReleaseRegistry", StringComparison.Ordinal);
                Check(mainEnd > 0); string main = source.Substring(0, mainEnd);
                Ordered(main, "args == null || args.Length != 5 || (args[0] != \"verify\" && args[0] != \"finalize\")",
                    "operation = args[0];", "WorkshopReleaseRegistry.Open(item)", "UploadPackage.Open(", "if (operation == \"finalize\")");
                int branch = main.IndexOf("if (operation == \"finalize\")", StringComparison.Ordinal);
                int otherwise = main.IndexOf("else", branch, StringComparison.Ordinal);
                int cleanup = main.IndexOf("// Both package and registry cleanup", otherwise, StringComparison.Ordinal);
                Check(branch >= 0 && otherwise > branch && cleanup > otherwise);
                string observed = main.Substring(otherwise, cleanup - otherwise);
                Ordered(main.Substring(branch, otherwise - branch), "if (operation == \"finalize\")",
                    "result = registry.VerifyAndFinalize(package, out finalizationSHA);");
                Ordered(observed, "result = SteamInstalledDelivery.Verify(package, path =>", "registry.RequireExact();",
                    "return package.LeaseInstalled(path);", "registry.RequireExact();");
                foreach (string effect in new[] { "VerifyAndFinalize(", "ReadHistory(", "CreateInstallation(",
                    "UploadAttemptLease.Create(", "finalizationSHA =" }) Check(!observed.Contains(effect));
                Check(!main.Substring(0, branch).Contains("VerifyAndFinalize(") && !main.Contains("ReadHistory("));
                Check(main.Contains("string operation = null, finalizationSHA = null;"));
                Ordered(main, "// Both package and registry cleanup", "if (result == null)",
                    "operation = operation,", "finalizationSHA = finalizationSHA,",
                    "attemptFinalized = operation == \"finalize\" && finalizationSHA != null");
                string refused = main.Substring(main.IndexOf("catch (Exception error)", StringComparison.Ordinal));
                Check(refused.Contains("operation = operation,") && refused.Contains("finalizationSHA = finalizationSHA,"));
            });
            RecandidatePure();
            return Result("Release finalization pure/source fixtures");
        }

        public static int RunWindows()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return 2;
            passed = failed = 0;
            Case("first planning does not create; next fence stays after cleanup", () => {
                string root = Root();
                using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                {
                    string planned = Plan(registry, "0.3.1");
                    Check(!Directory.Exists(Path.GetDirectoryName(planned)));
                    Check(Begin(registry, "0.3.1") == planned);
                    Refuses(() => Begin(registry, "0.3.2"));
                }
                using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                    Refuses(() => registry.RequireFinalizedHistory());
            });
            Case("complete exact private chain admits equal or higher unseen package", () => {
                string root = Root(); string first = Seed(root, 1, "0.3.1", "");
                Seed(root, 2, "0.3.9", first); byte[] retained = File.ReadAllBytes(AttemptPath(root, 1) + ".finalization.json");
                using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                {
                    registry.RequireFinalizedHistory();
                    foreach (string version in new[] { "0.3.8", "0.4.10" }) Refuses(() => Plan(registry, version));
                    Check(Plan(registry, "0.3.9") == AttemptPath(root, 3));
                    string path = Plan(registry, "0.3.10");
                    Check(path == AttemptPath(root, 3) && !Directory.Exists(Path.GetDirectoryName(path)));
                    Check(Begin(registry, "0.3.10") == path); registry.RequireExact();
                }
                Check(ReleaseFinalizationRules.Equal(retained, File.ReadAllBytes(AttemptPath(root, 1) + ".finalization.json")));
            });
            foreach (string shape in new[] { "empty", "attempt", "submission", "installation", "unknown", "timeout", "io", "legal", "rejected", "junk", "bad-finalization", "foreign-directory" })
                Case("retained tail refuses " + shape, () => {
                    string root = Root(); Seed(root, 1, "0.3.1", "", shape);
                    using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                    { Refuses(() => registry.RequireFinalizedHistory()); Refuses(() => Begin(registry, "0.3.2")); }
                    Check(!Directory.Exists(Path.GetDirectoryName(AttemptPath(root, 2))));
                });
            foreach (string shape in new[] { "gap", "regression", "wrong-previous", "extra" })
                Case("history topology refuses " + shape, () => {
                    string root = Root(), first = Seed(root, 1, "0.3.1", "");
                    if (shape == "extra") WriteNew(Path.Combine(Seat(root), "attempts", "unrecognized"), new byte[] { 1 });
                    else Seed(root, shape == "gap" ? 3 : 2, shape == "regression" ? "0.3.0" : "0.3.2",
                        shape == "wrong-previous" ? Hash('f') : first);
                    using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL)) Refuses(() => registry.RequireFinalizedHistory());
                });
            Case("held history denies writers and retains exact bytes", () => {
                string root = Root(); Seed(root, 1, "0.3.1", "");
                using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                {
                    registry.RequireFinalizedHistory();
                    foreach (string suffix in new[] { "", ".submission.json", ".installation.json", ".finalization.json" })
                        Refuses(() => { using (var writer = new FileStream(AttemptPath(root, 1) + suffix, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } });
                    registry.RequireExact();
                }
            });
            Case("Inspect stays names-only after finalized history was loaded", () => {
                string root = Root(); Seed(root, 1, "0.3.1", "");
                string path = AttemptPath(root, 1);
                string[] suffixes = { "", ".submission.json", ".installation.json", ".finalization.json" };
                byte[][] records = Array.ConvertAll(suffixes, suffix => File.ReadAllBytes(path + suffix));
                using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                {
                    registry.RequireFinalizedHistory(); registry.RequireExact();
                    string[] before = registry.Inspect();
                    Check(Array.IndexOf(before, "retainedEntry=0001") >= 0
                        && Array.IndexOf(before, "retainedEntries=1") >= 0);
                    // Fault only the held child lease, not its directory or records. Other record
                    // leases still pin all ancestors; this is not a simulated filesystem deletion.
                    var field = typeof(WorkshopReleaseRegistry).GetField("directories",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    Check(field != null);
                    var held = field.GetValue(registry) as System.Collections.Generic.List<UploadAttemptLease.DirectoryLease>;
                    Check(held != null);
                    var child = held.Find(lease => string.Equals(lease.Path, Path.GetDirectoryName(path), StringComparison.OrdinalIgnoreCase));
                    Check(child != null && child.Revalidate()); child.Dispose(); Check(!child.Revalidate());
                    Check(string.Join("\n", before) == string.Join("\n", registry.Inspect()));
                    Refuses(() => registry.RequireExact());
                    Refuses(() => Plan(registry, "0.3.2"));
                    Refuses(() => Begin(registry, "0.3.2"));
                    Check(!Directory.Exists(Path.GetDirectoryName(AttemptPath(root, 2))));
                    Check(string.Join("\n", before) == string.Join("\n", registry.Inspect()));
                    for (int i = 0; i < suffixes.Length; i++)
                        Check(ReleaseFinalizationRules.Equal(records[i], File.ReadAllBytes(path + suffixes[i])));
                }
                for (int i = 0; i < suffixes.Length; i++)
                    Check(ReleaseFinalizationRules.Equal(records[i], File.ReadAllBytes(path + suffixes[i])));
            });
            Case("new foreign child invalidates a held history", () => {
                string root = Root(); Seed(root, 1, "0.3.1", "");
                using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                {
                    registry.RequireFinalizedHistory();
                    WriteNew(AttemptPath(root, 1) + ".unknown", new byte[] { 1 });
                    Refuses(() => registry.RequireExact()); Refuses(() => Begin(registry, "0.3.2"));
                }
            });
            Case("sixty-four complete attempts cannot grow or evict history", () => {
                string root = Root(), previous = "";
                for (int i = 1; i <= 64; i++) previous = Seed(root, i, "0.3." + i.ToString(CultureInfo.InvariantCulture), previous);
                using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                { registry.RequireFinalizedHistory(); Refuses(() => Plan(registry, "0.3.65")); }
                Check(Directory.GetFileSystemEntries(Path.Combine(Seat(root), "attempts")).Length == 64);
            });
            RecandidateWindows();
            return Result("Release finalization synthetic Windows fixtures; no SDK/finalizer execution");
        }

        private static byte[] Attempt(string version = "0.3.1", string item = Item)
        {
            return JsonSerializer.SerializeToUtf8Bytes(new { attemptId = "11111111-1111-1111-1111-111111111111",
                item = item, manifestId = "r_ThousandAndFirst", requestVersion = version, steamVisibility = item == Item ? 2 : 0,
                planSHA = Hash('b'), receiptSHA = Hash('c'), packagePath = Content, contentPath = Content, startUtc = Stamp });
        }
        private static ReleaseSubmissionObservation Observation(byte[] attempt, string version = "0.3.1",
            ReleaseSubmissionCompletion completion = ReleaseSubmissionCompletion.Ok, string attemptSHA = null,
            string previous = null, string plan = null, string receipt = null, string stamp = Stamp, string item = Item)
        {
            bool observed = completion != ReleaseSubmissionCompletion.TimedOut;
            ReleaseSubmissionObservation value; string reason;
            Check(ReleaseSubmissionObservation.TryCreate(attemptSHA ?? ReleaseFinalizationRules.Sha(attempt), previous, item,
                version, plan ?? Hash('b'), receipt ?? Hash('c'), stamp, observed,
                observed ? (int?)(completion == ReleaseSubmissionCompletion.Rejected ? 2 : 1) : null,
                observed ? item : null, observed ? (bool?)(completion == ReleaseSubmissionCompletion.IoFailure) : null,
                observed ? (bool?)(completion == ReleaseSubmissionCompletion.LegalAgreementRequired) : null,
                completion, null, out value, out reason));
            return value;
        }
        private static ReleaseInstallationObservation Installation(ReleaseSubmissionObservation observation,
            string marker = null, string directory = null, string inventory = null)
        {
            // Canonical synthetic bytes are shape fixtures only, never live installation proof.
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new[] { ReleaseInstallationObservation.Schema,
                observation.AttemptSHA, ReleaseFinalizationRules.Sha(Bytes(observation)), observation.Item,
                observation.RequestVersion, observation.PlanSHA, observation.ReceiptSHA,
                marker ?? Hash('d'), directory ?? Hash('e'), inventory ?? Hash('a'), Later, ReleaseInstallationObservation.Scope });
            ReleaseInstallationObservation installed;
            Check(ReleaseInstallationObservation.TryDecode(bytes, out installed));
            return installed;
        }
        private static byte[] Bytes(ReleaseSubmissionObservation value) { return ReleaseSubmissionObservationCodec.EncodeUtf8(value); }

        private static string Root(string item = Item)
        {
            string root = Path.Combine(Path.GetTempPath(), "taf-repeat-release-test." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); Console.WriteLine("retained synthetic fixture=" + root);
            using (var registry = WorkshopReleaseRegistry.OpenCore(root, ulong.Parse(item, CultureInfo.InvariantCulture))) registry.RequireExact();
            return root;
        }
        private static string Seat(string root, string item = Item) { return Path.Combine(root, "registry", item); }
        private static string AttemptPath(string root, int ordinal, string item = Item)
        { return Path.Combine(Seat(root, item), "attempts", ordinal.ToString("D4", CultureInfo.InvariantCulture), item + ".active.attempt.json"); }
        private static string Seed(string root, int ordinal, string version, string previous, string shape = "finalized", string inventory = null, string item = Item)
        {
            string path = AttemptPath(root, ordinal, item); Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (shape == "empty") return null;
            byte[] attempt = Attempt(version, item); WriteNew(path, attempt);
            if (shape == "attempt") return null;
            var completion = shape == "timeout" ? ReleaseSubmissionCompletion.TimedOut : shape == "unknown" ? ReleaseSubmissionCompletion.Unknown
                : shape == "io" ? ReleaseSubmissionCompletion.IoFailure : shape == "legal" ? ReleaseSubmissionCompletion.LegalAgreementRequired
                : shape == "rejected" ? ReleaseSubmissionCompletion.Rejected : ReleaseSubmissionCompletion.Ok;
            var observation = Observation(attempt, version, completion, item: item); WriteNew(path + ".submission.json", Bytes(observation));
            if (shape == "submission") return null;
            string directory;
            using (var lease = UploadAttemptLease.DirectoryLease.Open(Path.GetDirectoryName(path))) directory = lease.IdentityDigest();
            byte[] installed = Installation(observation, ReleaseFinalizationRules.Sha(File.ReadAllBytes(Path.Combine(Seat(root, item), "registry.marker.json"))),
                shape == "foreign-directory" ? Hash('f') : directory,
                inventory ?? ReleaseFinalizationRules.Sha(Encoding.UTF8.GetBytes("synthetic inventory " + ordinal))).Encode();
            WriteNew(path + ".installation.json", installed);
            if (shape == "installation") return null;
            byte[] final = ReleaseFinalizationRules.FinalizationBytes(ordinal, previous, ReleaseFinalizationRules.Sha(installed));
            if (shape == "bad-finalization") Array.Resize(ref final, final.Length + 1);
            WriteNew(path + ".finalization.json", final);
            if (shape == "junk") WriteNew(path + ".unknown", new byte[] { 1 });
            return ReleaseFinalizationRules.Sha(final);
        }
        private static void WriteNew(string path, byte[] bytes)
        { using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); } }
        private static string Source(string name, [CallerFilePath] string source = "")
        { return File.ReadAllText(Path.Combine(Path.GetDirectoryName(source), name)); }
        private static void Ordered(string source, params string[] tokens)
        {
            int at = 0;
            foreach (string token in tokens)
            { int next = source.IndexOf(token, at, StringComparison.Ordinal); Check(next >= at); at = next + token.Length; }
        }
        private static void Check(bool value) { if (!value) throw new InvalidOperationException("finalization fixture assertion failed"); }
        private static void Refuses(Action action)
        {
            bool refused = false;
            try { action(); }
            catch (IOException) { refused = true; }
            catch (InvalidDataException) { refused = true; }
            catch (UnauthorizedAccessException) { refused = true; }
            Check(refused);
        }
        private static void Case(string name, Action action)
        { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); } }
        private static int Result(string label)
        { Console.WriteLine(label + ": passed=" + passed + " failed=" + failed); return failed == 0 ? 0 : 1; }
    }
}
