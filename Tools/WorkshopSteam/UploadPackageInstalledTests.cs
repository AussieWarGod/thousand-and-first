using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ThousandAndFirst.WorkshopSteam
{
    public static partial class UploadPackageTests
    {
        [DllImport("kernel32.dll", EntryPoint = "GetShortPathNameW", CharSet = CharSet.Unicode,
            ExactSpelling = true, SetLastError = true)]
        private static extern uint InstalledShortPath(string path, StringBuilder buffer, uint length);

        public static int RunInstalled()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return 2;
            passed = failed = 0;
            Case("installed exact copy, digest, and independent lease disposal", InstalledExact);
            Case("installed missing, extra, changed, and wrong-kind entries refuse", InstalledInventory);
            Case("installed wrong item, app, and noncanonical paths refuse", InstalledPaths);
            Case("installed same or ancestor of approved source refuses", InstalledOverlap);
            Case("8.3 source alias refuses at canonical or physical boundary", InstalledPhysicalAlias);
            Case("installed receipt or plan overlap refuses", InstalledInputOverlap);
            Case("installed drift is sticky and releases only its own handles", InstalledDrift);
            Case("original drift invalidates installed evidence", InstalledSourceDrift);
            Case("original disposal invalidates installed evidence", InstalledSourceDisposed);
            Case("installed hardlink refuses without harming original", InstalledHardLink);
            Case("installed NFC alias refuses", InstalledAlias);
            Case("installed junction and linked ancestor refuse", InstalledReparse);
            Case("installed digest is culture and root independent", InstalledCulture);
            Case("delivery rejects invalid inputs before SDK initialization", InstalledAdapterInputs);
            Console.WriteLine("Installed package synthetic Windows fixtures: passed=" + passed + " failed=" + failed);
            return failed == 0 ? 0 : 1;
        }

        private static void InstalledExact()
        {
            using (Fixture f = new Fixture())
            {
                string target = InstalledCopy(f);
                using (UploadPackage p = f.Open())
                {
                    IInstalledDeliveryEvidence proof = p.LeaseInstalled(target);
                    string digest = proof.InventorySHA;
                    try
                    {
                        Check(proof.Revalidate() && Regex.IsMatch(digest, "\\A[0-9a-f]{64}\\z"));
                        Check(digest == ExpectedInstalledDigest(target));
                        foreach (string path in Directory.GetFiles(target, "*", SearchOption.AllDirectories))
                        {
                            Throws<IOException>(() => File.WriteAllText(path, "changed"));
                            Throws<IOException>(() => File.Move(path, path + ".moved"));
                        }
                        Throws<IOException>(() => Directory.Move(target, target + ".moved"));
                        Throws<IOException>(() => File.WriteAllText(f.PlanPath, "changed"));
                    }
                    finally { proof.Dispose(); }
                    proof.Dispose(); Check(!proof.Revalidate() && proof.InventorySHA == digest && p.Revalidate());
                    using (FileStream stream = new FileStream(Path.Combine(target, "Core", "Payload.cs"),
                        FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Check(stream.CanWrite);
                    Throws<IOException>(() => File.WriteAllText(f.Payload, "changed"));
                }
                f.ProveWritable();
            }
        }

        private static void InstalledInventory()
        {
            foreach (int kind in new[] { 0, 1, 2, 3 }) using (Fixture f = new Fixture())
            {
                string target = InstalledCopy(f), payload = Path.Combine(target, "Core", "Payload.cs");
                if (kind == 0) File.Delete(payload);
                if (kind == 1) File.WriteAllText(Path.Combine(target, "unexpected"), "extra");
                if (kind == 2) File.WriteAllText(payload, "// same-size wrong payload!\n", Utf8);
                if (kind == 3) { File.Delete(payload); Directory.CreateDirectory(payload); }
                using (UploadPackage p = f.Open()) { RefuseInstalled(p, target); Check(p.Revalidate()); }
                f.ProveWritable();
            }
        }

        private static void InstalledPaths()
        {
            using (Fixture f = new Fixture()) using (UploadPackage p = f.Open())
            {
                string target = InstalledCopy(f);
                foreach (string path in new[] { null, "", f.Content, target + "\\", target + ".",
                    target + " ", target + "\\..\\12345", target.Replace("333640", "333641"),
                    target.Replace("12345", "012345"), target.Replace("steamapps", "foreign"),
                    target.Replace("12345", "12346"), @"\\server\steamapps\workshop\content\333640\12345" })
                    RefuseInstalled(p, path);
                Check(p.Revalidate());
            }
        }

        private static void InstalledOverlap()
        {
            foreach (bool nested in new[] { false, true }) using (Fixture f = new Fixture())
            {
                string target = InstalledRoot(f), source = nested ? Path.Combine(target, "candidate") : target;
                CopyInstalledFiles(f.Content, source);
                f.Plan["contentPath"] = Fixture.Posix(source);
                f.Plan["previewPath"] = Fixture.Posix(Path.Combine(source, "preview.png")); f.WritePlan();
                using (UploadPackage p = f.Open()) { RefuseInstalled(p, target); Check(p.Revalidate()); }
            }
        }
        private static void InstalledInputOverlap()
        {
            foreach (bool plan in new[] { false, true }) using (Fixture f = new Fixture())
            {
                string target = InstalledCopy(f), input = Path.Combine(target, plan ? "approved-plan.json" : "approved-receipt.sha256");
                if (!plan) { File.Copy(f.Receipt, input); f.Plan["receiptPath"] = Fixture.Posix(input); f.WritePlan(); }
                else File.Copy(f.PlanPath, input);
                using (UploadPackage p = UploadPackage.Open(plan ? input : f.PlanPath, f.Hash, f.Item, "Synthetic fixture"))
                { RefuseInstalled(p, target); Check(p.Revalidate()); }
            }
        }
        private static void InstalledPhysicalAlias()
        {
            using (Fixture f = new Fixture())
            {
                string target = InstalledCopy(f); StringBuilder buffer = new StringBuilder(32768);
                uint length = InstalledShortPath(target, buffer, 32768);
                Check(length > 0 && length < 32768 && buffer.Length == length);
                string alias = buffer.ToString();
                if (string.Equals(alias, target, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("8.3 alias fixture unavailable; no alias coverage claimed");
                f.Plan["contentPath"] = Fixture.Posix(alias);
                f.Plan["previewPath"] = Fixture.Posix(Path.Combine(alias, "preview.png")); f.WritePlan();
                UploadPackage p = null;
                try
                {
                    try { p = f.Open(); }
                    catch (InvalidDataException error)
                    { Check(error.Message == "noncanonical native path"); f.ProveWritable(); return; }
                    bool refused = false;
                    try { using (IInstalledDeliveryEvidence unexpected = p.LeaseInstalled(target)) Check(false); }
                    catch (InvalidDataException error)
                    { refused = error.Message == "source and installed physical content overlap"; }
                    Check(refused && p.Revalidate());
                }
                finally { if (p != null) p.Dispose(); }
            }
        }
        private static void InstalledAdapterInputs()
        {
            DeliveryResult missing = SteamInstalledDelivery.Verify(null, null);
            Check(!missing.SubscribedInstallationVerified && !missing.FreshTransferVerified
                && !missing.ReleaseReady && missing.Reason == "missing_input");
            using (Fixture f = new Fixture()) using (UploadPackage p = f.Open())
            {
                DeliveryResult wrong = SteamInstalledDelivery.Verify(p, p.LeaseInstalled);
                Check(wrong.Reason == "wrong_item_lane" && !wrong.SubscribedInstallationVerified);
                foreach (int timeout in new[] { 0, -1, 120001 })
                    Check(SteamInstalledDelivery.Verify(p, p.LeaseInstalled, timeout).Reason == "timeout_out_of_bounds");
                Check(p.Revalidate());
            }
        }
        private static void InstalledDrift()
        {
            using (Fixture f = new Fixture()) using (UploadPackage p = f.Open())
            {
                string target = InstalledCopy(f), extra = Path.Combine(target, "later-extra");
                using (IInstalledDeliveryEvidence proof = p.LeaseInstalled(target))
                {
                    File.WriteAllText(extra, "extra"); Check(!proof.Revalidate());
                    File.Delete(extra); Check(!proof.Revalidate() && p.Revalidate());
                }
                using (IInstalledDeliveryEvidence next = p.LeaseInstalled(target)) Check(next.Revalidate());
            }
        }
        private static void InstalledSourceDrift()
        {
            using (Fixture f = new Fixture()) using (UploadPackage p = f.Open())
            using (IInstalledDeliveryEvidence proof = p.LeaseInstalled(InstalledCopy(f)))
            {
                string extra = Path.Combine(f.Content, "later-extra");
                File.WriteAllText(extra, "extra"); Check(!proof.Revalidate());
                File.Delete(extra); Check(!proof.Revalidate() && !p.Revalidate());
            }
        }

        private static void InstalledSourceDisposed()
        {
            using (Fixture f = new Fixture())
            {
                UploadPackage p = f.Open();
                try
                {
                    using (IInstalledDeliveryEvidence proof = p.LeaseInstalled(InstalledCopy(f)))
                    { p.Dispose(); Check(!proof.Revalidate()); }
                    RefuseInstalled(p, InstalledRoot(f)); f.ProveWritable();
                }
                finally { p.Dispose(); }
            }
        }

        private static void InstalledHardLink()
        {
            using (Fixture f = new Fixture())
            {
                string target = InstalledCopy(f);
                Check(CreateHardLinkW(Path.Combine(f.Root, "owned-installed-link"),
                    Path.Combine(target, "Core", "Payload.cs"), IntPtr.Zero));
                using (UploadPackage p = f.Open()) { RefuseInstalled(p, target); Check(p.Revalidate()); }
                f.ProveWritable();
            }
        }

        private static void InstalledAlias()
        {
            using (Fixture f = new Fixture())
            {
                File.WriteAllText(Path.Combine(f.Content, "é.txt"), "same", Utf8); f.Rebuild();
                string target = InstalledCopy(f);
                File.WriteAllText(Path.Combine(target, "e\u0301.txt"), "same", Utf8);
                using (UploadPackage p = f.Open()) { RefuseInstalled(p, target); Check(p.Revalidate()); }
            }
        }

        private static void InstalledReparse()
        {
            foreach (bool ancestor in new[] { false, true })
            {
                Fixture f = new Fixture(); string target = InstalledCopy(f);
                string backing = ancestor ? target + ".backing" : Path.Combine(f.Root, "backing");
                string link = ancestor ? target : Path.Combine(target, "owned-junction");
                bool removable = true;
                try
                {
                    if (ancestor) Directory.Move(target, backing); else Directory.CreateDirectory(backing);
                    Junction(f, link, backing);
                    using (UploadPackage p = f.Open()) { RefuseInstalled(p, target); Check(p.Revalidate()); }
                }
                finally
                {
                    try { DeleteOwnedJunction(link, backing); }
                    catch { removable = false; Console.WriteLine("junction fixture retained: " + f.Root); throw; }
                    finally { if (removable) f.Dispose(); }
                }
            }
        }

        private static void InstalledCulture()
        {
            CultureInfo before = CultureInfo.CurrentCulture; string expected = null;
            try
            {
                foreach (string culture in new[] { "en-US", "tr-TR", "ar-SA" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    using (Fixture f = new Fixture()) using (UploadPackage p = f.Open())
                    using (IInstalledDeliveryEvidence proof = p.LeaseInstalled(InstalledCopy(f)))
                    { Check(proof.Revalidate()); if (expected == null) expected = proof.InventorySHA; Check(expected == proof.InventorySHA); }
                }
            }
            finally { CultureInfo.CurrentCulture = before; }
        }

        private static string InstalledRoot(Fixture f) => Path.Combine(f.Root, "steamapps", "workshop", "content", "333640", f.Item);
        private static string InstalledCopy(Fixture f)
        { string target = InstalledRoot(f); CopyInstalledFiles(f.Content, target); return target; }
        private static void CopyInstalledFiles(string source, string target)
        {
            foreach (string path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string copy = Path.Combine(target, path.Substring(source.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)); File.Copy(path, copy);
            }
        }
        private static void RefuseInstalled(UploadPackage package, string target)
        {
            bool refused = false; IInstalledDeliveryEvidence unexpected = null;
            try { unexpected = package.LeaseInstalled(target); } catch (Exception) { refused = true; }
            finally { if (unexpected != null) unexpected.Dispose(); }
            Check(refused);
        }
        private static string ExpectedInstalledDigest(string root)
        {
            string[] paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories); Array.Sort(paths, StringComparer.Ordinal);
            using (MemoryStream bytes = new MemoryStream())
            {
                Action<string> frame = value =>
                {
                    byte[] text = Utf8.GetBytes(value);
                    for (int i = 0; i < 4; i++) bytes.WriteByte((byte)(text.Length >> (8 * i)));
                    bytes.Write(text, 0, text.Length);
                };
                frame("taf-installed-inventory-v1"); frame(paths.Length.ToString(CultureInfo.InvariantCulture));
                foreach (string path in paths)
                { frame(path.Substring(root.Length + 1).Replace('\\', '/')); frame(Hash(path)); frame(new FileInfo(path).Length.ToString(CultureInfo.InvariantCulture)); }
                using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
