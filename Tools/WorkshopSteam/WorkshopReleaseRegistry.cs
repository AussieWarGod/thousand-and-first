using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.WorkshopSteam
{
    /// <summary>Fixed-root, item-locked custody for immutable release evidence. Later releases
    /// require the exact finalized history; uncertain submissions remain permanent fences.</summary>
    public sealed partial class WorkshopReleaseRegistry : IDisposable
    {
        public const string StateRoot = @"C:\taf-workshop-state.dRBivM";
        private readonly ulong item;
        private readonly WorkshopItemLock itemLock;
        private readonly List<IDisposable> leases = new List<IDisposable>();
        private readonly List<UploadAttemptLease.DirectoryLease> directories = new List<UploadAttemptLease.DirectoryLease>();
        private readonly List<UploadAttemptLease.DirectoryLease> registryDirectories = new List<UploadAttemptLease.DirectoryLease>();
        private UploadAttemptLease marker;
        private UploadAttemptLease.DirectoryLease itemDirectory;
        private UploadAttemptLease.DirectoryLease attempts;
        private byte[] markerBytes;
        private bool disposed, begun;

        /// <summary>An observed abandoned item mutex. Uncertainty for reconciliation, never retry:
        /// no caller may read this as permission to submit again.</summary>
        public sealed class AbandonedLock : IOException
        { internal AbandonedLock(string message) : base(message) { } }

        /// <summary>An incomplete or unrecognized retained attempt blocks later submissions.
        /// Neither inspection nor a matching installed copy clears uncertainty.</summary>
        public sealed class RetainedAttempt : IOException
        { internal RetainedAttempt(string message) : base(message) { } }

        private WorkshopReleaseRegistry(ulong item, WorkshopItemLock itemLock)
        { this.item = item; this.itemLock = itemLock; }

        public static WorkshopReleaseRegistry Open(ulong item) { return OpenCore(StateRoot, item); }

        // Internal seam for synthetic temporary-directory tests; CLI has no root parameter.
        internal static WorkshopReleaseRegistry OpenCore(string root, ulong item)
        {
            WorkshopItemLock held = WorkshopItemLock.Acquire(item);
            WorkshopReleaseRegistry registry = new WorkshopReleaseRegistry(item, held);
            try
            {
                held.RequireHeld(item);
                if (held.Abandoned) throw new AbandonedLock("Abandoned item lock requires reconciliation.");
                UploadAttemptLease.DirectoryLease parent = registry.KeepRegistry(UploadAttemptLease.DirectoryLease.Open(root));
                UploadAttemptLease.DirectoryLease common = registry.Child(parent, "registry");
                UploadAttemptLease.DirectoryLease seat = registry.Child(common, item.ToString(CultureInfo.InvariantCulture));
                registry.itemDirectory = seat;
                registry.markerBytes = Encoding.UTF8.GetBytes("{\"schema\":1,\"item\":\""
                    + item.ToString(CultureInfo.InvariantCulture) + "\",\"directoryIdentity\":\""
                    + seat.IdentityDigest() + "\"}\n");
                string path = Path.Combine(seat.Path, "registry.marker.json");
                if (File.Exists(path)) registry.marker = UploadAttemptLease.ReadExisting(path);
                else
                {
                    if (Directory.GetFileSystemEntries(seat.Path).Length != 0)
                        throw new InvalidDataException("Unmarked registry contains retained evidence.");
                    registry.marker = UploadAttemptLease.Create(path, registry.markerBytes);
                }
                registry.leases.Add(registry.marker);
                registry.RequireExact();
                registry.attempts = registry.Child(seat, "attempts");
                registry.RequireExact();
                return registry;
            }
            catch (Exception error)
            {
                try { registry.Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }

        private UploadAttemptLease.DirectoryLease Keep(UploadAttemptLease.DirectoryLease lease)
        { directories.Add(lease); leases.Add(lease); return lease; }

        private UploadAttemptLease.DirectoryLease KeepRegistry(UploadAttemptLease.DirectoryLease lease)
        { Keep(lease); registryDirectories.Add(lease); return lease; }

        private UploadAttemptLease.DirectoryLease Child(UploadAttemptLease.DirectoryLease parent, string name)
        {
            itemLock.RequireHeld(item);
            if (!parent.Revalidate()) throw new InvalidDataException("Registry parent changed.");
            string path = Path.Combine(parent.Path, name);
            UploadAttemptLease.DirectoryLease child = Directory.Exists(path)
                ? UploadAttemptLease.DirectoryLease.Open(path) : parent.CreateChild(name);
            return KeepRegistry(child);
        }

        /// <summary>Creates a durable empty attempt directory before the port creates its receipt.
        /// A crash or any existing attempt blocks another call. Nothing is deleted or retried.</summary>
        public string BeginFirstAttempt()
        {
            RequireExact();
            if (begun || HasAttemptEntry())
                throw new RetainedAttempt("Retained attempt requires reconciliation; no retry.");
            begun = true;
            UploadAttemptLease.DirectoryLease attempt = Keep(attempts.CreateChild("0001"));
            if (historyNames != null) historyNames.Add("0001");
            RequireExact();
            return Path.Combine(attempt.Path, item.ToString(CultureInfo.InvariantCulture) + ".active.attempt.json");
        }

        /// <summary>True when any attempt is retained for this item. Re-proves the marker first;
        /// it creates nothing and grants nothing. A true result is a refusal, not a retry decision.</summary>
        public bool HasRetainedAttempt()
        {
            RequireExact();
            return begun || HasAttemptEntry();
        }

        private bool HasAttemptEntry()
        {
            using (IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(attempts.Path).GetEnumerator())
                return entries.MoveNext();
        }

        /// <summary>The path BeginFirstAttempt would return, without creating anything. The caller
        /// may name it (a port needs a path before it records) but must not create it itself.</summary>
        public string PlannedAttemptPath()
        {
            RequireExact();
            return Path.Combine(attempts.Path, "0001",
                item.ToString(CultureInfo.InvariantCulture) + ".active.attempt.json");
        }

        /// <summary>Re-proves held registry identities and reports at most 64 immediate entry names.
        /// Child entries are not opened or followed. No per-file identity, content or chain proof is
        /// implied by a name. Inspection grants no retry, finalization or publication authority.</summary>
        public string[] Inspect()
        {
            RequireRegistryExact();
            List<string> proofs = new List<string>();
            proofs.Add("registryRoot=" + Path.GetDirectoryName(Path.GetDirectoryName(itemDirectory.Path)));
            proofs.Add("item=" + item.ToString(CultureInfo.InvariantCulture));
            proofs.Add("itemDirectory=" + itemDirectory.Path);
            proofs.Add("itemDirectoryIdentity=" + itemDirectory.IdentityDigest());
            proofs.Add("marker=" + Path.Combine(itemDirectory.Path, "registry.marker.json"));
            proofs.Add("markerSHA=" + Sha(marker.ReadRetainedBytes()));
            proofs.Add("attemptsIdentity=" + attempts.IdentityDigest());
            List<string> names = new List<string>();
            foreach (string entry in Directory.EnumerateFileSystemEntries(attempts.Path))
            {
                string name = Path.GetFileName(entry);
                if (names.Count >= 64 || string.IsNullOrEmpty(name) || name.Length > 255)
                    throw new InvalidDataException("Registry inspection entry bounds.");
                foreach (char character in name)
                    if (char.IsControl(character)) throw new InvalidDataException("Registry inspection entry text.");
                new UTF8Encoding(false, true).GetByteCount(name);
                names.Add(name);
            }
            names.Sort(StringComparer.Ordinal);
            foreach (string name in names) proofs.Add("retainedEntry=" + name);
            proofs.Add("retainedEntries=" + names.Count.ToString(CultureInfo.InvariantCulture));
            proofs.Add("inspectionScope=held-registry-identities-and-immediate-entry-names-only");
            RequireRegistryExact();
            return proofs.ToArray();
        }

        private static string Sha(byte[] value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", string.Empty).ToLowerInvariant();
        }

        public void RequireExact()
        {
            RequireRegistryExact();
            foreach (UploadAttemptLease.DirectoryLease directory in directories)
                if (!directory.Revalidate()) throw new InvalidDataException("Registry directory identity changed.");
            RequireHistoryExact();
        }

        private void RequireRegistryExact()
        {
            if (disposed) throw new ObjectDisposedException(nameof(WorkshopReleaseRegistry));
            itemLock.RequireHeld(item);
            foreach (UploadAttemptLease.DirectoryLease directory in registryDirectories)
                if (!directory.Revalidate()) throw new InvalidDataException("Registry directory identity changed.");
            foreach (string entry in Directory.GetFileSystemEntries(itemDirectory.Path))
                if (Path.GetFileName(entry) != "registry.marker.json" && Path.GetFileName(entry) != "attempts")
                    throw new InvalidDataException("Unrecognized item registry evidence requires inspection.");
            if (marker == null || !marker.Revalidate()) throw new InvalidDataException("Registry marker unavailable.");
            byte[] actual = marker.ReadRetainedBytes();
            if (actual.Length != markerBytes.Length) throw new InvalidDataException("Registry marker identity mismatch.");
            for (int i = 0; i < actual.Length; i++)
                if (actual[i] != markerBytes[i]) throw new InvalidDataException("Registry marker identity mismatch.");
        }

        public void Dispose()
        {
            if (disposed) return;
            itemLock.RequireHeld(item);
            disposed = true; List<Exception> failures = new List<Exception>();
            for (int i = leases.Count - 1; i >= 0; i--)
                try { leases[i].Dispose(); } catch (Exception error) { failures.Add(error); }
            try { itemLock.Dispose(); } catch (Exception error) { failures.Add(error); }
            if (failures.Count > 0) throw new AggregateException("Registry cleanup failed.", failures);
        }
    }
}
