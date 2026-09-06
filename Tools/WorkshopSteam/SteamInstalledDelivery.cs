using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Steamworks;

namespace ThousandAndFirst.WorkshopSteam
{
    public interface IInstalledDeliveryEvidence : IDisposable
    {
        string InventorySHA { get; }
        bool Revalidate();
    }

    public sealed class DeliveryResult
    {
        public string Status { get; }
        public string Reason { get; }
        public ulong Item { get; }
        public string Version { get; }
        public string PlanSHA { get; }
        public string ReceiptSHA { get; }
        public string InstalledPath { get; }
        public string InventorySHA { get; }
        public bool SubscribedInstallationVerified { get; }
        public bool FreshTransferVerified { get { return false; } }
        public bool ReleaseReady { get { return false; } }

        internal DeliveryResult(UploadRequest request, string plan, string receipt, string path,
            string inventory, string reason)
        {
            SubscribedInstallationVerified = reason == null;
            Status = reason == null ? "SubscribedInstallationVerified" : "Refused";
            Reason = reason;
            Item = request == null ? 0UL : request.Item;
            Version = request == null ? null : request.Version;
            PlanSHA = plan; ReceiptSHA = receipt; InstalledPath = path; InventorySHA = inventory;
        }
    }

    public static class SteamInstalledDelivery
    {
        private const uint AppId = 333640u;
        private const ulong PrivateItem = 3796495680UL;
        private const uint Subscribed = 1u, Legacy = 2u, Installed = 4u, KnownStates = 63u;
        private static int running;

        private sealed class Refusal : Exception
        {
            internal readonly string Code;
            internal Refusal(string code) { Code = code; }
        }

        private sealed class Pending
        {
            internal readonly ulong Item;
            internal bool Completed, Conflicting;
            internal EResult Result;
            internal Pending(ulong item) { Item = item; }
            internal void Observe(DownloadItemResult_t result)
            {
                if (result.m_unAppID.m_AppId != AppId || result.m_nPublishedFileId.m_PublishedFileId != Item) return;
                if (Completed) { if (Result != result.m_eResult) Conflicting = true; return; }
                Result = result.m_eResult;
                Completed = true;
            }
        }

        private sealed class Installation
        {
            internal readonly string Path;
            internal readonly ulong Size;
            internal readonly uint Timestamp;
            internal Installation(string path, ulong size, uint timestamp)
            { Path = path; Size = size; Timestamp = timestamp; }
        }

        /// <summary>Standalone-process SDK owner; do not run beside another SDK adapter.
        /// The caller retains its package. acquire must lease and verify the complete SDK-returned
        /// installation against that package's exact closed receipt, not merely hash arbitrary files.
        /// DownloadItem may reuse cached bytes. GetItemInstallInfo marks the item UsedOrPlayed.
        /// Timestamps and callbacks do not establish a content-manifest revision or fresh transfer.</summary>
        public static DeliveryResult Verify(UploadPackage package,
            Func<string, IInstalledDeliveryEvidence> acquire, int timeoutMs = 120000)
        {
            UploadRequest request = package == null ? null : package.Request;
            string plan = package == null ? null : package.PlanSHA;
            string receipt = package == null ? null : package.ReceiptSHA;
            string path = null, inventory = null, refusal = null;
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
                return new DeliveryResult(request, plan, receipt, null, null, "sdk_adapter_busy");
            bool initialized = false;
            Callback<DownloadItemResult_t> callback = null;
            IInstalledDeliveryEvidence installed = null;
            try
            {
                Require(package != null && request != null && acquire != null, "missing_input");
                Require(timeoutMs >= 1 && timeoutMs <= 120000, "timeout_out_of_bounds");
                Require((request.Item == PrivateItem && request.SteamVisibility == 2)
                    || (request.Item == SteamUploadAuthority.PublicAlphaItem && request.SteamVisibility == 0), "wrong_item_lane");
                Require(Hash(plan) && Hash(receipt) && package.Revalidate(), "package_invalid");
                Require(string.IsNullOrEmpty(SteamUploadAuthority.DrainCleanupNotes()), "prior_authority_cleanup_failed");
                initialized = SteamAPI.Init();
                Require(initialized, "steam_init_refused");
                ulong owner = SteamUser.GetSteamID().m_SteamID;
                Prove(package, request, owner);
                Stopwatch budget = Stopwatch.StartNew();
                AuthorityFacts before = Query(package, request, owner, budget, timeoutMs);
                State(request.Item, false);
                Pending pending = new Pending(request.Item);
                callback = Callback<DownloadItemResult_t>.Create(pending.Observe);
                Prove(package, request, owner);
                State(request.Item, false);
                Require(SteamUGC.DownloadItem(new PublishedFileId_t(request.Item), false), "download_not_started");
                while (!pending.Completed && budget.ElapsedMilliseconds < timeoutMs)
                {
                    ProveContext(owner);
                    SteamAPI.RunCallbacks();
                    if (!pending.Completed) Thread.Sleep(25);
                }
                Require(pending.Completed, "download_timeout");
                Require(!pending.Conflicting && pending.Result == EResult.k_EResultOK, "download_result_refused");
                Prove(package, request, owner);
                State(request.Item, true);
                Installation first = Locate(request.Item);
                path = first.Path;
                Prove(package, request, owner);
                State(request.Item, true);
                installed = acquire(path);
                Require(installed != null, "installation_not_leased");
                inventory = installed.InventorySHA;
                Require(Hash(inventory) && installed.Revalidate(), "installed_inventory_invalid");
                AuthorityFacts after = Query(package, request, owner, budget, timeoutMs);
                Require(before.TimeUpdated == after.TimeUpdated, "remote_timestamp_changed");
                Prove(package, request, owner);
                State(request.Item, true);
                Installation second = Locate(request.Item);
                Require(string.Equals(first.Path, second.Path, StringComparison.Ordinal)
                    && first.Size == second.Size && first.Timestamp == second.Timestamp, "installation_identity_changed");
                Require(installed.Revalidate() && string.Equals(inventory, installed.InventorySHA,
                    StringComparison.Ordinal), "installed_inventory_changed");
                Prove(package, request, owner);
                State(request.Item, true);
                Require(!pending.Conflicting, "download_results_conflict");
            }
            catch (Refusal error) { refusal = error.Code; }
            catch (Exception error) { refusal = "exception_" + error.GetType().Name; }
            finally
            {
                try { if (callback != null) callback.Dispose(); }
                catch (Exception error) { CleanupFailure(ref refusal, "callback", error); }
                try { if (installed != null) installed.Dispose(); }
                catch (Exception error) { CleanupFailure(ref refusal, "installed", error); }
                try
                {
                    if (!string.IsNullOrEmpty(SteamUploadAuthority.DrainCleanupNotes()))
                        refusal = Add(refusal, "authority_cleanup_failed");
                }
                catch (Exception error) { CleanupFailure(ref refusal, "authority", error); }
                try { if (initialized) SteamAPI.Shutdown(); }
                catch (Exception error) { CleanupFailure(ref refusal, "sdk", error); }
                finally { Interlocked.Exchange(ref running, 0); }
            }
            return new DeliveryResult(request, plan, receipt, path, inventory, refusal);
        }

