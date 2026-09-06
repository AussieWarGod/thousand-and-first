// SteamUploadPort - the Steamworks.NET adapter behind the SDK-free IWorkshopUploadPort. It leases
// one UploadPackage, proves readiness and fresh remote authority, maps every protocol operation
// onto one checked SDK call, holds a durable attempt-receipt lease it re-proves at the submit call,
// waits once (bounded) for the callresult, then re-reads the published metadata. No create, delete,
// subscribe, download or console write. Built by root's csproj; not an entrypoint.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Steamworks;

namespace ThousandAndFirst.WorkshopSteam
{
    public sealed partial class SteamUploadPort : IWorkshopUploadPort, IWorkshopSubmissionEvidencePort, IDisposable
    {
        private const uint AppId = SteamUploadAuthority.AppId;
        private const int AuthorityTimeoutMs = 15000, CompletionTimeoutMs = 120000, PumpSleepMs = 25, MaxNotesChars = 4096, MaxRefusalChars = 200;
        private readonly UploadPackage package;
        private readonly UploadRequest leased;
        private readonly string attemptPath;
        private ulong owner;
        private UploadAttemptLease lease;
        private bool initialized, prepared, attemptPoisoned, submitBound, issued, waited, disposed;
        private bool removedId, addedId, removedVersion, addedVersion;
        private UGCUpdateHandle_t update = UGCUpdateHandle_t.Invalid;
        private CallResult<SubmitItemUpdateResult_t> submitCall;
        private UploadCompletion outcome = UploadCompletion.Unknown;
        private string notes;
        public SteamUploadPort(UploadPackage package, string attemptPath)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (package.Request == null) throw new ArgumentException("package carries no request", nameof(package));
            if (string.IsNullOrEmpty(attemptPath) || !Path.IsPathFullyQualified(attemptPath))
                throw new ArgumentException("attempt path must be fully qualified", nameof(attemptPath));
            this.package = package;
            this.attemptPath = attemptPath;
            leased = package.Request;
        }
        public string LastDiagnostics { get { return notes ?? string.Empty; } }
        private bool Started { get { return update != UGCUpdateHandle_t.Invalid; } }
        public bool Try(UploadOperation operation, UploadRequest request,
            string metadataKey = null, string metadataValue = null)
        {
            if (disposed) return Refuse("port_disposed");
            if (request == null || !ReferenceEquals(request, leased)) return Refuse("request_not_leased");
            switch (operation)
            {
                case UploadOperation.Prepare: return Prepare();
                case UploadOperation.Revalidate: return Revalidate();
                case UploadOperation.StartUpdate: return StartUpdate();
                case UploadOperation.SetTitle: case UploadOperation.SetDescription: case UploadOperation.SetTags:
                case UploadOperation.SetVisibility: case UploadOperation.SetContent: case UploadOperation.SetPreview:
                    return Setter(operation);
                case UploadOperation.RemoveManifestId: case UploadOperation.AddManifestId:
                case UploadOperation.RemoveManifestVersion: case UploadOperation.AddManifestVersion:
                    return KeyValue(operation, metadataKey, metadataValue);
                case UploadOperation.RecordAttempt: return RecordAttempt();
                case UploadOperation.Submit: return Submit();
                default: return Refuse("operation_not_supported");
            }
        }

        private bool Prepare()
        {
            if (prepared) return Refuse("prepare_repeated");
            if (!SteamAPI.Init()) return Refuse("steam_not_initialized");
            initialized = true;
            if (SteamUtils.GetAppID().m_AppId != AppId) return Refuse("app_mismatch");
            if (!SteamUser.BLoggedOn()) return Refuse("not_logged_on");
            if (!SteamApps.BIsSubscribedApp(new AppId_t(AppId))) return Refuse("no_license");
            // The authenticated account is held for this process only and never written anywhere.
            owner = SteamUser.GetSteamID().m_SteamID;
            if (owner == 0UL) return Refuse("steam_id_unavailable");
            if (!package.Revalidate()) return Refuse("prepare_package_changed");
            if (!Authority("prepare")) return false;
            prepared = true;
            return true;
        }
        private bool Revalidate()
        {
            if (!prepared) return Refuse("revalidate_before_prepare");
            if (!SteamUser.BLoggedOn()) return Refuse("revalidate_not_logged_on");
            if (SteamUtils.GetAppID().m_AppId != AppId) return Refuse("revalidate_app_mismatch");
            if (SteamUser.GetSteamID().m_SteamID != owner) return Refuse("revalidate_owner_changed");
            if (!package.Revalidate()) return Refuse("revalidate_package_changed");
            return Authority("revalidate");
        }

