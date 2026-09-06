using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThousandAndFirst.WorkshopSteam.Evidence;

namespace ThousandAndFirst.WorkshopSteam
{
    public sealed partial class SteamUploadPort
    {
        private UploadSubmissionRecord submissionRecord;
        private UploadAttemptLease observationLease;
        private string observationSHA;
        private bool observationWriteStarted;

        public sealed class Attempt
        {
            [JsonPropertyName("attemptId")] public string AttemptId { get; set; } [JsonPropertyName("item")] public string Item { get; set; }
            [JsonPropertyName("manifestId")] public string ManifestId { get; set; } [JsonPropertyName("requestVersion")] public string RequestVersion { get; set; }
            [JsonPropertyName("steamVisibility")] public int SteamVisibility { get; set; } [JsonPropertyName("planSHA")] public string PlanSHA { get; set; }
            [JsonPropertyName("receiptSHA")] public string ReceiptSHA { get; set; } [JsonPropertyName("packagePath")] public string PackagePath { get; set; }
            [JsonPropertyName("contentPath")] public string ContentPath { get; set; } [JsonPropertyName("startUtc")] public string StartUtc { get; set; }
        }

        private bool RecordAttempt()
        {
            if (!Started) return Refuse("record_before_start_update");
            if (!addedId || !addedVersion) return Refuse("record_before_manifest_tags");
            if (attemptPoisoned) return Refuse("record_poisoned");
            if (lease != null) return Refuse("record_repeated");
            Attempt attempt = new Attempt
            {
                AttemptId = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
                Item = leased.Item.ToString(CultureInfo.InvariantCulture), ManifestId = leased.ManifestId,
                RequestVersion = leased.Version, SteamVisibility = leased.SteamVisibility,
                PlanSHA = package.PlanSHA, ReceiptSHA = package.ReceiptSHA,
                // Historical v1 records retain packagePath == contentPath.
                PackagePath = leased.ContentPath, ContentPath = leased.ContentPath,
                StartUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
            byte[] receipt = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(attempt));
            UploadSubmissionRecord record;
            string refusal;
            if (!UploadSubmissionRecord.TryCreate(ReleaseSubmissionObservationCodec.Sha256Hex(receipt), null,
                leased.Item, leased.Version, package.PlanSHA, package.ReceiptSHA, attempt.StartUtc, out record, out refusal))
            { attemptPoisoned = true; return Refuse("record_identity_" + refusal); }
            try
            {
                lease = UploadAttemptLease.Create(attemptPath, receipt);
                submissionRecord = record;
            }
            catch (Exception error)
            {
                attemptPoisoned = true;
                return Refuse("record_attempt_failed " + error.GetType().FullName + ": " + Bound(error.Message));
            }
            return true;
        }

        private UploadCompletion Settle(UploadCompletion value, string note)
        {
            if (submissionRecord == null) outcome = value;
            else
            {
                ReleaseSubmissionObservation observation;
                string refusal;
                if (!submissionRecord.TrySettle(value, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    note, out observation, out refusal)) throw new InvalidDataException("submission settlement refused: " + refusal);
                outcome = submissionRecord.Completion.Value;
            }
            Note(note);
            return outcome;
        }

        /// <summary>Persists the frozen callback/outcome before later metadata work or disposal.
        /// Null means no complete attempt was recorded. Any write failure retains its fence;
        /// repeated calls can only re-prove the same held record, never create another.</summary>
        public string PersistSubmissionObservation()
        {
            if (disposed) throw new InvalidOperationException("observation after disposal");
            if (submissionRecord == null)
            {
                if (lease != null || submitBound) throw new InvalidDataException("submission record missing");
                return null;
            }
            if (!submissionRecord.Settled) Settle(UploadCompletion.Unknown, "protocol_without_completion");
            if (observationWriteStarted)
            {
                if (observationLease == null || observationSHA == null || !lease.Revalidate() || !observationLease.Revalidate())
                    throw new InvalidDataException("observation write uncertain or changed");
                return observationSHA;
            }
            observationWriteStarted = true;
            observationLease = lease.CreateObservation(submissionRecord.Settlement);
            observationSHA = submissionRecord.SettlementSha();
            return observationSHA;
        }

        private void DisposeObservation()
        {
            try { if (observationLease != null) observationLease.Dispose(); }
            finally { observationLease = null; }
        }
    }
}
