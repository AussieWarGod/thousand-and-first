using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace ThousandAndFirst.Tools
{
    // Native held-input proof, not source-version authority or continuous process exclusion.
    public sealed class UpgradeProfileTrust : IDisposable
    {
        public const long MaximumFile = 536870912, MaximumTotal = 2147483648;
        public const int MaximumEntries = 16384;
        [StructLayout(LayoutKind.Sequential)] private struct Information
        {
            public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh;
            public uint WriteLow, WriteHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [StructLayout(LayoutKind.Sequential)] private struct Identity { public ulong Volume, Low, High; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint GetFileType(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(IntPtr handle, out Information value);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandleEx(IntPtr handle, int kind, out Identity value, uint size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandleW(IntPtr handle, StringBuilder value, uint size, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateDirectoryW(string path, IntPtr security);
        private sealed class Lease
        {
            public IntPtr Handle; public string Physical; public bool Directory;
            public Information Original; public Identity Id; public FileStream Stream;
        }
        public sealed class FileProof { public string path; public long size; public string before, after, copy; }
        public sealed class Proof
        {
            public string schema = "taf-upgrade-copy-proof-v1", mode, source, destination, planSHA256;
            public bool idleBefore, idleAfter, continuousIdleVerified, gracefulQuitVerified;
            public bool endpointIdle { get { return idleBefore && idleAfter; } }
            public bool sourceVersionVerified, saveCompatibilityVerified, cleanupComplete;
            public string status = "verified";
            public string[] roots, directories, selected; public FileProof[] files;
        }
        private readonly List<Lease> Held = new List<Lease>();
        private readonly Dictionary<string, Lease> Folders = new Dictionary<string, Lease>(StringComparer.Ordinal);
        private Lease Plan; private string PlanHash; private bool Disposed, Ran;
        public string PlanText { get; private set; }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidDataException(reason); }
        private static string Native(string value)
        {
            Require(value != null && Regex.IsMatch(value, @"\A[A-Z]:\\") && value.Length <= 1024, "native_path_required");
            Require(Path.GetFullPath(value) == value && value.IndexOf('/', 0) < 0, "noncanonical_native_path");
            foreach (string part in value.Substring(3).Split('\\')) if (part.Length != 0) Component(part);
            return value;
        }
        private static void Component(string part)
        {
            Require(part.Length > 0 && part.Length <= 255 && part != "." && part != ".." && part == part.Normalize(NormalizationForm.FormC)
                && !part.EndsWith(".", StringComparison.Ordinal) && !part.EndsWith(" ", StringComparison.Ordinal)
                && !Regex.IsMatch(part, @"[\x00-\x1f<>:""\\|?*]"), "unsafe_component");
            Require(!Regex.IsMatch(part, @"\A(?:CON|PRN|AUX|NUL|COM[0-9¹²³]|LPT[0-9¹²³])(?:\.|\z)", RegexOptions.IgnoreCase), "device_component");
            new UTF8Encoding(false, true).GetByteCount(part);
        }
        private static string Relative(string value, string[] roots)
        {
            Require(value != null && value.Length <= 1024, "relative_path_required"); string[] parts = value.Split('/');
            Require(parts.Length <= 32 && Array.IndexOf(roots, parts[0]) >= 0, "outside_declared_roots");
            foreach (string part in parts) Component(part); return value;
        }
        private static Information Info(IntPtr handle)
        { Information value; if (!GetFileInformationByHandle(handle, out value)) throw new Win32Exception(); return value; }
        private static Identity FileId(IntPtr handle)
        { Identity value; if (!GetFileInformationByHandleEx(handle, 18, out value, 24)) throw new Win32Exception(); return value; }
        private static string Physical(IntPtr handle)
        {
            StringBuilder value = new StringBuilder(32768); uint length = GetFinalPathNameByHandleW(handle, value, 32768, 1);
            Require(length > 0 && length < 32768, "physical_identity_unavailable"); string path = value.ToString();
            Require(Regex.IsMatch(path, @"\A\\\\\?\\Volume\{[0-9A-Fa-f-]{36}\}\\"), "volume_guid_required"); return path.TrimEnd('\\');
        }
        private Lease Hold(string path, bool directory, bool create)
        {
            IntPtr handle = CreateFileW(path, create ? 0xc0000000u : 0x80000000u, create ? 0u : 1u,
                IntPtr.Zero, create ? 1u : 3u, 0x00200000u | (directory ? 0x02000000u : 0u), IntPtr.Zero);
            if (handle == new IntPtr(-1)) throw new Win32Exception();
            Lease lease = new Lease { Handle = handle, Directory = directory }; Held.Add(lease);
            lease.Original = Info(handle); lease.Id = FileId(handle); lease.Physical = Physical(handle);
            Require(GetFileType(handle) == 1 && (lease.Original.Attributes & (0x400u | 0x40u)) == 0
                && ((lease.Original.Attributes & 0x10u) != 0) == directory && (directory || lease.Original.Links == 1), "nonordinary_input");
            if (!directory) lease.Stream = new FileStream(new SafeFileHandle(handle, false), create ? FileAccess.ReadWrite : FileAccess.Read, 65536, false);
            return lease;
        }
        private Lease Folder(string path)
        {
            Lease found; if (Folders.TryGetValue(path, out found)) { Check(found); return found; }
            string parent = Path.GetDirectoryName(path); Lease ancestor = parent == null ? null : Folder(parent);
            Lease result = Hold(path, true, false);
            if (ancestor != null) Require(result.Physical == ancestor.Physical + "\\" + Path.GetFileName(path), "directory_alias");
            Folders.Add(path, result); if (!Folders.ContainsKey(result.Physical)) Folders.Add(result.Physical, result); return result;
        }
        private static void Check(Lease lease)
        {
            Information info = Info(lease.Handle); Identity id = FileId(lease.Handle);
            Require(id.Volume == lease.Id.Volume && id.Low == lease.Id.Low && id.High == lease.Id.High
                && Physical(lease.Handle) == lease.Physical && (info.Attributes & (0x400u | 0x40u)) == 0
                && ((info.Attributes & 0x10u) != 0) == lease.Directory, "held_identity_changed");
            if (!lease.Directory) Require(info.Links == 1, "hardlink_changed");
        }
        private static string Hash(Lease lease, long expected) { return Transfer(lease, expected, null); }
        private static string Transfer(Lease lease, long expected, Stream output)
        {
            Check(lease); Require(lease.Stream.Length == expected, "file_size_changed"); lease.Stream.Position = 0;
            string result; byte[] buffer = new byte[65536]; long remaining = expected;
            using (SHA256 sha = SHA256.Create())
            {
                while (remaining > 0)
                {
                    int count = lease.Stream.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length)); Require(count > 0, "file_truncated");
                    sha.TransformBlock(buffer, 0, count, buffer, 0); if (output != null) output.Write(buffer, 0, count); remaining -= count;
                }
                Require(lease.Stream.ReadByte() == -1, "file_grew"); sha.TransformFinalBlock(new byte[0], 0, 0);
                result = BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
            Require(lease.Stream.Position == expected && lease.Stream.Length == expected, "read_length_changed"); Check(lease); return result;
        }
        public UpgradeProfileTrust(string path)
        {
            try
            {
                Native(path); Lease parent = Folder(Path.GetDirectoryName(path)); Plan = Hold(parent.Physical + "\\" + Path.GetFileName(path), false, false);
                Require(Plan.Physical == parent.Physical + "\\" + Path.GetFileName(path) && Plan.Stream.Length <= 16777216, "plan_invalid");
                PlanHash = Hash(Plan, Plan.Stream.Length); Plan.Stream.Position = 0; byte[] bytes = new byte[(int)Plan.Stream.Length];
                int at = 0; while (at < bytes.Length) { int n = Plan.Stream.Read(bytes, at, bytes.Length - at); Require(n > 0, "plan_short_read"); at += n; }
                PlanText = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch { Dispose(); throw; }
        }
        private static void Idle()
        {
            foreach (string name in new[] { "CoQ", "CavesOfQud" })
            {
                Process[] processes = Process.GetProcessesByName(name);
                try { Require(processes.Length == 0, "game_process_present"); }
                finally { foreach (Process process in processes) process.Dispose(); }
            }
        }
        private void Inventory(Lease root, string[] roots, HashSet<string> directories, HashSet<string> files)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); Queue<string> todo = new Queue<string>(roots);
            HashSet<string> actualDirs = new HashSet<string>(StringComparer.Ordinal), actualFiles = new HashSet<string>(StringComparer.Ordinal);
            while (todo.Count != 0)
            {
                string relative = todo.Dequeue(); Require(actualDirs.Count < MaximumEntries && actualDirs.Add(relative), "directory_budget");
                string path = root.Physical + "\\" + relative.Replace('/', '\\'); Lease directory = Folder(path); Check(directory);
                foreach (string entry in Directory.EnumerateFileSystemEntries(path))
                {
                    string child = Relative(relative + "/" + Path.GetFileName(entry), roots);
                    Require(seen.Add(child) && seen.Count <= MaximumEntries * 2, "inventory_collision_or_budget");
                    FileAttributes attrs = File.GetAttributes(entry); Require((attrs & FileAttributes.ReparsePoint) == 0, "reparse_entry");
                    if ((attrs & FileAttributes.Directory) != 0) { Require(directories.Contains(child), "unexpected_directory"); todo.Enqueue(child); }
                    else Require(files.Contains(child) && actualFiles.Add(child) && actualFiles.Count <= MaximumEntries, "unexpected_file");
                }
            }
            Require(actualDirs.SetEquals(directories) && actualFiles.SetEquals(files), "inventory_changed");
        }
        public Proof Run(string mode, string schema, string source, string destination, string[] roots,
            string[] paths, long[] sizes, string[] hashes, string[] directories, string[] selected)
        {
            Require(!Disposed && !Ran, "single_operation_only"); Ran = true;
            Require(schema == "taf-upgrade-copy-v1" && (mode == "Inspect" || mode == "Copy" || mode == "Slots"), "mode_or_schema");
            Require(roots != null && ((roots.Length == 1 && roots[0] == "Synced") || (mode == "Inspect" && roots.Length == 2 && roots[0] == "Local" && roots[1] == "Synced")), "roots_invalid");
            Require(Regex.IsMatch(source ?? "", @"\AC:\\taf-scenario\.[A-Za-z0-9]{1,96}\z"), "source_profile_required"); Native(source);
            Require(mode == "Inspect" ? destination == null : Regex.IsMatch(destination ?? "", @"\AC:\\taf-scenario\.[A-Za-z0-9]{1,96}\z") && source != destination, "destination_invalid");
            Require(paths != null && sizes != null && hashes != null && directories != null && selected != null && paths.Length <= MaximumEntries
                && sizes.Length == paths.Length && hashes.Length == paths.Length && directories.Length <= MaximumEntries, "array_bounds");
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase), dirs = new HashSet<string>(StringComparer.Ordinal), files = new HashSet<string>(StringComparer.Ordinal);
            foreach (string dir in directories) Require(names.Add(Relative(dir, roots)) && dirs.Add(dir), "directory_collision");
            foreach (string top in roots) Require(dirs.Contains(top), "root_directory_missing");
            long total = 0; for (int i = 0; i < paths.Length; i++)
            {
                Require(names.Add(Relative(paths[i], roots)) && files.Add(paths[i]) && paths[i].Contains("/") && sizes[i] >= 0 && sizes[i] <= MaximumFile
                    && Regex.IsMatch(hashes[i] ?? "", @"\A[0-9a-f]{64}\z"), "file_plan_invalid"); total = checked(total + sizes[i]);
            }
            Require(total <= MaximumTotal, "total_budget");
            foreach (string name in names) if (name.Contains("/")) Require(dirs.Contains(name.Substring(0, name.LastIndexOf('/'))), "missing_parent");
            HashSet<string> copying = new HashSet<string>(StringComparer.Ordinal); string origin = null;
            Require(mode == "Slots" ? selected.Length >= 1 && selected.Length <= 2 : selected.Length == 0, "selection_invalid");
            foreach (string path in selected)
            {
                Match match = Regex.Match(path ?? "", @"\ASynced/ThousandAndFirst/Stages/([A-Za-z0-9_-]{1,96})\.([ab])\.seal\z");
                Require(match.Success && files.Contains(path) && copying.Add(path) && (origin == null || origin == match.Groups[1].Value), "slot_selection_invalid"); origin = match.Groups[1].Value;
            }
            if (mode == "Copy") copying.UnionWith(files); Idle(); Lease input = Folder(source);
            Inventory(input, roots, dirs, files); Lease[] inputs = new Lease[paths.Length]; FileProof[] proofs = new FileProof[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                string path = input.Physical + "\\" + paths[i].Replace('/', '\\'); inputs[i] = Hold(path, false, false);
                Require(inputs[i].Physical == path, "input_alias"); string before = Hash(inputs[i], sizes[i]); Require(before == hashes[i], "source_hash_mismatch");
                proofs[i] = new FileProof { path = paths[i], size = sizes[i], before = before };
            }
            if (mode != "Inspect") Copy(destination, input, dirs, copying, paths, sizes, inputs, proofs, mode == "Copy");
            Inventory(input, roots, dirs, files);
            for (int i = 0; i < inputs.Length; i++)
            {
                proofs[i].after = Hash(inputs[i], sizes[i]); Information current = Info(inputs[i].Handle);
                Require(proofs[i].after == hashes[i] && current.WriteLow == inputs[i].Original.WriteLow && current.WriteHigh == inputs[i].Original.WriteHigh, "source_changed_after_copy");
            }
            foreach (Lease held in Held) Check(held); Require(Hash(Plan, Plan.Stream.Length) == PlanHash, "plan_changed"); Idle();
            return new Proof { mode = mode, source = source, destination = destination, planSHA256 = PlanHash, idleBefore = true, idleAfter = true,
                roots = roots, directories = directories, selected = selected, files = proofs };
        }
        private void Copy(string destination, Lease input, HashSet<string> sourceDirs, HashSet<string> copying,
            string[] paths, long[] sizes, Lease[] inputs, FileProof[] proofs, bool allDirectories)
        {
            Native(destination); Lease output = Folder(destination); Require(output.Physical != input.Physical, "same_physical_profile");
            string synced = output.Physical + "\\Synced";
            if (Directory.Exists(synced))
            { Folder(synced); using (IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(synced).GetEnumerator()) Require(!entries.MoveNext(), "destination_not_empty"); }
            else { if (!CreateDirectoryW(synced, IntPtr.Zero)) throw new Win32Exception(); Folder(synced); }
            HashSet<string> copyDirs = new HashSet<string>(StringComparer.Ordinal) { "Synced" };
            if (allDirectories) copyDirs.UnionWith(sourceDirs);
            else foreach (string path in copying) { string parent = path; while (parent.Contains("/")) { parent = parent.Substring(0, parent.LastIndexOf('/')); copyDirs.Add(parent); } }
            List<string> ordered = new List<string>(copyDirs); ordered.Sort(StringComparer.Ordinal);
            foreach (string dir in ordered) if (dir != "Synced")
            { string path = output.Physical + "\\" + dir.Replace('/', '\\'); if (!CreateDirectoryW(path, IntPtr.Zero)) throw new Win32Exception(); Folder(path); }
            for (int i = 0; i < paths.Length; i++) if (copying.Contains(paths[i]))
            {
                string path = output.Physical + "\\" + paths[i].Replace('/', '\\');
                Check(output); Check(Folder(Path.GetDirectoryName(path))); Check(inputs[i]); Lease file = Hold(path, false, true);
                Require(file.Physical == path, "output_alias"); Require(Transfer(inputs[i], sizes[i], file.Stream) == proofs[i].before, "source_changed_during_copy");
                file.Stream.Flush(true); proofs[i].copy = Hash(file, sizes[i]); Require(proofs[i].copy == proofs[i].before, "copy_readback_mismatch");
            }
            Inventory(output, new[] { "Synced" }, copyDirs, copying);
        }
        public void Dispose()
        {
            if (Disposed) return; Disposed = true; List<Exception> failures = new List<Exception>();
            for (int i = Held.Count - 1; i >= 0; i--)
            {
                try { if (Held[i].Stream != null) Held[i].Stream.Dispose(); } catch (Exception e) { failures.Add(e); }
                if (!CloseHandle(Held[i].Handle)) failures.Add(new Win32Exception());
            }
            if (failures.Count != 0) throw new AggregateException("handle_cleanup_failed", failures);
        }
    }
}