        /// <summary>New uncached query, then context re-proved: neither may authorise a write alone.</summary>
        private bool Authority(string stage)
        {
            AuthorityFacts facts;
            string refusal;
            bool queried = SteamUploadAuthority.TryQuery(leased.Item, AuthorityTimeoutMs, out facts, out refusal);
            Note(SteamUploadAuthority.DrainCleanupNotes());
            if (!queried) return Refuse(stage + "_" + refusal);
            if (!SteamUploadAuthority.ContextStillExact(owner)) return Refuse(stage + "_context_changed");
            Note(stage + "_remote_time_updated_" + facts.TimeUpdated.ToString(CultureInfo.InvariantCulture));
            if (!SteamUploadAuthority.AcceptsLane(leased, facts, out refusal)) return Refuse(stage + "_" + refusal);
            return true;
        }
        private bool StartUpdate()
        {
            if (!prepared) return Refuse("start_update_before_prepare");
            if (Started) return Refuse("start_update_repeated");
            update = SteamUGC.StartItemUpdate(new AppId_t(AppId), new PublishedFileId_t(leased.Item));
            return Started || Refuse("start_update_invalid_handle");
        }

        private bool Setter(UploadOperation operation)
        {
            if (!Started) return Refuse("setter_before_start_update");
            bool ok;
            string name;
            switch (operation)
            {
                case UploadOperation.SetTitle: name = "SetItemTitle"; ok = SteamUGC.SetItemTitle(update, leased.Title); break;
                case UploadOperation.SetDescription: name = "SetItemDescription";
                    ok = SteamUGC.SetItemDescription(update, leased.Description); break;
                case UploadOperation.SetTags: name = "SetItemTags"; ok = SteamUGC.SetItemTags(update, leased.Tags); break;
                case UploadOperation.SetVisibility: name = "SetItemVisibility";
                    ok = SteamUGC.SetItemVisibility(update, (ERemoteStoragePublishedFileVisibility)leased.SteamVisibility); break;
                case UploadOperation.SetContent: name = "SetItemContent"; ok = SteamUGC.SetItemContent(update, leased.ContentPath); break;
                case UploadOperation.SetPreview: name = "SetItemPreview"; ok = SteamUGC.SetItemPreview(update, leased.PreviewPath); break;
                default: return Refuse("setter_not_supported");
            }
            return ok || Refuse("setter_failed_" + name);
        }

        /// <summary>Each manifest key is cleared once and written once; no predecessor survives.</summary>
        private bool KeyValue(UploadOperation operation, string key, string value)
        {
            if (!Started) return Refuse("key_value_before_start_update");
            bool identity = operation == UploadOperation.RemoveManifestId || operation == UploadOperation.AddManifestId;
            bool add = operation == UploadOperation.AddManifestId || operation == UploadOperation.AddManifestVersion;
            string expectedKey = identity ? SteamUploadAuthority.ManifestIdKey : SteamUploadAuthority.ManifestVersionKey;
            string expectedValue = identity ? leased.ManifestId : leased.Version;
            if (!string.Equals(key, expectedKey, StringComparison.Ordinal)) return Refuse("key_value_key_unexpected");
            if (!add)
            {
                if (value != null) return Refuse("key_value_remove_carries_value");
                if (identity ? removedId : removedVersion) return Refuse("key_value_remove_repeated_" + expectedKey);
                if (!SteamUGC.RemoveItemKeyValueTags(update, expectedKey)) return Refuse("key_value_remove_failed_" + expectedKey);
                if (identity) removedId = true; else removedVersion = true;
                return true;
            }
            if (!string.Equals(value, expectedValue, StringComparison.Ordinal)) return Refuse("key_value_value_unexpected");
            if (!(identity ? removedId : removedVersion)) return Refuse("key_value_add_before_remove_" + expectedKey);
            if (identity ? addedId : addedVersion) return Refuse("key_value_add_repeated_" + expectedKey);
            if (!SteamUGC.AddItemKeyValueTag(update, expectedKey, expectedValue)) return Refuse("key_value_add_failed_" + expectedKey);
            if (identity) addedId = true; else addedVersion = true;
            return true;
        }

        private bool Submit()
        {
            if (!Started) return Refuse("submit_before_start_update");
            if (attemptPoisoned) return Refuse("submit_poisoned");
            if (lease == null) return Refuse("submit_before_record");
            if (submitBound) return Refuse("submit_repeated");
            // Bound before the call: a throw here must never leave a second submission possible.
            submitBound = true;
            submitCall = CallResult<SubmitItemUpdateResult_t>.Create(OnSubmitCompleted);
            // Nothing may sit between this proof and the call it authorises.
            if (!lease.Revalidate()) { attemptPoisoned = true; return Refuse("submit_lease_invalid"); }
            SteamAPICall_t call = SteamUGC.SubmitItemUpdate(update, leased.ChangeNote);
            if (call == SteamAPICall_t.Invalid) return Refuse("submit_call_invalid");
            submitCall.Set(call);
            issued = true;
            return true;
        }
        private void OnSubmitCompleted(SubmitItemUpdateResult_t param, bool bIOFailure)
        {
            string refusal;
            if (submissionRecord == null) { Note("callback_without_record"); return; }
            if (!submissionRecord.TryRecordCallback((int)param.m_eResult, param.m_nPublishedFileId.m_PublishedFileId,
                bIOFailure, param.m_bUserNeedsToAcceptWorkshopLegalAgreement, out refusal))
                Note("callback_" + refusal);
        }

