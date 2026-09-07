using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ThousandAndFirst.WorkshopSteam.Evidence;

namespace ThousandAndFirst.WorkshopSteam
{
    public sealed partial class WorkshopReleaseRegistry
    {
        private sealed class RetainedRelease
        {
            internal UploadAttemptLease.DirectoryLease Directory;
            internal UploadAttemptLease Attempt, Submission, Installation, Finalization;
            internal ReleaseSubmissionObservation Observation;
            internal string ContentPath, FinalizationSHA, InventorySHA;
            internal string[] Names;
        }
        private List<RetainedRelease> history;
        private List<string> historyNames;
        private bool historyPoisoned;

        internal void RequireFinalizedHistory()
        {
            ReadHistory();
            if (begun || history.Count > 0 && history[history.Count - 1].Finalization == null)
                throw new RetainedAttempt("Retained attempt requires fresh installation finalization, not retry.");
        }

        /// <summary>Names, but never creates, an attempt for a separately leased unseen package.
        /// Any unfinalized, partial, unknown, reordered or overbound retained history refuses.</summary>
        internal string PlannedAttemptPath(UploadPackage package)
        {
            RequireFinalizedHistory();
            if (begun || history.Count >= ReleaseFinalizationRules.MaxAttempts
                || package == null || package.Request == null || package.Request.Item != item
                || !package.Revalidate()
                || !ReleaseFinalizationRules.VersionAllowed(item.ToString(CultureInfo.InvariantCulture),
                    package.Request.Version, history.Count == 0 ? null
                    : history[history.Count - 1].Observation.RequestVersion))
                throw new RetainedAttempt("No exact finalized history authorizes this package version.");
            RequireUnseenInventory(package.VerifiedInventorySHA());
            RequireExact();
            return Path.Combine(attempts.Path, (history.Count + 1).ToString("D4", CultureInfo.InvariantCulture),
                item.ToString(CultureInfo.InvariantCulture) + ".active.attempt.json");
        }

        /// <summary>Creates one empty, durable next-attempt fence under the held item lock.
        /// Every older record remains leased and immutable. Nothing is retried or replaced.</summary>
        internal string BeginAttempt(UploadPackage package)
        {
            string path = PlannedAttemptPath(package);
            string name = Path.GetFileName(Path.GetDirectoryName(path));
            begun = true;
            try
            {
                Keep(attempts.CreateChild(name)); historyNames.Add(name);
                RequireExact(); return path;
            }
            catch { historyPoisoned = true; throw; }
        }



