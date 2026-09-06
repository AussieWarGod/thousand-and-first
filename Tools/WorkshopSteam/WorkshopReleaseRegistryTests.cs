using System;
using System.IO;
using System.Text;
using ThousandAndFirst.WorkshopSteam;

internal static class WorkshopReleaseRegistryTests
{
    private const ulong Item = WorkshopItemLock.StagingItem;
    private static int passed, failed;
    private static int Main()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) return 2;
        Case("public entry has one item argument and fixed root", PublicEntry);
        Case("opening initializes an exact marker without an attempt", Marker);
        Case("begin fences empty directory before receipt creation", Begin);
        Case("another open cannot retry a retained attempt", Retained);
        Case("item lock excludes alternate roots", AlternateRoots);
        Case("item lock and marker are released after failed open", FailedOpen);
        Case("copied marker cannot authorize replacement directory", Replacement);
        Case("changed marker bytes refuse without replacement", Tamper);
        Case("unmarked nonempty state refuses", Unmarked);
        Case("unrecognized retained evidence refuses", Unexpected);
        Case("missing bootstrap root is never created", MissingRoot);
        Console.WriteLine("Fixed-root registry Windows fixtures: passed=" + passed + " failed=" + failed);
        return failed == 0 ? 0 : 1;
    }
    private static string Root()
    {
        string path = Path.Combine(Path.GetTempPath(), "taf-registry-test." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); Console.WriteLine("retained fixture=" + path); return path;
    }
    private static string Seat(string root) { return Path.Combine(root, "registry", "3796495680"); }
    private static string MarkerPath(string root) { return Path.Combine(Seat(root), "registry.marker.json"); }

    private static void PublicEntry()
    {
        Check(WorkshopReleaseRegistry.StateRoot == @"C:\taf-workshop-state.dRBivM");
        var method = typeof(WorkshopReleaseRegistry).GetMethod("Open");
        Check(method != null && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(ulong));
        Refuses(() => WorkshopReleaseRegistry.OpenCore("not a path", 1));
    }
    private static void Marker()
    {
        string root = Root(), before;
        using (var registry = WorkshopReleaseRegistry.OpenCore(root, Item))
        {
            registry.RequireExact(); before = ReadHeldMarker(MarkerPath(root));
            Check(before.Contains("\"item\":\"3796495680\""));
            Check(Directory.GetFileSystemEntries(Path.Combine(Seat(root), "attempts")).Length == 0);
        }
        using (var registry = WorkshopReleaseRegistry.OpenCore(root, Item)) registry.RequireExact();
        Check(before == File.ReadAllText(MarkerPath(root)));
    }
    private static string ReadHeldMarker(string path)
    {
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true))) return reader.ReadToEnd();
    }
    private static void Begin()
    {
        string root = Root();
        using (var registry = WorkshopReleaseRegistry.OpenCore(root, Item))
        {
            string path = registry.BeginFirstAttempt();
            Check(path == Path.Combine(Seat(root), "attempts", "0001", "3796495680.active.attempt.json"));
            Check(Directory.Exists(Path.GetDirectoryName(path)) && !File.Exists(path));
            Refuses(() => registry.BeginFirstAttempt());
            using (var receipt = UploadAttemptLease.Create(path, Encoding.UTF8.GetBytes("exact attempt bytes")))
            { Check(receipt.Revalidate()); registry.RequireExact(); }
        }
    }
    private static void Retained()
    {
        string root = Root(), path;
        using (var first = WorkshopReleaseRegistry.OpenCore(root, Item)) path = first.BeginFirstAttempt();
        using (var second = WorkshopReleaseRegistry.OpenCore(root, Item)) Refuses(() => second.BeginFirstAttempt());
        Check(Directory.Exists(Path.GetDirectoryName(path)) && !File.Exists(path));
    }
    private static void AlternateRoots()
    {
        string first = Root(), other = Root();
        using (var registry = WorkshopReleaseRegistry.OpenCore(first, Item))
        {
            Refuses(() => WorkshopReleaseRegistry.OpenCore(other, Item));
            Check(Directory.GetFileSystemEntries(other).Length == 0); registry.RequireExact();
        }
    }
    private static void FailedOpen()
    {
        string root = Root(); Directory.CreateDirectory(Seat(root));
        File.WriteAllText(MarkerPath(root), "bad marker");
        Refuses(() => WorkshopReleaseRegistry.OpenCore(root, Item));
        using (var held = WorkshopItemLock.Acquire(Item)) held.RequireHeld(Item);
        using (FileStream writer = File.Open(MarkerPath(root), FileMode.Open, FileAccess.Write, FileShare.Read)) { }
        Check(File.ReadAllText(MarkerPath(root)) == "bad marker");
    }
    private static void Replacement()
    {
        string root = Root();
        using (var registry = WorkshopReleaseRegistry.OpenCore(root, Item)) registry.RequireExact();
        byte[] marker = File.ReadAllBytes(MarkerPath(root));
        Directory.Move(Seat(root), Seat(root) + ".retained"); Directory.CreateDirectory(Seat(root));
        File.WriteAllBytes(MarkerPath(root), marker);
        Refuses(() => WorkshopReleaseRegistry.OpenCore(root, Item));
        Check(Directory.Exists(Seat(root) + ".retained"));
    }
    private static void Tamper()
    {
        string root = Root();
        using (var registry = WorkshopReleaseRegistry.OpenCore(root, Item)) registry.RequireExact();
        File.AppendAllText(MarkerPath(root), " "); string before = File.ReadAllText(MarkerPath(root));
        Refuses(() => WorkshopReleaseRegistry.OpenCore(root, Item)); Check(File.ReadAllText(MarkerPath(root)) == before);
    }
    private static void Unmarked()
    {
        string root = Root(); Directory.CreateDirectory(Seat(root));
        File.WriteAllText(Path.Combine(Seat(root), "old-attempt.json"), "uncertain");
        Refuses(() => WorkshopReleaseRegistry.OpenCore(root, Item)); Check(!File.Exists(MarkerPath(root)));
    }
    private static void Unexpected()
    {
        string root = Root();
        using (var registry = WorkshopReleaseRegistry.OpenCore(root, Item)) registry.RequireExact();
        File.WriteAllText(Path.Combine(Seat(root), "unknown.json"), "retained");
        Refuses(() => WorkshopReleaseRegistry.OpenCore(root, Item));
        Check(Directory.GetFileSystemEntries(Path.Combine(Seat(root), "attempts")).Length == 0);
    }
    private static void MissingRoot()
    {
        string parent = Root(), missing = Path.Combine(parent, "missing");
        Refuses(() => WorkshopReleaseRegistry.OpenCore(missing, Item)); Check(!Directory.Exists(missing));
    }
    private static void Case(string name, Action test)
    { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
    private static void Check(bool value) { if (!value) throw new Exception("assertion failed"); }
    private static void Refuses(Action action)
    {
        bool refused = false;
        try { action(); } catch (IOException) { refused = true; } catch (InvalidDataException) { refused = true; }
        catch (ArgumentException) { refused = true; } catch (UnauthorizedAccessException) { refused = true; }
        catch (InvalidOperationException) { refused = true; }
        Check(refused);
    }
}