        public UploadCompletion WaitForCompletion()
        {
            if (waited) return outcome;
            waited = true;
            if (disposed) return Settle(UploadCompletion.Unknown, "await_after_dispose");
            if (!issued) return Settle(UploadCompletion.Unknown, "await_without_submission");
            try
            {
                Stopwatch pump = Stopwatch.StartNew();
                while (!submissionRecord.CallbackObserved && pump.ElapsedMilliseconds < CompletionTimeoutMs)
                {
                    SteamAPI.RunCallbacks();
                    Thread.Sleep(PumpSleepMs);
                }
            }
            catch (Exception e) { return Settle(UploadCompletion.Unknown, "await_exception_" + e.GetType().FullName); }
            if (!submissionRecord.CallbackObserved) return Settle(UploadCompletion.TimedOut, "await_timeout");
            UploadCompletion result = UploadCallbackRules.Classify(leased.Item, submissionRecord.RawEResult.Value,
                submissionRecord.ReturnedItem.Value, submissionRecord.IoFailure.Value, submissionRecord.LegalAgreementRequired.Value);
            string reason = result == UploadCompletion.LegalAgreementRequired ? "await_legal_agreement"
                : result == UploadCompletion.IoFailure ? "await_io_failure"
                : result == UploadCompletion.Rejected ? "await_result_" + submissionRecord.RawEResult.Value.ToString(CultureInfo.InvariantCulture)
                : result == UploadCompletion.Unknown ? "await_returned_item_mismatch" : "await_ok";
            return Settle(result, reason);
        }

        /// <summary>Read-only proof the item now publishes exactly this request's owner, lane, manifest,
        /// prose and tags. Preview transfer and subscriber bytes stay unverified; never "delivered".</summary>
        public bool VerifyPublishedMetadata()
        {
            if (disposed) return Refuse("verify_after_dispose");
            if (outcome != UploadCompletion.Ok) return Refuse("verify_without_completed_submission");
            if (submissionRecord == null || submissionRecord.Settlement == null
                || submissionRecord.Settlement.ReturnedItem != leased.Item.ToString(CultureInfo.InvariantCulture))
                return Refuse("verify_completion_item_mismatch");
            AuthorityFacts facts;
            string refusal;
            bool queried = SteamUploadAuthority.TryQuery(leased.Item, AuthorityTimeoutMs, out facts, out refusal);
            Note(SteamUploadAuthority.DrainCleanupNotes());
            if (!queried) return Refuse("verify_" + refusal);
            if (!SteamUploadAuthority.ContextStillExact(owner)) return Refuse("verify_context_changed");
            if (facts.Cached) return Refuse("verify_cached_facts");
            if (facts.TagsTruncated) return Refuse("verify_tags_truncated");
            if (!facts.OwnerMatches) return Refuse("verify_owner_mismatch");
            if ((int)facts.Visibility != leased.SteamVisibility) return Refuse("verify_visibility_mismatch");
            if (!string.Equals(facts.ManifestId, leased.ManifestId, StringComparison.Ordinal)) return Refuse("verify_manifest_id_mismatch");
            if (!string.Equals(facts.ManifestVersion, leased.Version, StringComparison.Ordinal)) return Refuse("verify_manifest_version_mismatch");
            if (!string.Equals(facts.Title, leased.Title, StringComparison.Ordinal)) return Refuse("verify_title_mismatch");
            if (!string.Equals(facts.Description, leased.Description, StringComparison.Ordinal)) return Refuse("verify_description_mismatch");
            if (!SteamUploadAuthority.SameTagSet(leased.Tags, facts.Tags)) return Refuse("verify_tag_set_mismatch");
            if (!SteamUploadAuthority.ContextStillExact(owner)) return Refuse("verify_context_changed_final");
            return true;
        }

        /// <summary>Guarded, idempotent teardown. Cancelling cannot recall a submission Steam holds.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            UploadCleanup.Run(
                delegate { if (submitCall != null) submitCall.Cancel(); },
                delegate { try { if (submitCall != null) submitCall.Dispose(); } finally { submitCall = null; } },
                DisposeObservation,
                delegate { try { if (lease != null) lease.Dispose(); } finally { lease = null; } },
                delegate { try { if (initialized) SteamAPI.Shutdown(); } finally { initialized = false; } });
        }
        private bool Refuse(string reason) { Note(reason); return false; }
        private static string Bound(string t) { return t == null ? string.Empty : t.Length <= MaxRefusalChars ? t : t.Substring(0, MaxRefusalChars); }
        private void Note(string line)
        {
            if (line == null) return;
            string next = notes == null ? line : notes + Environment.NewLine + line;
            notes = next.Length > MaxNotesChars ? next.Substring(0, MaxNotesChars) + "[truncated]" : next;
        }
    }
}
