using System;
using System.IO;
using System.Globalization;
using System.Text.Json;
using ThousandAndFirst.WorkshopSteam.Evidence;

namespace ThousandAndFirst.WorkshopSteam
{
    public static class SteamVerify
    {
        private const string Note = "Verify one subscribed installation; no Workshop update is requested.";

        public static int Main(string[] args)
        {
            string operation = null, finalizationSHA = null;
            try
            {
                if (args == null || args.Length != 5 || (args[0] != "verify" && args[0] != "finalize"))
                    throw new ArgumentException("Expected verify|finalize PLAN PLAN_SHA ITEM RECEIPT_SHA.");
                operation = args[0];
                DeliveryResult result;
                ulong item;
                if (!ulong.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out item)
                    || args[3] != item.ToString(CultureInfo.InvariantCulture))
                    throw new ArgumentException("Expected canonical item identity.");
                using (WorkshopReleaseRegistry registry = WorkshopReleaseRegistry.Open(item))
                using (UploadPackage package = UploadPackage.Open(args[1], args[2], args[3], Note))
                {
                    if (!string.Equals(package.ReceiptSHA, args[4], StringComparison.Ordinal))
                        throw new InvalidDataException("Receipt approval mismatch.");
                    registry.RequireExact();
                    if (operation == "finalize")
                    {
                        result = registry.VerifyAndFinalize(package, out finalizationSHA);
                    }
                    else
                    {
                        result = SteamInstalledDelivery.Verify(package, path =>
                        {
                            registry.RequireExact();
                            return package.LeaseInstalled(path);
                        });
                    }
                    registry.RequireExact();
                }
                // Both package and registry cleanup precede any positive record.
                if (result == null) throw new InvalidDataException("Missing installation result.");
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    operation = operation,
                    status = result.Status,
                    reason = result.Reason,
                    item = result.Item,
                    version = result.Version,
                    planSHA = result.PlanSHA,
                    receiptSHA = result.ReceiptSHA,
                    installedPath = result.InstalledPath,
                    inventorySHA = result.InventorySHA,
                    // Verify does not inspect whether any prior attempt was finalized.
                    finalizationSHA = finalizationSHA,
                    attemptFinalized = operation == "finalize" && finalizationSHA != null,
                    scope = "one_subscribed_client_installation",
                    verifiedClientInstallations = result.SubscribedInstallationVerified ? 1 : 0,
                    subscribedInstallationVerified = result.SubscribedInstallationVerified,
                    freshTransferVerified = false,
                    releaseReady = false
                }));
                return result.SubscribedInstallationVerified ? 0 : 3;
            }
            catch (Exception error)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    operation = operation,
                    status = "Refused",
                    errorType = error.GetType().Name,
                    finalizationSHA = finalizationSHA,
                    attemptFinalized = operation == "finalize" && finalizationSHA != null,
                    scope = "one_subscribed_client_installation",
                    verifiedClientInstallations = 0,
                    subscribedInstallationVerified = false,
                    freshTransferVerified = false,
                    releaseReady = false
                }));
                return 2;
            }
        }
    }

    public sealed partial class WorkshopReleaseRegistry
    {
        /// <summary>Only native finalization creation route: selects the latest exact retained
        /// attempt, invokes actual installed verification, waits for all SDK cleanup, then retains
        /// immutable installation and finalization records. A DeliveryResult argument is not accepted.
        /// No attempt means observation only, preserving standalone first-release verification.</summary>
        internal DeliveryResult VerifyAndFinalize(UploadPackage package, out string finalizationSHA)
        {
            finalizationSHA = null;
            ReadHistory();
            if (begun || package == null || package.Request == null || package.Request.Item != item || !package.Revalidate())
                throw new RetainedAttempt("Verification package or retained owner changed.");
            RetainedRelease latest = history.Count == 0 ? null : history[history.Count - 1];
            if (latest != null && (latest.Observation.RequestVersion != package.Request.Version
                || latest.Observation.PlanSHA != package.PlanSHA || latest.Observation.ReceiptSHA != package.ReceiptSHA
                || latest.ContentPath != package.Request.ContentPath))
                throw new RetainedAttempt("Verification does not name the latest exact retained package.");
            RequireExact();
            DeliveryResult result = SteamInstalledDelivery.Verify(package, path =>
            {
                RequireExact();
                return package.LeaseInstalled(path);
            });
            // Verify returns only after callback, installation and Steam API cleanup have finished.
            RequireExact();
            if (!package.Revalidate()) throw new RetainedAttempt("Verification package changed.");
            if (latest == null || result == null || !result.SubscribedInstallationVerified) return result;
            if (latest.Finalization != null) { finalizationSHA = latest.FinalizationSHA; return result; }
            ReleaseInstallationObservation observed;
            if (!ReleaseInstallationObservation.TryCapture(latest.Observation, result, Sha(markerBytes),
                latest.Directory.IdentityDigest(), DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), out observed)
                || !ReleaseFinalizationRules.Binds(latest.Observation, observed, Sha(markerBytes), latest.Directory.IdentityDigest()))
                throw new RetainedAttempt("Fresh installation does not bind retained submission.");
            byte[] installationBytes = observed.Encode();
            if (observed.InventorySHA != package.VerifiedInventorySHA())
                throw new RetainedAttempt("Installed inventory differs from the exact source package.");
            RequireUnseenInventory(observed.InventorySHA);
            byte[] finalization = ReleaseFinalizationRules.FinalizationBytes(history.Count,
                history.Count == 1 ? "" : history[history.Count - 2].FinalizationSHA, Sha(installationBytes));
            RequireExact();
            if (!package.Revalidate()) throw new RetainedAttempt("Package changed before finalization.");
            string attemptPath = Path.Combine(latest.Directory.Path, latest.Names[0]);
            try
            {
                latest.Installation = KeepRecord(latest.Attempt.CreateInstallation(observed, latest.Submission));
                latest.Names = RecordNames(false, true);
                RequireExact();
                if (!package.Revalidate()) throw new RetainedAttempt("Package changed after installation observation.");
                latest.Finalization = KeepRecord(UploadAttemptLease.Create(attemptPath + ".finalization.json", finalization));
                latest.Names = RecordNames(true, true); latest.FinalizationSHA = Sha(finalization);
                latest.InventorySHA = observed.InventorySHA;
                RequireExact(); finalizationSHA = latest.FinalizationSHA; return result;
            }
            catch { historyPoisoned = true; throw; }
        }
    }

    internal sealed partial class ReleaseInstallationObservation
    {
        /// <summary>Captures the actual adapter result only when all package/submission identities
        /// agree. Registry hashes must subsequently be checked against their still-held leases.
        /// Unknown callback outcome is retained by its own hash; it is not changed into success.</summary>
        internal static bool TryCapture(ReleaseSubmissionObservation submission, DeliveryResult installation,
            string markerSHA, string directorySHA, string observedUtc, out ReleaseInstallationObservation result)
        {
            result = null;
            if (submission == null || installation == null || !installation.SubscribedInstallationVerified
                || installation.Status != "SubscribedInstallationVerified" || installation.Reason != null
                || installation.FreshTransferVerified || installation.ReleaseReady
                || installation.Item.ToString(CultureInfo.InvariantCulture) != submission.Item
                || installation.Version != submission.RequestVersion || installation.PlanSHA != submission.PlanSHA
                || installation.ReceiptSHA != submission.ReceiptSHA
                || string.CompareOrdinal(observedUtc, submission.ObservedUtc) < 0) return false;
            byte[] original = ReleaseSubmissionObservationCodec.EncodeUtf8(submission);
            string[] values = { Schema, submission.AttemptSHA,
                ReleaseSubmissionObservationCodec.Sha256Hex(original), submission.Item, submission.RequestVersion,
                submission.PlanSHA, submission.ReceiptSHA, markerSHA, directorySHA, installation.InventorySHA,
                observedUtc, Scope };
            if (!Valid(values)) return false;
            result = new ReleaseInstallationObservation(values);
            return true;
        }
    }
}
