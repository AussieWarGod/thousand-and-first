using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using ThousandAndFirst.WorkshopSteam;

internal static class RegistryLeaseTests
{
    private static int passed, failed;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string link, string existing, IntPtr security);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint sharing,
        IntPtr security, uint creation, uint flags, IntPtr template);

    private static int Main()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) return 2;
        Case("retained bytes are exact and defensively copied", Retained);
        Case("retained read denies write, rename and delete", Writers);
        Case("read refuses empty, overbound, absent and directory records", Bounds);
        Case("read refuses hard-linked evidence", HardLinks);
        Case("directory identity survives reopen and child additions", Identity);
        Case("held directory denies actual native write access", DirectoryWriter);
        Case("directory identity changes on physical replacement", Replacement);
        Case("exclusive child creation never adopts existing entries", ExistingChild);
        Case("child name validation cannot escape parent", ChildNames);
        Case("disposed directory cannot create or attest", Disposed);
        Case("child pins ancestors after parent disposal", ChildOwnership);
        Case("directory path validation refuses aliases and roots", Paths);
        int legacy = UploadAttemptLeaseTests.Run();
        Console.WriteLine("Registry lease Windows fixtures: passed=" + passed + " failed=" + failed + "; legacyExit=" + legacy);
        return failed == 0 && legacy == 0 ? 0 : 1;
    }

    private sealed class Fixture
    {
        internal readonly string Root, State, Record;
        internal readonly byte[] Bytes = Encoding.UTF8.GetBytes("retained registry evidence\n");
        internal Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "taf-registry-lease-test." + Guid.NewGuid().ToString("N"));
            State = Path.Combine(Root, "state"); Record = Path.Combine(State, "record.json");
            Directory.CreateDirectory(State); File.WriteAllBytes(Record, Bytes);
            Console.WriteLine("retained fixture=" + Root);
        }
    }

    private static void Retained()
    {
        Fixture f = new Fixture();
        using (UploadAttemptLease lease = UploadAttemptLease.ReadExisting(f.Record))
        {
            Equal(f.Bytes, lease.ReadRetainedBytes());
            byte[] copy = lease.ReadRetainedBytes(); copy[0] ^= 0xff;
            Equal(f.Bytes, lease.ReadRetainedBytes()); Check(lease.Revalidate());
        }
        Equal(f.Bytes, File.ReadAllBytes(f.Record));
    }

    private static void Writers()
    {
        Fixture f = new Fixture();
        using (UploadAttemptLease lease = UploadAttemptLease.ReadExisting(f.Record))
        {
            Refuses(() => File.WriteAllBytes(f.Record, new byte[] { 9 }));
            Refuses(() => File.AppendAllText(f.Record, "changed"));
            Refuses(() => File.Move(f.Record, f.Record + ".moved"));
            Refuses(() => File.Delete(f.Record));
            Check(lease.Revalidate()); Equal(f.Bytes, lease.ReadRetainedBytes());
        }
        using (FileStream writer = new FileStream(f.Record, FileMode.Open, FileAccess.Write, FileShare.Read)) { }
    }

    private static void Bounds()
    {
        Fixture f = new Fixture();
        foreach (int size in new[] { 0, 65537 })
        {
            string path = Path.Combine(f.State, "size-" + size); File.WriteAllBytes(path, new byte[size]);
            Refuses(() => UploadAttemptLease.ReadExisting(path)); Check(new FileInfo(path).Length == size);
        }
        string missing = Path.Combine(f.State, "missing");
        Refuses(() => UploadAttemptLease.ReadExisting(missing)); Check(!File.Exists(missing));
        Refuses(() => UploadAttemptLease.ReadExisting(f.State));
        string maximum = Path.Combine(f.State, "maximum"); File.WriteAllBytes(maximum, new byte[65536]);
        using (UploadAttemptLease lease = UploadAttemptLease.ReadExisting(maximum)) Check(lease.ReadRetainedBytes().Length == 65536);
    }

    private static void HardLinks()
    {
        Fixture f = new Fixture(); string alias = Path.Combine(f.State, "alias");
        Check(CreateHardLinkW(alias, f.Record, IntPtr.Zero));
        Refuses(() => UploadAttemptLease.ReadExisting(f.Record));
        Equal(f.Bytes, File.ReadAllBytes(f.Record)); Equal(f.Bytes, File.ReadAllBytes(alias));
    }

    private static void Identity()
    {
        Fixture f = new Fixture(); string digest;
        using (UploadAttemptLease.DirectoryLease parent = UploadAttemptLease.DirectoryLease.Open(f.State))
        {
            digest = parent.IdentityDigest(); Check(digest.Length == 64);
            using (UploadAttemptLease.DirectoryLease child = parent.CreateChild("attempts"))
            { Check(child.IdentityDigest() != digest); Check(child.Revalidate()); }
            Check(parent.IdentityDigest() == digest);
        }
        using (UploadAttemptLease.DirectoryLease again = UploadAttemptLease.DirectoryLease.Open(f.State))
            Check(again.IdentityDigest() == digest);
    }

    private static void DirectoryWriter()
    {
        Fixture f = new Fixture(); NativeWrite(f.State, false);
        using (UploadAttemptLease.DirectoryLease lease = UploadAttemptLease.DirectoryLease.Open(f.State))
        {
            NativeWrite(f.State, true); NativeWrite(f.Root, true);
            Refuses(() => Directory.Move(f.State, f.State + ".moved")); Check(lease.Revalidate());
        }
        NativeWrite(f.State, false); NativeWrite(f.Root, false);
    }

    private static void NativeWrite(string path, bool blocked)
    {
        using (SafeFileHandle handle = CreateFileW(path, 0x40000000u, 7u, IntPtr.Zero, 3u, 0x02200000u, IntPtr.Zero))
        {
            int error = Marshal.GetLastWin32Error();
            Check(blocked ? handle.IsInvalid && error == 32 : !handle.IsInvalid);
        }
    }

    private static void Replacement()
    {
        Fixture f = new Fixture(); string before;
        using (UploadAttemptLease.DirectoryLease lease = UploadAttemptLease.DirectoryLease.Open(f.State)) before = lease.IdentityDigest();
        Directory.Move(f.State, f.State + ".retained"); Directory.CreateDirectory(f.State);
        using (UploadAttemptLease.DirectoryLease lease = UploadAttemptLease.DirectoryLease.Open(f.State)) Check(before != lease.IdentityDigest());
        Check(File.Exists(Path.Combine(f.State + ".retained", "record.json")));
    }

    private static void ExistingChild()
    {
        Fixture f = new Fixture(); Directory.CreateDirectory(Path.Combine(f.State, "existing"));
        using (UploadAttemptLease.DirectoryLease lease = UploadAttemptLease.DirectoryLease.Open(f.State))
        {
            Refuses(() => lease.CreateChild("existing")); Refuses(() => lease.CreateChild("record.json"));
            Check(lease.Revalidate()); Equal(f.Bytes, File.ReadAllBytes(f.Record));
        }
    }

    private static void ChildNames()
    {
        Fixture f = new Fixture();
        using (UploadAttemptLease.DirectoryLease lease = UploadAttemptLease.DirectoryLease.Open(f.State))
        {
            foreach (string name in new[] { null, "", ".", "..", "../escape", "a\\b", "C:escape", "NUL", "x.", "x ", "x\n" })
                Refuses(() => lease.CreateChild(name));
            Check(Directory.GetFileSystemEntries(f.State).Length == 1); Check(lease.Revalidate());
        }
    }

    private static void Disposed()
    {
        Fixture f = new Fixture(); UploadAttemptLease.DirectoryLease directory = UploadAttemptLease.DirectoryLease.Open(f.State);
        directory.Dispose(); directory.Dispose(); Check(!directory.Revalidate());
        Refuses(() => directory.IdentityDigest()); Refuses(() => directory.CreateChild("no"));
        UploadAttemptLease record = UploadAttemptLease.ReadExisting(f.Record); record.Dispose();
        Refuses(() => record.ReadRetainedBytes()); Check(!Directory.Exists(Path.Combine(f.State, "no")));
    }

    private static void ChildOwnership()
    {
        Fixture f = new Fixture(); UploadAttemptLease.DirectoryLease parent = UploadAttemptLease.DirectoryLease.Open(f.State);
        using (UploadAttemptLease.DirectoryLease child = parent.CreateChild("child"))
        {
            parent.Dispose(); Check(child.Revalidate()); NativeWrite(f.State, true);
            Refuses(() => Directory.Move(f.State, f.State + ".moved"));
        }
        NativeWrite(f.State, false);
    }

    private static void Paths()
    {
        Fixture f = new Fixture();
        foreach (string path in new[] { null, "relative", "C:\\", f.State + "\\", f.State + "\\..", f.State + "\\missing" })
            Refuses(() => UploadAttemptLease.DirectoryLease.Open(path));
    }

    private static void Case(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); }
    }
    private static void Refuses(Action action)
    {
        bool refused = false; try { action(); } catch (IOException) { refused = true; }
        catch (InvalidDataException) { refused = true; } catch (ArgumentException) { refused = true; } catch (UnauthorizedAccessException) { refused = true; }
        Check(refused);
    }
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("assertion failed"); }
    private static void Equal(byte[] left, byte[] right)
    { Check(left.Length == right.Length); for (int i = 0; i < left.Length; i++) Check(left[i] == right[i]); }
}
