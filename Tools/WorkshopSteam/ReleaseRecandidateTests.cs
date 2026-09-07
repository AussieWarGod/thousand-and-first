using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst.WorkshopSteam
{
    public static partial class ReleaseFinalizationTests
    {
        private static void RecandidatePure()
        {
            foreach (string item in new[] { Item, "3794797472" })
                foreach (string requested in new[] { "0.3.0", "0.3.1", "0.3.2", "0.3.01", "0.4.1", null })
                    Case("lane version " + item + " / " + (requested ?? "null"), () => {
                        bool expected = requested == "0.3.2" || item == Item && requested == "0.3.1";
                        Check(ReleaseFinalizationRules.VersionAllowed(item, requested, "0.3.1") == expected);
                    });
            Case("unknown lane never admits a recandidate", () =>
                Check(!ReleaseFinalizationRules.VersionAllowed("12345", "0.3.2", "0.3.1")));
            Case("canonical inventory retains historical installed framing", () => {
                var rows = InventoryRows();
                Check(UploadPackage.CanonicalInventorySHA(rows)
                    == "2af2bdc878994b2d77c9d17648b4afa4c7b1a096f466d695f4152b1418315d66");
                Array.Reverse(rows);
                Check(UploadPackage.CanonicalInventorySHA(rows) == UploadPackage.CanonicalInventorySHA(InventoryRows()));
            });
            foreach (string change in new[] { "name", "hash", "size" })
                Case("canonical inventory distinguishes " + change, () => {
                    var rows = InventoryRows();
                    rows[0] = new UploadPackage.InventoryRow(change == "name" ? "Core/Other.cs" : rows[0].Name,
                        change == "hash" ? Hash('c') : rows[0].Hash, change == "size" ? 8 : rows[0].Size);
                    Check(UploadPackage.CanonicalInventorySHA(rows) != UploadPackage.CanonicalInventorySHA(InventoryRows()));
                });
            foreach (string bad in new[] { "null", "empty", "duplicate", "bad-name", "bad-hash", "negative", "overflow" })
                Case("canonical inventory refuses " + bad, () => {
                    var rows = InventoryRows();
                    if (bad == "null") rows = null;
                    else if (bad == "empty") rows = new UploadPackage.InventoryRow[0];
                    else if (bad == "duplicate") rows[1] = rows[0];
                    else rows[0] = new UploadPackage.InventoryRow(bad == "bad-name" ? "../escape" : rows[0].Name,
                        bad == "bad-hash" ? "not-a-hash" : rows[0].Hash,
                        bad == "negative" ? -1 : bad == "overflow" ? long.MaxValue : rows[0].Size);
                    Refuses(() => UploadPackage.CanonicalInventorySHA(rows));
                });
            Case("unseen inventory checks all earlier attempts, not only latest", () => {
                Check(ReleaseFinalizationRules.UnseenInventory(Hash('c'), new[] { Hash('a'), Hash('b') }));
                Check(!ReleaseFinalizationRules.UnseenInventory(Hash('a'), new[] { Hash('a'), Hash('b') }));
                Check(!ReleaseFinalizationRules.UnseenInventory(Hash('b'), new[] { Hash('a'), Hash('b') }));
            });
            Case("unseen inventory refuses missing malformed and overbound history", () => {
                Check(!ReleaseFinalizationRules.UnseenInventory(null, new string[0]));
                Check(!ReleaseFinalizationRules.UnseenInventory(Hash('a'), null));
                Check(!ReleaseFinalizationRules.UnseenInventory(Hash('a'), new[] { "bad" }));
                Check(!ReleaseFinalizationRules.UnseenInventory(Hash('a'), new string[65]));
            });
            Case("native admission derives inventory only from exact leased package", () => {
                string registry = Source("WorkshopReleaseRegistry.Finalization.cs");
                Ordered(registry, "internal string PlannedAttemptPath(UploadPackage package)", "RequireFinalizedHistory();",
                    "package.Request.Item != item", "package.Revalidate()", "ReleaseFinalizationRules.VersionAllowed(",
                    "RequireUnseenInventory(package.VerifiedInventorySHA());", "RequireExact();");
                Ordered(registry, "internal string BeginAttempt(UploadPackage package)", "PlannedAttemptPath(package)",
                    "begun = true;", "attempts.CreateChild(name)", "RequireExact();");
                Check(!registry.Contains("PlannedAttemptPath(string") && !registry.Contains("BeginAttempt(string"));
                Check(registry.Contains("ReleaseFinalizationRules.UnseenInventory(installed.InventorySHA, Inventories(read))"));
                string verify = Source("SteamVerify.cs");
                Ordered(verify, "SteamInstalledDelivery.Verify(package", "ReleaseInstallationObservation.TryCapture(",
                    "observed.InventorySHA != package.VerifiedInventorySHA()", "RequireUnseenInventory(observed.InventorySHA)",
                    "latest.Attempt.CreateInstallation(");
                Check(!Source("UploadPackage.Inventory.cs").Contains("Steamworks"));
                Check(Source("UploadPackage.Install.cs").Contains("mirror.InstallInventorySHA()"));
            });
        }

        private static UploadPackage.InventoryRow[] InventoryRows()
        {
            return new[] { new UploadPackage.InventoryRow("Core/Payload.cs", Hash('a'), 7),
                new UploadPackage.InventoryRow("é.txt", Hash('b'), 11) };
        }

        private static string Plan(WorkshopReleaseRegistry registry, string version)
        {
            using (var fixture = new UploadPackageTests.Fixture(version: version, item: Item))
            using (UploadPackage package = fixture.Open()) return registry.PlannedAttemptPath(package);
        }

        private static string Begin(WorkshopReleaseRegistry registry, string version)
        {
            using (var fixture = new UploadPackageTests.Fixture(version: version, item: Item))
            using (UploadPackage package = fixture.Open()) return registry.BeginAttempt(package);
        }

        private static void RecandidateWindows()
        {
            Case("private equal-version corrected bytes claim a new immutable attempt", () => {
                using (var fixture = new UploadPackageTests.Fixture(version: "0.3.1", item: Item))
                {
                    string original;
                    using (UploadPackage package = fixture.Open()) original = package.VerifiedInventorySHA();
                    File.AppendAllText(fixture.Payload, "// corrected synthetic bytes\n"); fixture.Rebuild();
                    string root = Root(); Seed(root, 1, "0.3.1", "", inventory: original);
                    byte[][] before = Records(root, 1);
                    using (UploadPackage package = fixture.Open())
                    using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                    {
                        Check(package.VerifiedInventorySHA() != original);
                        Check(registry.PlannedAttemptPath(package) == AttemptPath(root, 2));
                        Check(!Directory.Exists(Path.GetDirectoryName(AttemptPath(root, 2))));
                        Check(registry.BeginAttempt(package) == AttemptPath(root, 2));
                        Refuses(() => registry.BeginAttempt(package));
                    }
                    SameRecords(before, Records(root, 1));
                    Check(Directory.GetFileSystemEntries(Path.Combine(Seat(root), "attempts")).Length == 2);
                }
            });
            foreach (bool receiptVariant in new[] { false, true })
                Case("identical private bytes refuse despite new path/receipt variant " + receiptVariant, () => {
                    using (var first = new UploadPackageTests.Fixture(version: "0.3.1", item: Item))
                    using (var copy = new UploadPackageTests.Fixture(version: "0.3.1", item: Item))
                    {
                        if (receiptVariant)
                        {
                            string[] rows = File.ReadAllText(copy.Receipt).TrimEnd('\n').Split('\n');
                            Array.Reverse(rows);
                            copy.ChangeReceipt(string.Join("\n", rows).Replace("  ./", " *") + "\n");
                        }
                        using (UploadPackage original = first.Open())
                        using (UploadPackage package = copy.Open())
                        {
                            Check(original.PlanSHA != package.PlanSHA);
                            if (receiptVariant) Check(original.ReceiptSHA != package.ReceiptSHA);
                            Check(original.VerifiedInventorySHA() == package.VerifiedInventorySHA());
                            string root = Root(); Seed(root, 1, "0.3.1", "", inventory: original.VerifiedInventorySHA());
                            byte[][] before = Records(root, 1);
                            using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                            { Refuses(() => registry.PlannedAttemptPath(package)); Refuses(() => registry.BeginAttempt(package)); }
                            Check(!Directory.Exists(Path.GetDirectoryName(AttemptPath(root, 2))));
                            SameRecords(before, Records(root, 1));
                        }
                    }
                });
            Case("nonadjacent private same-version replay refuses", () => {
                using (var fixture = new UploadPackageTests.Fixture(version: "0.3.1", item: Item))
                using (UploadPackage package = fixture.Open())
                {
                    string root = Root(), first = Seed(root, 1, "0.3.1", "", inventory: package.VerifiedInventorySHA());
                    Seed(root, 2, "0.3.1", first, inventory: Hash('b'));
                    using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                    { registry.RequireFinalizedHistory(); Refuses(() => registry.BeginAttempt(package)); }
                    Check(!Directory.Exists(Path.GetDirectoryName(AttemptPath(root, 3))));
                }
            });
            Case("duplicate finalized inventories refuse even with distinct receipts", () => {
                string root = Root(), first = Seed(root, 1, "0.3.1", "", inventory: Hash('a'));
                Seed(root, 2, "0.3.2", first, inventory: Hash('a'));
                using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                    Refuses(() => registry.RequireFinalizedHistory());
            });
            Case("public equal-version distinct bytes refuse; higher remains admitted", () => {
                const string publicItem = "3794797472";
                string root = Root(publicItem); Seed(root, 1, "0.3.1", "", item: publicItem);
                using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3794797472UL))
                {
                    using (var fixture = new UploadPackageTests.Fixture(true, "0.3.1"))
                    using (UploadPackage package = fixture.Open()) Refuses(() => registry.BeginAttempt(package));
                    using (var fixture = new UploadPackageTests.Fixture(true, "0.3.2"))
                    using (UploadPackage package = fixture.Open())
                        Check(registry.BeginAttempt(package) == AttemptPath(root, 2, publicItem));
                }
            });
            Case("disposed or foreign package cannot authorize a claim", () => {
                string root = Root(); Seed(root, 1, "0.3.1", "");
                using (var registry = WorkshopReleaseRegistry.OpenCore(root, 3796495680UL))
                {
                    using (var fixture = new UploadPackageTests.Fixture(true, "0.3.2"))
                    using (UploadPackage package = fixture.Open()) Refuses(() => registry.BeginAttempt(package));
                    using (var fixture = new UploadPackageTests.Fixture(version: "0.3.2", item: Item))
                    {
                        UploadPackage package = fixture.Open(); package.Dispose();
                        Refuses(() => registry.BeginAttempt(package));
                        Refuses(() => package.VerifiedInventorySHA());
                    }
                }
                Check(!Directory.Exists(Path.GetDirectoryName(AttemptPath(root, 2))));
            });
        }

        private static byte[][] Records(string root, int ordinal)
        {
            string path = AttemptPath(root, ordinal);
            return Array.ConvertAll(new[] { "", ".submission.json", ".installation.json", ".finalization.json" },
                suffix => File.ReadAllBytes(path + suffix));
        }

        private static void SameRecords(byte[][] expected, byte[][] actual)
        {
            Check(expected.Length == actual.Length);
            for (int i = 0; i < expected.Length; i++) Check(ReleaseFinalizationRules.Equal(expected[i], actual[i]));
        }
    }
}
