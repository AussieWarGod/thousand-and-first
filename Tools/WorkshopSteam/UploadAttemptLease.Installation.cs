using System;
using System.IO;
using ThousandAndFirst.WorkshopSteam.Evidence;

namespace ThousandAndFirst.WorkshopSteam
{
    public sealed partial class UploadAttemptLease
    {
        private bool installationAttempted;

        /// <summary>Retains an installation observation beside the exact held attempt and submission.
        /// This does not finalize an attempt, clear any fence or authorize another submission.
        /// The caller must obtain a fresh adapter result before constructing the observation.</summary>
        internal UploadAttemptLease CreateInstallation(ReleaseInstallationObservation value, UploadAttemptLease submission)
        {
            lock (gate)
            {
                Require(value != null && submission != null && !ReferenceEquals(this, submission)
                    && !disposed && !refused && !installationAttempted, "installation observation unavailable");
                Require(Path.GetFileName(path) == value.Item + ".active.attempt.json"
                    && submission.path == path + ".submission.json", "installation observation path mismatch");
                Require(RevalidateHeld() && submission.Revalidate(), "installation input lease changed");
                byte[] submitted = submission.ReadRetainedBytes();
                ReleaseSubmissionObservation original; string reason;
                Require(ReleaseSubmissionObservationCodec.TryDecode(submitted, out original, out reason),
                    "installation submission is not canonical");
                Require(value.AttemptSHA == ReleaseSubmissionObservationCodec.Sha256Hex(receipt)
                    && value.SubmissionSHA == ReleaseSubmissionObservationCodec.Sha256Hex(submitted)
                    && original.AttemptSHA == value.AttemptSHA && original.Item == value.Item
                    && original.RequestVersion == value.Version && original.PlanSHA == value.PlanSHA
                    && original.ReceiptSHA == value.ReceiptSHA
                    && string.CompareOrdinal(value.ObservedUtc, original.ObservedUtc) >= 0,
                    "installation observation identity mismatch");
                byte[] bytes = value.Encode(); ReleaseInstallationObservation parsed;
                Require(ReleaseInstallationObservation.TryDecode(bytes, out parsed), "installation observation shape");
                installationAttempted = true;
                UploadAttemptLease child = null;
                try
                {
                    child = Create(path + ".installation.json", bytes);
                    Require(RevalidateHeld() && submission.Revalidate() && child.Revalidate(),
                        "installation observation publication changed");
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
    }
}
