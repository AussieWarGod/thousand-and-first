using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using ThousandAndFirst.Tools;
using ThousandAndFirst.WorkshopSteam.Evidence;

namespace ThousandAndFirst.WorkshopSteam
{
    public sealed partial class UploadAttemptLease : IDisposable
    {
        private readonly object gate = new object();
        private readonly List<DirectoryPin> parents = new List<DirectoryPin>();
        private FileStream stream;
        private string path;
        private byte[] receipt, hash;
        private Identity identity;
        private bool disposed, refused, observationAttempted;
        private UploadAttemptLease() { }

        [StructLayout(LayoutKind.Sequential)]
        private struct Identity
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        private sealed class DirectoryPin
        { internal string Path; internal SafeFileHandle Handle; internal Identity Identity; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string path, uint access, uint share,
            IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Identity information);

        public static UploadAttemptLease Create(string path, byte[] receipt)
        {
            Require(receipt != null && receipt.Length >= 1 && receipt.Length <= 65536, "attempt byte bounds");
            UploadAttemptLease lease = new UploadAttemptLease();
            try
            {
                lease.path = Native(path); lease.receipt = (byte[])receipt.Clone(); lease.hash = Hash(lease.receipt);
                lease.Anchor(Path.GetDirectoryName(lease.path));
                lease.stream = new FileStream(lease.path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
                lease.identity = Read(lease.stream.SafeFileHandle, false);
                Require(ScenarioFileTrust.GetLinkCount(lease.stream.SafeFileHandle) == 1, "attempt has multiple links");
                lease.stream.Write(lease.receipt, 0, lease.receipt.Length); lease.stream.Flush(true);
                Require(lease.Revalidate(), "attempt authority changed during creation");
                return lease;
            }
            catch (Exception error)
            {
                // A created receipt, including a partial write, remains the retry fence.
                try { lease.Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }

        public bool Revalidate()
        { lock (gate) return RevalidateHeld(); }

        private bool RevalidateHeld()
        {
            if (disposed || refused) return false;
            try
            {
                foreach (DirectoryPin parent in parents)
                {
                    Require(Same(Read(parent.Handle, true), parent.Identity), "held parent changed");
                    using (SafeFileHandle probe = Open(parent.Path, true, 1))
                        Require(Same(Read(probe, true), parent.Identity), "parent path changed");
                }
                Require(Same(Read(stream.SafeFileHandle, false), identity)
                    && ScenarioFileTrust.GetLinkCount(stream.SafeFileHandle) == 1, "held attempt changed");
                using (SafeFileHandle probe = Open(path, false, 3))
                    Require(Same(Read(probe, false), identity) && ScenarioFileTrust.GetLinkCount(probe) == 1, "attempt path changed");
                Require(stream.Length == receipt.Length, "attempt length changed");
                byte[] current = new byte[receipt.Length]; stream.Position = 0; stream.ReadExactly(current);
                Require(Equal(current, receipt) && Equal(Hash(current), hash), "attempt bytes changed");
                return true;
            }
            catch (Exception) { refused = true; return false; }
        }

        /// <summary>Creates the first immutable submission observation beside this exact active
        /// attempt. The caller owns the returned lease. Failure preserves every created file;
        /// neither success nor disposal clears the active attempt or grants retry authority.</summary>
        public UploadAttemptLease CreateObservation(ReleaseSubmissionObservation observation)
        {
            lock (gate)
            {
                Require(!disposed && !refused && !observationAttempted, "observation unavailable or already attempted");
                Require(observation != null && observation.PreviousObservationSHA == null,
                    "expected first submission observation");
                Require(string.Equals(observation.AttemptSHA,
                    BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant(), StringComparison.Ordinal),
                    "observation does not bind exact attempt bytes");
                Require(string.Equals(Path.GetFileName(path), observation.Item + ".active.attempt.json", StringComparison.Ordinal),
                    "observation item does not match active attempt path");
                byte[] bytes = ReleaseSubmissionObservationCodec.EncodeUtf8(observation);
                Require(bytes.Length <= ReleaseSubmissionObservationCodec.MaxWireBytes, "observation byte bounds");
                observationAttempted = true;
                Require(RevalidateHeld(), "active attempt changed before observation");
                UploadAttemptLease child = null;
                try
                {
                    child = Create(path + ".submission.json", bytes);
                    Require(RevalidateHeld() && child.Revalidate(), "observation authority changed during creation");
                    return child;
                }
                catch (Exception error)
                {
                    try { if (child != null) child.Dispose(); }
                    catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                    throw;
                }
            }
        }

        private void Anchor(string directory)
        {
            string parent = Path.GetDirectoryName(directory);
            if (!string.IsNullOrEmpty(parent)) Anchor(parent);
            SafeFileHandle handle = Open(directory, true, 1, 0x80000000u);
            try { parents.Add(new DirectoryPin { Path = directory, Handle = handle, Identity = Read(handle, true) }); }
            catch { handle.Dispose(); throw; }
        }
        private static SafeFileHandle Open(string path, bool directory, uint sharing, uint access = 0x80u)
        {
            SafeFileHandle handle = CreateFileW(path, access, sharing, IntPtr.Zero, 3,
                0x00200000u | (directory ? 0x02000000u : 0), IntPtr.Zero);
            if (handle.IsInvalid) { handle.Dispose(); throw new InvalidDataException("cannot lease attempt authority"); }
            return handle;
        }
        private static Identity Read(SafeFileHandle handle, bool directory)
        {
            Identity value;
            Require(handle != null && !handle.IsClosed && !handle.IsInvalid, "unreadable attempt authority");
            Require(GetFileInformationByHandle(handle, out value)
                && (value.Attributes & (uint)(FileAttributes.Directory | FileAttributes.ReparsePoint))
                    == (directory ? (uint)FileAttributes.Directory : 0u), "linked or wrong-type attempt authority");
            return value;
        }
        private static bool Same(Identity a, Identity b)
        { return a.Volume == b.Volume && a.IndexHigh == b.IndexHigh && a.IndexLow == b.IndexLow
            && a.Creation.dwHighDateTime == b.Creation.dwHighDateTime && a.Creation.dwLowDateTime == b.Creation.dwLowDateTime; }
        private static string Native(string path)
        {
            Require(Environment.OSVersion.Platform == PlatformID.Win32NT, "Windows attempt leases required");
            Require(path != null && path.Length >= 4 && path.Length <= 32767
                && ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z'))
                && path[1] == ':' && (path[2] == '\\' || path[2] == '/'), "ordinary local attempt path required");
            string full = Path.GetFullPath(path);
            Require(string.Equals(path.Replace('/', '\\'), full, StringComparison.OrdinalIgnoreCase), "noncanonical attempt path");
            Require(new DriveInfo(Path.GetPathRoot(full)).DriveType != DriveType.Network, "network attempt path");
            new UTF8Encoding(false, true).GetByteCount(full);
            string[] segments = full.Substring(3).Split('\\');
            Require(segments.Length >= 2 && segments.Length <= 128, "broad or overbound attempt path");
            foreach (string segment in segments)
            {
                Require(segment.Length > 0 && !segment.EndsWith(".", StringComparison.Ordinal)
                    && !segment.EndsWith(" ", StringComparison.Ordinal) && segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                    && !Regex.IsMatch(segment, @"\A(?:CON|PRN|AUX|NUL|CLOCK\$|CONIN\$|CONOUT\$|(?:COM|LPT)[1-9¹²³])(?:\.|\z)",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "unsafe attempt component");
                foreach (char c in segment) Require(!char.IsControl(c), "control in attempt path");
            }
            return full;
        }
        private static byte[] Hash(byte[] value) { using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(value); }
        private static bool Equal(byte[] a, byte[] b)
        { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
        public void Dispose()
        { lock (gate) DisposeHeld(); }

        private void DisposeHeld()
        {
            if (disposed) return;
            disposed = true; List<Exception> failures = new List<Exception>();
            try { if (stream != null) stream.Dispose(); } catch (Exception error) { failures.Add(error); }
            for (int i = parents.Count - 1; i >= 0; i--)
                try { parents[i].Handle.Dispose(); } catch (Exception error) { failures.Add(error); }
            if (failures.Count > 0) throw new AggregateException("attempt lease cleanup failed", failures);
        }
    }
}