        private void ReadHistory()
        {
            RequireExact();
            if (history != null) return;
            List<RetainedRelease> read = new List<RetainedRelease>();
            List<string> names = Names(attempts.Path, ReleaseFinalizationRules.MaxAttempts);
            try
            {
                for (int index = 0; index < names.Count; index++)
                {
                    if (names[index] != (index + 1).ToString("D4", CultureInfo.InvariantCulture))
                        throw new RetainedAttempt("Retained attempt sequence is not contiguous and canonical.");
                    var row = new RetainedRelease { Directory = Keep(UploadAttemptLease.DirectoryLease.Open(Path.Combine(attempts.Path, names[index]))) };
                    List<string> files = Names(row.Directory.Path, 4);
                    bool finalized = SameNames(files, RecordNames(true, true));
                    if (!finalized && (index != names.Count - 1 || !SameNames(files, RecordNames(false, false))))
                        throw new RetainedAttempt("Retained empty, partial or unrecognized attempt requires reconciliation.");
                    row.Names = files.ToArray();
                    string path = Path.Combine(row.Directory.Path, item.ToString(CultureInfo.InvariantCulture) + ".active.attempt.json");
                    row.Attempt = KeepRecord(UploadAttemptLease.ReadExisting(path));
                    row.Submission = KeepRecord(UploadAttemptLease.ReadExisting(path + ".submission.json"));
                    if (!ReleaseFinalizationRules.TrySubmission(row.Attempt.ReadRetainedBytes(), row.Submission.ReadRetainedBytes(),
                        item.ToString(CultureInfo.InvariantCulture), out row.Observation, out row.ContentPath)
                        || !ReleaseFinalizationRules.VersionAllowed(item.ToString(CultureInfo.InvariantCulture), row.Observation.RequestVersion,
                            index == 0 ? null : read[index - 1].Observation.RequestVersion))
                        throw new RetainedAttempt("Retained submission is not an exact clean permitted-version completion.");
                    if (finalized)
                    {
                        row.Installation = KeepRecord(UploadAttemptLease.ReadExisting(path + ".installation.json"));
                        row.Finalization = KeepRecord(UploadAttemptLease.ReadExisting(path + ".finalization.json"));
                        byte[] finalization = row.Finalization.ReadRetainedBytes();
                        ReleaseInstallationObservation installed;
                        if (!ReleaseFinalizationRules.Finalized(row.Observation, row.Installation.ReadRetainedBytes(), finalization,
                            index + 1, index == 0 ? "" : read[index - 1].FinalizationSHA, Sha(markerBytes),
                            row.Directory.IdentityDigest(), out installed)
                            || !ReleaseFinalizationRules.UnseenInventory(installed.InventorySHA, Inventories(read)))
                            throw new RetainedAttempt("Retained finalization chain does not bind this registry and directory.");
                        row.InventorySHA = installed.InventorySHA;
                        row.FinalizationSHA = Sha(finalization);
                    }
                    read.Add(row);
                }
                history = read; historyNames = names; RequireExact();
            }
            catch { historyPoisoned = true; throw; }
        }

        private UploadAttemptLease KeepRecord(UploadAttemptLease lease) { leases.Add(lease); return lease; }

        private static List<string> Inventories(List<RetainedRelease> rows)
        {
            List<string> inventories = new List<string>();
            foreach (RetainedRelease row in rows)
                if (row.Finalization != null) inventories.Add(row.InventorySHA);
            return inventories;
        }

        private void RequireUnseenInventory(string inventory)
        {
            if (history == null || !ReleaseFinalizationRules.UnseenInventory(inventory, Inventories(history)))
                throw new RetainedAttempt("Package inventory already retained or unavailable; no content replay.");
        }

        private void RequireHistoryExact()
        {
            if (historyPoisoned) throw new RetainedAttempt("Retained history authority changed.");
            if (history == null) return;
            try
            {
                if (!SameNames(Names(attempts.Path, ReleaseFinalizationRules.MaxAttempts), historyNames))
                    throw new RetainedAttempt("Retained attempt entries changed.");
                foreach (RetainedRelease row in history)
                {
                    if (!row.Directory.Revalidate() || !SameNames(Names(row.Directory.Path, 4), row.Names)
                        || !row.Attempt.Revalidate() || !row.Submission.Revalidate()
                        || row.Installation != null && !row.Installation.Revalidate()
                        || row.Finalization != null && !row.Finalization.Revalidate())
                        throw new RetainedAttempt("Retained release evidence changed.");
                }
            }
            catch { historyPoisoned = true; throw; }
        }

        private string[] RecordNames(bool finalized, bool installed)
        {
            string name = item.ToString(CultureInfo.InvariantCulture) + ".active.attempt.json";
            List<string> names = new List<string> { name, name + ".submission.json" };
            if (installed) names.Add(name + ".installation.json");
            if (finalized) names.Add(name + ".finalization.json");
            names.Sort(StringComparer.Ordinal); return names.ToArray();
        }

        private static List<string> Names(string path, int maximum)
        {
            List<string> result = new List<string>();
            foreach (string entry in Directory.EnumerateFileSystemEntries(path))
            {
                if (result.Count >= maximum) throw new RetainedAttempt("Retained registry entry bounds.");
                result.Add(Path.GetFileName(entry));
            }
            result.Sort(StringComparer.Ordinal); return result;
        }

        private static bool SameNames(IList<string> left, IList<string> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++) if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
            return true;
        }
    }
}