        private static AuthorityFacts Query(UploadPackage package, UploadRequest request,
            ulong owner, Stopwatch budget, int timeoutMs)
        {
            Prove(package, request, owner);
            long remaining = timeoutMs - budget.ElapsedMilliseconds;
            Require(remaining > 0, "verification_timeout");
            AuthorityFacts facts;
            string reason;
            bool queried = SteamUploadAuthority.TryQuery(request.Item, (int)Math.Min(30000L, remaining), out facts, out reason);
            Require(string.IsNullOrEmpty(SteamUploadAuthority.DrainCleanupNotes()), "authority_cleanup_failed");
            Require(queried && facts != null, "authority_query_refused");
            Prove(package, request, owner);
            Require(!facts.Cached && !facts.TagsTruncated && facts.OwnerMatches, "authority_facts_refused");
            // Delivery requires equality; upload's AcceptsLane intentionally requires an older public version.
            Require((int)facts.Visibility == request.SteamVisibility
                && string.Equals(facts.ManifestId, request.ManifestId, StringComparison.Ordinal)
                && string.Equals(facts.ManifestVersion, request.Version, StringComparison.Ordinal)
                && string.Equals(facts.Title, request.Title, StringComparison.Ordinal)
                && string.Equals(facts.Description, request.Description, StringComparison.Ordinal)
                && SteamUploadAuthority.SameTagSet(request.Tags, facts.Tags), "published_metadata_mismatch");
            return facts;
        }

        private static void Prove(UploadPackage package, UploadRequest request, ulong owner)
        {
            ProveContext(owner);
            Require(ReferenceEquals(package.Request, request) && package.Revalidate(), "package_changed");
            ProveContext(owner);
        }

        private static void ProveContext(ulong owner)
        {
            Require(owner != 0UL && SteamUploadAuthority.ContextStillExact(owner)
                && SteamApps.BIsSubscribedApp(new AppId_t(AppId)), "client_context_changed");
        }

        private static void State(ulong item, bool settled)
        {
            uint state = SteamUGC.GetItemState(new PublishedFileId_t(item));
            Require((state & ~KnownStates) == 0 && (state & Subscribed) != 0
                && (state & Legacy) == 0, "item_not_subscribed_or_unknown_state");
            Require(!settled || state == (Subscribed | Installed), "installation_not_settled");
        }

        private static Installation Locate(ulong item)
        {
            ulong size;
            uint timestamp;
            string path;
            Require(SteamUGC.GetItemInstallInfo(new PublishedFileId_t(item), out size, out path,
                32768u, out timestamp), "install_info_refused");
            Require(!string.IsNullOrEmpty(path) && path.Length < 32768
                && Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal), "install_path_invalid");
            return new Installation(path, size, timestamp);
        }

        private static bool Hash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }

        private static void Require(bool condition, string reason)
        { if (!condition) throw new Refusal(reason); }

        private static void CleanupFailure(ref string reason, string owner, Exception error)
        { reason = Add(reason, owner + "_cleanup_" + error.GetType().Name); }

        private static string Add(string before, string next)
        { return before == null ? next : before + ";" + next; }
    }
}
