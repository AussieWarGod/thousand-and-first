using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ThousandAndFirst.WorkshopSteam
{
    public sealed partial class UploadAttemptLease
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateDirectoryW(string path, IntPtr security);

        // Reading a retained record grants no retry or finalization authority.
        internal static UploadAttemptLease ReadExisting(string path)
        {
            UploadAttemptLease lease = new UploadAttemptLease();
            try
            {
                lease.path = Native(path);
                lease.Anchor(Path.GetDirectoryName(lease.path));
                lease.stream = new FileStream(lease.path, FileMode.Open, FileAccess.Read, FileShare.Read);
                lease.identity = Read(lease.stream.SafeFileHandle, false);
                Require(lease.stream.Length >= 1 && lease.stream.Length <= 65536, "record byte bounds");
                lease.receipt = new byte[(int)lease.stream.Length];
                lease.stream.ReadExactly(lease.receipt); lease.hash = Hash(lease.receipt);
                Require(lease.Revalidate(), "retained record changed during opening");
                return lease;
            }
            catch (Exception error)
            {
                try { lease.Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }

        internal byte[] ReadRetainedBytes()
        {
            lock (gate)
            {
                Require(RevalidateHeld(), "retained record unavailable");
                return (byte[])receipt.Clone();
            }
        }

        internal sealed class DirectoryLease : IDisposable
        {
            private readonly UploadAttemptLease owner;
            internal readonly string Path;
            private DirectoryLease(UploadAttemptLease owner, string path) { this.owner = owner; Path = path; }

            internal static DirectoryLease Open(string directory)
            {
                // Native validates a non-root path without weakening the file-path contract.
                string path = System.IO.Path.GetDirectoryName(Native(directory + "\\registry.marker.json"));
                Require(string.Equals(path, directory, StringComparison.OrdinalIgnoreCase), "noncanonical registry directory");
                UploadAttemptLease holder = new UploadAttemptLease();
                try
                {
                    holder.Anchor(path);
                    DirectoryLease lease = new DirectoryLease(holder, path);
                    Require(lease.Revalidate(), "registry directory changed during opening");
                    return lease;
                }
                catch (Exception error)
                {
                    try { holder.Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                    throw;
                }
            }

            internal bool Revalidate()
            {
                lock (owner.gate)
                {
                    if (owner.disposed || owner.refused) return false;
                    try
                    {
                        foreach (DirectoryPin parent in owner.parents)
                        {
                            Require(Same(Read(parent.Handle, true), parent.Identity), "held registry directory changed");
                            using (SafeFileHandle probe = OpenDirectoryPath(parent.Path))
                                Require(Same(Read(probe, true), parent.Identity), "registry directory path changed");
                        }
                        return true;
                    }
                    catch (Exception) { owner.refused = true; return false; }
                }
            }

            private static SafeFileHandle OpenDirectoryPath(string path) { return UploadAttemptLease.Open(path, true, 1); }

            internal string IdentityDigest()
            {
                lock (owner.gate)
                {
                    Require(Revalidate(), "registry identity unavailable");
                    StringBuilder value = new StringBuilder("taf-directory-identity-v1:");
                    foreach (DirectoryPin parent in owner.parents)
                    {
                        Identity id = parent.Identity;
                        value.Append(id.Volume.ToString("x8")).Append(id.IndexHigh.ToString("x8"))
                            .Append(id.IndexLow.ToString("x8")).Append(id.Creation.dwHighDateTime.ToString("x8"))
                            .Append(id.Creation.dwLowDateTime.ToString("x8")).Append(':');
                    }
                    return BitConverter.ToString(Hash(Encoding.ASCII.GetBytes(value.ToString())))
                        .Replace("-", string.Empty).ToLowerInvariant();
                }
            }

            internal DirectoryLease CreateChild(string name)
            {
                lock (owner.gate)
                {
                    Require(!string.IsNullOrEmpty(name) && name.IndexOfAny(new[] { '\\', '/' }) < 0,
                        "one registry child component required");
                    string child = Native(System.IO.Path.Combine(Path, name));
                    Require(Revalidate(), "registry parent unavailable");
                    Require(CreateDirectoryW(child, IntPtr.Zero), "registry child already exists or creation failed");
                    DirectoryLease lease = null;
                    try
                    {
                        lease = Open(child);
                        Require(Revalidate() && lease.Revalidate(), "registry child changed during creation");
                        return lease;
                    }
                    catch (Exception error)
                    {
                        try { if (lease != null) lease.Dispose(); }
                        catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                        throw;
                    }
                }
            }

            public void Dispose() { owner.Dispose(); }
        }
    }
}
