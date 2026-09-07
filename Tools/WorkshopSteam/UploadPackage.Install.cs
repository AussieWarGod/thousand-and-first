using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ThousandAndFirst.WorkshopSteam
{
    public sealed partial class UploadPackage
    {
        [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode,
            ExactSpelling = true, SetLastError = true)]
        private static extern uint GetInstalledFinalPath(SafeFileHandle handle, StringBuilder path,
            uint pathLength, uint flags);

        /// <summary>Leases an exact installed copy using the same closed inventory validator.
        /// This proves bytes, not SDK subscription or transfer. The caller must keep this original
        /// package alive; disposing the returned evidence never closes the original's leases.</summary>
        public IInstalledDeliveryEvidence LeaseInstalled(string installedPath)
        {
            Require(Revalidate() && Request != null, "source package no longer valid");
            string target = Native(installedPath, false);
            string suffix = @"\steamapps\workshop\content\333640\"
                + Request.Item.ToString(CultureInfo.InvariantCulture);
            Require(target.EndsWith(suffix, StringComparison.OrdinalIgnoreCase), "installed Workshop item path mismatch");
            Require(!InstallOverlap(target, content), "source and installed content overlap");
            Require(!InstallOverlap(target, planFile.Path) && !InstallOverlap(target, receiptFile.Path),
                "installed content overlaps approved inputs");
            UploadPackage mirror = new UploadPackage();
            try
            {
                mirror.content = target;
                mirror.Request = Request;
                mirror.PlanSHA = PlanSHA;
                mirror.ReceiptSHA = ReceiptSHA;
                // Borrowed entries are never added to mirror.leases, so only the caller owns them.
                mirror.planFile = planFile;
                mirror.receiptFile = receiptFile;
                foreach (KeyValuePair<string, Entry> pair in entries)
                {
                    mirror.entries.Add(pair.Key, new Entry
                    {
                        Path = Path.Combine(target, pair.Key.Replace('/', '\\')),
                        Hash = pair.Value.Hash,
                        Size = pair.Value.Size
                    });
                }
                mirror.Scan(true);
                RequireInstallDistinct(mirror);
                Require(Revalidate() && mirror.Revalidate(), "installed acquisition changed");
                InstalledEvidence evidence = new InstalledEvidence(this, mirror, mirror.InstallInventorySHA());
                Require(evidence.Revalidate(), "installed evidence changed before return");
                return evidence;
            }
            catch (Exception error)
            {
                try { mirror.Dispose(); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }

        private static bool InstallOverlap(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
                || Inside(left, right) || Inside(right, left);
        }

        private void RequireInstallDistinct(UploadPackage mirror)
        {
            string source = InstallPhysicalPath(directories[content]);
            string target = InstallPhysicalPath(mirror.directories[mirror.content]);
            string plan = InstallPhysicalPath(planFile.Stream.SafeFileHandle);
            string receipt = InstallPhysicalPath(receiptFile.Stream.SafeFileHandle);
            Require(!InstallOverlap(target, source), "source and installed physical content overlap");
            Require(!InstallOverlap(target, plan) && !InstallOverlap(target, receipt),
                "installed physical content overlaps approved inputs");
        }

        private static string InstallPhysicalPath(SafeFileHandle handle)
        {
            const int maximum = 32768;
            const string prefix = @"\\?\Volume{";
            Require(handle != null && !handle.IsInvalid && !handle.IsClosed, "held input unavailable");
            StringBuilder buffer = new StringBuilder(maximum);
            // FILE_NAME_NORMALIZED (0) | VOLUME_NAME_GUID (1); never resolve an arbitrary pathname.
            uint length = GetInstalledFinalPath(handle, buffer, maximum, 1u);
            Require(length > 0 && length < maximum, "held input path resolution failed or exceeded bound");
            string path = buffer.ToString();
            Require(path.Length == length && path.Length > prefix.Length + 38
                && path.StartsWith(prefix, StringComparison.Ordinal)
                && path[prefix.Length + 36] == '}' && path[prefix.Length + 37] == '\\',
                "held input did not resolve to a canonical volume GUID path");
            string token = path.Substring(prefix.Length, 36);
            Guid volume;
            Require(Guid.TryParseExact(token, "D", out volume) && volume != Guid.Empty
                && token == volume.ToString("D"), "held input volume GUID is noncanonical");
            Name(path.Substring(prefix.Length + 38).Replace('\\', '/'));
            return path;
        }

        private sealed class InstalledEvidence : IInstalledDeliveryEvidence
        {
            private readonly UploadPackage original, mirror;
            private bool disposed, refused;
            public string InventorySHA { get; }

            internal InstalledEvidence(UploadPackage original, UploadPackage mirror, string inventorySHA)
            { this.original = original; this.mirror = mirror; InventorySHA = inventorySHA; }

            public bool Revalidate()
            {
                if (disposed || refused) return false;
                try
                {
                    if (original.Revalidate() && mirror.Revalidate())
                    {
                        original.RequireInstallDistinct(mirror);
                        return true;
                    }
                }
                catch (Exception) { refused = true; return false; }
                refused = true;
                return false;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                mirror.Dispose();
            }
        }
    }
}
