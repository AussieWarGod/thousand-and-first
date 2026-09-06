using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ThousandAndFirst.WorkshopSteam
{
    /// <summary>Synthetic Windows file-handle tests; no SDK, submission or power-loss proof.</summary>
    public static class UploadAttemptLeaseTests
    {
        private static int passed, failed;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string path, uint access, uint sharing,
            IntPtr security, uint creation, uint flags, IntPtr template);
        public static int Run()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            { Console.WriteLine("UNSUPPORTED: UploadAttemptLease tests require Windows file leases"); return 2; }
            passed = failed = 0;
            Case("created receipt is exact, readable and defensively copied", Exact);
            Case("active receipt denies overwrite and append", Writers);
            Case("active receipt denies deletion and rename", Removal);
            Case("both owned directory ancestors deny rename", Parents);
            Case("existing receipt is never replaced", Existing);
            Case("disposal releases handles but preserves retry fence", Disposal);
            Case("one-byte and maximum receipts remain exact", Bounds);
            Case("invalid receipt sizes create no file", InvalidBytes);
            Case("invalid paths and missing parents create no file", InvalidPaths);
            Console.WriteLine("UploadAttemptLease synthetic Windows fixtures: passed=" + passed + " failed=" + failed);
            return failed == 0 ? 0 : 1;
        }
        private static void Exact()
        {
            using (Fixture f = new Fixture())
            {
                byte[] input = (byte[])f.Bytes.Clone();
                using (UploadAttemptLease lease = UploadAttemptLease.Create(f.Path, input))
                {
                    input[0] ^= 0xff;
                    Check(lease.Revalidate()); Equal(Read(f.Path), f.Bytes);
                    Refuses(() => UploadAttemptLease.Create(f.Path, new byte[] { 9 }));
                    Check(lease.Revalidate()); Equal(Read(f.Path), f.Bytes);
                }
            }
        }
        private static void Writers()
        {
            using (Fixture f = new Fixture()) using (UploadAttemptLease lease = f.Create())
            {
                Blocked(() => File.WriteAllBytes(f.Path, new byte[] { 9 }));
                Blocked(() => File.AppendAllText(f.Path, "changed"));
                Blocked(() => { using (FileStream stream = new FileStream(f.Path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } });
                Check(lease.Revalidate()); Equal(Read(f.Path), f.Bytes);
            }
        }
        private static void Removal()
        {
            using (Fixture f = new Fixture()) using (UploadAttemptLease lease = f.Create())
            {
                Blocked(() => File.Delete(f.Path)); Blocked(() => File.Move(f.Path, f.Path + ".moved"));
                Check(lease.Revalidate() && !File.Exists(f.Path + ".moved")); Equal(Read(f.Path), f.Bytes);
            }
        }
        private static void Parents()
        {
            using (Fixture f = new Fixture())
            {
                bool access = DirectoryWriteAccess(f.State, false, "state-before")
                    & DirectoryWriteAccess(f.Root, false, "root-before");
                Check(access);
                using (UploadAttemptLease lease = f.Create())
                {
                    access &= DirectoryWriteAccess(f.State, true, "state-held");
                    access &= DirectoryWriteAccess(f.Root, true, "root-held");
                    Blocked(() => Directory.Move(f.State, f.State + ".moved"));
                    Blocked(() => Directory.Move(f.Root, f.Root + ".moved"));
                    Check(lease.Revalidate() && !Directory.Exists(f.State + ".moved") && !Directory.Exists(f.Root + ".moved"));
                    Equal(Read(f.Path), f.Bytes);
                }
                access &= DirectoryWriteAccess(f.State, false, "state-after");
                access &= DirectoryWriteAccess(f.Root, false, "root-after");
                Check(access); Equal(File.ReadAllBytes(f.Path), f.Bytes);
            }
        }
        private static bool DirectoryWriteAccess(string path, bool blocked, string stage)
        {
            // Probe access only: nonempty directories must not mask a mutation-sharing failure.
            using (SafeFileHandle handle = CreateFileW(path, 0x40000000u, 7u, IntPtr.Zero, 3u, 0x02200000u, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                bool opened = !handle.IsInvalid;
                Console.WriteLine("directory_write_probe " + stage + " opened=" + opened + " error=" + (opened ? 0 : error));
                return blocked ? !opened && error == 32 : opened;
            }
        }
        private static void Existing()
        {
            using (Fixture f = new Fixture())
            {
                File.WriteAllBytes(f.Path, f.Bytes);
                Refuses(() => UploadAttemptLease.Create(f.Path, new byte[] { 9 }));
                Equal(File.ReadAllBytes(f.Path), f.Bytes);
                using (FileStream stream = new FileStream(f.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Check(stream.CanWrite);
                Directory.Move(f.State, f.State + ".moved"); Directory.Move(f.State + ".moved", f.State);
            }
        }
        private static void Disposal()
        {
            using (Fixture f = new Fixture())
            {
                UploadAttemptLease lease = f.Create();
                try { Check(lease.Revalidate()); } finally { lease.Dispose(); }
                lease.Dispose(); Check(!lease.Revalidate()); Equal(File.ReadAllBytes(f.Path), f.Bytes);
                Refuses(() => UploadAttemptLease.Create(f.Path, f.Bytes));
                using (FileStream stream = new FileStream(f.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Check(stream.CanWrite);
                Directory.Move(f.State, f.State + ".moved"); Directory.Move(f.State + ".moved", f.State);
                Directory.Move(f.Root, f.Root + ".moved"); Directory.Move(f.Root + ".moved", f.Root);
                Equal(File.ReadAllBytes(f.Path), f.Bytes);
            }
        }
        private static void Bounds()
        {
            foreach (int count in new[] { 1, 65536 }) using (Fixture f = new Fixture())
            {
                byte[] bytes = new byte[count]; for (int i = 0; i < count; i++) bytes[i] = (byte)i;
                using (UploadAttemptLease lease = UploadAttemptLease.Create(f.Path, bytes))
                { Check(lease.Revalidate()); Equal(Read(f.Path), bytes); }
            }
        }
        private static void InvalidBytes()
        {
            using (Fixture f = new Fixture()) foreach (byte[] bytes in new[] { null, new byte[0], new byte[65537] })
            { Invalid(() => UploadAttemptLease.Create(f.Path, bytes)); Check(!File.Exists(f.Path)); }
        }
        private static void InvalidPaths()
        {
            using (Fixture f = new Fixture())
            {
                string rootFile = System.IO.Path.Combine(System.IO.Path.GetPathRoot(f.Root), "taf-attempt-refused-" + Guid.NewGuid().ToString("N"));
                Invalid(() => UploadAttemptLease.Create(rootFile, f.Bytes));
                foreach (string path in new[] { null, "", "relative.attempt", "C:relative.attempt", @"\\invalid\share\attempt",
                    f.Path + ":stream", f.Path + ".", f.Path + " ", System.IO.Path.Combine(f.State, "CON.txt"),
                    System.IO.Path.Combine(f.State, "..", "escape.attempt"), f.Path + "\n", f.Path + "\ud800",
                    System.IO.Path.Combine(f.State, "missing", "attempt.json") })
                { Refuses(() => UploadAttemptLease.Create(path, f.Bytes)); Check(!File.Exists(f.Path)); }
                Check(!File.Exists(rootFile) && Directory.GetFiles(f.State).Length == 0);
                File.WriteAllBytes(f.Path, f.Bytes);
                Refuses(() => UploadAttemptLease.Create(System.IO.Path.Combine(f.Path, "child.attempt"), f.Bytes));
                Equal(File.ReadAllBytes(f.Path), f.Bytes);
            }
        }
        private static byte[] Read(string path)
        {
            // The held handle writes during acquisition; readers share that access without receiving it.
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            { byte[] bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); return bytes; }
        }
        private static void Refuses(Func<UploadAttemptLease> action)
        {
            bool refused = false; UploadAttemptLease unexpected = null;
            try { unexpected = action(); }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException) { refused = true; }
            finally { if (unexpected != null) unexpected.Dispose(); }
            Check(refused);
        }
        private static void Invalid(Func<UploadAttemptLease> action)
        {
            bool refused = false; UploadAttemptLease unexpected = null;
            try { unexpected = action(); } catch (InvalidDataException) { refused = true; }
            finally { if (unexpected != null) unexpected.Dispose(); }
            Check(refused);
        }
        private static void Blocked(Action action)
        { bool blocked = false; try { action(); } catch (IOException) { blocked = true; } Check(blocked); }
        private static void Equal(byte[] a, byte[] b)
        { Check(a.Length == b.Length); for (int i = 0; i < a.Length; i++) Check(a[i] == b[i]); }
        private static void Check(bool condition) { if (!condition) throw new InvalidOperationException("attempt fixture assertion failed"); }
        private static void Case(string name, Action body)
        { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); } }
        private sealed class Fixture : IDisposable
        {
            internal readonly string Root, State, Path;
            internal readonly byte[] Bytes = Encoding.UTF8.GetBytes("{\"attemptId\":\"synthetic\",\"item\":12345}\n");
            internal Fixture()
            {
                Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "taf-upload-attempt-test." + Guid.NewGuid().ToString("N"));
                Check(!Directory.Exists(Root) && !File.Exists(Root)); State = System.IO.Path.Combine(Root, "state");
                Directory.CreateDirectory(State); Path = System.IO.Path.Combine(State, "12345-0.3.7.attempt.json");
            }
            internal UploadAttemptLease Create() { return UploadAttemptLease.Create(Path, Bytes); }
            public void Dispose() { Directory.Delete(Root, true); }
        }
    }
}
